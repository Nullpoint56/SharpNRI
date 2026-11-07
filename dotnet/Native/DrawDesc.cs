namespace SharpNRI.Native;

/// <summary>see NRI_FILL_DRAW_DESC</summary>
public partial struct DrawDesc
{
    [NativeTypeName("uint32_t")]
    public uint vertexNum;

    [NativeTypeName("uint32_t")]
    public uint instanceNum;

    /// <summary>vertex buffer offset = CmdSetVertexBuffers.offset + baseVertex * VertexStreamDesc::stride</summary>
    [NativeTypeName("uint32_t")]
    public uint baseVertex;

    [NativeTypeName("uint32_t")]
    public uint baseInstance;
}
