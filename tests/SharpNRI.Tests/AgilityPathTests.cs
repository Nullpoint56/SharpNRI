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

    /// <summary>
    /// D3D12 API validation on the Agility SDK needs the debug layer in the SDK's own directory: without
    /// AgilitySDK\d3d12SDKLayers.dll NRI's D3D12GetDebugInterface fails and the device runs unvalidated
    /// without a word, also with the system's Graphics Tools layer installed (measured 2026-11-07,
    /// D3D12DebugLayerTests: "no ID3D12InfoQueue"). SharpNRI.AgilitySDK copies the layer in Debug only,
    /// so a Release build asking for validation is refused, naming the switch.
    /// </summary>
    [Fact]
    public void D3D12_API_validation_without_the_Agility_debug_layer_is_refused()
    {
        var dir = Directory.CreateTempSubdirectory("sharpnri-agility-").FullName + Path.DirectorySeparatorChar;
        try
        {
            var validated = new NriDeviceOptions { GraphicsApi = GraphicsAPI.D3D12, EnableGraphicsApiValidation = true };
            var e = Assert.Throws<NriUnsupportedException>(() => NriDevice.RequireDebugLayer(validated, dir));
            Assert.Contains(Path.Combine(dir, "d3d12SDKLayers.dll"), e.Message, StringComparison.Ordinal);
            Assert.Contains("SharpNRIAgilityDebugLayer", e.Message, StringComparison.Ordinal);

            // not validated, the inbox runtime, another backend: nothing to refuse
            NriDevice.RequireDebugLayer(new NriDeviceOptions { GraphicsApi = GraphicsAPI.D3D12 }, dir);
            NriDevice.RequireDebugLayer(validated, null);
            NriDevice.RequireDebugLayer(new NriDeviceOptions { GraphicsApi = GraphicsAPI.VK, EnableGraphicsApiValidation = true }, dir);

            File.WriteAllBytes(Path.Combine(dir, "d3d12SDKLayers.dll"), []);
            NriDevice.RequireDebugLayer(validated, dir);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Device creation applies the refusal before NRI is called: no Agility SDK is applied to the process.</summary>
    [Fact]
    public void A_validated_D3D12_device_on_an_Agility_SDK_without_its_debug_layer_is_not_created()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("D3D12: Windows only");
        var staged = NriDevice.DefaultAgilitySdkPath;
        if (staged is null) Assert.Skip($"no AgilitySDK{Path.DirectorySeparatorChar}D3D12Core.dll beside the tests (build-native.ps1 stages it)");

        var dir = Directory.CreateTempSubdirectory("sharpnri-agility-").FullName + Path.DirectorySeparatorChar;
        try
        {
            File.Copy(Path.Combine(staged, "D3D12Core.dll"), Path.Combine(dir, "D3D12Core.dll"));
            var e = Assert.Throws<NriUnsupportedException>(() => NriDevice.Create(new NriDeviceOptions
            {
                GraphicsApi = GraphicsAPI.D3D12,
                D3D12AgilitySdkPath = dir,
                EnableGraphicsApiValidation = true,
            }).Dispose());
            Assert.Equal("nriCreateDevice", e.Call);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
