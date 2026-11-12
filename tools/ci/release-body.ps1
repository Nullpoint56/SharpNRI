<#
.SYNOPSIS
    Assembles a GitHub release body: Bound libraries, the notes, API changes, Downloads.

.DESCRIPTION
    Bound libraries comes from the Upscalers payload's manifest (tools/ci/upscaler-runtimes.ps1), plus
    the Agility SDK when given; the middle from docs/releases/<tag>.md (Added, Fixed, Breaking changes,
    Verified: compact lists, what happened, no why or how), API changes from -ApiChanges (tools/ci/api-changes.ps1:
    the public surface against the previous release, mechanical), and Downloads names the NuGet.org packages
    of the release and the zip-only macOS library. release.yml runs it; run it locally to preview or
    to rewrite a published body (gh release edit <tag> --notes-file <out>).

        pwsh tools/ci/release-body.ps1 -Tag v<SharpNRIVersion> -Manifest runtimes-upscalers/win-x64/upscaler-runtimes.json -DescsVersion <SharpNRIDescsVersion> -AgilitySdk <AgilitySdkVersion> -Out body.md

    SharpNRI.Descs has its own version since 1.0.0-beta.3 (AGENTS.md: Versions); without
    -DescsVersion it is the tag's, as for the earlier releases. -AgilitySdk also lists the
    SharpNRI.AgilitySDK package (since 1.0.0-beta.3).
#>
param(
    [Parameter(Mandatory)] [string]$Tag,
    [Parameter(Mandatory)] [string]$Manifest,
    [Parameter(Mandatory)] [string]$Out,
    [string]$DescsVersion,
    [string]$AgilitySdk,
    [string]$ApiChanges,      # tools/ci/api-changes.ps1's output (since 1.0.0-beta.3)
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
if (-not $DescsVersion) { $DescsVersion = $version }
$packages = [ordered]@{ "SharpNRI" = $version; "SharpNRI.Descs" = $DescsVersion; "SharpNRI.Testing" = $version; "SharpNRI.Native.Upscalers" = $version }
if ($AgilitySdk) { $packages["SharpNRI.AgilitySDK"] = $version }
$nuget = if ($NotOnNuGet) { "- NuGet packages: attached below only" }
         else { "- NuGet.org: " + (($packages.GetEnumerator() | ForEach-Object { "[$($_.Key) $($_.Value)](https://www.nuget.org/packages/$($_.Key)/$($_.Value))" }) -join ", ") }

$downloads = @("", "## Downloads", $nuget, "- osx-arm64: native library as a zip below, not in the packages")
if (-not $NoNativeSymbols) { $downloads += "- Symbols: NRI's PDB per win-x64 payload as a zip below (``SharpNRI-symbols-*``); managed PDBs in the packages" }
$bound = @(& (Join-Path $PSScriptRoot "upscaler-runtimes.ps1") -Manifest $Manifest -AgilitySdk $AgilitySdk)
$api = if ($ApiChanges) { @("") + @((Get-Content $ApiChanges -Raw).TrimEnd() -split "`r?`n") } else { @() }
$body = $bound + $notes + $api + $downloads
$body | Set-Content $Out -Encoding utf8NoBOM
Get-Content $Out
