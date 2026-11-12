// Device creation and the lifetime contract.
//
// The lifetime tests (disposal order, disposed objects, host-allocator accounting) run on NONE as
// well as the GPU backends: what they measure is the binding's bookkeeping, which NONE exercises
// without a GPU. Everything that asks NRI or its validation layer a question runs on VK and D3D12
// only, because NRI never wraps NONE in its validation layer and NONE answers success for every
// call (AGENTS.md: Device creation and callbacks).
//
// The creation options below the first block: queue priorities, wrapped devices' queue lists and
// their failure path, the explicit Agility SDK path, disabling Vulkan ray tracing, and how long the
// message and allocation bridges stay reachable.

using System.Runtime.CompilerServices;
using SharpNRI.Native;
using SharpNRI.Testing;
using Xunit;
using static SharpNRI.Tests.GpuWork;
using V12 = Vortice.Direct3D12;
using VD3D = Vortice.Direct3D;

namespace SharpNRI.Tests;

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

    // NRI's Wait is void and gives up after NRI_TIMEOUT_FENCE (5 s) with an ERROR message; the
    // binding turns that into an exception so no caller reads results that are not there yet
    // (AGENTS.md: CI runners and flakes, item 1). The fence here is never signalled.
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Fence_wait_throws_when_NRI_gives_up_before_the_value_is_reached(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        using var fence = device.CreateFence();

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var e = Assert.Throws<TimeoutException>(() => fence.Wait(1));
        clock.Stop();
        GpuWork.Record($"{backend}: NRI gave up after {clock.ElapsedMilliseconds} ms; {e.Message}");

        Assert.Equal(0UL, fence.Value);
        Assert.InRange(clock.ElapsedMilliseconds, 4000, 60_000);
        Assert.Contains(messages, m => m.IsError);
        device.WaitIdle();
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
        // for any index because it answers SUCCESS for everything (AGENTS.md: Device creation and callbacks).
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

    // ---------------------------------------------------------------- queue priorities

    /// <summary>
    /// Queue priorities reach the D3D12 command queues NRI creates: 1 and 0.25 make HIGH queues, 0
    /// and -1 NORMAL ones (D3D12 has no low priority; "high > 0" as NRIDeviceCreation.h says, which
    /// v191 broke with HIGH only above 0.5, #321), read back from each ID3D12CommandQueue's desc, in
    /// family and index order.
    /// </summary>
    [Fact]
    public void Queue_priorities_reach_the_D3D12_command_queues()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.D3D12));
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.D3D12, messages, b => b.QueueFamilies =
        [
            new NriQueueFamily(QueueType.GRAPHICS, 2, [1f, 0f]),
            new NriQueueFamily(QueueType.COMPUTE, 2, [-1f, 0.25f]),
        ]);

        int Priority(QueueType type, uint index)
        {
            var native = device.GetQueue(type, index).GetNativeObject();
            System.Runtime.InteropServices.Marshal.AddRef(native); // released by the Vortice wrapper's Dispose
            using var queue = new V12.ID3D12CommandQueue(native);
            return queue.GetDescription().Priority;
        }

        Assert.Equal((int)V12.CommandQueuePriority.High, Priority(QueueType.GRAPHICS, 0));
        Assert.Equal((int)V12.CommandQueuePriority.Normal, Priority(QueueType.GRAPHICS, 1));
        Assert.Equal((int)V12.CommandQueuePriority.Normal, Priority(QueueType.COMPUTE, 0));
        Assert.Equal((int)V12.CommandQueuePriority.High, Priority(QueueType.COMPUTE, 1));
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Queue priorities reach vkCreateDevice: a priority of 1 is clean, and one outside Vulkan's [0, 1]
    /// (2, outside NRI's documented [-1, 1] too, as a positive control) draws the Khronos layer's
    /// VUID-VkDeviceQueueCreateInfo-pQueuePriorities-00383 at device creation, which only the
    /// delivered value can cause. Vulkan has no call that reads a queue's priority back.
    /// </summary>
    [Fact]
    public void Queue_priorities_reach_vkCreateDevice()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        var valid = new List<NriMessage>();
        using (TestDevice.Create(GraphicsAPI.VK, valid, b => b.QueueFamilies = [new NriQueueFamily(QueueType.GRAPHICS, 1, [1f])]))
        {
        }
        var invalid = new List<NriMessage>();
        using (TestDevice.Create(GraphicsAPI.VK, invalid, b => b.QueueFamilies = [new NriQueueFamily(QueueType.GRAPHICS, 1, [2f])]))
        {
        }
        foreach (var m in invalid.Where(m => m.IsError)) Record("priority 2: " + m);

        Assert.DoesNotContain(valid, m => m.IsError);
        Assert.Contains(invalid, m => m.IsError && m.Text.Contains("VUID-VkDeviceQueueCreateInfo-pQueuePriorities-00383", StringComparison.Ordinal));
    }

    /// <summary>
    /// A priority list whose length is not the family's queue count is refused with ArgumentException
    /// before nriCreateDevice: no message arrives and the host allocator sees nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_priority_list_of_the_wrong_length_is_refused_before_NRI_is_called(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        var allocator = new NriCountingAllocator();
        var e = Assert.Throws<ArgumentException>(() => NriDevice.Create(new NriDeviceOptions
        {
            GraphicsApi = backend.Api,
            Adapter = TestDevice.AdapterFor(backend.Api),
            QueueFamilies = [new NriQueueFamily(QueueType.GRAPHICS, 1), new NriQueueFamily(QueueType.COMPUTE, 2, [1f])],
            Allocator = allocator,
            MessageHandler = (in NriMessage m) => { lock (messages) messages.Add(m); },
        }));

        Assert.Equal("options", e.ParamName);
        Assert.Contains("QueueFamilies[1]: 1 priorities for 2 queues", e.Message, StringComparison.Ordinal);
        Assert.Empty(messages);
        Assert.Equal(0L, allocator.Allocations);
    }

    // ---------------------------------------------------------------- wrapped devices

    /// <summary>
    /// A device wrapped around a raw ID3D12Device with the caller's command queues submits on exactly
    /// those queues: GetQueueNativeObject returns the queue handed in for each family, and a copy
    /// submitted on the wrapped GRAPHICS queue lands.
    /// </summary>
    [Fact]
    public void A_wrapped_D3D12_device_uses_the_command_queues_it_is_given()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.D3D12));
        using var adapter = WrapperExtensionTests.DxgiAdapterFor(GraphicsAPI.D3D12);
        var hr = V12.D3D12.D3D12CreateDevice(adapter, VD3D.FeatureLevel.Level_12_0, out V12.ID3D12Device? d3d);
        Assert.True(hr.Success, $"D3D12CreateDevice: {hr}");
        using var _ = d3d;
        using var direct = d3d!.CreateCommandQueue(new V12.CommandQueueDescription(V12.CommandListType.Direct));
        using var copy = d3d.CreateCommandQueue(new V12.CommandQueueDescription(V12.CommandListType.Copy));

        var messages = new List<NriMessage>();
        using (var device = NriDevice.CreateFromD3D12(new NriD3D12DeviceOptions
        {
            D3D12Device = d3d.NativePointer,
            QueueFamilies = [new NriQueueFamilyD3D12Desc([direct.NativePointer], 1, QueueType.GRAPHICS), new NriQueueFamilyD3D12Desc([copy.NativePointer], 1, QueueType.COPY)],
            EnableNriValidation = true,
            MessageHandler = (in NriMessage m) => { lock (messages) messages.Add(m); },
        }))
        {
            Assert.Equal(direct.NativePointer, device.GetQueue(QueueType.GRAPHICS).GetNativeObject());
            Assert.Equal(copy.NativePointer, device.GetQueue(QueueType.COPY).GetNativeObject());

            using var upload = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 256 });
            using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = 256 });
            upload.Map().Fill(0x6E);
            upload.Unmap();
            Run(device, cmd =>
            {
                cmd.Barrier([], [new NriBufferBarrier(upload, Nothing, CopySource), new NriBufferBarrier(readback, Nothing, CopyDestination)], []);
                cmd.CopyBuffer(readback, 0, upload, 0, 256);
            });
            var bytes = readback.Map().ToArray();
            readback.Unmap();
            Assert.True(bytes.All(b => b == 0x6E), $"readback starts {Convert.ToHexString(bytes, 0, 16)}, expected 6E throughout");
            device.WaitIdle();
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// A wrapped D3D12 queue list whose length is not its queueNum is refused with ArgumentException
    /// before nriCreateDeviceFromD3D12Device (NRI would read queueNum pointers): no message, nothing
    /// allocated. The device pointer is null, so a check that went missing would surface as NRI's
    /// INVALID_ARGUMENT, not as a read past the array.
    /// </summary>
    [Fact]
    public void A_wrapped_D3D12_queue_list_of_the_wrong_length_is_refused_before_NRI_is_called()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "D3D12 is Windows only");
        var messages = new List<NriMessage>();
        var allocator = new NriCountingAllocator();
        var e = Assert.Throws<ArgumentException>(() => NriDevice.CreateFromD3D12(new NriD3D12DeviceOptions
        {
            D3D12Device = 0,
            QueueFamilies = [new NriQueueFamilyD3D12Desc([0], 2, QueueType.GRAPHICS)],
            Allocator = allocator,
            MessageHandler = (in NriMessage m) => { lock (messages) messages.Add(m); },
        }));

        Assert.Equal("options", e.ParamName);
        Assert.Contains("QueueFamilies[0]: 1 queues for queueNum 2", e.Message, StringComparison.Ordinal);
        Assert.Empty(messages);
        Assert.Equal(0L, allocator.Allocations);
    }

    public static TheoryData<string> WrappedApis => OperatingSystem.IsWindows() ? new TheoryData<string>("VK", "D3D12") : new TheoryData<string>("VK");

    /// <summary>
    /// A wrapped device NRI refuses (a null VkPhysicalDevice, a null ID3D12Device: INVALID_ARGUMENT
    /// before NRI touches anything, Creation.cpp) throws NriException naming the call, and the
    /// message and allocation bridges are released on the way out: after a full collection neither
    /// the host allocator nor the message handler is still reachable. (While a device lives the same
    /// objects stay reachable through the bridges; see the next test.) No GPU is involved.
    /// </summary>
    [Theory]
    [MemberData(nameof(WrappedApis))]
    public void A_wrapped_device_NRI_refuses_throws_and_releases_its_callbacks(string api)
    {
        var (allocator, handler, e) = RefusedWrappedDevice(api);
        Collect();

        Assert.Equal(Result.INVALID_ARGUMENT, e.Result);
        Assert.Equal(api == "VK" ? "nriCreateDeviceFromVKDevice" : "nriCreateDeviceFromD3D12Device", e.Call);
        Assert.False(allocator.IsAlive, "the host allocator is still reachable after the refused creation");
        Assert.False(handler.IsAlive, "the message handler is still reachable after the refused creation");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Allocator, WeakReference Handler, NriException Error) RefusedWrappedDevice(string api)
    {
        var allocator = new NriCountingAllocator();
        var seen = new List<NriMessage>(); // captured, so the handler is a new delegate (a non-capturing lambda is cached in a static field)
        NriMessageHandler handler = (in NriMessage m) => { lock (seen) seen.Add(m); };
        var e = Assert.Throws<NriException>(() =>
        {
            if (api == "VK")
            {
                NriDevice.CreateFromVulkan(new NriVulkanDeviceOptions
                {
                    VkInstance = 0, VkDevice = 0, VkPhysicalDevice = 0,
                    Allocator = allocator, MessageHandler = handler, EnableNriValidation = true,
                });
            }
            else
            {
                NriDevice.CreateFromD3D12(new NriD3D12DeviceOptions
                {
                    D3D12Device = 0,
                    QueueFamilies = [new NriQueueFamilyD3D12Desc([0], 1, QueueType.GRAPHICS)],
                    Allocator = allocator, MessageHandler = handler, EnableNriValidation = true,
                });
            }
        });
        return (new WeakReference(allocator), new WeakReference(handler), e);
    }

    /// <summary>
    /// The message and allocation bridges root what NRI can call back into for exactly as long as the
    /// native device lives: with the device undisposed and no managed reference left to it, a full
    /// collection keeps the device wrapper, the host allocator and the message handler (NRI may still
    /// call them); after Dispose the same collection frees all three.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void The_callbacks_stay_reachable_until_the_device_is_disposed(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var (device, allocator, handler) = UnreferencedDevice(backend.Api);
        Collect();
        Assert.True(device.IsAlive, "the undisposed device was collected");
        Assert.True(allocator.IsAlive, "the host allocator of an undisposed device was collected");
        Assert.True(handler.IsAlive, "the message handler of an undisposed device was collected");

        DisposeTarget(device);
        Collect();
        Assert.False(device.IsAlive, "the disposed device is still reachable");
        Assert.False(allocator.IsAlive, "the host allocator is still reachable after Dispose");
        Assert.False(handler.IsAlive, "the message handler is still reachable after Dispose");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Device, WeakReference Allocator, WeakReference Handler) UnreferencedDevice(GraphicsAPI api)
    {
        var allocator = new NriCountingAllocator();
        var seen = new List<NriMessage>(); // captured, so the handler is a new delegate (a non-capturing lambda is cached in a static field)
        NriMessageHandler handler = (in NriMessage m) => { lock (seen) seen.Add(m); };
        var device = NriDevice.Create(new NriDeviceOptions
        {
            GraphicsApi = api,
            Adapter = TestDevice.AdapterFor(api),
            Allocator = allocator,
            MessageHandler = handler,
            EnableNriValidation = true,
        });
        return (new WeakReference(device), new WeakReference(allocator), new WeakReference(handler));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DisposeTarget(WeakReference device) => ((NriDevice)device.Target!).Dispose();

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    // ---------------------------------------------------------------- Agility SDK and Vulkan ray tracing

    /// <summary>
    /// An explicit Agility SDK directory without a trailing separator gets one before NRI sees it:
    /// NRI's report "Using Agility SDK v1.N from '...'" (patch 0001) names the directory with the
    /// separator, and a path that already has one is passed as it is. The directory is the staged SDK
    /// the other D3D12 tests use, so the process-wide SDK does not change.
    /// </summary>
    [Fact]
    public void An_explicit_Agility_SDK_path_gets_a_trailing_separator()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.D3D12));
        var staged = NriDevice.DefaultAgilitySdkPath;
        Assert.SkipWhen(staged is null, "no Agility SDK staged beside the assembly");
        var bare = staged!.TrimEnd(Path.DirectorySeparatorChar);
        Assert.False(bare.EndsWith(Path.DirectorySeparatorChar));

        string Report(string path)
        {
            var messages = new List<NriMessage>();
            using var device = NriDevice.Create(new NriDeviceOptions
            {
                GraphicsApi = GraphicsAPI.D3D12,
                Adapter = TestDevice.AdapterFor(GraphicsAPI.D3D12),
                D3D12AgilitySdkPath = path,
                EnableNriValidation = true,
                EnableGraphicsApiValidation = true,
                MessageHandler = (in NriMessage m) => { lock (messages) messages.Add(m); },
            });
            Assert.DoesNotContain(messages, m => m.IsError);
            return Assert.Single(messages, m => m.Text.Contains("Using Agility SDK v1.", StringComparison.Ordinal)).Text;
        }

        var fromBare = Report(bare);
        var fromStaged = Report(staged);
        Record("bare: " + fromBare);
        Assert.EndsWith($"from '{staged}'", fromBare, StringComparison.Ordinal);
        Assert.EndsWith($"from '{staged}'", fromStaged, StringComparison.Ordinal);
    }

    /// <summary>
    /// DisableVkRayTracing leaves the ray-tracing extensions out of the Vulkan device: tiers.rayTracing
    /// is 0, TryRayTracing is null (nriGetInterface answers UNSUPPORTED, with NRI's warning
    /// "Interface 'RayTracingInterface' is not supported by the device!"), and RayTracing throws
    /// NriUnsupportedException. The same adapter without the switch has the extension.
    /// </summary>
    [Fact]
    public void Disabling_Vulkan_ray_tracing_leaves_no_RayTracing_extension()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        using (var control = NriTestDevice.Create(GraphicsAPI.VK))
        {
            Assert.SkipWhen(control.Device.Desc.tiers.rayTracing == 0, "tiers.rayTracing = 0 without the switch: nothing to disable");
            Assert.NotNull(control.Device.TryRayTracing);
        }

        var messages = new List<NriMessage>();
        using var device = NriDevice.Create(new NriDeviceOptions
        {
            GraphicsApi = GraphicsAPI.VK,
            Adapter = TestDevice.AdapterFor(GraphicsAPI.VK),
            DisableVkRayTracing = true,
            EnableNriValidation = true,
            EnableGraphicsApiValidation = true,
            MessageHandler = (in NriMessage m) => { lock (messages) messages.Add(m); },
        });

        Assert.Equal(0, device.Desc.tiers.rayTracing);
        Assert.Null(device.TryRayTracing);
        var e = Assert.Throws<NriUnsupportedException>(() => device.RayTracing);
        Assert.Equal("nriGetInterface(RayTracingInterface)", e.Call);
        Assert.Contains(messages, m => m.IsWarning && m.Text.Contains("Interface 'RayTracingInterface' is not supported by the device!", StringComparison.Ordinal));
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
