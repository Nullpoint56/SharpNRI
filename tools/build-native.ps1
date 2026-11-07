<#
.SYNOPSIS
  Build the NRI native payload from the pinned submodule and stage one native package variant.

.DESCRIPTION
  1. Checks that nri/Include/NRI.h is at the NRI_VERSION pinned in Directory.Build.props.
  2. Applies patches/*.patch to nri/ in name order. A patch that is already applied (its reverse
     applies cleanly) is skipped, so the script is re-runnable. An empty patches/ is valid.
  3. Configures and builds NRI with CMake into _build/<rid>-<variant>/, one build tree per variant so
     no CMake cache is shared. Nothing is written into nri/ except the header NRI's own CMakeLists
     generates there (Include/NRIAgilitySDK.h, ignored by nri/.gitignore); it is moved into the build
     tree after configure so the submodule stays pristine.
  4. Stages NRI plus the files it needs into the variant's staging directory, and the licence of every
     component that is actually inside what is staged into <staging>/licenses/.
  5. On Windows, also stages the Agility runtime for the repository's tests
     (runtimes-agility/<rid>/native/AgilitySDK/); no package carries it.
  6. Prints a table of what was staged.

  Variants (AGENTS.md: Native packages). Both produce a library named NRI.dll / libNRI.so / libNRI.dylib,
  so a consumer takes exactly one of them:

    Base       runtimes/<rid>/native/             no upscaler SDK compiled in. NRI, AMD AGS, licences.
    Upscalers  runtimes-upscalers/<rid>/native/   NIS, FSR and XeSS compiled in (DLSS only with -WithDlss).
                                                  Vendor runtimes (amd_fidelityfx_*, libxess, nvngx_*)
                                                  are NOT staged: the application ships them.
    Agility    runtimes-agility/<rid>/native/AgilitySDK/   D3D12Core.dll, d3d12SDKLayers.dll and
                                                  Microsoft's licence, for the tests only (never packed;
                                                  applications take Microsoft.Direct3D.D3D12). Staged by
                                                  either variant on Windows.

  Options are the SharpNRI contract, not NRI's defaults. Only options that exist on the target
  platform are passed: NRI declares most SDK switches with cmake_dependent_option, and passing one
  whose condition is false is silently ignored, which would make the log lie about what was built.

  FetchContent downloads go to _build/<rid>-<variant>/_deps. When a previous run left a matching
  _deps (same NRI version, patches and option set) the configure runs with
  FETCHCONTENT_FULLY_DISCONNECTED=ON and never touches the network. NRI downloads an SDK only when
  its option is ON, so the Base variant never downloads DLSS, FidelityFX or XeSS, and without
  -WithDlss nothing downloads DLSS at all.

.PARAMETER Rid
  win-x64 (default on Windows), win-arm64, linux-x64 (default on Linux), linux-arm64 or osx-arm64
  (default on macOS). Must match the host OS; this script does not cross-compile.

.PARAMETER Variant
  Base (default) or Upscalers. osx-arm64 builds Base only.

.PARAMETER WithDlss
  Path to an NVIDIA DLSS SDK checkout (the github.com/NVIDIA/DLSS layout: include/, lib/). Builds the
  Upscalers variant with NRI_ENABLE_NGX_SDK=ON against that directory (FETCHCONTENT_SOURCE_DIR_NGX,
  so NRI downloads nothing for it) into _build/<rid>-upscalers-dlss/, and stages into
  runtimes-upscalers-dlss/<rid>/native/. This is a consumer-side build: the result is under
  NVIDIA's RTX SDK licence, is never packed by this repository, and the nvngx_* runtimes are not
  staged. Implies -Variant Upscalers.

.PARAMETER Configuration
  CMake configuration. Release by default.

.PARAMETER Disable
  NRI options to force OFF, e.g. -Disable NRI_ENABLE_XESS_SDK. For when an upstream SDK breaks the
  build; the staged payload and tests/SharpNRI.Tests/NativePayloadTests.cs must then be reconciled.

.PARAMETER Generator
  CMake generator override. Default: CMake's default on Windows (Visual Studio), Ninja on Linux and
  macOS if installed, otherwise Unix Makefiles.

.PARAMETER Clean
  Delete the variant's build directory (including downloads) before configuring.
#>
[CmdletBinding()]
param(
    [ValidateSet("win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-arm64")]
    [string]$Rid,
    [ValidateSet("Base", "Upscalers")]
    [string]$Variant = "Base",
    [string]$WithDlss,
    [string]$Configuration = "Release",
    [string[]]$Disable = @(),
    [string]$Generator,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 3.0
$PSNativeCommandUseErrorActionPreference = $false

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$nri  = Join-Path $root "nri"

function Invoke-Native {
    param([string]$What, [scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$What failed with exit code $LASTEXITCODE" }
}

# ------------------------------------------------------------------------------------------ target
if (-not $Rid) {
    $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    $a = if ($arch -eq [System.Runtime.InteropServices.Architecture]::Arm64) { "arm64" } else { "x64" }
    $Rid = if ($IsWindows) { "win-$a" } elseif ($IsLinux) { "linux-$a" } elseif ($IsMacOS) { "osx-$a" } else { throw "Unsupported host OS" }
}
$targetWindows = $Rid.StartsWith("win-")
$targetLinux   = $Rid.StartsWith("linux-")
$targetMac     = $Rid.StartsWith("osx-")
$targetArm64   = $Rid.EndsWith("-arm64")
if ($targetWindows -and -not $IsWindows) { throw "$Rid must be built on Windows" }
if ($targetLinux -and -not $IsLinux) { throw "$Rid must be built on Linux" }
if ($targetMac -and -not $IsMacOS) { throw "$Rid must be built on macOS" }
$libName = if ($targetWindows) { "NRI.dll" } elseif ($targetMac) { "libNRI.dylib" } else { "libNRI.so" }

# ------------------------------------------------------------------------------------------ variant
$dlssDir = $null
if ($WithDlss) {
    if ($PSBoundParameters.ContainsKey("Variant") -and $Variant -ne "Upscalers") { throw "-WithDlss builds the Upscalers variant; it cannot be combined with -Variant $Variant" }
    $Variant = "Upscalers"
    if (-not (Test-Path $WithDlss -PathType Container)) { throw "-WithDlss: $WithDlss is not a directory" }
    $dlssDir = (Resolve-Path $WithDlss).Path
    if (-not (Test-Path (Join-Path $dlssDir "include" "nvsdk_ngx.h"))) { throw "-WithDlss: $dlssDir has no include/nvsdk_ngx.h (expected the github.com/NVIDIA/DLSS layout)" }
    if (-not (Test-Path (Join-Path $dlssDir "lib"))) { throw "-WithDlss: $dlssDir has no lib/" }
}
$upscalers  = $Variant -eq "Upscalers"
# The only upscaler NRI offers on macOS is NIS, which needs ShaderMake and DXC to compile its
# shaders on the build host; that path has never been built here, so there is no macOS Upscalers
# package to stage.
if ($targetMac -and $upscalers) { throw "$Rid builds the Base variant only" }
$variantKey = if ($dlssDir) { "upscalers-dlss" } elseif ($upscalers) { "upscalers" } else { "base" }
$stageRoot  = switch ($variantKey) { "base" { "runtimes" } "upscalers" { "runtimes-upscalers" } "upscalers-dlss" { "runtimes-upscalers-dlss" } }

# ------------------------------------------------------------------------------------------ 1. pin
$props = Get-Content (Join-Path $root "Directory.Build.props") -Raw
if ($props -notmatch '<NriVersion>([^<]+)</NriVersion>') { throw "NriVersion not found in Directory.Build.props" }
$NriVersion = $Matches[1].Trim()

$nriHeader = Join-Path $nri "Include" "NRI.h"
if (-not (Test-Path $nriHeader)) { throw "$nriHeader not found. Run: git submodule update --init" }
$m = Select-String -Path $nriHeader -Pattern '^\s*#define\s+NRI_VERSION\s+(\d+)\s*$' | Select-Object -First 1
if (-not $m) { throw "#define NRI_VERSION not found in $nriHeader" }
$headerVersion = $m.Matches[0].Groups[1].Value
if ($headerVersion -ne $NriVersion) {
    throw "nri/ is at NRI_VERSION $headerVersion but Directory.Build.props pins $NriVersion. Check out tag v$NriVersion in nri/."
}
Write-Host "==> NRI v$NriVersion, $Rid, $Configuration, variant $Variant$(if ($dlssDir) { " + DLSS from $dlssDir" })"

# ------------------------------------------------------------------------------------------ 2. patches
$patches = @(Get-ChildItem (Join-Path $root "patches") -Filter "*.patch" -File -ErrorAction SilentlyContinue | Sort-Object Name)
$sha = [System.Security.Cryptography.SHA256]::Create()
$patchHashInput = [System.Text.StringBuilder]::new()
foreach ($p in $patches) {
    $bytes = [System.IO.File]::ReadAllBytes($p.FullName)
    [void]$patchHashInput.Append($p.Name).Append(':').Append([Convert]::ToHexString($sha.ComputeHash($bytes))).Append(';')
}
$patchHash = [Convert]::ToHexString($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($patchHashInput.ToString()))).Substring(0, 16)

Write-Host "==> patches: $($patches.Count)"
foreach ($p in $patches) {
    git -C $nri apply --check --reverse -C0 $p.FullName 2>$null  # -C0: a later patch may change a line within this one's context
    if ($LASTEXITCODE -eq 0) { Write-Host "    $($p.Name): already applied, skipped"; continue }
    git -C $nri apply --check $p.FullName
    if ($LASTEXITCODE -ne 0) { throw "patch $($p.Name) does not apply to nri/ at v$NriVersion" }
    Invoke-Native "git apply $($p.Name)" { git -C $nri apply $p.FullName }
    Write-Host "    $($p.Name): applied"
}

# ------------------------------------------------------------------------------------------ 3. options
# Ordered: name -> ON/OFF. Each entry exists on the target; see nri/CMakeLists.txt for the conditions.
$sdk = if ($upscalers) { "ON" } else { "OFF" }
$opt = [ordered]@{
    NRI_STATIC_LIBRARY                     = "OFF"
    NRI_ENABLE_DEBUG_NAMES_AND_ANNOTATIONS = "ON"
    NRI_ENABLE_NONE_SUPPORT                = "ON"
    NRI_ENABLE_VALIDATION_SUPPORT          = "ON"
    NRI_ENABLE_IMGUI_EXTENSION             = "ON"
    NRI_ENABLE_VK_SUPPORT                  = "ON"   # "NOT IOS"
    NRI_ENABLE_NVTX_SUPPORT                = "ON"   # "NOT IOS"; header-only, upstream default, pinned for reproducibility
    NRI_ENABLE_NIS_SDK                     = $sdk   # "NOT IOS"; pulls in ShaderMake + DXC to compile the NIS shaders
    # "WGPU_BIN_ARCHITECTURE; NOT ANDROID; NOT IOS", and its *default* is ON under GitHub Actions
    # (GITHUB_CI). Must be passed explicitly or CI would silently build a different library.
    NRI_ENABLE_WGPU_SUPPORT                = "OFF"
}
# NGX: "(D3D11 OR D3D12 OR VK); WIN32 OR NOT IS_ARM64" -> Windows any arch, Linux x64 only. Passed
# explicitly OFF so NRI's FetchContent never declares (and never downloads) the DLSS repository.
if ($targetWindows -or -not $targetArm64) { $opt.NRI_ENABLE_NGX_SDK = $(if ($dlssDir) { "ON" } else { "OFF" }) }
elseif ($dlssDir) { throw "-WithDlss: NRI has no NGX support on $Rid" }
if ($targetWindows) {
    $opt.NRI_ENABLE_D3D11_SUPPORT          = "OFF"  # "WIN32"
    $opt.NRI_ENABLE_D3D12_SUPPORT          = "ON"   # "WIN32"
    $opt.NRI_ENABLE_NVAPI                  = "ON"   # "D3D11 OR D3D12"
    # "D3D12". Compiles NRI against the Agility headers; patch 0001 loads the SDK from a directory at
    # run time when one is present. The DLLs go to the separate Agility package, never to a variant.
    $opt.NRI_ENABLE_AGILITY_SDK_SUPPORT    = "ON"
    if (-not $targetArm64) {
        $opt.NRI_ENABLE_AMDAGS             = "ON"   # "(D3D11 OR D3D12); NOT IS_ARM64"
        $opt.NRI_ENABLE_FFX_SDK            = $sdk   # "(VK OR D3D12); WIN32; NOT IS_ARM64"
        $opt.NRI_ENABLE_XESS_SDK           = $sdk   # "D3D12; WIN32; NOT IS_ARM64"
    }
}
if ($targetMac) {
    # "APPLE". On macOS this binding is Vulkan through MoltenVK. NRI's Metal 4 backend defaults ON
    # whenever Xcode 26 and the Metal toolchain are present, so it is passed explicitly OFF: which
    # backends the library contains must not depend on the runner's Xcode.
    $opt.NRI_ENABLE_METAL_SUPPORT          = "OFF"
}
# Not passed anywhere: NRI_ENABLE_METAL_SUPPORT off Apple, D3D11/D3D12/NVAPI/AGS/Agility/FFX/XeSS
# off Windows. NRI_ENABLE_XLIB_SUPPORT / NRI_ENABLE_WAYLAND_SUPPORT are left to NRI's header
# detection, which only runs on Linux.
foreach ($d in $Disable) {
    if (-not $opt.Contains($d)) { throw "-Disable ${d}: not an option this script passes for $Rid" }
    $opt[$d] = "OFF"
    Write-Warning "$d forced OFF by -Disable"
}
$on = { param($n) $opt.Contains($n) -and $opt[$n] -eq "ON" }

# ------------------------------------------------------------------------------------------ configure
$build   = Join-Path $root "_build" "$Rid-$variantKey"
$deps    = Join-Path $build "_deps"
$binRoot = Join-Path $build "bin"
if ($Clean -and (Test-Path $build)) { Remove-Item $build -Recurse -Force -Confirm:$false }
New-Item -ItemType Directory -Force -Path $build | Out-Null

$fwd = { param($p) $p -replace '\\', '/' }
# Windows: d3d12.lib and dxgi.lib make d3d12.dll and dxgi.dll load-time imports of NRI.dll, so every
# process that loads NRI.dll loads them, a Vulkan-only one included; with XeSS, libxess.lib does the
# same for libxess.dll, without which NRI.dll would not load at all. A CMake fragment injected after
# NRI's project() adds /DELAYLOAD for the NRI target only (AGENTS.md: Native packages).
$delayLoad = $targetWindows
$delayLoadDlls = @(if (& $on NRI_ENABLE_D3D12_SUPPORT) { "d3d12.dll", "dxgi.dll" }) + @(if (& $on NRI_ENABLE_XESS_SDK) { "libxess.dll" })
$delayLoadFragment = Join-Path $PSScriptRoot "cmake" "delayload.cmake"

$optString = ($opt.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ";"
# NRI_VERSION alone does not identify the sources: NRI's main moves its dependency pins (VMA, D3D12MA)
# without bumping it, and the "next" branch builds such commits (AGENTS.md: Branches).
$nriCommit = (git -C $nri rev-parse HEAD 2>$null) -join ""
$depsKey = "nri=$NriVersion;commit=$nriCommit;patches=$patchHash;rid=$Rid;$optString$(if ($dlssDir) { ";ngx=$dlssDir" })"
$depsMarker = Join-Path $deps ".sharpnri-deps"
$offline = (Test-Path $depsMarker) -and ((Get-Content $depsMarker -Raw).Trim() -eq $depsKey)

$cmakeArgs = @("-S", $nri, "-B", $build)
if ($Generator) {
    $cmakeArgs += @("-G", $Generator)
} elseif ($targetWindows) {
    $cmakeArgs += @("-A", $(if ($targetArm64) { "ARM64" } else { "x64" }))
} else {
    $cmakeArgs += @("-G", $(if (Get-Command ninja -ErrorAction SilentlyContinue) { "Ninja" } else { "Unix Makefiles" }))
}
# NRI builds with -Werror and GCC 13 reports a false maybe-uninitialized in HelperInterface.h
# (measured on Ubuntu 24.04 and on the GitHub runner); NRI's own flags are written for clang.
# Prefer clang when it is installed and the caller has not chosen a compiler.
if ($targetLinux -and -not $env:CXX -and (Get-Command clang++ -ErrorAction SilentlyContinue)) {
    $cmakeArgs += @("-DCMAKE_C_COMPILER=clang", "-DCMAKE_CXX_COMPILER=clang++")
    Write-Host "==> compiler: clang++ (set CXX to override)"
}
# macOS: CMake's default compiler is Xcode's AppleClang, which is what NRI's Apple support is
# written against. The architecture is pinned so a pwsh running under Rosetta, or a universal
# default, cannot produce anything but an arm64 library for an arm64 RID.
if ($targetMac) { $cmakeArgs += "-DCMAKE_OSX_ARCHITECTURES=$(if ($targetArm64) { 'arm64' } else { 'x86_64' })" }
# The loader is loaded at runtime like everywhere else (patch 0007), never linked: a library that
# links Homebrew's loader by absolute path does not load on a Mac without it. The deployment
# target is .NET 11's oldest macOS (15); MoltenVK asks for 12, NRI pins nothing (AGENTS.md: Decisions).
if ($targetMac) { $cmakeArgs += @("-DNRI_ENABLE_VULKAN_LOADER_LINKING=OFF", "-DCMAKE_OSX_DEPLOYMENT_TARGET=15.0") }
if (-not $targetWindows -or $Generator) { $cmakeArgs += "-DCMAKE_BUILD_TYPE=$Configuration" }  # single-config generators
$cmakeArgs += @(
    "-DCMAKE_RUNTIME_OUTPUT_DIRECTORY=$(& $fwd $binRoot)"      # NRI defaults these to nri/_Bin
    "-DCMAKE_LIBRARY_OUTPUT_DIRECTORY=$(& $fwd $binRoot)"
    "-DNRI_SHADERS_PATH=$(& $fwd (Join-Path $build '_Shaders'))" # NRI defaults this to nri/_Shaders
    "-DFETCHCONTENT_BASE_DIR=$(& $fwd $deps)"
    "-DFETCHCONTENT_FULLY_DISCONNECTED=$(if ($offline) { 'ON' } else { 'OFF' })"
    # ShaderMake otherwise prefers whatever dxc a local Vulkan SDK has; use its pinned GitHub DXC for
    # both DXIL and SPIR-V so the embedded shader blobs do not depend on the machine.
    "-DSHADERMAKE_FIND_DXC_VK=OFF"
    # Always passed (empty when unused) so a stale value can never linger in an existing cache.
    "-DCMAKE_PROJECT_NRI_INCLUDE=$(if ($delayLoad) { & $fwd $delayLoadFragment })"
)
# Only the -WithDlss build tree ever has NGX on, so this never needs clearing elsewhere.
if ($dlssDir) { $cmakeArgs += "-DFETCHCONTENT_SOURCE_DIR_NGX=$(& $fwd $dlssDir)" }
foreach ($kv in $opt.GetEnumerator()) { $cmakeArgs += "-D$($kv.Key)=$($kv.Value)" }

# A new NRI commit, patch set or option set starts the variant's tree from scratch, downloads included.
# NRI declares pins as cache variables (NRI_AGILITY_SDK_VERSION_MAJOR / _MINOR), and a cache entry
# outlives the default that created it: v191 moved Agility 1.619.6 to 1.620.1, and an existing tree
# kept compiling and staging 1.619 while a fresh one (CI) got 1.620. With only the cache cleared,
# FetchContent kept the extracted 1.619 package in _deps, and NRI.dll built for 1.620 met a 1.619
# runtime: every D3D12 device failed INVALID_SDK (measured 2026-11-03). CI builds fresh trees anyway.
if (-not $offline -and (Test-Path (Join-Path $build "CMakeCache.txt"))) {
    Write-Host "==> NRI commit, patches or options changed since the last configure: fresh build tree, downloads again"
    Remove-Item -Recurse -Force $build
}
Write-Host "==> configure -> $build$(if ($offline) { ' (deps cached, offline)' })"
foreach ($kv in $opt.GetEnumerator()) { Write-Host ("    {0,-40} {1}" -f $kv.Key, $kv.Value) }
if ($delayLoad) { Write-Host ("    {0,-40} {1}" -f "/DELAYLOAD:$($delayLoadDlls -join ',')", "(tools/cmake/delayload.cmake)") }
if ($dlssDir)   { Write-Host "    FETCHCONTENT_SOURCE_DIR_NGX              $dlssDir" }
Invoke-Native "cmake configure" { cmake @cmakeArgs }
New-Item -ItemType Directory -Force -Path $deps | Out-Null
Set-Content -Path $depsMarker -Value $depsKey -NoNewline

# NRI writes Include/NRIAgilitySDK.h into its own source tree at configure time (for the host
# executable to include). Keep it, but not in the submodule.
$agilityHeader = Join-Path $nri "Include" "NRIAgilitySDK.h"
if (Test-Path $agilityHeader) { Move-Item $agilityHeader (Join-Path $build "NRIAgilitySDK.h") -Force }

# What CMake actually decided (dependent options can still be forced OFF by their conditions).
$cache = Get-Content (Join-Path $build "CMakeCache.txt")
$effective = [ordered]@{}
foreach ($n in @($opt.Keys) + @("NRI_ENABLE_XLIB_SUPPORT", "NRI_ENABLE_WAYLAND_SUPPORT")) {
    $line = $cache | Where-Object { $_ -match "^$n`:(BOOL|INTERNAL)=(.*)$" } | Select-Object -First 1
    if ($line -and $line -match "=(.*)$") { $effective[$n] = $Matches[1] }
}
foreach ($kv in $opt.GetEnumerator()) {
    if ($effective.Contains($kv.Key) -and $effective[$kv.Key] -ne $kv.Value) {
        throw "$($kv.Key): requested $($kv.Value) but CMake has $($effective[$kv.Key]) (dependent option condition false?)"
    }
}
if ($targetLinux) {
    Write-Host "    NRI_ENABLE_XLIB_SUPPORT    (auto) $($effective['NRI_ENABLE_XLIB_SUPPORT'])"
    Write-Host "    NRI_ENABLE_WAYLAND_SUPPORT (auto) $($effective['NRI_ENABLE_WAYLAND_SUPPORT'])"
}
# The DLSS repository is 1.4 GB and under NVIDIA's terms: without -WithDlss it must not be fetched.
if (-not $dlssDir -and (Test-Path (Join-Path $deps "ngx-src"))) {
    throw "NGX is off but $deps/ngx-src exists: something downloaded the DLSS SDK"
}

# ------------------------------------------------------------------------------------------ build
Write-Host "==> build ($Configuration)"
Invoke-Native "cmake build" { cmake --build $build --config $Configuration --parallel }
if (Test-Path $agilityHeader) { Move-Item $agilityHeader (Join-Path $build "NRIAgilitySDK.h") -Force }

# Multi-config generators append the configuration to a non-genex output directory.
$bin = @((Join-Path $binRoot $Configuration), $binRoot) | Where-Object {
    Test-Path (Join-Path $_ $libName)
} | Select-Object -First 1
if (-not $bin) { throw "NRI library not found under $binRoot" }

$nriStatus = git -C $nri status --porcelain
if ($nriStatus -and $patches.Count -eq 0) { Write-Warning "nri/ is not clean after the build:`n$($nriStatus -join "`n")" }

# Load-time imports of NRI.dll: no vendor runtime may be among them (a missing one would make the
# whole library fail to load), and every DLL the fragment delay-loads must have left them for the
# delay-load table. Read with dumpbin when Visual Studio provides one.
if ($targetWindows) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio" "Installer" "vswhere.exe"
    $dumpbin = if (Test-Path $vswhere) {
        & $vswhere -latest -products * -find "VC/Tools/MSVC/**/bin/Hostx64/x64/dumpbin.exe" | Select-Object -First 1
    }
    if ($dumpbin) {
        $deps_ = & $dumpbin /nologo /dependents (Join-Path $bin "NRI.dll")
        $section = ""; $loadTime = @(); $delayed = @()
        foreach ($l in $deps_) {
            if ($l -match 'following dependencies:') { $section = "load" }
            elseif ($l -match 'following delay load dependencies:') { $section = "delay" }
            elseif ($l -match '^\s+(\S+\.dll)\s*$') { if ($section -eq "load") { $loadTime += $Matches[1] } elseif ($section -eq "delay") { $delayed += $Matches[1] } }
            elseif ($l -match '^\s+Summary') { $section = "" }
        }
        Write-Host "==> NRI.dll imports: $($loadTime -join ', ')"
        if ($delayed) { Write-Host "    delay-loaded:    $($delayed -join ', ')" }
        $vendor = @($loadTime | Where-Object { $_ -match '^(libxess|nvngx|amd_fidelityfx|amd_ags|D3D12Core)' })
        if ($vendor) { throw "NRI.dll has a load-time import of a vendor runtime: $($vendor -join ', ')" }
        foreach ($dll in $delayLoadDlls) {
            if ($delayed -notcontains $dll) { throw "$dll is not delay-loaded by NRI.dll" }
            if ($loadTime -contains $dll) { throw "$dll is still a load-time import of NRI.dll" }
        }
    } else {
        Write-Warning "dumpbin not found; NRI.dll's imports were not checked"
    }
}
# macOS: NRI's CMakeLists would link the Vulkan loader into the library on Apple by the absolute
# install name of whatever loader the build machine had (AGENTS.md: MoltenVK and the paravirtual Mac); patch 0007 turns that off
# and the loader is dlopened at device creation as on Linux. So the library may link nothing but
# the system: a loader link means the option was lost, MoltenVK linked directly would bypass the
# loader and with it the validation layer. The minimum macOS version stamped into the library is
# the deployment target above.
if ($targetMac) {
    $linked = @(otool -L (Join-Path $bin $libName) | Select-Object -Skip 1 | ForEach-Object { ($_.Trim() -split ' \(')[0] })
    Write-Host "==> $libName links: $($linked -join ', ')"
    $foreign = @($linked | Where-Object { $_ -notmatch '^(/usr/lib/|/System/|@rpath/libNRI\.dylib$)' })
    if ($foreign) { throw "$libName links something other than the system: $($foreign -join ', ')" }
    $build_ = @(otool -l (Join-Path $bin $libName) | Select-String -Pattern '^\s*(minos|sdk)\s' | ForEach-Object { $_.Line.Trim() })
    Write-Host "    build version: $($build_ -join ', ')"
}

# ------------------------------------------------------------------------------------------ 4. stage
$staged = [System.Collections.Generic.List[string]]::new()
$out = $null
function Start-Staging([string]$Dir) {
    if (Test-Path $Dir) { Remove-Item $Dir -Recurse -Force -Confirm:$false }
    New-Item -ItemType Directory -Force -Path $Dir | Out-Null
    $script:out = $Dir
    $script:staged = [System.Collections.Generic.List[string]]::new()
}
function Stage([string]$Source, [string]$RelDest) {
    if (-not (Test-Path $Source -PathType Leaf)) { throw "expected file missing: $Source" }
    $dest = Join-Path $script:out $RelDest
    New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
    Copy-Item $Source $dest -Force
    $script:staged.Add(($RelDest -replace '\\', '/'))
}
function StageLicense([string]$Component, [string]$Source, [string]$Name) {
    if (-not $Name) { $Name = Split-Path $Source -Leaf }
    Stage $Source "licenses/$Component/$Name"
}
function Write-Summary([string]$Title) {
    Write-Host ""
    Write-Host "==> $Title staged into $script:out"
    $total = 0L
    $rows = foreach ($rel in $script:staged) {
        $len = (Get-Item (Join-Path $script:out $rel)).Length
        $total += $len
        [pscustomobject]@{ File = $rel; Bytes = $len; Size = if ($len -ge 1MB) { "{0:N1} MB" -f ($len / 1MB) } else { "{0:N1} KB" -f ($len / 1KB) } }
    }
    $rows | Format-Table -AutoSize File, @{ n = "Size"; e = { $_.Size }; a = "right" }, @{ n = "Bytes"; e = { $_.Bytes }; a = "right" } | Out-String | Write-Host
    Write-Host ("    {0} files, {1:N1} MB total" -f $script:staged.Count, ($total / 1MB))
}

Start-Staging (Join-Path $root $stageRoot $Rid "native")

$arch = if ($targetArm64) { "arm64" } else { "x64" }
if ($targetWindows) {
    Stage (Join-Path $bin "NRI.dll") "NRI.dll"
    if (& $on NRI_ENABLE_AMDAGS) { Stage (Join-Path $bin "amd_ags_$arch.dll") "amd_ags_$arch.dll" }   # LoadLibrary'd by name; MIT
} else {
    Stage (Join-Path $bin $libName) $libName
}

# Anything NRI's post-build copied that we did not stage is a vendor runtime the application ships,
# the Agility SDK (staged apart, tests only), a tool, debug data, or new.
$notOurs = '^(nvngx_dlssd?\.dll|libnvidia-ngx-dlssd?\.so\..*|amd_fidelityfx_(loader|upscaler)_(dx12|vk)\.dll|libxess\.dll)$'
$ignored = '\.(lib|exp|pdb|ilk|a)$|^AgilitySDK[\\/]'
$unexpected = @(Get-ChildItem $bin -Recurse -File | ForEach-Object {
    [System.IO.Path]::GetRelativePath($bin, $_.FullName) -replace '\\', '/'
} | Where-Object { $_ -notin $staged -and $_ -notmatch $ignored })
foreach ($u in $unexpected) {
    if ($u -match $notOurs) { Write-Host "    not staged: $u (vendor runtime; the application ships it)" }
    else { Write-Warning "not staged: $u (review whether NRI needs it at run time)" }
}

# ------------------------------------------------------------------------------- vendor runtimes
# What this NRI was built against, for the application that ships the runtimes: each upscaler SDK
# at NRI's pin (nri/CMakeLists.txt) and each runtime NRI's build fetched with it, by file version
# and SHA-256. Beside native/, so the package carries it (runtimes/<rid>/upscaler-runtimes.json)
# and it is not copied next to the application; the release notes list it (release.yml).
if ($variantKey -ne "base") {
    $cmake = Get-Content (Join-Path $nri "CMakeLists.txt") -Raw
    function PinOf([string]$Pattern, [string]$What) {
        if ($cmake -notmatch $Pattern) { throw "the $What pin was not found in nri/CMakeLists.txt" }
        $Matches[1]
    }
    function RuntimesOf([string]$Pattern) {
        @(Get-ChildItem $bin -File | Where-Object { $_.Name -match $Pattern } | Sort-Object Name | ForEach-Object {
            $version = $_.VersionInfo.FileVersion
            if (-not $version -and $_.Name -match '\.so\.([\d.]+)$') { $version = $Matches[1] }
            [ordered]@{ file = $_.Name; version = "$version".Trim(); sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
        })
    }
    # Each SDK's release page at that version, for the release notes' links
    function ReleasePage([string]$Repo, [string]$Version) { "https://github.com/$Repo/releases/tag/v$Version" }
    $sdks = [System.Collections.Generic.List[object]]::new()
    if (& $on NRI_ENABLE_NIS_SDK) {
        $nisVersion = if ((Get-Content (Join-Path $nri "Shaders" "NIS.hlsl") -Raw) -match 'NVIDIA Image Scaling SDK\s+-\s+v([\d.]+)') { $Matches[1] } else { throw "the NIS SDK version was not found in nri/Shaders/NIS.hlsl" }
        $sdks.Add([ordered]@{ name = "NVIDIA Image Scaling"; version = $nisVersion; url = (ReleasePage "NVIDIAGameWorks/NVIDIAImageScaling" $nisVersion); runtimes = @(); note = "compiled in" })
    }
    if (& $on NRI_ENABLE_FFX_SDK) {
        $ffx = PinOf 'FidelityFX-SDK/archive/refs/tags/v([\d.]+)\.zip' "FidelityFX SDK"
        $sdks.Add([ordered]@{ name = "AMD FidelityFX SDK"; version = $ffx; url = (ReleasePage "GPUOpen-LibrariesAndSDKs/FidelityFX-SDK" $ffx); runtimes = [object[]](RuntimesOf '^amd_fidelityfx_(loader|upscaler)_(dx12|vk)\.dll$'); note = $null })
    }
    if (& $on NRI_ENABLE_XESS_SDK) {
        $xess = PinOf 'intel/xess/archive/refs/tags/v([\d.]+)\.zip' "XeSS SDK"
        $sdks.Add([ordered]@{ name = "Intel XeSS SDK"; version = $xess; url = (ReleasePage "intel/xess" $xess); runtimes = [object[]](RuntimesOf '^libxess\.dll$'); note = $null })
    }
    $ngx = PinOf 'set\(NGX_VERSION "([\d.]+)"\)' "NGX"
    if (& $on NRI_ENABLE_NGX_SDK) {
        $sdks.Add([ordered]@{ name = "NVIDIA DLSS"; version = $ngx; url = (ReleasePage "NVIDIA/DLSS" $ngx); runtimes = [object[]](RuntimesOf '^(nvngx_dlssd?\.dll|libnvidia-ngx-dlssd?\.so\..*)$'); note = $null })
    } else {
        $sdks.Add([ordered]@{ name = "NVIDIA DLSS"; version = $ngx; url = (ReleasePage "NVIDIA/DLSS" $ngx); runtimes = @(); note = "not packaged" })
    }
    foreach ($s in $sdks) {
        if (-not $s.note -and $s.runtimes.Count -eq 0) { throw "$($s.name): NRI's build fetched no runtime to record" }
        foreach ($r in $s.runtimes) { if (-not $r.version) { throw "$($s.name): $($r.file) has no version" } }
    }
    $nriRepo = (git -C $root config -f .gitmodules submodule.nri.url) -replace '\.git$', ''
    $manifest = [ordered]@{ nri = $NriVersion; nriUrl = "$nriRepo/releases/tag/v$NriVersion"; rid = $Rid; sdks = $sdks }
    $manifestPath = Join-Path $root $stageRoot $Rid "upscaler-runtimes.json"
    $manifest | ConvertTo-Json -Depth 5 | Set-Content $manifestPath -Encoding utf8NoBOM
    Write-Host "    vendor runtimes recorded in $manifestPath"
    foreach ($s in $sdks) {
        $files = ($s.runtimes | ForEach-Object { "$($_.file) $($_.version)" }) -join ", "
        Write-Host ("      {0} {1}: {2}" -f $s.name, $s.version, $(if ($files) { $files } else { $s.note }))
    }
}

# ------------------------------------------------------------------------------------------ licences
# Only for what is inside the staged files: compiled into NRI, linked into NRI, or shipped beside it.
StageLicense "NRI" (Join-Path $nri "LICENSE.txt")
if (& $on NRI_ENABLE_NVTX_SUPPORT) { StageLicense "NVTX" (Join-Path $deps "nvtx-src" "LICENSE.txt") }
if (& $on NRI_ENABLE_VK_SUPPORT) {
    StageLicense "Vulkan-Headers" (Join-Path $deps "vulkan_headers-src" "LICENSE.md")
    foreach ($f in Get-ChildItem (Join-Path $deps "vulkan_headers-src" "LICENSES") -File) { StageLicense "Vulkan-Headers" $f.FullName }
    StageLicense "VulkanMemoryAllocator" (Join-Path $deps "vma-src" "LICENSE.txt")
}
if (& $on NRI_ENABLE_D3D12_SUPPORT) {
    StageLicense "D3D12MemoryAllocator" (Join-Path $deps "d3d12ma-src" "LICENSE.txt")
    StageLicense "D3D12MemoryAllocator" (Join-Path $deps "d3d12ma-src" "NOTICES.txt")
}
# The Agility headers are compiled into NRI (MIT). The runtime's licence goes with the runtime, in
# runtimes-agility/, which no package carries.
if (& $on NRI_ENABLE_AGILITY_SDK_SUPPORT) { StageLicense "DirectX-AgilitySDK" (Join-Path $deps "agilitysdk-src" "LICENSE-CODE.txt") }
if (& $on NRI_ENABLE_NVAPI)   { StageLicense "NVAPI" (Join-Path $deps "nvapi-src" "License.txt") }
if (& $on NRI_ENABLE_AMDAGS)  { StageLicense "AMD-AGS" (Join-Path $deps "amdags-src" "ags_lib" "LICENSE.txt") }
if (& $on NRI_ENABLE_NGX_SDK) {
    # nvsdk_ngx is statically linked into NRI: the NVIDIA RTX SDKs licence attaches to NRI.dll itself.
    StageLicense "NVIDIA-DLSS-NGX" (Join-Path $dlssDir "LICENSE.txt")
}
if (& $on NRI_ENABLE_FFX_SDK) {
    # NRI compiles against the FSR SDK's API headers (D3D12 and, through the same API, Vulkan) and
    # loads the runtime loader by name (amd_fidelityfx_loader_dx12.dll / _vk.dll since NRI v191),
    # which loads the upscaler; both are the application's to ship.
    StageLicense "AMD-FSR-SDK" (Join-Path $deps "fsr-src" "docs" "license.md")
    StageLicense "AMD-FSR-SDK" (Join-Path $deps "fsr-src" "3rdpartynotice.md")
}
if (& $on NRI_ENABLE_XESS_SDK) {
    # Headers and the libxess.lib import stubs are inside NRI.dll; libxess.dll itself is not staged.
    StageLicense "Intel-XeSS" (Join-Path $deps "xess-src" "LICENSE.txt")
    StageLicense "Intel-XeSS" (Join-Path $deps "xess-src" "third-party-programs.txt")
}
if (& $on NRI_ENABLE_NIS_SDK) {
    # NIS ships inside NRI (Shaders/NIS*.hlsl, compiled into NRI); its MIT notice is the header comment.
    $nisLines = Get-Content (Join-Path $nri "Shaders" "NIS.hlsl")
    $end = [Array]::FindIndex([string[]]$nisLines, [Predicate[string]] { param($l) $l -notmatch '^//' })
    $nisLicense = Join-Path $build "NIS-LICENSE.txt"
    ($nisLines[0..($end - 1)] | ForEach-Object { $_ -replace '^// ?', '' }) -join "`n" | Set-Content $nisLicense -NoNewline
    StageLicense "NVIDIA-Image-Scaling" $nisLicense "LICENSE.txt"
    # ShaderMakeBlob (blob lookup for the embedded shaders) is linked into NRI.
    StageLicense "ShaderMake" (Join-Path $deps "shadermake-src" "LICENSE.txt")
}
$notices = Join-Path $root "THIRD-PARTY-NOTICES.md"
if (Test-Path $notices) { Stage $notices "licenses/THIRD-PARTY-NOTICES.md" }

Write-Summary "$Variant$(if ($dlssDir) { ' + DLSS' })"
if ($dlssDir) {
    Write-Warning "This NRI.dll statically links NVIDIA's NGX SDK and is under the NVIDIA RTX SDKs licence. It is for your application only: do not commit or pack it. nvngx_dlss.dll / nvngx_dlssd.dll are not staged; ship them from $dlssDir/lib beside your executable."
}

# ------------------------------------------------------------------------------------------ 5. Agility
# Same SDK version for every variant (NRI_AGILITY_SDK_VERSION_*), so whichever variant ran last
# stages identical bytes.
if ($targetWindows -and (& $on NRI_ENABLE_AGILITY_SDK_SUPPORT)) {
    Start-Staging (Join-Path $root "runtimes-agility" $Rid "native")
    # NRI copies the whole bin/<arch> of the NuGet package; only what the package's
    # "distributable files.txt" names may be redistributed, and only the DLLs are runtime files.
    $redist = Get-Content (Join-Path $deps "agilitysdk-src" "distributable files.txt")
    foreach ($f in "D3D12Core.dll", "d3d12SDKLayers.dll") {
        if ($redist -notcontains $f) { throw "Agility SDK no longer lists $f as distributable" }
        Stage (Join-Path $bin "AgilitySDK" $f) "AgilitySDK/$f"
    }
    Stage (Join-Path $deps "agilitysdk-src" "LICENSE.txt") "AgilitySDK/LICENSE.txt"   # Microsoft Software License Terms
    Write-Summary "Agility"
}
