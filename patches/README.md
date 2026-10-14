# patches/

NRI-shaped extensions applied on top of the pinned upstream tag before the native build. Each patch
is a `git diff` of `nri/` against `v$(NriVersion)` with a prose header explaining it, numbered in
application order, and must apply cleanly onto the next upstream tag when the pin moves.

Applied by `tools/build-native.ps1` and by `tools/generate-native.ps1`, because a patch that changes
a header changes the L0 layer and the ABI oracle measures the patched header. Both skip a patch
that is already applied, so the submodule working tree normally sits with every patch applied and
`git -C nri status` shows the patched files as modified. Listed and explained in `CLAUDE.md` (Patches).

| Patch | What | Why |
|---|---|---|
| `0001-agility-sdk-device-factory` | `DeviceCreationDesc::d3d12AgilitySdkPath`; device creation goes through `ID3D12SDKConfiguration1::CreateDeviceFactory` + `ApplyToGlobalState` when set | a managed host cannot export `D3D12SDKVersion`, so NRI's documented route never loads the SDK (CLAUDE.md: D3D12 and the Agility SDK) |
| `0002-shared-handle-extension` | `NRISharedHandle.h`: shared textures and fences with caller-owned exported handles, on Vulkan (opaque win32/fd), D3D12 (NT handles), NONE (dummy), through the validation layer; plus `EXTERNAL_QUEUE`, a sentinel queue for barriers that release to or acquire from `VK_QUEUE_FAMILY_EXTERNAL`; `CreateSharedFence` with the `SWAPCHAIN_SEMAPHORE` initial value creates an exportable binary semaphore on Vulkan (`UNSUPPORTED` on D3D12) | upstream has no export path at all; sharing a texture or fence with another API or process needs one, and external compositors import binary semaphores (CLAUDE.md: Shared handles) |
| `0003-validation-has-buffer-sentinel` | the validation layer's geometry conversion passes `HAS_BUFFER` through instead of calling `GetImpl()` on address 1 | upstream bug: with NRI validation on, sizing an acceleration structure with the documented sentinel is an access violation (CLAUDE.md: Ray tracing) |
| `0004-vulkan-clamp-minor-version-to-instance` | `DeviceVK::Create` caps `m_MinorVersion` at the version `CreateInstance` requested | upstream bug: an older loader in front of a newer driver (Ubuntu 24.04's 1.3 loader, Mesa 25's 1.4 lavapipe) makes NRI load core 1.4 entry points the driver refuses, so `CmdSetRootConstants` calls null (CLAUDE.md: Vulkan loader and validation layers) |
| `0005-helper-memoryheap-alignment-init` | `MemoryHeap::alignment` initialised in the constructor | upstream bug: GCC 13 at -O2 fails the `-Werror` build on the uninitialised member; upstream's Ubuntu job configures unoptimised and never sees it (CLAUDE.md: CI runners and flakes) |
| `0006-vulkan-portability-sampler-lod-bias` | `DeviceDesc::other.samplerLodBiasMax` is 0 when `VK_KHR_portability_subset` is enabled without `samplerMipLodBias` | upstream bug: MoltenVK forbids any mip LOD bias while reporting `maxSamplerLodBias = 15.999`, so honouring NRI's limit fails validation (CLAUDE.md: MoltenVK and the paravirtual Mac) |
| `0007-cmake-vulkan-loader-link-option` | `NRI_ENABLE_VULKAN_LOADER_LINKING` wraps the Apple `find_package(Vulkan)` + `Vulkan_LIBRARIES` link, default ON | upstream PR 134 linked the loader for an IDE case and the maintainer called it "just not needed"; a shipped `libNRI.dylib` then needs the build machine's loader path to load at all (CLAUDE.md: MoltenVK and the paravirtual Mac) |
| `0008-vulkan-format-table-compressed` | `VK_FORMAT_TABLE` covers VkFormat 131 to 184: BC, ETC2/EAC and ASTC map to NRI's formats (BC1 RGB, which NRI lacks, stays `UNKNOWN`) | upstream gap: `nriConvertVKFormatToNRI` answered `UNKNOWN` for every compressed format although `nriConvertNRIFormatToVK` produces them (CLAUDE.md: Flat exports and formats) |
| `0009-vulkan-swapchain-out-of-date-not-an-error` | `AcquireNextTexture` and `QueuePresent` return `OUT_OF_DATE` for `VK_ERROR_OUT_OF_DATE_KHR` without reporting it; every other failure is reported as before | upstream reporting defect: the documented result of a resize was reported as `Message::ERROR` (CLAUDE.md: Extensions) |
