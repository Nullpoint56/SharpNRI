# patches/

NRI-shaped extensions applied on top of the pinned upstream tag before the native build. Each patch
is a `git diff` of `nri/` against `v$(NriVersion)` with a prose header explaining it, numbered in
application order, and must apply cleanly onto the next upstream tag when the pin moves.

Applied by `tools/build-native.ps1` and by `tools/generate-native.ps1`, because a patch that changes
a header changes the L0 layer and the ABI oracle measures the patched header. Both skip a patch
that is already applied, so the submodule working tree normally sits with every patch applied and
`git -C nri status` shows the patched files as modified. Listed and explained in `docs/design.md`.

| Patch | What | Why |
|---|---|---|
| `0001-agility-sdk-device-factory` | `DeviceCreationDesc::d3d12AgilitySdkPath`; device creation goes through `ID3D12SDKConfiguration1::CreateDeviceFactory` + `ApplyToGlobalState` when set | a managed host cannot export `D3D12SDKVersion`, so NRI's documented route never loads the SDK (findings §6.4) |
| `0002-shared-handle-extension` | `NRISharedHandle.h`: shared textures and fences with caller-owned exported handles, on Vulkan (opaque win32/fd), D3D12 (NT handles), NONE (dummy), through the validation layer; plus `EXTERNAL_QUEUE`, a sentinel queue for barriers that release to or acquire from `VK_QUEUE_FAMILY_EXTERNAL` | upstream has no export path at all; a compositor importing the viewport needs one (findings §7.4) |
| `0003-validation-has-buffer-sentinel` | the validation layer's geometry conversion passes `HAS_BUFFER` through instead of calling `GetImpl()` on address 1 | upstream bug: with NRI validation on, sizing an acceleration structure with the documented sentinel is an access violation (findings §7.5) |
| `0004-vulkan-clamp-minor-version-to-instance` | `DeviceVK::Create` caps `m_MinorVersion` at the version `CreateInstance` requested | upstream bug: an older loader in front of a newer driver (Ubuntu 24.04's 1.3 loader, Mesa 25's 1.4 lavapipe) makes NRI load core 1.4 entry points the driver refuses, so `CmdSetRootConstants` calls null (findings §8.4) |
| `0005-helper-memoryheap-alignment-init` | `MemoryHeap::alignment` initialised in the constructor | upstream bug: GCC 13 at -O2 fails the `-Werror` build on the uninitialised member; upstream's Ubuntu job configures unoptimised and never sees it (findings §8.1) |
| `0006-vulkan-portability-sampler-lod-bias` | `DeviceDesc::other.samplerLodBiasMax` is 0 when `VK_KHR_portability_subset` is enabled without `samplerMipLodBias` | upstream bug: MoltenVK forbids any mip LOD bias while reporting `maxSamplerLodBias = 15.999`, so honouring NRI's limit fails validation (findings §9.4) |
