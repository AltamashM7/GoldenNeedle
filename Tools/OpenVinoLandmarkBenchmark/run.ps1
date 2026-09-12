param(
    [ValidateSet("CPU", "GPU")]
    [string[]]$Devices = @("CPU", "GPU"),
    [int]$Warmup = 30,
    [int]$Iterations = 300,
    [int]$CopyIterations = 100,
    [switch]$Reinstall
)

$ErrorActionPreference = "Stop"
$ToolDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = (Resolve-Path (Join-Path $ToolDir "..\..")).Path
$VenvDir = Join-Path $ToolDir ".venv"
$VenvPython = Join-Path $VenvDir "Scripts\python.exe"
$Requirements = Join-Path $ToolDir "requirements.txt"
$Benchmark = Join-Path $ToolDir "benchmark.py"

function Test-PythonCandidate {
    param([string]$Exe, [string[]]$PrefixArgs)
    try {
        & $Exe @PrefixArgs -c "import sys,struct; ok=(3,10)<=sys.version_info[:2]<=(3,14) and struct.calcsize('P')*8==64; raise SystemExit(0 if ok else 1)" 2>$null
        return $LASTEXITCODE -eq 0
    }
    catch {
        return $false
    }
}

function Invoke-BasePython {
    param([string[]]$CommandArgs)
    if ($script:BaseKind -eq "py") {
        & py $script:BasePrefix @CommandArgs
    }
    else {
        & $script:BaseExe @CommandArgs
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Python command failed with exit code $LASTEXITCODE."
    }
}

$script:BaseKind = $null
$script:BasePrefix = $null
$script:BaseExe = $null

if (Get-Command py -ErrorAction SilentlyContinue) {
    foreach ($Minor in @(14, 13, 12, 11, 10)) {
        $Selector = "-3.$Minor"
        if (Test-PythonCandidate "py" @($Selector)) {
            $script:BaseKind = "py"
            $script:BasePrefix = $Selector
            break
        }
    }
}

if (-not $script:BaseKind) {
    foreach ($Name in @("python", "python3")) {
        $Command = Get-Command $Name -ErrorAction SilentlyContinue
        if ($Command -and (Test-PythonCandidate $Command.Source @())) {
            $script:BaseKind = "exe"
            $script:BaseExe = $Command.Source
            break
        }
    }
}

if (-not $script:BaseKind) {
    throw "No supported 64-bit Python 3.10-3.14 was found. Install a user-level 64-bit Python, then rerun this script."
}

if ($Reinstall -and (Test-Path $VenvDir)) {
    Remove-Item -Recurse -Force $VenvDir
}

if (-not (Test-Path $VenvPython)) {
    Write-Host "Creating isolated virtual environment at $VenvDir"
    Invoke-BasePython -CommandArgs @("-m", "venv", $VenvDir)
}

& $VenvPython -c "import sys,struct; assert (3,10)<=sys.version_info[:2]<=(3,14); assert struct.calcsize('P')*8==64"
if ($LASTEXITCODE -ne 0) {
    throw "The benchmark venv is not a supported 64-bit Python 3.10-3.14 environment."
}

$NeedInstall = $Reinstall
if (-not $NeedInstall) {
    & $VenvPython -c "import importlib.metadata as m; v=next((d.version for d in m.distributions() if (d.metadata.get('Name') or '').lower() == 'openvino'), ''); raise SystemExit(0 if v == '2026.3.0' else 1)"
    $NeedInstall = $LASTEXITCODE -ne 0
}

if ($NeedInstall) {
    Write-Host "Installing pinned benchmark dependency openvino==2026.3.0 into the local venv..."
    & $VenvPython -m pip install --disable-pip-version-check -r $Requirements
    if ($LASTEXITCODE -ne 0) {
        throw "OpenVINO dependency installation failed."
    }
}

Write-Host "Running exact-landmark benchmark. No AUTO/HETERO fallback will be used."
$Arguments = @(
    $Benchmark,
    "--repo-root", $RepoRoot,
    "--warmup", $Warmup,
    "--iterations", $Iterations,
    "--copy-iterations", $CopyIterations,
    "--devices"
) + $Devices

& $VenvPython @Arguments
exit $LASTEXITCODE
