// Copyright 2026 Golden Needle contributors.
// Licensed under the Apache License, Version 2.0.
// Research-only Gate B calculator: surrounding pose semantics stay MediaPipe 0.10.22.

#include <chrono>
#include <cstring>
#include <filesystem>
#include <memory>
#include <string>
#include <vector>
#include <openvino/openvino.hpp>
#include <openvino/runtime/properties.hpp>
#include "absl/status/status.h"
#include "mediapipe/calculators/tensor/inference_calculator.pb.h"
#include "mediapipe/tasks/cc/vision/pose_landmarker/golden_needle_gate_b/gate_b_telemetry.h"
#include "mediapipe/framework/calculator_framework.h"
#include "mediapipe/framework/formats/tensor.h"

namespace mediapipe { namespace {
constexpr char kTensorsTag[]="TENSORS";
double Ms(std::chrono::steady_clock::time_point s){return std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-s).count();}
bool SameShape(const Tensor::Shape& a,const ov::Shape& b){if(a.dims.size()!=b.size())return false;for(int i=0;i<a.dims.size();++i)if(a.dims[i]!=static_cast<int>(b[i]))return false;return true;}
bool SameShape(const ov::Shape&a,const ov::Shape&b){return a==b;}
absl::Status F32(const ov::Output<const ov::Node>&p,const char*l){return p.get_element_type()==ov::element::f32?absl::OkStatus():absl::InvalidArgumentError(std::string(l)+" must be float32 for Gate B.");}
}
class GoldenNeedleOpenVinoInferenceCalculator:public CalculatorBase{
 public:
  static absl::Status GetContract(CalculatorContract*cc){cc->Inputs().Tag(kTensorsTag).Set<std::vector<Tensor>>();cc->Outputs().Tag(kTensorsTag).Set<std::vector<Tensor>>();return absl::OkStatus();}
  absl::Status Open(CalculatorContext*cc)override{
    const auto&o=cc->Options<InferenceCalculatorOptions>();if(!o.has_model_path()||o.model_path().empty())return absl::InvalidArgumentError("Gate B OpenVINO calculator requires model_path.");
    model_path_=o.model_path();std::string f=std::filesystem::path(model_path_).filename().string();
    if(f=="pose_detector.tflite"){kind_="detector";input_={1,224,224,3};outputs_={{1,2254,12},{1,2254,1}};}
    else if(f=="pose_landmarks_detector.tflite"){kind_="landmark";input_={1,256,256,3};outputs_={{1,195},{1,1},{1,256,256,1},{1,64,64,39},{1,117}};}
    else return absl::InvalidArgumentError("Gate B refuses unexpected model filename: "+f);
    try{
      model_=core_.read_model(model_path_);if(model_->inputs().size()!=1||!SameShape(Tensor::Shape(std::vector<int>{static_cast<int>(input_[0]),static_cast<int>(input_[1]),static_cast<int>(input_[2]),static_cast<int>(input_[3])}),model_->input().get_shape()))return absl::InvalidArgumentError("Gate B model input contract mismatch.");
      auto st=F32(model_->input(),"model input");if(!st.ok())return st;if(model_->outputs().size()!=outputs_.size())return absl::InvalidArgumentError("Gate B model output count mismatch.");
      indices_.assign(outputs_.size(),-1);for(size_t e=0;e<outputs_.size();++e){for(size_t a=0;a<model_->outputs().size();++a){if(SameShape(model_->output(a).get_shape(),outputs_[e])){if(indices_[e]!=-1)return absl::InvalidArgumentError("Ambiguous output shape.");auto os=F32(model_->output(a),"model output");if(!os.ok())return os;indices_[e]=static_cast<int>(a);}}if(indices_[e]<0)return absl::InvalidArgumentError("Expected output shape missing.");}
      ov::AnyMap cfg={{ov::hint::performance_mode.name(),ov::hint::PerformanceMode::LATENCY},{ov::hint::inference_precision.name(),ov::element::f32}};compiled_=core_.compile_model(model_,"CPU",cfg);request_=compiled_.create_infer_request();input_tensor_=ov::Tensor(ov::element::f32,input_);request_.set_input_tensor(input_tensor_);
    }catch(const std::exception&e){return absl::InternalError(std::string("OpenVINO Gate B initialization failed: ")+e.what());}return absl::OkStatus();}
  absl::Status Process(CalculatorContext*cc)override{
    if(cc->Inputs().Tag(kTensorsTag).IsEmpty())return absl::OkStatus();const auto&ins=cc->Inputs().Tag(kTensorsTag).Get<std::vector<Tensor>>();if(ins.size()!=1)return absl::InvalidArgumentError("Gate B expects one input tensor.");const Tensor&in=ins[0];if(in.element_type()!=Tensor::ElementType::kFloat32||!SameShape(in.shape(),input_))return absl::InvalidArgumentError("Gate B MediaPipe input contract mismatch.");golden_needle_gate_b::InferenceSample s;s.model_kind=kind_;
    try{auto t=std::chrono::steady_clock::now();auto rv=in.GetCpuReadView();std::memcpy(input_tensor_.data<float>(),rv.buffer<float>(),ov::shape_size(input_)*sizeof(float));s.input_copy_ms=Ms(t);t=std::chrono::steady_clock::now();request_.infer();s.inference_ms=Ms(t);t=std::chrono::steady_clock::now();auto outs=std::make_unique<std::vector<Tensor>>();for(size_t i=0;i<outputs_.size();++i){auto ovo=request_.get_output_tensor(indices_[i]);std::vector<int>d;for(auto x:outputs_[i])d.push_back(static_cast<int>(x));Tensor mp(Tensor::ElementType::kFloat32,Tensor::Shape(d));auto w=mp.GetCpuWriteView();std::memcpy(w.buffer<float>(),ovo.data<const float>(),ovo.get_byte_size());outs->push_back(std::move(mp));}s.output_copy_ms=Ms(t);golden_needle_gate_b::AddInferenceSample(s);cc->Outputs().Tag(kTensorsTag).Add(outs.release(),cc->InputTimestamp());}catch(const std::exception&e){return absl::InternalError(std::string("OpenVINO Gate B inference failed: ")+e.what());}return absl::OkStatus();}
 private:std::string model_path_,kind_;ov::Shape input_;std::vector<ov::Shape>outputs_;std::vector<int>indices_;ov::Core core_;std::shared_ptr<ov::Model>model_;ov::CompiledModel compiled_;ov::InferRequest request_;ov::Tensor input_tensor_;
};
REGISTER_CALCULATOR(GoldenNeedleOpenVinoInferenceCalculator);
} // namespace mediapipe
