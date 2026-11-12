# SharpNRI: native packages, coverage and CI

Part of the working notes (`AGENTS.md`, which indexes every section). Section names in parentheses
name a section of `AGENTS.md` or of a file it indexes.

## Native packages

Staged by `tools/build-native.ps1`, each variant in its own CMake tree `_build/<rid>-<variant>/`.

| Package | Staging | win-x64 contents | Built with |
|---|---|---|---|
| `SharpNRI` (Base) | `runtimes/<rid>/native/` | `NRI.dll` (1.9 MB, C runtime static), `amd_ags_x64.dll`, licences; 14 files, 2.2 MB | NGX, FFX, XeSS, NIS all OFF explicitly |
| `SharpNRI.Native.Upscalers` | `runtimes-upscalers/<rid>/native/` | `NRI.dll` (3.4 MB), AGS, licences; 20 files, 3.8 MB | NIS, FFX, XeSS ON, NGX OFF; `libxess.dll` delay-loaded too |
| (Agility, tests only, never packed) | `runtimes-agility/<rid>/native/AgilitySDK/` | `D3D12Core.dll`, `d3d12SDKLayers.dll`, licence; 3 files, 10.2 MB | staged by either variant on Windows; applications take `SharpNRI.AgilitySDK` (below) |
| `SharpNRI.AgilitySDK` (targets only) | `agility/buildTransitive/` | no binary: `SharpNRI.AgilitySDK.targets` + a props written at pack time | depends on `Microsoft.Direct3D.D3D12` `[$(AgilitySdkVersion)]` |
| (`-WithDlss`, never packed) | `runtimes-upscalers-dlss/<rid>/native/` | 21 files, 3.2 MB; NGX static, imports `ADVAPI32.dll` too | NGX ON from `FETCHCONTENT_SOURCE_DIR_NGX` |

- Both NRI builds: Vulkan, D3D12, NONE, NRI validation, ImGui, NVAPI (static), AGS, debug names,
  compiled against the Agility headers (`NRI_ENABLE_AGILITY_SDK_SUPPORT=ON`), Agility runtime not
  inside; D3D11, Metal, WebGPU passed OFF. osx-arm64: Base only, `libNRI.dylib` (about 1.07 MB; 8
  files, 1.2 MB), Vulkan + NONE; not in the packages: a release attaches it as a zip when a macOS
  runner builds it (Actions budget).
- **Delay-loaded imports** (`tools/cmake/delayload.cmake`, injected on every Windows build as
  `CMAKE_PROJECT_NRI_INCLUDE`, so nothing under `nri/` is edited): after NRI's `project()` it defers
  itself (`cmake_language(DEFER)`) and adds `/DELAYLOAD` + `delayimp.lib` to the NRI target only:
  `d3d12.dll` and `dxgi.dll` always, `libxess.dll` with XeSS (otherwise NRI does not load without
  the XeSS runtime). The script checks with dumpbin and throws when a listed DLL is still a
  load-time import. So `NRI.dll` loads with the system only (`KERNEL32`, `USER32`), and a process that
  never calls D3D12 or DXGI loads neither. On NRI's creation path
  the first D3D12 call is the Agility path's `D3D12GetInterface`, so Agility still loads (`AgilityTests`).
- **Static C runtime on Windows** (owner, 2026-11-07): `build-native.ps1` passes
  `CMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded$<$<CONFIG:Debug>:Debug>` (and CMP0091 NEW for the
  dependencies), so no VC++ redistributable or UCRT set is imported; before, a machine without the
  redistributable failed the first call with `DllNotFoundException`. Measured at v191: Base `NRI.dll`
  1.5 -> 1.9 MB, Upscalers 2.7 -> 3.4 MB, no linker warning; NRI picks NGX's static-runtime library from
  the same variable (`-WithDlss`). Every DLL keeps its own heap; NRI frees what it allocates.
- **`SharpNRI.AgilitySDK`** (`agility/`): build targets only. The package depends
  on `Microsoft.Direct3D.D3D12` at `AgilitySdkVersion` (Directory.Build.props; `build-native.ps1`
  refuses a value other than NRI's `NRI_AGILITY_SDK_VERSION_MAJOR` / `_MINOR`), so the application's
  restore takes Microsoft's package under Microsoft's licence, and its `buildTransitive` targets copy
  `build/native/bin/<x64|arm64>/D3D12Core.dll` (plus `d3d12SDKLayers.dll` with
  `SharpNRIAgilityDebugLayer`, default true in Debug) into `AgilitySDK\` (Windows RIDs and portable
  builds; publish too, excluded from single-file bundles). The resolved version is read from
  `project.assets.json`: another one fails with `SHARPNRI011` (an explicit reference overriding the
  dependency), a missing file with `SHARPNRI012`. The targets also set the runtime switch
  `SharpNRI.RequireAgilitySdk`: with it `NriDevice.Create` throws `INVALID_SDK` when
  `AgilitySDK\D3D12Core.dll` is missing instead of falling back to the inbox runtime
  (`AgilityPathTests`). **The debug layer comes from the SDK's directory only**: without
  `d3d12SDKLayers.dll` beside `D3D12Core.dll`, NRI's `D3D12GetDebugInterface` fails and it enables
  nothing, without a message, also with the system's Graphics Tools layer installed (measured
  2026-11-07: no `ID3D12InfoQueue`; reported downstream for Release builds, where the package copies no
  layer). `NriDevice.Create` refuses D3D12 `EnableGraphicsApiValidation` on an Agility path without the
  file (`NriUnsupportedException` naming `SharpNRIAgilityDebugLayer`, before NRI is called;
  `AgilityPathTests`); the inbox runtime's layer is the system's and is not checked. Microsoft's own targets are in `build/native`, which SDK-style .NET projects
  never import. No Microsoft file in the package: the licence point under Decisions stands.
- Base and Upscalers both put `NRI.dll` at the same package path, and NuGet's choice between two
  packages' same-named assets is not to be relied on (beta.2: without `ExcludeAssets="native"` on
  `SharpNRI` the Base library won and the upscalers silently did nothing, reported downstream). From
  1.0.0-beta.3 `SharpNRI.Native.Upscalers` depends on `SharpNRI` `[v]` and its
  `buildTransitive/SharpNRI.Native.Upscalers.targets` removes the `SharpNRI` package's native assets
  after `ResolvePackageAssets` (`NativeCopyLocalItems` for RID builds, `RuntimeTargetsCopyLocalItems`
  for portable ones and the deps.json), so referencing it alone is enough; `ExcludeAssets` still works.
  `tools/consume -p:SharpNRIConsumeUpscalers=true` holds it to that (`IsUpscalerSupported(NIS)` on
  each GPU device, portable and published). `NriApi` imports library `NRI` for both.
- **Licences outside `runtimes/<rid>/native/`**. NuGet treats every file under
  `native/` as a native library and a RID-specific build or publish copies them flat: beta.2's
  `licenses/<lib>/LICENSE.txt` collided (`dotnet publish -r` failed with NETSDK1152) or overwrote each
  other and the application's own `LICENSE.txt` (reported downstream). Staging keeps
  `runtimes/<rid>/native/licenses/`; the packages carry them as `licenses/<rid>/...`, and each
  package's `buildTransitive` targets copy them to `licenses/<PackageId>/<rid>/` beside the
  application, folders kept (`SharpNRICopyLicenses=false` copies none). `release.yml`'s pack check
  refuses any subfolder under `runtimes/<rid>/native/`; the consume job publishes `-r` and Native AOT
  and checks the layout (`tools/consume`).
- **A missing NRI library** (`Interop/NativeLibraryHint.cs`): device creation,
  the two wrapped forms and adapter enumeration (the calls an application makes into NRI first)
  catch the runtime's `DllNotFoundException` and throw one naming the file, the RID, the directories
  looked in and where that platform's library comes from, the runtime's inside (checked with
  `NRI.dll` deleted, 2026-11-08). The other flat exports (`Nri.*`) throw the runtime's own. SharpNRI
  registers no `DllImport` resolver (the application's, one per assembly) and no
  `ResolvingUnmanagedDll` handler: until 191.0.0-beta.4 a handler of ours threw for "NRI", which ends
  the event's chain, so an application's handler registered after SharpNRI loaded never ran (reported
  downstream; `VersionCheckTests`, red with that handler).
- **Trimming and Native AOT** declared (`IsAotCompatible` on `SharpNRI`, `SharpNRI.Descs`,
  `SharpNRI.Testing`; the analyzers run in every build, lint fails on a finding) and run by the
  consume job's AOT publish.
- In-repo, `dotnet/SharpNRI.csproj` copies `runtimes/<rid>/native` (or, with
  `-p:SharpNRIDevPayload=upscalers` / `upscalers-dlss`, that variant and its vendor runtimes, local
  only and never packed: Upscalers and vendor runtimes) beside `ProjectReference`
  consumers, plus `runtimes-agility/<rid>/native` when staged (`-p:SharpNRIDevAgility=false` turns
  it off), so tests run D3D12 on Agility. Without a staged library for the RID the build fails with
  `SHARPNRI001` naming the `build-native.ps1` command (else the failure is a
  `DllNotFoundException` at device creation); `-p:SharpNRIRequireNative=false` skips the check.
- **Consumption smoke test** (`tools/consume`, not in the solution): `PackageReference`s to
  `SharpNRI` + `SharpNRI.Testing` at `-p:SharpNRIPackageVersion` from the folder in `SHARPNRI_FEED`
  (default `nupkg/`), restored into `tools/consume/obj/packages` so the global cache never stands
  in. Prints the loaded NRI path and creates NONE and each available GPU backend's validated device
  with a buffer and a fence; exit 0 only when NONE and one GPU backend worked; on Windows it also
  takes `SharpNRI.AgilitySDK` as the guide says (debug layer on) and requires `Using Agility SDK`. The
  release `consume` job runs it on windows and ubuntu, portable and `-r <rid>`, and on Windows builds
  it once more with `-p:SharpNRIAgilityOverride=1.619.6` (an explicit older `Microsoft.Direct3D.D3D12`),
  which must fail with `SHARPNRI011`. After editing the targets, build with `-nodeReuse:false`: a
  reused MSBuild node kept evaluating the old file (measured 2026-11-07). Locally (win-x64):
  `dotnet pack SharpNRI.slnx -c Release -o <dir>`, `$env:SHARPNRI_FEED = "<dir>"`, then `dotnet run
  --project tools/consume -c Release [-r win-x64] -p:SharpNRIPackageVersion=<version>`; delete
  `tools/consume/obj` after re-packing the same version.
- `NativePayloadTests` hold the staged files to an allowlist and the PE import and delay-import
  tables to exact lists (load-time: `KERNEL32`, `USER32`; delay-loaded: `d3d12`, `dxgi`, plus `libxess`
  in Upscalers).
  `NativeExportTests` resolve every `NriApi` `DllImport` with `NativeLibrary.TryGetExport`.
- **Debugging into the binding**: `SharpNRI`, `SharpNRI.Descs` and
  `SharpNRI.Testing` carry each portable PDB beside its DLL and name the repository and commit in
  `<repository>` (`PublishRepositoryUrl`); Source Link (in the SDK) maps the PDB's documents to that
  commit on GitHub, the `nri` submodule's to NRI's. The release pack adds
  `ContinuousIntegrationBuild`; `release.yml`'s pack check requires the PDBs and the commit. A PDB beside
  the DLL, not embedded: `tools/coverage/BoundSurface` reads the file. NRI's native `NRI.pdb` (25.3 MB on
  win-x64 at 191.0.0-beta.4, with the static C runtime) is not in the packages: `release.yml` attaches it
  per payload as `SharpNRI-symbols-<base|upscalers>-win-x64-<tag>.zip` (6.4 MB), checked to belong to the staged
  `NRI.dll` (same file, its debug directory names `NRI.pdb`). NRI ships no binaries or symbols itself,
  so a release is the only source of symbols matching its `NRI.dll`. `libNRI.so` and `libNRI.dylib` are
  built without debug info, so Linux and macOS have none; they get symbols only when someone asks
  (owner, 2026-11-07).
- Licences that shaped the split: NGX terms attach to `NRI.dll` itself when NGX is linked
  (static), require downstream terms "at least as protective" and forbid stand-alone
  redistribution. The Agility runtime may be redistributed only in applications that bind users to
  terms protecting Microsoft and indemnify it, which a library package cannot do, so no package
  carries it. NRI's post-build copies `D3D12StateObjectCompiler.*`, which the Agility licence does
  not allow redistributing: the allowlist leaves it out. Texts in `THIRD-PARTY-NOTICES.md`.


## Coverage

`pwsh tools/coverage.ps1` (after `build-native.ps1`) builds with `-p:SharpNRICoverage=true`, runs
the suite under coverage and writes `docs/coverage.md` and the two README badges (`docs/badges/`);
`-SkipBuild` / `-SkipTests` reuse the last build or run (`_build/coverage/`). A failing suite
publishes nothing. Each badge reads `@ <commit>`; the report's header names the commit, date, NRI
ref and patch count, the payload the tests loaded (by content) and the adapter each backend ran on
(the test executable's own choice, `SHARPNRI_TEST_CHILD=test-adapters`), all recorded at test time
(`_build/coverage/measurement.json`). CI does not run the measurement.

- **Staleness** (`-Check`, `.github/workflows/coverage.yml`): the report records the measured
  commit's tree entries for `dotnet`, `descs`, `testing`, `tests`, `patches` and `nri`. `-Check`
  builds nothing: it fails naming the paths changed since, when a badge names another commit, or
  when the measurement had uncommitted changes there. The workflow (git only) runs on pushes to
  master touching those paths or the report, and on a branch when started by hand. Red is the signal and
  blocks nothing (not a required check). The tool and build scripts are not inputs:
  after changing what is measured or how, run it again. Measure a committed tree.
- **Bound surface** (`tools/coverage/BoundSurface`, System.Reflection.Metadata over the built
  assemblies and PDB; not in the solution): a function is bound when binding code outside `NriApi`
  loads its table field or calls its export (each call site keeps file and line). A struct field is
  bound when the struct is reachable from a public or protected signature outside
  `SharpNRI.Native`, or, for a struct built from a managed form, when binding or `descs/` code
  touches the field; a struct no signature reaches is a data layout and counts through its public
  type. The raw tables, `device.Core`, `NriApi` and the two callback structs are not counted. Every
  function is bound; the one unbound field is `DeviceCreationDesc.enableD3D11CommandBufferEmulation`
  (D3D11 is not built).
- **Test coverage**: `Microsoft.Testing.Extensions.CodeCoverage` 18.12.0 through `dotnet test
  --coverage` (MTP only; the executable run directly is xunit's runner), cobertura, modules
  `SharpNRI`, `SharpNRI.Descs`, `SharpNRI.Testing` (`tools/coverage/coverage.settings.xml`),
  `dotnet/Native` dropped by path; child processes collected. Referenced only under
  `SharpNRICoverage`: Microsoft .NET Library licence, and no osx-arm64 or linux-arm64 collector.
- "Call site run by the tests" joins the two. The ones not run need the Upscalers payload
  (`tools/upscalers.ps1`); the video encode feedback resolve runs since the Vulkan H.264 encode does.


## CI runners and flakes

Both were on the Linux leg (lavapipe on GitHub hosts of varying CPU); the Windows and macOS legs
have no intermittent failures. Both were test defects, not driver bugs.

1. **"lavapipe ray query miss": a fence wait that gave up after 5 s.**
   `RayTracingTests.A_ray_query_hits_the_triangle_in_a_built_TLAS(VK)` sometimes missed entirely
   ("(8,8) centre ... expected 1, got 0; 0 hits of 256"). NRI's `Wait(fence, value)` gives up after
   `NRI_TIMEOUT_FENCE` (5000 ms), reports "WaitSemaphores() timed out!" as an ERROR and returns (the
   API is `void`), and the test read a readback not yet written. lavapipe JIT-compiles shaders when
   a command buffer executes (the BVH build alone runs five), so with a cold Mesa shader cache, 2
   vCPUs and parallel test classes the first submit is slow. Measured in WSL: 8-15 ms warm, 570-600
   ms with `MESA_SHADER_CACHE_DISABLE=true`, 5112 ms cold on one CPU shared with 14 busy loops,
   reproducing the failure 3 of 3; the runner's cold-cache step later logged 6098 ms.
   - Tests: `GpuWork.Wait(fence, value)` polls `NriFence.Value` (fails after `GpuWork.GpuTimeout`,
     2 min) and logs waits of 250 ms or more; every test reading GPU results waits through it.
   - Binding: `NriFence.Wait` reads `Value` after NRI's Wait and throws `TimeoutException` when
     short (not on NONE):
     `DeviceTests.Fence_wait_throws_when_NRI_gives_up_before_the_value_is_reached`.
   - Ruled out: synchronization. Syncval (`VK_KHRONOS_VALIDATION_VALIDATE_SYNC=1` plus
     `..._SYNCVAL_SHADER_ACCESSES_HEURISTIC=1`, without which shader descriptor accesses are
     invisible to it) found 0 hazards in the suite and catches a deleted TLAS->dispatch or
     dispatch->copy barrier (not BLAS->TLAS: a device address). lavapipe makes every barrier a full
     flush anyway. CI runs the suite with syncval on.
2. **Linux test host SIGSEGV: a stale Vortice function table.** Two runs died with exit 139 after
   106 of 239 tests. Vortice keeps `Vulkan.GetApi`'s table past NRI's destroy (Wrappers), and the
   loader reloads the validation layer library per instance, so a later device at the same address
   got pointers into the old layer mapping (in WSL: 6 stale hits in 300 runs, the 2 with a moved
   layer crashed). Fixed by `VulkanApi.cs`; the minidump and "Crash stacks" CI steps stay.

- `.github/workflows/ci.yml`: `gate` (Linux, seconds), then `win-x64` (WARP + NONE), `linux-x64`
  (lavapipe, stock 1.3 loader, 1.4.363 layer, `ubuntu-24.04`), `osx-arm64` (MoltenVK; opt-in, below),
  `lint` (Linux, no native payload: analyzers as errors, the formatter and the `SharpNRI.Descs` version; Lint, Versions) and `zero-alloc`
  (Linux, after `linux-x64`, whose cached payload it restores: the allocation classes on NONE and
  lavapipe; Lifetime). `regen.yml`
  is the ClangSharp drift check, run only when `nri`, `patches/`, the generator, `dotnet/Native/` or
  the pin change (prints the first 120 diff lines on drift). `coverage.yml` runs `tools/coverage.ps1
  -Check` (Coverage).
- **GCC 13** rejected v190 (`-Werror` + `maybe-uninitialized` in `HelperInterface.h`, only at
  `-O2`+); NRI 9b45d77 fixed it. `build-native.ps1` prefers clang on Linux when installed and `CXX`
  unset; CI installs `clang`.
- `linux-x64` runs "Ray tracing, cold shader cache" (the two ray tracing classes with
  `MESA_SHADER_CACHE_DISABLE=true`, printing waits of 250 ms or more) before the suite, and prints
  `vulkaninfo --summary`, the CPU, `nproc` and `avx512f`; the macOS job prints CPU, `sw_vers`, brew
  versions, the ICD, the layer manifest and the relevant `vulkaninfo` lines. The Windows runner has
  WARP, no Vulkan ICD, and Developer Mode off.

## Actions budget

Hosted minutes are limited and billed per job, rounded up; a macOS minute costs about ten Linux
minutes and a Windows minute about two. That shapes the runs:

- **Repository rules** (public since 2026-11-04): ruleset `master` (no deletion, no force-push,
  pull requests only with 0 approvals, required checks `gate`, `win-x64`, `linux-x64` (since
  2026-11-06), `lint` and `zero-alloc` (since 2026-11-07) on a branch that is up to date with master; repository
  admins may bypass when merging a pull request, for GitHub outages); ruleset `release tags`
  (`v*` cannot be deleted, moved or force-pushed; admins may bypass, to move a failed release's
  tag). Actions: the workflow token is read-only by default and cannot approve pull requests (the
  release jobs ask for `contents: write`); workflows of fork pull requests wait for approval for
  every outside contributor. Secret scanning with push protection and Dependabot alerts are on.
- **macOS is opt-in** (owner, 2026-11-04): hosted macOS arm64 runners were unavailable for hours at a
  time ("The job was not acquired by Runner of type hosted even after multiple attempts"; GitHub's
  annotation: "Due to capacity constraints, jobs targeting macOS arm64 runners may experience
  longer queue times"). The gate runs `osx-arm64` for a manual run with the `macos` input and the
  weekly schedule (Mondays 04:00 UTC, macOS alone, on master); never on master pushes. Nothing in a release waits for macOS either (owner, 2026-11-04): the packages carry no
  osx-arm64 library, and the `osx-arm64` job runs after the release is published and attaches
  `SharpNRI-native-base-osx-arm64-<tag>.zip` when a runner turns up (no macOS smoke test).
- **Branch runs are started by hand** (owner, 2026-11-06): `ci.yml` has no pull-request trigger. On
  a branch, Actions > ci > Run workflow (the branch selected). A pull request's checks leave out
  `workflow_dispatch` runs (PR #44, 2026-11-06: the run was green and linked to the pull request,
  the required checks kept "Waiting for status to be reported"), so the gate's first step marks
  `gate`, `win-x64` and `linux-x64` pending with a link to the run (the pull request shows at once
  that a run started), and the `report` job posts them as commit statuses on the tested
  commit (skipped by the gate counts as passed, any other non-success or a failed gate as failed),
  which the required checks read: a pull request merges only after a green run of its latest
  commit. Pushes to master run by themselves. When master has moved, the branch must be
  updated (merge or rebase master into it) and `ci` started again before it can merge. A fork's branch cannot be
  started that way: push its commits to a branch here first.
- **The quick checks alone** (owner, 2026-11-07): `lint` and `zero-alloc` are required, and a
  development branch iterates on them without the whole suite: Actions > checks > Run workflow
  (`checks.yml`, either or both), which posts the same two commit statuses on the commit it ran, or
  `pwsh tools/check.ps1` before pushing. `checks.yml`'s zero-alloc restores `ci.yml`'s payload under the
  same key (`ci-gate.ps1`) and on a miss builds and saves it. `ci.yml` runs both too, so a full run
  covers all five.
- **`gate`** (`tools/ci/ci-gate.ps1`): a manual run tests everything but macOS (above); on a branch
  whose changes against master are all in the `paths-ignore` list it skips every platform job, which
  the required checks count as passed. A push to master whose tree equals the head of a merged pull
  request that passed a `ci` run of that head runs nothing: the up-to-date rule makes the merged tree
  the tested one, and master builds no payload for its cache (owner, 2026-11-06). A branch's caches
  are visible to it alone, so the first branch run after a payload change builds the payload in its
  own run, cheaper than a separate warming job. A direct push or an API error runs everything. The run
  summary gives the decision.
- **Native payload cache**: key `native-<rid>-<20 hex>` over the git object ids of `nri`, `patches`,
  `tools/build-native.ps1`, `tools/cmake`, `Directory.Build.props`, `THIRD-PARTY-NOTICES.md` and
  `ci.yml`, the same on every OS and line ending. Saved right after a build, before the tests, so a
  red run keeps it. A restored payload gets `tools/ci/apply-patches.ps1`, because `NativeAbiTests`
  compile against the patched `nri/Include`.
- **`paths-ignore`** (push to master) = the gate's `$noResult` (a branch run): Markdown, `docs/`,
  `.idea/`, the other workflows, and every tool no `ci.yml` job runs: the local ones (`check.ps1`,
  `check-docs.ps1`, `upscalers.ps1`, `compile-test-shaders.ps1`, `ci/verify-linux.sh`), the release's
  (`ci/api-changes.ps1`, `ci/check-packages.ps1`, `ci/release-body.ps1`, `ci/upscaler-runtimes.ps1`,
  `apidiff/`), the generator, the coverage tool, `consume/`. The tools the jobs run are the gate's
  `$ciTools` (`build-native.ps1`, `cmake/`, `ci/ci-gate.ps1`, `ci/apply-patches.ps1`,
  `ci/descs-version.ps1`, `ci/fetch-vulkan-*`). The gate warns (an annotation on the run) when a file
  under `tools/` is in neither list, and treats it as changing a result, and when `ci.yml`'s list
  differs from its own (owner, 2026-11-12: #61, docs plus a comment in `check-docs.ps1`, ran
  everything). A new tool goes into one of the two lists in the same commit. A docs-only branch still
  needs its run started, which then skips every platform job, so `gate`, `win-x64` and `linux-x64`
  report and the required checks pass.
- **Timeouts**: gate 5, Windows 25, Linux 20, macOS 15 minutes; release jobs 10 to 45 (a few times
  the slowest measured job, so a hung job cannot run for an hour).
- **Releases are started by hand, from master only** (owner, 2026-11-06): Actions > release > Run
  workflow on master; no tag push or merge starts `release.yml`. The `version` job refuses another
  branch, a version already tagged and missing notes; the `release` job creates the tag `v<Version>`
  on the run's commit with the GitHub release. A run that fails earlier creates no tag and publishes
  nothing: fix through a pull request and run again, or "Re-run failed jobs" (also when hosted jobs
  never started). The `dry_run` input (any branch) only tries a change to the workflow; there is no
  dry run before a release. A release can be published by hand from a complete run of the identical
  tree (its `nupkg` and `zips-*` artifacts, kept 90 days, uploaded and compared by SHA-256).
- **NuGet.org** (since 1.0.0-beta.2): `release.yml`'s `nuget` job pushes the release's `.nupkg`
  artifact after the GitHub release, by Trusted Publishing: `NuGet/login` trades the job's OIDC
  token for a one-hour key, which nuget.org grants because the owner's policy names this
  repository, `release.yml` and the `nuget` environment, **and lists the package IDs it may push**:
  a new package needs adding to the policy before its first release (1.0.0-beta.3: SharpNRI.AgilitySDK
  was refused with 403, the policy listed the other four). No key is stored. Each package is pushed on
  its own and every refusal is named at the end, so one refusal leaves the
  others published (at beta.3 it left SharpNRI.Native.Upscalers unpushed). A push that failed after
  the GitHub release is retried by starting release.yml on master with `nuget_from_release` = its tag:
  it builds nothing and pushes the release's own `.nupkg` assets (GitHub re-runs jobs only of a run's
  latest attempt; a full re-run stops at the version check because the tag exists, and drops the earlier
  attempts' artifacts). The repository variable
  `NUGET_USER` is the nuget.org account; without it the job skips. The `nuget` environment requires
  the owner's approval and admits `master` only. A version on NuGet.org is permanent (unlisting only
  hides it), so a failure after the push means a new version. Releases only: no CI or merge build is
  published there. Each package carries `docs/nuget/README.md` (absolute links), which the
  `pack` job requires. `SharpNRI.Descs` is pushed only when its version is new: an unchanged published
  one is downloaded in `pack` and reused (Versions under Decisions), and `--skip-duplicate` skips it.
  `tools/ci/api-changes.ps1` (in `pack`) writes the body's "API changes": `tools/apidiff`
  (MetadataLoadContext, not in the solution) lists each package's public types and members with
  signatures and values against the previous SharpNRI version on NuGet.org and its SharpNRI.Descs,
  removed / changed / added, mechanically; NRI's own changelog is not repeated (owner, 2026-11-07:
  consumers need what changed in our API, not why). The `pack` check also holds the packages to their exact dependencies and `SharpNRI`'s description
  to the platforms packed (`SharpNRIPackageDescription`, built from the staged libraries; beta.2
  claimed macOS arm64 and said "Alpha").
- **apt without an up-front refresh**: packages install from the runner image's lists; only an
  install that fails on a stale list runs `apt-get update` and retries (the unconditional refresh
  alone took minutes on a slow mirror, 2026-11-06). Test packages (`libvulkan1`,
  `mesa-vulkan-drivers`, `vulkan-tools`) install only when tests run, build packages (`clang`,
  `ninja-build`, X11/Wayland headers) only when the payload is built. Ubuntu's
  `vulkan-validationlayers` is not installed: the SDK layer replaces it.
- **Ubuntu pinned to 24.04** in the Linux leg and the release's Linux build and consume jobs:
  `ubuntu-latest` moved to Ubuntu 26, whose newer glibc a payload built there would need and whose
  1.4 loader would stop exercising the old-loader path. Moving is a decision.
