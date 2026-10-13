namespace PrismNRI.Native;

public unsafe partial struct CopyImguiDataDesc
{
    [NativeTypeName("const ImDrawList *const *")]
    public ImDrawList** drawLists;

    [NativeTypeName("uint32_t")]
    public uint drawListNum;

    [NativeTypeName("ImTextureData *const *")]
    public ImTextureData** textures;

    [NativeTypeName("uint32_t")]
    public uint textureNum;
}
