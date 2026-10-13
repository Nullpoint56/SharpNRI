// The RayTracing extension: a BLAS from one triangle, a TLAS with one instance, and an inline ray
// query from a compute shader that reports hit or miss per ray, read back and checked.

using System.Runtime.InteropServices;
using PrismNRI.Native;
using Xunit;

namespace PrismNRI.Tests;

public sealed class RayTracingTests
{
    private const int Width = 16;

    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void The_extension_follows_the_feature_flag(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var supported = device.Desc.tiers.rayTracing != 0;
        Assert.Equal(supported, device.TryRayTracing is not null);
        if (!supported) Assert.Throws<NriUnsupportedException>(() => device.RayTracing);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_ray_query_hits_the_triangle_in_a_built_TLAS(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.tiers.rayTracing == 0, $"{backend} reports no ray tracing");
        var rt = device.RayTracing;

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        using var fence = device.CreateFence();

        // Geometry: one triangle on z = 1 covering the middle of the ray field.
        float[] vertices = [-0.5f, -0.5f, 1f, 0.5f, -0.5f, 1f, 0f, 0.5f, 1f];
        using var vertexBuffer = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 36, usage = BufferUsageBits.ACCELERATION_STRUCTURE_BUILD_INPUT });
        MemoryMarshal.AsBytes(vertices.AsSpan()).CopyTo(vertexBuffer.Map());
        vertexBuffer.Unmap();

        var triangles = new NriBottomLevelTriangles(vertexBuffer, 0, 3, 12, Format.RGB32_SFLOAT);
        var geometry = NriBottomLevelGeometry.FromTriangles(triangles, BottomLevelGeometryBits.OPAQUE_GEOMETRY);

        using var blas = rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, new NriAccelerationStructureDesc
        {
            Type = AccelerationStructureType.BOTTOM_LEVEL,
            Geometries = [NriBottomLevelGeometry.FromTriangles(triangles with { VertexBuffer = NriBuffer.HasBuffer }, BottomLevelGeometryBits.OPAQUE_GEOMETRY)],
            Flags = AccelerationStructureBits.PREFER_FAST_TRACE,
        });
        using var tlas = rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, new NriAccelerationStructureDesc
        {
            Type = AccelerationStructureType.TOP_LEVEL,
            InstanceNum = 1,
            Flags = AccelerationStructureBits.PREFER_FAST_TRACE,
        });
        Assert.True(blas.BuildScratchBufferSize > 0 && tlas.BuildScratchBufferSize > 0);

        using var blasScratch = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = blas.BuildScratchBufferSize, usage = BufferUsageBits.SCRATCH });
        using var tlasScratch = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = tlas.BuildScratchBufferSize, usage = BufferUsageBits.SCRATCH });

        // One instance, identity transform, pointing at the BLAS.
        var instance = new TopLevelInstance { instanceId = 0, mask = 0xFF, shaderBindingTableRecordOffset = 0, flags = TopLevelInstanceBits.NONE, accelerationStructureHandle = blas.GpuHandle };
        instance.transform[0] = 1; instance.transform[5] = 1; instance.transform[10] = 1;
        using var instanceBuffer = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = (ulong)System.Runtime.CompilerServices.Unsafe.SizeOf<TopLevelInstance>(), usage = BufferUsageBits.ACCELERATION_STRUCTURE_BUILD_INPUT });
        MemoryMarshal.AsBytes(new ReadOnlySpan<TopLevelInstance>(in instance)).CopyTo(instanceBuffer.Map());
        instanceBuffer.Unmap();

        // Output: one uint per ray, in a storage buffer copied to a readback buffer.
        const ulong outputSize = Width * Width * 4;
        using var output = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = outputSize, structureStride = 4, usage = BufferUsageBits.SHADER_RESOURCE_STORAGE });
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = outputSize });
        using var outputView = output.CreateView(BufferView.STORAGE_STRUCTURED_BUFFER);
        using var tlasDescriptor = tlas.CreateDescriptor();

        // Layout: set 0 = { t0: TLAS, u1: hits }, zero binding offsets, so SPIR-V binding = register.
        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
        {
            DescriptorSets =
            [
                new NriDescriptorSetDesc(0,
                [
                    new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 1, descriptorType = DescriptorType.ACCELERATION_STRUCTURE, shaderStages = StageBits.COMPUTE_SHADER },
                    new DescriptorRangeDesc { baseRegisterIndex = 1, descriptorNum = 1, descriptorType = DescriptorType.STORAGE_STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER },
                ]),
            ],
            ShaderStages = StageBits.COMPUTE_SHADER,
        });
        using var pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, accelerationStructureMaxNum = 1, storageStructuredBufferMaxNum = 1 });
        var set = pool.AllocateDescriptorSet(layout, 0);
        set.Update(0, 0, tlasDescriptor);
        set.Update(1, 0, outputView);

        var spirv = backend.Api == GraphicsAPI.VK;
        using var pipeline = device.CreateComputePipeline(new NriComputePipelineDesc
        {
            PipelineLayout = layout,
            Shader = new NriShaderDesc(StageBits.COMPUTE_SHADER, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", spirv ? "RayQuery.cs.spv" : "RayQuery.cs.dxil")), "csMain"),
        });

        var asWrite = new AccessStage { access = AccessBits.ACCELERATION_STRUCTURE_WRITE, stages = StageBits.ACCELERATION_STRUCTURE };
        var asRead = new AccessStage { access = AccessBits.ACCELERATION_STRUCTURE_READ, stages = StageBits.ACCELERATION_STRUCTURE | StageBits.COMPUTE_SHADER };

        commandBuffer.Begin(pool);
        rt.BuildBottomLevelAccelerationStructure(commandBuffer, new NriBuildBottomLevelAccelerationStructure(blas, [geometry], blasScratch));
        commandBuffer.Barrier(new NriBufferBarrier(blas.Buffer, asWrite, asRead));
        rt.BuildTopLevelAccelerationStructure(commandBuffer, new NriBuildTopLevelAccelerationStructure(tlas, 1, instanceBuffer, 0, tlasScratch));
        commandBuffer.Barrier(new NriBufferBarrier(tlas.Buffer, asWrite, asRead));

        commandBuffer.SetPipelineLayout(BindPoint.COMPUTE, layout);
        commandBuffer.SetDescriptorSet(0, set);
        commandBuffer.SetPipeline(pipeline);
        commandBuffer.Dispatch(Width / 8, Width / 8);

        commandBuffer.Barrier(new NriBufferBarrier(output,
            new AccessStage { access = AccessBits.SHADER_RESOURCE_STORAGE, stages = StageBits.COMPUTE_SHADER },
            new AccessStage { access = AccessBits.COPY_SOURCE, stages = StageBits.COPY }));
        commandBuffer.CopyBuffer(readback, 0, output, 0, outputSize);
        commandBuffer.End();

        queue.Submit(commandBuffer, fence, 1);
        fence.Wait(1);

        var hits = MemoryMarshal.Cast<byte, uint>(readback.Map()).ToArray();
        readback.Unmap();
        device.WaitIdle();

        // One picture of the whole field in the failure message: a software ray tracer that misses
        // on one runner host and hits on another (findings 8.8) is diagnosed from the map, not from
        // "expected 1, got 0".
        var hitCount = hits.Count(h => h == 1);
        var map = string.Join("\n", Enumerable.Range(0, Width).Select(y =>
            string.Concat(Enumerable.Range(0, Width).Select(x => hits[y * Width + x] switch { 0 => '.', 1 => '#', _ => '?' }))));
        GpuWork.Record($"{backend}: {hitCount} hits\n{map}");
        void Expect(uint value, int x, int y, string where) =>
            Assert.True(hits[y * Width + x] == value, $"({x},{y}) {where}: expected {value}, got {hits[y * Width + x]}; {hitCount} hits of {Width * Width}\n{map}");
        Expect(1, 8, 8, "centre, inside the triangle");
        Expect(1, 8, 6, "inside the triangle");
        Expect(0, 0, 0, "corner, outside");
        Expect(0, Width - 1, 0, "corner, outside");
        Expect(0, 0, Width - 1, "corner, outside");
        Expect(0, Width - 1, Width - 1, "corner, outside");
        Assert.InRange(hitCount, 30, 130);         // a triangle of area 0.5 in a 2x2 field of 256 rays is 64 hits, give or take the edges
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
