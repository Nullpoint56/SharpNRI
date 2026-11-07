# SharpNRI usage reference

Every entry is the real API; every caveat sits next to the API it concerns, with the measured fact.
NRI's own header comments still apply; this covers what the binding adds and what NRI does that the
headers do not say. NRI v191.

- [Packages and install](#packages-and-install)
- [Device creation](#device-creation)
- [Lifetime and ownership](#lifetime-and-ownership)
- [Memory and resources](#memory-and-resources)
- [Descriptors and layouts](#descriptors-and-layouts)
- [Recording and submitting](#recording-and-submitting)
- [Zero allocation](#zero-allocation)
- [Queries](#queries)
- [Extensions](#extensions)
- [Wrappers and the escape hatch](#wrappers-and-the-escape-hatch)
- [Flat exports](#flat-exports)
- [The validated test device](#the-validated-test-device)
- [Threads](#threads)
- [Platform notes](#platform-notes)
- [Traps](#traps)

---

## Packages and install

| Package | Namespace / content | Reference when |
|---|---|---|
| `SharpNRI` | `SharpNRI` (hand-written, `Nri*`), native NRI, Base variant | always |
| `SharpNRI.Descs` | `SharpNRI.Native` (generated, NRI's names) + managed desc forms | layouts without a device; pulled in by `SharpNRI` |
| `SharpNRI.Testing` | `SharpNRI.Testing` | tests |
| `SharpNRI.Native.Upscalers` | NRI with NIS, FSR, XeSS | upscaler extension |
| `SharpNRI.AgilitySDK` | build targets only: the Agility SDK runtime from Microsoft's package | D3D12 on the Agility SDK (below) |

```xml
<PackageReference Include="SharpNRI" Version="1.0.0-beta.3" ExcludeAssets="native" />  <!-- only with Upscalers -->
<PackageReference Include="SharpNRI.Native.Upscalers" Version="1.0.0-beta.3" />
<PackageReference Include="SharpNRI.AgilitySDK" Version="1.0.0-beta.3" />             <!-- Windows, D3D12 -->
```

- On NuGet.org since 1.0.0-beta.2 (prereleases while in beta: `dotnet add package SharpNRI
  --prerelease`). Each GitHub release carries the same `.nupkg` files, usable as a folder feed
  (`nuget.config` beside the project):

  ```xml
  <configuration>
    <packageSources>
      <add key="sharpnri" value="path/to/nupkg" />
    </packageSources>
  </configuration>
  ```

- **Versions.** `SharpNRI`, `SharpNRI.Testing`, `SharpNRI.Native.Upscalers` and `SharpNRI.AgilitySDK`
  carry the release version. `SharpNRI.Descs` has its own, with the NRI version as its major
  (`191.0.0-beta.1` is NRI v191), and moves only when the descs change: a binding-only release keeps
  it, so a library built on `SharpNRI.Descs` alone needs no new release. Pin one major
  (`[191.0.0-beta.1, 192.0.0)`): NRI breaks between versions, and the enum constants are compiled into
  your assembly. The packages depend on each other exactly (`SharpNRI` on one `SharpNRI.Descs`).
- **Version guard.** Device creation compares the NRI version of the loaded native library
  (`DeviceDesc.nriVersion`) and of the loaded `SharpNRI.Descs` with SharpNRI's own and throws
  `NriException` with `INVALID_SDK`, naming both and the library's path, instead of misreading
  renumbered enums (v191 renumbered `StageBits` and `AccessBits`): a stale `NRI.dll` left on the load
  path fails loudly.
- The release workflow's `consume` job restores them that way (`tools/consume`) and creates NONE
  plus a GPU device on windows and ubuntu before the release and the NuGet push.
- Base and Upscalers both contain `NRI.dll` / `libNRI.so` at the same path: take exactly one; with
  Upscalers, `ExcludeAssets="native"` on `SharpNRI`.
- Base: Vulkan, D3D12, NONE, NRI validation, ImGui, NVAPI, AGS, debug names; no upscaler SDK.
  `IsUpscalerSupported` answers nothing there.
- osx-arm64: not in the packages. Each release gets `SharpNRI-native-base-osx-arm64-<tag>.zip`
  (`libNRI.dylib`, Base only; Vulkan and NONE) once a macOS runner has built it; put its files
  where the application loads native libraries from.
- Windows needs no Visual C++ runtime (from 1.0.0-beta.3): `NRI.dll` links the C and C++ runtime
  statically and imports only `KERNEL32` and `USER32` at load time.
- Debugging (from 1.0.0-beta.3): the packages carry PDBs with Source Link, so with Source Link support
  on in the debugger, stepping into SharpNRI shows its source at the package's commit. NRI's native symbols
  (win-x64 `NRI.pdb`) are on each GitHub release as `SharpNRI-symbols-*.zip`: put `NRI.pdb` beside
  `NRI.dll` or on the debugger's symbol path, with native debugging on, to step into NRI. Linux and macOS
  libraries have no symbols.
- No package ships a vendor runtime. Beside your executable, under the vendor's terms:

  | Upscaler | D3D12 | Vulkan |
  |---|---|---|
  | FSR | `amd_fidelityfx_loader_dx12.dll`, `amd_fidelityfx_upscaler_dx12.dll` | `amd_fidelityfx_loader_vk.dll`, `amd_fidelityfx_upscaler_vk.dll` |
  | XeSS | `libxess.dll` | not offered |
  | DLSR / DLRR | `nvngx_dlss*.dll` (DLSS build only) | same |

  `NriUpscalerExtension.GetRuntimeFiles(type, api)` names them for the running build. Which versions:
  `SharpNRI.Native.Upscalers` carries `runtimes/<rid>/upscaler-runtimes.json`, each SDK at NRI's pin and
  each runtime NRI's build fetched with its file version and SHA-256, and each release lists them
  under Bound libraries. Ship those, or a later version the vendor declares compatible.
- DLSS: `pwsh tools/build-native.ps1 -WithDlss <DLSS SDK dir>` builds an NGX-linked `NRI.dll` into
  `runtimes-upscalers-dlss/<rid>/native/`. NGX is linked statically, so that `NRI.dll` is under
  NVIDIA's RTX SDK licence: yours to ship, never packed here.

### Agility SDK

D3D12 runs on the inbox runtime unless an Agility SDK runtime is beside the application. Microsoft's
licence lets an application, not a library, redistribute it, so no SharpNRI package carries it.
`SharpNRI.AgilitySDK` is build targets only: it depends on `Microsoft.Direct3D.D3D12` at exactly the
version NRI was compiled against (1.620.1 for NRI v191), so your restore takes Microsoft's package,
and it copies the runtime into `AgilitySDK/`, the directory SharpNRI uses when `D3D12AgilitySdkPath`
is null:

```xml
<PackageReference Include="SharpNRI.AgilitySDK" Version="1.0.0-beta.3" />
```

- Another `Microsoft.Direct3D.D3D12` version (an explicit reference that overrides the dependency)
  fails the build with `SHARPNRI011`, naming both versions: NRI loads the runtime through the SDK
  version it was compiled against (`NRI_AGILITY_SDK_VERSION_MAJOR` in NRI's `CMakeLists.txt`), and a
  1.619 runtime under NRI v191 fails every D3D12 device creation with `INVALID_SDK` (measured).
- `d3d12SDKLayers.dll` (the debug layer, for `EnableGraphicsApiValidation`) is copied in Debug
  builds; `<SharpNRIAgilityDebugLayer>true</SharpNRIAgilityDebugLayer>` (or `false`) decides
  otherwise. Windows RIDs and portable builds get the files; `win-arm64` takes `bin/arm64`. Publish
  copies them too, outside a single-file bundle.
- With the package referenced, a missing `AgilitySDK/D3D12Core.dll` at run time is an error
  (`NriException`, `INVALID_SDK`, from the runtime switch `SharpNRI.RequireAgilitySdk` the package
  sets) instead of a silent fall back to the inbox runtime; `D3D12AgilitySdkPath = ""` still asks for
  the inbox runtime on purpose.
- Shipping the runtime makes its Microsoft Software License Terms yours (`THIRD-PARTY-NOTICES.md`).
- The release's `consume` job builds with this package and fails unless NRI reports
  `Using Agility SDK v1.620`, and checks that another version fails the build.
## Device creation

```csharp
foreach (var a in NriAdapter.Enumerate())
    Console.WriteLine(a);                       // name, vendor, architecture, supported backends

using var device = NriDevice.Create(new NriDeviceOptions
{
    GraphicsApi = GraphicsAPI.VK,               // VK, D3D12, NONE
    Adapter = NriAdapter.Enumerate(GraphicsAPI.VK).First(a => a.Supports(GraphicsAPI.VK)),
    QueueFamilies = [new NriQueueFamily(QueueType.GRAPHICS), new NriQueueFamily(QueueType.COMPUTE)],
    EnableNriValidation = true,
    EnableGraphicsApiValidation = true,         // VVL / D3D12 debug layer
    MessageHandler = (in NriMessage m) => Console.Error.WriteLine(m),
});
ref readonly DeviceDesc caps = ref device.Desc;
```

| Option | Note |
|---|---|
| `NriDeviceOptions` | mirrors `DeviceCreationDesc` field for field |
| `QueueFamilies` | empty means one graphics queue. Request every family up front: NRI trims the device's queue counts to what was created, so `Video` is `UNSUPPORTED` unless `VIDEO_DECODE` / `VIDEO_ENCODE` were requested |
| `MessageHandler` | receives NRI and validation-layer messages. A throwing handler loses that one message. An `NriException` (and `NriDeviceLostException`, `NriUnsupportedException`) carries the last error text reported on its own thread since the binding last checked a result there: the failing call's own, or that of a refused `Cmd*` before it (those return nothing, so nothing checks them); never another thread's, and none when nothing was reported; `device.LastError` holds the device's last error text, from any thread. A Khronos layer message arrives as `[id] name: text` (patch 0015 adds the name, e.g. `BestPractices-vkBindImageMemory-small-dedicated-allocation`; upstream forwards the number and the text, and current layers no longer embed the name in the text) |
| `BreakOnError` | breaks only when a debugger is attached; otherwise errors never terminate the process. Also decides the D3D12 debug layer's own break (`SetBreakOnSeverity` for ERROR and CORRUPTION), evaluated at device creation: on only with `BreakOnError` and a debugger attached then (patch 0018). Otherwise a D3D12 debug-layer error arrives as an error message, `[id] text` |
| `EnableD3D12GpuBasedValidation` | D3D12 GPU-based validation (patch 0019): checks descriptors and resource states while shaders run. Slow; needs `EnableGraphicsApiValidation`. Process-global and never switched off: every D3D12 device created later in the process has it |
| `Allocator` | `INriAllocator` for NRI's own host allocations; `NriCountingAllocator` counts them. It must not fail: a null (a throwing allocator returns one) fails an NRI object's creation with `OUT_OF_MEMORY` (NRI main 9d276c3; patch 0034 on master), but NRI's containers allocate through it too and crash on one, device creation's first allocation among them |
| `ForwardAllocatorToThirdParty` | also routes VMA / D3D12MA host allocations; off by default (chatty) |
| `D3D12AgilitySdkPath` | null: the `AgilitySDK` directory beside the assembly when present (`NriDevice.DefaultAgilitySdkPath`), else inbox. `""`: force inbox |
| `VkBindingOffsets` | see [Descriptors and layouts](#descriptors-and-layouts) |
| `VkInstanceCreateInfoNext` | Vulkan: a pointer to your own `pNext` chain, which NRI appends to `VkInstanceCreateInfo` after its `VkDebugUtilsMessengerCreateInfoEXT` and (with `EnableGraphicsApiValidation`) `VkValidationFeaturesEXT`; do not repeat those two (patch 0038). The use is `VkLayerSettingsCreateInfoEXT`: validation-layer settings (`validate_best_practices`, `validate_sync`, ...) for this device's instance only, where `VK_KHRONOS_VALIDATION_*` environment variables apply to the whole process and, set from .NET at run time, are not seen by native code on Linux or macOS. Your memory; it must stay valid until `Create` returns. Not on `CreateFromVulkan` (the instance is yours) |
| `Robustness`, `DeviceLostInfoLevel`, `EnableD3D12RayTracingValidation`, `EnableD3D12GpuBasedValidation` | only on `NriDevice.Create`, not on the wrapped routes (a wrapped D3D12 device has the debug layer its creator gave it, and NRI sets no break on it) |

Caveats:

- **Agility from a managed host.** NRI's documented route (`D3D12SDKVersion` exported from the
  executable) never works under `dotnet`, `testhost` or an apphost; without
  `D3D12AgilitySdkPath` the inbox runtime runs and NRI warns
  `CheckFeatureSupport(options22) failed, result = 0x80070057`. Patch 0001 loads the SDK through
  `ID3D12SDKConfiguration1::CreateDeviceFactory` + `ApplyToGlobalState`; success logs
  `Using Agility SDK v1.620 from '...'`.
- **Agility is process-wide.** Once applied, the inbox runtime is not reachable again in that
  process.
- **Agility without the patch:** create the `ID3D12Device` from your own device factory and pass it
  to `NriDevice.CreateFromD3D12`. Works (NRI reports `ID3D12Device15`), but loses `Robustness`,
  `DeviceLostInfoLevel`, D3D12 ray tracing validation and debug-layer message filtering.
- **Developer Mode** is not required by Microsoft's documentation for either route; the Windows CI
  runner (Developer Mode off) runs the Agility tests.
- **D3D12 debug-layer errors are messages, not breaks.** Upstream NRI sets the info queue's break
  on ERROR and CORRUPTION for every debug-layer device; without a debugger the first error is a
  `DebugBreak` that surfaces as an `SEHException` out of the call that caused it (measured: a
  512-byte `CopyBuffer` into a 256-byte buffer) or kills a headless process. With patch 0018 the
  binding asks for the break only with `BreakOnError` and a debugger attached, so the same copy
  returns and reports `[841] ID3D12CommandList::CopyBufferRegion: The destination region extends
  past the end of the destination resource...` as an error message. A command list with such an
  error fails `End` (D3D12 `Close` returns `E_INVALIDARG`).
- **GPU-based validation** (`EnableD3D12GpuBasedValidation`) finds what the CPU-side layer cannot,
  for example a shader writing through a never-written descriptor in a `PARTIALLY_BOUND` range:
  `[938] GPU-BASED VALIDATION: Dispatch, Uninitialized descriptor accessed: ...`, reported after
  the work executes. On a range without `PARTIALLY_BOUND` the CPU-side layer already reports a
  never-written descriptor at `SetDescriptorSet` ([646]) and at the dispatch ([1023]).
- **Software adapters are enumerated**: WARP and lavapipe have `Architecture.SOFTWARE`. Filter
  them out when picking a GPU.
- **Enumerate once, for the backend you use, and pass the adapter.** An unfiltered
  `NriAdapter.Enumerate()` creates and destroys a Vulkan instance (API 1.2, every implicit layer
  loaded and unloaded), creates a DXGI factory, runs a null-device `D3D12CreateDevice` probe per
  adapter and creates real inbox D3D12 devices for adapters Vulkan did not list, which loads
  `D3D12Core.dll` for the rest of the process. `NriAdapter.Enumerate(GraphicsAPI.VK)` (NRI main's
  `nriEnumerateAdapters` with a backend mask, 962615b; patch 0024 on master) runs only the Vulkan pass: measured on NVIDIA 5 ms against 69 ms warm,
  and no D3D12 runtime in the process (`NRI.dll` delay-loads `d3d12.dll` and `dxgi.dll`, so a
  Vulkan-only process loads neither through NRI; the Windows Vulkan loader loads `dxgi.dll` itself).
  A filtered adapter carries only the asked API's bits in
  `SupportedGraphicsApis` (plus `NONE`), so pass it only to that API's device. Each call is one
  native call with a 32-entry buffer. `NriDevice.Create` with `Adapter` null lets NRI enumerate
  once more: for Vulkan the Vulkan pass only (same adapter as the first of
  `Enumerate(GraphicsAPI.VK)`), for D3D12 the full enumeration, whose desc for an adapter Vulkan
  also lists carries Vulkan's queue counts (NVIDIA: 16, 8, 2; a D3D12-only enumeration says 4, 4,
  4). Keep the `NriAdapter` and pass it, and the device's own Vulkan instance is the process's
  second, not its third.
- **RivaTuner Statistics Server's implicit layer** (`VK_LAYER_RTSS`) maps each physical device to
  its instance from `vkEnumeratePhysicalDevices` and uses that map at the first present. NRI v191
  finds physical devices only through `vkEnumeratePhysicalDeviceGroups`, so on NRI's device the
  loader logged `vkEnumeratePhysicalDevices: Invalid instance` and the process failed fast at the
  first present (exit 0xC0000409), with or without validation (RTSS 7.3.5). The shipped `NRI.dll`
  carries patch 0045, which enumerates physical devices instead, so a presenting Vulkan process
  survives with the layer on and the binding makes no call of its own. A device from
  `CreateFromVulkan` is on your instance: enumerate physical devices on it with
  `vkEnumeratePhysicalDevices` yourself. The number of instances is not the cause (measured with
  the device's instance the only one the layer saw).
- **Adapter uid** (`NriAdapter.Uid`): LUID when `high` is 0, UUID otherwise. On Linux
  `deviceLUIDValid` is false, so NRI builds it from `VkPhysicalDeviceIDProperties.deviceUUID`;
  match on the UUID there.
- **`GraphicsAPI.NONE`** answers `SUCCESS` to everything, validates nothing (NRI never wraps it in
  its validation layer, even when asked), `{ size = 1 }` for every buffer, one static desc for every
  texture, `Map` null, 0 allocations from `AllocateAndBindMemory`, zero shared handles. Use it for
  lifetime and allocation tests on a machine with no GPU; nothing it answers is a measurement.
- **`GetQueue` with a bad index**: `INVALID_ARGUMENT` on Vulkan and D3D12, no message first.
- **`NriVersion`** reports `NRI_VERSION` as built (190).

## Lifetime and ownership

- Every object is an `NriObject`: one create call, `Dispose` calls the one `Destroy*`. No
  finalizers anywhere.
- `NriDevice.Dispose` throws while any child is alive; the device stays usable.
  `device.LiveObjects` is the count.
- A forgotten `Dispose` leaks until process exit; it never corrupts.
- Disposal during process shutdown is skipped (the native library may be unloaded).
- Not disposable: `NriQueue` (the device owns it), `NriDescriptorSet` (a struct, not an object:
  the handle plus its pool; the pool owns the set, and `pool.Reset()` or disposing the pool
  invalidates every set).
- Dispose resources before the memory they are bound to. `NriMemory.Dispose` throws
  `InvalidOperationException` naming the count, before NRI is called, while a buffer, texture,
  acceleration structure or micromap bound into it (`Bind*Memory`) or placed in it
  (`CreatePlaced*`) is alive; the memory stays usable. Freeing it first would leak it (NRI's
  validation refuses) and, on D3D12, make every later device creation in the process fail. NRI
  does not say which resource `AllocateAndBindMemory` put into which allocation, so each allocation
  it returns refuses while any resource of that call is alive: dispose every resource of the call,
  then its allocations.
- `SetDebugName(string)` on every object, and on `NriQueue`.

```csharp
using var device = NriDevice.Create(...);
using var fence = device.CreateFence();   // `using` order disposes it first
```

## Memory and resources

```csharp
// Committed (VMA / D3D12MA)
using var vb = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD,
    new BufferDesc { size = 1024, usage = BufferUsageBits.VERTEX });
Span<byte> mapped = vb.Map();  vb.Unmap();

// Create, measure, allocate, bind. `using` disposes in reverse order and the memory must go last,
// so the texture's `using` comes after the memory's.
var unbound = device.CreateTexture(new TextureDesc
{
    type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE,
    format = Format.RGBA8_UNORM, width = 256, height = 256,
});
var req = unbound.GetMemoryDesc(MemoryLocation.DEVICE);
using var mem = device.AllocateMemory(new AllocateMemoryDesc { size = req.size, type = req.type });
using var tex = unbound;
device.BindTextureMemory(tex, mem);

// Group unbound resources into few allocations; dispose tex2 and buf2 before any of these
NriMemory[] allocations = device.AllocateAndBindMemory(MemoryLocation.DEVICE, [tex2], [buf2]);

// Placed
using var placed = device.CreatePlacedBuffer(mem, offset: 0, new BufferDesc { size = 256, usage = BufferUsageBits.SHADER_RESOURCE });
```

| Fact | Measured |
|---|---|
| `TextureDesc` zeros for height, depth, mipNum, layerNum, sampleNum | become 1 on VK and D3D12 |
| `BufferDesc` | returned exactly as given |
| 1000-byte buffer requirement | VK 1,008 B at 16; D3D12 inbox 65,536 at 65,536; D3D12 Agility 1,024 at 128 |
| upload row / slice alignment | VK 1 / 16; D3D12 1 / 1 |

Initial contents: `NriQueue.UploadData` stages, submits on that queue, waits, and leaves each
resource in its `After` state.

```csharp
queue.UploadData(
    [new NriTextureUpload(tex,
        [new NriTextureSubresourceUpload(pixels, SliceNum: 1, RowPitch: 256 * 4, SlicePitch: 256 * 256 * 4)],
        new AccessLayoutStage { access = AccessBits.SHADER_RESOURCE, layout = Layout.SHADER_RESOURCE, stages = StageBits.FRAGMENT_SHADER })],
    [new NriBufferUpload(vb, vertexBytes, new AccessStage { access = AccessBits.VERTEX_BUFFER, stages = StageBits.VERTEX_SHADER })]);
```

- Subresources: all of them, layer-major (`layer * mipNum + mip`). Rows as tight as you have them;
  NRI realigns.
- Data shorter than NRI will read throws `ArgumentException` (NRI would read past it).
- **NV12 / two-plane formats are refused**: Vulkan copies both planes in one region
  (VUID-VkCopyBufferToImageInfo2-dstImage-07981), D3D12 refuses the second plane's barrier (debug
  error 1417). Upload per plane with `CommandBuffer.UploadBufferToTexture`.
- **No host barrier on buffers.** NRI's validation rejects `HOST_READ` / `HOST_WRITE` in any buffer
  barrier ("not supported by the usage mask of the buffer"). For a `HOST_READBACK` buffer the fence
  wait is the host visibility. Host access belongs to textures created with `HOST_TRANSFER`.
- **`HOST_TRANSFER` textures on the Apple Paravirtual device** (GitHub macOS VMs): `vkBindImageMemory`
  segfaults inside the Metal driver (`EXC_BAD_ACCESS address=0xc`), no Vulkan error first, although
  the device advertises `hostImageCopy`. Avoid there; physical Apple GPUs not measured.
- Views are descriptors: `texture.CreateView(TextureView.COLOR_ATTACHMENT, format)`,
  `buffer.CreateView(BufferView.STORAGE_STRUCTURED_BUFFER, structureStride: 16)`,
  `device.CreateSampler(in samplerDesc)`.
- **Sampler `mipBias`**: keep within `device.Desc.other.samplerLodBiasMax`. On MoltenVK it is 0
  (NRI #303, v191): MoltenVK reports `maxSamplerLodBias = 15.999` but forbids any bias
  (`samplerMipLodBias = false`, VUID-VkSamplerCreateInfo-samplerMipLodBias-04467).
- `device.CreatePipelineCache(bytes)`: a cache rebuilt from its own bytes reports the same size.
  Stale or foreign bytes throw `NriException` with `Result.OUT_OF_DATE` and create nothing (no
  leak, measured); catch it and create an empty cache.

- An `EXCLUSIVE` texture (`TextureDesc.sharingMode`) is created with no queue family indices
  (patch 0013). Upstream passes every active family's index, one per family `GetQueue` has
  handed out, whatever the sharing mode; Vulkan ignores them for `EXCLUSIVE`, and the Khronos
  best-practices checks report `BestPractices-vkCreateImage-sharing-mode-exclusive` for every
  such image on a device with more than one active family (measured with graphics, compute and
  copy queues on NVIDIA). Buffers have no sharing mode: they are `CONCURRENT` with more than one
  active family.

## Descriptors and layouts

```csharp
using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
{
    RootConstants = [new RootConstantDesc { registerIndex = 0, size = 16, shaderStages = StageBits.COMPUTE_SHADER }],
    DescriptorSets =
    [
        new NriDescriptorSetDesc(0,
        [
            new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 1, descriptorType = DescriptorType.CONSTANT_BUFFER, shaderStages = StageBits.COMPUTE_SHADER },
            new DescriptorRangeDesc { baseRegisterIndex = 1, descriptorNum = 1, descriptorType = DescriptorType.STORAGE_STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER },
        ]),
    ],
    ShaderStages = StageBits.COMPUTE_SHADER,
});

using var pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, constantBufferMaxNum = 1, storageStructuredBufferMaxNum = 1 });
var set = pool.AllocateDescriptorSet(layout, setIndex: 0);
set.Update(rangeIndex: 0, descriptorIndex: 0, constantsView);
set.Update(rangeIndex: 1, descriptorIndex: 0, outputView);
```

- Managed forms exist only where NRI holds pointer + count: `NriPipelineLayoutDesc`,
  `NriDescriptorSetDesc`, `NriVertexInputDesc`, `NriOutputMergerDesc`, `NriGraphicsPipelineDesc`,
  `NriShaderDesc`. Same field names; arrays are pinned for the call, NRI copies what it keeps.
  Everything else is NRI's struct as generated.
- The layout descs live in `SharpNRI.Descs`; build them without a device or the native library.

| Backend | `baseRegisterIndex` | `registerSpace` |
|---|---|---|
| D3D12 | HLSL register per class (`b`, `t`, `u`, `s`); from DXIL reflection | HLSL space |
| Vulkan, zero `VkBindingOffsets` (default) | SPIR-V binding | descriptor set |
| Vulkan, non-zero offsets | binding minus the per-class shift; compile shaders with the same shifts (`-fvk-*-shift`) | descriptor set |

- `VkBindingOffsets` must be identical in `NriDeviceOptions` and the shader compiler.
- `NriDescriptorSet` is a `readonly struct`: handle plus pool, equal by handle, nothing to
  dispose. `default` is not a set (NRI validation rejects the null handle).
- Per frame: `pool.Reset()`, then `pool.AllocateDescriptorSets(layout, setIndex, span)` into
  storage you own (a field array reused every frame) or `pool.AllocateDescriptorSet(...)`; neither
  allocates. The array-returning `AllocateDescriptorSets(layout, setIndex, count)` allocates the
  array.
- `pool.Reset()` invalidates every set. D3D12 heap offsets for three consecutive sets: (0,0),
  (3,1), (6,2); back to (0,0) after `Reset`. Vulkan: (0,0) always.
- `Nri.FitPipelineLayoutSettingsIntoDeviceLimits(device.Desc, wanted)` clamps counts and sizes in
  NRI's priority order.
- `ShaderStages = StageBits.ALL` on a layout means every shader stage. `ALL` is 0 in NRI (the
  barrier default); since NRI #302 (v191) every backend and NRI validation resolve `ALL` and
  `ALL_SHADERS` to the layout's stages. Before, validation masked each range's stages with 0 and refused a range with explicit
  stages ("not compatible with 'shaderStages'"), and any graphics pipeline on an all-`ALL` layout
  ("not enabled in the pipeline layout").
- **Root descriptor offsets mean different things per backend.** `SetRootDescriptor(index, view,
  offset)` on Vulkan slides the view's window: the offset is added to the view's own offset and the
  view's range is kept. D3D12 binds the view's address plus the offset with no range. NRI
  validation caps the offset at buffer size minus view offset minus view size (a `WHOLE_SIZE` view
  counts as the rest of the buffer), so a whole-buffer view takes offset 0 only. For a sliding
  window over a large buffer, create the view as large as the window. Not patched: making the
  backends agree is a semantic choice for upstream.

## Recording and submitting

```csharp
var queue = device.GetQueue(QueueType.GRAPHICS);
using var allocator = queue.CreateCommandAllocator();
using var cmd = allocator.CreateCommandBuffer();
using var fence = device.CreateFence();

cmd.Begin(pool);
cmd.Barrier(new NriTextureBarrier(target, default,
    new AccessLayoutStage { access = AccessBits.COLOR_ATTACHMENT, layout = Layout.COLOR_ATTACHMENT, stages = StageBits.COLOR_ATTACHMENT }));
cmd.BeginRendering([new NriAttachment(targetView, clearValue, LoadOp.CLEAR, StoreOp.STORE)]);
cmd.SetViewport(new Viewport { width = 64, height = 64, depthMax = 1 });
cmd.SetScissor(new Rect { width = 64, height = 64 });
cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
cmd.SetPipeline(pipeline);
cmd.Draw(3);
cmd.EndRendering();
cmd.End();

queue.Submit(cmd, fence, signalValue: 1);
fence.Wait(1);
```

- One method per `Cmd*`, prefix dropped, NRI's argument order.
- Arrays naming NRI objects are spans of records: `NriBufferBarrier`, `NriTextureBarrier`,
  `NriAttachment`, `NriVertexBuffer`, `NriFenceSubmit`. Native arrays are built on the stack;
  a whole frame allocates nothing ([Zero allocation](#zero-allocation)).
- Barriers are never inserted for you. `AccessStage` / `AccessLayoutStage` are NRI's before/after.
- **A texture barrier side with access `NONE` and a defined layout, on D3D12** (NRI 48a14c3):
  with stages it is recorded with the conservative `COMMON` access and the stages' sync, so the
  dependency on those stages holds and the barrier may flush caches; with stages `NONE` it is
  layout-only (`SYNC_NONE`, `NO_ACCESS`). A layout-only side says "nothing touched this subresource
  earlier in this submit": after an access in the same command list (an upload, say) the debug
  layer refuses it with 1417 ("has been accessed before, a barrier that specifies
  D3D12_BARRIER_SYNC_NONE as SyncBefore is invalid"). On Vulkan either side is an execution
  dependency on its stages with no access, e.g. after a semaphore wait. Name stages whenever the
  order matters; use stages `NONE` only for the first barrier of a submit on a resource another
  submit finished with.
- **`ZeroBuffer` is stage `COPY`, access `COPY_DESTINATION`, on both backends** (NRI 1163658). It
  is `CopyBufferRegion` from a zero buffer on D3D12 and `vkCmdFillBuffer` on Vulkan, a clear
  command; NRI's Vulkan `COPY` stage includes `VK_PIPELINE_STAGE_2_CLEAR_BIT`.
  `CLEAR_STORAGE` is wrong for it on D3D12 (`CLEAR_UNORDERED_ACCESS_VIEW`).
- **Copy queues: use `COPY_DESTINATION` / `COPY_SOURCE` on every queue, and name the hand-off.**
  Vulkan's copies (`CopyTexture`, `ResolveTexture`, `UploadBufferToTexture`,
  `ReadbackTextureToBuffer`) require the transfer layouts, so a texture in `GENERAL` cannot be
  their destination there. D3D12 copy command lists allow only `D3D12_BARRIER_LAYOUT_COMMON`; NRI
  (732f737) records `COPY_SOURCE` / `COPY_DESTINATION` as `COMMON` in a copy list and on the
  copy-queue side of a texture barrier whose `SrcQueue` / `DstQueue` is a copy queue. Across
  queues: on the COPY queue `{UNDEFINED}` -> `{COPY_DESTINATION}`, the copy, then a release
  `{COPY_DESTINATION}` -> `{COPY_DESTINATION}` with `SrcQueue: copy, DstQueue: graphics`; on the
  other queue, after the fence, the same barrier as the acquire, then any transition. Without the
  queues on the other queue's barrier D3D12 reports a layout mismatch at execution (1334); keep the
  layout through the hand-off, because a Vulkan release and acquire of an `EXCLUSIVE` image must
  name the same transition and a D3D12 copy list cannot transition. Mind the Vulkan sharing mode
  (fetch the COPY queue before creating the resources it touches, [Threads](#threads)).
- **`SrcQueue` / `DstQueue` on Vulkan are an ownership transfer only for `EXCLUSIVE` textures**
  (or with `NriQueue.External` on one side). On a `CONCURRENT` texture (the default, with more
  than one queue family in use) NRI writes `VK_QUEUE_FAMILY_IGNORED` for every side that is not
  external (NRI 2603edc), so the release and the acquire of a hand-off are two ordinary barriers,
  each validated against its own queue: the COPY queue's barrier may name only copy stages. On NRI
  v190 the Khronos layer treated them as a transfer: a release and acquire naming different
  transitions drew VUID-vkCmdDraw-None-09600, and best practices flagged a sampled-only texture's
  hand-off as `BestPractices-PipelineBarrier-unneeded-QFOT`. The portable hand-off above is right
  for both modes; on D3D12 the queues are only the copy-queue hint.
- **Attachment resolves have their own stage and accesses** (NRI 48d4a33). With
  `NriAttachment.ResolveDst` set, add `RESOLVE_ATTACHMENT_SOURCE` access and the
  `RESOLVE_ATTACHMENT` stage to the multisampled attachment's barriers, and prepare the resolve
  destination with `RESOLVE_ATTACHMENT_DESTINATION` and `RESOLVE_ATTACHMENT` (layout
  `COLOR_ATTACHMENT`; not `GENERAL` or `INPUT_ATTACHMENT`, and not on `SIMULTANEOUS` textures).
  `GRAPHICS` and `ALL_ATTACHMENT` no longer include resolves; `ALL_RESOLVE` does. Measured on
  NVIDIA: a 4x RGBA8 triangle resolved this way validates clean on both backends, and so do the
  older colour-attachment barriers, with the same pixels.
- **One recording command buffer per allocator at a time.** D3D12 `Begin` resets the command
  list against its allocator; a second `Begin` while another command buffer of the same
  allocator records draws debug error 540 (`COMMAND_ALLOCATOR_CONTENTION`) and throws
  `NriException` (`INVALID_ARGUMENT`). Vulkan and NRI validation say nothing. One allocator per
  command buffer in flight is the simplest shape.
- **Vertex stride comes from `SetVertexBuffers`**, not the pipeline, on D3D12 and on Vulkan with
  `features.extendedDynamicState` (every Vulkan 1.3+ device; NRI binds with
  `vkCmdBindVertexBuffers2`). `VertexStreamDesc.stride` is the fallback for a Vulkan device
  without the feature only. A stride of 0 reads every vertex from the offset, with no message
  from any layer (it is legal), so `NriVertexBuffer.Stride` and `SetVertexBuffer`'s stride have no
  default.
- Fences are timelines on both backends: `fence.Wait(value)`, `fence.Value`, and in a submit
  `NriFenceSubmit(fence, value, stages)`.
- **`fence.Wait` throws `TimeoutException` after 5 s** (NRI's `NRI_TIMEOUT_FENCE`, Vulkan and
  D3D12): NRI reports an ERROR message ("WaitSemaphores() timed out!" on Vulkan) and returns with
  the fence still short of the value; the binding checks `fence.Value` and throws. For work that
  may take longer (a first submit on lavapipe JIT-compiles its shaders), poll `fence.Value`.
- Queue ownership transfer to or from another API / process: `SrcQueue` / `DstQueue` =
  `NriQueue.External` (NRI's `EXTERNAL_QUEUE`; `VK_QUEUE_FAMILY_EXTERNAL` on Vulkan) on one side and
  the local queue executing the barrier on the other, on `NriTextureBarrier` and on
  `NriBufferBarrier`. Buffers need no transfer between the device's own queues, only to or from an
  external user; D3D12 ignores both. Release a shared
  buffer to `External` after writing it and before the other side takes it, acquire from `External`
  before reading it back (the CUDA round trip in the tests does exactly that).
- UNORM rounding is the driver's: `0.1 * 255 = 25.5` reads 25 on NVIDIA Vulkan, 26 elsewhere. Allow
  one unit when comparing cleared or rendered pixels. VK and D3D12 readbacks differ at triangle
  edges.

## Zero allocation

The contract: every call of the binding allocates nothing per frame, managed or through the host
allocator, except creation and destruction and the setup calls named below. Measured on NONE (which
does nothing per call, so any allocation is the binding's) after one warm-up frame, over 300
frames: 0 managed bytes (`AllocationTests`, `ExtensionAllocationTests`), and for the core frames 0
host allocations, reallocations and frees through `NriCountingAllocator`; `Map` / `Unmap` on each
GPU backend (NONE maps nothing). The measurement is transitive: whatever a call reaches inside the
binding is counted.

- Covered, default-deny: `AllocationCoverageTests` holds every public method and computed property
  of every public type to those frames. Every command-buffer command and overload, submission,
  queue, fence, query-pool, descriptor-pool and descriptor-set call, the device's per-frame reads
  (`GetQueue`, `QueryVideoMemoryInfo`, `GetFormatSupport`, the extension properties), the static
  `Nri` helpers (format conversions, `GetFormatProps`, thread annotations), and every extension's
  per-frame calls (streamer, upscaler, ImGui, video decode and encode with their feedback and
  bitstream writers, ray tracing builds, copies, size queries and dispatches, low latency,
  acquire, present and `WaitForPresent`) are measured; a call added to the binding fails that test
  until it is.
- Annotations (`cmd.BeginAnnotation`, `queue.Annotation`, `Nri.Annotate`, ...) encode their name on
  the stack up to 255 UTF-8 bytes; a longer name goes through native memory, still no managed
  allocation.
- Allocate by design, named in `AllocationCoverageTests` with the reason: object creation
  (`Create*`, `AllocateMemory`, `AllocateAndBindMemory`) and binding memory, memory requirements,
  capability queries, exports (`GetSharedHandle`), debug names, adapter enumeration, the desc forms
  with arrays (`NriPipelineLayoutDesc`, `NriGraphicsPipelineDesc`, ...), `queue.UploadData`,
  `Nri.GetFormatName` and `Nri.GetGraphicsAPIString` (new strings; `GetFormatProps` is the per-frame
  form), `NriPipelineCache.GetData`, and the array-returning
  `AllocateDescriptorSets(layout, setIndex, count)`.
- `NriDescriptorSet` is a struct so that resetting the pool and reallocating sets every frame
  allocates nothing; the span overload is the per-frame path.
- Build the spans a frame passes (barriers, attachments, submits, writes) once, outside the frame.
- A message the callback reports carries its text as a new string; a clean frame reports none.
- Exceptions allocate only when thrown.
## Queries

```csharp
using var pool = device.CreateQueryPool(QueryType.TIMESTAMP, capacity: 2);
cmd.ResetQueries(pool, 0, 2);
cmd.EndQuery(pool, 0);  /* work */  cmd.EndQuery(pool, 1);
cmd.CopyQueries(pool, 0, 2, readback, dstOffset: 0);
```

- `pool.QuerySize`: timestamp result is 8 bytes on both backends. Timestamp frequency
  (`device.Desc.other.timestampFrequencyHz`) measured 1 GHz on both.
- `queue.GetCalibratedTimestamps()` returns `(Gpu, Cpu)`.
- **MoltenVK without GPU counter sampling** (Apple Paravirtual device): timestamps are written on the
  host when the command buffer completes. A `CopyQueries` in the same submission reads 0 and 0;
  copy in a later submission (that read two equal non-zero values). The device still reports
  `features.timestamp`, `timestampValidBits = 64`. Applies to imported (wrapped) query pools too.

## Extensions

Acquired once per device on first use. `device.TryX` is null when the backend lacks it;
`device.X` throws `NriUnsupportedException`.

| Property | NONE | Vulkan | D3D12 |
|---|---|---|---|
| `RayTracing`, `MeshShader`, `DescriptorHeap`, `Streamer`, `SwapChain`, `LowLatency`, `Upscaler`, `Imgui`, `Video`, `SharedHandles` (master only; `next`: Core) | yes | per device | per device |
| `WrapperVK` | no | yes | no |
| `WrapperD3D12` | no | no | yes |

"Per device" = the device's features. MoltenVK has no ray tracing, mesh shaders, video, descriptor
heaps or shading-rate attachments.

### RayTracing

```csharp
if (device.TryRayTracing is { } rt)
{
    using var blas = rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, new NriAccelerationStructureDesc
    {
        Type = AccelerationStructureType.BOTTOM_LEVEL,
        Geometries = [NriBottomLevelGeometry.FromTriangles(new NriBottomLevelTriangles(NriBuffer.HasBuffer, 0, 3, 12, Format.RGB32_SFLOAT))],
    });
    rt.BuildBottomLevelAccelerationStructure(cmd, new NriBuildBottomLevelAccelerationStructure(blas, [geometry], scratch));
}
```

- `NriBuffer.HasBuffer` (NRI's `HAS_BUFFER`) sizes a structure before its inputs exist. NRI v190's
  validation layer dereferenced it (access violation); fixed in NRI 7915eca, which also refuses it
  at build time: pass the real buffers to `BuildBottomLevelAccelerationStructure`.
- Unbound size equals `GetAccelerationStructureMemoryDesc`'s prediction. Measured one-triangle BLAS:
  VK 3,072 B at 256, D3D12 2,944 B at 128, WARP 65,536 B.
- Update scratch: equal to build scratch on NVIDIA (1,664 B); WARP reports build 120 B, update 0 B.
- Compaction: VK 2,944 of 3,072 B, D3D12 2,944 of 2,944 B. Placed, updated, `CLONE` and `COMPACT`
  copies trace the same.
- Geometry and build descs are marshalled on the stack per call: hundreds of geometries fine, tens
  of thousands want batching.
- **Ray query loop**: loop `while (q.Proceed())`, not a single call. One `Proceed()` is enough for
  opaque geometry on hardware but is one thing a software traversal may disagree on.
- **lavapipe ray tracing is slow to start**: shaders are JIT-compiled when the command buffer runs,
  so a first build-and-trace submit with a cold Mesa shader cache can exceed `fence.Wait`'s 5 s
  limit (6.1 s measured on a 2-vCPU CI runner). `fence.Wait` throws `TimeoutException` then; poll
  `fence.Value` for such work. Reading back after the old, silent timeout gave an all-miss map;
  that was the intermittent CI "ray query miss", not a lavapipe traversal bug.
- **Ray tracing pipelines**: shader group identifier 32 B, SBT alignment 64 (NVIDIA VK, D3D12, WARP).
  `DispatchRays` and `DispatchRaysIndirect` give the same result as a ray query.
- **D3D12 RT library per export.** NRI adds each `NriShaderDesc` as a DXIL library exporting
  everything in it. One `lib_6_3` library named by several descs exports each name repeatedly and
  `CreateRayTracingPipeline` faults (SEH under the debug layer). Compile one library per entry
  point (`dxc -T lib_6_3 -exports <name>`). Vulkan takes one SPIR-V module for all.
- `cs_6_5` / `lib_6_3` with `[[vk::binding]]`: dxc warns the attribute is ignored for DXIL; harmless.
- **Micromaps, Vulkan**: work (transparent micro-triangle hides, opaque shows). Index-less OMM
  (index type NONE, address 0) draws VUID-...-pInfos-10904 from VVL 1.4.341, apparently a false
  positive; an index buffer is clean.
- **Micromaps, D3D12.** NRI v191's prebuild query passes null input-buffer and per-OMM-desc
  addresses, which the debug layer refuses at every micromap creation (error 1162); the shipped
  `NRI.dll` carries patch 0046, which passes dummy non-zero ones as the DXR spec describes, so
  micromaps create cleanly with the debug layer on. (On v190 the BLAS build that links the
  micromap also failed `Close` with `E_INVALIDARG`; fixed since.)
- Import by native object: `WrapperVK.CreateAccelerationStructureVK(desc, type)` /
  `WrapperD3D12.CreateAccelerationStructureD3D12(desc, type)`; caller names the type; needs
  `RayTracing`. The import reports the same GPU handle, native object, buffer and scratch sizes.

### Shared buffers, textures and fences

```csharp
using var shared = device.CreateSharedTexture(in desc);               // committed, DEVICE memory, exportable
using var handle = shared.GetSharedHandle();                          // NriSharedHandle: Handle, Type, Size; a new handle per call, Dispose closes it once (NRI's DestroySharedHandle)
using var buffer = device.CreateSharedBuffer(new BufferDesc { size = 4096, usage = BufferUsageBits.SHADER_RESOURCE });
using var bufferHandle = buffer.GetSharedHandle();
using var sharedFence = device.CreateSharedFence();
using var fenceHandle = sharedFence.GetSharedHandle();
using var binary = device.CreateSharedBinaryFence();                  // Vulkan only: binary semaphore
using var binaryHandle = binary.GetSharedHandle();
var ok = device.Desc.features.sharedResources != 0 && device.Desc.features.sharedFences != 0;
```

The same thing through the descs: `BufferDesc.shared` / `TextureDesc.shared` set to 1 with
`CreateCommittedBuffer` / `CreateCommittedTexture` in `MemoryLocation.DEVICE`, and
`CreateFence(new FenceDesc { initialValue = 0, shared = 1 })`.

| Backend | Buffer and texture handle | Fence handle |
|---|---|---|
| Vulkan, Windows | `OPAQUE_WIN32` NT handle | `OPAQUE_WIN32` NT handle (timeline or binary semaphore) |
| Vulkan, Linux | opaque fd | opaque fd |
| D3D12 | `D3D12_RESOURCE` NT handle | `D3D12_FENCE` NT handle |
| NONE | 0 | 0 |

- `Desc.features.sharedResources` needs `VK_KHR_external_memory_win32`/`_fd`,
  `Desc.features.sharedFences` needs `VK_KHR_external_semaphore_win32`/`_fd`; D3D12 reports both.
  lavapipe has external memory fd but not external semaphore fd (resources export, fences do not);
  MoltenVK has neither fd extension (only `VK_EXT_external_memory_metal`). Vulkan also reports
  `sharedFences` 0 when neither a timeline nor a binary semaphore can be exported. Creating a shared
  object without the feature throws `NriUnsupportedException`, and so does a Vulkan buffer, image
  or semaphore that the device cannot export (NRI asks `vkGetPhysicalDeviceExternal*Properties` /
  `vkGetPhysicalDeviceImageFormatProperties2` first: an image's format, usage, size, mip and layer
  count and sample count all count).
- **No feature bit predicts those refusals**, so probe with the Try forms: `TryCreateSharedBuffer`,
  `TryCreateSharedTexture`, `TryCreateSharedFence` and `TryCreateSharedBinaryFence` answer false
  (and create nothing) where NRI says `UNSUPPORTED`, and throw for anything else. `features.sharedFences`
  is 1 when either semaphore type exports: on D3D12 it is 1 while a binary fence is always
  `UNSUPPORTED`, and a Vulkan device can export one type and not the other.
- A shared resource is committed and in `DEVICE` memory: `shared` with `CreateBuffer` /
  `CreateTexture` / `CreatePlaced*`, or a committed one in `HOST_UPLOAD` / `HOST_READBACK`, is
  `INVALID_ARGUMENT` from NRI validation (D3D12 cannot share an upload or readback heap; Vulkan's
  exported allocation is not mapped).
- `Size` is the allocation size on both backends: what the importer's
  `VkMemoryAllocateInfo::allocationSize` or `CUDA_EXTERNAL_MEMORY_HANDLE_DESC::size` takes. 0 for
  a fence.
- The handle is yours. `Dispose` hands it to NRI's `DestroySharedHandle` (`CloseHandle` for an NT
  handle, `close` for a file descriptor), once: later calls do nothing, and after the first the
  object reads as NRI's cleared struct, `Handle` 0, `Type` `NONE`, `Size` 0 (`NriSharedHandle` is
  a class, so every reference sees that). No finalizer: a handle never disposed stays open until
  the process exits. It may outlive the device that exported it.
- **An import that takes ownership**: a file descriptor imported into Vulkan
  (`VkImportMemoryFdInfoKHR`, `VkImportSemaphoreFdInfoKHR`) or CUDA is closed by the importer, so
  do not dispose it: `var fd = handle.Detach();` returns the value and leaves the object empty,
  and a later `Dispose` does nothing (NRI's header: "After an FD import takes ownership, clear
  SharedHandle instead of destroying it"). An NT handle import duplicates, so dispose after it as
  usual. File descriptor 0 is valid (`IsValid` true), an NT handle 0 is not.
- Every call returns a handle of its own: Vulkan exports one NT handle per object and hands each
  caller a duplicate (Vulkan allows one export per object and handle type; NVIDIA returned the same
  value for a repeated raw export), D3D12 creates one per call.
- An object that is not shared asked for a handle: `INVALID_ARGUMENT` ("'shared' is false").
- A shared buffer, texture or fence still works as a normal one.
- **Binary shared fence** (`CreateSharedBinaryFence()`, i.e. `FenceDesc { SwapChainSemaphore, shared }`):
  the signal-once/wait-once semaphore external compositors import as an opaque NT handle or fd.
  Vulkan only; D3D12 has no binary fences and throws `NriUnsupportedException` (no error message),
  though it reports `features.sharedFences`: `TryCreateSharedBinaryFence` answers false there.
  The handshake rule is the binary semaphore's: every signal is followed by exactly one wait
  before the next signal, counting both sides of the export (yours signals, theirs waits, theirs
  signals, yours waits). In `NriFenceSubmit` the value is ignored (pass 0); never call `Value` or
  `Wait` on it. Import it into a binary `VkSemaphore` (an import replaces the payload, not the type).
- **CUDA**: `cuImportExternalMemory` takes the buffer or texture handle (`OPAQUE_WIN32` for
  Vulkan; `D3D12_RESOURCE` with `CUDA_EXTERNAL_MEMORY_DEDICATED` for D3D12; `size` = `Size`) and
  `cuImportExternalSemaphore` the fence handle (`TIMELINE_SEMAPHORE_WIN32` for a Vulkan fence,
  `D3D12_FENCE` for D3D12); a texture is mapped with `cuExternalMemoryGetMappedMipmappedArray`
  (restate the format, size and `CUDA_ARRAY3D_COLOR_ATTACHMENT` for an attachment, flag the
  import `CUDA_EXTERNAL_MEMORY_DEDICATED`). CUDA waits and signals the fence by value like a queue
  does, so a frame can hand a buffer or a frame to CUDA and take it back with two fence values. The binding has no CUDA
  code; the proof in `CudaInteropTests` uses the driver API from the NVIDIA driver's `nvcuda.dll`.
- Verified imports: Vulkan texture into a second Vulkan device (byte-exact), Vulkan timeline
  semaphore both ways, Vulkan binary semaphore both ways (queue signal and queue wait on each side,
  validation clean), D3D12 fence and texture into a second D3D12 device, CUDA buffer, texture and
  fence from both backends, both directions.
- **D3D11 cannot open D3D12 shared textures**: NRI creates every D3D12 texture `TYPELESS`, and
  `ID3D11Device1::OpenSharedResource1` refuses it with `E_INVALIDARG`. Import into D3D12, Vulkan
  or CUDA.
- Exclusive Vulkan images need a release barrier with `DstQueue = NriQueue.External` before
  another device acquires them, and an acquire with `SrcQueue = NriQueue.External` coming back.
- On master this is the `SharedHandle` extension (`device.SharedHandles.CreateSharedTexture(...)`
  and friends, no buffers); `next` has the shape merged upstream (#294, NRI main eb4ed356, with the
  maintainer's f18c6df9 and d0f5ce49).

### Upscaler

```csharp
var up = device.Upscaler;
if (up.IsUpscalerSupported(UpscalerType.FSR) && up.IsRuntimeInstalled(UpscalerType.FSR))
{
    using var fsr = up.CreateUpscaler(desc);   // NriUpscalerDesc
}
```

- `IsUpscalerSupported` answers what is compiled in, not what is installed.
- `CreateUpscaler` checks `GetRuntimeFiles` first and throws `NriUnsupportedException` naming the
  missing file. Without the guard: FSR returns `FAILURE` with no message; XeSS raises an SEH
  exception (0xC06D007E, delay-load) that leaves NRI's process-wide XeSS lock held, so the next XeSS
  creation hangs forever. (The packaged `NRI.dll` delay-loads `libxess.dll` so that it loads without
  the XeSS runtime; NRI's own build links it at load time.)
- NRI loads vendor DLLs by bare name, which searches the executable's directory. Under
  `dotnet app.dll` that is `dotnet.exe`'s directory: FSR fails, XeSS kills the process.
  `CreateUpscaler` therefore preloads each file by full path from `AppContext.BaseDirectory`, which
  makes both hosts work. Put the files beside your app's assembly.
- XeSS: D3D12 only. FSR: D3D12 and Vulkan. NIS: everywhere the Upscalers build runs. DLSR and
  DLRR (a `-WithDlss` build): NVIDIA adapters with ray tracing, both backends.
- Raw DLSR creation without `nvngx_dlss*.dll`: clean `FAILURE` on both backends.
- A failed vendor call reports an ERROR through your message handler (patch 0049): at creation it
  names the missing FSR loader DLL or the NGX result, at dispatch the vendor call and its result
  (`ffxDispatch`, `xessD3D12Execute`, NGX's evaluation). In NRI v191 itself creation returned a bare
  `FAILURE` and a failed dispatch was compiled out of release builds. DLSS may create without its DLL
  beside your executable: NGX also finds its feature DLLs elsewhere (measured for DLRR).
- Measured on every type and backend (figures in [upscalers.md](upscalers.md)): a constant image
  stays constant in every mode, at the dynamic-resolution minimum too; HDR with an exposure texture
  and pre-exposure; a moving edge with motion vectors lands where it should; `RESET_HISTORY` drops
  the history; NIS and FSR sharpness work. Motion vectors are `previous - current` in pixels with
  `MvScale = (1, 1)`; the tests use R16_SFLOAT colour, R32_SFLOAT depth, RG16_SFLOAT motion.
- **DLRR on D3D12: no R8 reactive mask.** NGX views the mask with the resource's own format, which
  is `R8_TYPELESS` (NRI creates D3D12 textures TYPELESS): debug error [28], then the device is
  removed. An R16_SFLOAT mask works; DLSR takes R8_UNORM.
- DLRR on D3D12 with an input below its creation size: the debug layer reports [527] on NGX's own
  internal resources (`nv.ngx.dlssd.resource`); the output is right. NGX's, not yours.
- The NIS and FSR sharpness settings show on low-contrast detail; NIS leaves strong edges as they
  are (a 0.2 to 0.6 edge measured the same at 0 and 1).

### LowLatency (Reflex)

- Needs `features.lowLatency` (NVAPI on D3D12, `VK_NV_low_latency2` on Vulkan) and a swap chain.
- D3D12: `GetLatencyReport` is all zeros after 72 frames; filled and ordered after 200. Do not read
  it in the first frames.
- **Vulkan: presenting from Vulkan with Reflex in a process breaks later D3D12 device creation**:
  `D3D12CreateDevice` returned `0x887A0007` (`DXGI_ERROR_DEVICE_RESET`), with or without
  `ALLOW_LOW_LATENCY` and `VSYNC`. Do not mix a Vulkan Reflex swap chain with D3D12 devices in one
  process.
- RivaTuner Statistics Server's implicit `VK_LAYER_RTSS` killed the process at the first Vulkan
  present on NRI v191, which enumerates only device groups; patch 0045 enumerates physical devices
  (see [Device creation](#device-creation)).

### Video

- Request `VIDEO_DECODE` / `VIDEO_ENCODE` in `QueueFamilies`, or `Video` is `UNSUPPORTED`.
- H.264 decode capabilities, 64x64 NV12: D3D12 min = max = 64x64, no references; Vulkan 48x16 to
  4096x4096, 16 references.
- **H.264 encode, D3D12**: minimum 256x256; a 64x64 `GetVideoCapabilities` query draws debug error
  1312 (SEH with break-on-error). One IDR frame at QP 26 is 277 B starting `00 00 00 01 65`.
- **H.264 encode, Vulkan**: minimum 160x64; resolve the feedback with `ResolveVideoEncodeFeedback`
  on the queue `resolvedMetadataQueueType` names (GRAPHICS on NVIDIA), then read it with
  `GetVideoEncodeFeedback`. One IDR at QP 26 is 277 B, as on D3D12. (v190 hung in the resolve;
  v191 reads the results on the host.) The bitstream's first barrier on another queue after the
  encode names no stages: `VIDEO_ENCODE` is not a stage of a GRAPHICS queue.
- **AV1 encode, Vulkan**: session parameters take a sequence without a colour description with the
  shipped `NRI.dll` (patch 0047; v191 passed reserved colour values and NVIDIA refused them).
  **On NVIDIA put the AV1 bitstream in host-visible memory** (`MemoryLocation.HOST_READBACK`
  through NRI; every host-visible memory type in a raw-Vulkan reproduction): driver 617.14 fails an AV1 encode into a `DEVICE` bitstream (no byte written,
  feedback never complete, no validation message), though that placement is valid; H.264 is
  unaffected. Vulkan describes an AV1 frame (`GetVideoAV1EncodeDecodeInfo`) only from the encoded
  payload passed as the header; D3D12 only without it. A picture's screen-content and
  integer-MV flags must agree with the sequence's `seqForce*` values on Vulkan.
- `WriteVideoAnnexBParameterSets` for H.265 returns `UNSUPPORTED` for short- and long-term reference
  sets, VUI, scaling lists and tiles. The header does not say.
- `GetVideoAV1EncodeDecodeInfo` returns pointers into the result struct itself: keep the struct
  alive and unmoved while using them.
- **Describing an AV1 frame**: pass the submitted `PictureDesc` and `Av1PictureDesc` in
  `NriVideoAV1EncodeDecodeInfoDesc` (required without a payload header); the frame type is the
  picture desc's, and a frame without `References` is a key frame. `EncodedPayloadHeader` is for
  Vulkan; D3D12's payload is tile data with no OBUs and answers `UNSUPPORTED` for one (NRI main
  9131c1b). `SavedFrameStates` (8, by slot) serve headers that inherit state.
- **AV1 encode, D3D12**: NRI's default picture flags encode (automatic segmentation follows the
  encoder's required feature flags since NRI main af45e72; patch 0032 on master); one tile only, no
  explicit segmentation. Under CQP the
  picture desc's base q index is ignored: set it through the rate control.

### Streamer

- `StreamerDesc.constantBufferMemoryLocation` and `constantBufferSize` are optional: leave both at
  their defaults for a streamer without a constant buffer (patch 0048; v191's validation refused
  the default location even then).
- **`StreamerDesc.queuedFrameNum` counts the frame being filled.** It is the number of per-frame
  regions in the dynamic buffer: data streamed in frame N is overwritten by frame
  N + `queuedFrameNum`, so the GPU must be done with frame N before that frame streams anything.
  An application that waits for frame N - k to finish before it streams frame N's data passes k;
  one that streams before that wait passes k + 1. Upstream's header says NRI adds one for the
  current frame. The code stopped doing that in May 2025; the comment is stale and not patched.
  Measured: with 2, frame 2 reuses frame 0's offset.
- **Block-compressed textures**: `StreamTextureData`'s `DataRowPitch` is the distance between rows
  of blocks, and they are staged in rows of blocks (NRI bc3ebe7). NRI v190 staged texel rows,
  16 times the space for 4x4 blocks (a 64x64 BC1 mip took 32,768 B instead of 2,048), and read
  past the end of `Data`. The bytes still arrived intact, so only the footprint showed it.
- `EndStreamerFrame` discards every copy batch not yet recorded with `CopyStreamedData`: fill and
  record a batch within one streamer frame. A batch filled on another thread must reach the
  recording thread before that frame's `EndStreamerFrame`.
- **`StreamerDesc.dynamicBufferDesc.size` is the initial per-frame size of the dynamic buffer**
  (patch 0012; upstream's header says it is ignored, and the ring grows from empty). The buffer is
  allocated at creation as `size` rounded up to 64 KiB times `queuedFrameNum`; frames that stream
  less never allocate, and a frame that streams more still grows it: a new committed buffer, the
  old one destroyed `queuedFrameNum + 1` frames later inside `EndStreamerFrame`, on the thread that
  calls it. Measured: 100 KiB x 2 frames gives a 256 KiB buffer at creation; one 192 KiB frame
  grows it to 384 KiB. Growth is to the exact 64 KiB-rounded high-water mark, not geometric, so
  size the ring for the biggest frame rather than rely on growth. The host copy runs under the
  streamer's lock; NRI's `MapBuffer` on the dynamic buffer is a pointer add (it is persistently
  mapped on both backends).

### SwapChain

- `NriWindow` (NRI's `Window`) is a struct of five platform members side by side, not a union. NRI
  reads the member of the running OS (`Windows` only on Windows).
- **`OUT_OF_DATE` is a value, not an exception.** `AcquireNextTexture(swapChain, semaphore, out
  index)` and `QueuePresent(swapChain, semaphore, presentId)` return `NriSwapChainStatus`: `Ok`, or
  `OutOfDate` (Vulkan only: the window changed under a swap chain that cannot follow it); recreate
  the swap chain. After `OutOfDate` from acquire nothing was acquired and the semaphore is not
  signalled; after `OutOfDate` from present the semaphore wait still happened. It is an enum so a
  further outcome can be added later; switch with a default branch. Every other failure throws. NRI reports no
  ERROR message for it (NRI #304, v191; v190 reported one on every resize).
- When it happens: NRI creates the swap chain with present scaling where the device has
  `swapchain_maintenance1` (`features.resizableSwapChain`), and then the driver keeps presenting
  through resizes. Measured on NVIDIA (RTX 4070 SUPER, Windows): an NRI-created device saw no
  `OUT_OF_DATE` across 8 resizes including a 0x0 client area; a wrapped device with only
  `VK_KHR_swapchain` got `OUT_OF_DATE` from present 6 times over the same 8 resizes, never from acquire.
- A wrapped Vulkan device that uses the swap chain needs `VK_KHR_surface`, the platform surface
  extension and `VK_KHR_get_surface_capabilities2` on its instance: NRI calls
  `vkGetPhysicalDeviceSurfaceCapabilities2KHR` unconditionally (an access violation without it).
- **The swap chain's size is clamped, not checked** (NRI PR #285): with present scaling to the
  surface's scaled range, without it to the window's current size, so read the real size from
  `NriSwapChain.Textures`. Measured on NVIDIA: 100x80 asked on a 64x64 window gives 100x80
  textures with scaling and 64x64 without. A window with no client area, a minimised one, makes
  Vulkan answer `NOT_READY` when the clamp gives zero: `TryCreateSwapChain` returns
  `NriSwapChainStatus.NotReady` and no swap chain, `CreateSwapChain` throws `NriException`
  (`NOT_READY`). With present scaling a driver may still create one there (NVIDIA does, at the
  requested size), so do not rely on either answer for a minimised window: skip frames while it
  has no client area.
- **`SwapChainBits.LATEST_READY` with `VSYNC`** (patch 0017) asks for Vulkan's FIFO_LATEST_READY:
  the display changes image only at vblank, without tearing, and shows the newest ready image,
  dropping older ones. Several frames may finish per vblank, so rendering is not capped at the
  refresh rate (`VSYNC` caps it except with this bit, as NRI's header now says). It applies only when `device.Desc.features.latestReadySwapChain`
  is set (Vulkan with `VK_EXT_present_mode_fifo_latest_ready`); otherwise, and on D3D12, the bit is
  ignored and `VSYNC` gives FIFO (FIFO_RELAXED with `ALLOW_TEARING`). Without `VSYNC` the bit does
  nothing: NRI's uncapped order is MAILBOX (or IMMEDIATE with tearing) first, and FIFO_LATEST_READY
  only where MAILBOX is not offered. NRI never reports the present mode it chose, so the feature
  bit is the only answer. Measured on NVIDIA: the feature is 1 and a `VSYNC | LATEST_READY` swap
  chain presents with the Khronos layer clean.
- **`WaitForPresent(swapChain, presentId)` needs the non-zero id of a tracked present**: the id
  given to an earlier `QueuePresent`. NRI validation refuses 0 with `INVALID_ARGUMENT` and an
  ERROR message on every backend, D3D12 included, although D3D12 ignores the id and waits on the
  swap chain's frame-latency waitable object. Present with increasing non-zero ids and wait for the
  last one; a present with id 0 is untracked and cannot be waited for.

### MeshShader, DescriptorHeap, Imgui

- Thin; NRI's semantics. `CreateImgui` needs ImGui 1.92.9+ with
  `ImGuiBackendFlags_RendererHasTextures` and an unmodified 20-byte `ImDrawVert`.

## Wrappers and the escape hatch

Import native objects (`WrapperVK`, `WrapperD3D12`), or create an NRI device over yours:

```csharp
using var device = NriDevice.CreateFromVulkan(new NriVulkanDeviceOptions
{
    VkInstance = instance, VkDevice = vkDevice, VkPhysicalDevice = physicalDevice,
    QueueFamilies = [/* QueueFamilyVKDesc per family you created */],
    MinorVersion = 3,
    VkDeviceExtensions = enabledDeviceExtensions,
});
var tex = device.WrapperVK.CreateTextureVK(/* ... */);
```

- `CreateFromD3D12` with no queue families: the device has **no queues** (`NriDevice.Create`
  defaults to one graphics queue).
- `CreateFromVulkan`: every listed extension must be enabled; NRI derives what it uses from the
  lists and the physical device's features. `LibraryPath` is the only loader-path field NRI has.
- On macOS a raw `VkInstance` must enable `VK_KHR_portability_enumeration` with
  `VK_INSTANCE_CREATE_ENUMERATE_PORTABILITY_BIT_KHR` (else the loader hides MoltenVK), and the
  `VkDevice` must enable `VK_KHR_portability_subset`
  (VUID-VkDeviceCreateInfo-pProperties-04451).
- **Wrapped command buffer counts as recording from creation** under NRI validation: `Begin` is
  refused ("already in the recording state"). Begin it natively (or pass the D3D12 list open), call
  `SetDescriptorPool`, record, `End`.
- **`CreateBufferVK` records usage `NONE`**: a buffer barrier with shader access is refused by
  NRI's validation. Use a global barrier for imported buffers.
- `CreateMemoryVK` with `mappedMemory = 0`: NRI maps the memory itself.
- `WrapperVK` also returns `GetInstanceVK`, `GetPhysicalDeviceVK`, `GetQueueFamilyIndexVK`,
  `GetInstanceProcAddrVK`, `GetDeviceProcAddrVK`.
- Every object has `GetNativeObject()` (the `VkImage`, `ID3D12Resource*`, ...).

Below the wrappers is NRI itself:

```csharp
ref readonly var core = ref device.Core;                         // CoreInterface; device.Helper likewise
uint size = core.GetQuerySize((QueryPool*)pool.NriHandle);       // NriHandle on every object, NriQueue, NriDescriptorSet
```

- `SharpNRI.Native.NriApi` has every export.
- Bypasses the binding: no argument checks, no child counting, no marshalling. A native `Destroy*`
  leaves the wrapper dangling. NRI's validation still runs if the device has it on.
- Creating a device through `NriApi` directly with a null `CallbackInterface.AbortExecution`: NRI
  installs its own, which calls `DebugBreak()` on every error, killing the process with
  `0x80000003` when no debugger is attached. `NriDevice.Create` always supplies one.

## Flat exports

Static class `Nri`, NRI's names without the `nri` prefix; no device needed.

```csharp
uint vk = Nri.ConvertNRIFormatToVK(Format.RGBA8_UNORM);              // 37
ref readonly FormatProps p = ref Nri.GetFormatProps(Format.BC7_RGBA_UNORM);
string name = Nri.GetFormatName(Format.BC7_RGBA_UNORM);
string api = Nri.GetGraphicsAPIString(GraphicsAPI.VK);               // "VK"
Nri.SetThreadName("render");
using (Nri.Annotate("upload")) { }                                   // NVTX range, this thread
Nri.ReportLiveObjects();                                             // DXGI debug report
```

- `Format.MAX_NUM` and beyond throw `ArgumentOutOfRangeException`: NRI indexes its tables
  unchecked (`nriGetFormatProps(MAX_NUM)` returned another format's entry).
- **`ConvertVKFormatToNRI`** is the inverse of `ConvertNRIFormatToVK` for all 108 formats,
  block-compressed ones included (NRI 5ed8bb6; NRI v190's table ended at 130 and returned `UNKNOWN`
  for all 52 BC, ETC2 and ASTC formats). BC1's RGB `VkFormat`s (131, 132) stay `UNKNOWN`.
- **`ConvertDXGIFormatToNRI(18)`** returns `RGB32_SINT`.
- `ConvertNRIFormatToDXGI` has no value for the 38 ETC2 / ASTC formats.
- **Linux and macOS: every DXGI conversion returns 0 / `UNKNOWN`**: the table is compiled only with
  a D3D backend.
- Annotations here are host-side, per thread; `NriCommandBuffer` and `NriQueue` have their own
  `BeginAnnotation` / `EndAnnotation` / `Annotation`.

## The validated test device

```csharp
using SharpNRI.Testing;

if (NriTestDevice.Unavailable(GraphicsAPI.VK) is { } reason) Skip(reason);
using var fixture = NriTestDevice.Create(GraphicsAPI.VK, o => o.QueueFamilies = [new NriQueueFamily(QueueType.GRAPHICS)]);
var device = fixture.Device;
// ...
fixture.ThrowIfErrors();     // NriValidationException listing every error message
```

- NRI validation and the backend validation layer on by default; every message in
  `fixture.Messages` / `Errors` / `Warnings`.
- `NriTestDevice.Backends` (NONE, VK, D3D12), `GpuBackends` (VK, D3D12), `AdapterFor(api)`,
  `IsAvailable(api)`.

| Variable | Effect |
|---|---|
| `SHARPNRI_TEST_SOFTWARE=1` (implied by `GITHUB_ACTIONS=true`) | prefer WARP / lavapipe |
| `SHARPNRI_TEST_BACKEND=VK` | only that backend |
| `SHARPNRI_TRACE=1` | print each message as it arrives; the only record that survives a native crash |

- Validation layer version must match NRI's Vulkan headers (1.4.365 in v190). An older Khronos layer
  reports every newer struct NRI chains (`VkPhysicalDeviceVulkan14Features`, maintenance7/8,
  `VK_EXT_zero_initialize_device_memory`) as "unknown VkStructureType" errors, which fail
  `ThrowIfErrors`. Ubuntu 24.04 ships 1.3.275; LunarG's noble apt stops at 1.4.313; 1.4.363 is
  clean. `tools/ci/fetch-vulkan-layer.sh` extracts the layer from the SDK tarball; put it first with
  `VK_ADD_LAYER_PATH`.

## Threads

- `Create*`: free-threaded. `QueueSubmit`: free-threaded.
- `Destroy*` and every `Cmd*`: externally synchronized, one thread at a time per object. The
  binding adds no locks.
- **Vulkan sharing mode**: `GetQueue` for a COMPUTE or COPY queue switches resources created
  afterwards without `queueExclusive` to concurrent sharing. Request every family in
  `QueueFamilies` and get the queues before creating resources that cross them.
- `Nri.Annotate` / `SetThreadName` act on the calling thread.

## Platform notes

### Windows

- D3D12 on the Agility SDK needs its runtime in `AgilitySDK/` beside the application, from
  Microsoft's package ([Agility SDK](#agility-sdk)), or another directory in `D3D12AgilitySdkPath`.
- Visual C++ 2015-2022 runtime required, not shipped.
- `NRI.dll` delay-loads `d3d12.dll` and `dxgi.dll` (both packages), resolved at the first D3D12 or
  DXGI call, and the Upscalers `NRI.dll` also `libxess.dll`: absent is fine until XeSS is created.
- WARP: D3D12 software adapter, `Architecture.SOFTWARE`; runs the whole suite including ray
  tracing (BLAS 65,536 B).
- RenderDoc 1.44 captures NRI's D3D12 device on the inbox runtime only: with the Agility SDK
  applied (`D3D12AgilitySdkPath` null and an `AgilitySDK` directory present) the device is
  `D3D12Core.dll`'s own, RenderDoc never wraps it and a capture records nothing. Set
  `D3D12AgilitySdkPath = ""` for capture sessions (measured; AGENTS.md: D3D12 and the Agility SDK).

### Linux

- **Unproven on hardware.** Linux is verified on lavapipe (software Vulkan) only; no hardware
  Linux driver has run this code. The two intermittent Linux CI failures were test bugs, both
  identified and fixed in the tests: the ray-query "miss" was a readback after NRI's 5 s fence-wait
  timeout on a slow first submit, and the test-host SIGSEGV was a stale Vortice function table.
  Details: AGENTS.md, "CI runners and flakes".
- NRI `dlopen`s `libvulkan.so.1` at device creation.
- **Loader older than the driver** (Ubuntu 24.04's stock 1.3.275 loader with Mesa 25's 1.4
  lavapipe): NRI v190 loaded core 1.4 entry points the driver returns null for, and
  `CmdSetRootConstants` crashed (SIGSEGV at 0) while `HOST_TRANSFER` textures were rejected. NRI
  a4e562e clamps its Vulkan version to the instance's. Such a device reports
  as Vulkan 1.3.
- lavapipe: no external semaphores (`features.sharedFences` 0; shared buffers and textures export); a first ray tracing submit can take
  seconds (JIT, see RayTracing).
- `NriAdapter.Uid` is built from the device UUID (no LUID on Linux).
- NRI builds with GCC 13 since 9b45d77; clang is what CI uses.

### macOS

- Vulkan through MoltenVK only; `libNRI.dylib` needs macOS 15+.
- The library links only system libraries (patch 0007) and `dlopen`s `libvulkan.1.dylib` at device
  creation. Ship or install the Vulkan loader and MoltenVK (Homebrew `vulkan-loader` +
  `molten-vk`, or the LunarG SDK) on a path dyld searches. `NriDeviceOptions` has no loader path.
- Homebrew's `/opt/homebrew/lib` is not on dyld's default fallback path: set
  `DYLD_FALLBACK_LIBRARY_PATH=$(brew --prefix)/lib:/usr/local/lib:/usr/lib`.
- Homebrew's molten-vk keeps its ICD in the keg's `etc/vulkan/icd.d`, not `share/vulkan/icd.d`
  ("Found no drivers" otherwise): set `VK_DRIVER_FILES` to `MoltenVK_icd.json`.
- Homebrew's validation layer is found without help; adding it via `VK_ADD_LAYER_PATH` gives a
  "duplicate layer" warning.
- Portability device: `samplerLodBiasMax` is 0 (sampler `mipBias` must be 0); raw instances must opt
  into portability enumeration (see Wrappers).
- No shared handles (`features.sharedResources` and `sharedFences` 0: no fd extensions), ray tracing, mesh shaders, video, descriptor heaps, shading-rate attachments.
- Apple Paravirtual device (GitHub's macOS VMs): `HOST_TRANSFER` texture binding segfaults;
  same-submission timestamp copies read 0. Not measured on physical Macs.
- MoltenVK 1.4.2: `ResolveTexture` of a region smaller than the destination mip, into a texture with
  more than one mip, aborts the process (Metal: "MTLTextureDescriptor mipmapLevelCount must equal 1
  for multisample textures"): MoltenVK resolves through a temporary multisample texture with the
  destination's mip count. Resolve whole mips, or into a one-mip texture and copy.

## Traps

| Trap | Effect | Section |
|---|---|---|
| D3D12 under `dotnet` without `D3D12AgilitySdkPath` / an `AgilitySDK` directory | inbox runtime, `options22` warning | Device creation |
| Agility applied once | inbox runtime unreachable for the rest of the process | Device creation |
| `Microsoft.Direct3D.D3D12` of another version than NRI's (1.620.1 for v191) | `INVALID_SDK` on every D3D12 device; with `SharpNRI.AgilitySDK` a build error, `SHARPNRI011` | Agility SDK |
| A stale `NRI.dll` or `SharpNRI.Descs` of another NRI version | `INVALID_SDK` at device creation naming both versions | Packages and install |
| `UNSUPPORTED` that no feature bit predicts | shared objects the driver cannot export (use the `TryCreateShared*` forms), a D3D12 binary shared fence, FSR / XeSS refused by the device or driver, NIS without `storageWriteWithoutFormat` beyond RGBA8 / RGBA16F / RGBA32F output, Annex-B parameter sets outside H.264 High / plain H.265, the AV1 encode-info input form per backend, `QueryVideoMemoryInfo` without `VK_EXT_memory_budget` | Shared buffers, textures and fences; Extensions |
| Anything measured on NONE | NONE answers SUCCESS to everything and never validates | Device creation |
| `GetQueue` bad index | `INVALID_ARGUMENT`, no message | Device creation |
| Video without video queues requested | `Video` is `UNSUPPORTED` | Extensions: Video |
| Disposing the device first | throws, device stays usable | Lifetime |
| Disposing memory with a bound resource | `InvalidOperationException` naming the count, the memory kept, for resources bound or placed through `Bind*Memory` / `CreatePlaced*`; an `AllocateAndBindMemory` allocation refuses while any resource of its call is alive. Freed first, it would be a validation error, a leak, and on D3D12 every later `D3D12CreateDevice` in the process failing 0x887A0007 | Lifetime |
| `HOST_READ` / `HOST_WRITE` in a buffer barrier | validation error; fence wait is the visibility | Memory |
| `UploadData` on NV12 | VK VUID-...-07981, D3D12 error 1417 | Memory |
| `HOST_TRANSFER` on Apple Paravirtual | segfault in `vkBindImageMemory` | Memory |
| Sampler `mipBias` on MoltenVK | VUID-...-04467; `samplerLodBiasMax` is 0 | Memory |
| Region `ResolveTexture` into a multi-mip texture on MoltenVK | the process aborts in Metal | Platform notes: macOS |
| Non-zero `VkBindingOffsets` mismatch | descriptors bind to the wrong slots | Descriptors |
| Vulkan COMPUTE/COPY queue fetched late | resources switch to concurrent sharing | Threads |
| Pixel compare without tolerance | UNORM rounding differs by one | Recording |
| Texture barrier side with access `NONE`, a layout and no stages, after an access in the same list | D3D12 debug error 1417; name the stages (then `COMMON` access, NRI 48a14c3) or the real access | Recording |
| `ZeroBuffer` ordered with `CLEAR_STORAGE` | wrong on D3D12 (a copy there); use `COPY`, which covers the Vulkan fill (NRI 1163658) | Recording |
| `GENERAL` around a Vulkan copy, or a hand-off without `SrcQueue` / `DstQueue` | Vulkan refuses a copy in `GENERAL`; D3D12 debug error 1334 at execution; use the copy layouts and name both queues on the hand-off (NRI 732f737) | Recording |
| Two command buffers of one allocator recording at once | D3D12: debug error 540, `Begin` throws | Recording |
| Same-submission timestamp copy on MoltenVK | reads 0 | Queries |
| One DXIL library for a D3D12 RT pipeline | `CreateRayTracingPipeline` faults | RayTracing |
| Single `Proceed()` in a ray query | software traversal may disagree | RayTracing |
| GPU work longer than 5 s behind `fence.Wait` | `TimeoutException` (NRI's `NRI_TIMEOUT_FENCE`); poll `fence.Value` | RayTracing |
| D3D12 micromaps | debug error 1162 at creation on v191 (the prebuild; patch 0046 fixes it). A `{MICROMAP_WRITE, MICROMAP}` before side closes (NRI main 1dc13b2; patch 0030 on master); before the fix NRI added postbuild-info sync and `Close` failed | RayTracing |
| Index-less OMM on Vulkan | VUID-...-pInfos-10904 (likely false positive) | RayTracing |
| `features.sharedFences` 0 | lavapipe (no external semaphore fd), MoltenVK (no fd handles; `sharedResources` 0 too), any Vulkan device that exports no semaphore type | SharedHandles |
| D3D11 opening a D3D12 shared texture | `E_INVALIDARG` (TYPELESS) | SharedHandles |
| Two exports of one object | two handles: NRI exports once and duplicates per call (a raw repeated export returns the same NT handle on NVIDIA) | SharedHandles |
| `IsUpscalerSupported` true, DLL absent | `CreateUpscaler` throws naming the file | Upscaler |
| XeSS without the guard | lock held, next creation hangs | Upscaler |
| Vulkan Reflex + D3D12 in one process | `D3D12CreateDevice` 0x887A0007 | LowLatency |
| RTSS running, a wrapped device whose instance never ran `vkEnumeratePhysicalDevices` (or an `NRI.dll` without patch 0045) | process dies at first Vulkan present | Device creation |
| Reflex report in the first frames | all zeros | LowLatency |
| AV1 encode on Vulkan (NVIDIA), bitstream in `DEVICE` memory | driver fails the encode, nothing written | Video |
| H.264 encode 64x64 on D3D12 | debug error 1312; minimum is 256x256 | Video |
| `StreamerDesc` default memory location | validation error | Streamer |
| Wrapped device from D3D12 without queue families | no queues | Wrappers |
| `Begin` on a wrapped command buffer | "already in the recording state" | Wrappers |
| Buffer barrier on a `CreateBufferVK` import | refused (usage NONE); use a global barrier | Wrappers |
| Raw `VkInstance` on macOS | MoltenVK hidden without portability enumeration | Wrappers |
| Raw `NriApi` device creation with null `AbortExecution` | `DebugBreak()` kills the process on any error | Wrappers |
| `ConvertVKFormatToNRI` on BC1 RGB (131, 132) | `UNKNOWN` (NRI has BC1 RGBA only; other compressed formats map, NRI 5ed8bb6) | Flat exports |
| Wrapped VK device without `VK_KHR_get_surface_capabilities2` | access violation in `CreateSwapChain` | SwapChain |
| D3D12 debug layer warnings `[989]` at device creation | NRI v191 builds against Agility 1.620, which reports every legacy `CheckFeatureSupport` as deprecated: 33 warnings per device, answers unchanged (NRI #307, D3D12MA #102); harmless, filter ID 989 if your log treats warnings as failures | D3D12 |
| Expecting `OUT_OF_DATE` on resize | none with present scaling (`features.resizableSwapChain`) | SwapChain |
| DXGI conversions on Linux / macOS | 0 / `UNKNOWN` | Flat exports |
| `Format.MAX_NUM` to a flat export | `ArgumentOutOfRangeException` | Flat exports |
| Khronos layer older than NRI's headers | "unknown VkStructureType" errors | Test device |
| Vulkan loader older than the driver | crashed in `CmdSetRootConstants` on NRI v190 (fixed in NRI a4e562e) | Platform: Linux |
| Homebrew Vulkan on macOS | not found without `DYLD_FALLBACK_LIBRARY_PATH` and `VK_DRIVER_FILES` | Platform: macOS |
| An allocator that throws or returns null | an NRI object's creation fails with `OUT_OF_MEMORY` (NRI main 9d276c3; patch 0034 on master); inside NRI's containers, device creation's first allocation among them, still an access violation. An allocator must not fail | Device creation |
| Queue priority between 0 and 0.5 on D3D12, or below 0 on Vulkan | D3D12 stays Normal (High only above 0.5); Vulkan treats low as normal | Device creation |
| `NriSharedHandle` disposed twice, or never | the second `Dispose` does nothing (`Handle` 0 and `Type` `NONE` after the first); one never disposed stays open until the process exits (no finalizer); `Detach` gives it up unclosed for an import that takes ownership | SharedHandles |
| Host-memory texture copy into a short span | `ArgumentException`: the binding refuses what NRI would write past | Memory |
| `AllocateAndBindMemory` with a `preferredMemorySize` | packs alike in every order (NRI #305, v191); before, NRI's alignment sort left out the last buffer and the last texture, so the count could be one or two for the same three textures | Memory |
| Vertex streams declared out of slot order on Vulkan | each attribute reads its stream's slot (NRI main 0593d91; patch 0025 on master); before the fix the stream index: nothing drawn, VUID-...-06209 | Recording |
| An empty `NriVertexInputDesc` on Vulkan | draws as `VertexInput = null` (NRI main 8a8fc2b; patch 0026 on master); before the fix VUID-vkCmdDraw-pStrides-04913 on every draw | Recording |
| `ClearAttachments` in a stencil-only pass on D3D12 | clears (NRI main 909a139; patch 0027 on master); before the fix an access violation in NRI | Recording |
| Depth-stencil view as `depth` only, `LoadOp.CLEAR` | clears the stencil too, with the `depth` clear value, as on Vulkan (NRI main c8fe3b2; patch 0028 on master); before the fix D3D12 left it | Recording |
| `SetDepthBias` on a pipeline without a static bias | the bias is not dynamic: VUID-vkCmdDraw-None-08608 / D3D12 1366 | Recording |
| `ENABLE_DRAW_PARAMETERS_EMULATION` on Vulkan | inert: indirect records stay `DrawDesc` / `DrawIndexedDesc`, so set the flag on every backend (patch 0043; before it, validation demanded the D3D12 record stride on Vulkan) | Recording |
| No barrier between a compute write and an indirect read | NVIDIA reads zeros without a message; only synchronization validation reports it | Recording |
| A `TEXTURE` / `STORAGE_TEXTURE` / `TEXTURE_CUBE` view of a layer other than the first | addresses that layer on every backend (NRI #306, v191: D3D12 builds it as a one-layer array view); NRI v190 used the first layer on D3D12, silently | Descriptors |
| A storage view over several mips in `ClearStorage` | Vulkan clears every mip, D3D12 only the first; one mip per storage view | Descriptors |
| D3D12 attachment resolve into layer or mip > 0, or of a layered view | right with patch 0036 (NRI main's fix, backported to v190); without it a wrong subresource, `Close` failing, or the first layer only | Recording |
| Occlusion query counts on Vulkan | NRI begins them without `PRECISE`: only non-zero is guaranteed | Queries |
| Pipeline statistics read at `sizeof(PipelineStatisticsDesc)` | wrong on Vulkan (104 bytes a query); read at `QuerySize` | Queries |
| Acceleration structure placed at the D3D12 memory desc's alignment | 256 (NRI main 561e2be; patch 0031 on master); before the fix 128 under tight alignment, and the descriptor was refused | RayTracing |
| AV1 encode on D3D12 with NRI's default picture flags | encodes (NRI main af45e72; patch 0032 on master); at v190 debug error 1306 and `Close` failed when the encoder requires automatic segmentation | Video |
| `GetVideoAV1EncodeDecodeInfo` on D3D12 with references for a key frame | described as the submitted `PictureDesc` says (NRI main 57d92ff; a payload header answers `UNSUPPORTED`, 9131c1b); at v190 inter, and a header a silent `FAILURE` (patch 0033 on master) | Video |
