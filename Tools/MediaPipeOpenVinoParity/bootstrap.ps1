param(
    [switch]$Recreate
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ToolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
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
$ExpectedBazel = "6.5.0"

function Require-Command([string]$Name, [string]$Help) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $command) { throw "$Name was not found. $Help" }
    return $command.Source
}

function Find-Python312 {
    $candidates = @(
        @{ Exe = "py"; Prefix = "-3.12" },
        @{ Exe = "python"; Prefix = "" },
        @{ Exe = "python3"; Prefix = "" }
    )
    foreach ($candidate in $candidates) {
        try {
            $probe = @()
            if ($candidate.Prefix) { $probe += $candidate.Prefix }
            $probe += @("-c", "import struct,sys; assert sys.version_info[:2] == (3,12); assert struct.calcsize('P')*8 == 64; print(sys.executable)")
            $output = & $candidate.Exe @probe 2>$null
            if ($LASTEXITCODE -eq 0 -and $output) {
                $path = ([string]($output | Select-Object -Last 1)).Trim()
                if (Test-Path $path) { return (Resolve-Path $path).Path }
            }
        } catch {}
    }
    throw "64-bit Python 3.12 was not found. Homuler v0.16.3 / MediaPipe 0.10.22 only carries hermetic Python locks through 3.12, and its Windows CI uses 3.12. Install a user-level 64-bit Python 3.12, then rerun bootstrap. Python 3.14 alone is not sufficient for this native build."
}

function Find-MsysBash {
    $candidates = @()
    if ($env:BAZEL_SH) { $candidates += $env:BAZEL_SH }
    $candidates += @(
        "C:\msys64\usr\bin\bash.exe",
        "C:\tools\msys64\usr\bin\bash.exe"
    )
    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (-not (Test-Path $candidate)) { continue }
        & $candidate -lc "command -v git >/dev/null && command -v patch >/dev/null && command -v unzip >/dev/null && command -v zip >/dev/null"
        if ($LASTEXITCODE -eq 0) { return (Resolve-Path $candidate).Path }
    }
    throw "MSYS2 with git, patch, unzip and zip was not found. Homuler's Windows build environment requires MSYS2. Install MSYS2 (normally C:\msys64) and in its MINGW64 shell run: pacman -S --needed git patch unzip zip. Then rerun bootstrap."
}

function Reset-PinnedClone([string]$Url, [string]$Directory, [string]$Commit, [string]$Label) {
    if (-not (Test-Path (Join-Path $Directory ".git"))) {
        git -c core.autocrlf=false clone --filter=blob:none $Url $Directory
        if ($LASTEXITCODE -ne 0) { throw "Failed to clone $Label." }
    }
    git -C $Directory config core.autocrlf false
    git -C $Directory fetch origin $Commit
    if ($LASTEXITCODE -ne 0) { throw "Failed to fetch pinned $Label commit $Commit." }
    git -C $Directory checkout --detach $Commit
    if ($LASTEXITCODE -ne 0) { throw "Failed to checkout pinned $Label commit $Commit." }
    git -C $Directory reset --hard $Commit | Out-Null
    git -C $Directory clean -fd | Out-Null
    $actual = (git -C $Directory rev-parse HEAD).Trim()
    if ($actual -ne $Commit) { throw "FAIL CLOSED: $Label must be $Commit; got $actual" }
}

$Git = Require-Command "git" "Install Git for Windows and reopen PowerShell."
$CMake = Require-Command "cmake" "Install CMake (or the Visual Studio C++ CMake tools) and ensure cmake.exe is on PATH. Homuler's --opencv cmake path needs it."
$PythonExe = Find-Python312
$MsysBash = Find-MsysBash
$MsysBin = Split-Path -Parent $MsysBash

$Bazel = $null
if (Get-Command "bazelisk" -ErrorAction SilentlyContinue) {
    $Bazel = (Get-Command "bazelisk").Source
} elseif (Get-Command "bazel" -ErrorAction SilentlyContinue) {
    $Bazel = (Get-Command "bazel").Source
}
if (-not $Bazel) {
    throw "Bazel/Bazelisk was not found. Install Bazelisk (recommended) and put bazelisk.exe on PATH. Gate B requires the Homuler-pinned Bazel 6.5.0 generation."
}

$VsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $VsWhere)) {
    throw "Visual Studio C++ Build Tools were not detected. Install Visual Studio 2022 Build Tools with 'Desktop development with C++' (MSVC v143 + Windows 10/11 SDK), then rerun bootstrap."
}
$VsInstall = & $VsWhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $VsInstall) {
    throw "MSVC x64 tools were not detected. Modify Visual Studio 2022 Build Tools and add 'Desktop development with C++' with MSVC v143 and a Windows SDK."
}

New-Item -ItemType Directory -Force -Path $Work, $Downloads | Out-Null
if ($Recreate) {
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $Homuler, $MediaPipe, $OpenVinoExtract
}

Reset-PinnedClone "https://github.com/homuler/MediaPipeUnityPlugin.git" $Homuler $HomulerCommit "Homuler"
Reset-PinnedClone "https://github.com/google-ai-edge/mediapipe.git" $MediaPipe $MediaPipeCommit "MediaPipe"

Push-Location $Homuler
try {
    $bazelVersionText = (& $Bazel --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $bazelVersionText -notmatch "(?m)\bbazel\s+6\.5\.0\b") {
        throw "FAIL CLOSED: Gate B requires Bazel 6.5.0 from Homuler's .bazelversion. '$Bazel --version' returned: $bazelVersionText"
    }
} finally {
    Pop-Location
}

$Zip = Join-Path $Downloads $OpenVinoArchive
$ShaFile = "$Zip.sha256"
if (-not (Test-Path $Zip)) {
    Write-Host "[Gate B] downloading official OpenVINO $OpenVinoVersion C++ toolkit..."
    Invoke-WebRequest -UseBasicParsing -Uri $OpenVinoUrl -OutFile $Zip
}
if (-not (Test-Path $ShaFile)) {
    Invoke-WebRequest -UseBasicParsing -Uri $OpenVinoShaUrl -OutFile $ShaFile
}
$shaText = (Get-Content $ShaFile -Raw).Trim()
$match = [regex]::Match($shaText, "(?i)\b[0-9a-f]{64}\b")
if (-not $match.Success) {
    throw "Official OpenVINO .sha256 sidecar did not contain a SHA-256 digest. Refusing to continue."
}
$expectedSha = $match.Value.ToLowerInvariant()
$actualSha = (Get-FileHash -Algorithm SHA256 $Zip).Hash.ToLowerInvariant()
if ($actualSha -ne $expectedSha) {
    throw "FAIL CLOSED: OpenVINO archive checksum mismatch. expected=$expectedSha actual=$actualSha"
}
Write-Host "[Gate B] OpenVINO archive checksum PASS: $actualSha"

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
    (Join-Path $OpenVinoRoot "runtime\bin\intel64\Release\openvino_tensorflow_lite_frontend.dll")
)
foreach ($item in $required) {
    if (-not (Test-Path $item)) { throw "Required OpenVINO C++/TFLite frontend artifact missing: $item" }
}

$OverlayRoot = Join-Path $ToolRoot "overlay"
& $PythonExe (Join-Path $ToolRoot "scripts\apply_overlay.py") --homuler $Homuler --mediapipe $MediaPipe --overlay-root $OverlayRoot
if ($LASTEXITCODE -ne 0) { throw "Gate B overlay helper failed." }

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

$OutputUserRoot = Join-Path $env:LOCALAPPDATA "GN_Bazel"
New-Item -ItemType Directory -Force -Path $OutputUserRoot | Out-Null
$State = @{
    homuler_commit = $HomulerCommit
    mediapipe_commit = $MediaPipeCommit
    openvino_version = $OpenVinoVersion
    openvino_archive = $OpenVinoArchive
    openvino_sha256 = $actualSha
    openvino_root = $OpenVinoRoot
    bazel_command = $Bazel
    bazel_version = $ExpectedBazel
    python_exe = $PythonExe
    python_version = "3.12"
    msys_bash = $MsysBash
    msys_bin = $MsysBin
    output_user_root = $OutputUserRoot
    cmake_exe = $CMake
    vs_install = [string]$VsInstall
}
$State | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 (Join-Path $Work "bootstrap-state.json")

Write-Host ""
Write-Host "[Gate B] bootstrap PASS"
Write-Host "Homuler: $HomulerCommit"
Write-Host "MediaPipe: $MediaPipeCommit"
Write-Host "Python: $PythonExe (3.12 x64)"
Write-Host "MSYS2 bash: $MsysBash"
Write-Host "Bazel: $ExpectedBazel"
Write-Host "OpenVINO: $OpenVinoVersion / $actualSha"
Write-Host "Next: .\Tools\MediaPipeOpenVinoParity\build.ps1"
