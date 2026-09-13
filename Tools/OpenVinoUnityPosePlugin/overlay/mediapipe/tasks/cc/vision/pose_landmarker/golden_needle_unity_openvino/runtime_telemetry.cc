#include "mediapipe/tasks/cc/vision/pose_landmarker/golden_needle_unity_openvino/runtime_telemetry.h"

#include <mutex>
#include <vector>

namespace golden_needle_unity_openvino {
namespace {
std::mutex g_mutex;
std::vector<InferenceSample> g_samples;
}  // namespace

void ResetTelemetry() {
  std::lock_guard<std::mutex> lock(g_mutex);
  g_samples.clear();
}

void AddInferenceSample(const InferenceSample& sample) {
  std::lock_guard<std::mutex> lock(g_mutex);
  g_samples.push_back(sample);
}

std::vector<InferenceSample> SnapshotTelemetry() {
  std::lock_guard<std::mutex> lock(g_mutex);
  return g_samples;
}

}  // namespace golden_needle_unity_openvino
