// An attachment resolve, AttachmentDesc::resolveDst: the triangle rendered into a 4x multisampled
// colour attachment, resolved at CmdEndRendering into a single-sample texture, read back and
// checked pixel by pixel, with the validation layers on.
//
// NRI 48d4a33 (after v190) made attachment resolves explicit in barriers: RESOLVE_ATTACHMENT stage,
// RESOLVE_ATTACHMENT_SOURCE / _DESTINATION access, and the header now says to add the source access
// and stage to the multisampled attachment's barriers and to prepare the resolve destination with
// the destination access and stage. Under v190 the header asked for the destination "in
// COLOR_ATTACHMENT state". Both ways are measured, and both are clean with the layers on and
// resolve the same pixels (NVIDIA, Agility 1.619, VVL 1.4.341): barriers written for v190 keep
// working for a plain attachment resolve, and the resolve names are what the header asks for now.
// See AGENTS.md: Barriers.

using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed class AttachmentResolveTests(ITestOutputHelper output)
{
    private const ushort Size = 64;
    private const byte Samples = 4;

    public static TheoryData<Backend, bool> Cases
    {
        get
        {
            var data = new TheoryData<Backend, bool>();
            foreach (var api in SharpNRI.Testing.NriTestDevice.GpuBackends)
            {
                data.Add(new Backend(api), true);
                data.Add(new Backend(api), false);
            }
            return data;
        }
    }

    /// <summary>
    /// <paramref name="resolveBarriers"/>: the barriers name the resolve access and stage as NRI's
    /// header asks since 48d4a33; otherwise they are v190's colour-attachment barriers on both
    /// textures.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void A_multisampled_attachment_resolves_into_its_resolve_destination(Backend backend, bool resolveBarriers)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var support = device.GetFormatSupport(Format.RGBA8_UNORM);
        Assert.SkipUnless((support & FormatSupportBits.MULTISAMPLE_4X) != 0, "RGBA8_UNORM has no MULTISAMPLE_4X support");
        Assert.SkipUnless((support & FormatSupportBits.MULTISAMPLE_RESOLVE) != 0, "RGBA8_UNORM has no MULTISAMPLE_RESOLVE support");
        var spirv = backend.Api == GraphicsAPI.VK;

        using var msaa = device.CreateCommittedTexture(MemoryLocation.DEVICE, Texture(Samples));
        using var resolved = device.CreateCommittedTexture(MemoryLocation.DEVICE, Texture(1));
        using var msaaView = msaa.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
        using var resolvedView = resolved.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);

        ref readonly var desc = ref device.Desc;
        var rowPitch = Align(Size * 4u, desc.memoryAlignment.uploadBufferTextureRow);
        var slicePitch = Align(rowPitch * Size, desc.memoryAlignment.uploadBufferTextureSlice);
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = slicePitch });

        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc { ShaderStages = StageBits.VERTEX_SHADER | StageBits.FRAGMENT_SHADER });
        using var pipeline = device.CreateGraphicsPipeline(new NriGraphicsPipelineDesc
        {
            PipelineLayout = layout,
            InputAssembly = new InputAssemblyDesc { topology = Topology.TRIANGLE_LIST },
            Rasterization = new RasterizationDesc { fillMode = FillMode.SOLID, cullMode = CullMode.NONE },
            Multisample = new MultisampleDesc { sampleMask = uint.MaxValue, sampleNum = Samples },
            OutputMerger = new NriOutputMergerDesc
            {
                Colors = [new ColorAttachmentDesc { format = Format.RGBA8_UNORM, colorWriteMask = ColorWriteBits.RGBA }],
            },
            Shaders =
            [
                new NriShaderDesc(StageBits.VERTEX_SHADER, Shader("Triangle.vs", spirv), "vsMain"),
                new NriShaderDesc(StageBits.FRAGMENT_SHADER, Shader("Triangle.ps", spirv), "psMain"),
            ],
        });

        var drawing = resolveBarriers
            ? State(AccessBits.COLOR_ATTACHMENT | AccessBits.RESOLVE_ATTACHMENT_SOURCE, StageBits.COLOR_ATTACHMENT | StageBits.RESOLVE_ATTACHMENT)
            : State(AccessBits.COLOR_ATTACHMENT, StageBits.COLOR_ATTACHMENT);
        var receiving = resolveBarriers
            ? State(AccessBits.RESOLVE_ATTACHMENT_DESTINATION, StageBits.RESOLVE_ATTACHMENT)
            : State(AccessBits.COLOR_ATTACHMENT, StageBits.COLOR_ATTACHMENT);
        var copySource = new AccessLayoutStage { access = AccessBits.COPY_SOURCE, layout = Layout.COPY_SOURCE, stages = StageBits.COPY };

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        using var fence = device.CreateFence();
        var start = messages.Count;
        commandBuffer.Begin();
        commandBuffer.Barrier([], [], [new NriTextureBarrier(msaa, GpuWork.Undefined, drawing), new NriTextureBarrier(resolved, GpuWork.Undefined, receiving)]);
        var clear = new ClearValue();
        clear.color.f = new Color32f { x = 0.2f, y = 0.4f, z = 0.6f, w = 1.0f };
        commandBuffer.BeginRendering([new NriAttachment(msaaView, clear, LoadOp.CLEAR, StoreOp.STORE, ResolveOp.AVERAGE, resolvedView)]);
        commandBuffer.SetViewport(new Viewport { x = 0, y = 0, width = Size, height = Size, depthMin = 0, depthMax = 1 });
        commandBuffer.SetScissor(new Rect { x = 0, y = 0, width = Size, height = Size });
        commandBuffer.SetPipelineLayout(BindPoint.GRAPHICS, layout);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.Draw(3);
        commandBuffer.EndRendering();
        commandBuffer.Barrier(new NriTextureBarrier(resolved, receiving, copySource));
        commandBuffer.ReadbackTextureToBuffer(
            readback,
            new TextureDataLayoutDesc { offset = 0, rowPitch = rowPitch, slicePitch = slicePitch },
            resolved,
            new TextureRegionDesc { x = 0, y = 0, z = 0, width = Size, height = Size, depth = 1, mipOffset = 0, layerOffset = 0, planes = PlaneBits.COLOR });
        commandBuffer.End();
        queue.Submit(commandBuffer, fence, 1);
        GpuWork.Wait(fence, 1);

        var mapped = readback.Map();
        var pixels = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
            mapped.Slice((int)(y * rowPitch), Size * 4).CopyTo(pixels.AsSpan(y * Size * 4, Size * 4));
        readback.Unmap();
        device.WaitIdle();

        List<NriMessage> provoked;
        lock (messages) provoked = messages.Skip(start).ToList();
        foreach (var m in provoked) output.WriteLine("message: " + m);
        var corner = Pixel(pixels, 0, 0);
        var top = Pixel(pixels, 32, 20);
        GpuWork.Record($"{backend}, {(resolveBarriers ? "resolve access and stage named" : "v190 colour-attachment barriers")}: " +
            $"{provoked.Count} message(s), {provoked.Count(m => m.IsError)} error(s), corner {corner}, top {top}");

        Assert.True(Math.Abs(corner.R - 51) <= 1 && Math.Abs(corner.G - 102) <= 1 && Math.Abs(corner.B - 153) <= 1 && corner.A == 255, $"corner is {corner}, expected the clear colour");
        Assert.True(top.R > top.G && top.R > top.B && top.A == 255, $"top vertex region is {top}, expected red-dominant");
        Assert.DoesNotContain(provoked, m => m.IsError);
    }

    private static TextureDesc Texture(byte samples) => new()
    {
        type = TextureType.TEXTURE_2D,
        usage = TextureUsageBits.COLOR_ATTACHMENT,
        format = Format.RGBA8_UNORM,
        width = Size, height = Size, depth = 1, mipNum = 1, layerNum = 1, sampleNum = samples,
    };

    private static AccessLayoutStage State(AccessBits access, StageBits stages) => new() { access = access, layout = Layout.COLOR_ATTACHMENT, stages = stages };

    private static byte[] Shader(string name, bool spirv) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", name + (spirv ? ".spv" : ".dxil")));

    private static uint Align(uint value, uint alignment) =>
        alignment <= 1 ? value : (value + alignment - 1) / alignment * alignment;

    private static (byte R, byte G, byte B, byte A) Pixel(byte[] pixels, int x, int y)
    {
        var i = (y * Size + x) * 4;
        return (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }
}
