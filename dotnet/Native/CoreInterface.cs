namespace PrismNRI.Native;

public unsafe partial struct CoreInterface
{
    [NativeTypeName("const DeviceDesc &(*)(const Device &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, DeviceDesc*> GetDeviceDesc;

    [NativeTypeName("const BufferDesc &(*)(const Buffer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Buffer*, BufferDesc*> GetBufferDesc;

    [NativeTypeName("const TextureDesc &(*)(const Texture &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Texture*, TextureDesc*> GetTextureDesc;

    [NativeTypeName("FormatSupportBits (*)(const Device &, Format) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, Format, FormatSupportBits> GetFormatSupport;

    [NativeTypeName("Result (*)(Device &, QueueType, uint32_t, Queue *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, QueueType, uint, Queue**, Result> GetQueue;

    [NativeTypeName("Result (*)(Queue &, CommandAllocator *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, CommandAllocator**, Result> CreateCommandAllocator;

    [NativeTypeName("Result (*)(CommandAllocator &, CommandBuffer *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandAllocator*, CommandBuffer**, Result> CreateCommandBuffer;

    [NativeTypeName("Result (*)(Device &, uint64_t, Fence *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, ulong, Fence**, Result> CreateFence;

    [NativeTypeName("Result (*)(Device &, const DescriptorPoolDesc &, DescriptorPool *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, DescriptorPoolDesc*, DescriptorPool**, Result> CreateDescriptorPool;

    [NativeTypeName("Result (*)(Device &, const PipelineLayoutDesc &, PipelineLayout *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, PipelineLayoutDesc*, PipelineLayout**, Result> CreatePipelineLayout;

    [NativeTypeName("Result (*)(Device &, const GraphicsPipelineDesc &, Pipeline *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, GraphicsPipelineDesc*, Pipeline**, Result> CreateGraphicsPipeline;

    [NativeTypeName("Result (*)(Device &, const ComputePipelineDesc &, Pipeline *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, ComputePipelineDesc*, Pipeline**, Result> CreateComputePipeline;

    [NativeTypeName("Result (*)(Device &, const PipelineCacheDesc &, PipelineCache *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, PipelineCacheDesc*, PipelineCache**, Result> CreatePipelineCache;

    [NativeTypeName("Result (*)(Device &, const QueryPoolDesc &, QueryPool *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, QueryPoolDesc*, QueryPool**, Result> CreateQueryPool;

    [NativeTypeName("Result (*)(Device &, const SamplerDesc &, Descriptor *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, SamplerDesc*, Descriptor**, Result> CreateSampler;

    [NativeTypeName("Result (*)(const BufferViewDesc &, Descriptor *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<BufferViewDesc*, Descriptor**, Result> CreateBufferView;

    [NativeTypeName("Result (*)(const TextureViewDesc &, Descriptor *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<TextureViewDesc*, Descriptor**, Result> CreateTextureView;

    [NativeTypeName("void (*)(CommandAllocator *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandAllocator*, void> DestroyCommandAllocator;

    [NativeTypeName("void (*)(CommandBuffer *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, void> DestroyCommandBuffer;

    [NativeTypeName("void (*)(DescriptorPool *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<DescriptorPool*, void> DestroyDescriptorPool;

    [NativeTypeName("void (*)(Buffer *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Buffer*, void> DestroyBuffer;

    [NativeTypeName("void (*)(Texture *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Texture*, void> DestroyTexture;

    [NativeTypeName("void (*)(Descriptor *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Descriptor*, void> DestroyDescriptor;

    [NativeTypeName("void (*)(PipelineLayout *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<PipelineLayout*, void> DestroyPipelineLayout;

    [NativeTypeName("void (*)(Pipeline *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Pipeline*, void> DestroyPipeline;

    [NativeTypeName("void (*)(PipelineCache *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<PipelineCache*, void> DestroyPipelineCache;

    [NativeTypeName("void (*)(QueryPool *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<QueryPool*, void> DestroyQueryPool;

    [NativeTypeName("void (*)(Fence *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Fence*, void> DestroyFence;

    [NativeTypeName("Result (*)(Device &, const AllocateMemoryDesc &, Memory *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, AllocateMemoryDesc*, Memory**, Result> AllocateMemory;

    [NativeTypeName("void (*)(Memory *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Memory*, void> FreeMemory;

    [NativeTypeName("Result (*)(Device &, const BufferDesc &, Buffer *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, BufferDesc*, Buffer**, Result> CreateBuffer;

    [NativeTypeName("Result (*)(Device &, const TextureDesc &, Texture *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, TextureDesc*, Texture**, Result> CreateTexture;

    [NativeTypeName("void (*)(const Buffer &, MemoryLocation, MemoryDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Buffer*, MemoryLocation, MemoryDesc*, void> GetBufferMemoryDesc;

    [NativeTypeName("void (*)(const Texture &, MemoryLocation, MemoryDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Texture*, MemoryLocation, MemoryDesc*, void> GetTextureMemoryDesc;

    [NativeTypeName("Result (*)(const BindBufferMemoryDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<BindBufferMemoryDesc*, uint, Result> BindBufferMemory;

    [NativeTypeName("Result (*)(const BindTextureMemoryDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<BindTextureMemoryDesc*, uint, Result> BindTextureMemory;

    [NativeTypeName("void (*)(const Device &, const BufferDesc &, MemoryLocation, MemoryDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, BufferDesc*, MemoryLocation, MemoryDesc*, void> GetBufferMemoryDesc2;

    [NativeTypeName("void (*)(const Device &, const TextureDesc &, MemoryLocation, MemoryDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, TextureDesc*, MemoryLocation, MemoryDesc*, void> GetTextureMemoryDesc2;

    [NativeTypeName("Result (*)(Device &, MemoryLocation, float, const BufferDesc &, Buffer *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, MemoryLocation, float, BufferDesc*, Buffer**, Result> CreateCommittedBuffer;

    [NativeTypeName("Result (*)(Device &, MemoryLocation, float, const TextureDesc &, Texture *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, MemoryLocation, float, TextureDesc*, Texture**, Result> CreateCommittedTexture;

    [NativeTypeName("Result (*)(Device &, Memory *, uint64_t, const BufferDesc &, Buffer *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, Memory*, ulong, BufferDesc*, Buffer**, Result> CreatePlacedBuffer;

    [NativeTypeName("Result (*)(Device &, Memory *, uint64_t, const TextureDesc &, Texture *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, Memory*, ulong, TextureDesc*, Texture**, Result> CreatePlacedTexture;

    [NativeTypeName("Result (*)(DescriptorPool &, const PipelineLayout &, uint32_t, DescriptorSet **, uint32_t, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<DescriptorPool*, PipelineLayout*, uint, DescriptorSet**, uint, uint, Result> AllocateDescriptorSets;

    [NativeTypeName("void (*)(const UpdateDescriptorRangeDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<UpdateDescriptorRangeDesc*, uint, void> UpdateDescriptorRanges;

    [NativeTypeName("void (*)(const CopyDescriptorRangeDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CopyDescriptorRangeDesc*, uint, void> CopyDescriptorRanges;

    [NativeTypeName("void (*)(DescriptorPool &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<DescriptorPool*, void> ResetDescriptorPool;

    [NativeTypeName("void (*)(const DescriptorSet &, uint32_t &, uint32_t &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<DescriptorSet*, uint*, uint*, void> GetDescriptorSetOffsets;

    [NativeTypeName("Result (*)(CommandBuffer &, const DescriptorPool *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DescriptorPool*, Result> BeginCommandBuffer;

    [NativeTypeName("void (*)(CommandBuffer &, const DescriptorPool &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DescriptorPool*, void> CmdSetDescriptorPool;

    [NativeTypeName("void (*)(CommandBuffer &, BindPoint, const PipelineLayout &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, BindPoint, PipelineLayout*, void> CmdSetPipelineLayout;

    [NativeTypeName("void (*)(CommandBuffer &, const SetDescriptorSetDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, SetDescriptorSetDesc*, void> CmdSetDescriptorSet;

    [NativeTypeName("void (*)(CommandBuffer &, const SetRootConstantsDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, SetRootConstantsDesc*, void> CmdSetRootConstants;

    [NativeTypeName("void (*)(CommandBuffer &, const SetRootDescriptorDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, SetRootDescriptorDesc*, void> CmdSetRootDescriptor;

    [NativeTypeName("void (*)(CommandBuffer &, const Pipeline &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Pipeline*, void> CmdSetPipeline;

    [NativeTypeName("void (*)(CommandBuffer &, const BarrierDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, BarrierDesc*, void> CmdBarrier;

    [NativeTypeName("void (*)(CommandBuffer &, const Buffer &, uint64_t, IndexType) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, ulong, IndexType, void> CmdSetIndexBuffer;

    [NativeTypeName("void (*)(CommandBuffer &, uint32_t, const VertexBufferDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, uint, VertexBufferDesc*, uint, void> CmdSetVertexBuffers;

    [NativeTypeName("void (*)(CommandBuffer &, const Viewport *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Viewport*, uint, void> CmdSetViewports;

    [NativeTypeName("void (*)(CommandBuffer &, const Rect *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Rect*, uint, void> CmdSetScissors;

    [NativeTypeName("void (*)(CommandBuffer &, uint8_t, uint8_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, byte, byte, void> CmdSetStencilReference;

    [NativeTypeName("void (*)(CommandBuffer &, float, float) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, float, float, void> CmdSetDepthBounds;

    [NativeTypeName("void (*)(CommandBuffer &, const Color32f &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Color32f*, void> CmdSetBlendConstants;

    [NativeTypeName("void (*)(CommandBuffer &, const SampleLocation *, Sample_t, Sample_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, SampleLocation*, byte, byte, void> CmdSetSampleLocations;

    [NativeTypeName("void (*)(CommandBuffer &, const ShadingRateDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, ShadingRateDesc*, void> CmdSetShadingRate;

    [NativeTypeName("void (*)(CommandBuffer &, const DepthBiasDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DepthBiasDesc*, void> CmdSetDepthBias;

    [NativeTypeName("void (*)(CommandBuffer &, const RenderingDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, RenderingDesc*, void> CmdBeginRendering;

    [NativeTypeName("void (*)(CommandBuffer &, const ClearAttachmentDesc *, uint32_t, const Rect *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, ClearAttachmentDesc*, uint, Rect*, uint, void> CmdClearAttachments;

    [NativeTypeName("void (*)(CommandBuffer &, const DrawDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DrawDesc*, void> CmdDraw;

    [NativeTypeName("void (*)(CommandBuffer &, const DrawIndexedDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DrawIndexedDesc*, void> CmdDrawIndexed;

    [NativeTypeName("void (*)(CommandBuffer &, const Buffer &, uint64_t, uint32_t, uint32_t, const Buffer *, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, ulong, uint, uint, Buffer*, ulong, void> CmdDrawIndirect;

    [NativeTypeName("void (*)(CommandBuffer &, const Buffer &, uint64_t, uint32_t, uint32_t, const Buffer *, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, ulong, uint, uint, Buffer*, ulong, void> CmdDrawIndexedIndirect;

    [NativeTypeName("void (*)(CommandBuffer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, void> CmdEndRendering;

    [NativeTypeName("void (*)(CommandBuffer &, const DispatchDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DispatchDesc*, void> CmdDispatch;

    [NativeTypeName("void (*)(CommandBuffer &, const Buffer &, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, ulong, void> CmdDispatchIndirect;

    [NativeTypeName("void (*)(CommandBuffer &, Buffer &, uint64_t, const Buffer &, uint64_t, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, ulong, Buffer*, ulong, ulong, void> CmdCopyBuffer;

    [NativeTypeName("void (*)(CommandBuffer &, Texture &, const TextureRegionDesc *, const Texture &, const TextureRegionDesc *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Texture*, TextureRegionDesc*, Texture*, TextureRegionDesc*, void> CmdCopyTexture;

    [NativeTypeName("void (*)(CommandBuffer &, Texture &, const TextureRegionDesc &, const Buffer &, const TextureDataLayoutDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Texture*, TextureRegionDesc*, Buffer*, TextureDataLayoutDesc*, void> CmdUploadBufferToTexture;

    [NativeTypeName("void (*)(CommandBuffer &, Buffer &, const TextureDataLayoutDesc &, const Texture &, const TextureRegionDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, TextureDataLayoutDesc*, Texture*, TextureRegionDesc*, void> CmdReadbackTextureToBuffer;

    [NativeTypeName("void (*)(CommandBuffer &, Buffer &, uint64_t, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, ulong, ulong, void> CmdZeroBuffer;

    [NativeTypeName("void (*)(CommandBuffer &, Texture &, const TextureRegionDesc *, const Texture &, const TextureRegionDesc *, ResolveOp) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Texture*, TextureRegionDesc*, Texture*, TextureRegionDesc*, ResolveOp, void> CmdResolveTexture;

    [NativeTypeName("void (*)(CommandBuffer &, const ClearStorageDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, ClearStorageDesc*, void> CmdClearStorage;

    [NativeTypeName("void (*)(CommandBuffer &, QueryPool &, uint32_t, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, QueryPool*, uint, uint, void> CmdResetQueries;

    [NativeTypeName("void (*)(CommandBuffer &, QueryPool &, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, QueryPool*, uint, void> CmdBeginQuery;

    [NativeTypeName("void (*)(CommandBuffer &, QueryPool &, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, QueryPool*, uint, void> CmdEndQuery;

    [NativeTypeName("void (*)(CommandBuffer &, const QueryPool &, uint32_t, uint32_t, Buffer &, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, QueryPool*, uint, uint, Buffer*, ulong, void> CmdCopyQueries;

    [NativeTypeName("void (*)(CommandBuffer &, const char *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, sbyte*, uint, void> CmdBeginAnnotation;

    [NativeTypeName("void (*)(CommandBuffer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, void> CmdEndAnnotation;

    [NativeTypeName("void (*)(CommandBuffer &, const char *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, sbyte*, uint, void> CmdAnnotation;

    [NativeTypeName("Result (*)(CommandBuffer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Result> EndCommandBuffer;

    [NativeTypeName("void (*)(Queue &, const char *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, sbyte*, uint, void> QueueBeginAnnotation;

    [NativeTypeName("void (*)(Queue &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, void> QueueEndAnnotation;

    [NativeTypeName("void (*)(Queue &, const char *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, sbyte*, uint, void> QueueAnnotation;

    [NativeTypeName("void (*)(QueryPool &, uint32_t, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<QueryPool*, uint, uint, void> ResetQueries;

    [NativeTypeName("uint32_t (*)(const QueryPool &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<QueryPool*, uint> GetQuerySize;

    [NativeTypeName("void (*)(Queue &, uint64_t &, uint64_t &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, ulong*, ulong*, void> GetCalibratedTimestamps;

    [NativeTypeName("Result (*)(Queue &, const QueueSubmitDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, QueueSubmitDesc*, Result> QueueSubmit;

    [NativeTypeName("Result (*)(Queue *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, Result> QueueWaitIdle;

    [NativeTypeName("Result (*)(Device *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, Result> DeviceWaitIdle;

    [NativeTypeName("void (*)(Fence &, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Fence*, ulong, void> Wait;

    [NativeTypeName("uint64_t (*)(Fence &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Fence*, ulong> GetFenceValue;

    [NativeTypeName("void (*)(CommandAllocator &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandAllocator*, void> ResetCommandAllocator;

    [NativeTypeName("void *(*)(Buffer &, uint64_t, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Buffer*, ulong, ulong, void*> MapBuffer;

    [NativeTypeName("void (*)(Buffer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Buffer*, void> UnmapBuffer;

    [NativeTypeName("Result (*)(Queue &, const UploadHostMemoryToTextureDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, UploadHostMemoryToTextureDesc*, uint, Result> UploadHostMemoryToTexture;

    [NativeTypeName("Result (*)(Queue &, const ReadbackTextureToHostMemoryDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, ReadbackTextureToHostMemoryDesc*, uint, Result> ReadbackTextureToHostMemory;

    [NativeTypeName("uint64_t (*)(const Buffer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Buffer*, ulong> GetBufferDeviceAddress;

    [NativeTypeName("Result (*)(PipelineCache &, void *, uint64_t &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<PipelineCache*, void*, ulong*, Result> GetPipelineCacheData;

    [NativeTypeName("void (*)(Object *, const char *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<void*, sbyte*, void> SetDebugName;

    [NativeTypeName("void *(*)(const Device *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, void*> GetDeviceNativeObject;

    [NativeTypeName("void *(*)(const Queue *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, void*> GetQueueNativeObject;

    [NativeTypeName("void *(*)(const CommandBuffer *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, void*> GetCommandBufferNativeObject;

    [NativeTypeName("uint64_t (*)(const Buffer *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Buffer*, ulong> GetBufferNativeObject;

    [NativeTypeName("uint64_t (*)(const Texture *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Texture*, ulong> GetTextureNativeObject;

    [NativeTypeName("uint64_t (*)(const Descriptor *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Descriptor*, ulong> GetDescriptorNativeObject;
}
