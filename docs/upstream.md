# Upstream reports

Each patch in `patches/` is either a bug upstream should fix or an extension upstream might want.
This file holds the report text for each and records what was filed, where, and how upstream
resolved it. The sections below the table are our drafts and the measurements behind them; the
linked issues and pull requests hold the text as posted.

**This is the live copy, on the `next` branch.** At a tag both branches carry the same copy:
since v191 (2026-11-03) master's copy is this one as it stood at the adoption. Line numbers and
measurements here are against the NRI commit `next` pins, and against v190 where they say so.

2026-11-03: NRI v191 (46c93d79, 2026-10-30) carries every filed report: #295-#301 and #302-#306, and
PR #294 (our 0002); #302 and #306 were fixed in another shape. Master and `next` both moved to v191
with eleven patches: 0001, 0007, 0012, 0013, 0015, 0017, 0018, 0019, 0024 (two lines), 0037, 0038.
Not filed: 0007 (build option), 0013 and 0037 (layer warnings), 0015 (message format). The
proposals (0038, 0018, 0019, 0017, 0012, 0024, 0001) go as pull requests, one feature each.

2026-11-05: `next` moved to main 7ea93bb5 (two commits past v191: 3dd4cbfa adopts the Agility 1.620
interfaces and `CheckFeatureSupport1`, fixing #307; 7ea93bb5 requires 4-byte `CmdZeroBuffer`
ranges). All twelve patches apply unchanged; no L0 drift (only a header comment changed). 3dd4cbfa
broke every resource creation on the inbox runtime (N13), patched as 0040. Measured: Windows 538
tests, 0 failed, 13 skipped (NVIDIA RTX 4070 SUPER); the `[989]` warnings fell from 33 to D3D12MA's 6.
The same day the two held read-only findings were measured and patched: N14 (0041, a crash) and
N15 (0042, with the Khronos Profiles layer standing in for a device without mesh-shader queries).
With 0040-0042: Windows 541 tests, 0 failed, 13 skipped.
Filed the same day: the four bugs as issues #309-#312 (fixes inline, N12 as a fork commit); the
seven proposals as four pull requests from the owner's fork, one commit per item: #313 (0018,
0019, 0024, 0038), #314 (0001), #315 (0012), #316 (0017).

2026-10-19: all six filed issues were fixed on main within the day. Main is ten commits past v190
at 8ee40ca, still `NRI_VERSION 190`, untagged.
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
`StageBits` / `AccessBits`. `next` pinned 2f7a49f with thirteen patches, 0002 re-cut. Measured:
Windows 297 tests, 0 failed (NVIDIA; the COMMON-access shapes draw no message at all, not even a
warning); Linux lavapipe 297, 0 failed.

**2026-10-23.** Main moved 46 commits to 72f5f46 ("Quality of life improvements and API
simplifications": `DescriptorSetBits` removed, texture views inherit the texture format, Streamer
pitches optional). In the range: #286 (962615b) gives `nriEnumerateAdapters` the backends to
probe, master 0024's idea in his shape (the two `nriCreateDevice` lines stay as `next`'s 0024);
#292 (0fade6e, 16f56f7) rolls back a failed committed or placed creation (0034 re-cut over it);
#264 and #265 (merged 2026-10-22) fix the AV1 encode defects behind N8 and N9: af45e72 enables
automatic segmentation from the required feature flags (0032 dropped), 57d92ff and af45e72 add
`pictureDesc`, `av1PictureDesc` and `savedFrameStates` to `VideoAV1EncodeDecodeInfoDesc`, the
frame type from the picture desc and a key frame without references (0033 dropped, the binding
follows), 9131c1b answers `UNSUPPORTED` for a payload header on D3D12, and 9011264 works around
the NVIDIA encode feedback query hang the suite skips H.264 encode for on Vulkan. 0029 re-cut
over the renamed `textureViewDescFixed`. `next` pins 72f5f46 with twenty-two patches.

**2026-10-27.** The maintainer merged PR #294 (0002) as eb4ed356, after two commits of his own on
the branch: eecb4cfd (the shared-handle cleanup as a helper) and 93a542a8 (the shared allocation
path queried the device address of a video-only buffer, which the ordinary path skips). He polished
it on main the same day: f18c6df9 adds `CoreInterface::DestroySharedHandle`, which closes the
caller-owned handle by its type (`CloseHandle` or `close`) and clears the struct; d0f5ce49 queries
export support before creating a shared object (`vkGetPhysicalDeviceExternalBufferProperties`,
`vkGetPhysicalDeviceImageFormatProperties2` with the external info and the image's limits,
`vkGetPhysicalDeviceExternalSemaphoreProperties`) and answers `UNSUPPORTED` where Vulkan cannot
export, reports `features.sharedFences` only when a timeline or binary semaphore exports, folds the
queue-family choice of both barrier kinds into one helper, moves `EXTERNAL_QUEUE` beside the other
constants and rewrites the comments of `GetQueue`, `SharingMode`, the barrier descs and
`SharedHandle` ("After an FD import takes ownership, clear `SharedHandle` instead of destroying
it"; FD 0 is valid). `next` pins b5f5c62 with sixteen patches, 0002 dropped; the binding disposes
`NriSharedHandle` through `DestroySharedHandle` and adds `Detach` for imports that take ownership.
Measured: Windows 535 tests, 0 failed, 13 skipped (NVIDIA RTX 4070 SUPER, VK and D3D12); lavapipe
534, 0 failed, 294 skipped.

On #49 the maintainer asked for a PR that extends the existing buffer, texture and fence descs
rather than a separate extension. 0002 was re-cut in that shape: `shared` on `BufferDesc` and
`TextureDesc` (committed resources), a new `FenceDesc` (`initialValue` plus `shared`) that
`CreateFence` takes, and the handle getters beside Core's `Get*NativeObject`, which any shape needs
because Core exposes no native fence or memory object to export. The `EXTERNAL_QUEUE` sentinel
follows `HAS_BUFFER`. Placed resources can follow; some Vulkan drivers export images only from
dedicated allocations, so they need a capability check first.

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
| 0022 D3D12 copy layouts refused in copy command lists | bug | filed 2026-10-19 against main 8ee40ca, [#283](https://github.com/NVIDIA-RTX/NRI/issues/283); fixed 2026-10-20 in 732f737, ours without the sentinel guard, which 0002 carried and #294 merged; dropped on `next` |
| 0023 Vulkan queue family indices on `CONCURRENT` images make every hand-off an ownership transfer | bug | filed 2026-10-19 against main 8ee40ca, [#284](https://github.com/NVIDIA-RTX/NRI/issues/284); fixed 2026-10-20 in 2603edc, the sharing mode recorded at creation; dropped on `next`, 0002 kept the external family (merged as #294) |
| 0002 shared buffers, textures and fences | proposal, PR | merged 2026-10-27 as eb4ed356, polished by the maintainer in f18c6df9 (`DestroySharedHandle`) and d0f5ce49 (export support queries, `UNSUPPORTED`, comments); dropped on `next` at b5f5c62. Offered 2026-10-16 as a [comment on the maintainer's #49](https://github.com/NVIDIA-RTX/NRI/issues/49#issuecomment-6212054525); he answered 2026-10-21: "A PR would be very welcome", preferring fields on the existing buffer, texture and fence descs; re-cut in that shape on `next` 2026-10-23 (section below); filed 2026-10-24 as [PR #294](https://github.com/NVIDIA-RTX/NRI/pull/294) from the owner's fork (`Nullpoint56:shared-handles`, main 72f5f46 + 0002), the `FenceDesc` question asked in a [comment on #49](https://github.com/NVIDIA-RTX/NRI/issues/49) that links it; reviewed 2026-10-26, four items fixed on the branch |
| 0006 `samplerLodBiasMax` above what a portability device allows | bug | filed 2026-10-27 against main b5f5c626 as [#303](https://github.com/NVIDIA-RTX/NRI/issues/303), the fix of patch 0006 attached; fixed in v191 by 0682d020 (2026-10-28, our lines); patch dropped on both branches 2026-11-03 |
| 0016 Validation refuses a pipeline layout with `shaderStages = ALL` | bug | filed 2026-10-27 against main b5f5c626 as [#302](https://github.com/NVIDIA-RTX/NRI/issues/302), the fix of patch 0016 attached; fixed in v191 by 9764221e (2026-10-28, another shape: `GetEffectiveShaderStages` resolves `ALL` and `ALL_SHADERS` to the layout's stages in every backend and in validation); patch dropped on both branches 2026-11-03 |
| 0017 `SwapChainBits::LATEST_READY`: FIFO_LATEST_READY with `VSYNC` | proposal | drafted 2026-10-19; filed 2026-11-05 as [PR #316](https://github.com/NVIDIA-RTX/NRI/pull/316); the uncapped order stays as it is |
| 0018 `disableD3D12BreakOnError`: D3D12 info-queue break as a switch | proposal | drafted 2026-10-19; filed 2026-11-05 in [PR #313](https://github.com/NVIDIA-RTX/NRI/pull/313) (device creation group, with 0019, 0024, 0038); the default (break) stays |
| 0019 `enableD3D12GpuBasedValidation`: GPU-based validation as a switch | proposal | drafted 2026-10-19; filed 2026-11-05 in [PR #313](https://github.com/NVIDIA-RTX/NRI/pull/313) |
| 0012 `StreamerDesc::dynamicBufferDesc.size` as the initial ring size | proposal | drafted 2026-10-19; filed 2026-11-05 as [PR #315](https://github.com/NVIDIA-RTX/NRI/pull/315); per-frame or total size is open |
| 0024 `nriEnumerateAdapters2`: adapter enumeration filtered by graphics API | proposal | drafted 2026-10-19; main took the idea in its own shape on 2026-10-22 (962615b, #286: `nriEnumerateAdapters` takes the backends to probe); `next` keeps two lines, `nriCreateDevice` with no adapter probing VK only for a VK device; filed 2026-11-05 in [PR #313](https://github.com/NVIDIA-RTX/NRI/pull/313) |
| 0001 Agility SDK from a path | proposal | drafted 2026-10-19; filed 2026-11-05 as [PR #314](https://github.com/NVIDIA-RTX/NRI/pull/314) (related: #157); the load is process-global, as the report states |
| 0007 Apple loader link should be optional | build option | not filed: a packaging option (PR 134 added the link) |
| 0009 `OUT_OF_DATE` from acquire/present reported as an error | reporting | filed 2026-10-27 against main b5f5c626 as [#304](https://github.com/NVIDIA-RTX/NRI/issues/304), the fix of patch 0009 attached; fixed in v191 by c1261950 (2026-10-28, our lines, a third for `WaitForPresent`, and `ReportMessage` demotes an `OUT_OF_DATE` report to `WARNING`); patch dropped on both branches 2026-11-03 |
| 0013 EXCLUSIVE images created with queue family indices | layer warning | not filed: Vulkan ignores the indices; cosmetic |
| 0015 Khronos message id name not forwarded | reporting | not filed: message format (59d0785) |
| 0037 Vulkan swap chain without `VkSwapchainPresentModesCreateInfoKHR` | layer warning | not filed: one present mode is what Vulkan assumes without the structure; the best-practices warning is cosmetic (drafted 2026-10-24, section below) |
| 0038 `vkInstanceCreateInfoNext`: a caller's chain for `VkInstanceCreateInfo` | proposal | drafted 2026-10-24; filed 2026-11-05 in [PR #313](https://github.com/NVIDIA-RTX/NRI/pull/313) (section below) |
| N1 Vulkan vertex attributes bind to `streamIndex`, not the stream's `bindingSlot` | bug | fixed upstream in 0593d91 (2026-10-26), dropped on next; patched on master as 0025 (2026-10-22, PR #26), carried on next until 83fd010; filed 2026-10-24 against main 72f5f46, [#295](https://github.com/NVIDIA-RTX/NRI/issues/295); reproduced on main 2f7a49f (2026-10-22) |
| N2 Vulkan empty vertex input makes the binding stride dynamic | bug | fixed upstream in 8a8fc2b (2026-10-26), dropped on next; patched on master as 0026 (2026-10-22, PR #26), carried on next until 83fd010; filed 2026-10-24 against main 72f5f46, [#296](https://github.com/NVIDIA-RTX/NRI/issues/296); reproduced on main 2f7a49f (2026-10-22) |
| N3 D3D12 `CmdClearAttachments` crashes in a stencil-only pass | bug (crash) | fixed upstream in 909a139 (2026-10-26), dropped on next; patched on master as 0027 (2026-10-22, PR #26), carried on next until 83fd010; filed 2026-10-24 against main 72f5f46, [#297](https://github.com/NVIDIA-RTX/NRI/issues/297); reproduced on main 2f7a49f (2026-10-22) |
| N4 D3D12 does not clear the stencil of a depth-stencil view passed as `depth` | bug | fixed upstream in c8fe3b2 (2026-10-26, D3D11 too), dropped on next; patched on master as 0028 (2026-10-22, PR #26), carried on next until 83fd010; filed 2026-10-24 against main 72f5f46, [#298](https://github.com/NVIDIA-RTX/NRI/issues/298); reproduced on main 2f7a49f (2026-10-22) |
| N5 D3D12 2D texture views ignore `layerOffset` | bug | filed 2026-10-27 against main b5f5c626 as [#306](https://github.com/NVIDIA-RTX/NRI/issues/306), the fix of patch 0029 attached; fixed in v191 by 71d3c586 (2026-10-28, another shape: D3D11 and D3D12 build the view as a one-layer array view, so it addresses the layer instead of being refused); patch dropped on both branches 2026-11-03 |
| N6 D3D12 micromap barriers with `MICROMAP_WRITE` fail `Close` | bug | fixed upstream in 1dc13b2 (2026-10-26; its message says "fixed #300"), dropped on next; patched on master as 0030 (2026-10-22, PR #26), carried on next until 83fd010; filed 2026-10-24 against main 72f5f46, [#299](https://github.com/NVIDIA-RTX/NRI/issues/299); reproduced on main 2f7a49f (2026-10-22) |
| N7 D3D12 acceleration-structure memory reported 128-byte aligned; placements need 256 | bug | fixed upstream in 561e2be (2026-10-26, another shape: the buffer desc asks for 256 under tight alignment), dropped on next; patched on master as 0031 (2026-10-22, PR #26), carried on next until 83fd010; filed 2026-10-24 against main 72f5f46, [#300](https://github.com/NVIDIA-RTX/NRI/issues/300); reproduced on main 2f7a49f (2026-10-22) |
| N8 D3D12 AV1 encode with the default picture flags fails `Close` | bug | patched on master as 0032 (2026-10-22, PR #26); fixed on main by af45e72 (merged 2026-10-22, #264), the same rule, so dropped on next (2026-10-23); nothing to file; reproduced on main 2f7a49f (2026-10-22) |
| N9 D3D12 `GetVideoAV1EncodeDecodeInfo`: silent `FAILURE` on a payload header, frame type inferred | bug, fix shape open | patched on master as 0033 (2026-10-22, PR #26); main chose another shape (57d92ff, af45e72, 9131c1b: the submitted picture descs in the desc, a key frame without references, `UNSUPPORTED` for a header on D3D12), so dropped on next (2026-10-23) and the binding follows; nothing to file: the desc carried no frame type, so the fix was an API choice; the patch proposed `VideoAV1EncodeDecodeInfoDesc::keyFrame` and D3D12 describing from the metadata whether a header is given or not; reproduced on main 2f7a49f (2026-10-22) |
| N10 An allocator that returns null crashes NRI instead of failing the creation | bug (crash) | fixed upstream in 9d276c3 and aa23763 (2026-10-26), dropped on next; patched on master as 0034 (2026-10-22, PR #26), re-cut on next for 72f5f46's creation rollback (#292, 2026-10-23) and carried until 83fd010; filed 2026-10-24 against main 72f5f46, [#301](https://github.com/NVIDIA-RTX/NRI/issues/301); reproduced on main 2f7a49f (2026-10-22) |
| N11 The memory helper's alignment sort leaves out the last buffer and the last texture | bug (packing) | filed 2026-10-27 against main b5f5c626 as [#305](https://github.com/NVIDIA-RTX/NRI/issues/305), the fix of patch 0035 attached; fixed in v191 by 14a61d47 (2026-10-28, our lines); patch dropped on both branches 2026-11-03 |
| N12 VK: stage masks name shader stages the device lacks | bug | patched on master and `next` as 0039 (2026-11-04); filed 2026-11-05 against main 7ea93bb5 as [#310](https://github.com/NVIDIA-RTX/NRI/issues/310), the fix linked as a fork commit (Nullpoint56/NRI@da79e4a8, main + 0039); measured on v191: MoltenVK in CI, NVIDIA with `disableVKRayTracing` |
| N13 D3D12: every resource creation fails on the inbox runtime | bug (regression) | introduced on main by 3dd4cbfa (after v191; master unaffected); patched on `next` as 0040 (2026-11-05); filed 2026-11-05 as [#309](https://github.com/NVIDIA-RTX/NRI/issues/309), the fix inline; measured on main 7ea93bb5, Windows 11 26200 inbox runtime, NVIDIA |
| N14 `WriteShaderGroupIdentifiers` with a group range past the pipeline's groups crashes | bug (crash) | patched on `next` as 0041 (2026-11-05); filed 2026-11-05 as [#311](https://github.com/NVIDIA-RTX/NRI/issues/311), the fix inline; measured on main 7ea93bb5 (v191 has the same code), NVIDIA, VK and D3D12 |
| N15 VK: pipeline statistics add the mesh counters on `features.meshShader` | bug | patched on `next` as 0042 (2026-11-05); filed 2026-11-05 as [#312](https://github.com/NVIDIA-RTX/NRI/issues/312), the fix inline; measured on main 7ea93bb5 (v191 has the same code), NVIDIA with the Khronos Profiles layer reporting `meshShaderQueries = false` |
| D3D12 attachment resolve into a non-zero layer or mip | bug | not filed: fixed on main by 48d4a33 ("array layers and destination mip indexing"); v190, and so master, still has it; master carries the fix as patch 0036 (a backport of the index and the per-layer loop) until the next tag || Draw-parameters emulation on Vulkan | design question | not filed: validation demands `DrawBaseDesc` strides on every backend, while Vulkan reads plain records at `offset` |

The sections below are the drafts and the audit behind them (every claim re-verified against the
unpatched tree). The filed ones were posted in a shorter form (Observed, Cause with line numbers,
Fix as a snippet); the linked issues hold that text.

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

**Status 2026-11-03.** Fixed in v191 by 0682d020 (our lines); the patch is dropped on master and `next`, which both pin v191.

**Status 2026-10-27.** Filed as [#303](https://github.com/NVIDIA-RTX/NRI/issues/303) against main b5f5c626 (the macOS CI measurement; `DeviceVK.hpp:1554` there).

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
your specific case" and suggested a condition; it was merged unconditionally. A library built on a
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

**Status 2026-11-03.** Fixed in v191 by c1261950 (our lines, a third for `WaitForPresent`, and `ReportMessage` demotes an `OUT_OF_DATE` report to `WARNING`); the patch is dropped on master and `next`, which both pin v191.

**Status 2026-10-27.** Filed as [#304](https://github.com/NVIDIA-RTX/NRI/issues/304) against main b5f5c626 (reproduced there: 6 of 8 resizes report `QueuePresentKHR(): failed, result = 0xC4653214`).

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

## Not ours: NRI #307, Agility 1.620 deprecation warnings (2026-11-03)

Filed by another user on Intel Arc. NRI v191 builds against Agility 1.620.1, whose debug layer reports
each legacy `ID3D12Device::CheckFeatureSupport` as `D3D12_MESSAGE_ID_DEPRECATED_API` (989): 33 per D3D12
device on NVIDIA and WARP, 27 from NRI (`DeviceD3D12.hpp`) and 6 from D3D12MA (its #102, fixed on
its maintainer's fork, not yet upstream). It failed our Windows CI (the device-lost test refuses warnings at
creation), so that test allows exactly these until both fixes reach NRI.

## 0002 — Shared buffers, textures and fences

**Title:** Core: shared buffers, textures and fences (export to another device, API or process), the #49 request

**Status 2026-10-27.** Merged as eb4ed356 (with the maintainer's eecb4cfd and 93a542a8 on the branch) and polished on main in f18c6df9 (`CoreInterface::DestroySharedHandle`) and d0f5ce49 (export support queries and `UNSUPPORTED`, `features.sharedFences` only when a semaphore type exports, the comments rewritten); dropped on `next` at b5f5c62. The text below is the PR as filed.

**Status 2026-10-24.** Filed as PR #294 from the owner's fork, the `FenceDesc` question in a comment on #49. Re-cut in the shape the maintainer asked for on #49: no extension, fields
on the existing descs, the getters in Core.

**Status 2026-10-26.** Reviewed with four items to address, `FenceDesc { initialValue, shared }` accepted, D3D11 sharing may stay a follow-up. Main 83fd010 was merged into the branch (038cecbc). Items and the commits on top of that merge: (1) P1, repeated Win32 exports returned the same handle (VUID 00663 / 01127; reproduced on his RTX 4080): one export per object, retained until destruction, `DuplicateHandle` per caller, first export under a device lock, FD exports per call, `3a49e381`; (2) P1, no external ownership-transfer path for buffers: `BufferBarrierDesc::srcQueue` / `dstQueue`, the texture rules (an `EXCLUSIVE` buffer names the queue's family, a `CONCURRENT` one `IGNORED`, `EXTERNAL_QUEUE` is `VK_QUEUE_FAMILY_EXTERNAL` for either; the all-`IGNORED` first cut drew VUID-vkCmdPipelineBarrier2-srcQueueFamilyIndex-10387 on a one-family buffer), Validation passes the sentinel through, D3D12 ignores, `44aff46c`; (3) P2, `EXTERNAL_QUEUE` expanded to `const Queue*` (MSVC C2440): `static NriPtr(Queue) const`, `5e413361`; (4) the memory-location and shared-object checks moved out of the D3D12 and VK backends into Validation, `FenceVal` tracks `shared`, `2320cc63`; and, after his pointer to `.agents/skills/nri-coding-standard`, `0a7f3b2b` drops the Vulkan `Create` early-outs on `features.sharedResources` / `sharedFences` (Validation checks them) and moves `DuplicateSharedHandle` to the helper section. Reply posted on #294 (2026-10-26).

What the PR added, against main 72f5f46:

- `BufferDesc::shared`, `TextureDesc::shared`: a committed resource in `DEVICE` memory with a
  dedicated, exportable allocation (VK: `VkExternalMemoryBufferCreateInfo` /
  `VkExternalMemoryImageCreateInfo` and `VkExportMemoryAllocateInfo`, allocated outside VMA so the
  export info can be chained, `DeviceVK::AllocateSharedMemory`; D3D12: `D3D12_HEAP_FLAG_SHARED`).
- `FenceDesc { initialValue, shared }`, which `CreateFence` now takes (VK:
  `VkExportSemaphoreCreateInfo`, timeline or binary; D3D12: `D3D12_FENCE_FLAG_SHARED`,
  `UNSUPPORTED` for a binary semaphore). The one API break; the question for the maintainer is
  whether he would rather keep `CreateFence(device, initialValue, fence)` and take the flag
  another way.
- `SharedHandleType`, `SharedHandle { handle, type, size }` and `CoreInterface::GetBufferSharedHandle`
  / `GetTextureSharedHandle` / `GetFenceSharedHandle` beside the native-object getters: a new
  caller-owned handle per call (VK: `vkGetMemory*` / `vkGetSemaphore*` `Win32HandleKHR` or
  `FdKHR`; D3D12: `ID3D12Device::CreateSharedHandle`); `size` is the allocation size on both
  backends, what an importer's `VkMemoryAllocateInfo::allocationSize` or
  `CUDA_EXTERNAL_MEMORY_HANDLE_DESC::size` takes.
- `DeviceDesc::features.sharedResources` / `sharedFences` (VK: the platform's external memory /
  semaphore extension, which the backend enables itself; D3D12: true).
- `EXTERNAL_QUEUE`, a sentinel for `TextureBarrierDesc::srcQueue` / `dstQueue` naming
  `VK_QUEUE_FAMILY_EXTERNAL`, which the wrappers cannot express.
- Validation refuses `shared` outside `CreateCommitted*` and outside `DEVICE` memory, a getter on
  an object that is not shared, and a shared object on a device without the feature. NONE returns
  zero handles. D3D11, WGPU and Metal answer `UNSUPPORTED`: the D3D11 stubs compiled here
  (`NRI_ENABLE_D3D11_SUPPORT=ON`, 2026-10-24), WGPU and Metal are not compiled here.

Measured on main 72f5f46, RTX 4070 SUPER, Windows: a Vulkan texture imports into a second Vulkan
device byte for byte, the timeline semaphore signals both ways, the binary semaphore imported into
a binary VkSemaphore is signalled and waited by queue submits in both directions with the Khronos
layer clean; a D3D12 fence opens on a second D3D12 device and signals both ways; a D3D12 texture
opens on a second D3D12 device; CUDA (the driver API, `cuImportExternalMemory` /
`cuImportExternalSemaphore`) imports a shared buffer and the shared fence from both backends, reads
what NRI wrote, writes, signals, and NRI's queue waits on CUDA's signal and reads CUDA's bytes;
a shared texture NRI rendered imports as a mipmapped array, level 0 identical to NRI's readback
(`CudaInteropTests`). Lavapipe: `sharedResources` 1, `sharedFences` 0 (no
`VK_KHR_external_semaphore_fd`), buffer and texture export through fds. Known limitation to state:
D3D11 refuses the typeless resource NRI creates. Not in the PR, a possible follow-up:
`AllocateMemoryDesc::shared` and a memory getter for placed resources (some Vulkan drivers export
images only from dedicated allocations, so it needs a capability check first).

The earlier extension shape (`NRISharedHandle.h`, `CreateSharedTexture` / `CreateSharedFence` /
`GetTextureSharedHandle` / `GetFenceSharedHandle`) is master's 0002 at v190; its measurements
(2026-10-13 to 2026-10-16) are the same imports, without buffers and CUDA.

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

**Status 2026-11-03.** Fixed in v191 by 9764221e (another shape: `GetEffectiveShaderStages` resolves `ALL` and `ALL_SHADERS` to the layout's stages in every backend and in validation); the patch is dropped on master and `next`, which both pin v191.

**Status 2026-10-27.** Filed as [#302](https://github.com/NVIDIA-RTX/NRI/issues/302) against main b5f5c626 (reproduced there: both messages, VK and D3D12).

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
frees inside `EndStreamerFrame` (measured on a streaming workload: 222 stream calls at 477 ms, single calls
up to 62 ms, and `EndStreamerFrame` at 189 ms per load). Proposal: `size` is the initial
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
the `EXTERNAL_QUEUE` term, which is patch 0002's (merged upstream as #294 on 2026-10-27).

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
upstream form drops the `EXTERNAL_QUEUE` term, which is patch 0002's, merged as #294 (keep
`VK_QUEUE_FAMILY_EXTERNAL` sides if upstream ever has one).

## 0024 — Adapter enumeration filtered by graphics API (proposal)

Status 2026-10-23: main's 962615b (#286) gives `nriEnumerateAdapters` a `desiredGraphicsAPIs`
mask, which is this proposal's first half in the maintainer's shape; the draft below is kept for
the record. What `next` still patches (two lines) is the second half: `nriCreateDevice` with a
null `adapterDesc` probes VK only for a VK device.

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

## 0037 — The Vulkan swap chain is created without `VkSwapchainPresentModesCreateInfoKHR`

Status: not filed, a layer warning (like 0013): one present mode is what Vulkan assumes without the
structure, so nothing behaves differently.

**Title:** VK: `VkSwapchainPresentModesCreateInfoKHR` is not chained with `swapchainMaintenance1`

`SwapChainVK::Create` (`Source/VK/SwapChainVK.hpp:368-374` at 72f5f46) chains
`VkSwapchainPresentScalingCreateInfoKHR` when the device has `swapchainMaintenance1`, but not
`VkSwapchainPresentModesCreateInfoKHR`, and the Khronos best-practices checks report
`BestPractices-vkCreateSwapchainKHR-no-VkSwapchainPresentModesCreateInfoKHR-provided` for every
swap chain: "No VkSwapchainPresentModesCreateInfoKHR was provided to VkCreateSwapchainKHR. When
VK_KHR_swapchain_maintenance1 is enabled, a VkSwapchainPresentModesCreateInfoKHR should be provided
to inform the implementation that the application is aware of the new features in a backward
compatible way." Measured on main 72f5f46, NVIDIA, layer 1.4.341. Fix: chain it with the chosen
`presentMode` as the only entry (NRI never switches modes on present, and one entry is what Vulkan
assumes without the structure):

```cpp
VkSwapchainPresentModesCreateInfoKHR presentModesInfo = {VK_STRUCTURE_TYPE_SWAPCHAIN_PRESENT_MODES_CREATE_INFO_KHR};
presentModesInfo.presentModeCount = 1;
presentModesInfo.pPresentModes = &presentMode;

if (m_Device.m_IsSupported.swapChainMaintenance1)
    PNEXTCHAIN_APPEND_STRUCT(presentModesInfo);
```

Patch attached (`patches/0037-vulkan-swapchain-present-modes-info.patch`), eight lines.

## 0038 — `DeviceCreationDesc::vkInstanceCreateInfoNext` (proposal)

Status: proposal, drafted 2026-10-24; filed 2026-11-05 in [PR #313](https://github.com/NVIDIA-RTX/NRI/pull/313).

**Title:** `DeviceCreationDesc`: let the caller chain structures into `VkInstanceCreateInfo`

NRI creates the Vulkan instance itself (`DeviceVK::CreateInstance`, `Source/VK/DeviceVK.hpp:2069`
at 72f5f46) and chains only its own `VkDebugUtilsMessengerCreateInfoEXT` and, with
`enableGraphicsAPIValidation`, `VkValidationFeaturesEXT`. So `VkLayerSettingsCreateInfoEXT`
(`VK_EXT_layer_settings`) cannot be used, and the validation layer can be configured only
through `VK_KHRONOS_VALIDATION_*` environment variables or `vk_layer_settings.txt`. Both apply to
every instance in the process, and the environment is read at instance creation, so two devices
cannot differ and a host that sets them at run time must do it before NRI creates the instance.
Measured on main 72f5f46, NVIDIA, layer 1.4.341: with the field below, a
`VkLayerSettingsCreateInfoEXT` setting `validate_best_practices` switches the best-practices checks
on for that device only (the 64 KiB committed buffer's
`BestPractices-vkBindBufferMemory-small-dedicated-allocation` appears, and not on a device created
without it). Proposal, next to `vkExtensions`:

```cpp
NriOptional const void* vkInstanceCreateInfoNext; // appended to "VkInstanceCreateInfo::pNext" after NRI's "VkDebugUtilsMessengerCreateInfoEXT" and "VkValidationFeaturesEXT" (i.e. "VkLayerSettingsCreateInfoEXT")
```

and in `CreateInstance`, after NRI's own structures: `*_tail = instanceCreateInfoNext;`. It goes
last because the chain is the caller's and stays `const`. Ignored by the wrapper path (the instance
is the caller's) and by the other backends. Patch attached
(`patches/0038-vulkan-instance-create-info-next.patch`), one header line, three source lines added and three changed.
Open question: a generic chain, as here, or a typed `VKLayerSettings` desc.

---

## Found by the coverage tests (patched on master as 0025-0035, PR #26; N1-N4, N6, N7 and N10 filed 2026-10-24 as #295-#301)

**Status 2026-10-26.** All seven filed reports are fixed on NRI main, each in its own commit:
N1 0593d91 (#295; our line, plus validation of `streamIndex`, of `bindingSlot` and of the
`SetVertexBuffers` slot range), N2 8a8fc2b (#296), N3 909a139 (#297), N4 c8fe3b2 (#298, also in
D3D11), N6 1dc13b2 (#299; the commit message says "fixed #300"), N7 561e2be (#300, another shape:
`GetResourceDesc` asks for 256 for an acceleration-structure buffer, and 128 for a micromap one,
under tight alignment, and `GetMemoryDesc` keeps that floor), N10 9d276c3 and aa23763 (#301,
`CreateImplementation` and `DeviceVal::WrapImplementation` in every backend and validation).
`next` moved to 83fd010 and dropped 0025-0028, 0030, 0031 and 0034; each test passes there
without its patch (Windows, NVIDIA RTX 4070 SUPER, VK and D3D12; lavapipe in WSL). Master keeps
them until the tag. N5 and N11 were filed later (#306, #305) and are fixed in v191.

Found on 2026-10-21 by the tests that close the coverage report's blind spots (branch
`coverage-tests`, on master); N11 on 2026-10-22, reading the memory helper while the binding's
disposal rule for its allocations was written, then measured. Measured on v190 with master's
patches, none of which touch these lines, on the development machine's discrete GPU, and on WARP
where it says so. Line numbers and code are NRI main's at 2f7a49f, where every line N1 to N11 cite
is unchanged. N1 to N8, N10 and N11 are written in the filed form; N9 is a design question.

**Reproduced on NRI main** (2026-10-22): `next` at 7c57f89, NRI 2f7a49f with `next`'s 13 patches,
none of which touch these lines, on the same GPU. Each probe is the shipped test without its
patch (temporary, not committed); N6 and N9 also come from the suite itself, whose micromap test
skips quoting N6's `Close` failure and whose AV1 test records N9. Every item answers as on v190,
with the same message texts:
- N1: Vulkan draws 2,162 of the 4,096 pixels wrong, with VUID-vkCmdBindVertexBuffers2-pStrides-06209;
  D3D12 is exact.
- N2: VUID-vkCmdDraw-pStrides-04913 on the draw, the image unchanged; D3D12 is clean.
- N3: D3D12 dies inside `CmdClearAttachments` with 0xC0000005; Vulkan clears, clean.
- N4: over a stencil of 9, a pass with the view as `depth` only and a clear value of 5: Vulkan 5,
  D3D12 9.
- N5: `ClearStorage` through a `STORAGE_TEXTURE` view with `layerOffset` 1: Vulkan clears layer 1,
  D3D12 layer 0, with no message.
- N6: [1331] and the `Close` failure after `{MICROMAP_WRITE, MICROMAP}`; the build alone and the
  build with `{MICROMAP_WRITE, ALL}` close; [1162] at the micromap's creation, as before.
- N7: reported alignment 128; a TLAS placed at 2,688 (its address 128 past a multiple of 256) draws
  [31] from `TryCreateShaderResourceView`.
- N8: the encoder requires `LOOP_RESTORATION_FILTER | AUTO_SEGMENTATION | CDEF_FILTERING`; NRI's
  default picture flags draw [1306], and `Close` fails.
- N9: the payload-header path answers `FAILURE` with no message; two agreeing references describe
  the key frame as P.
- N10: `CreateFence` dies with 0xC0000005 on Vulkan and D3D12, NRI validation on and off.
- N11: the layouts and allocation counts measured on v190, unchanged (Vulkan 1 or 2 allocations at
  3,056 bytes, D3D12 1 or 2 at 128 KiB, by input order).

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

Validation never checked `attributes[i].streamIndex < streamNum`; D3D12 read `streams[]` past the
array, and the fix makes Vulkan do the same. Patch 0025 adds the check to `DeviceVal::CreatePipeline`
(`INVALID_ARGUMENT`, naming `streamIndex`); an attribute with streamIndex 1 of one stream created a
pipeline on both backends before it.

### N2: Vulkan: an empty vertex input makes the binding stride dynamic

**Observed.** A graphics pipeline whose `vertexInput` has no attributes and no streams, drawn with
`CmdDraw` and nothing bound: `[2005986124] VUID-vkCmdDraw-pStrides-04913: vkCmdDraw(): VK_DYNAMIC_STATE_VERTEX_INPUT_BINDING_STRIDE state is dynamic, but the command buffer never called vkCmdBindVertexBuffers2.`
The image equals the one with `vertexInput = nullptr`; D3D12 is clean.

**Cause.** `Source/VK/PipelineVK.hpp:378-379` add the dynamic stride for any non-null `vertexInput`;
with no streams there is nothing to bind. Since e7d4c4f, which made the state depend on a non-null `vertexInput` (254424c only added the `extendedDynamicState` condition).

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

**Status 2026-11-03.** Fixed in v191 by 71d3c586 (another shape: D3D11 and D3D12 build the view as a one-layer array view, so it addresses the layer instead of being refused); the patch is dropped on master and `next`, which both pin v191.

**Status 2026-10-27.** Filed as [#306](https://github.com/NVIDIA-RTX/NRI/issues/306) against main b5f5c626 (the view is created there without a message; `DescriptorD3D12.hpp:165`, `:217`), proposing the Validation refusal and offering documentation instead.

**Observed.** A two-layer `R32_UINT` `TEXTURE_2D`, a `STORAGE_TEXTURE` view with `layerOffset = 1`,
`CmdClearStorage`: Vulkan clears layer 1, D3D12 clears layer 0 (hardware and WARP). Neither NRI
validation nor either backend's layer says anything.

**Cause.** `Source/D3D12/DescriptorD3D12.hpp:214-218` create a `D3D12_UAV_DIMENSION_TEXTURE2D`
view, which has no array slice, so `layerOffset` is dropped. `:164-167` do the same for `TEXTURE`
(an SRV; not measured). Vulkan honours it (`baseArrayLayer`, `DescriptorVK.hpp:286`). Since the
initial commit.

**Fix.** D3D12 has no non-array view of layer N (1D, 2D, 2DMS and cube), and an array view would not
match the shader's non-array declaration. Patch 0029 (master, 2026-10-22) refuses `layerOffset != 0`
for `TEXTURE` / `SUBPASS_INPUT` / `STORAGE_TEXTURE` / `TEXTURE_CUBE` views of a 1D or 2D texture: the
D3D12 backend reports it and returns `INVALID_ARGUMENT`, and validation refuses it on every backend,
pointing to the `_ARRAY` types.

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
required set is gone. Since 2741780, which added the picture-flag condition (3b9fe2d only moved the feature flags behind an accessor).

**Fix.** Keep `RequiredFeatureFlags` in the session, and set the picture flag when
`AUTO_SEGMENTATION` is required, as well as when the picture asks for segmentation.

### N9: D3D12: GetVideoAV1EncodeDecodeInfo fails silently on a payload header and infers the frame type (design question)

**Observed.**
- With `encodedPayloadHeader` set to the first bytes of a D3D12 AV1 payload, the call answers
  `FAILURE` with no message. The D3D12 encoder writes bare tile data, with no OBUs.
- With a DPB snapshot passed for a key frame, the info describes the frame as inter.

**Cause.** `Source/D3D12/VideoSessionD3D12.hpp:128-129` parse a given header as OBUs. `:193` decides
inter from `desc.referenceNum` (since 39ce136), and `VideoAV1EncodeDecodeInfoDesc` carries no frame
type.

**Fix.** His choice; ours is patch 0033 (master, 2026-10-22): D3D12 does not read the header and
describes the frame from the metadata, so one desc works on both backends, and
`VideoAV1EncodeDecodeInfoDesc::keyFrame` (`NriOptional bool`) says the references are not used. The
alternative is a note in `NRIVideo.h` that `references` stays empty for key frames.

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
if (!impl) {
    entity = nullptr;
    return Result::OUT_OF_MEMORY;
}
```

### N11: the memory helper's alignment sort leaves out the last buffer and the last texture

**Status 2026-11-03.** Fixed in v191 by 14a61d47 (our lines); the patch is dropped on master and `next`, which both pin v191.

**Status 2026-10-27.** Filed as [#305](https://github.com/NVIDIA-RTX/NRI/issues/305) against main b5f5c626 (reproduced there: VK two allocations in 2 of 6 orders, D3D12 in 4 of 6; `HelperInterface.hpp:556`, `:568`).

**Observed.** `AllocateAndBindMemory` packs the last buffer and the last texture of a group where they
were passed, after the sorted rest, so the same resources in another order get another layout and,
with `preferredMemorySize`, another allocation count.
- Vulkan, three 1000-byte buffers of one memory type, aligned to 16 (vertex), 64 (constant) and 128
  (scratch). Passed as scratch, constant, vertex, they land at +1024, +0 and +2032: the first two
  sorted, the vertex buffer last. Passed as vertex, scratch, constant, the constant buffer lands at
  +2048, after the scratch buffer, and with `preferredMemorySize` 3056 it gets a second allocation,
  where vertex, constant, scratch fit one.
- D3D12, one memory type, two shader-resource textures (64x64 and 16x16, 4 KiB aligned) and a 64x64
  colour attachment (64 KiB aligned), `preferredMemorySize` 128 KiB: one allocation when the
  attachment is passed last, two whenever a small texture is.
- Two buffers, or two textures, are never sorted.

**Cause.** `Source/Shared/HelperInterface.hpp:558` and `:570` end the sorted range at
`&buffers[bufferNum - 1]` and `&textures[textureNum - 1]`, and `std::sort` excludes its end. Since
b5e2b07 ("improved resource packing", first in v176).

**Fix.**

```cpp
std::sort(&buffers[0], &buffers[0] + resourceGroupDesc.bufferNum,
...
std::sort(&textures[0], &textures[0] + resourceGroupDesc.textureNum,
```

**Scope.** Packing only: every layout is valid. The order the fix restores is not always the
tightest either: as passed, scratch, constant, vertex fit 3,040 bytes; sorted, vertex, constant,
scratch take 3,056.

### N12: VK: stage masks name shader stages the device lacks

**Observed.** On a Vulkan device without tessellation, geometry, task, mesh or ray tracing shaders,
a barrier or fence wait with `StageBits::ALL_SHADERS` and a pipeline layout whose stages are `ALL`
give stage bits the device does not allow. MoltenVK (no geometry, mesh or ray tracing), v191: every
such barrier draws VUID-VkImageMemoryBarrier2-dstStageMask-03929, -03934, -03935 and -07946; every
such layout VUID-VkPushConstantRange-stageFlags-parameter at `vkCreatePipelineLayout` and
VUID-VkPushConstantsInfo-stageFlags-parameter at `vkCmdPushConstants2`. NVIDIA with
`disableVKRayTracing`: a global barrier on `ALL_SHADERS` draws VUID-VkMemoryBarrier2-srcStageMask-07946
and -dstStageMask-07946, a root constant visible to `ALL` both push-constant VUIDs.

**Cause.** `GetPipelineStageFlags` (`ConversionVK.h:356`, since 5f7fd91) and `GetShaderStageFlags`
(`:436`) map each named stage to its Vulkan bit with no device in reach; `CommandBufferVK::Barrier`
(`CommandBufferVK.hpp:2648-2688`), `QueueVK::Submit` (`QueueVK.hpp:90`, `:104`) and the layout's
resolved stages (`PipelineLayoutVK.hpp:34`) pass the result through. `CmdSetDescriptorSet` already
adds the mesh stages only with `features.meshShader`.

**Fix.** Patch 0039: `DeviceVK` records the shader stages it lacks, from the features it enabled
(tessellation and geometry from `features`, task and mesh from `VkPhysicalDeviceMeshShaderFeaturesEXT`,
ray tracing from `rayTracingPipeline`); barriers and fence waits drop those pipeline stages, and a
pipeline layout drops them from its stages, which `ALL` and `ALL_SHADERS` resolve to. In Vulkan
terms for barriers, since `StageBits::NONE` is `0x7FFFFFFF` and cannot be masked.

### N13: D3D12: every resource creation fails on the inbox runtime

**Observed.** Main 7ea93bb5 built with `NRI_ENABLE_AGILITY_SDK_SUPPORT`, running on the inbox runtime
(no Agility SDK in the process; Windows 11 26200, "Using ID3D12Device14"): every committed, bound
and placed buffer and texture fails with `INVALID_SDK`, "D3D12MA::CreateResource3(): failed, result
= 0x80004002" (committed) or "ID3D12Device10::CreatePlacedResource2(): failed, result = 0x80004002"
(bound, placed). With the D3D12 debug layer on, the failed bind ends the process (0xC0000409) with
no message. RenderDoc, which captures the inbox runtime only, runs no NRI frame. v191 creates all of
them; with Agility 1.620 both do.

**Cause.** 3dd4cbfa sets `ID3D12ResourceBest` to `ID3D12Resource3` (`BufferD3D12.h:6`,
`TextureD3D12.h:6`). The device and command lists go through `QueryLatestInterface`, but resources
are created with `IID_PPV_ARGS(&m_Buffer)` / `IID_PPV_ARGS(&m_Texture)` (`BufferD3D12.hpp:49`, `:98`,
`:101`; `TextureD3D12.hpp:63`, `:118`, `:121`), i.e. for `ID3D12Resource3`, which the inbox runtime
does not implement. NRI calls none of its additions (`GetDesc2`, `GetCastableFormatsCount`,
`GetCastableFormats`).

**Fix.** Patch 0040: `typedef ID3D12Resource2 ID3D12ResourceBest` in both headers. (Creating as
`ID3D12Resource` and querying the latest would also do, if `Resource3` is wanted later.) The video
encoder has the same shape (`ID3D12VideoEncoderBest` = `ID3D12VideoEncoder1`, created by IID, with
`TODO-VIDEO: use "QueryLatestInterface"` on the line); not measured on the inbox runtime.

### N14: WriteShaderGroupIdentifiers with a group range past the pipeline's groups crashes

**Observed.** NRI validation on, a ray tracing pipeline with three groups,
`WriteShaderGroupIdentifiers(pipeline, 2, 2, ...)`: D3D12 ends the process with an access violation
(0xC0000005) and no message; Vulkan reports
VUID-vkGetRayTracingShaderGroupHandlesKHR-firstGroup-02419 ("firstGroup (2) + groupCount (2) must be
less than or equal to the number of shader groups in pipeline (3)") and then ends the process with
an access violation in the NVIDIA driver. NVIDIA RTX 4070 SUPER, main 7ea93bb5.

**Cause.** `PipelineVal::WriteShaderGroupIdentifiers` checks only `dstStride` (`PipelineVal.hpp:25`);
the validation pipeline does not keep the group count (`DeviceVal::CreateRayTracingPipeline` wraps
the pipeline with the generic `PipelineVal` constructor, `DeviceVal.hpp:1900`, so the one taking the
`RayTracingPipelineDesc` is never used). D3D12 then reads
`m_ShaderGroupNames[shaderGroupIndex]` without a bound (`PipelineD3D12.hpp:691`), and Vulkan passes the
range to `vkGetRayTracingShaderGroupHandlesKHR` (`PipelineVK.hpp:707`).

**Fix.** Patch 0041: the validation pipeline is created with its `RayTracingPipelineDesc`, keeps its
`shaderGroupNum` and refuses
`baseShaderGroupIndex + shaderGroupNum` past it with `INVALID_ARGUMENT` (64-bit sum). A wrapped
pipeline has no known count (`PipelineVKDesc` has none) and stays unchecked.

### N15: VK: pipeline statistics add the mesh counters on features.meshShader

**Observed.** On a Vulkan device with mesh shaders but without `meshShaderQueries`, every
`PIPELINE_STATISTICS` pool draws VUID-VkQueryPoolCreateInfo-meshShaderQueries-07069
("pipelineStatistics ... contains mesh/task shader bit, but meshShaderQueries feature was not
enabled"), and `GetQuerySize` answers 104 bytes for 11 counters. Measured on NVIDIA RTX 4070 SUPER,
main 7ea93bb5, with the Khronos Profiles layer below the validation layer reporting
`meshShaderQueries = false` and simulating nothing else.

**Cause.** `QueryPoolVK.hpp:38` adds the task and mesh statistic bits, and `:47` and `:56` size the
query for 13 counters, when `features.meshShader` is set (since f53490f). Those bits need
`meshShaderQueries`, which `DeviceVK.hpp:1710` reports as `features.meshShaderPipelineStats`, the bit
`PipelineStatisticsDesc` documents for the two fields.

**Fix.** Patch 0042: the three lines test `features.meshShaderPipelineStats`.

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
- **Draw-parameters emulation on Vulkan** (a question).
  - Validation (`CommandBufferVal.hpp`) demands `sizeof(DrawBaseDesc)` strides for indirect draws
    with the emulation flag on every backend.
  - The Vulkan backend reads plain records at `offset`. Measured with the flag on Vulkan, a stride
    of 16 is refused ("'stride' is too small, expected >= 24"), `DrawBaseDesc` records at stride 24
    draw nothing, and at `offset + 8` they draw.
  - The portable rule today: set the flag on D3D12 only.
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
