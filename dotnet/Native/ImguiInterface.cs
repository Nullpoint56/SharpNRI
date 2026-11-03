namespace SharpNRI.Native;

public unsafe partial struct ImguiInterface
{
    [NativeTypeName("Result (*)(Device &, const ImguiDesc &, Imgui *&) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Device*, ImguiDesc*, Imgui**, Result> CreateImgui;

    [NativeTypeName("void (*)(Imgui *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Imgui*, void> DestroyImgui;

    [NativeTypeName("void (*)(CommandBuffer &, Streamer &, Imgui &, const CopyImguiDataDesc &, ImguiRenderData &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, Streamer*, Imgui*, CopyImguiDataDesc*, ImguiRenderData*, void> CmdCopyImguiData;

    [NativeTypeName("void (*)(CommandBuffer &, const ImguiRenderData &, const DrawImguiDesc &) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<CommandBuffer*, ImguiRenderData*, DrawImguiDesc*, void> CmdDrawImgui;
}
