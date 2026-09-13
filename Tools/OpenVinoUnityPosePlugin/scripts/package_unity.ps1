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
if ([string]$State.openvino_version -ne "2026.3.0") {
    throw "FAIL CLOSED: packaging requires the approved OpenVINO 2026.3.0 state."
}
$OpenVinoRoot = [IO.Path]::GetFullPath([string]$State.openvino_root)
$Plugin = Join-Path $Build "golden_needle_openvino_pose.dll"
$Smoke = Join-Path $Build "gnovpose_smoke.exe"
if (-not (Test-Path $Plugin) -or -not (Test-Path $Smoke)) { throw "Run build.ps1 before packaging." }

$Destination = Join-Path $RepoRoot "Assets\GoldenNeedle\Plugins\OpenVinoPose\x86_64"
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
Get-ChildItem $Destination -File | Where-Object {
    $_.Extension -in @(".dll", ".pdb") -or $_.Name -eq "plugins.xml"
} | Remove-Item -Force

Copy-Item -Force $Plugin (Join-Path $Destination "golden_needle_openvino_pose.dll")

$releaseBin = Join-Path $OpenVinoRoot "runtime\bin\intel64\Release"
$requiredSeeds = @(
    (Join-Path $releaseBin "openvino.dll"),
    (Join-Path $releaseBin "openvino_intel_cpu_plugin.dll"),
    (Join-Path $releaseBin "openvino_tensorflow_lite_frontend.dll")
)
foreach ($seed in $requiredSeeds) {
    if (-not (Test-Path $seed)) { throw "Required CPU/TFLite runtime library missing: $seed" }
}

# Stage the explicit CPU/TFLite runtime plus its OpenVINO-owned transitive DLL
# closure. This intentionally avoids copying every setupvars PATH DLL (GPU/NPU,
# tools, duplicate loaders, etc.). PE import names are stored as ASCII strings;
# only names that resolve inside this exact verified OpenVINO root are followed.
$allOpenVinoDlls = @(Get-ChildItem $OpenVinoRoot -Recurse -Filter *.dll -File)
function Resolve-OpenVinoDll([string]$Name) {
    $matches = @($allOpenVinoDlls | Where-Object { $_.Name -ieq $Name })
    if ($matches.Count -eq 0) { return $null }
    if ($matches.Count -eq 1) { return $matches[0] }

    $preferred = @($matches | Where-Object {
        $_.DirectoryName -ieq $releaseBin
    })
    if ($preferred.Count -eq 1) { return $preferred[0] }

    $groups = @($matches | Group-Object { (Get-FileHash -Algorithm SHA256 $_.FullName).Hash })
    if ($groups.Count -eq 1) { return $matches[0] }
    throw "Ambiguous OpenVINO dependency '$Name' has multiple different binaries and no unique Release runtime candidate."
}

$queue = New-Object System.Collections.Generic.Queue[System.IO.FileInfo]
$seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($seed in $requiredSeeds) { $queue.Enqueue((Get-Item $seed)) }
while ($queue.Count -gt 0) {
    $file = $queue.Dequeue()
    if (-not $seen.Add($file.Name)) { continue }
    Copy-Item -Force $file.FullName (Join-Path $Destination $file.Name)

    $ascii = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($file.FullName))
    $names = @([regex]::Matches($ascii, '(?i)\b[A-Za-z0-9_.-]+\.dll\b') |
        ForEach-Object { $_.Value } | Select-Object -Unique)
    foreach ($name in $names) {
        if ($seen.Contains($name)) { continue }
        $dependency = Resolve-OpenVinoDll $name
        if ($null -ne $dependency) { $queue.Enqueue($dependency) }
    }
}

$pluginsXml = Join-Path $releaseBin "plugins.xml"
if (-not (Test-Path $pluginsXml)) {
    $pluginsXml = (Get-ChildItem $OpenVinoRoot -Recurse -Filter plugins.xml -File | Select-Object -First 1).FullName
}
if (-not $pluginsXml -or -not (Test-Path $pluginsXml)) { throw "OpenVINO plugins.xml was not found." }
Copy-Item -Force $pluginsXml (Join-Path $Destination "plugins.xml")

$PackagedPlugin = Join-Path $Destination "golden_needle_openvino_pose.dll"
& $Smoke $PackagedPlugin
if ($LASTEXITCODE -ne 0) {
    throw "Packaged U1 plugin failed same-process Load/Version/SelfTest/Unload. CPU runtime dependency set is incomplete."
}

$staged = @(Get-ChildItem $Destination -Filter *.dll -File | Sort-Object Name | ForEach-Object { $_.Name })
Write-Host "[U1] Unity additive package PASS: $Destination"
Write-Host "[U1] staged CPU/TFLite DLLs: $($staged -join ', ')"
Write-Host "[U1] stock MediaPipe/TFLite plugin files were not replaced or renamed."
