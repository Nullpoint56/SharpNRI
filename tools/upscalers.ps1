#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Runs the upscaler tests on a local payload with the upscaler SDKs and their vendor runtimes, and
  writes docs/upscalers.md from what they measured, stamped with the commit, the payload, the
  runtime files and the adapter each backend ran on.

.DESCRIPTION
  No SharpNRI package ships a vendor runtime (AGENTS.md: Native packages), and CI tests the Base
  payload, where every upscaler test skips. This is the local run that exercises them:
  `dotnet build -p:SharpNRIDevPayload=<payload>` puts that variant's NRI library beside the tests
  with the FSR, XeSS and DLSS runtimes NRI's own build copied into its build tree; the test classes
  UpscalerDispatchTests and UpscalerExtensionTests run with SHARPNRI_UPSCALER_FIGURES naming a file,
  where each measurement lands as a JSON line; the report is made from those lines. A run with a
  failed test publishes nothing. The build is put back to the Base payload at the end.

  Needs `pwsh tools/build-native.ps1 -Variant Upscalers` (NIS, FSR, XeSS) or
  `pwsh tools/build-native.ps1 -WithDlss <dir>` (DLSS too) first. Windows only: the vendor runtimes
  are Windows DLLs.

.PARAMETER Payload
  upscalers-dlss (default when staged) or upscalers.

.PARAMETER SkipTests
  Reuse the last run's results in _build/upscalers/ and only write the report.

.EXAMPLE
  pwsh tools/upscalers.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet("upscalers", "upscalers-dlss")]
    [string]$Payload,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture
$PSNativeCommandUseErrorActionPreference = $true
if (-not $IsWindows) { throw "the vendor upscaler runtimes are Windows DLLs; run this on Windows" }
$root = Split-Path $PSScriptRoot -Parent
$work = Join-Path $root "_build" "upscalers"
New-Item -ItemType Directory -Force -Path $work | Out-Null
$rid = "win-$([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString().ToLowerInvariant())"
$report = Join-Path $root "docs" "upscalers.md"
$figuresFile = Join-Path $work "figures.jsonl"
$runFile = Join-Path $work "tests.txt"
$measurementJson = Join-Path $work "measurement.json"
$testBin = Join-Path $root "tests" "SharpNRI.Tests" "bin" "Release" "net11.0"
$sln = Join-Path $root "SharpNRI.slnx"

if (-not $Payload) {
    $Payload = (Test-Path (Join-Path $root "runtimes-upscalers-dlss" $rid "native" "NRI.dll")) ? "upscalers-dlss" : "upscalers"
}
$staged = Join-Path $root "runtimes-$Payload" $rid "native" "NRI.dll"

if (-not $SkipTests) {
    if (-not (Test-Path $staged)) {
        throw "no $Payload payload staged ($staged): run pwsh tools/build-native.ps1 $(($Payload -eq 'upscalers') ? '-Variant Upscalers' : '-WithDlss <DLSS SDK directory>')"
    }
    Write-Host "==> dotnet build -p:SharpNRIDevPayload=$Payload"
    dotnet build $sln -c Release --nologo -v quiet "-p:SharpNRIDevPayload=$Payload" | Out-Host
    try {
        if ((Get-FileHash (Join-Path $testBin "NRI.dll")).Hash -ne (Get-FileHash $staged).Hash) { throw "the NRI.dll beside the tests is not the staged $Payload one" }

        # What ran: the commit, the payload, the vendor files beside the tests, the adapters
        $env:SHARPNRI_TEST_CHILD = "test-adapters"
        $PSNativeCommandUseErrorActionPreference = $false
        try { $lines = @(& (Join-Path $testBin "SharpNRI.Tests.exe") 2>&1 | ForEach-Object { "$_" }) }
        finally { Remove-Item Env:SHARPNRI_TEST_CHILD; $PSNativeCommandUseErrorActionPreference = $true }
        $adapters = @(foreach ($l in $lines) { if ($l -match '^ADAPTER (\S+) (.+)$') { "$($Matches[1]) on $($Matches[2])" } })
        if (-not $adapters) { throw "the test executable did not say which adapters its tests use: $($lines -join ' / ')" }
        $runtimes = @(Get-ChildItem $testBin -File | Where-Object { $_.Name -match '^(NRI\.dll|amd_fidelityfx_.*\.dll|libxess.*\.dll|nvngx_.*\.dll)$' } | Sort-Object Name | ForEach-Object {
            [pscustomobject]@{ Name = $_.Name; Version = ($_.VersionInfo.FileVersion -replace ',', '.' -replace ' ', ''); Bytes = $_.Length; Sha256 = (Get-FileHash $_.FullName).Hash.ToLowerInvariant() }
        })
        $sha = git -C $root rev-parse HEAD
        $measurement = [pscustomobject]@{
            Commit = $sha
            Dirty = [bool](git -C $root status --porcelain --ignore-submodules=dirty -- dotnet descs testing tests patches nri)
            Date = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd")
            Nri = (git -C (Join-Path $root "nri") describe --tags --always)
            Patches = @(Get-ChildItem (Join-Path $root "patches") -Filter "*.patch").Count
            Payload = $Payload
            Adapters = $adapters
            Runtimes = $runtimes
        }

        Write-Host "==> upscaler tests ($($adapters -join '; '))"
        Remove-Item $figuresFile -ErrorAction SilentlyContinue
        $env:SHARPNRI_UPSCALER_FIGURES = $figuresFile
        $PSNativeCommandUseErrorActionPreference = $false
        try {
            $output = & (Join-Path $testBin "SharpNRI.Tests.exe") -class SharpNRI.Tests.UpscalerDispatchTests -class SharpNRI.Tests.UpscalerExtensionTests 2>&1 | ForEach-Object { "$_" }
            $exit = $LASTEXITCODE
        }
        finally { Remove-Item Env:SHARPNRI_UPSCALER_FIGURES; $PSNativeCommandUseErrorActionPreference = $true }
        $output | Set-Content $runFile
        if ($exit -ne 0) { $output | Select-String '\[FAIL\]' -Context 0, 4 | Out-Host; throw "upscaler tests failed ($exit); figures of a failing run are not published" }
        $measurement | ConvertTo-Json -Depth 4 | Set-Content $measurementJson
    }
    finally {
        Write-Host "==> dotnet build (Base payload back beside the tests)"
        dotnet build $sln -c Release --nologo -v quiet | Out-Host
    }
}
if (-not ((Test-Path $figuresFile) -and (Test-Path $measurementJson) -and (Test-Path $runFile))) { throw "no complete run in $work; run without -SkipTests" }
$m = Get-Content $measurementJson -Raw | ConvertFrom-Json
$run = Get-Content $runFile
$total = ($run | Select-String 'Total: (\d+), Errors: (\d+), Failed: (\d+), Skipped: (\d+)').Matches[0].Groups
$figures = @(Get-Content $figuresFile | ForEach-Object { $_ | ConvertFrom-Json })
$skips = @(for ($i = 0; $i -lt $run.Count; $i++) {
    if ($run[$i] -match '^\s+(\S+)\((.*)\) \[SKIP\]$') { [pscustomobject]@{ Test = ($Matches[1] -replace '^SharpNRI\.Tests\.', ''); Args = $Matches[2]; Reason = $run[$i + 1].Trim() } }
})

# ------------------------------------------------------------------------------------------ report
$f = { param($v, $digits = 4) if ($null -eq $v) { "-" } else { ([double]$v).ToString("F$digits") } }
$backends = "VK", "D3D12"
$types = "NIS", "FSR", "XESS", "DLSR", "DLRR"
$modes = "NATIVE", "ULTRA_QUALITY", "QUALITY", "BALANCED", "PERFORMANCE", "ULTRA_PERFORMANCE"
$pick = { param($kind, $backend, $type) @($figures | Where-Object { $_.kind -eq $kind -and $_.backend -eq $backend -and $_.type -eq $type }) }
$stamp = $m.Dirty ? "$($m.Commit.Substring(0, 7)) plus uncommitted changes" : $m.Commit.Substring(0, 7)

$md = [System.Collections.Generic.List[string]]::new()
$md.Add("# Upscalers")
$md.Add("")
$md.Add("Measured at commit ``$stamp`` on $($m.Date): NRI ``$($m.Nri)`` with $($m.Patches) patches, the local ``$($m.Payload)``")
$md.Add("payload (``-p:SharpNRIDevPayload=$($m.Payload)``), on Windows with $($m.Adapters -join '; ').")
$md.Add("Tests: $($total[1].Value) total, $($total[3].Value) failed, $($total[4].Value) skipped (``UpscalerDispatchTests`` and ``UpscalerExtensionTests``).")
$md.Add("")
$md.Add("These figures describe that commit, payload and machine only. Generated by ``pwsh tools/upscalers.ps1``")
$md.Add("(AGENTS.md: Upscalers and vendor runtimes); do not edit. CI never runs these tests: no SharpNRI")
$md.Add("package ships the vendor runtimes, and CI tests the Base payload, where they skip.")
$md.Add("")
$md.Add("## Runtime files beside the tests")
$md.Add("")
$md.Add("| File | Version | Bytes | SHA-256 |")
$md.Add("|---|---|---:|---|")
foreach ($r in $m.Runtimes) { $md.Add("| ``$($r.Name)`` | $($r.Version) | $($r.Bytes) | ``$($r.Sha256)`` |") }
$md.Add("")
$md.Add("``NRI.dll`` is the staged ``$($m.Payload)`` build of the pinned NRI; the others are what NRI's own build")
$md.Add("copied into its build tree from the SDKs it fetched (and, for DLSS, from the local DLSS SDK).")
$md.Add("")

$md.Add("## What runs where")
$md.Add("")
$md.Add("| Type | " + ($backends -join " | ") + " |")
$md.Add("|---|" + (($backends | ForEach-Object { "---" }) -join "|") + "|")
foreach ($t in $types) {
    $cells = foreach ($b in $backends) {
        if (& $pick "props" $b $t) { "dispatched" }
        else {
            $reason = ($skips | Where-Object { $_.Test -like 'UpscalerDispatchTests.Every_mode*' -and $_.Args -match "backend: $b, type: $t" } | Select-Object -First 1).Reason
            $reason ? "not offered: $($reason -replace '^\S+: ', '')" : "not measured"
        }
    }
    $md.Add("| $t | " + ($cells -join " | ") + " |")
}
$md.Add("")

$md.Add("## Render resolutions for a 3840x2160 output")
$md.Add("")
$md.Add("``GetUpscalerProps`` per mode, the same on every type and backend measured (the test asserts the")
$md.Add("documented factors, rounding, mip bias and jitter phases); the minimum render resolution is the")
$md.Add("type's own. Creation time is the wall-clock time of ``CreateUpscaler`` with validation on.")
$md.Add("")
$md.Add("| Mode | Factor | Render | Mip bias | Jitter phases |")
$md.Add("|---|---:|---|---:|---:|")
$first = $figures | Where-Object kind -eq "props" | Group-Object { $_.values.mode } | ForEach-Object { $_.Group[0] }
foreach ($mode in $modes) {
    $p = ($first | Where-Object { $_.values.mode -eq $mode }).values
    if ($p) { $md.Add("| $mode | $($p.scalingFactor) | $($p.render) | $(& $f $p.mipBias 3) | $($p.jitterPhaseNum) |") }
}
$md.Add("")
$md.Add("| Backend | Type | Minimum render resolution per mode (NATIVE ... ULTRA_PERFORMANCE) | Creation ms per mode |")
$md.Add("|---|---|---|---|")
foreach ($b in $backends) { foreach ($t in $types) {
    $rows = & $pick "props" $b $t
    if (-not $rows) { continue }
    $min = ($modes | ForEach-Object { $mode = $_; ($rows | Where-Object { $_.values.mode -eq $mode }).values.renderMin }) -join ", "
    $ms = ($modes | ForEach-Object { $mode = $_; ($rows | Where-Object { $_.values.mode -eq $mode }).values.createMs }) -join ", "
    $md.Add("| $b | $t | $min | $ms |")
} }
$md.Add("")

$md.Add("## A constant image stays constant")
$md.Add("")
$md.Add("Largest deviation of any output channel from the input colour (0.25, 0.5, 0.75), 512x288 output,")
$md.Add("8 jittered frames; in brackets the same at the mode's minimum render resolution where that is")
$md.Add("below the render resolution (dynamic resolution). Asserted at most 0.02.")
$md.Add("")
$md.Add("| Backend | Type | " + ($modes -join " | ") + " |")
$md.Add("|---|---|" + (($modes | ForEach-Object { "---:" }) -join "|") + "|")
foreach ($b in $backends) { foreach ($t in $types) {
    $rows = & $pick "constant" $b $t
    if (-not $rows) { continue }
    $cells = foreach ($mode in $modes) {
        $v = ($rows | Where-Object { $_.values.mode -eq $mode }).values
        if (-not $v) { "-" } elseif ($null -ne $v.dynamicDeviation) { "$(& $f $v.deviation) ($(& $f $v.dynamicDeviation))" } else { & $f $v.deviation }
    }
    $md.Add("| $b | $t | " + ($cells -join " | ") + " |")
} }
$md.Add("")

$md.Add("## Optional inputs, history, motion and sharpening")
$md.Add("")
$md.Add("QUALITY mode, 512x288 output. **HDR**: relative deviation of an HDR constant (8, 4, 2) with an")
$md.Add("exposure texture and a pre-exposure of 2. **Reactive**: deviation of the constant with an all-zero")
$md.Add("reactive mask in R8_UNORM / R16_SFLOAT. **Reset**: deviation from a new colour one frame after")
$md.Add("the change, with RESET_HISTORY / without (only the first is asserted). **Edge**: a vertical edge")
$md.Add("moving 2 render pixels a frame with matching motion vectors, worst row's distance from where the")
$md.Add("scene puts it in output pixels (asserted at most 2), and the flat sides' deviation. **Sharpness**:")
$md.Add("output amplitude of stripes of input amplitude 0.1 at sharpness 0 / 1 (the types with a setting).")
$md.Add("")
$md.Add("| Backend | Type | HDR | Reactive R8 / R16 | Reset with / without | Edge px / sides | Sharpness 0 / 1 |")
$md.Add("|---|---|---:|---|---|---|---|")
foreach ($b in $backends) { foreach ($t in $types) {
    if (-not (& $pick "props" $b $t)) { continue }
    $hdr = (& $pick "hdr" $b $t).values
    $reactive = & $pick "reactive" $b $t
    $r8 = ($reactive | Where-Object { $_.values.format -eq "R8_UNORM" }).values.deviation
    $r16 = ($reactive | Where-Object { $_.values.format -eq "R16_SFLOAT" }).values.deviation
    $reset = (& $pick "reset" $b $t).values
    $edge = (& $pick "edge" $b $t).values
    $sharp = (& $pick "sharpness" $b $t).values
    $md.Add("| $b | $t | $(& $f $hdr.relativeDeviation) | " +
        ($reactive ? "$(($null -eq $r8) ? 'skipped' : (& $f $r8)) / $(& $f $r16)" : "-") + " | " +
        ($reset ? "$(& $f $reset.withReset) / $(& $f $reset.withoutReset)" : "-") + " | " +
        ($edge ? "$(& $f $edge.edgeError 2) / $(& $f $edge.sideDeviation)" : "-") + " | " +
        ($sharp ? "$(& $f $sharp.amplitude0) / $(& $f $sharp.amplitude1)" : "-") + " |")
} }
$md.Add("")

$md.Add("## GPU time per dispatch, 3840x2160 output")
$md.Add("")
$md.Add("Median of 32 dispatches after 8 warm-up ones, between two timestamps, in milliseconds; validation")
$md.Add("off; a constant input (an upscaler's cost does not depend on content much, but this is not a game")
$md.Add("frame). Render resolution per mode as in the table above.")
$md.Add("")
$md.Add("| Backend | Type | " + ($modes -join " | ") + " |")
$md.Add("|---|---|" + (($modes | ForEach-Object { "---:" }) -join "|") + "|")
foreach ($b in $backends) { foreach ($t in $types) {
    $rows = & $pick "timing" $b $t
    if (-not $rows) { continue }
    $cells = foreach ($mode in $modes) { $v = ($rows | Where-Object { $_.values.mode -eq $mode }).values; $v ? (& $f $v.medianMs 3) : "-" }
    $md.Add("| $b | $t | " + ($cells -join " | ") + " |")
} }
$md.Add("")

$md.Add("## Skipped")
$md.Add("")
$md.Add("| Test | Arguments | Reason |")
$md.Add("|---|---|---|")
foreach ($s in $skips) { $md.Add("| ``$($s.Test)`` | $($s.Args) | $($s.Reason -replace '\|', '\|') |") }
$md.Add("")

($md -join "`n") + "`n" | Set-Content $report -NoNewline
Write-Host "measured at $stamp with $($m.Payload): $($total[1].Value) tests, $($total[3].Value) failed, $($total[4].Value) skipped"
Write-Host "==> docs/upscalers.md"
