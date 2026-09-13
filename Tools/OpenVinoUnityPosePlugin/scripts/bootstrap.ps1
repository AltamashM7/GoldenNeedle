param(
    [switch]$Recreate
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ToolRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Work = Join-Path $ToolRoot ".work"
$Downloads = Join-Path $Work "downloads"
$OpenVinoExtract = Join-Path $Work "openvino"
$StatePath = Join-Path $Work "bootstrap-state.json"

$OpenVinoVersion = "2026.3.0"
$OpenVinoArchive = "openvino_toolkit_windows_2026.3.0.22451.bd8d6542e3c_x86_64.zip"
$OpenVinoBase = "https://storage.openvinotoolkit.org/repositories/openvino/packages/2026.3/windows"
$OpenVinoUrl = "$OpenVinoBase/$OpenVinoArchive"
$OpenVinoShaUrl = "$OpenVinoUrl.sha256"
$MediaPipeVersion = "0.10.22"
$MediaPipeCommit = "c54c06dd8c4314a316c14da31493bcc38ed302e2"
$HomulerVersion = "0.16.3"
$HomulerCommit = "cf4c11d8eef724fe24111b7cd795d55ba490aeec"

if (-not [Environment]::Is64BitOperatingSystem) { throw "U1 requires Windows x64." }
if (-not (Get-Command cmake -ErrorAction SilentlyContinue)) {
    throw "cmake.exe is required. Install Visual Studio 2022 C++ CMake tools or CMake and reopen PowerShell."
}
$VsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $VsWhere)) {
    throw "Visual Studio 2022 Build Tools were not detected. Install Desktop development with C++ (MSVC v143 + Windows SDK)."
}
$VsInstall = & $VsWhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $VsInstall) { throw "MSVC x64 tools were not detected. Install Visual Studio 2022 Desktop development with C++." }

New-Item -ItemType Directory -Force -Path $Work, $Downloads | Out-Null
if ($Recreate) {
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $OpenVinoExtract
    Remove-Item -Force -ErrorAction SilentlyContinue $StatePath
}

$Zip = Join-Path $Downloads $OpenVinoArchive
$ShaFile = "$Zip.sha256"
if (-not (Test-Path $Zip)) {
    Write-Host "[U1] downloading official OpenVINO $OpenVinoVersion Windows toolkit..."
    Invoke-WebRequest -UseBasicParsing -Uri $OpenVinoUrl -OutFile $Zip
}
if (-not (Test-Path $ShaFile)) { Invoke-WebRequest -UseBasicParsing -Uri $OpenVinoShaUrl -OutFile $ShaFile }
$shaText = (Get-Content $ShaFile -Raw).Trim()
$match = [regex]::Match($shaText, "(?i)\b[0-9a-f]{64}\b")
if (-not $match.Success) { throw "Official OpenVINO checksum sidecar did not contain a SHA-256 digest." }
$expectedSha = $match.Value.ToLowerInvariant()
$actualSha = (Get-FileHash -Algorithm SHA256 $Zip).Hash.ToLowerInvariant()
if ($actualSha -ne $expectedSha) {
    throw "FAIL CLOSED: OpenVINO archive checksum mismatch. expected=$expectedSha actual=$actualSha"
}
Write-Host "[U1] OpenVINO archive checksum PASS: $actualSha"

if (-not (Test-Path $OpenVinoExtract)) {
    New-Item -ItemType Directory -Force -Path $OpenVinoExtract | Out-Null
    Expand-Archive -Path $Zip -DestinationPath $OpenVinoExtract
}
$Setup = Get-ChildItem -Path $OpenVinoExtract -Recurse -Filter setupvars.bat | Select-Object -First 1
if (-not $Setup) { throw "OpenVINO setupvars.bat not found after extraction." }
$OpenVinoRoot = $Setup.Directory.FullName
$required = @(
    (Join-Path $OpenVinoRoot "runtime\include\openvino\openvino.hpp"),
    (Join-Path $OpenVinoRoot "runtime\lib\intel64\Release\openvino.lib"),
    (Join-Path $OpenVinoRoot "runtime\bin\intel64\Release\openvino.dll"),
    (Join-Path $OpenVinoRoot "runtime\bin\intel64\Release\openvino_intel_cpu_plugin.dll"),
    (Join-Path $OpenVinoRoot "runtime\cmake\OpenVINOConfig.cmake")
)
foreach ($item in $required) {
    if (-not (Test-Path $item)) { throw "Required OpenVINO 2026.3.0 artifact missing: $item" }
}

$State = [ordered]@{
    openvino_version = $OpenVinoVersion
    openvino_archive = $OpenVinoArchive
    openvino_sha256 = $actualSha
    openvino_root = $OpenVinoRoot
    mediapipe_version = $MediaPipeVersion
    mediapipe_commit = $MediaPipeCommit
    homuler_version = $HomulerVersion
    homuler_commit = $HomulerCommit
    vs_install = [string]$VsInstall
}
$State | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $StatePath

Write-Host ""
Write-Host "[U1] bootstrap PASS"
Write-Host "OpenVINO: $OpenVinoVersion / $actualSha"
Write-Host "MediaPipe semantic generation pin: $MediaPipeVersion / $MediaPipeCommit"
Write-Host "Homuler pin: $HomulerVersion / $HomulerCommit"
Write-Host "Next: .\Tools\OpenVinoUnityPosePlugin\scripts\build.ps1"
