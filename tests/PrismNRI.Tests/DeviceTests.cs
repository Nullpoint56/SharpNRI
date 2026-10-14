// Device creation and the lifetime contract.
//
// The lifetime tests (disposal order, disposed objects, host-allocator accounting) run on NONE as
// well as the GPU backends: what they measure is the binding's bookkeeping, which NONE exercises
// without a GPU. Everything that asks NRI or its validation layer a question runs on VK and D3D12
// only, because NRI never wraps NONE in its validation layer and NONE answers success for every
// call (CLAUDE.md: Device creation and callbacks).

using PrismNRI.Native;
using Xunit;

namespace PrismNRI.Tests;

public sealed class DeviceTests
{
    [Fact]
    public void Adapters_enumerate_without_a_device()
    {
        var adapters = NriAdapter.Enumerate();
        foreach (var a in adapters)
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Name), "an adapter with no name");
            Assert.NotEqual(default, a.SupportedGraphicsApis);
        }
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Device_reports_the_pinned_NRI_version(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        Assert.Equal(NriHeader.PinnedVersion, device.NriVersion);
        Assert.Equal(backend.Api, device.GraphicsApi);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Core_objects_create_and_dispose_cleanly(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        using var fence = device.CreateFence();
        using var buffer = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 256, usage = BufferUsageBits.NONE });
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.RGBA8_UNORM,
            width = 4, height = 4, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
        });
        using var view = texture.CreateView(TextureView.TEXTURE, Format.RGBA8_UNORM);
        using var sampler = device.CreateSampler(new SamplerDesc());
        using var queryPool = device.CreateQueryPool(QueryType.TIMESTAMP, 2);

        Assert.Equal(8, device.LiveObjects);
        Assert.Equal(0UL, fence.Value);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void Disposing_the_device_with_live_objects_throws_instead_of_corrupting(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        var device = TestDevice.Create(backend.Api, messages);
        var fence = device.CreateFence();

        var e = Assert.Throws<InvalidOperationException>(device.Dispose);
        Assert.Contains("1 object", e.Message);
        Assert.False(device.IsDisposed);

        fence.Dispose();
        device.Dispose();
        Assert.True(device.IsDisposed);
    }

    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void A_disposed_object_throws_rather_than_dangling(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var fence = device.CreateFence();
        fence.Dispose();
        fence.Dispose();

        Assert.Throws<ObjectDisposedException>(() => fence.Value);
        Assert.Equal(0, device.LiveObjects);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Fence_signals_through_a_queue_and_is_waited_on_the_host(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        using var fence = device.CreateFence();

        commandBuffer.Begin();
        commandBuffer.End();
        queue.Submit(commandBuffer, fence, 1);
        fence.Wait(1);

        Assert.Equal(1UL, fence.Value);
        device.WaitIdle();
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Fact]
    public void Creating_on_an_unknown_backend_fails_with_the_result_and_the_message()
    {
        var messages = new List<NriMessage>();
        var e = Assert.Throws<NriException>(() => NriDevice.Create(new NriDeviceOptions
        {
            GraphicsApi = GraphicsAPI.WGPU,
            MessageHandler = (in NriMessage m) => messages.Add(m),
        }));
        Assert.Equal("nriCreateDevice", e.Call);
        Assert.NotEqual(Result.SUCCESS, e.Result);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void GetQueue_out_of_range_is_INVALID_ARGUMENT(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        // NRI.h documents GetQueue: INVALID_ARGUMENT when the index is out of bounds. Measured:
        // the result comes back without a message through the callback, and NONE answers SUCCESS
        // for any index because it answers SUCCESS for everything (CLAUDE.md: Device creation and callbacks).
        var e = Assert.Throws<NriException>(() => device.GetQueue(QueueType.GRAPHICS, 1000));
        Assert.Equal(Result.INVALID_ARGUMENT, e.Result);
        Assert.Equal("GetQueue", e.Call);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Messages_arrive_through_the_handler(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        // Both real backends report at least one INFO line while creating a device (the adapter
        // and the API level picked), which is enough to prove the UnmanagedCallersOnly bridge
        // delivers and decodes UTF-8 without a contrived error.
        Assert.NotEmpty(messages);
        Assert.All(messages, m => Assert.False(string.IsNullOrEmpty(m.Text)));
    }

    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void A_host_allocator_sees_NRI_allocations(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        var allocator = new NriCountingAllocator();
        using var device = TestDevice.Create(backend.Api, messages, b => b.Allocator = allocator);

        Assert.Same(allocator, device.Allocator);
        Assert.True(allocator.Allocations > 0, "device creation allocated nothing through the host allocator");
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
