namespace PrismNRI.Native;

public unsafe partial struct MeshShaderInterface
{
    [NativeTypeName("void (*)(CommandBuffer &, const DrawMeshTasksDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, DrawMeshTasksDesc*, void> CmdDrawMeshTasks;

    [NativeTypeName("void (*)(CommandBuffer &, const Buffer &, uint64_t, uint32_t, uint32_t, const Buffer *, uint64_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Buffer*, ulong, uint, uint, Buffer*, ulong, void> CmdDrawMeshTasksIndirect;
}
