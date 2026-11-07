namespace SharpNRI.Native;

public partial struct ImguiDesc
{
    /// <summary>upper bound of textures used by Imgui for drawing across all threads: {number of queued frames} * {number of "CmdDrawImgui" calls} * (1 + {"drawList-&gt;AddImage*" calls})</summary>
    [NativeTypeName("uint32_t")]
    public uint descriptorPoolSize;
}
