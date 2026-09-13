#include "mediapipe/tasks/cc/vision/pose_landmarker/golden_needle_unity_openvino/golden_needle_openvino_pose.h"

#include <openvino/openvino.hpp>

#include <chrono>
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <memory>
#include <mutex>
#include <optional>
#include <sstream>
#include <string>
#include <system_error>
#include <utility>
#include <vector>

#include "mediapipe/framework/api2/builder.h"
#include "mediapipe/framework/formats/image.h"
#include "mediapipe/framework/formats/image_frame.h"
#include "mediapipe/framework/formats/landmark.pb.h"
#include "mediapipe/framework/formats/rect.pb.h"
#include "mediapipe/framework/packet.h"
#include "mediapipe/framework/timestamp.h"
#include "mediapipe/tasks/cc/core/mediapipe_builtin_op_resolver.h"
#include "mediapipe/tasks/cc/core/task_runner.h"
#include "mediapipe/tasks/cc/vision/pose_detector/proto/pose_detector_graph_options.pb.h"
#include "mediapipe/tasks/cc/vision/pose_landmarker/golden_needle_unity_openvino/runtime_telemetry.h"
#include "mediapipe/tasks/cc/vision/pose_landmarker/proto/pose_landmarker_graph_options.pb.h"
#include "mediapipe/tasks/cc/vision/pose_landmarker/proto/pose_landmarks_detector_graph_options.pb.h"

namespace fs = std::filesystem;
namespace mp = mediapipe;
namespace core = mediapipe::tasks::core;

namespace {

constexpr char kPluginVersion[] = "0.2.0";
constexpr char kOpenVinoPin[] = "2026.3.0";
constexpr char kMediaPipePin[] = "0.10.22";
constexpr char kHomulerPin[] = "0.16.3";
constexpr char kGraphType[] =
    "mediapipe.tasks.vision.pose_landmarker.PoseLandmarkerGraph";
constexpr char kBackendSelectorName[] = "GOLDEN_NEEDLE_GATE_B_BACKEND";
constexpr char kBackendSelectorValue[] = "OPENVINO_CPU_FP32";
constexpr uintmax_t kDetectorSize = 2959078u;
constexpr uintmax_t kLandmarkSize = 2818390u;
constexpr double kPi = 3.14159265358979323846;

thread_local std::string g_last_error;
std::mutex g_process_mutex;
std::mutex g_graph_create_mutex;

void SetError(const std::string& value) { g_last_error = value; }
void ClearError() { g_last_error.clear(); }

double Ms(std::chrono::steady_clock::time_point start) {
  return std::chrono::duration<double, std::milli>(
             std::chrono::steady_clock::now() - start)
      .count();
}

int32_t CopyString(const std::string& value, char* buffer, uint32_t capacity) {
  const uint64_t required = static_cast<uint64_t>(value.size()) + 1u;
  if (required > UINT32_MAX) {
    SetError("String result exceeds ABI capacity range.");
    return GNOVPOSE_ERROR_INTERNAL;
  }
  if (buffer == nullptr || capacity < required) {
    SetError("Output buffer is null or too small; required bytes=" +
             std::to_string(required));
    return GNOVPOSE_ERROR_BUFFER_TOO_SMALL;
  }
  std::memcpy(buffer, value.c_str(), static_cast<size_t>(required));
  return GNOVPOSE_OK;
}

bool RuntimeMatchesPin() {
  const ov::Version version = ov::get_openvino_version();
  const std::string build =
      version.buildNumber == nullptr ? "" : version.buildNumber;
  return build.find(kOpenVinoPin) != std::string::npos;
}

bool CheckModelIdentity(const fs::path& path, const char* expected_name,
                        uintmax_t expected_size, std::string* error) {
  std::error_code ec;
  if (!fs::is_regular_file(path, ec) || ec) {
    *error = "Model file is missing: " + path.string();
    return false;
  }
  if (path.filename().string() != expected_name) {
    *error = "Unexpected model filename: " + path.filename().string() +
             "; expected " + expected_name;
    return false;
  }
  const uintmax_t size = fs::file_size(path, ec);
  if (ec || size != expected_size) {
    *error = "Model size mismatch for " + path.string() + "; expected " +
             std::to_string(expected_size) + ", got " +
             (ec ? std::string("<unavailable>") : std::to_string(size));
    return false;
  }
  return true;
}

class ScopedBackendSelector {
 public:
  ScopedBackendSelector() {
#if defined(_WIN32)
    char* value = nullptr;
    size_t length = 0;
    if (_dupenv_s(&value, &length, kBackendSelectorName) == 0 &&
        value != nullptr) {
      had_value_ = true;
      previous_ = value;
    }
    std::free(value);
    _putenv_s(kBackendSelectorName, kBackendSelectorValue);
#else
    const char* value = std::getenv(kBackendSelectorName);
    if (value != nullptr) {
      had_value_ = true;
      previous_ = value;
    }
    setenv(kBackendSelectorName, kBackendSelectorValue, 1);
#endif
  }

  ~ScopedBackendSelector() {
#if defined(_WIN32)
    _putenv_s(kBackendSelectorName, had_value_ ? previous_.c_str() : "");
#else
    if (had_value_) {
      setenv(kBackendSelectorName, previous_.c_str(), 1);
    } else {
      unsetenv(kBackendSelectorName);
    }
#endif
  }

  ScopedBackendSelector(const ScopedBackendSelector&) = delete;
  ScopedBackendSelector& operator=(const ScopedBackendSelector&) = delete;

 private:
  bool had_value_ = false;
  std::string previous_;
};

mp::CalculatorGraphConfig CreateExpandedGraph(
    const fs::path& detector_model, const fs::path& landmark_model) {
  using Options =
      mp::tasks::vision::pose_landmarker::proto::PoseLandmarkerGraphOptions;

  mp::api2::builder::Graph graph;
  auto& subgraph = graph.AddNode(kGraphType);
  auto& options = subgraph.GetOptions<Options>();

  options.mutable_base_options()->set_use_stream_mode(true);
  options.mutable_base_options()->mutable_acceleration()->mutable_tflite();
  options.set_min_tracking_confidence(0.5f);

  auto* detector = options.mutable_pose_detector_graph_options();
  detector->set_num_poses(1);
  detector->set_min_detection_confidence(0.5f);
  detector->mutable_base_options()->set_use_stream_mode(true);
  detector->mutable_base_options()->mutable_acceleration()->mutable_tflite();
  detector->mutable_base_options()->mutable_model_asset()->set_file_name(
      fs::absolute(detector_model).string());

  auto* landmark = options.mutable_pose_landmarks_detector_graph_options();
  landmark->set_min_detection_confidence(0.5f);
  landmark->mutable_base_options()->set_use_stream_mode(true);
  landmark->mutable_base_options()->mutable_acceleration()->mutable_tflite();
  landmark->mutable_base_options()->mutable_model_asset()->set_file_name(
      fs::absolute(landmark_model).string());

  graph.In("IMAGE").SetName("image_in");
  graph.In("NORM_RECT").SetName("norm_rect_in");
  graph.In("IMAGE") >> subgraph.In("IMAGE");
  graph.In("NORM_RECT") >> subgraph.In("NORM_RECT");

  subgraph.Out("NORM_LANDMARKS").SetName("norm_landmarks") >>
      graph.Out("NORM_LANDMARKS");
  subgraph.Out("WORLD_LANDMARKS").SetName("world_landmarks") >>
      graph.Out("WORLD_LANDMARKS");
  subgraph.Out("POSE_RECTS_NEXT_FRAME").SetName("pose_rects_next") >>
      graph.Out("POSE_RECTS_NEXT_FRAME");
  subgraph.Out("DETECTIONS").SetName("detections") >>
      graph.Out("DETECTIONS");
  subgraph.Out("IMAGE").SetName("image_out") >> graph.Out("IMAGE");
  return graph.GetConfig();
}

mp::NormalizedRect FullImageRect(int32_t width, int32_t height,
                                 int32_t rotation_degrees) {
  mp::NormalizedRect rect;
  rect.set_x_center(0.5f);
  rect.set_y_center(0.5f);
  rect.set_width(1.0f);
  rect.set_height(1.0f);
  rect.set_rotation(
      static_cast<float>(-rotation_degrees * kPi / 180.0));

  if (std::abs(rotation_degrees) % 180 != 0) {
    const float w = static_cast<float>(height) / static_cast<float>(width);
    const float h = static_cast<float>(width) / static_cast<float>(height);
    rect.set_width(w);
    rect.set_height(h);
  }
  return rect;
}

mp::Image CopyRgbaImage(const uint8_t* rgba, int32_t width, int32_t height,
                        int32_t stride_bytes) {
  auto frame = std::make_shared<mp::ImageFrame>(
      mp::ImageFormat::SRGBA, width, height, 1);
  const size_t row_bytes = static_cast<size_t>(width) * 4u;
  for (int32_t y = 0; y < height; ++y) {
    std::memcpy(
        frame->MutablePixelData() +
            static_cast<size_t>(y) * frame->WidthStep(),
        rgba + static_cast<size_t>(y) * static_cast<size_t>(stride_bytes),
        row_bytes);
  }
  return mp::Image(std::move(frame));
}

struct TelemetrySummary {
  uint32_t detector_calls = 0;
  uint32_t landmark_calls = 0;
  double detector_inference_ms = 0.0;
  double landmark_inference_ms = 0.0;
  double input_copy_ms = 0.0;
  double output_copy_ms = 0.0;
};

TelemetrySummary SummarizeTelemetry(
    const std::vector<golden_needle_unity_openvino::InferenceSample>& samples) {
  TelemetrySummary out;
  for (const auto& sample : samples) {
    out.input_copy_ms += sample.input_copy_ms;
    out.output_copy_ms += sample.output_copy_ms;
    if (sample.model_kind == "detector") {
      ++out.detector_calls;
      out.detector_inference_ms += sample.inference_ms;
    } else if (sample.model_kind == "landmark") {
      ++out.landmark_calls;
      out.landmark_inference_ms += sample.inference_ms;
    }
  }
  return out;
}

}  // namespace

struct gnovpose_context {
  std::unique_ptr<ov::Core> core;
  std::string device;
};

struct gnovpose_pose_engine {
  std::unique_ptr<core::TaskRunner> runner;
  fs::path detector_model;
  fs::path landmark_model;
  int64_t last_timestamp_millisec = -1;
  std::mutex mutex;
};

extern "C" {

uint32_t GNOVPOSE_CALL gnovpose_get_abi_version(void) {
  return GNOVPOSE_ABI_VERSION;
}

int32_t GNOVPOSE_CALL gnovpose_get_version_string(
    char* buffer, uint32_t capacity) {
  ClearError();
  return CopyString(kPluginVersion, buffer, capacity);
}

int32_t GNOVPOSE_CALL gnovpose_get_build_info(
    char* buffer, uint32_t capacity) {
  ClearError();
  std::ostringstream out;
  out << "plugin=" << kPluginVersion
      << ";abi=" << GNOVPOSE_ABI_VERSION_MAJOR << "."
      << GNOVPOSE_ABI_VERSION_MINOR
      << ";mediapipe_pin=" << kMediaPipePin
      << ";homuler_pin=" << kHomulerPin
      << ";openvino_pin=" << kOpenVinoPin
      << ";backend=OPENVINO_CPU_FP32";
  return CopyString(out.str(), buffer, capacity);
}

int32_t GNOVPOSE_CALL gnovpose_get_last_error(
    char* buffer, uint32_t capacity) {
  const std::string snapshot = g_last_error;
  if (buffer == nullptr || capacity < snapshot.size() + 1u) {
    return GNOVPOSE_ERROR_BUFFER_TOO_SMALL;
  }
  std::memcpy(buffer, snapshot.c_str(), snapshot.size() + 1u);
  return GNOVPOSE_OK;
}

int32_t GNOVPOSE_CALL gnovpose_create(
    const gnovpose_config* config, gnovpose_context** out_context) {
  ClearError();
  if (config == nullptr || out_context == nullptr) {
    SetError("config and out_context are required.");
    return GNOVPOSE_ERROR_INVALID_ARGUMENT;
  }
  *out_context = nullptr;
  if (config->struct_size != sizeof(gnovpose_config) ||
      config->abi_version != GNOVPOSE_ABI_VERSION ||
      config->config_version != GNOVPOSE_CONFIG_VERSION) {
    SetError("ABI/config version mismatch.");
    return GNOVPOSE_ERROR_ABI_MISMATCH;
  }

  const std::string device =
      config->device == nullptr ? "CPU" : config->device;
  if (device != "CPU") {
    SetError("Only explicit CPU is supported; no AUTO/GPU/NPU fallback.");
    return GNOVPOSE_ERROR_UNSUPPORTED_DEVICE;
  }

  try {
    auto context = std::make_unique<gnovpose_context>();
    context->core = std::make_unique<ov::Core>();
    context->device = device;
    (void)context->core->get_versions(device);
    *out_context = context.release();
    return GNOVPOSE_OK;
  } catch (const std::exception& ex) {
    SetError(std::string("OpenVINO context creation failed: ") + ex.what());
    return GNOVPOSE_ERROR_OPENVINO;
  } catch (...) {
    SetError("OpenVINO context creation failed with an unknown exception.");
    return GNOVPOSE_ERROR_INTERNAL;
  }
}

void GNOVPOSE_CALL gnovpose_destroy(gnovpose_context* context) {
  delete context;
}

int32_t GNOVPOSE_CALL gnovpose_self_test(gnovpose_context* context) {
  ClearError();
  if (context == nullptr || context->core == nullptr) {
    SetError("context is null.");
    return GNOVPOSE_ERROR_INVALID_ARGUMENT;
  }
  if (context->device != "CPU") {
    SetError("context device is not explicit CPU.");
    return GNOVPOSE_ERROR_UNSUPPORTED_DEVICE;
  }
  try {
    if (!RuntimeMatchesPin()) {
      SetError(std::string("Loaded OpenVINO runtime does not match pin ") +
               kOpenVinoPin + ". Actual build: " +
               ov::get_openvino_version().buildNumber);
      return GNOVPOSE_ERROR_OPENVINO;
    }
    const auto versions = context->core->get_versions("CPU");
    if (versions.empty()) {
      SetError("OpenVINO CPU device returned no version information.");
      return GNOVPOSE_ERROR_OPENVINO;
    }
    return GNOVPOSE_OK;
  } catch (const std::exception& ex) {
    SetError(std::string("OpenVINO self-test failed: ") + ex.what());
    return GNOVPOSE_ERROR_OPENVINO;
  } catch (...) {
    SetError("OpenVINO self-test failed with an unknown exception.");
    return GNOVPOSE_ERROR_INTERNAL;
  }
}

int32_t GNOVPOSE_CALL gnovpose_get_runtime_info(
    gnovpose_context* context, char* buffer, uint32_t capacity) {
  ClearError();
  if (context == nullptr || context->core == nullptr) {
    SetError("context is null.");
    return GNOVPOSE_ERROR_INVALID_ARGUMENT;
  }
  try {
    const ov::Version version = ov::get_openvino_version();
    const auto versions = context->core->get_versions(context->device);
    std::ostringstream out;
    out << "plugin=" << kPluginVersion
        << ";abi=" << GNOVPOSE_ABI_VERSION_MAJOR << "."
        << GNOVPOSE_ABI_VERSION_MINOR
        << ";mediapipe_pin=" << kMediaPipePin
        << ";openvino_pin=" << kOpenVinoPin
        << ";openvino_build=" << version.buildNumber
        << ";device=" << context->device;
    if (!versions.empty()) {
      out << ";device_plugin_build=" << versions.begin()->second.buildNumber;
    }
    return CopyString(out.str(), buffer, capacity);
  } catch (const std::exception& ex) {
    SetError(std::string("Runtime-info query failed: ") + ex.what());
    return GNOVPOSE_ERROR_OPENVINO;
  } catch (...) {
    SetError("Runtime-info query failed with an unknown exception.");
    return GNOVPOSE_ERROR_INTERNAL;
  }
}

int32_t GNOVPOSE_CALL gnovpose_pose_engine_create(
    gnovpose_context* context,
    const gnovpose_pose_engine_config* config,
    gnovpose_pose_engine** out_engine) {
  ClearError();
  if (context == nullptr || context->core == nullptr ||
      config == nullptr || out_engine == nullptr) {
    SetError("context, config, and out_engine are required.");
    return GNOVPOSE_ERROR_INVALID_ARGUMENT;
  }
  *out_engine = nullptr;
  if (context->device != "CPU") {
    SetError("pose engine requires explicit CPU context.");
    return GNOVPOSE_ERROR_UNSUPPORTED_DEVICE;
  }
  if (config->struct_size != sizeof(gnovpose_pose_engine_config) ||
      config->abi_version != GNOVPOSE_ABI_VERSION ||
      config->config_version != GNOVPOSE_POSE_ENGINE_CONFIG_VERSION) {
    SetError("pose-engine ABI/config version mismatch.");
    return GNOVPOSE_ERROR_ABI_MISMATCH;
  }
  if (config->detector_model_path == nullptr ||
      config->landmark_model_path == nullptr) {
    SetError("detector_model_path and landmark_model_path are required.");
    return GNOVPOSE_ERROR_INVALID_ARGUMENT;
  }

  const fs::path detector = fs::absolute(config->detector_model_path);
  const fs::path landmark = fs::absolute(config->landmark_model_path);
  std::string identity_error;
  if (!CheckModelIdentity(
          detector, "pose_detector.tflite", kDetectorSize, &identity_error) ||
      !CheckModelIdentity(
          landmark, "pose_landmarks_detector.tflite", kLandmarkSize,
          &identity_error)) {
    SetError(identity_error);
    return GNOVPOSE_ERROR_MODEL;
  }

  try {
    auto engine = std::make_unique<gnovpose_pose_engine>();
    engine->detector_model = detector;
    engine->landmark_model = landmark;

    auto graph = CreateExpandedGraph(detector, landmark);
    std::lock_guard<std::mutex> graph_lock(g_graph_create_mutex);
    ScopedBackendSelector selector;
#if MEDIAPIPE_DISABLE_GPU
    auto created = core::TaskRunner::Create(
        std::move(graph),
        std::make_unique<core::MediaPipeBuiltinOpResolver>(),
        nullptr, nullptr, std::nullopt, std::nullopt,
        /*disable_default_service=*/true);
#else
#error Golden Needle OpenVINO runtime must be built with MEDIAPIPE_DISABLE_GPU=1.
#endif
    if (!created.ok()) {
      SetError("MediaPipe/OpenVINO pose graph creation failed: " +
               created.status().ToString());
      return GNOVPOSE_ERROR_MEDIAPIPE;
    }
    engine->runner = std::move(created.value());
    *out_engine = engine.release();
    return GNOVPOSE_OK;
  } catch (const std::exception& ex) {
    SetError(std::string("Pose engine creation failed: ") + ex.what());
    return GNOVPOSE_ERROR_MEDIAPIPE;
  } catch (...) {
    SetError("Pose engine creation failed with an unknown exception.");
    return GNOVPOSE_ERROR_INTERNAL;
  }
}

void GNOVPOSE_CALL gnovpose_pose_engine_destroy(gnovpose_pose_engine* engine) {
  delete engine;
}

int32_t GNOVPOSE_CALL gnovpose_pose_engine_get_info(
    gnovpose_pose_engine* engine, char* buffer, uint32_t capacity) {
  ClearError();
  if (engine == nullptr || engine->runner == nullptr) {
    SetError("pose engine is null.");
    return GNOVPOSE_ERROR_INVALID_ARGUMENT;
  }
  std::ostringstream out;
  out << "backend=OPENVINO_CPU_FP32"
      << ";mediapipe_pin=" << kMediaPipePin
      << ";openvino_pin=" << kOpenVinoPin
      << ";detector=" << engine->detector_model.filename().string()
      << ";landmark=" << engine->landmark_model.filename().string()
      << ";num_poses=1;detection=0.5;presence=0.5;tracking=0.5"
      << ";segmentation=false;stream_mode=true";
  return CopyString(out.str(), buffer, capacity);
}

int32_t GNOVPOSE_CALL gnovpose_pose_engine_process_rgba(
    gnovpose_pose_engine* engine,
    const uint8_t* rgba,
    int32_t width,
    int32_t height,
    int32_t stride_bytes,
    int32_t rotation_degrees,
    int64_t timestamp_millisec,
    gnovpose_pose_result* out_result) {
  ClearError();
  if (engine == nullptr || engine->runner == nullptr || rgba == nullptr ||
      out_result == nullptr || width <= 0 || height <= 0 ||
      stride_bytes < width * 4) {
    SetError("invalid process_rgba arguments.");
    return GNOVPOSE_ERROR_INVALID_ARGUMENT;
  }
  if (out_result->struct_size != sizeof(gnovpose_pose_result) ||
      out_result->version != GNOVPOSE_POSE_RESULT_VERSION) {
    SetError("pose-result ABI/version mismatch.");
    return GNOVPOSE_ERROR_ABI_MISMATCH;
  }
  if (rotation_degrees % 90 != 0) {
    SetError("rotation_degrees must be a multiple of 90.");
    return GNOVPOSE_ERROR_INVALID_ARGUMENT;
  }

  std::lock_guard<std::mutex> engine_lock(engine->mutex);
  if (timestamp_millisec <= engine->last_timestamp_millisec) {
    SetError("timestamps must be strictly increasing for stream-mode tracking.");
    return GNOVPOSE_ERROR_TIMESTAMP;
  }

  try {
    const uint32_t result_struct_size = out_result->struct_size;
    const uint32_t result_version = out_result->version;
    std::memset(out_result, 0, sizeof(*out_result));
    out_result->struct_size = result_struct_size;
    out_result->version = result_version;
    out_result->backend = GNOVPOSE_BACKEND_OPENVINO_CPU_FP32;
    out_result->timestamp_millisec = timestamp_millisec;

    auto frame_copy_start = std::chrono::steady_clock::now();
    mp::Image image =
        CopyRgbaImage(rgba, width, height, stride_bytes);
    out_result->frame_copy_ms = Ms(frame_copy_start);
    mp::NormalizedRect rect =
        FullImageRect(width, height, rotation_degrees);

    core::PacketMap inputs;
    const int64_t timestamp_us = timestamp_millisec * 1000;
    inputs["image_in"] =
        mp::MakePacket<mp::Image>(std::move(image))
            .At(mp::Timestamp(timestamp_us));
    inputs["norm_rect_in"] =
        mp::MakePacket<mp::NormalizedRect>(rect)
            .At(mp::Timestamp(timestamp_us));

    std::lock_guard<std::mutex> process_lock(g_process_mutex);
    golden_needle_unity_openvino::ResetTelemetry();
    auto graph_start = std::chrono::steady_clock::now();
    auto processed = engine->runner->Process(std::move(inputs));
    out_result->graph_process_ms = Ms(graph_start);
    if (!processed.ok()) {
      SetError("MediaPipe/OpenVINO graph processing failed: " +
               processed.status().ToString());
      return GNOVPOSE_ERROR_MEDIAPIPE;
    }

    const auto telemetry = SummarizeTelemetry(
        golden_needle_unity_openvino::SnapshotTelemetry());
    out_result->detector_ran = telemetry.detector_calls > 0 ? 1u : 0u;
    out_result->detector_inference_ms = telemetry.detector_inference_ms;
    out_result->landmark_inference_ms = telemetry.landmark_inference_ms;
    out_result->tensor_input_copy_ms = telemetry.input_copy_ms;
    out_result->tensor_output_copy_ms = telemetry.output_copy_ms;

    auto marshal_start = std::chrono::steady_clock::now();
    auto& packets = processed.value();
    const mp::NormalizedLandmarkList* normalized = nullptr;
    const mp::LandmarkList* world = nullptr;

    auto norm_it = packets.find("norm_landmarks");
    if (norm_it != packets.end() && !norm_it->second.IsEmpty()) {
      const auto& lists =
          norm_it->second.Get<std::vector<mp::NormalizedLandmarkList>>();
      if (!lists.empty()) normalized = &lists.front();
    }
    auto world_it = packets.find("world_landmarks");
    if (world_it != packets.end() && !world_it->second.IsEmpty()) {
      const auto& lists =
          world_it->second.Get<std::vector<mp::LandmarkList>>();
      if (!lists.empty()) world = &lists.front();
    }

    if (normalized != nullptr) {
      if (normalized->landmark_size() !=
          static_cast<int>(GNOVPOSE_LANDMARK_COUNT)) {
        SetError("MediaPipe returned a pose with non-33 normalized landmarks.");
        return GNOVPOSE_ERROR_MEDIAPIPE;
      }
      if (world == nullptr ||
          world->landmark_size() !=
              static_cast<int>(GNOVPOSE_LANDMARK_COUNT)) {
        SetError("MediaPipe returned a pose without exactly 33 world landmarks.");
        return GNOVPOSE_ERROR_MEDIAPIPE;
      }

      out_result->has_pose = 1u;
      out_result->landmark_count = GNOVPOSE_LANDMARK_COUNT;
      for (uint32_t i = 0; i < GNOVPOSE_LANDMARK_COUNT; ++i) {
        const auto& n = normalized->landmark(static_cast<int>(i));
        const auto& w = world->landmark(static_cast<int>(i));
        auto& target = out_result->landmarks[i];
        target.x = n.x();
        target.y = n.y();
        target.z = n.z();
        if (n.has_visibility()) {
          target.visibility = n.visibility();
          target.flags |= GNOVPOSE_LANDMARK_HAS_VISIBILITY;
        }
        if (n.has_presence()) {
          target.presence = n.presence();
          target.flags |= GNOVPOSE_LANDMARK_HAS_PRESENCE;
        }
        target.world_x = w.x();
        target.world_y = w.y();
        target.world_z = w.z();
        target.flags |= GNOVPOSE_LANDMARK_HAS_WORLD;
      }
    }

    out_result->output_marshal_ms = Ms(marshal_start);
    engine->last_timestamp_millisec = timestamp_millisec;
    return GNOVPOSE_OK;
  } catch (const std::exception& ex) {
    SetError(std::string("process_rgba failed: ") + ex.what());
    return GNOVPOSE_ERROR_MEDIAPIPE;
  } catch (...) {
    SetError("process_rgba failed with an unknown exception.");
    return GNOVPOSE_ERROR_INTERNAL;
  }
}

}  // extern "C"
