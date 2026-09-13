param(
    [switch]$Recreate
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ToolRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$RepoRoot = Split-Path -Parent (Split-Path -Parent $ToolRoot)
$Work = Join-Path $ToolRoot ".work"
$StatePath = Join-Path $Work "bootstrap-state.json"
$GateBRoot = Join-Path $RepoRoot "Tools\MediaPipeOpenVinoParity"
$GateBBootstrap = Join-Path $GateBRoot "bootstrap.ps1"
$GateBStatePath = Join-Path $GateBRoot ".work\bootstrap-state.json"

if (-not (Test-Path $GateBBootstrap)) {
    throw "Gate B bootstrap is missing: $GateBBootstrap"
}
New-Item -ItemType Directory -Force -Path $Work | Out-Null

Write-Host "[U2] reusing the proven Gate B pinned native source bootstrap..."
if ($Recreate) {
    & $GateBBootstrap -Recreate
} else {
    & $GateBBootstrap
}
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $GateBStatePath)) {
    throw "Gate B pinned source bootstrap failed."
}

$GateBState = Get-Content $GateBStatePath -Raw | ConvertFrom-Json
$expectedMediaPipe = "c54c06dd8c4314a316c14da31493bcc38ed302e2"
$expectedHomuler = "cf4c11d8eef724fe24111b7cd795d55ba490aeec"
if ([string]$GateBState.mediapipe_commit -ne $expectedMediaPipe -or
    [string]$GateBState.homuler_commit -ne $expectedHomuler -or
    [string]$GateBState.openvino_version -ne "2026.3.0" -or
    [string]$GateBState.bazel_version -ne "6.5.0") {
    throw "FAIL CLOSED: Gate B bootstrap state no longer matches approved U2 pins."
}

$Homuler = Join-Path $GateBRoot ".work\homuler"
$MediaPipe = Join-Path $GateBRoot ".work\mediapipe"
$OpenVinoRoot = [IO.Path]::GetFullPath([string]$GateBState.openvino_root)
foreach ($required in @(
    (Join-Path $OpenVinoRoot "runtime\bin\intel64\Release\openvino.dll"),
    (Join-Path $OpenVinoRoot "runtime\bin\intel64\Release\openvino_intel_cpu_plugin.dll"),
    (Join-Path $OpenVinoRoot "runtime\bin\intel64\Release\openvino_tensorflow_lite_frontend.dll"),
    (Join-Path $OpenVinoRoot "runtime\lib\intel64\Release\openvino.lib")
)) {
    if (-not (Test-Path $required)) {
        throw "Required OpenVINO CPU runtime artifact missing: $required"
    }
}

$PythonExe = [string]$GateBState.python_exe
$OverlayScript = Join-Path $ToolRoot "scripts\apply_runtime_overlay.py"
& $PythonExe $OverlayScript --tool-root $ToolRoot --mediapipe $MediaPipe
if ($LASTEXITCODE -ne 0) {
    throw "U2 runtime overlay installation failed."
}

$actualHomuler = (git -C $Homuler rev-parse HEAD).Trim()
$actualMediaPipe = (git -C $MediaPipe rev-parse HEAD).Trim()
if ($actualHomuler -ne $expectedHomuler -or $actualMediaPipe -ne $expectedMediaPipe) {
    throw "FAIL CLOSED: pinned native source moved during U2 bootstrap."
}

$ModelTaskGraph = Join-Path $MediaPipe "mediapipe\tasks\cc\core\model_task_graph.cc"
if (-not (Select-String -Path $ModelTaskGraph -Pattern "GOLDEN_NEEDLE_GATE_B_BACKEND" -Quiet) -or
    -not (Select-String -Path $ModelTaskGraph -Pattern "GOLDEN_NEEDLE_GATE_B_MODEL_ASSET" -Quiet)) {
    throw "FAIL CLOSED: proven ModelTaskGraph OpenVINO seam is missing."
}

$State = [ordered]@{
    openvino_version = "2026.3.0"
    openvino_sha256 = [string]$GateBState.openvino_sha256
    openvino_root = $OpenVinoRoot
    mediapipe_version = "0.10.22"
    mediapipe_commit = $expectedMediaPipe
    mediapipe_root = $MediaPipe
    homuler_version = "0.16.3"
    homuler_commit = $expectedHomuler
    homuler_root = $Homuler
    bazel_command = [string]$GateBState.bazel_command
    bazel_version = "6.5.0"
    python_exe = $PythonExe
    python_version = "3.12"
    msys_bash = [string]$GateBState.msys_bash
    msys_bin = [string]$GateBState.msys_bin
    vs_install = [string]$GateBState.vs_install
    repo_root = $RepoRoot
    gate_b_root = $GateBRoot
}
$State | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $StatePath

Write-Host ""
Write-Host "[U2] bootstrap PASS"
Write-Host "OpenVINO: 2026.3.0 / $($State.openvino_sha256)"
Write-Host "MediaPipe: 0.10.22 / $expectedMediaPipe"
Write-Host "Homuler: 0.16.3 / $expectedHomuler"
Write-Host "Bazel: 6.5.0"
Write-Host "Runtime overlay: golden_needle_unity_openvino"
Write-Host "Next: .\Tools\OpenVinoUnityPosePlugin\scripts\build.ps1"
