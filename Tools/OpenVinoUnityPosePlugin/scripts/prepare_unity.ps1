param(
    [switch]$Recreate
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ToolRoot = Split-Path -Parent $ScriptRoot
$RepoRoot = (Resolve-Path (Join-Path $ToolRoot "..\..")).Path

$Bootstrap = Join-Path $ScriptRoot "bootstrap.ps1"
$Build = Join-Path $ScriptRoot "build.ps1"
$Package = Join-Path $ScriptRoot "package_unity.ps1"
foreach ($path in @($Bootstrap, $Build, $Package)) {
    if (-not (Test-Path $path)) {
        throw "Required OpenVINO Unity preparation script is missing: $path"
    }
}

Write-Host "[OpenVINO Unity] preparing exact pinned native runtime for local Unity QA..."
if ($Recreate) {
    & $Bootstrap -Recreate
} else {
    & $Bootstrap
}
if ($LASTEXITCODE -ne 0) { throw "OpenVINO Unity bootstrap failed." }

& $Build
if ($LASTEXITCODE -ne 0) { throw "OpenVINO Unity native build/smoke failed." }

& $Package -RepoRoot $RepoRoot
if ($LASTEXITCODE -ne 0) { throw "OpenVINO Unity additive package staging failed." }

Write-Host ""
Write-Host "OPENVINO_UNITY_LOCAL_PREP=PASS"
Write-Host "Unity package: Assets/GoldenNeedle/Plugins/OpenVinoPose/x86_64"
Write-Host "Exact models: Assets/StreamingAssets/GoldenNeedle/OpenVinoPoseModels"
Write-Host "Stock MediaPipe/TFLite files were not replaced."
Write-Host "Open Unity only after this command has returned PASS."
