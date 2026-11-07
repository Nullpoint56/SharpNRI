<#
.SYNOPSIS
    Renders the "Bound libraries" section of a release body from an Upscalers payload's manifest.

.DESCRIPTION
    Reads upscaler-runtimes.json, which tools/build-native.ps1 writes beside each Upscalers payload
    (runtimes-upscalers/<rid>/upscaler-runtimes.json; the package carries it as
    runtimes/<rid>/upscaler-runtimes.json): NRI's version, then each upscaler SDK at NRI's pin with
    the runtime files the application ships, each version linked to its release page. release.yml
    puts the output above docs/releases/<tag>.md. Preview:

        pwsh tools/ci/upscaler-runtimes.ps1 runtimes-upscalers/win-x64/upscaler-runtimes.json
#>
param(
    [Parameter(Mandatory)] [string]$Manifest
)

$ErrorActionPreference = "Stop"
$m = Get-Content $Manifest -Raw | ConvertFrom-Json
if (-not $m.sdks) { throw "$Manifest lists no upscaler SDK" }

"## Bound libraries"
"- NRI: [v$($m.nri)]($($m.nriUrl))"
foreach ($s in $m.sdks) {
    $files = @($s.runtimes | ForEach-Object { "``$($_.file)`` $($_.version)" })
    $detail = if ($files.Count) { $files -join ", " } else { $s.note }
    "- $($s.name): [$($s.version)]($($s.url)) ($detail)"
}
""
