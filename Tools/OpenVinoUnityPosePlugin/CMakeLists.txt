cmake_minimum_required(VERSION 3.24)
project(GoldenNeedleOpenVinoPoseSmoke LANGUAGES CXX)

if(NOT WIN32)
  message(FATAL_ERROR "Golden Needle OpenVINO Unity pose plugin currently targets Windows x86_64 only.")
endif()
if(NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
  message(FATAL_ERROR "Golden Needle OpenVINO Unity pose plugin requires x64.")
endif()
if(NOT MSVC)
  message(FATAL_ERROR "The approved build uses MSVC/Visual Studio 2022.")
endif()

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreadedDLL")

# The production U2 plugin is Bazel-built from pinned Homuler/MediaPipe sources.
# CMake intentionally builds only the independent dynamic-load lifecycle host.
add_executable(gnovpose_smoke tests/plugin_smoke_test.cpp)
target_include_directories(gnovpose_smoke PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/include")
target_compile_definitions(gnovpose_smoke PRIVATE
  _WIN32_WINNT=0x0602
  WIN32_LEAN_AND_MEAN
  NOMINMAX)
target_compile_options(gnovpose_smoke PRIVATE /W4 /permissive- /EHsc)
