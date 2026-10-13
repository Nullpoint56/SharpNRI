// The SharedHandle extension (patches/0002): export a texture and a fence on each backend and
// check the handle is the kind the backend promises. Importing it elsewhere is a separate proof
// (SharedHandleImportTests) because it needs the other API's binding.

using PrismNRI.Native;
using Xunit;

namespace PrismNRI.Tests;

public sealed class SharedHandleTests
{
    private static SharedHandleType ExpectedTextureType(GraphicsAPI api) => api switch
    {
        GraphicsAPI.D3D12 => SharedHandleType.D3D12_RESOURCE,
        GraphicsAPI.VK => OperatingSystem.IsWindows() ? SharedHandleType.OPAQUE_WIN32 : SharedHandleType.OPAQUE_FD,
        _ => SharedHandleType.NONE,
    };

    private static SharedHandleType ExpectedFenceType(GraphicsAPI api) => api switch
    {
        GraphicsAPI.D3D12 => SharedHandleType.D3D12_FENCE,
        GraphicsAPI.VK => OperatingSystem.IsWindows() ? SharedHandleType.OPAQUE_WIN32 : SharedHandleType.OPAQUE_FD,
        _ => SharedHandleType.NONE,
    };

    /// <summary>
    /// Skips when NRI's Vulkan backend could not fill the interface. It needs both the external
    /// memory and the external semaphore extension of the platform; lavapipe (Mesa 25) advertises
    /// VK_KHR_external_memory_fd but not VK_KHR_external_semaphore_fd (docs/findings.md 8.2).
    /// </summary>
    private static void RequireSharedHandles(NriDevice device) =>
        Assert.SkipWhen(device.GraphicsApi == GraphicsAPI.VK && device.TrySharedHandles is null,
            "the adapter lacks VK_KHR_external_memory_* or VK_KHR_external_semaphore_* (lavapipe has no external semaphores)");

    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void The_extension_is_available(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireSharedHandles(device);

        Assert.NotNull(device.TrySharedHandles);
        Assert.Same(device.SharedHandles, device.TrySharedHandles);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_shared_texture_exports_a_handle_of_the_backend_kind(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireSharedHandles(device);

        using var texture = device.SharedHandles.CreateSharedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.COLOR_ATTACHMENT | TextureUsageBits.SHADER_RESOURCE,
            format = Format.RGBA8_UNORM, width = 64, height = 64,
        });
        Assert.Equal(64, texture.Desc.width);

        using var handle = device.SharedHandles.GetSharedHandle(texture);
        Assert.True(handle.IsValid, $"handle is {handle.Handle}");
        Assert.Equal(ExpectedTextureType(backend.Api), handle.Type);
        if (backend.Api == GraphicsAPI.VK) Assert.True(handle.Size >= 64 * 64 * 4, $"size {handle.Size}");
        Assert.Equal(0UL, handle.Offset);

        // A second call also succeeds. Whether its value differs is the driver's business: NVIDIA's
        // Vulkan driver returned the same NT handle value for the same memory on 2026-10-13
        // (docs/findings.md §7.4), so nothing here asserts on distinctness.
        using var second = device.SharedHandles.GetSharedHandle(texture);
        Assert.True(second.IsValid);

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_shared_fence_exports_a_handle_and_still_works_as_a_fence(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireSharedHandles(device);

        using var fence = device.SharedHandles.CreateSharedFence(5);
        Assert.Equal(5UL, fence.Value);

        using var handle = device.SharedHandles.GetSharedHandle(fence);
        Assert.True(handle.IsValid, $"handle is {handle.Handle}");
        Assert.Equal(ExpectedFenceType(backend.Api), handle.Type);

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        commandBuffer.Begin();
        commandBuffer.End();
        queue.Submit(commandBuffer, fence, 6);
        fence.Wait(6);
        Assert.Equal(6UL, fence.Value);
        device.WaitIdle();

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_shared_texture_can_be_released_to_and_acquired_from_the_external_queue_family(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireSharedHandles(device);

        using var texture = device.SharedHandles.CreateSharedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.COLOR_ATTACHMENT | TextureUsageBits.SHADER_RESOURCE,
            format = Format.RGBA8_UNORM, width = 64, height = 64,
        });

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        using var fence = device.CreateFence();

        // Release to the external family (VK_QUEUE_FAMILY_EXTERNAL on Vulkan; D3D12 ignores the
        // queues), then acquire back, through NRI's validation layer and the backend's.
        var attachment = new AccessLayoutStage { access = AccessBits.COLOR_ATTACHMENT, layout = Layout.COLOR_ATTACHMENT, stages = StageBits.COLOR_ATTACHMENT };
        var general = new AccessLayoutStage { access = AccessBits.COPY_SOURCE, layout = Layout.COPY_SOURCE, stages = StageBits.COPY }; // the handed-off state: a real access, so both backends accept the release and the acquire
        commandBuffer.Begin();
        commandBuffer.Barrier(new NriTextureBarrier(texture, default, attachment));
        commandBuffer.Barrier(new NriTextureBarrier(texture, attachment, general, SrcQueue: queue, DstQueue: NriQueue.External));
        commandBuffer.Barrier(new NriTextureBarrier(texture, general, attachment, SrcQueue: NriQueue.External, DstQueue: queue));
        commandBuffer.End();
        queue.Submit(commandBuffer, fence, 1);
        fence.Wait(1);
        device.WaitIdle();

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void An_ordinary_texture_has_no_handle(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireSharedHandles(device);

        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.RGBA8_UNORM, width = 4, height = 4,
        });
        var e = Assert.Throws<NriException>(() => device.SharedHandles.GetSharedHandle(texture));
        Assert.Equal(Result.INVALID_ARGUMENT, e.Result);
    }
}
