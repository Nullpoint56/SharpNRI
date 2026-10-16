// A texture uploaded on the COPY queue and consumed on the GRAPHICS queue, with NRI's copy
// layouts on both queues and the hand-off named in srcQueue / dstQueue (patch 0022). See
// CLAUDE.md: Barriers.

using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;
using V12 = Vortice.Direct3D12;
using V12Debug = Vortice.Direct3D12.Debug;

namespace PrismNRI.Tests;

public sealed class CrossQueueCopyTests
{
    private const int Width = 16, Height = 16, Texel = 4;

    /// <summary>Why the device has no debug layer (no ID3D12InfoQueue), or null when it has one.</summary>
    private static string? NoDebugLayer(NriDevice device)
    {
        System.Runtime.InteropServices.Marshal.AddRef(device.GetNativeObject()); // released by the Vortice wrapper's Dispose
        using var d3d = new V12.ID3D12Device(device.GetNativeObject());
        using var infoQueue = d3d.QueryInterfaceOrNull<V12Debug.ID3D12InfoQueue>();
        return infoQueue is null ? "the D3D12 device has no ID3D12InfoQueue: the debug layer (d3d12SDKLayers.dll) did not load" : null;
    }

    public static TheoryData<Backend, SharingMode> Cases
    {
        get
        {
            var data = new TheoryData<Backend, SharingMode>();
            foreach (var api in new[] { GraphicsAPI.VK, GraphicsAPI.D3D12 })
            foreach (var mode in new[] { SharingMode.CONCURRENT, SharingMode.EXCLUSIVE })
                data.Add(new Backend(api), mode);
            return data;
        }
    }

    /// <summary>
    /// The portable cross-queue upload. COPY queue: {UNDEFINED} -> {COPY_DESTINATION}, the upload,
    /// then the release {COPY_DESTINATION} -> {COPY_DESTINATION} with srcQueue = COPY and dstQueue =
    /// GRAPHICS. GRAPHICS queue, after waiting for the copy's fence: the matching acquire (same
    /// layouts and queues), {COPY_DESTINATION} -> {SHADER_RESOURCE}, {SHADER_RESOURCE} ->
    /// {COPY_SOURCE}, readback. The hand-off keeps the layout because a Vulkan release and acquire
    /// of an EXCLUSIVE image must name the same transition, and no transition but to and from the
    /// copy layouts is expressible on a D3D12 copy list.
    /// Vulkan copies hard-code TRANSFER_SRC/DST_OPTIMAL, so COPY_DESTINATION is the layout the
    /// upload needs there. D3D12 copy lists allow only the COMMON layout: patch 0022 records
    /// COPY_SOURCE / COPY_DESTINATION as COMMON in a copy list and on the copy-queue side of a
    /// barrier whose srcQueue or dstQueue is a copy queue; without it the copy-list barrier is 1334
    /// and Close fails, and without the queues on the GRAPHICS barrier its COPY_DEST LayoutBefore
    /// mismatches the COMMON the debug layer tracks (1334 at ExecuteCommandLists). D3D12 skips
    /// without the debug layer: NRI validation checks neither.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void A_texture_uploaded_on_the_COPY_queue_is_read_on_GRAPHICS_with_the_copy_layouts(Backend backend, SharingMode sharingMode)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages, b => b.QueueFamilies =
            [new NriQueueFamily(QueueType.GRAPHICS), new NriQueueFamily(QueueType.COMPUTE), new NriQueueFamily(QueueType.COPY)]);
        if (backend.Api == GraphicsAPI.D3D12)
        {
            var noDebugLayer = NoDebugLayer(device);
            Assert.SkipWhen(noDebugLayer is not null, noDebugLayer ?? "");
        }
        Assert.SkipWhen(device.Desc.adapterDesc.queueNum[(int)QueueType.COPY] == 0, $"{backend}: adapterDesc.queueNum[COPY] is 0, no COPY queue");

        var graphics = device.GetQueue(QueueType.GRAPHICS);
        var copy = device.GetQueue(QueueType.COPY);
        device.GetQueue(QueueType.COMPUTE); // three active families on Vulkan: a CONCURRENT image is really concurrent
        if (backend.Api == GraphicsAPI.VK && device.TryWrapperVK is { } vk)
            Record($"{backend}: GRAPHICS family {vk.GetQueueFamilyIndexVK(graphics)}, COPY family {vk.GetQueueFamilyIndexVK(copy)}");

        var rowPitch = (uint)Align(Width * Texel, device.Desc.memoryAlignment.uploadBufferTextureRow);
        var slicePitch = (uint)Align((ulong)rowPitch * Height, device.Desc.memoryAlignment.uploadBufferTextureSlice);
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D,
            usage = TextureUsageBits.SHADER_RESOURCE | TextureUsageBits.COLOR_ATTACHMENT, // NRI validation allows EXCLUSIVE for attachments only
            format = Format.RGBA8_UNORM,
            width = Width,
            height = Height,
            depth = 1,
            mipNum = 1,
            layerNum = 1,
            sampleNum = 1,
            sharingMode = sharingMode,
        });
        using var upload = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = slicePitch });
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = slicePitch });

        var pattern = new byte[Width * Height * Texel];
        for (var i = 0; i < pattern.Length; i++) pattern[i] = (byte)(i * 7 + 3);
        var mapped = upload.Map();
        mapped.Clear();
        for (var y = 0; y < Height; y++) pattern.AsSpan(y * Width * Texel, Width * Texel).CopyTo(mapped[(int)(y * rowPitch)..]);
        upload.Unmap();

        var region = new TextureRegionDesc { width = Width, height = Height, depth = 1, planes = PlaneBits.ALL };
        var layout = new TextureDataLayoutDesc { offset = 0, rowPitch = rowPitch, slicePitch = slicePitch };
        var shaderRead = new AccessLayoutStage { access = AccessBits.SHADER_RESOURCE, layout = Layout.SHADER_RESOURCE, stages = StageBits.ALL_SHADERS };
        var handOff = new NriTextureBarrier(texture, TextureCopyDestination, TextureCopyDestination, SrcQueue: copy, DstQueue: graphics);

        using var fence = device.CreateFence();
        using var copyAllocator = copy.CreateCommandAllocator();
        using var copyCommands = copyAllocator.CreateCommandBuffer();
        using var graphicsAllocator = graphics.CreateCommandAllocator();
        using var graphicsCommands = graphicsAllocator.CreateCommandBuffer();
        var start = messages.Count;

        copyCommands.Begin();
        copyCommands.Barrier(new NriTextureBarrier(texture, Undefined, TextureCopyDestination));
        copyCommands.UploadBufferToTexture(texture, region, upload, layout);
        copyCommands.Barrier(handOff); // release
        copyCommands.End();
        copy.Submit([copyCommands], default, [new NriFenceSubmit(fence, 1, StageBits.COPY)]);

        graphicsCommands.Begin();
        graphicsCommands.Barrier(handOff); // acquire
        graphicsCommands.Barrier(new NriTextureBarrier(texture, TextureCopyDestination, shaderRead));
        graphicsCommands.Barrier(new NriTextureBarrier(texture, shaderRead, TextureCopySource));
        graphicsCommands.ReadbackTextureToBuffer(readback, layout, texture, region);
        graphicsCommands.End();
        graphics.Submit([graphicsCommands], [new NriFenceSubmit(fence, 1, StageBits.COPY)], [new NriFenceSubmit(fence, 2)]);
        Wait(fence, 2);
        device.WaitIdle();

        var bytes = readback.Map();
        var actual = new byte[pattern.Length];
        for (var y = 0; y < Height; y++) bytes.Slice((int)(y * rowPitch), Width * Texel).CopyTo(actual.AsSpan(y * Width * Texel));
        readback.Unmap();

        List<NriMessage> provoked;
        lock (messages) provoked = messages.Skip(start).ToList();
        foreach (var m in provoked) Record("message: " + m);
        Record($"{backend}, {sharingMode}: {provoked.Count} message(s), {provoked.Count(m => m.IsError)} error(s) from recording to readback; " +
            $"{messages.Count(m => m.IsError)} error(s) in all; readback {(actual.AsSpan().SequenceEqual(pattern) ? "equals" : "differs from")} the upload");

        Assert.DoesNotContain(messages, m => m.IsError);
        Assert.Equal(pattern, actual);
    }
}
