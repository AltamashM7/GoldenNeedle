$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ToolRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Work = Join-Path $ToolRoot ".work"
$StatePath = Join-Path $Work "bootstrap-state.json"
if (-not (Test-Path $StatePath)) { throw "Gate B is not bootstrapped. Run .\Tools\MediaPipeOpenVinoParity\bootstrap.ps1 first." }
$State = Get-Content $StatePath -Raw | ConvertFrom-Json
$Homuler = Join-Path $Work "homuler"
$MediaPipe = Join-Path $Work "mediapipe"
$BuildOut = Join-Path $ToolRoot "build"
$Target = "@mediapipe//mediapipe/tasks/cc/vision/pose_landmarker/golden_needle_gate_b:parity_runner"

$actualHomuler = (git -C $Homuler rev-parse HEAD).Trim()
$actualMediaPipe = (git -C $MediaPipe rev-parse HEAD).Trim()
if ($actualHomuler -ne $State.homuler_commit) { throw "FAIL CLOSED: Homuler source moved: expected $($State.homuler_commit), got $actualHomuler" }
if ($actualMediaPipe -ne $State.mediapipe_commit) { throw "FAIL CLOSED: MediaPipe source moved: expected $($State.mediapipe_commit), got $actualMediaPipe" }
if (-not (Select-String -Path (Join-Path $MediaPipe "mediapipe\tasks\cc\core\model_task_graph.cc") -Pattern "GOLDEN_NEEDLE_GATE_B_BACKEND" -Quiet)) { throw "Gate B inference seam is missing from the ignored MediaPipe workspace. Rerun bootstrap.ps1 -Recreate." }

$Bazel = [string]$State.bazel_command
if (-not (Get-Command $Bazel -ErrorAction SilentlyContinue)) { throw "Configured Bazel command '$Bazel' is no longer on PATH. Install/restore Bazelisk and rerun bootstrap." }
$Override = "--override_repository=mediapipe=$($MediaPipe.Replace('\','/'))"
Push-Location $Homuler
try {
    Write-Host "[Gate B] building pinned MediaPipe 0.10.22 parity runner..."
    & $Bazel build --config=windows --define=MEDIAPIPE_DISABLE_GPU=1 $Override $Target
    if ($LASTEXITCODE -ne 0) { throw "Bazel build failed. Return the complete terminal log to the Orchestrator." }
    $files = & $Bazel cquery --config=windows --define=MEDIAPIPE_DISABLE_GPU=1 $Override --output=files $Target
    if ($LASTEXITCODE -ne 0) { throw "Bazel cquery failed after a successful build." }
    $exeLine = $files | Where-Object { $_ -match "parity_runner(\.exe)?$" } | Select-Object -First 1
    if (-not $exeLine) { throw "Could not locate parity_runner.exe from Bazel cquery output." }
    $exe = $exeLine.Trim()
    if (-not [System.IO.Path]::IsPathRooted($exe)) { $exe = Join-Path $Homuler $exe }
    if (-not (Test-Path $exe)) { throw "Built parity runner not found: $exe" }
    New-Item -ItemType Directory -Force -Path $BuildOut | Out-Null
    Copy-Item -Force $exe (Join-Path $BuildOut "parity_runner.exe")
} finally { Pop-Location }
Write-Host "[Gate B] build PASS: $(Join-Path $BuildOut 'parity_runner.exe')"
Write-Host "Next: .\Tools\MediaPipeOpenVinoParity\run.ps1 -InputVideo `"C:\path\motion.mp4`""
