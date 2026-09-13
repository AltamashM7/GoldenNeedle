cmake_minimum_required(VERSION 3.24)
project(GoldenNeedleOpenVinoPose LANGUAGES CXX)

if(NOT WIN32)
  message(FATAL_ERROR "Golden Needle OpenVINO Unity pose plugin currently targets Windows x86_64 only.")
endif()
if(NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
  message(FATAL_ERROR "Golden Needle OpenVINO Unity pose plugin requires x64.")
endif()
if(NOT MSVC)
  message(FATAL_ERROR "The approved U1 build uses MSVC/Visual Studio 2022.")
endif()

set(OPENVINO_ROOT "" CACHE PATH "Root of the exact OpenVINO 2026.3.0 Windows toolkit")
if(NOT OPENVINO_ROOT)
  message(FATAL_ERROR "OPENVINO_ROOT is required and must point to OpenVINO 2026.3.0.")
endif()

find_package(OpenVINO REQUIRED
  PATHS "${OPENVINO_ROOT}/runtime/cmake"
  NO_DEFAULT_PATH)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreadedDLL")

add_library(golden_needle_openvino_pose SHARED
  src/golden_needle_openvino_pose.cpp
  include/golden_needle_openvino_pose.h)
target_include_directories(golden_needle_openvino_pose PUBLIC "${CMAKE_CURRENT_SOURCE_DIR}/include")
target_compile_definitions(golden_needle_openvino_pose PRIVATE
  GNOVPOSE_EXPORTS
  GNOVPOSE_PLUGIN_VERSION="0.1.0"
  GNOVPOSE_OPENVINO_PIN="2026.3.0"
  GNOVPOSE_MEDIAPIPE_PIN="0.10.22"
  WIN32_LEAN_AND_MEAN
  NOMINMAX)
target_compile_options(golden_needle_openvino_pose PRIVATE /W4 /permissive- /EHsc)
target_link_libraries(golden_needle_openvino_pose PRIVATE openvino::runtime)
set_target_properties(golden_needle_openvino_pose PROPERTIES
  OUTPUT_NAME "golden_needle_openvino_pose"
  DEBUG_POSTFIX "")

add_executable(gnovpose_smoke tests/plugin_smoke_test.cpp)
target_include_directories(gnovpose_smoke PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/include")
target_compile_definitions(gnovpose_smoke PRIVATE _WIN32_WINNT=0x0602 WIN32_LEAN_AND_MEAN NOMINMAX)
target_compile_options(gnovpose_smoke PRIVATE /W4 /permissive- /EHsc)

include(CTest)
