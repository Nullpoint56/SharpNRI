// The NRI version guard (Interop/NriVersionCheck.cs): a native library or SharpNRI.Descs of another
// NRI version fails device creation with INVALID_SDK naming both versions, instead of misreading
// every renumbered enum (v191 moved StageBits and AccessBits). A real foreign NRI.dll cannot be put
// on the load path inside one test process, so the expected version is overridden for the creating
// thread; the check, the destruction of the refused device and the message are the real ones.

using SharpNRI.Interop;
using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed class VersionCheckTests
{
    [Fact]
    public void SharpNRI_SharpNRI_Descs_and_the_native_library_report_one_NRI_version()
    {
        Assert.Equal(NriVersionCheck.Built, NriVersionCheck.Descs);
        using var device = NriDevice.Create(new NriDeviceOptions { GraphicsApi = GraphicsAPI.NONE });
        Assert.Equal(NriVersionCheck.Built, device.NriVersion);
        Assert.Null(NriVersionCheck.Mismatch(device.NriVersion));
    }

    /// <summary>
    /// A missing NRI library is explained (Interop/NativeLibraryHint.cs): the file, the RID, where it
    /// was looked for and where that platform's library comes from. Device creation throwing it was
    /// checked with NRI.dll deleted from a copy of the test output (2026-11-08); it cannot be removed
    /// from this process.
    /// </summary>
    [Theory]
    [InlineData("win-x64", "the SharpNRI package carries it under runtimes/win-x64/native/")]
    [InlineData("linux-x64", "the SharpNRI package carries it under runtimes/linux-x64/native/")]
    [InlineData("osx-arm64", "SharpNRI-native-base-osx-arm64-<tag>.zip")]
    [InlineData("linux-arm64", "pwsh tools/build-native.ps1 -Rid linux-arm64")]
    public void A_missing_NRI_library_is_explained_per_platform(string rid, string hint)
    {
        var text = NativeLibraryHint.Describe(rid, "/app/");
        Assert.Contains($"for {rid}", text, StringComparison.Ordinal);
        Assert.Contains("/app/", text, StringComparison.Ordinal);
        Assert.Contains(hint, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// An application's own ResolvingUnmanagedDll handler is asked for SharpNRI's "NRI" whenever it was
    /// registered: SharpNRI registers no handler that could answer first, or throw and end the chain
    /// (until 191.0.0-beta.4 its handler threw, so a handler registered after SharpNRI loaded never ran;
    /// reported downstream). Resolution of "NRI" is forced to fail by searching System32 only, in a
    /// child process: once NRI.dll is loaded, Windows answers the bare name with the loaded module.
    /// </summary>
    [Fact]
    public void An_applications_unmanaged_library_handler_registered_after_SharpNRI_is_asked_for_NRI()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("the forced failure searches System32: Windows only");
        var self = Environment.ProcessPath!;
        var start = new System.Diagnostics.ProcessStartInfo(self) { RedirectStandardOutput = true, UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(System.Reflection.Assembly.GetEntryAssembly()!.Location);
        start.Environment["SHARPNRI_TEST_CHILD"] = ChildResolverChain;
        using var child = System.Diagnostics.Process.Start(start)!;
        var output = child.StandardOutput.ReadToEnd();
        Assert.True(child.WaitForExit(GpuWork.GpuTimeout), "the child did not finish");
        Assert.True(child.ExitCode == 0, $"the {ChildResolverChain} child exited with {child.ExitCode}: {output}");
        Assert.Contains("RESULT asked for NRI", output, StringComparison.Ordinal);
    }

    private const string ChildResolverChain = "native-resolver-chain";

    [System.Runtime.CompilerServices.ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "the child mode must run before xunit's entry point")]
    internal static void RunResolverChainChildIfAsked()
    {
        if (Environment.GetEnvironmentVariable("SHARPNRI_TEST_CHILD") != ChildResolverChain) return;
        var code = 1;
        try
        {
            var sharpNri = typeof(NriDevice).Assembly;
            _ = Nri.BindingVersion; // SharpNRI loaded, its module initializer run; NRI.dll not loaded
            var asked = false;
            System.Runtime.Loader.AssemblyLoadContext.Default.ResolvingUnmanagedDll += (assembly, name) =>
            {
                if (assembly == sharpNri && name == "NRI") asked = true;
                return 0;
            };
            var loaded = System.Runtime.InteropServices.NativeLibrary.TryLoad("NRI", sharpNri, System.Runtime.InteropServices.DllImportSearchPath.System32, out _);
            Console.WriteLine(loaded ? "FAIL NRI.dll was found in System32" : asked ? "RESULT asked for NRI" : "FAIL the later handler was not asked");
            code = !loaded && asked ? 0 : 1;
        }
        catch (Exception e)
        {
            Console.WriteLine("FAIL " + e.ToString().ReplaceLineEndings(" / "));
        }
        Console.Out.Flush();
        Environment.Exit(code);
    }

    /// <summary>
    /// A missing NRI library reaching device creation or adapter enumeration is explained there (the
    /// file, the RID, where it was looked for, where it comes from), the runtime's exception inside.
    /// </summary>
    [Fact]
    public void A_missing_NRI_library_is_explained_with_the_runtimes_exception_inside()
    {
        var original = new DllNotFoundException("Unable to load DLL 'NRI' or one of its dependencies");
        var e = NativeLibraryHint.Explain(original);
        Assert.StartsWith("SharpNRI could not load the NRI native library", e.Message, StringComparison.Ordinal);
        Assert.Contains(original.Message, e.Message, StringComparison.Ordinal);
        Assert.Same(original, e.InnerException);
    }

    /// <summary>A SharpNRI.Descs of another NRI version fails creation like a native library of one, naming both, and leaves nothing alive.</summary>
    [Fact]
    public void A_SharpNRI_Descs_of_another_NRI_version_fails_creation_and_leaves_nothing_alive()
    {
        var allocator = new NriCountingAllocator();
        var other = NriVersionCheck.Built + 1;
        NriVersionCheck.t_descsOverride = other;
        try
        {
            var e = Assert.Throws<NriException>(() => NriDevice.Create(new NriDeviceOptions { GraphicsApi = GraphicsAPI.NONE, Allocator = allocator }).Dispose());
            Assert.Equal(Result.INVALID_SDK, e.Result);
            Assert.Equal("SharpNRI.Descs", e.Call);
            Assert.Contains($"built for NRI v{NriVersionCheck.Built}", e.Message, StringComparison.Ordinal);
            Assert.Contains($"for v{other}", e.Message, StringComparison.Ordinal);
        }
        finally
        {
            NriVersionCheck.t_descsOverride = 0;
        }
        Assert.Equal(0, allocator.Live);
    }

    /// <summary>
    /// A table nriGetInterface refuses by size (a library of another NRI version) is diagnosed as one:
    /// a real refusal, provoked by asking for CoreInterface into HelperInterface's size.
    /// </summary>
    [Fact]
    public unsafe void A_table_refused_by_size_is_diagnosed_as_a_library_of_another_NRI_version()
    {
        using var device = NriDevice.Create(new NriDeviceOptions { GraphicsApi = GraphicsAPI.NONE });
        var refused = Assert.Throws<NriException>(() => InterfaceTable.Acquire<HelperInterface>((Device*)device.NriHandle, "CoreInterface"));
        Assert.Equal(Result.INVALID_ARGUMENT, refused.Result);
        var diagnosed = NriVersionCheck.TableMismatch(refused);
        Assert.Equal(Result.INVALID_SDK, diagnosed.Result);
        Assert.Equal("nriGetInterface(CoreInterface)", diagnosed.Call);
        Assert.Contains($"does not match SharpNRI, built for NRI v{NriVersionCheck.Built}", diagnosed.Message, StringComparison.Ordinal);
        Assert.Contains(refused.Message, diagnosed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_binding_and_the_descs_state_their_NRI_version_as_constants() =>
        Assert.Equal(NriHeaders.Version, Nri.NriVersion);

    /// <summary>
    /// The constants are compiled into the caller; these are read from the loaded assemblies at run
    /// time: their package versions as Directory.Build.props sets them, and the NRI version of each.
    /// </summary>
    [Fact]
    public void The_loaded_binding_and_descs_report_their_versions_at_run_time()
    {
        Assert.Equal(NriHeader.Property("SharpNRIVersion"), Nri.BindingVersion);
        Assert.Equal(NriHeader.Property("SharpNRIDescsVersion"), Nri.DescsVersion);
        Assert.Equal(NriHeader.PinnedVersion, Nri.BindingNriVersion);
        Assert.Equal(NriHeader.PinnedVersion, Nri.DescsNriVersion);
    }

    /// <summary>
    /// A SharpNRI.Descs package other than the one SharpNRI depends on fails creation like one of
    /// another NRI version, though the NRI versions agree: NuGet takes a direct reference to another
    /// SharpNRI.Descs over SharpNRI's exact dependency with only the warning NU1608.
    /// </summary>
    [Fact]
    public void A_SharpNRI_Descs_package_of_another_version_fails_creation_and_leaves_nothing_alive()
    {
        var allocator = new NriCountingAllocator();
        const string other = "191.99.0";
        NriVersionCheck.t_descsPackageOverride = other;
        try
        {
            var e = Assert.Throws<NriException>(() => NriDevice.Create(new NriDeviceOptions { GraphicsApi = GraphicsAPI.NONE, Allocator = allocator }).Dispose());
            Assert.Equal(Result.INVALID_SDK, e.Result);
            Assert.Equal("SharpNRI.Descs", e.Call);
            Assert.Contains($"depends on SharpNRI.Descs {NriHeader.Property("SharpNRIDescsVersion")}", e.Message, StringComparison.Ordinal);
            Assert.Contains($"the loaded one is {other}", e.Message, StringComparison.Ordinal);
        }
        finally
        {
            NriVersionCheck.t_descsPackageOverride = null;
        }
        Assert.Equal(0, allocator.Live);
    }

    [Fact]
    public void A_native_library_of_another_NRI_version_fails_creation_and_leaves_nothing_alive()
    {
        var allocator = new NriCountingAllocator();
        var other = NriVersionCheck.Built - 1;
        NriVersionCheck.t_expectedOverride = other;
        try
        {
            var e = Assert.Throws<NriException>(() => NriDevice.Create(new NriDeviceOptions { GraphicsApi = GraphicsAPI.NONE, Allocator = allocator }).Dispose());
            Assert.Equal(Result.INVALID_SDK, e.Result);
            Assert.Contains($"built for NRI v{other}", e.Message, StringComparison.Ordinal);
            Assert.Contains($"reports v{NriVersionCheck.Built}", e.Message, StringComparison.Ordinal);
        }
        finally
        {
            NriVersionCheck.t_expectedOverride = 0;
        }
        Assert.True(allocator.Allocations > 0, "the refused device was created through the allocator");
        Assert.Equal(0, allocator.Live);
    }
}
