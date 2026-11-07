namespace SharpNRI.Native;

public unsafe partial struct CopyImguiDataDesc
{
    /// <summary>ImDrawData::CmdLists.Data</summary>
    [NativeTypeName("const ImDrawList *const *")]
    public ImDrawList** drawLists;

    /// <summary>ImDrawData::CmdLists.Size</summary>
    [NativeTypeName("uint32_t")]
    public uint drawListNum;

    /// <summary>ImDrawData::Textures-&gt;Data (same as "ImGui::GetPlatformIO().Textures.Data")</summary>
    [NativeTypeName("ImTextureData *const *")]
    public ImTextureData** textures;

    /// <summary>ImDrawData::Textures-&gt;Size (same as "ImGui::GetPlatformIO().Textures.Size")</summary>
    [NativeTypeName("uint32_t")]
    public uint textureNum;
}
