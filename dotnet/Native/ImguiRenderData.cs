namespace SharpNRI.Native;

/// <summary>Read-only input for "CmdDrawImgui"</summary>
public unsafe partial struct ImguiRenderData
{
    /// <summary>producing instance</summary>
    [NativeTypeName("nri::Imgui *")]
    public Imgui* imgui;

    /// <summary>in transient Streamer host data</summary>
    [NativeTypeName("const void *")]
    public void* drawCommands;

    /// <summary>in transient Streamer buffer</summary>
    [NativeTypeName("nri::BufferOffset")]
    public BufferOffset vertices;

    /// <summary>in "vertices.buffer"</summary>
    [NativeTypeName("uint64_t")]
    public ulong indexBufferOffset;

    [NativeTypeName("uint32_t")]
    public uint drawCmdNum;
}
