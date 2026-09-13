$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ToolRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Work = Join-Path $ToolRoot ".work"
$StatePath = Join-Path $Work "bootstrap-state.json"
$Build = Join-Path $ToolRoot "build"
if (-not (Test-Path $StatePath)) {
    throw "U1 is not bootstrapped. Run .\Tools\OpenVinoUnityPosePlugin\scripts\bootstrap.ps1 first."
}
$State = Get-Content $StatePath -Raw | ConvertFrom-Json
if ([string]$State.openvino_version -ne "2026.3.0" -or
    [string]$State.mediapipe_version -ne "0.10.22" -or
    [string]$State.homuler_version -ne "0.16.3") {
    throw "FAIL CLOSED: dependency pins in bootstrap-state.json do not match the approved U1 generations."
}
$OpenVinoRoot = [string]$State.openvino_root
if (-not (Test-Path (Join-Path $OpenVinoRoot "runtime\cmake\OpenVINOConfig.cmake"))) {
    throw "Pinned OpenVINO root is missing. Rerun bootstrap.ps1 -Recreate."
}

cmake -S $ToolRoot -B $Build -G "Visual Studio 17 2022" -A x64 "-DOPENVINO_ROOT=$OpenVinoRoot"
if ($LASTEXITCODE -ne 0) { throw "U1 CMake configure failed." }
cmake --build $Build --config Release --target golden_needle_openvino_pose gnovpose_smoke
if ($LASTEXITCODE -ne 0) { throw "U1 Release build failed." }

$Plugin = Join-Path $Build "Release\golden_needle_openvino_pose.dll"
$Smoke = Join-Path $Build "Release\gnovpose_smoke.exe"
foreach ($path in @($Plugin, $Smoke)) {
    if (-not (Test-Path $path)) { throw "Expected U1 build output missing: $path" }
}

$binaryText = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($Plugin))
foreach ($debugRuntime in @("VCRUNTIME140D.dll", "MSVCP140D.dll", "ucrtbased.dll")) {
    if ($binaryText.IndexOf($debugRuntime, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "FAIL CLOSED: Release plugin references debug CRT dependency $debugRuntime"
    }
}

$SetupVars = Join-Path $OpenVinoRoot "setupvars.bat"
if (-not (Test-Path $SetupVars)) { throw "OpenVINO setupvars.bat missing: $SetupVars" }
$envLines = & cmd.exe /d /s /c "call `"$SetupVars`" >nul && set"
if ($LASTEXITCODE -ne 0) { throw "OpenVINO setupvars.bat failed while resolving smoke-test dependencies." }
$ovPath = $null
foreach ($line in $envLines) {
    $eq = $line.IndexOf("=")
    if ($eq -le 0) { continue }
    $name = $line.Substring(0, $eq)
    $value = $line.Substring($eq + 1)
    if ($name -ieq "PATH") { $ovPath = $value }
}
if (-not $ovPath) { throw "OpenVINO setupvars.bat did not produce PATH." }
$rootNormalized = [IO.Path]::GetFullPath($OpenVinoRoot).TrimEnd('\') + '\'
$runtimeDirs = @($ovPath -split ';' | Where-Object {
    $_ -and (Test-Path $_) -and
    [IO.Path]::GetFullPath($_).StartsWith($rootNormalized, [StringComparison]::OrdinalIgnoreCase)
} | ForEach-Object { [IO.Path]::GetFullPath($_) } | Select-Object -Unique)
if ($runtimeDirs.Count -eq 0) { throw "No verified OpenVINO runtime dependency directories were discovered." }

& $Smoke $Plugin @runtimeDirs
if ($LASTEXITCODE -ne 0) { throw "U1 build-directory Load/Version/SelfTest/Unload smoke failed." }

Write-Host "[U1] build PASS: $Plugin"
Write-Host "[U1] debug CRT guard PASS"
Write-Host "[U1] same-process Load/Version/SelfTest/Unload PASS"
Write-Host "Next: .\Tools\OpenVinoUnityPosePlugin\scripts\package_unity.ps1"
