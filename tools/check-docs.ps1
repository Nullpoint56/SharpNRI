<#
.SYNOPSIS
    Before a release: the mechanical part of "the docs describe this release" (AGENTS.md: Releases).

.DESCRIPTION
    Fails, naming each finding, when
    - README.md, docs/nuget/README.md or docs/guide.md name a release version (191.0.0-beta.4,
      a PackageReference Version): those pages describe the latest release and say how to find its
      version (the NuGet badge, `dotnet add package ... --prerelease`) instead of naming one;
    - a user-facing page, patches/README.md or the shipped and tool code names a patch that is not
      in patches/ (a patch upstream took is NRI's behaviour now, described without the number);
      AGENTS.md and docs/agents/, docs/upstream.md, docs/releases/ and the tests keep that history on purpose;
    - THIRD-PARTY-NOTICES.md lists another version of a component than the pinned NRI's
      CMakeLists.txt fetches, or another Agility SDK than Directory.Build.props.

    What it cannot judge stays the release checklist's: whether each statement is still true.

        pwsh tools/check-docs.ps1
#>
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$findings = [System.Collections.Generic.List[string]]::new()

function Read-Text([string]$rel) { [IO.File]::ReadAllText((Join-Path $root $rel)) }

# 1. No release version on the pages that describe the latest release
foreach ($rel in 'README.md', 'docs/nuget/README.md', 'docs/guide.md') {
    $lines = (Read-Text $rel) -split "`r?`n"
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '\b\d+\.\d+\.\d+-beta\.\d+\b|PackageReference\s+Include="SharpNRI[^"]*"\s+Version="[0-9]') {
            $findings.Add("${rel}:$($i + 1): names a release version: $($lines[$i].Trim())")
        }
    }
}

# 2. Every patch named is in the set
$patches = @(Get-ChildItem (Join-Path $root 'patches') -Filter '*.patch' | ForEach-Object { $_.Name.Substring(0, 4) })
$scan = @('README.md', 'docs/nuget/README.md', 'docs/guide.md', 'patches/README.md')
$scan += git -C $root ls-files -- 'dotnet/*.cs' 'descs/*.cs' 'testing/*.cs' 'tools/*.ps1' 'tools/*.cs' 'tools/*.sh' 'tools/cmake/*' |
    Where-Object { $_ -notlike 'dotnet/Native/*' }
foreach ($rel in $scan) {
    $lines = (Read-Text $rel) -split "`r?`n"
    for ($i = 0; $i -lt $lines.Count; $i++) {
        foreach ($m in [regex]::Matches($lines[$i], '\b[Pp]atch(?:es)? (\d{4})\b|`(\d{4})-[a-z0-9-]+`')) {
            $n = if ($m.Groups[1].Success) { $m.Groups[1].Value } else { $m.Groups[2].Value }
            if ($patches -notcontains $n) { $findings.Add("${rel}:$($i + 1): patch $n is not in patches/") }
        }
    }
}

# 3. THIRD-PARTY-NOTICES.md at the pinned NRI's dependency pins
$notices = Read-Text 'THIRD-PARTY-NOTICES.md'
$cmake = Read-Text 'nri/CMakeLists.txt'
foreach ($m in [regex]::Matches($cmake, 'URL "https://github\.com/([^/]+/[^/]+)/archive/(refs/tags/)?([^"/]+)\.zip"')) {
    $repo = $m.Groups[1].Value
    $row = ($notices -split "`r?`n") | Where-Object { $_ -match "^\|\s*\[[^\]]+\]\(https://github\.com/$([regex]::Escape($repo))\)" } | Select-Object -First 1
    if (-not $row) { continue }   # not built into a package (Metal, WebGPU, DLSS)
    $expected = if ($m.Groups[2].Success) { $m.Groups[3].Value } else { "commit $($m.Groups[3].Value.Substring(0, 7))" }
    if ($row -notmatch "\|\s*$([regex]::Escape($expected))\s*\|") { $findings.Add("THIRD-PARTY-NOTICES.md: $repo is pinned at '$expected' by nri/CMakeLists.txt: $row") }
}
$agility = ([xml](Read-Text 'Directory.Build.props')).Project.PropertyGroup.AgilitySdkVersion | Where-Object { $_ } | Select-Object -First 1
if ($notices -notmatch "Microsoft\.Direct3D\.D3D12/$([regex]::Escape($agility))\) headers \| $([regex]::Escape($agility)) \|") {
    $findings.Add("THIRD-PARTY-NOTICES.md: the Agility SDK headers row is not at AgilitySdkVersion $agility")
}

if ($findings.Count -gt 0) {
    $findings | ForEach-Object { Write-Host $_ }
    throw "check-docs: $($findings.Count) finding(s)"
}
Write-Host "check-docs: clean"
