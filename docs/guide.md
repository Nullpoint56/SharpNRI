# PrismNRI — Guide

How to use it: what to type, and what NRI does that will surprise you. Every code sample is the
real API; the traps at the end are the ones that bit somebody, with the section of
[findings.md](findings.md) that measured each.

---

## Contents

- [The three packages](#the-three-packages)
- [Creating a device](#creating-a-device)
- [Lifetime](#lifetime)
- [Resources and memory](#resources-and-memory)
- [Descriptors and pipeline layouts](#descriptors-and-pipeline-layouts)
- [Recording and submitting](#recording-and-submitting)
- [Extensions](#extensions)
- [Threads](#threads)
- [The validated device for your own tests](#the-validated-device-for-your-own-tests)
- [Traps worth knowing](#traps-worth-knowing)

---

## The three packages

```xml
<PackageReference Include="PrismNRI" Version="0.1.0-alpha" />          <!-- the binding, carries NRI.dll and its SDKs -->
<PackageReference Include="PrismNRI.Descs" Version="0.1.0-alpha" />    <!-- enums and descs only, no native code -->
<PackageReference Include="PrismNRI.Testing" Version="0.1.0-alpha" />  <!-- a validated headless device, for tests -->
```

`PrismNRI` references `PrismNRI.Descs`, so a renderer takes the first and gets both. A tool that
only builds pipeline layouts takes `PrismNRI.Descs` alone. Everything generated from NRI's headers
is in the `PrismNRI.Native` namespace and keeps NRI's names: `TextureDesc`, `Format.RGBA8_UNORM`,
`StageBits.COMPUTE_SHADER`. Everything hand-written is in `PrismNRI` and starts with `Nri`.

## Creating a device

```csharp
using PrismNRI;
using PrismNRI.Native;

foreach (var adapter in NriAdapter.Enumerate())
    Console.WriteLine(adapter);                 // name, vendor, architecture, supported backends

using var device = NriDevice.Create(new NriDeviceOptions
{
    GraphicsApi = GraphicsAPI.D3D12,            // or VK, or NONE
    Adapter = NriAdapter.Enumerate().First(a => a.Supports(GraphicsAPI.D3D12)),
    QueueFamilies = [new NriQueueFamily(QueueType.GRAPHICS), new NriQueueFamily(QueueType.COMPUTE)],
    EnableNriValidation = true,                 // NRI's own checks
    EnableGraphicsApiValidation = true,         // Vulkan validation layers / D3D12 debug layer
    MessageHandler = (in NriMessage m) => Console.Error.WriteLine(m),
});

ref readonly DeviceDesc caps = ref device.Desc; // limits, tiers, features, the adapter
```

`NriDeviceOptions` mirrors `DeviceCreationDesc` field for field. The message handler receives
everything NRI and the validation layers say; the device also keeps the last error and attaches it
to the `NriException` of the call that failed. On D3D12 the Agility SDK staged beside the assembly
loads automatically ([design §4.4](design.md#44-the-agility-sdk-is-loaded-by-path-through-a-patch));
set `D3D12AgilitySdkPath = ""` to force the inbox runtime.

`GraphicsAPI.NONE` is a real device that accepts everything and does nothing. Use it to test
lifetime and allocation behaviour on a machine with no GPU, and nowhere else.

## Lifetime

Every object is an `NriObject`: created by one call, destroyed by `Dispose`, no finalizer. NRI has
no reference counts and its `Destroy*` calls are not thread safe, so the binding does not guess for
you. Two rules follow:

- Dispose children before their device. `NriDevice.Dispose` throws while any object created on it
  is alive, and the device stays usable; `device.LiveObjects` says how many.
- A forgotten `Dispose` leaks until process exit. It never corrupts.

```csharp
using var device = NriDevice.Create(...);
using var fence = device.CreateFence();       // disposed before the device by `using` order
```

Queues and descriptor sets are not disposable: the device owns its queues; a pool owns its sets
and `Reset()` or disposing the pool invalidates all of them.

## Resources and memory

NRI's memory model is Vulkan's, with D3D12-style shortcuts.

```csharp
// Committed: NRI allocates through VMA / D3D12MA.
using var vertices = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD,
    new BufferDesc { size = 1024, usage = BufferUsageBits.VERTEX });
vertices.Map().Clear();                        // persistent mapping; Unmap flushes if needed
vertices.Unmap();

// Vulkan style: create, measure, allocate, bind.
using var texture = device.CreateTexture(new TextureDesc
{
    type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE,
    format = Format.RGBA8_UNORM, width = 256, height = 256,   // mipNum, layerNum, sampleNum default to 1
});
var requirement = texture.GetMemoryDesc(MemoryLocation.DEVICE);
using var memory = device.AllocateMemory(new AllocateMemoryDesc { size = requirement.size, type = requirement.type });
device.BindTextureMemory(texture, memory);

// Or let NRI group a set of resources into as few allocations as it can.
NriMemory[] allocations = device.AllocateAndBindMemory(MemoryLocation.DEVICE, [texture], [vertices]);
```

Dispose resources before the memory they are bound into; NRI's validation refuses the other
order. Views are descriptors: `texture.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM)`,
`buffer.CreateView(BufferView.STORAGE_STRUCTURED_BUFFER)`, `device.CreateSampler(in samplerDesc)`.

## Descriptors and pipeline layouts

A pipeline layout is NRI's desc with arrays where NRI has pointers; the field names are the same.

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

`baseRegisterIndex` is the HLSL register on D3D12 and, on Vulkan, the SPIR-V binding after the
device's `VkBindingOffsets` are added per descriptor class (`s`, `t`, `b`, `u`). With all-zero
offsets, which is the default, the binding is the register index and `registerSpace` is the
descriptor set. Shaders compiled with per-class shifts need the same shifts in the options.

## Recording and submitting

One method per NRI command, same name without the `Cmd` prefix, same argument order. Arrays that
name NRI objects are spans of small records; nothing on this path allocates.

```csharp
var queue = device.GetQueue(QueueType.GRAPHICS);
using var allocator = queue.CreateCommandAllocator();
using var commandBuffer = allocator.CreateCommandBuffer();
using var fence = device.CreateFence();

commandBuffer.Begin(pool);
commandBuffer.Barrier(new NriTextureBarrier(target, default,
    new AccessLayoutStage { access = AccessBits.COLOR_ATTACHMENT, layout = Layout.COLOR_ATTACHMENT, stages = StageBits.COLOR_ATTACHMENT }));
commandBuffer.BeginRendering([new NriAttachment(targetView, clearValue, LoadOp.CLEAR, StoreOp.STORE)]);
commandBuffer.SetViewport(new Viewport { width = 64, height = 64, depthMax = 1 });
commandBuffer.SetScissor(new Rect { width = 64, height = 64 });
commandBuffer.SetPipelineLayout(BindPoint.GRAPHICS, layout);
commandBuffer.SetPipeline(pipeline);
commandBuffer.Draw(3);
commandBuffer.EndRendering();
commandBuffer.End();

queue.Submit(commandBuffer, fence, signalValue: 1);
fence.Wait(1);
```

Fences are timelines on both backends: wait and signal by value, from the host or in a submit
(`NriFenceSubmit`). Barriers are explicit and never inserted for you; `AccessLayoutStage` is NRI's
before/after triple.

## Extensions

Each NRI extension is acquired once per device on first use. `TryX` is null when the backend
lacks it; `X` throws `NriUnsupportedException`.

```csharp
if (device.TryRayTracing is { } rt)
{
    using var blas = rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, new NriAccelerationStructureDesc
    {
        Type = AccelerationStructureType.BOTTOM_LEVEL,
        Geometries = [NriBottomLevelGeometry.FromTriangles(new NriBottomLevelTriangles(NriBuffer.HasBuffer, 0, 3, 12, Format.RGB32_SFLOAT))],
    });
    rt.BuildBottomLevelAccelerationStructure(commandBuffer, new NriBuildBottomLevelAccelerationStructure(blas, [geometry], scratch));
}

using var shared = device.SharedHandles.CreateSharedTexture(MemoryLocation.DEVICE, in desc);   // patch 0002
using var handle = device.SharedHandles.GetSharedHandle(shared);                             // NT handle / fd, yours to close
```

Available: `RayTracing`, `SharedHandles`, `MeshShader`, `DescriptorHeap`, `Streamer`, `SwapChain`,
`LowLatency`, `Upscaler`, `Imgui`, `Video`, `WrapperVK`, `WrapperD3D12`. The two wrappers and the
`GetNativeObject` accessors on every object are the native escape hatch; use them last.

## Threads

NRI's contract, unchanged: `Create*` calls are free-threaded; `Destroy*` and every `Cmd*` are
externally synchronized, meaning one thread at a time per object. `QueueSubmit` is free-threaded.
`GetQueue` for compute or copy queues on Vulkan switches resources created afterwards without
`queueExclusive` to concurrent sharing, so ask for every queue family at device creation and get
the queues before creating resources that cross them.

## The validated device for your own tests

```csharp
using PrismNRI.Testing;

if (NriTestDevice.Unavailable(GraphicsAPI.VK) is { } reason) Skip(reason);
using var fixture = NriTestDevice.Create(GraphicsAPI.VK);
var device = fixture.Device;
// ... exercise ...
fixture.ThrowIfErrors();     // every validation-layer error, listed
```

Set `PRISMNRI_TEST_SOFTWARE=1` to prefer WARP or lavapipe, `PRISMNRI_TEST_BACKEND=VK` to run one
backend, `PRISMNRI_TRACE=1` to see every message as it arrives, which is the only record that
survives a native crash.

## Traps worth knowing

| Trap | What happens | Where measured |
|---|---|---|
| A host barrier on a readback buffer | NRI's validation rejects `HOST_READ`/`HOST_WRITE` access on any buffer barrier; the fence wait is the host visibility | findings §6.8 |
| Freeing memory with a resource still bound | validation error; dispose resources first | §7.3 |
| `GetQueue` with a bad index | `INVALID_ARGUMENT` with no message; NONE answers success for anything | §6.6 |
| Zero dimensions in `TextureDesc` | height, depth, mipNum, layerNum, sampleNum become 1 | §7.3 |
| The clear colour you asked for is one unit off | UNORM rounding is the driver's; allow ±1 | §6.9 |
| D3D12 without the Agility SDK | inbox runtime, `OPTIONS22` missing; make sure the `AgilitySDK` directory ships beside the assembly | §6.4, §7.1 |
| `NriBuffer.HasBuffer` under NRI validation | crashed before patch 0003; fixed in the shipped library | §7.5 |
| Same handle value from two `GetSharedHandle` calls on Vulkan | NVIDIA returns the same NT handle value for the same memory; do not assume distinctness | §7.4 |
| `libxess.dll` missing beside `NRI.dll` | NRI does not load at all: it is a load-time import | §6.11 |
| Visual C++ runtime missing | `NRI.dll` needs the 2015–2022 runtime; it is not shipped | §6.11 |
| Vulkan loader older than the driver (Ubuntu 24.04's stock 1.3 loader with Mesa 25) | crashed in `CmdSetRootConstants` and rejected `HOST_TRANSFER` textures before patch 0004; fixed in the shipped library | §8.4 |
| Khronos validation layer older than NRI's headers | every newer struct NRI chains is an `[ERROR]` message; match the layer to the headers (`tools/ci/fetch-vulkan-layer.sh`) | §8.3 |
| `TrySharedHandles` null on Vulkan | the device lacks external memory or external semaphores (lavapipe has no `VK_KHR_external_semaphore_fd`) | §8.5 |
