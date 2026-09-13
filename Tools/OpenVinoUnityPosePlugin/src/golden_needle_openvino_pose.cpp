#include "golden_needle_openvino_pose.h"

#include <openvino/openvino.hpp>

#include <cstring>
#include <memory>
#include <sstream>
#include <string>

#ifndef GNOVPOSE_PLUGIN_VERSION
#define GNOVPOSE_PLUGIN_VERSION "0.1.0"
#endif
#ifndef GNOVPOSE_OPENVINO_PIN
#define GNOVPOSE_OPENVINO_PIN "2026.3.0"
#endif
#ifndef GNOVPOSE_MEDIAPIPE_PIN
#define GNOVPOSE_MEDIAPIPE_PIN "0.10.22"
#endif

struct gnovpose_context {
  std::unique_ptr<ov::Core> core;
  std::string device;
};

namespace {
thread_local std::string g_last_error;

void SetError(const std::string& value) { g_last_error = value; }
void ClearError() { g_last_error.clear(); }

int32_t CopyString(const std::string& value, char* buffer, uint32_t capacity) {
  const uint64_t required = static_cast<uint64_t>(value.size()) + 1u;
  if (required > UINT32_MAX) {
    SetError("String result exceeds ABI capacity range.");
    return GNOVPOSE_ERROR_INTERNAL;
  }
  if (buffer == nullptr || capacity < required) {
    SetError("Output buffer is null or too small; required bytes=" + std::to_string(required));
    return GNOVPOSE_ERROR_BUFFER_TOO_SMALL;
  }
  std::memcpy(buffer, value.c_str(), static_cast<size_t>(required));
  return GNOVPOSE_OK;
}

std::string RuntimeInfo(gnovpose_context* context) {
  const ov::Version version = ov::get_openvino_version();
  const auto versions = context->core->get_versions(context->device);
  std::ostringstream out;
  out << "plugin=" << GNOVPOSE_PLUGIN_VERSION
      << ";abi=" << GNOVPOSE_ABI_VERSION_MAJOR << "." << GNOVPOSE_ABI_VERSION_MINOR
      << ";mediapipe_pin=" << GNOVPOSE_MEDIAPIPE_PIN
      << ";openvino_pin=" << GNOVPOSE_OPENVINO_PIN
      << ";openvino_build=" << version.buildNumber
      << ";device=" << context->device;
  if (!versions.empty()) {
    out << ";device_plugin_build=" << versions.begin()->second.buildNumber;
  }
  return out.str();
}

bool RuntimeMatchesPin() {
  const ov::Version version = ov::get_openvino_version();
  const std::string build = version.buildNumber == nullptr ? "" : version.buildNumber;
  return build.find(GNOVPOSE_OPENVINO_PIN) != std::string::npos;
}
}  // namespace

extern "C" {

uint32_t GNOVPOSE_CALL gnovpose_get_abi_version(void) { return GNOVPOSE_ABI_VERSION; }

int32_t GNOVPOSE_CALL gnovpose_get_version_string(char* buffer, uint32_t capacity) {
  ClearError();
  return CopyString(GNOVPOSE_PLUGIN_VERSION, buffer, capacity);
}

int32_t GNOVPOSE_CALL gnovpose_get_build_info(char* buffer, uint32_t capacity) {
  ClearError();
  const std::string info = std::string("plugin=") + GNOVPOSE_PLUGIN_VERSION +
      ";abi=1.0;mediapipe_pin=" + GNOVPOSE_MEDIAPIPE_PIN +
      ";openvino_pin=" + GNOVPOSE_OPENVINO_PIN + ";backend=CPU_FP32";
  return CopyString(info, buffer, capacity);
}

int32_t GNOVPOSE_CALL gnovpose_get_last_error(char* buffer, uint32_t capacity) {
  const std::string snapshot = g_last_error;
  if (buffer == nullptr || capacity < snapshot.size() + 1u) {
    return GNOVPOSE_ERROR_BUFFER_TOO_SMALL;
  }
  std::memcpy(buffer, snapshot.c_str(), snapshot.size() + 1u);
  return GNOVPOSE_OK;
}

int32_t GNOVPOSE_CALL gnovpose_create(const gnovpose_config* config, gnovpose_context** out_context) {
  ClearError();
  if (config == nullptr || out_context == nullptr) {
    SetError("config and out_context are required.");
    return GNOVPOSE_ERROR_INVALID_ARGUMENT;
  }
  *out_context = nullptr;
  if (config->struct_size != sizeof(gnovpose_config) ||
      config->abi_version != GNOVPOSE_ABI_VERSION ||
      config->config_version != GNOVPOSE_CONFIG_VERSION) {
    SetError("ABI/config version mismatch.");
    return GNOVPOSE_ERROR_ABI_MISMATCH;
  }
  const std::string device = config->device == nullptr ? "CPU" : config->device;
  if (device != "CPU") {
    SetError("U1/U2 contract supports only explicit CPU; no AUTO/GPU fallback is allowed.");
    return GNOVPOSE_ERROR_UNSUPPORTED_DEVICE;
  }

  try {
    auto context = std::make_unique<gnovpose_context>();
    context->core = std::make_unique<ov::Core>();
    context->device = device;
    (void)context->core->get_versions(device);
    *out_context = context.release();
    return GNOVPOSE_OK;
  } catch (const std::exception& ex) {
    SetError(std::string("OpenVINO context creation failed: ") + ex.what());
    return GNOVPOSE_ERROR_OPENVINO;
  } catch (...) {
    SetError("OpenVINO context creation failed with an unknown exception.");
    return GNOVPOSE_ERROR_INTERNAL;
  }
}

void GNOVPOSE_CALL gnovpose_destroy(gnovpose_context* context) { delete context; }

int32_t GNOVPOSE_CALL gnovpose_self_test(gnovpose_context* context) {
  ClearError();
  if (context == nullptr || context->core == nullptr) {
    SetError("context is null.");
    return GNOVPOSE_ERROR_INVALID_ARGUMENT;
  }
  if (context->device != "CPU") {
    SetError("context device is not explicit CPU.");
    return GNOVPOSE_ERROR_UNSUPPORTED_DEVICE;
  }
  try {
    if (!RuntimeMatchesPin()) {
      SetError(std::string("Loaded OpenVINO runtime does not match required pin ") +
               GNOVPOSE_OPENVINO_PIN + ". Actual build: " + ov::get_openvino_version().buildNumber);
      return GNOVPOSE_ERROR_OPENVINO;
    }
    const auto versions = context->core->get_versions("CPU");
    if (versions.empty()) {
      SetError("OpenVINO CPU device plugin returned no version information.");
      return GNOVPOSE_ERROR_OPENVINO;
    }
    return GNOVPOSE_OK;
  } catch (const std::exception& ex) {
    SetError(std::string("OpenVINO self-test failed: ") + ex.what());
    return GNOVPOSE_ERROR_OPENVINO;
  } catch (...) {
    SetError("OpenVINO self-test failed with an unknown exception.");
    return GNOVPOSE_ERROR_INTERNAL;
  }
}

int32_t GNOVPOSE_CALL gnovpose_get_runtime_info(
    gnovpose_context* context, char* buffer, uint32_t capacity) {
  ClearError();
  if (context == nullptr || context->core == nullptr) {
    SetError("context is null.");
    return GNOVPOSE_ERROR_INVALID_ARGUMENT;
  }
  try {
    return CopyString(RuntimeInfo(context), buffer, capacity);
  } catch (const std::exception& ex) {
    SetError(std::string("Runtime-info query failed: ") + ex.what());
    return GNOVPOSE_ERROR_OPENVINO;
  } catch (...) {
    SetError("Runtime-info query failed with an unknown exception.");
    return GNOVPOSE_ERROR_INTERNAL;
  }
}

}  // extern "C"
