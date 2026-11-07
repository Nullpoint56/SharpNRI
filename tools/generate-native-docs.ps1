<#
.SYNOPSIS
    Step 7 of tools/generate-native.ps1: NRI's header comments as XML docs on the generated L0 types.

.DESCRIPTION
    ClangSharp carries no comments. NRI documents its API in the headers: a comment block above a
    struct, enum or member, and a trailing "// ..." on a field or enum value. This reads both from the
    traversed headers and writes them as /// <summary> on the matching declaration in dotnet/Native,
    matched by NRI's own type and member names, which L0 keeps. Not carried: comments on anonymous
    unions' members, glyph-only table rows (the format support matrix), column headings, separators.
    The text is NRI's, unchanged except whitespace folded and XML-escaped.

        pwsh tools/generate-native-docs.ps1 -Headers <h1>,<h2>,... -Output dotnet/Native
#>
param(
    [Parameter(Mandatory)] [string[]]$Headers,
    [Parameter(Mandatory)] [string]$Output
)

$ErrorActionPreference = "Stop"

function Clean([string[]]$lines) {
    # The format table's support matrix: a row's "+ . . +" glyphs (text after them, behind a second
    # "//", is kept) and the column legend ("TEXTURE | | |" rows) are drawings, not text.
    $lines = @($lines | ForEach-Object { if ($_) { $_ -replace '^[+.\s]+(//\s*)?', '' } })
    if ($lines | Where-Object { $_ -match '\|\s*\|' }) { $lines = @($lines | Where-Object { $_ -notmatch '^[A-Z0-9_|\s]+$' }) }
    $kept = @($lines | Where-Object { $_ })
    if (-not $kept) { return $null }
    $text = (($kept -join " ") -replace '\s{2,}', ' ').Trim()
    if (-not $text) { return $null }
    return $text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
}

# A trailing comment on a declaration line, outside string literals.
function Split-Comment([string]$line) {
    $inString = $false
    for ($i = 0; $i -lt $line.Length - 1; $i++) {
        if ($line[$i] -eq '"') { $inString = -not $inString }
        if (-not $inString -and $line[$i] -eq '/' -and $line[$i + 1] -eq '/') { return @($line.Substring(0, $i).Trim(), $line.Substring($i + 2).Trim()) }
    }
    return @($line.Trim(), $null)
}

$typeDocs = @{}
$memberDocs = @{}
foreach ($h in $Headers) {
    $pending = [System.Collections.Generic.List[string]]::new()
    $struct = $null; $depth = 0; $enum = $null
    foreach ($raw in Get-Content $h) {
        $trim = $raw.Trim()
        if ($trim.StartsWith("//")) {
            $text = $trim.Substring(2).Trim()
            if ($text -match '^[=\-/]{3,}$') { $pending.Clear() } else { $pending.Add($text) }
            continue
        }
        if ($trim -eq "" -or $trim.StartsWith("#")) { $pending.Clear(); continue }
        $code, $trailing = Split-Comment $trim
        $doc = @($pending) + @(if ($trailing) { $trailing })
        $pending.Clear()

        if (-not $struct -and -not $enum -and $code -match '^Nri(?:Struct|Union)\((\w+)\)') {
            $struct = $Matches[1]
            if (($d = Clean $doc)) { $typeDocs[$struct] = $d }
            $depth = ($code.ToCharArray() | Where-Object { $_ -eq '{' }).Count - ($code.ToCharArray() | Where-Object { $_ -eq '}' }).Count
            if ($depth -le 0 -and $code.Contains("}")) { $struct = $null }
            continue
        }
        if (-not $struct -and -not $enum -and $code -match '^Nri(?:Enum|Bits)\((\w+),') {
            $enum = $Matches[1]
            # the opening line's trailing comment is a column heading of the value table, not the type's
            if (($d = Clean @($doc | Select-Object -SkipLast $(if ($trailing) { 1 } else { 0 })))) { $typeDocs[$enum] = $d }
            if ($code -match '\);\s*$') { $enum = $null }
            continue
        }
        if ($enum) {
            if ($code -match '^(\w+)\s*(=|,|$)') { if (($d = Clean $doc)) { $memberDocs["$enum.$($Matches[1])"] = $d } }
            if ($code -match '\);\s*$') { $enum = $null }
            continue
        }
        if ($struct) {
            if ($depth -eq 1 -and $code.EndsWith(";")) {
                $name = $null
                if ($code -match '\(\s*NRI_CALL\s*\*\s*(\w+)\s*\)') { $name = $Matches[1] }
                elseif ($code -match '(\w+)\s*(\[[^\]]*\]\s*)*(:\s*\d+\s*)?;$') { $name = $Matches[1] }
                if ($name -and ($d = Clean $doc)) { $memberDocs["$struct.$name"] = $d }
            }
            $depth += ($code.ToCharArray() | Where-Object { $_ -eq '{' }).Count - ($code.ToCharArray() | Where-Object { $_ -eq '}' }).Count
            if ($depth -le 0) { $struct = $null; $depth = 0 }
        }
    }
}

$typesDone = 0; $membersDone = 0
foreach ($f in Get-ChildItem (Join-Path $Output "*.cs")) {
    $lines = [System.Collections.Generic.List[string]](Get-Content $f.FullName)
    $type = $null
    $changed = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match '^public (?:unsafe )?(?:partial struct|enum) (\w+)') {
            $type = $Matches[1]
            if ($typeDocs.ContainsKey($type)) {
                $at = $i
                while ($at -gt 0 -and $lines[$at - 1].Trim().StartsWith("[")) { $at-- }
                $lines.Insert($at, "/// <summary>$($typeDocs[$type])</summary>"); $i++; $typesDone++; $changed = $true
            }
            continue
        }
        if (-not $type) { continue }
        $member = $null
        if ($line -match '^    public (?:fixed |readonly )?[^=;(]*?\b(\w+)(\[\w+\])?;\s*$') { $member = $Matches[1] }
        elseif ($line -match '^    ([A-Z_][A-Z0-9_]*)( = [^,]*)?,?\s*$') { $member = $Matches[1] }
        if ($member -and $memberDocs.ContainsKey("$type.$member")) {
            $at = $i
            while ($at -gt 0 -and $lines[$at - 1].Trim().StartsWith("[")) { $at-- }
            $lines.Insert($at, "    /// <summary>$($memberDocs["$type.$member"])</summary>"); $i++; $membersDone++; $changed = $true
        }
    }
    if ($changed) { [System.IO.File]::WriteAllText($f.FullName, ($lines -join "`n") + "`n") }
}
Write-Host "    NRI header comments as XML docs: $typesDone types, $membersDone members (of $($typeDocs.Count) type and $($memberDocs.Count) member comments read)"
if ($typesDone -eq 0 -or $membersDone -eq 0) { throw "no header comment was carried into L0: the header or the generated layout changed" }
