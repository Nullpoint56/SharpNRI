<#
.SYNOPSIS
    Checks SharpNRI.Descs' version against NuGet.org: a new version is published, a published one
    must still describe the same descs.

.DESCRIPTION
    SharpNRI.Descs is versioned apart from the release (Directory.Build.props: SharpNRIDescsVersion,
    the NRI version as major; AGENTS.md: Versions), so a binding-only release leaves everything built
    on the descs untouched. That holds only while a published version keeps meaning one assembly:

      - SharpNRIDescsVersion not on NuGet.org yet: this release publishes it ("publish=true").
      - published: the package names the commit it was built from (<repository commit>), and the
        descs' sources (dotnet/Native without NriApi.cs, descs/, NativeSplit.props) must be unchanged
        since; the release then reuses the published package ("publish=false"). Changed: fails,
        naming the paths, until SharpNRIDescsVersion moves.

    Needs the history back to that commit (actions/checkout fetch-depth: 0). Writes descs_version
    and descs_publish to GITHUB_OUTPUT when it is set. Locally: pwsh tools/ci/descs-version.ps1
#>
param(
    [string]$Feed = "https://api.nuget.org/v3-flatcontainer"
)

$ErrorActionPreference = "Stop"
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$props = Get-Content (Join-Path $root "Directory.Build.props") -Raw
if ($props -notmatch '<SharpNRIDescsVersion>([^<]+)</SharpNRIDescsVersion>') { throw "SharpNRIDescsVersion not found in Directory.Build.props" }
$version = $Matches[1].Trim()
$id = "sharpnri.descs"
$paths = @("dotnet/Native", ":(exclude)dotnet/Native/NriApi.cs", "descs", "NativeSplit.props")

$published = @()
try { $published = @((Invoke-RestMethod "$Feed/$id/index.json").versions) }
catch { if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw } }   # 404: never published

$publish = $published -notcontains $version.ToLowerInvariant()
if ($publish) {
    Write-Host "SharpNRI.Descs $version is not on NuGet.org: this release publishes it"
} else {
    [xml]$nuspec = (Invoke-WebRequest "$Feed/$id/$($version.ToLowerInvariant())/$id.nuspec").Content
    $commit = $nuspec.package.metadata.repository.commit
    if (-not $commit) { throw "the published SharpNRI.Descs $version names no commit" }
    git -C $root cat-file -e "$commit^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) { throw "commit $commit (SharpNRI.Descs $version) is not in this clone: fetch the full history" }
    $changed = @(git -C $root diff --name-only $commit HEAD -- @paths)
    if ($LASTEXITCODE -ne 0) { throw "git diff failed" }
    if ($changed) {
        throw "SharpNRI.Descs $version was published from $commit, and its sources changed since:`n  $($changed -join "`n  ")`nBump SharpNRIDescsVersion in Directory.Build.props (a new NRI version starts <NRI>.0.0)."
    }
    Write-Host "SharpNRI.Descs $version is published (from $commit) and unchanged: this release reuses it"
}

if ($env:GITHUB_OUTPUT) {
    "descs_version=$version" >> $env:GITHUB_OUTPUT
    "descs_publish=$($publish.ToString().ToLowerInvariant())" >> $env:GITHUB_OUTPUT
}
