#include <algorithm>
#include <chrono>
#include <cstring>
#include <filesystem>
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
#include "mediapipe/tasks/cc/vision/pose_landmarker/golden_needle_unity_openvino/runtime_telemetry.h"
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

bool SameShape(const TfLiteIntArray* dims, const ov::Shape& shape) {
  if (dims == nullptr || dims->size != static_cast<int>(shape.size())) return false;
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

absl::Status RequireF32(const ov::Output<const ov::Node>& port, const char* label) {
  return port.get_element_type() == ov::element::f32
             ? absl::OkStatus()
             : absl::InvalidArgumentError(std::string(label) + " must be float32.");
}

}  // namespace

// Keep the exact calculator name expected by the proven ModelTaskGraph seam.
// Unlike Gate B, this runtime implementation never invokes a shadow TFLite
// interpreter. TFLite is used only as model metadata authority for the exact
// output ordering and tensor names that MediaPipe's InferenceIoMapper expects.
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
        << "Golden Needle OpenVINO runtime requires a file-backed model path.";
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
      expected_output_shapes_ = {
          {1, 195}, {1, 1}, {1, 256, 256, 1}, {1, 64, 64, 39}, {1, 117}};
    } else {
      return absl::InvalidArgumentError(
          "Golden Needle OpenVINO runtime refuses unexpected model filename: " +
          filename);
    }

    try {
      model_ = core_.read_model(model_path_);
      if (model_->inputs().size() != 1 ||
          !SameShape(MediaPipeShape(input_shape_), model_->input().get_shape())) {
        return absl::InvalidArgumentError("OpenVINO model input contract mismatch.");
      }
      MP_RETURN_IF_ERROR(RequireF32(model_->input(), "model input"));
      if (model_->outputs().size() != expected_output_shapes_.size()) {
        return absl::InvalidArgumentError("OpenVINO model output count mismatch.");
      }

      metadata_model_ =
          tflite::FlatBufferModel::BuildFromFile(model_path_.c_str());
      if (!metadata_model_) {
        return absl::InternalError("Could not load TFLite model metadata.");
      }
      tasks::core::MediaPipeBuiltinOpResolver resolver;
      tflite::InterpreterBuilder builder(*metadata_model_, resolver);
      if (builder(&metadata_interpreter_) != kTfLiteOk ||
          !metadata_interpreter_) {
        return absl::InternalError("Could not create TFLite metadata interpreter.");
      }
      if (metadata_interpreter_->AllocateTensors() != kTfLiteOk) {
        return absl::InternalError("Could not allocate TFLite metadata tensors.");
      }
      if (metadata_interpreter_->inputs().size() != 1) {
        return absl::InternalError("Expected one TFLite input.");
      }

      const TfLiteTensor* metadata_input = metadata_interpreter_->tensor(
          metadata_interpreter_->inputs()[0]);
      if (metadata_input == nullptr || metadata_input->type != kTfLiteFloat32 ||
          !SameShape(metadata_input->dims, input_shape_)) {
        return absl::InternalError("TFLite metadata input contract mismatch.");
      }
      if (metadata_interpreter_->outputs().size() !=
          expected_output_shapes_.size()) {
        return absl::InternalError("TFLite metadata output count mismatch.");
      }

      openvino_port_by_tflite_ordinal_.assign(
          metadata_interpreter_->outputs().size(), -1);
      output_shapes_in_tflite_order_.clear();
      output_shapes_in_tflite_order_.reserve(
          metadata_interpreter_->outputs().size());

      for (size_t ordinal = 0;
           ordinal < metadata_interpreter_->outputs().size(); ++ordinal) {
        const TfLiteTensor* tensor = metadata_interpreter_->tensor(
            metadata_interpreter_->outputs()[ordinal]);
        if (tensor == nullptr || tensor->type != kTfLiteFloat32) {
          return absl::InternalError("TFLite metadata output type mismatch.");
        }
        const ov::Shape tflite_shape = TfLiteShape(*tensor);

        int expected_match = -1;
        for (size_t expected = 0; expected < expected_output_shapes_.size();
             ++expected) {
          if (tflite_shape == expected_output_shapes_[expected]) {
            if (expected_match != -1) {
              return absl::InternalError("Ambiguous expected output shape.");
            }
            expected_match = static_cast<int>(expected);
          }
        }
        if (expected_match < 0) {
          return absl::InternalError("Unexpected TFLite metadata output shape.");
        }

        int openvino_port = -1;
        for (size_t port = 0; port < model_->outputs().size(); ++port) {
          if (model_->output(port).get_shape() == tflite_shape) {
            if (openvino_port != -1) {
              return absl::InvalidArgumentError("Ambiguous OpenVINO output shape.");
            }
            MP_RETURN_IF_ERROR(RequireF32(model_->output(port), "model output"));
            openvino_port = static_cast<int>(port);
          }
        }
        if (openvino_port < 0) {
          return absl::InvalidArgumentError("Expected OpenVINO output shape missing.");
        }

        openvino_port_by_tflite_ordinal_[ordinal] = openvino_port;
        output_shapes_in_tflite_order_.push_back(tflite_shape);
      }

      ov::AnyMap config = {
          {ov::hint::performance_mode.name(),
           ov::hint::PerformanceMode::LATENCY},
          {ov::hint::inference_precision.name(), ov::element::f32}};
      compiled_ = core_.compile_model(model_, "CPU", config);
      request_ = compiled_.create_infer_request();
      input_tensor_ = ov::Tensor(ov::element::f32, input_shape_);
      request_.set_input_tensor(input_tensor_);

      MP_ASSIGN_OR_RETURN(
          auto tensor_names,
          InferenceIoMapper::GetInputOutputTensorNamesFromInterpreter(
              *metadata_interpreter_));
      MP_RETURN_IF_ERROR(UpdateIoMapping(cc, tensor_names));
    } catch (const std::exception& e) {
      return absl::InternalError(
          std::string("OpenVINO runtime initialization failed: ") + e.what());
    }
    return absl::OkStatus();
  }

 private:
  absl::StatusOr<std::vector<Tensor>> Process(
      CalculatorContext* cc, const TensorSpan& tensor_span) override {
    (void)cc;
    if (tensor_span.size() != 1) {
      return absl::InvalidArgumentError("Expected one MediaPipe input tensor.");
    }
    const Tensor& input = tensor_span[0];
    if (input.element_type() != Tensor::ElementType::kFloat32 ||
        !SameShape(input.shape(), input_shape_)) {
      return absl::InvalidArgumentError("MediaPipe input tensor contract mismatch.");
    }

    golden_needle_unity_openvino::InferenceSample sample;
    sample.model_kind = kind_;

    try {
      auto start = std::chrono::steady_clock::now();
      auto input_view = input.GetCpuReadView();
      const size_t input_bytes = ov::shape_size(input_shape_) * sizeof(float);
      std::memcpy(input_tensor_.data<float>(),
                  input_view.buffer<float>(), input_bytes);
      sample.input_copy_ms = Ms(start);

      start = std::chrono::steady_clock::now();
      request_.infer();
      sample.inference_ms = Ms(start);

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
            ov_output.get_shape() != expected_shape ||
            ov_output.get_byte_size() != expected_bytes) {
          return absl::InternalError(
              "OpenVINO output type/shape/size mismatch.");
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
      golden_needle_unity_openvino::AddInferenceSample(sample);
      return outputs;
    } catch (const std::exception& e) {
      return absl::InternalError(
          std::string("OpenVINO runtime inference failed: ") + e.what());
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
  std::unique_ptr<tflite::FlatBufferModel> metadata_model_;
  std::unique_ptr<tflite::Interpreter> metadata_interpreter_;
};

}  // namespace api2
}  // namespace mediapipe
