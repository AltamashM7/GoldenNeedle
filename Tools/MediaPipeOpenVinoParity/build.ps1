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

$OutputUserRoot = [string]$State.output_user_root
New-Item -ItemType Directory -Force -Path $OutputUserRoot | Out-Null
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
