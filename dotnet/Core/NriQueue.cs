// L2 — a queue.
//
// Queues are created with the device and destroyed with it; there is no DestroyQueue. So this is
// not an NriObject: it has no Dispose, and the device hands out the same wrapper for the same
// (type, index) every time.

using SharpNRI.Native;

namespace SharpNRI;

/// <summary>
/// <c>TextureSubresourceUploadDesc</c>: one mip of one layer. <c>SliceNum</c> slices at
/// <c>SlicePitch</c>, each <c>SlicePitch / RowPitch</c> rows of <c>RowPitch</c> bytes, tightly
/// laid out as the caller has them; NRI realigns for the device.
/// </summary>
public readonly record struct NriTextureSubresourceUpload(ReadOnlyMemory<byte> Slices, uint SliceNum, uint RowPitch, uint SlicePitch);

/// <summary>
/// <c>TextureUploadDesc</c> naming the wrapper. <c>Subresources</c>, when given, holds every one,
/// layer-major (<c>layer * mipNum + mip</c>); empty only transitions the texture to <c>After</c>.
/// </summary>
public readonly record struct NriTextureUpload(
    NriTexture Texture,
    NriTextureSubresourceUpload[]? Subresources,
    AccessLayoutStage After,
    PlaneBits Planes = PlaneBits.ALL);

/// <summary>
/// <c>BufferUploadDesc</c> naming the wrapper. <c>Data</c>, when given, covers the whole buffer;
/// empty only transitions the buffer to <c>After</c>.
/// </summary>
public readonly record struct NriBufferUpload(NriBuffer Buffer, ReadOnlyMemory<byte> Data, AccessStage After);

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
    /// The external queue family (<c>EXTERNAL_QUEUE</c>): use as <see cref="NriTextureBarrier.DstQueue"/> or
    /// <see cref="NriBufferBarrier.DstQueue"/> to release a shared texture or buffer before another device or
    /// process imports it, or as the <c>SrcQueue</c> to acquire it afterwards, with the local queue on the
    /// other side. Vulkan maps it to <c>VK_QUEUE_FAMILY_EXTERNAL</c>; D3D12 ignores it. Valid nowhere else.
    /// </summary>
    public static NriQueue External { get; } = new(null!, NriApi.EXTERNAL_QUEUE, QueueType.GRAPHICS, uint.MaxValue);

    internal Queue* Raw => _handle;

    /// <summary>NRI's <c>nri::Queue*</c>; see <see cref="NriObject.NriHandle"/>. Last resort.</summary>
    public nint NriHandle => (nint)_handle;

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
        // A span over the local, not a one-element array: this overload is per frame too (48 bytes
        // per call before, measured by AllocationTests' whole frame).
        var one = signal is null ? default : new NriFenceSubmit(signal, signalValue);
        var signals = signal is null ? default : new ReadOnlySpan<NriFenceSubmit>(in one);
        Submit([commandBuffer], default, signals);
    }

    /// <summary>
    /// Synchronous host-to-texture copy. The texture must be in <c>{HOST_WRITE, GENERAL, HOST}</c>
    /// and created with <c>HOST_TRANSFER</c> usage. Returns when the data has landed.
    /// </summary>
    /// <exception cref="ArgumentException">When <paramref name="data"/> is shorter than NRI will read.</exception>
    public void UploadHostMemoryToTexture(NriTexture texture, in TextureRegionDesc region, ReadOnlySpan<byte> data, uint rowPitch = 0, uint slicePitch = 0)
    {
        CheckHostCopySpan(texture, region, data.Length, rowPitch, slicePitch);
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
    /// <exception cref="ArgumentException">When <paramref name="data"/> is shorter than NRI will write.</exception>
    public void ReadbackTextureToHostMemory(NriTexture texture, in TextureRegionDesc region, Span<byte> data, uint rowPitch = 0, uint slicePitch = 0)
    {
        CheckHostCopySpan(texture, region, data.Length, rowPitch, slicePitch);
        fixed (byte* p = data)
        {
            var desc = new ReadbackTextureToHostMemoryDesc { srcTexture = texture.Raw, dstData = p, srcRegion = region, dstRowPitch = rowPitch, dstSlicePitch = slicePitch };
            Device.Check(Device.Core.ReadbackTextureToHostMemory(_handle, &desc, 1), "ReadbackTextureToHostMemory");
        }
    }

    // NRI touches, per slice of the region, (rows - 1) row pitches plus one row of texel blocks of host
    // memory, whatever the span's length (CopyTextureData in DeviceVK.hpp / DeviceD3D12.hpp, or the
    // driver's host image copy). A pitch of 0 means tightly packed, as in NRI.
    private static void CheckHostCopySpan(NriTexture texture, in TextureRegionDesc region, int length, uint rowPitch, uint slicePitch)
    {
        ref readonly var desc = ref texture.Desc;
        ref readonly var props = ref Nri.GetFormatProps(desc.format);
        var mipOffset = region.mipOffset;
        uint Extent(ushort size, ushort requested) => requested != NriApi.WHOLE_SIZE ? requested : Math.Max((uint)size >> mipOffset, 1u);
        var width = Extent(desc.width, region.width);
        var height = Extent(desc.height, region.height);
        var depth = Extent(desc.depth, region.depth);
        var rowSize = (ulong)((width + props.blockWidth - 1u) / props.blockWidth) * props.stride;
        var rowNum = (ulong)((height + props.blockHeight - 1u) / props.blockHeight);
        var rows = rowPitch != 0 ? rowPitch : rowSize;
        var slices = slicePitch != 0 ? slicePitch : rows * rowNum;
        var needed = (depth - 1) * slices + (rowNum - 1) * rows + rowSize;
        if ((ulong)length < needed)
            throw new ArgumentException($"{length} bytes for a {width}x{height}x{depth} {desc.format} region at row pitch {rows}, slice pitch {slices}; NRI touches {needed}", "data");
    }

    /// <summary>
    /// NRI's <c>HelperInterface::UploadData</c>: fills resources through a temporary upload buffer
    /// and leaves each in its <c>After</c> state. Synchronous: records, submits on this queue and
    /// waits. For initial contents, not streaming. A record without data only transitions.
    /// </summary>
    /// <exception cref="ArgumentException">When data is shorter than NRI will read.</exception>
    public void UploadData(ReadOnlySpan<NriTextureUpload> textures, ReadOnlySpan<NriBufferUpload> buffers)
    {
        var pinCount = buffers.Length;
        foreach (ref readonly var t in textures) pinCount += t.Subresources?.Length ?? 0;
        var pins = new System.Buffers.MemoryHandle[pinCount];
        var pinned = 0;
        var arena = Interop.ScratchArena.Rent();
        try
        {
            var nativeTextures = arena.Alloc<TextureUploadDesc>(textures.Length);
            for (var i = 0; i < textures.Length; i++)
            {
                ref readonly var t = ref textures[i];
                TextureSubresourceUploadDesc* subresources = null;
                if (t.Subresources is { Length: > 0 } source)
                {
                    ref readonly var desc = ref t.Texture.Desc;
                    var expected = desc.layerNum * desc.mipNum;
                    if (source.Length != expected)
                        throw new ArgumentException($"textures[{i}]: {source.Length} subresources for {desc.layerNum} layers x {desc.mipNum} mips; NRI reads all {expected}, layer-major", nameof(textures));
                    subresources = arena.Alloc<TextureSubresourceUploadDesc>(expected);
                    for (var j = 0; j < source.Length; j++)
                    {
                        var s = source[j];
                        if ((ulong)s.Slices.Length < (ulong)s.SlicePitch * s.SliceNum)
                            throw new ArgumentException($"textures[{i}].Subresources[{j}]: {s.Slices.Length} bytes for {s.SliceNum} slices at pitch {s.SlicePitch}", nameof(textures));
                        pins[pinned] = s.Slices.Pin();
                        subresources[j] = new TextureSubresourceUploadDesc { slices = pins[pinned++].Pointer, sliceNum = s.SliceNum, rowPitch = s.RowPitch, slicePitch = s.SlicePitch };
                    }
                }
                nativeTextures[i] = new TextureUploadDesc { subresources = subresources, texture = t.Texture.Raw, after = t.After, planes = t.Planes };
            }

            var nativeBuffers = arena.Alloc<BufferUploadDesc>(buffers.Length);
            for (var i = 0; i < buffers.Length; i++)
            {
                ref readonly var b = ref buffers[i];
                void* data = null;
                if (!b.Data.IsEmpty)
                {
                    var size = b.Buffer.Desc.size;
                    if ((ulong)b.Data.Length < size)
                        throw new ArgumentException($"buffers[{i}]: {b.Data.Length} bytes for a {size}-byte buffer; NRI reads the whole buffer", nameof(buffers));
                    pins[pinned] = b.Data.Pin();
                    data = pins[pinned++].Pointer;
                }
                nativeBuffers[i] = new BufferUploadDesc { data = data, buffer = b.Buffer.Raw, after = b.After };
            }

            Device.Check(Device.Helper.UploadData(_handle, nativeTextures, (uint)textures.Length, nativeBuffers, (uint)buffers.Length), "UploadData");
        }
        finally
        {
            for (var i = 0; i < pinned; i++) pins[i].Dispose();
            arena.Return();
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
    /// Sets a debug name on the native queue (<c>ID3D12CommandQueue</c>, <c>VkQueue</c>), shown by
    /// validation layers and GPU debuggers, as <see cref="NriObject.SetDebugName"/> does for objects.
    /// </summary>
    public void SetDebugName(string name)
    {
        using var utf8 = new Interop.Utf8.Scoped(name);
        Device.Core.SetDebugName(_handle, utf8);
    }

    /// <summary>
    /// The native queue: <c>ID3D12CommandQueue*</c> on D3D12, <c>VkQueue</c> on Vulkan. Last resort.
    /// </summary>
    public nint GetNativeObject() => (nint)Device.Core.GetQueueNativeObject(_handle);

    public override string ToString() => $"{Type} queue {Index}";
}
