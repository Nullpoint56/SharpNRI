// L1 — a missing NRI library, explained.
//
// NriApi imports library "NRI". When the runtime finds no NRI.dll / libNRI.so / libNRI.dylib, the
// bare DllNotFoundException says nothing about why or what to do. The calls an application makes
// into NRI first (device creation, the wrapped forms, adapter enumeration) catch it and throw one
// with what was looked for, where, and where each platform's library comes from, the runtime's
// inside. SharpNRI registers no resolver and no ResolvingUnmanagedDll handler: the DllImport resolver
// is the application's (one per assembly), and a handler of ours that answers or throws for "NRI"
// would keep every handler registered after it from running (it threw until 191.0.0-beta.4).

using System.Runtime.InteropServices;

namespace SharpNRI.Interop;

internal static class NativeLibraryHint
{
    /// <summary>The runtime's exception for NRI's library, explained; the original inside.</summary>
    internal static DllNotFoundException Explain(DllNotFoundException e) =>
        new($"{Describe(RuntimeInformation.RuntimeIdentifier, AppContext.BaseDirectory)} ({e.Message})", e);

    internal static string Describe(string rid, string baseDirectory)
    {
        var file = OperatingSystem.IsWindows() ? "NRI.dll" : OperatingSystem.IsMacOS() ? "libNRI.dylib" : "libNRI.so";
        var source = rid switch
        {
            "win-x64" or "linux-x64" => "the SharpNRI package carries it under runtimes/" + rid + "/native/ (or SharpNRI.Native.Upscalers); check that the package's native assets are not excluded",
            "osx-arm64" => "the SharpNRI packages carry no macOS library: each GitHub release attaches SharpNRI-native-base-osx-arm64-<tag>.zip; put its files beside the application",
            _ => "SharpNRI packages no library for this platform; build one from the SharpNRI repository (pwsh tools/build-native.ps1 -Rid " + rid + ")",
        };
        return $"SharpNRI could not load the NRI native library ({file}) for {rid}: not beside the application ({baseDirectory}) "
             + $"nor under runtimes/{rid}/native/ there. Where it comes from: {source}. A SharpNRI checkout builds it with pwsh tools/build-native.ps1.";
    }
}
