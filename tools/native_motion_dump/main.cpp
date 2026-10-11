// Headless Cubism Native motion dump.
// Loads Hiyori via CubismNativeFramework + Core (no renderer) and writes
// per-frame parameter / part-opacity CSVs at dt=1/60.
//
// Clock matches Unity / the Godot demo: Play at t=0, then sample after each dt.
// Native assigns StartTime on the first UpdateMotion; we prime with dt=0 so the
// first dumped frame is t=1/60, same as Godot MotionController.Update(dt).
// Loop fade-in is disabled (Unity CubismFadeController fades in once).
//
// PartOpacity: official CubismMotion uses GetParameterIndex (part ids miss).
// The harness compiles a patched copy that writes GetPartIndex / SetPartOpacity
// so the CSV matches Unity AnimationClip bindings. Loop wrap is still Native V2.

#include <CubismFramework.hpp>
#include <CubismModelSettingJson.hpp>
#include <ICubismAllocator.hpp>
#include <Id/CubismIdManager.hpp>
#include <Model/CubismMoc.hpp>
#include <Model/CubismModel.hpp>
#include <Motion/CubismMotion.hpp>
#include <Motion/CubismMotionManager.hpp>
#include <Utils/CubismString.hpp>

#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <iomanip>
#include <string>
#include <vector>

using namespace Live2D::Cubism::Framework;

namespace {

class Allocator : public ICubismAllocator
{
public:
    void* Allocate(const csmSizeType size) override { return std::malloc(size); }
    void Deallocate(void* memory) override { std::free(memory); }
    void* AllocateAligned(const csmSizeType size, const csmUint32 alignment) override
    {
        const size_t offset = alignment - 1 + sizeof(void*);
        void* allocation = Allocate(size + static_cast<csmUint32>(offset));
        size_t aligned = reinterpret_cast<size_t>(allocation) + sizeof(void*);
        const size_t shift = aligned % alignment;
        if (shift)
            aligned += (alignment - shift);
        reinterpret_cast<void**>(aligned)[-1] = allocation;
        return reinterpret_cast<void*>(aligned);
    }
    void DeallocateAligned(void* alignedMemory) override
    {
        Deallocate(reinterpret_cast<void**>(alignedMemory)[-1]);
    }
};

std::vector<csmByte> ReadFile(const std::string& path)
{
    std::ifstream in(path, std::ios::binary);
    if (!in)
        return {};
    in.seekg(0, std::ios::end);
    const auto n = static_cast<size_t>(in.tellg());
    in.seekg(0, std::ios::beg);
    std::vector<csmByte> buf(n);
    in.read(reinterpret_cast<char*>(buf.data()), static_cast<std::streamsize>(n));
    return buf;
}

std::string Join(const std::string& dir, const std::string& file)
{
    if (dir.empty())
        return file;
    if (dir.back() == '/')
        return dir + file;
    return dir + "/" + file;
}

void ResetModel(CubismModel* model, const std::vector<float>& defaultParts)
{
    for (csmInt32 i = 0; i < model->GetParameterCount(); ++i)
        model->SetParameterValue(i, model->GetParameterDefaultValue(i));
    for (csmInt32 i = 0; i < model->GetPartCount(); ++i)
        model->SetPartOpacity(i, defaultParts[static_cast<size_t>(i)]);
    model->SaveParameters();
}

void WriteHeader(std::ofstream& out, CubismModel* model)
{
    out << "frame,time";
    for (csmInt32 i = 0; i < model->GetParameterCount(); ++i)
        out << ",P:" << model->GetParameterId(i)->GetString().GetRawString();
    for (csmInt32 i = 0; i < model->GetPartCount(); ++i)
        out << ",O:" << model->GetPartId(i)->GetString().GetRawString();
    out << "\n";
}

void WriteRow(std::ofstream& out, int frame, float time, CubismModel* model)
{
    out << std::setprecision(9);
    out << frame << "," << time;
    for (csmInt32 i = 0; i < model->GetParameterCount(); ++i)
        out << "," << model->GetParameterValue(i);
    for (csmInt32 i = 0; i < model->GetPartCount(); ++i)
        out << "," << model->GetPartOpacity(i);
    out << "\n";
}

struct Job
{
    std::string Group;
    int Index;
    bool Loop;
};

} // namespace

int main(int argc, char** argv)
{
    std::string model3;
    std::string outDir = ".";
    float seconds = 10.0f;
    float fps = 60.0f;
    for (int i = 1; i < argc; ++i)
    {
        const std::string a = argv[i];
        if (a == "--model" && i + 1 < argc)
            model3 = argv[++i];
        else if (a == "--out" && i + 1 < argc)
            outDir = argv[++i];
        else if (a == "--seconds" && i + 1 < argc)
            seconds = std::strtof(argv[++i], nullptr);
        else if (a == "--fps" && i + 1 < argc)
            fps = std::strtof(argv[++i], nullptr);
        else
        {
            std::fprintf(stderr, "Unknown arg: %s\n", argv[i]);
            return 2;
        }
    }
    if (model3.empty())
    {
        std::fprintf(stderr, "Usage: native_motion_dump --model Hiyori.model3.json [--out dir] [--seconds 10] [--fps 60]\n");
        return 2;
    }

    const auto slash = model3.find_last_of('/');
    const std::string home = slash == std::string::npos ? std::string() : model3.substr(0, slash + 1);

    Allocator allocator;
    CubismFramework::Option option;
    option.LoggingLevel = CubismFramework::Option::LogLevel_Off;
    if (!CubismFramework::StartUp(&allocator, &option))
    {
        std::fprintf(stderr, "CubismFramework::StartUp failed\n");
        return 1;
    }
    CubismFramework::Initialize();

    auto settingBuf = ReadFile(model3);
    if (settingBuf.empty())
    {
        std::fprintf(stderr, "Cannot read %s\n", model3.c_str());
        return 1;
    }
    auto* setting = new CubismModelSettingJson(settingBuf.data(), static_cast<csmSizeInt>(settingBuf.size()));

    const std::string mocPath = Join(home, setting->GetModelFileName());
    auto mocBuf = ReadFile(mocPath);
    if (mocBuf.empty())
    {
        std::fprintf(stderr, "Cannot read %s\n", mocPath.c_str());
        return 1;
    }
    CubismMoc* moc = CubismMoc::Create(mocBuf.data(), static_cast<csmSizeInt>(mocBuf.size()));
    CubismModel* model = moc->CreateModel();
    std::vector<float> defaultParts(static_cast<size_t>(model->GetPartCount()));
    for (csmInt32 i = 0; i < model->GetPartCount(); ++i)
        defaultParts[static_cast<size_t>(i)] = model->GetPartOpacity(i);
    model->SaveParameters();

    csmVector<CubismIdHandle> eyeBlinkIds;
    for (csmInt32 i = 0; i < setting->GetEyeBlinkParameterCount(); ++i)
        eyeBlinkIds.PushBack(setting->GetEyeBlinkParameterId(i));
    csmVector<CubismIdHandle> lipSyncIds;
    for (csmInt32 i = 0; i < setting->GetLipSyncParameterCount(); ++i)
        lipSyncIds.PushBack(setting->GetLipSyncParameterId(i));

    const Job jobs[] = {
        {"Idle", 0, true}, {"Idle", 1, true}, {"Idle", 2, true}, {"Idle", 3, true},
        {"Idle", 4, true}, {"Idle", 5, true}, {"Idle", 6, true}, {"Idle", 7, true},
        {"Idle", 8, true}, {"TapBody", 0, false},
    };

    const int frames = static_cast<int>(std::lround(seconds * fps));
    const float dt = 1.0f / fps;

    for (const auto& job : jobs)
    {
        const char* fileName = setting->GetMotionFileName(job.Group.c_str(), job.Index);
        if (fileName == nullptr || fileName[0] == '\0')
        {
            std::fprintf(stderr, "skip %s[%d] (missing)\n", job.Group.c_str(), job.Index);
            continue;
        }

        const std::string motionPath = Join(home, fileName);
        auto motionBuf = ReadFile(motionPath);
        if (motionBuf.empty())
        {
            std::fprintf(stderr, "Cannot read %s\n", motionPath.c_str());
            return 1;
        }

        CubismMotion* motion = CubismMotion::Create(motionBuf.data(), static_cast<csmSizeInt>(motionBuf.size()));
        const float fadeIn = setting->GetMotionFadeInTimeValue(job.Group.c_str(), job.Index);
        const float fadeOut = setting->GetMotionFadeOutTimeValue(job.Group.c_str(), job.Index);
        if (fadeIn >= 0.0f)
            motion->SetFadeInTime(fadeIn);
        if (fadeOut >= 0.0f)
            motion->SetFadeOutTime(fadeOut);
        motion->SetLoop(job.Loop);
        motion->SetLoopFadeIn(false);
        motion->SetEffectIds(eyeBlinkIds, lipSyncIds);

        ResetModel(model, defaultParts);
        CubismMotionManager manager;
        manager.StartMotionPriority(motion, true, 2);

        // Prime StartTime = 0 without advancing (Unity Play-at-0, then tick).
        model->LoadParameters();
        manager.UpdateMotion(model, 0.0f);
        model->SaveParameters();

        const std::string csvPath = Join(outDir, job.Group + "_" + std::to_string(job.Index) + ".csv");
        std::ofstream out(csvPath);
        if (!out)
        {
            std::fprintf(stderr, "Cannot write %s\n", csvPath.c_str());
            return 1;
        }
        WriteHeader(out, model);

        for (int f = 0; f < frames; ++f)
        {
            model->LoadParameters();
            manager.UpdateMotion(model, dt);
            model->SaveParameters();
            WriteRow(out, f, (f + 1) * dt, model);
        }
        out.close();
        std::printf("wrote %s frames=%d fadeIn=%.3f loop=%d file=%s\n",
                    csvPath.c_str(), frames, fadeIn, job.Loop ? 1 : 0, fileName);
    }

    moc->DeleteModel(model);
    CubismMoc::Delete(moc);
    delete setting;
    CubismFramework::Dispose();
    return 0;
}
