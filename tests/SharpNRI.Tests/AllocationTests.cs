// The recording path allocates nothing on the managed heap, and NRI gives back through the host
// allocator everything it took.
//
// NONE is the recording device: it does nothing per command, so a managed allocation in the loop
// can only be the binding's. Note that NRI never wraps NONE in its validation layer, even when
// enableNRIValidation is set (Creation.cpp: "enableNRIValidation && graphicsAPI != NONE"), so on
// NONE the stream is not validated and the message list stays empty by construction; the stream
// is still written to be valid on a real backend. NONE also allocates almost nothing through the
// host allocator (2 blocks for the device, objects are static dummies), so the "everything is
// freed" check runs on the GPU backends as well, where the validation layer and the backend
// allocate per object.

using SharpNRI.Native;
using Xunit;
using static SharpNRI.Tests.GpuWork;

namespace SharpNRI.Tests;

public sealed class AllocationTests
{
    private const int Iterations = 300;

    [Fact]
    public void Recording_a_representative_stream_allocates_nothing_on_the_managed_heap()
    {
        var messages = new List<NriMessage>();
        var allocator = new NriCountingAllocator();
        var device = TestDevice.Create(GraphicsAPI.NONE, messages, b => b.Allocator = allocator);
        var afterDevice = (allocator.Allocations, allocator.Reallocations, allocator.Frees);

        long gcDelta, nativeAllocationsDuringLoop, nativeFreesDuringLoop;
        int messagesDuringLoop;
        try
        {
            var queue = device.GetQueue(QueueType.GRAPHICS);
            using var commandAllocator = queue.CreateCommandAllocator();
            using var cmd = commandAllocator.CreateCommandBuffer();
            using var source = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 256 });
            using var destination = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 256 });
            using var target = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
            {
                type = TextureType.TEXTURE_2D, usage = TextureUsageBits.COLOR_ATTACHMENT, format = Format.RGBA8_UNORM,
                width = 64, height = 64, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
            });
            using var attachment = target.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);

            // Everything the loop passes by span is built once, here.
            GlobalBarrierDesc[] globals = [new GlobalBarrierDesc { before = CopyDestination, after = CopySource }];
            NriBufferBarrier[] bufferBarriers = [new NriBufferBarrier(destination, CopyDestination, CopySource)];
            NriTextureBarrier[] textureBarriers = [new NriTextureBarrier(target, Undefined, ColorAttachment)];
            Viewport[] viewports = [new Viewport { width = 64, height = 64, depthMax = 1 }];
            Rect[] scissors = [new Rect { width = 64, height = 64 }];
            NriAttachment[] colors = [new NriAttachment(attachment, LoadOp: LoadOp.CLEAR)];

            var afterObjects = (allocator.Allocations, allocator.Frees);
            Record($"NONE: device creation made {afterDevice.Allocations} host allocations, {afterDevice.Reallocations} reallocations, {afterDevice.Frees} frees; the objects took the totals to {afterObjects.Allocations} allocations, {afterObjects.Frees} frees");

            // Warm-up: JIT, static constructors and first-call paths allocate once.
            RecordStream(cmd, globals, bufferBarriers, textureBarriers, viewports, scissors, colors, destination, source);

            var messagesBefore = messages.Count;
            var nativeBefore = (allocator.Allocations, allocator.Frees);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < Iterations; i++)
            {
                RecordStream(cmd, globals, bufferBarriers, textureBarriers, viewports, scissors, colors, destination, source);
            }
            gcDelta = GC.GetAllocatedBytesForCurrentThread() - before;
            nativeAllocationsDuringLoop = allocator.Allocations - nativeBefore.Allocations;
            nativeFreesDuringLoop = allocator.Frees - nativeBefore.Frees;
            messagesDuringLoop = messages.Count - messagesBefore;
        }
        finally
        {
            Assert.Equal(0, device.LiveObjects);
            device.Dispose();
        }

        Record($"NONE: {Iterations} recordings: {gcDelta} managed bytes, {nativeAllocationsDuringLoop} host allocations, {nativeFreesDuringLoop} host frees, {messagesDuringLoop} messages");
        Record($"NONE: after device disposal: {allocator.Allocations} allocations, {allocator.Reallocations} reallocations, {allocator.Frees} frees, Live = {allocator.Live}");

        // Measured: NONE device creation makes 2 host allocations and frees 1; the objects add none
        // (static dummies); 300 recordings: 0 managed bytes, 0 host allocations, 0 messages; after
        // disposal 2 allocations, 2 frees, Live = 0.
        Assert.Equal(0, messagesDuringLoop);
        Assert.Equal(0L, gcDelta);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// A whole frame, the shape an engine runs every frame: reset the command allocator and the
    /// descriptor pool, allocate and write this frame's sets, record, submit with fences, wait,
    /// acquire and present. NriDescriptorSet is a struct and the sets land in storage owned by
    /// the caller, so resetting and reallocating sets per frame allocates nothing. Buffer
    /// <c>Map</c> is not in the frame: NONE's <c>MapBuffer</c> returns null, which the binding
    /// reports as a failure.
    /// </summary>
    [Fact]
    public void A_whole_frame_allocates_nothing_on_the_managed_heap_or_through_the_host_allocator()
    {
        var messages = new List<NriMessage>();
        var allocator = new NriCountingAllocator();
        var device = TestDevice.Create(GraphicsAPI.NONE, messages, b => b.Allocator = allocator);

        long gcDelta, nativeAllocationsDuringLoop, nativeReallocationsDuringLoop, nativeFreesDuringLoop;
        int messagesDuringLoop;
        try
        {
            var queue = device.GetQueue(QueueType.GRAPHICS);
            using var commandAllocator = queue.CreateCommandAllocator();
            using var cmd = commandAllocator.CreateCommandBuffer();
            using var fence = device.CreateFence();
            using var acquire = device.CreateFence(NriSwapChainExtension.SwapChainSemaphore);
            using var release = device.CreateFence(NriSwapChainExtension.SwapChainSemaphore);
            using var constants = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 256, usage = BufferUsageBits.CONSTANT });
            using var constantView = constants.CreateView(BufferView.CONSTANT_BUFFER, 0, 256);
            using var source = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 256 });
            using var destination = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 256 });
            using var target = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
            {
                type = TextureType.TEXTURE_2D, usage = TextureUsageBits.COLOR_ATTACHMENT, format = Format.RGBA8_UNORM,
                width = 64, height = 64, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
            });
            using var attachment = target.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
            using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
            {
                DescriptorSets = [new NriDescriptorSetDesc(0, [new DescriptorRangeDesc { descriptorNum = 2, descriptorType = DescriptorType.CONSTANT_BUFFER, shaderStages = StageBits.ALL }])],
                RootConstants = [new RootConstantDesc { registerIndex = 0, size = 16, shaderStages = StageBits.ALL }],
            });
            // NONE accepts any bytecode; the frame binds the pipeline and sets root constants through both overloads.
            using var pipeline = device.CreateComputePipeline(new NriComputePipelineDesc
            {
                PipelineLayout = layout,
                Shader = new NriShaderDesc(StageBits.COMPUTE_SHADER, PipelineTests.Bytecode(device, "Fill.cs"), "main"),
            });
            byte[] rootBytes = new byte[16];
            using var pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 4, constantBufferMaxNum = 8 });
            using var copySourcePool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, constantBufferMaxNum = 2, flags = DescriptorPoolBits.COPY_SOURCE });
            using var swapChain = device.SwapChain.CreateSwapChain(new NriSwapChainDesc
            {
                Window = new NriWindow { Windows = new NriWindowsWindow(0x1234) }, // NONE never touches it
                Queue = queue, Width = 64, Height = 64, TextureNum = 2,
            });

            // The template set the frame copies from, written once.
            var template = copySourcePool.AllocateDescriptorSet(layout, 0);
            template.Update(0, 0, constantView);
            template.Update(0, 1, constantView);

            // Everything the frame passes by span is built once, here, including the storage the
            // frame's sets are allocated into.
            var frameSets = new NriDescriptorSet[3];
            NriDescriptor[] views = [constantView, constantView];
            GlobalBarrierDesc[] globals = [new GlobalBarrierDesc { before = CopyDestination, after = CopySource }];
            NriBufferBarrier[] bufferBarriers = [new NriBufferBarrier(destination, CopyDestination, CopySource)];
            NriTextureBarrier[] textureBarriers = [new NriTextureBarrier(target, Undefined, ColorAttachment)];
            Viewport[] viewports = [new Viewport { width = 64, height = 64, depthMax = 1 }];
            Rect[] scissors = [new Rect { width = 64, height = 64 }];
            NriAttachment[] colors = [new NriAttachment(attachment, LoadOp: LoadOp.CLEAR)];
            NriCommandBuffer[] commandBuffers = [cmd];
            NriFenceSubmit[] waits = [new NriFenceSubmit(acquire, 0)];
            var signals = new NriFenceSubmit[2];
            ulong frameValue = 0;
            ulong observed = 0;

            void Frame()
            {
                frameValue++;
                device.SwapChain.AcquireNextTexture(swapChain, acquire, out _);

                commandAllocator.Reset();
                pool.Reset();
                pool.AllocateDescriptorSets(layout, 0, frameSets);
                var extra = pool.AllocateDescriptorSet(layout, 0);
                frameSets[0].Update(0, 0, constantView);
                frameSets[0].Update(0, 1, constantView);
                frameSets[1].UpdateRange(0, 0, views);
                frameSets[2].CopyRange(0, 0, template, 0, 0, 2);
                extra.CopyRange(0, 0, template, 0, 0, 2);

                cmd.Begin();
                cmd.SetDescriptorPool(pool);
                cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
                cmd.SetDescriptorSet(0, frameSets[(int)(frameValue % 3)], BindPoint.GRAPHICS);
                cmd.Barrier(globals, bufferBarriers, textureBarriers);
                cmd.SetViewports(viewports);
                cmd.SetScissors(scissors);
                cmd.BeginRendering(colors);
                cmd.Draw(3);
                cmd.EndRendering();
                cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);
                cmd.SetPipeline(pipeline);
                cmd.SetRootConstants(0, rootBytes, 0, BindPoint.COMPUTE);   // span overload
                cmd.SetRootConstants(0, frameValue, 0, BindPoint.COMPUTE);  // struct overload, 8 bytes
                cmd.SetDescriptorSet(0, extra, BindPoint.COMPUTE);
                cmd.Dispatch(1);
                cmd.CopyBuffer(destination, 0, source, 0, 256);
                cmd.End();

                signals[0] = new NriFenceSubmit(release, 0);
                signals[1] = new NriFenceSubmit(fence, frameValue);
                queue.Submit(commandBuffers, waits, signals);
                queue.Submit(cmd, fence, frameValue); // the single-buffer convenience overload, as the guide shows it
                device.SwapChain.QueuePresent(swapChain, release, frameValue);

                fence.Wait(frameValue); // NONE: returns at once, the fence never advances
                observed += fence.Value;
            }

            var afterObjects = (allocator.Allocations, allocator.Reallocations, allocator.Frees);
            Record($"NONE: device and frame objects: {afterObjects.Allocations} host allocations, {afterObjects.Reallocations} reallocations, {afterObjects.Frees} frees");

            // Warm-up: JIT, static constructors and first-call paths allocate once.
            Frame();

            var messagesBefore = messages.Count;
            var nativeBefore = (allocator.Allocations, allocator.Reallocations, allocator.Frees);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < Iterations; i++) Frame();
            gcDelta = GC.GetAllocatedBytesForCurrentThread() - before;
            nativeAllocationsDuringLoop = allocator.Allocations - nativeBefore.Allocations;
            nativeReallocationsDuringLoop = allocator.Reallocations - nativeBefore.Reallocations;
            nativeFreesDuringLoop = allocator.Frees - nativeBefore.Frees;
            messagesDuringLoop = messages.Count - messagesBefore;
            Record($"NONE: fence value after the loop {fence.Value} (NONE never advances it; sum observed {observed})");
        }
        finally
        {
            Assert.Equal(0, device.LiveObjects);
            device.Dispose();
        }

        Record($"NONE: {Iterations} whole frames: {gcDelta} managed bytes, {nativeAllocationsDuringLoop} host allocations, {nativeReallocationsDuringLoop} host reallocations, {nativeFreesDuringLoop} host frees, {messagesDuringLoop} messages");

        // Measured: device and frame objects 2 host allocations, 1 free (objects are static
        // dummies on NONE); 300 whole frames: 0 managed bytes, 0 host allocations, reallocations
        // or frees, 0 messages; Live = 0 after disposal.

        Assert.Equal(0, messagesDuringLoop);
        Assert.Equal(0L, gcDelta);
        Assert.Equal(0L, nativeAllocationsDuringLoop);
        Assert.Equal(0L, nativeReallocationsDuringLoop);
        Assert.Equal(0L, nativeFreesDuringLoop);
        Assert.Equal(0L, allocator.Live);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Every command, queue, query and descriptor-set call the binding has, each overload once per
    /// frame, so <see cref="AllocationCoverageTests"/> can hold the per-frame surface to this
    /// measurement. NONE accepts every call, so the stream need not make sense on a GPU; the
    /// annotations' names fit the binding's stack buffer, as an engine's markers do.
    /// </summary>
    [Fact]
    public void Every_command_and_queue_call_allocates_nothing_on_the_managed_heap_or_through_the_host_allocator()
    {
        var messages = new List<NriMessage>();
        var allocator = new NriCountingAllocator();
        var device = TestDevice.Create(GraphicsAPI.NONE, messages, b => b.Allocator = allocator);

        long gcDelta, nativeDelta;
        try
        {
            var queue = device.GetQueue(QueueType.GRAPHICS);
            using var commandAllocator = queue.CreateCommandAllocator();
            using var cmd = commandAllocator.CreateCommandBuffer();
            using var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 256, usage = BufferUsageBits.VERTEX | BufferUsageBits.INDEX | BufferUsageBits.ARGUMENT });
            using var constants = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 256, usage = BufferUsageBits.CONSTANT });
            using var constantView = constants.CreateView(BufferView.CONSTANT_BUFFER, 0, 256);
            var textureDesc = new TextureDesc
            {
                type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE_STORAGE | TextureUsageBits.COLOR_ATTACHMENT, format = Format.RGBA8_UNORM,
                width = 4, height = 4, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
            };
            using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, textureDesc);
            using var other = device.CreateCommittedTexture(MemoryLocation.DEVICE, textureDesc);
            using var storage = texture.CreateView(TextureView.STORAGE_TEXTURE, Format.RGBA8_UNORM);
            using var attachment = texture.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
            using var queries = device.CreateQueryPool(QueryType.TIMESTAMP, 4);
            using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
            {
                DescriptorSets = [new NriDescriptorSetDesc(0, [new DescriptorRangeDesc { descriptorNum = 1, descriptorType = DescriptorType.CONSTANT_BUFFER, shaderStages = StageBits.ALL }])],
            });
            using var pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, constantBufferMaxNum = 1 });
            var set = pool.AllocateDescriptorSet(layout, 0);

            // Everything passed by span is built once, here.
            NriVertexBuffer[] vertexBuffers = [new NriVertexBuffer(buffer, 0, 16), new NriVertexBuffer(buffer, 64, 16)];
            ClearAttachmentDesc[] clears = [new ClearAttachmentDesc { planes = PlaneBits.COLOR }];
            Rect[] clearRects = [new Rect { width = 4, height = 4 }];
            SampleLocation[] sampleLocations = [new SampleLocation { x = 1, y = -1 }];
            NriAttachment[] colors = [new NriAttachment(attachment)];
            var region = new TextureRegionDesc { width = 4, height = 4, depth = 1 };
            var layoutDesc = new TextureDataLayoutDesc { rowPitch = 256, slicePitch = 1024 };
            var hostData = new byte[4096];
            var longName = new string('a', 300);
            var textureBarrier = new NriTextureBarrier(texture, Undefined, ColorAttachment);
            var bufferBarrier = new NriBufferBarrier(buffer, CopyDestination, CopySource);
            var globalBarrier = new GlobalBarrierDesc { before = CopyDestination, after = CopySource };
            ulong sink = 0;

            void Frame()
            {
                commandAllocator.Reset();
                cmd.Begin();
                cmd.BeginAnnotation("frame", 0xFF00FF00);
                cmd.Annotation("marker");
                cmd.SetDescriptorPool(pool);
                cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
                cmd.SetDescriptorSet(0, set);
                cmd.SetRootDescriptor(0, constantView, 0, BindPoint.GRAPHICS);
                cmd.Barrier(in textureBarrier);
                cmd.Barrier(in bufferBarrier);
                cmd.Barrier(in globalBarrier);
                cmd.SetIndexBuffer(buffer, 0, IndexType.UINT16);
                cmd.SetVertexBuffers(0, vertexBuffers);
                cmd.SetVertexBuffer(2, buffer, 128, 16);
                cmd.SetViewport(new Viewport { width = 4, height = 4, depthMax = 1 });
                cmd.SetScissor(new Rect { width = 4, height = 4 });
                cmd.SetStencilReference(1, 2);
                cmd.SetDepthBounds(0, 1);
                cmd.SetBlendConstants(new Color32f { x = 1, y = 1, z = 1, w = 1 });
                cmd.SetSampleLocations(sampleLocations, 1);
                cmd.SetShadingRate(new ShadingRateDesc { shadingRate = ShadingRate.FRAGMENT_SIZE_1X1 });
                cmd.SetDepthBias(new DepthBiasDesc { constant = 1 });
                cmd.BeginRendering(colors);
                cmd.ClearAttachments(clears, clearRects);
                cmd.Draw(new DrawDesc { vertexNum = 3, instanceNum = 1 });
                cmd.DrawIndexed(new DrawIndexedDesc { indexNum = 3, instanceNum = 1 });
                cmd.DrawIndexed(3);
                cmd.DrawIndirect(buffer, 0, 1, 16);
                cmd.DrawIndexedIndirect(buffer, 0, 1, 20, buffer, 128);
                cmd.EndRendering();
                cmd.Dispatch(new DispatchDesc { workGroupNumX = 1, workGroupNumY = 1, workGroupNumZ = 1 });
                cmd.DispatchIndirect(buffer, 0);
                cmd.CopyTexture(other, null, texture, null);
                cmd.CopyTexture(other, region, texture, region);
                cmd.UploadBufferToTexture(texture, region, buffer, layoutDesc);
                cmd.ReadbackTextureToBuffer(buffer, layoutDesc, texture, region);
                cmd.ZeroBuffer(buffer, 0, 256);
                cmd.ResolveTexture(other, null, texture, null, ResolveOp.AVERAGE);
                cmd.ClearStorage(storage, default, 0, 0, 0);
                cmd.ResetQueries(queries, 0, 4);
                cmd.BeginQuery(queries, 0);
                cmd.EndQuery(queries, 0);
                cmd.CopyQueries(queries, 0, 4, buffer, 0);
                cmd.EndAnnotation();
                sink += (ulong)cmd.GetNativeObject();
                cmd.End();

                queue.BeginAnnotation("submit", 0xFFFF0000);
                queue.Annotation("marker");
                queue.EndAnnotation();
                queue.UploadHostMemoryToTexture(texture, region, hostData);
                queue.ReadbackTextureToHostMemory(texture, region, hostData);
                var (gpu, cpu) = queue.GetCalibratedTimestamps();
                queue.WaitIdle();
                queries.Reset(0, 4);
                var (resourceHeap, samplerHeap) = set.GetOffsets();
                sink += gpu + cpu + resourceHeap + samplerHeap + queries.QuerySize + (ulong)queue.GetNativeObject() + (ulong)queue.NriHandle + (ulong)set.NriHandle
                    + buffer.DeviceAddress + buffer.GetNativeObject() + (buffer.IsOwned ? 1ul : 0) + (set.Device == device ? 1ul : 0);

                // Thread annotations, a name past the binding's stack buffer (the native-memory path), and the static helpers.
                Nri.BeginAnnotation("thread", 0xFF0000FF);
                Nri.Annotation(longName);
                Nri.EndAnnotation();
                using (Nri.Annotate("scope")) { }
                ref readonly var props = ref Nri.GetFormatProps(Format.RGBA8_UNORM);
                sink += props.stride + Nri.ConvertNRIFormatToVK(Format.RGBA8_UNORM) + Nri.ConvertNRIFormatToDXGI(Format.RGBA8_UNORM)
                    + (ulong)Nri.ConvertVKFormatToNRI(37) + (ulong)Nri.ConvertDXGIFormatToNRI(28);

                // The device's and objects' cheap reads, including the extension lookups an engine makes per frame.
                device.WaitIdle();
                sink += (ulong)device.GetQueue(QueueType.GRAPHICS).Index + (ulong)device.LiveObjects;
                ref readonly var core = ref device.Core;   // the raw tables, read by reference
                ref readonly var helper = ref device.Helper;
                var memory = device.QueryVideoMemoryInfo(MemoryLocation.DEVICE);
                sink += memory.budgetSize + (ulong)device.GetFormatSupport(Format.RGBA8_UNORM) + (ulong)device.GetNativeObject() + device.Desc.nriVersion
                    + (ulong)device.GraphicsApi + device.NriVersion
                    + (device.Allocator is null ? 0ul : 1) + (ulong)(device.LastError?.Length ?? 0)
                    + (device.TryMeshShader is null ? 0ul : 1) + (device.TryDescriptorHeap is null ? 0ul : 1) + (device.TryStreamer is null ? 0ul : 1)
                    + (device.TrySwapChain is null ? 0ul : 1) + (device.TryLowLatency is null ? 0ul : 1) + (device.TryUpscaler is null ? 0ul : 1)
                    + (device.TryImgui is null ? 0ul : 1) + (device.TryVideo is null ? 0ul : 1) + (device.TryRayTracing is null ? 0ul : 1)
                    + (device.TryWrapperVK is null ? 0ul : 1) + (device.TryWrapperD3D12 is null ? 0ul : 1)
                    + texture.GetNativeObject() + texture.Desc.width + (texture.IsOwned ? 1ul : 0) + storage.GetNativeObject()
                    + (ulong)texture.NriHandle + (texture.IsDisposed ? 1ul : 0) + (texture.Device == device ? 1ul : 0);
            }

            Frame(); // warm-up: JIT, static constructors and first-call paths allocate once

            var nativeBefore = allocator.Allocations + allocator.Reallocations;
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < Iterations; i++) Frame();
            gcDelta = GC.GetAllocatedBytesForCurrentThread() - before;
            nativeDelta = allocator.Allocations + allocator.Reallocations - nativeBefore;
            Record($"NONE: sink {sink}");
        }
        finally
        {
            Assert.Equal(0, device.LiveObjects);
            device.Dispose();
        }

        Record($"NONE: {Iterations} frames of every command and queue call: {gcDelta} managed bytes, {nativeDelta} host allocations and reallocations");
        Assert.Equal(0L, gcDelta);
        Assert.Equal(0L, nativeDelta);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>Map and Unmap, which NONE cannot answer (its MapBuffer returns null), on each GPU backend.</summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Mapping_a_buffer_allocates_nothing_on_the_managed_heap(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        using var buffer = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 256 });

        void Frame()
        {
            buffer.Map()[0]++;
            buffer.Unmap();
            buffer.Map(64, 64)[0]++;
            buffer.Unmap();
        }

        Frame(); // warm-up
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Iterations; i++) Frame();
        var delta = GC.GetAllocatedBytesForCurrentThread() - before;

        Record($"{backend}: {Iterations} frames of Map/Unmap: {delta} managed bytes");
        Assert.Equal(0L, delta);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void NRI_frees_everything_it_allocated_through_the_host_allocator(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        var allocator = new NriCountingAllocator();
        var device = TestDevice.Create(backend.Api, messages, b => b.Allocator = allocator);
        var created = allocator.Allocations;
        long peakLive;

        try
        {
            var queue = device.GetQueue(QueueType.GRAPHICS);
            using var commandAllocator = queue.CreateCommandAllocator();
            using var cmd = commandAllocator.CreateCommandBuffer();
            using var fence = device.CreateFence();
            using var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 256, usage = BufferUsageBits.CONSTANT });
            using var bufferView = buffer.CreateView(BufferView.CONSTANT_BUFFER, 0, 256);
            using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
            {
                type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.RGBA8_UNORM,
                width = 4, height = 4, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
            });
            using var textureView = texture.CreateView(TextureView.TEXTURE, Format.RGBA8_UNORM);
            using var sampler = device.CreateSampler(new SamplerDesc());
            using var queries = device.CreateQueryPool(QueryType.TIMESTAMP, 2);
            using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
            {
                DescriptorSets = [new NriDescriptorSetDesc(0, [new DescriptorRangeDesc { descriptorNum = 1, descriptorType = DescriptorType.CONSTANT_BUFFER, shaderStages = StageBits.ALL }])],
            });
            using var pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, constantBufferMaxNum = 1 });
            pool.AllocateDescriptorSet(layout, 0).Update(0, 0, bufferView);

            cmd.Begin();
            cmd.End();
            queue.Submit(cmd, fence, 1);
            fence.Wait(1);
            peakLive = allocator.Live;
        }
        finally
        {
            device.Dispose();
        }

        Record($"{backend}: {created} host allocations at device creation, {peakLive} live with the objects; after disposal {allocator.Allocations} allocations, {allocator.Reallocations} reallocations, {allocator.Frees} frees, Live = {allocator.Live}");
        // Measured (third-party allocators not forwarded, the default): NONE 2 allocations at
        // creation, 2/2 at the end; VK 29 at creation, 39 live with these objects, 60/60 at the
        // end; D3D12 16 at creation, 48 live, 50/50 at the end. No reallocations anywhere. NRI
        // frees everything it allocated through the host allocator on all three backends.
        Assert.True(created > 0, "device creation allocated nothing through the host allocator");
        Assert.Equal(0L, allocator.Live);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    private static void RecordStream(
        NriCommandBuffer cmd,
        GlobalBarrierDesc[] globals,
        NriBufferBarrier[] bufferBarriers,
        NriTextureBarrier[] textureBarriers,
        Viewport[] viewports,
        Rect[] scissors,
        NriAttachment[] colors,
        NriBuffer destination,
        NriBuffer source)
    {
        cmd.Begin();
        cmd.Barrier(globals, bufferBarriers, textureBarriers);
        cmd.SetViewports(viewports);
        cmd.SetScissors(scissors);
        cmd.BeginRendering(colors);
        cmd.Draw(3);
        cmd.EndRendering();
        cmd.Dispatch(1);
        cmd.CopyBuffer(destination, 0, source, 0, 256);
        cmd.End();
    }
}
