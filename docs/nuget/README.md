# SharpNRI

C# bindings for [NRI](https://github.com/NVIDIA-RTX/NRI), the NVIDIA Render Interface: one
low-level API over Vulkan and D3D12. NRI is explicit, with no hidden management and no automatic
barriers, and SharpNRI keeps it that way: NRI's objects become disposable classes, its descs are
structs with NRI's field names, and its threading rules are documented rather than hidden.

An independent project, not affiliated with NVIDIA. MIT licensed. **Beta**: the API may still
change between releases. Each release pins one NRI release.

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

| Package | What it is |
|---|---|
| `SharpNRI` | the binding and the native NRI library (Windows x64, Linux x64) |
| `SharpNRI.Descs` | every NRI enum and desc struct, with no native code; versioned by NRI (`191.x` is NRI v191) |
| `SharpNRI.Testing` | a validated headless device per backend, for your own GPU tests |
| `SharpNRI.Native.Upscalers` | NRI built with NIS, FSR and XeSS, replacing the library in `SharpNRI`: reference it, nothing else |
| `SharpNRI.AgilitySDK` | build targets that copy Microsoft's D3D12 Agility SDK runtime at NRI's version into `AgilitySDK\` and fail the build on another version; your restore takes Microsoft's package |

No package ships a vendor upscaler runtime or a Microsoft file.
[The guide](https://github.com/Nullpoint56/SharpNRI/blob/master/docs/guide.md) covers how an
application adds them, and everything else: device creation, resources, descriptors, recording and
submitting, every extension, and the caveats.

Windows x64 (D3D12, Vulkan; no Visual C++ runtime needed), Linux x64 (Vulkan; needs the
Vulkan loader). .NET 11.

[Repository](https://github.com/Nullpoint56/SharpNRI) ·
[Releases](https://github.com/Nullpoint56/SharpNRI/releases) ·
[Third-party notices](https://github.com/Nullpoint56/SharpNRI/blob/master/THIRD-PARTY-NOTICES.md)
