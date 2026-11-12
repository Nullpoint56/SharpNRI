<#
.SYNOPSIS
    The release body's "API changes": this release's packages against the previous release's, from NuGet.org.

.DESCRIPTION
    The baseline is the SharpNRI version on NuGet.org just before -Version (NuGet's index is in SemVer
    order), with the SharpNRI.Descs version that release depended on and its SharpNRI.Testing. They are
    downloaded into a folder and compared with the packages in -New by tools/apidiff (each package's
    public types and members, signatures and values). release.yml's pack job runs it; locally:

        pwsh tools/ci/api-changes.ps1 -New <dir with the new .nupkg files> -Version <SharpNRIVersion> -Out api-changes.md
#>
param(
    [Parameter(Mandatory)] [string]$New,
    [Parameter(Mandatory)] [string]$Version,
    [Parameter(Mandatory)] [string]$Out,
    [string]$Feed = "https://api.nuget.org/v3-flatcontainer"
)

$ErrorActionPreference = "Stop"
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$published = @((Invoke-RestMethod "$Feed/sharpnri/index.json").versions)
$earlier = @($published | Where-Object { $_ -ne $Version.ToLowerInvariant() })
if (-not $earlier) { throw "no earlier SharpNRI on NuGet.org to compare with" }
$baseline = $earlier[-1]

$old = Join-Path ([System.IO.Path]::GetTempPath()) "sharpnri-api-baseline-$baseline"
New-Item -ItemType Directory -Force $old | Out-Null
function Get-Package([string]$id, [string]$v) {
    $lower = $id.ToLowerInvariant(); $lv = $v.ToLowerInvariant()
    $file = Join-Path $old "$id.$v.nupkg"
    if (-not (Test-Path $file)) { Invoke-WebRequest "$Feed/$lower/$lv/$lower.$lv.nupkg" -OutFile $file }
    $file
}
$sharpnri = Get-Package "SharpNRI" $baseline
Get-Package "SharpNRI.Testing" $baseline | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($sharpnri)
try {
    $entry = $zip.Entries | Where-Object { $_.FullName -like "*.nuspec" } | Select-Object -First 1
    $reader = [System.IO.StreamReader]::new($entry.Open()); [xml]$nuspec = $reader.ReadToEnd(); $reader.Dispose()
} finally { $zip.Dispose() }
$descs = @($nuspec.package.metadata.dependencies.group.dependency | Where-Object id -eq "SharpNRI.Descs")[0].version.Trim('[', ']').Split(',')[0].Trim()
Get-Package "SharpNRI.Descs" $descs | Out-Null
Write-Host "baseline: SharpNRI $baseline, SharpNRI.Descs $descs"

dotnet run --project (Join-Path $root "tools/apidiff") -c Release -- $old $New $Out "v$baseline"
if ($LASTEXITCODE -ne 0) { throw "tools/apidiff failed" }
