<#
.SYNOPSIS
    Vulkan for the Windows CI runner: the loader, the Khronos validation layer and lavapipe.

.DESCRIPTION
    The hosted Windows runner has no GPU and no Vulkan driver, so without this every Vulkan test case
    skips there (WARP covers D3D12 only). This lays out, under -Dest:

      bin\vulkan-1.dll                              the Khronos loader, from the Vulkan SDK
      layers\VkLayer_khronos_validation.{dll,json}  the validation layer, from the same SDK
      lvp\vulkan_lvp.dll, lvp\lvp_icd.x86_64.json   Mesa's lavapipe (software Vulkan), a pinned
                                                    mesa-dist-win build, checked by SHA-256

    and the caller points the tests at them:

      PATH            += <Dest>\bin
      VK_ADD_LAYER_PATH = <Dest>\layers
      VK_DRIVER_FILES   = <Dest>\lvp\lvp_icd.x86_64.json

    The SDK version is the one ci.yml pins for Linux (VULKAN_SDK_VERSION), so both legs validate with
    the same layer. Its installer runs unattended (--confirm-command install) into a temporary root;
    the loader comes from that root's Bin when present, else from the runtime the installer put into
    System32. A -Dest that already holds every file is left alone (the CI cache restores it).
#>
param(
    [Parameter(Mandatory)][string]$Dest,
    [string]$SdkVersion = $env:VULKAN_SDK_VERSION,
    [string]$MesaVersion = $env:MESA_WIN_VERSION,
    [string]$MesaSha256 = $env:MESA_WIN_SHA256
)

$ErrorActionPreference = 'Stop'
$files = 'bin\vulkan-1.dll', 'layers\VkLayer_khronos_validation.dll', 'layers\VkLayer_khronos_validation.json',
         'lvp\vulkan_lvp.dll', 'lvp\lvp_icd.x86_64.json'
if (-not ($files | Where-Object { -not (Test-Path (Join-Path $Dest $_)) })) {
    Write-Host "==> Vulkan for Windows present in $Dest"
    return
}
if (-not $SdkVersion -or -not $MesaVersion -or -not $MesaSha256) { throw "SdkVersion, MesaVersion and MesaSha256 are required" }

$tmp = Join-Path ([IO.Path]::GetTempPath()) "vk-win-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force $tmp, (Join-Path $Dest 'bin'), (Join-Path $Dest 'layers'), (Join-Path $Dest 'lvp') | Out-Null
try {
    # ---------------------------------------------------------------- loader and layer, from the SDK
    $installer = Join-Path $tmp 'sdk.exe'
    $url = "https://sdk.lunarg.com/sdk/download/$SdkVersion/windows/vulkansdk-windows-X64-$SdkVersion.exe"
    Write-Host "==> downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $installer
    $root = Join-Path $tmp 'sdk'
    Write-Host "==> installing the SDK into $root"
    $p = Start-Process -FilePath $installer -ArgumentList '--root', $root, '--accept-licenses', '--default-answer', '--confirm-command', 'install' -Wait -PassThru -NoNewWindow
    if ($p.ExitCode -ne 0) { throw "the SDK installer exited with $($p.ExitCode)" }
    $bin = Join-Path $root 'Bin'
    foreach ($f in 'VkLayer_khronos_validation.dll', 'VkLayer_khronos_validation.json') {
        $src = Join-Path $bin $f
        if (-not (Test-Path $src)) { throw "$src missing; the SDK's Bin holds: $((Get-ChildItem $bin -Name) -join ', ')" }
        Copy-Item $src (Join-Path $Dest "layers\$f")
    }
    $loader = @((Join-Path $bin 'vulkan-1.dll'), (Join-Path $env:SystemRoot 'System32\vulkan-1.dll')) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $loader) { throw "no vulkan-1.dll in the SDK's Bin or in System32 after the install" }
    Copy-Item $loader (Join-Path $Dest 'bin\vulkan-1.dll')
    Write-Host "==> loader from $loader, layer $SdkVersion"

    # ---------------------------------------------------------------- lavapipe, from mesa-dist-win
    $archive = Join-Path $tmp 'mesa.7z'
    $url = "https://github.com/pal1000/mesa-dist-win/releases/download/$MesaVersion/mesa3d-$MesaVersion-release-msvc.7z"
    Write-Host "==> downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $archive
    $hash = (Get-FileHash $archive -Algorithm SHA256).Hash
    if ($hash -ne $MesaSha256.ToUpperInvariant()) { throw "mesa archive SHA-256 is $hash, expected $MesaSha256" }
    $7z = (Get-Command 7z -ErrorAction SilentlyContinue).Source
    if (-not $7z) { $7z = Join-Path $env:ProgramFiles '7-Zip\7z.exe' }
    & $7z e $archive "-o$(Join-Path $Dest 'lvp')" 'x64\vulkan_lvp.dll' 'x64\lvp_icd.x86_64.json' -y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "7z exited with $LASTEXITCODE" }
    Write-Host "==> lavapipe from Mesa $MesaVersion"
}
finally {
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}

foreach ($f in $files) { if (-not (Test-Path (Join-Path $Dest $f))) { throw "$f missing in $Dest" } }
Get-ChildItem $Dest -Recurse -File | ForEach-Object { "{0,12:N0}  {1}" -f $_.Length, $_.FullName.Substring($Dest.Length).TrimStart('\') }
