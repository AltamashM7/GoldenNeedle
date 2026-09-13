// Copyright 2026 Golden Needle contributors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Research-only Gate B runner. It intentionally keeps MediaPipe 0.10.22 pose
// graph semantics and swaps only the inference calculator in OPENVINO mode.

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstdlib>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <memory>
#include <optional>
#include <sstream>
#include <string>
#include <vector>

#include "absl/status/status.h"
#include "mediapipe/tasks/cc/vision/pose_landmarker/golden_needle_gate_b/gate_b_telemetry.h"
#include "mediapipe/framework/api2/builder.h"
#include "mediapipe/framework/calculator.pb.h"
#include "mediapipe/framework/formats/detection.pb.h"
#include "mediapipe/framework/formats/image.h"
#include "mediapipe/framework/formats/image_frame.h"
#include "mediapipe/framework/formats/landmark.pb.h"
#include "mediapipe/framework/formats/rect.pb.h"
#include "mediapipe/framework/packet.h"
#include "mediapipe/framework/timestamp.h"
#include "mediapipe/tasks/cc/core/base_options.h"
#include "mediapipe/tasks/cc/core/task_runner.h"
#include "mediapipe/tasks/cc/core/utils.h"
#include "mediapipe/tasks/cc/vision/core/running_mode.h"
#include "mediapipe/tasks/cc/vision/pose_landmarker/pose_landmarker.h"
#include "mediapipe/tasks/cc/vision/pose_landmarker/proto/pose_landmarker_graph_options.pb.h"
#include "mediapipe/tasks/cc/vision/pose_detector/proto/pose_detector_graph_options.pb.h"
#include "mediapipe/tasks/cc/vision/pose_landmarker/proto/pose_landmarks_detector_graph_options.pb.h"
#include "opencv2/imgcodecs.hpp"
#include "opencv2/imgproc.hpp"
#include "opencv2/videoio.hpp"

namespace fs = std::filesystem;
namespace mp = mediapipe;
namespace core = mediapipe::tasks::core;
namespace pl = mediapipe::tasks::vision::pose_landmarker;

namespace {

constexpr char kGraphType[] =
    "mediapipe.tasks.vision.pose_landmarker.PoseLandmarkerGraph";

struct Args {
  std::string mode;
  fs::path task_bundle;
  fs::path detector_model;
  fs::path landmark_model;
  fs::path input_frames;
  fs::path output;
  std::string input_label;
  std::string input_sha256;
  double fps = 30.0;
  fs::path prepare_video;
  fs::path prepare_frames_dir;
  fs::path prepare_metadata;
};

std::string EscapeJson(const std::string& value) {
  std::ostringstream out;
  for (unsigned char c : value) {
    switch (c) {
      case '"': out << "\\\""; break;
      case '\\': out << "\\\\"; break;
      case '\b': out << "\\b"; break;
      case '\f': out << "\\f"; break;
      case '\n': out << "\\n"; break;
      case '\r': out << "\\r"; break;
      case '\t': out << "\\t"; break;
      default:
        if (c < 0x20) {
          out << "\\u" << std::hex << std::setw(4) << std::setfill('0')
              << static_cast<int>(c) << std::dec;
        } else {
          out << static_cast<char>(c);
        }
    }
  }
  return out.str();
}

double MsSince(std::chrono::steady_clock::time_point start) {
  return std::chrono::duration<double, std::milli>(
             std::chrono::steady_clock::now() - start)
      .count();
}

std::optional<std::string> TakeValue(int& i, int argc, char** argv) {
  if (i + 1 >= argc) return std::nullopt;
  return std::string(argv[++i]);
}

bool ParseArgs(int argc, char** argv, Args* args, std::string* error) {
  for (int i = 1; i < argc; ++i) {
    std::string key = argv[i];
    auto take = [&]() -> std::optional<std::string> {
      return TakeValue(i, argc, argv);
    };
    if (key == "--mode") {
      auto v = take(); if (!v) { *error = "missing --mode value"; return false; }
      args->mode = *v;
    } else if (key == "--task-bundle") {
      auto v = take(); if (!v) return false; args->task_bundle = *v;
    } else if (key == "--detector-model") {
      auto v = take(); if (!v) return false; args->detector_model = *v;
    } else if (key == "--landmark-model") {
      auto v = take(); if (!v) return false; args->landmark_model = *v;
    } else if (key == "--input-frames") {
      auto v = take(); if (!v) return false; args->input_frames = *v;
    } else if (key == "--output") {
      auto v = take(); if (!v) return false; args->output = *v;
    } else if (key == "--fps") {
      auto v = take(); if (!v) return false;
      try { args->fps = std::stod(*v); } catch (...) { *error = "invalid --fps"; return false; }
    } else if (key == "--input-label") {
      auto v = take(); if (!v) return false; args->input_label = *v;
    } else if (key == "--input-sha256") {
      auto v = take(); if (!v) return false; args->input_sha256 = *v;
    } else if (key == "--prepare-video") {
      auto v = take(); if (!v) return false; args->prepare_video = *v;
    } else if (key == "--prepare-frames-dir") {
      auto v = take(); if (!v) return false; args->prepare_frames_dir = *v;
    } else if (key == "--prepare-metadata") {
      auto v = take(); if (!v) return false; args->prepare_metadata = *v;
    } else {
      *error = "unknown argument: " + key;
      return false;
    }
  }
  if (!args->prepare_video.empty()) {
    if (args->prepare_frames_dir.empty() || args->prepare_metadata.empty()) {
      *error = "--prepare-video requires --prepare-frames-dir and --prepare-metadata";
      return false;
    }
    return true;
  }
  if (args->mode != "TASKS_REFERENCE" &&
      args->mode != "GRAPH_TFLITE_CPU" &&
      args->mode != "GRAPH_OPENVINO_CPU_FP32") {
    *error = "--mode must be TASKS_REFERENCE, GRAPH_TFLITE_CPU, or GRAPH_OPENVINO_CPU_FP32";
    return false;
  }
  if (args->input_frames.empty() || args->output.empty() || args->fps <= 0.0) {
    *error = "run mode requires --input-frames, --output, and positive --fps";
    return false;
  }
  return true;
}

int PrepareVideo(const Args& args) {
  cv::VideoCapture capture(args.prepare_video.string());
  if (!capture.isOpened()) {
    std::cerr << "Failed to open video: " << args.prepare_video << "\n";
    return 2;
  }
  double fps = capture.get(cv::CAP_PROP_FPS);
  if (!(fps > 0.0) || !std::isfinite(fps)) fps = 30.0;
  fs::remove_all(args.prepare_frames_dir);
  fs::create_directories(args.prepare_frames_dir);
  cv::Mat frame;
  int count = 0;
  int width = 0, height = 0;
  while (capture.read(frame)) {
    if (frame.empty()) continue;
    if (count == 0) { width = frame.cols; height = frame.rows; }
    std::ostringstream name;
    name << "frame-" << std::setw(6) << std::setfill('0') << count << ".png";
    if (!cv::imwrite((args.prepare_frames_dir / name.str()).string(), frame)) {
      std::cerr << "Failed to write decoded frame " << count << "\n";
      return 2;
    }
    ++count;
  }
  if (count == 0) {
    std::cerr << "Video decoded zero frames.\n";
    return 2;
  }
  fs::create_directories(args.prepare_metadata.parent_path());
  std::ofstream out(args.prepare_metadata);
  out << std::setprecision(12)
      << "{\"fps\":" << fps << ",\"frame_count\":" << count
      << ",\"width\":" << width << ",\"height\":" << height << "}\n";
  std::cout << "[Gate B] decoded " << count << " frames at source fps=" << fps
            << " into " << args.prepare_frames_dir << "\n";
  return 0;
}

std::vector<fs::path> ListFrames(const fs::path& directory) {
  std::vector<fs::path> frames;
  for (const auto& entry : fs::directory_iterator(directory)) {
    if (!entry.is_regular_file()) continue;
    std::string ext = entry.path().extension().string();
    std::transform(ext.begin(), ext.end(), ext.begin(), ::tolower);
    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp") {
      frames.push_back(entry.path());
    }
  }
  std::sort(frames.begin(), frames.end());
  return frames;
}

mp::Image MatToImage(const cv::Mat& bgr) {
  cv::Mat rgb;
  cv::cvtColor(bgr, rgb, cv::COLOR_BGR2RGB);
  auto image_frame = std::make_shared<mp::ImageFrame>(
      mp::ImageFormat::SRGB, rgb.cols, rgb.rows, 1);
  const size_t row_bytes = static_cast<size_t>(rgb.cols) * 3;
  for (int y = 0; y < rgb.rows; ++y) {
    std::memcpy(image_frame->MutablePixelData() +
                    static_cast<size_t>(y) * image_frame->WidthStep(),
                rgb.ptr(y), row_bytes);
  }
  return mp::Image(image_frame);
}

void WriteOptional(std::ostream& out, const std::optional<float>& value) {
  if (value.has_value()) out << std::setprecision(9) << *value;
  else out << "null";
}

void WriteProtoLandmark(std::ostream& out, const mp::NormalizedLandmark& lm) {
  out << "{\"x\":" << std::setprecision(9) << lm.x()
      << ",\"y\":" << lm.y() << ",\"z\":" << lm.z()
      << ",\"visibility\":";
  if (lm.has_visibility()) out << lm.visibility(); else out << "null";
  out << ",\"presence\":";
  if (lm.has_presence()) out << lm.presence(); else out << "null";
  out << "}";
}

void WriteProtoLandmark(std::ostream& out, const mp::Landmark& lm) {
  out << "{\"x\":" << std::setprecision(9) << lm.x()
      << ",\"y\":" << lm.y() << ",\"z\":" << lm.z()
      << ",\"visibility\":";
  if (lm.has_visibility()) out << lm.visibility(); else out << "null";
  out << ",\"presence\":";
  if (lm.has_presence()) out << lm.presence(); else out << "null";
  out << "}";
}

template <typename ListT>
void WriteProtoList(std::ostream& out, const ListT* list) {
  if (list == nullptr) { out << "[]"; return; }
  out << "[";
  for (int i = 0; i < list->landmark_size(); ++i) {
    if (i) out << ",";
    WriteProtoLandmark(out, list->landmark(i));
  }
  out << "]";
}

template <typename LandmarkT>
void WriteContainerLandmark(std::ostream& out, const LandmarkT& lm) {
  out << "{\"x\":" << std::setprecision(9) << lm.x
      << ",\"y\":" << lm.y << ",\"z\":" << lm.z
      << ",\"visibility\":";
  WriteOptional(out, lm.visibility);
  out << ",\"presence\":";
  WriteOptional(out, lm.presence);
  out << "}";
}

template <typename ContainerT>
void WriteContainerList(std::ostream& out, const ContainerT* list) {
  if (list == nullptr) { out << "[]"; return; }
  out << "[";
  for (size_t i = 0; i < list->landmarks.size(); ++i) {
    if (i) out << ",";
    WriteContainerLandmark(out, list->landmarks[i]);
  }
  out << "]";
}

mp::CalculatorGraphConfig CreateExpandedGraph(const Args& args) {
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
      fs::absolute(args.detector_model).string());

  auto* landmark = options.mutable_pose_landmarks_detector_graph_options();
  landmark->set_min_detection_confidence(0.5f);
  landmark->mutable_base_options()->set_use_stream_mode(true);
  landmark->mutable_base_options()->mutable_acceleration()->mutable_tflite();
  landmark->mutable_base_options()->mutable_model_asset()->set_file_name(
      fs::absolute(args.landmark_model).string());

  graph.In("IMAGE").SetName("image_in");
  graph.In("NORM_RECT").SetName("norm_rect_in");
  graph.In("IMAGE") >> subgraph.In("IMAGE");
  graph.In("NORM_RECT") >> subgraph.In("NORM_RECT");

  subgraph.Out("NORM_LANDMARKS").SetName("norm_landmarks") >>
      graph.Out("NORM_LANDMARKS");
  subgraph.Out("WORLD_LANDMARKS").SetName("world_landmarks") >>
      graph.Out("WORLD_LANDMARKS");
  subgraph.Out("AUXILIARY_LANDMARKS").SetName("aux_landmarks") >>
      graph.Out("AUXILIARY_LANDMARKS");
  subgraph.Out("POSE_RECTS_NEXT_FRAME").SetName("pose_rects_next") >>
      graph.Out("POSE_RECTS_NEXT_FRAME");
  subgraph.Out("DETECTIONS").SetName("detections") >>
      graph.Out("DETECTIONS");
  subgraph.Out("IMAGE").SetName("image_out") >> graph.Out("IMAGE");
  return graph.GetConfig();
}

std::unique_ptr<pl::PoseLandmarker> CreateTasksReference(
    const Args& args, double* startup_ms, std::string* error) {
  auto options = std::make_unique<pl::PoseLandmarkerOptions>();
  options->base_options.model_asset_path = fs::absolute(args.task_bundle).string();
  options->base_options.delegate = core::BaseOptions::Delegate::CPU;
  options->running_mode = mp::tasks::vision::core::RunningMode::VIDEO;
  options->num_poses = 1;
  options->min_pose_detection_confidence = 0.5f;
  options->min_pose_presence_confidence = 0.5f;
  options->min_tracking_confidence = 0.5f;
  options->output_segmentation_masks = false;

  auto start = std::chrono::steady_clock::now();
  auto created = pl::PoseLandmarker::Create(std::move(options));
  *startup_ms = MsSince(start);
  if (!created.ok()) {
    *error = created.status().ToString();
    return nullptr;
  }
  return std::move(created.value());
}

std::unique_ptr<core::TaskRunner> CreateGraphRunner(
    const Args& args, double* startup_ms, std::string* error) {
  if (args.mode == "GRAPH_OPENVINO_CPU_FP32") {
#ifdef _WIN32
    _putenv_s("GOLDEN_NEEDLE_GATE_B_BACKEND", "OPENVINO_CPU_FP32");
#else
    setenv("GOLDEN_NEEDLE_GATE_B_BACKEND", "OPENVINO_CPU_FP32", 1);
#endif
  } else {
#ifdef _WIN32
    _putenv_s("GOLDEN_NEEDLE_GATE_B_BACKEND", "");
#else
    unsetenv("GOLDEN_NEEDLE_GATE_B_BACKEND");
#endif
  }
  auto config = CreateExpandedGraph(args);
  auto start = std::chrono::steady_clock::now();
#if MEDIAPIPE_DISABLE_GPU
  auto created = core::TaskRunner::Create(
      std::move(config), nullptr, nullptr, nullptr, std::nullopt, std::nullopt,
      /*disable_default_service=*/true);
#else
#error Gate B runner must be built with MEDIAPIPE_DISABLE_GPU=1.
#endif
  *startup_ms = MsSince(start);
  if (!created.ok()) {
    *error = created.status().ToString();
    return nullptr;
  }
  return std::move(created.value());
}

void WriteHeader(std::ostream& out, const Args& args, int width, int height,
                 size_t frame_count, double startup_ms) {
  out << "{\"type\":\"header\",\"mode\":\"" << EscapeJson(args.mode)
      << "\",\"input_label\":\"" << EscapeJson(args.input_label)
      << "\",\"input_sha256\":\"" << EscapeJson(args.input_sha256)
      << "\",\"fps\":" << std::setprecision(12) << args.fps
      << ",\"width\":" << width << ",\"height\":" << height
      << ",\"frame_count\":" << frame_count
      << ",\"startup_ms\":" << startup_ms
      << ",\"timing_scope\":\"offline_video_mode_graph_capacity\"}\n";
}

struct TelemetryDelta {
  int detector_calls = 0;
  int landmark_calls = 0;
  double detector_infer_ms = 0.0;
  double landmark_infer_ms = 0.0;
  double bridge_copy_ms = 0.0;
};

TelemetryDelta DeltaTelemetry(size_t from,
    const std::vector<golden_needle_gate_b::InferenceSample>& samples) {
  TelemetryDelta d;
  for (size_t i = from; i < samples.size(); ++i) {
    const auto& s = samples[i];
    d.bridge_copy_ms += s.input_copy_ms + s.output_copy_ms;
    if (s.model_kind == "detector") {
      ++d.detector_calls; d.detector_infer_ms += s.inference_ms;
    } else if (s.model_kind == "landmark") {
      ++d.landmark_calls; d.landmark_infer_ms += s.inference_ms;
    }
  }
  return d;
}

int RunMode(const Args& args) {
  auto frames = ListFrames(args.input_frames);
  if (frames.empty()) {
    std::cerr << "No input frames found in " << args.input_frames << "\n";
    return 2;
  }
  cv::Mat first = cv::imread(frames.front().string(), cv::IMREAD_COLOR);
  if (first.empty()) {
    std::cerr << "Could not read first input frame.\n";
    return 2;
  }
  fs::create_directories(args.output.parent_path());
  std::ofstream out(args.output);
  if (!out) {
    std::cerr << "Could not open output: " << args.output << "\n";
    return 2;
  }

  double startup_ms = 0.0;
  std::string error;
  std::unique_ptr<pl::PoseLandmarker> tasks;
  std::unique_ptr<core::TaskRunner> graph_runner;
  golden_needle_gate_b::ResetTelemetry();
  if (args.mode == "TASKS_REFERENCE") {
    tasks = CreateTasksReference(args, &startup_ms, &error);
    if (!tasks) { std::cerr << error << "\n"; return 2; }
  } else {
    graph_runner = CreateGraphRunner(args, &startup_ms, &error);
    if (!graph_runner) { std::cerr << error << "\n"; return 2; }
  }
  WriteHeader(out, args, first.cols, first.rows, frames.size(), startup_ms);

  size_t telemetry_seen = 0;
  int detector_output_frames = 0;
  int pose_frames = 0;
  double first_frame_ms = -1.0;

  for (size_t index = 0; index < frames.size(); ++index) {
    cv::Mat bgr = cv::imread(frames[index].string(), cv::IMREAD_COLOR);
    if (bgr.empty()) {
      std::cerr << "Could not read frame: " << frames[index] << "\n";
      return 2;
    }
    const int64_t timestamp_ms =
        static_cast<int64_t>(std::llround(index * 1000.0 / args.fps));
    auto image = MatToImage(bgr);
    auto start = std::chrono::steady_clock::now();

    bool pose_present = false;
    const mp::NormalizedLandmarkList* norm = nullptr;
    const mp::LandmarkList* world = nullptr;
    std::optional<mp::NormalizedLandmarkList> norm_storage;
    std::optional<mp::LandmarkList> world_storage;
    bool auxiliary_available = false;
    bool detector_ran = false;
    bool detector_known = false;
    std::optional<mp::NormalizedRect> roi;
    TelemetryDelta telemetry;

    std::optional<mp::tasks::components::containers::NormalizedLandmarks>
        task_norm;
    std::optional<mp::tasks::components::containers::Landmarks> task_world;

    if (tasks) {
      auto result = tasks->DetectForVideo(std::move(image), timestamp_ms);
      if (!result.ok()) {
        std::cerr << "TASKS_REFERENCE failed at frame " << index << ": "
                  << result.status() << "\n";
        return 2;
      }
      if (!result->pose_landmarks.empty()) {
        pose_present = true;
        task_norm = result->pose_landmarks.front();
        task_world = result->pose_world_landmarks.front();
      }
    } else {
      mp::NormalizedRect full;
      full.set_x_center(0.5f);
      full.set_y_center(0.5f);
      full.set_width(1.0f);
      full.set_height(1.0f);
      full.set_rotation(0.0f);
      core::PacketMap inputs;
      const int64_t timestamp_us = timestamp_ms * 1000;
      inputs["image_in"] =
          mp::MakePacket<mp::Image>(std::move(image)).At(mp::Timestamp(timestamp_us));
      inputs["norm_rect_in"] =
          mp::MakePacket<mp::NormalizedRect>(full).At(mp::Timestamp(timestamp_us));
      auto result = graph_runner->Process(std::move(inputs));
      if (!result.ok()) {
        std::cerr << args.mode << " failed at frame " << index << ": "
                  << result.status() << "\n";
        return 2;
      }
      auto& packets = result.value();
      auto norm_it = packets.find("norm_landmarks");
      if (norm_it != packets.end() && !norm_it->second.IsEmpty()) {
        const auto& lists =
            norm_it->second.Get<std::vector<mp::NormalizedLandmarkList>>();
        if (!lists.empty()) {
          norm_storage = lists.front();
          norm = &*norm_storage;
          pose_present = true;
        }
      }
      auto world_it = packets.find("world_landmarks");
      if (world_it != packets.end() && !world_it->second.IsEmpty()) {
        const auto& lists = world_it->second.Get<std::vector<mp::LandmarkList>>();
        if (!lists.empty()) {
          world_storage = lists.front();
          world = &*world_storage;
        }
      }
      auto aux_it = packets.find("aux_landmarks");
      if (aux_it != packets.end() && !aux_it->second.IsEmpty()) {
        const auto& lists =
            aux_it->second.Get<std::vector<mp::NormalizedLandmarkList>>();
        auxiliary_available = !lists.empty();
      }
      auto roi_it = packets.find("pose_rects_next");
      if (roi_it != packets.end() && !roi_it->second.IsEmpty()) {
        const auto& rects =
            roi_it->second.Get<std::vector<mp::NormalizedRect>>();
        if (!rects.empty()) roi = rects.front();
      }
      auto det_it = packets.find("detections");
      detector_known = true;
      detector_ran =
          det_it != packets.end() && !det_it->second.IsEmpty();
      if (detector_ran) ++detector_output_frames;

      if (args.mode == "GRAPH_OPENVINO_CPU_FP32") {
        auto samples = golden_needle_gate_b::SnapshotTelemetry();
        telemetry = DeltaTelemetry(telemetry_seen, samples);
        telemetry_seen = samples.size();
      }
    }

    double latency_ms = MsSince(start);
    if (index == 0) first_frame_ms = latency_ms;
    if (pose_present) ++pose_frames;

    out << "{\"type\":\"frame\",\"index\":" << index
        << ",\"timestamp_ms\":" << timestamp_ms
        << ",\"latency_ms\":" << std::setprecision(9) << latency_ms
        << ",\"pose_present\":" << (pose_present ? "true" : "false")
        << ",\"normalized\":";
    if (tasks) WriteContainerList(out, task_norm ? &*task_norm : nullptr);
    else WriteProtoList(out, norm);
    out << ",\"world\":";
    if (tasks) WriteContainerList(out, task_world ? &*task_world : nullptr);
    else WriteProtoList(out, world);
    out << ",\"auxiliary_available\":";
    if (tasks) out << "null"; else out << (auxiliary_available ? "true" : "false");
    out << ",\"detector_ran\":";
    if (!detector_known) out << "null";
    else out << (detector_ran ? "true" : "false");
    out << ",\"next_roi\":";
    if (!roi) {
      out << "null";
    } else {
      out << "{\"x_center\":" << roi->x_center()
          << ",\"y_center\":" << roi->y_center()
          << ",\"width\":" << roi->width()
          << ",\"height\":" << roi->height()
          << ",\"rotation\":" << roi->rotation() << "}";
    }
    out << ",\"detector_inference_ms\":";
    if (args.mode == "GRAPH_OPENVINO_CPU_FP32")
      out << telemetry.detector_infer_ms;
    else out << "null";
    out << ",\"landmark_inference_ms\":";
    if (args.mode == "GRAPH_OPENVINO_CPU_FP32")
      out << telemetry.landmark_infer_ms;
    else out << "null";
    out << ",\"bridge_copy_ms\":";
    if (args.mode == "GRAPH_OPENVINO_CPU_FP32")
      out << telemetry.bridge_copy_ms;
    else out << "null";
    out << "}\n";
  }

  auto all_samples = golden_needle_gate_b::SnapshotTelemetry();
  TelemetryDelta totals = DeltaTelemetry(0, all_samples);
  out << "{\"type\":\"footer\",\"mode\":\"" << EscapeJson(args.mode)
      << "\",\"startup_ms\":" << startup_ms
      << ",\"first_frame_ms\":" << first_frame_ms
      << ",\"pose_frames\":" << pose_frames
      << ",\"detector_output_frames\":" << detector_output_frames
      << ",\"detector_calls\":";
  if (args.mode == "GRAPH_OPENVINO_CPU_FP32")
    out << totals.detector_calls;
  else if (args.mode == "GRAPH_TFLITE_CPU")
    out << detector_output_frames;
  else out << "null";
  out << ",\"landmark_calls\":";
  if (args.mode == "GRAPH_OPENVINO_CPU_FP32")
    out << totals.landmark_calls;
  else out << "null";
  out << ",\"bridge_copy_total_ms\":";
  if (args.mode == "GRAPH_OPENVINO_CPU_FP32")
    out << totals.bridge_copy_ms;
  else out << "null";
  out << "}\n";

  if (tasks) tasks->Close();
  if (graph_runner) graph_runner->Close();
  std::cout << "[Gate B] " << args.mode << " complete: frames=" << frames.size()
            << " output=" << args.output << "\n";
  return 0;
}

}  // namespace

int main(int argc, char** argv) {
  Args args;
  std::string error;
  if (!ParseArgs(argc, argv, &args, &error)) {
    std::cerr << "Argument error: " << error << "\n";
    return 2;
  }
  if (!args.prepare_video.empty()) return PrepareVideo(args);
  return RunMode(args);
}
