#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include "golden_needle_openvino_pose.h"

#include <filesystem>
#include <iostream>
#include <string>

namespace {
template <typename T>
T LoadProc(HMODULE module, const char* name) {
  const auto proc = reinterpret_cast<T>(GetProcAddress(module, name));
  if (proc == nullptr) {
    std::cerr << "Missing export: " << name << " win32=" << GetLastError() << "\n";
  }
  return proc;
}

std::string LastErrorText(int32_t (GNOVPOSE_CALL *get_last_error)(char*, uint32_t)) {
  char buffer[2048] = {};
  if (get_last_error(buffer, sizeof(buffer)) == GNOVPOSE_OK) return buffer;
  return "<last-error unavailable>";
}
}

int wmain(int argc, wchar_t** argv) {
  if (argc != 2) {
    std::wcerr << L"usage: gnovpose_smoke.exe <absolute-path-to-golden_needle_openvino_pose.dll>\n";
    return 64;
  }
  const std::filesystem::path dll_path = std::filesystem::absolute(argv[1]);
  const std::wstring dir = dll_path.parent_path().wstring();

  if (!SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_USER_DIRS)) {
    std::cerr << "SetDefaultDllDirectories failed: " << GetLastError() << "\n";
    return 2;
  }
  DLL_DIRECTORY_COOKIE cookie = AddDllDirectory(dir.c_str());
  if (cookie == nullptr) {
    std::cerr << "AddDllDirectory failed: " << GetLastError() << "\n";
    return 3;
  }

  HMODULE module = LoadLibraryExW(
      dll_path.c_str(), nullptr, LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_USER_DIRS);
  if (module == nullptr) {
    std::cerr << "LoadLibraryExW failed: " << GetLastError() << "\n";
    RemoveDllDirectory(cookie);
    return 4;
  }

  using GetAbi = uint32_t (GNOVPOSE_CALL *)(void);
  using GetText = int32_t (GNOVPOSE_CALL *)(char*, uint32_t);
  using Create = int32_t (GNOVPOSE_CALL *)(const gnovpose_config*, gnovpose_context**);
  using Destroy = void (GNOVPOSE_CALL *)(gnovpose_context*);
  using SelfTest = int32_t (GNOVPOSE_CALL *)(gnovpose_context*);
  using GetRuntimeInfo = int32_t (GNOVPOSE_CALL *)(gnovpose_context*, char*, uint32_t);

  const auto get_abi = LoadProc<GetAbi>(module, "gnovpose_get_abi_version");
  const auto get_version = LoadProc<GetText>(module, "gnovpose_get_version_string");
  const auto get_build = LoadProc<GetText>(module, "gnovpose_get_build_info");
  const auto get_last_error = LoadProc<GetText>(module, "gnovpose_get_last_error");
  const auto create = LoadProc<Create>(module, "gnovpose_create");
  const auto destroy = LoadProc<Destroy>(module, "gnovpose_destroy");
  const auto self_test = LoadProc<SelfTest>(module, "gnovpose_self_test");
  const auto get_runtime = LoadProc<GetRuntimeInfo>(module, "gnovpose_get_runtime_info");
  if (!get_abi || !get_version || !get_build || !get_last_error || !create || !destroy ||
      !self_test || !get_runtime) {
    FreeLibrary(module);
    RemoveDllDirectory(cookie);
    return 5;
  }
  if (get_abi() != GNOVPOSE_ABI_VERSION) {
    std::cerr << "ABI mismatch: " << get_abi() << " != " << GNOVPOSE_ABI_VERSION << "\n";
    FreeLibrary(module);
    RemoveDllDirectory(cookie);
    return 6;
  }

  char text[2048] = {};
  if (get_version(text, sizeof(text)) != GNOVPOSE_OK) return 7;
  std::cout << "plugin_version=" << text << "\n";
  if (get_build(text, sizeof(text)) != GNOVPOSE_OK) return 8;
  std::cout << "build_info=" << text << "\n";

  gnovpose_config config{};
  config.struct_size = sizeof(config);
  config.abi_version = GNOVPOSE_ABI_VERSION;
  config.config_version = GNOVPOSE_CONFIG_VERSION;
  config.device = "CPU";
  gnovpose_context* context = nullptr;
  int32_t result = create(&config, &context);
  if (result != GNOVPOSE_OK || context == nullptr) {
    std::cerr << "create failed result=" << result << " error=" << LastErrorText(get_last_error) << "\n";
    FreeLibrary(module);
    RemoveDllDirectory(cookie);
    return 9;
  }
  result = self_test(context);
  if (result != GNOVPOSE_OK) {
    std::cerr << "self_test failed result=" << result << " error=" << LastErrorText(get_last_error) << "\n";
    destroy(context);
    FreeLibrary(module);
    RemoveDllDirectory(cookie);
    return 10;
  }
  if (get_runtime(context, text, sizeof(text)) != GNOVPOSE_OK) {
    std::cerr << "runtime_info failed: " << LastErrorText(get_last_error) << "\n";
    destroy(context);
    FreeLibrary(module);
    RemoveDllDirectory(cookie);
    return 11;
  }
  std::cout << "runtime_info=" << text << "\n";
  destroy(context);

  if (!FreeLibrary(module)) {
    std::cerr << "FreeLibrary failed: " << GetLastError() << "\n";
    RemoveDllDirectory(cookie);
    return 12;
  }
  RemoveDllDirectory(cookie);
  std::cout << "GNOVPOSE_LOAD_VERSION_SELFTEST_UNLOAD=PASS\n";
  return 0;
}
