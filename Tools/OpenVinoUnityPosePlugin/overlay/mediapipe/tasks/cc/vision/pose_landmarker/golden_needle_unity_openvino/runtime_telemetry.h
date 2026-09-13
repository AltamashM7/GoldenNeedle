#pragma once

#include <string>
#include <vector>

namespace golden_needle_unity_openvino {

struct InferenceSample {
  std::string model_kind;
  double input_copy_ms = 0.0;
  double inference_ms = 0.0;
  double output_copy_ms = 0.0;
};

void ResetTelemetry();
void AddInferenceSample(const InferenceSample& sample);
std::vector<InferenceSample> SnapshotTelemetry();

}  // namespace golden_needle_unity_openvino
