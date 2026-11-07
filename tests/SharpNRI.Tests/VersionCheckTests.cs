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
