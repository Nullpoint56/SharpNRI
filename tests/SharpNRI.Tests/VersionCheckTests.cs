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
    /// was looked for and where that platform's library comes from. The handler's throw reaching the
    /// caller was checked by an application with NRI.dll deleted (2026-11-07); one cannot be removed
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

    [Fact]
    public void The_binding_and_the_descs_state_their_NRI_version_as_constants() =>
        Assert.Equal(NriHeaders.Version, Nri.NriVersion);

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
