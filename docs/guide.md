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
<PackageReference Include="PrismNRI" Version="0.1.0-alpha" ExcludeAssets="native" />  <!-- only with Upscalers -->
<PackageReference Include="PrismNRI.Native.Upscalers" Version="0.1.0-alpha" />
<PackageReference Include="PrismNRI.Native.Agility" Version="0.1.0-alpha" />
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
| `MessageHandler` | receives NRI and validation-layer messages. A throwing handler loses that one message. `device.LastError` holds the last error text and is attached to the `NriException` of the call that failed |
| `BreakOnError` | breaks only when a debugger is attached; otherwise errors never terminate the process |
| `Allocator` | `INriAllocator` for NRI's own host allocations; `NriCountingAllocator` counts them. A throwing allocator returns null, which NRI reports as out of memory |
| `ForwardAllocatorToThirdParty` | also routes VMA / D3D12MA host allocations; off by default (chatty) |
| `D3D12AgilitySdkPath` | null: the `AgilitySDK` directory beside the assembly when present (`NriDevice.DefaultAgilitySdkPath`), else inbox. `""`: force inbox |
| `VkBindingOffsets` | see [Descriptors and layouts](#descriptors-and-layouts) |
| `Robustness`, `DeviceLostInfoLevel`, `EnableD3D12RayTracingValidation` | only on `NriDevice.Create`, not on the wrapped routes |

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
- **Software adapters are enumerated**: WARP and lavapipe have `Architecture.SOFTWARE`. Filter
  them out when picking a GPU.
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
- Not disposable: `NriQueue` (the device owns it), `NriDescriptorSet` (the pool owns it;
  `pool.Reset()` or disposing the pool invalidates every set).
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
- `pool.Reset()` invalidates every set. D3D12 heap offsets for three consecutive sets: (0,0),
  (3,1), (6,2); back to (0,0) after `Reset`. Vulkan: (0,0) always.
- `Nri.FitPipelineLayoutSettingsIntoDeviceLimits(device.Desc, wanted)` clamps counts and sizes in
  NRI's priority order.

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
  recording allocates 0 managed bytes and 0 host allocations per call (measured over 300
  iterations, every extension's per-frame path included).
- Barriers are never inserted for you. `AccessStage` / `AccessLayoutStage` are NRI's before/after.
- Fences are timelines on both backends: `fence.Wait(value)`, `fence.Value`, and in a submit
  `NriFenceSubmit(fence, value, stages)`.
- Queue ownership transfer to or from another API / process: `SrcQueue` / `DstQueue` =
  `NriQueue.External` (patch 0002; `VK_QUEUE_FAMILY_EXTERNAL` on Vulkan).
- UNORM rounding is the driver's: `0.1 * 255 = 25.5` reads 25 on NVIDIA Vulkan, 26 elsewhere. Allow
  one unit when comparing cleared or rendered pixels. VK and D3D12 readbacks differ at triangle
  edges.

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
- **lavapipe ray queries** miss intermittently on some hosts (wrong hit map, no validation error,
  same packages); never seen on hardware or on WARP. Do not treat lavapipe RT results as ground
  truth.
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
```

| Backend | Texture handle | Fence handle |
|---|---|---|
| Vulkan, Windows | `OPAQUE_WIN32` NT handle | `OPAQUE_WIN32` NT handle (timeline semaphore) |
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
- Verified imports: Vulkan texture into a second Vulkan device (byte-exact), Vulkan timeline
  semaphore both ways, D3D12 fence and texture into a second D3D12 device.
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
  present. Disable implicit layers if it is installed.

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

### SwapChain

- `NriWindow` (NRI's `Window`) is a struct of five platform members side by side, not a union. NRI
  reads the member of the running OS (`Windows` only on Windows).

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
- **`ConvertVKFormatToNRI`** returns `UNKNOWN` for all 52 block-compressed `VkFormat`s (BC, ETC2,
  ASTC): NRI's table ends at 130.
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

- **Unproven and not stable.** Linux is verified on lavapipe (software Vulkan) only; no hardware
  Linux driver has run this code. Linux CI is flaky, with two open issues: an intermittent
  lavapipe ray-query miss (wrong hit map, no validation error, varies by CI host) and one
  unexplained test-host crash (SIGSEGV, not reproduced). A red Linux job can be either. Details:
  CLAUDE.md, "CI runners and flakes".
- In practice: ray query results on lavapipe are not trustworthy. Use a hardware driver for
  anything that matters.
- NRI `dlopen`s `libvulkan.so.1` at device creation.
- **Loader older than the driver** (Ubuntu 24.04's stock 1.3.275 loader with Mesa 25's 1.4
  lavapipe): upstream NRI loads core 1.4 entry points the driver returns null for, and
  `CmdSetRootConstants` crashes (SIGSEGV at 0) while `HOST_TRANSFER` textures are rejected. Patch 0004
  clamps NRI's Vulkan version to the instance's; fixed in the shipped library. Such a device reports
  as Vulkan 1.3.
- lavapipe: no external semaphores (no `SharedHandles`); ray queries miss intermittently (see
  RayTracing).
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
| Same-submission timestamp copy on MoltenVK | reads 0 | Queries |
| One DXIL library for a D3D12 RT pipeline | `CreateRayTracingPipeline` faults | RayTracing |
| Single `Proceed()` in a ray query | software traversal may disagree | RayTracing |
| lavapipe ray query | intermittent misses | RayTracing |
| D3D12 micromaps | debug error 1162; `Close` fails `E_INVALIDARG` | RayTracing |
| Index-less OMM on Vulkan | VUID-...-pInfos-10904 (likely false positive) | RayTracing |
| `TrySharedHandles` null | lavapipe (no external semaphore fd), MoltenVK (no fd handles) | SharedHandles |
| D3D11 opening a D3D12 shared texture | `E_INVALIDARG` (TYPELESS) | SharedHandles |
| Two exports, same handle value | NVIDIA Vulkan returns the same NT handle | SharedHandles |
| `IsUpscalerSupported` true, DLL absent | `CreateUpscaler` throws naming the file | Upscaler |
| XeSS without the guard | lock held, next creation hangs | Upscaler |
| Vulkan Reflex + D3D12 in one process | `D3D12CreateDevice` 0x887A0007 | LowLatency |
| RTSS installed | process dies at first Vulkan present | LowLatency |
| Reflex report in the first frames | all zeros | LowLatency |
| H.264 encode on Vulkan | feedback resolve hangs | Video |
| H.264 encode 64x64 on D3D12 | debug error 1312; minimum is 256x256 | Video |
| `StreamerDesc` default memory location | validation error | Streamer |
| Wrapped device from D3D12 without queue families | no queues | Wrappers |
| `Begin` on a wrapped command buffer | "already in the recording state" | Wrappers |
| Buffer barrier on a `CreateBufferVK` import | refused (usage NONE); use a global barrier | Wrappers |
| Raw `VkInstance` on macOS | MoltenVK hidden without portability enumeration | Wrappers |
| Raw `NriApi` device creation with null `AbortExecution` | `DebugBreak()` kills the process on any error | Wrappers |
| `ConvertVKFormatToNRI` on compressed formats | `UNKNOWN` | Flat exports |
| DXGI conversions on Linux / macOS | 0 / `UNKNOWN` | Flat exports |
| `Format.MAX_NUM` to a flat export | `ArgumentOutOfRangeException` | Flat exports |
| Khronos layer older than NRI's headers | "unknown VkStructureType" errors | Test device |
| Vulkan loader older than the driver | crash in `CmdSetRootConstants` (fixed by patch 0004) | Platform: Linux |
| Homebrew Vulkan on macOS | not found without `DYLD_FALLBACK_LIBRARY_PATH` and `VK_DRIVER_FILES` | Platform: macOS |
