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

using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

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
