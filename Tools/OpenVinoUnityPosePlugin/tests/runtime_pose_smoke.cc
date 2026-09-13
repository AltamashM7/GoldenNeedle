#include <windows.h>

#include "mediapipe/tasks/cc/vision/pose_landmarker/golden_needle_unity_openvino/golden_needle_openvino_pose.h"

#include <cmath>
#include <filesystem>
#include <iostream>
#include <string>
#include <vector>

#include "opencv2/imgcodecs.hpp"
#include "opencv2/imgproc.hpp"

namespace fs = std::filesystem;

namespace {

template <typename T>
T LoadProc(HMODULE module, const char* name) {
  const auto proc = reinterpret_cast<T>(GetProcAddress(module, name));
  if (proc == nullptr) {
    std::cerr << "Missing export: " << name
              << " win32=" << GetLastError() << "\n";
  }
  return proc;
}

void RemoveSearchDirectories(std::vector<DLL_DIRECTORY_COOKIE>* cookies) {
  for (auto it = cookies->rbegin(); it != cookies->rend(); ++it) {
    if (*it != nullptr) RemoveDllDirectory(*it);
  }
  cookies->clear();
}

bool ResultLooksValid(const gnovpose_pose_result& result) {
  if (result.backend != GNOVPOSE_BACKEND_OPENVINO_CPU_FP32 ||
      result.has_pose != 1u ||
      result.landmark_count != GNOVPOSE_LANDMARK_COUNT ||
      result.landmark_inference_ms <= 0.0 ||
      result.graph_process_ms <= 0.0) {
    return false;
  }
  for (uint32_t i = 0; i < GNOVPOSE_LANDMARK_COUNT; ++i) {
    const auto& lm = result.landmarks[i];
    if (!std::isfinite(lm.x) || !std::isfinite(lm.y) ||
        !std::isfinite(lm.z) || !std::isfinite(lm.world_x) ||
        !std::isfinite(lm.world_y) || !std::isfinite(lm.world_z) ||
        (lm.flags & GNOVPOSE_LANDMARK_HAS_WORLD) == 0u) {
      return false;
    }
  }
  return true;
}

}  // namespace

int wmain(int argc, wchar_t** argv) {
  if (argc < 5) {
    std::wcerr
        << L"usage: runtime_pose_smoke.exe <plugin.dll> <pose_detector.tflite> "
           L"<pose_landmarks_detector.tflite> <pose.jpg> [dependency-dir ...]\n";
    return 64;
  }

  const fs::path dll_path = fs::absolute(argv[1]);
  const fs::path detector_path = fs::absolute(argv[2]);
  const fs::path landmark_path = fs::absolute(argv[3]);
  const fs::path image_path = fs::absolute(argv[4]);

  if (!SetDefaultDllDirectories(
          LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_USER_DIRS)) {
    std::cerr << "SetDefaultDllDirectories failed: " << GetLastError() << "\n";
    return 2;
  }

  std::vector<DLL_DIRECTORY_COOKIE> cookies;
  auto add_dir = [&](const fs::path& path) {
    DLL_DIRECTORY_COOKIE cookie = AddDllDirectory(fs::absolute(path).c_str());
    if (cookie == nullptr) {
      std::wcerr << L"AddDllDirectory failed for " << path.c_str()
                 << L": " << GetLastError() << L"\n";
      return false;
    }
    cookies.push_back(cookie);
    return true;
  };
  if (!add_dir(dll_path.parent_path())) return 3;
  for (int i = 5; i < argc; ++i) {
    if (!add_dir(argv[i])) {
      RemoveSearchDirectories(&cookies);
      return 3;
    }
  }

  HMODULE module = LoadLibraryExW(
      dll_path.c_str(), nullptr,
      LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_USER_DIRS);
  if (module == nullptr) {
    std::cerr << "LoadLibraryExW failed: " << GetLastError() << "\n";
    RemoveSearchDirectories(&cookies);
    return 4;
  }

  using GetText = int32_t (GNOVPOSE_CALL *)(char*, uint32_t);
  using Create = int32_t (GNOVPOSE_CALL *)(
      const gnovpose_config*, gnovpose_context**);
  using Destroy = void (GNOVPOSE_CALL *)(gnovpose_context*);
  using EngineCreate = int32_t (GNOVPOSE_CALL *)(
      gnovpose_context*, const gnovpose_pose_engine_config*,
      gnovpose_pose_engine**);
  using EngineDestroy = void (GNOVPOSE_CALL *)(gnovpose_pose_engine*);
  using EngineInfo = int32_t (GNOVPOSE_CALL *)(
      gnovpose_pose_engine*, char*, uint32_t);
  using Process = int32_t (GNOVPOSE_CALL *)(
      gnovpose_pose_engine*, const uint8_t*, int32_t, int32_t, int32_t,
      int32_t, int64_t, gnovpose_pose_result*);

  const auto get_last_error =
      LoadProc<GetText>(module, "gnovpose_get_last_error");
  const auto create = LoadProc<Create>(module, "gnovpose_create");
  const auto destroy = LoadProc<Destroy>(module, "gnovpose_destroy");
  const auto engine_create =
      LoadProc<EngineCreate>(module, "gnovpose_pose_engine_create");
  const auto engine_destroy =
      LoadProc<EngineDestroy>(module, "gnovpose_pose_engine_destroy");
  const auto engine_info =
      LoadProc<EngineInfo>(module, "gnovpose_pose_engine_get_info");
  const auto process =
      LoadProc<Process>(module, "gnovpose_pose_engine_process_rgba");
  if (!get_last_error || !create || !destroy || !engine_create ||
      !engine_destroy || !engine_info || !process) {
    FreeLibrary(module);
    RemoveSearchDirectories(&cookies);
    return 5;
  }

  auto last_error = [&]() {
    char buffer[4096] = {};
    if (get_last_error(buffer, sizeof(buffer)) == GNOVPOSE_OK) {
      return std::string(buffer);
    }
    return std::string("<last-error unavailable>");
  };

  gnovpose_config config{};
  config.struct_size = sizeof(config);
  config.abi_version = GNOVPOSE_ABI_VERSION;
  config.config_version = GNOVPOSE_CONFIG_VERSION;
  config.device = "CPU";

  gnovpose_context* context = nullptr;
  int32_t code = create(&config, &context);
  if (code != GNOVPOSE_OK || context == nullptr) {
    std::cerr << "context create failed: " << code
              << " " << last_error() << "\n";
    FreeLibrary(module);
    RemoveSearchDirectories(&cookies);
    return 6;
  }

  const std::string detector_utf8 = detector_path.string();
  const std::string landmark_utf8 = landmark_path.string();
  gnovpose_pose_engine_config engine_config{};
  engine_config.struct_size = sizeof(engine_config);
  engine_config.abi_version = GNOVPOSE_ABI_VERSION;
  engine_config.config_version = GNOVPOSE_POSE_ENGINE_CONFIG_VERSION;
  engine_config.detector_model_path = detector_utf8.c_str();
  engine_config.landmark_model_path = landmark_utf8.c_str();

  gnovpose_pose_engine* engine = nullptr;
  code = engine_create(context, &engine_config, &engine);
  if (code != GNOVPOSE_OK || engine == nullptr) {
    std::cerr << "pose engine create failed: " << code
              << " " << last_error() << "\n";
    destroy(context);
    FreeLibrary(module);
    RemoveSearchDirectories(&cookies);
    return 7;
  }

  char info[4096] = {};
  if (engine_info(engine, info, sizeof(info)) != GNOVPOSE_OK) {
    std::cerr << "pose engine info failed: " << last_error() << "\n";
    engine_destroy(engine);
    destroy(context);
    FreeLibrary(module);
    RemoveSearchDirectories(&cookies);
    return 8;
  }
  std::cout << "pose_engine_info=" << info << "\n";

  cv::Mat bgr = cv::imread(image_path.string(), cv::IMREAD_COLOR);
  if (bgr.empty()) {
    std::cerr << "could not read pose image: " << image_path << "\n";
    engine_destroy(engine);
    destroy(context);
    FreeLibrary(module);
    RemoveSearchDirectories(&cookies);
    return 9;
  }
  cv::Mat rgba;
  cv::cvtColor(bgr, rgba, cv::COLOR_BGR2RGBA);
  if (!rgba.isContinuous()) rgba = rgba.clone();

  gnovpose_pose_result first{};
  first.struct_size = sizeof(first);
  first.version = GNOVPOSE_POSE_RESULT_VERSION;
  code = process(engine, rgba.data, rgba.cols, rgba.rows,
                 static_cast<int32_t>(rgba.step), 0, 1000, &first);
  if (code != GNOVPOSE_OK || !ResultLooksValid(first)) {
    std::cerr << "first real-frame inference failed/invalid: code=" << code
              << " error=" << last_error()
              << " has_pose=" << first.has_pose
              << " landmarks=" << first.landmark_count << "\n";
    engine_destroy(engine);
    destroy(context);
    FreeLibrary(module);
    RemoveSearchDirectories(&cookies);
    return 10;
  }

  gnovpose_pose_result second{};
  second.struct_size = sizeof(second);
  second.version = GNOVPOSE_POSE_RESULT_VERSION;
  code = process(engine, rgba.data, rgba.cols, rgba.rows,
                 static_cast<int32_t>(rgba.step), 0, 1033, &second);
  if (code != GNOVPOSE_OK || !ResultLooksValid(second)) {
    std::cerr << "second stream-frame inference failed/invalid: code=" << code
              << " error=" << last_error()
              << " has_pose=" << second.has_pose
              << " landmarks=" << second.landmark_count << "\n";
    engine_destroy(engine);
    destroy(context);
    FreeLibrary(module);
    RemoveSearchDirectories(&cookies);
    return 11;
  }

  std::cout
      << "frame1 graph_ms=" << first.graph_process_ms
      << " detector_ms=" << first.detector_inference_ms
      << " landmark_ms=" << first.landmark_inference_ms
      << " detector_ran=" << first.detector_ran
      << " landmarks=" << first.landmark_count << "\n";
  std::cout
      << "frame2 graph_ms=" << second.graph_process_ms
      << " detector_ms=" << second.detector_inference_ms
      << " landmark_ms=" << second.landmark_inference_ms
      << " detector_ran=" << second.detector_ran
      << " landmarks=" << second.landmark_count << "\n";

  engine_destroy(engine);
  destroy(context);
  if (!FreeLibrary(module)) {
    std::cerr << "FreeLibrary failed: " << GetLastError() << "\n";
    RemoveSearchDirectories(&cookies);
    return 12;
  }
  RemoveSearchDirectories(&cookies);

  std::cout << "GNOVPOSE_U2_REAL_FRAME_33_NORMALIZED_WORLD=PASS\n";
  return 0;
}
