namespace SharpNRI.Native;

public unsafe partial struct SharedHandle
{
    /// <summary>caller-owned NT handle or FD cast to a pointer (FD 0 is valid); see "DestroySharedHandle"</summary>
    public void* handle;

    [NativeTypeName("nri::SharedHandleType")]
    public SharedHandleType type;

    /// <summary>allocation size in bytes for import (0 for fences)</summary>
    [NativeTypeName("uint64_t")]
    public ulong size;
}
