// A texture uploaded on the COPY queue and consumed on the GRAPHICS queue, with NRI's copy
// layouts on both queues and the hand-off named in srcQueue / dstQueue (NRI 732f737), and what the
// Vulkan layer makes of the hand-off on CONCURRENT and EXCLUSIVE images (NRI 2603edc). See
// AGENTS.md: Barriers.

using SharpNRI.Native;
using Xunit;
using static SharpNRI.Tests.GpuWork;
using V12 = Vortice.Direct3D12;
using V12Debug = Vortice.Direct3D12.Debug;

namespace SharpNRI.Tests;

public sealed class CrossQueueCopyTests
{
    private const int Width = 16, Height = 16, Texel = 4;
    private const string BestPracticesSetting = "VK_KHRONOS_VALIDATION_VALIDATE_BEST_PRACTICES";

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

    public static TheoryData<SharingMode> SharingModes => new(SharingMode.CONCURRENT, SharingMode.EXCLUSIVE);

    private static readonly AccessLayoutStage ShaderRead = new() { access = AccessBits.SHADER_RESOURCE, layout = Layout.SHADER_RESOURCE, stages = StageBits.ALL_SHADERS };

    /// <summary>Skips unless the GRAPHICS and COPY queues are in different Vulkan queue families (lavapipe has one family).</summary>
    private static void RequireTwoFamilies(NriDevice device)
    {
        var vk = device.WrapperVK;
        var graphics = vk.GetQueueFamilyIndexVK(device.GetQueue(QueueType.GRAPHICS));
        var copy = vk.GetQueueFamilyIndexVK(device.GetQueue(QueueType.COPY));
        Assert.SkipWhen(graphics == copy, $"VK: the GRAPHICS and COPY queues are both in queue family {graphics}, so no barrier between them is a queue family ownership transfer");
    }

    private static NriDevice CreateThreeQueueDevice(GraphicsAPI api, List<NriMessage> messages) =>
        TestDevice.Create(api, messages, b => b.QueueFamilies =
            [new NriQueueFamily(QueueType.GRAPHICS), new NriQueueFamily(QueueType.COMPUTE), new NriQueueFamily(QueueType.COPY)]);

    /// <summary>
    /// COPY queue: {UNDEFINED} -> {COPY_DESTINATION}, the upload, then <paramref name="release"/>.
    /// GRAPHICS queue, after waiting for the copy's fence: <paramref name="acquire"/>, then from
    /// <paramref name="afterAcquire"/> to {COPY_SOURCE} (through {SHADER_RESOURCE} when the acquire
    /// left COPY_DESTINATION) and the readback. Returns the messages from recording to readback and
    /// whether the readback equals the upload.
    /// </summary>
    private static (List<NriMessage> Provoked, bool Equal) UploadOnCopyReadOnGraphics(NriDevice device, List<NriMessage> messages, SharingMode sharingMode,
        Func<NriTexture, NriQueue, NriQueue, NriTextureBarrier> release, Func<NriTexture, NriQueue, NriQueue, NriTextureBarrier> acquire, AccessLayoutStage afterAcquire,
        TextureUsageBits usage = TextureUsageBits.SHADER_RESOURCE | TextureUsageBits.COLOR_ATTACHMENT)
    {
        var graphics = device.GetQueue(QueueType.GRAPHICS);
        var copy = device.GetQueue(QueueType.COPY);
        device.GetQueue(QueueType.COMPUTE); // three active families on Vulkan: a CONCURRENT image is really concurrent
        if (device.Desc.graphicsAPI == GraphicsAPI.VK && device.TryWrapperVK is { } vk)
            Record($"VK: GRAPHICS family {vk.GetQueueFamilyIndexVK(graphics)}, COPY family {vk.GetQueueFamilyIndexVK(copy)}");

        var rowPitch = (uint)Align(Width * Texel, device.Desc.memoryAlignment.uploadBufferTextureRow);
        var slicePitch = (uint)Align((ulong)rowPitch * Height, device.Desc.memoryAlignment.uploadBufferTextureSlice);
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D,
            usage = usage, // NRI validation allows EXCLUSIVE for attachments only
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

        using var fence = device.CreateFence();
        using var copyAllocator = copy.CreateCommandAllocator();
        using var copyCommands = copyAllocator.CreateCommandBuffer();
        using var graphicsAllocator = graphics.CreateCommandAllocator();
        using var graphicsCommands = graphicsAllocator.CreateCommandBuffer();
        var start = messages.Count;

        copyCommands.Begin();
        copyCommands.Barrier(new NriTextureBarrier(texture, Undefined, TextureCopyDestination));
        copyCommands.UploadBufferToTexture(texture, region, upload, layout);
        copyCommands.Barrier(release(texture, copy, graphics));
        copyCommands.End();
        copy.Submit([copyCommands], default, [new NriFenceSubmit(fence, 1, StageBits.COPY)]);

        graphicsCommands.Begin();
        graphicsCommands.Barrier(acquire(texture, copy, graphics));
        var current = afterAcquire;
        if (current.layout == Layout.COPY_DESTINATION)
        {
            graphicsCommands.Barrier(new NriTextureBarrier(texture, current, ShaderRead));
            current = ShaderRead;
        }
        if (current.layout != Layout.COPY_SOURCE)
            graphicsCommands.Barrier(new NriTextureBarrier(texture, current, TextureCopySource));
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
        return (provoked, actual.AsSpan().SequenceEqual(pattern));
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
    /// upload needs there. D3D12 copy lists allow only the COMMON layout: NRI (732f737) records
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
        using var device = CreateThreeQueueDevice(backend.Api, messages);
        if (backend.Api == GraphicsAPI.D3D12)
        {
            var noDebugLayer = NoDebugLayer(device);
            Assert.SkipWhen(noDebugLayer is not null, noDebugLayer ?? "");
        }
        Assert.SkipWhen(device.Desc.adapterDesc.queueNum[(int)QueueType.COPY] == 0, $"{backend}: adapterDesc.queueNum[COPY] is 0, no COPY queue");

        NriTextureBarrier HandOff(NriTexture t, NriQueue copy, NriQueue graphics) => new(t, TextureCopyDestination, TextureCopyDestination, SrcQueue: copy, DstQueue: graphics);
        var (provoked, equal) = UploadOnCopyReadOnGraphics(device, messages, sharingMode, HandOff, HandOff, TextureCopyDestination);
        Record($"{backend}, {sharingMode}: {provoked.Count} message(s), {provoked.Count(m => m.IsError)} error(s) from recording to readback; " +
            $"{messages.Count(m => m.IsError)} error(s) in all; readback {(equal ? "equals" : "differs from")} the upload");

        Assert.DoesNotContain(messages, m => m.IsError);
        Assert.True(equal, "the readback differs from the upload");
    }

    /// <summary>
    /// A hand-off between two ordinary queues on a CONCURRENT image, with the Khronos
    /// best-practices checks on. The texture is sampled only (no attachment usage), the usual
    /// shape of an upload on the COPY queue. A queue family ownership transfer is defined only for
    /// an EXCLUSIVE image or with the external family; the layer treats any barrier whose two
    /// family indices differ as one, whatever the sharing mode, and with maintenance9 supported it
    /// flags it on an image without attachment usage as "BestPractices-PipelineBarrier-unneeded-QFOT"
    /// (text in AGENTS.md: Barriers), once for the release and once for the acquire. NRI v190
    /// wrote the two queues' indices whatever the sharing mode; since 2603edc it writes
    /// VK_QUEUE_FAMILY_IGNORED on both sides for a CONCURRENT image. The checks are switched on
    /// through the process environment before device creation (the layer reads it at instance
    /// creation); .NET on Unix does not write the native environment, so the test skips there, with
    /// a committed 64 KiB buffer's "small-dedicated-allocation" warning as the marker.
    /// </summary>
    [Fact]
    public void A_hand_off_between_two_queues_is_no_ownership_transfer_on_a_CONCURRENT_image()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        var messages = new List<NriMessage>();
        NriDevice device;
        var previous = Environment.GetEnvironmentVariable(BestPracticesSetting);
        Environment.SetEnvironmentVariable(BestPracticesSetting, "true");
        try
        {
            device = CreateThreeQueueDevice(GraphicsAPI.VK, messages);
        }
        finally
        {
            Environment.SetEnvironmentVariable(BestPracticesSetting, previous);
        }
        using var _ = device;
        Assert.SkipWhen(device.Desc.adapterDesc.queueNum[(int)QueueType.COPY] == 0, "VK: adapterDesc.queueNum[COPY] is 0, no COPY queue");

        const string marker = "small-dedicated-allocation";
        using (device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 64 * 1024 }))
        {
        }
        Assert.SkipWhen(!messages.Any(m => m.Text.Contains(marker, StringComparison.Ordinal) || m.Text.Contains("fully consumed by the buffer", StringComparison.Ordinal)),
            $"the Khronos best-practices checks did not activate ({BestPracticesSetting} is read from the process environment at instance creation; no '{marker}' warning for a committed 64 KiB buffer)");
        RequireTwoFamilies(device);

        NriTextureBarrier HandOff(NriTexture t, NriQueue copy, NriQueue graphics) => new(t, TextureCopyDestination, TextureCopyDestination, SrcQueue: copy, DstQueue: graphics);
        var (provoked, equal) = UploadOnCopyReadOnGraphics(device, messages, SharingMode.CONCURRENT, HandOff, HandOff, TextureCopyDestination, TextureUsageBits.SHADER_RESOURCE);
        var qfot = provoked.Where(m => m.Text.Contains("unneeded-QFOT", StringComparison.Ordinal)).ToList();
        Record($"VK, CONCURRENT, SHADER_RESOURCE only, best practices on: {qfot.Count} 'unneeded-QFOT' message(s), {provoked.Count(m => m.IsError)} error(s); " +
            $"readback {(equal ? "equals" : "differs from")} the upload");

        Assert.Empty(qfot);
        Assert.DoesNotContain(messages, m => m.IsError);
        Assert.True(equal, "the readback differs from the upload");
    }

    /// <summary>
    /// A release that transitions and an acquire that names only the destination layout: the COPY
    /// queue releases {COPY_DESTINATION} -> {COPY_SOURCE}, the GRAPHICS queue acquires
    /// {COPY_SOURCE} -> {COPY_SOURCE} (copy stages on both sides: without a transfer each barrier is
    /// validated against its own queue, and a COPY queue has no shader stages). For an ownership
    /// transfer Vulkan requires both halves to name the same transition; the Khronos layer applies
    /// an acquire's layouts and ignores a release's, so the GRAPHICS command buffer expects
    /// TRANSFER_SRC_OPTIMAL where the layer tracks TRANSFER_DST_OPTIMAL and the submit draws
    /// VUID-vkCmdDraw-None-09600 (text in AGENTS.md: Barriers). That is the EXCLUSIVE case, and the
    /// CONCURRENT case on NRI v190, which wrote the family indices there too. Since 2603edc a
    /// CONCURRENT image's two barriers carry no indices: two ordinary barriers on their own queues,
    /// ordered by the fence, and legal.
    /// (The layer matches a release and an acquire by queues and subresources only, so a pair
    /// that differs only in the acquire's new layout draws nothing on either mode.)
    /// </summary>
    [Theory]
    [MemberData(nameof(SharingModes))]
    public void A_release_and_acquire_naming_different_transitions_is_an_error_only_on_an_EXCLUSIVE_image(SharingMode sharingMode)
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        var messages = new List<NriMessage>();
        using var device = CreateThreeQueueDevice(GraphicsAPI.VK, messages);
        Assert.SkipWhen(device.Desc.adapterDesc.queueNum[(int)QueueType.COPY] == 0, "VK: adapterDesc.queueNum[COPY] is 0, no COPY queue");
        RequireTwoFamilies(device);

        var (provoked, equal) = UploadOnCopyReadOnGraphics(device, messages, sharingMode,
            (t, copy, graphics) => new(t, TextureCopyDestination, TextureCopySource, SrcQueue: copy, DstQueue: graphics),
            (t, copy, graphics) => new(t, TextureCopySource, TextureCopySource, SrcQueue: copy, DstQueue: graphics),
            TextureCopySource);
        var errors = provoked.Where(m => m.IsError).ToList();
        Record($"VK, {sharingMode}, release COPY_DESTINATION -> COPY_SOURCE, acquire COPY_SOURCE -> COPY_SOURCE: " +
            $"{errors.Count} error(s); readback {(equal ? "equals" : "differs from")} the upload");

        if (sharingMode == SharingMode.EXCLUSIVE)
        {
            Assert.NotEmpty(errors);
            return;
        }
        Assert.DoesNotContain(messages, m => m.IsError);
        Assert.True(equal, "the readback differs from the upload");
    }
}
