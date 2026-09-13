# Copyright 2026 Golden Needle contributors.
# Licensed under the Apache License, Version 2.0.
#
# Isolated Gate B research targets. This directory is copied only into the
# ignored MediaPipe 0.10.22 workspace; it is not part of production Unity.

package(default_visibility = ["//visibility:public"])
licenses(["notice"])

cc_library(
    name = "gate_b_telemetry",
    srcs = ["gate_b_telemetry.cc"],
    hdrs = ["gate_b_telemetry.h"],
)

cc_library(
    name = "openvino_inference_calculator",
    srcs = ["openvino_inference_calculator.cc"],
    deps = [
        ":gate_b_telemetry",
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

cc_binary(
    name = "parity_runner",
    srcs = ["parity_runner.cc"],
    deps = [
        ":gate_b_telemetry",
        ":openvino_inference_calculator",
        "//mediapipe/framework:calculator_cc_proto",
        "//mediapipe/framework:calculator_framework",
        "//mediapipe/framework:packet",
        "//mediapipe/framework:timestamp",
        "//mediapipe/framework/api2:builder",
        "//mediapipe/framework/formats:detection_cc_proto",
        "//mediapipe/framework/formats:image",
        "//mediapipe/framework/formats:image_frame",
        "//mediapipe/framework/formats:landmark_cc_proto",
        "//mediapipe/framework/formats:rect_cc_proto",
        "//mediapipe/framework/port:opencv_core",
        "//mediapipe/framework/port:opencv_imgcodecs",
        "//mediapipe/framework/port:opencv_imgproc",
        "//mediapipe/framework/port:opencv_videoio",
        "//mediapipe/tasks/cc/core:base_options",
        "//mediapipe/tasks/cc/core:task_runner",
        "//mediapipe/tasks/cc/vision/core:running_mode",
        "//mediapipe/tasks/cc/vision/pose_detector/proto:pose_detector_graph_options_cc_proto",
        "//mediapipe/tasks/cc/vision/pose_landmarker:pose_landmarker",
        "//mediapipe/tasks/cc/vision/pose_landmarker:pose_landmarker_graph",
        "//mediapipe/tasks/cc/vision/pose_landmarker/proto:pose_landmarker_graph_options_cc_proto",
        "//mediapipe/tasks/cc/vision/pose_landmarker/proto:pose_landmarks_detector_graph_options_cc_proto",
        "@com_google_absl//absl/status",
    ],
)
