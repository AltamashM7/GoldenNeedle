$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ToolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Work = Join-Path $ToolRoot ".work"
$StatePath = Join-Path $Work "bootstrap-state.json"
if (-not (Test-Path $StatePath)) {
    throw "Gate B is not bootstrapped. Run .\Tools\MediaPipeOpenVinoParity\bootstrap.ps1 first."
}

$State = Get-Content $StatePath -Raw | ConvertFrom-Json
$Homuler = Join-Path $Work "homuler"
$MediaPipe = Join-Path $Work "mediapipe"
$BuildOut = Join-Path $ToolRoot "build"
$Target = "@mediapipe//mediapipe/tasks/cc/vision/pose_landmarker/golden_needle_gate_b:parity_runner"

$actualHomuler = (git -C $Homuler rev-parse HEAD).Trim()
$actualMediaPipe = (git -C $MediaPipe rev-parse HEAD).Trim()
if ($actualHomuler -ne $State.homuler_commit) {
    throw "FAIL CLOSED: Homuler source moved: expected $($State.homuler_commit), got $actualHomuler"
}
if ($actualMediaPipe -ne $State.mediapipe_commit) {
    throw "FAIL CLOSED: MediaPipe source moved: expected $($State.mediapipe_commit), got $actualMediaPipe"
}

$ModelTaskGraph = Join-Path $MediaPipe "mediapipe\tasks\cc\core\model_task_graph.cc"
if (-not (Select-String -Path $ModelTaskGraph -Pattern "GOLDEN_NEEDLE_GATE_B_BACKEND" -Quiet) -or
    -not (Select-String -Path $ModelTaskGraph -Pattern "GOLDEN_NEEDLE_GATE_B_MODEL_ASSET" -Quiet)) {
    throw "Gate B inference seams are missing from the ignored MediaPipe workspace. Rerun bootstrap.ps1 -Recreate."
}

# Refresh only the isolated inference source and its BUILD metadata from the
# tracked overlay. Do not recopy the whole overlay here because parity_runner.cc
# receives deterministic local build-time patches below.
$OverlayGateB = Join-Path $ToolRoot "overlay\mediapipe\tasks\cc\vision\pose_landmarker\golden_needle_gate_b"
$WorkspaceGateB = Join-Path $MediaPipe "mediapipe\tasks\cc\vision\pose_landmarker\golden_needle_gate_b"
foreach ($name in @("openvino_inference_calculator.cc", "BUILD")) {
    $source = Join-Path $OverlayGateB $name
    $destination = Join-Path $WorkspaceGateB $name
    if (-not (Test-Path $source) -or -not (Test-Path $destination)) {
        throw "Gate B isolated overlay file is missing: $name. Rerun bootstrap.ps1 -Recreate."
    }
    Copy-Item -Force $source $destination
}
$OpenVinoSource = Join-Path $WorkspaceGateB "openvino_inference_calculator.cc"
if (-not (Select-String -Path $OpenVinoSource -Pattern "Gate B raw parity" -Quiet) -or
    -not (Select-String -Path $OpenVinoSource -Pattern "InferenceCalculatorNodeImpl" -Quiet) -or
    -not (Select-String -Path $OpenVinoSource -Pattern "kDefaultTensorAlignment" -Quiet)) {
    throw "FAIL CLOSED: Gate B OpenVINO source did not refresh with the MediaPipe inference-adapter semantics."
}
Write-Host "[Gate B] refreshed OpenVINO source using MediaPipe InferenceCalculatorNodeImpl semantics"

# TaskRunner always installs ModelResourcesCache. The custom GRAPH modes must
# therefore provide the same MediaPipeBuiltinOpResolver that BaseOptions uses
# by default; passing nullptr leaves the cache empty and makes the stock TFLite
# graph fail before inference with MediaPipeTasksStatus 601. Patch only the
# ignored generated MediaPipe workspace, deterministically and idempotently.
$RunnerSource = Join-Path $MediaPipe "mediapipe\tasks\cc\vision\pose_landmarker\golden_needle_gate_b\parity_runner.cc"
if (-not (Test-Path $RunnerSource)) {
    throw "Gate B parity runner source is missing from the ignored MediaPipe workspace. Rerun bootstrap.ps1 -Recreate."
}
$RunnerText = Get-Content $RunnerSource -Raw
$ResolverNeedle = "std::make_unique<core::MediaPipeBuiltinOpResolver>()"
if ($RunnerText -notmatch [regex]::Escape($ResolverNeedle)) {
    $NullResolverPattern = 'std::move\(config\),\s*nullptr,\s*nullptr,\s*nullptr,\s*std::nullopt,\s*std::nullopt,\s*\r?\n\s*/\*disable_default_service=\*/true\);'
    $ResolverReplacement = "std::move(config), std::make_unique<core::MediaPipeBuiltinOpResolver>(),`n      nullptr, nullptr, std::nullopt, std::nullopt,`n      /*disable_default_service=*/true);"
    $ResolverRegex = [regex]::new($NullResolverPattern)
    $PatchedRunnerText = $ResolverRegex.Replace($RunnerText, $ResolverReplacement, 1)
    if ($PatchedRunnerText -eq $RunnerText) {
        throw "FAIL CLOSED: could not locate the expected null TaskRunner op-resolver call in parity_runner.cc. Return this error to the Orchestrator."
    }
    [IO.File]::WriteAllText($RunnerSource, $PatchedRunnerText, (New-Object Text.UTF8Encoding($false)))
    $RunnerText = $PatchedRunnerText
    Write-Host "[Gate B] installed MediaPipeBuiltinOpResolver for custom graph TaskRunner"
}
if ($RunnerText -notmatch [regex]::Escape($ResolverNeedle)) {
    throw "FAIL CLOSED: custom graph TaskRunner op-resolver patch did not verify."
}

# MediaPipe 0.10.22 TaskRunner::Process may collapse a graph failure into an
# unhelpful `UNKNOWN:` status after WaitUntilIdle(). Install a graph error
# callback for this research runner so the original calculator/downstream status
# is printed before TaskRunner returns. Also dump the already-recorded OpenVINO
# inference telemetry when a frame fails; this tells us whether detector and/or
# landmark inference completed before the downstream graph error.
$GraphErrorNeedle = "[Gate B graph error]"
if ($RunnerText -notmatch [regex]::Escape($GraphErrorNeedle)) {
    $CreatePattern = 'std::move\(config\),\s*std::make_unique<core::MediaPipeBuiltinOpResolver>\(\),\s*\r?\n\s*nullptr,\s*nullptr,\s*std::nullopt,\s*std::nullopt,\s*\r?\n\s*/\*disable_default_service=\*/true\);'
    $CreateReplacement = @'
std::move(config), std::make_unique<core::MediaPipeBuiltinOpResolver>(),
      nullptr, nullptr, std::nullopt,
      core::ErrorFn([](absl::Status status) {
        std::cerr << "[Gate B graph error] " << status << "\n";
      }),
      /*disable_default_service=*/true);
'@
    $CreateRegex = [regex]::new($CreatePattern)
    $PatchedRunnerText = $CreateRegex.Replace($RunnerText, $CreateReplacement.TrimEnd(), 1)
    if ($PatchedRunnerText -eq $RunnerText) {
        throw "FAIL CLOSED: could not install Gate B graph error callback in parity_runner.cc. Return this error to the Orchestrator."
    }
    $RunnerText = $PatchedRunnerText

    $FailureNeedle = @'
      if (!result.ok()) {
        std::cerr << args.mode << " failed at frame " << index << ": "
                  << result.status() << "\n";
        return 2;
      }
'@
    $FailureReplacement = @'
      if (!result.ok()) {
        std::cerr << args.mode << " failed at frame " << index << ": "
                  << result.status() << "\n";
        if (args.mode == "GRAPH_OPENVINO_CPU_FP32") {
          const auto samples = golden_needle_gate_b::SnapshotTelemetry();
          std::cerr << "[Gate B OpenVINO failure telemetry] samples="
                    << samples.size();
          for (const auto& sample : samples) {
            std::cerr << " " << sample.model_kind
                      << "(infer_ms=" << sample.inference_ms
                      << ",input_copy_ms=" << sample.input_copy_ms
                      << ",output_copy_ms=" << sample.output_copy_ms << ")";
          }
          std::cerr << "\n";
        }
        return 2;
      }
'@
    if (-not $RunnerText.Contains($FailureNeedle.Trim())) {
        throw "FAIL CLOSED: could not locate the custom graph frame-failure block for diagnostics. Return this error to the Orchestrator."
    }
    $RunnerText = $RunnerText.Replace($FailureNeedle.Trim(), $FailureReplacement.Trim())
    [IO.File]::WriteAllText($RunnerSource, $RunnerText, (New-Object Text.UTF8Encoding($false)))
    Write-Host "[Gate B] installed graph-error and OpenVINO failure diagnostics"
}
if ($RunnerText -notmatch [regex]::Escape($GraphErrorNeedle)) {
    throw "FAIL CLOSED: Gate B graph error diagnostic patch did not verify."
}

$Bazel = [string]$State.bazel_command
if (-not (Test-Path $Bazel) -and -not (Get-Command $Bazel -ErrorAction SilentlyContinue)) {
    throw "Configured Bazel command '$Bazel' is no longer available. Restore Bazelisk/Bazel and rerun bootstrap."
}
$PythonExe = [string]$State.python_exe
if (-not (Test-Path $PythonExe)) {
    throw "Pinned Python 3.12 interpreter is missing: $PythonExe. Rerun bootstrap."
}
$MsysBash = [string]$State.msys_bash
$MsysBin = [string]$State.msys_bin
if (-not (Test-Path $MsysBash)) {
    throw "Pinned MSYS2 bash is missing: $MsysBash. Rerun bootstrap after restoring MSYS2."
}

$env:BAZEL_SH = $MsysBash
$env:PATH = "$MsysBin;$env:PATH"
$env:HERMETIC_PYTHON_VERSION = "3.12"
$env:PYTHON_BIN_PATH = $PythonExe
$env:ANDROID_NDK_HOME = ""

# Keep the Windows Bazel execution root deliberately short. The previous
# LOCALAPPDATA-based root pushed generated MediaPipe proto object paths past
# the MSVC path limit and produced C1083 "compiler generated file" failures.
$OutputUserRoot = Join-Path $env:USERPROFILE "gb"
New-Item -ItemType Directory -Force -Path $OutputUserRoot | Out-Null
Write-Host "[Gate B] short Bazel output root: $OutputUserRoot"
$PythonBazelPath = $PythonExe.Replace("\", "/")
$MediaPipeBazelPath = $MediaPipe.Replace("\", "/")
$Override = "--override_repository=mediapipe=$MediaPipeBazelPath"

$ActionEnv = @(
    "--action_env=PYTHON_BIN_PATH=$PythonBazelPath"
)
foreach ($name in @("ProgramData", "PROCESSOR_ARCHITECTURE", "PROCESSOR_IDENTIFIER", "PROCESSOR_LEVEL", "PROCESSOR_REVISION")) {
    $value = [Environment]::GetEnvironmentVariable($name)
    if ($null -ne $value -and $value.Length -gt 0) {
        $ActionEnv += "--action_env=$name=$value"
    }
}

$Startup = @("--output_user_root=$OutputUserRoot")
$Configuration = @(
    "-c", "opt",
    "--jobs=2",
    "--verbose_failures",
    "--define=MEDIAPIPE_DISABLE_GPU=1",
    "--@opencv//:switch=cmake",
    "--repo_env=HERMETIC_PYTHON_VERSION=3.12",
    "--repo_env=PYTHON_BIN_PATH=$PythonBazelPath",
    $Override
) + $ActionEnv

Push-Location $Homuler
try {
    Write-Host "[Gate B] building pinned MediaPipe 0.10.22 parity runner (Windows CPU, OpenCV CMake, 2 jobs)..."
    $BuildArgs = $Startup + @("build") + $Configuration + @($Target)
    & $Bazel @BuildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Bazel build failed. Return the complete terminal log to the Orchestrator; do not apply random fixes."
    }

    $QueryConfiguration = @(
        "-c", "opt",
        "--define=MEDIAPIPE_DISABLE_GPU=1",
        "--@opencv//:switch=cmake",
        "--repo_env=HERMETIC_PYTHON_VERSION=3.12",
        "--repo_env=PYTHON_BIN_PATH=$PythonBazelPath",
        $Override,
        "--output=files"
    ) + $ActionEnv
    $QueryArgs = $Startup + @("cquery") + $QueryConfiguration + @($Target)
    $files = & $Bazel @QueryArgs
    if ($LASTEXITCODE -ne 0) { throw "Bazel cquery failed after a successful build." }

    $exeLine = $files | Where-Object { $_ -match "parity_runner(\.exe)?$" } | Select-Object -First 1
    if (-not $exeLine) { throw "Could not locate parity_runner.exe from Bazel cquery output." }
    $exe = $exeLine.Trim()
    if (-not [System.IO.Path]::IsPathRooted($exe)) { $exe = Join-Path $Homuler $exe }
    if (-not (Test-Path $exe)) { throw "Built parity runner not found: $exe" }

    New-Item -ItemType Directory -Force -Path $BuildOut | Out-Null
    Copy-Item -Force $exe (Join-Path $BuildOut "parity_runner.exe")
} finally {
    Pop-Location
}

Write-Host "[Gate B] build PASS: $(Join-Path $BuildOut 'parity_runner.exe')"
Write-Host "Next: .\Tools\MediaPipeOpenVinoParity\run.ps1 -InputVideo `"C:\path\motion.mp4`""
