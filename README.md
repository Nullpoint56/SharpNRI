# PrismNRI

**C# bindings over [NRI](https://github.com/NVIDIA-RTX/NRI), the NVIDIA Render Interface: Vulkan
and D3D12 through one low-level API.**

NRI is a thin, explicit abstraction over D3D12 and Vulkan with no hidden management and no
automatic barriers. PrismNRI exposes it to .NET as it is: NRI's objects as handles, NRI's descs as
structs with NRI's field names, NRI's threading contract documented rather than papered over. The
native library is built from a pinned NRI release plus a small patch set, both in this repository.

> **This is [PrismEngine](https://github.com/Nullpoint56)'s binding first.** It serves one renderer
> that is itself being rebuilt, and that renderer's needs decide the order of work. It is packaged
> and documented to be reusable, but nobody else has used it yet.

---

## Status

`0.1.0-alpha`, against NRI `v190`, on .NET 11. Windows x64 and Linux x64.

Phase one is done: the generated interop layer, device creation on Vulkan, D3D12 and NRI's NONE
backend, the core object model, and a triangle rendered offscreen and read back on both GPU
backends with NRI's validation and the backend validation layers on. Extensions and the
PrismSlang projection are next. See [docs/findings.md](docs/findings.md) for what was measured
along the way.

## What you get

Three managed packages and two optional native ones.

### `PrismNRI` — the binding

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
using var allocator = queue.CreateCommandAllocator();
using var commandBuffer = allocator.CreateCommandBuffer();
using var fence = device.CreateFence();

using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
{
    type = TextureType.TEXTURE_2D, usage = TextureUsageBits.COLOR_ATTACHMENT,
    format = Format.RGBA8_UNORM, width = 64, height = 64,
});
using var view = texture.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);

commandBuffer.Begin();
commandBuffer.Barrier(new NriTextureBarrier(texture, default,
    new AccessLayoutStage { access = AccessBits.COLOR_ATTACHMENT, layout = Layout.COLOR_ATTACHMENT, stages = StageBits.COLOR_ATTACHMENT }));
commandBuffer.BeginRendering([new NriAttachment(view, clearValue, LoadOp.CLEAR, StoreOp.STORE)]);
// ... set viewport, scissor, pipeline layout, pipeline, draw ...
commandBuffer.EndRendering();
commandBuffer.End();

queue.Submit(commandBuffer, fence, 1);
fence.Wait(1);
```

Every type a shader layout needs is NRI's own: `PipelineLayoutDesc` becomes
`NriPipelineLayoutDesc` with arrays where NRI has pointers, `DescriptorRangeDesc` is used as is.

### `PrismNRI.Descs` — the vocabulary, no native code

Every enum, desc struct and table layout the generator produces, plus the managed desc forms.
Reference this from a tool that builds pipeline layouts without a device in hand; it carries no
native payload.

### `PrismNRI.Testing` — a validated headless device for your tests

A device per backend with NRI validation and the backend validation layer on, every message
recorded, software adapters under CI, and a reason string when a backend is absent. No test
framework dependency. PrismSlang's NRI projection and the engine's renderer tests build on it.

### Native packages — pick one NRI, optionally add Agility

| Package | What is in it | When |
|---|---|---|
| `PrismNRI` (its native assets) | `NRI.dll` / `libNRI.so` with **no upscaler SDK**, AMD AGS, licences. MIT throughout, about 2 MB | the default |
| `PrismNRI.Native.Upscalers` | `NRI.dll` / `libNRI.so` with **NIS, FSR and XeSS** compiled in, AMD AGS, licences | you want NRI's upscaler extension |
| `PrismNRI.Native.Agility` | `AgilitySDK/D3D12Core.dll`, `d3d12SDKLayers.dll`, Microsoft's licence | D3D12 on the Agility SDK rather than the inbox runtime (Windows) |

Both NRI payloads are a file called `NRI.dll`, so **take exactly one**: with the upscalers, exclude
PrismNRI's own native assets.

```xml
<PackageReference Include="PrismNRI" Version="0.1.0-alpha" ExcludeAssets="native" />
<PackageReference Include="PrismNRI.Native.Upscalers" Version="0.1.0-alpha" />
<PackageReference Include="PrismNRI.Native.Agility" Version="0.1.0-alpha" />
```

No package contains a vendor upscaler runtime. FSR needs `amd_fidelityfx_loader_dx12.dll` +
`amd_fidelityfx_upscaler_dx12.dll` (D3D12) or `amd_fidelityfx_vk.dll` (Vulkan), XeSS needs
`libxess.dll`: your application ships them beside its executable, under AMD's and Intel's terms.
`NriUpscalerExtension.IsRuntimeInstalled(type)` tells you whether they are there, and
`CreateUpscaler` throws `NriUnsupportedException` naming the missing file. DLSS is not a package at
all: build NRI yourself with `pwsh tools/build-native.ps1 -WithDlss <path to the DLSS SDK>`, which
puts an NGX-linked `NRI.dll` (NVIDIA's licence, yours to ship) in `runtimes-upscalers-dlss/`. See
[docs/design.md §3.6](docs/design.md) and `THIRD-PARTY-NOTICES.md`.

## Building

```powershell
git submodule update --init
dotnet tool install --global ClangSharpPInvokeGenerator   # only to regenerate dotnet/Native
pwsh tools/build-native.ps1          # NRI + patches → runtimes/<rid>/native (Base) and runtimes-agility/ (CMake, ~250 MB of downloads the first time)
pwsh tools/build-native.ps1 -Variant Upscalers   # optional: → runtimes-upscalers/<rid>/native (~1.6 GB of SDK downloads)
dotnet build PrismNRI.slnx
dotnet test --solution PrismNRI.slnx # NONE always; VK and D3D12 when a device exists; software adapters under CI
```

The native payload is not committed. Windows needs Visual Studio with C++ and CMake 3.30+; Linux
needs CMake, a C++ compiler, the Vulkan loader and X11/Wayland headers. `pwsh tools/generate-native.ps1`
regenerates the interop layer from the pinned, patched headers and is checked by CI for drift.

## What it uses

Ships in `PrismNRI`: NRI (MIT) built with VMA and D3D12MA (MIT), NVAPI and AMD AGS (MIT), compiled
against the Vulkan and Agility headers. `PrismNRI.Native.Upscalers` adds NIS (MIT) and the FSR (MIT)
and XeSS (Intel licence) SDK headers; `PrismNRI.Native.Agility` is the DirectX Agility SDK runtime
(Microsoft licence). See `THIRD-PARTY-NOTICES.md`, which lists each package's contents and includes
every licence text and the conditions that apply. Does not ship: ClangSharp for generation, dxc for the test
shaders, xunit v3 for the tests.

## Docs

- [docs/design.md](docs/design.md) — how it is built and why
- [docs/findings.md](docs/findings.md) — what was measured, in order
- [docs/guide.md](docs/guide.md) — how to use it, and what will surprise you (as the surface settles)
- [patches/README.md](patches/README.md) — the patch set

## Licence

MIT. The native payload carries its components' own licences, listed in `THIRD-PARTY-NOTICES.md`.
