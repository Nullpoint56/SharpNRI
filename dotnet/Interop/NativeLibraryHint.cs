// L1 — a missing NRI library, explained.
//
// NriApi imports library "NRI". When the runtime finds no NRI.dll / libNRI.so / libNRI.dylib, the
// bare DllNotFoundException says nothing about why or what to do. AssemblyLoadContext's
// ResolvingUnmanagedDll event fires only after the default probing failed, for the SharpNRI assembly's
// "NRI" here, and leaves applications free to set their own DllImport resolver (one per assembly,
// which a resolver of ours would take). It throws DllNotFoundException with what was looked for,
// where, and where each platform's library comes from.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace SharpNRI.Interop;

internal static class NativeLibraryHint
{
    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "the handler must be in place before the first P/Invoke into NRI, whichever public call makes it")]
    internal static void Register() => AssemblyLoadContext.Default.ResolvingUnmanagedDll += Resolve;

    private static nint Resolve(System.Reflection.Assembly assembly, string libraryName)
    {
        if (assembly != typeof(NativeLibraryHint).Assembly || libraryName != "NRI") return 0;
        throw new DllNotFoundException(Describe(RuntimeInformation.RuntimeIdentifier, AppContext.BaseDirectory));
    }

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
