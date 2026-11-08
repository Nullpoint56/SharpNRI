<#
.SYNOPSIS
    Checks a folder of packed .nupkg files: the files each package must carry, nothing in a subfolder
    of runtimes/<rid>/native/, exact dependencies, the commit in <repository>, SharpNRI's description.

.DESCRIPTION
    release.yml's pack job runs it on the packages it is about to publish; run it locally on a pack
    of the same tree before a release, on Linux (WSL) as well as Windows, since pack paths differ
    between the two (1.0.0-beta.4's first run: PackagePath "lib\net11.0\" packed as lib/net11.0//_._
    on the Linux runner, which only this check, not a Windows pack, showed).

        pwsh tools/ci/check-packages.ps1 -Folder nupkg -Version 191.0.0-beta.4 -DescsVersion 191.0.0-beta.1 -AgilitySdk 1.620.1 -Commit <sha> [-DescsReused] [-Rids win-x64]

    -Rids names the platforms the pack had native libraries for: both on the release runner,
    the host's alone on a local pack.
#>
param(
    [Parameter(Mandatory)] [string]$Folder,
    [Parameter(Mandatory)] [string]$Version,
    [Parameter(Mandatory)] [string]$DescsVersion,
    [Parameter(Mandatory)] [string]$AgilitySdk,
    [Parameter(Mandatory)] [string]$Commit,
    [switch]$DescsReused,
    [string[]]$Rids = @("win-x64", "linux-x64")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression.FileSystem

$native = @{ "win-x64" = "NRI.dll"; "linux-x64" = "libNRI.so" }
$platformNames = [ordered]@{ "win-x64" = "Windows x64"; "win-arm64" = "Windows arm64"; "linux-x64" = "Linux x64"; "linux-arm64" = "Linux arm64"; "osx-arm64" = "macOS arm64 (Vulkan through MoltenVK)" }
$perRid = { param($pattern) @($Rids | ForEach-Object { $pattern -f $_, $native[$_] }) }

$must = @{
    "SharpNRI"                  = @(& $perRid "runtimes/{0}/native/{1}") + @(& $perRid "licenses/{0}/NRI/LICENSE.txt") +
                                  "lib/net11.0/SharpNRI.dll", "lib/net11.0/SharpNRI.xml", "lib/net11.0/SharpNRI.pdb", "buildTransitive/SharpNRI.targets", "buildTransitive/SharpNRI.props", "README.md"
    "SharpNRI.Descs"            = "lib/net11.0/SharpNRI.Descs.dll", "lib/net11.0/SharpNRI.Descs.xml", "lib/net11.0/SharpNRI.Descs.pdb", "README.md"
    "SharpNRI.Testing"          = "lib/net11.0/SharpNRI.Testing.dll", "lib/net11.0/SharpNRI.Testing.xml", "lib/net11.0/SharpNRI.Testing.pdb", "README.md"
    "SharpNRI.Native.Upscalers" = @(& $perRid "runtimes/{0}/native/{1}") + @(& $perRid "runtimes/{0}/upscaler-runtimes.json") + @(& $perRid "licenses/{0}/NRI/LICENSE.txt") +
                                  "buildTransitive/net11.0/SharpNRI.Native.Upscalers.targets", "lib/net11.0/_._", "README.md"
    "SharpNRI.AgilitySDK"       = "buildTransitive/net11.0/SharpNRI.AgilitySDK.targets", "buildTransitive/net11.0/SharpNRI.AgilitySDK.props", "lib/net11.0/_._", "README.md"
}
# Exact dependencies ([v], Directory.Build.targets): the packages are built against each other
$deps = @{
    "SharpNRI"                  = @{ "SharpNRI.Descs" = "[$DescsVersion]" }
    "SharpNRI.Testing"          = @{ "SharpNRI" = "[$Version]" }
    "SharpNRI.AgilitySDK"       = @{ "Microsoft.Direct3D.D3D12" = "[$AgilitySdk]" }
    "SharpNRI.Native.Upscalers" = @{ "SharpNRI" = "[$Version]" }
}
$expectedDescription = "Native library inside for: " + (($platformNames.Keys | Where-Object { $Rids -contains $_ } | ForEach-Object { $platformNames[$_] }) -join ", ") + "."

$failed = @()
foreach ($id in $must.Keys | Sort-Object) {
    $v = if ($id -eq "SharpNRI.Descs") { $DescsVersion } else { $Version }
    $pkg = Join-Path $Folder "$id.$v.nupkg"
    if (-not (Test-Path $pkg)) { $failed += "$pkg not produced"; continue }
    $zip = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path $pkg))
    try {
        $entries = @($zip.Entries | ForEach-Object { $_.FullName })
        Write-Host "`n==> $pkg ($([math]::Round((Get-Item $pkg).Length / 1KB)) KB)"
        $zip.Entries | Sort-Object FullName | ForEach-Object { Write-Host ("    {0,12}  {1}" -f $_.Length, $_.FullName) }
        foreach ($e in $must[$id]) { if ($entries -notcontains $e) { $failed += "$pkg lacks $e" } }
        # no empty path segment (a separator NuGet did not take as one)
        foreach ($e in $entries | Where-Object { $_ -match '//' }) { $failed += "$pkg has the malformed entry $e" }
        # nothing but libraries under runtimes/<rid>/native/: NuGet copies those flat (beta.2's licences collided)
        foreach ($e in $entries | Where-Object { $_ -match '^runtimes/[^/]+/native/.+/' }) { $failed += "$pkg has $e in a subfolder of native/" }
        $nuspec = $zip.Entries | Where-Object { $_.FullName -like "*.nuspec" } | Select-Object -First 1
        $reader = [System.IO.StreamReader]::new($nuspec.Open()); $text = $reader.ReadToEnd(); $reader.Dispose()
        [xml]$xml = $text
        # Source Link: the package names the commit it was built from (PublishRepositoryUrl); a reused
        # SharpNRI.Descs names the commit it was published from
        if ($id -ne "SharpNRI.Native.Upscalers" -and -not ($id -eq "SharpNRI.Descs" -and $DescsReused)) {
            if ($text -notmatch "<repository [^>]*commit=""$Commit""") { $failed += "$pkg does not name commit $Commit in <repository>" }
        }
        foreach ($d in ($deps[$id] ?? @{}).GetEnumerator()) {
            $found = @($xml.package.metadata.dependencies.group.dependency | Where-Object id -eq $d.Key)
            if (-not $found) { $failed += "$pkg does not depend on $($d.Key)" }
            foreach ($f in $found) { if ($f.version -ne $d.Value) { $failed += "$pkg depends on $($d.Key) $($f.version), expected $($d.Value)" } }
        }
        # SharpNRI.props names the SharpNRI.Descs the build holds the restore to (SharpNRI.targets, SHARPNRI005)
        $props = $zip.Entries | Where-Object FullName -eq "buildTransitive/SharpNRI.props" | Select-Object -First 1
        if ($props) {
            $reader = [System.IO.StreamReader]::new($props.Open()); $propsText = $reader.ReadToEnd(); $reader.Dispose()
            if ($propsText -notmatch "<SharpNRIDescsDependency>$([regex]::Escape($DescsVersion))</SharpNRIDescsDependency>") { $failed += "$pkg SharpNRI.props does not name SharpNRI.Descs $DescsVersion" }
            if ($propsText -notmatch "<SharpNRIPackedVersion>$([regex]::Escape($Version))</SharpNRIPackedVersion>") { $failed += "$pkg SharpNRI.props does not name SharpNRI $Version" }
        }
        # The description names exactly the platforms whose library is inside (SharpNRIPackageDescription)
        if ($id -eq "SharpNRI") {
            $description = $xml.package.metadata.description
            Write-Host "    description: $description"
            if (-not $description.EndsWith($expectedDescription)) { $failed += "$pkg description does not end with '$expectedDescription': $description" }
        }
    } finally { $zip.Dispose() }
}
$expected = $must.Keys | ForEach-Object { "$_.$(if ($_ -eq 'SharpNRI.Descs') { $DescsVersion } else { $Version }).nupkg" }
foreach ($x in @(Get-ChildItem $Folder -Filter *.nupkg | Where-Object { $_.Name -notin $expected })) { Write-Warning "unexpected package $($x.Name)" }
if ($failed) { throw "package check failed:`n  $($failed -join "`n  ")" }
Write-Host "`npackage check passed: $($must.Count) packages, platforms $($Rids -join ', ')"
