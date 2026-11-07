namespace SharpNRI.Native;

/// <summary>see NRI_FILL_DRAW_INDEXED_DESC</summary>
public partial struct DrawIndexedBaseDesc
{
    /// <summary>root constant</summary>
    [NativeTypeName("int32_t")]
    public int shaderEmulatedBaseVertex;

    /// <summary>root constant</summary>
    [NativeTypeName("uint32_t")]
    public uint shaderEmulatedBaseInstance;

    [NativeTypeName("uint32_t")]
    public uint indexNum;

    [NativeTypeName("uint32_t")]
    public uint instanceNum;

    /// <summary>index buffer offset = CmdSetIndexBuffer.offset + baseIndex * sizeof(CmdSetIndexBuffer.indexType)</summary>
    [NativeTypeName("uint32_t")]
    public uint baseIndex;

    /// <summary>index += baseVertex</summary>
    [NativeTypeName("int32_t")]
    public int baseVertex;

    [NativeTypeName("uint32_t")]
    public uint baseInstance;
}
