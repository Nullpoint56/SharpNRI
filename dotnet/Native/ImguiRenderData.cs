namespace PrismNRI.Native;

public unsafe partial struct ImguiRenderData
{
    [NativeTypeName("nri::Imgui *")]
    public Imgui* imgui;

    [NativeTypeName("const void *")]
    public void* drawCommands;

    [NativeTypeName("nri::BufferOffset")]
    public BufferOffset vertices;

    [NativeTypeName("uint64_t")]
    public ulong indexBufferOffset;

    [NativeTypeName("uint32_t")]
    public uint drawCmdNum;
}
