// Every signature in the generated layer is blittable: pointers, integers, enums, and structs whose
// only non-primitive members are more of the same (C `bool` is emitted as `byte`). With runtime
// marshalling disabled the runtime stops inspecting them on every call, and a non-blittable type
// slipping into a DllImport becomes a build-time error instead of a silent per-call copy.
//
// The function tables are `delegate* unmanaged[Stdcall]` fields and were never marshalled; this
// covers the 19 flat exports in NriApi.cs and the by-value return of
// nriFitPipelineLayoutSettingsIntoDeviceLimits.

using System.Runtime.CompilerServices;

[assembly: DisableRuntimeMarshalling]
