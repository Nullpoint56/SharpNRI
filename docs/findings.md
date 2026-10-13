# PrismNRI — Findings

An engineering log, kept because most of what is hard about binding NRI is not in a signature.
Every entry below was measured against NRI v190 and the toolchain named in the entry; most
contradicted a reasonable guess, including guesses in the handoff this repo started from.

Chronological. Section numbers continue from [design.md](design.md) (§1–§5) so a bare `§6.3` in a
code comment is unambiguous across both files. At release the consumer-relevant rows move into the
guide's traps table, as PrismSlang did; this file stays the record of how they were found.

---

## 6. Phase one: generator, device, triangle

### 6.1 C++ mode, not C mode

The handoff said to run ClangSharp over NRI in C mode. Measured with ClangSharp 21.1.8.4 on
2026-10-13:

| Mode | Files | Lines | Errors as generated | After mechanical fixes |
|---|---:|---:|---:|---:|
| C (`NRI_FORCE_C`) | 373 | 6,352 | 8 | typing gone |
| C++ | 373 | 7,661 | 152 | 0 |

NRI's C surface turns every `NriEnum` into `typedef uint8_t NriFormat;` plus a separately named
`enum NriFormat_`. Every enum-typed struct field and every function-pointer parameter therefore
comes out as `byte`/`ushort`, and `Result` as `sbyte`. The bytes line up and the types are gone;
fixing it would mean ~95 type remaps plus re-deriving each enum's backing type by hand, which is
exactly the silent-layout-corruption class of edit the ABI oracle exists to catch.

C++ mode keeps `enum class Format : byte`, typed bitfields and typed signatures. Its 152 errors are
one cause: the `NriBits` operator overloads (`~ & | &= |=`, five per enum, 38 enums) are
`constexpr` functions ClangSharp translates into static helpers that do not compile. Removing
them, and dropping the `U` suffix from `1U << n` in byte- and ushort-backed enums, gives a clean
build. Both rules live in `tools/generate-native.ps1` and nowhere else.

Also measured: zero C++ default member initializers in NRI's headers (`NriDefault` is unused), so
PrismSlang's worst silent trap — defaults dropped by the generator — cannot recur here. C `bool`
is emitted as `byte` with `[NativeTypeName("bool")]`.

### 6.2 The ABI oracle agrees with the header, everywhere

`NativeAbiTests` generates a C++ probe from the generated sources, compiles it against
`nri/Include` with clang++ 22.1.8 and compares. On 2026-10-13:

| Checked | Count |
|---|---:|
| Structs, `sizeof` and `alignof` | 271 (238 top-level, 33 nested anonymous) |
| Field offsets | 1,831 |
| Fixed buffers, `sizeof` | 55 |
| Enums, size and signedness | 95 |
| Bitfields, exact bytes written | 4 |
| Function-pointer signatures | 234 of 234 |
| `DllImport` signatures | 19 of 19 |

Zero mismatches. The probe cannot see calling convention (a no-op on x64 and arm64), `const`,
reference-versus-pointer, or `bool`-versus-`uint8_t`; all are erased on purpose. It also cannot
measure the anonymous union inside `BottomLevelGeometryDesc`, whose members are checked as offsets
in the outer struct instead.

### 6.3 NRI checks the table size itself, and reports its version

`nriGetInterface` compares the caller's `sizeof(Interface)` against the implementation's and
refuses a mismatch with `INVALID_ARGUMENT` after reporting both sizes through the message callback
(`Source/Creation/Creation.cpp`). `DeviceDesc.nriVersion` carries `NRI_VERSION` as built. Both are
free runtime checks of the pin; `DeviceTests` asserts the second against `Directory.Build.props`.

### 6.4 The Agility SDK does not load in a managed process

NRI's CMake writes `Include/NRIAgilitySDK.h` declaring exported `D3D12SDKVersion` and
`D3D12SDKPath` for the host *executable* to compile in, and the D3D12 backend calls
`D3D12CreateDevice` directly. `dotnet.exe`, `testhost.exe` and an apphost export nothing, so the
inbox D3D12 runtime is used and the staged `AgilitySDK/D3D12Core.dll` is never opened. Visible
symptom on this machine (Windows 11 26200, RTX 4070 SUPER, inbox runtime):

```
[WARNING] DeviceD3D12.hpp:911: nri::DeviceD3D12::FillDesc(): ID3D12Device::CheckFeatureSupport(options22) failed, result = 0x80070057!
```

`OPTIONS22` does not exist in the inbox runtime. Resolved by the device-factory patch (§7.1). The
D3D12 adapter-support probe in `nriEnumerateAdapters` also calls `D3D12CreateDevice` and is left
on the inbox runtime on purpose: it only asks whether the adapter can do D3D12 at all.

### 6.5 Software adapters are enumerated, not hidden

`nriEnumerateAdapters` lists WARP (`DXGI_ADAPTER_FLAG_SOFTWARE`) and lavapipe
(`VK_PHYSICAL_DEVICE_TYPE_CPU`) and marks both `Architecture::SOFTWARE`. So the CI oracles are
reachable through the adapter desc with no patch; `TestDevice.PreferSoftware` picks them under
`GITHUB_ACTIONS` or `PRISMNRI_TEST_SOFTWARE=1`.

### 6.6 `GetQueue` out of range returns without a message; NONE returns success for everything

NRI.h documents `GetQueue` returning `INVALID_ARGUMENT` for a bad index. Measured: it does, on
Vulkan and D3D12, and nothing arrives through the message callback first. The NONE backend answers
`SUCCESS` for the same call because it answers `SUCCESS` for every call, including an index of
1000. A test that wants a message has to provoke the validation layer, not a result code; a test
that wants a result code cannot run on NONE.

### 6.7 Disposal order is enforced by refusing, and the refusal must come before the handle is dropped

`NriDevice.Dispose` throws while any object created on it is alive. The first version zeroed the
handle before running that check, so the exception left the wrapper reporting `IsDisposed` and
the native device leaked. `NriObject.Dispose` now runs `BeforeDestroy` first and gives the handle
up only after it returns; `DeviceTests` covers both halves.

### 6.8 A host barrier on a readback buffer is rejected by NRI's validation

The triangle test originally ended with a buffer barrier `{COPY_DESTINATION, COPY}` →
`{HOST_READ, HOST}` on the `HOST_READBACK` buffer, as a Vulkan programmer would write. NRI's
validation layer refuses it:

```
[ERROR] CommandBufferVal.hpp:173: ValidateBufferBarrierDesc: 'barrierDesc.buffers[0].after.access' is not supported by the usage mask of the buffer
```

`HOST_READ`/`HOST_WRITE` access belongs to textures created with `HOST_TRANSFER`; for a buffer the
fence wait is the host visibility, as on D3D12. The barrier is gone and the test is green.

### 6.9 The triangle renders on both backends

`TriangleTests` draws three vertices from `SV_VertexID` into a 64×64 RGBA8 attachment, reads it
back through `CmdReadbackTextureToBuffer` and checks corners, three vertex regions and the centre,
with NRI validation and the backend validation layers on. Green on Vulkan 1.4 and D3D12 (inbox
runtime, `ID3D12Device14`) on the RTX 4070 SUPER on 2026-10-13. The clear colour is
`(0.2, 0.4, 0.6)` because `0.1 × 255 = 25.5` rounded to 25 on NVIDIA's Vulkan driver and the
expected 26 elsewhere; the assertion allows one unit either way, which is the UNORM rounding
freedom the specs leave to the driver.

The two backends' readbacks are not byte-identical at triangle edges and the test does not ask
them to be; it records each backend's SHA-256 in the output instead.

### 6.10 xunit v3 under Microsoft.Testing.Platform

The suite is xunit.v3 4.0.2 because backend theories need to skip at run time with a reason
(`Assert.Skip`), which xunit 2 lacks. With the .NET 11 SDK that means Microsoft.Testing.Platform:
`global.json` carries `"test": { "runner": "Microsoft.Testing.Platform" }`, the test project is an
`Exe`, there is no `Microsoft.NET.Test.Sdk`, serialization is `xunit.runner.json`, and the
commands are `dotnet test --solution PrismNRI.slnx` and `dotnet test --project tests/PrismNRI.Tests`.
A `dotnet.config` with the same setting did nothing on 11.0.100-rc.1; the doc page says
`global.json`.

### 6.11 The native payload, as staged

`tools/build-native.ps1` on win-x64, 2026-10-13: 31 files, 225.6 MB, of which `NRI.dll` is 2.8 MB.
The rest is the upscaler SDKs (DLSS 107 MB across two DLLs, XeSS 78 MB, FSR 38 MB), AGS, and the
Agility SDK core and layers. NVAPI is statically linked. Two facts matter for consumers:

- `libxess.dll` is a load-time import of `NRI.dll`: without it NRI does not load at all.
- The Visual C++ 2015–2022 runtime is required and not shipped.

The licence review found three things the package does not currently do: the DLSS/NGX terms require
downstream terms "at least as protective" as NVIDIA's, forbid stand-alone redistribution, and
attach to `NRI.dll` itself because NGX is statically linked; the Agility SDK terms require
distributors to bind users to terms protecting Microsoft; and NRI's own post-build copies
`D3D12StateObjectCompiler.*`, which the Agility licence does not allow redistributing, so the
staging allowlist leaves it out. `THIRD-PARTY-NOTICES.md` has the texts. This is a decision for
the project owner, recorded here rather than resolved.

---

## 7. Phase two: patches

### 7.1 The Agility SDK loads through a device factory, from a managed host

Patch `0001-agility-sdk-device-factory` adds `DeviceCreationDesc::d3d12AgilitySdkPath`. When set,
`DeviceD3D12::Create` calls `D3D12GetInterface(CLSID_D3D12SDKConfiguration)`, then
`ID3D12SDKConfiguration1::CreateDeviceFactory(NRI_AGILITY_SDK_VERSION_MAJOR, path)`, then
`ID3D12DeviceFactory::ApplyToGlobalState()`, before any other D3D12 entry point. Measured on
2026-10-13 from the xunit test host, which exports nothing:

```
[INFO] DeviceD3D12.hpp:508: Using Agility SDK v1.619 from '...\bin\Debug\net11.0\AgilitySDK\'
```

and the `OPTIONS22` warning of §6.4 is gone. `ApplyToGlobalState` was chosen over creating the
device from the factory directly so that `D3D12GetDebugInterface` (the debug layer, DRED) and
AGS's `agsDriverExtensionsDX12_CreateDevice` on AMD see the same SDK without further changes.

Two caveats, both recorded in `AgilityTests`: the setting is process-wide, so once applied the
inbox runtime is not reachable again in that process; and this machine has Windows Developer Mode
on, so it cannot show that the factory route needs no Developer Mode. Microsoft documents
`CreateDeviceFactory` as the route that does not; the Windows CI runner, which has Developer Mode
off, is the measurement.

The patch changes a header, so the generator and the ABI oracle both run against the patched
submodule. `generate-native.ps1` now applies `patches/` the same way `build-native.ps1` does, and
the oracle's 1,831 offsets include the new field.

### 7.2 A null AbortExecution means NRI breaks into a debugger that is not there

Found by the per-object test pass, after a crash the triangle test had met first (§6.8 was only
half the story). `CallbackInterface::AbortExecution` is optional in the header; when it is null,
NRI's `CheckAndSetDefaultCallbacks` installs its own, which calls `DebugBreak()` on every
`Message::ERROR`. With no debugger attached that terminates the process with `0x80000003` before
the failed call returns its `Result` and before any test sees the recorded message: a validation
regression showed up as a vanished test host with exit code 0 through a pipe.

`MessageBridge` now always supplies its own thunk, which breaks only when `BreakOnError` was asked
for and a debugger is attached. The measured record of what NRI reported is then the message
handler's, and a validation error is a red test rather than a dead process.

### 7.3 What the per-object pass measured

168 tests on NONE, Vulkan and D3D12 (RTX 4070 SUPER, Agility 1.619), 2026-10-13. The numbers
worth keeping, each recorded in the test that measured it:

- NRI never wraps the NONE backend in its validation layer, even when asked
  (`Creation.cpp`, `enableNRIValidation && graphicsAPI != NONE`). NONE answers `{size=1}` for
  every buffer, one static desc for every texture, `MapBuffer` null, and 0 allocations from
  `AllocateAndBindMemory`.
- `TextureDesc` zeros for height, depth, mipNum, layerNum and sampleNum come back as 1 on both GPU
  backends (`FixTextureDesc`). `BufferDesc` comes back exactly as given.
- Memory requirements for a 1000-byte buffer: Vulkan 1,008 bytes at 16; D3D12 inbox 65,536 at
  65,536; D3D12 Agility 1,024 at 128. Upload row and slice alignments: 1/16 on Vulkan, 1/1 on D3D12.
- D3D12 descriptor-set heap offsets for three consecutive sets: (0,0), (3,1), (6,2); back to (0,0)
  after `Reset`. Vulkan: (0,0) always.
- Pipeline cache after one compute pipeline: 6,872 bytes on Vulkan, 3,318 on D3D12; a cache
  rebuilt from those bytes reports the same size.
- Timestamp frequency 1 GHz on both. A timestamp result is 8 bytes on both.
- Recording a representative command stream 300 times on NONE allocates 0 managed bytes and 0 host
  allocations through `NriCountingAllocator`.
- The host allocator's live count returns to 0 after device disposal on every backend
  (NONE 2/2, Vulkan 60/60, D3D12 50/50 allocations/frees; no reallocations).
- NRI's validation rejects `HOST_READ`/`HOST_WRITE` in any buffer barrier
  (`IsAccessMaskSupported`), and freeing memory while a resource is still bound to it.

### 7.4 Shared handles export on both backends

Patch `0002-shared-handle-extension` adds `NRISharedHandle.h`: `CreateSharedTexture`,
`CreateSharedFence`, `GetTextureSharedHandle`, `GetFenceSharedHandle`, with the Vulkan backend
enabling `VK_KHR_external_memory_win32`/`_fd` and `VK_KHR_external_semaphore_win32`/`_fd` itself
(the device now initializes 55 extensions here, up from 53) and allocating the shared texture's
memory directly rather than through VMA so the export info can be chained. Measured 2026-10-13:

| Backend | Texture handle | Fence handle | Notes |
|---|---|---|---|
| Vulkan 1.4, NVIDIA | `OPAQUE_WIN32` NT handle, size reported | `OPAQUE_WIN32` NT handle | a second `vkGetMemoryWin32HandleKHR` on the same memory returned the same handle value |
| D3D12, Agility 1.619 | `D3D12_RESOURCE` NT handle | `D3D12_FENCE` NT handle | from `ID3D12Device::CreateSharedHandle` |
| NONE | zero | zero | dummy, as everything on NONE |

A shared fence still works as a fence (signalled through a queue and waited on the host), and
asking an ordinary texture for a handle is `INVALID_ARGUMENT` through the validation layer. The
other side of the proof, opening the handles with D3D11 and D3D12 and reading the pixels back, is
`SharedHandleImportTests`. The extension is a `git diff` against the tree with patch 0001 applied,
produced through a temporary index so the two patches stay independent; the build script applies
them in order onto a clean checkout and the result was compared against the working tree.

### 7.5 NRI's validation layer crashes on `HAS_BUFFER`

`NRIRayTracing.h` defines `HAS_BUFFER` as `(Buffer*)1`: an `AccelerationStructureDesc` may name
it instead of a real buffer so that a structure can be sized before its inputs exist. The Vulkan
and D3D12 backends both test for it. The validation layer does not: `ConvertBottomLevelGeometries`
passes every geometry buffer through `NRI_GET_IMPL`, which calls a method on address 1. With
`enableNRIValidation` on, the first `CreateCommittedAccelerationStructure` with the sentinel is an
access violation that takes the process down before any message is reported. Found by
`RayTracingTests` on 2026-10-13, Vulkan backend, and it would be the same on D3D12.

Patch `0003-validation-has-buffer-sentinel` makes the conversion return the sentinel unchanged.
`NriBuffer.HasBuffer` is the binding's spelling of it and is now safe with validation on.

### 7.6 Ray tracing through the extension, on both backends

`RayTracingTests` builds a BLAS from one triangle (sized with `NriBuffer.HasBuffer`, built with the
real buffer), a TLAS with one instance pointing at the BLAS's `GpuHandle`, and dispatches a compute
shader that traces an inline ray query per thread against the TLAS. 16×16 rays from z = 0 towards
+z; the centre hits, the corners miss, and the hit count sits in the band a triangle of area 0.5 in a
2×2 field should give. Green on Vulkan and D3D12 (RTX 4070 SUPER, Agility 1.619) on 2026-10-13
with NRI validation on, after patch 0003. The shader is `cs_6_5` on both targets; dxc warns that
`vk::binding` is ignored for the DXIL target, which is expected and harmless. The managed desc
forms for geometries and builds are marshalled on the stack per call; a build with hundreds of
geometries is fine, tens of thousands would want a different shape.

### 7.7 The Agility SDK also reaches NRI through its own wrapped-device route

Measured 2026-10-13 in a process where patch 0001 had not applied anything: the host obtains
`ID3D12SDKConfiguration1`, creates a device factory for SDK 1.619 from the staged directory,
creates the `ID3D12Device` from the factory, and hands it to `nriCreateDeviceFromD3D12Device`.
NRI reports `Using ID3D12Device15`, no `OPTIONS22` warning, and a buffer, a view and a fence round
trip through the wrapped device with NRI validation on (`AgilityFactoryRouteTests`, four vtable
calls, no D3D12 binding). NRI supports this on purpose: root signatures of such "independent"
devices are serialized through `ID3D12DeviceConfiguration` (`PipelineLayoutD3D12.hpp`, upstream
PR #177).

So patch 0001 is a convenience, not a necessity. What the wrapped route lacks is everything NRI
only does on its own creation path: `Robustness`, `DeviceLostInfoLevel`, D3D12 ray-tracing
validation, and the debug-layer message filtering; and the binding would own adapter selection by
LUID and the debug layer through the factory's configuration interface, about 150 lines of COM
against 20 lines of patch. The patch stays, and this route is the documented fallback for a host
that cannot carry it. Neither route needs Developer Mode by Microsoft's documentation; this
machine cannot show that, the Windows CI runner can.

### 7.8 What the import proof and the extension pass measured

**Import proof** (`SharedHandleImportTests`, RTX 4070 SUPER, 2026-10-13). A Vulkan texture
exported by the extension imports into a second Vulkan device as a dedicated allocation and all
16,384 bytes of the triangle match, same SHA-256 on both sides; an exported timeline semaphore
imports and signals both ways. A D3D12 fence opens on a second D3D12 device and signals both ways.
A D3D12 texture opens on a second D3D12 device but D3D11's `OpenSharedResource1` refuses it with
`E_INVALIDARG`, isolated by experiment to the format: NRI creates every texture `TYPELESS`
(`DeviceD3D12::GetResourceDesc`) and D3D11 will only open a typed shared resource. Since the
editor imports through Vulkan, shared D3D12 textures stay typeless like every other NRI texture
and the header says so; the test records the refusal as the measured limitation. One API gap
stands: an exclusive Vulkan image strictly needs a queue-family release to
`VK_QUEUE_FAMILY_EXTERNAL` before another device acquires it, and NRI's barrier takes queue
pointers, so it cannot be expressed; NVIDIA tolerated the omission.

**Extension availability** (`*ExtensionTests`, same machine): NONE supports every extension except
`WrapperVK` and `WrapperD3D12`; Vulkan everything except `WrapperD3D12`; D3D12 everything except
`WrapperVK`. `Video` is unsupported on both GPU backends unless video queues are requested at
device creation, because NRI trims the adapter's queue counts to the queues it created. A device
wrapped from a raw D3D12 device with no queue families has no queues at all, where NRI's own
creation defaults to one graphics queue. `Window` in `NRISwapChain.h` is a struct of five members
side by side, not a union. `StreamerDesc::constantBufferMemoryLocation` is marked optional but
validation rejects its default. H.265 Annex-B writing returns `UNSUPPORTED` for short-term and
long-term reference sets, VUI, scaling lists and tiles, which the header does not say.
`GetVideoAV1EncodeDecodeInfo` returns pointers into the result struct itself. D3D12 reports H.264
decode for 64×64 NV12 as min = max = 64×64 with no references; Vulkan reports 48×16 to 4096×4096
with 16. A 300-frame loop over every extension's per-frame path allocates 0 managed bytes.

### 7.9 Three native packages, and what a missing vendor runtime does

The payload of §6.11 is split (design §3.6): `PrismNRI` carries an NRI built with no upscaler SDK,
`PrismNRI.Native.Upscalers` one with NIS, FSR and XeSS, `PrismNRI.Native.Agility` the Agility
runtime, and no package carries a vendor runtime. Measured on win-x64, RTX 4070 SUPER, 2026-10-13:

| Staging | Files | Size | `NRI.dll` |
|---|---|---|---|
| `runtimes/` (Base) | 14 | 1.8 MB | 1.4 MB |
| `runtimes-upscalers/` | 20 | 3.1 MB | 2.7 MB |
| `runtimes-agility/` | 3 | 9.6 MB | |
| `runtimes-upscalers-dlss/` (`-WithDlss`, never packed) | 21 | 3.2 MB | 2.7 MB |

against 31 files and 225.6 MB before. Base's `NRI.dll` halves because the NIS shader blobs and
ShaderMakeBlob leave with NIS.

**No DLSS download without `-WithDlss`.** NRI declares the `ngx` FetchContent only inside
`if(NRI_ENABLE_NGX_SDK)`, and the script now passes it OFF explicitly. NRI's own dependency list
at configure: Base `agilitysdk;nvtx;vulkan_headers;vma;d3d12ma;amdags;nvapi` (263 MB of `_deps`),
Upscalers `...;ffx;fsr;xess;...;shadermake` (1.8 GB), neither with `ngx`, and no `ngx-src` in
either `_deps`; the script throws if one ever appears. With `-WithDlss <dir>` the list includes
`ngx`, but `FETCHCONTENT_SOURCE_DIR_NGX` points it at the given directory, and again no `ngx-src`
or `ngx-subbuild` is created. The DLSS `NRI.dll` imports `ADVAPI32.dll` in addition (NGX's).

**Load-time imports.** Both packaged `NRI.dll`s import only system DLLs and the VC++ runtime
(`d3d12`, `dxgi`, `KERNEL32`, `USER32`, `MSVCP140`, `VCRUNTIME140[_1]`, UCRT). `libxess.dll`, a
load-time import before, is delay-loaded in the Upscalers build: `/DELAYLOAD:libxess.dll` and
`delayimp.lib` on the NRI target only, from `tools/cmake/xess-delayload.cmake`, which the script
passes as `CMAKE_PROJECT_NRI_INCLUDE` and which defers itself to the end of NRI's CMakeLists with
`cmake_language(DEFER)`. No patch was needed. `NativePayloadTests` reads both import tables.

**What each upscaler does without its runtime**, Upscalers `NRI.dll`, raw L0 calls (no managed
guard), the vendor DLLs absent from beside the apphost:

| Backend | Device creation | `IsUpscalerSupported` | `CreateUpscaler` |
|---|---|---|---|
| D3D12 | SUCCESS (Agility 1.619) | NIS, FSR, XESS | FSR: `FAILURE`, no message. XESS: SEH exception from the delay-load stub, surfaced in .NET as `SEHException` (E_FAIL) |
| VK | SUCCESS | NIS, FSR | FSR: `FAILURE`, no message |

FSR is clean: NRI `LoadLibrary`s `amd_fidelityfx_loader_dx12.dll` / `amd_fidelityfx_vk.dll` by name
and returns `FAILURE` on null (`UpscalerInterface.hpp`). It is `FAILURE`, not `UNSUPPORTED`, and
`IsUpscalerSupported` still says yes: it answers what is compiled in, not what is installed.

XeSS is not. The exception is catchable in .NET (0xC06D007E is not a corrupting-state code), but
NRI takes its process-wide `g_xess.lock` before `xessD3D12CreateContext`, and the lock is not
released as the exception unwinds (C++ scope guards are not run for SEH exceptions under CMake's
default `/EHsc`): a second XeSS `CreateUpscaler` from another thread did not return within 10 s.
The `UpscalerImpl` and its `Xess` allocation are presumably leaked too (not measured). So the
binding must not let the call happen: `NriUpscalerExtension.CreateUpscaler` checks the runtime files
first (`IsRuntimeInstalled`, `GetRuntimeFiles`) and throws `NriUnsupportedException` naming the file.
With the guard, every case above becomes that exception, and NIS still creates.

**The host matters.** With the vendor DLLs beside `Measure.dll` but run as `dotnet Measure.dll`
from another directory, FSR on D3D12 returned `FAILURE` and XeSS terminated the process with
unhandled exit code 0xC06D007E: NRI and the delay-load helper load by bare name, and the
application directory Windows searches is the executable's, `dotnet.exe`'s. The guard therefore
also loads each file by full path from `AppContext.BaseDirectory` before calling NRI, so the
bare-name load finds the module already in the process. After that, under `dotnet Measure.dll`:
FSR on D3D12 and Vulkan, XeSS, and (DLSS build) DLSR on D3D12 and DLRR on Vulkan all create.

Positive controls, vendor DLLs present: FSR on D3D12 (provider 3.1.5) and Vulkan (3.1.4) and XeSS
on D3D12 create on the NVIDIA GPU. The `-WithDlss` build reports NIS, FSR, XESS, DLSR, DLRR on
D3D12 (NIS, FSR, DLSR, DLRR on Vulkan); without `nvngx_dlss*.dll`, raw DLSR creation is a clean
`FAILURE` on both backends, and with them DLSR and DLRR create.

## 8. Phase five: the Linux leg

Measured on WSL2 Ubuntu 24.04 (stock `libvulkan1` 1.3.275, Mesa 25 lavapipe "llvmpipe (LLVM
20.1.2)" reporting Vulkan 1.4.318) and on the `ubuntu-latest` GitHub runner. The Windows leg was
already green on the runner (WARP for D3D12, no Vulkan ICD, 121 passed / 94 skipped / 0 failed);
these are the things the Linux leg measured that Windows could not.

### 8.1 GCC 13 rejects NRI; clang builds it

NRI builds with `-Werror`, and GCC 13 reports `maybe-uninitialized` in
`Source/Shared/HelperInterface.h:44` (`MemoryHeap`), a false positive clang does not raise. NRI's
own flags are written for clang, so `tools/build-native.ps1` passes clang to CMake on Linux when
it is installed and `CXX` is unset. The apt lists in `tools/ci/verify-linux.sh` and the workflow
install `clang` instead of `g++`.

The cause is one member: `MemoryHeap`'s constructor never initialises `alignment`
(`HelperInterface.hpp`), and GCC's analysis only runs at `-O2` and above. Upstream's own Ubuntu job
configures without `CMAKE_BUILD_TYPE` and so compiles unoptimised, which is why it stays green there.
Patch 0005 adds the initialiser; measured with GCC 13.3.0 (`CC=gcc CXX=g++`): native build clean,
suite 215 total, 107 passed, 108 skipped, 0 failed, the same numbers as the clang build. CI keeps
clang, which is what NRI's Apple and most Linux users compile with; the patch is there so the
distribution compiler works too and so the report upstream carries a verified fix.

### 8.2 The regen job failed on line endings, not content

The drift check was `git status --porcelain -- dotnet/Native`. On the Windows runner git has
`core.autocrlf=true`, the checkout is CRLF, ClangSharp writes LF, and `git status` reports every
regenerated file as modified while `git diff --stat` prints nothing: the content was identical.
Three changes: a `.gitattributes` (`* text=auto`; patches `-text`, since they must apply byte for
byte; shader binaries `binary`), the generator normalises its output to LF whatever the host, and
the drift check re-adds the directory and compares `git diff --cached`, which goes through the
clean filter and compares content.

### 8.3 Ubuntu's validation layer predates the headers NRI builds against

NRI v190 pins Vulkan-Headers 1.4.365 and chains into `vkCreateDevice` every feature struct the
driver offers. Ubuntu 24.04's `vulkan-validationlayers` is 1.3.275: it reports
`VkPhysicalDeviceVulkan14Features`, maintenance7/8 and `VK_EXT_zero_initialize_device_memory` as
"unknown VkStructureType" ERRORs through the debug messenger, which NRI forwards as `[ERROR]`
messages and the tests assert against. LunarG's apt repository for noble stops at 1.4.313, which
still does not know `VK_EXT_zero_initialize_device_memory` (sType 1000620000, which lavapipe
advertises). The only current Linux binary is inside the SDK tarball (366 MB), so
`tools/ci/fetch-vulkan-layer.sh` takes the one library and its manifest out of it, strips the
library, and `VK_ADD_LAYER_PATH` puts it in front of the system one; the workflow caches the two
files per SDK version. With 1.4.363 every "unknown struct" message is gone.

### 8.4 An older loader in front of a newer driver crashes NRI: patch 0004

Two failures remained with the current layer and went away together when the SDK's 1.4 loader
was put on `LD_LIBRARY_PATH`, which made the cause the loader version:

- `DescriptorTests.Several_sets_are_allocated_and_written_...` killed the process with
  `SIGSEGV` at address 0 from `CmdSetRootConstants` (gdb). NRI resolves `vkCmdPushConstants2` as
  an optional core 1.4 function because `m_MinorVersion` is 4, taken from the physical device;
  Mesa returns null for core 1.4 entry points when the instance is 1.3, and
  `CmdSetRootConstants` branches on the version, not on the pointer.
- `TextureTests.Committed_2D_texture_is_created_with_each_usage(VK, HOST_TRANSFER)`: NRI does not
  enable `VK_EXT_host_image_copy` on a "1.4" device (core there), the layer evaluates the 1.3
  instance and rejects `VK_IMAGE_USAGE_HOST_TRANSFER_BIT` as needing the extension.

`CreateInstance` asks for `min(vkEnumerateInstanceVersion(), 1.4)`, so the usable version is the
lower of the two. Patch 0004 clamps `m_MinorVersion` to it (14 lines, `DeviceVK::Create`, the
wrapper path keeps the caller's value). With the patch and the stock loader, NRI runs lavapipe
as a 1.3 device, enables the extension and loads the KHR names, and both tests pass. This pairing
is the stock Ubuntu 24.04 plus a Mesa update, so CI keeps the distribution loader on purpose.
Upstream text in `docs/upstream.md`.

### 8.5 Lavapipe has no external semaphores

`vulkaninfo` on lavapipe lists `VK_KHR_external_memory_fd` and not
`VK_KHR_external_semaphore_fd`. The SharedHandle extension (patch 0002) needs both, so
`nriGetInterface(SharedHandleInterface)` is `UNSUPPORTED` there and `TrySharedHandles` is null.
`SharedHandleTests` now skip on a Vulkan device without the interface, naming the two
extensions; on D3D12 and NONE the interface is still asserted present. The import proofs are
Windows-only already.

### 8.6 Two wrapper tests assumed Windows

With everything above in place the suite still died once per run, in
`WrapperExtensionTests.WrapperVK_hands_out_the_native_instance_device_and_loader`: a null call
from managed code (gdb frame 0 at address 0, frame 1 in JIT code). Vortice's `GetApi` goes
through `vkGetDeviceProcAddr`, which is null until `vkInitialize()` has run, and only the
raw-device test called it; on Windows that test had always run first. The loader is now a
process-wide lazy both tests touch. The raw-device test then failed to find NRI's adapter: it
matched `VkPhysicalDeviceIDProperties.deviceLUID` against `AdapterDesc::uid`, and on Linux
`deviceLUIDValid` is false, so NRI builds the uid from the UUID (`ConstructUid`,
`SharedExternal.h`); the test now does the same.

### 8.7 Where the Linux leg ends up

| | Windows (RTX 4070 SUPER, Base variant) | Linux (lavapipe, stock 1.3 loader, 1.4.363 layer) |
|---|---|---|
| total | 215 | 215 |
| passed | 213 | 107 |
| skipped | 2 (NIS under Base) | 108 (D3D12, Agility, NIS, shared handles, import proofs) |
| failed | 0 | 0 |

`tools/ci/verify-linux.sh` runs the same steps as the workflow's `linux-x64` job; under WSL2 the
native build takes about four minutes with clang and the tests three seconds.

### 8.8 The ray query test fails on some runner hosts: lavapipe's 512-bit path

After patch 0005 landed, `RayTracingTests.A_ray_query_hits_the_triangle_in_a_built_TLAS(VK)`
failed on the Linux runner twice in a row (the centre pixel reports no hit), having passed the
day before with identical packages: Mesa 25.2.8, loader 1.3.275, clang 18. It was not the patch:
the test uses committed resources only, and `MemoryHeap::alignment` is only ever copied into a
VMA field. It was not the Mesa version: WSL upgraded to the same 25.2.8 passes 18 runs out of 18
on "llvmpipe (LLVM 20.1.2, 256 bits)". The difference is the host. GitHub's runner pool mixes
CPUs, and lavapipe JIT-compiles its shaders for the host it finds, taking a 512-bit vector path
on AVX-512 machines.

The job now prints `vulkaninfo --summary` and the CPU model, and sets `LP_NATIVE_VECTOR_WIDTH=256`
for the whole job. The first run with that: host "AMD EPYC 9V45 96-Core Processor" with `avx512f`,
device reported as 256 bits, 216 total, 108 passed, 108 skipped, 0 failed. Circumstantial, not a
proof: the failing host was not caught with the diagnostics on, and the pin was applied before
`vulkaninfo` ran. If the test fails again with the pin in place, the printed device line and CPU
model are the first thing to read. This is a lavapipe matter, nothing a user of the binding sees
on a real GPU.

**Correction, later the same day.** The AVX-512 reading above was backwards. With
`LP_NATIVE_VECTOR_WIDTH=256` in place the test failed again, on an "AMD EPYC 7763" host that has
no `avx512f`; it passed five runs out of five on an "AMD EPYC 9V74" that has it, and the WSL host
that never fails is a Ryzen 9700X, also AVX-512. So the hosts that hit are the AVX-512 ones and
the host that misses is the one without, which points at LLVM 20's code generation for lavapipe's
ray query on the AVX2 path. `GALLIUM_OVERRIDE_CPU_CAPS` cannot narrow it further: `avx` and
`sse4.1` both make lavapipe die with an illegal instruction before any test runs. The pin is
removed. The test now treats a software adapter as a measurement of the build-and-dispatch path:
validation errors still fail it, a wrong hit map is recorded in the skip reason, and the hit
assertions stay as they are on hardware. The probe step stays so that each host leaves its map.

**Second correction.** The CPU pattern did not hold either: the next run on an "AMD EPYC 7763"
passed the probe five times out of five and the suite with it. Four runner hosts, two CPU
models, the same packages: the miss is intermittent, and nothing in the environment the job can
print predicts it. What is known: the shader writes 0 or 1 for every ray, the geometry is opaque,
the build-and-dispatch sequence passes both validation layers on every run, and the miss has
never reproduced locally (Windows on the GPU, WSL on lavapipe, over fifty runs). The shader now
loops on `Proceed()` as the spec describes instead of calling it once, which is correct for
opaque geometry either way and removes one thing a software traversal could disagree about. The
probe stays: the next miss leaves its map in the log, and that is the next piece of evidence.

## 9. Phase six: the macOS leg

Measured on the `macos-latest` GitHub runner: "Apple M2 Pro (Virtual)" (`VirtualMac2,1`), macOS
26.6.2, AppleClang 21.0.0, CMake 4.4.3, .NET SDK 11.0.100-rc.2. Vulkan is Homebrew's MoltenVK
1.4.2 behind Homebrew's Khronos loader 1.4.357, with Homebrew's validation layer 1.4.357. The
device is "Apple Paravirtual device", Vulkan 1.4.357, conformance 1.4.4.0; NRI creates it as a
Vulkan 1.4 device with 18 device extensions. Only the Base variant is built, with NRI's Metal
backend off: on macOS this binding is Vulkan through MoltenVK.

### 9.1 The build, and what the library links

`tools/build-native.ps1 -Rid osx-arm64` configures with AppleClang, Ninja,
`CMAKE_OSX_ARCHITECTURES=arm64` and `NRI_ENABLE_METAL_SUPPORT=OFF` passed explicitly: NRI turns its
Metal 4 backend on by default whenever Xcode 26 and the Metal toolchain are present, and the runner
has both, so which backends the library contains would otherwise depend on the image. NGX is not
passed (its condition, `WIN32 OR NOT IS_ARM64`, is false). The native build takes about 25 seconds
including the dependency download; the payload is `libNRI.dylib` (1,069,376 bytes) plus the same
licences as Linux, 8 files, 1.2 MB.

Two facts about the library that differ from the other platforms, both measured with `otool`:

- **NRI links the Vulkan loader on Apple.** `nri/CMakeLists.txt` runs `find_package(Vulkan
  REQUIRED)` under `APPLE` and links `Vulkan_LIBRARIES` into the VK backend, so `libNRI.dylib`
  has a load-time dependency on `/opt/homebrew/opt/vulkan-loader/lib/libvulkan.1.dylib`, the
  absolute install name of the build machine's loader, and still `dlopen`s `libvulkan.1.dylib`
  at device creation as on Linux. A Mac without a Vulkan loader cannot load the library at all,
  NONE backend included; one with the loader elsewhere depends on dyld's fallback search by leaf
  name. The script records the link list and refuses anything outside the system and the loader.
- **Minimum macOS 26.0** (`minos 26.0, sdk 26.5`). No deployment target is set, so the library
  is stamped with the runner's OS and will not load on an older macOS.

Both are packaging decisions for the owner rather than things this leg changes: the first would
be a patch to NRI's CMakeLists, the second a `CMAKE_OSX_DEPLOYMENT_TARGET`.

### 9.2 Homebrew's Vulkan is on none of the default paths

Homebrew on arm64 lives in `/opt/homebrew`. dyld's default fallback path is
`$HOME/lib:/usr/local/lib:/usr/lib`, so a `dlopen("libvulkan.1.dylib")` by leaf name (NRI's, and
Vortice's in the wrapper tests) does not search it; the job sets `DYLD_FALLBACK_LIBRARY_PATH`
with the Homebrew prefix first. Whether the suite passes without it was not measured. molten-vk
1.4.2 keeps its ICD manifest in the keg's `etc/vulkan/icd.d`, not `share/vulkan/icd.d` (the first
run failed with "Found no drivers" on the `share` path); the job finds the file and sets
`VK_DRIVER_FILES` to it alone. The loader finds the validation layer in
`/opt/homebrew/share/vulkan/explicit_layer.d` without help: adding the keg's own directory through
`VK_ADD_LAYER_PATH` only produced a "duplicate layer" loader warning.

The layer (1.4.357) is older than the Vulkan-Headers NRI builds against (1.4.365), the situation
that produced "unknown struct" errors on Linux (8.3). With MoltenVK it produced none: every
struct NRI chains for MoltenVK's 18 extensions is one 1.4.357 knows.

### 9.3 A raw Vulkan device must opt into portability enumeration

`WrapperExtensionTests` creates its own `VkInstance` and `VkDevice` through Vortice.Vulkan. The
loader hides portability drivers (MoltenVK) from an instance that does not enable
`VK_KHR_portability_enumeration` with `VK_INSTANCE_CREATE_ENUMERATE_PORTABILITY_BIT_KHR`, and a
device that advertises `VK_KHR_portability_subset` must have it enabled
(VUID-VkDeviceCreateInfo-pProperties-04451). NRI does both under `__APPLE__`. The test now does
both when the loader and the device offer them, which only a portability driver does; on Windows
the six wrapper tests still pass. On the runner the raw device is Vulkan 1.3 on the paravirtual
device with validation on, and wraps without a validation error.

### 9.4 A sampler with a mip LOD bias the device forbids: patch 0006

`DescriptorTests.Sampler_is_created_from_a_filled_desc(VK)` failed on the layer's
VUID-VkSamplerCreateInfo-samplerMipLodBias-04467: "mipLodBias (portability error) is 0.500000,
but samplerMipLodBias not supported." MoltenVK reports
`VkPhysicalDevicePortabilitySubsetFeaturesKHR::samplerMipLodBias = false` and
`maxSamplerLodBias = 15.999`, and NRI copies the limit into `DeviceDesc::other.samplerLodBiasMax`,
so the reported limit permitted a sampler the device forbids. Patch 0006 reports 0 when the
portability subset is enabled without the feature (6 lines next to NRI's existing
`imageView2DOn3DImage` read of the same struct); on the runner the device then reports
`samplerLodBiasMax = 0`. The test fills `mipBias` with `min(0.5, samplerLodBiasMax)` and records
both; on Windows and Linux the limit is far above 0.5 and the test is unchanged.

### 9.5 Host-transfer images crash the paravirtual Metal driver

The first complete run died with SIGSEGV (exit 139) in
`TextureTests.Committed_2D_texture_is_created_with_each_usage(VK, HOST_TRANSFER)`. Under lldb on
the runner: `EXC_BAD_ACCESS (address=0xc)` in `AppleParavirtGPUMetalIOGPUFamily`, called from
MoltenVK's `MVKImagePlane::updateMTLTextureContent` <- `MVKImageMemoryBinding::flushToDevice` <-
`bindDeviceMemory` <- `vkBindImageMemory`, through the validation layer, from NRI's
`TextureVK::AllocateAndBindMemory`. No Vulkan error precedes it, and the device advertises
`hostImageCopy = true`, which is why NRI sets `VK_IMAGE_USAGE_HOST_TRANSFER_BIT` and reports
`FormatSupportBits::HOST_COPY`. Nothing NRI reports predicts it, so this one case skips by device
name (`TestDevice.IsAppleParavirtualVK`) with the crash as the reason. Whether a physical Apple
GPU behaves the same is not measured here.

### 9.6 Timestamps are written at completion

`QueryTests.Two_timestamps_around_GPU_work_are_ordered(VK)` read 0 and 0. MoltenVK reports
`timestampComputeAndGraphics = true`, `timestampValidBits = 64` on every family and
`timestampPeriod = 1`, so NRI reports `features.timestamp`. When it sees two zeros on the
paravirtual device, the test now copies the same queries again from a later submission: that copy
reads 140273875750 -> 140273875750, two equal non-zero values. That is what timestamps written on
the host when the command buffer completes look like, which is MoltenVK's path for a GPU without
counter sampling: a copy recorded in the submission that wrote them runs before they exist. The
test records both readings and skips on that device only; wherever the in-submission copy reads
non-zero it asserts as before.

### 9.7 What else the leg measured

- **The ABI oracle agrees on arm64.** `NativeAbiTests` compiled its probe with AppleClang on
  arm64 and matched all 273 structs, 1,840 field offsets, 55 fixed buffers, 96 enums, 4
  bitfields, 19 DllImports and 238 function-pointer fields. Until now it had only run on x64.
- **External handles.** MoltenVK has core external memory and semaphores and
  `VK_EXT_external_memory_metal`, but neither `VK_KHR_external_memory_fd` nor
  `VK_KHR_external_semaphore_fd`, so patch 0002's interface is `UNSUPPORTED` and the five
  `SharedHandleTests` skip on Vulkan, as on lavapipe.
- **Capabilities MoltenVK lacks here**, each already a named skip: ray tracing, mesh shaders,
  video, descriptor heaps, shading-rate attachments.
- The triangle renders and reads back on MoltenVK with validation on (`TriangleTests`, 115 ms).

### 9.8 Where the macOS leg ends up

| | Linux (lavapipe, 8.8) | macOS (MoltenVK 1.4.2, paravirtual M2 Pro, 1.4.357 layer) |
|---|---|---|
| total | 216 | 216 |
| passed | 108 | 104 |
| skipped | 108 | 112 |
| failed | 0 | 0 |

The 112 skips: 88 D3D12 (Windows only), 5 SharedHandle (no fd external handles), 4 NT-handle
import proofs (Windows path), 3 video, 3 payload contracts that do not exist for osx-arm64
(Upscalers twice, Agility), and one each for ray tracing, mesh shaders, descriptor heaps, the
shading-rate attachment, PE imports, the Windows swap-chain window, NIS (Base variant) and the
two paravirtual-device defects (9.5, 9.6). The tests take about 4 seconds.

### 9.9 Four decisions, and what was done with them

- **The two device-name skips stay.** §9.5 and §9.6 are driver defects on the paravirtual Metal
  device with no feature bit to key on. They remain keyed on the adapter name with the defect as
  the reason, and the rule and its exception are written down in design §4.6.
- **The loader link becomes patch 0007.** Upstream history: PR 134 (merged April 2025) added
  the Apple link so that samples launched from an IDE without the Vulkan SDK environment could
  find the loader; the maintainer's review said the static link "is just not needed because it
  gets loaded dynamically, except your specific case" and asked for a condition, and the change
  merged unconditional. No issue reports the consequence for a shipped library. The patch wraps
  the two lines in `NRI_ENABLE_VULKAN_LOADER_LINKING`, default ON so upstream's workflow is
  unchanged, and `build-native.ps1` passes OFF for osx-arm64. The library then links only the
  system and the Vulkan backend finds the loader through `DYLD_FALLBACK_LIBRARY_PATH` on the
  runner as before. NRI's `DeviceCreationDesc` has no loader-path field (only the wrapper route's
  `DeviceCreationVKDesc::libraryPath` has one), so a consumer that ships its own loader puts it
  on a path dyld searches; recorded in the guide.
- **Deployment target 15.0.** NRI pins none, so the library inherited the runner's own version
  (26.0). MoltenVK's runtime minimum is macOS 12.0; .NET 11 supports macOS 15 and newer; the
  binding cannot run below .NET's floor, so `CMAKE_OSX_DEPLOYMENT_TARGET=15.0` is the lowest
  version that can matter and is what the script passes.
- **The sampler bias adaptation stays.** The test asks for `min(0.5, samplerLodBiasMax)` and
  records both numbers (§9.4); the guide's traps table carries the consumer-facing rule.
