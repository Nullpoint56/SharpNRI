#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Measures PrismNRI's two coverages and writes docs/coverage.md plus the two README badges.

.DESCRIPTION
  Bound surface: which of NRI's functions and native struct fields PrismNRI's own C# API reaches.
  tools/coverage/BoundSurface reads the built assemblies: every function-pointer field of NRI's
  interface tables and every flat export, bound when binding code calls it; every field of a native
  struct, bound when the idiomatic API exposes the struct or binding code reads or writes the field
  (data layouts no NRI function takes count through their public type). Every call site is mapped
  to its source line, and the report lists the bound functions whose call sites the tests never ran.

  Test coverage: which lines of PrismNRI's hand-written C# (dotnet/Interop, Core, Extensions,
  descs/, testing/) the test suite executes, through Microsoft.Testing.Extensions.CodeCoverage on
  `dotnet test`, child processes included. The generated L0 (dotnet/Native) is not counted.

  Both depend on what the machine can run: on the Windows NVIDIA development machine nearly every
  test runs; CI's WARP and lavapipe runners skip half of them. The committed report says where it
  was measured. Run after `pwsh tools/build-native.ps1` (the tests need the native library).

.PARAMETER SkipBuild
  Use the existing Release build (made with -p:PrismNRICoverage=true, as this script builds).

.PARAMETER SkipTests
  Reuse _build/coverage/coverage.cobertura.xml and _build/coverage/tests.txt from the last run.

.EXAMPLE
  pwsh tools/coverage.ps1
#>
[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
# Numbers in the report and badges read the same whatever the machine's locale
[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture
$PSNativeCommandUseErrorActionPreference = $true
$root = Split-Path $PSScriptRoot -Parent
$work = Join-Path $root "_build" "coverage"
New-Item -ItemType Directory -Force -Path $work | Out-Null
$nl = [Environment]::NewLine

# ------------------------------------------------------------------------------------------ build
# PrismNRICoverage brings Microsoft.Testing.Extensions.CodeCoverage into the test project, which
# only this script uses (tests/PrismNRI.Tests/PrismNRI.Tests.csproj says why it is off otherwise)
if (-not $SkipBuild) {
    Write-Host "==> dotnet build -p:PrismNRICoverage=true"
    dotnet build (Join-Path $root "PrismNRI.slnx") -c Release --nologo -v quiet -p:PrismNRICoverage=true | Out-Host
}
$testDeps = Join-Path $root "tests" "PrismNRI.Tests" "bin" "Release" "net11.0" "PrismNRI.Tests.deps.json"
if (-not $SkipTests -and -not ((Test-Path $testDeps) -and (Select-String -Path $testDeps -Pattern "Microsoft.Testing.Extensions.CodeCoverage" -SimpleMatch -Quiet))) {
    throw "the test build has no coverage extension: run without -SkipBuild, or build with -p:PrismNRICoverage=true"
}
dotnet build (Join-Path $root "tools" "coverage" "BoundSurface") -c Release --nologo -v quiet | Out-Host

# ------------------------------------------------------------------------------------------ bound surface
$bin = Join-Path $root "dotnet" "bin" "Release" "net11.0"
$boundJson = Join-Path $work "bound.json"
Write-Host "==> bound surface"
dotnet (Join-Path $root "tools" "coverage" "BoundSurface" "bin" "Release" "net11.0" "BoundSurface.dll") `
    (Join-Path $bin "PrismNRI.Descs.dll") (Join-Path $bin "PrismNRI.dll") $boundJson | Out-Host
$bound = Get-Content $boundJson -Raw | ConvertFrom-Json

# ------------------------------------------------------------------------------------------ test coverage
$cobertura = Join-Path $work "coverage.cobertura.xml"
$testsTxt = Join-Path $work "tests.txt"
if (-not $SkipTests) {
    Write-Host "==> dotnet test --coverage (the whole suite)"
    Remove-Item $cobertura -ErrorAction SilentlyContinue
    $PSNativeCommandUseErrorActionPreference = $false
    $output = dotnet test --project (Join-Path $root "tests" "PrismNRI.Tests") -c Release --no-build `
        --coverage --coverage-output-format cobertura --coverage-output "coverage.cobertura.xml" `
        --coverage-settings (Join-Path $root "tools" "coverage" "coverage.settings.xml") --results-directory $work 2>&1
    $testExit = $LASTEXITCODE
    $PSNativeCommandUseErrorActionPreference = $true
    $output | Set-Content $testsTxt
    if ($testExit -ne 0) { $output | Select-Object -Last 30 | Out-Host; throw "dotnet test failed ($testExit); coverage of a failing suite is not published" }
}
if (-not (Test-Path $cobertura)) { throw "no $cobertura; run without -SkipTests" }
$testLog = Get-Content $testsTxt -Raw
$count = { param($name) if ($testLog -match "(?m)^\s*${name}:\s*(\d+)") { [int]$Matches[1] } else { 0 } }
$tests = [pscustomobject]@{ Total = & $count "total"; Passed = & $count "succeeded"; Failed = & $count "failed"; Skipped = & $count "skipped" }

# Lines and branches per source file, the maximum over every class that maps lines to it
[xml]$xml = Get-Content $cobertura -Raw
$files = @{}
foreach ($class in $xml.coverage.packages.package.classes.class) {
    $path = [IO.Path]::GetRelativePath($root, $class.filename) -replace '\\', '/'
    if (-not $files.ContainsKey($path)) { $files[$path] = @{} }
    foreach ($line in $class.lines.line) {
        $n = [int]$line.number
        $hits = [int]$line.hits
        $branches = if ($line.'condition-coverage' -match '\((\d+)/(\d+)\)') { @([int]$Matches[1], [int]$Matches[2]) } else { @(0, 0) }
        $old = $files[$path][$n]
        if (-not $old -or $hits -gt $old.Hits -or $branches[0] -gt $old.Taken) {
            $files[$path][$n] = @{ Hits = [Math]::Max($hits, ($old ? $old.Hits : 0)); Taken = [Math]::Max($branches[0], ($old ? $old.Taken : 0)); Branches = [Math]::Max($branches[1], ($old ? $old.Branches : 0)) }
        }
    }
}
function Area([string]$path) {
    switch -Regex ($path) {
        '^dotnet/Native/' { return $null }        # generated L0
        '^dotnet/Interop/' { return "L1 dotnet/Interop" }
        '^dotnet/Core/' { return "L2 dotnet/Core" }
        '^dotnet/Extensions/' { return "L3 dotnet/Extensions" }
        '^descs/' { return "descs/ (managed desc forms)" }
        '^testing/' { return "testing/ (PrismNRI.Testing)" }
        '^dotnet/' { return "dotnet/ (other)" }
        default { return $null }
    }
}
$fileRows = foreach ($path in $files.Keys) {
    $area = Area $path
    if (-not $area) { continue }
    $lines = $files[$path].Values
    [pscustomobject]@{
        Path = $path; Area = $area
        Lines = $lines.Count; Covered = @($lines | Where-Object Hits -gt 0).Count
        Branches = ($lines | Measure-Object Branches -Sum).Sum; Taken = ($lines | Measure-Object Taken -Sum).Sum
    }
}
$total = [pscustomobject]@{
    Lines = ($fileRows | Measure-Object Lines -Sum).Sum; Covered = ($fileRows | Measure-Object Covered -Sum).Sum
    Branches = ($fileRows | Measure-Object Branches -Sum).Sum; Taken = ($fileRows | Measure-Object Taken -Sum).Sum
}
$bt = [char]96
function Code([string]$s) { "$bt$s$bt" }
function Pct($a, $b) { if ($b -eq 0) { "-" } else { "{0:0.0}%" -f (100.0 * $a / $b) } }

# Methods with lines and none of them run, in hand-written files; overloads carry their parameters
$signatures = @{}
foreach ($class in $xml.coverage.packages.package.classes.class) {
    foreach ($method in $class.methods.method) {
        $key = "$($class.name)|$($method.name)"
        if (-not $signatures.ContainsKey($key)) { $signatures[$key] = [System.Collections.Generic.HashSet[string]]::new() }
        [void]$signatures[$key].Add($method.signature)
    }
}
$unrun = foreach ($class in $xml.coverage.packages.package.classes.class) {
    $path = [IO.Path]::GetRelativePath($root, $class.filename) -replace '\\', '/'
    if (-not (Area $path)) { continue }
    $className = ($class.name -replace '[./]<>c(__DisplayClass\d+_\d+)?$', '' -replace '[./]<[^>]*>d__\d+$', '') -replace '^PrismNRI(\.Testing)?\.', ''
    foreach ($method in $class.methods.method) {
        $lines = @($method.lines.line)
        if ($lines.Count -eq 0 -or @($lines | Where-Object { [int]$_.hits -gt 0 }).Count -gt 0) { continue }
        $name = $method.name
        if ($name -match '^<([^>]+)>') { $name = "$($Matches[1]) (lambda)" }
        elseif ($signatures["$($class.name)|$name"].Count -gt 1) { $name += $method.signature -replace '\b(PrismNRI\.(Native\.|Interop\.)?|System\.)', '' }
        [pscustomobject]@{ Path = $path; Class = $className; Method = $name; Line = [int]$lines[0].number }
    }
}
$unrun = $unrun | Sort-Object Path, Class, Line, Method -Unique

# Bound NRI functions whose call sites the tests never ran
function LineRan([string]$file, [int]$line) {
    if (-not $file) { return $false }
    $path = [IO.Path]::GetRelativePath($root, $file) -replace '\\', '/'
    return $files.ContainsKey($path) -and $files[$path].ContainsKey($line) -and $files[$path][$line].Hits -gt 0
}
$functionRows = foreach ($table in $bound.tables) {
    foreach ($f in $table.functions) {
        [pscustomobject]@{ Table = $table.name; Name = $f.name; Bound = $f.callers.Count -gt 0; Ran = @($f.callers | Where-Object { LineRan $_.File $_.Line }).Count -gt 0; Callers = $f.callers }
    }
}
$functionRows += foreach ($e in $bound.exports) {
    [pscustomobject]@{ Table = "flat exports"; Name = $e.name; Bound = $e.callers.Count -gt 0; Ran = @($e.callers | Where-Object { LineRan $_.File $_.Line }).Count -gt 0; Callers = $e.callers }
}
$fieldRows = foreach ($s in $bound.structs) { foreach ($f in $s.fields) { [pscustomobject]@{ Struct = $s.name; Kind = $s.kind; Name = $f.name; Bound = $f.bound } } }

$fnBound = @($functionRows | Where-Object Bound).Count
$fnTotal = @($functionRows).Count
$fnRan = @($functionRows | Where-Object Ran).Count
$fieldBound = @($fieldRows | Where-Object Bound).Count
$fieldTotal = @($fieldRows).Count

# ------------------------------------------------------------------------------------------ report
$commit = (git -C $root rev-parse --short HEAD)
# What the numbers measure: the binding, its tests and the patch set (nri/ always shows the applied patches)
$dirty = git -C $root status --porcelain --ignore-submodules -- dotnet descs testing tests patches
if ($dirty) { $commit += " plus uncommitted changes" }
$nriRef = (git -C (Join-Path $root "nri") describe --tags --always)
$patchCount = @(Get-ChildItem (Join-Path $root "patches") -Filter "*.patch").Count
$gpus = if ($IsWindows) { (Get-CimInstance Win32_VideoController | ForEach-Object Name) -join ", " }
elseif (Get-Command vulkaninfo -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
    (vulkaninfo --summary 2>$null | Select-String '^\s*deviceName\s*=\s*(.+)$' | ForEach-Object { $_.Matches[0].Groups[1].Value.Trim() }) -join ", "
    $PSNativeCommandUseErrorActionPreference = $true
}
if (-not $gpus) { $gpus = uname -m }
$date = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd")
$rel = { param($file, $line) if ($file) { "$([IO.Path]::GetRelativePath($root, $file) -replace '\\', '/'):$line" } else { "?" } }

$md = [System.Collections.Generic.List[string]]::new()
$md.Add("# Coverage")
$md.Add("")
$md.Add("Generated by ``pwsh tools/coverage.ps1``; do not edit. Measured on $date at commit $commit, NRI ``$nriRef`` with $patchCount patches,")
$md.Add("on $($IsWindows ? 'Windows' : (uname -s)) ($gpus). Tests: $($tests.Total) total, $($tests.Passed) passed, $($tests.Failed) failed, $($tests.Skipped) skipped.")
$md.Add("")
$md.Add("- **Bound surface**: NRI's functions (every function-pointer field of the interface tables NRI")
$md.Add("  fills, and every flat export) that PrismNRI's own code calls, and the fields of NRI's native")
$md.Add("  structs that PrismNRI's idiomatic API reaches: a struct the public API takes or returns is")
$md.Add("  exposed with all its fields; a struct the binding builds from its own managed forms counts")
$md.Add("  only the fields binding code reads or writes; a data layout no NRI function takes (indirect")
$md.Add("  arguments, instances, query results) counts through its public type. The raw tables and")
$md.Add("  ``NriApi`` reach everything and are not counted. Read from the built assemblies (IL and PDB).")
$md.Add("- **Test coverage**: the lines of PrismNRI's hand-written C# that the test suite executes,")
$md.Add("  child processes included; the generated L0 (``dotnet/Native``) is not counted. It depends on")
$md.Add("  what the machine can run: CI's software adapters skip about half the tests.")
$md.Add("")
$md.Add("## Bound surface")
$md.Add("")
$md.Add("| Surface | Bound | Total | % | Call site run by the tests |")
$md.Add("|---|---|---|---|---|")
$md.Add("| NRI functions | $fnBound | $fnTotal | $(Pct $fnBound $fnTotal) | $fnRan ($(Pct $fnRan $fnTotal)) |")
$md.Add("| Native struct fields | $fieldBound | $fieldTotal | $(Pct $fieldBound $fieldTotal) | |")
$md.Add("")
$md.Add("| Table | Functions | Bound | Run by the tests |")
$md.Add("|---|---|---|---|")
foreach ($g in $functionRows | Group-Object Table) {
    $md.Add("| $($g.Name) | $($g.Count) | $(@($g.Group | Where-Object Bound).Count) | $(@($g.Group | Where-Object Ran).Count) |")
}
$md.Add("")
$unboundFunctions = @($functionRows | Where-Object { -not $_.Bound })
$list = if ($unboundFunctions.Count) { ($unboundFunctions | ForEach-Object { Code ("{0}.{1}" -f $_.Table, $_.Name) }) -join ", " } else { "none" }
$md.Add("Unbound functions: $list.")
$md.Add("")
$unboundFields = @($fieldRows | Where-Object { -not $_.Bound })
$list = if ($unboundFields.Count) { ($unboundFields | ForEach-Object { Code ("{0}.{1}" -f $_.Struct, $_.Name) }) -join ", " } else { "none" }
$md.Add("Unbound fields: $list.")
$md.Add("")
$layouts = @($bound.structs | Where-Object kind -eq "layout" | ForEach-Object { Code $_.name })
$md.Add("Data layouts, counted through their public type: $($layouts -join ', ').")
$md.Add("")
$md.Add("### Bound, but no test runs the call")
$md.Add("")
$md.Add("NRI functions whose call sites in the binding never executed during the suite. The C# method is the")
$md.Add("binding's wrapper; the line is where it calls NRI.")
$md.Add("")
$md.Add("| Table | NRI function | Binding call site |")
$md.Add("|---|---|---|")
foreach ($f in $functionRows | Where-Object { $_.Bound -and -not $_.Ran }) {
    $sites = ($f.Callers | ForEach-Object { (Code $_.Method) + " (" + (& $rel $_.File $_.Line) + ")" }) -join "<br>"
    $md.Add("| " + $f.Table + " | " + (Code $f.Name) + " | " + $sites + " |")
}
$md.Add("")
$md.Add("## Test coverage")
$md.Add("")
$md.Add("| Area | Lines | Covered | % | Branches | Taken | % |")
$md.Add("|---|---|---|---|---|---|---|")
foreach ($g in $fileRows | Group-Object Area | Sort-Object Name) {
    $l = ($g.Group | Measure-Object Lines -Sum).Sum; $c = ($g.Group | Measure-Object Covered -Sum).Sum
    $b = ($g.Group | Measure-Object Branches -Sum).Sum; $t = ($g.Group | Measure-Object Taken -Sum).Sum
    $md.Add("| $($g.Name) | $l | $c | $(Pct $c $l) | $b | $t | $(Pct $t $b) |")
}
$md.Add("| **Total** | $($total.Lines) | $($total.Covered) | $(Pct $total.Covered $total.Lines) | $($total.Branches) | $($total.Taken) | $(Pct $total.Taken $total.Branches) |")
$md.Add("")
$md.Add("### Methods no test runs")
$md.Add("")
$md.Add("Hand-written methods with lines, none of which executed. Property accessors appear as ``get_`` / ``set_``.")
$md.Add("")
$md.Add("| File | Class | Methods |")
$md.Add("|---|---|---|")
foreach ($g in $unrun | Group-Object Path, Class) {
    $first = $g.Group[0]
    $methods = ($g.Group | ForEach-Object { Code $_.Method }) -join ", "
    $md.Add("| " + $first.Path + " | " + (Code $first.Class) + " | " + $methods + " |")
}
$md.Add("")
$md.Add("### Files")
$md.Add("")
$md.Add("| File | Lines | Covered | % | Branches % |")
$md.Add("|---|---|---|---|---|")
foreach ($f in $fileRows | Sort-Object @{ Expression = { if ($_.Lines) { $_.Covered / $_.Lines } else { 1 } } }, Path) {
    $md.Add("| $($f.Path) | $($f.Lines) | $($f.Covered) | $(Pct $f.Covered $f.Lines) | $(Pct $f.Taken $f.Branches) |")
}
$docs = Join-Path $root "docs"
($md -join $nl) + $nl | Set-Content (Join-Path $docs "coverage.md") -NoNewline -Encoding utf8NoBOM

# ------------------------------------------------------------------------------------------ badges
function Badge([string]$label, [string]$value, [double]$percent, [string]$path) {
    $color = switch ($percent) { { $_ -ge 90 } { "#4c1"; break } { $_ -ge 75 } { "#97ca00"; break } { $_ -ge 60 } { "#a4a61d"; break } { $_ -ge 45 } { "#dfb317"; break } { $_ -ge 30 } { "#fe7d37"; break } default { "#e05d44" } }
    $lw = [int](6.6 * $label.Length) + 12
    $vw = [int](6.6 * $value.Length) + 12
    $w = $lw + $vw
    $svg = @"
<svg xmlns="http://www.w3.org/2000/svg" width="$w" height="20" role="img" aria-label="${label}: $value">
  <title>${label}: $value</title>
  <linearGradient id="s" x2="0" y2="100%"><stop offset="0" stop-color="#bbb" stop-opacity=".1"/><stop offset="1" stop-opacity=".1"/></linearGradient>
  <clipPath id="r"><rect width="$w" height="20" rx="3" fill="#fff"/></clipPath>
  <g clip-path="url(#r)"><rect width="$lw" height="20" fill="#555"/><rect x="$lw" width="$vw" height="20" fill="$color"/><rect width="$w" height="20" fill="url(#s)"/></g>
  <g fill="#fff" text-anchor="middle" font-family="Verdana,Geneva,DejaVu Sans,sans-serif" font-size="11">
    <text x="$($lw / 2)" y="15" fill="#010101" fill-opacity=".3">$label</text><text x="$($lw / 2)" y="14">$label</text>
    <text x="$($lw + $vw / 2)" y="15" fill="#010101" fill-opacity=".3">$value</text><text x="$($lw + $vw / 2)" y="14">$value</text>
  </g>
</svg>
"@
    ($svg -replace "`r`n", "`n") + "`n" | Set-Content $path -NoNewline -Encoding utf8NoBOM
}
$badges = Join-Path $docs "badges"
New-Item -ItemType Directory -Force -Path $badges | Out-Null
$fnPct = 100.0 * $fnBound / $fnTotal
$fieldPct = 100.0 * $fieldBound / $fieldTotal
Badge "NRI surface bound" ("functions {0:0.#}% | fields {1:0.#}%" -f $fnPct, $fieldPct) ([Math]::Min($fnPct, $fieldPct)) (Join-Path $badges "bound-surface.svg")
$linePct = 100.0 * $total.Covered / $total.Lines
Badge "test coverage" ("{0:0.#}% lines" -f $linePct) $linePct (Join-Path $badges "test-coverage.svg")

Write-Host ""
Write-Host ("bound surface: functions {0}/{1}, fields {2}/{3}; call sites run by the tests: {4}/{5}" -f $fnBound, $fnTotal, $fieldBound, $fieldTotal, $fnRan, $fnTotal)
Write-Host ("test coverage: lines {0}/{1} ({2}), branches {3}/{4} ({5})" -f $total.Covered, $total.Lines, (Pct $total.Covered $total.Lines), $total.Taken, $total.Branches, (Pct $total.Taken $total.Branches))
Write-Host "==> docs/coverage.md, docs/badges/bound-surface.svg, docs/badges/test-coverage.svg"
