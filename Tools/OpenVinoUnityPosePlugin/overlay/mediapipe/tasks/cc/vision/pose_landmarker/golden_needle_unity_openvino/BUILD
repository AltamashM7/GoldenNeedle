# Copyright 2026 Golden Needle contributors.
# Licensed under the Apache License, Version 2.0.

package(default_visibility = ["//visibility:public"])
licenses(["notice"])

cc_library(
    name = "abi",
    hdrs = ["golden_needle_openvino_pose.h"],
)

cc_library(
    name = "runtime_telemetry",
    srcs = ["runtime_telemetry.cc"],
    hdrs = ["runtime_telemetry.h"],
)

cc_library(
    name = "runtime_openvino_inference_calculator",
    srcs = ["runtime_openvino_inference_calculator.cc"],
    deps = [
        ":runtime_telemetry",
        "//mediapipe/calculators/tensor:inference_calculator_cc_proto",
        "//mediapipe/calculators/tensor:inference_calculator_interface",
        "//mediapipe/calculators/tensor:inference_io_mapper",
        "//mediapipe/calculators/tensor:tensor_span",
        "//mediapipe/framework:calculator_framework",
        "//mediapipe/framework/formats:tensor",
        "//mediapipe/framework/port:ret_check",
        "//mediapipe/framework/port:status",
        "//mediapipe/tasks/cc/core:mediapipe_builtin_op_resolver",
        "@com_google_absl//absl/status",
        "@com_google_absl//absl/status:statusor",
        "@openvino_gate_b//:openvino",
        "@org_tensorflow//tensorflow/lite:framework_stable",
        "@org_tensorflow//tensorflow/lite:util",
        "@org_tensorflow//tensorflow/lite/kernels:builtin_ops",
    ],
    alwayslink = 1,
)

cc_library(
    name = "plugin_impl",
    srcs = ["golden_needle_openvino_pose_plugin.cc"],
    defines = ["GNOVPOSE_EXPORTS"],
    deps = [
        ":abi",
        ":runtime_openvino_inference_calculator",
        ":runtime_telemetry",
        "//mediapipe/framework:calculator_cc_proto",
        "//mediapipe/framework:calculator_framework",
        "//mediapipe/framework:packet",
        "//mediapipe/framework:timestamp",
        "//mediapipe/framework/api2:builder",
        "//mediapipe/framework/formats:image",
        "//mediapipe/framework/formats:image_frame",
        "//mediapipe/framework/formats:landmark_cc_proto",
        "//mediapipe/framework/formats:rect_cc_proto",
        "//mediapipe/tasks/cc/core:mediapipe_builtin_op_resolver",
        "//mediapipe/tasks/cc/core:task_runner",
        "//mediapipe/tasks/cc/vision/pose_detector/proto:pose_detector_graph_options_cc_proto",
        "//mediapipe/tasks/cc/vision/pose_landmarker:pose_landmarker_graph",
        "//mediapipe/tasks/cc/vision/pose_landmarker/proto:pose_landmarker_graph_options_cc_proto",
        "//mediapipe/tasks/cc/vision/pose_landmarker/proto:pose_landmarks_detector_graph_options_cc_proto",
        "@com_google_absl//absl/status",
        "@openvino_gate_b//:openvino",
    ],
    alwayslink = 1,
)

cc_binary(
    name = "golden_needle_openvino_pose.dll",
    linkshared = True,
    linkstatic = True,
    deps = [":plugin_impl"],
)

cc_binary(
    name = "runtime_pose_smoke",
    srcs = ["runtime_pose_smoke.cc"],
    deps = [
        ":abi",
        "//mediapipe/framework/port:opencv_core",
        "//mediapipe/framework/port:opencv_imgcodecs",
        "//mediapipe/framework/port:opencv_imgproc",
    ],
)
