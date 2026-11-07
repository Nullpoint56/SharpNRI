namespace SharpNRI.Native;

public unsafe partial struct DeviceLostDump
{
    /// <summary>opaque backend-specific binary data, NRI-owned and valid until device destruction</summary>
    [NativeTypeName("const void *")]
    public void* data;

    /// <summary>bytes, 0 if "data" is NULL</summary>
    [NativeTypeName("uint64_t")]
    public ulong size;
}
