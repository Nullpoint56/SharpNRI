# patches/

NRI-shaped extensions applied on top of the pinned upstream tag before the native build. Each patch
is a `git diff` of `nri/` against `v$(NriVersion)` with a prose header explaining it, numbered in
application order, and must apply cleanly onto the next upstream tag when the pin moves.

Applied by `tools/build-native.ps1` and by `tools/generate-native.ps1`, because a patch that changes
a header changes the L0 layer and the ABI oracle measures the patched header. Both skip a patch
that is already applied, so the submodule working tree normally sits with every patch applied and
`git -C nri status` shows the patched files as modified. Listed and explained in `CLAUDE.md` (Patches).

The set for NRI v191 (46c93d79, 2026-10-30), on master and on `next` since 2026-11-03 (`CLAUDE.md`:
Branches). Master carried thirty-seven patches on v190; v191 carries the fix of twenty-six. Fixed on
NRI main before v191: 0003, 0004, 0005, 0008, 0010, 0014, 0020, 0021, 0022, 0023, 0032, 0033, 0036
(48d4a33) and master 0024's `nriEnumerateAdapters2` (962615b); the seven filed reports 0025 (#295),
0026 (#296), 0027 (#297), 0028 (#298), 0030 (#299), 0031 (#300) and 0034 (#301); 0002, merged as #294
(eb4ed356, polished in f18c6df9 and d0f5ce49). In v191 itself: 0016 (#302, 9764221e), 0006 (#303,
0682d020), 0009 (#304, c1261950), 0035 (#305, 14a61d47) and 0029 (#306, 71d3c586, another shape: the
view addresses the layer instead of being refused). Of 0024 only the two lines main did not take stay.
All eleven apply in order onto v191, each also alone, and each passes the scripts' `-C0` reverse
check; 0007 and 0017 are re-cut for line offsets (2026-11-03). 0037 and 0038 were cut on `next`
(2026-10-24): a layer warning and a phase 2 proposal.

| Patch | What | Why |
|---|---|---|
| `0001-agility-sdk-device-factory` | `DeviceCreationDesc::d3d12AgilitySdkPath`; device creation goes through `ID3D12SDKConfiguration1::CreateDeviceFactory` + `ApplyToGlobalState` when set | a managed host cannot export `D3D12SDKVersion`, so NRI's documented route never loads the SDK (CLAUDE.md: D3D12 and the Agility SDK) |
| `0007-cmake-vulkan-loader-link-option` | `NRI_ENABLE_VULKAN_LOADER_LINKING` wraps the Apple `find_package(Vulkan)` + `Vulkan_LIBRARIES` link, default ON | upstream PR 134 linked the loader for an IDE case and the maintainer called it "just not needed"; a shipped `libNRI.dylib` then needs the build machine's loader path to load at all (CLAUDE.md: MoltenVK and the paravirtual Mac) |
| `0012-streamer-dynamic-buffer-initial-size` | `StreamerDesc::dynamicBufferDesc.size` is the initial per-frame size of the dynamic buffer, allocated at creation through `Grow`; 0 keeps the on-demand growth | upstream ignores the field and grows from empty, one committed buffer per new per-frame high-water mark, destroyed `queuedFrameNum` frames later inside `EndStreamerFrame` (CLAUDE.md: Extensions) |
| `0013-vulkan-exclusive-image-no-family-indices` | `DeviceVK::FillCreateInfo` passes no queue family indices for `VK_SHARING_MODE_EXCLUSIVE` images | upstream passes every active family's index whatever the sharing mode; the Khronos best-practices checks report `BestPractices-vkCreateImage-sharing-mode-exclusive` per image (CLAUDE.md: Memory and resources) |
| `0015-vulkan-message-id-name` | the debug-utils callback forwards `pMessageIdName` as `[id] name: text` | upstream forwards the number and the text; current Khronos layers keep the name only in `pMessageIdName` (CLAUDE.md: Device creation and callbacks) |
| `0017-swapchain-latest-ready-bit` | `SwapChainBits::LATEST_READY`: with `VSYNC`, Vulkan prefers FIFO_LATEST_READY over FIFO when `DeviceDesc::features.latestReadySwapChain` is set; the bit is ignored otherwise and on D3D | upstream reaches FIFO_LATEST_READY only without `VSYNC` and after MAILBOX, which NVIDIA always offers, so it is never chosen there, and NRI never reports the chosen mode (CLAUDE.md: Extensions) |
| `0018-d3d12-break-on-error-switch` | `DeviceCreationDesc::disableD3D12BreakOnError` skips `ID3D12InfoQueue::SetBreakOnSeverity` for ERROR and CORRUPTION; the messages still reach `CallbackInterface` | upstream sets the break with every debug-layer device, so in a process without a debugger the first D3D12 error is a breakpoint exception out of the NRI call before the caller sees the message (CLAUDE.md: D3D12 and the Agility SDK) |
| `0019-d3d12-gpu-based-validation-switch` | `DeviceCreationDesc::enableD3D12GpuBasedValidation` calls `ID3D12Debug1::SetEnableGPUBasedValidation` after `EnableDebugLayer` | upstream has no way to ask for GPU-based validation, which must be enabled before the device is created; the alternative is D3D12 COM in the caller (CLAUDE.md: D3D12 and the Agility SDK) |
| `0024-enumerate-adapters-by-graphics-api` | `nriCreateDevice` with no adapter probes only the Vulkan backend for a Vulkan device (NRI main's `nriEnumerateAdapters` takes the backends to probe since 962615b, which master's 0024 did as `nriEnumerateAdapters2`) | upstream gap: the device creation still probes every backend, so a Vulkan device created a DXGI factory and D3D12 devices and loaded `D3D12Core.dll` for the process (CLAUDE.md: Device creation and callbacks) |
| `0037-vulkan-swapchain-present-modes-info` | with `swapchainMaintenance1`, the Vulkan swap chain is created with `VkSwapchainPresentModesCreateInfoKHR` listing its present mode | upstream chained the present-scaling info but not this, and the Khronos best-practices checks report `BestPractices-vkCreateSwapchainKHR-no-VkSwapchainPresentModesCreateInfoKHR-provided` for every swap chain (CLAUDE.md: Extensions) |
| `0038-vulkan-instance-create-info-next` | `DeviceCreationDesc::vkInstanceCreateInfoNext`, a caller's chain appended to `VkInstanceCreateInfo::pNext` after NRI's own structures | NRI creates the instance and chains only its own structures, so `VkLayerSettingsCreateInfoEXT` cannot reach the validation layer and its settings go through process-wide environment variables (CLAUDE.md: Device creation and callbacks) |
