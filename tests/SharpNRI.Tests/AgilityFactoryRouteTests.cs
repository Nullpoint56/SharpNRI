// Measurement: can the Agility SDK reach NRI without patch 0001, through NRI's own wrapped-device
// route? The host creates the ID3D12Device from an Agility device factory (Microsoft's
// "independent devices" API, which needs no exports and no Developer Mode) and hands it to
// nriCreateDeviceFromD3D12Device. NRI already serializes root signatures through
// ID3D12DeviceConfiguration for such devices (PipelineLayoutD3D12.hpp), so the route is one NRI
// supports on purpose.
//
// The three COM calls are made through their vtables directly rather than through Vortice, because
// that is what the binding's own L1 would do if this route replaces the patch: no D3D12 binding
// dependency, four slots, two IIDs. Slot numbers and IIDs are from the Agility SDK's d3d12.h.
//
// Deliberately does not touch TestDevice: its availability probe creates a D3D12 device through
// patch 0001, which applies the SDK to the whole process and would contaminate this measurement.
// Run alone: SharpNRI.Tests.exe -class SharpNRI.Tests.AgilityFactoryRouteTests

using System.Runtime.InteropServices;
using System.Text;
using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed unsafe class AgilityFactoryRouteTests(ITestOutputHelper output)
{
    private static readonly Guid ClsidD3D12SDKConfiguration = new("7cda6aca-a03e-49c8-9458-0334d20e07ce");
    private static readonly Guid IidD3D12SDKConfiguration1 = new("8aaf9303-ad25-48b9-9a57-d9c37e009d9f");
    private static readonly Guid IidD3D12DeviceFactory = new("61f307d3-d34e-4e7c-8374-3ba4de23cccb");
    private static readonly Guid IidD3D12Device = new("189819f1-1db6-4b57-be54-1821339b85f7");
    private const int FeatureLevel12_0 = 0xc000;

    [DllImport("d3d12", ExactSpelling = true)]
    private static extern int D3D12GetInterface(Guid* rclsid, Guid* riid, void** ppv);

    private static void Release(void* unknown) => ((delegate* unmanaged[Stdcall]<void*, uint>)(*(void***)unknown)[2])(unknown);

    [Fact]
    public void A_device_from_an_Agility_device_factory_wrapped_into_NRI_uses_the_SDK()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "D3D12 is Windows only");
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.D3D12));
        var sdkPath = NriDevice.DefaultAgilitySdkPath;
        Assert.SkipWhen(sdkPath is null, "no AgilitySDK directory beside the test assembly");

        // The SDK version NRI was compiled against: the staged D3D12Core.dll is that exact version,
        // which CreateDeviceFactory demands.
        const uint sdkVersion = 619;

        void* configuration = null;
        var clsid = ClsidD3D12SDKConfiguration;
        var iidConfiguration = IidD3D12SDKConfiguration1;
        var hr = D3D12GetInterface(&clsid, &iidConfiguration, &configuration);
        Assert.True(hr >= 0, $"D3D12GetInterface(CLSID_D3D12SDKConfiguration): 0x{hr:X8}");

        void* factory = null;
        try
        {
            // ID3D12SDKConfiguration1 vtable: 3 SetSDKVersion, 4 CreateDeviceFactory, 5 FreeUnusedSDKs
            var path = Encoding.UTF8.GetBytes(sdkPath! + "\0");
            var iidFactory = IidD3D12DeviceFactory;
            fixed (byte* p = path)
            {
                hr = ((delegate* unmanaged[Stdcall]<void*, uint, byte*, Guid*, void**, int>)(*(void***)configuration)[4])(configuration, sdkVersion, p, &iidFactory, &factory);
            }
            Assert.True(hr >= 0, $"ID3D12SDKConfiguration1::CreateDeviceFactory({sdkVersion}, {sdkPath}): 0x{hr:X8}");
        }
        finally
        {
            Release(configuration);
        }

        void* d3d12 = null;
        try
        {
            // The suite's D3D12 adapter, by LUID, not whichever adapter drives the display
            using (var adapter = WrapperExtensionTests.DxgiAdapterFor(GraphicsAPI.D3D12))
            {
                output.WriteLine($"adapter: {adapter.Description1.Description}");

                // ID3D12DeviceFactory vtable: 3 InitializeFromGlobalState, 4 ApplyToGlobalState, 5 SetFlags,
                // 6 GetFlags, 7 GetConfigurationInterface, 8 EnableExperimentalFeatures, 9 CreateDevice
                var iidDevice = IidD3D12Device;
                hr = ((delegate* unmanaged[Stdcall]<void*, void*, int, Guid*, void**, int>)(*(void***)factory)[9])(factory, (void*)adapter.NativePointer, FeatureLevel12_0, &iidDevice, &d3d12);
            }
            Assert.True(hr >= 0, $"ID3D12DeviceFactory::CreateDevice: 0x{hr:X8}");
        }
        finally
        {
            Release(factory);
        }

        try
        {
            var messages = new List<NriMessage>();
            using var device = NriDevice.CreateFromD3D12(new NriD3D12DeviceOptions
            {
                D3D12Device = (nint)d3d12,
                QueueFamilies = [new NriQueueFamilyD3D12Desc(null, 1, QueueType.GRAPHICS)],
                EnableNriValidation = true,
                MessageHandler = (in NriMessage m) => { messages.Add(m); output.WriteLine(m.ToString()); },
            });
            Assert.Equal(GraphicsAPI.D3D12, device.GraphicsApi);

            // The oracle: NRI reports the device interface version it found, and warns about
            // OPTIONS22 only on the inbox runtime (AGENTS.md: D3D12 and the Agility SDK). On Agility 1.619 it found
            // ID3D12Device15 through patch 0001; the same here means the SDK is in use.
            var versionLine = messages.FirstOrDefault(m => m.Text.Contains("Using ID3D12Device", StringComparison.Ordinal));
            Assert.True(versionLine.Text is not null, "NRI did not report the device interface version");
            Assert.Contains("ID3D12Device15", versionLine.Text);
            Assert.DoesNotContain(messages, m => m.Text.Contains("options22", StringComparison.OrdinalIgnoreCase));

            // And a round trip through the wrapped device: a buffer, a view, a fence.
            using var buffer = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 256, usage = BufferUsageBits.CONSTANT });
            using var view = buffer.CreateView(BufferView.CONSTANT_BUFFER);
            using var fence = device.CreateFence();
            Assert.DoesNotContain(messages, m => m.IsError);
        }
        finally
        {
            Release(d3d12);
        }
    }
}
