# PrismNRI: working notes

Knowledge for working in this repository: what it is, the owner's rules, how to build and test,
the patch set, the decisions and their reasons, and every oddity measured so far with where it is
handled. User-facing usage and caveats are in `docs/guide.md`; keep the two consistent when a fact
changes. The chronological record these notes were distilled from is in git history under
`docs/design.md` and `docs/findings.md` (removed after folding; `git log --follow` on either path).

Facts below are measured against NRI v190 unless stated. Numbers are kept where they identify a
recurrence.

---

## The repository

C# binding over NRI (NVIDIA Render Interface), Vulkan and D3D12. One PrismNRI release pins exactly
one `NRI_VERSION` (`<NriVersion>` in `Directory.Build.props`, exact pin, not a floor): NRI is
integer-versioned, breaks between versions and promises no ABI. .NET 11 / C# 14, xunit v3.

| Path | What |
|---|---|
| `nri/` | submodule at tag `v$(NriVersion)`. Never edit; never stage. Normally sits with every patch applied (`git -C nri status` shows modified files; expected) |
| `patches/` | `git diff`s against the tag, applied in name order. Do not edit without the owner |
| `dotnet/Native/` | L0, generated. Do not edit by hand |
| `dotnet/Interop/` | L1 |
| `dotnet/Core/` | L2 |
| `dotnet/Extensions/` | L3 |
| `descs/` | `PrismNRI.Descs` project + hand-written managed desc forms that need no device (`NriPipelineLayoutDesc`, `NriShaderDesc`, `NriVertexInputDesc`, `NriWindow`, video descs, `NriQueueFamilyD3D12Desc`) |
| `testing/` | `PrismNRI.Testing` (`NriTestDevice`) |
| `native/` | packaging projects for `PrismNRI.Native.Upscalers` and `.Agility` |
| `tests/PrismNRI.Tests/` | the suite; `TestDevice.cs` is the thin xunit wrapper over `NriTestDevice` |
| `tools/` | `build-native.ps1`, `generate-native.ps1`, `compile-test-shaders.ps1`, `ci/`, `cmake/` |
| `runtimes*/`, `_build/`, `_tools/` | build output, git-ignored |
| `docs/guide.md` | usage reference |
| `docs/upstream.md` | report text for each patch (owner's rule: it lives there) |

### Layers

```
L3  dotnet/Extensions/   one class per NRI extension, acquired on first use through NriDevice (TryX / X)
L2  dotnet/Core/         NriDevice, NriQueue, NriBuffer, ... one class per NRI object, one method per Cmd*
L1  dotnet/Interop/      table acquisition, Result -> exceptions, callback thunks, UTF-8, lifetime base
L0  dotnet/Native/       ClangSharp output. Never referenced above L1 except descs and enums
```

- Assemblies: `PrismNRI.Descs` = all of L0 except `NriApi.cs` (the only file with `DllImport`s)
  plus `descs/`; no native dependency. `PrismNRI` = `NriApi.cs` + L1-L3. The split is by file in
  `NativeSplit.props` so the two projects cannot disagree.
- L1 does four things a generator cannot: `InterfaceTable.Acquire<T>` (size check;
  `UNSUPPORTED` -> `NriUnsupportedException`, a capability answer); `Result` -> `NriException` with
  call name and the last error text the message callback reported (`DEVICE_LOST`, `UNSUPPORTED` get
  own types); `[UnmanagedCallersOnly]` thunks for `CallbackInterface` and `AllocationCallbacks`
  recovering the receiver from a `GCHandle` in `userArg` (no managed exception crosses a native
  frame: throwing handler loses one message, throwing allocator returns null = OOM; bridges outlive
  the native device until `nriDestroyDevice` returns); lifetime (`NriObject`).
- L2: names are `Nri` + NRI's name; `Cmd*` methods drop the prefix and keep argument order. Desc
  structs are the generated ones, used directly. A second managed form exists only where NRI holds
  pointer + count (`PipelineLayoutDesc`, `DescriptorSetDesc`, `VertexInputDesc`, `OutputMergerDesc`,
  `GraphicsPipelineDesc`, `ShaderDesc`): arrays, same field names, pinned for the call. Forms that
  name device objects (`NriGraphicsPipelineDesc`) live in L2, the rest in `descs/`. Per-call arrays
  of objects (barriers, attachments, submits) are spans of small records turned into native arrays
  with `stackalloc`; `AllocationTests` hold a whole frame on NONE (allocator and pool reset, set
  allocation and writes, recording, submit with fences, wait, acquire and present) to zero managed
  bytes and zero host allocations. `NriDescriptorSet` is a `readonly struct` (handle + pool, equal
  by handle) with an `AllocateDescriptorSets(..., Span<NriDescriptorSet>)` overload, because an
  engine that resets its pool and reallocates sets every frame must not allocate.
- L2 deliberately does not: hide `GetQueue`'s effect on Vulkan sharing mode, reorder barriers,
  choose memory types, dispose children for a parent, add locks.
- L3 wrappers (`WrapperVK`, `WrapperD3D12`), `GetNativeObject`, `NriHandle`, `device.Core` /
  `device.Helper` and `NriApi` are the escape hatches, in that order of preference.

NRI shape facts that decide the above: C API of flat `nri*` exports plus function-pointer table
structs filled by `nriGetInterface(device, "Name", sizeof(Name), &table)`; no reference counts;
`Create*` free-threaded, `Destroy*` / `Cmd*` externally synchronized; nothing implicit. v190 surface
(excluding D3D11/Metal/WebGPU wrappers): 16 interface structs, 234 function pointers, 19 flat
exports, 246 named structs + 33 nested anonymous + 2 unions, 95 enums (57 `NriEnum` + 38 `NriBits`),
4 bitfields (all in `TopLevelInstance`).

### Native packages

Staged by `tools/build-native.ps1`, each variant in its own CMake tree `_build/<rid>-<variant>/`.

| Package | Staging | win-x64 contents | Built with |
|---|---|---|---|
| `PrismNRI` (Base) | `runtimes/<rid>/native/` | `NRI.dll` (1.4 MB), `amd_ags_x64.dll`, licences; 14 files, 1.8 MB | NGX, FFX, XeSS, NIS all OFF explicitly |
| `PrismNRI.Native.Upscalers` | `runtimes-upscalers/<rid>/native/` | `NRI.dll` (2.7 MB), AGS, licences; 20 files, 3.1 MB | NIS, FFX, XeSS ON, NGX OFF; `libxess.dll` delay-loaded |
| `PrismNRI.Native.Agility` | `runtimes-agility/<rid>/native/AgilitySDK/` | `D3D12Core.dll`, `d3d12SDKLayers.dll`, licence; 3 files, 9.6 MB | staged by either variant on Windows |
| (`-WithDlss`, never packed) | `runtimes-upscalers-dlss/<rid>/native/` | 21 files, 3.2 MB; NGX static, imports `ADVAPI32.dll` too | NGX ON from `FETCHCONTENT_SOURCE_DIR_NGX` |

- Both NRI builds: Vulkan, D3D12, NONE, NRI validation, ImGui, NVAPI (static), AGS, debug names,
  compiled against the Agility headers (`NRI_ENABLE_AGILITY_SDK_SUPPORT=ON`), Agility runtime not
  inside. D3D11, Metal, WebGPU passed OFF explicitly.
- osx-arm64: Base only, `libNRI.dylib` (about 1.07 MB, 8 files, 1.2 MB), Vulkan + NONE.
- Base and Upscalers both contain `NRI.dll` at the same package path; NuGet's resolution between
  two packages' same-named native assets is not to be relied on, so a consumer takes exactly one
  (`ExcludeAssets="native"` on `PrismNRI` with Upscalers). `NriApi` imports library `NRI` for both.
- In-repo, `dotnet/PrismNRI.csproj` copies `runtimes/<rid>/native` beside `ProjectReference`
  consumers and also `runtimes-agility/<rid>/native` when staged (never packed;
  `-p:PrismNRIDevAgility=false` turns it off), so tests run D3D12 on Agility.
- A build from source with no staged library for its RID fails with `PRISMNRI001`, naming the
  directory and the `build-native.ps1` command; before, it built and the consumer failed at device
  creation with `DllNotFoundException`. `-p:PrismNRIRequireNative=false` skips the check. Every
  workflow builds after the payload is staged or restored, so none needs it.
- **Consumption smoke test** (`tools/consume`, not in the solution): `PackageReference`s to
  `PrismNRI` + `PrismNRI.Testing` at `-p:PrismNRIPackageVersion`, from the folder in
  `PRISMNRI_FEED` (default `nupkg/`), restored into `tools/consume/obj/packages` so the global
  cache never stands in; the build prints the source NuGet recorded. Prints the loaded NRI path,
  creates NONE and each available GPU backend's validated device with a buffer and a fence; exit 0
  only when NONE and one GPU backend worked. The release `consume` job runs it on windows, ubuntu
  (`libvulkan1 mesa-vulkan-drivers`) and macos (`molten-vk vulkan-loader`, ci.yml's dyld/ICD env),
  portable build and `-r <rid>`; `release` needs it. Locally (Windows, packs win-x64 only):
  ```powershell
  dotnet pack PrismNRI.slnx -c Release -o <dir>
  $env:PRISMNRI_FEED = "<dir>"
  dotnet build tools/consume -c Release -p:PrismNRIPackageVersion=0.4.0-alpha
  dotnet tools/consume/bin/Release/net11.0/PrismNRI.Consume.dll
  dotnet run --project tools/consume -c Release -r win-x64 -p:PrismNRIPackageVersion=0.4.0-alpha
  ```
  Portable build loads `bin/.../runtimes/win-x64/native/NRI.dll` through the deps.json; `-r`
  loads the flat `NRI.dll`. Delete `tools/consume/obj` after re-packing the same version.
- `NativePayloadTests` hold the staged files to an allowlist and read the PE import tables.
  `NativeExportTests` resolve every `NriApi` `DllImport` with `NativeLibrary.TryGetExport`.
- Licence facts that shaped the split: NGX terms attach to `NRI.dll` itself when NGX is linked
  (static), require downstream terms "at least as protective" and forbid stand-alone
  redistribution; Agility terms require binding users to terms protecting Microsoft; NRI's
  post-build copies `D3D12StateObjectCompiler.*`, which the Agility licence does not allow
  redistributing, so the allowlist leaves it out. Texts in `THIRD-PARTY-NOTICES.md`.

---

## Hard rules

From the owner. Do not relitigate.

- **Patches are in-repo.** NRI changes are `patches/*.patch` with a prose header; report text for
  upstream lives in `docs/upstream.md`. Never file anything upstream (issues, PRs, comments).
- **A patch is NRI-shaped**: a field on an existing desc, a new desc, or a new interface struct in
  NRI's style, comment in NRI's voice; or a bug fix. Never a semantic change to an existing call.
  Every patch must apply cleanly to the next upstream tag.
- **The library knows nothing about any consumer.** No project, test, package or doc here
  references a downstream project. Downstream bindings reference `PrismNRI.Descs` /
  `PrismNRI.Testing`, never the reverse. No probe or test targets an application.
- **No vendor DLLs shipped**: no `nvngx_*`, `libxess.dll`, `amd_fidelityfx_*` in any package or
  commit. DLSS is the `-WithDlss` build switch only.
- **No meaningless tests.** A skip names the missing capability (feature bit, extension, file), so a
  capable device cannot skip by accident. Nothing runs on NONE unless NONE is what it measures
  (NONE validates nothing). Exceptions keyed on a device name or backend must quote a measured
  defect (see Tests and skips).
- **Don't flood CI runners.** Test locally first (Windows directly, Linux via WSL), push when green,
  batch commits: every push to a pull request is a full run, billed per job in whole minutes with
  macOS at about ten times Linux (Actions budget). No release dry runs.
- **Backends are Vulkan and D3D12 (+ NONE).** Passed OFF explicitly so contents never depend on the
  build machine.
  - D3D11: compatibility backend; memory, barriers, queues, register spaces and pipeline cache are
    emulated or no-ops; DXBC as a third bytecode; no RT, mesh shaders, bindless, shared handles,
    DLSS.
  - Metal: requires macOS 26 + `GPUFamilyMetal4` against the 15.0 floor; CI testing needs Apple's
    shader converter (not on the runner) or native Metal copies of every test shader; merged three
    weeks before v190. macOS is Vulkan through MoltenVK.
  - WebGPU: links a prebuilt `wgpu-native` binary this repo would have to ship; NRI calls its
    limitations "similar to D3D11".
- **Linux is verified on lavapipe only, by decision.** Hardware Linux validation (NVIDIA driver on
  metal, a cloud GPU runner, Mesa's dozen in WSL) is out of scope: Linux is not the primary target.
  WSL and Hyper-V expose the GPU only as D3D12/CUDA through dxg, with no Vulkan ICD. The README says
  so; do not claim more.
- `nri/` and `patches/` are not edited as a side effect of other work. Stage by path; never `git add
  nri`.

---

## Build and test

### Windows

```powershell
git submodule update --init
pwsh tools/build-native.ps1                          # Base + Agility, win-x64, Release
pwsh tools/build-native.ps1 -Variant Upscalers       # optional, ~1.8 GB of _deps
pwsh tools/build-native.ps1 -WithDlss E:\path\DLSS   # local only, never packed
dotnet build PrismNRI.slnx -c Release
dotnet test --solution PrismNRI.slnx -c Release --no-build
dotnet test --project tests/PrismNRI.Tests -c Release --no-build
```

`build-native.ps1` flags that matter:

| Flag | Effect |
|---|---|
| `-Rid` | `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-arm64`; must match the host |
| `-Variant Base\|Upscalers` | osx-arm64 is Base only |
| `-WithDlss <dir>` | Upscalers + `NRI_ENABLE_NGX_SDK=ON`, `FETCHCONTENT_SOURCE_DIR_NGX=<dir>`; throws if an `ngx-src` ever appears in `_deps` |
| `-Disable NRI_ENABLE_X` | force an option OFF when an upstream SDK breaks; reconcile `NativePayloadTests` |
| `-Clean` | delete the variant's build tree including downloads |
| `-Generator` | default VS on Windows, Ninja on Linux/macOS if present |

- It checks the submodule tag against `NriVersion`, applies `patches/` (skipping applied ones),
  configures, builds, stages, and prints a table. A matching `_deps` makes configure run with
  `FETCHCONTENT_FULLY_DISCONNECTED=ON`.
- Only options that exist on the target are passed: NRI declares most with
  `cmake_dependent_option`, and an option whose condition is false is silently ignored.
- NRI's generated `Include/NRIAgilitySDK.h` is moved into the build tree after configure.
- Base `_deps`: `agilitysdk;nvtx;vulkan_headers;vma;d3d12ma;amdags;nvapi` (263 MB). Upscalers adds
  `ffx;fsr;xess;shadermake` (1.8 GB). No `ngx` without `-WithDlss`.

### Regenerating L0

```powershell
dotnet tool install --global ClangSharpPInvokeGenerator --version 21.1.8.4   # targets net10.0; needs the .NET 10 runtime
pwsh tools/generate-native.ps1          # applies patches/ first, writes dotnet/Native, LF
git add -A -- dotnet/Native; git diff --cached --quiet -- dotnet/Native   # the CI drift check
```

Regenerate after any patch that touches a header; `NativeAbiTests` then re-measures.

### Test shaders

`pwsh tools/compile-test-shaders.ps1 [-Dxc <path>]` compiles each `tests/PrismNRI.Tests/Shaders/*.hlsl`
to `.spv` and `.dxil` by entry-point name (`vsMain`, `psMain`, `csMain` cs_6_5, `msMain`, `main`,
`rayGen` lib_6_3). RT libraries: one SPIR-V module, one DXIL per export (`-exports`). No
`-fvk-*-shift`; tests use zero `VKBindingOffsets`. dxc from the Vulkan SDK (1.9.0.5180, default
`$env:VULKAN_SDK\Bin\dxc.exe`, then `E:\programs\VulkanSDK\Bin\dxc.exe`); no `dxil.dll`, so DXIL is
unsigned with the bypass hash current runtimes accept. Binaries are committed (`.gitattributes`
marks them binary).

### Linux (WSL2 Ubuntu 24.04)

```bash
SKIP_APT=1 bash tools/ci/verify-linux.sh    # in a Linux-filesystem clone, e.g. ~/src/PrismNRI
```

- Mirrors the `linux-x64` job: fetches the Khronos layer into `_tools/vvl` via
  `tools/ci/fetch-vulkan-layer.sh` (one library + manifest out of the 366 MB SDK tarball, stripped),
  sets `VK_ADD_LAYER_PATH`, builds native (about four minutes with clang), builds, tests (seconds).
- Needs pwsh 7, .NET 11 SDK, cmake >= 3.30, clang, Ninja, `libvulkan1`, `mesa-vulkan-drivers`,
  `vulkan-validationlayers`, X11/Wayland headers (NRI enables those swapchain paths when it finds
  them; installing keeps the CI payload identical to a release one).
- Run from a Linux filesystem, not `/mnt/e`: several GB through 9p is very slow. The WSL clone pulls
  commits only; copy uncommitted edits by hand.
- From the Bash tool, `$VAR`, `$?` and `$(...)` inside an inline `wsl -d Ubuntu -- bash -lc '...'`
  arrive stripped. Write a script file, `cp` it into WSL, `sed -i 's/\r$//'`, run it.
- Keep the distribution's 1.3 loader: patch 0004 is for exactly that pairing.

### macOS (CI `osx-arm64` job)

```bash
brew install ninja molten-vk vulkan-loader vulkan-validationlayers vulkan-tools
export DYLD_FALLBACK_LIBRARY_PATH="$(brew --prefix)/lib:/usr/local/lib:/usr/lib"
export VK_DRIVER_FILES="$(find "$(brew --prefix molten-vk)/" -name MoltenVK_icd.json | head -n1)"
pwsh tools/build-native.ps1 -Rid osx-arm64
dotnet build PrismNRI.slnx -c Release && dotnet test --solution PrismNRI.slnx -c Release --no-build --output Detailed
```

Native build about 25 s; tests about 4 s. `--output Detailed` puts every skip reason in the log.

### Test runner

- xunit.v3 4.0.2 (runtime `Assert.Skip` with a reason; xunit 2 lacks it) on
  Microsoft.Testing.Platform: `global.json` has `"test": { "runner": "Microsoft.Testing.Platform" }`
  (a `dotnet.config` with the same setting did nothing on 11.0.100-rc.1), the test project is an
  `Exe`, no `Microsoft.NET.Test.Sdk`, serialization in `xunit.runner.json`.
- `PRISMNRI_TEST_SOFTWARE=1` (implied by `GITHUB_ACTIONS=true`) prefers WARP / lavapipe;
  `PRISMNRI_TEST_BACKEND=VK` restricts; `PRISMNRI_TRACE=1` prints messages live (the only record
  that survives a native crash).
- Single test, bypassing `dotnet test`: `tests/PrismNRI.Tests/bin/Release/net11.0/PrismNRI.Tests
  -method "PrismNRI.Tests.RayTracingTests.A_ray_query_hits_the_triangle_in_a_built_TLAS"`.

---

## Patches

| Patch | What | Why |
|---|---|---|
| 0001 agility-sdk-device-factory | `DeviceCreationDesc::d3d12AgilitySdkPath`; `D3D12GetInterface(CLSID_D3D12SDKConfiguration)` -> `ID3D12SDKConfiguration1::CreateDeviceFactory(NRI_AGILITY_SDK_VERSION_MAJOR, path)` -> `ApplyToGlobalState()` before any other D3D12 call | a managed host cannot export `D3D12SDKVersion`, so the SDK never loads otherwise |
| 0002 shared-handle-extension | `NRISharedHandle.h`: `CreateSharedTexture`, `CreateSharedFence`, `GetTextureSharedHandle`, `GetFenceSharedHandle`; Vulkan opaque win32/fd, D3D12 NT, NONE dummy, through validation; `EXTERNAL_QUEUE` sentinel for barriers to/from `VK_QUEUE_FAMILY_EXTERNAL`; `CreateSharedFence(SWAPCHAIN_SEMAPHORE)` = exportable binary VkSemaphore (D3D12 `UNSUPPORTED`, silently; validation marks it a swap-chain semaphore) | upstream has no export path; compositors import binary semaphores |
| 0003 validation-has-buffer-sentinel | validation's geometry conversion passes `HAS_BUFFER` through | upstream bug: access violation on the documented sentinel with NRI validation on |
| 0004 vulkan-clamp-minor-version-to-instance | `DeviceVK::Create` caps `m_MinorVersion` at the instance version (14 lines; wrapper path keeps the caller's) | upstream bug: old loader + new driver -> null core 1.4 entry points |
| 0005 helper-memoryheap-alignment-init | `MemoryHeap::alignment` initialised | upstream bug: GCC 13 `-O2 -Werror` `maybe-uninitialized` |
| 0006 vulkan-portability-sampler-lod-bias | `samplerLodBiasMax = 0` with `VK_KHR_portability_subset` and no `samplerMipLodBias` (6 lines beside NRI's `imageView2DOn3DImage` read) | upstream bug: MoltenVK reports 15.999 and forbids any bias |
| 0007 cmake-vulkan-loader-link-option | `NRI_ENABLE_VULKAN_LOADER_LINKING` (default ON) wraps the Apple `find_package(Vulkan)` + link; we pass OFF | shipped dylib otherwise needs the build machine's loader path; NRI PR 134 added it for an IDE case, maintainer: "just not needed" |
| 0008 vulkan-format-table-compressed | `VK_FORMAT_TABLE` extended from 131 to 185 entries: VkFormat 131-184 (BC, ETC2/EAC, ASTC LDR) map to NRI's formats; BC1 RGB (131, 132) stay `UNKNOWN` | upstream gap: `nriConvertVKFormatToNRI` returned `UNKNOWN` for all 52 compressed formats while the reverse table produces them |
| 0009 vulkan-swapchain-out-of-date-not-an-error | `AcquireNextTexture` / `Present` return `OUT_OF_DATE` for exactly `VK_ERROR_OUT_OF_DATE_KHR` before `NRI_RETURN_ON_BAD_VKRESULT` (no report); return values unchanged | upstream reporting defect: a documented, expected result reported as `Message::ERROR` on every resize |
| 0010 streamer-block-compressed-rows | `StreamTextureData` sizes and copies compressed formats in rows of blocks: `ceil(w / blockWidth) * stride` per row, `ceil(h / blockHeight)` rows; other formats (the planar video ones included) unchanged | upstream bug: texel rows, 16x the dynamic-buffer space for 4x4 blocks and a read past the end of the caller's data; the texels still landed right (same oversized pitch on both sides) |
| 0011 streamer-queued-frame-num-comment | `NRIStreamer.h` comment only: `queuedFrameNum` counts the current frame; data streamed in a frame is overwritten `queuedFrameNum` frames later | upstream doc defect: the comment says NRI adds 1, which 6f80a7c (May 2025) stopped doing; the code is the contract |

- Applied by both `build-native.ps1` and `generate-native.ps1` (a header patch changes L0), each
  skipping already-applied patches (reverse applies cleanly).
- 0002 was produced as a diff against the tree with 0001 applied, through a temporary index, so the
  patches stay independent; verified by applying in order onto a clean checkout. Re-cut the same way
  for the binary fence (a scratch clone at the tag with 0001 committed, old 0002 applied, edited,
  `git diff --cached HEAD`); 0003-0011 each also apply alone onto the bare tag.
- Patch 0007 inserts three lines at `CMakeLists.txt:126`; upstream line numbers past that are three
  lower than the patched tree's.
- 0001 stays even though the wrapped-device route also loads Agility (see D3D12 and the Agility
  SDK): the patch keeps `Robustness`, `DeviceLostInfoLevel`, D3D12 RT validation and debug-layer
  filtering on NRI's own creation path, 20 lines against about 150 lines of COM in the binding.
- `docs/upstream.md` holds the report text and filing order. Do not file.

---

## Decisions

- **Pure managed, no C++ of our own.** NRI has no prebuilt releases; the library is built from the
  submodule. The repo's native content is the patch set.
- **L0 in C++ mode, not C mode.** C mode (`NRI_FORCE_C`) erases every enum to an integer typedef:
  enum fields and parameters come out `byte`/`ushort`, `Result` as `sbyte`; fixing it would be ~95
  hand remaps, the silent-layout-corruption class of edit. C++ mode keeps `enum class Format : byte`
  and typed bitfields.
- **No finalizers; a device refuses to die before its children.** GC finalization order is
  arbitrary; a finalizer calling `Destroy*` could free a buffer after its device (heap corruption).
  Each wrapper references its device, the device counts children, `NriDevice.Dispose` throws while
  any remain. A forgotten `Dispose` leaks until exit; shutdown disposal is skipped.
- **Callbacks opt-in; allocator does not forward by default.** NRI's allocator is the default.
  `ForwardAllocatorToThirdParty` routes VMA/D3D12MA too; off because it is chatty and the usual reason
  to supply one is accounting NRI's own use.
- **Agility by path, through a patch.** Calling the SDK configuration from C# would put a D3D12 COM
  sequence in the binding that NRI owns, and AGS on AMD creates the device inside NRI.
- **Packages: Base / Upscalers / Agility, no vendor runtimes, DLSS as a build switch.** Licence
  exposure (NGX static terms, Agility terms), payload size (225.6 MB unsplit vs 1.8 MB Base), users
  swap vendor DLLs.
- **Oracles, not self-description**:

  | Claim | Oracle |
  |---|---|
  | L0 bytes match the header | `NativeAbiTests`: C++ probe compiled against the pinned, patched headers |
  | table layouts match the library | `nriGetInterface`'s own size check on every acquisition |
  | the pin is what runs | `DeviceDesc.nriVersion` vs `Directory.Build.props` (`DeviceTests`) |
  | L1/L2 do what NRI says | NRI validation + backend layers on every test device; any error fails |
  | whole stack, both backends | `TriangleTests`: offscreen, read back, pixel-checked |
  | recording allocates nothing | `AllocationTests` on NONE with `NriCountingAllocator` + GC counters |
  | payload complete | `NativePayloadTests` vs the staging allowlist |
  | every `DllImport` exported | `NativeExportTests` |

- **NONE is for lifetime and allocation tests only**: a real device that validates and allocates
  nothing; wrong for anything that asks a question.
- **Name- and backend-keyed skips are allowed only for measured defects with no capability bit**:
  Apple Paravirtual `HOST_TRANSFER` crash, paravirtual timestamps (both via
  `TestDevice.IsAppleParavirtualVK`), Reflex on VK, H.264 encode on VK, D3D12 micromap `Close`. A
  failing job on every push hides everything else; a physical Mac is not covered by the name skips.
- **macOS deployment target 15.0**: NRI pins none, so the dylib inherited the runner's 26.0;
  MoltenVK needs 12.0; .NET 11 supports 15+; 15.0 is the lowest that can matter.
- **Sampler bias test** asks `min(0.5, samplerLodBiasMax)` and records both.
- **Shared D3D12 textures stay TYPELESS** like every NRI texture; D3D11 import refusal is a recorded
  limitation, not a patch.
- **Software ray tracer answers are asserted like any other** (owner, 2026-10-14, reversing the
  earlier record-and-skip): a wrong hit map fails on every adapter. The intermittent lavapipe
  "miss" was a test reading back after NRI's 5 s fence-wait timeout (CI runners and flakes, item 1);
  tests wait with `GpuWork.Wait`, not `NriFence.Wait`, before reading GPU results.

---

## Oddities and caveats

### Generator and ABI

- **ClangSharp 21.1.8.4, C++ mode**: 373 files, 7,661 lines, 152 errors as generated, all one cause:
  `NriBits` operator overloads (`~ & | &= |=`, five per enum, 38 enums) become static helpers that do
  not compile. Removed. Second rule: `NriBit(n)` = `(1u << n)` does not convert inside byte/ushort
  enums; `U` suffix dropped for those only. Third: `[Flags]` added to every `NriBits` enum, read
  from the headers. All in `tools/generate-native.ps1` and nowhere else. (C mode: 6,352 lines, 8
  errors, types gone.)
- NRI's headers have zero C++ default member initializers (`NriDefault` unused), so generator-dropped
  defaults cannot happen here. C `bool` is `byte` with `[NativeTypeName("bool")]`.
- **ABI oracle** (`NativeAbiTests`, clang++ 22 / AppleClang): 273 structs, 1,840 offsets, 55 fixed
  buffers, 96 enums, 4 bitfields (exact bytes written), 238 function-pointer fields, 19 `DllImport`s,
  zero mismatches on x64 and arm64. Blind to calling convention, `const`, reference vs pointer,
  `bool` vs `uint8_t`. The anonymous union in `BottomLevelGeometryDesc` is checked as outer offsets.
- **`nriGetInterface` checks the table size itself**: mismatch = `INVALID_ARGUMENT` after both sizes
  are reported through the message callback (`Source/Creation/Creation.cpp`). A drifted table fails
  on acquisition, before any call.
- **Regen drift and line endings.** On the Windows runner `core.autocrlf=true`: checkout CRLF,
  ClangSharp writes LF, `git status` showed every file modified while content was identical. Fixes:
  `.gitattributes` (`* text=auto`; `patches/*` `-text` since they apply byte for byte; shader
  binaries `binary`), the generator normalises to LF, and the drift check is `git add -A --
  dotnet/Native` + `git diff --cached --quiet` (goes through the clean filter).

### Device creation and callbacks

- **Null `AbortExecution` kills the process.** NRI's `CheckAndSetDefaultCallbacks` installs a
  handler that calls `DebugBreak()` on every `Message::ERROR`; with no debugger: exit `0x80000003`
  before the failing call returns and before the test sees the message. Showed up as a vanished test
  host with exit 0 through a pipe. `MessageBridge` always supplies its own thunk, breaking only when
  `BreakOnError` and a debugger is attached (`dotnet/Interop/MessageBridge.cs`).
- **`GetQueue` out of range**: `INVALID_ARGUMENT` on VK and D3D12 with no message; NONE returns
  `SUCCESS` even for index 1000. Tests wanting a message must provoke the validation layer.
- **NONE is never validated**: `Creation.cpp` wraps only when `enableNRIValidation && graphicsAPI !=
  NONE`. NONE answers `{size=1}` for every buffer, one static texture desc, `MapBuffer` null, 0
  allocations from `AllocateAndBindMemory` (`ImplNONE.cpp`).
- **Software adapters are enumerated** with `Architecture::SOFTWARE` (WARP via
  `DXGI_ADAPTER_FLAG_SOFTWARE`, lavapipe via `VK_PHYSICAL_DEVICE_TYPE_CPU`); `TestDevice` /
  `NriTestDevice.PreferSoftware` picks them.
- **Queue counts are trimmed to what was created**, so `Video` is `UNSUPPORTED` without video queues
  requested. A device wrapped from a raw D3D12 device with no queue families has no queues; NRI's own
  creation defaults to one graphics queue (`NriD3D12DeviceOptions.QueueFamilies` doc).
- **Adapter uid on Linux**: `deviceLUIDValid` is false, NRI builds `AdapterDesc::uid` from the UUID
  (`ConstructUid`, `SharedExternal.h`). Matching on LUID failed there; tests match as NRI does.
- Rider flags `?.` on a pointer-returning member ("Unable to lift conditional access expression
  type 'void*' to nullable type"); Roslyn compiles it, a false positive. The allocator thunks use
  explicit null checks instead (`dotnet/Interop/AllocatorBridge.cs`).

### Lifetime

- **The refusal must come before the handle is dropped.** An early `NriDevice.Dispose` zeroed the
  handle before checking children, so the exception left the wrapper `IsDisposed` and leaked the
  native device. `NriObject.Dispose` runs `BeforeDestroy` first and gives the handle up only after
  it returns (`dotnet/Interop/NriObject.cs`; `DeviceTests` covers both halves).
- Host allocator live count returns to 0 after device disposal: NONE 2/2, VK 60/60, D3D12 50/50
  allocations/frees, no reallocations.
- Recording a representative stream 300 times on NONE: 0 managed bytes, 0 host allocations; 300
  whole frames (allocator and descriptor pool reset, sets allocated into a reused span plus one
  single, `Update` / `UpdateRange` / `CopyRange`, recording with `SetPipeline` and both `SetRootConstants` overloads, submit with wait and signal fences and the one-buffer `Submit(cmd, fence, value)` (48 bytes per call until it passed its signal as a span over a local),
  `fence.Wait` / `Value`, acquire and present): 0 managed bytes, 0 host allocations, reallocations
  or frees; a 300-frame loop over every extension's per-frame path: 0 managed bytes. `Map` is not
  in the frame: NONE's `MapBuffer` returns null, which `NriBuffer.Map` reports as a failure.

### Memory and resources

- **Host barrier on a buffer is refused**: `CommandBufferVal.hpp:173: ValidateBufferBarrierDesc:
  'barrierDesc.buffers[0].after.access' is not supported by the usage mask of the buffer`
  (`IsAccessMaskSupported`). `HOST_READ`/`HOST_WRITE` belong to `HOST_TRANSFER` textures; for a
  readback buffer the fence wait is the visibility (`TriangleTests`).
- Freeing memory with a resource bound: validation error.
- `TextureDesc` zeros (height, depth, mipNum, layerNum, sampleNum) come back as 1 on both GPU
  backends (`FixTextureDesc`); `BufferDesc` comes back as given.
- 1000-byte buffer requirement: VK 1,008 at 16; D3D12 inbox 65,536 at 65,536; Agility 1,024 at 128.
  Upload row/slice alignment: VK 1/16, D3D12 1/1.
- D3D12 descriptor-set heap offsets for three sets: (0,0), (3,1), (6,2); (0,0) after `Reset`. VK
  (0,0) always.
- Pipeline cache after one compute pipeline: VK 6,872 B, D3D12 3,318 B; rebuilt from bytes, same size.
  Stale or foreign bytes: `CreatePipelineCache` throws `NriException` with `OUT_OF_DATE` and hands
  nothing back to leak. NRI's `CreateImplementation` (`DeviceVK.h`, `DeviceD3D12.h`) destroys the
  half-made object and nulls the handle on any non-`SUCCESS`, the validation layer likewise
  (`DeviceVal.hpp`). Measured with 4,096 random bytes (`PipelineTests`): `OUT_OF_DATE` on NVIDIA VK
  and D3D12, host allocator balanced (VK 30/30, D3D12 18/18), device disposes with 0 children.
  A claim that NRI returns `OUT_OF_DATE` together with a usable cache is false for v190.
- **UNORM rounding**: clear `0.1` -> 25 on NVIDIA Vulkan, 26 elsewhere. `TriangleTests` clears
  `(0.2, 0.4, 0.6)` and allows +-1; VK and D3D12 readbacks differ at edges, so it logs each SHA-256
  instead of comparing.
- **`UploadData`** (`NriQueue.UploadData`): managed records; short data throws `ArgumentException`
  before NRI reads past it; subresources layer-major. Buffer + 2 layers x 2 mips RGBA8 exact on VK,
  D3D12, WARP. **NV12 refused**: VK VUID-VkCopyBufferToImageInfo2-dstImage-07981 (both planes in one
  region), D3D12 debug error 1417 (second plane's barrier).

### D3D12 and the Agility SDK

- **Agility does not load in a managed process.** NRI's CMake writes `Include/NRIAgilitySDK.h`
  declaring `D3D12SDKVersion` / `D3D12SDKPath` exports for the host executable; `dotnet.exe`,
  `testhost.exe` and apphosts export nothing, so the inbox runtime runs. Symptom:
  `DeviceD3D12.hpp:911: ... CheckFeatureSupport(options22) failed, result = 0x80070057!`
  (`OPTIONS22` does not exist in the inbox runtime). Fixed by patch 0001; success line:
  `DeviceD3D12.hpp:508: Using Agility SDK v1.619 from '...\AgilitySDK\'`.
- `ApplyToGlobalState` (not device-from-factory) so `D3D12GetDebugInterface` (debug layer, DRED) and
  AGS's `agsDriverExtensionsDX12_CreateDevice` see the same SDK.
- **Process-wide**: once applied, the inbox runtime is unreachable in that process (`AgilityTests`).
- The adapter-support probe in `nriEnumerateAdapters` stays on the inbox runtime on purpose (asks
  only "D3D12 at all").
- **Wrapped route works too**: host creates the device from its own `ID3D12DeviceFactory` and
  passes it to `nriCreateDeviceFromD3D12Device`; NRI reports `Using ID3D12Device15`, no OPTIONS22
  warning (`AgilityFactoryRouteTests`, four vtable calls). NRI supports it on purpose: root
  signatures of such devices go through `ID3D12DeviceConfiguration` (`PipelineLayoutD3D12.hpp`,
  upstream PR #177).
- Developer Mode: the dev machine has it on and cannot show it is unneeded; Microsoft documents the
  factory route as not needing it; the Windows CI runner (Developer Mode off) is the measurement.
- `NriDeviceOptions.D3D12AgilitySdkPath`: null = `AgilitySDK` beside the assembly if present, `""`
  = inbox.
- **Reflex on Vulkan poisons later D3D12 device creation** in the process: see Extensions.

### Vulkan loader and validation layers

- **Old loader, new driver: patch 0004.** Ubuntu 24.04's stock loader 1.3.275 with Mesa 25 lavapipe
  reporting 1.4.318. NRI took `m_MinorVersion = 4` from the physical device; Mesa returns null for
  core 1.4 entry points when the instance is 1.3. Two failures:
  `DescriptorTests.Several_sets_are_allocated_and_written_...` SIGSEGV at address 0 in
  `CmdSetRootConstants` (it branches on the version, not the pointer; `vkCmdPushConstants2` null),
  and `TextureTests...(VK, HOST_TRANSFER)` rejected because NRI does not enable
  `VK_EXT_host_image_copy` on a "1.4" device while the layer evaluates the 1.3 instance. Both vanish
  with the SDK's 1.4 loader on `LD_LIBRARY_PATH` or with the patch. `CreateInstance` asks
  `min(vkEnumerateInstanceVersion(), 1.4)`; the patch clamps to that. CI keeps the stock loader so
  the patch stays exercised.
- **Layer older than NRI's headers.** v190 pins Vulkan-Headers 1.4.365 and chains every feature
  struct the driver offers into `vkCreateDevice`. Ubuntu's `vulkan-validationlayers` 1.3.275 reports
  `VkPhysicalDeviceVulkan14Features`, maintenance7/8 and `VK_EXT_zero_initialize_device_memory`
  (sType 1000620000, lavapipe advertises it) as "unknown VkStructureType" ERRORs; NRI forwards them,
  tests fail. LunarG's noble apt stops at 1.4.313 (still lacks sType 1000620000). The only current
  Linux binary is in the SDK tarball: `tools/ci/fetch-vulkan-layer.sh`, `VULKAN_SDK_VERSION:
  "1.4.363.0"` in `ci.yml`, cached per version. With 1.4.363 every unknown-struct message is gone.
- Homebrew's macOS layer 1.4.357 is also older than 1.4.365 but produced none: every struct NRI
  chains for MoltenVK's 18 extensions is known to it.
- **Vortice's `GetApi` before `vkInitialize()`**: `vkGetDeviceProcAddr` is null until
  `vkInitialize()` runs; only one wrapper test called it, and on Windows that test always ran first.
  On Linux the other ran first: null call from JIT code (gdb frame 0 at address 0). The loader is a
  process-wide lazy both tests touch (`WrapperExtensionTests.cs`).

### Lavapipe

- Mesa 25.2.8 "llvmpipe (LLVM 20.1.2, 256 bits)", Vulkan 1.4.318, on the stock 1.3 loader -> NRI runs
  it as a 1.3 device (patch 0004).
- **No external semaphores**: has `VK_KHR_external_memory_fd`, not `VK_KHR_external_semaphore_fd`;
  `SharedHandleInterface` is `UNSUPPORTED`, `TrySharedHandles` null; `SharedHandleTests` skip naming
  both extensions (`SharedHandleTests.cs`).
- **Slow first submit**: shaders are JIT-compiled when the command buffer executes; with a cold Mesa
  shader cache a ray tracing submit took 0.6 s on a Ryzen 9700X and passed NRI's 5 s fence timeout
  under load. That was the "ray query miss" (CI runners and flakes, item 1), not a traversal bug.
- `GALLIUM_OVERRIDE_CPU_CAPS=avx` or `sse4.1` makes lavapipe die with an illegal instruction before
  any test runs; not usable for narrowing.

### WARP

- D3D12 software adapter on the Windows runner; no Vulkan ICD there, so Windows CI is D3D12 + NONE.
- RT works: one-triangle BLAS 65,536 B; build scratch 120 B, **update scratch 0 B**. RT pipeline
  identifier 32 B, SBT alignment 64, as on hardware.
- `UploadData`, import wrappers, RT pipelines all pass on WARP.

### MoltenVK and the paravirtual Mac

Runner: "Apple M2 Pro (Virtual)" (`VirtualMac2,1`), macOS 26.x, AppleClang 21, MoltenVK 1.4.2 behind
Homebrew loader 1.4.357; device "Apple Paravirtual device", Vulkan 1.4.357, NRI enables 18 device
extensions.

- **`NRI_ENABLE_METAL_SUPPORT=OFF` explicitly**: NRI turns Metal on whenever Xcode 26 + Metal
  toolchain are present, which the runner has. NGX not passed (`WIN32 OR NOT IS_ARM64` false).
- **NRI links the Vulkan loader on Apple** (`find_package(Vulkan REQUIRED)` under `APPLE`, links
  `Vulkan_LIBRARIES`): the dylib got a load-time dependency on
  `/opt/homebrew/opt/vulkan-loader/lib/libvulkan.1.dylib` (build machine's absolute install name)
  while still `dlopen`ing at device creation; without a loader the library would not load at all,
  NONE included. Patch 0007 + OFF in `build-native.ps1`; the script records the `otool` link list
  and refuses anything outside the system.
- **Minimum macOS**: no deployment target = stamped `minos 26.0, sdk 26.5`. Script passes
  `CMAKE_OSX_DEPLOYMENT_TARGET=15.0`.
- **Homebrew paths**: dyld's default fallback is `$HOME/lib:/usr/local/lib:/usr/lib`; `/opt/homebrew`
  is not on it, and NRI and Vortice `dlopen` by leaf name. molten-vk keeps its ICD in the keg's
  `etc/vulkan/icd.d` ("Found no drivers" with the `share` path). The loader finds the layer in
  `/opt/homebrew/share/vulkan/explicit_layer.d`; adding it via `VK_ADD_LAYER_PATH` only produced a
  "duplicate layer" warning. NRI's `DeviceCreationDesc` has no loader path; only the wrapper route's
  `DeviceCreationVKDesc::libraryPath` (`NriVulkanDeviceOptions.LibraryPath`).
- **Raw Vulkan devices must opt into portability**: the loader hides MoltenVK from an instance
  without `VK_KHR_portability_enumeration` + `VK_INSTANCE_CREATE_ENUMERATE_PORTABILITY_BIT_KHR`; a
  device advertising `VK_KHR_portability_subset` must enable it
  (VUID-VkDeviceCreateInfo-pProperties-04451). NRI does both under `__APPLE__`;
  `WrapperExtensionTests` does both when offered. The raw device there is Vulkan 1.3.
- **Sampler LOD bias, patch 0006**: VUID-VkSamplerCreateInfo-samplerMipLodBias-04467 "mipLodBias
  (portability error) is 0.500000, but samplerMipLodBias not supported". MoltenVK:
  `samplerMipLodBias = false`, `maxSamplerLodBias = 15.999`. After the patch the device reports 0;
  `DescriptorTests.Sampler_is_created_from_a_filled_desc` uses `min(0.5, samplerLodBiasMax)`.
- **`HOST_TRANSFER` image crash**: SIGSEGV (exit 139) in
  `TextureTests.Committed_2D_texture_is_created_with_each_usage(VK, HOST_TRANSFER)`. lldb:
  `EXC_BAD_ACCESS (address=0xc)` in `AppleParavirtGPUMetalIOGPUFamily` <-
  `MVKImagePlane::updateMTLTextureContent` <- `MVKImageMemoryBinding::flushToDevice` <-
  `bindDeviceMemory` <- `vkBindImageMemory` <- NRI `TextureVK::AllocateAndBindMemory`. No Vulkan
  error first; device advertises `hostImageCopy = true`, NRI sets the usage bit and
  `FormatSupportBits::HOST_COPY`. Skipped by `TestDevice.IsAppleParavirtualVK`.
- **Timestamps written at completion**: `QueryTests.Two_timestamps_around_GPU_work_are_ordered(VK)`
  read 0 and 0. MoltenVK reports `timestampComputeAndGraphics = true`, `timestampValidBits = 64`,
  `timestampPeriod = 1`. Copying again from a later submission read 140273875750 twice (equal,
  non-zero): host-written at completion, MoltenVK's path without GPU counter sampling. Test records
  both and skips on that device only; the imported-pool test in `WrapperExtensionTests.Imports.cs`
  skips likewise after its copies pass.
- No fd external handles (has core external memory/semaphores and `VK_EXT_external_memory_metal`):
  SharedHandle `UNSUPPORTED`. No RT, mesh shaders, video, descriptor heaps, shading-rate attachments.
- Triangle renders with validation on (`TriangleTests`, 115 ms).
- Metal backend facts if it is ever reconsidered: `Source/Metal` 40 files, 11,066 lines, merged via
  NRI PR #271 into v190; needs Xcode 26 / AppleClang 17+, the Metal toolchain, metal-cpp (macOS 26+),
  `-std=metal4.0`; runtime floor macOS 26 and `GPUFamilyMetal4` (adapters without it are not
  enumerated). No LowLatency, Video, SharedHandle; micromap functions are stubs;
  `CmdSetShadingRate` `UNSUPPORTED`; no pipeline-statistics queries; `atomicsI64` false; colour-only
  resolve; `enableGraphicsAPIValidation` ignored (use `MTL_DEBUG_LAYER=1`). Without the Metal Shader
  Converter, DXIL is rejected and RT pipelines and geometry/tessellation do not exist. Whether the
  paravirtual GPU exposes `GPUFamilyMetal4` is unmeasured.
- D3D11 backend facts, same purpose: targets feature set 11.1; Core entries all assigned but queue
  annotations / `ResetQueries` empty, `GetCalibratedTimestamps` 0/0, `GetBufferDeviceAddress` 0,
  fake pipeline cache; `Memory` records location only, `Bind*Memory` makes a committed resource per
  object (no aliasing); barriers no-op except NVAPI/AGS UAV overlap while `enhancedBarriers` is
  advertised; every queue is the immediate context behind a device-wide lock; register space not
  read; root constant offset not read; `variableDescriptorNum` `UNSUPPORTED`; DXBC SM 5.1; Upscaler
  NIS only; no DescriptorHeap, RayTracing, MeshShader, Video, SharedHandle.

### Ray tracing

- **`HAS_BUFFER` crash in validation (patch 0003)**: `NRIRayTracing.h` defines `HAS_BUFFER` as
  `(Buffer*)1`; backends test for it, validation's `ConvertBottomLevelGeometries` passed it through
  `NRI_GET_IMPL` -> method call on address 1 -> access violation with no message, first
  `CreateCommittedAccelerationStructure` with the sentinel. `NriBuffer.HasBuffer` is the binding's
  spelling.
- `RayTracingTests`: one-triangle BLAS, one-instance TLAS, compute shader with an inline ray query per
  thread, 16x16 rays from z = 0 to +z; centre hits, corners miss, 32 hits of 256 on hardware.
  `cs_6_5` on both; dxc warns `vk::binding` ignored for DXIL (harmless). The shader loops on
  `Proceed()`.
- Geometry and build descs are marshalled on the stack per call; tens of thousands of geometries
  would want another shape.
- **D3D12 RT library per export**: NRI adds each `ShaderDesc` as a DXIL library exporting
  everything. One shared `lib_6_3` library named by three descs faults in
  `CreateRayTracingPipeline` (SEH under the debug layer). `compile-test-shaders.ps1` emits one DXIL
  per export; Vulkan takes one SPIR-V module.
- Measured (NVIDIA VK / D3D12 / WARP): identifier 32 B, SBT alignment 64; `DispatchRays` and
  `DispatchRaysIndirect` give the ray query's map. Unbound BLAS = `GetMemoryDesc2` prediction (VK
  3,072 B/256, D3D12 2,944 B/128, WARP 65,536 B). Update scratch = build scratch on NVIDIA (1,664 B).
  Compacted VK 2,944 of 3,072, D3D12 2,944 of 2,944. Placed+updated, CLONE, COMPACT trace the same.
  BLAS imported by native object reports the same GPU handle, native object, sizes; traces the same.
- **Micromaps, VK**: transparent micro-triangle hides the non-opaque triangle, opaque shows it.
  Index-less OMM (NONE, address 0) draws VUID-...-pInfos-10904 from VVL 1.4.341, likely a false
  positive; with an index buffer it is clean.
- **Micromaps, D3D12**: NRI's prebuild query passes no input buffer or per-OMM descs: debug error
  1162, SEH via break-on-error. So that test runs D3D12 without the debug layer (NRI validation on).
  Then the BLAS build linking the micromap fails `Close` with `E_INVALIDARG`: skipped on D3D12 naming
  that.

### Upscalers and vendor runtimes

- `IsUpscalerSupported` answers what is compiled in, not installed. Without runtimes, raw L0 calls:
  D3D12 FSR `FAILURE` with no message; XeSS SEH from the delay-load stub (`SEHException`, E_FAIL,
  0xC06D007E); VK FSR `FAILURE`. FSR loads `amd_fidelityfx_loader_dx12.dll` /
  `amd_fidelityfx_vk.dll` by name and returns `FAILURE` on null (`UpscalerInterface.hpp`).
- **XeSS hang**: NRI takes the process-wide `g_xess.lock` before `xessD3D12CreateContext`; the SEH
  unwind does not release it (C++ scope guards are not run for SEH under `/EHsc`); a second XeSS
  `CreateUpscaler` from another thread did not return in 10 s. `UpscalerImpl` likely leaks too
  (unmeasured). Hence `NriUpscalerExtension.CreateUpscaler` checks `GetRuntimeFiles` /
  `IsRuntimeInstalled` and throws `NriUnsupportedException` naming the file.
- **Host directory**: NRI and the delay-load helper load by bare name; Windows searches the
  executable's directory. Under `dotnet app.dll` FSR on D3D12 returned `FAILURE` and XeSS killed the
  process with unhandled 0xC06D007E. The guard preloads each file by full path from
  `AppContext.BaseDirectory`, so NRI's bare-name load finds the loaded module. After that FSR (D3D12,
  VK), XeSS, DLSR (D3D12), DLRR (VK) create under `dotnet`.
- `libxess.dll` was a load-time import of `NRI.dll` (NRI would not load without it). Now
  `/DELAYLOAD:libxess.dll` + `delayimp.lib` on the NRI target only, via
  `tools/cmake/xess-delayload.cmake`, passed as `CMAKE_PROJECT_NRI_INCLUDE`, deferring itself with
  `cmake_language(DEFER)`. No patch needed. Packaged `NRI.dll`s import only `d3d12`, `dxgi`,
  `KERNEL32`, `USER32`, `MSVCP140`, `VCRUNTIME140[_1]`, UCRT. VC++ runtime is required, not shipped.
- Positive controls: FSR D3D12 (provider 3.1.5), VK (3.1.4), XeSS D3D12 create on NVIDIA. `-WithDlss`
  reports NIS, FSR, XESS, DLSR, DLRR on D3D12 (NIS, FSR, DLSR, DLRR on VK); raw DLSR without
  `nvngx_dlss*.dll` is a clean `FAILURE`.
- NIS test skips under Base (not compiled in).

### Shared handles

- Vulkan backend (patch 0002) enables `VK_KHR_external_memory_win32`/`_fd` and
  `VK_KHR_external_semaphore_win32`/`_fd` itself (55 device extensions on NVIDIA, up from 53) and
  allocates the shared texture's memory directly (not VMA) to chain the export info.
- NVIDIA VK: second `vkGetMemoryWin32HandleKHR` on the same memory returned the same handle value.
  Tests assert nothing about distinctness.
- Ordinary texture asked for a handle: `INVALID_ARGUMENT` through validation.
- Import proofs (`SharedHandleImportTests`, Windows): VK texture into a second VK device as a
  dedicated allocation, 16,384 bytes match (same SHA-256); timeline semaphore both ways; D3D12 fence
  both ways; D3D12 texture opens on a second D3D12 device.
- **D3D11 refuses D3D12 shared textures**: `OpenSharedResource1` -> `E_INVALIDARG`, isolated to the
  format: NRI creates every texture `TYPELESS` (`DeviceD3D12::GetResourceDesc`), D3D11 opens typed
  only. The test asserts the refusal; if D3D11 ever opens it, the header comment in
  `NRISharedHandle.h` needs revisiting.
- **Binary shared fence** (`CreateSharedBinaryFence`, patch 0002): VK binary semaphore with
  `VkExportSemaphoreCreateInfo`; NRI's `QueueSubmit` passes the value, which a binary semaphore
  ignores, as for swap-chain semaphores. Import proof (Windows, NVIDIA): imported into a binary
  VkSemaphore on a second device; NRI queue signals -> second queue waits, second queue signals ->
  NRI queue waits; second device's Khronos layer and NRI's messages clean. D3D12: `UNSUPPORTED`,
  no message. Upstream 0002 refused the sentinel in validation ("a shared fence can't be a swap
  chain semaphore"); the extended patch passes it through and marks the `FenceVal` a swap-chain
  semaphore.
- Exclusive VK images need a release to `VK_QUEUE_FAMILY_EXTERNAL`; `NriQueue.External` (patch
  0002's `EXTERNAL_QUEUE`) expresses it. NVIDIA tolerated the omission before.

### Extensions

- Availability: NONE supports every extension except the two wrappers; VK everything except
  `WrapperD3D12`; D3D12 everything except `WrapperVK` (feature bits permitting).
- `Window` in `NRISwapChain.h` is five members side by side, not a union (`descs/NriWindow.cs`).
- **OUT_OF_DATE (patch 0009, binding `AcquireNextTexture` / `QueuePresent` -> `NriSwapChainStatus.OutOfDate`; an enum so a later suboptimal value needs no API break; VK_SUBOPTIMAL_KHR is SUCCESS in NRI and not surfaced, pending owner decision).**
  Upstream reports `Message::ERROR` through `NRI_RETURN_ON_BAD_VKRESULT` for it. The validation
  layer passes both results through; D3D12 never returns it from these calls. Measured on NVIDIA
  (RTX 4070 SUPER, driver's VK 1.4), `SwapChainResizeTests`: an NRI-created device has
  `swapchain_maintenance1`, `features.resizableSwapChain = 1`, NRI chains present scaling, and 8
  resizes (including 0x0, and minimise/restore with a visible window in a trial) never gave
  `OUT_OF_DATE`; a raw VkDevice with only `VK_KHR_swapchain`, wrapped (`resizableSwapChain = 0`),
  got it from present 6 times over the same 8 resizes, never from acquire, no ERROR message, the
  raw instance's Khronos layer clean.
- **The resize test runs in a child process**: the test exe started again with
  `PRISMNRI_TEST_CHILD=swapchain-resize` and `VK_LOADER_LAYERS_DISABLE=~implicit~`; a
  `[ModuleInitializer]` runs the frames and exits before xunit starts. Reasons: RTSS's implicit layer
  kills a Vulkan-presenting process here, and Vulkan presents poisoned later D3D12 device creation
  in the same process (Reflex, below).
- **Wrapped VK device + swap chain needs `VK_KHR_get_surface_capabilities2`** on the instance:
  `SwapChainVK::Create` calls `vkGetPhysicalDeviceSurfaceCapabilities2KHR` unconditionally; without
  it, an access violation in `CreateSwapChain` (measured). Not patched (caller error, NRI's own
  instance always enables it).
- `StreamerDesc::constantBufferMemoryLocation` is marked optional but validation rejects its default.
- **Streamer ring: `queuedFrameNum` regions, no +1 (patch 0011 fixes the comment).** `Grow` sizes the
  dynamic buffer `perFrame * queuedFrameNum`, `EndFrame` cycles modulo `queuedFrameNum`, a grown-out
  buffer is destroyed `queuedFrameNum` frames later. The header's "adds 1 under the hood" came with
  3f38b1a (2025-05-06); 6f80a7c (2025-05-30, "fixed wrong ring-buffer logic") dropped the `++` and
  kept the comment. Fixed the comment, not the code: the code has been the behaviour since, and a +1
  would grow every caller's ring. `StreamerExtensionTests`: with 2, frame 2 reuses frame 0's offset
  (VK, D3D12). Raised by a consumer that sized from the header.
- **Streamer BC staging (patch 0010).** v190's `StreamTextureData` used `w * stride` per row and `h`
  rows (texels; 1c70f5b, v172, replaced the caller's `dataRowPitch` with that). 64x64 BC1 with 7
  mips, measured: mips 0-3 staged in 16 times their block footprint on VK and D3D12 (mip 0 32,768 B
  instead of 2,048); the one-block mips 4x4, 2x2, 1x1 in 128, 32, 16 B on VK (footprint 16 after
  the 16-byte slice alignment) and 128 B each on D3D12 (footprint 8). By the code, the copy reads
  up to byte 8,576 of the 2,048-byte mip 0. Readback was byte-exact before the patch on NVIDIA VK
  and D3D12: the upload is recorded with the same oversized pitch, so only the footprint shows it. Gated on `isCompressed`: NV12/P010/P016 also have 2x2
  `FormatProps` blocks, and their texel-row staging is left as upstream has it (unmeasured through
  the Streamer). Batches not recorded by `EndStreamerFrame` are discarded (`EndFrame` clears them);
  NRI's generation check on a stale batch is an `NRI_CHECK`, an `assert`, gone in release builds;
  only NRI validation refuses it.
- SwapChain / Streamer tests on NONE only prove the marshalling: NONE has no validation, nothing
  says the calls are legal.
- **Video**: H.265 Annex-B writing returns `UNSUPPORTED` for short/long-term reference sets, VUI,
  scaling lists, tiles. `GetVideoAV1EncodeDecodeInfo` returns pointers into the result struct.
  H.264 decode caps for 64x64 NV12: D3D12 min = max = 64x64, no references; VK 48x16 to 4096x4096,
  16 references.
- **H.264 encode**: D3D12 minimum 256x256; a 64x64 capability query draws debug error 1312 (SEH). One
  IDR at QP 26 = 277 B, `00 00 00 01 65`. VK minimum 160x64, resolve on GRAPHICS; the feedback query
  never becomes available and `vkCmdCopyQueryPoolResults` with WAIT never completes. Skipped on VK
  naming that; it would hang the run (`VideoExtensionTests.cs`).
- **Reflex**: D3D12 report all zero after 72 frames, filled and ordered after 200. VK: RivaTuner's
  implicit `VK_LAYER_RTSS` killed the process at the first `vkQueuePresentKHR`; with implicit layers
  disabled VK passed, but later `D3D12CreateDevice` calls in the process returned `0x887A0007`
  (`DXGI_ERROR_DEVICE_RESET`), with or without `ALLOW_LOW_LATENCY` and `VSYNC`, failing unrelated
  D3D12 tests. VK case skipped naming that (`LowLatencyExtensionTests.cs`).

### Wrappers

- All twelve import wrappers carry a dispatch, copies, a texture upload + readback, and timestamps,
  byte-exact (`WrapperExtensionTests.Imports.cs`).
- **Wrapped command buffer counts as recording from creation** under NRI validation: `Begin` refused
  ("already in the recording state"). Begin natively (or pass the D3D12 list open),
  `SetDescriptorPool`, record, `End`.
- **`CreateBufferVK` records usage NONE**: a buffer barrier with storage access is refused; a global
  barrier passes.
- `CreateMemoryVK` with no mapping: NRI maps it itself.
- `CreateAccelerationStructureVK` / `D3D12`: caller names the type, needs `RayTracing`.
- Raw VK devices on macOS: portability opt-in, see MoltenVK.
- **Never Vortice's `Vulkan.GetApi` in tests**: 3.3.0 caches tables in static dictionaries keyed by
  handle value (`Vulkan.cs` `s_instanceTables` / `s_deviceTables`), dropped only by
  `vkDestroyInstance` / `vkDestroyDevice` through that table. NRI destroys its own objects, so the
  entry outlives the device and a later device at the same address gets dead pointers.
  `tests/PrismNRI.Tests/VulkanApi.cs` builds a fresh table per object (`new VkDeviceApi(...)`).

### Flat exports and formats

- Twelve flat exports on static `Nri`. `nriGetFormatProps(MAX_NUM)` returned another format's entry:
  NRI indexes unchecked, so `Nri` throws `ArgumentOutOfRangeException` for `MAX_NUM` and above.
- Formats (108): on Linux/macOS payloads every DXGI conversion is 0 and `ConvertDXGIFormatToNRI` is
  `UNKNOWN` (`Creation.cpp` guards the table with `NRI_ENABLE_D3D11_SUPPORT ||
  NRI_ENABLE_D3D12_SUPPORT`); `FlatExportTests` measures that there. Windows: DXGI round trips hold
  except DXGI has none for the 38 ETC2/ASTC formats; DXGI 18 -> `RGB32_SINT`. NRI -> VK -> NRI is the
  identity for all 108 formats (patch 0008; upstream v190 gave `UNKNOWN` for all 52 block-compressed
  ones, table ending at 130); no format round-trips to a different one (sRGB/UNORM and the D24
  variants each map back to themselves; VK 125 `X8_D24` -> `D24_UNORM_S8_UINT` is one-way). BC1 RGB
  (131, 132) stay `UNKNOWN`: NRI has only BC1 RGBA.

### Tests and skips

- Skip strings name the capability: feature bit, extension, file. The exceptions quote a measured
  defect: `TestDevice.IsAppleParavirtualVK` (adapter name "Apple Paravirtual device":
  `HOST_TRANSFER` crash, timestamps), backend-keyed Reflex VK and H.264 encode VK, D3D12 micromap
  `Close`.
- **No skip turns a wrong result into a pass on a software adapter** (lavapipe, WARP), or on any
  adapter: a skip names a missing capability only. A wrong answer fails, with enough in the message
  (the hit map for ray queries) to diagnose it. The two macOS device-name skips are the recorded
  exception; the backend-keyed ones above skip a case that cannot run to completion (a hang, a
  failed `Close`, a poisoned test process), not a wrong answer.
- Timestamp frequency 1 GHz on both GPU backends; result 8 bytes.
- `PRISMNRI_TRACE=1` when a test host dies: the message stream is the only record.
- Expected skip shape per platform (counts move as tests are added): Windows hardware skips almost
  nothing (NIS under Base); Windows CI (WARP, no Vulkan ICD) skips every VK case; Linux skips D3D12,
  Agility, NIS, shared handles, import proofs; macOS additionally skips SharedHandle, RT, mesh,
  video, descriptor heaps, shading rate, PE imports, the Windows swap-chain window, payload contracts
  for Upscalers/Agility, and the two paravirtual defects.

### CI runners and flakes

Both were on the Linux leg (lavapipe on GitHub hosts of varying CPU); the Windows and macOS legs
have no intermittent failures. Both are identified and fixed in the tests; neither was a driver
bug.

1. **"lavapipe ray query miss": a fence wait that gave up after 5 s. Fixed in the tests and the binding.**
   `RayTracingTests.A_ray_query_hits_the_triangle_in_a_built_TLAS(VK)` on the Linux runner sometimes
   reported a miss; the last one (run 38551840987, EPYC 7763, probe step, 1 of 5) was a total miss,
   "(8,8) centre ... expected 1, got 0; 0 hits of 256". Cause: NRI's `Wait(fence, value)` is
   `vkWaitSemaphores` with `NRI_TIMEOUT_FENCE` = 5000 ms (`Source/Shared/SharedExternal.h`,
   `Source/VK/FenceVK.hpp:52-54`); on timeout it reports "WaitSemaphores() timed out!" as an ERROR
   message and returns (the API is `void`), and `NriFence.Wait` returns normally. The test then read
   a readback buffer the queue had not written yet: all zeros, an all-miss map. The test asserted
   the map before `Assert.DoesNotContain(messages, IsError)`, so the failure named the map, never the
   timeout. lavapipe JIT-compiles shader variants when it executes the command buffer (llvmpipe
   compiles at first launch; the BVH build alone runs leaf, morton, radix-sort, LBVH and encode
   shaders), so the first submit of a process with a cold Mesa shader cache is slow.
   Measured in WSL (Ryzen 9700X, 16 threads): submit+wait 8-15 ms with a warm cache, 570-600 ms with
   `MESA_SHADER_CACHE_DISABLE=true`, 1.0-1.3 s cold on 4 CPUs shared with 8 busy loops. Cold on one
   CPU shared with 14 busy loops: 5112 ms, "WaitSemaphores() timed out!", and exactly the CI failure,
   "(8,8) centre, inside the triangle: expected 1, got 0; 0 hits of 256", 3 runs of 3. A fresh GitHub
   runner has a cold cache and 2 vCPUs (`nproc` in the CI log), and `dotnet test` runs classes in
   parallel. First inferred from the identical symptom, then measured on the runner: with the fix in
   place the cold-cache probe step recorded "GPU work took 6098 ms to reach fence" on the
   placed/updated BLAS test. Why "never locally": the WSL cache was warm after the first run.
   Fix: `GpuWork.Wait(fence, value)` polls `NriFence.Value` until it is reached (fails the test after
   `GpuWork.GpuTimeout`, 2 min) and records any wait of 250 ms or more; every test that reads GPU
   results waits through it (`GpuWork.Run` too). With the fix the same one-CPU setup passes 3 of 3
   (13-15 s per run), and cold on 4 CPUs with 10 busy loops both ray tracing classes pass 40 of 40;
   the full suite with sync validation passes. `DeviceTests.Fence_*` keeps `NriFence.Wait` (it
   asserts `Value` after) and so does `AllocationTests` (it runs on NONE, whose fence never advances).
   Binding fix (owner, 2026-10-14): `NriFence.Wait` reads `Value` after NRI's Wait and throws
   `TimeoutException` when it is short (`dotnet/Core/NriFence.cs`; not on NONE, whose fence never
   advances and whose Wait is a no-op). `DeviceTests.Fence_wait_throws_when_NRI_gives_up_before_the_value_is_reached`
   waits on a never-signalled fence per GPU backend (about 5 s each; D3D12 times out the same way,
   `WaitForSingleObjectEx(m_Event, NRI_TIMEOUT_FENCE)` in `FenceD3D12.hpp`).
   Ruled out on the way, with evidence: barriers and synchronization. The NRI barriers map to
   `ACCELERATION_STRUCTURE_BUILD` / `ACCELERATION_STRUCTURE_WRITE` -> `ACCELERATION_STRUCTURE_READ`
   at `BUILD | COMPUTE_SHADER | RAY_TRACING_SHADER` (`ConversionVK.h:418-446`,
   `CommandBufferVK.hpp:386-390, 2534-2550`); NRI adds no internal barrier to builds; the BLAS and
   TLAS have separate scratch buffers; instances and vertices are host-written before submit.
   Khronos synchronization validation (`VK_KHRONOS_VALIDATION_VALIDATE_SYNC=1` plus
   `VK_KHRONOS_VALIDATION_SYNCVAL_SHADER_ACCESSES_HEURISTIC=1`; without the heuristic syncval does
   not see shader descriptor accesses and misses even a deleted dispatch->copy barrier): 0 hazards in
   the whole suite (239 tests). Positive controls: deleting the TLAS->dispatch barrier gives
   SYNC-HAZARD READ_AFTER_WRITE at `vkCmdDispatch`; deleting the dispatch->copy barrier gives one at
   `vkCmdCopyBuffer2`; deleting the BLAS->TLAS barrier gives nothing (the TLAS build reads the BLAS
   through a device address in the instance buffer, which syncval cannot follow; that barrier is
   checked by reading the code). lavapipe turns every `vkCmdPipelineBarrier2` into a full flush and
   fence wait (`lvp_execute.c` `handle_pipeline_barrier` -> `finish_fence`), so a barrier mistake
   could not have caused a lavapipe-only failure anyway. Stress without the fix (100 runs per row,
   each class, `MALLOC_PERTURB_=165`): `LP_NUM_THREADS` 64, 4, 0, and 64 with sync validation: 0
   failures of 800. CI now runs the suite with sync validation on and a "Ray tracing, cold shader
   cache" step (`MESA_SHADER_CACHE_DISABLE=true`, `-showLiveOutput`) that prints the wait times.
   Earlier misses are recorded in the repository only as "no hit at the centre", without their
   maps; a timeout explains an all-zero map, and a partial map, if there was one, is not explained
   by it.
2. **Linux test host SIGSEGV: a stale Vortice function table. Fixed in the tests.** Runs
   38546336866 (EPYC 9V74) and 38551840987 (EPYC 7763) died with exit 139 after exactly 106 of 239
   tests (58 passed, 48 skipped). In the fixed order of a detailed green run the 107th test is
   `WrapperExtensionTests.WrapperVK_hands_out_the_native_instance_device_and_loader`; the 99th is
   `Imported_Vulkan_objects_dispatch_copy_and_time_through_NRI`. Both called Vortice's
   `Vulkan.GetApi(instance, device)` on NRI's own VkDevice. Vortice 3.3.0 caches that table in a
   static dictionary keyed by the handle value and drops it only on `vkDestroyDevice` through the
   table; NRI destroys the device itself, so the entry survives. The loader unloads the validation
   layer library on `vkDestroyInstance` and loads it again for the next instance (`VK_LOADER_DEBUG`
   shows "Unloading/Loading layer library" per instance). When a later NRI device lands at the same
   address, `GetApi` returns the dead table, whose `vkCreate*` pointers point into the old layer
   mapping. Same address: harmless. Moved: SIGSEGV or SIGILL on the first call.
   Measured in WSL by printing the cache state: 6 stale hits in 300 runs of the class, 2 of them
   with the layer moved, and those 2 crashed (SIGSEGV, SIGILL) at the first device call. No other
   run crashed. The dump showed the managed frame on `api.vkCreateBuffer`. In CI the 107th test hit
   the 99th's entry; that ordering is inferred from the count, not from a CI dump.
   Fix: `tests/PrismNRI.Tests/VulkanApi.cs` builds a fresh table from the public constructors for
   every object, and no test calls `GetApi` any more (Wrappers caveat). The minidump and
   "Crash stacks" CI steps stay in place for the next host crash.

- `.github/workflows/ci.yml`: `gate` (Linux, seconds), then `win-x64` (WARP + NONE), `linux-x64`
  (lavapipe, stock 1.3 loader, 1.4.363 layer, pinned `ubuntu-24.04`), `osx-arm64` (MoltenVK). The
  ClangSharp drift check is `regen.yml`, run only when `nri`, `patches/`, the generator,
  `dotnet/Native/` or the pin change. How runs are shaped and what they cost: Actions budget.
- **GCC 13 rejects NRI**: `-Werror` + `maybe-uninitialized` at `Source/Shared/HelperInterface.h:44`
  (`MemoryHeap`), only at `-O2`+; upstream's Ubuntu job configures without `CMAKE_BUILD_TYPE`, so
  unoptimised, so green. Patch 0005 fixes it (GCC 13.3.0 builds and the suite matches clang's
  numbers). `build-native.ps1` still picks clang on Linux when installed and `CXX` unset; CI installs
  `clang`, not `g++`.
- **"Ray tracing, cold shader cache"** (`linux-x64`) runs the two ray tracing classes with
  `MESA_SHADER_CACHE_DISABLE=true` before the suite and prints every fence wait of 250 ms or more;
  the `Test` step runs the whole suite. The "Vulkan device and host CPU" step prints `vulkaninfo
  --summary`, the CPU model, `nproc` and `avx512f`.
- macOS job prints CPU, `sw_vers`, brew versions, the ICD, the layer manifest, and
  `maxSamplerLodBias|samplerMipLodBias|hostImageCopy|timestamp*|external_*` from `vulkaninfo`.
- Windows runner: WARP for D3D12, no Vulkan ICD; Developer Mode off (the Agility measurement).
- `regen` installs ClangSharpPInvokeGenerator 21.1.8.4 with .NET 10 + 11 (90 s of its 139 s on the
  Windows runner) and fails with the first 120 diff lines on drift.

### Actions budget

The repository is private, so hosted minutes come out of the account's monthly allowance, which
the account's other repositories share. On 2026-10-14 it ran out mid-release: GitHub stopped
starting jobs ("The job was not started because recent account payments have failed or your
spending limit needs to be increased"); the v0.3.0-alpha tag run built its native payloads and
failed at `pack`. That release was published by hand from a complete run of the identical tree:
its `nupkg` and `zips-*` artifacts downloaded through the API, a draft release created with
`generate_release_notes`, the eleven assets uploaded, read back and compared by SHA-256, then
published. Artifacts are kept 90 days.

- Each job is billed in whole minutes, rounded up. Prices from the account's usage report, USD per
  minute: Linux 0.006, Windows 0.010, macOS 0.062.
- October 1 to 14, 48 runs, every job measured (`actions/runs/<id>/jobs`): macOS 52% of the cost
  with 13% of the minutes, Windows 38%, Linux 10%. By trigger: pull request runs 59%, CI on master
  pushes 23% (each merge repeated its pull request's run on the same tree), release dry runs 11%,
  tag runs 6%. By job: CI macOS 42%, CI Windows 20%, `regen` 13%, CI Linux 8%.
- A CI job before the payload cache, seconds: Windows 179 (native build 63, setup-dotnet 33,
  managed build 31, tests 27), Linux 138 (native 39), macOS 62 (native 13, which made it two billed
  minutes), `regen` 139.
- What answers each:
  - **`gate`** (`tools/ci/ci-gate.ps1`): pull requests and manual runs test everything. A push to
    master whose tree equals the head of a merged pull request that passed CI as a `pull_request`
    run is not tested again; a platform then only builds and caches the payload when master has no
    cache for its key, because a pull request restores master's caches and its own are visible to
    it alone. A direct push, a merge of a branch behind master, or an API error runs everything.
    The decision and its reason are in the run summary. Run against October's history: the five
    pull-request merges come out tested, the one direct push runs everything.
  - **Native payload cache**: key `native-<rid>-<20 hex>` over the git object ids of `nri`,
    `patches`, `tools/build-native.ps1`, `tools/cmake`, `Directory.Build.props`,
    `THIRD-PARTY-NOTICES.md` and `ci.yml`, identical on every OS whatever the line endings
    (measured: same key from the Windows CRLF and the WSL LF checkout). Saved right after a build,
    before the tests, so a red test run keeps it. A restored payload gets
    `tools/ci/apply-patches.ps1`, because NativeAbiTests compile against the patched `nri/Include`.
    Measured locally: a fresh clone with a restored payload passes the whole suite on NVIDIA
    Windows (244, 7 skipped: the two extra are the Upscalers contracts, never built in CI) and on
    lavapipe (244, 122 skipped).
  - **`paths-ignore`**: Markdown, `docs/`, the other workflows, `tools/consume/`, the generator.
  - **Timeouts**: gate 5, Windows 25, Linux 20, macOS 15 minutes; release jobs 10 to 45. They were
    60 to 120: one hung macOS job could bill 60 macOS minutes, 3.72 USD at list price.
  - **No release dry run**: a tag run that fails publishes nothing; fix, then move the tag or use
    "Re-run failed jobs".
- **Ubuntu pinned to 24.04** in the Linux CI leg and the release's Linux build and consume job:
  `ubuntu-latest` moves to Ubuntu 26 from 2026-10-19. A payload built there needs its newer glibc,
  and its 1.4 loader would stop exercising patch 0004. Moving is a decision.
