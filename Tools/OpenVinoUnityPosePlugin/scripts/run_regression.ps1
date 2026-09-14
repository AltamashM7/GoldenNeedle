param(
    [string]$ArtifactDir = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ToolRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$RepoRoot = (Resolve-Path (Join-Path $ToolRoot "..\..")).Path
$StatePath = Join-Path $ToolRoot ".work\bootstrap-state.json"
$Build = Join-Path $ToolRoot "build"
$ReleaseDir = Join-Path $Build "Release"
if (-not (Test-Path $StatePath)) {
    throw "U2 is not bootstrapped. Run .\Tools\OpenVinoUnityPosePlugin\scripts\bootstrap.ps1 first."
}
$State = Get-Content $StatePath -Raw | ConvertFrom-Json
if ([string]$State.openvino_version -ne "2026.3.0" -or
    [string]$State.mediapipe_version -ne "0.10.22" -or
    [string]$State.homuler_version -ne "0.16.3" -or
    [string]$State.bazel_version -ne "6.5.0") {
    throw "FAIL CLOSED: regression state does not match approved U2 dependency pins."
}

$OpenVinoRoot = [string]$State.openvino_root
$MediaPipe = [string]$State.mediapipe_root
$PythonExe = [string]$State.python_exe
foreach ($path in @($OpenVinoRoot, $MediaPipe, $PythonExe)) {
    if (-not (Test-Path $path)) { throw "Pinned U2 regression path is missing: $path" }
}

New-Item -ItemType Directory -Force -Path $ReleaseDir | Out-Null
$Plugin = Join-Path $ReleaseDir "golden_needle_openvino_pose.dll"
$RuntimeSmoke = Join-Path $ReleaseDir "runtime_pose_smoke.exe"
$ManifestPath = Join-Path $ReleaseDir "native-build-manifest.json"

if ($ArtifactDir) {
    $ArtifactDir = [IO.Path]::GetFullPath($ArtifactDir)
    $ArtifactPlugin = Join-Path $ArtifactDir "golden_needle_openvino_pose.dll"
    $ArtifactRuntimeSmoke = Join-Path $ArtifactDir "runtime_pose_smoke.exe"
    $ArtifactManifest = Join-Path $ArtifactDir "native-build-manifest.json"
    foreach ($path in @($ArtifactPlugin, $ArtifactRuntimeSmoke, $ArtifactManifest)) {
        if (-not (Test-Path $path)) { throw "Preserved U2 native artifact is incomplete: $path" }
    }

    $Manifest = Get-Content $ArtifactManifest -Raw | ConvertFrom-Json
    if ([int]$Manifest.schema -ne 1 -or
        [string]$Manifest.openvino_version -ne [string]$State.openvino_version -or
        [string]$Manifest.openvino_sha256 -ne [string]$State.openvino_sha256 -or
        [string]$Manifest.mediapipe_commit -ne [string]$State.mediapipe_commit -or
        [string]$Manifest.homuler_commit -ne [string]$State.homuler_commit -or
        [string]$Manifest.bazel_version -ne [string]$State.bazel_version) {
        throw "FAIL CLOSED: preserved native artifact pins do not match the current approved U2 bootstrap."
    }

    $pluginHash = (Get-FileHash -Algorithm SHA256 $ArtifactPlugin).Hash.ToLowerInvariant()
    $runtimeSmokeHash = (Get-FileHash -Algorithm SHA256 $ArtifactRuntimeSmoke).Hash.ToLowerInvariant()
    if ($pluginHash -ne [string]$Manifest.plugin_sha256 -or
        $runtimeSmokeHash -ne [string]$Manifest.runtime_smoke_sha256) {
        throw "FAIL CLOSED: preserved U2 native artifact hash verification failed."
    }

    $sourceSha = [string]$Manifest.source_sha
    git -C $RepoRoot cat-file -e "$sourceSha^{commit}"
    if ($LASTEXITCODE -ne 0) { throw "Native artifact source SHA is not present in checkout: $sourceSha" }
    git -C $RepoRoot merge-base --is-ancestor $sourceSha HEAD
    if ($LASTEXITCODE -ne 0) {
        throw "FAIL CLOSED: preserved native artifact source $sourceSha is not an ancestor of current HEAD."
    }

    $nativePaths = @(
        "Tools/OpenVinoUnityPosePlugin/overlay",
        "Tools/OpenVinoUnityPosePlugin/include",
        "Tools/OpenVinoUnityPosePlugin/src",
        "Tools/OpenVinoUnityPosePlugin/tests/runtime_pose_smoke.cc",
        "Tools/OpenVinoUnityPosePlugin/scripts/apply_runtime_overlay.py",
        "Tools/OpenVinoUnityPosePlugin/scripts/bootstrap.ps1",
        "Tools/OpenVinoUnityPosePlugin/scripts/build_native.ps1",
        "Tools/MediaPipeOpenVinoParity/bootstrap.ps1",
        "Tools/MediaPipeOpenVinoParity/overlay"
    )
    $nativeChanges = @(git -C $RepoRoot diff --name-only "$sourceSha..HEAD" -- @nativePaths)
    if ($LASTEXITCODE -ne 0) { throw "Could not compare current tree with preserved native artifact source." }
    if ($nativeChanges.Count -gt 0) {
        throw "FAIL CLOSED: native-impacting files changed after preserved build $sourceSha. A fresh native artifact is required.`n$($nativeChanges -join "`n")"
    }

    Copy-Item -Force $ArtifactPlugin $Plugin
    Copy-Item -Force $ArtifactRuntimeSmoke $RuntimeSmoke
    Copy-Item -Force $ArtifactManifest $ManifestPath
    Write-Host "[U2 regression] restored immutable native build from $sourceSha"
    Write-Host "[U2 regression] plugin SHA-256: $pluginHash"
} else {
    foreach ($path in @($Plugin, $RuntimeSmoke, $ManifestPath)) {
        if (-not (Test-Path $path)) {
            throw "Native build output is missing: $path. Run build_native.ps1 first."
        }
    }
}

# The lifecycle host is tiny and intentionally rebuilt on every regression run so
# loader diagnostics can evolve without forcing a one-hour MediaPipe/OpenVINO rebuild.
cmake -S $ToolRoot -B $Build -G "Visual Studio 17 2022" -A x64
if ($LASTEXITCODE -ne 0) { throw "U2 smoke-host CMake configure failed." }
cmake --build $Build --config Release --target gnovpose_smoke
if ($LASTEXITCODE -ne 0) { throw "U2 smoke-host Release build failed." }
$Smoke = Join-Path $ReleaseDir "gnovpose_smoke.exe"
if (-not (Test-Path $Smoke)) { throw "Lifecycle smoke host missing: $Smoke" }

$binaryText = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($Plugin))
foreach ($debugRuntime in @("VCRUNTIME140D.dll", "MSVCP140D.dll", "ucrtbased.dll")) {
    if ($binaryText.IndexOf($debugRuntime, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "FAIL CLOSED: Release plugin references debug CRT dependency $debugRuntime"
    }
}

# Emit direct import diagnostics before LoadLibrary so Win32 error 126 is actionable.
$vsInstall = [string]$State.vs_install
if ($vsInstall -and (Test-Path $vsInstall)) {
    $dumpbin = Get-ChildItem (Join-Path $vsInstall "VC\Tools\MSVC") -Recurse -Filter dumpbin.exe -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\bin\\Hostx64\\x64\\dumpbin\.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($dumpbin) {
        Write-Host "[U2 regression] direct DLL dependencies from dumpbin:"
        & $dumpbin.FullName /DEPENDENTS $Plugin
    } else {
        Write-Warning "dumpbin.exe was not found under the pinned Visual Studio install; continuing with loader smoke."
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

Write-Host "[U2 regression] OpenVINO runtime search directories:"
$runtimeDirs | ForEach-Object { Write-Host "  $_" }

& $Smoke $Plugin @runtimeDirs
if ($LASTEXITCODE -ne 0) {
    throw "U2 lifecycle Load/Version/SelfTest/Unload regression failed. Native build is preserved; fix regression/loading without rebuilding unless native inputs change."
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
    throw "U2 real-frame MediaPipe/OpenVINO 33-landmark semantic smoke failed. Native build is preserved for a regression-only retry."
}

Write-Host ""
Write-Host "[U2 regression] lifecycle regression PASS"
Write-Host "[U2 regression] exact production model identity PASS"
Write-Host "[U2 regression] real-frame 33 normalized/world landmark smoke PASS"
Write-Host "[U2 regression] shadow-TFLite inference is absent from runtime calculator"
