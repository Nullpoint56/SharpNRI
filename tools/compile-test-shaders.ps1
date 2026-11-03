#requires -Version 7
<#
.SYNOPSIS
    Regenerates every test shader binary under tests/SharpNRI.Tests/Shaders from its .hlsl source.

.DESCRIPTION
    Each .hlsl file is scanned for entry points by name; every entry found is compiled twice,
    to SPIR-V (Vulkan) and to DXIL (D3D12), next to the source:

        entry       stage   outputs
        vsMain      vs_6_0  <Name>.vs.spv  <Name>.vs.dxil
        psMain      ps_6_0  <Name>.ps.spv  <Name>.ps.dxil
        csMain      cs_6_5  <Name>.cs.spv  <Name>.cs.dxil
        msMain      ms_6_5  <Name>.ms.spv  <Name>.ms.dxil   (mesh shader; SPIR-V uses SPV_EXT_mesh_shader)
        main        cs_6_0  <Name>.cs.spv  <Name>.cs.dxil   (only with [numthreads], i.e. compute)
        rayGen      lib_6_3 <Name>.lib.spv <Name>.<export>.dxil  (ray tracing library: one SPIR-V module with
                                                              every entry point; one DXIL library per
                                                              [shader("...")] function, -exports <export>)

    The exact command lines (with <T> the target profile and <E> the entry point):

        dxc.exe -T <T> -E <E> -spirv -fspv-target-env=vulkan1.2 -Fo <Name>.<stage>.spv  <Name>.hlsl
        dxc.exe -T <T> -E <E>                                    -Fo <Name>.<stage>.dxil <Name>.hlsl

    No include paths, no defines, no -fvk-*-shift: the shaders bind with explicit
    [[vk::binding(b, s)]] and register(xN, spaceS), and the test devices are created with all-zero
    VKBindingOffsets, so the Vulkan binding is the register index.

    The DXC used for the committed binaries is the one in the Vulkan SDK
    (E:\programs\VulkanSDK\Bin\dxc.exe, 1.9.0.5180). It ships no dxil.dll, so the DXIL is not
    signed by the validator; DXC 1.8+ emits the "bypass" hash that current D3D12 runtimes accept.

.PARAMETER Dxc
    Path to dxc.exe. Defaults to $env:VULKAN_SDK\Bin\dxc.exe, then E:\programs\VulkanSDK\Bin\dxc.exe,
    then dxc on PATH.
#>
param(
    [string] $Dxc
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$shaderDir = Join-Path $root 'tests' 'SharpNRI.Tests' 'Shaders'

if (-not $Dxc) {
    $candidates = @()
    if ($env:VULKAN_SDK) { $candidates += Join-Path $env:VULKAN_SDK 'Bin' 'dxc.exe' }
    $candidates += 'E:\programs\VulkanSDK\Bin\dxc.exe'
    $Dxc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $Dxc) { $Dxc = (Get-Command dxc -ErrorAction SilentlyContinue)?.Source }
}
if (-not $Dxc -or -not (Test-Path $Dxc)) { throw "dxc.exe not found; pass -Dxc <path>" }
Write-Host "dxc: $Dxc"
& $Dxc --version

$entries = @(
    @{ Name = 'vsMain'; Profile = 'vs_6_0'; Stage = 'vs' },
    @{ Name = 'psMain'; Profile = 'ps_6_0'; Stage = 'ps' },
    @{ Name = 'csMain'; Profile = 'cs_6_5'; Stage = 'cs' },  # 6.5: RayQuery.hlsl needs inline ray tracing
    @{ Name = 'msMain'; Profile = 'ms_6_5'; Stage = 'ms' },  # mesh shader; SPIR-V gets SPV_EXT_mesh_shader
    @{ Name = 'main';   Profile = 'cs_6_0'; Stage = 'cs'; RequiresNumThreads = $true },
    @{ Name = 'rayGen'; Profile = 'lib_6_3'; Stage = 'lib' } # ray tracing pipeline: raygen, miss and hit shaders in one library
)

$compiled = 0
foreach ($source in Get-ChildItem -Path $shaderDir -Filter '*.hlsl' -File | Sort-Object Name) {
    $text = Get-Content -Raw $source.FullName
    $base = [IO.Path]::GetFileNameWithoutExtension($source.Name)
    $found = $false

    foreach ($e in $entries) {
        if ($text -notmatch "\b$($e.Name)\s*\(") { continue }
        if ($e.RequiresNumThreads -and $text -notmatch '\[numthreads') { continue }
        $found = $true

        $spv = Join-Path $shaderDir "$base.$($e.Stage).spv"
        $dxil = Join-Path $shaderDir "$base.$($e.Stage).dxil"

        if ($e.Stage -eq 'lib') {
            # One SPIR-V module holds every entry point. DXIL gets one library per [shader("...")]
            # function (-exports): NRI's D3D12 backend adds each ShaderDesc as a DXIL library
            # subobject exporting everything in it, so one shared library would export each name
            # three times.
            Write-Host "$($source.Name): library -> $(Split-Path -Leaf $spv)"
            & $Dxc -T $e.Profile -spirv '-fspv-target-env=vulkan1.2' -Fo $spv $source.FullName
            if ($LASTEXITCODE -ne 0) { throw "dxc failed (SPIR-V) for $($source.Name)" }
            $compiled++
            foreach ($m in [regex]::Matches($text, '\[shader\("\w+"\)\]\s*void\s+(\w+)\s*\(')) {
                $export = $m.Groups[1].Value
                $exportDxil = Join-Path $shaderDir "$base.$export.dxil"
                Write-Host "$($source.Name): $export -> $(Split-Path -Leaf $exportDxil)"
                & $Dxc -T $e.Profile -exports $export -Fo $exportDxil $source.FullName
                if ($LASTEXITCODE -ne 0) { throw "dxc failed (DXIL) for $($source.Name):$export" }
                $compiled++
            }
            continue
        }

        Write-Host "$($source.Name): $($e.Name) -> $(Split-Path -Leaf $spv), $(Split-Path -Leaf $dxil)"
        & $Dxc -T $e.Profile -E $e.Name -spirv '-fspv-target-env=vulkan1.2' -Fo $spv $source.FullName
        if ($LASTEXITCODE -ne 0) { throw "dxc failed (SPIR-V) for $($source.Name):$($e.Name)" }
        & $Dxc -T $e.Profile -E $e.Name -Fo $dxil $source.FullName
        if ($LASTEXITCODE -ne 0) { throw "dxc failed (DXIL) for $($source.Name):$($e.Name)" }
        $compiled += 2
    }

    if (-not $found) { throw "$($source.Name): no known entry point (vsMain, psMain, csMain, msMain, rayGen, or main with [numthreads])" }
}

Write-Host "compiled $compiled binaries"
