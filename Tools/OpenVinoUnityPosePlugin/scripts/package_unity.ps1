param(
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$ToolRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if (-not $RepoRoot) {
    $RepoRoot = (Resolve-Path (Join-Path $ToolRoot "..\..")).Path
} else {
    $RepoRoot = (Resolve-Path $RepoRoot).Path
}
$StatePath = Join-Path $ToolRoot ".work\bootstrap-state.json"
$Build = Join-Path $ToolRoot "build\Release"
if (-not (Test-Path $StatePath)) { throw "Run bootstrap.ps1 first." }
$State = Get-Content $StatePath -Raw | ConvertFrom-Json
$OpenVinoRoot = [string]$State.openvino_root
$Plugin = Join-Path $Build "golden_needle_openvino_pose.dll"
$Smoke = Join-Path $Build "gnovpose_smoke.exe"
if (-not (Test-Path $Plugin) -or -not (Test-Path $Smoke)) { throw "Run build.ps1 before packaging." }

$Destination = Join-Path $RepoRoot "Assets\GoldenNeedle\Plugins\OpenVinoPose\x86_64"
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
Get-ChildItem $Destination -File | Where-Object {
    $_.Extension -in @(".dll", ".pdb") -or $_.Name -eq "plugins.xml"
} | Remove-Item -Force

Copy-Item -Force $Plugin (Join-Path $Destination "golden_needle_openvino_pose.dll")

function Copy-UniqueRuntimeDll([IO.FileInfo]$File) {
    $target = Join-Path $Destination $File.Name
    if (Test-Path $target) {
        $existing = (Get-FileHash -Algorithm SHA256 $target).Hash
        $incoming = (Get-FileHash -Algorithm SHA256 $File.FullName).Hash
        if ($existing -ne $incoming) { throw "Runtime DLL name collision with different content: $($File.Name)" }
        return
    }
    Copy-Item -Force $File.FullName $target
}

$SetupVars = Join-Path $OpenVinoRoot "setupvars.bat"
if (-not (Test-Path $SetupVars)) { throw "OpenVINO setupvars.bat missing: $SetupVars" }
$envLines = & cmd.exe /d /s /c "call `"$SetupVars`" >nul && set"
if ($LASTEXITCODE -ne 0) { throw "OpenVINO setupvars.bat failed during packaging." }
$ovPath = $null
foreach ($line in $envLines) {
    $eq = $line.IndexOf("=")
    if ($eq -le 0) { continue }
    $name = $line.Substring(0, $eq)
    $value = $line.Substring($eq + 1)
    if ($name -ieq "PATH") { $ovPath = $value }
}
if (-not $ovPath) { throw "OpenVINO setupvars.bat did not produce PATH." }
$rootNormalized = [IO.Path]::GetFullPath($OpenVinoRoot).TrimEnd('\') + '\'
$runtimeDirs = @($ovPath -split ';' | Where-Object {
    $_ -and (Test-Path $_) -and [IO.Path]::GetFullPath($_).StartsWith($rootNormalized, [StringComparison]::OrdinalIgnoreCase)
} | Select-Object -Unique)
if ($runtimeDirs.Count -eq 0) { throw "No OpenVINO runtime PATH directories were discovered." }
foreach ($dir in $runtimeDirs) {
    Get-ChildItem $dir -Filter *.dll -File | ForEach-Object { Copy-UniqueRuntimeDll $_ }
}

$pluginsXml = Get-ChildItem $OpenVinoRoot -Recurse -Filter plugins.xml -File | Select-Object -First 1
if ($pluginsXml) { Copy-Item -Force $pluginsXml.FullName (Join-Path $Destination "plugins.xml") }

$PackagedPlugin = Join-Path $Destination "golden_needle_openvino_pose.dll"
& $Smoke $PackagedPlugin
if ($LASTEXITCODE -ne 0) {
    throw "Packaged U1 plugin failed same-process Load/Version/SelfTest/Unload. Runtime dependency set is incomplete."
}

Write-Host "[U1] Unity additive package PASS: $Destination"
Write-Host "[U1] stock MediaPipe/TFLite plugin files were not replaced or renamed."
