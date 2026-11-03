namespace SharpNRI.Native;

public unsafe partial struct RayTracingInterface
{
    [NativeTypeName("Result (*)(Device &, const RayTracingPipelineDesc &, Pipeline *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, RayTracingPipelineDesc*, Pipeline**, Result> CreateRayTracingPipeline;

    [NativeTypeName("Result (*)(const AccelerationStructure &, Descriptor *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<AccelerationStructure*, Descriptor**, Result> CreateAccelerationStructureDescriptor;

    [NativeTypeName("uint64_t (*)(const AccelerationStructure &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<AccelerationStructure*, ulong> GetAccelerationStructureHandle;

    [NativeTypeName("uint64_t (*)(const AccelerationStructure &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<AccelerationStructure*, ulong> GetAccelerationStructureUpdateScratchBufferSize;

    [NativeTypeName("uint64_t (*)(const AccelerationStructure &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<AccelerationStructure*, ulong> GetAccelerationStructureBuildScratchBufferSize;

    [NativeTypeName("uint64_t (*)(const Micromap &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Micromap*, ulong> GetMicromapBuildScratchBufferSize;

    [NativeTypeName("Buffer *(*)(const AccelerationStructure &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<AccelerationStructure*, Buffer*> GetAccelerationStructureBuffer;

    [NativeTypeName("Buffer *(*)(const Micromap &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Micromap*, Buffer*> GetMicromapBuffer;

    [NativeTypeName("void (*)(AccelerationStructure *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<AccelerationStructure*, void> DestroyAccelerationStructure;

    [NativeTypeName("void (*)(Micromap *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Micromap*, void> DestroyMicromap;

    [NativeTypeName("Result (*)(Device &, const AccelerationStructureDesc &, AccelerationStructure *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, AccelerationStructureDesc*, AccelerationStructure**, Result> CreateAccelerationStructure;

    [NativeTypeName("Result (*)(Device &, const MicromapDesc &, Micromap *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, MicromapDesc*, Micromap**, Result> CreateMicromap;

    [NativeTypeName("void (*)(const AccelerationStructure &, MemoryLocation, MemoryDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<AccelerationStructure*, MemoryLocation, MemoryDesc*, void> GetAccelerationStructureMemoryDesc;

    [NativeTypeName("void (*)(const Micromap &, MemoryLocation, MemoryDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Micromap*, MemoryLocation, MemoryDesc*, void> GetMicromapMemoryDesc;

    [NativeTypeName("Result (*)(const BindAccelerationStructureMemoryDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<BindAccelerationStructureMemoryDesc*, uint, Result> BindAccelerationStructureMemory;

    [NativeTypeName("Result (*)(const BindMicromapMemoryDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<BindMicromapMemoryDesc*, uint, Result> BindMicromapMemory;

    [NativeTypeName("void (*)(const Device &, const AccelerationStructureDesc &, MemoryLocation, MemoryDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, AccelerationStructureDesc*, MemoryLocation, MemoryDesc*, void> GetAccelerationStructureMemoryDesc2;

    [NativeTypeName("void (*)(const Device &, const MicromapDesc &, MemoryLocation, MemoryDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, MicromapDesc*, MemoryLocation, MemoryDesc*, void> GetMicromapMemoryDesc2;

    [NativeTypeName("Result (*)(Device &, MemoryLocation, float, const AccelerationStructureDesc &, AccelerationStructure *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, MemoryLocation, float, AccelerationStructureDesc*, AccelerationStructure**, Result> CreateCommittedAccelerationStructure;

    [NativeTypeName("Result (*)(Device &, MemoryLocation, float, const MicromapDesc &, Micromap *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, MemoryLocation, float, MicromapDesc*, Micromap**, Result> CreateCommittedMicromap;

    [NativeTypeName("Result (*)(Device &, Memory *, uint64_t, const AccelerationStructureDesc &, AccelerationStructure *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, Memory*, ulong, AccelerationStructureDesc*, AccelerationStructure**, Result> CreatePlacedAccelerationStructure;

    [NativeTypeName("Result (*)(Device &, Memory *, uint64_t, const MicromapDesc &, Micromap *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, Memory*, ulong, MicromapDesc*, Micromap**, Result> CreatePlacedMicromap;

    [NativeTypeName("Result (*)(const Pipeline &, uint32_t, uint32_t, uint32_t, void *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Pipeline*, uint, uint, uint, void*, Result> WriteShaderGroupIdentifiers;

    [NativeTypeName("void (*)(CommandBuffer &, const BuildMicromapDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, BuildMicromapDesc*, uint, void> CmdBuildMicromaps;

    [NativeTypeName("void (*)(CommandBuffer &, const Micromap *const *, uint32_t, QueryPool &, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Micromap**, uint, QueryPool*, uint, void> CmdWriteMicromapSizes;

    [NativeTypeName("void (*)(CommandBuffer &, Micromap &, const Micromap &, CopyMode) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Micromap*, Micromap*, CopyMode, void> CmdCopyMicromap;

    [NativeTypeName("void (*)(CommandBuffer &, const BuildTopLevelAccelerationStructureDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, BuildTopLevelAccelerationStructureDesc*, uint, void> CmdBuildTopLevelAccelerationStructures;

    [NativeTypeName("void (*)(CommandBuffer &, const BuildBottomLevelAccelerationStructureDesc *, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, BuildBottomLevelAccelerationStructureDesc*, uint, void> CmdBuildBottomLevelAccelerationStructures;

    [NativeTypeName("void (*)(CommandBuffer &, const AccelerationStructure *const *, uint32_t, QueryPool &, uint32_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, AccelerationStructure**, uint, QueryPool*, uint, void> CmdWriteAccelerationStructureSizes;

    [NativeTypeName("void (*)(CommandBuffer &, AccelerationStructure &, const AccelerationStructure &, CopyMode) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, AccelerationStructure*, AccelerationStructure*, CopyMode, void> CmdCopyAccelerationStructure;

    [NativeTypeName("void (*)(CommandBuffer &, const DispatchRaysDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DispatchRaysDesc*, void> CmdDispatchRays;

    [NativeTypeName("void (*)(CommandBuffer &, const Buffer &, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, ulong, void> CmdDispatchRaysIndirect;

    [NativeTypeName("uint64_t (*)(const AccelerationStructure *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<AccelerationStructure*, ulong> GetAccelerationStructureNativeObject;

    [NativeTypeName("uint64_t (*)(const Micromap *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Micromap*, ulong> GetMicromapNativeObject;
}
