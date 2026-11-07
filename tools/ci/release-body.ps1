<#
.SYNOPSIS
    Assembles a GitHub release body: Bound libraries, the notes, Packages.

.DESCRIPTION
    Bound libraries comes from the Upscalers payload's manifest (tools/ci/upscaler-runtimes.ps1), the
    middle from docs/releases/<tag>.md (Added, Fixed, Breaking changes, Verified: compact lists, what
    happened, no why or how), and Downloads names the NuGet.org packages of the version and the
    zip-only macOS library. release.yml runs it; run it locally to preview or to rewrite a published
    body (gh release edit <tag> --notes-file <out>).

        pwsh tools/ci/release-body.ps1 -Tag v1.0.0-beta.2 -Manifest runtimes-upscalers/win-x64/upscaler-runtimes.json -Out body.md
#>
param(
    [Parameter(Mandatory)] [string]$Tag,
    [Parameter(Mandatory)] [string]$Manifest,
    [Parameter(Mandatory)] [string]$Out,
    [switch]$NotOnNuGet,      # a release from before NuGet.org publishing (v1.0.0-beta.1)
    [switch]$NoNativeSymbols  # a release from before the symbols zips (v1.0.0-beta.1, v1.0.0-beta.2)
)

$ErrorActionPreference = "Stop"
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$notesPath = Join-Path $root "docs/releases/$Tag.md"
if (-not (Test-Path $notesPath)) { throw "release notes $notesPath missing" }
$notes = @(Get-Content $notesPath)
if ($notes[0] -notmatch '^## ') { throw "$notesPath must start with a section (## Added, ## Fixed, ...); Bound libraries is generated" }

$version = $Tag.TrimStart('v')
$ids = "SharpNRI", "SharpNRI.Descs", "SharpNRI.Testing", "SharpNRI.Native.Upscalers"
$packages = if ($NotOnNuGet) { "- NuGet packages: attached below only" }
            else { "- NuGet.org: " + (($ids | ForEach-Object { "[$_](https://www.nuget.org/packages/$_/$version)" }) -join ", ") }

$downloads = @("", "## Downloads", $packages, "- osx-arm64: native library as a zip below, not in the packages")
if (-not $NoNativeSymbols) { $downloads += "- Symbols: NRI's PDB per win-x64 payload as a zip below (``SharpNRI-symbols-*``); managed PDBs in the packages" }
$body = @(& (Join-Path $PSScriptRoot "upscaler-runtimes.ps1") -Manifest $Manifest) + $notes + $downloads
$body | Set-Content $Out -Encoding utf8NoBOM
Get-Content $Out
