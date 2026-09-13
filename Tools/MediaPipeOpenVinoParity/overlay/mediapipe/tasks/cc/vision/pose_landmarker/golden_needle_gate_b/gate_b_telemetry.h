// Copyright 2026 Golden Needle contributors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Gate B research-only telemetry. This is not production Unity code.

#ifndef MEDIAPIPE_EXAMPLES_DESKTOP_GOLDEN_NEEDLE_GATE_B_TELEMETRY_H_
#define MEDIAPIPE_EXAMPLES_DESKTOP_GOLDEN_NEEDLE_GATE_B_TELEMETRY_H_

#include <string>
#include <vector>

namespace golden_needle_gate_b {

struct InferenceSample {
  std::string model_kind;
  double input_copy_ms = 0.0;
  double inference_ms = 0.0;
  double output_copy_ms = 0.0;
};

void ResetTelemetry();
void AddInferenceSample(const InferenceSample& sample);
std::vector<InferenceSample> SnapshotTelemetry();

}  // namespace golden_needle_gate_b

#endif
