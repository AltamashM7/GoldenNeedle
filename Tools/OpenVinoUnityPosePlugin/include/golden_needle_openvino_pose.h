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
#define GNOVPOSE_ABI_VERSION_MINOR 0u
#define GNOVPOSE_ABI_VERSION ((GNOVPOSE_ABI_VERSION_MAJOR << 16u) | GNOVPOSE_ABI_VERSION_MINOR)
#define GNOVPOSE_CONFIG_VERSION 1u

typedef enum gnovpose_result {
  GNOVPOSE_OK = 0,
  GNOVPOSE_ERROR_INVALID_ARGUMENT = 1,
  GNOVPOSE_ERROR_ABI_MISMATCH = 2,
  GNOVPOSE_ERROR_UNSUPPORTED_DEVICE = 3,
  GNOVPOSE_ERROR_OPENVINO = 4,
  GNOVPOSE_ERROR_BUFFER_TOO_SMALL = 5,
  GNOVPOSE_ERROR_INTERNAL = 6
} gnovpose_result;

typedef struct gnovpose_context gnovpose_context;

typedef struct gnovpose_config {
  uint32_t struct_size;
  uint32_t abi_version;
  uint32_t config_version;
  const char* device;
} gnovpose_config;

GNOVPOSE_API uint32_t GNOVPOSE_CALL gnovpose_get_abi_version(void);
GNOVPOSE_API int32_t GNOVPOSE_CALL gnovpose_get_version_string(char* buffer, uint32_t capacity);
GNOVPOSE_API int32_t GNOVPOSE_CALL gnovpose_get_build_info(char* buffer, uint32_t capacity);
GNOVPOSE_API int32_t GNOVPOSE_CALL gnovpose_get_last_error(char* buffer, uint32_t capacity);
GNOVPOSE_API int32_t GNOVPOSE_CALL gnovpose_create(const gnovpose_config* config, gnovpose_context** out_context);
GNOVPOSE_API void GNOVPOSE_CALL gnovpose_destroy(gnovpose_context* context);
GNOVPOSE_API int32_t GNOVPOSE_CALL gnovpose_self_test(gnovpose_context* context);
GNOVPOSE_API int32_t GNOVPOSE_CALL gnovpose_get_runtime_info(
    gnovpose_context* context, char* buffer, uint32_t capacity);

#ifdef __cplusplus
}
#endif
