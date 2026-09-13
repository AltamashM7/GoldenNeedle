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
$allOpenVinoDlls = @(Get-ChildItem $OpenVinoRoot -Recurse -Filter *.dll -File)
function Resolve-OpenVinoDll([string]$Name) {
    $matches = @($allOpenVinoDlls | Where-Object { $_.Name -ieq $Name })
    if ($matches.Count -eq 0) { throw "Required OpenVINO runtime dependency '$Name' was not found." }
    if ($matches.Count -eq 1) { return $matches[0] }

    $preferred = @($matches | Where-Object { $_.DirectoryName -ieq $releaseBin })
    if ($preferred.Count -eq 1) { return $preferred[0] }

    $hashes = @($matches | ForEach-Object { (Get-FileHash -Algorithm SHA256 $_.FullName).Hash } | Select-Object -Unique)
    if ($hashes.Count -eq 1) { return $matches[0] }
    throw "Ambiguous OpenVINO dependency '$Name' has multiple different binaries and no unique Release runtime candidate."
}

# Exact CPU/TFLite runtime set. U1 run #5 demonstrated these oneTBB names are
# the runtime dependencies reached by OpenVINO 2026.3.0 on Windows. Do not use
# a broad string scan here: OpenVINO core embeds names for optional GPU/NPU/AUTO
# plugins even when those DLLs are not dependencies of this CPU-only package.
$runtimeNames = @(
    "openvino.dll",
    "openvino_intel_cpu_plugin.dll",
    "openvino_tensorflow_lite_frontend.dll",
    "tbb12.dll",
    "tbbbind_2_5.dll",
    "tbbmalloc.dll"
)
foreach ($name in $runtimeNames) {
    $source = Resolve-OpenVinoDll $name
    Copy-Item -Force $source.FullName (Join-Path $Destination $source.Name)
}

# The dedicated runtime is CPU-only. OpenVINO's dynamic Core accepts a local
# plugins.xml registry; generating the narrow registry here prevents accidental
# GPU/NPU/AUTO/HETERO registration and makes backend identity deterministic.
$pluginsXml = Join-Path $Destination "plugins.xml"
@'
<ie>
  <plugins>
    <plugin name="CPU" location="openvino_intel_cpu_plugin.dll"/>
  </plugins>
</ie>
'@ | Set-Content -Encoding UTF8 $pluginsXml

$forbidden = @(
    "openvino_intel_gpu_plugin.dll",
    "openvino_intel_npu_plugin.dll",
    "openvino_auto_plugin.dll",
    "openvino_auto_batch_plugin.dll",
    "openvino_hetero_plugin.dll"
)
foreach ($name in $forbidden) {
    if (Test-Path (Join-Path $Destination $name)) {
        throw "FAIL CLOSED: CPU-only Unity package unexpectedly contains $name"
    }
}

$PackagedPlugin = Join-Path $Destination "golden_needle_openvino_pose.dll"
& $Smoke $PackagedPlugin
if ($LASTEXITCODE -ne 0) {
    throw "Packaged U1 plugin failed same-process Load/Version/SelfTest/Unload. CPU runtime dependency set is incomplete."
}

$staged = @(Get-ChildItem $Destination -Filter *.dll -File | Sort-Object Name | ForEach-Object { $_.Name })
Write-Host "[U1] Unity additive package PASS: $Destination"
Write-Host "[U1] staged CPU/TFLite DLLs: $($staged -join ', ')"
Write-Host "[U1] generated CPU-only plugins.xml PASS"
Write-Host "[U1] GPU/NPU/AUTO/HETERO exclusion guard PASS"
Write-Host "[U1] stock MediaPipe/TFLite plugin files were not replaced or renamed."
