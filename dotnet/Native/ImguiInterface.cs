namespace SharpNRI.Native;

/// <summary>Threadsafe: yes, if command buffers are externally synchronized and Streamer thread safety is enabled (or provided externally). Access to shared "ImTextureData" is internally synchronized.</summary>
public unsafe partial struct ImguiInterface
{
    [NativeTypeName("Result (*)(Device &, const ImguiDesc &, Imgui *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, ImguiDesc*, Imgui**, Result> CreateImgui;

    [NativeTypeName("void (*)(Imgui *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Imgui*, void> DestroyImgui;

    /// <summary>Command buffer { Copy</summary>
    [NativeTypeName("void (*)(CommandBuffer &, Streamer &, Imgui &, const CopyImguiDataDesc &, ImguiRenderData &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Streamer*, Imgui*, CopyImguiDataDesc*, ImguiRenderData*, void> CmdCopyImguiData;

    /// <summary>Draw (changes descriptor pool, pipeline layout and pipeline, barriers are externally controlled)</summary>
    [NativeTypeName("void (*)(CommandBuffer &, const ImguiRenderData &, const DrawImguiDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, ImguiRenderData*, DrawImguiDesc*, void> CmdDrawImgui;
}
