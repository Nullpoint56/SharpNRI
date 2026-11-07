# SharpNRI

[![NRI surface bound](docs/badges/bound-surface.svg)](docs/coverage.md#bound-surface)
[![test coverage](docs/badges/test-coverage.svg)](docs/coverage.md#test-coverage)

C# bindings for [NRI](https://github.com/NVIDIA-RTX/NRI), the NVIDIA Render Interface: one
low-level API over Vulkan and D3D12. NRI is explicit, with no hidden management and no automatic
barriers, and SharpNRI keeps it that way: NRI's objects become disposable classes, its descs are
structs with NRI's field names, and its threading rules are documented rather than hidden.

An independent project, not affiliated with NVIDIA. MIT licensed. **Beta**: the API may still
change between releases.

## Quick start

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

[The guide](docs/guide.md) covers device creation, resources, descriptors, recording and
submitting, every extension, and the caveats next to the API they concern.

## Install

From [NuGet.org](https://www.nuget.org/packages/SharpNRI) (a prerelease while in beta):

```
dotnet add package SharpNRI --prerelease
```

```xml
<PackageReference Include="SharpNRI" Version="1.0.0-beta.3" />
```

Each [release](https://github.com/Nullpoint56/SharpNRI/releases) also carries the same `.nupkg`
files, for a local folder feed.

| Package | What it is |
|---|---|
| `SharpNRI` | the binding and the native NRI library (Windows x64, Linux x64) |
| `SharpNRI.Descs` | every NRI enum and desc struct, with no native code: for code that builds layouts and descs without a device. Versioned by NRI (`191.x` is NRI v191), new only when the descs change |
| `SharpNRI.Testing` | a validated headless device per backend, for your own GPU tests |
| `SharpNRI.Native.Upscalers` | NRI built with its upscalers (NIS, FSR, XeSS), replacing the library in `SharpNRI`: reference it with `ExcludeAssets="native"` on `SharpNRI` |
| `SharpNRI.AgilitySDK` | build targets that copy Microsoft's D3D12 Agility SDK runtime at NRI's version into `AgilitySDK\` and fail the build on another version; your restore takes Microsoft's package |

No package ships a vendor upscaler runtime or a Microsoft file; the guide's
[Packages and install](docs/guide.md#packages-and-install) shows how an application adds them.

## Platforms

| Platform | Backends | Notes |
|---|---|---|
| Windows x64 | D3D12, Vulkan | no Visual C++ runtime needed (static) |
| Linux x64 | Vulkan | needs the Vulkan loader (`libvulkan1`) |
| macOS arm64 | Vulkan through MoltenVK | the native library is a release attachment (`SharpNRI-native-base-osx-arm64-<tag>.zip`) when a macOS build ran, not part of the packages; needs macOS 15+, the Vulkan loader and MoltenVK |

.NET 11. NRI's D3D11, Metal and WebGPU backends are not built.

## What is bound

All of NRI's Core interface, the device creation and helper interfaces, and the DescriptorHeap,
Imgui, LowLatency, MeshShader, RayTracing, Streamer, SwapChain, Upscaler, Video, WrapperD3D12 and
WrapperVK extensions. [docs/coverage.md](docs/coverage.md) lists the bound surface and the test
coverage at the commit it was measured on. [docs/upscalers.md](docs/upscalers.md) has what the
upscaler tests measured on a local payload with the vendor runtimes (NIS, FSR, XeSS, DLSS super
resolution and ray reconstruction on Vulkan and D3D12): render resolutions, accuracy and GPU time
per mode. CI cannot run those, since no package ships the vendor runtimes.

Each release pins one NRI release (currently v191), built from source with a small set of
[patches](patches/README.md): fixes not yet upstream and a few additions.

## Building from source

```powershell
git submodule update --init
pwsh tools/build-native.ps1
dotnet build SharpNRI.slnx -c Release
dotnet test --solution SharpNRI.slnx -c Release --no-build
```

Requirements per platform, the build options and how the repository is organised are in
[AGENTS.md](AGENTS.md).

## Licence

MIT ([LICENSE.md](LICENSE.md)). The native libraries carry the licences of the components built
into them, listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
