<#
.SYNOPSIS
  Regenerate the SharpNRI L0 raw interop layer from the pinned NRI headers.

.DESCRIPTION
  Runs ClangSharpPInvokeGenerator over NRI.h and the extension headers of the submodule at the tag
  pinned in Directory.Build.props, then applies the post-processing ClangSharp cannot do itself.

  C++ mode, deliberately. NRI's C surface (NRI_FORCE_C) turns every enum into an integer typedef, so
  every enum-typed field and every function-pointer parameter comes out as byte/ushort and Result
  comes out as sbyte: the bytes line up and the types are gone. C++ mode keeps `enum class X : type`,
  typed bitfields and typed signatures. Measured on v190 with ClangSharp 21.1.8.4: C++ mode is 373
  files and 7,661 lines, builds with 0 errors after the two rules below (AGENTS.md: Generator and ABI).

  Post-processing, all mechanical and all re-checked by the tests:
    * NriBits operator overloads (~ & | &= |=) are C++ constexpr functions; ClangSharp translates them
      into static helpers on the API class that do not compile. Removed; C# enums have the operators.
    * NriBit(n) expands to (1u << n); inside a byte- or ushort-backed enum the uint literal does not
      implicitly convert. The `U` suffix is dropped for those enums only. For uint-backed enums the
      literal is correct as generated and is left alone.
    * [Flags] is added to every enum declared with NriBits, read from the headers, so a consumer can
      tell a mask from a value. The generator cannot know which is which.

  Not generated here: D3D11, Metal and WebGPU wrappers. Those backends are off in the native build.

.PARAMETER Output
  Destination for the generated sources. Defaults to dotnet/Native.
#>
param(
    [string]$Output,
    [string]$Namespace = "SharpNRI.Native",
    [string]$Library   = "NRI"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")

$props = Get-Content (Join-Path $root "Directory.Build.props") -Raw
if ($props -notmatch '<NriVersion>([^<]+)</NriVersion>') { throw "NriVersion not found in Directory.Build.props" }
$NriVersion = $Matches[1].Trim()
if (-not $Output) { $Output = Join-Path $root "dotnet" "Native" }

# The submodule must be at the pinned tag: the headers are the ABI, and a drifted checkout would
# generate a layer that resolves and dispatches against a library of a different shape.
$nri = Join-Path $root "nri"
$inc = Join-Path $nri "Include"
$headerVersion = (Select-String -Path (Join-Path $inc "NRI.h") -Pattern '#define NRI_VERSION (\d+)').Matches[0].Groups[1].Value
if ($headerVersion -ne $NriVersion) { throw "nri/ is at NRI_VERSION $headerVersion but Directory.Build.props pins $NriVersion" }

# Patches first, the same way build-native.ps1 applies them: the L0 layer is generated from the
# patched headers, because the native library is built from them and the ABI oracle measures them.
$patches = @(Get-ChildItem (Join-Path $root "patches") -Filter "*.patch" -File -ErrorAction SilentlyContinue | Sort-Object Name)
foreach ($p in $patches) {
    git -C $nri apply --check --reverse -C0 $p.FullName 2>$null  # -C0: a later patch may change a line within this one's context
    if ($LASTEXITCODE -eq 0) { Write-Host "==> patch $($p.Name): already applied"; continue }
    git -C $nri apply --check $p.FullName
    if ($LASTEXITCODE -ne 0) { throw "patch $($p.Name) does not apply to nri/ at v$NriVersion" }
    git -C $nri apply $p.FullName
    if ($LASTEXITCODE -ne 0) { throw "git apply $($p.Name) failed" }
    Write-Host "==> patch $($p.Name): applied"
}

$gen = Get-Command ClangSharpPInvokeGenerator -ErrorAction SilentlyContinue
if (-not $gen) { throw "ClangSharpPInvokeGenerator not found on PATH. Install it with:`n  dotnet tool install --global ClangSharpPInvokeGenerator" }
Write-Host "==> generator: $($gen.Source)"
Write-Host "==> NRI:       v$NriVersion"

# ---------------------------------------------------------------- umbrella header
# One translation unit including everything we bind. Written to obj/ so the submodule stays clean.
$objDir = Join-Path $root "obj" "nri-gen"
New-Item -ItemType Directory -Force -Path $objDir | Out-Null
$headers = @(
    "NRI.h",
    "Extensions/NRIDeviceCreation.h", "Extensions/NRIHelper.h", "Extensions/NRIDescriptorHeap.h",
    "Extensions/NRIImgui.h", "Extensions/NRILowLatency.h", "Extensions/NRIMeshShader.h",
    "Extensions/NRIRayTracing.h", "Extensions/NRIStreamer.h", "Extensions/NRISwapChain.h",
    "Extensions/NRIUpscaler.h", "Extensions/NRIVideo.h",
    "Extensions/NRIWrapperVK.h", "Extensions/NRIWrapperD3D12.h"
)
$umbrella = Join-Path $objDir "nri_all.h"
($headers | ForEach-Object { "#include `"$_`"" }) -join "`n" | Set-Content $umbrella
$traverse = @((Join-Path $inc "NRIDescs.h")) + @($headers | ForEach-Object { Join-Path $inc $_ })

# ---------------------------------------------------------------- generate
New-Item -ItemType Directory -Force -Path $Output | Out-Null
Get-ChildItem (Join-Path $Output "*.cs") -ErrorAction SilentlyContinue | Remove-Item -Force -Confirm:$false

Write-Host "==> generating -> $Output"
& $gen.Source `
    --file $umbrella `
    --file-directory $objDir --include-directory $inc `
    --traverse @traverse `
    --language c++ --std c++17 `
    --namespace $Namespace --method-class-name NriApi --library-path $Library `
    --output $Output `
    --config codegen=latest file=multi `
    --generate file-scoped-namespaces helper-types

$produced = @(Get-ChildItem (Join-Path $Output "*.cs"))
if ($produced.Count -lt 300) { throw "generation produced only $($produced.Count) files - expected ~373" }

# ---------------------------------------------------------------- post-process
Write-Host "==> post-processing"

# 1. NriBits operator overloads translated into static helpers.
$api = Join-Path $Output "NriApi.cs"
$t = Get-Content $api -Raw
$pattern = '(?ms)^(    \[return: NativeTypeName\("[^"]*"\)\]\r?\n)?    public static \S+ (operator\S*|OnesComplement|And|Or|AndAssign|OrAssign)\([^\r\n]*\r?\n    \{.*?^    \}\r?\n(\r?\n)?'
$ops = ([regex]::Matches($t, $pattern)).Count
$t = [regex]::Replace($t, $pattern, '')
Set-Content $api $t -NoNewline
Write-Host "    removed $ops operator helpers"

# 2. uint literals in narrow enums; 3. [Flags] on NriBits enums.
$bits = @()
foreach ($h in @((Join-Path $inc "NRIDescs.h")) + ($headers | ForEach-Object { Join-Path $inc $_ })) {
    foreach ($m in @(Select-String -Path $h -Pattern '^NriBits\((\w+),')) { $bits += $m.Matches[0].Groups[1].Value }
}
$bits = $bits | Sort-Object -Unique
$narrowed = 0; $flagged = 0
foreach ($f in $produced) {
    $c = Get-Content $f.FullName -Raw
    $n = $c
    if ($n -match 'public enum (\w+) : (byte|ushort)') {
        $n = $n -replace '\(1U << \((\d+)\)\)', '(1 << $1)'
        if ($n -ne $c) { $narrowed++ }
    }
    if ($n -match 'public enum (\w+)( : \w+)?\r?\n' -and $bits -contains $Matches[1]) {
        $n = $n -replace '(?m)^public enum ', "[Flags]`r`npublic enum "
        # [Flags] is System.FlagsAttribute: both projects compile with implicit usings, so no using is added
        $flagged++
    }
    if ($n -ne $c) { Set-Content $f.FullName $n -NoNewline }
}
Write-Host "    narrowed literals in $narrowed enums, flagged $flagged NriBits enums (header declares $($bits.Count))"
if ($flagged -ne $bits.Count) { throw "expected to flag $($bits.Count) NriBits enums, flagged $flagged" }

# 4. NRI's pointer constants (HAS_BUFFER) as static readonly: ClangSharp emits them as mutable public
#    fields, which any caller could reassign for the whole process (CA2211).
$t = Get-Content $api -Raw
$pointerPattern = '(?m)^    public static (\w+\*) ([A-Z_]+) = '
$pointers = ([regex]::Matches($t, $pointerPattern)).Count
if ($pointers -lt 1) { throw "expected NRI's pointer constants (HAS_BUFFER) in NriApi.cs, found none" }
$t = [regex]::Replace($t, $pointerPattern, '    public static readonly $1 $2 = ')
Set-Content $api $t -NoNewline
Write-Host "    made $pointers pointer constants readonly"

# 5. "using System;" is redundant everywhere: both projects compile with implicit usings (IDE0005).
$unused = 0
foreach ($f in $produced) {
    $c = Get-Content $f.FullName -Raw
    $n = $c -replace '(?m)^using System;\r?\n', ''
    if ($n -ne $c) { Set-Content $f.FullName $n -NoNewline; $unused++ }
}
Write-Host "    removed a redundant 'using System;' from $unused files"

# 6. ClangSharp's "using static" of an enum NriApi.cs never names a member of (GraphicsAPI: the one
#    candidate, ALL in a default value, is NriApi's own constant, which wins the lookup) (IDE0005).
$t = Get-Content $api -Raw
$body = $t -replace '(?m)^using .*\r?\n', ''
$declared = [regex]::Matches($t, '(?m)^    public const \w+ (\w+) =') | ForEach-Object { $_.Groups[1].Value }
foreach ($m in [regex]::Matches($t, '(?m)^using static SharpNRI\.Native\.(\w+);\r?\n')) {
    $enumFile = Join-Path $Output "$($m.Groups[1].Value).cs"
    if (-not (Test-Path $enumFile)) { continue }
    $members = [regex]::Matches((Get-Content $enumFile -Raw), '(?m)^    (\w+)( =|,)') | ForEach-Object { $_.Groups[1].Value }
    if (-not ($members | Where-Object { $declared -notcontains $_ -and $body -match "\b$_\b" })) {
        $t = $t.Replace($m.Value, '')
        Write-Host "    removed the unused 'using static SharpNRI.Native.$($m.Groups[1].Value);'"
    }
}
Set-Content $api $t -NoNewline

# 7. NRI's header comments as XML docs on the types and members they describe (tools/generate-native-docs.ps1).
& (Join-Path $PSScriptRoot "generate-native-docs.ps1") -Headers $traverse -Output $Output

# ---------------------------------------------------------------- line endings
# ClangSharp writes the host platform's newline. The committed layer is LF so that the CI drift
# check compares content rather than line endings, whichever runner regenerates it.
foreach ($f in $produced) {
    $c = Get-Content $f.FullName -Raw
    $n = $c -replace "
", "
"
    if ($n -ne $c) { [System.IO.File]::WriteAllText($f.FullName, $n) }
}

# ---------------------------------------------------------------- summary
$fnptrs = (Select-String -Path (Join-Path $Output "*.cs") -Pattern 'delegate\* unmanaged' | Measure-Object).Count
$imports = (Select-String -Path $api -Pattern 'DllImport' | Measure-Object).Count
Write-Host "==> $($produced.Count) files, $fnptrs function pointers, $imports DllImports"
