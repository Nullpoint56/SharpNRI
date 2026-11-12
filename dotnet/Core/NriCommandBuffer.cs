// L2 — command recording.
//
// One method per Cmd* entry in CoreInterface, same name without the prefix, same argument order.
// Where NRI takes a pointer to an array of descs that themselves point at NRI objects (barriers,
// attachments, vertex buffers), the method takes a span of small records naming the wrapper and
// builds the native array on the stack: no heap allocation per call, which the allocation tests
// hold the recording path to.
//
// Not thread safe, per NRI: one thread records into one command buffer at a time.

using SharpNRI.Interop;
using SharpNRI.Native;
using Buffer = SharpNRI.Native.Buffer;

namespace SharpNRI;

/// <summary><c>BufferBarrierDesc</c> naming the wrapper.</summary>
/// <remarks>
/// <see cref="SrcQueue"/> / <see cref="DstQueue"/>: only for a queue family ownership transfer of a shared
/// buffer to or from an external user: <see cref="NriQueue.External"/> on one side (the release before an
/// export, or the acquire after an import) and the local queue executing the barrier on the other. Buffers
/// need no transfer between the device's own queues; D3D12 ignores both.
/// </remarks>
public readonly unsafe record struct NriBufferBarrier(NriBuffer Buffer, AccessStage Before, AccessStage After, NriQueue? SrcQueue = null, NriQueue? DstQueue = null)
{
    internal BufferBarrierDesc ToNative() => new()
    {
        buffer = Buffer.Raw,
        before = Before,
        after = After,
        srcQueue = SrcQueue is null ? null : SrcQueue.Raw,
        dstQueue = DstQueue is null ? null : DstQueue.Raw,
    };
}

/// <summary><c>TextureBarrierDesc</c> naming the wrapper. Defaults cover the whole texture.</summary>
/// <remarks>
/// A side whose access is <c>NONE</c> and whose layout is not <c>UNDEFINED</c>, on D3D12: with stages it
/// is recorded with the conservative <c>COMMON</c> access and the stages' sync, so the execution
/// dependency holds and the barrier may flush caches (NRI 48a14c3); with stages <c>NONE</c> it is
/// <c>SYNC_NONE</c> / <c>NO_ACCESS</c>, layout-only, which says nothing touched the subresource earlier in
/// the same submit, so the debug layer refuses it after an access in the same command list (1417).
/// On Vulkan the side is an execution dependency on its stages with no access.
/// Copies use <c>COPY_DESTINATION</c> / <c>COPY_SOURCE</c> on every queue (Vulkan's copies require the
/// transfer layouts, so not <c>GENERAL</c>). D3D12 copy lists allow only <c>COMMON</c>: NRI records the copy
/// layouts as <c>COMMON</c> in a copy list and on the copy-queue side of a barrier whose
/// <see cref="SrcQueue"/> / <see cref="DstQueue"/> is a copy queue (NRI 732f737), so a texture handed between a
/// COPY queue and another queue gets a release barrier on the source queue and the same barrier as the
/// acquire on the destination queue, naming both queues and keeping the copy layout.
/// <see cref="SrcQueue"/> / <see cref="DstQueue"/> on Vulkan: a queue family ownership transfer only for
/// a texture created <c>EXCLUSIVE</c> (or with one active queue family), or with
/// <see cref="NriQueue.External"/> on one side; on a <c>CONCURRENT</c> texture NRI writes
/// <c>VK_QUEUE_FAMILY_IGNORED</c> for every side that is not external (NRI 2603edc; the external side is
/// #294's), so the two barriers of a hand-off are ordinary barriers, each naming only its own
/// queue's stages. On D3D12 they are the copy-queue hint and nothing else.
/// </remarks>
public readonly unsafe record struct NriTextureBarrier(
    NriTexture Texture,
    AccessLayoutStage Before,
    AccessLayoutStage After,
    ushort MipOffset = 0,
    ushort MipNum = NriApi.REMAINING,
    ushort LayerOffset = 0,
    ushort LayerNum = NriApi.REMAINING,
    PlaneBits Planes = PlaneBits.ALL,
    NriQueue? SrcQueue = null,
    NriQueue? DstQueue = null)
{
    internal unsafe TextureBarrierDesc ToNative() => new()
    {
        texture = Texture.Raw,
        before = Before,
        after = After,
        mipOffset = MipOffset,
        mipNum = MipNum,
        layerOffset = LayerOffset,
        layerNum = LayerNum,
        planes = Planes,
        srcQueue = SrcQueue is null ? null : SrcQueue.Raw,
        dstQueue = DstQueue is null ? null : DstQueue.Raw,
    };
}

/// <summary><c>AttachmentDesc</c> naming the wrapper. A default value means "no attachment".</summary>
public readonly unsafe record struct NriAttachment(
    NriDescriptor Descriptor,
    ClearValue ClearValue = default,
    LoadOp LoadOp = LoadOp.LOAD,
    StoreOp StoreOp = StoreOp.STORE,
    ResolveOp ResolveOp = ResolveOp.AVERAGE,
    NriDescriptor? ResolveDst = null)
{
    internal unsafe AttachmentDesc ToNative() => new()
    {
        descriptor = Descriptor is null ? null : Descriptor.Raw,
        clearValue = ClearValue,
        loadOp = LoadOp,
        storeOp = StoreOp,
        resolveOp = ResolveOp,
        resolveDst = ResolveDst is null ? null : ResolveDst.Raw,
    };
}

/// <summary><c>VertexBufferDesc</c> naming the wrapper.</summary>
/// <param name="Buffer">The vertex buffer.</param>
/// <param name="Offset">Where the first vertex starts, in bytes.</param>
/// <param name="Stride">
/// The vertex stride. It is what the draw uses on D3D12 and on Vulkan devices with
/// <c>features.extendedDynamicState</c> (every Vulkan 1.3+ device), where the pipeline's
/// <c>VertexStreamDesc.stride</c> is ignored; 0 there reads every vertex from <paramref name="Offset"/>,
/// silently. Only a Vulkan device without the feature falls back to the pipeline's stride.
/// </param>
public readonly unsafe record struct NriVertexBuffer(NriBuffer Buffer, ulong Offset, uint Stride)
{
    internal VertexBufferDesc ToNative() => new() { buffer = Buffer.Raw, offset = Offset, stride = Stride };
}

/// <summary>A command buffer. Begin, record, End, submit; then wait before recording again.</summary>
public sealed unsafe class NriCommandBuffer : NriObject
{
    private readonly NriDevice _device;

    internal NriCommandBuffer(NriCommandAllocator allocator, CommandBuffer* handle) : base(handle, "NriCommandBuffer")
    {
        Allocator = allocator;
        _device = allocator.Device;
        _device.ChildCreated();
    }

    /// <summary>A command buffer wrapped from a native one (WrapperVK, WrapperD3D12): no NRI allocator backs it.</summary>
    internal NriCommandBuffer(NriDevice device, CommandBuffer* handle) : base(handle, "NriCommandBuffer")
    {
        _device = device;
        device.ChildCreated();
    }

    /// <summary>The allocator backing this command buffer; null for one wrapped from a native command buffer.</summary>
    public NriCommandAllocator? Allocator { get; }

    public override NriDevice Device => _device;

    internal CommandBuffer* Raw => (CommandBuffer*)Handle;

    private ref readonly CoreInterface Core => ref Device.Core;

    // ---------------------------------------------------------------- begin / end

    /// <summary>Starts recording. The pool, if given, is set for the whole buffer.</summary>
    public void Begin(NriDescriptorPool? descriptorPool = null) =>
        NriException.Check(Core.BeginCommandBuffer(Raw, descriptorPool is null ? null : descriptorPool.Raw), "BeginCommandBuffer");

    public void End() => NriException.Check(Core.EndCommandBuffer(Raw), "EndCommandBuffer");

    // ---------------------------------------------------------------- binding

    public void SetDescriptorPool(NriDescriptorPool pool) => Core.CmdSetDescriptorPool(Raw, pool.Raw);

    public void SetPipelineLayout(BindPoint bindPoint, NriPipelineLayout layout) => Core.CmdSetPipelineLayout(Raw, bindPoint, layout.Raw);

    public void SetDescriptorSet(uint setIndex, NriDescriptorSet set, BindPoint bindPoint = BindPoint.INHERIT)
    {
        var desc = new SetDescriptorSetDesc { setIndex = setIndex, descriptorSet = set.Raw, bindPoint = bindPoint };
        Core.CmdSetDescriptorSet(Raw, &desc);
    }

    public void SetRootConstants(uint rootConstantIndex, ReadOnlySpan<byte> data, uint offset = 0, BindPoint bindPoint = BindPoint.INHERIT)
    {
        fixed (byte* p = data)
        {
            var desc = new SetRootConstantsDesc { rootConstantIndex = rootConstantIndex, data = p, size = (uint)data.Length, offset = offset, bindPoint = bindPoint };
            Core.CmdSetRootConstants(Raw, &desc);
        }
    }

    public void SetRootConstants<T>(uint rootConstantIndex, in T value, uint offset = 0, BindPoint bindPoint = BindPoint.INHERIT) where T : unmanaged
    {
        fixed (T* p = &value)
        {
            var desc = new SetRootConstantsDesc { rootConstantIndex = rootConstantIndex, data = p, size = (uint)sizeof(T), offset = offset, bindPoint = bindPoint };
            Core.CmdSetRootConstants(Raw, &desc);
        }
    }

    /// <summary>
    /// Binds a buffer view (or an acceleration structure) as root descriptor
    /// <paramref name="rootDescriptorIndex"/>, <paramref name="offset"/> bytes past the view's start.
    /// The backends disagree on what the offset means: Vulkan slides the view's fixed window
    /// (the push-descriptor write takes the view's offset plus this one and keeps the view's range),
    /// D3D12 binds the view's address plus this offset with no range at all. NRI validation caps the
    /// offset at buffer size minus view offset minus view size, with a <c>WHOLE_SIZE</c> view
    /// resolved to the rest of the buffer, so a whole-buffer view accepts offset 0 only. For a
    /// sliding window, make the view as large as the window. See the guide (root descriptors).
    /// </summary>
    public void SetRootDescriptor(uint rootDescriptorIndex, NriDescriptor descriptor, uint offset = 0, BindPoint bindPoint = BindPoint.INHERIT)
    {
        var desc = new SetRootDescriptorDesc { rootDescriptorIndex = rootDescriptorIndex, descriptor = descriptor.Raw, offset = offset, bindPoint = bindPoint };
        Core.CmdSetRootDescriptor(Raw, &desc);
    }

    public void SetPipeline(NriPipeline pipeline) => Core.CmdSetPipeline(Raw, pipeline.Raw);

    // ---------------------------------------------------------------- barriers

    /// <summary>One barrier call covering any mix of global, buffer and texture barriers.</summary>
    public void Barrier(ReadOnlySpan<GlobalBarrierDesc> globals, ReadOnlySpan<NriBufferBarrier> buffers, ReadOnlySpan<NriTextureBarrier> textures)
    {
        var nativeBuffers = stackalloc BufferBarrierDesc[buffers.Length];
        for (var i = 0; i < buffers.Length; i++) nativeBuffers[i] = buffers[i].ToNative();

        var nativeTextures = stackalloc TextureBarrierDesc[textures.Length];
        for (var i = 0; i < textures.Length; i++) nativeTextures[i] = textures[i].ToNative();

        fixed (GlobalBarrierDesc* nativeGlobals = globals)
        {
            var desc = new BarrierDesc
            {
                globals = nativeGlobals,
                globalNum = (uint)globals.Length,
                buffers = nativeBuffers,
                bufferNum = (uint)buffers.Length,
                textures = nativeTextures,
                textureNum = (uint)textures.Length,
            };
            Core.CmdBarrier(Raw, &desc);
        }
    }

    public void Barrier(in NriTextureBarrier texture) => Barrier([], [], new ReadOnlySpan<NriTextureBarrier>(in texture));
    public void Barrier(in NriBufferBarrier buffer) => Barrier([], new ReadOnlySpan<NriBufferBarrier>(in buffer), []);
    public void Barrier(in GlobalBarrierDesc global) => Barrier(new ReadOnlySpan<GlobalBarrierDesc>(in global), [], []);

    // ---------------------------------------------------------------- input assembly and state

    public void SetIndexBuffer(NriBuffer buffer, ulong offset, IndexType indexType) => Core.CmdSetIndexBuffer(Raw, buffer.Raw, offset, indexType);

    public void SetVertexBuffers(uint baseSlot, ReadOnlySpan<NriVertexBuffer> buffers)
    {
        var native = stackalloc VertexBufferDesc[buffers.Length];
        for (var i = 0; i < buffers.Length; i++) native[i] = buffers[i].ToNative();
        Core.CmdSetVertexBuffers(Raw, baseSlot, native, (uint)buffers.Length);
    }

    /// <summary>One vertex buffer; <paramref name="stride"/> as on <see cref="NriVertexBuffer"/> (the pipeline's stride is ignored on D3D12 and on Vulkan with <c>extendedDynamicState</c>).</summary>
    public void SetVertexBuffer(uint slot, NriBuffer buffer, ulong offset, uint stride)
    {
        var native = new VertexBufferDesc { buffer = buffer.Raw, offset = offset, stride = stride };
        Core.CmdSetVertexBuffers(Raw, slot, &native, 1);
    }

    public void SetViewports(ReadOnlySpan<Viewport> viewports)
    {
        fixed (Viewport* p = viewports) Core.CmdSetViewports(Raw, p, (uint)viewports.Length);
    }

    public void SetViewport(in Viewport viewport) => SetViewports(new ReadOnlySpan<Viewport>(in viewport));

    public void SetScissors(ReadOnlySpan<Rect> rects)
    {
        fixed (Rect* p = rects) Core.CmdSetScissors(Raw, p, (uint)rects.Length);
    }

    public void SetScissor(in Rect rect) => SetScissors(new ReadOnlySpan<Rect>(in rect));

    public void SetStencilReference(byte frontRef, byte backRef) => Core.CmdSetStencilReference(Raw, frontRef, backRef);
    public void SetDepthBounds(float boundsMin, float boundsMax) => Core.CmdSetDepthBounds(Raw, boundsMin, boundsMax);

    public void SetBlendConstants(in Color32f color)
    {
        fixed (Color32f* p = &color) Core.CmdSetBlendConstants(Raw, p);
    }

    public void SetSampleLocations(ReadOnlySpan<SampleLocation> locations, byte sampleNum)
    {
        fixed (SampleLocation* p = locations) Core.CmdSetSampleLocations(Raw, p, checked((byte)locations.Length), sampleNum);
    }

    public void SetShadingRate(in ShadingRateDesc desc)
    {
        fixed (ShadingRateDesc* p = &desc) Core.CmdSetShadingRate(Raw, p);
    }

    public void SetDepthBias(in DepthBiasDesc desc)
    {
        fixed (DepthBiasDesc* p = &desc) Core.CmdSetDepthBias(Raw, p);
    }

    // ---------------------------------------------------------------- rendering

    /// <summary>
    /// Begins a render pass. Pass a default <see cref="NriAttachment"/> for no depth or stencil.
    /// </summary>
    public void BeginRendering(ReadOnlySpan<NriAttachment> colors, in NriAttachment depth = default, in NriAttachment stencil = default, NriDescriptor? shadingRate = null, uint viewMask = 0)
    {
        var native = stackalloc AttachmentDesc[colors.Length];
        for (var i = 0; i < colors.Length; i++) native[i] = colors[i].ToNative();

        var desc = new RenderingDesc
        {
            colors = native,
            colorNum = (uint)colors.Length,
            depth = depth.ToNative(),
            stencil = stencil.ToNative(),
            shadingRate = shadingRate is null ? null : shadingRate.Raw,
            viewMask = viewMask,
        };
        Core.CmdBeginRendering(Raw, &desc);
    }

    public void EndRendering() => Core.CmdEndRendering(Raw);

    public void ClearAttachments(ReadOnlySpan<ClearAttachmentDesc> attachments, ReadOnlySpan<Rect> rects = default)
    {
        fixed (ClearAttachmentDesc* a = attachments)
        fixed (Rect* r = rects)
        {
            Core.CmdClearAttachments(Raw, a, (uint)attachments.Length, r, (uint)rects.Length);
        }
    }

    public void Draw(in DrawDesc desc)
    {
        fixed (DrawDesc* p = &desc) Core.CmdDraw(Raw, p);
    }

    public void Draw(uint vertexNum, uint instanceNum = 1, uint baseVertex = 0, uint baseInstance = 0) =>
        Draw(new DrawDesc { vertexNum = vertexNum, instanceNum = instanceNum, baseVertex = baseVertex, baseInstance = baseInstance });

    public void DrawIndexed(in DrawIndexedDesc desc)
    {
        fixed (DrawIndexedDesc* p = &desc) Core.CmdDrawIndexed(Raw, p);
    }

    public void DrawIndexed(uint indexNum, uint instanceNum = 1, uint baseIndex = 0, int baseVertex = 0, uint baseInstance = 0) =>
        DrawIndexed(new DrawIndexedDesc { indexNum = indexNum, instanceNum = instanceNum, baseIndex = baseIndex, baseVertex = baseVertex, baseInstance = baseInstance });

    public void DrawIndirect(NriBuffer buffer, ulong offset, uint drawNum, uint stride, NriBuffer? countBuffer = null, ulong countBufferOffset = 0) =>
        Core.CmdDrawIndirect(Raw, buffer.Raw, offset, drawNum, stride, countBuffer is null ? null : countBuffer.Raw, countBufferOffset);

    public void DrawIndexedIndirect(NriBuffer buffer, ulong offset, uint drawNum, uint stride, NriBuffer? countBuffer = null, ulong countBufferOffset = 0) =>
        Core.CmdDrawIndexedIndirect(Raw, buffer.Raw, offset, drawNum, stride, countBuffer is null ? null : countBuffer.Raw, countBufferOffset);

    // ---------------------------------------------------------------- compute

    public void Dispatch(in DispatchDesc desc)
    {
        fixed (DispatchDesc* p = &desc) Core.CmdDispatch(Raw, p);
    }

    public void Dispatch(uint x, uint y = 1, uint z = 1) => Dispatch(new DispatchDesc { workGroupNumX = x, workGroupNumY = y, workGroupNumZ = z });

    public void DispatchIndirect(NriBuffer buffer, ulong offset) => Core.CmdDispatchIndirect(Raw, buffer.Raw, offset);

    // ---------------------------------------------------------------- copy, clear, resolve

    public void CopyBuffer(NriBuffer dst, ulong dstOffset, NriBuffer src, ulong srcOffset, ulong size) =>
        Core.CmdCopyBuffer(Raw, dst.Raw, dstOffset, src.Raw, srcOffset, size);

    /// <summary>
    /// Whole-texture copy when both regions are null. Source in <c>COPY_SOURCE</c>, destination in
    /// <c>COPY_DESTINATION</c>: Vulkan records the transfer layouts whatever the barriers said.
    /// </summary>
    public void CopyTexture(NriTexture dst, TextureRegionDesc? dstRegion, NriTexture src, TextureRegionDesc? srcRegion)
    {
        var d = dstRegion ?? default;
        var s = srcRegion ?? default;
        Core.CmdCopyTexture(Raw, dst.Raw, dstRegion is null ? null : &d, src.Raw, srcRegion is null ? null : &s);
    }

    /// <summary>
    /// Buffer-to-texture copy. The texture must be in <c>COPY_DESTINATION</c>: Vulkan records the copy in
    /// <c>TRANSFER_DST_OPTIMAL</c> whatever the barrier said, so <c>GENERAL</c> is refused there. On a COPY queue
    /// see <see cref="NriTextureBarrier"/> for the hand-off.
    /// </summary>
    public void UploadBufferToTexture(NriTexture dst, in TextureRegionDesc dstRegion, NriBuffer src, in TextureDataLayoutDesc srcDataLayout)
    {
        fixed (TextureRegionDesc* r = &dstRegion)
        fixed (TextureDataLayoutDesc* l = &srcDataLayout)
        {
            Core.CmdUploadBufferToTexture(Raw, dst.Raw, r, src.Raw, l);
        }
    }

    /// <summary>Texture-to-buffer copy; the texture must be in <c>COPY_SOURCE</c> (Vulkan records <c>TRANSFER_SRC_OPTIMAL</c>).</summary>
    public void ReadbackTextureToBuffer(NriBuffer dst, in TextureDataLayoutDesc dstDataLayout, NriTexture src, in TextureRegionDesc srcRegion)
    {
        fixed (TextureDataLayoutDesc* l = &dstDataLayout)
        fixed (TextureRegionDesc* r = &srcRegion)
        {
            Core.CmdReadbackTextureToBuffer(Raw, dst.Raw, l, src.Raw, r);
        }
    }

    /// <summary>
    /// Fills a range with zeros. In barriers it is stage <c>COPY</c>, access <c>COPY_DESTINATION</c>, on
    /// both backends: <c>CopyBufferRegion</c> from a zero buffer on D3D12, <c>vkCmdFillBuffer</c> (a clear
    /// command, which NRI's <c>COPY</c> stage covers since 1163658) on Vulkan. Not <c>CLEAR_STORAGE</c>.
    /// <paramref name="offset"/> and the size (the buffer's size for <c>WHOLE_SIZE</c>) must be multiples
    /// of 4, which NRI validation checks since 7ea93bb5.
    /// </summary>
    public void ZeroBuffer(NriBuffer buffer, ulong offset, ulong size) => Core.CmdZeroBuffer(Raw, buffer.Raw, offset, size);

    public void ResolveTexture(NriTexture dst, TextureRegionDesc? dstRegion, NriTexture src, TextureRegionDesc? srcRegion, ResolveOp op)
    {
        var d = dstRegion ?? default;
        var s = srcRegion ?? default;
        Core.CmdResolveTexture(Raw, dst.Raw, dstRegion is null ? null : &d, src.Raw, srcRegion is null ? null : &s, op);
    }

    public void ClearStorage(NriDescriptor descriptor, in Color value, uint setIndex, uint rangeIndex, uint descriptorIndex)
    {
        var desc = new ClearStorageDesc { descriptor = descriptor.Raw, value = value, setIndex = setIndex, rangeIndex = rangeIndex, descriptorIndex = descriptorIndex };
        Core.CmdClearStorage(Raw, &desc);
    }

    // ---------------------------------------------------------------- queries

    public void ResetQueries(NriQueryPool pool, uint offset, uint num) => Core.CmdResetQueries(Raw, pool.Raw, offset, num);
    public void BeginQuery(NriQueryPool pool, uint offset) => Core.CmdBeginQuery(Raw, pool.Raw, offset);
    public void EndQuery(NriQueryPool pool, uint offset) => Core.CmdEndQuery(Raw, pool.Raw, offset);
    public void CopyQueries(NriQueryPool pool, uint offset, uint num, NriBuffer dst, ulong dstOffset) => Core.CmdCopyQueries(Raw, pool.Raw, offset, num, dst.Raw, dstOffset);

    // ---------------------------------------------------------------- annotations

    public void BeginAnnotation(string name, uint bgra = NriApi.BGRA_UNUSED)
    {
        using var utf8 = new Utf8.Scoped(name, stackalloc byte[Utf8.StackBytes]);
        Core.CmdBeginAnnotation(Raw, utf8, bgra);
    }

    public void EndAnnotation() => Core.CmdEndAnnotation(Raw);

    public void Annotation(string name, uint bgra = NriApi.BGRA_UNUSED)
    {
        using var utf8 = new Utf8.Scoped(name, stackalloc byte[Utf8.StackBytes]);
        Core.CmdAnnotation(Raw, utf8, bgra);
    }

    /// <summary><c>ID3D12CommandList*</c> on D3D12, <c>VkCommandBuffer</c> on Vulkan. Last resort.</summary>
    public nint GetNativeObject() => (nint)Core.GetCommandBufferNativeObject(Raw);

    private protected override void Destroy(void* handle) => Core.DestroyCommandBuffer((CommandBuffer*)handle);
}
