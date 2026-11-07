namespace SharpNRI.Native;

public partial struct StreamerDesc
{
    /// <summary>Statically allocated ring-buffer for dynamic constants UPLOAD or DEVICE_UPLOAD</summary>
    [NativeTypeName("nri::MemoryLocation")]
    public MemoryLocation constantBufferMemoryLocation;

    /// <summary>should be large enough to avoid overwriting data for enqueued frames</summary>
    [NativeTypeName("uint64_t")]
    public ulong constantBufferSize;

    /// <summary>Dynamically (re)allocated ring-buffer for copying and rendering UPLOAD or DEVICE_UPLOAD</summary>
    [NativeTypeName("nri::MemoryLocation")]
    public MemoryLocation dynamicBufferMemoryLocation;

    /// <summary>"size" is the initial per-frame size (0 - allocate on demand), the buffer still grows if a frame streams more</summary>
    [NativeTypeName("nri::BufferDesc")]
    public BufferDesc dynamicBufferDesc;

    /// <summary>number of frames "in-flight" (usually 1-3), adds 1 under the hood for the current "not-yet-committed" frame</summary>
    [NativeTypeName("uint32_t")]
    public uint queuedFrameNum;

    /// <summary>Statically allocated read/write ring-buffer for generic CPU data capacity for all simultaneously live data and alignment padding</summary>
    [NativeTypeName("uint64_t")]
    public ulong hostDataCapacity;
}
