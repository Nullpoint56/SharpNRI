# SharpNRI: oddities and caveats

Part of the working notes (`AGENTS.md`, which indexes every section). Measurements were taken on NRI v190 unless they name a later NRI commit or version; source line
numbers belong to the NRI version named. Hardware figures are NVIDIA (RTX 4070 SUPER) unless
another adapter is named. Section names in parentheses name a section of `AGENTS.md` or of a file
it indexes.

Each entry: the fact, the evidence, where it is handled, and the caller's rule. N-numbers name
reports in `docs/upstream.md`. Entries marked "fixed upstream" were NRI defects on v190, fixed in
v191; they stay for the test that holds the fix and to recognise a regression.

## Generator and ABI

- **ClangSharp 21.1.8.4, C++ mode**: 373 files, 7,661 lines, 152 errors as generated, one cause:
  `NriBits` operator overloads (`~ & | &= |=`, five per enum, 38 enums) become static helpers that
  do not compile; removed. `NriBit(n)` = `(1u << n)` does not convert inside byte/ushort enums: `U`
  suffix dropped for those only. `[Flags]` added to every `NriBits` enum, read from the headers.
  NRI's pointer constant `HAS_BUFFER` came out as a mutable public static (any caller could reassign
  it for the process, CA2211): made `static readonly`. ClangSharp's redundant `using System;` (two
  helper attributes; both projects use implicit usings) and its unused `using static` of
  `GraphicsAPI` in `NriApi.cs` (the one candidate, `ALL` in a default value, resolves to `NriApi`'s
  own constant) are dropped, so the layer is clean under the lint rules (Lint). NRI's header comments
  become `/// <summary>` on the types and members they describe (`tools/generate-native-docs.ps1`,
  step 7; at v192 115 types and 928 of 929 member comments), matched by NRI's names:
  the comment block above a declaration plus its trailing `//`, whitespace folded, the format table's
  glyph rows and column legend left out; anonymous unions' members get none. All in
  `tools/generate-native.ps1` only.
- NRI's headers have no C++ default member initializers (`NriDefault` unused), so generator-dropped
  defaults cannot happen. C `bool` is `byte` with `[NativeTypeName("bool")]`.
- **ABI oracle** (`NativeAbiTests`, clang++ 22 / AppleClang) at v192: 275 structs, 1,880 offsets,
  56 fixed buffers, 95 enums, 4 bitfields (exact bytes written), 238 function-pointer fields, 19
  `DllImport`s; zero mismatches on win-x64 (arm64 macOS last compared before patch 0017). Blind to
  calling convention, `const`, reference vs pointer, `bool` vs `uint8_t`. The anonymous union in
  `BottomLevelGeometryDesc` is checked as outer offsets. The probe also assigns `EXTERNAL_QUEUE` to
  a `Queue*` (Shared handles).
- **`nriGetInterface` checks the table size itself**: a mismatch is `INVALID_ARGUMENT` after both
  sizes are reported through the message callback, so a drifted table fails on acquisition.
- **Regen drift and line endings.** With `core.autocrlf=true` the checkout is CRLF while ClangSharp
  writes LF, so `git status` showed every file modified with identical content. Fixes:
  `.gitattributes` (`* text=auto`; `patches/*` `-text`, applied byte for byte; shader binaries
  `binary`), LF from the generator, and the drift check `git add -A -- dotnet/Native` + `git diff
  --cached --quiet` (through the clean filter).

## Device creation and callbacks

- **Layer settings for one instance (patch 0038 until v192; upstream since PR #313, d296386a).** NRI chains only its own
  `VkDebugUtilsMessengerCreateInfoEXT` and (with `enableGraphicsAPIValidation`)
  `VkValidationFeaturesEXT` into the instance, so otherwise the Khronos layer is configurable only
  through environment variables (process-wide, read at instance creation, and not updated by .NET
  on Linux and macOS, hence the tests that skip there) or `vk_layer_settings.txt`. The caller's
  chain goes last (NRI cannot write into its tail) and must not repeat NRI's two structures
  (VUID-VkInstanceCreateInfo-sType-unique). Binding: `NriDeviceOptions.VkInstanceCreateInfoNext`, a
  raw pointer owned by the caller until `NriDevice.Create` returns; `CreateFromVulkan` has none.
  `LayerSettingsTests` (NVIDIA and lavapipe): `validate_best_practices` = `VK_TRUE` gives the
  committed 64 KiB buffer's `BestPractices-vkBindBufferMemory-small-dedicated-allocation` warning, a
  device without it does not; no `VK_EXT_layer_settings` needed; runs on Linux too.
- **NRI version guard** (`Interop/NriVersionCheck.cs`). NRI exports nothing that
  states its version before a device exists, so every creation path (`NriDevice.Create`, the two
  wrapped forms) takes the new device through `Adopt`: a table `nriGetInterface` refuses by size, a
  `DeviceDesc::nriVersion` other than SharpNRI's `NriVersion` metadata, or a loaded `SharpNRI.Descs`
  whose `NriVersion` metadata differs destroys the native device (before the bridges) and throws
  `NriException` `INVALID_SDK` naming both versions and the loaded library's path. So does a loaded
  `SharpNRI.Descs` package other than the one SharpNRI depends on (`AssemblyMetadata`
  `SharpNRIDescsVersion` against the loaded assembly's informational version without `+<commit>`): a
  direct reference to another version wins the restore with only `NU1608`, and within one NRI version
  the NRI check passes. The package's `buildTransitive/SharpNRI.targets` fails that build first
  (`SHARPNRI005`, the resolved version from `project.assets.json` against `SharpNRI.props`, written at
  pack time; checked by hand 2026-11-07). The constants `Nri.NriVersion` and `NriHeaders.Version` are
  compiled into callers; `Nri.BindingVersion`, `DescsVersion`, `BindingNriVersion` and
  `DescsNriVersion` read the loaded assemblies. A stale `NRI.dll`
  of another tag on the load path otherwise misread every renumbered enum silently (v191 renumbered
  `StageBits` / `AccessBits`). Blind on `next` between tags: main keeps its `NRI_VERSION` while enums
  move (Branches). `VersionCheckTests` overrides the expected native or descs version for the
  creating thread and checks the refusal, the message and that the allocator ends at 0 live
  allocations; the table diagnosis is checked on a real `nriGetInterface` size refusal. Not run by any
  test: `Adopt` catching a constructor failure (it needs a library of another version on the load
  path), and the missing-library handler firing for real (NRI is loaded in the test process; its
  `Resolve` is called directly, and the throw reaching the caller was checked by hand, 2026-11-07).
- **Null `AbortExecution` kills the process.** NRI's default handler calls `DebugBreak()` for every
  report with a positive result, i.e. every ERROR except `DEVICE_LOST`, `OUT_OF_DATE`,
  `INVALID_SDK`, `NOT_READY` and `EXT_ERROR` (v192: a failed third-party call, NRI PR #324;
  Upscalers and vendor runtimes): without a debugger, exit `0x80000003` before the call returns (a vanished test
  host, exit 0 through a pipe). `MessageBridge` always supplies its own thunk, breaking only when
  `BreakOnError` and a debugger is attached.
- **`GetQueue` out of range**: `INVALID_ARGUMENT` on VK and D3D12 with no message; NONE returns
  `SUCCESS` even for index 1000. Tests wanting a message must provoke the validation layer.
- **Adapter enumeration (patch 0024 until v192; upstream since PR #313, d296386a).** `nriEnumerateAdapters` with `ALL` runs the Vulkan pass
  (a throwaway VkInstance, API 1.2, so the loader loads and unloads every implicit layer), then the
  D3D pass (a DXGI factory, a null-device `D3D12CreateDevice` probe per adapter, and for an adapter
  Vulkan did not list, real devices for the architecture and video-queue queries, on the inbox
  runtime), then WGPU / Metal; nothing is cached. For an adapter both list, the D3D pass only adds
  D3D bits, so the desc is the Vulkan pass's: a D3D12 device created with no adapter gets queue
  counts 16, 8, 2 (Vulkan's families) where a D3D-only desc says 4, 4, 4. `nriCreateDevice` with a
  desc reads `supportedGraphicsAPIs` (must carry the API), `uid` (VK matches it on its own
  instance, D3D12 `EnumAdapterByLuid`) and clamps `queueNum`; `DeviceVK` never refills it. Measured
  (`AdapterEnumerationTests`, children): VK-only 67.0 ms first, 5.3 ms second, `D3D12Core.dll` not
  loaded, also after a Vulkan device with no adapter (104.5 ms); unfiltered 125.1 / 69.1 ms,
  `D3D12Core.dll` loaded; the pick is the same either way. Binding:
  `NriAdapter.Enumerate(GraphicsAPI)`; `NriDeviceOptions.Adapter = null` passes null, so NRI's null
  path decides. A filtered desc carries only the asked APIs' bits (plus `NONE`): never hand it to
  another API's device creation.
- **RivaTuner's implicit layer killed presenting Vulkan processes; NRI enumerates physical devices
  (patch 0045 until v192; upstream since PR #319, 331e3971).** `VK_LAYER_RTSS` (RivaTuner Statistics Server: MSI Afterburner's overlay, also
  CapFrameX's and HWiNFO's in-game display) maps physical devices to instances from
  `vkEnumeratePhysicalDevices` (not the `Groups` variant), and its overlay uses the map at the first
  `vkQueuePresentKHR`. v191 enumerates only groups (`DeviceVK.hpp:791`, `Creation.cpp:422`), a
  leftover of the mGPU support dropped in v1.124 (7bb624d): it uses only each group's
  `physicalDevices[0]` and creates the device without `VkDeviceGroupDeviceCreateInfo`. So: the loader
  logs `vkEnumeratePhysicalDevices: Invalid instance
  [VUID-vkEnumeratePhysicalDevices-instance-parameter]` and the process exits 0xC0000409 inside the
  first `QueuePresent`, with or without any validation (RTSS 7.3.5, 2026-11-06, a scratch app on
  the binding: NRI validation off and on, the Khronos layer off and on, hidden and shown window, 16
  runs; `DISABLE_RTSS_LAYER=1` survives). It hits release builds of any NRI Vulkan application
  presenting while RTSS runs; applications that call `vkEnumeratePhysicalDevices` themselves, as most
  engines do, never see it; the 7.3.7 release notes name no fix. Until 0045 the binding made one
  extra count-then-fill call on NRI's instance after creation (an option, now removed); the patch
  instead replaces the group enumeration in both places, the same number of calls. Difference on
  linked GPUs (a group of several physical devices): each is listed, merged by uid as before.
  `OverlayLayerTests.An_NRI_Vulkan_device_presents_under_the_RivaTuner_Statistics_Server_layer` (a
  child; skips unless the loader reports `VK_LAYER_RTSS` and `RTSS.exe` runs): red without the patch
  (0xC0000409 before the first of 8 presents), green with it, and green on main a20efc8f with no patch. A wrapped device's instance is the
  caller's: it must have run `vkEnumeratePhysicalDevices` itself. An instance-count cause is refuted.
- **NRI.dll loads no D3D module by itself; the Vulkan loader loads `dxgi.dll`.**
  `AdapterEnumerationTests.A_Vulkan_only_enumeration_and_device_load_no_D3D_module_of_NRI`: NRI.dll
  loaded, nothing else: none of `d3d12.dll`, `dxgi.dll`, `D3D12Core.dll`; VK-only enumeration and a
  Vulkan device: `dxgi.dll` only; unfiltered enumeration: all three. A bare `vkCreateInstance`
  without NRI loads `dxgi.dll` (Khronos loader 1.4.341, even with all drivers and implicit layers
  disabled), so the test holds NRI to adding nothing to that.
- **Khronos messages carry the id name (patch 0015)**: `[1809909096]
  BestPractices-vkCreateImage-sharing-mode-exclusive: vkCreateImage(): ...`. The 1.4.3xx layer's
  `pMessage` no longer starts with "Validation Warning: [ name ]". NRI drops id-0 (loader) messages.
- **NONE is never validated** (NRI wraps only when `enableNRIValidation && graphicsAPI != NONE`).
  NONE answers `{size=1}` for every buffer, one static texture desc, `MapBuffer` null, 0
  allocations from `AllocateAndBindMemory` (`ImplNONE.cpp`).
- Software adapters are enumerated with `Architecture::SOFTWARE` (WARP, lavapipe);
  `NriTestDevice.PreferSoftware` picks them. Queue counts are trimmed to what was created, so
  `Video` is `UNSUPPORTED` without video queues; a device wrapped from a raw D3D12 device with no
  queue families has no queues (NRI's own creation defaults to one graphics queue). On Linux
  `deviceLUIDValid` is false and `AdapterDesc::uid` comes from the UUID (`ConstructUid`): match as
  NRI does, not on LUID.
- Rider flags `?.` on a pointer-returning member as unliftable; Roslyn compiles it. The allocator
  thunks use explicit null checks (`AllocatorBridge.cs`).
- **A failing allocator: `OUT_OF_MEMORY` for objects, a crash in containers.** Fixed upstream (N10,
  9d276c3, aa23763): v190 called `Create` on whatever `Allocate` returned (0xC0000005 in
  `CreateFence`); now `CreateImplementation` returns `OUT_OF_MEMORY` and
  `DeviceVal::WrapImplementation` destroys the backend object when its wrapper allocation fails.
  `AllocationFailureTests` (a child per backend and validation setting) fails each allocation of
  `CreateFence`, `CreateBuffer`, `CreateTexture`, `AllocateMemory`, `CreateCommandAllocator`,
  `CreateCommandBuffer`, `CreateQueryPool` and `CreateSampler` in turn: `OUT_OF_MEMORY`, no leak,
  the device then submits. Not covered: NRI's containers allocate through the `noexcept`
  `StdAllocator::allocate`, which cannot report null; device creation's first allocation
  (`Vector<QueueFamilyDesc>`, 0xC0000005) and a D3D12 device's first sampler (descriptor batches)
  are such. So an `INriAllocator` must not fail; the bridge's null covers objects only. A throwing
  `Free` is harmless (every free threw, `Live` ended 0; `InteropTests`).
- **Forwarding to the third-party allocators**: Vulkan 31 allocations without
  `ForwardAllocatorToThirdParty`, 10,840 plus 210 reallocations with it (only VMA reaches
  `OnReallocate`); D3D12 18 and 26; `Live` 0 always. `NriCountingAllocator.Reallocate(p, 0)` frees
  and returns null, like Vulkan's contract and NRI's Windows default; NRI's POSIX `AlignedRealloc`
  returns a new block for size 0 (never seen in practice).
- **Queue priorities** (fixed upstream, #321, a20efc8f). v191's D3D12 made a queue HIGH only above
  0.5 while `NRIDeviceCreation.h` said "high > 0" (`QueueD3D12.hpp:5`, `priority > 0.5f`); v192
  makes it HIGH above 0, and the header now says "D3D12: low == normal".
  `DeviceTests.Queue_priorities_reach_the_D3D12_command_queues` holds 0.25 to HIGH. Vulkan clamps at
  0, so low equals normal, and 2 draws
  VUID-VkDeviceQueueCreateInfo-pQueuePriorities-00383. A list of the wrong length throws
  `ArgumentException` before NRI is called.
- **Exception texts.** An exception carries the error text reported on the calling thread since the
  binding last checked a result there: the failing call's own, or none. `MessageBridge` stores each
  ERROR's text in a `[ThreadStatic]`; every check (`NriException.Check`, also behind
  `NriBuffer.Map`) and the device creations take it on failure and clear it on
  success. NRI and the layers report synchronously on the calling thread. No lock; success stays at
  0 managed bytes. A device-wide text was wrong (a silent FAILURE of `GetVideoAV1EncodeDecodeInfo`
  carried an earlier `EncodeVideo` message). `InteropTests`: a refused `CreateBuffer` carries
  "'size' is 0"; a following message-less `GetQueue` failure carries nothing, as does one after an
  error on another thread. A text from an unchecked call (`Cmd*`) goes to the next failure on its
  thread. `LastError` is the device's last text from any thread. `NriException.For` builds every
  failure, `NriDeviceLostException` and `NriUnsupportedException` included.
- **Device lost, D3D12** (`DeviceLostTests`, child `d3d12-device-lost`; hardware and WARP alike).
  After `ID3D12Device5::RemoveDevice` (INFO [234]), `QueueSubmit`, `CreateCommittedBuffer` and
  `DeviceWaitIdle` throw `NriDeviceLostException`, each with its own call's text ("QueueSubmit:
  DEVICE_LOST — D3D12::(adapter) - Submit(): failed, result = 0x887A0005 (-2005270523)!"); a fence
  then reads 0xFFFFFFFFFFFFFFFF. `ReportDeviceLostInfo` begins "[DeviceLost] GetDeviceRemovedReason:
  0x887A0005, result=-3"; DRED gives only "Device state: Unknown" and a zero page-fault VA. The
  child fails on any creation warning (a DRED warning means no diagnostics; the `[989]`
  allowance went at v192, D3D12 and the Agility SDK). Vulkan has no call that removes a device: skipped.
- **More device-creation facts** (`DeviceTests`): an Agility path without a trailing separator is
  normalised; a wrapped D3D12 device uses the queues it is given; a refused wrapped creation throws
  with the call named and releases its callbacks; `DisableVkRayTracing` gives `tiers.rayTracing` 0
  and a null `TryRayTracing` ("Interface 'RayTracingInterface' is not supported by the device!").

## Lifetime

- **The refusal comes before the handle is dropped.** A `Dispose` that zeroed the handle before
  checking children left the wrapper `IsDisposed` and leaked the native device. `NriObject.Dispose`
  runs `BeforeDestroy` first and gives the handle up only after it returns (`DeviceTests`).
- **A kept D3D12 device can break later ones.** A test that got an unexpected view never disposed
  it, its device's `Dispose` threw, and the next D3D12 test in the process failed
  `D3D12CreateDevice` with 0x887A0007 (`DXGI_ERROR_DEVICE_RESET`). Tests dispose inside the assertion
  what an unexpected success would create (`Assert.Throws<NriException>(() =>
  texture.CreateView(...).Dispose())`).
- Host allocator live count returns to 0 after device disposal (NONE 2/2, VK 60/60, D3D12 50/50).
- **Zero allocation per frame is mandatory, default-deny** (owner, 2026-11-07). Every public method
  and computed property of every public type in the `SharpNRI` assembly counts as per-frame and must
  be called inside a measured loop (`Frame` or `RecordStream` in `AllocationTests` /
  `ExtensionAllocationTests`), unless it creates or destroys an object or is named in
  `AllocationCoverageTests.NotPerFrame` with its reason: once per object or at setup (memory
  requirements and binding, capability queries, exports, names, adapter enumeration), or an allocating
  form by design with a measured zero-allocation twin (the array overload of
  `AllocateDescriptorSets`, `Nri.GetFormatName`). `AllocationCoverageTests` reads the loops' IL and
  fails naming each unmeasured overload; a stale exclusion fails too. The measurement is transitive:
  `GC.GetAllocatedBytesForCurrentThread` counts everything a public call reaches. Not counted: the
  binding's own `NativeMemory` use (reviewed instead) and branches the frames' arguments do not take.
  At 821b95f: 195 calls measured, 0 managed bytes, 0 host allocations on NONE (Map on
  every GPU backend). Found by it: `NriAccelerationStructure.Buffer` / `NriMicromap.Buffer` made a
  new wrapper per read (33,600 bytes over 300 frames for two reads per frame), now cached per object;
  command-buffer, queue and thread annotations `malloc`ed their UTF-8 name per call, now encoded on
  the stack up to 255 bytes (`Utf8.StackBytes`). CI job `zero-alloc` (Actions budget).
- **Zero-allocation frames** (`AllocationTests`, NONE): 300 whole frames (allocator and pool reset,
  sets into a reused span plus one single, `Update` / `UpdateRange` / `CopyRange`, recording with
  `SetPipeline` and both `SetRootConstants` overloads, submit with wait and signal fences and
  `Submit(cmd, fence, value)`, fence wait, acquire and present): 0 managed bytes, 0 host
  allocations; every extension's per-frame path: 0 managed bytes. (`Submit(cmd, fence, value)` cost
  48 bytes per call until its signal became a span over a local.) `Map` is not in the frame: NONE's
  `MapBuffer` returns null, a failure.
- **Freeing memory before the resources bound to it.** With NRI validation NRI reports "... is still
  bound to the memory" and returns without freeing: the allocation leaks, and on D3D12 every later
  `D3D12CreateDevice` in the process fails with 0x887A0007 (measured in probes only; a test would
  break later tests). So `NriMemory` refuses disposal while resources are bound: `Bind*Memory`
  (after NRI's success) and the four `CreatePlaced*` count each resource (`NriMemory.Track`,
  `Interlocked`), its `OnDestroyed` uncounts it, and `BeforeDestroy` throws
  `InvalidOperationException` naming the count before NRI is called (`MemoryTests`,
  `RayTracingMemoryTests`).
- **One `AllocateAndBindMemory` call's allocations share one count**: NRI's helper does not report
  which resource went into which allocation (it packs by memory type up to `preferredMemorySize`,
  plus one per dedicated resource). Caller's rule: dispose every resource, then the allocations.
  `MemoryTests`: with `preferredMemorySize` 1, two buffers and a texture get three allocations, each
  refusing with 3, then 2 once a buffer is gone.
- **`NriSharedHandle` is a sealed class whose `Dispose` closes once.** As a `readonly record struct`,
  disposing twice or disposing a copy closed the value again, by then possibly another handle:
  Windows hands a closed value to the next handle opened (every time in 20,000 pairs, also with
  four threads churning; `InteropTests`). `Dispose` releases with `Interlocked.Exchange`, then closes
  through NRI (Shared handles).

## Memory and resources

- **Host barrier on a buffer is refused** ("'barrierDesc.buffers[0].after.access' is not supported by
  the usage mask of the buffer"): `HOST_READ`/`HOST_WRITE` belong to `HOST_TRANSFER` textures; for a
  readback buffer the fence wait is the visibility (`TriangleTests`).
- `TextureDesc` zeros (height, depth, mipNum, layerNum, sampleNum) come back as 1 on both GPU
  backends; `BufferDesc` comes back as given.
- Sizes and alignments (NVIDIA): a 1000-byte buffer needs VK 1,008 at 16, D3D12 inbox 65,536 at
  65,536, Agility 1,024 at 128; upload row/slice alignment VK 1/16, D3D12 1/1. Vulkan buffers align
  16 for vertex, index, shader-resource, storage and argument use, 64 constant, 128 scratch, 256
  acceleration-structure storage; Vulkan textures 1,024. D3D12 buffers 128 for every usage;
  textures 4 KiB up to a 64x64 RGBA8 shader resource, 64 KiB when larger or an attachment, 4 MiB
  with 4 samples. D3D12 descriptor-set heap offsets for three sets: (0,0), (3,1), (6,2), (0,0) after
  `Reset`; VK (0,0) always.
- **`AllocateAndBindMemory` packing order** (fixed upstream, N11, #305, 14a61d47): v190's sort range
  excluded the last element, so the last of each kind was packed unsorted.
  `MemoryTests.AllocateAndBindMemory_packs_a_group_alike_in_every_order` passes three alignments in
  all six orders at their sorted packing size and expects one allocation each (VK buffers 1,008 B at
  16, 1,024 B at 64, 1,008 B at 128 = 3,056 B; D3D12 textures 16 KiB and 4 KiB at 4 KiB, 64 KiB at
  64 KiB = 128 KiB). Sorted is not always tightest (scratch, constant, vertex fit 3,040 bytes).
- **EXCLUSIVE images get no queue family indices (patch 0013).** `DeviceVK::FillCreateInfo` sets one
  index per family `GetQueue` handed out whatever the sharing mode; best practices then report
  `BestPractices-vkCreateImage-sharing-mode-exclusive` "while specifying multiple queues
  (queueFamilyIndexCount of 3)" per image. NRI buffers have no sharing mode (CONCURRENT with more
  than one active family); the swap chain passes one index, which the check (count > 1) ignores.
  `TextureTests.An_EXCLUSIVE_texture_is_created_without_queue_family_indices`.
- **Layout `shaderStages = ALL`** (fixed upstream, #302, 9764221e): `StageBits::ALL` is 0, and v190's
  validation masked ranges with it, refusing every explicit range and shader. `ALL` and
  `ALL_SHADERS` now resolve to the layout's stages; a mask with non-shader bits is refused.
  `PipelineTests.A_layout_with_ShaderStages_ALL_takes_explicit_range_stages_and_pipelines`.
- **Vertex stride**: the draw uses `VertexBufferDesc::stride` on D3D12 always, and on Vulkan with
  `features.extendedDynamicState` (forced on for 1.3+), where `VertexStreamDesc::stride` is not
  even written into the pipeline. Stride 0 reads every vertex from the offset, and no layer objects
  (legal in both APIs), so the binding's stride has no default.
- **Root descriptor offset: a sliding window on Vulkan, an address on D3D12.** Vulkan's
  push-descriptor path adds `SetRootDescriptorDesc::offset` to the descriptor's offset and keeps
  the view's range (`WHOLE_SIZE` = `bufferSize - viewOffset`); D3D12, and Vulkan's descriptor-heap
  path, bind `address + offset` with no range. NRI validation caps the offset at `bufferSize -
  view.offset - view.size`, so a whole-buffer view accepts offset 0 only. Caller's rule: a sliding
  window needs a view the size of the window. Not patched (upstream's choice to make the backends
  agree). `RootDescriptorTests`: root CBV, SRV and UAV work without a descriptor set on compute and
  graphics; 256-byte views over 512-byte buffers read window 1 at offset 256; a whole-buffer view
  at 256 draws "'offset=256' must be <= maximum root descriptor 'offset=0'"; root descriptors switch
  between dispatches while a set stays bound.
- **Pipeline cache**: after one compute pipeline VK 6,872 B, D3D12 3,318 B; rebuilt from bytes, same
  size. Stale or foreign bytes (`PipelineTests`, 4,096 random): `CreatePipelineCache` throws
  `NriException` with `OUT_OF_DATE`, hands nothing back (NRI destroys the half-made object), and the
  allocator balances. NRI never returns `OUT_OF_DATE` with a usable cache.
  **D3D12 debug-layer warnings on the expected paths (patch 0050).** NRI creates a pipeline by
  looking it up in the `ID3D12PipelineLibrary` first (`PipelineD3D12.hpp:447`, `:492`), so each
  miss, every first creation, drew "[971] ID3D12PipelineLibrary::Load*Pipeline: This library does
  not have a pipeline stored with name ...". A blob from another driver or adapter drew "[963] ...
  intended for a different driver version" / "[964] ... for a different adapter" before NRI's
  `OUT_OF_DATE`; NRI's deny list (`DeviceD3D12.hpp`) had only [962], the same refusal of a blob that
  is no library. Measured 2026-11-08 (Agility 1.620, NVIDIA): one byte flipped in a real
  serialization gave [962] (filtered) at offsets 0-15 and 24-27, [963] at 16-23, [964] at 28-59,
  and a usable cache from 60 on. `OUT_OF_DATE` named no cause though `CreatePipelineLibrary`'s result
  does: the patch also reports it as an INFO in every build ("Pipeline cache data refused: made by
  another driver version (result = 0x887E0002)"; 0x887E0001 another adapter, 0x80070057 no library).
  `PipelineTests.D3D12_pipeline_cache_misses_and_stale_blobs_draw_no_debug_layer_warning`: red
  without the patch ([971], then no INFO), green with it. Reported downstream; not filed (a layer
  warning plus a message; main a20efc8f denies only [962] and drops the result).
- **UNORM rounding**: clear `0.1` -> 25 on NVIDIA Vulkan, 26 elsewhere. `TriangleTests` clears
  `(0.2, 0.4, 0.6)` and allows +-1; VK and D3D12 differ at edges, so it logs each SHA-256.
- **`UploadData`** (`NriQueue.UploadData`): managed records; short data throws `ArgumentException`
  before NRI reads past it; subresources layer-major; exact on VK, D3D12, WARP. **NV12 refused**: VK
  VUID-VkCopyBufferToImageInfo2-dstImage-07981 (both planes in one region), D3D12 error 1417 (the
  second plane's barrier).

## Barriers

- **A texture barrier side with access `NONE` and a defined layout (D3D12).** The debug layer's
  texture rule: a side with `NO_ACCESS` and a layout other than `UNDEFINED` must have `SYNC_NONE`
  (1331, `INCOMPATIBLE_BARRIER_VALUES`). v190 derived sync from the stages and broke it ("[1331] ...
  Invalid LayoutBefore value D3D12_BARRIER_LAYOUT_COPY_DEST. LayoutBefore must be LAYOUT_UNDEFINED
  or SyncBefore must be SYNC_NONE when AccessBefore is ACCESS_NO_ACCESS", then `Close`
  `E_INVALIDARG`). Since NRI 48a14c3 such a side with stages gets `D3D12_BARRIER_ACCESS_COMMON` and
  keeps the stages' sync ("conservative COMMON access to preserve execution dependencies (may flush
  caches)"); only a side with stages `NONE` stays `NO_ACCESS` / `SYNC_NONE`. (Forcing `SYNC_NONE`
  would drop the execution dependency Vulkan needs after a semaphore wait.)
  `D3D12DebugLayerTests.A_texture_barrier_whose_side_has_no_access_records_on_both_backends`: six
  shapes clean on both backends, not even a warning for `COMMON` as a before-access. NRI validation
  does not check the rule; buffer and global barriers have no layout.
- **The limit of a layout-only side.** Stages `NONE` say the subresource was not accessed earlier in
  the same submit, and the D3D12 layer checks the list
  (`D3D12DebugLayerTests.After_an_access_in_the_same_list_only_a_before_side_without_stages_is_refused_on_D3D12`):
  after an upload in the same list, `{NONE, COPY_DESTINATION, NONE}` draws "[1417] ... has been
  accessed before, a barrier that specifies D3D12_BARRIER_SYNC_NONE as SyncBefore is invalid."
  (`Close` succeeds); `{NONE, COPY_DESTINATION, COPY}` and `{COPY_DESTINATION, COPY_DESTINATION,
  COPY}` are clean. `NRIDescs.h`: "StageBits::NONE requires no accesses or barriers to the affected
  subresources on that side within the same QueueSubmit scope". Caller's rule: stages `NONE` only
  for a submit's first barrier on a resource another submit finished with.
- **`CmdZeroBuffer` is ordered by `COPY`** (fixed upstream, 1163658). Vulkan's `ZeroBuffer` is
  `vkCmdFillBuffer`, a clear command (`CLEAR_BIT`), while v190's `COPY` mapped to `COPY_BIT` only:
  "SYNC-HAZARD-WRITE-AFTER-WRITE: vkCmdFillBuffer() ... it must allow these accesses at
  VK_PIPELINE_STAGE_2_CLEAR_BIT" (D3D12's is a copy). `COPY` now covers clears (`CmdClearStorage`'s
  too). `BufferTests.ZeroBuffer_is_ordered_by_a_COPY_barrier` runs syncval through the environment,
  with a barrier-less zero + copy as positive control (no hazard from it = syncval inactive = skip).
- **Copy queues: `COPY_SOURCE` / `COPY_DESTINATION` on every queue, `srcQueue` / `dstQueue` on the
  hand-off** (upstream since 732f737).
  - Vulkan's copies hard-code `TRANSFER_SRC_OPTIMAL` / `TRANSFER_DST_OPTIMAL`: a texture in
    `GENERAL` cannot be an NRI copy destination (VUID-VkCopyBufferToImageInfo2-dstImageLayout-00180),
    and claiming `GENERAL` after such a copy draws VUID-vkCmdDraw-None-09600 ("... current layout
    is VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL").
  - D3D12 allows only `COMMON` on a COPY list ("Copy queues do not support layout transition
    Barriers"; COMMON is readable anywhere and writable as a copy destination), every NRI texture
    starts in `COMMON`, and the layer checks LayoutBefore across queues. COPY_DEST on a copy list
    drew "[1334] ... LayoutAfter D3D12_BARRIER_LAYOUT_COPY_DEST is incompatible with command list
    type D3D12_COMMAND_LIST_TYPE_COPY", `Close` failed and the submit removed the device; mapping
    only the copy list to `COMMON` moved the error to the GRAPHICS submit.
  - NRI's rule: in a copy list, on the before side of a barrier whose `srcQueue` is a copy queue,
    and on the after side of one whose `dstQueue` is, `COPY_SOURCE` / `COPY_DESTINATION` mean
    `COMMON` (`EXTERNAL_QUEUE` excluded).
  - Portable rule: copy with `COPY_DESTINATION` / `COPY_SOURCE` on every queue; across a COPY queue,
    a release on the source queue and the same barrier (same layouts, `srcQueue` / `dstQueue`) as
    the acquire on the destination queue, then any transition there.
    `CrossQueueCopyTests.A_texture_uploaded_on_the_COPY_queue_is_read_on_GRAPHICS_with_the_copy_layouts`
    (both backends, CONCURRENT and EXCLUSIVE, readback compared).
- **No queue family ownership transfer on `CONCURRENT` images** (upstream since 2603edc). The spec's
  transfer is for an EXCLUSIVE image whose indices differ, or a CONCURRENT one with `EXTERNAL` /
  `FOREIGN` against `IGNORED`; the Khronos layer treats any barrier whose indices differ as one
  (sharing mode not read), applies only the acquire's layouts, and pairs release and acquire by
  queues and range. With family indices on a CONCURRENT image (v190) the portable hand-off drew
  VUID-vkCmdDraw-None-09600, and a `SHADER_RESOURCE`-only image
  "BestPractices-PipelineBarrier-unneeded-QFOT: ... not required, because maintenance9 is enabled".
  NRI now writes `IGNORED` on both sides of a CONCURRENT image (except `EXTERNAL_QUEUE`); wrapped
  images (`CreateTextureVK`, swap chain) keep the indices. Consequences:
  - on a CONCURRENT image the two hand-off barriers are ordinary ones, validated on their own
    queues: a release on COPY may name only copy stages (`{SHADER_RESOURCE, ALL_SHADERS}` there
    draws VUID-vkCmdPipelineBarrier2-dstStageMask-09676);
  - on an EXCLUSIVE image release and acquire must name the same transition (else 09600);
  - a CONCURRENT image allows only the families active at its creation: fetch every queue first
    (else VUID-vkQueueSubmit-pSubmits-04626).
  - `CrossQueueCopyTests.A_hand_off_between_two_queues_is_no_ownership_transfer_on_a_CONCURRENT_image`,
    `A_release_and_acquire_naming_different_transitions_is_an_error_only_on_an_EXCLUSIVE_image`;
    both skip on lavapipe (one queue family).
- **Stage masks naming shader stages the device lacks (patch 0039 until v192; upstream since #310, 667d47de, which also masks explicitly named stages).** `GetPipelineStageFlags` and
  `GetShaderStageFlags` (`ConversionVK.h`) turn `ALL_SHADERS` (a barrier, a fence wait) and a
  layout whose stages are `ALL` into every Vulkan shader stage bit, whatever the device has. On
  MoltenVK (no geometry, mesh or ray tracing shaders) every barrier naming `ALL_SHADERS` drew
  VUID-VkImageMemoryBarrier2-dstStageMask-03929 / 03934 / 03935 / 07946, and every layout with `ALL`
  stages VUID-VkPushConstantRange-stageFlags-parameter at `vkCreatePipelineLayout` and
  VUID-VkPushConstantsInfo-stageFlags-parameter at `vkCmdPushConstants2` (task, mesh and ray tracing
  bits without their extensions): `CrossQueueCopyTests`, `D3D12DebugLayerTests`, `DescriptorTests`
  failed there, unseen on NVIDIA and lavapipe, which have every stage. The same on NVIDIA with
  `DisableVkRayTracing` (the ray tracing half): `StageMaskTests` (a global barrier on `ALL_SHADERS`,
  a root constant visible to `ALL`) drew 07946 and both push-constant VUIDs on v191, nothing with the
  patch. The patch drops the lacking stages once per device (`DeviceVK`: tessellation and geometry
  from `features`, task and mesh from `VkPhysicalDeviceMeshShaderFeaturesEXT`, ray tracing from
  `rayTracingPipeline`, each chained only when its extension is enabled), as `CmdSetDescriptorSet`
  already did for the mesh stages; in Vulkan terms for barriers and fence waits, since NRI's
  `StageBits::NONE` is `0x7FFFFFFF` and cannot be masked. Filed as NRI #310 (2026-11-05).
- **Attachment resolves are explicit in barriers** (NRI 48d4a33): stage `RESOLVE_ATTACHMENT`,
  accesses `RESOLVE_ATTACHMENT_SOURCE` / `_DESTINATION`, umbrellas `ALL_ATTACHMENT` and
  `ALL_RESOLVE`; `GRAPHICS` and `ALL_ATTACHMENT` no longer include resolves. Every `StageBits` value
  from bit 20 and `AccessBits` value from bit 22 moved against v190 (`COPY` 20 -> 22, ...), so a
  managed build and an `NRI.dll` from different NRI commits disagree silently (Branches). The
  header asks for the source access + stage on a multisampled attachment's barriers and the
  destination access + stage on `resolveDst`; resolve views cannot use `GENERAL` or
  `INPUT_ATTACHMENT`; `SIMULTANEOUS` textures are not supported. Vulkan maps the stage onto
  `COLOR_ATTACHMENT_OUTPUT`; D3D12 drives its own transitions at `EndRendering` from the access.
  `AttachmentResolveTests.A_multisampled_attachment_resolves_into_its_resolve_destination`: new
  names and plain colour-attachment barriers both clean, the same pixels.
- **One recording command buffer per allocator** (knowledge, not patched). D3D12 `Begin` is
  `ID3D12GraphicsCommandList::Reset(allocator)`, refused for a second open list: "[540] ... The
  command allocator is currently in-use by another command list ...", then `BeginCommandBuffer:
  INVALID_ARGUMENT`. Vulkan: both begin. Neither `NRI.h` nor NRI validation says so.
  `D3D12DebugLayerTests.A_second_command_buffer_of_one_allocator_cannot_begin_while_the_first_records_on_D3D12`.
  Portable rule: one recording command buffer per allocator; simplest is one allocator per command
  buffer in flight.
- Unreproduced report: with the info-queue break on, error 638 from `CreatePlacedResource2` only
  logged. With the binding's patch 0018 default every debug-layer error is a message, and the
  failed creation throws `NriException`.

## Draws, render state and attachments

Measured by `DrawTests`, `IndirectTests` and `RenderStateTests` (every render compares all 4,096
pixels of a 64x64 target exactly).

- **Fixed upstream, held by tests:** Vulkan bound vertex attributes to `streamIndex` instead of the
  stream's `bindingSlot`, so streams out of slot order drew nothing (N1, #295, 0593d91;
  "VUID-vkCmdBindVertexBuffers2-pStrides-06209"); validation now also checks `streamIndex`,
  `bindingSlot` and the `SetVertexBuffers` range
  (`DrawTests.Streams_declared_out_of_slot_order_feed_each_attribute_from_its_stream_slot`,
  `An_attribute_naming_a_stream_the_vertex_input_does_not_have_is_refused`). An empty
  `NriVertexInputDesc` made the Vulkan stride dynamic, so every draw reported
  "VUID-vkCmdDraw-pStrides-04913" (N2, #296, 8a8fc2b;
  `An_empty_vertex_input_draws_as_no_vertex_input`). D3D12 `ClearAttachments` in a stencil-only
  pass dereferenced the depth attachment (N3, #297, 909a139;
  `RenderStateTests.ClearAttachments_in_a_stencil_only_pass_clears_the_stencil`, in child
  `stencil-only-clear` so a crash fails the test, not the run). D3D12 did not clear the stencil of a
  depth-stencil view passed as `depth` only, which "may be treated as depth-stencil" (N4, #298,
  c8fe3b2; `A_depth_stencil_view_passed_as_depth_alone_clears_its_stencil_too`).
- **Dynamic depth bias exists only for a pipeline with a non-zero static bias.** `SetDepthBias` on
  another pipeline draws "VUID-vkCmdDraw-None-08608" on Vulkan and "[1366] ... without the
  D3D12_PIPELINE_STATE_FLAG_DYNAMIC_DEPTH_BIAS flag is undefined" on D3D12; NVIDIA Vulkan then
  ignores the bias, D3D12 and WARP apply it. After re-binding the pipeline, Vulkan and WARP restore
  its bias and NVIDIA's D3D12 driver keeps the dynamic one (a driver difference, untested).
- **Draw-parameters emulation is D3D12 and Metal only (patch 0043 until v192; upstream since #317, via PR #318, 12891278).** The flag is "D3D12,
  METAL"; on SPIR-V `NRI.hlsl` writes plain `DrawDesc` records and the Vulkan backend reads them.
  Validation applied the emulated layout on every backend: on Vulkan with the flag, stride 16 was
  refused ("'stride' is too small, expected >= 24", 28 indexed) and `DrawBaseDesc` records at 24
  drew nothing (`offset + 8` drew). Now the flag is inert on Vulkan, so portable code sets it on
  every backend. `IndirectTests` sets it everywhere and renders Vulkan with and without it, the same
  picture.
- **A missing barrier between a compute write and an indirect read is silent on NVIDIA**: VK and
  D3D12 draw nothing with no message from either layer; WARP draws. Only syncval reports it
  ("SYNC-HAZARD-READ-AFTER-WRITE: vkCmdDrawIndirect(): ... previously written by vkCmdDispatch"),
  which `IndirectTests` keeps as its Vulkan proof.
- WARP reports every bit these tests gate on (dynamic depth bias, depth bounds, indirect count,
  independent stencil references, rect clears, draw parameters), shader model 6.8, and attaches both
  `D32_SFLOAT_S8_UINT` and `D24_UNORM_S8_UINT`.

## Multisampling, copies and queries

Measured by `MultisampleTests`, `TextureCopyTests` and `QueryTests`.

- **Host-memory texture copies check the span (binding)**: unchecked, an 8x8 RGBA8
  `ReadbackTextureToHostMemory` into 16 bytes wrote 240 past them. `NriQueue.CheckHostCopySpan`
  requires (depth - 1) x slice pitch + (rows - 1) x row pitch + one row of blocks.
- **Fixed upstream, held by tests:** D3D12 resolved into the wrong subresource of a later mip or
  layer and only the first layer of a layered view ("[877] ... subresource index is out of
  bounds"; 48d4a33; `An_attachment_resolve_lands_in_the_mip_and_layer_of_its_view`,
  `An_attachment_resolve_of_a_layered_view_resolves_every_layer`). D3D12 non-array views ignored
  `layerOffset`, silently (N5, #306, 71d3c586): such a view is now a one-layer array view (six for a
  cube), and validation refuses `layerOffset` only for 3D textures and a D3D11 `STORAGE_TEXTURE`
  (`TextureCopyTests.A_non_array_view_of_a_layer_other_than_the_first_addresses_that_layer`).
- **Resolve ops.** Vulkan reports only `AVERAGE` for colour and `SAMPLE_ZERO` for integer formats
  (maintenance10). D3D12 reports `AVERAGE | MIN | MAX` and `MIN | MAX`, yet no integer format
  reports `MULTISAMPLE_RESOLVE`, so integer MIN/MAX cannot pass validation. An attachment resolve
  equals `ResolveTexture` byte for byte.
- **A storage view over several mips** is cleared whole by Vulkan's `ClearStorage` and only at mip
  0 by D3D12 (a UAV addresses one mip). Use one mip per storage view.
- **Occlusion queries** begin without `VK_QUERY_CONTROL_PRECISE_BIT` (only non-zero guaranteed);
  measured counts are exact anyway: 256 for a full 16x16 quad, 0 behind a failing depth test.
- **Pipeline statistics**: results `QuerySize` apart (VK 104 bytes, D3D12 112); fragment
  invocations for 256 pixels: 256 on hardware, 288 on WARP (helper lanes on diagonal quads).
  **Mesh counters without mesh-shader queries (patch 0042 until v192; upstream since #312, b787bd77).** The Vulkan pool added
  the task and mesh counters and sized queries for 13 on `features.meshShader`; the counters need
  `meshShaderQueries` (`features.meshShaderPipelineStats`). Every device here has both, so
  `MeshShaderQueryTests` runs a child with the Khronos Profiles layer (Vulkan SDK) below the
  validation layer, reporting `meshShaderQueries = false` and simulating nothing else
  (`SIMULATE_FEATURES_BIT`, portability emulation off): without the patch
  VUID-VkQueryPoolCreateInfo-meshShaderQueries-07069 and 104-byte queries, with it none and 88
  bytes; skipped where the layer is not installed (CI). Lavapipe counts 0 clipping output primitives
  for two visible triangles (Vulkan requires at least 1 each): the test asserts the bound
  everywhere else and the wrong 0 on lavapipe (Tests and skips).
- **Timestamps**: a GPU timestamp written between two calibrated readings lies between them; 1 GHz
  on both GPU backends, 8-byte results; WARP's clock is 10 MHz and equals QPC.
- Shading-rate texel value 5 means 2x2 on both backends. WARP reports `tiers.sampleLocations` 2,
  `tiers.shadingRate` 1 and attachment tile size 0; the attachment test skips there.

## Debug aids and native objects

Measured by `DebugAidTests`.

- **Annotations.** NRI validation reports an unmatched command-buffer end or begin. Without it,
  Vulkan checks balance only at submit (VUID-vkCmdEndDebugUtilsLabelEXT-commandBuffer-01912), a
  region may span command buffers, and queue labels are never checked. D3D12 reports "[1384] ...
  Number of `PixEndEvent` occurrences in command queue ... exceed number of `PixBeginEvent`
  occurrences", cumulatively per queue, so once negative every later end reports. Lavapipe pops an
  empty label stack safely.
- **Debug names reach the layers**: `VkImage 0x...[name]` (Vulkan), `'name'` (D3D12, 1334);
  overrun-copy messages (VUID-VkCopyBufferInfo2-size-00116, D3D12 841) print none. D3D12's
  `GetPrivateData(WKPDID_D3DDebugObjectName)` returns the name with a terminating NUL. `NriQueue` is
  not an `NriObject`; `NriQueue.SetDebugName` names it through the Core table (each D3D12 queue
  reads its own name back; clean on Vulkan).
- **Native objects are the real ones**: the D3D12 queue's type matches; `vkGetDeviceQueue(family,
  0)` returns the same `VkQueue`; a raw `vkCmdCopyBuffer` / `CopyBufferRegion` recorded between NRI
  commands executes in order.
- **Memory budget.** A 64 MiB allocation raises usage by exactly 67,108,864 bytes on hardware VK,
  D3D12 and WARP, and the free returns it. Lavapipe reports system-wide usage (heap size minus
  available system memory, `lvp_device.c`) where `VK_EXT_memory_budget` defines the process's, so it
  moves with every allocation on the machine, the parallel test classes included: 64 MiB written
  raised it by 41 MB on a CI runner (PR #44; also PR #40). The test asserts the budget there and
  skips the delta, quoting that (Tests and skips). WARP is UMA; DXGI's non-local budget there is 0.

## D3D12 and the Agility SDK

- **Agility 1.620 reports every legacy `CheckFeatureSupport` as deprecated** (NRI #307, D3D12MA
  #102): WARNING 989 `D3D12_MESSAGE_ID_DEPRECATED_API` ("ID3D12Device::CheckFeatureSupport is
  superseded by ID3D12Device16::CheckFeatureSupport1 ..."), 33 per device creation on NVIDIA and
  WARP since v191 pins Agility 1.620.1 (c33dd915); answers unchanged. NRI's own 27 moved to
  `CheckFeatureSupport1` on main (3dd4cbfa, which also made `ID3D12DeviceBest` `ID3D12Device16`:
  `Using ID3D12Device16` on Agility); six remained on main 7ea93bb5, D3D12MA's (#102). Fixed in v192:
  its dependency update (adb5b118, #307) takes a D3D12MA that uses `CheckFeatureSupport1`, and a
  D3D12 device is created without a warning; the device-lost child's allowance went with it and
  any creation warning fails it again (measured 2026-11-12).
- **Resources on the inbox runtime (upstream since #309, 3ac981f1: resources and video encoders query the latest interface the runtime has; 0040 dropped).** 3dd4cbfa made `ID3D12ResourceBest`
  `ID3D12Resource3`. The device and command lists are queried down to what the runtime has
  (`QueryLatestInterface`), resources are created for that IID directly, and the inbox runtime
  (Windows 11 26200) has no `ID3D12Resource3`: every committed, bound and placed buffer and texture
  failed with `E_NOINTERFACE` as `INVALID_SDK` ("D3D12MA::CreateResource3(): failed, result =
  0x80004002", "ID3D12Device10::CreatePlacedResource2(): failed ..."), and with the D3D12 debug
  layer on a failed bind ended the process (0xC0000409, no message). NRI calls none of
  `Resource3`'s methods (`GetDesc2`, castable formats), so the patch goes back to `Resource2`.
  `InboxRuntimeTests` (a child whose first D3D12 device is the inbox one) creates each kind;
  `RenderDocTests` (inbox capture) failed the same way. v191 is not affected. The video encoder
  has the same shape (`ID3D12VideoEncoderBest` = `ID3D12VideoEncoder1` by IID, NRI's own
  `TODO-VIDEO: use "QueryLatestInterface"`), unmeasured on the inbox runtime.
- **Agility does not load in a managed process without `d3d12AgilitySdkPath`** (patch 0001 until v192; upstream since PR #314, e52e46e2, with the maintainer's 2658d48d: `InitializeFromGlobalState` before the apply keeps the host's debug settings, `D3D12GetInterface` resolved at run time, a null path inherits the current selection). NRI's CMake writes
  `Include/NRIAgilitySDK.h` declaring `D3D12SDKVersion` / `D3D12SDKPath` exports for the host
  executable; `dotnet.exe`, `testhost.exe` and apphosts export nothing, so the inbox runtime runs.
  Symptom: `CheckFeatureSupport(options22) failed, result = 0x80070057!`. Success line: `Using
  Agility SDK v1.6xx from '...\AgilitySDK\'`.
- `ApplyToGlobalState`, not device-from-factory, so the debug layer, DRED and AGS's
  `agsDriverExtensionsDX12_CreateDevice` see the same SDK. **Process-wide**: once applied, the
  inbox runtime is unreachable (`AgilityTests`). The adapter probe in `nriEnumerateAdapters` stays
  on the inbox runtime on purpose.
- **Patch 0001's order rule: the first device the process keeps.** The comment used to say "Must be
  called before any other D3D12 entry point in this process", stricter than the truth, and Codex's
  review of NRI PR #314 reasoned from it (P1: "Apply the factory before adapter probing"). With no
  adapter desc, `nriEnumerateAdapters` creates and releases D3D12 devices on the inbox runtime
  first (the inbox `D3D12Core.dll` is loaded), and the apply still works: "Using Agility SDK
  v1.620", "Using ID3D12Device16", main 7ea93bb5, 2026-11-05. The comment now says it must precede
  the first D3D12 device the process keeps, and that it is process-wide (#314's second commit,
  0753c63d).
- **Wrapped route works too**: a device from the host's own `ID3D12DeviceFactory`, passed to
  `nriCreateDeviceFromD3D12Device`, reports `Using ID3D12Device16` (15 on v191), no OPTIONS22 warning
  (`AgilityFactoryRouteTests`); NRI routes such devices' root signatures through
  `ID3D12DeviceConfiguration` (upstream PR #177).
- Developer Mode is not needed for the factory route (Microsoft's documentation; the Windows CI
  runner has it off). `NriDeviceOptions.D3D12AgilitySdkPath`: null = `AgilitySDK` beside the
  assembly if present, `""` = inbox.
- **Reflex on Vulkan poisons later D3D12 device creation** in the process: see Extensions.
- **RenderDoc 1.44 captures NRI's D3D12 device on the inbox runtime only** (`RenderDocTests`, a child
  under `renderdoccmd capture`). Inbox: NRI reports `Using ID3D12Device14` (RenderDoc answers
  `E_NOINTERFACE` for 15), the vtable is in `renderdoc.dll`, captures are made. Agility: `Using
  ID3D12Device15`, the vtable is in `D3D12Core.dll` for every interface version (same pointer, same
  IUnknown): never wrapped, `GetNumCaptures` 0. Blaming `QueryLatestInterface` is refuted: it keeps
  COM identity. Caller's rule: capture with `D3D12AgilitySdkPath = ""` (the process's first D3D12
  device). The test asserts both halves, so a RenderDoc that captures Agility fails it; it skips
  without `renderdoccmd.exe`. NRI turns NVAPI off when `renderdoc.dll` is loaded.
- **Info-queue break (patch 0018 until v192; upstream since PR #313, d296386a).** With `enableGraphicsAPIValidation` NRI sets the break on
  CORRUPTION and ERROR unconditionally (since d003e22), and with `NRI_ENABLE_AGILITY_SDK_SUPPORT`
  also registers `ID3D12InfoQueue1::RegisterMessageCallback`, which reports every message below
  WARNING as `Message::ERROR` (`[id] text`, then `AbortExecution`, handled by `MessageBridge`). The
  break adds only the stop: an `SEHException` out of the NRI call, or a dead headless process.
  Binding: `disableD3D12BreakOnError = !(BreakOnError && Debugger.IsAttached)`. The wrapped route
  sets no break. `D3D12DebugLayerTests.A_D3D12_debug_layer_error_arrives_as_a_message_without_a_break`
  (NVIDIA and WARP, NRI validation off): a 512-byte copy into 256 bytes gives "[841]
  ID3D12CommandList::CopyBufferRegion: The destination region extends past the end of the
  destination resource ..." as an ERROR and returns; `End` then throws `INVALID_ARGUMENT`. With the
  break on, the copy threw `SEHException`.
- **GPU-based validation (patch 0019 until v192; upstream since PR #313, d296386a).** NRI never calls `ID3D12Debug1::SetEnableGPUBasedValidation`,
  which must precede device creation and stays on for the process.
  `NriDeviceOptions.EnableD3D12GpuBasedValidation` maps 1:1.
  `D3D12DebugLayerTests.GPU_based_validation_reports_a_never_written_descriptor_and_only_when_asked_for`
  (children `d3d12-gbv-on` / `-off`): a dispatch through a `PARTIALLY_BOUND` range of two UAVs with
  only slot 0 written reports "[938] GPU-BASED VALIDATION: Dispatch, Uninitialized descriptor
  accessed ..." only with the option; dispatch + wait 13-40 ms on, 7-10 ms off. Without
  `PARTIALLY_BOUND` the CPU-side layer reports the slot either way ([646], [1023]): no test of GBV.

## Vulkan loader and validation layers

- **Old loader, new driver.** Ubuntu 24.04's stock loader 1.3.275 with lavapipe reporting 1.4.318:
  v190 took minor version 4 from the physical device, while Mesa returns null for core 1.4 entry
  points on a 1.3 instance: SIGSEGV at address 0 in `CmdSetRootConstants` (`vkCmdPushConstants2`
  null), and `HOST_TRANSFER` textures rejected (`VK_EXT_host_image_copy` not enabled on a "1.4"
  device the layer evaluates as 1.3). NRI a4e562e caps at `min(vkEnumerateInstanceVersion(), 1.4)`.
  CI keeps the stock loader so that path stays exercised.
- **Layer older than NRI's headers.** NRI pins recent Vulkan-Headers and chains every feature struct
  the driver offers into `vkCreateDevice`. Ubuntu's `vulkan-validationlayers` 1.3.275 reports
  `VkPhysicalDeviceVulkan14Features`, maintenance7/8 and `VK_EXT_zero_initialize_device_memory`
  (sType 1000620000, advertised by lavapipe) as "unknown VkStructureType" ERRORs, which fail tests;
  LunarG's noble apt stops at 1.4.313 (still lacks 1000620000). The current Linux layer comes from
  the SDK tarball: `tools/ci/fetch-vulkan-layer.sh`, `VULKAN_SDK_VERSION: "1.4.363.0"` in `ci.yml`,
  cached per version; with it no unknown-struct message remains. Homebrew's macOS layer 1.4.357
  knows every struct NRI chains for MoltenVK.
- **Vortice's `GetApi` before `vkInitialize()`**: `vkGetDeviceProcAddr` is null until
  `vkInitialize()` runs; test order differed between Windows and Linux, and Linux called through
  null (gdb frame 0 at address 0). The loader is a process-wide lazy (`WrapperExtensionTests.cs`).
  See Wrappers on `GetApi`.

## Lavapipe

- Mesa 25.2.8 "llvmpipe (LLVM 20.1.2, 256 bits)", Vulkan 1.4.318, on the stock 1.3 loader: NRI runs
  it as a 1.3 device.
- **No external semaphores**: `VK_KHR_external_memory_fd` but not `VK_KHR_external_semaphore_fd`;
  `features.sharedResources` 1, `sharedFences` 0, so buffers and textures export (fd) and the fence
  tests skip naming the extension.
- **Slow first submit**: shaders are JIT-compiled when the command buffer executes; a cold-cache ray
  tracing submit took 0.6 s on a 16-thread desktop and passed NRI's 5 s fence timeout under load (CI
  runners and flakes, item 1).
- **One queue family**: GRAPHICS and COPY are both family 0, so no barrier is an ownership transfer;
  the two concurrent-image tests skip naming it.
- `GALLIUM_OVERRIDE_CPU_CAPS=avx` or `sse4.1` kills lavapipe with an illegal instruction before any
  test runs.
- **Lavapipe on Windows (Windows CI's Vulkan device) jumps to a garbage address for a disabled logic
  op; patch 0044 works around it.** NRI creates a pipeline without a logic op with `logicOpEnable =
  VK_FALSE` and `logicOp = VK_LOGIC_OP_MAX_ENUM` (legal: VUID-VkPipelineColorBlendStateCreateInfo-
  logicOpEnable-00607 asks for a valid value only when enabled; the layer is silent). Lavapipe
  converts it anyway (`lvp_execute.c:1045`, Mesa 26.2.6, `handle_graphics_pipeline`), and
  `vk_logic_op_to_pipe` (`src/vulkan/runtime/vk_blend.c`) ends its switch in `UNREACHABLE`, which
  MSVC builds turn into `__assume(0)`: no range check on the jump table, so `0x7FFFFFFF` jumps to a
  garbage address ("Attempt to execute non-executable address", on lavapipe's queue thread). Found
  2026-11-06 with mesa-dist-win 26.2.6 and 25.2.8 and its PDBs (the symbolized stack ends at that
  line): 25 tests crashed the process (depth and stencil state, blending, multisampling and
  resolves, indirect draws, mesh shaders, ImGui, occlusion), another one with 0xC0000409; Linux
  builds of the same code take the undefined path without crashing. Proof: with `VK_LOGIC_OP_COPY`
  in that one place the whole Vulkan suite runs on lavapipe for Windows. Patch 0044 passes
  `VK_LOGIC_OP_COPY` whenever the logic op is off (a value every driver ignores there); it is a
  driver workaround, the one exception to the patch rule (Rules), to be dropped when the pinned
  Mesa converts the logic op only when enabled. Not reported to Mesa yet: revisited on 2027-01-07
  against the then-current mesa-dist-win (owner).
- **Lavapipe on Windows has no external handles** (`features.sharedResources` and `sharedFences` 0):
  the Win32 import proofs skip naming the bits.

## WARP

- D3D12 software adapter on the Windows runner. The runner has no Vulkan ICD of its own; since
  2026-11-06 the Windows leg installs the Khronos loader, the SDK layer and Mesa's lavapipe
  (`tools/ci/fetch-vulkan-windows.ps1`), so Windows CI runs D3D12 on WARP, Vulkan on lavapipe and NONE.
- RT works: one-triangle BLAS 65,536 B; build scratch 120 B, **update scratch 0 B**; identifier 32
  B, SBT alignment 64. `UploadData`, import wrappers, RT pipelines pass.

## MoltenVK and the paravirtual Mac

Runner: "Apple M2 Pro (Virtual)" (`VirtualMac2,1`), macOS 26.x, AppleClang 21, MoltenVK 1.4.2,
Homebrew loader 1.4.357; device "Apple Paravirtual device", Vulkan 1.4.357, 18 device extensions.

- **`NRI_ENABLE_METAL_SUPPORT=OFF` explicitly**: NRI turns Metal on whenever Xcode 26 + the Metal
  toolchain are present, as on the runner.
- **NRI links the Vulkan loader on Apple**: the dylib got a load-time dependency on
  `/opt/homebrew/opt/vulkan-loader/lib/libvulkan.1.dylib` (the build machine's install name) while
  still `dlopen`ing at device creation, so without that loader it would not load at all, NONE
  included. Patch 0007 + OFF; the script records the `otool` link list and refuses anything outside
  the system. Without a deployment target the dylib is stamped `minos 26.0`; the script passes
  `CMAKE_OSX_DEPLOYMENT_TARGET=15.0` (Decisions).
- **Homebrew paths**: dyld's default fallback lacks `/opt/homebrew`, and NRI and Vortice `dlopen` by
  leaf name. molten-vk keeps its ICD in the keg's `etc/vulkan/icd.d` ("Found no drivers" with the
  `share` path). The loader finds the layer in `/opt/homebrew/share/vulkan/explicit_layer.d`
  (`VK_ADD_LAYER_PATH` there only warns "duplicate layer"). Only the wrapper route takes a loader
  path (`NriVulkanDeviceOptions.LibraryPath`).
- **Raw Vulkan devices must opt into portability**: without `VK_KHR_portability_enumeration` + the
  enumerate-portability flag the loader hides MoltenVK, and a device with
  `VK_KHR_portability_subset` must enable it (VUID-VkDeviceCreateInfo-pProperties-04451). NRI does
  both; so does `WrapperExtensionTests` (its raw device is 1.3).
- **Sampler LOD bias** (fixed upstream, #303, 0682d020): MoltenVK has `samplerMipLodBias = false`
  with `maxSamplerLodBias = 15.999`; v190 passed the bias anyway
  (VUID-VkSamplerCreateInfo-samplerMipLodBias-04467); the device now reports 0.
  `DescriptorTests.Sampler_is_created_from_a_filled_desc` asks `min(0.5, samplerLodBiasMax)`.
- **`HOST_TRANSFER` image crash**: SIGSEGV in
  `TextureTests.Committed_2D_texture_is_created_with_each_usage(VK, HOST_TRANSFER)`, `EXC_BAD_ACCESS
  (address=0xc)` in `AppleParavirtGPUMetalIOGPUFamily` under `MVKImagePlane::updateMTLTextureContent`
  <- `vkBindImageMemory` <- `TextureVK::AllocateAndBindMemory`; no Vulkan error first, and the device
  advertises `hostImageCopy`. Skipped by `TestDevice.IsAppleParavirtualVK`.
- **A region resolve into a texture with more than one mip aborts the process** (MoltenVK 1.4.2).
  For a region smaller than the destination mip, `MVKCmdResolveImage::encode` (`MVKCmdTransfer.mm`)
  resolves through a temporary image described like the destination (`getTransferDescriptorData`)
  with only the sample count replaced, so a 4x texture with the destination's 2 mips, and Metal
  asserts: "MTLTextureDescriptor mipmapLevelCount must equal 1 for multisample textures", exit 134
  (`MultisampleTests.A_region_resolve_lands_in_the_destination_mip_and_layer_and_nowhere_else`, a
  6x4 region into mip 1). Valid Vulkan; NVIDIA, WARP and lavapipe pass. The test skips by
  `TestDevice.IsMoltenVK`, quoting it. Caller's rule on macOS: resolve whole mips, or into a
  one-mip texture and copy.
- **Timestamps written at completion**: `QueryTests.Two_timestamps_around_GPU_work_are_ordered(VK)`
  read 0 and 0 (64 valid bits, period 1 reported); copied from a later submission both read
  140273875750: host-written at completion. The test records both and skips on that device only, as
  does the imported-pool test in `WrapperExtensionTests.Imports.cs` after its copies pass.
- No fd external handles (only `VK_EXT_external_memory_metal`): shared handles `UNSUPPORTED`. No RT,
  mesh shaders, video, descriptor heaps, shading-rate attachments. The triangle renders with
  validation on.
- Metal backend facts, if it is ever reconsidered: 40 files, 11,066 lines; Xcode 26 / AppleClang
  17+, metal-cpp, `-std=metal4.0`; runtime macOS 26 + `GPUFamilyMetal4` (unmeasured on the
  paravirtual GPU); no LowLatency, Video, shared handles, pipeline statistics, `atomicsI64`;
  micromap stubs; colour-only resolve; validation via `MTL_DEBUG_LAYER=1` only; without the Metal
  Shader Converter no DXIL, RT pipelines or geometry/tessellation.
- D3D11 backend facts, same purpose: feature set 11.1; empty queue annotations and `ResetQueries`,
  zero timestamps and device addresses, fake pipeline cache; one committed resource per bound
  object (no aliasing); barriers no-op except NVAPI/AGS UAV overlap; every queue is the immediate
  context behind a lock; register space and root constant offset not read; DXBC SM 5.1; Upscaler
  NIS only; no descriptor heaps, RT, mesh shaders, video, shared handles.

## Ray tracing

- **`HAS_BUFFER` sentinel** (`(Buffer*)1`; fixed upstream, 7915eca): v190's validation dereferenced
  it (access violation, no message); NRI now also refuses it in
  `CmdBuildBottomLevelAccelerationStructures`. The binding spells it `NriBuffer.HasBuffer`.
- `RayTracingTests`: one-triangle BLAS, one-instance TLAS, an inline ray query per thread of a
  `cs_6_5` compute shader looping on `Proceed()`, 16x16 rays along +z: centre hits, corners miss, 32
  hits of 256 on hardware; a wrong map fails with the map in the message. dxc's warning that
  `vk::binding` is ignored for DXIL is harmless. Geometry and build descs are marshalled on the
  stack per call (tens of thousands of geometries would want another shape).
- **D3D12 RT library per export**: NRI adds each `ShaderDesc` as a DXIL library exporting
  everything, so one `lib_6_3` library named by three descs faults in `CreateRayTracingPipeline`.
  `compile-test-shaders.ps1` emits one DXIL per export; Vulkan takes one SPIR-V module.
- Measured (NVIDIA VK / D3D12 / WARP): identifier 32 B, SBT alignment 64; `DispatchRays` and
  `DispatchRaysIndirect` give the ray query's map. Unbound BLAS = `GetMemoryDesc2` prediction (VK
  3,072 B/256, D3D12 2,944 B/128, WARP 65,536 B). Update scratch = build scratch on NVIDIA (1,664
  B); WARP: build 120 B, update 0 B. Compacted VK 2,944 of 3,072, D3D12 2,944 of 2,944.
  Placed+updated, CLONE, COMPACT and a BLAS imported by native object trace the same.
- **Micromaps, VK**: a transparent micro-triangle hides the non-opaque triangle, opaque shows it.
  Index-less OMM draws VUID-...-pInfos-10904 from VVL 1.4.341, likely a false positive; with an
  index buffer it is clean.
- **Micromaps, D3D12: the prebuild query's null addresses (patch 0046 until v192; upstream since PR #320, be9e6f82).**
  `DeviceD3D12::GetMicromapPrebuildInfo` sizes a micromap from its usage histogram and left
  `InputBuffer` and `PerOmmDescs` null, while every build passes real ones. The DXR spec lets the
  prebuild query inspect whether an address is null ("even though the pointers are not
  dereferenced") and says "dummy non-zero values can be used just for the purposes for the prebuild
  call"; the Agility 1.620 debug layer refused the nulls at every micromap creation ("[1162] ...
  pOmmHistogram indicate 2 OMMs in total, so InputBuffer and PerOmmDescs can't be null"), and the
  micromap tests had run D3D12 without the debug layer to avoid it (a workaround, until
  2026-11-07). The patch passes 256 for both with the build's stride; the micromap tests now run
  D3D12 with the debug layer: red without the patch (4 failures, [1162]), green with it (v191), and green on main a20efc8f with no patch. Fixed upstream (N6, #299, 1dc13b2): a
  `{MICROMAP_WRITE, MICROMAP}` before side got the postbuild-info sync beside the acceleration
  structure write access ("[1331] ... incompatible ... in buffer barrier") and the BLAS build's
  `Close` failed. `RayScene.MicromapWritten` names the `MICROMAP` stage.
- **Placed acceleration structures** (`RayTracingMemoryTests`): Vulkan BLAS 2,560 B and TLAS 2,048 B
  at 256 (NVIDIA), 152 B and 264 B at 64 (lavapipe); D3D12 the NVIDIA Vulkan sizes; WARP 65,536
  each. On Vulkan the memory desc is the structure's buffer's, bound at the placement offset with
  the structure at offset 0; `memoryAlignment.accelerationStructureOffset` (256) is for the offset
  inside that buffer, not a placement alignment (lavapipe's desc is 64-aligned). On D3D12 it is the
  placement address rule. D3D12 alignment (fixed upstream, N7, #300, 561e2be): v190 reported 128,
  and a TLAS at 2,688 had its descriptor refused ("[31] ... the address (...) must be aligned to 256
  bytes"); the test places the TLAS at an odd multiple of the reported alignment and traces
  (2,816). A placed pair traces the committed map.
- **Micromap memory paths**: created, bound, placed, cloned or compacted micromaps trace like a
  committed one; 768 B at 256 on Vulkan, 640 B at 128 on D3D12 (`WriteMicromapSizes` says 640 B).
  NVIDIA's Vulkan driver refuses `MICROMAP_COMPACTED_SIZE` and `SERIALIZATION_SIZE` query pools
  with `VK_ERROR_UNKNOWN` (a raw `vkCreateQueryPool` too, layer silent); the test skips quoting it.
- **AABB geometry** (`FromAabbs`, offset 32, stride 32): committing every procedural candidate gives
  the boxes' exact hit map on VK, D3D12 and WARP.
- **`WriteShaderGroupIdentifiers`**: the binding's size check multiplies in 64 bits (2^28 x 16
  wrapped to 0 in 32). **The group range (patch 0041 until v192; upstream since #311, 022fda45, which also refuses `shaderGroupNum` 0 and a null `dst`):** validation checked only the
  stride, so groups past the pipeline's reached the backend: on a three-group pipeline, groups 2..3
  ended the process (0xC0000005) on D3D12 with no message (`m_ShaderGroupNames` indexed unbounded)
  and on Vulkan after VUID-vkGetRayTracingShaderGroupHandlesKHR-firstGroup-02419, in the NVIDIA
  driver. Now `INVALID_ARGUMENT` with one message on both (`ShaderGroupRangeTests`, a child per
  backend).

## Upscalers and vendor runtimes

- `IsUpscalerSupported` answers what is compiled in, not installed. Without runtimes, raw L0 calls:
  D3D12 and VK FSR `EXT_ERROR` and DLSR a failure NRI maps from NGX's code, each with an ERROR naming the cause (NRI PR #324, v192; before it a bare `FAILURE`, patch 0049 until v192): "`'amd_fidelityfx_loader_dx12.dll'
  can't be loaded`", "`NGX feature creation failed, result = 0xBAD0000B`"); DLRR created here even
  without `nvngx_dlssd.dll` beside the application (NGX loads its feature DLLs from outside it too);
  XeSS an `SEHException` from the delay-load stub (0xC06D007E). FSR loads `amd_fidelityfx_loader_dx12.dll` / `amd_fidelityfx_loader_vk.dll` by name
  (the VK loader then loads `amd_fidelityfx_upscaler_vk.dll`).
- **The vendor runtimes a payload was built against** (`upscaler-runtimes.json`): `build-native.ps1`
  writes it beside each Upscalers payload (`runtimes-upscalers/<rid>/`, outside `native/` so it is not
  copied next to applications): each SDK at NRI's pin (FidelityFX and XeSS archive tags and `NGX_VERSION`
  in `nri/CMakeLists.txt`, NIS from `nri/Shaders/NIS.hlsl`) and each runtime NRI's build fetched, by file
  version and SHA-256. At v192 (unchanged from v191): NIS 1.0.3 (compiled in), FidelityFX SDK 2.4.0 (loaders 2.3.0.4555 and
  1.0.2.4555, upscalers 4.1.2.4555), XeSS SDK 3.0.2 (`libxess.dll` 2.0.2.68), NGX 310.9.16 (not built).
  The package carries it; the release body's Bound libraries lists it with NRI, versions linked
  (`tools/ci/upscaler-runtimes.ps1`); `NativePayloadTests` holds it to the pins and to
  `GetRuntimeFiles`.
- **`GetRuntimeFiles` must name what the pinned NRI loads**; check it whenever the pin moves. v190
  loaded `amd_fidelityfx_vk.dll` for FSR on Vulkan; naming that file on v191 made the guard refuse
  FSR with the right files installed (`UpscalerExtensionTests`).
- **XeSS hang, from our delay-load (not NRI's)**: NRI takes the process-wide `g_xess.lock` before
  `xessD3D12CreateContext`; with `libxess.dll` missing, the delay-load stub's SEH exception
  (0xC06D007E) unwinds without releasing it (no C++ scope guards for SEH under `/EHsc`); a second
  XeSS `CreateUpscaler` from another thread did not return in 10 s. NRI itself links `libxess.dll`
  at load time, so in NRI's own build a missing runtime fails `NRI.dll`'s load instead; the
  exception exists only because `tools/cmake/delayload.cmake` delay-loads it (re-read 2026-11-07).
  Hence
  `NriUpscalerExtension.CreateUpscaler` checks `GetRuntimeFiles` / `IsRuntimeInstalled` and throws
  `NriUnsupportedException` naming the file.
- **Host directory**: NRI and the delay-load helper load by bare name, and Windows searches the
  executable's directory: under `dotnet app.dll` FSR on D3D12 returned `FAILURE` and XeSS killed the
  process (unhandled 0xC06D007E). The guard preloads each file by full path from
  `AppContext.BaseDirectory`; then FSR (D3D12, VK), XeSS, DLSR (D3D12), DLRR (VK) create under
  `dotnet`.
- Positive controls: FSR on VK and D3D12 with FSR SDK 2.4.0's `signedbin` DLLs (provider 3.1.6 on
  v191), XeSS on D3D12. `-WithDlss` reports NIS, FSR, XESS, DLSR, DLRR on D3D12 (no XeSS on VK); raw
  DLSR without `nvngx_dlss*.dll` is a clean `FAILURE`.
- **Local upscaler run** (`pwsh tools/upscalers.ps1`, writes `docs/upscalers.md`; owner,
  2026-11-06: local only, everything testable tested, figures published). `dotnet build
  -p:SharpNRIDevPayload=upscalers` or `upscalers-dlss` puts that staged `NRI.dll` beside
  ProjectReference consumers with the vendor runtimes NRI's own build copied into
  `_build/<rid>-<variant>/bin/Release` (FSR loaders 2.3.0 / 1.0.2 and upscalers 4.1.2, XeSS 2.0.2,
  DLSS 310.9.1 from the local SDK); packing refuses it (`SHARPNRI002`), a build without it puts
  Base back and deletes the vendor files beside the tests. `UpscalerDispatchTests` (151 cases with
  `UpscalerExtensionTests`, 0 failed on NVIDIA at 9265723) dispatches every type on every backend
  that offers it and reads every output back; on Base every case skips naming the missing build or
  file. A failed vendor dispatch was only an `NRI_CHECK` (`UpscalerInterface.hpp`), gone in release
  builds; since v192 (NRI PR #324) it reports an ERROR with `EXT_ERROR`, which never calls `AbortExecution`. The pixels are the oracle: a constant image in every mode and at the
  dynamic-resolution minimum (worst 0.0078, DLRR; the others at half precision), HDR with an exposure
  texture and pre-exposure, R8 and R16 reactive masks, a moving edge with motion vectors (within
  0.33 output pixels, XeSS 1.67), `RESET_HISTORY` (DLSR and XeSS keep 30 and 39 % of the old colour
  one frame on without it; FSR and DLRR reject it themselves), NIS and FSR sharpness, DLSS created
  in the caller's command buffer, NRI validation's resolution and jitter refusals, GPU time at a 4K
  output (NIS 0.2 ms, FSR 0.8-1.8, XeSS 1.9-2.0, DLRR 3-16; DLSR 1.8-4.0, a mode moving between 1.8
  and 3.1 ms from one run to the next while tight within a run). Runs as the test executable (`-class`), so the payload's `NRI.dll` and the tests' are one file.
- **DLRR on D3D12 cannot take an R8 reactive mask** (NGX 310.9.1, measured 2026-11-06): NGX
  creates its own SRV of the responsivity mask from the resource's format, `R8_TYPELESS` because NRI
  creates every D3D12 texture TYPELESS: "[28] ID3D12Device::CreateShaderResourceView: The Format
  (0x3c, R8_TYPELESS) is invalid", then "[232] ... RemoveDevice ... DXGI_ERROR_INVALID_CALL", after
  which every `D3D12CreateDevice` in the process fails (0x887A0001). R16_SFLOAT (R16_TYPELESS) works
  there, DLSR takes R8 on D3D12, Vulkan passes formats explicitly. The case skips quoting it (it
  poisons the process). NGX's defect: NRI hands both features the mask the same way, a bare
  `ID3D12Resource` (`UpscalerInterface.hpp:1731` DLSR's `Bias_Current_Color_Mask`, `:1820` DLRR's
  `ResponsivityMask`; NGX's D3D12 parameters carry no format), and DLSR views the same
  `R8_TYPELESS` texture correctly; DLRR lacks that mapping. Caller's rule: an R16 mask for DLRR on D3D12.
- **DLRR on D3D12 below its creation size**: NGX's own barriers on its internal resources name a
  state they are not in, "[527] ... Before state (0x400: D3D12_RESOURCE_STATE_COPY_DEST) of resource
  (...:'nv.ngx.dlssd.resource') ... does not match with the state (0x40:
  D3D12_RESOURCE_STATE_NON_PIXEL_SHADER_RESOURCE)", twice per test, at `currentResolution` below
  `renderResolution` (the modes with a lower minimum: ULTRA_QUALITY, QUALITY, BALANCED); the output
  stays right; DLSR and Vulkan DLRR are clean. Third-party: the constant-image test allows exactly
  these by ID and resource name and fails when they stop (Tests and skips).
- **FSR on Vulkan** draws `Undefined-Value-StorageImage-FormatMismatch-ImageView` warnings from
  FSR's own internal storage views (R32 shader formats on R16 views); warnings only.
- NIS sharpening shows on low-contrast detail and not on a strong edge: a 0.2 / 0.6 edge overshot
  0.046 at sharpness 0 and 1 alike; stripes of amplitude 0.1 come out at 0.117 and 0.140.
- **What the Base payload reaches** (`UpscalerExtensionTests`): `IsRuntimeInstalled` follows the
  vendor files beside the application, so it is true for NIS and MetalFX, which need none. VK and
  D3D12 devices refuse every vendor type at validation ("CreateUpscaler: 'type' is not supported").
  NONE claims them all; there the binding refuses FSR, XeSS, DLSR and DLRR naming the missing file.
  `DispatchUpscale` writes each union arm with 0 managed bytes over 300 dispatches on NONE.

## Shared handles

- **Shape** (NRI #294, eb4ed356, polished in f18c6df9 and d0f5ce49): `shared` on `BufferDesc` and
  `TextureDesc`, `FenceDesc { initialValue, shared }`, three Core getters, `DestroySharedHandle`,
  `features.sharedResources` / `sharedFences`, `EXTERNAL_QUEUE`. Binding:
  `NriDevice.CreateSharedBuffer` / `CreateSharedTexture` (committed, `DEVICE`), `CreateSharedFence` / `CreateSharedBinaryFence`,
  `GetSharedHandle()` on `NriBuffer` / `NriTexture` / `NriFence`, `NriSharedHandle { Handle, Type,
  Size }`.
- **NRI's behaviour.** `DestroySharedHandle` (no device) closes by type (`CloseHandle` for the Win32
  types, `close` for `OPAQUE_FD`) and clears the struct. Export support is asked before creation;
  not exportable is `UNSUPPORTED` without a message (never measured). Vulkan enables the external
  memory / semaphore extensions itself and allocates shared memory directly, not through VMA.
  `sharedFences` only when a timeline or binary semaphore exports (1 on NVIDIA VK, 0 on lavapipe);
  D3D12 reports both. Header: buffers "follow D3D12: ownership transfers are only needed to or from
  an external user"; "After an FD import takes ownership, clear `SharedHandle` instead of
  destroying it"; "FD 0 is valid". D3D11, WGPU and Metal answer `UNSUPPORTED` for `shared`.
- **Probing what exports: the Try forms.** `TryCreateSharedFence`, `TryCreateSharedBinaryFence`,
  `TryCreateSharedBuffer`, `TryCreateSharedTexture` answer false (and clear the reported text) for
  `UNSUPPORTED`, throw for anything else (`SharedHandleTests`). No feature bit predicts those
  refusals (UNSUPPORTED without a capability, below): `features.sharedFences` is true when a timeline
  *or* a binary semaphore exports (`DeviceVK.hpp:1700`), D3D12 reports it while a binary fence is
  always `UNSUPPORTED` (`FenceD3D12.hpp:11`), and Vulkan asks the driver per buffer usage and per
  image format, usage, size, mips, layers and samples. Their `false` answer is measured for the D3D12
  binary fence only: no adapter here (NVIDIA, WARP, lavapipe) refuses a buffer or texture export.
- **`NriSharedHandle`** captures the device's `DestroySharedHandle` and calls it once on `Dispose`;
  it may outlive its device (`InteropTests.A_shared_handle_outlives_its_device_and_still_closes`).
  Released, it reads as the cleared struct (`Handle` 0, `Type` `NONE`, `Size` 0). `Detach()` gives
  the value up unclosed, for an import that takes ownership (an FD into Vulkan or CUDA); a second
  `Detach` throws `ObjectDisposedException`. `IsValid`: FD 0 valid; NT handle 0, -1 and `NONE` not.
- `SharedHandle::size` is the allocation size (what CUDA's `size` takes); 0 for a fence.
- **One Win32 export per object.** Vulkan allows one NT-handle export per object and handle type
  (VUID-VkMemoryGetWin32HandleInfoKHR-handleType-00663,
  VUID-VkSemaphoreGetWin32HandleInfoKHR-handleType-01127): on NVIDIA a repeated export returns the same `HANDLE`, and closing one silently invalidates the
  other. NRI exports once, keeps the handle until the object dies and returns a `DuplicateHandle`
  per call; FD exports stay one per call; D3D12 makes a new handle per call. `SharedHandleTests`
  assert two handles differ; `SharedHandleImportTests` close the first before the second imports.
- **Refusals, by validation only**: a handle of an unshared object ("'shared' is false"); `shared`
  through `CreateBuffer` / `CreateTexture` / `CreatePlaced*`, or committed outside `DEVICE` (D3D12
  cannot share upload or readback heaps; Vulkan's shared allocation is not mapped).
- Import proofs (`SharedHandleImportTests`, Windows): a VK texture into a second VK device (dedicated
  allocation, 16,384 bytes identical); timeline semaphore and D3D12 fence both ways; a D3D12 texture
  on a second D3D12 device.
- **CUDA** (`CudaInteropTests`, Windows, NVIDIA; skips naming `nvcuda.dll`). `cuImportExternalMemory`
  (`OPAQUE_WIN32` for Vulkan, `D3D12_RESOURCE` + `CUDA_EXTERNAL_MEMORY_DEDICATED` for D3D12) and
  `cuImportExternalSemaphore` (`TIMELINE_SEMAPHORE_WIN32`, `D3D12_FENCE`): NRI writes 0x11 and
  signals 1, CUDA waits, reads 0x11, writes 0x5A, signals 2, NRI waits and reads it. A second test
  renders the triangle into a shared texture, releases it to the external family, and CUDA maps it
  as a mipmapped array (format, size, `CUDA_ARRAY3D_COLOR_ATTACHMENT`, dedicated) and copies level 0
  out, identical to NRI's readback. The CUDA device is matched by LUID (`adapterDesc.uid.low`).
  The CUDA structs are declared in the test with explicit offsets and sizes measured from cuda.h
  12.9 on x64 by a C++ probe; `CUDAAPI` is `__stdcall` on Windows.
- **D3D11 refuses D3D12 shared textures** (`OpenSharedResource1` -> `E_INVALIDARG`): NRI creates
  every texture `TYPELESS`, D3D11 opens typed only. The test asserts the refusal; if D3D11 ever
  opens it, revisit the `D3D12_RESOURCE` comment in `NRIDescs.h`.
- **Binary shared fence** (`FenceDesc { SWAPCHAIN_SEMAPHORE, shared }`): a VK binary semaphore;
  `QueueSubmit` passes a value it ignores. Imported into a second device, signals cross both ways,
  layers clean. D3D12: `UNSUPPORTED`, no message.
- **Ownership transfers to the external user.** Exclusive VK images need a release to
  `VK_QUEUE_FAMILY_EXTERNAL` (`NriQueue.External`; NVIDIA tolerates the omission); buffers too,
  through `NriBufferBarrier.SrcQueue` / `DstQueue`: an `EXCLUSIVE` buffer's local side names its
  queue's family (`IGNORED` there draws VUID-vkCmdPipelineBarrier2-srcQueueFamilyIndex-10387), a
  `CONCURRENT` one's is `IGNORED`; D3D12 ignores both. The CUDA round trip releases and acquires on
  both backends; a CONCURRENT shared texture is clean with `IGNORED` + `EXTERNAL`.
  `EXTERNAL_QUEUE` is `static NriPtr(Queue) const` (a pointer to const `Queue` was not assignable to
  the fields, MSVC C2440), which the ABI probe checks.

## Extensions

- Availability: NONE supports every extension except the two wrappers; VK everything except
  `WrapperD3D12`; D3D12 everything except `WrapperVK` (feature bits permitting). SwapChain /
  Streamer tests on NONE prove only the marshalling. `Window` in `NRISwapChain.h` is five members
  side by side, not a union (`descs/NriWindow.cs`).
- **OUT_OF_DATE.** Since NRI #304 (c1261950) acquire, present and `WaitForPresent` report no message
  for it, and `ReportMessage` demotes any `OUT_OF_DATE` report to `WARNING`. Binding:
  `AcquireNextTexture` / `QueuePresent` -> `NriSwapChainStatus.OutOfDate` (an enum, so a suboptimal
  value can follow without an API break; `VK_SUBOPTIMAL_KHR` is `SUCCESS` in NRI and not surfaced,
  undecided). D3D12 never returns it there. `SwapChainResizeTests`: an NRI-created device
  (`swapchain_maintenance1`, present scaling) never got it over 8 resizes including 0x0 and
  minimise/restore; a wrapped raw device with only `VK_KHR_swapchain` got it from present 6 times,
  never from acquire.
- **`WaitForPresent` with `presentId` 0 is refused on every backend** ("'presentId' is 0"), though
  D3D12 ignores the id. `NRISwapChain.h`: "presentId must identify a previously queued
  presentation"; for `QueuePresent`, "Use 0 for an untracked presentation". Caller's rule (in the
  binding's docs): pass the id of the last present queued with a non-zero id.
- **The resize test runs in a child** (`swapchain-resize`): Vulkan presents poison later D3D12
  device creation (Reflex, below). It runs with implicit layers on; the RTSS regression test is
  `OverlayLayerTests` (Device creation and callbacks).
- **Wrapped VK device + swap chain needs `VK_KHR_get_surface_capabilities2`** on the instance
  (`SwapChainVK::Create` calls `vkGetPhysicalDeviceSurfaceCapabilities2KHR` unconditionally; an
  access violation without it). A caller error; NRI's own instance enables it.
- **Swap chain extent.** With present scaling the extent is clamped to the scaled range
  (VUID-VkSwapchainCreateInfoKHR-pNext-07782); without it the surface's current extent is used when
  defined; a zero extent returns `NOT_READY` (never reported). Nothing is refused. Resize child:
  100x80 on a 64x64 window gives 100x80 with scaling, 64x64 without; on a 0x0 window the wrapped
  device answers `NOT_READY` while NRI's own device creates one. Binding: `TryCreateSwapChain`
  returns `NriSwapChainStatus.NotReady`; `CreateSwapChain` throws. Read the real size from the
  swap chain's textures.
- **FIFO_LATEST_READY with `VSYNC` (patch 0017 until v192; upstream since PR #316, 425a8323).** Upstream's capped list (`VSYNC`) is FIFO_RELAXED
  (with `ALLOW_TEARING`) or FIFO; the uncapped one MAILBOX, IMMEDIATE, then FIFO_LATEST_READY
  ("Uncapped frame rate ... Similar to MAILBOX"), then FIFO. NVIDIA always offers MAILBOX, so the
  mode was never chosen. Reordering the uncapped list was rejected: FIFO_LATEST_READY queues like
  FIFO (acquire blocks once every image is queued) and would throttle default callers, and the
  order matches D3D12's uncapped `Present(0, 0)`. Latest-ready is a flavour of vsync, hence a bit
  applied with `VSYNC`, plus a feature bit since NRI never reports the chosen mode. Resize child:
  `latestReadySwapChain` 1, no ERROR; positive control (wrapped device without the extension,
  support check removed): "presentMode is VK_PRESENT_MODE_FIFO_LATEST_READY_KHR, but feature
  presentModeFifoLatestReady is not enabled".
- **Swap chain present modes (patch 0037)**: without `VkSwapchainPresentModesCreateInfoKHR` best
  practices report "[2429328340]
  BestPractices-vkCreateSwapchainKHR-no-VkSwapchainPresentModesCreateInfoKHR-provided" per swap
  chain. `SwapChainResizeTests.A_Vulkan_swap_chain_with_present_scaling_lists_its_present_modes` (a
  child with best practices on) fails only on that warning and errors; also seen there, not NRI's:
  `suboptimal-swapchain-image-count` (two images) and, with implicit layers, an overlay's
  `ImageBarrierAccessLayout` from `vkCmdPipelineBarrier` (NRI records only `...Barrier2`).
- **A streamer without a constant buffer (patch 0048 until v192; upstream since PR #323, 8f89a03c).** `constantBufferMemoryLocation` and
  `constantBufferSize` are `NriOptional`, and `StreamerImpl::Create` creates the constant buffer
  only for a non-zero size, but validation (`ImplVal.cpp:2057`) demanded an UPLOAD location
  whatever the size: "CreateStreamer: 'constantBufferMemoryLocation' must be an UPLOAD heap" for
  the defaults. The patch checks the location only with a constant buffer.
  `StreamerExtensionTests.A_streamer_without_a_constant_buffer_takes_the_default_constant_buffer_location`:
  red without it on both backends, green with it, no constant buffer handed out.
- **Streamer ring: `queuedFrameNum` regions, no +1; the header comment is wrong** (its "adds 1
  under the hood" came with 3f38b1a; 6f80a7c dropped the `++` and kept the comment). The binding's
  docs state the code's behaviour. `StreamerExtensionTests`: with 2, frame 2 reuses frame 0's
  offset.
- **Streamer texture staging.** Block-compressed formats stage rows of blocks (fixed upstream,
  bc3ebe7; v190 staged a 64x64 BC1 mip 0 in 32,768 B instead of 2,048, readback still exact).
  NV12/P010/P016 stay on texel rows (unmeasured). Batches not recorded by `EndStreamerFrame` are
  discarded; the stale-batch check is an `NRI_CHECK` (gone in release builds), so only NRI
  validation refuses one.
- **Streamer initial size (patch 0012 until v192; upstream since PR #315, efb158d1).** Upstream grows from empty: `Grow` sizes exactly
  `Align(offset, 64 KiB) * queuedFrameNum`, parks the old buffer, and `EndFrame` destroys it
  `queuedFrameNum + 1` frames later under the streamer's spin lock, which `StreamBufferData` /
  `StreamTextureData` also hold across `Grow`, map, memcpy and unmap (`MapBuffer` is a pointer add on
  both backends). With the patch: 100 KiB x 2 gives 262,144 B at creation, unchanged over 64 KiB
  frames, 393,216 B after one 192 KiB frame. Not taken: geometric growth (policy), freeing outside
  `EndFrame` (the documented synchronisation point), memcpy outside the lock (a concurrent `Grow`
  replaces the buffer; non-coherent VK memory keeps one flush range per `BufferVK`), a
  fence-retired mode (belongs above NRI).
- **Video capabilities.** H.265 Annex-B writing returns `UNSUPPORTED` for reference sets, VUI,
  scaling lists, tiles. `GetVideoAV1EncodeDecodeInfo` returns pointers into the result struct.
  H.264 decode, 64x64 NV12: D3D12 min = max = 64x64, no references; VK 48x16 to 4096x4096, 16
  references. AV1: Vulkan decode level 73, encode tiles 16x128, tile size 192x64 to 4096x2304,
  references 7/4/7, q 1-255; D3D12 level 41, tiles 1x1, q 1-255, required features 0x1A, supported
  0x3B. Minimum sizes: Vulkan decode AV1 128x128, H.265 144x144, encode AV1 192x128, H.265 160x64;
  D3D12 answers min = max = the size asked. Decodes record and close; validation names each broken
  field.
- **H.264 encode**: D3D12 minimum 256x256 (a 64x64 capability query draws error 1312, SEH); one IDR
  at QP 26 = 277 B, `00 00 00 01 65`. VK minimum 160x64; on v190 the feedback query never became
  available (`vkCmdCopyQueryPoolResults` with WAIT never completed) and VK was skipped. Since
  90112642 (in v191) NRI records only the query index at the resolve and reads the results on the
  host; re-measured 2026-11-07: VK encodes the same IDR, 277 B, `00 00 00 01 65`, through
  `ResolveVideoEncodeFeedback` on GRAPHICS, and the skip is gone. The readback barrier on GRAPHICS
  had named `VIDEO_ENCODE` as its before stage (VUID-vkCmdPipelineBarrier2-srcStageMask-09675, a
  test defect the skip had hidden): the encode was another queue's submit, waited for, so the side
  names no stages.
- **AV1 encode on Vulkan.** Session parameters from a sequence without a colour description failed
  with `VK_ERROR_INVALID_VIDEO_STD_PARAMETERS_KHR`, no validation message (patch 0047 until v192; upstream since PR #322, 0db7e15a): NRI's
  `FillVideoAV1ColorConfig` copied the desc's colour values whatever the flags, while AV1 infers
  "unspecified" (2) without `COLOR_DESCRIPTION_PRESENT` and NRI's own OBU writer omits them; the
  desc's 0 is reserved for primaries and transfer. Measured: refused for 0/0/0 with or without the
  flag and for 1/0/0 with it; accepted for 2/2/2 with it and 1/1/1 without.
  `VideoExtensionTests.AV1_session_parameters_take_a_sequence_without_a_colour_description`.
- **NVIDIA's Vulkan AV1 encode fails into a bitstream without `HOST_VISIBLE` (driver 617.14, RTX
  4070 SUPER; a driver defect, worked around in the test).** Past 0047 the AV1 encode wrote no byte
  and its feedback query never became available, no validation message. Pinned 2026-11-07 with a
  minimal NRI-free program (one 256x256 key frame, intra, CQP q index 40): the feedback query
  answers `VK_NOT_READY` with status -1 (`VK_QUERY_RESULT_STATUS_ERROR_KHR`) and nothing is written
  when the bitstream buffer's memory type is 0 (no flags) or 1 (`DEVICE_LOCAL`), and 35 bytes with
  status 1 for types 2, 3 and 4 (`HOST_VISIBLE`, including `DEVICE_LOCAL | HOST_VISIBLE`); all five
  are in the buffer's `memoryTypeBits`, so every placement is valid Vulkan. Every other NRI choice
  was ruled out in that program, one by one and all together (the AV1 rate-control GOP info, the
  quality level at reset, the separate reset scope, DPB slots 2 / one active reference, the decoder
  model and buffer delays, the encode usage info, profile-independent and `CONCURRENT` resources,
  `disable_cdf_update`, loop-filter deltas, global motion, plane-aspect barriers, video
  maintenance 1/2, unified image layouts, descriptor heaps, zero-initialized memory, maintenance10,
  allocation callbacks, the quality level in the parameters, `vkQueueSubmit2` with a timeline
  semaphore, `ONE_TIME_SUBMIT`, the Khronos layer); NRI's H.264 encode into the same device-local
  memory works. The AV1 encode test puts the Vulkan bitstream in `HOST_READBACK` memory, and then
  Vulkan encodes a 39-byte key frame (an OBU stream, `32 A2 ...`). Caller's rule: on NVIDIA Vulkan
  give the AV1 bitstream a host-visible location. Not NRI's: NRI does not choose the memory.
  Still present on driver 621.14 (re-measured 2026-11-07). Not reported to NVIDIA yet: revisited on
  2027-01-07 against the then-current driver (owner).
- **AV1 encode info per backend.** Vulkan's payload is OBUs, so NRI describes a frame only from the
  payload passed as the header (`UNSUPPORTED` without it); D3D12's is bare tile data, so the
  reverse. The described payload is the frame OBU on Vulkan (34 B at 5: frame header, then the
  tile group) and the tile itself on D3D12; a tile's offset is relative to the payload, so the one
  tile ends where the payload does. With `seq_force_screen_content_tools` / `seq_force_integer_mv`
  0 in the sequence, a picture asking for screen-content tools or integer MVs is refused on Vulkan
  ("session parameters do not allow screen-content tools", as AV1 requires) and ignored on D3D12.
  D3D12's AV1 and H.265 encode need no feedback resolve.
- **D3D12 AV1 encode with default picture flags** (fixed upstream, N8, af45e72): the encoder needs
  `AUTO_SEGMENTATION`, `LOOP_RESTORATION_FILTER`, `CDEF_FILTERING`; v190 enabled auto segmentation
  only with `SEGMENTATION_ENABLED` ("[1306] ... ENABLE_FRAME_SEGMENTATION_AUTO is required in
  RequiredFeatureFlags but not set"). The test encodes with default flags and without
  `SEGMENTATION_ENABLED`. Under CQP the picture's base q index is ignored: set it through the rate
  control. Key frames at q 40 and 160: 22 and 21 bytes of bare tile data.
- **`GetVideoAV1EncodeDecodeInfo` on D3D12** (N9; upstream's shape since 57d92ff, af45e72, 9131c1b):
  `pictureDesc` and `av1PictureDesc` (the submitted descs) are required without a header and give
  the frame type; a frame without references is a key frame; `savedFrameStates` (8, by slot) serve
  headers that inherit state; a payload header answers `UNSUPPORTED` (v190 answered FAILURE
  silently). Binding: `PictureDesc`, `Av1PictureDesc`, `SavedFrameStates`.
- **An H.265 decode capability query at 64x64 removed the device** on NVIDIA's D3D12 driver ("[233]
  ... DXGI_ERROR_DRIVER_INTERNAL_ERROR", in NRI's decoder-heap probe); the tests use 256x256.
- **Reflex**: the D3D12 report is all zero after 72 frames, filled and ordered after 200. Once Vulkan
  has presented in a process, later `D3D12CreateDevice` calls return 0x887A0007
  (`DXGI_ERROR_DEVICE_RESET`), with or without `ALLOW_LOW_LATENCY` and `VSYNC`; the VK case is
  skipped naming that (`LowLatencyExtensionTests.cs`).

## Wrappers

- All twelve import wrappers carry a dispatch, copies, a texture upload + readback, and timestamps,
  byte-exact (`WrapperExtensionTests.Imports.cs`).
- **A wrapped command buffer counts as recording from creation** under NRI validation (`Begin`:
  "already in the recording state"). Begin natively (or pass the D3D12 list open),
  `SetDescriptorPool`, record, `End`.
- **`CreateBufferVK` records usage NONE**: a buffer barrier with storage access is refused; a global
  barrier passes.
- `CreateMemoryVK` with no mapping: NRI maps it itself. `CreateAccelerationStructureVK` / `D3D12`:
  the caller names the type; needs `RayTracing`. Raw VK devices on macOS: portability opt-in.
- **Never Vortice's `Vulkan.GetApi` in tests**: 3.3.0 caches tables in static dictionaries keyed by
  handle value, dropped only by `vkDestroyInstance` / `vkDestroyDevice` through that table; NRI
  destroys its own objects, so a later device at the same address gets dead pointers (CI runners and
  flakes, item 2). `tests/SharpNRI.Tests/VulkanApi.cs` builds a fresh table per object.
- **`CreateQueryPoolVK` maps the query type by value (binding)**: `ACCELERATION_STRUCTURE_SIZE` and
  `MICROMAP_COMPACTED_SIZE` are 1000386001 and 1000396001, as in NRI's `GetQueryTypeVK` (the
  serialization types' ...000 values made a raw size pool report `TIMESTAMP`). Unknown types map to
  `TIMESTAMP`.
- `CreatePipelineVK`: NRI draws with a wrapped raw graphics pipeline; on a wrapped ray-tracing
  pipeline `WriteShaderGroupIdentifiers` equals `vkGetRayTracingShaderGroupHandlesKHR`, packed, at a
  wider stride.

## Flat exports and formats

- Twelve flat exports on static `Nri`. `nriGetFormatProps(MAX_NUM)` returned another format's entry
  (unchecked index), so `Nri` throws `ArgumentOutOfRangeException` for `MAX_NUM` and above.
- Formats (108): on Linux/macOS payloads every DXGI conversion is 0 and `ConvertDXGIFormatToNRI` is
  `UNKNOWN` (the table needs D3D11 or D3D12 support compiled in); `FlatExportTests` measures that.
  Windows: DXGI round trips hold, except DXGI has none for the 38 ETC2/ASTC formats; DXGI 18 ->
  `RGB32_SINT`. NRI -> VK -> NRI is the identity for all 108 formats (since NRI 5ed8bb6; v190 gave
  `UNKNOWN` for the 52 block-compressed ones); VK 125 `X8_D24` -> `D24_UNORM_S8_UINT` is one-way.
  BC1 RGB (131, 132) stay `UNKNOWN`: NRI has only BC1 RGBA.

## UNSUPPORTED without a capability

Audited 2026-11-07 on v191 + patches (`Source/VK`, `Source/D3D12`, `Source/Shared`): the paths where a
call validation accepted returns `UNSUPPORTED` and no `DeviceDesc` field, `GetFormatSupport` or query
says so first. Shared objects are covered by the Try forms (Shared handles); the rest is documented
here and in the guide (Traps).

| Call | Backend | Condition | Message |
|---|---|---|---|
| shared `CreateCommittedBuffer` / `Texture`, shared `CreateFence` | VK; D3D12 binary fence | export refused by the driver (`BufferVK.hpp:44`, `TextureVK.hpp:53, 61`, `FenceVK.hpp:22`); D3D12 has no binary fence (`FenceD3D12.hpp:11`) | silent; `Try*` forms |
| `CreateUpscaler` FSR / XeSS | VK, D3D12 | the runtime or driver refuses (`FFX_API_RETURN_NO_PROVIDER`, `XESS_RESULT_ERROR_UNSUPPORTED*`; `UpscalerInterface.hpp:216, 382`); `IsUpscalerSupported` answers what is compiled in | ERROR (reported as `EXT_ERROR`, v192) |
| `CreateUpscaler` NIS | all | without `shaderFeatures.storageWriteWithoutFormat`, an output format other than `RGBA8_UNORM`, `RGBA16_SFLOAT`, `RGBA32_SFLOAT` (`UpscalerInterface.hpp:59-72, 868`) | silent |
| `GetVideoAV1EncodeDecodeInfo` | VK / D3D12 | VK needs the payload header, D3D12 refuses it (`VideoSessionVK.hpp:487`, `VideoSessionD3D12.hpp:148`; Extensions) | silent |
| `WriteVideoAnnexBParameterSets` | all | H.264 outside the High profiles or with `TRANSFORM_8X8_MODE`; H.265 with reference sets, long-term references, VUI, scaling lists or tiles (`SharedVideo.h:205, 538`) | silent |
| `CreateVideoSessionParameters` | VK | AV1 encode whose sequence header the driver overrides (`VideoSessionParametersVK.hpp:700`) | ERROR |
| `CreateVideoSession` | VK | no memory type for a session memory requirement (`VideoSessionVK.hpp:672`) | silent |
| `QueryVideoMemoryInfo` | VK | no `VK_EXT_memory_budget` (`DeviceVK.hpp:3241`) | silent |
| `nriReportDeviceLostInfo` | VK | no `deviceFault` (`DeviceVK.hpp:2619`) | silent |
| `GetDisplayDesc` | VK on Linux / macOS | no D3D backend compiled: 80-nit defaults filled (`SharedExternal.h:763`) | silent |
| `WaitForPresent` | D3D12 | a swap chain below `IDXGISwapChain2` while `waitableSwapChain` is 1 (`SwapChainD3D12.hpp:200`; never seen) | silent |
| any call with a native result | VK, D3D12 | `VK_ERROR_FORMAT_NOT_SUPPORTED`, `..._NATIVE_WINDOW_IN_USE_KHR` and the other "not supported" codes (`ConversionVK.h:540-551`), `DXGI_ERROR_UNSUPPORTED` (`SharedExternal.hpp:268`) | ERROR |

Also found: Vulkan's `GetVideoCapabilities` reports an ERROR when the driver refuses a profile
while probing (`VideoSessionVK.hpp:271`); with validation on, a micromap below `tiers.rayTracing` 3 is
`INVALID_ARGUMENT` and `WaitForPresent` without `WAITABLE` is `FAILURE` (the backends' `UNSUPPORTED`
only without validation); validation answers `UNSUPPORTED` itself for the shared features,
`HOST_COPY`, DXIL, descriptor heaps, `maxReferenceNum`, the upscaler type and an Annex-B codec. No
query pool returns `UNSUPPORTED`.

## Tests and skips

- Skip strings name the capability: feature bit, extension, file. The exceptions quote a measured
  defect:
  - `TestDevice.IsAppleParavirtualVK` (adapter "Apple Paravirtual device"): the `HOST_TRANSFER`
    crash and timestamps (MoltenVK and the paravirtual Mac).
  - `TestDevice.IsMoltenVK` (Vulkan on macOS): defects in MoltenVK's own code, which every Mac
    shares: the region-resolve abort (MoltenVK and the paravirtual Mac).
  - Backend-keyed: Reflex on VK (Extensions), NVIDIA Vulkan's refused
    micromap-size query pools (Ray tracing).
  - Lavapipe's clipping-primitive counter (`TestDevice.IsLavapipeVK`): a known bug to remove. The
    test asserts the wrong value 0 on lavapipe instead of skipping, so the day Mesa counts the
    primitives it fails, naming the exception, and the exception goes.
  - Lavapipe's memory budget (`TestDevice.IsLavapipeVK`): its heap usage is the machine's, not the
    process's, so the 64 MiB delta is skipped there after the budget checks (Debug aids). Not
    assertable as a wrong value: it moves with everything else on the machine.
  - The same pattern (an allowance that fails when it is no longer needed; the D3D12 device-lost
    child's `[989]` allowance did that at v192 and went: D3D12 and the Agility SDK) for the upscaler
    constant-image test's allowance of NGX's `[527]` barrier errors on `nv.ngx.dlssd.resource`
    (D3D12 DLRR below its creation size; Upscalers and vendor runtimes), and the skip of D3D12
    DLRR with an R8 reactive mask, which removes the device and poisons the process.
- **No skip turns a wrong result into a pass**, on a software adapter or any other: a wrong answer
  fails with enough in the message (the hit map for ray queries) to diagnose it. The backend-keyed
  skips cover cases that cannot run to completion (a hang, a refused query pool, a poisoned
  process), not wrong answers. A defect NRI owns is patched and asserted, not skipped.
- Best-practices and syncval tests set the Khronos layer's environment variables in the process
  before device creation, use a positive control or the committed 64 KiB buffer's
  `small-dedicated-allocation` warning as the marker that the checks are active, and skip where .NET
  does not write the native environment (Linux, macOS).
- `SHARPNRI_TRACE=1` when a test host dies: the message stream is the only record.
- Expected skip shape (counts move as tests are added): Windows hardware skips almost nothing
  beyond the upscaler tests, which skip on the Base payload (every CI leg too) and run only through
  `tools/upscalers.ps1`, and RenderDoc without `renderdoccmd.exe`; Windows CI (WARP, lavapipe) skips RenderDoc and what
  the software adapters lack; Linux skips D3D12, Agility, NIS, shared fences, the import proofs and CUDA, the
  environment-variable layer checks; macOS additionally skips shared handles, RT, mesh, video,
  descriptor heaps, shading rate, PE imports, the Windows swap-chain window, payload contracts for
  Upscalers/Agility, and the two paravirtual defects.

