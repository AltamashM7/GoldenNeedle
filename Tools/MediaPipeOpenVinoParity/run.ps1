param(
    [string]$InputVideo,
    [string]$InputFrames,
    [double]$Fps = 0,
    [ValidateSet("BaselineFirst", "OpenVinoFirst")]
    [string]$Order = "BaselineFirst"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ToolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ToolRoot "..\..")).Path
$Work = Join-Path $ToolRoot ".work"
$StatePath = Join-Path $Work "bootstrap-state.json"
$Exe = Join-Path $ToolRoot "build\parity_runner.exe"

if (-not (Test-Path $StatePath)) { throw "Run bootstrap.ps1 first." }
if (-not (Test-Path $Exe)) { throw "Run build.ps1 first; parity_runner.exe is missing." }
if (($InputVideo -and $InputFrames) -or (-not $InputVideo -and -not $InputFrames)) {
    throw "Specify exactly one of -InputVideo or -InputFrames."
}

$State = Get-Content $StatePath -Raw | ConvertFrom-Json
$PythonExe = [string]$State.python_exe
if (-not (Test-Path $PythonExe)) {
    throw "The pinned Python 3.12 interpreter from bootstrap is missing: $PythonExe. Rerun bootstrap."
}
$OpenVinoRoot = [string]$State.openvino_root
$SetupVars = Join-Path $OpenVinoRoot "setupvars.bat"
if (-not (Test-Path $SetupVars)) { throw "OpenVINO setupvars.bat missing: $SetupVars" }

function Import-BatEnvironment([string]$Bat) {
    $command = "call `"$Bat`" >nul && set"
    $lines = & cmd.exe /d /s /c $command
    if ($LASTEXITCODE -ne 0) { throw "OpenVINO setupvars.bat failed." }
    foreach ($line in $lines) {
        $eq = $line.IndexOf("=")
        if ($eq -gt 0) {
            [Environment]::SetEnvironmentVariable(
                $line.Substring(0, $eq),
                $line.Substring($eq + 1),
                "Process"
            )
        }
    }
}

function Invoke-Python([string[]]$PythonArgs) {
    & $PythonExe @PythonArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Python helper failed: $($PythonArgs -join ' ')"
    }
}

function Get-DirectoryDigest([string]$Directory) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $files = Get-ChildItem -File $Directory | Sort-Object Name
        if ($files.Count -eq 0) { throw "Input frame directory is empty." }
        foreach ($file in $files) {
            $nameBytes = [Text.Encoding]::UTF8.GetBytes($file.Name + "`n")
            $sha.TransformBlock($nameBytes, 0, $nameBytes.Length, $null, 0) | Out-Null
            $bytes = [IO.File]::ReadAllBytes($file.FullName)
            $sha.TransformBlock($bytes, 0, $bytes.Length, $null, 0) | Out-Null
        }
        $sha.TransformFinalBlock([byte[]]::new(0), 0, 0) | Out-Null
        return ([BitConverter]::ToString($sha.Hash)).Replace("-", "").ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

Import-BatEnvironment $SetupVars

$Models = Join-Path $Work "models"
New-Item -ItemType Directory -Force -Path $Models | Out-Null
Invoke-Python @(
    (Join-Path $ToolRoot "scripts\prepare_models.py"),
    "--repo-root", $RepoRoot,
    "--output-dir", $Models,
    "--report", (Join-Path $Work "model-identity.json")
)

$Bundle = Join-Path $RepoRoot "Assets\StreamingAssets\GoldenNeedle\PoseTrackingSpike\Models\pose_landmarker_lite.bytes"
$Detector = Join-Path $Models "pose_detector.tflite"
$Landmark = Join-Path $Models "pose_landmarks_detector.tflite"
$RunStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$RunDir = Join-Path $Work "runs\$RunStamp"
$Results = Join-Path $ToolRoot "results"
New-Item -ItemType Directory -Force -Path $RunDir, $Results | Out-Null

if ($InputVideo) {
    $InputVideo = (Resolve-Path $InputVideo).Path
    $Frames = Join-Path $RunDir "decoded-frames"
    $Metadata = Join-Path $RunDir "video-metadata.json"
    & $Exe --prepare-video $InputVideo --prepare-frames-dir $Frames --prepare-metadata $Metadata
    if ($LASTEXITCODE -ne 0) { throw "Video decode failed. Return the console output to the Orchestrator." }
    $meta = Get-Content $Metadata -Raw | ConvertFrom-Json
    if ($Fps -le 0) { $Fps = [double]$meta.fps }
    $InputLabel = Split-Path -Leaf $InputVideo
    $InputSha = (Get-FileHash -Algorithm SHA256 $InputVideo).Hash.ToLowerInvariant()
} else {
    $Frames = (Resolve-Path $InputFrames).Path
    if ($Fps -le 0) { throw "-Fps is required when using -InputFrames." }
    $InputLabel = Split-Path -Leaf $Frames
    $InputSha = Get-DirectoryDigest $Frames
}

if ($Fps -le 0) { throw "Resolved FPS must be positive." }
$Modes = if ($Order -eq "OpenVinoFirst") {
    @("GRAPH_OPENVINO_CPU_FP32", "GRAPH_TFLITE_CPU", "TASKS_REFERENCE")
} else {
    @("TASKS_REFERENCE", "GRAPH_TFLITE_CPU", "GRAPH_OPENVINO_CPU_FP32")
}
$Outputs = @{}

foreach ($Mode in $Modes) {
    $Output = Join-Path $RunDir "$Mode.jsonl"
    Write-Host "[Gate B] running full sequence: $Mode"
    & $Exe \
        --mode $Mode \
        --task-bundle $Bundle \
        --detector-model $Detector \
        --landmark-model $Landmark \
        --input-frames $Frames \
        --fps ([string]::Format([Globalization.CultureInfo]::InvariantCulture, "{0:R}", $Fps)) \
        --input-label $InputLabel \
        --input-sha256 $InputSha \
        --output $Output
    if ($LASTEXITCODE -ne 0) {
        throw "$Mode failed. Return the complete console output plus the ignored run directory: $RunDir"
    }
    $Outputs[$Mode] = $Output
}

$JsonReport = Join-Path $Results "parity-$RunStamp.json"
$TextReport = Join-Path $Results "parity-$RunStamp.txt"
Invoke-Python @(
    (Join-Path $ToolRoot "scripts\compare.py"),
    "--runs",
    $Outputs["TASKS_REFERENCE"],
    $Outputs["GRAPH_TFLITE_CPU"],
    $Outputs["GRAPH_OPENVINO_CPU_FP32"],
    "--json-out", $JsonReport,
    "--text-out", $TextReport
)

Write-Host ""
Write-Host "[Gate B] complete. Return BOTH reports and the summary block printed above:"
Write-Host "  $TextReport"
Write-Host "  $JsonReport"
