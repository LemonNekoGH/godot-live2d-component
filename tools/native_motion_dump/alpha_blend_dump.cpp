// GPU reference matching Cubism Native's alpha-blend fragment shaders.
#include <Live2DCubismCore.h>
#include <GL/glew.h>
#include <GLFW/glfw3.h>
#include <algorithm>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <fstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace {
constexpr int W = 96, H = 96;
const char* Vertex = R"(#version 330 core
out vec2 uv;
void main(){ vec2 p=vec2((gl_VertexID<<1)&2,gl_VertexID&2); uv=p; gl_Position=vec4(p*2.0-1.0,0,1); })";
const char* CopyFragment = R"(#version 330 core
in vec2 uv; out vec4 outColor; uniform sampler2D image;
void main(){ vec4 c=texture(image,uv); outColor=vec4(c.rgb*c.a,c.a); })";
const char* BlendPrefix = R"(#version 330 core
in vec2 uv; out vec4 outColor; uniform sampler2D sourceImage; uniform sampler2D destinationImage;
vec4 AlphaBlend(vec3 color, vec4 colorSource, vec4 colorDestination);
vec4 Straight(vec4 p){return p.a<0.00001?vec4(0):vec4(p.rgb/p.a,p.a);}
void main(){vec4 s=texture(sourceImage,uv);vec4 d=Straight(texture(destinationImage,uv));outColor=clamp(AlphaBlend(s.rgb,s,d),0,1);}
)";

GLuint Compile(GLenum type, const char* text){ GLuint s=glCreateShader(type); glShaderSource(s,1,&text,nullptr); glCompileShader(s); GLint ok; glGetShaderiv(s,GL_COMPILE_STATUS,&ok); if(!ok){char log[2048];glGetShaderInfoLog(s,sizeof log,nullptr,log);throw std::runtime_error(log);} return s; }
GLuint Program(const char* fragment){ GLuint p=glCreateProgram(),v=Compile(GL_VERTEX_SHADER,Vertex),f=Compile(GL_FRAGMENT_SHADER,fragment); glAttachShader(p,v);glAttachShader(p,f);glLinkProgram(p);glDeleteShader(v);glDeleteShader(f);return p; }
std::string Read(const char* path){std::ifstream f(path);return std::string(std::istreambuf_iterator<char>(f),{});}
std::uint8_t Q(float x){return static_cast<std::uint8_t>(std::floor(std::max(0.f,std::min(1.f,x))*255.f+0.5f));}
GLuint Texture(const std::vector<std::uint8_t>& data){GLuint t;glGenTextures(1,&t);glBindTexture(GL_TEXTURE_2D,t);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MIN_FILTER,GL_NEAREST);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MAG_FILTER,GL_NEAREST);glTexImage2D(GL_TEXTURE_2D,0,GL_RGBA8,W,H,0,GL_RGBA,GL_UNSIGNED_BYTE,data.data());return t;}
GLuint Target(GLuint& texture){std::vector<std::uint8_t> empty(W*H*4);texture=Texture(empty);GLuint f;glGenFramebuffers(1,&f);glBindFramebuffer(GL_FRAMEBUFFER,f);glFramebufferTexture2D(GL_FRAMEBUFFER,GL_COLOR_ATTACHMENT0,GL_TEXTURE_2D,texture,0);return f;}
std::vector<std::uint8_t> Input(bool source){std::vector<std::uint8_t>b(W*H*4);for(int y=0;y<H;y++)for(int x=0;x<W;x++){float u=x/float(W-1),v=y/float(H-1);float a=source?((x*17+y*13)%W)/float(W-1):((x*7+y*29+11)%W)/float(W-1);int p=(y*W+x)*4;b[p]=Q(source?u:1-u);b[p+1]=Q(source?1-v:v);b[p+2]=Q(source?.25f+.5f*v:.75f-.5f*u);b[p+3]=Q(a);}return b;}
}
int main(int argc,char**argv){
 if(argc!=3)return 2; glfwInit();glfwWindowHint(GLFW_VISIBLE,GL_FALSE);glfwWindowHint(GLFW_CONTEXT_VERSION_MAJOR,3);glfwWindowHint(GLFW_CONTEXT_VERSION_MINOR,3);GLFWwindow*w=glfwCreateWindow(W,H,"alpha",nullptr,nullptr);glfwMakeContextCurrent(w);glewInit();glDisable(GL_BLEND);glDisable(GL_FRAMEBUFFER_SRGB);glPixelStorei(GL_PACK_ALIGNMENT,1);
 const std::string officialAlpha=Read(argv[2]);if(officialAlpha.empty())return 3;
 GLuint vao;glGenVertexArrays(1,&vao);glBindVertexArray(vao);GLuint source=Texture(Input(true)),destinationInput=Texture(Input(false)),destination,output;GLuint destinationFbo=Target(destination),outputFbo=Target(output);GLuint copy=Program(CopyFragment);
 glViewport(0,0,W,H);glBindFramebuffer(GL_FRAMEBUFFER,destinationFbo);glUseProgram(copy);glActiveTexture(GL_TEXTURE0);glBindTexture(GL_TEXTURE_2D,destinationInput);glUniform1i(glGetUniformLocation(copy,"image"),0);glDrawArrays(GL_TRIANGLES,0,3);
 const char*names[]={"over","atop","out","conjoint_over","disjoint_over"};std::vector<std::uint8_t>pixels(W*H*4);
 for(int mode=0;mode<5;mode++){std::string fragment=std::string(BlendPrefix)+"\n#define CSM_ALPHA_BLEND_MODE "+std::to_string(mode)+"\n"+officialAlpha;GLuint blend=Program(fragment.c_str());glBindFramebuffer(GL_FRAMEBUFFER,outputFbo);glUseProgram(blend);glActiveTexture(GL_TEXTURE0);glBindTexture(GL_TEXTURE_2D,source);glUniform1i(glGetUniformLocation(blend,"sourceImage"),0);glActiveTexture(GL_TEXTURE1);glBindTexture(GL_TEXTURE_2D,destination);glUniform1i(glGetUniformLocation(blend,"destinationImage"),1);glDrawArrays(GL_TRIANGLES,0,3);glReadPixels(0,0,W,H,GL_RGBA,GL_UNSIGNED_BYTE,pixels.data());std::ofstream(std::string(argv[1])+"/"+names[mode]+".rgba",std::ios::binary).write(reinterpret_cast<char*>(pixels.data()),pixels.size());glDeleteProgram(blend);}
 std::printf("Cubism Core=%u; OpenGL=%s; GPU RGBA8 readback complete\n",csmGetVersion(),glGetString(GL_RENDERER));glfwDestroyWindow(w);glfwTerminate();return 0;
}
