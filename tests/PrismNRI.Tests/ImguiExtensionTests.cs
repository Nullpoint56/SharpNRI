// The Imgui extension. NRI reads ImGui's own structs through pointers and needs no ImGui library at
// run time, so the test builds the draw data by hand in native memory, laid out exactly as NRI's
// copy of the ImGui 1.92 definitions (Source/Shared/ImguiInterface.hpp): a 4x4 white RGBA32 "font
// atlas" in the WantCreate state, and one draw list with one draw command of one triangle whose
// vertices carry the release-gate colours. On the GPU backends that is copied through a streamer,
// drawn into a 64x64 target, read back and checked; NRI also creates and uploads the atlas texture
// and flips its ImTextureData to OK with a texture ID, which the test checks.

using System.Runtime.InteropServices;
using PrismNRI.Native;
using Xunit;

namespace PrismNRI.Tests;

public sealed unsafe class ImguiExtensionTests
{
    private const ushort Size = 64;

    // ---------------------------------------------------------------- ImGui 1.92 layouts, as NRI copies them

    [StructLayout(LayoutKind.Sequential)]
    private struct ImVector { public int Size, Capacity; public void* Data; }

    [StructLayout(LayoutKind.Sequential)]
    private struct ImTextureRect { public ushort X, Y, W, H; }

    [StructLayout(LayoutKind.Sequential)]
    private struct ImTextureData
    {
        public int UniqueID;
        public int Status;              // ImTextureStatus: OK, Destroyed, WantCreate, WantUpdates, WantDestroy
        public void* BackendUserData;
        public void* QueueUserData;
        public ulong TexID;
        public int Format;              // ImTextureFormat: RGBA32, Alpha8
        public int Width, Height, BytesPerPixel;
        public byte* Pixels;
        public ImTextureRect UsedRect, UpdateRect;
        public ImVector Updates;
        public int UnusedFrames;
        public ushort RefCount;
        public byte UseColors, WantDestroyNextFrame;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ImDrawCmd
    {
        public float ClipX, ClipY, ClipZ, ClipW;
        public ImTextureData* TexData;  // ImTextureRef
        public ulong TexID;
        public uint VtxOffset, IdxOffset, ElemCount;
        public void* UserCallback;
        public void* UserCallbackData;
        public int UserCallbackDataSize, UserCallbackDataOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ImDrawVert { public float X, Y, U, V; public uint Col; }

    [StructLayout(LayoutKind.Sequential)]
    private struct ImDrawList { public ImVector CmdBuffer, IdxBuffer, VtxBuffer; }

    /// <summary>The draw data, in one native block that outlives the copy.</summary>
    private sealed class DrawData : IDisposable
    {
        private readonly byte* _block;
        public readonly ImTextureData* Texture;
        public readonly ImDrawList* List;

        public DrawData()
        {
            var size = sizeof(ImTextureData) + 64 + sizeof(ImDrawCmd) + 3 * sizeof(ImDrawVert) + 8 + sizeof(ImDrawList);
            _block = (byte*)NativeMemory.AllocZeroed((nuint)size);
            var p = _block;
            Texture = (ImTextureData*)p; p += sizeof(ImTextureData);
            var pixels = p; p += 64;
            var cmd = (ImDrawCmd*)p; p += sizeof(ImDrawCmd);
            var verts = (ImDrawVert*)p; p += 3 * sizeof(ImDrawVert);
            var indices = (ushort*)p; p += 8;
            List = (ImDrawList*)p;

            new Span<byte>(pixels, 64).Fill(0xFF);
            *Texture = new ImTextureData
            {
                UniqueID = 1, Status = 2 /* WantCreate */, Format = 0 /* RGBA32 */, Width = 4, Height = 4, BytesPerPixel = 4,
                Pixels = pixels, UsedRect = new ImTextureRect { W = 4, H = 4 }, UpdateRect = new ImTextureRect { W = 4, H = 4 }, RefCount = 1,
            };

            // ImGui colours are ABGR in a uint: red 0xFF0000FF, green 0xFF00FF00, blue 0xFFFF0000.
            verts[0] = new ImDrawVert { X = 32, Y = 16, U = 0.5f, V = 0.5f, Col = 0xFF0000FF };
            verts[1] = new ImDrawVert { X = 48, Y = 48, U = 0.5f, V = 0.5f, Col = 0xFF00FF00 };
            verts[2] = new ImDrawVert { X = 16, Y = 48, U = 0.5f, V = 0.5f, Col = 0xFFFF0000 };
            indices[0] = 0; indices[1] = 1; indices[2] = 2;
            *cmd = new ImDrawCmd { ClipZ = Size, ClipW = Size, TexData = Texture, ElemCount = 3 };

            *List = new ImDrawList
            {
                CmdBuffer = new ImVector { Size = 1, Capacity = 1, Data = cmd },
                IdxBuffer = new ImVector { Size = 3, Capacity = 3, Data = indices },
                VtxBuffer = new ImVector { Size = 3, Capacity = 3, Data = verts },
            };
        }

        public void Dispose() => NativeMemory.Free(_block);
    }

    // ---------------------------------------------------------------- tests

    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void The_extension_is_available_and_an_imgui_lives_and_dies(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        Assert.NotNull(device.TryImgui);
        using (device.Imgui.CreateImgui(descriptorPoolSize: 8))
        {
            Assert.Equal(1, device.LiveObjects);
        }
        Assert.Equal(0, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void No_draw_lists_copy_to_empty_render_data(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        using var streamer = device.Streamer.CreateStreamer(StreamerDesc());
        using var imgui = device.Imgui.CreateImgui();

        GpuWork.Run(device, cb =>
        {
            var data = device.Imgui.CopyImguiData(cb, streamer, imgui, new NriCopyImguiDataDesc());
            Assert.Equal(0u, data.drawCmdNum);
            device.Imgui.DrawImgui(cb, data, new DrawImguiDesc { displaySize = new Dim2_t { w = Size, h = Size }, attachmentFormat = Format.RGBA8_UNORM });
        });
        device.Streamer.EndStreamerFrame(streamer);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Hand_built_draw_data_renders_and_reads_back(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        using var streamer = device.Streamer.CreateStreamer(StreamerDesc());
        using var imgui = device.Imgui.CreateImgui(descriptorPoolSize: 8);
        using var drawData = new DrawData();

        using var target = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.COLOR_ATTACHMENT, format = Format.RGBA8_UNORM,
            width = Size, height = Size, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
        });
        using var view = target.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
        var rowPitch = (uint)GpuWork.Align(Size * 4u, device.Desc.memoryAlignment.uploadBufferTextureRow);
        var slicePitch = (uint)GpuWork.Align(rowPitch * Size, device.Desc.memoryAlignment.uploadBufferTextureSlice);
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = slicePitch });

        nint list = (nint)drawData.List;
        nint texture = (nint)drawData.Texture;

        GpuWork.Run(device, cb =>
        {
            var renderData = device.Imgui.CopyImguiData(cb, streamer, imgui, new NriCopyImguiDataDesc
            {
                DrawLists = new ReadOnlySpan<nint>(in list),
                Textures = new ReadOnlySpan<nint>(in texture),
            });
            Assert.Equal(1u, renderData.drawCmdNum);

            cb.Barrier(new NriTextureBarrier(target, default, GpuWork.ColorAttachment));
            var clear = new ClearValue();
            clear.color.f = new Color32f { w = 1 };
            cb.BeginRendering([new NriAttachment(view, clear, LoadOp.CLEAR, StoreOp.STORE)]);
            cb.SetScissor(new Rect { width = Size, height = Size });
            device.Imgui.DrawImgui(cb, renderData, new DrawImguiDesc { displaySize = new Dim2_t { w = Size, h = Size }, hdrScale = 1, attachmentFormat = Format.RGBA8_UNORM });
            cb.EndRendering();
            cb.Barrier(new NriTextureBarrier(target, GpuWork.ColorAttachment, GpuWork.TextureCopySource));
            cb.ReadbackTextureToBuffer(readback, new TextureDataLayoutDesc { rowPitch = rowPitch, slicePitch = slicePitch }, target,
                new TextureRegionDesc { width = Size, height = Size, depth = 1, planes = PlaneBits.COLOR });
        });
        device.Streamer.EndStreamerFrame(streamer);

        // NRI took the atlas: created a texture for it and marked it up to date.
        Assert.Equal(0 /* OK */, drawData.Texture->Status);
        Assert.NotEqual(0UL, drawData.Texture->TexID);

        var pixels = readback.Map().ToArray();
        readback.Unmap();
        (byte R, byte G, byte B) Pixel(int x, int y) { var i = (int)(y * rowPitch) + x * 4; return (pixels[i], pixels[i + 1], pixels[i + 2]); }

        Assert.Equal((0, 0, 0), ((int, int, int))Pixel(0, 0));
        var top = Pixel(32, 20);
        Assert.True(top.R > top.G && top.R > top.B, $"top vertex region is {top}, expected red-dominant");
        var right = Pixel(42, 44);
        Assert.True(right.G > right.R && right.G > right.B, $"bottom-right region is {right}, expected green-dominant");
        var left = Pixel(22, 44);
        Assert.True(left.B > left.R && left.B > left.G, $"bottom-left region is {left}, expected blue-dominant");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    private static StreamerDesc StreamerDesc() => new()
    {
        // "NriOptional" in the header, but validation rejects the default (DEVICE) even with no constant ring.
        constantBufferMemoryLocation = MemoryLocation.HOST_UPLOAD,
        dynamicBufferMemoryLocation = MemoryLocation.HOST_UPLOAD,
        dynamicBufferDesc = new BufferDesc { usage = BufferUsageBits.VERTEX | BufferUsageBits.INDEX },
        queuedFrameNum = 1,
        hostDataCapacity = 64 * 1024,
    };
}
