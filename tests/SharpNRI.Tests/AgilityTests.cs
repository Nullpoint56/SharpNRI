// The Agility SDK, loaded from a path (patches/0001).
//
// Without the patch a managed host runs D3D12 on the inbox runtime and NRI warns that OPTIONS22
// does not exist; with it, the staged D3D12Core.dll beside the assembly is the runtime. The
// oracle is NRI's own report plus the absence of that warning.
//
// On this machine Windows Developer Mode is on, so these tests cannot show that the device factory
// route needs no Developer Mode; the Windows CI runner, which has it off, is the test of that.

using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed class AgilityTests
{
    [Fact]
    public void The_staged_Agility_SDK_is_found_beside_the_assembly()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "D3D12 is Windows only");
        var path = NriDevice.DefaultAgilitySdkPath;
        Assert.NotNull(path);
        Assert.True(File.Exists(Path.Combine(path!, "D3D12Core.dll")));
        Assert.True(File.Exists(Path.Combine(path!, "d3d12SDKLayers.dll")));
    }

    [Fact]
    public void A_D3D12_device_uses_the_Agility_SDK_by_default()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.D3D12));
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.D3D12, messages);

        Assert.Contains(messages, m => m.Text.Contains("Using Agility SDK", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, m => m.Text.Contains("options22", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Fact]
    public void An_empty_path_forces_the_inbox_runtime()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.D3D12));
        var messages = new List<NriMessage>();
        using var device = NriDevice.Create(new NriDeviceOptions
        {
            GraphicsApi = GraphicsAPI.D3D12,
            Adapter = TestDevice.AdapterFor(GraphicsAPI.D3D12),
            D3D12AgilitySdkPath = "",
            MessageHandler = (in NriMessage m) => messages.Add(m),
        });

        // Once a factory has been applied to the process by an earlier test, the inbox runtime is
        // no longer reachable in this process: ApplyToGlobalState is process-wide and NRI says so
        // in the header. The assertion is therefore only that no SDK report is made for this
        // device, not that OPTIONS22 is absent.
        Assert.DoesNotContain(messages, m => m.Text.Contains("Using Agility SDK", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
