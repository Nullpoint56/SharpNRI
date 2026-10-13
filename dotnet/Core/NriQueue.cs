// L2 — a queue.
//
// Queues are created with the device and destroyed with it; there is no DestroyQueue. So this is
// not an NriObject: it has no Dispose, and the device hands out the same wrapper for the same
// (type, index) every time.

using PrismNRI.Native;

namespace PrismNRI;

/// <summary>One of the device's queues. Submit is free-threaded per NRI; see the guide for the rest.</summary>
public sealed unsafe class NriQueue
{
    private readonly Queue* _handle;

    internal NriQueue(NriDevice device, Queue* handle, QueueType type, uint index)
    {
        Device = device;
        _handle = handle;
        Type = type;
        Index = index;
    }

    public NriDevice Device { get; }
    public QueueType Type { get; }
    public uint Index { get; }

    /// <summary>
    /// The external queue family (patch 0002): use as <see cref="NriTextureBarrier.DstQueue"/> to
    /// release a shared texture before another device or process imports it, or as
    /// <see cref="NriTextureBarrier.SrcQueue"/> to acquire it afterwards. Vulkan maps it to
    /// <c>VK_QUEUE_FAMILY_EXTERNAL</c>; D3D12 ignores it. Valid nowhere else.
    /// </summary>
    public static NriQueue External { get; } = new(null!, NriApi.EXTERNAL_QUEUE, QueueType.GRAPHICS, uint.MaxValue);

    internal Queue* Raw => _handle;

    /// <summary>A command allocator for this queue's type.</summary>
    public NriCommandAllocator CreateCommandAllocator()
    {
        CommandAllocator* handle = null;
        Device.Check(Device.Core.CreateCommandAllocator(_handle, &handle), "CreateCommandAllocator");
        return new NriCommandAllocator(this, handle);
    }

    /// <summary>
    /// Submits command buffers, waiting on and signalling timeline fences by value. Any span may
    /// be empty. No heap allocation.
    /// </summary>
    public void Submit(ReadOnlySpan<NriCommandBuffer> commandBuffers, ReadOnlySpan<NriFenceSubmit> waits = default, ReadOnlySpan<NriFenceSubmit> signals = default) =>
        Submit(commandBuffers, waits, signals, null, 0);

    /// <summary>
    /// <see cref="Submit(ReadOnlySpan{NriCommandBuffer}, ReadOnlySpan{NriFenceSubmit}, ReadOnlySpan{NriFenceSubmit})"/>
    /// associated with a low-latency swap chain (<c>QueueSubmitDesc::swapChain</c>): NRILowLatency
    /// requires every submission of a frame to name the swap chain, with the frame's non-zero
    /// <paramref name="presentId"/> that is also passed to <c>QueuePresent</c>.
    /// </summary>
    public void Submit(ReadOnlySpan<NriCommandBuffer> commandBuffers, ReadOnlySpan<NriFenceSubmit> waits, ReadOnlySpan<NriFenceSubmit> signals, NriSwapChain? swapChain, ulong presentId)
    {
        var raw = stackalloc CommandBuffer*[commandBuffers.Length];
        for (var i = 0; i < commandBuffers.Length; i++) raw[i] = commandBuffers[i].Raw;

        var waitDescs = stackalloc FenceSubmitDesc[waits.Length];
        for (var i = 0; i < waits.Length; i++) waitDescs[i] = waits[i].ToNative();

        var signalDescs = stackalloc FenceSubmitDesc[signals.Length];
        for (var i = 0; i < signals.Length; i++) signalDescs[i] = signals[i].ToNative();

        var desc = new QueueSubmitDesc
        {
            waitFences = waitDescs,
            waitFenceNum = (uint)waits.Length,
            commandBuffers = raw,
            commandBufferNum = (uint)commandBuffers.Length,
            signalFences = signalDescs,
            signalFenceNum = (uint)signals.Length,
            swapChain = swapChain is null ? null : swapChain.Raw,
            presentId = presentId,
        };
        Device.Check(Device.Core.QueueSubmit(_handle, &desc), "QueueSubmit");
    }

    /// <summary>One command buffer, optionally signalling one fence.</summary>
    public void Submit(NriCommandBuffer commandBuffer, NriFence? signal = null, ulong signalValue = 0)
    {
        var signals = signal is null ? default : new ReadOnlySpan<NriFenceSubmit>([new NriFenceSubmit(signal, signalValue)]);
        Submit([commandBuffer], default, signals);
    }

    /// <summary>
    /// Synchronous host-to-texture copy. The texture must be in <c>{HOST_WRITE, GENERAL, HOST}</c>
    /// and created with <c>HOST_TRANSFER</c> usage. Returns when the data has landed.
    /// </summary>
    public void UploadHostMemoryToTexture(NriTexture texture, in TextureRegionDesc region, ReadOnlySpan<byte> data, uint rowPitch = 0, uint slicePitch = 0)
    {
        fixed (byte* p = data)
        {
            var desc = new UploadHostMemoryToTextureDesc { srcData = p, dstTexture = texture.Raw, dstRegion = region, srcRowPitch = rowPitch, srcSlicePitch = slicePitch };
            Device.Check(Device.Core.UploadHostMemoryToTexture(_handle, &desc, 1), "UploadHostMemoryToTexture");
        }
    }

    /// <summary>
    /// Synchronous texture-to-host copy. The texture must be in <c>{HOST_READ, GENERAL, HOST}</c>
    /// and created with <c>HOST_TRANSFER</c> usage. The data is valid when this returns.
    /// </summary>
    public void ReadbackTextureToHostMemory(NriTexture texture, in TextureRegionDesc region, Span<byte> data, uint rowPitch = 0, uint slicePitch = 0)
    {
        fixed (byte* p = data)
        {
            var desc = new ReadbackTextureToHostMemoryDesc { srcTexture = texture.Raw, dstData = p, srcRegion = region, dstRowPitch = rowPitch, dstSlicePitch = slicePitch };
            Device.Check(Device.Core.ReadbackTextureToHostMemory(_handle, &desc, 1), "ReadbackTextureToHostMemory");
        }
    }

    /// <summary>Blocks until this queue is idle.</summary>
    public void WaitIdle() => Device.Check(Device.Core.QueueWaitIdle(_handle), "QueueWaitIdle");

    /// <summary>GPU and CPU timestamps taken together, for correlating query results with host time.</summary>
    public (ulong Gpu, ulong Cpu) GetCalibratedTimestamps()
    {
        ulong gpu, cpu;
        Device.Core.GetCalibratedTimestamps(_handle, &gpu, &cpu);
        return (gpu, cpu);
    }

    public void BeginAnnotation(string name, uint bgra = NriApi.BGRA_UNUSED)
    {
        using var utf8 = new Interop.Utf8.Scoped(name);
        Device.Core.QueueBeginAnnotation(_handle, utf8, bgra);
    }

    public void EndAnnotation() => Device.Core.QueueEndAnnotation(_handle);

    public void Annotation(string name, uint bgra = NriApi.BGRA_UNUSED)
    {
        using var utf8 = new Interop.Utf8.Scoped(name);
        Device.Core.QueueAnnotation(_handle, utf8, bgra);
    }

    /// <summary>
    /// The native queue: <c>ID3D12CommandQueue*</c> on D3D12, <c>VkQueue</c> on Vulkan. Last resort.
    /// </summary>
    public nint GetNativeObject() => (nint)Device.Core.GetQueueNativeObject(_handle);

    public override string ToString() => $"{Type} queue {Index}";
}
