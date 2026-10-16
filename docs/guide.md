# PrismNRI usage reference

Every entry is the real API; every caveat sits next to the API it concerns, with the measured fact.
NRI's own header comments still apply; this covers what the binding adds and what NRI does that the
headers do not say. NRI v190.

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
| `PrismNRI` | `PrismNRI` (hand-written, `Nri*`), native NRI, Base variant | always |
| `PrismNRI.Descs` | `PrismNRI.Native` (generated, NRI's names) + managed desc forms | layouts without a device; pulled in by `PrismNRI` |
| `PrismNRI.Testing` | `PrismNRI.Testing` | tests |
| `PrismNRI.Native.Upscalers` | NRI with NIS, FSR, XeSS | upscaler extension |
| `PrismNRI.Native.Agility` | `AgilitySDK/D3D12Core.dll`, `d3d12SDKLayers.dll` | D3D12 on the Agility SDK (Windows) |

```xml
<PackageReference Include="PrismNRI" Version="0.4.0-alpha" ExcludeAssets="native" />  <!-- only with Upscalers -->
<PackageReference Include="PrismNRI.Native.Upscalers" Version="0.4.0-alpha" />
<PackageReference Include="PrismNRI.Native.Agility" Version="0.4.0-alpha" />
```

- Not on NuGet.org: the `.nupkg` files are GitHub release assets. Folder feed, `nuget.config` beside
  the project:

  ```xml
  <configuration>
    <packageSources>
      <add key="prismnri" value="path/to/nupkg" />
    </packageSources>
  </configuration>
  ```

- The release workflow's `consume` job restores them that way (`tools/consume`) and creates NONE
  plus a GPU device on windows, ubuntu and macos before publishing.
- Base and Upscalers both contain `NRI.dll` / `libNRI.so` at the same path: take exactly one; with
  Upscalers, `ExcludeAssets="native"` on `PrismNRI`.
- Base: Vulkan, D3D12, NONE, NRI validation, ImGui, NVAPI, AGS, debug names; no upscaler SDK.
  `IsUpscalerSupported` answers nothing there.
- osx-arm64: Base only; Vulkan and NONE; no Agility, no Upscalers.
- Windows needs the Visual C++ 2015-2022 runtime; not shipped.
- No package ships a vendor runtime. Beside your executable, under the vendor's terms:

  | Upscaler | D3D12 | Vulkan |
  |---|---|---|
  | FSR | `amd_fidelityfx_loader_dx12.dll`, `amd_fidelityfx_upscaler_dx12.dll` | `amd_fidelityfx_vk.dll` |
  | XeSS | `libxess.dll` | not offered |
  | DLSR / DLRR | `nvngx_dlss*.dll` (DLSS build only) | same |

  `NriUpscalerExtension.GetRuntimeFiles(type, api)` names them for the running build.
- DLSS: `pwsh tools/build-native.ps1 -WithDlss <DLSS SDK dir>` builds an NGX-linked `NRI.dll` into
  `runtimes-upscalers-dlss/<rid>/native/`. NGX is linked statically, so that `NRI.dll` is under
  NVIDIA's RTX SDK licence: yours to ship, never packed here.

## Device creation

```csharp
foreach (var a in NriAdapter.Enumerate())
    Console.WriteLine(a);                       // name, vendor, architecture, supported backends

using var device = NriDevice.Create(new NriDeviceOptions
{
    GraphicsApi = GraphicsAPI.VK,               // VK, D3D12, NONE
    Adapter = NriAdapter.Enumerate().First(a => a.Supports(GraphicsAPI.VK)),
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
| `MessageHandler` | receives NRI and validation-layer messages. A throwing handler loses that one message. `device.LastError` holds the last error text and is attached to the `NriException` of the call that failed. A Khronos layer message arrives as `[id] name: text` (patch 0015 adds the name, e.g. `BestPractices-vkBindImageMemory-small-dedicated-allocation`; upstream forwards the number and the text, and current layers no longer embed the name in the text) |
| `BreakOnError` | breaks only when a debugger is attached; otherwise errors never terminate the process. Also decides the D3D12 debug layer's own break (`SetBreakOnSeverity` for ERROR and CORRUPTION), evaluated at device creation: on only with `BreakOnError` and a debugger attached then (patch 0018). Otherwise a D3D12 debug-layer error arrives as an error message, `[id] text` |
| `EnableD3D12GpuBasedValidation` | D3D12 GPU-based validation (patch 0019): checks descriptors and resource states while shaders run. Slow; needs `EnableGraphicsApiValidation`. Process-global and never switched off: every D3D12 device created later in the process has it |
| `Allocator` | `INriAllocator` for NRI's own host allocations; `NriCountingAllocator` counts them. A throwing allocator returns null, which NRI reports as out of memory |
| `ForwardAllocatorToThirdParty` | also routes VMA / D3D12MA host allocations; off by default (chatty) |
| `D3D12AgilitySdkPath` | null: the `AgilitySDK` directory beside the assembly when present (`NriDevice.DefaultAgilitySdkPath`), else inbox. `""`: force inbox |
| `VkBindingOffsets` | see [Descriptors and layouts](#descriptors-and-layouts) |
| `VkEnumeratePhysicalDevicesAfterCreation` | Vulkan, default true: one `vkEnumeratePhysicalDevices` on NRI's instance right after creation, results discarded, so implicit layers that map physical devices from that call (RivaTuner's) know NRI's device; false skips it. Not on `CreateFromVulkan` (the instance is yours) |
| `Robustness`, `DeviceLostInfoLevel`, `EnableD3D12RayTracingValidation`, `EnableD3D12GpuBasedValidation` | only on `NriDevice.Create`, not on the wrapped routes (a wrapped D3D12 device has the debug layer its creator gave it, and NRI sets no break on it) |

Caveats:

- **Agility from a managed host.** NRI's documented route (`D3D12SDKVersion` exported from the
  executable) never works under `dotnet`, `testhost` or an apphost; without
  `D3D12AgilitySdkPath` the inbox runtime runs and NRI warns
  `CheckFeatureSupport(options22) failed, result = 0x80070057`. Patch 0001 loads the SDK through
  `ID3D12SDKConfiguration1::CreateDeviceFactory` + `ApplyToGlobalState`; success logs
  `Using Agility SDK v1.619 from '...'`.
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
- **Enumerate once and pass the adapter.** Every `nriEnumerateAdapters` creates and destroys a
  Vulkan instance (API 1.2, every implicit layer loaded and unloaded) and a DXGI factory with up
  to three `D3D12CreateDevice` probes per adapter. `NriAdapter.Enumerate()` makes one such call
  (one 32-entry buffer, no count call first). `NriDevice.Create` with `Adapter` null enumerates
  once more inside NRI, so keep the `NriAdapter` and pass it: the device's own instance is then
  the process's second, not its third or fourth.
- **RivaTuner Statistics Server's implicit layer** (`VK_LAYER_RTSS`) maps each physical device to
  its instance from `vkEnumeratePhysicalDevices` and uses that map at the first present. NRI finds
  physical devices only through `vkEnumeratePhysicalDeviceGroups`, so on NRI's device the loader
  logged `vkEnumeratePhysicalDevices: Invalid instance` and the process failed fast at the first
  present (exit 0xC0000409). `NriDevice.Create` therefore calls `vkEnumeratePhysicalDevices` once
  on NRI's instance after creation (`VkEnumeratePhysicalDevicesAfterCreation`, default true), and
  a presenting Vulkan process survives with the layer on. A device from `CreateFromVulkan` is on
  your instance: enumerate physical devices on it with `vkEnumeratePhysicalDevices` yourself. The
  number of instances is not the cause (measured with the device's instance the only one the
  layer saw).
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
- Dispose resources before the memory they are bound to: NRI's validation refuses freeing memory
  with a resource still bound.
- `SetDebugName(string)` on every object.

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

// Create, measure, allocate, bind
using var tex = device.CreateTexture(new TextureDesc
{
    type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE,
    format = Format.RGBA8_UNORM, width = 256, height = 256,
});
var req = tex.GetMemoryDesc(MemoryLocation.DEVICE);
using var mem = device.AllocateMemory(new AllocateMemoryDesc { size = req.size, type = req.type });
device.BindTextureMemory(tex, mem);

// Group unbound resources into few allocations
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
  (patch 0006): MoltenVK reports `maxSamplerLodBias = 15.999` but forbids any bias
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
- The layout descs live in `PrismNRI.Descs`; build them without a device or the native library.

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
  barrier default); the D3D12 backend reads it as all shaders and patch 0016 makes NRI validation
  agree. Upstream validation masked each range's stages with 0 and refused a range with explicit
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
- **A texture barrier side with access `NONE` and a defined layout is layout-only on D3D12**
  (patch 0020): it is recorded with `SYNC_NONE` whatever its stages, because the D3D12 debug layer
  refuses `NO_ACCESS` with a layout other than `UNDEFINED` and any other sync (1331, then `End`
  throws `INVALID_ARGUMENT` from `Close`). On Vulkan the same side is an execution dependency on
  its stages with no access, e.g. after a semaphore wait. So such a side orders nothing on D3D12
  beyond the layout transition; give it the access the previous work used when the order matters.
- **`ZeroBuffer` is stage `COPY`, access `COPY_DESTINATION`, on both backends** (patch 0021). It
  is `CopyBufferRegion` from a zero buffer on D3D12 and `vkCmdFillBuffer` on Vulkan, a clear
  command; NRI's Vulkan `COPY` stage includes `VK_PIPELINE_STAGE_2_CLEAR_BIT` with the patch.
  `CLEAR_STORAGE` is wrong for it on D3D12 (`CLEAR_UNORDERED_ACCESS_VIEW`).
- **Copy queues: use `COPY_DESTINATION` / `COPY_SOURCE` on every queue, and name the hand-off.**
  Vulkan's copies (`CopyTexture`, `ResolveTexture`, `UploadBufferToTexture`,
  `ReadbackTextureToBuffer`) require the transfer layouts, so a texture in `GENERAL` cannot be
  their destination there. D3D12 copy command lists allow only `D3D12_BARRIER_LAYOUT_COMMON`; with
  patch 0022 NRI records `COPY_SOURCE` / `COPY_DESTINATION` as `COMMON` in a copy list and on the
  copy-queue side of a texture barrier whose `SrcQueue` / `DstQueue` is a copy queue. Across
  queues: on the COPY queue `{UNDEFINED}` -> `{COPY_DESTINATION}`, the copy, then a release
  `{COPY_DESTINATION}` -> `{COPY_DESTINATION}` with `SrcQueue: copy, DstQueue: graphics`; on the
  other queue, after the fence, the same barrier as the acquire, then any transition. Without the
  queues on the other queue's barrier D3D12 reports a layout mismatch at execution (1334); keep the
  layout through the hand-off, because a Vulkan release and acquire of an `EXCLUSIVE` image must
  name the same transition and a D3D12 copy list cannot transition. Mind the Vulkan sharing mode
  (fetch the COPY queue before creating the resources it touches, [Threads](#threads)).
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
  `NriQueue.External` (patch 0002; `VK_QUEUE_FAMILY_EXTERNAL` on Vulkan).
- UNORM rounding is the driver's: `0.1 * 255 = 25.5` reads 25 on NVIDIA Vulkan, 26 elsewhere. Allow
  one unit when comparing cleared or rendered pixels. VK and D3D12 readbacks differ at triangle
  edges.

## Zero allocation

The contract, measured on NONE (which does nothing per call, so any allocation is the binding's)
after one warm-up frame, over 300 frames: 0 managed bytes for both lists below
(`AllocationTests`, `ExtensionAllocationTests`); for the core frame also 0 host allocations,
reallocations and frees through `NriCountingAllocator`.

- Core frame: `NriCommandAllocator.Reset`, `cmd.Begin` / `End`, `NriDescriptorPool.Reset`,
  `AllocateDescriptorSets(layout, setIndex, span)`, `AllocateDescriptorSet`, `set.Update` /
  `UpdateRange` / `CopyRange`, `SetDescriptorPool`, `SetPipelineLayout`, `SetPipeline`, `SetDescriptorSet`, `SetRootConstants` (span and struct overloads),
  `Barrier` (global, buffer, texture), `SetViewports`, `SetScissors`, `BeginRendering` /
  `EndRendering`, `Draw`, `Dispatch`, `CopyBuffer`, `queue.Submit` (spans of command buffers,
  waits and signals, and the one-buffer `Submit(cmd, fence, value)`), `fence.Wait`, `fence.Value`, `SwapChain.AcquireNextTexture`,
  `SwapChain.QueuePresent` (the `OutOfDate` path allocates nothing either).
- Extension frame: `MeshShader.DrawMeshTasks` / `DrawMeshTasksIndirect`,
  `DescriptorHeap.WriteResourceDescriptors` / `WriteSamplerDescriptors` / `SetDescriptorHeap`,
  `Streamer.BeginStreamerCopyBatch` / `StreamBufferData` / `StreamConstantData` /
  `CopyStreamedData` / `EndStreamerFrame`, `Upscaler.DispatchUpscale`, `Imgui.CopyImguiData` /
  `DrawImgui`, `Video.DecodeVideo` (H.264 picture desc), `queue.Submit` with a swap chain,
  `LowLatency.SetLatencyMarker`.
- Not in the measured frame: `buffer.Map` / `Unmap` (NONE maps nothing; `Map` returns a span over
  NRI's pointer and allocates nothing itself).
- Allocate by design: object creation (`Create*`, `AllocateMemory`, `AllocateAndBindMemory`), the
  desc forms with arrays (`NriPipelineLayoutDesc`, `NriGraphicsPipelineDesc`, ...),
  `queue.UploadData`, the array-returning `AllocateDescriptorSets(layout, setIndex, count)`.
- `NriDescriptorSet` is a struct so that resetting the pool and reallocating sets every frame
  allocates nothing; the span overload is the per-frame path.
- Build the spans a frame passes (barriers, attachments, submits, writes) once, outside the frame.
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
| `RayTracing`, `MeshShader`, `DescriptorHeap`, `Streamer`, `SwapChain`, `LowLatency`, `Upscaler`, `Imgui`, `Video`, `SharedHandles` | yes | per device | per device |
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

- `NriBuffer.HasBuffer` (NRI's `HAS_BUFFER`) sizes a structure before its inputs exist. Upstream's
  validation layer dereferences it (access violation); patch 0003 fixes it in the shipped library.
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
- **Micromaps, D3D12: broken in v190.** NRI's prebuild query passes no input buffer or per-OMM
  descs (debug layer error 1162, SEH with break-on-error); without the debug layer, the BLAS build
  that links the micromap fails `Close` with `E_INVALIDARG`.
- Import by native object: `WrapperVK.CreateAccelerationStructureVK(desc, type)` /
  `WrapperD3D12.CreateAccelerationStructureD3D12(desc, type)`; caller names the type; needs
  `RayTracing`. The import reports the same GPU handle, native object, buffer and scratch sizes.

### SharedHandles (patch 0002)

```csharp
using var shared = device.SharedHandles.CreateSharedTexture(MemoryLocation.DEVICE, in desc);
using var handle = device.SharedHandles.GetSharedHandle(shared);   // NriSharedHandle: Handle, Type, Size, Offset; Dispose closes it
using var sharedFence = device.SharedHandles.CreateSharedFence();
using var fenceHandle = device.SharedHandles.GetSharedHandle(sharedFence);
using var binary = device.SharedHandles.CreateSharedBinaryFence();     // Vulkan only: binary semaphore
using var binaryHandle = device.SharedHandles.GetSharedHandle(binary);
```

| Backend | Texture handle | Fence handle |
|---|---|---|
| Vulkan, Windows | `OPAQUE_WIN32` NT handle | `OPAQUE_WIN32` NT handle (timeline or binary semaphore) |
| Vulkan, Linux | opaque fd | opaque fd |
| D3D12 | `D3D12_RESOURCE` NT handle | `D3D12_FENCE` NT handle |
| NONE | 0 | 0 |

- Needs `VK_KHR_external_memory_win32`/`_fd` **and** `VK_KHR_external_semaphore_win32`/`_fd`.
  lavapipe has external memory fd but not external semaphore fd; MoltenVK has neither fd
  extension (only `VK_EXT_external_memory_metal`). On both, `TrySharedHandles` is null.
- The handle is yours; dispose closes it. NVIDIA Vulkan returned the same NT handle value for two
  exports of the same memory: do not assume distinct values.
- A non-shared texture asked for a handle: `INVALID_ARGUMENT` (through the validation layer).
- A shared fence still works as a normal fence.
- **Binary shared fence** (`CreateSharedBinaryFence()`, i.e. `CreateSharedFence(SwapChainSemaphore)`):
  the signal-once/wait-once semaphore external compositors import as an opaque NT handle or fd.
  Vulkan only; D3D12 has no binary fences and throws `NriUnsupportedException` (no error message).
  The handshake rule is the binary semaphore's: every signal is followed by exactly one wait
  before the next signal, counting both sides of the export (yours signals, theirs waits, theirs
  signals, yours waits). In `NriFenceSubmit` the value is ignored (pass 0); never call `Value` or
  `Wait` on it. Import it into a binary `VkSemaphore` (an import replaces the payload, not the type).
- Verified imports: Vulkan texture into a second Vulkan device (byte-exact), Vulkan timeline
  semaphore both ways, Vulkan binary semaphore both ways (queue signal and queue wait on each side,
  validation clean), D3D12 fence and texture into a second D3D12 device.
- **D3D11 cannot open D3D12 shared textures**: NRI creates every D3D12 texture `TYPELESS`, and
  `ID3D11Device1::OpenSharedResource1` refuses it with `E_INVALIDARG`. Import into D3D12 or Vulkan.
- Exclusive Vulkan images need a release barrier with `DstQueue = NriQueue.External` before
  another device acquires them, and an acquire with `SrcQueue = NriQueue.External` coming back.

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
  creation hangs forever.
- NRI loads vendor DLLs by bare name, which searches the executable's directory. Under
  `dotnet app.dll` that is `dotnet.exe`'s directory: FSR fails, XeSS kills the process.
  `CreateUpscaler` therefore preloads each file by full path from `AppContext.BaseDirectory`, which
  makes both hosts work. Put the files beside your app's assembly.
- XeSS: D3D12 only. FSR: D3D12 and Vulkan. NIS: everywhere the Upscalers build runs.
- Raw DLSR creation without `nvngx_dlss*.dll`: clean `FAILURE` on both backends.

### LowLatency (Reflex)

- Needs `features.lowLatency` (NVAPI on D3D12, `VK_NV_low_latency2` on Vulkan) and a swap chain.
- D3D12: `GetLatencyReport` is all zeros after 72 frames; filled and ordered after 200. Do not read
  it in the first frames.
- **Vulkan: presenting from Vulkan with Reflex in a process breaks later D3D12 device creation**:
  `D3D12CreateDevice` returned `0x887A0007` (`DXGI_ERROR_DEVICE_RESET`), with or without
  `ALLOW_LOW_LATENCY` and `VSYNC`. Do not mix a Vulkan Reflex swap chain with D3D12 devices in one
  process.
- RivaTuner Statistics Server's implicit `VK_LAYER_RTSS` killed the process at the first Vulkan
  present until the binding started enumerating NRI's physical devices after creation
  (`NriDeviceOptions.VkEnumeratePhysicalDevicesAfterCreation`, default true; see
  [Device creation](#device-creation)). Leave the option on when RTSS may be installed.

### Video

- Request `VIDEO_DECODE` / `VIDEO_ENCODE` in `QueueFamilies`, or `Video` is `UNSUPPORTED`.
- H.264 decode capabilities, 64x64 NV12: D3D12 min = max = 64x64, no references; Vulkan 48x16 to
  4096x4096, 16 references.
- **H.264 encode, D3D12**: minimum 256x256; a 64x64 `GetVideoCapabilities` query draws debug error
  1312 (SEH with break-on-error). One IDR frame at QP 26 is 277 B starting `00 00 00 01 65`.
- **H.264 encode, Vulkan: hangs.** Minimum 160x64; resolve on a GRAPHICS queue; the encode feedback
  query never becomes available, and `ResolveVideoEncodeFeedback` (`vkCmdCopyQueryPoolResults` with
  WAIT) never completes. Do not use Vulkan encode in v190.
- `WriteVideoAnnexBParameterSets` for H.265 returns `UNSUPPORTED` for short- and long-term reference
  sets, VUI, scaling lists and tiles. The header does not say.
- `GetVideoAV1EncodeDecodeInfo` returns pointers into the result struct itself: keep the struct
  alive and unmoved while using them.

### Streamer

- `StreamerDesc.constantBufferMemoryLocation` is documented optional, but validation rejects its
  default: set it (e.g. `MemoryLocation.HOST_UPLOAD`).
- **`StreamerDesc.queuedFrameNum` counts the frame being filled.** It is the number of per-frame
  regions in the dynamic buffer: data streamed in frame N is overwritten by frame
  N + `queuedFrameNum`, so the GPU must be done with frame N before that frame streams anything.
  An application that waits for frame N - k to finish before it streams frame N's data passes k;
  one that streams before that wait passes k + 1. Upstream's header says NRI adds one for the
  current frame. The code stopped doing that in May 2025; the comment is stale and not patched.
  Measured: with 2, frame 2 reuses frame 0's offset.
- **Block-compressed textures**: `StreamTextureData`'s `DataRowPitch` is the distance between rows
  of blocks. With patch 0010 they are staged in rows of blocks. Upstream v190 staged texel rows,
  16 times the space for 4x4 blocks (a 64x64 BC1 mip took 32,768 B instead of 2,048), and read
  past the end of `Data`. The bytes still arrived intact, so only the footprint showed it.
- `EndStreamerFrame` discards every copy batch not yet recorded with `CopyStreamedData`: fill and
  record a batch within one streamer frame. A batch filled on another thread must reach the
  recording thread before that frame's `EndStreamerFrame`.
- **`StreamerDesc.dynamicBufferDesc.size` is the initial per-frame size of the dynamic buffer**
  (patch 0012; upstream's header says it is ignored, and the ring grows from empty). The buffer is
  allocated at creation as `size` rounded up to 64 KiB times `queuedFrameNum`; frames that stream
  less never allocate, and a frame that streams more still grows it: a new committed buffer, the
  old one destroyed `queuedFrameNum` frames later inside `EndStreamerFrame`, on the thread that
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
  further outcome can be added later; switch with a default branch. Every other failure throws. With patch 0009
  NRI reports no ERROR message for it (upstream v190 reports one on every resize).
- When it happens: NRI creates the swap chain with present scaling where the device has
  `swapchain_maintenance1` (`features.resizableSwapChain`), and then the driver keeps presenting
  through resizes. Measured on NVIDIA (RTX 4070 SUPER, Windows): an NRI-created device saw no
  `OUT_OF_DATE` across 8 resizes including a 0x0 client area; a wrapped device with only
  `VK_KHR_swapchain` got `OUT_OF_DATE` from present 6 times over the same 8 resizes, never from acquire.
- A wrapped Vulkan device that uses the swap chain needs `VK_KHR_surface`, the platform surface
  extension and `VK_KHR_get_surface_capabilities2` on its instance: NRI calls
  `vkGetPhysicalDeviceSurfaceCapabilities2KHR` unconditionally (an access violation without it).
- **With present scaling the requested extent need not be the window's current size** (patch
  0014). Vulkan accepts any extent in the surface's scaled range when the swap chain has present
  scaling; upstream v190 checked the unscaled range, which on Windows is exactly the current
  client size, and refused a size that lagged a resize, or a window minimised at creation, with
  `INVALID_ARGUMENT` ("swapChainDesc.width is out of [w, w] range"). Measured on NVIDIA: a 100x80
  swap chain on a 64x64 window creates with 100x80 textures and presents. Without scaling (a
  wrapped device with only `VK_KHR_swapchain`) the refusal stays, as Vulkan requires. A 0x0
  extent is refused either way: do not create or rebuild for a minimised window.
- **`SwapChainBits.LATEST_READY` with `VSYNC`** (patch 0017) asks for Vulkan's FIFO_LATEST_READY:
  paced to the display like FIFO, no tearing, but at each vblank the newest ready image is shown
  and older queued ones are dropped. It applies only when `device.Desc.features.latestReadySwapChain`
  is set (Vulkan with `VK_EXT_present_mode_fifo_latest_ready`); otherwise, and on D3D12, the bit is
  ignored and `VSYNC` gives FIFO (FIFO_RELAXED with `ALLOW_TEARING`). Without `VSYNC` the bit does
  nothing: NRI's uncapped order is MAILBOX (or IMMEDIATE with tearing) first, and FIFO_LATEST_READY
  only where MAILBOX is not offered. NRI never reports the present mode it chose, so the feature
  bit is the only answer. Measured on NVIDIA: the feature is 1 and a `VSYNC | LATEST_READY` swap
  chain presents with the Khronos layer clean.

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

- `PrismNRI.Native.NriApi` has every export.
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
  block-compressed ones included (patch 0008; NRI v190's table ended at 130 and returned `UNKNOWN`
  for all 52 BC, ETC2 and ASTC formats). BC1's RGB `VkFormat`s (131, 132) stay `UNKNOWN`.
- **`ConvertDXGIFormatToNRI(18)`** returns `RGB32_SINT`.
- `ConvertNRIFormatToDXGI` has no value for the 38 ETC2 / ASTC formats.
- **Linux and macOS: every DXGI conversion returns 0 / `UNKNOWN`**: the table is compiled only with
  a D3D backend.
- Annotations here are host-side, per thread; `NriCommandBuffer` and `NriQueue` have their own
  `BeginAnnotation` / `EndAnnotation` / `Annotation`.

## The validated test device

```csharp
using PrismNRI.Testing;

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
| `PRISMNRI_TEST_SOFTWARE=1` (implied by `GITHUB_ACTIONS=true`) | prefer WARP / lavapipe |
| `PRISMNRI_TEST_BACKEND=VK` | only that backend |
| `PRISMNRI_TRACE=1` | print each message as it arrives; the only record that survives a native crash |

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

- D3D12 on the Agility SDK needs `PrismNRI.Native.Agility` (or your own `AgilitySDK` directory and
  `D3D12AgilitySdkPath`).
- Visual C++ 2015-2022 runtime required, not shipped.
- `libxess.dll` is delay-loaded by the Upscalers `NRI.dll`: absent is fine until XeSS is created.
- WARP: D3D12 software adapter, `Architecture.SOFTWARE`; runs the whole suite including ray
  tracing (BLAS 65,536 B).

### Linux

- **Unproven on hardware.** Linux is verified on lavapipe (software Vulkan) only; no hardware
  Linux driver has run this code. The two intermittent Linux CI failures were test bugs, both
  identified and fixed in the tests: the ray-query "miss" was a readback after NRI's 5 s fence-wait
  timeout on a slow first submit, and the test-host SIGSEGV was a stale Vortice function table.
  Details: CLAUDE.md, "CI runners and flakes".
- NRI `dlopen`s `libvulkan.so.1` at device creation.
- **Loader older than the driver** (Ubuntu 24.04's stock 1.3.275 loader with Mesa 25's 1.4
  lavapipe): upstream NRI loads core 1.4 entry points the driver returns null for, and
  `CmdSetRootConstants` crashes (SIGSEGV at 0) while `HOST_TRANSFER` textures are rejected. Patch 0004
  clamps NRI's Vulkan version to the instance's; fixed in the shipped library. Such a device reports
  as Vulkan 1.3.
- lavapipe: no external semaphores (no `SharedHandles`); a first ray tracing submit can take
  seconds (JIT, see RayTracing).
- `NriAdapter.Uid` is built from the device UUID (no LUID on Linux).
- Building NRI with GCC 13 needs patch 0005 (included); clang is what CI uses.

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
- No `SharedHandles`, ray tracing, mesh shaders, video, descriptor heaps, shading-rate attachments.
- Apple Paravirtual device (GitHub's macOS VMs): `HOST_TRANSFER` texture binding segfaults;
  same-submission timestamp copies read 0. Not measured on physical Macs.

## Traps

| Trap | Effect | Section |
|---|---|---|
| D3D12 under `dotnet` without `D3D12AgilitySdkPath` / the Agility package | inbox runtime, `options22` warning | Device creation |
| Agility applied once | inbox runtime unreachable for the rest of the process | Device creation |
| Anything measured on NONE | NONE answers SUCCESS to everything and never validates | Device creation |
| `GetQueue` bad index | `INVALID_ARGUMENT`, no message | Device creation |
| Video without video queues requested | `Video` is `UNSUPPORTED` | Extensions: Video |
| Disposing the device first | throws, device stays usable | Lifetime |
| Freeing memory with a bound resource | validation error | Lifetime |
| `HOST_READ` / `HOST_WRITE` in a buffer barrier | validation error; fence wait is the visibility | Memory |
| `UploadData` on NV12 | VK VUID-...-07981, D3D12 error 1417 | Memory |
| `HOST_TRANSFER` on Apple Paravirtual | segfault in `vkBindImageMemory` | Memory |
| Sampler `mipBias` on MoltenVK | VUID-...-04467; `samplerLodBiasMax` is 0 | Memory |
| Non-zero `VkBindingOffsets` mismatch | descriptors bind to the wrong slots | Descriptors |
| Vulkan COMPUTE/COPY queue fetched late | resources switch to concurrent sharing | Threads |
| Pixel compare without tolerance | UNORM rounding differs by one | Recording |
| Texture barrier side with access `NONE` and a layout | D3D12: layout-only (`SYNC_NONE`, patch 0020); before the patch debug error 1331 and `End` throws | Recording |
| `ZeroBuffer` ordered with `CLEAR_STORAGE` | wrong on D3D12 (a copy there); use `COPY` (patch 0021 makes it cover the Vulkan fill) | Recording |
| `COPY_DESTINATION` on a D3D12 COPY queue, or `GENERAL` around a Vulkan copy | before patch 0022 D3D12 debug error 1334 and `End` throws; Vulkan refuses a copy in `GENERAL`; use the copy layouts and `SrcQueue` / `DstQueue` on the hand-off | Recording |
| Two command buffers of one allocator recording at once | D3D12: debug error 540, `Begin` throws | Recording |
| Same-submission timestamp copy on MoltenVK | reads 0 | Queries |
| One DXIL library for a D3D12 RT pipeline | `CreateRayTracingPipeline` faults | RayTracing |
| Single `Proceed()` in a ray query | software traversal may disagree | RayTracing |
| GPU work longer than 5 s behind `fence.Wait` | `TimeoutException` (NRI's `NRI_TIMEOUT_FENCE`); poll `fence.Value` | RayTracing |
| D3D12 micromaps | debug error 1162; `Close` fails `E_INVALIDARG` | RayTracing |
| Index-less OMM on Vulkan | VUID-...-pInfos-10904 (likely false positive) | RayTracing |
| `TrySharedHandles` null | lavapipe (no external semaphore fd), MoltenVK (no fd handles) | SharedHandles |
| D3D11 opening a D3D12 shared texture | `E_INVALIDARG` (TYPELESS) | SharedHandles |
| Two exports, same handle value | NVIDIA Vulkan returns the same NT handle | SharedHandles |
| `IsUpscalerSupported` true, DLL absent | `CreateUpscaler` throws naming the file | Upscaler |
| XeSS without the guard | lock held, next creation hangs | Upscaler |
| Vulkan Reflex + D3D12 in one process | `D3D12CreateDevice` 0x887A0007 | LowLatency |
| RTSS installed, `VkEnumeratePhysicalDevicesAfterCreation = false` or a wrapped device whose instance never ran `vkEnumeratePhysicalDevices` | process dies at first Vulkan present | Device creation |
| Reflex report in the first frames | all zeros | LowLatency |
| H.264 encode on Vulkan | feedback resolve hangs | Video |
| H.264 encode 64x64 on D3D12 | debug error 1312; minimum is 256x256 | Video |
| `StreamerDesc` default memory location | validation error | Streamer |
| Wrapped device from D3D12 without queue families | no queues | Wrappers |
| `Begin` on a wrapped command buffer | "already in the recording state" | Wrappers |
| Buffer barrier on a `CreateBufferVK` import | refused (usage NONE); use a global barrier | Wrappers |
| Raw `VkInstance` on macOS | MoltenVK hidden without portability enumeration | Wrappers |
| Raw `NriApi` device creation with null `AbortExecution` | `DebugBreak()` kills the process on any error | Wrappers |
| `ConvertVKFormatToNRI` on BC1 RGB (131, 132) | `UNKNOWN` (NRI has BC1 RGBA only; other compressed formats map, patch 0008) | Flat exports |
| Wrapped VK device without `VK_KHR_get_surface_capabilities2` | access violation in `CreateSwapChain` | SwapChain |
| Expecting `OUT_OF_DATE` on resize | none with present scaling (`features.resizableSwapChain`) | SwapChain |
| DXGI conversions on Linux / macOS | 0 / `UNKNOWN` | Flat exports |
| `Format.MAX_NUM` to a flat export | `ArgumentOutOfRangeException` | Flat exports |
| Khronos layer older than NRI's headers | "unknown VkStructureType" errors | Test device |
| Vulkan loader older than the driver | crash in `CmdSetRootConstants` (fixed by patch 0004) | Platform: Linux |
| Homebrew Vulkan on macOS | not found without `DYLD_FALLBACK_LIBRARY_PATH` and `VK_DRIVER_FILES` | Platform: macOS |
