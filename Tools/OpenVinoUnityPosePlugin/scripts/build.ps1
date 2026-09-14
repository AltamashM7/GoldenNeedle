$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$NativeBuild = Join-Path $ScriptRoot "build_native.ps1"
$Regression = Join-Path $ScriptRoot "run_regression.ps1"
foreach ($path in @($NativeBuild, $Regression)) {
    if (-not (Test-Path $path)) {
        throw "Required U2 build stage script is missing: $path"
    }
}

& $NativeBuild
if ($LASTEXITCODE -ne 0) { throw "U2 native build failed." }

& $Regression
if ($LASTEXITCODE -ne 0) { throw "U2 regression failed after native build." }
