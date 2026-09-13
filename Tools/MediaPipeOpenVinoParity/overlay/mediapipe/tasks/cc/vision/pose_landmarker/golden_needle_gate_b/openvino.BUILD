# Generated into the ignored Homuler workspace by bootstrap.ps1 through
# new_local_repository(build_file_content=...). Kept here as the canonical
# auditable template.

package(default_visibility = ["//visibility:public"])
licenses(["notice"])

cc_import(
    name = "openvino_runtime",
    interface_library = "runtime/lib/intel64/Release/openvino.lib",
    shared_library = "runtime/bin/intel64/Release/openvino.dll",
)

cc_library(
    name = "openvino",
    hdrs = glob([
        "runtime/include/**/*.h",
        "runtime/include/**/*.hpp",
    ]),
    includes = ["runtime/include"],
    deps = [":openvino_runtime"],
)
