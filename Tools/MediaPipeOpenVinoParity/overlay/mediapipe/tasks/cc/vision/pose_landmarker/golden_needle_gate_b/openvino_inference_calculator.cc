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
#include <utility>
#include <vector>

#include <openvino/openvino.hpp>
#include <openvino/runtime/properties.hpp>

#include "absl/status/status.h"
#include "absl/status/statusor.h"
#include "mediapipe/calculators/tensor/inference_calculator.h"
#include "mediapipe/calculators/tensor/inference_calculator.pb.h"
#include "mediapipe/calculators/tensor/inference_io_mapper.h"
#include "mediapipe/calculators/tensor/tensor_span.h"
#include "mediapipe/framework/calculator_framework.h"
#include "mediapipe/framework/formats/tensor.h"
#include "mediapipe/framework/port/ret_check.h"
#include "mediapipe/framework/port/status_macros.h"
#include "mediapipe/tasks/cc/core/mediapipe_builtin_op_resolver.h"
#include "mediapipe/tasks/cc/vision/pose_landmarker/golden_needle_gate_b/gate_b_telemetry.h"
#include "tensorflow/lite/interpreter.h"
#include "tensorflow/lite/interpreter_builder.h"
#include "tensorflow/lite/model_builder.h"
#include "tensorflow/lite/util.h"

namespace mediapipe {
namespace api2 {
namespace {

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

ov::Shape TfLiteShape(const TfLiteTensor& tensor) {
  ov::Shape result;
  if (tensor.dims == nullptr) return result;
  result.reserve(tensor.dims->size);
  for (int i = 0; i < tensor.dims->size; ++i) {
    result.push_back(static_cast<size_t>(tensor.dims->data[i]));
  }
  return result;
}

Tensor::Shape MediaPipeShape(const ov::Shape& shape) {
  std::vector<int> dims;
  dims.reserve(shape.size());
  for (size_t dim : shape) dims.push_back(static_cast<int>(dim));
  return Tensor::Shape(dims);
}

absl::Status RequireF32(const ov::Output<const ov::Node>& port,
                        const char* label) {
  return port.get_element_type() == ov::element::f32
             ? absl::OkStatus()
             : absl::InvalidArgumentError(std::string(label) +
                                          " must be float32 for Gate B.");
}

void PrintRawParity(const std::string& kind, size_t output_ordinal,
                    int openvino_port, const float* ov_data,
                    const float* tflite_data, size_t count) {
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
            << " output=" << output_ordinal
            << " ov_port=" << openvino_port
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

// Reuse MediaPipe's own InferenceCalculator interface instead of emulating a
// generic CalculatorBase. This preserves the stock node's zero timestamp
// offset, tensor I/O mapping, empty-input behavior, and typed output dispatch.
struct GoldenNeedleOpenVinoInferenceCalculator : public InferenceCalculator {
  static constexpr char kCalculatorName[] =
      "GoldenNeedleOpenVinoInferenceCalculator";
};

class GoldenNeedleOpenVinoInferenceCalculatorImpl
    : public InferenceCalculatorNodeImpl<
          GoldenNeedleOpenVinoInferenceCalculator,
          GoldenNeedleOpenVinoInferenceCalculatorImpl> {
 public:
  static absl::Status UpdateContract(CalculatorContract* cc) {
    const auto& options = cc->Options<InferenceCalculatorOptions>();
    RET_CHECK(!options.model_path().empty())
        << "Gate B OpenVINO calculator requires model_path.";
    MP_RETURN_IF_ERROR(TensorContractCheck(cc));
    return absl::OkStatus();
  }

  absl::Status Open(CalculatorContext* cc) override {
    const auto& options = cc->Options<InferenceCalculatorOptions>();
    model_path_ = options.model_path();
    const std::string filename =
        std::filesystem::path(model_path_).filename().string();
    if (filename == "pose_detector.tflite") {
      kind_ = "detector";
      input_shape_ = {1, 224, 224, 3};
      expected_output_shapes_ = {{1, 2254, 12}, {1, 2254, 1}};
    } else if (filename == "pose_landmarks_detector.tflite") {
      kind_ = "landmark";
      input_shape_ = {1, 256, 256, 3};
      expected_output_shapes_ = {{1, 195}, {1, 1}, {1, 256, 256, 1},
                                 {1, 64, 64, 39}, {1, 117}};
    } else {
      return absl::InvalidArgumentError(
          "Gate B refuses unexpected model filename: " + filename);
    }

    try {
      model_ = core_.read_model(model_path_);
      if (model_->inputs().size() != 1 ||
          !SameShape(MediaPipeShape(input_shape_), model_->input().get_shape())) {
        return absl::InvalidArgumentError(
            "Gate B model input contract mismatch.");
      }
      MP_RETURN_IF_ERROR(RequireF32(model_->input(), "model input"));
      if (model_->outputs().size() != expected_output_shapes_.size()) {
        return absl::InvalidArgumentError(
            "Gate B model output count mismatch.");
      }

      // Diagnostic/reference interpreter. Besides the first-frame raw parity
      // check, its native output order is the authority for the tensor vector
      // returned to MediaPipe. This avoids re-inventing model output ordering.
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
          !SameShape(shadow_input->dims, input_shape_)) {
        return absl::InternalError(
            "Gate B raw-parity shadow input contract mismatch.");
      }
      if (shadow_interpreter_->outputs().size() !=
          expected_output_shapes_.size()) {
        return absl::InternalError(
            "Gate B raw-parity shadow output count mismatch.");
      }

      openvino_port_by_tflite_ordinal_.assign(
          shadow_interpreter_->outputs().size(), -1);
      output_shapes_in_tflite_order_.clear();
      output_shapes_in_tflite_order_.reserve(shadow_interpreter_->outputs().size());
      for (size_t ordinal = 0;
           ordinal < shadow_interpreter_->outputs().size(); ++ordinal) {
        const TfLiteTensor* tensor = shadow_interpreter_->tensor(
            shadow_interpreter_->outputs()[ordinal]);
        if (tensor == nullptr || tensor->type != kTfLiteFloat32) {
          return absl::InternalError(
              "Gate B raw-parity shadow output type mismatch.");
        }
        const ov::Shape tflite_shape = TfLiteShape(*tensor);
        int expected_match = -1;
        for (size_t expected = 0; expected < expected_output_shapes_.size();
             ++expected) {
          if (SameShape(tflite_shape, expected_output_shapes_[expected])) {
            if (expected_match != -1) {
              return absl::InternalError(
                  "Gate B raw-parity shadow found ambiguous output shapes.");
            }
            expected_match = static_cast<int>(expected);
          }
        }
        if (expected_match < 0) {
          return absl::InternalError(
              "Gate B raw-parity shadow expected output shape missing.");
        }

        int openvino_port = -1;
        for (size_t port = 0; port < model_->outputs().size(); ++port) {
          if (SameShape(model_->output(port).get_shape(), tflite_shape)) {
            if (openvino_port != -1) {
              return absl::InvalidArgumentError("Ambiguous OpenVINO output shape.");
            }
            MP_RETURN_IF_ERROR(RequireF32(model_->output(port), "model output"));
            openvino_port = static_cast<int>(port);
          }
        }
        if (openvino_port < 0) {
          return absl::InvalidArgumentError(
              "Expected OpenVINO output shape missing.");
        }
        openvino_port_by_tflite_ordinal_[ordinal] = openvino_port;
        output_shapes_in_tflite_order_.push_back(tflite_shape);
      }

      ov::AnyMap config = {
          {ov::hint::performance_mode.name(), ov::hint::PerformanceMode::LATENCY},
          {ov::hint::inference_precision.name(), ov::element::f32}};
      compiled_ = core_.compile_model(model_, "CPU", config);
      request_ = compiled_.create_infer_request();
      input_tensor_ = ov::Tensor(ov::element::f32, input_shape_);
      request_.set_input_tensor(input_tensor_);

      MP_ASSIGN_OR_RETURN(
          auto tensor_names,
          InferenceIoMapper::GetInputOutputTensorNamesFromInterpreter(
              *shadow_interpreter_));
      MP_RETURN_IF_ERROR(UpdateIoMapping(cc, tensor_names));
    } catch (const std::exception& e) {
      return absl::InternalError(
          std::string("OpenVINO Gate B initialization failed: ") + e.what());
    }
    return absl::OkStatus();
  }

 private:
  absl::StatusOr<std::vector<Tensor>> Process(
      CalculatorContext* cc, const TensorSpan& tensor_span) override {
    if (tensor_span.size() != 1) {
      return absl::InvalidArgumentError("Gate B expects one input tensor.");
    }
    const Tensor& input = tensor_span[0];
    if (input.element_type() != Tensor::ElementType::kFloat32 ||
        !SameShape(input.shape(), input_shape_)) {
      return absl::InvalidArgumentError(
          "Gate B MediaPipe input contract mismatch.");
    }

    golden_needle_gate_b::InferenceSample sample;
    sample.model_kind = kind_;

    try {
      auto start = std::chrono::steady_clock::now();
      auto input_view = input.GetCpuReadView();
      const size_t input_bytes = ov::shape_size(input_shape_) * sizeof(float);
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
        for (size_t ordinal = 0;
             ordinal < output_shapes_in_tflite_order_.size(); ++ordinal) {
          const int port = openvino_port_by_tflite_ordinal_[ordinal];
          const auto ov_output = request_.get_output_tensor(port);
          const float* shadow_output =
              shadow_interpreter_->typed_output_tensor<float>(ordinal);
          if (shadow_output == nullptr) {
            return absl::InternalError(
                "Gate B raw-parity shadow output tensor is unavailable.");
          }
          PrintRawParity(kind_, ordinal, port,
                         ov_output.data<const float>(), shadow_output,
                         ov::shape_size(output_shapes_in_tflite_order_[ordinal]));
        }
        shadow_compared_ = true;
      }

      start = std::chrono::steady_clock::now();
      std::vector<Tensor> outputs;
      outputs.reserve(output_shapes_in_tflite_order_.size());
      for (size_t ordinal = 0;
           ordinal < output_shapes_in_tflite_order_.size(); ++ordinal) {
        const int port = openvino_port_by_tflite_ordinal_[ordinal];
        const auto ov_output = request_.get_output_tensor(port);
        const ov::Shape& expected_shape =
            output_shapes_in_tflite_order_[ordinal];
        const size_t expected_bytes =
            ov::shape_size(expected_shape) * sizeof(float);
        if (ov_output.get_element_type() != ov::element::f32 ||
            !SameShape(ov_output.get_shape(), expected_shape) ||
            ov_output.get_byte_size() != expected_bytes) {
          return absl::InternalError(
              "Gate B runtime OpenVINO output type/shape/size mismatch.");
        }

        Tensor mp_tensor(Tensor::ElementType::kFloat32,
                         MediaPipeShape(expected_shape),
                         /*memory_manager=*/nullptr,
                         tflite::kDefaultTensorAlignment);
        {
          auto write_view = mp_tensor.GetCpuWriteView();
          std::memcpy(write_view.buffer<float>(),
                      ov_output.data<const float>(), expected_bytes);
        }
        outputs.push_back(std::move(mp_tensor));
      }
      sample.output_copy_ms = Ms(start);
      golden_needle_gate_b::AddInferenceSample(sample);
      return outputs;
    } catch (const std::exception& e) {
      return absl::InternalError(
          std::string("OpenVINO Gate B inference failed: ") + e.what());
    }
  }

  std::string model_path_;
  std::string kind_;
  ov::Shape input_shape_;
  std::vector<ov::Shape> expected_output_shapes_;
  std::vector<ov::Shape> output_shapes_in_tflite_order_;
  std::vector<int> openvino_port_by_tflite_ordinal_;
  ov::Core core_;
  std::shared_ptr<ov::Model> model_;
  ov::CompiledModel compiled_;
  ov::InferRequest request_;
  ov::Tensor input_tensor_;

  std::unique_ptr<tflite::FlatBufferModel> shadow_model_;
  std::unique_ptr<tflite::Interpreter> shadow_interpreter_;
  bool shadow_compared_ = false;
};

}  // namespace api2
}  // namespace mediapipe
