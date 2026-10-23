// Shared buffers, textures and fences (patches/0002: BufferDesc::shared, TextureDesc::shared,
// FenceDesc::shared, the Core getters and the feature bits): export each on each backend and check
// the handle is the kind the backend promises, that the device reports the features, and that NRI
// validation refuses the shapes the patch excludes. Importing elsewhere is a separate proof
// (SharedHandleImportTests, CudaInteropTests) because it needs the other API's binding.

using PrismNRI.Native;
using Xunit;

namespace PrismNRI.Tests;

public sealed class SharedHandleTests
{
    private static SharedHandleType ExpectedResourceType(GraphicsAPI api) => api switch
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
    /// Skips when the Vulkan device lacks the platform's external memory extension (MoltenVK has no
    /// fd handles, only VK_EXT_external_memory_metal; CLAUDE.md: MoltenVK and the paravirtual Mac).
    /// </summary>
    private static void RequireSharedResources(NriDevice device) =>
        Assert.SkipWhen(device.Desc.features.sharedResources == 0,
            "features.sharedResources is false: the adapter lacks VK_KHR_external_memory_fd/_win32");

    /// <summary>
    /// Skips when the Vulkan device lacks the platform's external semaphore extension: lavapipe (Mesa
    /// 25) advertises VK_KHR_external_memory_fd but not VK_KHR_external_semaphore_fd (CLAUDE.md:
    /// Lavapipe), MoltenVK neither.
    /// </summary>
    private static void RequireSharedFences(NriDevice device) =>
        Assert.SkipWhen(device.Desc.features.sharedFences == 0,
            "features.sharedFences is false: the adapter lacks VK_KHR_external_semaphore_fd/_win32 (lavapipe has no external semaphores; MoltenVK neither)");

    private static readonly TextureDesc SharedTexture = new()
    {
        type = TextureType.TEXTURE_2D, usage = TextureUsageBits.COLOR_ATTACHMENT | TextureUsageBits.SHADER_RESOURCE,
        format = Format.RGBA8_UNORM, width = 64, height = 64,
    };

    private static readonly BufferDesc SharedBuffer = new() { size = 4096, usage = BufferUsageBits.SHADER_RESOURCE };

    /// <summary>
    /// What each backend reports: D3D12 both features, NONE both (it claims every feature), Vulkan
    /// the extensions of the platform (recorded; on NVIDIA Windows both, on lavapipe memory only).
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void The_device_reports_the_shared_handle_features(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var features = device.Desc.features;
        GpuWork.Record($"{backend}: features.sharedResources {features.sharedResources}, features.sharedFences {features.sharedFences}");
        if (backend.Api != GraphicsAPI.VK)
        {
            Assert.Equal(1, features.sharedResources);
            Assert.Equal(1, features.sharedFences);
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_shared_buffer_exports_a_handle_of_the_backend_kind(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireSharedResources(device);

        using var buffer = device.CreateSharedBuffer(SharedBuffer);
        Assert.Equal(4096UL, buffer.Desc.size);
        Assert.Equal(1, buffer.Desc.shared);

        using var handle = buffer.GetSharedHandle();
        GpuWork.Record($"{backend}: buffer handle {handle}");
        Assert.True(handle.IsValid, $"handle is {handle.Handle}");
        Assert.Equal(ExpectedResourceType(backend.Api), handle.Type);
        Assert.True(handle.Size >= 4096, $"size {handle.Size}");

        // A second call also succeeds. Whether its value differs is the driver's business: NVIDIA's
        // Vulkan driver returned the same NT handle value for the same memory on 2026-10-13
        // (CLAUDE.md: Shared handles), so nothing here asserts on distinctness.
        using var second = buffer.GetSharedHandle();
        Assert.True(second.IsValid);

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_shared_texture_exports_a_handle_of_the_backend_kind(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireSharedResources(device);

        using var texture = device.CreateSharedTexture(SharedTexture);
        Assert.Equal(64, texture.Desc.width);
        Assert.Equal(1, texture.Desc.shared);

        using var handle = texture.GetSharedHandle();
        GpuWork.Record($"{backend}: texture handle {handle}");
        Assert.True(handle.IsValid, $"handle is {handle.Handle}");
        Assert.Equal(ExpectedResourceType(backend.Api), handle.Type);
        Assert.True(handle.Size >= 64 * 64 * 4, $"size {handle.Size}");

        using var second = texture.GetSharedHandle();
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
        RequireSharedFences(device);

        using var fence = device.CreateSharedFence(5);
        Assert.Equal(5UL, fence.Value);

        using var handle = fence.GetSharedHandle();
        Assert.True(handle.IsValid, $"handle is {handle.Handle}");
        Assert.Equal(ExpectedFenceType(backend.Api), handle.Type);
        Assert.Equal(0UL, handle.Size);

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        commandBuffer.Begin();
        commandBuffer.End();
        queue.Submit(commandBuffer, fence, 6);
        GpuWork.Wait(fence, 6);
        Assert.Equal(6UL, fence.Value);
        device.WaitIdle();

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The binary form (FenceDesc { SWAPCHAIN_SEMAPHORE, shared }): on Vulkan a binary semaphore
    /// that exports like the timeline one and is signalled and then waited by NRI's own queue; on
    /// D3D12, which has no binary fences, UNSUPPORTED without an error report. The import into
    /// another device is SharedHandleImportTests.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_shared_binary_fence_exports_on_Vulkan_and_is_unsupported_on_D3D12(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireSharedFences(device);

        if (backend.Api == GraphicsAPI.D3D12)
        {
            Assert.Throws<NriUnsupportedException>(() => device.CreateSharedBinaryFence().Dispose());
            Assert.Equal(0, device.LiveObjects);
            Assert.DoesNotContain(messages, m => m.IsError);
            return;
        }

        using var binary = device.CreateSharedBinaryFence();
        using (var handle = binary.GetSharedHandle())
        {
            Assert.True(handle.IsValid, $"handle is {handle.Handle}");
            Assert.Equal(ExpectedFenceType(backend.Api), handle.Type);
        }

        // Signal once, wait once, on NRI's queue; the timeline fence says the wait was satisfied.
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var fence = device.CreateFence();
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        commandBuffer.Begin();
        commandBuffer.End();
        queue.Submit([], [], [new NriFenceSubmit(binary, 0)]);
        queue.Submit([commandBuffer], [new NriFenceSubmit(binary, 0)], [new NriFenceSubmit(fence, 1)]);
        GpuWork.Wait(fence, 1);
        device.WaitIdle();

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    public static TheoryData<Backend, bool> GpuBackendsAndQueueSetups
    {
        get
        {
            var data = new TheoryData<Backend, bool>();
            foreach (var api in PrismNRI.Testing.NriTestDevice.GpuBackends)
            foreach (var threeFamilies in new[] { false, true })
                data.Add(new Backend(api), threeFamilies);
            return data;
        }
    }

    /// <summary>
    /// Release to the external queue family and acquire back. With one queue the Vulkan image is
    /// EXCLUSIVE (one active family); with graphics, compute and copy queues fetched it is
    /// CONCURRENT, where Vulkan wants VK_QUEUE_FAMILY_IGNORED on the side that is not external
    /// (NRI 2603edc writes that for ordinary queues and patch 0002 keeps the external side; NRI
    /// v190 wrote the queue's family index).
    /// </summary>
    [Theory]
    [MemberData(nameof(GpuBackendsAndQueueSetups))]
    public void A_shared_texture_can_be_released_to_and_acquired_from_the_external_queue_family(Backend backend, bool threeFamilies)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = threeFamilies
            ? TestDevice.Create(backend.Api, messages, b => b.QueueFamilies =
                [new NriQueueFamily(QueueType.GRAPHICS), new NriQueueFamily(QueueType.COMPUTE), new NriQueueFamily(QueueType.COPY)])
            : TestDevice.Create(backend.Api, messages);
        RequireSharedResources(device);
        device.GetQueue(QueueType.GRAPHICS); // before the texture: a CONCURRENT image allows the families active when it is created
        if (threeFamilies)
        {
            device.GetQueue(QueueType.COMPUTE);
            Assert.SkipWhen(device.Desc.adapterDesc.queueNum[(int)QueueType.COPY] == 0, $"{backend}: adapterDesc.queueNum[COPY] is 0, no COPY queue");
            device.GetQueue(QueueType.COPY); // more than one active family: the texture below is CONCURRENT on Vulkan
        }

        using var texture = device.CreateSharedTexture(SharedTexture);

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
        GpuWork.Wait(fence, 1);
        device.WaitIdle();

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// An ordinary buffer, texture and fence asked for a handle: INVALID_ARGUMENT ("'shared' is
    /// false": NRI validation for the resources, whose desc says so; the backend for the fence),
    /// nothing created, nothing leaked.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void An_object_that_is_not_shared_has_no_handle(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, SharedBuffer);
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, SharedTexture);
        using var fence = device.CreateFence();
        Assert.Equal(0, buffer.Desc.shared);
        Assert.Equal(0, texture.Desc.shared);

        foreach (var (what, get) in new (string, Func<NriSharedHandle>)[] { ("buffer", buffer.GetSharedHandle), ("texture", texture.GetSharedHandle), ("fence", fence.GetSharedHandle) })
        {
            var start = messages.Count;
            var e = Assert.Throws<NriException>(() => get().Dispose());
            Assert.Equal(Result.INVALID_ARGUMENT, e.Result);
            Assert.Contains(messages.Skip(start), m => m.IsError && m.Text.Contains("'shared' is false", StringComparison.Ordinal));
            GpuWork.Record($"{backend}: {what}: {e.Message}");
            lock (messages) messages.RemoveRange(start, messages.Count - start);
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The shapes the patch excludes, refused by NRI validation before any backend call: a shared
    /// resource created for placement (CreateBuffer / CreateTexture, whose memory could not carry
    /// the export info) and a committed one outside DEVICE memory (D3D12 cannot share an upload or
    /// readback heap; Vulkan's shared allocation is not mapped). Each answers INVALID_ARGUMENT and
    /// names the rule; nothing is created.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Validation_refuses_a_shared_resource_outside_a_committed_DEVICE_allocation(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        RequireSharedResources(device);

        var sharedBuffer = SharedBuffer;
        sharedBuffer.shared = 1;
        var sharedTexture = SharedTexture;
        sharedTexture.shared = 1;
        foreach (var (what, create, rule) in new (string, Action, string)[]
        {
            ("CreateBuffer", () => device.CreateBuffer(sharedBuffer).Dispose(), "'CreateCommittedBuffer'"),
            ("CreateTexture", () => device.CreateTexture(sharedTexture).Dispose(), "'CreateCommittedTexture'"),
            ("CreateCommittedBuffer in HOST_UPLOAD", () => device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, sharedBuffer).Dispose(), "'DEVICE' memory"),
            ("CreateCommittedBuffer in HOST_READBACK", () => device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, sharedBuffer).Dispose(), "'DEVICE' memory"),
        })
        {
            var start = messages.Count;
            var e = Assert.Throws<NriException>(create);
            Assert.Equal(Result.INVALID_ARGUMENT, e.Result);
            Assert.Contains(messages.Skip(start), m => m.IsError && m.Text.Contains(rule, StringComparison.Ordinal));
            GpuWork.Record($"{backend}: {what}: {e.Message}");
            lock (messages) messages.RemoveRange(start, messages.Count - start);
        }
        Assert.Equal(0, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
