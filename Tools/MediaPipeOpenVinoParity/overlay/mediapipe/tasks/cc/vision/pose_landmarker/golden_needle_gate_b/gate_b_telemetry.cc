// Copyright 2026 Golden Needle contributors.
// Licensed under the Apache License, Version 2.0.

#include "mediapipe/tasks/cc/vision/pose_landmarker/golden_needle_gate_b/gate_b_telemetry.h"

#include <mutex>
#include <vector>

namespace golden_needle_gate_b {
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

}  // namespace golden_needle_gate_b
