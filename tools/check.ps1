<#
.SYNOPSIS
    The quick checks a merge requires besides the suite: lint and zero allocation, locally.

.DESCRIPTION
    The same commands as the lint and zero-alloc jobs of .github/workflows/ci.yml and checks.yml
    (AGENTS.md: Lint; Lifetime). Lint needs no native payload; zero-alloc needs the Base payload
    staged (pwsh tools/build-native.ps1) and runs on NONE plus each GPU backend the machine has.

        pwsh tools/check.ps1                 # both
        pwsh tools/check.ps1 -Lint           # lint only
        pwsh tools/check.ps1 -ZeroAlloc      # zero-alloc only
        pwsh tools/check.ps1 -Lint -Fix      # apply the formatter first, then check
#>
param(
    [switch]$Lint,
    [switch]$ZeroAlloc,
    [switch]$Fix
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
if (-not $Lint -and -not $ZeroAlloc) { $Lint = $true; $ZeroAlloc = $true }
$failed = @()

Push-Location $root
try {
    if ($Lint) {
        Write-Host "==> lint"
        if ($Fix) { dotnet format whitespace SharpNRI.slnx --exclude dotnet/Native }
        dotnet build SharpNRI.slnx -c Release -warnaserror -p:SharpNRIRequireNative=false -nologo -v q
        if ($LASTEXITCODE -ne 0) { $failed += "lint: analyzers" }
        dotnet format whitespace SharpNRI.slnx --exclude dotnet/Native --verify-no-changes
        if ($LASTEXITCODE -ne 0) { $failed += "lint: formatting (pwsh tools/check.ps1 -Lint -Fix applies it)" }
    }
    if ($ZeroAlloc) {
        Write-Host "==> zero-alloc"
        dotnet build tests/SharpNRI.Tests -c Release -nologo -v q
        if ($LASTEXITCODE -ne 0) { throw "the test project does not build" }
        $exe = Join-Path $root "tests/SharpNRI.Tests/bin/Release/net11.0/SharpNRI.Tests$(if ($IsWindows) { '.exe' })"
        & $exe -noColor -class SharpNRI.Tests.AllocationTests -class SharpNRI.Tests.ExtensionAllocationTests -class SharpNRI.Tests.AllocationCoverageTests
        if ($LASTEXITCODE -ne 0) { $failed += "zero-alloc" }
    }
}
finally {
    Pop-Location
}

if ($failed) { throw "failed: $($failed -join '; ')" }
Write-Host "==> all checks passed"
