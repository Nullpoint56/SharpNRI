<#
.SYNOPSIS
    Applies patches/*.patch to nri/ in name order, as step 2 of tools/build-native.ps1 does.

.DESCRIPTION
    For a ci.yml job that restored the native payload from the cache instead of building it: the
    tests still read nri/Include with the patches applied (NativeAbiTests compiles its probe against
    it). A patch that is already applied (its reverse applies cleanly) is skipped.
#>
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$nri = Join-Path $root 'nri'
foreach ($p in @(Get-ChildItem (Join-Path $root 'patches') -Filter '*.patch' -File | Sort-Object Name)) {
    git -C $nri apply --check --reverse $p.FullName 2>$null
    if ($LASTEXITCODE -eq 0) { Write-Host "$($p.Name): already applied, skipped"; continue }
    git -C $nri apply --check $p.FullName
    if ($LASTEXITCODE -ne 0) { throw "patch $($p.Name) does not apply to nri/" }
    git -C $nri apply $p.FullName
    if ($LASTEXITCODE -ne 0) { throw "git apply $($p.Name) failed" }
    Write-Host "$($p.Name): applied"
}
