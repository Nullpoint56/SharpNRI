// L3 — the Imgui extension (NRIImgui.h).
//
// Renders Dear ImGui draw data. NRI reads ImGui's own structs (ImDrawList, ImTextureData) through
// pointers, so this binding takes them as nint: whatever C# ImGui binding the caller uses hands
// out those pointers (ImDrawData::CmdLists.Data[i], ImDrawData::Textures->Data[i]). NRI's
// requirements, from the header:
//  - ImGui 1.92.9+ with ImGuiBackendFlags_RendererHasTextures; unmodified ImDrawVert (20 bytes)
//    and ImDrawIdx (2 bytes); ImTextureData of 96 bytes; ImTextureID_Invalid = 0;
//  - drawList->AddCallback is not supported, except NRI_IMGUI_OVERRIDE_HDR_SCALE;
//  - ImGui::Image* takes a SHADER_RESOURCE NRI descriptor as its ImTextureID;
//  - the ImguiRenderData from CmdCopyImguiData must be drawn before EndStreamerFrame and before
//    the streamer's host-data ring wraps, so StreamerDesc::hostDataCapacity must be non-zero and
//    large enough;
//  - everything referenced by CopyImguiDataDesc must stay valid and immutable until
//    CmdCopyImguiData returns.
// The Imgui object is created and destroyed by the extension. Threadsafe per NRI if command buffers
// are externally synchronized and the streamer is thread safe.

using PrismNRI.Interop;
using PrismNRI.Native;

namespace PrismNRI;

/// <summary><c>CopyImguiDataDesc</c> with its pointer arrays as spans of native ImGui pointers.</summary>
public ref struct NriCopyImguiDataDesc
{
    /// <summary><c>ImDrawList*</c> each: <c>ImDrawData::CmdLists.Data</c>, <c>CmdLists.Size</c> of them.</summary>
    public ReadOnlySpan<nint> DrawLists { get; init; }

    /// <summary>
    /// <c>ImTextureData*</c> each: <c>ImDrawData::Textures-&gt;Data</c> (same as
    /// <c>ImGui::GetPlatformIO().Textures.Data</c>). NRI updates their status and IDs.
    /// </summary>
    public ReadOnlySpan<nint> Textures { get; init; }
}

/// <summary>An ImGui renderer. Created by <see cref="NriImguiExtension.CreateImgui(uint)"/>.</summary>
public sealed unsafe class NriImgui : NriObject
{
    private readonly NriImguiExtension _extension;

    internal NriImgui(NriImguiExtension extension, Imgui* handle) : base(handle, "NriImgui")
    {
        _extension = extension;
        extension.Device.ChildCreated();
    }

    public override NriDevice Device => _extension.Device;

    internal Imgui* Raw => (Imgui*)Handle;

    private protected override void Destroy(void* handle) => _extension.DestroyImgui((Imgui*)handle);
}

/// <summary>NRI's <c>ImguiInterface</c>. Acquire through <see cref="NriDevice.Imgui"/>.</summary>
public sealed unsafe class NriImguiExtension
{
    private readonly ImguiInterface _table;

    internal NriImguiExtension(NriDevice device, in ImguiInterface table)
    {
        Device = device;
        _table = table;
    }

    public NriDevice Device { get; }

    internal static bool TryAcquire(NriDevice device, out NriImguiExtension? extension)
    {
        if (InterfaceTable.TryAcquire<ImguiInterface>(device.Raw, "ImguiInterface", out var table))
        {
            extension = new NriImguiExtension(device, in table);
            return true;
        }
        extension = null;
        return false;
    }

    /// <summary>
    /// An ImGui renderer. <c>descriptorPoolSize</c> (optional) is the upper bound of textures used
    /// for drawing across all threads: queued frames x <c>CmdDrawImgui</c> calls x (1 + image draws).
    /// </summary>
    public NriImgui CreateImgui(in ImguiDesc imguiDesc)
    {
        Imgui* handle = null;
        fixed (ImguiDesc* p = &imguiDesc) Device.Check(_table.CreateImgui(Device.Raw, p, &handle), "CreateImgui");
        return new NriImgui(this, handle);
    }

    /// <summary><see cref="CreateImgui(in ImguiDesc)"/> with the pool size inline.</summary>
    public NriImgui CreateImgui(uint descriptorPoolSize = 0) => CreateImgui(new ImguiDesc { descriptorPoolSize = descriptorPoolSize });

    internal void DestroyImgui(Imgui* imgui) => _table.DestroyImgui(imgui);

    /// <summary>
    /// <c>CmdCopyImguiData</c>: creates and updates textures, streams vertices, indices and draw
    /// commands through <paramref name="streamer"/>, and records the texture copies. The returned
    /// data is read-only input for <see cref="DrawImgui"/>; it lives in the streamer's transient
    /// memory. Records nothing and returns empty data when there are no draw lists. No heap allocation.
    /// </summary>
    public ImguiRenderData CopyImguiData(NriCommandBuffer commandBuffer, NriStreamer streamer, NriImgui imgui, in NriCopyImguiDataDesc copyImguiDataDesc)
    {
        ImguiRenderData renderData;
        fixed (nint* drawLists = copyImguiDataDesc.DrawLists)
        fixed (nint* textures = copyImguiDataDesc.Textures)
        {
            var desc = new CopyImguiDataDesc
            {
                drawLists = (ImDrawList**)drawLists,
                drawListNum = (uint)copyImguiDataDesc.DrawLists.Length,
                textures = (ImTextureData**)textures,
                textureNum = (uint)copyImguiDataDesc.Textures.Length,
            };
            _table.CmdCopyImguiData(commandBuffer.Raw, streamer.Raw, imgui.Raw, &desc, &renderData);
        }
        return renderData;
    }

    /// <summary>
    /// <c>CmdDrawImgui</c>, inside a render pass. Changes the descriptor pool, pipeline layout,
    /// pipeline, vertex and index buffers and viewport bound on <paramref name="commandBuffer"/>;
    /// barriers are the caller's.
    /// </summary>
    public void DrawImgui(NriCommandBuffer commandBuffer, in ImguiRenderData imguiRenderData, in DrawImguiDesc drawImguiDesc)
    {
        fixed (ImguiRenderData* r = &imguiRenderData)
        fixed (DrawImguiDesc* d = &drawImguiDesc)
        {
            _table.CmdDrawImgui(commandBuffer.Raw, r, d);
        }
    }
}
