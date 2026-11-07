<#
.SYNOPSIS
    Renders the vendor runtimes an Upscalers payload was built against as the Markdown list the
    release notes show under the NRI version line.

.DESCRIPTION
    Reads upscaler-runtimes.json, which tools/build-native.ps1 writes beside each Upscalers payload
    (runtimes-upscalers/<rid>/upscaler-runtimes.json; the package carries it as
    runtimes/<rid>/upscaler-runtimes.json), and prints one list item per upscaler SDK: its version at
    NRI's pin and each runtime file with its file version. release.yml inserts the output under the
    first line of docs/releases/<tag>.md. Run it locally to preview:

        pwsh tools/ci/upscaler-runtimes.ps1 runtimes-upscalers/win-x64/upscaler-runtimes.json
#>
param(
    [Parameter(Mandatory)] [string]$Manifest
)

$ErrorActionPreference = "Stop"
$m = Get-Content $Manifest -Raw | ConvertFrom-Json
if (-not $m.sdks) { throw "$Manifest lists no upscaler SDK" }

"- Upscalers (``SharpNRI.Native.Upscalers``, $($m.rid)) built against, runtimes shipped by the application:"
foreach ($s in $m.sdks) {
    $files = @($s.runtimes | ForEach-Object { "``$($_.file)`` $($_.version)" })
    $what = if ($files.Count) { $files -join ", " } else { $s.note }
    "  - $($s.name) $($s.version): $what"
}
