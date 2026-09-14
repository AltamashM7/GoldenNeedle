param(
    [string]$ArtifactDir = ""
)

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

$env:BAZEL_SH = $MsysBash
$env:PATH = "$MsysBin;$env:PATH"
$env:HERMETIC_PYTHON_VERSION = "3.12"
$env:PYTHON_BIN_PATH = $PythonExe
$env:ANDROID_NDK_HOME = ""

# Keep the hosted-runner Bazel execution root short enough for MSVC object paths.
if ($env:GITHUB_ACTIONS -eq "true") {
    $OutputUserRoot = Join-Path $env:SystemDrive "b"
} else {
    $OutputUserRoot = Join-Path $env:USERPROFILE "gnu2"
}
New-Item -ItemType Directory -Force -Path $OutputUserRoot | Out-Null
Write-Host "[U2 native] Bazel output root: $OutputUserRoot"

# Homuler's pinned WORKSPACE still points zlib 1.2.13 at an old zlib.net HTTP URL.
# Hosted runners can receive non-archive bytes from that endpoint, which Bazel
# correctly rejects against the pinned SHA. Supply the exact same upstream archive
# through Bazel's distdir so dependency identity remains unchanged and fail-closed.
$DistDir = Join-Path $ToolRoot ".work\distdir"
$ZlibArchive = Join-Path $DistDir "zlib-1.2.13.tar.gz"
$ZlibExpectedSha256 = "b3a24de97a8fdbc835b9833169501030b8977031bcb54b3b3ac13740f846ab30"
$ZlibUrl = "https://github.com/madler/zlib/releases/download/v1.2.13/zlib-1.2.13.tar.gz"
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null

$haveValidZlib = $false
if (Test-Path $ZlibArchive) {
    $existingHash = (Get-FileHash -Algorithm SHA256 $ZlibArchive).Hash.ToLowerInvariant()
    if ($existingHash -eq $ZlibExpectedSha256) {
        $haveValidZlib = $true
    } else {
        Remove-Item -Force $ZlibArchive
    }
}
if (-not $haveValidZlib) {
    $tempArchive = "$ZlibArchive.download"
    Remove-Item -Force $tempArchive -ErrorAction SilentlyContinue
    $downloaded = $false
    for ($attempt = 1; $attempt -le 3 -and -not $downloaded; $attempt++) {
        try {
            Write-Host "[U2 native] downloading exact zlib 1.2.13 archive (attempt $attempt/3)..."
            Invoke-WebRequest -UseBasicParsing -Uri $ZlibUrl -OutFile $tempArchive
            $downloaded = $true
        } catch {
            Remove-Item -Force $tempArchive -ErrorAction SilentlyContinue
            if ($attempt -eq 3) { throw }
            Start-Sleep -Seconds 2
        }
    }
    $downloadHash = (Get-FileHash -Algorithm SHA256 $tempArchive).Hash.ToLowerInvariant()
    if ($downloadHash -ne $ZlibExpectedSha256) {
        Remove-Item -Force $tempArchive -ErrorAction SilentlyContinue
        throw "FAIL CLOSED: official zlib 1.2.13 archive hash mismatch: expected $ZlibExpectedSha256, got $downloadHash"
    }
    Move-Item -Force $tempArchive $ZlibArchive
}
$ZlibHash = (Get-FileHash -Algorithm SHA256 $ZlibArchive).Hash.ToLowerInvariant()
if ($ZlibHash -ne $ZlibExpectedSha256) {
    throw "FAIL CLOSED: zlib distdir verification failed."
}
Write-Host "[U2 native] zlib 1.2.13 distdir checksum PASS: $ZlibHash"

$PythonBazelPath = $PythonExe.Replace("\", "/")
$MediaPipeBazelPath = $MediaPipe.Replace("\", "/")
$DistDirBazelPath = $DistDir.Replace("\", "/")
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
    "--distdir=$DistDirBazelPath",
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
    Write-Host "[U2 native] building pinned MediaPipe 0.10.22 + OpenVINO runtime DLL..."
    $BuildArgs = $Startup + @("build") + $Configuration + @($PluginTarget, $RuntimeSmokeTarget)
    & $Bazel @BuildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "U2 Bazel build failed. Preserve the full error; do not fall back to another backend."
    }

    function Resolve-BazelOutput([string]$Target, [string]$Suffix) {
        $QueryConfiguration = @(
            "-c", "opt",
            "--distdir=$DistDirBazelPath",
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

$sourceSha = (git -C $RepoRoot rev-parse HEAD).Trim()
$manifest = [ordered]@{
    schema = 1
    source_sha = $sourceSha
    created_utc = [DateTime]::UtcNow.ToString("o")
    openvino_version = [string]$State.openvino_version
    openvino_sha256 = [string]$State.openvino_sha256
    mediapipe_version = [string]$State.mediapipe_version
    mediapipe_commit = [string]$State.mediapipe_commit
    homuler_version = [string]$State.homuler_version
    homuler_commit = [string]$State.homuler_commit
    bazel_version = [string]$State.bazel_version
    zlib_version = "1.2.13"
    zlib_sha256 = $ZlibExpectedSha256
    configuration = "opt; jobs=2; MEDIAPIPE_DISABLE_GPU=1; OpenCV CMake; verified distdir"
    plugin_file = "golden_needle_openvino_pose.dll"
    plugin_sha256 = (Get-FileHash -Algorithm SHA256 $Plugin).Hash.ToLowerInvariant()
    runtime_smoke_file = "runtime_pose_smoke.exe"
    runtime_smoke_sha256 = (Get-FileHash -Algorithm SHA256 $RuntimeSmoke).Hash.ToLowerInvariant()
}
$ManifestPath = Join-Path $ReleaseDir "native-build-manifest.json"
$manifest | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $ManifestPath

if ($ArtifactDir) {
    $ArtifactDir = [IO.Path]::GetFullPath($ArtifactDir)
    New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null
    Get-ChildItem $ArtifactDir -File -ErrorAction SilentlyContinue | Remove-Item -Force
    Copy-Item -Force $Plugin (Join-Path $ArtifactDir "golden_needle_openvino_pose.dll")
    Copy-Item -Force $RuntimeSmoke (Join-Path $ArtifactDir "runtime_pose_smoke.exe")
    Copy-Item -Force $ManifestPath (Join-Path $ArtifactDir "native-build-manifest.json")
}

Write-Host ""
Write-Host "[U2 native] BUILD PASS"
Write-Host "[U2 native] source SHA: $sourceSha"
Write-Host "[U2 native] plugin SHA-256: $($manifest.plugin_sha256)"
Write-Host "[U2 native] runtime smoke SHA-256: $($manifest.runtime_smoke_sha256)"
if ($ArtifactDir) { Write-Host "[U2 native] artifact staged: $ArtifactDir" }
