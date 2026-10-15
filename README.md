# PrismNRI

C# bindings over [NRI](https://github.com/NVIDIA-RTX/NRI), the NVIDIA Render Interface: Vulkan and
D3D12 through one low-level API. NRI is a thin, explicit abstraction with no hidden management and
no automatic barriers; PrismNRI exposes it as it is: NRI's objects as disposable handles, NRI's descs
as structs with NRI's field names, NRI's threading contract documented rather than papered over.
Pure managed P/Invoke over a native library built from a pinned NRI release plus a small patch set,
both in this repository.

## Status

`0.4.0-alpha`, against NRI `v190` plus eleven patches ([patches/](patches/README.md)), on .NET 11.
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
| `PrismNRI` | the binding, plus `NRI.dll` / `libNRI.so` / `libNRI.dylib` with **no upscaler SDK**, AMD AGS (Windows), licences. MIT throughout, about 2 MB | always |
| `PrismNRI.Descs` | every enum, desc struct and table layout, plus the managed desc forms; no native code | building layouts without a device; referenced by `PrismNRI` |
| `PrismNRI.Testing` | a validated headless device per backend for tests; no test framework dependency | your own GPU tests |
| `PrismNRI.Native.Upscalers` | `NRI.dll` / `libNRI.so` with NIS, FSR and XeSS compiled in, AMD AGS, licences | you want NRI's upscaler extension |
| `PrismNRI.Native.Agility` | `AgilitySDK/D3D12Core.dll`, `d3d12SDKLayers.dll`, Microsoft's licence | D3D12 on the Agility SDK instead of the inbox runtime (Windows) |

The two NRI payloads are both a file named `NRI.dll`: take exactly one.

```xml
<!-- default -->
<PackageReference Include="PrismNRI" Version="0.4.0-alpha" />

<!-- with upscalers and Agility -->
<PackageReference Include="PrismNRI" Version="0.4.0-alpha" ExcludeAssets="native" />
<PackageReference Include="PrismNRI.Native.Upscalers" Version="0.4.0-alpha" />
<PackageReference Include="PrismNRI.Native.Agility" Version="0.4.0-alpha" />
```

The packages are [release](https://github.com/Nullpoint56/PrismNRI/releases) assets, not on
NuGet.org. From a local feed: put the `.nupkg` files in a folder and add it beside your project:

```xml
<!-- nuget.config -->
<configuration>
  <packageSources>
    <add key="prismnri" value="path/to/nupkg" />
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
using PrismNRI;
using PrismNRI.Native;

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

- `CoreInterface`: all 114 functions.
- Extensions, 12 of 16 headers in `Include/Extensions`: `DescriptorHeap`, `Imgui`, `LowLatency`,
  `MeshShader`, `RayTracing`, `SharedHandle` (patch 0002), `Streamer`, `SwapChain`, `Upscaler`,
  `Video`, `WrapperD3D12`, `WrapperVK`. `DeviceCreation` and `Helper` in full, `UploadData`
  included.
- The twelve flat exports (`nriConvert*Format*`, `nriGetFormatProps`, `nriGetGraphicsAPIString`,
  `nriFitPipelineLayoutSettingsIntoDeviceLimits`, `nriReportLiveObjects`, the three annotation
  calls, `nriSetThreadName`) as static helpers on `Nri`.
- Every object exposes NRI's own pointer as `NriHandle`, for `PrismNRI.Native.NriApi` and the raw
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
dotnet build PrismNRI.slnx -c Release
dotnet test --solution PrismNRI.slnx -c Release --no-build
```

| OS | Needs |
|---|---|
| Windows | Visual Studio with C++, CMake 3.30+, .NET 11 SDK. First Base build downloads about 250 MB; Upscalers about 1.6 GB |
| Linux | CMake 3.30+, clang (GCC 13 also works with patch 0005), Ninja, `libvulkan1`, X11 and Wayland headers, pwsh 7, .NET 11 SDK. `tools/ci/verify-linux.sh` installs them and runs the whole leg |
| macOS | Xcode command-line tools, CMake, Ninja if present, pwsh 7, .NET 11 SDK. Tests: Homebrew `molten-vk`, `vulkan-loader`, `vulkan-validationlayers`, with `DYLD_FALLBACK_LIBRARY_PATH` and `VK_DRIVER_FILES` set as the `osx-arm64` job in `.github/workflows/ci.yml` sets them |

The native payload is not committed. `pwsh tools/generate-native.ps1` regenerates the interop layer
from the pinned, patched headers (needs `ClangSharpPInvokeGenerator` 21.1.8.4).

## Docs

- [docs/guide.md](docs/guide.md): usage reference, with every caveat next to the API it concerns
- [CLAUDE.md](CLAUDE.md): working notes for this repository: rules, build, patches, measured oddities
- [docs/upstream.md](docs/upstream.md): report text for each patch
- [patches/README.md](patches/README.md): the patch set
- [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md): every component, package contents, licence texts

## Licence

MIT. The native payload carries its components' own licences, listed in `THIRD-PARTY-NOTICES.md`.
NRI (MIT) is built with VMA and D3D12MA (MIT), NVAPI and AMD AGS (MIT); the Upscalers package adds
NIS (MIT), FSR (MIT) and XeSS (Intel licence) SDK code; the Agility package is Microsoft's runtime
under Microsoft's licence. Not shipped: ClangSharp, dxc, xunit.
