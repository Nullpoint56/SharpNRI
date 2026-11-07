// L1 — the NRI version the three parts were built from must be one.
//
// NRI promises no ABI between versions: v191 renumbered StageBits and AccessBits, so a SharpNRI built
// for one version over an NRI library of another misreads every barrier without a single error. Each
// part records its version: SharpNRI and SharpNRI.Descs as AssemblyMetadata("NriVersion") from
// Directory.Build.props, the native library as DeviceDesc::nriVersion (its NRI_VERSION). NRI exports
// nothing that says its version before a device exists, so the check runs on the new device, before
// the binding hands it out; a table that already disagrees in size fails acquisition first and gets
// the same diagnosis.

using System.Diagnostics;
using System.Reflection;
using SharpNRI.Native;

namespace SharpNRI.Interop;

internal static class NriVersionCheck
{
    /// <summary>The NRI version SharpNRI was built for: <see cref="Nri.NriVersion"/>, compiled in.</summary>
    internal const int Built = Nri.NriVersion;

    /// <summary>The NRI version the loaded SharpNRI.Descs was generated from.</summary>
    internal static readonly int Descs = Read(typeof(DeviceDesc).Assembly);

    /// <summary>Tests only: the version this thread's device creations expect instead of <see cref="Built"/>.</summary>
    [ThreadStatic] internal static int t_expectedOverride;

    /// <summary>Tests only: the NRI version this thread sees for the loaded SharpNRI.Descs instead of <see cref="Descs"/>.</summary>
    [ThreadStatic] internal static int t_descsOverride;

    private static int Expected => t_expectedOverride != 0 ? t_expectedOverride : Built;

    private static int LoadedDescs => t_descsOverride != 0 ? t_descsOverride : Descs;

    /// <summary>The exception for a native library or descs assembly of another NRI version, or null.</summary>
    internal static NriException? Mismatch(ushort native)
    {
        var expected = Expected;
        var descs = LoadedDescs;
        if (descs != Built)
            return new NriException(Result.INVALID_SDK, "SharpNRI.Descs",
                $"SharpNRI is built for NRI v{Built}, the loaded SharpNRI.Descs ({typeof(DeviceDesc).Assembly.GetName().Version}) for v{descs}: reference the SharpNRI.Descs version SharpNRI depends on");
        if (native != expected)
            return new NriException(Result.INVALID_SDK, "nriCreateDevice",
                $"SharpNRI is built for NRI v{expected}, the loaded native library ({LoadedLibrary() ?? "path unknown"}) reports v{native}: a stale or foreign NRI library is on the load path");
        return null;
    }

    /// <summary>The diagnosis when a table of the new device does not even have the expected size.</summary>
    internal static NriException TableMismatch(NriException acquisition) =>
        new(Result.INVALID_SDK, acquisition.Call,
            $"the loaded native library ({LoadedLibrary() ?? "path unknown"}) does not match SharpNRI, built for NRI v{Expected} ({acquisition.Message})");

    private static int Read(Assembly assembly)
    {
        foreach (var a in assembly.GetCustomAttributes<AssemblyMetadataAttribute>())
            if (a.Key == "NriVersion" && int.TryParse(a.Value, out var v)) return v;
        throw new InvalidOperationException($"{assembly.GetName().Name} carries no NriVersion metadata");
    }

    // Failure path only: the process module list names the NRI library the DllImports resolved.
    private static string? LoadedLibrary()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            foreach (ProcessModule m in process.Modules)
            {
                var name = Path.GetFileName(m.FileName);
                if (name.Equals("NRI.dll", StringComparison.OrdinalIgnoreCase) || name == "libNRI.so" || name == "libNRI.dylib") return m.FileName;
            }
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
        }
        return null;
    }
}
