# Upstream — what to send to NRI, and in what order

Each patch in `patches/` is either a bug upstream should fix or an extension upstream might want.
This file holds the report text for each, so that filing is a copy and a link to a commit rather
than a reconstruction. Status is updated as they are filed and answered.

**This is the live copy, on the `next` branch.** Master's copy is the v190 document, frozen at the
status it had when the branches split on 2026-10-21 (CLAUDE.md: Branches). Line numbers and
measurements here are against the NRI commit `next` pins.

2026-10-19: the maintainer fixed all six filed issues on main within the day ("Thanks! Good
finding"), said on #277 "I like your short and clear form. PRs are OK too", and the phase 2 condition
(he engages) is met. Main is ten commits past v190 at 8ee40ca, still `NRI_VERSION 190`, untagged.
The other 17 patches apply in order onto 8ee40ca and every one is recognised by the scripts'
`--reverse -C0` check. Measured with the six removed (a trial tree, not merged): native build
clean, L0 drift `TextureViewDesc.layout` (3d8ca25) and `Result::NOT_READY` (8ee40ca, WGPU) plus
anonymous-record renames, `NativeAbiTests` clean; Windows 292 tests, 0 failed (NVIDIA); Linux
lavapipe on the stock 1.3 loader 292, 0 failed, the two tests 0004 was cut for passing on
a4e562e; the dropped patches' own tests (BC staging, the format round trip, `HAS_BUFFER`, the
stale-extent swap chain) pass on upstream's fixes. Line numbers in the drafts of the next round
are main's.

2026-10-21: the second round (#281-#284) was fixed on 2026-10-20, three as filed and #281
differently: a texture side with access `NONE`, a defined layout and stages gets
`D3D12_BARRIER_ACCESS_COMMON` and keeps the stages' sync, and only a side without stages stays
`SYNC_NONE` ("forcing `SYNC_NONE` discards the caller's requested execution dependency and imposes
additional restrictions on surrounding accesses and barriers. Using `COMMON` access preserves
synchronization, at the cost of potentially extra cache work"). Main also took a community PR
(#285) that turns the swap-chain extent check into a clamp and answers `NOT_READY` for a zero
extent, and 48d4a33, which makes attachment resolves explicit in barriers and renumbers
`StageBits` / `AccessBits`. `next` pins 2f7a49f with thirteen patches, 0002 re-cut. Measured:
Windows 297 tests, 0 failed (NVIDIA; the COMMON-access shapes draw no message at all, not even a
warning); Linux lavapipe 297, 0 failed.

On #49 he answered: "A PR would be very welcome! Regarding the API shape, I would prefer extending
the existing `buffer`, `texture` and `fence` descs if the additions fit naturally and keep the API
small. There's no need for a separate extension just to introduce parallel creation functions. If
the functionality requires substantial new API surface or fits more cleanly into a separate
interface, an extension would be preferable." Read against NRI main: textures and buffers can take
an export field in `TextureDesc` / `BufferDesc`, plus `AllocateMemoryDesc` for placed resources,
with `CreateCommittedTexture` / `CreateCommittedBuffer` as the dedicated path that 0002's
`CreateSharedTexture` is today. Fences have no desc: `CreateFence` takes an initial value only, with
`SWAPCHAIN_SEMAPHORE` as its one sentinel, so the fence desc he names would be a new `FenceDesc`
that `CreateFence` takes (`initialValue` plus the export field), which is his API to change. The
handle getters are not parallel creation functions and fit beside Core's `Get*NativeObject`; they
are needed in any shape, because Core exposes no native fence or memory object for an application
to export itself. The `EXTERNAL_QUEUE` sentinel follows `HAS_BUFFER`. Placed resources (the field on
`AllocateMemoryDesc` plus a memory getter) can follow: some Vulkan drivers export images only from
dedicated allocations, so they need a capability check first. Read this way, the reply needs no
question; the draft states the shape and offers the PR.

| Patch | Kind | Status |
|---|---|---|
| 0003 validation `HAS_BUFFER` crash | bug | filed 2026-10-16, [#279](https://github.com/NVIDIA-RTX/NRI/issues/279); fixed 2026-10-19 in 7915eca (also refuses `HAS_BUFFER` at build time); dropped on `next` 2026-10-21 |
| 0004 Vulkan minor version above the instance's | bug | filed 2026-10-16, [#278](https://github.com/NVIDIA-RTX/NRI/issues/278); fixed 2026-10-19 in a4e562e (`m_InstanceVersion`, another shape); dropped on `next` 2026-10-21 (by hand: ours still applied on top) |
| 0005 `MemoryHeap::alignment` uninitialised (read before written) | bug | filed 2026-10-16, [#275](https://github.com/NVIDIA-RTX/NRI/issues/275); fixed 2026-10-19 in 9b45d77; dropped on `next` 2026-10-21 |
| 0008 `nriConvertVKFormatToNRI` misses every compressed format | bug | filed 2026-10-16, [#276](https://github.com/NVIDIA-RTX/NRI/issues/276); fixed 2026-10-19 in 5ed8bb6 (our table); dropped on `next` 2026-10-21 |
| 0010 Streamer stages compressed textures in texel rows | bug | filed 2026-10-16, [#274](https://github.com/NVIDIA-RTX/NRI/issues/274); fixed 2026-10-19 in bc3ebe7; dropped on `next` 2026-10-21 |
| 0014 Swap chain extent checked against the unscaled range with present scaling | bug | filed 2026-10-16, [#277](https://github.com/NVIDIA-RTX/NRI/issues/277); fixed 2026-10-19 in 81245bd; dropped on `next` 2026-10-21 |
| 0020 D3D12 texture barrier side with access `NONE` and a defined layout keeps the stages' sync | bug | filed 2026-10-19 against main 8ee40ca, [#281](https://github.com/NVIDIA-RTX/NRI/issues/281); fixed 2026-10-20 in 48a14c3, another shape: a side with stages gets `COMMON` access and keeps its sync, one without stages stays `SYNC_NONE` ("forcing SYNC_NONE discards the caller's requested execution dependency"); dropped on `next` |
| 0021 Vulkan `StageBits::COPY` does not cover `CmdZeroBuffer` (`vkCmdFillBuffer`) | bug | filed 2026-10-19 against main 8ee40ca, [#282](https://github.com/NVIDIA-RTX/NRI/issues/282); fixed 2026-10-20 in 1163658, ours verbatim; dropped on `next` |
| 0022 D3D12 copy layouts refused in copy command lists | bug | filed 2026-10-19 against main 8ee40ca, [#283](https://github.com/NVIDIA-RTX/NRI/issues/283); fixed 2026-10-20 in 732f737, ours without the sentinel guard, which 0002 now carries; dropped on `next` |
| 0023 Vulkan queue family indices on `CONCURRENT` images make every hand-off an ownership transfer | bug | filed 2026-10-19 against main 8ee40ca, [#284](https://github.com/NVIDIA-RTX/NRI/issues/284); fixed 2026-10-20 in 2603edc, the sharing mode recorded at creation; dropped on `next`, 0002 keeps the external family |
| 0002 SharedHandle extension | proposal | offered 2026-10-16 as a [comment on the maintainer's #49](https://github.com/NVIDIA-RTX/NRI/issues/49#issuecomment-6212054525); he answered 2026-10-21: "A PR would be very welcome", preferring fields on the existing buffer, texture and fence descs, an extension only for substantial new API surface (2026-10-21 note above) |
| 0006 `samplerLodBiasMax` above what a portability device allows | bug, fix shape open | phase 2, drafted 2026-10-19, not filed; offer limit 0, a feature bit, or a clamp |
| 0016 Validation refuses a pipeline layout with `shaderStages = ALL` | design question | phase 2, drafted 2026-10-19 as a question, not filed: validation and D3D12 disagree (`PipelineLayoutD3D12.hpp:27` maps `ALL`, `:83/:98/:308` do not); ask which is intended |
| 0017 `SwapChainBits::LATEST_READY`: FIFO_LATEST_READY with `VSYNC` | proposal | phase 2, drafted 2026-10-19, not filed; the uncapped order is his and stays |
| 0018 `disableD3D12BreakOnError`: D3D12 info-queue break as a switch | proposal | phase 2, drafted 2026-10-19, not filed; the default (break) is his and stays |
| 0019 `enableD3D12GpuBasedValidation`: GPU-based validation as a switch | proposal | phase 2, drafted 2026-10-19, not filed |
| 0012 `StreamerDesc::dynamicBufferDesc.size` as the initial ring size | proposal | phase 2, drafted 2026-10-19, not filed; per-frame vs total semantics is his call |
| 0024 `nriEnumerateAdapters2`: adapter enumeration filtered by graphics API | proposal | phase 2, drafted 2026-10-19, not filed |
| 0001 Agility SDK from a path | proposal | phase 2, drafted 2026-10-19, not filed: comment on his closed #157 ("it can't be improved, unfortunately, in a generic case") or a new issue; disclose the process-global semantics |
| 0007 Apple loader link should be optional | build option | not filed: his PR 134 decision, a packaging case he does not have |
| 0009 `OUT_OF_DATE` from acquire/present reported as an error | reporting | not filed: `OUT_OF_DATE` is negative, so no `AbortExecution`; a misreport, not a stop |
| 0013 EXCLUSIVE images created with queue family indices | layer warning | not filed: Vulkan ignores the indices; cosmetic |
| 0015 Khronos message id name not forwarded | reporting | not filed: a format he changed on purpose in 59d0785 |
| N1 Vulkan vertex attributes bind to `streamIndex`, not the stream's `bindingSlot` | bug | phase 1 candidate, not filed: owner's trigger |
| N2 Vulkan empty vertex input makes the binding stride dynamic | bug | phase 1 candidate, not filed: owner's trigger |
| N3 D3D12 `CmdClearAttachments` crashes in a stencil-only pass | bug (crash) | phase 1 candidate, not filed: owner's trigger |
| N4 D3D12 does not clear the stencil of a depth-stencil view passed as `depth` | bug | phase 1 candidate, not filed: owner's trigger |
| N5 D3D12 2D texture views ignore `layerOffset` | bug | phase 1 candidate, not filed: owner's trigger; the fix shape (refuse or document) is his |
| N6 D3D12 micromap barriers with `MICROMAP_WRITE` fail `Close` | bug | phase 1 candidate, not filed: owner's trigger |
| N7 D3D12 acceleration-structure memory reported 128-byte aligned; placements need 256 | bug | phase 1 candidate, not filed: owner's trigger |
| N8 D3D12 AV1 encode with the default picture flags fails `Close` | bug | phase 1 candidate, not filed: owner's trigger |
| N9 D3D12 `GetVideoAV1EncodeDecodeInfo`: silent `FAILURE` on a payload header, frame type inferred | bug, fix shape open | phase 2: the desc carries no frame type, so the fix is his API choice |
| N10 An allocator that returns null crashes NRI instead of failing the creation | bug (crash) | phase 1 candidate, not filed: owner's trigger |
| `WriteShaderGroupIdentifiers` range unchecked | bug | held: read, not measured; validation checks only the stride (`PipelineVal.hpp:25`), and D3D12 indexes `m_ShaderGroupNames` without a bound (`PipelineD3D12.hpp:691`) |
| D3D12 attachment resolve into a non-zero layer or mip | bug | not filed: fixed on main by 48d4a33 ("array layers and destination mip indexing"); v190, and so master, still has it |
| Vulkan pipeline-statistics pool adds the mesh counters on `features.meshShader` | bug | held: read in `QueryPoolVK.hpp:38`, not measured; every device here (NVIDIA, lavapipe) has both mesh shaders and mesh-shader queries |
| Draw-parameters emulation on Vulkan | design question | phase 2: validation demands `DrawBaseDesc` strides on every backend, while Vulkan reads plain records at `offset` |

The texts as filed, and the audit that preceded them (every claim re-verified against the unpatched
tree by reviewers given the patches but not these conclusions), are the sections below; the filed
ones were posted in a shorter form (Observed, Cause with line numbers, Fix as a snippet).

---

## 0003 — Validation layer crashes on the documented `HAS_BUFFER` sentinel

**Title:** Validation layer dereferences `HAS_BUFFER` in `ConvertBottomLevelGeometries`

`NRIRayTracing.h` defines `HAS_BUFFER` as `(Buffer*)1` and documents it for
`AccelerationStructureDesc::geometries`: "can be used to indicate a buffer presence (no real
entities needed at initialization time)". The Vulkan and D3D12 backends both test for it
(`BufferVK.h`, `BufferD3D12.h`). The validation layer does not: `ConvertBottomLevelGeometries` in
`Source/Validation/ConversionVal.hpp` passes every geometry buffer through `NRI_GET_IMPL`, which
calls `GetImpl()` on address 1. With `enableNRIValidation` on, the first
`CreateCommittedAccelerationStructure` (or `CreateAccelerationStructure`) whose desc uses the
sentinel is an access violation before any message is reported.

Reproduces on v190, Vulkan and D3D12, with a one-triangle BLAS desc whose `vertexBuffer` is
`HAS_BUFFER`. Fix: return the sentinel unchanged for the five buffer fields the conversion
touches. Patch attached (`patches/0003-validation-has-buffer-sentinel.patch`), 12 lines.

## 0004 — `DeviceVK` uses the physical device's API version when the instance is older

**Title:** Vulkan: `m_MinorVersion` comes from the physical device, not from the instance

`DeviceVK::Create` sets `m_MinorVersion` from `VkPhysicalDeviceProperties::apiVersion`, while
`CreateInstance` asks for `min(vkEnumerateInstanceVersion(), 1.4)`. With a 1.3 loader in front of
a 1.4 driver (Ubuntu 24.04's stock `libvulkan1` is 1.3.275; Mesa 25's lavapipe reports 1.4.318)
the device is treated as 1.4: `ResolveDispatchTable` loads `vkCmdPushConstants2` and the other
maintenance6 core names, which Mesa refuses for a 1.3 instance, so `CmdSetRootConstants` calls a
null pointer; and `VK_EXT_host_image_copy` is not enabled because it is "core in 1.4", so the
validation layer rejects `VK_IMAGE_USAGE_HOST_TRANSFER_BIT`. Both go away with a 1.4 loader on
`LD_LIBRARY_PATH`.

Reproduces on v190, WSL2 Ubuntu 24.04, lavapipe (LLVM 20), stock loader: any root-constant
set, or any `HOST_TRANSFER` texture with validation on. Fix: clamp `m_MinorVersion` to the
version the instance was created with. Patch attached
(`patches/0004-vulkan-clamp-minor-version-to-instance.patch`), 14 lines.

## 0005 — Linux build fails with GCC: `MemoryHeap::alignment` is never initialised

**Title:** GCC 13: `-Werror=maybe-uninitialized` on `HelperDeviceMemoryAllocator::MemoryHeap::alignment`

`MemoryHeap`'s constructor (`Source/Shared/HelperInterface.hpp`) initialises `size` and `type` but
not `alignment`. With GCC 13.3 and any optimised build (`-O2`, `-O3`) `-Wextra -Werror` turns the
resulting `maybe-uninitialized` into a build failure at `HelperInterface.h:44`. Clang does not
warn. The CI workflow does not catch it because `Scripts/Linux/1-Deploy.sh` configures without
`CMAKE_BUILD_TYPE`, so the Ubuntu job compiles unoptimised and the analysis never runs.

Reproduces on v190, Ubuntu 24.04, `cmake -DCMAKE_BUILD_TYPE=Release` with the distribution GCC.
Fix: `, alignment(0)` in the initialiser list. Patch attached
(`patches/0005-helper-memoryheap-alignment-init.patch`), one line; with it the library builds
clean under GCC and passes our suite.

## 0006 — `samplerLodBiasMax` permits a bias a portability device forbids

**Title:** Vulkan on MoltenVK: `samplerLodBiasMax` ignores `VkPhysicalDevicePortabilitySubsetFeaturesKHR::samplerMipLodBias`

`DeviceVK::Create` copies `VkPhysicalDeviceLimits::maxSamplerLodBias` into
`DeviceDesc::other.samplerLodBiasMax`. Under `VK_KHR_portability_subset` a device may report
`samplerMipLodBias = false`, and then every sampler must have `mipLodBias == 0`
(VUID-VkSamplerCreateInfo-samplerMipLodBias-04467) whatever the limit says. MoltenVK 1.4.2 on an
"Apple Paravirtual device" (GitHub's macOS arm64 runner) reports the feature as false and
`maxSamplerLodBias = 15.999`, so a caller that keeps `SamplerDesc::mipBias` within NRI's reported
limit gets "mipLodBias (portability error) is 0.500000, but samplerMipLodBias not supported" from
the Khronos validation layer.

Reproduces on v190, macOS 26.6, MoltenVK 1.4.2, validation layer 1.4.357: `CreateSampler` with
`mipBias = 0.5`. NRI already chains `PortabilitySubsetFeatures` under `__APPLE__` and reads
`imageView2DOn3DImage` from it. Fix: report 0 when the subset is enabled without
`samplerMipLodBias`. Patch attached (`patches/0006-vulkan-portability-sampler-lod-bias.patch`), 6
lines; with it the device reports 0 and a sampler made within the limit validates clean.

## 0007 — The Apple link of the Vulkan loader makes the library unloadable without it

**Title:** `NRI_ENABLE_VULKAN_LOADER_LINKING`: make PR 134's Apple loader link optional

PR 134 added `find_package(Vulkan REQUIRED)` and a link of `Vulkan_LIBRARIES` into `NRI_VK` on
Apple so that samples started from an IDE without the Vulkan SDK environment could find the
loader; the review said the link "is just not needed because it gets loaded dynamically, except
your specific case" and asked for a condition, and it merged unconditional. A library built on a
machine with Homebrew's loader records `/opt/homebrew/opt/vulkan-loader/lib/libvulkan.1.dylib` as
a load-time dependency and does not load on a Mac without that file, NONE backend included,
while `DeviceVK` still `dlopen`s `libvulkan.1.dylib` at device creation exactly as on Linux.

Proposal: an option, default ON on Apple so the sample workflow keeps working, OFF for anyone
shipping the library. Measured on v190 on a GitHub macOS runner: with the option off the library
links only the system, loads without a loader, and the Vulkan backend finds Homebrew's loader
through `DYLD_FALLBACK_LIBRARY_PATH` as before. Patch attached
(`patches/0007-cmake-vulkan-loader-link-option.patch`), 3 lines changed.

## 0008 — `nriConvertVKFormatToNRI` returns `UNKNOWN` for every block-compressed format

**Title:** `VKFormatToNRIFormat`: the table stops at `VK_FORMAT_D32_SFLOAT_S8_UINT`

`VK_FORMAT_TABLE` in `Source/Shared/SharedExternal.hpp` has 131 entries, so every VkFormat from
131 on falls through to the four special cases after it (the two-plane YUV formats and
`A4R4G4B4`) and then to `UNKNOWN`. VkFormat 131 to 184 are BC1 to BC7, ETC2/EAC and ASTC LDR. NRI
has all of them as `Format`s that `NRIFormatToVKFormat` (`g_Formats` in `ConversionVK.hpp`)
produces, so NRI -> VK -> NRI is not an identity for any of the 52 compressed formats, and a
compressed `VkImage` imported with `CreateTextureVK` or described by its `VkFormat` cannot be
named in NRI terms.

Measured on v190 through the flat exports: `nriConvertVKFormatToNRI(nriConvertNRIFormatToVK(f))`
is `f` for every uncompressed format and `UNKNOWN` for all 52 compressed ones. Fix: extend the
table to 184 in its existing style, one commented entry per VkFormat; BC1's RGB variants (131,
132) stay `UNKNOWN` because NRI has only BC1 RGBA. With it the round trip holds for all 108
formats. Patch attached (`patches/0008-vulkan-format-table-compressed.patch`), 55 lines, table only.

## 0009 — `OUT_OF_DATE` from `AcquireNextTexture` / `QueuePresent` is reported as an error

**Title:** Vulkan swap chain: the documented `OUT_OF_DATE` result should not be reported as `Message::ERROR`

`NRISwapChain.h` documents `OUT_OF_DATE` as a result of `AcquireNextTexture` ("VK only: may return
OUT_OF_DATE"), `NRIDescs.h` explains it as the swap chain being out of date without
`features.resizableSwapChain`, and `SwapChainDesc::scaling` says unsupported scaling triggers it
on resize. It is an expected outcome the caller handles by recreating the swap chain, not a
failure. `SwapChainVK` nevertheless passes `VK_ERROR_OUT_OF_DATE_KHR` through
`NRI_RETURN_ON_BAD_VKRESULT`, which reports `Message::ERROR` for any negative VkResult before
returning. An application that treats reported errors as errors (a test harness that fails on
them, the default `AbortExecution` breaking into the debugger, a log scraped by CI) sees a failure
on every window resize and has to match message text to tell it from a real one.

This is a reporting defect only: the returned `Result` is right. Fix: in `AcquireNextTexture` and
`Present`, return `Result::OUT_OF_DATE` for exactly `VK_ERROR_OUT_OF_DATE_KHR` before the macro,
without a report; every other negative result is reported as today. The validation layer passes
both results through without a report of its own, and D3D12 does not return `OUT_OF_DATE` from
these calls. Measured on v190, RTX 4070 SUPER, a wrapped VkDevice without
`swapchain_maintenance1` (so NRI creates the swap chain without present scaling): resizing the
window made `QueuePresent` return `OUT_OF_DATE` 6 times over 8 resizes; with the patch no message
is reported and the swap chain is recreated normally. Patch attached
(`patches/0009-vulkan-swapchain-out-of-date-not-an-error.patch`), 9 lines.

## 0001 — Load the Agility SDK from a path when the host cannot export `D3D12SDKVersion`

**Note (2026-10-17):** the patch's code comment "IMPORTANT: Must be called before any other D3D12
entry point in this process" is stronger than the measurement: NRI's own adapter enumeration calls
`D3D12CreateDevice` on the inbox runtime before it, and the Agility apply still succeeds. What must
come first is the first D3D12 device the process keeps. Reword the comment in the filed form; the
patch is not re-cut for it.

**Title:** `DeviceCreationDesc::d3d12AgilitySdkPath`: Agility SDK for hosts that are not native executables

NRI's Agility route needs the host executable to compile in `NRIAgilitySDK.h` and export
`D3D12SDKVersion` / `D3D12SDKPath`. A managed runtime, a plugin, a scripting host or an editor that
hosts the renderer cannot export those, so the D3D12 backend silently runs on the inbox runtime
and everything newer than it (`OPTIONS22` and up) is absent. Microsoft's answer for this case is
`ID3D12SDKConfiguration1::CreateDeviceFactory`, which takes a version and a directory and needs
neither exports nor Developer Mode.

The patch adds one optional field. When set, `DeviceD3D12::Create` creates a device factory for
`NRI_AGILITY_SDK_VERSION_MAJOR` from that directory and applies it to the process before any other
D3D12 entry point, so the debug layer, DRED, AGS and `D3D12CreateDevice` all see the SDK. Nothing
changes when the field is null. Measured on v190 from a .NET test host: NRI reports
`Using ID3D12Device15` and the `OPTIONS22` warning disappears. The alternative, creating the
device from the factory and wrapping it with `nriCreateDeviceFromD3D12Device`, also works and is
what we fall back to; it loses `Robustness`, `DeviceLostInfoLevel` and ray-tracing validation,
which only the NRI-created path offers. Patch attached, 1 header line and ~20 lines.

## 0002 — SharedHandle extension

**Title:** Proposal: an extension to export textures and fences to other APIs and processes

NRI has no way to export a texture's memory or a fence: `TextureDesc` and `AllocateMemoryDesc`
carry no exportable flag and `CreateFence` takes no desc. A compositor, an editor hosting the
renderer, a capture tool or a second process importing a frame needs exactly that. The patch adds
`NRISharedHandle.h`: `CreateSharedTexture` (committed, dedicated, exportable), `CreateSharedFence`,
`GetTextureSharedHandle` and `GetFenceSharedHandle`, each returning a caller-owned handle
(`OPAQUE_WIN32` / `OPAQUE_FD` on Vulkan, NT handles on D3D12), plus an `EXTERNAL_QUEUE` sentinel
for barriers that release to or acquire from `VK_QUEUE_FAMILY_EXTERNAL`. `CreateSharedFence` with
the `SWAPCHAIN_SEMAPHORE` initial value, the value NRI already uses to ask for a binary semaphore,
creates an exportable binary semaphore on Vulkan, the signal-once/wait-once object external
compositors import; D3D12 has no binary fences and answers `UNSUPPORTED`. Vulkan enables the
external-memory and external-semaphore extensions itself and allocates the shared texture's memory
directly so the export info can be chained; D3D12 uses `D3D12_HEAP_FLAG_SHARED` and
`D3D12_FENCE_FLAG_SHARED`; NONE returns dummies; the validation layer wraps the four calls.

Measured on v190, RTX 4070 SUPER: a Vulkan texture imports into a second Vulkan device byte for
byte, the timeline semaphore signals both ways, and the binary semaphore, imported into a binary
VkSemaphore, is signalled and waited by queue submits in both directions with the Khronos layer
clean; a D3D12 fence opens on a second D3D12 device and signals
both ways; a D3D12 texture opens on a second D3D12 device. One known limitation to state in the
proposal: D3D11 refuses the typeless resource NRI creates. ~500 lines across 24 files.

## 0010 — Streamer stages block-compressed textures in texel rows

**Title:** Streamer: `StreamTextureData` computes the staging pitch in texels for compressed formats

`StreamerImpl::StreamTextureData` (`Source/Shared/StreamerInterface.hpp`) sizes the staging region
as `w * formatProps.stride` per row and `h` rows, `w` and `h` in texels, `stride` the size of a
block, and copies `h` rows of that from `data`, `dataRowPitch` apart. `CmdUploadBufferToTexture`
reads rows of blocks (Vulkan: `bufferRowLength = rowPitch / stride * blockWidth`; D3D12: the
footprint's `RowPitch`). Measured on v190, 64x64 BC1 with 7 mips, Vulkan and D3D12 on NVIDIA: mip
0 took 32,768 bytes of the dynamic buffer instead of 2,048 (16 times for mips 0-3), and by the code
the copy reads up to byte 8,576 of the 2,048-byte mip. The texels read back intact, because the
upload uses the same oversized pitch. Since 1c70f5b (v172); before it the pitch came from
`dataRowPitch`. Fix: for compressed formats, `(w + blockWidth - 1) / blockWidth` blocks of `stride`
bytes per row and `(h + blockHeight - 1) / blockHeight` rows; the planar formats, whose
`FormatProps` also have 2x2 blocks, are left as they are. Patch attached
(`patches/0010-streamer-block-compressed-rows.patch`), 7 lines.

## 0013 — EXCLUSIVE images are created with queue family indices

**Title:** VK: `queueFamilyIndexCount` is passed for `VK_SHARING_MODE_EXCLUSIVE` images

`DeviceVK::FillCreateInfo` (`Source/VK/DeviceVK.hpp`) sets `queueFamilyIndexCount =
m_NumActiveFamilyIndices` and `pQueueFamilyIndices` whatever `sharingMode` it chose. Vulkan
ignores both for `VK_SHARING_MODE_EXCLUSIVE`, and the Khronos best-practices checks report
`BestPractices-vkCreateImage-sharing-mode-exclusive` ("pCreateInfo->sharingMode is
VK_SHARING_MODE_EXCLUSIVE while specifying multiple queues (queueFamilyIndexCount of 3)") for
every such image once more than one family is active. Measured on v190, NVIDIA, layer 1.4.341,
with graphics, compute and copy queues fetched and `TextureDesc::sharingMode = EXCLUSIVE`. Fix:
pass the indices only for `VK_SHARING_MODE_CONCURRENT`. Patch attached
(`patches/0013-vulkan-exclusive-image-no-family-indices.patch`), two lines.

## 0014 — Swap chain extent is checked against the unscaled range with present scaling

**Title:** VK: `SwapChainVK::Create` refuses extents the scaled range allows

`SwapChainVK::Create` queries `VkSurfacePresentScalingCapabilitiesKHR` when
`swapChainMaintenance1` is supported and chains `VkSwapchainPresentScalingCreateInfoKHR` into the
swap chain, but validates `swapChainDesc.width` / `height` against
`surfaceCaps.minImageExtent` / `maxImageExtent`, never reads `minScaledImageExtent` /
`maxScaledImageExtent`, and decides `isScalingSupported` only after the check. With a scaling
create info the spec requires the extent in the scaled range
(VUID-VkSwapchainCreateInfoKHR-pNext-07782), and on a Win32 surface the unscaled minimum and
maximum are both the current extent, so every size other than the window's client size at that
instant is refused with `INVALID_ARGUMENT` ("swapChainDesc.width is out of [64, 64] range"): a size
that lagged a resize, or a window minimised at creation. Measured on v190, NVIDIA: a 100x80 swap
chain on a 64x64 window is refused; with the patch it creates 100x80 images and presents; a
device without the extension is still refused, as required. Fix: decide scaling first and check
the scaled range when scaling is used. Patch attached
(`patches/0014-vulkan-swapchain-scaled-extent-check.patch`), about twelve lines.

## 0015 — Khronos message id name is not forwarded

**Title:** VK: forward `pMessageIdName` from the debug-utils callback

`DeviceVK::MessageCallback` reports `"[%u] %s"` with `messageIdNumber` and `pMessage`. The layers
of the 1.4.3xx SDKs put the check name (`VUID-...`, `BestPractices-...`, `WARNING-...`) only in
`pMessageIdName`; `pMessage` starts with the function name. The forwarded text therefore has a
number and no name to read or search for (measured: `[1734198062] vkCreateDevice(): Attempting to
enable extension VK_EXT_custom_border_color, ...`). Fix: `"[%u] %s: %s"` when `pMessageIdName` is
non-null. Patch attached (`patches/0015-vulkan-message-id-name.patch`), four lines.

## 0016 — Validation refuses a pipeline layout whose `shaderStages` is `ALL`

**Title:** Validation: `PipelineLayoutDesc::shaderStages = ALL` is treated as no stages

`StageBits::ALL` is 0. `PipelineLayoutD3D12` maps a layout `shaderStages` of `ALL` to
`ALL_SHADERS` and `PipelineLayoutVK` does not read it, so the backends accept it.
`DeviceVal::CreatePipelineLayout` computes `range.shaderStages & pipelineLayoutDesc.shaderStages`
and refuses every range with explicit stages ("'descriptorSets[0].ranges[0].shaderStages' is not
compatible with 'shaderStages'"), and `DeviceVal::CreateGraphicsPipeline` checks
`shaderDesc->stage & shaderStages` and refuses every shader ("'shaders[0].stage' is not enabled in
the pipeline layout"). Measured on v190, Vulkan and D3D12. Fix: map `ALL` to `ALL_SHADERS` in both
checks, as the D3D12 backend does. Patch attached
(`patches/0016-validation-pipeline-layout-all-stages.patch`), three lines.

## 0012 — `StreamerDesc::dynamicBufferDesc.size` as the initial size of the dynamic buffer

**Title:** Streamer: honour `dynamicBufferDesc.size` as the initial per-frame size

`StreamerImpl::Create` stores `dynamicBufferDesc` and never reads `size` (the header says `"size"
is ignored`). The dynamic buffer starts empty; `Grow` creates a committed buffer of
`Align(offset, 64 KiB) * queuedFrameNum` at every new per-frame high-water mark and parks the
previous one, which `EndFrame` destroys `queuedFrameNum + 1` frames later on the calling thread
under the streamer's lock. A loader that streams a level through the Streamer pays one committed
allocation per high-water mark, the page faults of each fresh buffer inside the copy, and the
frees inside `EndStreamerFrame` (a consumer measured 222 stream calls at 477 ms, single calls up
to 62 ms, and `EndStreamerFrame` at 189 ms per level load). Proposal: `size` is the initial
per-frame size; `Create` runs `Grow` with it; 0 keeps today's behaviour. Measured with the patch
on v190, Vulkan and D3D12: 100 KiB with `queuedFrameNum = 2` gives one 262,144-byte buffer at
creation that frames streaming 64 KiB keep, and a 192 KiB frame grows it to 393,216 bytes. Patch
attached (`patches/0012-streamer-dynamic-buffer-initial-size.patch`), eight lines and the comment.

## 0017 — `SwapChainBits::LATEST_READY`: FIFO_LATEST_READY with `VSYNC`

**Title:** SwapChain: a way to ask for `FIFO_LATEST_READY` with `VSYNC`

`SwapChainVK::Create` maps `flags` on two preference lists. With `VSYNC` the list is
`FIFO_RELAXED` (with `ALLOW_TEARING`) or `FIFO`, then `FIFO`; without `VSYNC` it is `MAILBOX`
(`IMMEDIATE` with `ALLOW_TEARING`), then `FIFO_LATEST_READY` when
`VK_EXT_present_mode_fifo_latest_ready` is supported, then `FIFO` (`SwapChainVK.hpp:238-242`,
since 50d103b). Where `MAILBOX` is offered (always on NVIDIA) `FIFO_LATEST_READY` is never chosen,
and with `VSYNC` it is not in the list, so a caller that wants a vblank-paced swap chain that
presents the newest ready image cannot ask for it; NRI does not report the chosen present mode
either. The uncapped order stays as it is: `NONE` means uncapped, and `FIFO_LATEST_READY` queues
presents like `FIFO`. Proposal: `SwapChainBits::LATEST_READY`, which with `VSYNC` puts
`FIFO_LATEST_READY` first in the capped list when the device supports it and is ignored
otherwise (D3D11/D3D12 drop it), and `DeviceDesc::features.latestReadySwapChain` reporting the
support. Measured on v190, NVIDIA (the surface offers `FIFO_LATEST_READY` and `MAILBOX`):
`features.latestReadySwapChain = 1`, a `VSYNC | LATEST_READY` swap chain creates and presents
with the Khronos layer clean; the same mode forced on a device without the feature is reported
("presentMode is VK_PRESENT_MODE_FIFO_LATEST_READY_KHR, but feature presentModeFifoLatestReady is
not enabled"). Patch attached (`patches/0017-swapchain-latest-ready-bit.patch`), thirteen lines.

## 0018 — `disableD3D12BreakOnError`: the D3D12 info-queue break as a switch

**Title:** D3D12: a switch to skip `SetBreakOnSeverity` with `enableGraphicsAPIValidation`

With `enableGraphicsAPIValidation`, `DeviceD3D12::Create` calls
`ID3D12InfoQueue::SetBreakOnSeverity` for `CORRUPTION` and `ERROR` unconditionally
(`DeviceD3D12.hpp:591-595`, since d003e22). With the Agility SDK the same block registers
`ID3D12InfoQueue1::RegisterMessageCallback` (`:620`, 503f448), whose `MessageCallback` (`:191`) reports
every message below `WARNING` as `Message::ERROR` through `CallbackInterface`, where
`AbortExecution` already lets the caller decide whether to stop. The info-queue break comes first
and cannot be turned off, so in a process without a debugger the first debug-layer error is a
breakpoint exception out of the call that recorded it, or the end of a headless process, before
the caller sees the message. Proposal: `disableD3D12BreakOnError` among the "enabled by default"
switches, skipping the two calls; the deny list and the callback stay, and the default keeps the
break. Measured on v190 with Agility 1.619, NVIDIA and WARP, NRI validation off: `CmdCopyBuffer`
of 512 bytes into a 256-byte buffer reports "[841] ID3D12CommandList::CopyBufferRegion: The
destination region extends past the end of the destination resource" and throws a breakpoint
exception out of the call; with the switch the call returns, the message arrives through
`CallbackInterface`, and `EndCommandBuffer` fails `Close` with `E_INVALIDARG`. Patch attached
(`patches/0018-d3d12-break-on-error-switch.patch`), seven lines.

## 0019 — `enableD3D12GpuBasedValidation`: GPU-based validation as a switch

**Title:** D3D12: a switch for `ID3D12Debug1::SetEnableGPUBasedValidation`

With `enableGraphicsAPIValidation`, `DeviceD3D12::Create` enables the debug layer through
`ID3D12Debug::EnableDebugLayer` before the device is created (`DeviceD3D12.hpp:493-497`) but never
calls `ID3D12Debug1::SetEnableGPUBasedValidation`, which has to be called at the same point and is
process-global like the debug layer. The CPU-side debug layer does not know which descriptors a
shader accesses, so an access through an uninitialized descriptor in a volatile range is invisible
without it, and the only way to get it is a D3D12 call of the caller's own before NRI creates the
device. Proposal: `enableD3D12GpuBasedValidation` among the "disabled by default" switches beside
`enableD3D12RayTracingValidation`; with `enableGraphicsAPIValidation`, the debug controller is
queried for `ID3D12Debug1` after `EnableDebugLayer` and GPU-based validation is enabled. Measured on
v190 with Agility 1.619, NVIDIA and WARP: a dispatch writing through a `PARTIALLY_BOUND` range of
two UAVs with only slot 0 written reports "[938] GPU-BASED VALIDATION: Dispatch, Uninitialized
descriptor accessed: ... Descriptor Heap Index FromTableStart: [1]" with the switch and nothing
without it. Patch attached (`patches/0019-d3d12-gpu-based-validation-switch.patch`), nine lines.

## 0020 — D3D12 texture barrier: a side with access `NONE` and a defined layout keeps the stages' sync

**Title:** D3D12: texture barrier with `AccessBits::NONE` and a defined layout fails `Close`

`CommandBufferD3D12::Barrier` converts each texture barrier side with
`GetBarrierSyncFlags(stages, access)`, which derives `D3D12_BARRIER_SYNC` from the stages alone,
and `GetBarrierAccessFlags`, which maps `AccessBits::NONE` to `D3D12_BARRIER_ACCESS_NO_ACCESS`
(`CommandBufferD3D12.hpp:2444-2449`, since enhanced barriers came in, 1d52b40). The debug layer
refuses a texture barrier whose side has `NO_ACCESS`, a layout other than `UNDEFINED` and a sync
other than `SYNC_NONE` (1331, `INCOMPATIBLE_BARRIER_VALUES`); the enhanced-barriers spec lists
`SYNC_NONE` / `NO_ACCESS` as the form for a side that only transitions the layout. On Vulkan the
same NRI barrier is an execution dependency on the stages with no access mask, which is legal.
Measured on v190 with Agility 1.619, NVIDIA, a 4x4 RGBA8 texture: `{NONE, COPY_DESTINATION, COPY}`
-> `{SHADER_RESOURCE, SHADER_RESOURCE, ALL_SHADERS}` reports "[1331] ID3D12CommandList::Barrier:
Invalid LayoutBefore value D3D12_BARRIER_LAYOUT_COPY_DEST. LayoutBefore must be LAYOUT_UNDEFINED or
SyncBefore must be SYNC_NONE when AccessBefore is ACCESS_NO_ACCESS in texture barrier at group [0],
index [0]." and `Close` fails with `E_INVALIDARG`; the after side gives the same for LayoutAfter,
and `GENERAL` / `SHADER_RESOURCE` on both sides likewise; with stages `NONE` on that side it is
clean. Fix: for a texture side with access `NONE` and a layout other than `UNDEFINED`, use
`SYNC_NONE`; buffer and global barriers have no layout and are unchanged. With it all six shapes
record clean. Patch attached (`patches/0020-d3d12-texture-barrier-no-access-sync-none.patch`), four
lines and a comment.

## 0021 — Vulkan `StageBits::COPY` does not cover `CmdZeroBuffer`

**Title:** VK: `StageBits::COPY` misses `vkCmdFillBuffer` (`CmdZeroBuffer`)

`CmdZeroBuffer` is `CopyBufferRegion` from a zero buffer on D3D12 (`CommandBufferD3D12.hpp:2160`,
sync `COPY`) and `vkCmdFillBuffer` on Vulkan (`CommandBufferVK.hpp:2503`), which the spec counts
among the clear commands: `VK_PIPELINE_STAGE_2_COPY_BIT` "specifies the execution of all copy
commands", `VK_PIPELINE_STAGE_2_CLEAR_BIT` "the execution of clear commands, with the exception of
vkCmdClearAttachments". `GetPipelineStageFlags` maps `StageBits::COPY` to `COPY_BIT` only
(`ConversionVK.h:427-428`, since 1a1b6df split `TRANSFER_BIT`, which covered both), and
`NRIDescs.h:473` gives no stage for `CmdZeroBuffer`; `CLEAR_STORAGE` covers it on Vulkan but is
`CLEAR_UNORDERED_ACCESS_VIEW` on D3D12, so no stage orders it on both backends. Measured on v190,
NVIDIA, Khronos synchronization validation on: copy, barrier `{COPY_DESTINATION, COPY}` ->
`{COPY_DESTINATION, COPY}`, `CmdZeroBuffer`, barrier `{COPY_DESTINATION, COPY}` ->
`{COPY_SOURCE, COPY}`, copy reports "SYNC-HAZARD-WRITE-AFTER-WRITE: vkCmdFillBuffer(): ... The
current synchronization allows VK_ACCESS_2_TRANSFER_WRITE_BIT accesses at
VK_PIPELINE_STAGE_2_COPY_BIT, but to prevent this hazard, it must allow these accesses at
VK_PIPELINE_STAGE_2_CLEAR_BIT." and "SYNC-HAZARD-READ-AFTER-WRITE: vkCmdCopyBuffer2(): ... a read
(VK_ACCESS_2_TRANSFER_READ_BIT) at VK_PIPELINE_STAGE_2_COPY_BIT does not conflict with a prior write
(VK_ACCESS_2_TRANSFER_WRITE_BIT) at VK_PIPELINE_STAGE_2_CLEAR_BIT". Fix: `COPY` maps to
`COPY_BIT | CLEAR_BIT`, and the `COPY` comment names `CmdZeroBuffer`; with it the same sequence is
clean. Patch attached (`patches/0021-vulkan-copy-stage-covers-fill-buffer.patch`), two lines and
the comment.

## 0022 — D3D12 copy layouts refused in copy command lists

**Title:** D3D12: `Layout::COPY_DESTINATION` barrier fails `Close` on a COPY queue

`g_BarrierLayouts` maps `COPY_SOURCE` / `COPY_DESTINATION` to `D3D12_BARRIER_LAYOUT_COPY_SOURCE` /
`COPY_DEST` whatever the command list type, and the enhanced-barrier texture conversion
(`CommandBufferD3D12.hpp:2448-2449`, since enhanced barriers came in, 1d52b40) never reads
`m_CommandListType`. The enhanced-barriers spec allows only `D3D12_BARRIER_LAYOUT_COMMON` on
`D3D12_COMMAND_LIST_TYPE_COPY` ("Copy queues do not support layout transition Barriers"). Vulkan's
copies hard-code `TRANSFER_SRC/DST_OPTIMAL` (`CommandBufferVK.hpp` `UploadBufferToTexture` and
the other copies), so `GENERAL` is no way out there: no layout is legal on both backends for a
texture a COPY queue writes. Measured on v190 with Agility 1.619, NVIDIA, debug layer on, a 16x16
RGBA8 texture, COPY queue `{NONE, UNDEFINED}` -> `{COPY_DESTINATION, COPY_DESTINATION}` and
`CmdUploadBufferToTexture`: "[1334] ID3D12CommandList::Barrier: LayoutAfter
D3D12_BARRIER_LAYOUT_COPY_DEST is incompatible with command list type D3D12_COMMAND_LIST_TYPE_COPY
in texture barrier at group [0], index [0]." and `Close` fails with `E_INVALIDARG`. Mapping to
`COMMON` in copy lists alone is not enough: the debug layer tracks the layout across queues, and
the direct queue's next barrier from `COPY_DESTINATION` draws "[1334]
ID3D12CommandQueue1::ExecuteCommandLists: Barrier layout(D3D12_BARRIER_LAYOUT_COPY_DEST), ... does
not match expected layout (D3D12_BARRIER_LAYOUT_COMMON)". Fix: `COPY_SOURCE` / `COPY_DESTINATION`
are `COMMON` in a copy list, and on the before side of a barrier whose `srcQueue` is a copy queue
and the after side of one whose `dstQueue` is (the hand-off `TextureBarrierDesc` already names; on
Vulkan the same barriers are the queue family release and acquire). With it the upload on COPY and
the acquire on DIRECT are clean and the readback matches. Patch attached
(`patches/0022-d3d12-copy-list-layouts-common.patch`), thirteen lines; the upstream form drops
the `EXTERNAL_QUEUE` term, which is patch 0002's.

## 0023 — Vulkan writes queue family indices into barriers on `CONCURRENT` images

**Title:** VK: `srcQueue` / `dstQueue` make a queue family ownership transfer on `CONCURRENT` images

`CommandBufferVK::Barrier` (`CommandBufferVK.hpp:2572-2573`) writes the family indices of
`srcQueue` / `dstQueue` into `VkImageMemoryBarrier2` whatever sharing mode `FillCreateInfo` chose
for the image. A queue family ownership transfer is defined only for `VK_SHARING_MODE_EXCLUSIVE`
(or, on `CONCURRENT`, with the external / foreign family on one side and `IGNORED` on the other),
and `NRIDescs.h` says as much ("Queue ownership transfer is potentially needed only for
`SharingMode::EXCLUSIVE` textures"). The Khronos layer records any barrier with two different
indices as a transfer, applies the acquire's layouts and not the release's, and so a CONCURRENT
texture handed from a copy queue (release `COPY_DESTINATION` -> `COPY_SOURCE`, acquire
`COPY_SOURCE` -> `COPY_SOURCE`) draws at submit, measured on v190 with layer 1.4.341, NVIDIA,
graphics + compute + copy queues: "VUID-vkCmdDraw-None-09600: vkQueueSubmit2(): ... expects VkImage
... to be in layout VK_IMAGE_LAYOUT_TRANSFER_SRC_OPTIMAL--instead, current layout is
VK_IMAGE_LAYOUT_TRANSFER_DST_OPTIMAL"; with best practices on, a sampled-only CONCURRENT texture's
release and acquire each draw `BestPractices-PipelineBarrier-unneeded-QFOT`. Fix: `TextureVK`
remembers the number of active families at creation and, for a CONCURRENT image, every side that
is not the external family becomes `VK_QUEUE_FAMILY_IGNORED`; EXCLUSIVE and wrapped images keep
the indices. With it both are clean, and the EXCLUSIVE case still reports the mismatch. Patch
attached (`patches/0023-vulkan-concurrent-image-no-queue-family-transfer.patch`), twenty lines; the
upstream form drops the `EXTERNAL_QUEUE` term, which is patch 0002's (keep
`VK_QUEUE_FAMILY_EXTERNAL` sides if upstream ever has one).

## 0024 — Adapter enumeration filtered by graphics API (proposal, phase 2)

**Title:** `nriEnumerateAdapters2`: enumerate only the asked graphics APIs' adapters

`nriEnumerateAdapters` runs every enabled backend's pass unconditionally (`Creation.cpp`
`UpdateAdaptersVK`, then `UpdateAdaptersD3D`, then WGPU / Metal), and `nriCreateDevice` with a null
`adapterDesc` calls it. The D3D pass creates a DXGI factory, a null-device `D3D12CreateDevice`
probe per adapter and real `ID3D12Device`s for adapters the VK pass did not list, so creating a
Vulkan device leaves `D3D12Core.dll` loaded in the process. Measured on v190, NVIDIA RTX 4070
SUPER, Windows: VK-only enumeration 5.3 ms warm (67.0 ms first), full enumeration 69.1 ms warm
(125.1 ms first). Proposal: `nriEnumerateAdapters2(GraphicsAPI graphicsAPIs, AdapterDesc*,
uint32_t&)`, the same body with each pass guarded by its bit; `nriEnumerateAdapters` passes every
bit; `nriCreateDevice` with a null desc enumerates VK only for a VK device (an adapter listed by
the VK pass gets only D3D bits from the D3D pass, so its desc and the sort order are unchanged).
D3D devices keep the full enumeration, because for an adapter both passes list the desc is the VK
pass's (queue counts 16, 8, 2 on NVIDIA where the D3D pass says 4, 4, 4) and a D3D-only
enumeration would change it. Patch attached
(`patches/0024-enumerate-adapters-by-graphics-api.patch`), twenty-one lines.

---

## Found by the coverage tests (no patch)

Found on 2026-10-21 by the tests that close the coverage report's blind spots (branch
`coverage-tests`, on master). Measured on v190 with master's patches, none of which touch these
lines, on the development machine's discrete GPU, and on WARP where it says so. Line numbers and
code are NRI main's at 2f7a49f, where every line N1 to N10 cite is unchanged. N1 to N8 and N10 are written
in the filed form; N9 is a phase 2 question.

### N1: Vulkan: vertex attributes bind to streamIndex, not the stream's bindingSlot

**Observed.** Two vertex streams declared out of slot order, each attribute naming its stream by
index: D3D12 renders the quad exactly; Vulkan draws nothing, and the Khronos layer reports
`[2656940963] VUID-vkCmdBindVertexBuffers2-pStrides-06209: vkCmdDrawIndexed(): (attribute binding 1, attribute location 0) The pStrides value (4) parameter in the last call to vkCmdDrawIndexed is not 0 and is less than the extent of the binding for the attribute (8).`
Streams declared in slot order hide it, because index and slot then agree.

**Cause.** `Source/VK/PipelineVK.hpp:167` sets `vertexAttributeDesc.binding = attribute.streamIndex`,
while `:177` gives the binding `stream.bindingSlot`. D3D12 goes through the stream
(`PipelineD3D12.hpp:281`, `InputSlot = stream.bindingSlot`). Since 42ddcff.

**Fix.**

```cpp
vertexAttributeDesc.binding = vi->streams[attribute.streamIndex].bindingSlot;
```

### N2: Vulkan: an empty vertex input makes the binding stride dynamic

**Observed.** A graphics pipeline whose `vertexInput` has no attributes and no streams, drawn with
`CmdDraw` and nothing bound: `[2005986124] VUID-vkCmdDraw-pStrides-04913: vkCmdDraw(): VK_DYNAMIC_STATE_VERTEX_INPUT_BINDING_STRIDE state is dynamic, but the command buffer never called vkCmdBindVertexBuffers2.`
The image equals the one with `vertexInput = nullptr`; D3D12 is clean.

**Cause.** `Source/VK/PipelineVK.hpp:378-379` add the dynamic stride for any non-null `vertexInput`;
with no streams there is nothing to bind. Since 254424c.

**Fix.**

```cpp
if (vi && vi->streamNum && m_Device.GetDesc().features.extendedDynamicState)
```

### N3: D3D12: CmdClearAttachments crashes in a stencil-only pass

**Observed.** `CmdBeginRendering` with a `D32_SFLOAT_S8_UINT` view as `stencil` and no `depth`, then
`CmdClearAttachments` with `planes = STENCIL`: an access violation inside NRI (0xC0000005). Vulkan is
clean, and so is D3D12 when the view is passed as `depth` too.

**Cause.** `Source/D3D12/CommandBufferD3D12.hpp:1852` takes the view from `m_Depth.attachment`,
which `CmdBeginRendering` sets only from `RenderingDesc::depth` (`:1889`); a stencil-only pass has
`m_Stencil.attachment` (`:1901`). Since 110a667.

**Fix.**

```cpp
const DescriptorD3D12* depthStencil = m_Depth.attachment ? m_Depth.attachment : m_Stencil.attachment;
D3D12_CPU_DESCRIPTOR_HANDLE handle = {depthStencil->GetDescriptorHandleCPU()};
```

### N4: D3D12: the stencil of a depth-stencil view passed as depth is not cleared

**Observed.** `RenderingDesc::depth` set to a `D32_SFLOAT_S8_UINT` view with `loadOp = CLEAR` and a
stencil clear value of 5, `stencil` left empty, over a stencil of 9: Vulkan reads back 5, D3D12 9
(hardware and WARP). `NRIDescs.h:1719` says `depth` "may be treated as depth-stencil", and Vulkan
does: it points `pStencilAttachment` at the depth attachment when the format has stencil
(`CommandBufferVK.hpp:1634-1636`). D3D12 does too for resolves (`m_Stencil = m_Depth`, `:1896-1897`).

**Cause.** `Source/D3D12/CommandBufferD3D12.hpp:1919-1926` take the stencil clear flag and value
from `renderingDesc.stencil` only. Since 110a667.

**Fix.**

```cpp
const AttachmentDesc& stencil = renderingDesc.stencil.descriptor ? renderingDesc.stencil : renderingDesc.depth;
if (m_Stencil.attachment && stencil.loadOp == LoadOp::CLEAR)
    clearFlags |= D3D12_CLEAR_FLAG_STENCIL;
// ... and stencil.clearValue.depthStencil.stencil in ClearDepthStencilView
```

### N5: D3D12: 2D texture views ignore layerOffset

**Observed.** A two-layer `R32_UINT` `TEXTURE_2D`, a `STORAGE_TEXTURE` view with `layerOffset = 1`,
`CmdClearStorage`: Vulkan clears layer 1, D3D12 clears layer 0 (hardware and WARP). Neither NRI
validation nor either backend's layer says anything.

**Cause.** `Source/D3D12/DescriptorD3D12.hpp:214-218` create a `D3D12_UAV_DIMENSION_TEXTURE2D`
view, which has no array slice, so `layerOffset` is dropped. `:164-167` do the same for `TEXTURE`
(an SRV; not measured). Vulkan honours it (`baseArrayLayer`, `DescriptorVK.hpp:286`). Since the
initial commit.

**Fix.** D3D12 has no non-array 2D view of layer N, and an array view would not match a `Texture2D`
declaration. So refuse `layerOffset != 0` for `TEXTURE` / `STORAGE_TEXTURE` views of a 2D texture
in validation, pointing to the `_ARRAY` types, or document it in `TextureViewDesc`.

### N6: D3D12: micromap barriers with MICROMAP_WRITE fail Close

**Observed.** After `CmdBuildMicromaps`, a buffer barrier whose before side is
`{MICROMAP_WRITE, MICROMAP}` (before the BLAS build that links the micromap) gives
`[1331] ID3D12CommandList::Barrier: SyncBefore bits D3D12_BARRIER_SYNC_EMIT_RAYTRACING_ACCELERATION_STRUCTURE_POSTBUILD_INFO are incompatible with AccessBefore bits D3D12_BARRIER_ACCESS_RAYTRACING_ACCELERATION_STRUCTURE_WRITE in buffer barrier at group [0], index [0].`
`Close` then fails with `E_INVALIDARG`, with or without the debug layer. The build alone closes; with
`ALL` stages on that side, the micromap links and traces.

**Cause.** `Source/D3D12/CommandBufferD3D12.hpp:179-180` add
`EMIT_RAYTRACING_ACCELERATION_STRUCTURE_POSTBUILD_INFO` to the `ACCELERATION_STRUCTURE | MICROMAP`
stages unless the access holds `ACCELERATION_STRUCTURE_WRITE`. Meanwhile `:234-235` map
`MICROMAP_WRITE` to the same D3D12 write access. Since 022f7e6.

**Fix.**

```cpp
if (!(accessBits & (AccessBits::ACCELERATION_STRUCTURE_WRITE | AccessBits::MICROMAP_WRITE)))
```

### N7: D3D12: acceleration-structure memory is reported 128-byte aligned; placements need 256

**Observed.** On a device with tight alignment (Agility 1.619), `GetAccelerationStructureMemoryDesc2`
reports an alignment of 128. A TLAS placed at offset 2688, which is a multiple of 128 but not of 256,
passes NRI validation's placement check. Its descriptor is then refused:
`[31] ID3D12Device::TryCreateShaderResourceView: For D3D12_SRV_DIMENSION_RAYTRACING_ACCELERATION_STRUCTURE, the address (149441152) must be aligned to 256 bytes.`
Vulkan reports 256.

**Cause.** `Source/D3D12/ImplD3D12.cpp:1017-1023` size the structure as a plain buffer, and
`DeviceD3D12.hpp:1478-1479` give every buffer `D3D12_RESOURCE_FLAG_USE_TIGHT_ALIGNMENT`. The result
drops below `D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BYTE_ALIGNMENT`, which
`memoryAlignment.accelerationStructureOffset` already reports (`:997`). Since 3371cdb.

**Fix.** In `GetAccelerationStructureMemoryDesc2` and `AccelerationStructureD3D12::GetMemoryDesc`:

```cpp
memoryDesc.alignment = std::max(memoryDesc.alignment, (uint32_t)D3D12_RAYTRACING_ACCELERATION_STRUCTURE_BYTE_ALIGNMENT);
```

### N8: D3D12: AV1 encode with the default picture flags fails Close

**Observed.** An AV1 key frame encoded with NRI's default picture flags, on an encoder whose required
feature flags include automatic segmentation, gives
`[1306] CVideoEncodeCommandList::EncodeFrame: D3D12_VIDEO_ENCODER_AV1_PICTURE_CONTROL_FLAGS - D3D12_VIDEO_ENCODER_AV1_PICTURE_CONTROL_FLAG_ENABLE_FRAME_SEGMENTATION_AUTO is required in RequiredFeatureFlags but not set.`
`Close` then fails with `E_INVALIDARG`.

**Cause.** `Source/D3D12/CommandBufferD3D12.hpp:1536-1537` set `ENABLE_FRAME_SEGMENTATION_AUTO` only
when the picture flags hold `SEGMENTATION_ENABLED`, which `GetDefaultPictureFlags`
(`Source/Shared/SharedVideo.h:571-573`) leaves out. The session merges the required and the supported
feature flags (`VideoSessionD3D12.hpp:411-412`), so by the time the picture is built the encoder's
required set is gone. Since 3b9fe2d.

**Fix.** Keep `RequiredFeatureFlags` in the session, and set the picture flag when
`AUTO_SEGMENTATION` is required, as well as when the picture asks for segmentation.

### N9: D3D12: GetVideoAV1EncodeDecodeInfo fails silently on a payload header and infers the frame type (phase 2)

**Observed.**
- With `encodedPayloadHeader` set to the first bytes of a D3D12 AV1 payload, the call answers
  `FAILURE` with no message. The D3D12 encoder writes bare tile data, with no OBUs.
- With a DPB snapshot passed for a key frame, the info describes the frame as inter.

**Cause.** `Source/D3D12/VideoSessionD3D12.hpp:128-129` parse a given header as OBUs. `:193` decides
inter from `desc.referenceNum` (since 39ce136), and `VideoAV1EncodeDecodeInfoDesc` carries no frame
type.

**Fix.** His choice. Either ignore the header on D3D12 or report why it fails. Then either say in
`NRIVideo.h` that `references` stays empty for key frames, or add the frame type to the desc.

### N10: an allocator that returns null crashes NRI instead of failing the creation

**Observed.** An `AllocationCallbacks::Allocate` that returns null for the allocation of a fence:
`CreateFence` dies with an access violation (0xC0000005), on Vulkan and D3D12, with NRI validation on
and off.

**Cause.** `Allocate` returns null safely (`Source/Shared/SharedExternal.h:457-463`), but
`CreateImplementation` calls `Create` on the result without a check (`Source/VK/DeviceVK.h:204-205`,
`Source/D3D12/DeviceD3D12.h:87-88`). Device creation does the same (`ImplVK.cpp:64-65`,
`ImplD3D12.cpp:63-64`). Since 2104586.

**Fix.**

```cpp
Implementation* impl = Allocate<Implementation>(GetAllocationCallbacks(), *this);
if (!impl)
    return Result::OUT_OF_MEMORY;
```

### Not filed

- **D3D12 attachment resolve into a non-zero layer or mip.** In v190 `EndRendering` passed
  `DepthOrArraySize` where `GetSubresourceIndex` takes the mip count (`CommandBufferD3D12.hpp:1959`
  at v190, since 110a667). Measured there:
  - Layer 1 of a 1-mip, 2-layer destination gives
    `[877] ID3D12CommandList::ResolveSubresourceRegion: Source or destination subresource index is out of bounds.`
    and then a failed `Close`.
  - Mip 1, layer 1 of a 2-mip, 3-layer destination lands in subresource 4, with
    `[1334] ... subresource = [4], does not support the required access (D3D12_BARRIER_ACCESS_RESOLVE_DEST)`.
  - Vulkan is correct in both.

  Main's 48d4a33 passes `MipLevels` (`:2010`, `:2052`), so it is fixed. Master keeps the bug until
  the next tag, and the test written for it belongs on `next`.
- **Vulkan pipeline-statistics pool.** `QueryPoolVK.hpp:38` adds the task and mesh counters when
  `features.meshShader` is set, and the query size follows (`:47`, `:56`). NRI's own
  `features.meshShaderPipelineStats` (`DeviceVK.hpp:1633`, from `meshShaderQueries`) is the bit that
  allows them (VUID-VkQueryPoolCreateInfo-meshShaderQueries-07068). Since f53490f. Held: found by
  reading, and every device here reports both bits, so nothing measures it.
- **Draw-parameters emulation on Vulkan** (phase 2, a question).
  - Validation (`CommandBufferVal.hpp`) demands `sizeof(DrawBaseDesc)` strides for indirect draws
    with the emulation flag on every backend.
  - The Vulkan backend reads plain records at `offset`. Measured with the flag on Vulkan, a stride
    of 16 is refused ("'stride' is too small, expected >= 24"), `DrawBaseDesc` records at stride 24
    draw nothing, and at `offset + 8` they draw.
  - The portable rule today: set the flag on D3D12 only.
- **`WriteShaderGroupIdentifiers` group range.** Held, because it was found by reading and not
  measured. `PipelineVal::WriteShaderGroupIdentifiers` checks only the stride (`PipelineVal.hpp:25`),
  and the D3D12 backend reads `m_ShaderGroupNames[shaderGroupIndex]` without a bound
  (`PipelineD3D12.hpp:691`), so a base index plus group count past the pipeline's groups is never
  refused.
- **Driver behaviour, not NRI.**
  - On the development machine's D3D12 driver, an H.265 decode capability query at 64x64 removes the
    device (`[233] ... DXGI_ERROR_DRIVER_INTERNAL_ERROR`), inside the decoder-heap creation of NRI's
    `CanCreateVideoDecodeSession` (`VideoSessionD3D12.hpp:3-23`).
  - Its Vulkan driver refuses `MICROMAP_COMPACTED_SIZE` and `SERIALIZATION_SIZE` query pools with
    `VK_ERROR_UNKNOWN` and the layer silent; a raw `vkCreateQueryPool` does the same.
- **Knowledge, not reports.**
  - A `STORAGE_TEXTURE` view with `mipNum` over two mips is cleared whole by Vulkan
    (`CommandBufferVK.hpp`, `levelCount = mipNum`) and mip 0 only by D3D12 (a UAV addresses one
    mip): use one mip per storage view.
  - Dynamic depth bias applies only to a pipeline with a non-zero static bias
    (`PipelineVK.hpp:227`, `:380-381`; `PipelineD3D12.hpp:400`), which `NRI.h` does not say.
