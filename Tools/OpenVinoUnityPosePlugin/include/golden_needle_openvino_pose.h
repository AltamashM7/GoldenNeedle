#pragma once

#include <stdint.h>

#if defined(_WIN32)
  #if defined(GNOVPOSE_EXPORTS)
    #define GNOVPOSE_API __declspec(dllexport)
  #else
    #define GNOVPOSE_API __declspec(dllimport)
  #endif
  #define GNOVPOSE_CALL __cdecl
#else
  #define GNOVPOSE_API
  #define GNOVPOSE_CALL
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define GNOVPOSE_ABI_VERSION_MAJOR 1u
#define GNOVPOSE_ABI_VERSION_MINOR 1u
#define GNOVPOSE_ABI_VERSION ((GNOVPOSE_ABI_VERSION_MAJOR << 16u) | GNOVPOSE_ABI_VERSION_MINOR)
#define GNOVPOSE_CONFIG_VERSION 1u
#define GNOVPOSE_POSE_ENGINE_CONFIG_VERSION 1u
#define GNOVPOSE_POSE_RESULT_VERSION 1u
#define GNOVPOSE_LANDMARK_COUNT 33u

typedef enum gnovpose_result {
  GNOVPOSE_OK = 0,
  GNOVPOSE_ERROR_INVALID_ARGUMENT = 1,
  GNOVPOSE_ERROR_ABI_MISMATCH = 2,
  GNOVPOSE_ERROR_UNSUPPORTED_DEVICE = 3,
  GNOVPOSE_ERROR_OPENVINO = 4,
  GNOVPOSE_ERROR_BUFFER_TOO_SMALL = 5,
  GNOVPOSE_ERROR_INTERNAL = 6,
  GNOVPOSE_ERROR_MEDIAPIPE = 7,
  GNOVPOSE_ERROR_TIMESTAMP = 8,
  GNOVPOSE_ERROR_MODEL = 9
} gnovpose_result;

typedef enum gnovpose_backend {
  GNOVPOSE_BACKEND_UNKNOWN = 0,
  GNOVPOSE_BACKEND_OPENVINO_CPU_FP32 = 1
} gnovpose_backend;

typedef enum gnovpose_landmark_flags {
  GNOVPOSE_LANDMARK_HAS_VISIBILITY = 1u << 0u,
  GNOVPOSE_LANDMARK_HAS_PRESENCE = 1u << 1u,
  GNOVPOSE_LANDMARK_HAS_WORLD = 1u << 2u
} gnovpose_landmark_flags;

typedef struct gnovpose_context gnovpose_context;
typedef struct gnovpose_pose_engine gnovpose_pose_engine;

typedef struct gnovpose_config {
  uint32_t struct_size;
  uint32_t abi_version;
  uint32_t config_version;
  const char* device;
} gnovpose_config;

typedef struct gnovpose_pose_engine_config {
  uint32_t struct_size;
  uint32_t abi_version;
  uint32_t config_version;
  const char* detector_model_path;
  const char* landmark_model_path;
} gnovpose_pose_engine_config;

typedef struct gnovpose_landmark {
  float x;
  float y;
  float z;
  float visibility;
  float presence;
  float world_x;
  float world_y;
  float world_z;
  uint32_t flags;
} gnovpose_landmark;

typedef struct gnovpose_pose_result {
  uint32_t struct_size;
  uint32_t version;
  uint32_t backend;
  uint32_t has_pose;
  int64_t timestamp_millisec;
  uint32_t landmark_count;
  uint32_t detector_ran;
  double graph_process_ms;
  double detector_inference_ms;
  double landmark_inference_ms;
  double tensor_input_copy_ms;
  double tensor_output_copy_ms;
  double frame_copy_ms;
  double output_marshal_ms;
  gnovpose_landmark landmarks[GNOVPOSE_LANDMARK_COUNT];
} gnovpose_pose_result;

GNOVPOSE_API uint32_t GNOVPOSE_CALL gnovpose_get_abi_version(void);
GNOVPOSE_API int32_t GNOVPOSE_CALL gnovpose_get_version_string(char* buffer, uint32_t capacity);
GNOVPOSE_API int32_t GNOVPOSE_CALL gnovpose_get_build_info(char* buffer, uint32_t capacity);
GNOVPOSE_API int32_t GNOVPOSE_CALL gnovpose_get_last_error(char* buffer, uint32_t capacity);

GNOVPOSE_API int32_t GNOVPOSE_CALL gnovpose_create(
    const gnovpose_config* config, gnovpose_context** out_context);
GNOVPOSE_API void GNOVPOSE_CALL gnovpose_destroy(gnovpose_context* context);
GNOVPOSE_API int32_t GNOVPOSE_CALL gnovpose_self_test(gnovpose_context* context);
GNOVPOSE_API int32_t GNOVPOSE_CALL gnovpose_get_runtime_info(
    gnovpose_context* context, char* buffer, uint32_t capacity);

GNOVPOSE_API int32_t GNOVPOSE_CALL gnovpose_pose_engine_create(
    gnovpose_context* context,
    const gnovpose_pose_engine_config* config,
    gnovpose_pose_engine** out_engine);
GNOVPOSE_API void GNOVPOSE_CALL gnovpose_pose_engine_destroy(gnovpose_pose_engine* engine);
GNOVPOSE_API int32_t GNOVPOSE_CALL gnovpose_pose_engine_get_info(
    gnovpose_pose_engine* engine, char* buffer, uint32_t capacity);
GNOVPOSE_API int32_t GNOVPOSE_CALL gnovpose_pose_engine_process_rgba(
    gnovpose_pose_engine* engine,
    const uint8_t* rgba,
    int32_t width,
    int32_t height,
    int32_t stride_bytes,
    int32_t rotation_degrees,
    int64_t timestamp_millisec,
    gnovpose_pose_result* out_result);

#ifdef __cplusplus
}
#endif
