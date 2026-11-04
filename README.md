# SharpNRI

[![NRI surface bound](docs/badges/bound-surface.svg)](docs/coverage.md#bound-surface)
[![test coverage](docs/badges/test-coverage.svg)](docs/coverage.md#test-coverage)

C# bindings over [NRI](https://github.com/NVIDIA-RTX/NRI), the NVIDIA Render Interface: Vulkan and
D3D12 through one low-level API. NRI is a thin, explicit abstraction with no hidden management and
no automatic barriers; SharpNRI exposes it as it is: NRI's objects as disposable handles, NRI's descs
as structs with NRI's field names, NRI's threading contract documented rather than papered over.
An independent project, not affiliated with NVIDIA.
Pure managed P/Invoke over a native library built from a pinned NRI release plus a small patch set,
both in this repository.

## Status

`1.0.0-beta.1`, against NRI `v191` plus twelve patches ([patches/](patches/README.md)), on .NET 11. Beta:
the API may still change between releases. Releases come from master; the `next` branch follows NRI main between tags.
Windows x64, Linux x64, macOS arm64. On macOS the binding is Vulkan through MoltenVK; NRI's Metal
backend is not built, only the Base package exists, the application provides the Vulkan loader and
MoltenVK, and the library needs macOS 15 or newer (.NET 11's floor).

CI builds the native library from the pinned tag and the patch set on all three platforms and runs
the suite with NRI's validation and the backend validation layer on: D3D12 on WARP, Vulkan on
lavapipe and MoltenVK, NONE everywhere. A separate workflow regenerates the interop layer when the
headers, patches or generator change, and fails on drift. Windows is also verified on NVIDIA
hardware.

Linux CI runs on lavapipe (software Vulkan) only. Its two intermittent failures, a ray-query
"miss" (a readback after NRI's 5 s fence-wait timeout) and a test-host crash (a stale Vortice
function table), were test bugs and are fixed in the tests. No hardware Linux driver has run this
code.

## Packages

| Package | Contents | Take it when |
|---|---|---|
| `SharpNRI` | the binding, plus `NRI.dll` / `libNRI.so` / `libNRI.dylib` with **no upscaler SDK**, AMD AGS (Windows), licences. MIT throughout, about 2 MB | always |
| `SharpNRI.Descs` | every enum, desc struct and table layout, plus the managed desc forms; no native code | building layouts and descs without a device, in code that must not load the native library; `SharpNRI` references it, and so can any package of your own |
| `SharpNRI.Testing` | a validated headless device per backend for tests; no test framework dependency | your own GPU tests |
| `SharpNRI.Native.Upscalers` | `NRI.dll` / `libNRI.so` with NIS, FSR and XeSS compiled in, AMD AGS, licences | you want NRI's upscaler extension |

The two NRI payloads are both a file named `NRI.dll`: take exactly one.

D3D12 on the DirectX 12 Agility SDK: take Microsoft's `Microsoft.Direct3D.D3D12` package and copy its
two runtime DLLs into `AgilitySDK/` beside your application, where SharpNRI looks by default
([the guide](docs/guide.md#agility-sdk) has the snippet). No SharpNRI package carries Microsoft's runtime.

```xml
<!-- default -->
<PackageReference Include="SharpNRI" Version="1.0.0-beta.1" />

<!-- with upscalers -->
<PackageReference Include="SharpNRI" Version="1.0.0-beta.1" ExcludeAssets="native" />
<PackageReference Include="SharpNRI.Native.Upscalers" Version="1.0.0-beta.1" />
```

The packages are [release](https://github.com/Nullpoint56/SharpNRI/releases) assets, not on
NuGet.org. From a local feed: put the `.nupkg` files in a folder and add it beside your project:

```xml
<!-- nuget.config -->
<configuration>
  <packageSources>
    <add key="sharpnri" value="path/to/nupkg" />
  </packageSources>
</configuration>
```

The release workflow restores them that way and creates a device on Windows, Linux and macOS
before it publishes (`tools/consume`).

No package contains a vendor runtime (`amd_fidelityfx_*`, `libxess.dll`, `nvngx_*`): the
application ships them beside its executable. DLSS is a build switch, not a package:
`pwsh tools/build-native.ps1 -WithDlss <DLSS SDK path>` stages an NGX-linked `NRI.dll` under
NVIDIA's licence into `runtimes-upscalers-dlss/`. See [the guide](docs/guide.md#packages-and-install).

```csharp
using SharpNRI;
using SharpNRI.Native;

using var device = NriDevice.Create(new NriDeviceOptions
{
    GraphicsApi = GraphicsAPI.D3D12,          // or VK, or NONE
    EnableNriValidation = true,
    EnableGraphicsApiValidation = true,
    MessageHandler = (in NriMessage m) => Console.Error.WriteLine(m),
});
var queue = device.GetQueue(QueueType.GRAPHICS);
```

## Coverage

- `CoreInterface`: all 118 functions (114 plus the three shared-handle getters and `DestroySharedHandle`, NRI main since #294).
- Extensions, 11 of 15 headers in `Include/Extensions`: `DescriptorHeap`, `Imgui`, `LowLatency`,
  `MeshShader`, `RayTracing`, `Streamer`, `SwapChain`, `Upscaler`, `Video`, `WrapperD3D12`,
  `WrapperVK`. Shared buffers, textures and fences (NRI #294) are Core: `shared` on the descs
  and `GetBufferSharedHandle` / `GetTextureSharedHandle` / `GetFenceSharedHandle` / `DestroySharedHandle`. `DeviceCreation` and `Helper` in full, `UploadData`
  included.
- The twelve flat exports (`nriConvert*Format*`, `nriGetFormatProps`, `nriGetGraphicsAPIString`,
  `nriFitPipelineLayoutSettingsIntoDeviceLimits`, `nriReportLiveObjects`, the three annotation
  calls, `nriSetThreadName`) as static helpers on `Nri`.
- Every object exposes NRI's own pointer as `NriHandle`, for `SharpNRI.Native.NriApi` and the raw
  tables.
- Not bound: the D3D11, Metal and WebGPU backends and their wrapper headers. D3D11 is a
  compatibility backend with much of NRI's explicit model emulated, DXBC shaders, no ray tracing,
  mesh shaders or bindless. Metal requires macOS 26 and a Metal 4 GPU and cannot be tested on the
  CI runner. WebGPU is built on a prebuilt `wgpu-native` binary this repository would have to ship.

## Building

```powershell
git submodule update --init
pwsh tools/build-native.ps1                      # NRI + patches -> runtimes/<rid>/native (Base), runtimes-agility/ on Windows
pwsh tools/build-native.ps1 -Variant Upscalers   # optional -> runtimes-upscalers/<rid>/native
dotnet build SharpNRI.slnx -c Release
dotnet test --solution SharpNRI.slnx -c Release --no-build
```

| OS | Needs |
|---|---|
| Windows | Visual Studio with C++, CMake 3.30+, .NET 11 SDK. First Base build downloads about 250 MB; Upscalers about 1.6 GB |
| Linux | CMake 3.30+, clang (GCC 13 also works since NRI 9b45d77), Ninja, `libvulkan1`, X11 and Wayland headers, pwsh 7, .NET 11 SDK. `tools/ci/verify-linux.sh` installs them and runs the whole leg |
| macOS | Xcode command-line tools, CMake, Ninja if present, pwsh 7, .NET 11 SDK. Tests: Homebrew `molten-vk`, `vulkan-loader`, `vulkan-validationlayers`, with `DYLD_FALLBACK_LIBRARY_PATH` and `VK_DRIVER_FILES` set as the `osx-arm64` job in `.github/workflows/ci.yml` sets them |

The native payload is not committed. `pwsh tools/generate-native.ps1` regenerates the interop layer
from the pinned, patched headers (needs `ClangSharpPInvokeGenerator` 21.1.8.4).

## Docs

- [docs/guide.md](docs/guide.md): usage reference, with every caveat next to the API it concerns
- [AGENTS.md](AGENTS.md): working notes for agents and contributors: rules, build, patches, measured oddities
- [docs/upstream.md](docs/upstream.md): report text for each patch
- [docs/coverage.md](docs/coverage.md): bound surface and test coverage at the commit it names, generated by `tools/coverage.ps1`
- [patches/README.md](patches/README.md): the patch set
- [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md): every component, package contents, licence texts

## Licence

MIT ([LICENSE.md](LICENSE.md)). The native payload carries its components' own licences, listed in
`THIRD-PARTY-NOTICES.md`. NRI (MIT) is built with VMA and D3D12MA (MIT), NVAPI and AMD AGS (MIT); the
Upscalers package adds NIS (MIT), FSR (MIT) and XeSS (Intel licence) SDK code. The Agility SDK runtime
is not shipped: applications take Microsoft's package under Microsoft's licence. Not shipped either:
ClangSharp, dxc, xunit, and Microsoft's code coverage extension (`tools/coverage.ps1` only; Microsoft
.NET Library licence).
