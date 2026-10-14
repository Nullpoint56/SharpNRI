// The release gate: a triangle through the whole stack, offscreen, on every GPU backend, read back
// and checked pixel by pixel.
//
// Everything a renderer does in a frame is here once: a colour attachment, a pipeline layout, a
// graphics pipeline from real bytecode (SPIR-V on Vulkan, DXIL on D3D12, both compiled by dxc from
// Shaders/Triangle.hlsl), barriers, a render pass with a clear, a draw, a copy into a readback
// buffer, a fence, a map. The validation layers watch all of it and any error fails the test.

using System.Security.Cryptography;
using PrismNRI.Native;
using Xunit;

namespace PrismNRI.Tests;

public sealed class TriangleTests(ITestOutputHelper output)
{
    private const ushort Size = 64;

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_triangle_renders_offscreen_and_reads_back(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var pixels = RenderTriangle(device);
        var hash = Convert.ToHexString(SHA256.HashData(pixels));
        output.WriteLine($"{backend}: {Size}x{Size} RGBA8, sha256 {hash}");

        // Corners are outside the triangle: the clear colour, exactly.
        AssertPixel(pixels, 0, 0, 51, 102, 153, "top-left corner");
        AssertPixel(pixels, Size - 1, 0, 51, 102, 153, "top-right corner");
        AssertPixel(pixels, 0, Size - 1, 51, 102, 153, "bottom-left corner");
        AssertPixel(pixels, Size - 1, Size - 1, 51, 102, 153, "bottom-right corner");

        // Inside, near each vertex, that vertex's colour dominates. Top is red at (32,16); the
        // bottom-right is green at (48,48); the bottom-left is blue at (16,48). Sampling a few
        // pixels inside each corner avoids the edge, where coverage rules may differ per backend.
        var top = Pixel(pixels, 32, 20);
        Assert.True(top.R > top.G && top.R > top.B && top.A == 255, $"top vertex region is {top}, expected red-dominant");
        var right = Pixel(pixels, 42, 44);
        Assert.True(right.G > right.R && right.G > right.B && right.A == 255, $"bottom-right region is {right}, expected green-dominant");
        var left = Pixel(pixels, 22, 44);
        Assert.True(left.B > left.R && left.B > left.G && left.A == 255, $"bottom-left region is {left}, expected blue-dominant");

        // The centre is a mix of all three and nothing like the clear.
        var centre = Pixel(pixels, 32, 36);
        Assert.True(centre.A == 255 && centre.R + centre.G + centre.B > 150, $"centre is {centre}");

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Renders the triangle and returns the attachment tightly packed, RGBA8, row-major. Into
    /// <paramref name="target"/> when given (64x64 RGBA8_UNORM with COLOR_ATTACHMENT usage, owned
    /// by the caller, left in the COPY_SOURCE layout), otherwise into a committed texture of its own.
    /// </summary>
    internal static byte[] RenderTriangle(NriDevice device, NriTexture? target = null)
    {
        ref readonly var desc = ref device.Desc;
        var api = device.GraphicsApi;
        var spirv = api == GraphicsAPI.VK;
        Assert.True(spirv ? desc.features.shaderBytecodeSPIRV != 0 : desc.features.shaderBytecodeDXIL != 0, "device does not accept the bytecode kind the test ships");

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        using var fence = device.CreateFence();

        using var ownTarget = target is null
            ? device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
            {
                type = TextureType.TEXTURE_2D,
                usage = TextureUsageBits.COLOR_ATTACHMENT,
                format = Format.RGBA8_UNORM,
                width = Size, height = Size, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
            })
            : null;
        target ??= ownTarget!;
        Assert.True(target.Desc is { width: Size, height: Size, format: Format.RGBA8_UNORM }, "the target must be 64x64 RGBA8_UNORM");
        using var targetView = target.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);

        var rowPitch = Align(Size * 4u, desc.memoryAlignment.uploadBufferTextureRow);
        var slicePitch = Align(rowPitch * Size, desc.memoryAlignment.uploadBufferTextureSlice);
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = slicePitch, usage = BufferUsageBits.NONE });

        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
        {
            ShaderStages = StageBits.VERTEX_SHADER | StageBits.FRAGMENT_SHADER,
        });

        using var pipeline = device.CreateGraphicsPipeline(new NriGraphicsPipelineDesc
        {
            PipelineLayout = layout,
            InputAssembly = new InputAssemblyDesc { topology = Topology.TRIANGLE_LIST },
            Rasterization = new RasterizationDesc { fillMode = FillMode.SOLID, cullMode = CullMode.NONE },
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

        commandBuffer.Begin();
        commandBuffer.Barrier(new NriTextureBarrier(
            target,
            default,
            new AccessLayoutStage { access = AccessBits.COLOR_ATTACHMENT, layout = Layout.COLOR_ATTACHMENT, stages = StageBits.COLOR_ATTACHMENT }));

        var clear = new ClearValue();
        clear.color.f = new Color32f { x = 0.2f, y = 0.4f, z = 0.6f, w = 1.0f };
        commandBuffer.BeginRendering([new NriAttachment(targetView, clear, LoadOp.CLEAR, StoreOp.STORE)]);
        commandBuffer.SetViewport(new Viewport { x = 0, y = 0, width = Size, height = Size, depthMin = 0, depthMax = 1 });
        commandBuffer.SetScissor(new Rect { x = 0, y = 0, width = Size, height = Size });
        commandBuffer.SetPipelineLayout(BindPoint.GRAPHICS, layout);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.Draw(3);
        commandBuffer.EndRendering();

        commandBuffer.Barrier(new NriTextureBarrier(
            target,
            new AccessLayoutStage { access = AccessBits.COLOR_ATTACHMENT, layout = Layout.COLOR_ATTACHMENT, stages = StageBits.COLOR_ATTACHMENT },
            new AccessLayoutStage { access = AccessBits.COPY_SOURCE, layout = Layout.COPY_SOURCE, stages = StageBits.COPY }));
        commandBuffer.ReadbackTextureToBuffer(
            readback,
            new TextureDataLayoutDesc { offset = 0, rowPitch = rowPitch, slicePitch = slicePitch },
            target,
            new TextureRegionDesc { x = 0, y = 0, z = 0, width = Size, height = Size, depth = 1, mipOffset = 0, layerOffset = 0, planes = PlaneBits.COLOR });
        // No host barrier on the readback buffer: NRI's validation rejects HOST_READ access on a
        // buffer whose usage is NONE (measured, CLAUDE.md: Memory and resources); the fence wait is what makes
        // the copy visible to the host, as on D3D12.
        commandBuffer.End();

        queue.Submit(commandBuffer, fence, 1);
        fence.Wait(1);

        var mapped = readback.Map();
        var pixels = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            mapped.Slice((int)(y * rowPitch), Size * 4).CopyTo(pixels.AsSpan(y * Size * 4, Size * 4));
        }
        readback.Unmap();
        device.WaitIdle();
        return pixels;
    }

    private static byte[] Shader(string name, bool spirv) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", name + (spirv ? ".spv" : ".dxil")));

    private static uint Align(uint value, uint alignment) =>
        alignment <= 1 ? value : (value + alignment - 1) / alignment * alignment;

    private static (byte R, byte G, byte B, byte A) Pixel(byte[] pixels, int x, int y)
    {
        var i = (y * Size + x) * 4;
        return (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }

    private static void AssertPixel(byte[] pixels, int x, int y, byte r, byte g, byte b, string what)
    {
        var p = Pixel(pixels, x, y);
        // Within 1 of the expected value: UNORM rounding of the clear colour is the driver's.
        Assert.True(Math.Abs(p.R - r) <= 1 && Math.Abs(p.G - g) <= 1 && Math.Abs(p.B - b) <= 1 && p.A == 255, $"{what} ({x},{y}) is {p}, expected ({r},{g},{b},255)");
    }
}
