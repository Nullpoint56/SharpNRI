// Debugging aids and escape hatches: annotations, debug names, native objects, NRI handles, adapter
// facts, the memory budget, QueueWaitIdle and NriTestDevice.Warnings.
//
// What proves an annotation or a name reached the backend is the backend's own validation: the
// Khronos layer and the D3D12 debug layer count label regions and print object names in their
// messages, and on D3D12 a name is also read back from the object (WKPDID_D3DDebugObjectName, which
// NRI's SetDebugName writes). NRI validation counts command-buffer annotations itself. Measured on
// the development machine's discrete GPU (Vulkan layer 1.4.3xx, Agility SDK 1.619) and WARP.

using System.Runtime.InteropServices;
using System.Text;
using SharpNRI.Native;
using SharpNRI.Testing;
using Vortice.Vulkan;
using Xunit;
using static SharpNRI.Tests.GpuWork;
using Architecture = SharpNRI.Native.Architecture;
using NativeBuffer = SharpNRI.Native.Buffer;
using NativeCommandBuffer = SharpNRI.Native.CommandBuffer;
using NativeDevice = SharpNRI.Native.Device;
using NativeQueue = SharpNRI.Native.Queue;
using V12 = Vortice.Direct3D12;

namespace SharpNRI.Tests;

public sealed unsafe class DebugAidTests
{
    private const uint Bytes = 256;

    // WKPDID_D3DDebugObjectName: NRI_SET_D3D_DEBUG_OBJECT_NAME (Source/Shared/SharedExternal.h) stores
    // the name under it with SetPrivateData, as strlen(name) bytes without the terminator.
    private static readonly Guid D3DDebugObjectName = new("429b8c22-9188-4b0c-8742-acb0bf85c200");

    private static int Count(List<NriMessage> messages)
    {
        lock (messages) return messages.Count;
    }

    private static List<NriMessage> Since(List<NriMessage> messages, int start)
    {
        lock (messages) return messages.Skip(start).ToList();
    }

    /// <summary>A COM interface of <paramref name="unknown"/>, owned by the returned wrapper.</summary>
    private static T QueryInterface<T>(nint unknown) where T : SharpGen.Runtime.ComObject
    {
        var iid = typeof(T).GUID;
        var hr = Marshal.QueryInterface(unknown, in iid, out var pointer);
        Assert.True(hr == 0, $"QueryInterface({typeof(T).Name}) on 0x{unknown:X} returned 0x{hr:X8}");
        return (T)Activator.CreateInstance(typeof(T), pointer)!;
    }

    /// <summary>
    /// The debug name D3D12 holds for a COM object, or null when it has none. NRI stores strlen(name)
    /// bytes; the runtime hands them back with a terminating NUL (measured), which is not part of the name.
    /// </summary>
    private static string? D3D12DebugName(nint native)
    {
        using var obj = QueryInterface<V12.ID3D12Object>(native);
        uint size = 0;
        if (obj.GetPrivateData(D3DDebugObjectName, ref size, null).Failure || size == 0) return null;
        var bytes = new byte[size];
        fixed (byte* p = bytes) Assert.True(obj.GetPrivateData(D3DDebugObjectName, ref size, p).Success);
        return Encoding.UTF8.GetString(bytes, 0, (int)size).TrimEnd('\0');
    }

    private static NriBuffer UploadBuffer(NriDevice device, byte value)
    {
        var buffer = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = Bytes });
        buffer.Map().Fill(value);
        buffer.Unmap();
        return buffer;
    }

    private static byte[] Read(NriBuffer readback)
    {
        var bytes = readback.Map().ToArray();
        readback.Unmap();
        return bytes;
    }

    // ---------------------------------------------------------------- annotations

    /// <summary>
    /// NRI validation counts command-buffer annotations and reports an unbalanced count when recording
    /// ends (Validation/CommandBufferVal.hpp, End): one EndAnnotation with nothing open, one
    /// BeginAnnotation never closed. Nested regions with a marker, a non-ASCII name and colours are
    /// clean. Nothing is submitted.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void NRI_validation_counts_command_buffer_annotations(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();

        List<NriMessage> Recording(Action<NriCommandBuffer> record)
        {
            using var cmd = allocator.CreateCommandBuffer();
            var start = Count(messages);
            cmd.Begin();
            record(cmd);
            cmd.End();
            return Since(messages, start);
        }

        var balanced = Recording(cmd =>
        {
            cmd.BeginAnnotation("Pass Ä", 0xFF336699);
            cmd.Annotation("Marker", 0xFFFF0000);
            cmd.BeginAnnotation("Nested");
            cmd.EndAnnotation();
            cmd.EndAnnotation();
        });
        var endOnly = Recording(cmd => cmd.EndAnnotation());
        var beginOnly = Recording(cmd => cmd.BeginAnnotation("Never closed"));
        foreach (var m in balanced.Concat(endOnly).Concat(beginOnly)) Record("message: " + m);

        Assert.DoesNotContain(balanced, m => m.IsError);
        Assert.Contains(endOnly, m => m.IsError && m.Text.Contains("'CmdEndAnnotation' is called more times than 'CmdBeginAnnotation'", StringComparison.Ordinal));
        Assert.Contains(beginOnly, m => m.IsError && m.Text.Contains("'CmdBeginAnnotation' is called more times than 'CmdEndAnnotation'", StringComparison.Ordinal));
    }

    /// <summary>
    /// The backend sees the regions when the command buffers execute, with NRI validation off so the
    /// backend is the only checker. A region opened in one command buffer and closed in the next on
    /// the same queue is legal on both backends and clean, which shows the open reached the backend;
    /// a close with nothing open is reported at submission: on Vulkan
    /// VUID-vkCmdEndDebugUtilsLabelEXT-commandBuffer-01912 at vkQueueSubmit2 ("does not have a
    /// matching vkCmdBeginDebugUtilsLabelEXT in this or one of the previously submitted command
    /// buffers"; the layer checks nothing at record time), on D3D12 message 1384 at
    /// ExecuteCommandLists ("Number of `PixEndEvent` occurrences in command queue (including executed
    /// command lists) exceed number of `PixBeginEvent` occurrences by 1").
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_region_may_span_command_buffers_and_a_close_with_none_open_is_reported_at_submission(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages, o => o.EnableNriValidation = false);
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var fence = device.CreateFence();
        ulong value = 0;

        List<NriMessage> Submitted(Action<NriCommandBuffer> record)
        {
            using var cmd = allocator.CreateCommandBuffer();
            var start = Count(messages);
            cmd.Begin();
            record(cmd);
            cmd.End();
            queue.Submit(cmd, fence, ++value);
            Wait(fence, value);
            return Since(messages, start);
        }

        var opened = Submitted(cmd =>
        {
            cmd.BeginAnnotation("Frame Ä", 0xFF00FF00);
            cmd.Annotation("Marker");
        });
        var closed = Submitted(cmd => cmd.EndAnnotation());
        var unmatched = Submitted(cmd => cmd.EndAnnotation());
        device.WaitIdle();
        foreach (var m in opened.Concat(closed).Concat(unmatched)) Record("message: " + m);

        Assert.DoesNotContain(opened, m => m.IsError);
        Assert.DoesNotContain(closed, m => m.IsError);
        var expected = backend.Api == GraphicsAPI.VK ? "VUID-vkCmdEndDebugUtilsLabelEXT-commandBuffer-01912" : "[1384]";
        var reported = Assert.Single(unmatched, m => m.IsError);
        Assert.Contains(expected, reported.Text, StringComparison.Ordinal);
        if (backend.Api == GraphicsAPI.D3D12) Assert.Contains("`PixEndEvent` occurrences", reported.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Queue annotations reach the D3D12 command queue: a balanced region with a marker is clean, and
    /// one QueueEndAnnotation more draws debug-layer message 1384 from ID3D12CommandQueue::EndEvent.
    /// D3D12 only: the Khronos layer does not check queue label balance (measured:
    /// vkQueueEndDebugUtilsLabelEXT with no label open draws no message), so a Vulkan queue label has
    /// nothing to observe it.
    /// </summary>
    [Fact]
    public void Queue_annotations_reach_the_D3D12_command_queue()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.D3D12));
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.D3D12, messages);
        var queue = device.GetQueue(QueueType.GRAPHICS);

        var start = Count(messages);
        queue.BeginAnnotation("Queue region Ä", 0xFF00FFFF);
        queue.Annotation("Queue marker");
        queue.EndAnnotation();
        var balanced = Since(messages, start);
        start = Count(messages);
        queue.EndAnnotation();
        var unmatched = Since(messages, start);
        foreach (var m in balanced.Concat(unmatched)) Record("message: " + m);

        Assert.DoesNotContain(balanced, m => m.IsError);
        var reported = Assert.Single(unmatched, m => m.IsError);
        Assert.Contains("[1384]", reported.Text, StringComparison.Ordinal);
        Assert.Contains("EndEvent", reported.Text, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- debug names

    /// <summary>
    /// Debug names reach the backend's own messages. A texture and a command buffer are named (one
    /// name non-ASCII), and a barrier names the wrong layout before (SHADER_RESOURCE for a texture that
    /// was never used): the backend reports the mismatch at submission and names both objects, the
    /// Khronos layer as "VkCommandBuffer 0x...[name] expects VkImage 0x...[name] ... to be in layout
    /// VK_IMAGE_LAYOUT_SHADER_READ_ONLY_OPTIMAL--instead, current layout is VK_IMAGE_LAYOUT_UNDEFINED",
    /// the debug layer as message 1334 with "pResource = 0x...:'name'" and "command list 0x...:'name'".
    /// NRI validation off, so the backend is the checker.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Debug_names_appear_in_the_backend_layer_messages(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages, o => o.EnableNriValidation = false);
        const string textureName = "Named texture Ä";
        const string commandBufferName = "Named command buffer";

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var fence = device.CreateFence();
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.RGBA8_UNORM,
            width = 4, height = 4, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
        });
        using var cmd = allocator.CreateCommandBuffer();
        texture.SetDebugName(textureName);
        cmd.SetDebugName(commandBufferName);

        var start = Count(messages);
        cmd.Begin();
        var shaderRead = new AccessLayoutStage { access = AccessBits.SHADER_RESOURCE, layout = Layout.SHADER_RESOURCE, stages = StageBits.ALL_SHADERS };
        cmd.Barrier(new NriTextureBarrier(texture, shaderRead, TextureCopySource));
        cmd.End();
        queue.Submit(cmd, fence, 1);
        Wait(fence, 1);
        device.WaitIdle();
        var reported = Since(messages, start);
        foreach (var m in reported) Record("message: " + m);

        var (open, close) = backend.Api == GraphicsAPI.VK ? ("[", "]") : ("'", "'");
        Assert.Contains(reported, m => m.IsError
            && m.Text.Contains(open + textureName + close, StringComparison.Ordinal)
            && m.Text.Contains(open + commandBufferName + close, StringComparison.Ordinal));
    }

    /// <summary>
    /// One object of every kind takes a debug name with NRI validation and the backend layer on and
    /// clean: device, buffer, texture, memory, buffer view, texture view, sampler, descriptor pool,
    /// pipeline layout, pipeline, pipeline cache, query pool, fence, command allocator, command
    /// buffer, and the queue (NriQueue.SetDebugName; the queue is not an NriObject). On D3D12 the
    /// name is read back from each object that has a COM interface: the device, buffer, texture,
    /// command list and queue.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Every_kind_of_object_takes_a_debug_name(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = Bytes, structureStride = sizeof(uint), usage = BufferUsageBits.SHADER_RESOURCE_STORAGE });
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.RGBA8_UNORM,
            width = 4, height = 4, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
        });
        // The buffer is disposed before the memory it is bound to (using order is reverse declaration order).
        var unbound = device.CreateBuffer(new BufferDesc { size = Bytes });
        var requirements = unbound.GetMemoryDesc(MemoryLocation.DEVICE);
        using var memory = device.AllocateMemory(new AllocateMemoryDesc { size = requirements.size, type = requirements.type });
        using var placed = unbound;
        device.BindBufferMemory(placed, memory);
        using var bufferView = buffer.CreateView(BufferView.STORAGE_STRUCTURED_BUFFER, structureStride: sizeof(uint));
        using var textureView = texture.CreateView(TextureView.TEXTURE, Format.RGBA8_UNORM);
        using var sampler = device.CreateSampler(new SamplerDesc());
        using var pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, storageStructuredBufferMaxNum = 1 });
        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
        {
            DescriptorSets = [new NriDescriptorSetDesc(0, [new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 1, descriptorType = DescriptorType.STORAGE_STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER }])],
            ShaderStages = StageBits.COMPUTE_SHADER,
        });
        using var cache = device.CreatePipelineCache();
        using var pipeline = device.CreateComputePipeline(new NriComputePipelineDesc
        {
            PipelineLayout = layout,
            Shader = new NriShaderDesc(StageBits.COMPUTE_SHADER, PipelineTests.Bytecode(device, "Fill.cs"), "main"),
            Cache = cache,
        });
        using var queries = device.CreateQueryPool(QueryType.TIMESTAMP, 2);
        using var fence = device.CreateFence();
        using var allocator = queue.CreateCommandAllocator();
        using var cmd = allocator.CreateCommandBuffer();

        var named = new (NriObject Object, string Name)[]
        {
            (device, "Device Ä"), (buffer, "Buffer Ä"), (texture, "Texture Ä"), (placed, "Placed buffer"), (memory, "Memory"),
            (bufferView, "Buffer view"), (textureView, "Texture view"), (sampler, "Sampler"), (pool, "Descriptor pool"),
            (layout, "Pipeline layout"), (pipeline, "Pipeline"), (cache, "Pipeline cache"), (queries, "Query pool"),
            (fence, "Fence"), (allocator, "Command allocator"), (cmd, "Command buffer Ä"),
        };
        foreach (var (obj, name) in named) obj.SetDebugName(name);
        const string queueName = "Graphics queue Ä";
        queue.SetDebugName(queueName);

        // The named objects still work: the pipeline, the set and the command buffer in one dispatch.
        var set = pool.AllocateDescriptorSet(layout, 0);
        set.Update(0, 0, bufferView);
        cmd.Begin(pool);
        cmd.Barrier(new NriBufferBarrier(buffer, Nothing, ComputeStorage));
        cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);
        cmd.SetPipeline(pipeline);
        cmd.SetDescriptorSet(0, set);
        cmd.Dispatch(1);
        cmd.End();
        queue.Submit(cmd, fence, 1);
        Wait(fence, 1);

        if (backend.Api == GraphicsAPI.D3D12)
        {
            Assert.Equal("Device Ä", D3D12DebugName(device.GetNativeObject()));
            Assert.Equal("Buffer Ä", D3D12DebugName((nint)buffer.GetNativeObject()));
            Assert.Equal("Texture Ä", D3D12DebugName((nint)texture.GetNativeObject()));
            Assert.Equal("Command buffer Ä", D3D12DebugName(cmd.GetNativeObject()));
            Assert.Equal(queueName, D3D12DebugName(queue.GetNativeObject()));
        }
        device.WaitIdle();
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// NriQueue.SetDebugName names the queue it is called on: every queue type the adapter has,
    /// each with its own name, NRI validation and the backend layer on and clean. On D3D12 each
    /// ID3D12CommandQueue reads back its own name; Vulkan has no way to read an object name back, so
    /// there the layers are the check (NRI names the VkQueue through vkSetDebugUtilsObjectNameEXT).
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Each_queue_takes_its_own_debug_name(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var adapter = TestDevice.AdapterFor(backend.Api)!;
        QueueType[] types = [.. new[] { QueueType.GRAPHICS, QueueType.COMPUTE, QueueType.COPY }.Where(t => adapter.QueueCount(t) > 0)];
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages, o => o.QueueFamilies = [.. types.Select(t => new NriQueueFamily(t))]);

        foreach (var type in types) device.GetQueue(type).SetDebugName($"{type} queue Ä");

        Record($"{backend}: named the {string.Join(", ", types)} queues");
        if (backend.Api == GraphicsAPI.D3D12)
        {
            foreach (var type in types) Assert.Equal($"{type} queue Ä", D3D12DebugName(device.GetQueue(type).GetNativeObject()));
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    // ---------------------------------------------------------------- native objects and handles

    /// <summary>
    /// GetQueueNativeObject hands out the queue NRI submits to. D3D12: an ID3D12CommandQueue whose
    /// type is the command list type of the NRI queue type (DIRECT, COMPUTE, COPY). Vulkan: the
    /// VkQueue vkGetDeviceQueue returns for the queue's family at index 0, and vkQueueWaitIdle on it
    /// succeeds. Each queue type the adapter has is created.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void The_native_queue_is_the_queue_NRI_submits_to(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var adapter = TestDevice.AdapterFor(backend.Api)!;
        QueueType[] types = [.. new[] { QueueType.GRAPHICS, QueueType.COMPUTE, QueueType.COPY }.Where(t => adapter.QueueCount(t) > 0)];
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages, o => o.QueueFamilies = [.. types.Select(t => new NriQueueFamily(t))]);

        foreach (var type in types)
        {
            var queue = device.GetQueue(type);
            var native = queue.GetNativeObject();
            Assert.NotEqual(0, native);
            Assert.Equal(native, queue.GetNativeObject());
            if (backend.Api == GraphicsAPI.D3D12)
            {
                using var commandQueue = QueryInterface<V12.ID3D12CommandQueue>(native);
                var expected = type switch
                {
                    QueueType.GRAPHICS => V12.CommandListType.Direct,
                    QueueType.COMPUTE => V12.CommandListType.Compute,
                    _ => V12.CommandListType.Copy,
                };
                Assert.Equal(expected, commandQueue.GetDescription().Type);
            }
            else
            {
                var vk = device.WrapperVK;
                var api = VulkanApi.For(new VkInstance(vk.GetInstanceVK()), new Vortice.Vulkan.VkDevice(device.GetNativeObject()));
                VkQueue fetched;
                api.vkGetDeviceQueue(vk.GetQueueFamilyIndexVK(queue), 0, &fetched);
                Assert.Equal(native, fetched.Handle);
                Assert.Equal(VkResult.Success, api.vkQueueWaitIdle(new VkQueue(native)));
            }
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// GetCommandBufferNativeObject hands out the command buffer being recorded: a raw copy recorded
    /// through it (vkCmdCopyBuffer on the VkCommandBuffer, CopyBufferRegion on the
    /// ID3D12GraphicsCommandList) between NRI's barriers executes in order with NRI's own copy, and
    /// the readback holds the bytes the raw copy moved.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_raw_copy_through_the_native_command_buffer_executes_between_NRI_commands(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var source = UploadBuffer(device, 0xC5);
        using var target = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = Bytes });
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = Bytes });

        Run(device, cmd =>
        {
            cmd.Barrier([], [new NriBufferBarrier(source, Nothing, CopySource), new NriBufferBarrier(target, Nothing, CopyDestination)], []);
            var native = cmd.GetNativeObject();
            Assert.NotEqual(0, native);
            if (backend.Api == GraphicsAPI.D3D12)
            {
                using var list = QueryInterface<V12.ID3D12GraphicsCommandList>(native);
                using var src = QueryInterface<V12.ID3D12Resource>((nint)source.GetNativeObject());
                using var dst = QueryInterface<V12.ID3D12Resource>((nint)target.GetNativeObject());
                list.CopyBufferRegion(dst, 0, src, 0, Bytes);
            }
            else
            {
                var vk = device.WrapperVK;
                var api = VulkanApi.For(new VkInstance(vk.GetInstanceVK()), new Vortice.Vulkan.VkDevice(device.GetNativeObject()));
                var region = new VkBufferCopy { srcOffset = 0, dstOffset = 0, size = Bytes };
                api.vkCmdCopyBuffer(new Vortice.Vulkan.VkCommandBuffer(native), new VkBuffer(source.GetNativeObject()), new VkBuffer(target.GetNativeObject()), 1, &region);
            }
            cmd.Barrier(new NriBufferBarrier(target, CopyDestination, CopySource));
            cmd.CopyBuffer(readback, 0, target, 0, Bytes);
        });

        var bytes = Read(readback);
        Assert.True(bytes.All(b => b == 0xC5), $"readback starts {Convert.ToHexString(bytes, 0, 16)}, expected C5 throughout");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// NriHandle is the pointer NRI's own tables take: through device.Core, the buffer's, queue's,
    /// command buffer's and device's handles give back the same native objects as the binding's
    /// GetNativeObject. Handles are stable while the object lives and refused after disposal; a
    /// device is its own Device; a queue prints its type and index.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void NRI_handles_are_what_the_Core_table_takes(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var cmd = allocator.CreateCommandBuffer();
        var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = Bytes });

        var handle = buffer.NriHandle;
        Assert.NotEqual(0, handle);
        Assert.Equal(handle, buffer.NriHandle);
        Assert.Equal(buffer.GetNativeObject(), device.Core.GetBufferNativeObject((NativeBuffer*)handle));
        Assert.Equal(cmd.GetNativeObject(), (nint)device.Core.GetCommandBufferNativeObject((NativeCommandBuffer*)cmd.NriHandle));
        Assert.Equal(queue.GetNativeObject(), (nint)device.Core.GetQueueNativeObject((NativeQueue*)queue.NriHandle));
        Assert.Equal(queue.NriHandle, device.GetQueue(QueueType.GRAPHICS).NriHandle);
        Assert.Equal(device.GetNativeObject(), (nint)device.Core.GetDeviceNativeObject((NativeDevice*)device.NriHandle));
        Assert.Same(device, device.Device);
        Assert.Same(device, buffer.Device);
        Assert.Equal("GRAPHICS queue 0", queue.ToString());

        buffer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => buffer.NriHandle);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    // ---------------------------------------------------------------- adapters, memory, queues

    /// <summary>
    /// What nriEnumerateAdapters reports, as relationships: every adapter a graphics API can use has
    /// a defined vendor and architecture, IsSoftware is the SOFTWARE architecture, it has memory of
    /// some kind (WARP reports no video memory but shared system memory), a discrete adapter has video
    /// memory and a PCI device id, and ToString names it, its vendor and its architecture.
    /// </summary>
    [Fact]
    public void Adapters_report_consistent_facts()
    {
        var adapters = NriAdapter.Enumerate().Where(a => a.Supports(GraphicsAPI.VK) || a.Supports(GraphicsAPI.D3D12)).ToList();
        Assert.SkipWhen(adapters.Count == 0, "nriEnumerateAdapters found no adapter for VK or D3D12");
        foreach (var a in adapters)
        {
            Record($"{a}: video {a.VideoMemorySize}, shared {a.SharedSystemMemorySize}, device id 0x{a.DeviceId:X}, driver 0x{a.DriverVersion:X}");
            Assert.True(Enum.IsDefined(a.Vendor), $"{a.Name}: vendor {(int)a.Vendor}");
            Assert.True(Enum.IsDefined(a.Architecture), $"{a.Name}: architecture {(int)a.Architecture}");
            Assert.Equal(a.Architecture == Architecture.SOFTWARE, a.IsSoftware);
            Assert.True(a.VideoMemorySize + a.SharedSystemMemorySize > 0, $"{a.Name} reports no memory at all");
            if (a.Architecture == Architecture.DISCRETE)
            {
                Assert.True(a.VideoMemorySize > 0, $"{a.Name} is discrete with no video memory");
                Assert.NotEqual(0u, a.DeviceId);
            }
            var text = a.ToString();
            Assert.Contains(a.Name, text, StringComparison.Ordinal);
            Assert.Contains(a.Vendor.ToString(), text, StringComparison.Ordinal);
            Assert.Contains(a.Architecture.ToString(), text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A device created on an adapter reports that adapter: the device desc's vendor, architecture,
    /// device id, driver version, memory sizes and uid are the enumerated adapter's.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void The_device_reports_the_adapter_it_was_created_on(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var adapter = TestDevice.AdapterFor(backend.Api)!;
        ref readonly var desc = ref device.Desc.adapterDesc;

        Assert.Equal(adapter.Vendor, desc.vendor);
        Assert.Equal(adapter.Architecture, desc.architecture);
        Assert.Equal(adapter.DeviceId, desc.deviceId);
        Assert.Equal(adapter.DriverVersion, desc.driverVersion);
        Assert.Equal(adapter.VideoMemorySize, desc.videoMemorySize);
        Assert.Equal(adapter.SharedSystemMemorySize, desc.sharedSystemMemorySize);
        Assert.Equal((adapter.Uid.low, adapter.Uid.high), (desc.uid.low, desc.uid.high));
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The memory budget (DXGI's QueryVideoMemoryInfo on D3D12, VK_EXT_memory_budget on Vulkan): the
    /// device-local budget is non-zero and covers the usage, and a committed 64 MiB device buffer
    /// raises the device-local usage (measured: by exactly 67,108,864 bytes, VK and D3D12 on the
    /// discrete GPU and WARP, back to the start after the buffer is freed). The buffer is zero-filled
    /// first and a quarter of it is allowed as slack because lavapipe's heap usage is the system's
    /// (heap size minus available system memory, lvp_device.c in Mesa 25), which counts pages only once
    /// written and moves with other processes. The host figure is recorded only: WARP is UMA and
    /// DXGI's non-local segment group reports budget 0 there.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void The_memory_budget_counts_a_64_MiB_allocation(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        const ulong size = 64ul << 20;

        VideoMemoryInfo before;
        try
        {
            before = device.QueryVideoMemoryInfo(MemoryLocation.DEVICE);
        }
        catch (NriUnsupportedException)
        {
            Assert.Skip($"{backend}: QueryVideoMemoryInfo is UNSUPPORTED (Vulkan: no VK_EXT_memory_budget)");
            return;
        }
        var host = device.QueryVideoMemoryInfo(MemoryLocation.HOST_UPLOAD);
        VideoMemoryInfo with;
        using (var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = size }))
        {
            Run(device, cmd =>
            {
                cmd.Barrier(new NriBufferBarrier(buffer, Nothing, CopyDestination));
                cmd.ZeroBuffer(buffer, 0, size);
            });
            with = device.QueryVideoMemoryInfo(MemoryLocation.DEVICE);
        }
        var after = device.QueryVideoMemoryInfo(MemoryLocation.DEVICE);
        Record($"{backend}: device budget {before.budgetSize}, usage {before.usageSize} -> {with.usageSize} with 64 MiB -> {after.usageSize} after; host budget {host.budgetSize}, usage {host.usageSize}");

        Assert.True(before.budgetSize > 0, "device-local budget 0");
        Assert.True(before.usageSize <= before.budgetSize, $"usage {before.usageSize} above budget {before.budgetSize}");
        Assert.True(with.usageSize >= before.usageSize + size - size / 4, $"usage {before.usageSize} -> {with.usageSize} with a 64 MiB buffer written");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// QueueWaitIdle returns after the queue's work: a copy submitted with no fence has landed in the
    /// readback buffer when it returns.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void QueueWaitIdle_returns_after_the_submitted_work(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var cmd = allocator.CreateCommandBuffer();
        using var source = UploadBuffer(device, 0x3C);
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = Bytes });
        readback.Map().Clear();
        readback.Unmap();

        cmd.Begin();
        cmd.Barrier([], [new NriBufferBarrier(source, Nothing, CopySource), new NriBufferBarrier(readback, Nothing, CopyDestination)], []);
        cmd.CopyBuffer(readback, 0, source, 0, Bytes);
        cmd.End();
        queue.Submit(cmd);
        queue.WaitIdle();

        var bytes = Read(readback);
        Assert.True(bytes.All(b => b == 0x3C), $"readback starts {Convert.ToHexString(bytes, 0, 16)} after QueueWaitIdle, expected 3C throughout");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// NriTestDevice.Warnings is the warning messages, in order: asking a device for the other
    /// backend's wrapper interface draws NRI's warning "Interface '...' is not supported by the
    /// device!" (Creation.cpp, nriGetInterface), which Warnings lists, and nothing that is not a warning.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void NriTestDevice_Warnings_are_the_warning_messages(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        using var fixture = NriTestDevice.Create(backend.Api);
        var other = backend.Api == GraphicsAPI.VK ? "WrapperD3D12Interface" : "WrapperVKInterface";
        Assert.Null(backend.Api == GraphicsAPI.VK ? (object?)fixture.Device.TryWrapperD3D12 : fixture.Device.TryWrapperVK);

        var warnings = fixture.Warnings.ToList();
        foreach (var w in warnings) Record("warning: " + w);
        Assert.Contains(warnings, w => w.Text.Contains($"Interface '{other}' is not supported by the device!", StringComparison.Ordinal));
        Assert.All(warnings, w => Assert.True(w.IsWarning, w.ToString()));
        Assert.Equal(fixture.Messages.Where(m => m.IsWarning), warnings);
        Assert.Empty(fixture.Errors);
    }
}
