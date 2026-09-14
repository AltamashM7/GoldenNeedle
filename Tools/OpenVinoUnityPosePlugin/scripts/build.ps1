$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ToolRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$StatePath = Join-Path $ToolRoot ".work\bootstrap-state.json"
$Build = Join-Path $ToolRoot "build"
if (-not (Test-Path $StatePath)) {
    throw "U2 is not bootstrapped. Run .\Tools\OpenVinoUnityPosePlugin\scripts\bootstrap.ps1 first."
}
$State = Get-Content $StatePath -Raw | ConvertFrom-Json
if ([string]$State.openvino_version -ne "2026.3.0" -or
    [string]$State.mediapipe_version -ne "0.10.22" -or
    [string]$State.homuler_version -ne "0.16.3" -or
    [string]$State.bazel_version -ne "6.5.0") {
    throw "FAIL CLOSED: bootstrap state does not match approved U2 dependency pins."
}

$OpenVinoRoot = [string]$State.openvino_root
$Homuler = [string]$State.homuler_root
$MediaPipe = [string]$State.mediapipe_root
$RepoRoot = [string]$State.repo_root
$PythonExe = [string]$State.python_exe
$Bazel = [string]$State.bazel_command
$MsysBash = [string]$State.msys_bash
$MsysBin = [string]$State.msys_bin

foreach ($path in @($OpenVinoRoot, $Homuler, $MediaPipe, $RepoRoot, $PythonExe, $MsysBash)) {
    if (-not (Test-Path $path)) { throw "Pinned U2 path is missing: $path" }
}
$actualHomuler = (git -C $Homuler rev-parse HEAD).Trim()
$actualMediaPipe = (git -C $MediaPipe rev-parse HEAD).Trim()
if ($actualHomuler -ne [string]$State.homuler_commit -or
    $actualMediaPipe -ne [string]$State.mediapipe_commit) {
    throw "FAIL CLOSED: pinned Homuler/MediaPipe source moved after bootstrap."
}

# Build the independent dynamic-load host with MSVC. The actual plugin is built
# by Bazel below so that MediaPipe graph/calculator registration is in the same DLL.
cmake -S $ToolRoot -B $Build -G "Visual Studio 17 2022" -A x64
if ($LASTEXITCODE -ne 0) { throw "U2 smoke-host CMake configure failed." }
cmake --build $Build --config Release --target gnovpose_smoke
if ($LASTEXITCODE -ne 0) { throw "U2 smoke-host Release build failed." }
$Smoke = Join-Path $Build "Release\gnovpose_smoke.exe"
if (-not (Test-Path $Smoke)) { throw "Lifecycle smoke host missing: $Smoke" }

$env:BAZEL_SH = $MsysBash
$env:PATH = "$MsysBin;$env:PATH"
$env:HERMETIC_PYTHON_VERSION = "3.12"
$env:PYTHON_BIN_PATH = $PythonExe
$env:ANDROID_NDK_HOME = ""

# The hosted runner's USERPROFILE path makes the longest MediaPipe object path exceed
# the legacy MSVC path limit. Keep CI's Bazel output root deliberately short while
# preserving the existing local build root used by prepare_unity.ps1.
if ($env:GITHUB_ACTIONS -eq "true") {
    $OutputUserRoot = Join-Path $env:SystemDrive "b"
} else {
    $OutputUserRoot = Join-Path $env:USERPROFILE "gnu2"
}
New-Item -ItemType Directory -Force -Path $OutputUserRoot | Out-Null
$PythonBazelPath = $PythonExe.Replace("\", "/")
$MediaPipeBazelPath = $MediaPipe.Replace("\", "/")
$Override = "--override_repository=mediapipe=$MediaPipeBazelPath"
$ActionEnv = @("--action_env=PYTHON_BIN_PATH=$PythonBazelPath")
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

$Package = "@mediapipe//mediapipe/tasks/cc/vision/pose_landmarker/golden_needle_unity_openvino"
$PluginTarget = "${Package}:golden_needle_openvino_pose.dll"
$RuntimeSmokeTarget = "${Package}:runtime_pose_smoke"

Push-Location $Homuler
try {
    Write-Host "[U2] building pinned MediaPipe 0.10.22 + OpenVINO runtime DLL..."
    $BuildArgs = $Startup + @("build") + $Configuration + @($PluginTarget, $RuntimeSmokeTarget)
    & $Bazel @BuildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "U2 Bazel build failed. Preserve the full error; do not fall back to another backend."
    }

    function Resolve-BazelOutput([string]$Target, [string]$Suffix) {
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
        if ($LASTEXITCODE -ne 0) { throw "Bazel cquery failed for $Target" }
        $line = $files | Where-Object { $_.Trim() -match [regex]::Escape($Suffix) + '$' } | Select-Object -First 1
        if (-not $line) { throw "Could not locate $Suffix from Bazel cquery for $Target" }
        $path = $line.Trim()
        if (-not [IO.Path]::IsPathRooted($path)) { $path = Join-Path $Homuler $path }
        if (-not (Test-Path $path)) { throw "Bazel output missing: $path" }
        return [IO.Path]::GetFullPath($path)
    }

    $BuiltPlugin = Resolve-BazelOutput $PluginTarget "golden_needle_openvino_pose.dll"
    $BuiltRuntimeSmoke = Resolve-BazelOutput $RuntimeSmokeTarget "runtime_pose_smoke.exe"
} finally {
    Pop-Location
}

$ReleaseDir = Join-Path $Build "Release"
New-Item -ItemType Directory -Force -Path $ReleaseDir | Out-Null
$Plugin = Join-Path $ReleaseDir "golden_needle_openvino_pose.dll"
$RuntimeSmoke = Join-Path $ReleaseDir "runtime_pose_smoke.exe"
Copy-Item -Force $BuiltPlugin $Plugin
Copy-Item -Force $BuiltRuntimeSmoke $RuntimeSmoke

$binaryText = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($Plugin))
foreach ($debugRuntime in @("VCRUNTIME140D.dll", "MSVCP140D.dll", "ucrtbased.dll")) {
    if ($binaryText.IndexOf($debugRuntime, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "FAIL CLOSED: Release plugin references debug CRT dependency $debugRuntime"
    }
}

$SetupVars = Join-Path $OpenVinoRoot "setupvars.bat"
if (-not (Test-Path $SetupVars)) { throw "OpenVINO setupvars.bat missing: $SetupVars" }
$envLines = & cmd.exe /d /s /c "call `"$SetupVars`" >nul && set"
if ($LASTEXITCODE -ne 0) { throw "OpenVINO setupvars.bat failed." }
$ovPath = $null
foreach ($line in $envLines) {
    $eq = $line.IndexOf("=")
    if ($eq -le 0) { continue }
    $name = $line.Substring(0, $eq)
    $value = $line.Substring($eq + 1)
    [Environment]::SetEnvironmentVariable($name, $value, "Process")
    if ($name -ieq "PATH") { $ovPath = $value }
}
if (-not $ovPath) { throw "OpenVINO setupvars.bat did not produce PATH." }
$rootNormalized = [IO.Path]::GetFullPath($OpenVinoRoot).TrimEnd('\') + '\'
$runtimeDirs = @($ovPath -split ';' | Where-Object {
    $_ -and (Test-Path $_) -and
    [IO.Path]::GetFullPath($_).StartsWith($rootNormalized, [StringComparison]::OrdinalIgnoreCase)
} | ForEach-Object { [IO.Path]::GetFullPath($_) } | Select-Object -Unique)
if ($runtimeDirs.Count -eq 0) { throw "No verified OpenVINO runtime directories discovered." }

& $Smoke $Plugin @runtimeDirs
if ($LASTEXITCODE -ne 0) {
    throw "U2 lifecycle Load/Version/SelfTest/Unload regression failed."
}

$ModelDir = Join-Path $ToolRoot ".work\models"
$ModelReport = Join-Path $ToolRoot ".work\model-identity.json"
$Extractor = Join-Path $RepoRoot "Tools\MediaPipeOpenVinoParity\scripts\prepare_models.py"
& $PythonExe $Extractor --repo-root $RepoRoot --output-dir $ModelDir --report $ModelReport
if ($LASTEXITCODE -ne 0) { throw "Exact production model extraction/identity gate failed." }
$Detector = Join-Path $ModelDir "pose_detector.tflite"
$Landmark = Join-Path $ModelDir "pose_landmarks_detector.tflite"
$PoseImage = Join-Path $MediaPipe "mediapipe\tasks\testdata\vision\pose.jpg"
foreach ($path in @($Detector, $Landmark, $PoseImage)) {
    if (-not (Test-Path $path)) { throw "U2 real-frame smoke input missing: $path" }
}

& $RuntimeSmoke $Plugin $Detector $Landmark $PoseImage @runtimeDirs
if ($LASTEXITCODE -ne 0) {
    throw "U2 real-frame MediaPipe/OpenVINO 33-landmark semantic smoke failed."
}

Write-Host ""
Write-Host "[U2] native backend build PASS: $Plugin"
Write-Host "[U2] lifecycle regression PASS"
Write-Host "[U2] exact production model identity PASS"
Write-Host "[U2] real-frame 33 normalized/world landmark smoke PASS"
Write-Host "[U2] shadow-TFLite inference is absent from runtime calculator"
Write-Host "Next: .\Tools\OpenVinoUnityPosePlugin\scripts\package_unity.ps1"
