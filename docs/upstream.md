# Upstream — what to send to NRI, and in what order

Each patch in `patches/` is either a bug upstream should fix or an extension upstream might want.
This file holds the report text for each, so that filing is a copy and a link to a commit rather
than a reconstruction. Status is updated as they are filed and answered.

| Patch | Kind | Status |
|---|---|---|
| 0003 validation `HAS_BUFFER` crash | bug | to file, first |
| 0004 Vulkan minor version above the instance's | bug | to file with 0003 |
| 0005 `MemoryHeap::alignment` uninitialised (GCC `-Werror` build) | bug | to file with 0003 |
| 0006 `samplerLodBiasMax` above what a portability device allows | bug | to file with 0003 |
| 0007 Apple loader link should be optional | cleanup | to file after 0003, cite PR 134 |
| 0008 `nriConvertVKFormatToNRI` misses every compressed format | bug | to file with 0003 |
| 0009 `OUT_OF_DATE` from acquire/present reported as an error | bug (reporting) | to file with 0003 |
| 0001 Agility SDK from a path | proposal | to file after 0003 |
| 0002 SharedHandle extension | proposal | to file once the editor has used it |

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
