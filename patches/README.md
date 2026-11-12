# patches/

NRI-shaped extensions applied on top of the pinned upstream tag before the native build. Each patch
is a `git diff` of `nri/` against `v$(NriVersion)` with a prose header explaining it, numbered in
application order, and must apply cleanly onto the next upstream tag when the pin moves.

Applied by `tools/build-native.ps1` and by `tools/generate-native.ps1`, because a patch that changes
a header changes the L0 layer and the ABI oracle measures the patched header. Both skip a patch
that is already applied, so the submodule working tree normally sits with every patch applied and
`git -C nri status` shows the patched files as modified. Listed and explained in `AGENTS.md` (Patches).

The set for the pinned tag: six patches, all applying in order and alone and each passing the
scripts' `-C0` reverse check (`AGENTS.md`: Patches). Patches dropped when upstream took the fix are
in git log and `docs/upstream.md`.

| Patch | What | Why |
|---|---|---|
| `0007-cmake-vulkan-loader-link-option` | `NRI_ENABLE_VULKAN_LOADER_LINKING` wraps the Apple `find_package(Vulkan)` + `Vulkan_LIBRARIES` link, default ON | upstream PR 134 linked the loader for an IDE case and the maintainer called it "just not needed"; a shipped `libNRI.dylib` then needs the build machine's loader path to load at all (AGENTS.md: MoltenVK and the paravirtual Mac) |
| `0013-vulkan-exclusive-image-no-family-indices` | `DeviceVK::FillCreateInfo` passes no queue family indices for `VK_SHARING_MODE_EXCLUSIVE` images | upstream passes every active family's index whatever the sharing mode; the Khronos best-practices checks report `BestPractices-vkCreateImage-sharing-mode-exclusive` per image (AGENTS.md: Memory and resources) |
| `0015-vulkan-message-id-name` | the debug-utils callback forwards `pMessageIdName` as `[id] name: text` | upstream forwards the number and the text; current Khronos layers keep the name only in `pMessageIdName` (AGENTS.md: Device creation and callbacks) |
| `0037-vulkan-swapchain-present-modes-info` | with `swapchainMaintenance1`, the Vulkan swap chain is created with `VkSwapchainPresentModesCreateInfoKHR` listing its present mode | upstream chained the present-scaling info but not this, and the Khronos best-practices checks report `BestPractices-vkCreateSwapchainKHR-no-VkSwapchainPresentModesCreateInfoKHR-provided` for every swap chain (AGENTS.md: Extensions) |
| `0044-vulkan-disabled-logic-op-valid-value` | a pipeline without a logic op passes `VK_LOGIC_OP_COPY` for the (ignored) `logicOp` | workaround for lavapipe on Windows, which converts the disabled value and jumps to a garbage address in MSVC builds; not an NRI defect, dropped once the pinned Mesa is fixed (AGENTS.md: Lavapipe) |
| `0050-d3d12-pipeline-library-expected-messages` | `DeviceD3D12` also denies the debug-layer messages [963], [964] and [971]; `PipelineCacheD3D12::Create` reports why it refuses data as an INFO, still `OUT_OF_DATE` | NRI answers a cache blob from another driver or adapter with `OUT_OF_DATE` and looks every pipeline up before creating it, yet each drew a WARNING, and `OUT_OF_DATE` named no cause (AGENTS.md: Memory and resources) |
