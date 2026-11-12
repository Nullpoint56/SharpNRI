namespace SharpNRI.Native;

/// <summary>Threadsafe: yes</summary>
public unsafe partial struct CoreInterface
{
    /// <summary>Get</summary>
    [NativeTypeName("const DeviceDesc &(*)(const Device &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, DeviceDesc*> GetDeviceDesc;

    [NativeTypeName("const BufferDesc &(*)(const Buffer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Buffer*, BufferDesc*> GetBufferDesc;

    [NativeTypeName("const TextureDesc &(*)(const Texture &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Texture*, TextureDesc*> GetTextureDesc;

    [NativeTypeName("FormatSupportBits (*)(const Device &, Format) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, Format, FormatSupportBits> GetFormatSupport;

    /// <summary>Returns a pre-created queue (see "DeviceCreationDesc" or wrapper extensions). "UNSUPPORTED" if no queues of "queueType"; "INVALID_ARGUMENT" if "queueIndex" is out of range. VK: with only one queue type obtained (e.g. "GRAPHICS"), NRI can use "EXCLUSIVE" sharing internally: no local ownership transfers are needed. Additional queues (e.g. "COMPUTE" or "COPY") enable "CONCURRENT" sharing for new resources where needed. Get all participating queues before resource creation; explicit "SharingMode::EXCLUSIVE" textures are exempt. Buffers follow D3D12: ownership transfers are only needed to or from an external user</summary>
    [NativeTypeName("Result (*)(Device &, QueueType, uint32_t, Queue *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, QueueType, uint, Queue**, Result> GetQueue;

    /// <summary>Create (doesn't assume allocation of big chunks of memory on the device, but it happens for some entities implicitly)</summary>
    [NativeTypeName("Result (*)(Queue &, CommandAllocator *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, CommandAllocator**, Result> CreateCommandAllocator;

    [NativeTypeName("Result (*)(CommandAllocator &, CommandBuffer *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandAllocator*, CommandBuffer**, Result> CreateCommandBuffer;

    [NativeTypeName("Result (*)(Device &, const FenceDesc &, Fence *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, FenceDesc*, Fence**, Result> CreateFence;

    [NativeTypeName("Result (*)(Device &, const DescriptorPoolDesc &, DescriptorPool *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, DescriptorPoolDesc*, DescriptorPool**, Result> CreateDescriptorPool;

    [NativeTypeName("Result (*)(Device &, const PipelineLayoutDesc &, PipelineLayout *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, PipelineLayoutDesc*, PipelineLayout**, Result> CreatePipelineLayout;

    [NativeTypeName("Result (*)(Device &, const GraphicsPipelineDesc &, Pipeline *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, GraphicsPipelineDesc*, Pipeline**, Result> CreateGraphicsPipeline;

    [NativeTypeName("Result (*)(Device &, const ComputePipelineDesc &, Pipeline *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, ComputePipelineDesc*, Pipeline**, Result> CreateComputePipeline;

    /// <summary>"OUT_OF_DATE" is returned on stale data, try to start over with an empty cache</summary>
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

    /// <summary>Destroy</summary>
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

    /// <summary>Memory</summary>
    [NativeTypeName("Result (*)(Device &, const AllocateMemoryDesc &, Memory *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, AllocateMemoryDesc*, Memory**, Result> AllocateMemory;

    [NativeTypeName("void (*)(Memory *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Memory*, void> FreeMemory;

    /// <summary>Resources and memory (VK style) - create a resource (buffer or texture) - use "Get[Resource]MemoryDesc" to get "MemoryDesc" ("usageBits" and "MemoryLocation" affect returned "MemoryType") - (optional) group returned "MemoryDesc"s by "MemoryType", but don't group if "mustBeDedicated = true" - (optional) sort returned "MemoryDesc"s by alignment - call "AllocateMemory" (even if "mustBeDedicated = true") - call "Bind[Resource]Memory" to bind resources to "Memory" objects - (optional) "CalculateAllocationNumber" and "AllocateAndBindMemory" from "NRIHelper" interface simplify this process for buffers and textures</summary>
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

    /// <summary>Resources and memory (D3D12 style) - "Get[Resource]MemoryDesc2" requires "maintenance4" support on Vulkan - "memory, offset" pair can be replaced with a "Nri[Device/DeviceUpload/HostUpload/HostReadback]Heap" macro to create a placed resource in the corresponding memory using VMA (AMD Virtual Memory Allocator) implicitly requires "features.getMemoryDesc2"</summary>
    [NativeTypeName("void (*)(const Device &, const BufferDesc &, MemoryLocation, MemoryDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, BufferDesc*, MemoryLocation, MemoryDesc*, void> GetBufferMemoryDesc2;

    /// <summary>requires "features.getMemoryDesc2"</summary>
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

    /// <summary>Descriptor set management (entities don't require destroying) - if "ALLOW_UPDATE_AFTER_SET" not used, descriptor sets (and data pointed to by descriptors) must be updated before "CmdSetDescriptorSet" - "ResetDescriptorPool" resets the entire pool and wipes out all allocated descriptor sets. "DescriptorSet" is a tiny struct (&lt;= 48 bytes), so lots of descriptor sets can be created in advance and reused without calling "ResetDescriptorPool" - when directly indexed, a descriptor pool backs the directly indexed arrays: - D3D12: "GetDescriptorSetOffsets" returns offsets in resource and sampler descriptor heaps - these offsets are needed in shaders, if the corresponding descriptor set is not the first allocated from the descriptor pool - VK: "GetDescriptorSetOffsets" returns "0" - use "-fvk-bind-resource-heap" and "-fvk-bind-sampler-heap" DXC options to define bindings mimicking corresponding heaps</summary>
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

    /// <summary>Command buffer (one time submit)</summary>
    [NativeTypeName("Result (*)(CommandBuffer &, const DescriptorPool *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DescriptorPool*, Result> BeginCommandBuffer;

    /// <summary>{ { Set descriptor pool (initially can be set via "BeginCommandBuffer")</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const DescriptorPool &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DescriptorPool*, void> CmdSetDescriptorPool;

    /// <summary>Resource binding (expect "CmdSetPipelineLayout" to be called first)</summary>
    [NativeTypeName("void (*)(CommandBuffer &, BindPoint, const PipelineLayout &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, BindPoint, PipelineLayout*, void> CmdSetPipelineLayout;

    [NativeTypeName("void (*)(CommandBuffer &, const SetDescriptorSetDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, SetDescriptorSetDesc*, void> CmdSetDescriptorSet;

    [NativeTypeName("void (*)(CommandBuffer &, const SetRootConstantsDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, SetRootConstantsDesc*, void> CmdSetRootConstants;

    [NativeTypeName("void (*)(CommandBuffer &, const SetRootDescriptorDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, SetRootDescriptorDesc*, void> CmdSetRootDescriptor;

    /// <summary>Pipeline</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const Pipeline &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Pipeline*, void> CmdSetPipeline;

    /// <summary>Barrier (outside of rendering)</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const BarrierDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, BarrierDesc*, void> CmdBarrier;

    /// <summary>Input assembly</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const Buffer &, uint64_t, IndexType) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, ulong, IndexType, void> CmdSetIndexBuffer;

    [NativeTypeName("void (*)(CommandBuffer &, uint32_t, const VertexBufferDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, uint, VertexBufferDesc*, uint, void> CmdSetVertexBuffers;

    /// <summary>Initial state (mandatory)</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const Viewport *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Viewport*, uint, void> CmdSetViewports;

    [NativeTypeName("void (*)(CommandBuffer &, const Rect *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Rect*, uint, void> CmdSetScissors;

    /// <summary>Initial state (if enabled) "backRef" requires "features.independentFrontAndBackStencilReferenceAndMasks"</summary>
    [NativeTypeName("void (*)(CommandBuffer &, uint8_t, uint8_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, byte, byte, void> CmdSetStencilReference;

    /// <summary>requires "features.depthBoundsTest"</summary>
    [NativeTypeName("void (*)(CommandBuffer &, float, float) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, float, float, void> CmdSetDepthBounds;

    [NativeTypeName("void (*)(CommandBuffer &, const Color32f &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Color32f*, void> CmdSetBlendConstants;

    /// <summary>requires "tiers.sampleLocations != 0"</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const SampleLocation *, Sample_t, Sample_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, SampleLocation*, byte, byte, void> CmdSetSampleLocations;

    /// <summary>requires "tiers.shadingRate != 0"</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const ShadingRateDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, ShadingRateDesc*, void> CmdSetShadingRate;

    /// <summary>requires "features.dynamicDepthBias", actually it's an override</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const DepthBiasDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DepthBiasDesc*, void> CmdSetDepthBias;

    /// <summary>Graphics</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const RenderingDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, RenderingDesc*, void> CmdBeginRendering;

    /// <summary>{ { Clear ("rects" require the corresponding "features.rectColorClears" or "features.rectDepthStencilClears")</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const ClearAttachmentDesc *, uint32_t, const Rect *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, ClearAttachmentDesc*, uint, Rect*, uint, void> CmdClearAttachments;

    /// <summary>Draw</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const DrawDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DrawDesc*, void> CmdDraw;

    [NativeTypeName("void (*)(CommandBuffer &, const DrawIndexedDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DrawIndexedDesc*, void> CmdDrawIndexed;

    /// <summary>Draw indirect: - drawNum = min(drawNum, countBuffer ? countBuffer[countBufferOffset] : INF) - see "Modified draw command signatures" "buffer" contains "Draw(Base)Desc" commands</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const Buffer &, uint64_t, uint32_t, uint32_t, const Buffer *, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, ulong, uint, uint, Buffer*, ulong, void> CmdDrawIndirect;

    /// <summary>"buffer" contains "DrawIndexed(Base)Desc" commands</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const Buffer &, uint64_t, uint32_t, uint32_t, const Buffer *, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, ulong, uint, uint, Buffer*, ulong, void> CmdDrawIndexedIndirect;

    /// <summary>} }</summary>
    [NativeTypeName("void (*)(CommandBuffer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, void> CmdEndRendering;

    /// <summary>Compute (outside of rendering)</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const DispatchDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DispatchDesc*, void> CmdDispatch;

    /// <summary>buffer contains "DispatchDesc" commands</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const Buffer &, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, ulong, void> CmdDispatchIndirect;

    /// <summary>Copy (outside of rendering)</summary>
    [NativeTypeName("void (*)(CommandBuffer &, Buffer &, uint64_t, const Buffer &, uint64_t, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, ulong, Buffer*, ulong, ulong, void> CmdCopyBuffer;

    /// <summary>METAL: a single-plane depth-stencil copy requires identical non-multisampled formats</summary>
    [NativeTypeName("void (*)(CommandBuffer &, Texture &, const TextureRegionDesc *, const Texture &, const TextureRegionDesc *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Texture*, TextureRegionDesc*, Texture*, TextureRegionDesc*, void> CmdCopyTexture;

    [NativeTypeName("void (*)(CommandBuffer &, Texture &, const TextureRegionDesc &, const Buffer &, const TextureDataLayoutDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Texture*, TextureRegionDesc*, Buffer*, TextureDataLayoutDesc*, void> CmdUploadBufferToTexture;

    [NativeTypeName("void (*)(CommandBuffer &, Buffer &, const TextureDataLayoutDesc &, const Texture &, const TextureRegionDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, TextureDataLayoutDesc*, Texture*, TextureRegionDesc*, void> CmdReadbackTextureToBuffer;

    /// <summary>offset and size must be multiples of 4</summary>
    [NativeTypeName("void (*)(CommandBuffer &, Buffer &, uint64_t, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, ulong, ulong, void> CmdZeroBuffer;

    /// <summary>Resolve (outside of rendering). METAL: color only, use "RenderingDesc::depth.resolveDst" for depth "features.regionResolve" is needed for region specification</summary>
    [NativeTypeName("void (*)(CommandBuffer &, Texture &, const TextureRegionDesc *, const Texture &, const TextureRegionDesc *, ResolveOp) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Texture*, TextureRegionDesc*, Texture*, TextureRegionDesc*, ResolveOp, void> CmdResolveTexture;

    /// <summary>Clear (outside of rendering)</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const ClearStorageDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, ClearStorageDesc*, void> CmdClearStorage;

    /// <summary>Query and timestamps (outside of rendering, except Begin/End query) can't be used with a COPY queue</summary>
    [NativeTypeName("void (*)(CommandBuffer &, QueryPool &, uint32_t, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, QueryPool*, uint, uint, void> CmdResetQueries;

    [NativeTypeName("void (*)(CommandBuffer &, QueryPool &, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, QueryPool*, uint, void> CmdBeginQuery;

    /// <summary>writes a timestamp for timestamp query pools</summary>
    [NativeTypeName("void (*)(CommandBuffer &, QueryPool &, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, QueryPool*, uint, void> CmdEndQuery;

    [NativeTypeName("void (*)(CommandBuffer &, const QueryPool &, uint32_t, uint32_t, Buffer &, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, QueryPool*, uint, uint, Buffer*, ulong, void> CmdCopyQueries;

    /// <summary>Annotations for profiling tools: command buffer</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const char *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, sbyte*, uint, void> CmdBeginAnnotation;

    [NativeTypeName("void (*)(CommandBuffer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, void> CmdEndAnnotation;

    [NativeTypeName("void (*)(CommandBuffer &, const char *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, sbyte*, uint, void> CmdAnnotation;

    /// <summary>} } D3D11 performs state tracking and resets it there</summary>
    [NativeTypeName("Result (*)(CommandBuffer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Result> EndCommandBuffer;

    /// <summary>Annotations for profiling tools: command queue - D3D11: NOP</summary>
    [NativeTypeName("void (*)(Queue &, const char *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, sbyte*, uint, void> QueueBeginAnnotation;

    [NativeTypeName("void (*)(Queue &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, void> QueueEndAnnotation;

    [NativeTypeName("void (*)(Queue &, const char *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, sbyte*, uint, void> QueueAnnotation;

    /// <summary>Queries and timestamps on host</summary>
    [NativeTypeName("void (*)(QueryPool &, uint32_t, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<QueryPool*, uint, uint, void> ResetQueries;

    [NativeTypeName("uint32_t (*)(const QueryPool &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<QueryPool*, uint> GetQuerySize;

    [NativeTypeName("void (*)(Queue &, uint64_t &, uint64_t &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, ulong*, ulong*, void> GetCalibratedTimestamps;

    /// <summary>Work submission and synchronization to device</summary>
    [NativeTypeName("Result (*)(Queue &, const QueueSubmitDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, QueueSubmitDesc*, Result> QueueSubmit;

    [NativeTypeName("Result (*)(Queue *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, Result> QueueWaitIdle;

    [NativeTypeName("Result (*)(Device *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, Result> DeviceWaitIdle;

    /// <summary>on host</summary>
    [NativeTypeName("void (*)(Fence &, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Fence*, ulong, void> Wait;

    [NativeTypeName("uint64_t (*)(Fence &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Fence*, ulong> GetFenceValue;

    /// <summary>Command allocator</summary>
    [NativeTypeName("void (*)(CommandAllocator &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandAllocator*, void> ResetCommandAllocator;

    /// <summary>Host address D3D11: no persistent mapping D3D12: persistent mapping, "Map/Unmap" do nothing VK: persistent mapping, but "Unmap" can do a flush if underlying memory is not "HOST_COHERENT" (unlikely)</summary>
    [NativeTypeName("void *(*)(Buffer &, uint64_t, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Buffer*, ulong, ulong, void*> MapBuffer;

    [NativeTypeName("void (*)(Buffer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Buffer*, void> UnmapBuffer;

    /// <summary>Synchronous host copies - all prior GPU access to the copied subresources must be complete before the call - host memory is no longer accessed and readback data is available after the call returns - textures must be created with "TextureUsageBits::HOST_TRANSFER" - destination regions in a single "UploadHostMemoryToTexture" call must not overlap</summary>
    [NativeTypeName("Result (*)(Queue &, const UploadHostMemoryToTextureDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, UploadHostMemoryToTextureDesc*, uint, Result> UploadHostMemoryToTexture;

    [NativeTypeName("Result (*)(Queue &, const ReadbackTextureToHostMemoryDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, ReadbackTextureToHostMemoryDesc*, uint, Result> ReadbackTextureToHostMemory;

    /// <summary>Device address (aka GPU virtual address or "0" if unsupported)</summary>
    [NativeTypeName("uint64_t (*)(const Buffer &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Buffer*, ulong> GetBufferDeviceAddress;

    /// <summary>Pipeline cache (PSO blob storage, persisted across runs) - Threadsafe: no, external synchronization required, call after all pipeline creations using this cache have completed - 2-call pattern: pass "dst = NULL" to query required "size", then call again with allocated "dst"</summary>
    [NativeTypeName("Result (*)(PipelineCache &, void *, uint64_t &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<PipelineCache*, void*, ulong*, Result> GetPipelineCacheData;

    /// <summary>Debug name for any object declared as "NriForwardStruct" (skipped for buffers &amp; textures in D3D if they are not bound to a memory)</summary>
    [NativeTypeName("void (*)(Object *, const char *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<void*, sbyte*, void> SetDebugName;

    /// <summary>Native objects ___D3D11 (latest interface)________|_D3D12 (latest interface)____|_VK_________________________________|_WGPU__________________________________ ID3D11Device* | ID3D12Device* | VkDevice | WGPUDevice</summary>
    [NativeTypeName("void *(*)(const Device *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, void*> GetDeviceNativeObject;

    /// <summary>- | ID3D12CommandQueue* | VkQueue | WGPUQueue</summary>
    [NativeTypeName("void *(*)(const Queue *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Queue*, void*> GetQueueNativeObject;

    /// <summary>ID3D11DeviceContext* | ID3D12CommandList* | VkCommandBuffer | WGPUCommandBuffer</summary>
    [NativeTypeName("void *(*)(const CommandBuffer *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, void*> GetCommandBufferNativeObject;

    /// <summary>ID3D11Buffer* | ID3D12Resource* | VkBuffer | WGPUBuffer</summary>
    [NativeTypeName("uint64_t (*)(const Buffer *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Buffer*, ulong> GetBufferNativeObject;

    /// <summary>ID3D11Resource* | ID3D12Resource* | VkImage | WGPUTexture</summary>
    [NativeTypeName("uint64_t (*)(const Texture *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Texture*, ulong> GetTextureNativeObject;

    /// <summary>ID3D11View/ID3D11SamplerState* | D3D12_CPU_DESCRIPTOR_HANDLE | VkImageView/VkBufferView/VkSampler | WGPUTextureView/WGPUBuffer/WGPUSampler</summary>
    [NativeTypeName("uint64_t (*)(const Descriptor *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Descriptor*, ulong> GetDescriptorNativeObject;

    /// <summary>Shared handles: each successful call returns a new caller-owned handle (see "SharedHandle")</summary>
    [NativeTypeName("Result (*)(const Buffer &, SharedHandle &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Buffer*, SharedHandle*, Result> GetBufferSharedHandle;

    [NativeTypeName("Result (*)(const Texture &, SharedHandle &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Texture*, SharedHandle*, Result> GetTextureSharedHandle;

    [NativeTypeName("Result (*)(const Fence &, SharedHandle &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Fence*, SharedHandle*, Result> GetFenceSharedHandle;

    /// <summary>closes the caller-owned handle and clears the struct</summary>
    [NativeTypeName("void (*)(SharedHandle &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<SharedHandle*, void> DestroySharedHandle;
}
