// The MeshShader extension: acquisition everywhere, and on a GPU that has mesh shaders the
// release-gate triangle drawn by a mesh shader instead of a vertex shader (Shaders/MeshTriangle.hlsl,
// ms_6_5 / SPV_EXT_mesh_shader), read back and checked like TriangleTests.

using PrismNRI.Native;
using Xunit;

namespace PrismNRI.Tests;

public sealed class MeshShaderExtensionTests
{
    private const ushort Size = 64;

    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void Acquisition_follows_features_meshShader(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var expected = backend.Api == GraphicsAPI.NONE || device.Desc.features.meshShader != 0;
        GpuWork.Record($"{backend}: features.meshShader={device.Desc.features.meshShader}, acquired={device.TryMeshShader is not null}");
        Assert.Equal(expected, device.TryMeshShader is not null);
        if (expected) Assert.Same(device.MeshShader, device.TryMeshShader);
        else Assert.Throws<NriUnsupportedException>(() => device.MeshShader);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_mesh_shader_triangle_renders_offscreen_and_reads_back(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.TryMeshShader is null, $"{backend}: no mesh shaders on this adapter");

        var spirv = backend.Api == GraphicsAPI.VK;
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        using var fence = device.CreateFence();

        using var target = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.COLOR_ATTACHMENT, format = Format.RGBA8_UNORM,
            width = Size, height = Size, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
        });
        using var view = target.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);

        ref readonly var desc = ref device.Desc;
        var rowPitch = (uint)GpuWork.Align(Size * 4u, desc.memoryAlignment.uploadBufferTextureRow);
        var slicePitch = (uint)GpuWork.Align(rowPitch * Size, desc.memoryAlignment.uploadBufferTextureSlice);
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = slicePitch });

        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc { ShaderStages = StageBits.MESH_SHADER | StageBits.FRAGMENT_SHADER });
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
                new NriShaderDesc(StageBits.MESH_SHADER, Shader("MeshTriangle.ms", spirv), "msMain"),
                new NriShaderDesc(StageBits.FRAGMENT_SHADER, Shader("MeshTriangle.ps", spirv), "psMain"),
            ],
        });

        commandBuffer.Begin();
        commandBuffer.Barrier(new NriTextureBarrier(target, default, GpuWork.ColorAttachment));
        var clear = new ClearValue();
        clear.color.f = new Color32f { x = 0.2f, y = 0.4f, z = 0.6f, w = 1.0f };
        commandBuffer.BeginRendering([new NriAttachment(view, clear, LoadOp.CLEAR, StoreOp.STORE)]);
        commandBuffer.SetViewport(new Viewport { width = Size, height = Size, depthMax = 1 });
        commandBuffer.SetScissor(new Rect { width = Size, height = Size });
        commandBuffer.SetPipelineLayout(BindPoint.GRAPHICS, layout);
        commandBuffer.SetPipeline(pipeline);
        device.MeshShader.DrawMeshTasks(commandBuffer, 1);
        commandBuffer.EndRendering();
        commandBuffer.Barrier(new NriTextureBarrier(target, GpuWork.ColorAttachment, GpuWork.TextureCopySource));
        commandBuffer.ReadbackTextureToBuffer(readback, new TextureDataLayoutDesc { rowPitch = rowPitch, slicePitch = slicePitch }, target,
            new TextureRegionDesc { width = Size, height = Size, depth = 1, planes = PlaneBits.COLOR });
        commandBuffer.End();
        queue.Submit(commandBuffer, fence, 1);
        fence.Wait(1);

        var mapped = readback.Map().ToArray();
        readback.Unmap();
        (byte R, byte G, byte B, byte A) Pixel(int x, int y)
        {
            var i = (int)(y * rowPitch) + x * 4;
            return (mapped[i], mapped[i + 1], mapped[i + 2], mapped[i + 3]);
        }

        var corner = Pixel(0, 0);
        Assert.True(Math.Abs(corner.R - 51) <= 1 && Math.Abs(corner.G - 102) <= 1 && Math.Abs(corner.B - 153) <= 1, $"corner is {corner}, expected the clear colour");
        var top = Pixel(32, 20);
        Assert.True(top.R > top.G && top.R > top.B && top.A == 255, $"top vertex region is {top}, expected red-dominant");
        var right = Pixel(42, 44);
        Assert.True(right.G > right.R && right.G > right.B, $"bottom-right region is {right}, expected green-dominant");
        var left = Pixel(22, 44);
        Assert.True(left.B > left.R && left.B > left.G, $"bottom-left region is {left}, expected blue-dominant");
        device.WaitIdle();

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    private static byte[] Shader(string name, bool spirv) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", name + (spirv ? ".spv" : ".dxil")));
}
