cmake_minimum_required(VERSION 3.16)
project(native_motion_dump CXX)

set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(NOT DEFINED CUBISM_NATIVE_SDK)
  if(DEFINED ENV{CUBISM_NATIVE_SDK})
    set(CUBISM_NATIVE_SDK $ENV{CUBISM_NATIVE_SDK})
  else()
    set(CUBISM_NATIVE_SDK /tmp/cubism_native/CubismSdkForNative-5-r.5)
  endif()
endif()

set(CORE_PATH ${CUBISM_NATIVE_SDK}/Core)
set(FW ${CUBISM_NATIVE_SDK}/Framework/src)

add_library(Live2DCubismCore STATIC IMPORTED)
if(APPLE)
  if(CMAKE_SYSTEM_PROCESSOR MATCHES "arm64|aarch64")
    set(CORE_LIBRARY ${CORE_PATH}/lib/macos/arm64/libLive2DCubismCore.a)
  else()
    set(CORE_LIBRARY ${CORE_PATH}/lib/macos/x86_64/libLive2DCubismCore.a)
  endif()
  set(CUBISM_TARGET CSM_TARGET_MAC_GL)
else()
  set(CORE_LIBRARY ${CORE_PATH}/lib/linux/x86_64/libLive2DCubismCore.a)
  set(CUBISM_TARGET CSM_TARGET_LINUX_GL)
endif()
set_target_properties(Live2DCubismCore PROPERTIES
  IMPORTED_LOCATION ${CORE_LIBRARY}
  INTERFACE_INCLUDE_DIRECTORIES ${CORE_PATH}/include
)

# Headless Framework: motion + model + json, no renderer / effects / physics.
set(FW_SRCS
  ${FW}/CubismFramework.cpp
  ${FW}/CubismModelSettingJson.cpp
  ${FW}/CubismDefaultParameterId.cpp
  ${FW}/CubismCdiJson.cpp
  ${FW}/Id/CubismId.cpp
  ${FW}/Id/CubismIdManager.cpp
  ${FW}/Math/CubismMath.cpp
  ${FW}/Math/CubismMatrix44.cpp
  ${FW}/Math/CubismModelMatrix.cpp
  ${FW}/Math/CubismTargetPoint.cpp
  ${FW}/Math/CubismVector2.cpp
  ${FW}/Math/CubismViewMatrix.cpp
  ${FW}/Model/CubismMoc.cpp
  ${FW}/Model/CubismModel.cpp
  ${FW}/Model/CubismModelMultiplyAndScreenColor.cpp
  ${FW}/Rendering/csmBlendMode.cpp
  ${CMAKE_CURRENT_SOURCE_DIR}/renderer_stub.cpp
  ${FW}/Motion/ACubismMotion.cpp
  ${CMAKE_CURRENT_BINARY_DIR}/CubismMotion_unity_parts.cpp
  ${FW}/Motion/CubismMotionJson.cpp
  ${FW}/Motion/CubismMotionManager.cpp
  ${FW}/Motion/CubismMotionQueueEntry.cpp
  ${FW}/Motion/CubismMotionQueueManager.cpp
  ${FW}/Type/csmRectF.cpp
  ${FW}/Type/csmString.cpp
  ${FW}/Utils/CubismDebug.cpp
  ${FW}/Utils/CubismJson.cpp
  ${FW}/Utils/CubismString.cpp
)

add_custom_command(
  OUTPUT ${CMAKE_CURRENT_BINARY_DIR}/CubismMotion_unity_parts.cpp
  COMMAND ${CMAKE_COMMAND} -E env python3
          ${CMAKE_CURRENT_SOURCE_DIR}/patch_part_opacity.py
          ${FW}/Motion/CubismMotion.cpp
          ${CMAKE_CURRENT_BINARY_DIR}/CubismMotion_unity_parts.cpp
  DEPENDS ${FW}/Motion/CubismMotion.cpp ${CMAKE_CURRENT_SOURCE_DIR}/patch_part_opacity.py
  COMMENT "Patch CubismMotion PartOpacity to write parts (Unity)"
)

add_library(FrameworkHeadless STATIC ${FW_SRCS})
target_include_directories(FrameworkHeadless PUBLIC ${FW} ${FW}/Motion ${CORE_PATH}/include)
target_compile_definitions(FrameworkHeadless PUBLIC ${CUBISM_TARGET})
target_link_libraries(FrameworkHeadless PUBLIC Live2DCubismCore)

add_executable(native_motion_dump main.cpp)
target_link_libraries(native_motion_dump PRIVATE FrameworkHeadless)
target_compile_definitions(native_motion_dump PRIVATE ${CUBISM_TARGET})
