param(
    [switch]$Recreate
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ToolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ToolRoot "..\..")).Path
$Work = Join-Path $ToolRoot ".work"
$Homuler = Join-Path $Work "homuler"
$MediaPipe = Join-Path $Work "mediapipe"
$Downloads = Join-Path $Work "downloads"
$OpenVinoExtract = Join-Path $Work "openvino"

$HomulerCommit = "cf4c11d8eef724fe24111b7cd795d55ba490aeec"
$MediaPipeCommit = "c54c06dd8c4314a316c14da31493bcc38ed302e2"
$OpenVinoVersion = "2026.3.0"
$OpenVinoArchive = "openvino_toolkit_windows_2026.3.0.22451.bd8d6542e3c_x86_64.zip"
$OpenVinoBase = "https://storage.openvinotoolkit.org/repositories/openvino/packages/2026.3/windows"
$OpenVinoUrl = "$OpenVinoBase/$OpenVinoArchive"
$OpenVinoShaUrl = "$OpenVinoUrl.sha256"

function Find-Python {
    $candidates = @(
        @("py", "-3.14"), @("py", "-3.13"), @("py", "-3.12"),
        @("py", "-3.11"), @("py", "-3.10"), @("python", "")
    )
    foreach ($candidate in $candidates) {
        try {
            $exe = $candidate[0]
            $prefix = $candidate[1]
            $cmd = @()
            if ($prefix) { $cmd += $prefix }
            $cmd += @("-c", "import sys; assert sys.version_info >= (3,10); assert sys.maxsize > 2**32")
            & $exe @cmd 2>$null
            if ($LASTEXITCODE -eq 0) { return @{ Exe=$exe; Prefix=$prefix } }
        } catch {}
    }
    throw "64-bit Python 3.10+ was not found. Install Python 3.10-3.14; no packages are required for Gate B helper scripts."
}

function Invoke-Python([hashtable]$Python, [string[]]$Args) {
    $all = @()
    if ($Python.Prefix) { $all += $Python.Prefix }
    $all += $Args
    & $Python.Exe @all
    if ($LASTEXITCODE -ne 0) { throw "Python helper failed: $($Args -join ' ')" }
}

function Require-Command([string]$Name, [string]$Help) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) { throw "$Name was not found. $Help" }
}

Require-Command "git" "Install Git for Windows and reopen PowerShell."
$Bazel = $null
if (Get-Command "bazelisk" -ErrorAction SilentlyContinue) { $Bazel = "bazelisk" }
elseif (Get-Command "bazel" -ErrorAction SilentlyContinue) { $Bazel = "bazel" }
if (-not $Bazel) { throw "Bazel/Bazelisk was not found. Install Bazelisk (recommended) and put bazelisk.exe on PATH. Homuler v0.16.3 pins Bazel 6.5.0 via .bazelversion." }

$VsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $VsWhere)) { throw "Visual Studio C++ Build Tools were not detected. Install Visual Studio 2022 Build Tools with 'Desktop development with C++' (MSVC v143 + Windows 10/11 SDK), then rerun bootstrap." }
$VsInstall = & $VsWhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $VsInstall) { throw "MSVC x64 tools were not detected. Modify Visual Studio 2022 Build Tools and add 'Desktop development with C++' with MSVC v143 and a Windows SDK." }

$Python = Find-Python
New-Item -ItemType Directory -Force -Path $Work, $Downloads | Out-Null
if ($Recreate) { Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $Homuler, $MediaPipe, $OpenVinoExtract }

if (-not (Test-Path (Join-Path $Homuler ".git"))) {
    git clone --filter=blob:none https://github.com/homuler/MediaPipeUnityPlugin.git $Homuler
    if ($LASTEXITCODE -ne 0) { throw "Failed to clone Homuler MediaPipeUnityPlugin." }
}
$actual = (git -C $Homuler rev-parse HEAD).Trim()
if ($actual -ne $HomulerCommit) { git -C $Homuler fetch origin $HomulerCommit; git -C $Homuler checkout --detach $HomulerCommit }
$actual = (git -C $Homuler rev-parse HEAD).Trim()
if ($actual -ne $HomulerCommit) { throw "FAIL CLOSED: Homuler must be $HomulerCommit; got $actual" }

if (-not (Test-Path (Join-Path $MediaPipe ".git"))) {
    git clone --filter=blob:none https://github.com/google-ai-edge/mediapipe.git $MediaPipe
    if ($LASTEXITCODE -ne 0) { throw "Failed to clone MediaPipe." }
}
$actual = (git -C $MediaPipe rev-parse HEAD).Trim()
if ($actual -ne $MediaPipeCommit) { git -C $MediaPipe fetch origin $MediaPipeCommit; git -C $MediaPipe checkout --detach $MediaPipeCommit }
$actual = (git -C $MediaPipe rev-parse HEAD).Trim()
if ($actual -ne $MediaPipeCommit) { throw "FAIL CLOSED: MediaPipe must be $MediaPipeCommit; got $actual" }

$Zip = Join-Path $Downloads $OpenVinoArchive
$ShaFile = "$Zip.sha256"
if (-not (Test-Path $Zip)) { Write-Host "[Gate B] downloading official OpenVINO $OpenVinoVersion C++ toolkit..."; Invoke-WebRequest -UseBasicParsing -Uri $OpenVinoUrl -OutFile $Zip }
if (-not (Test-Path $ShaFile)) { Invoke-WebRequest -UseBasicParsing -Uri $OpenVinoShaUrl -OutFile $ShaFile }
$shaText = (Get-Content $ShaFile -Raw).Trim()
$match = [regex]::Match($shaText, "(?i)\b[0-9a-f]{64}\b")
if (-not $match.Success) { throw "Official OpenVINO .sha256 sidecar did not contain a SHA-256 digest. Refusing to continue." }
$expectedSha = $match.Value.ToLowerInvariant()
$actualSha = (Get-FileHash -Algorithm SHA256 $Zip).Hash.ToLowerInvariant()
if ($actualSha -ne $expectedSha) { throw "FAIL CLOSED: OpenVINO archive checksum mismatch. expected=$expectedSha actual=$actualSha" }
Write-Host "[Gate B] OpenVINO archive checksum PASS: $actualSha"

if (-not (Test-Path $OpenVinoExtract)) { New-Item -ItemType Directory -Force -Path $OpenVinoExtract | Out-Null; Expand-Archive -Path $Zip -DestinationPath $OpenVinoExtract }
$Setup = Get-ChildItem -Path $OpenVinoExtract -Recurse -Filter setupvars.bat | Select-Object -First 1
if (-not $Setup) { throw "OpenVINO setupvars.bat not found after extraction." }
$OpenVinoRoot = $Setup.Directory.FullName
$required = @(
    (Join-Path $OpenVinoRoot "runtime\include\openvino\openvino.hpp"),
    (Join-Path $OpenVinoRoot "runtime\lib\intel64\Release\openvino.lib"),
    (Join-Path $OpenVinoRoot "runtime\bin\intel64\Release\openvino.dll"),
    (Join-Path $OpenVinoRoot "runtime\bin\intel64\Release\openvino_tensorflow_lite_frontend.dll")
)
foreach ($item in $required) { if (-not (Test-Path $item)) { throw "Required OpenVINO C++/TFLite frontend artifact missing: $item" } }

$OverlayRoot = Join-Path $ToolRoot "overlay"
Invoke-Python $Python @((Join-Path $ToolRoot "scripts\apply_overlay.py"), "--homuler", $Homuler, "--mediapipe", $MediaPipe, "--overlay-root", $OverlayRoot)

$Workspace = Join-Path $Homuler "WORKSPACE"
$Marker = "# GOLDEN_NEEDLE_GATE_B_OPENVINO_REPOSITORY"
$workspaceText = Get-Content $Workspace -Raw
if ($workspaceText -notmatch [regex]::Escape($Marker)) {
    $ovPath = $OpenVinoRoot.Replace("\", "/")
    $block = @"

$Marker
new_local_repository(
    name = "openvino_gate_b",
    path = "$ovPath",
    build_file_content = """
package(default_visibility = ["//visibility:public"])
licenses(["notice"])
cc_import(name = "openvino_runtime", interface_library = "runtime/lib/intel64/Release/openvino.lib", shared_library = "runtime/bin/intel64/Release/openvino.dll")
cc_library(name = "openvino", hdrs = glob(["runtime/include/**/*.h", "runtime/include/**/*.hpp"]), includes = ["runtime/include"], deps = [":openvino_runtime"])
""",
)
"@
    Add-Content -Path $Workspace -Value $block
}

$State = @{ homuler_commit=$HomulerCommit; mediapipe_commit=$MediaPipeCommit; openvino_version=$OpenVinoVersion; openvino_archive=$OpenVinoArchive; openvino_sha256=$actualSha; openvino_root=$OpenVinoRoot; bazel_command=$Bazel; vs_install=$VsInstall }
$State | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 (Join-Path $Work "bootstrap-state.json")
Write-Host ""; Write-Host "[Gate B] bootstrap PASS"; Write-Host "Homuler: $HomulerCommit"; Write-Host "MediaPipe: $MediaPipeCommit"; Write-Host "OpenVINO: $OpenVinoVersion / $actualSha"; Write-Host "Next: .\Tools\MediaPipeOpenVinoParity\build.ps1"
