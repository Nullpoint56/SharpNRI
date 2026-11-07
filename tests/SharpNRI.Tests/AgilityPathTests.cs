// How the binding picks the Agility SDK path for a D3D12 device (NriDevice.ResolveAgilitySdkPath):
// the caller's path, "" for the inbox runtime, or by default AgilitySDK\ beside the application when
// it holds D3D12Core.dll. With the SharpNRI.AgilitySDK package referenced (its runtime switch
// SharpNRI.RequireAgilitySdk), a missing default is an error instead of a silent fall back to the
// inbox runtime. The package's build side runs in tools/consume (release.yml, consume).

using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed class AgilityPathTests
{
    private static readonly NriDeviceOptions D3D12Default = new() { GraphicsApi = GraphicsAPI.D3D12 };

    [Fact]
    public void Without_the_package_a_missing_AgilitySDK_directory_falls_back_to_the_inbox_runtime()
    {
        Assert.Null(NriDevice.ResolveAgilitySdkPath(D3D12Default, defaultPath: null, required: false));
        Assert.Equal(@"C:\app\AgilitySDK\", NriDevice.ResolveAgilitySdkPath(D3D12Default, @"C:\app\AgilitySDK\", required: false));
    }

    [Fact]
    public void With_the_package_a_missing_AgilitySDK_directory_fails_D3D12_device_creation()
    {
        var e = Assert.Throws<NriException>(() => NriDevice.ResolveAgilitySdkPath(D3D12Default, defaultPath: null, required: true));
        Assert.Equal(Result.INVALID_SDK, e.Result);
        Assert.Contains("SharpNRI.AgilitySDK", e.Message, StringComparison.Ordinal);
        Assert.Contains("D3D12Core.dll", e.Message, StringComparison.Ordinal);

        // present, an explicit inbox request, an explicit path, another backend: no refusal
        Assert.Equal(@"C:\app\AgilitySDK\", NriDevice.ResolveAgilitySdkPath(D3D12Default, @"C:\app\AgilitySDK\", required: true));
        Assert.Null(NriDevice.ResolveAgilitySdkPath(new NriDeviceOptions { GraphicsApi = GraphicsAPI.D3D12, D3D12AgilitySdkPath = "" }, null, required: true));
        Assert.EndsWith("sdk" + Path.DirectorySeparatorChar,
            NriDevice.ResolveAgilitySdkPath(new NriDeviceOptions { GraphicsApi = GraphicsAPI.D3D12, D3D12AgilitySdkPath = "sdk" }, null, required: true), StringComparison.Ordinal);
        Assert.Null(NriDevice.ResolveAgilitySdkPath(new NriDeviceOptions { GraphicsApi = GraphicsAPI.VK }, null, required: true));
    }
}
