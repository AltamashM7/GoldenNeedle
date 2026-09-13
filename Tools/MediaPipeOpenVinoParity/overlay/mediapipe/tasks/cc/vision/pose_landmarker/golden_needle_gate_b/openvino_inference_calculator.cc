// Copyright 2026 Golden Needle contributors.
// Licensed under the Apache License, Version 2.0.
// Research-only Gate B calculator: surrounding pose semantics stay MediaPipe 0.10.22.

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstring>
#include <filesystem>
#include <iomanip>
#include <iostream>
#include <limits>
#include <memory>
#include <string>
#include <vector>

#include <openvino/openvino.hpp>
#include <openvino/runtime/properties.hpp>

#include "absl/status/status.h"
#include "mediapipe/calculators/tensor/inference_calculator.pb.h"
#include "mediapipe/framework/calculator_framework.h"
#include "mediapipe/framework/formats/tensor.h"
#include "mediapipe/tasks/cc/core/mediapipe_builtin_op_resolver.h"
#include "mediapipe/tasks/cc/vision/pose_landmarker/golden_needle_gate_b/gate_b_telemetry.h"
#include "tensorflow/lite/interpreter.h"
#include "tensorflow/lite/interpreter_builder.h"
#include "tensorflow/lite/model_builder.h"

namespace mediapipe {
namespace {

constexpr char kTensorsTag[] = "TENSORS";

double Ms(std::chrono::steady_clock::time_point start) {
  return std::chrono::duration<double, std::milli>(
             std::chrono::steady_clock::now() - start)
      .count();
}

bool SameShape(const Tensor::Shape& a, const ov::Shape& b) {
  if (a.dims.size() != b.size()) return false;
  for (size_t i = 0; i < b.size(); ++i) {
    if (a.dims[i] != static_cast<int>(b[i])) return false;
  }
  return true;
}

bool SameShape(const ov::Shape& a, const ov::Shape& b) { return a == b; }

bool SameShape(const TfLiteIntArray* dims, const ov::Shape& shape) {
  if (dims == nullptr || dims->size != static_cast<int>(shape.size())) {
    return false;
  }
  for (int i = 0; i < dims->size; ++i) {
    if (dims->data[i] != static_cast<int>(shape[i])) return false;
  }
  return true;
}

absl::Status RequireF32(const ov::Output<const ov::Node>& port,
                        const char* label) {
  return port.get_element_type() == ov::element::f32
             ? absl::OkStatus()
             : absl::InvalidArgumentError(std::string(label) +
                                          " must be float32 for Gate B.");
}

void PrintRawParity(const std::string& kind, size_t output_index,
                    const float* ov_data, const float* tflite_data,
                    size_t count) {
  size_t finite_pairs = 0;
  size_t nonfinite_pairs = 0;
  double abs_sum = 0.0;
  double sq_sum = 0.0;
  double max_abs = 0.0;
  double ov_min = std::numeric_limits<double>::infinity();
  double ov_max = -std::numeric_limits<double>::infinity();
  double tf_min = std::numeric_limits<double>::infinity();
  double tf_max = -std::numeric_limits<double>::infinity();

  for (size_t i = 0; i < count; ++i) {
    const double ov_value = ov_data[i];
    const double tf_value = tflite_data[i];
    if (!std::isfinite(ov_value) || !std::isfinite(tf_value)) {
      ++nonfinite_pairs;
      continue;
    }
    ++finite_pairs;
    ov_min = std::min(ov_min, ov_value);
    ov_max = std::max(ov_max, ov_value);
    tf_min = std::min(tf_min, tf_value);
    tf_max = std::max(tf_max, tf_value);
    const double error = std::abs(ov_value - tf_value);
    abs_sum += error;
    sq_sum += error * error;
    max_abs = std::max(max_abs, error);
  }

  const double mean_abs = finite_pairs ? abs_sum / finite_pairs : 0.0;
  const double rmse = finite_pairs ? std::sqrt(sq_sum / finite_pairs) : 0.0;
  std::cerr << std::setprecision(9)
            << "[Gate B raw parity] model=" << kind
            << " output=" << output_index
            << " elements=" << count
            << " finite_pairs=" << finite_pairs
            << " nonfinite_pairs=" << nonfinite_pairs
            << " mean_abs=" << mean_abs
            << " rmse=" << rmse
            << " max_abs=" << max_abs
            << " ov_min=" << ov_min
            << " ov_max=" << ov_max
            << " tflite_min=" << tf_min
            << " tflite_max=" << tf_max << "\n";
}

}  // namespace

class GoldenNeedleOpenVinoInferenceCalculator : public CalculatorBase {
 public:
  static absl::Status GetContract(CalculatorContract* cc) {
    cc->Inputs().Tag(kTensorsTag).Set<std::vector<Tensor>>();
    cc->Outputs().Tag(kTensorsTag).Set<std::vector<Tensor>>();
    return absl::OkStatus();
  }

  absl::Status Open(CalculatorContext* cc) override {
    const auto& options = cc->Options<InferenceCalculatorOptions>();
    if (!options.has_model_path() || options.model_path().empty()) {
      return absl::InvalidArgumentError(
          "Gate B OpenVINO calculator requires model_path.");
    }

    model_path_ = options.model_path();
    const std::string filename =
        std::filesystem::path(model_path_).filename().string();
    if (filename == "pose_detector.tflite") {
      kind_ = "detector";
      input_ = {1, 224, 224, 3};
      outputs_ = {{1, 2254, 12}, {1, 2254, 1}};
    } else if (filename == "pose_landmarks_detector.tflite") {
      kind_ = "landmark";
      input_ = {1, 256, 256, 3};
      outputs_ = {{1, 195}, {1, 1}, {1, 256, 256, 1},
                  {1, 64, 64, 39}, {1, 117}};
    } else {
      return absl::InvalidArgumentError(
          "Gate B refuses unexpected model filename: " + filename);
    }

    try {
      model_ = core_.read_model(model_path_);
      const Tensor::Shape expected_input(
          std::vector<int>{static_cast<int>(input_[0]),
                           static_cast<int>(input_[1]),
                           static_cast<int>(input_[2]),
                           static_cast<int>(input_[3])});
      if (model_->inputs().size() != 1 ||
          !SameShape(expected_input, model_->input().get_shape())) {
        return absl::InvalidArgumentError(
            "Gate B model input contract mismatch.");
      }
      auto input_status = RequireF32(model_->input(), "model input");
      if (!input_status.ok()) return input_status;
      if (model_->outputs().size() != outputs_.size()) {
        return absl::InvalidArgumentError(
            "Gate B model output count mismatch.");
      }

      indices_.assign(outputs_.size(), -1);
      for (size_t expected = 0; expected < outputs_.size(); ++expected) {
        for (size_t actual = 0; actual < model_->outputs().size(); ++actual) {
          if (!SameShape(model_->output(actual).get_shape(),
                         outputs_[expected])) {
            continue;
          }
          if (indices_[expected] != -1) {
            return absl::InvalidArgumentError("Ambiguous output shape.");
          }
          auto output_status = RequireF32(model_->output(actual), "model output");
          if (!output_status.ok()) return output_status;
          indices_[expected] = static_cast<int>(actual);
        }
        if (indices_[expected] < 0) {
          return absl::InvalidArgumentError("Expected output shape missing.");
        }
      }

      ov::AnyMap config = {
          {ov::hint::performance_mode.name(), ov::hint::PerformanceMode::LATENCY},
          {ov::hint::inference_precision.name(), ov::element::f32}};
      compiled_ = core_.compile_model(model_, "CPU", config);
      request_ = compiled_.create_infer_request();
      input_tensor_ = ov::Tensor(ov::element::f32, input_);
      request_.set_input_tensor(input_tensor_);

      // Diagnostic only: build a one-thread TFLite shadow interpreter for the
      // same file. It runs only on the first input handled by each calculator
      // instance and never supplies data to the MediaPipe graph. This gives us
      // a direct raw-output comparison before any pose postprocessing.
      shadow_model_ = tflite::FlatBufferModel::BuildFromFile(model_path_.c_str());
      if (!shadow_model_) {
        return absl::InternalError(
            "Gate B raw-parity shadow could not load the TFLite model.");
      }
      tasks::core::MediaPipeBuiltinOpResolver resolver;
      tflite::InterpreterBuilder builder(*shadow_model_, resolver);
      if (builder(&shadow_interpreter_) != kTfLiteOk || !shadow_interpreter_) {
        return absl::InternalError(
            "Gate B raw-parity shadow could not create a TFLite interpreter.");
      }
      shadow_interpreter_->SetNumThreads(1);
      if (shadow_interpreter_->AllocateTensors() != kTfLiteOk) {
        return absl::InternalError(
            "Gate B raw-parity shadow could not allocate TFLite tensors.");
      }
      if (shadow_interpreter_->inputs().size() != 1) {
        return absl::InternalError(
            "Gate B raw-parity shadow expected one TFLite input.");
      }
      const TfLiteTensor* shadow_input = shadow_interpreter_->tensor(
          shadow_interpreter_->inputs()[0]);
      if (shadow_input == nullptr || shadow_input->type != kTfLiteFloat32 ||
          !SameShape(shadow_input->dims, input_)) {
        return absl::InternalError(
            "Gate B raw-parity shadow input contract mismatch.");
      }

      shadow_output_ordinals_.assign(outputs_.size(), -1);
      for (size_t expected = 0; expected < outputs_.size(); ++expected) {
        for (size_t ordinal = 0;
             ordinal < shadow_interpreter_->outputs().size(); ++ordinal) {
          const TfLiteTensor* tensor = shadow_interpreter_->tensor(
              shadow_interpreter_->outputs()[ordinal]);
          if (tensor == nullptr || tensor->type != kTfLiteFloat32 ||
              !SameShape(tensor->dims, outputs_[expected])) {
            continue;
          }
          if (shadow_output_ordinals_[expected] != -1) {
            return absl::InternalError(
                "Gate B raw-parity shadow found ambiguous output shapes.");
          }
          shadow_output_ordinals_[expected] = static_cast<int>(ordinal);
        }
        if (shadow_output_ordinals_[expected] < 0) {
          return absl::InternalError(
              "Gate B raw-parity shadow expected output shape missing.");
        }
      }
    } catch (const std::exception& e) {
      return absl::InternalError(
          std::string("OpenVINO Gate B initialization failed: ") + e.what());
    }
    return absl::OkStatus();
  }

  absl::Status Process(CalculatorContext* cc) override {
    if (cc->Inputs().Tag(kTensorsTag).IsEmpty()) return absl::OkStatus();
    const auto& inputs =
        cc->Inputs().Tag(kTensorsTag).Get<std::vector<Tensor>>();
    if (inputs.size() != 1) {
      return absl::InvalidArgumentError("Gate B expects one input tensor.");
    }
    const Tensor& input = inputs[0];
    if (input.element_type() != Tensor::ElementType::kFloat32 ||
        !SameShape(input.shape(), input_)) {
      return absl::InvalidArgumentError(
          "Gate B MediaPipe input contract mismatch.");
    }

    golden_needle_gate_b::InferenceSample sample;
    sample.model_kind = kind_;

    try {
      auto start = std::chrono::steady_clock::now();
      auto input_view = input.GetCpuReadView();
      const size_t input_bytes = ov::shape_size(input_) * sizeof(float);
      const float* input_data = input_view.buffer<float>();
      std::memcpy(input_tensor_.data<float>(), input_data, input_bytes);
      sample.input_copy_ms = Ms(start);

      if (!shadow_compared_) {
        float* shadow_input = shadow_interpreter_->typed_input_tensor<float>(0);
        if (shadow_input == nullptr) {
          return absl::InternalError(
              "Gate B raw-parity shadow input tensor is unavailable.");
        }
        std::memcpy(shadow_input, input_data, input_bytes);
        if (shadow_interpreter_->Invoke() != kTfLiteOk) {
          return absl::InternalError(
              "Gate B raw-parity shadow TFLite invocation failed.");
        }
      }

      start = std::chrono::steady_clock::now();
      request_.infer();
      sample.inference_ms = Ms(start);

      if (!shadow_compared_) {
        for (size_t i = 0; i < outputs_.size(); ++i) {
          const auto ov_output = request_.get_output_tensor(indices_[i]);
          if (ov_output.get_element_type() != ov::element::f32 ||
              !SameShape(ov_output.get_shape(), outputs_[i])) {
            return absl::InternalError(
                "Gate B compiled OpenVINO output contract mismatch.");
          }
          const float* shadow_output =
              shadow_interpreter_->typed_output_tensor<float>(
                  shadow_output_ordinals_[i]);
          if (shadow_output == nullptr) {
            return absl::InternalError(
                "Gate B raw-parity shadow output tensor is unavailable.");
          }
          PrintRawParity(kind_, i, ov_output.data<const float>(), shadow_output,
                         ov::shape_size(outputs_[i]));
        }
        shadow_compared_ = true;
      }

      start = std::chrono::steady_clock::now();
      auto outputs = std::make_unique<std::vector<Tensor>>();
      outputs->reserve(outputs_.size());
      for (size_t i = 0; i < outputs_.size(); ++i) {
        const auto ov_output = request_.get_output_tensor(indices_[i]);
        const size_t expected_bytes =
            ov::shape_size(outputs_[i]) * sizeof(float);
        if (ov_output.get_element_type() != ov::element::f32 ||
            !SameShape(ov_output.get_shape(), outputs_[i]) ||
            ov_output.get_byte_size() != expected_bytes) {
          return absl::InternalError(
              "Gate B runtime OpenVINO output type/shape/size mismatch.");
        }

        std::vector<int> dims;
        dims.reserve(outputs_[i].size());
        for (size_t dim : outputs_[i]) dims.push_back(static_cast<int>(dim));
        Tensor mp_tensor(Tensor::ElementType::kFloat32, Tensor::Shape(dims));
        {
          // Release the write view before moving the Tensor into its output
          // vector. Tensor::Move transfers storage but not the view mutex.
          auto write_view = mp_tensor.GetCpuWriteView();
          std::memcpy(write_view.buffer<float>(),
                      ov_output.data<const float>(), expected_bytes);
        }
        outputs->push_back(std::move(mp_tensor));
      }
      sample.output_copy_ms = Ms(start);
      golden_needle_gate_b::AddInferenceSample(sample);
      cc->Outputs().Tag(kTensorsTag).Add(outputs.release(),
                                        cc->InputTimestamp());
    } catch (const std::exception& e) {
      return absl::InternalError(
          std::string("OpenVINO Gate B inference failed: ") + e.what());
    }
    return absl::OkStatus();
  }

 private:
  std::string model_path_;
  std::string kind_;
  ov::Shape input_;
  std::vector<ov::Shape> outputs_;
  std::vector<int> indices_;
  ov::Core core_;
  std::shared_ptr<ov::Model> model_;
  ov::CompiledModel compiled_;
  ov::InferRequest request_;
  ov::Tensor input_tensor_;

  std::unique_ptr<tflite::FlatBufferModel> shadow_model_;
  std::unique_ptr<tflite::Interpreter> shadow_interpreter_;
  std::vector<int> shadow_output_ordinals_;
  bool shadow_compared_ = false;
};

REGISTER_CALCULATOR(GoldenNeedleOpenVinoInferenceCalculator);

}  // namespace mediapipe
