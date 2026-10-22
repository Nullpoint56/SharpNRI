// The RayTracing extension's procedural geometry: a BLAS of axis-aligned boxes built through
// NriBottomLevelGeometry.FromAabbs, traced with an inline ray query (Shaders/AabbQuery.hlsl) that
// commits every procedural candidate, so the hit map is the set of boxes the BLAS holds.

using System.Runtime.InteropServices;
using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

public sealed class RayTracingAabbTests
{
    private const uint Stride = 32; // a BottomLevelAabb is 24 bytes; both APIs need a multiple of 8
    private const ulong Offset = 32; // where the first box starts: a decoy box sits before it

    /// <summary>The two boxes, on z = 1 to 1.5 in the RayScene ray field: box 0 left and tall, box 1 right and short.</summary>
    private static readonly BottomLevelAabb[] Boxes =
    [
        new() { minX = -0.75f, minY = -0.5f, minZ = 1, maxX = -0.25f, maxY = 0.5f, maxZ = 1.5f },
        new() { minX = 0.25f, minY = -0.25f, minZ = 1, maxX = 0.75f, maxY = 0.25f, maxZ = 1.5f },
    ];

    /// <summary>
    /// Two boxes in a BLAS of type AABBS, read from an AABB buffer at a non-zero offset with a
    /// stride larger than the struct: every ray whose origin lies inside a box reports that box
    /// (primitive index + 1), every other ray misses. No ray origin is within 1/16 of a box edge,
    /// so the map is exact on any implementation. The buffer begins with a decoy box covering the
    /// whole field and pads each stride with zeros, so a BLAS read from the wrong offset or at the
    /// struct size shows a different map.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_BLAS_of_two_boxes_answers_each_ray_with_the_box_it_hits(Backend backend)
    {
        var messages = new List<NriMessage>();
        using var device = RayScene.CreateDevice(backend, messages);
        var rt = device.RayTracing;
        using var scene = new RayScene(device, outputNum: 1);

        var bytes = new byte[Offset + Stride * (ulong)Boxes.Length];
        var decoy = new BottomLevelAabb { minX = -1, minY = -1, minZ = 0.5f, maxX = 1, maxY = 1, maxZ = 2 };
        MemoryMarshal.Write(bytes, in decoy);
        for (var i = 0; i < Boxes.Length; i++) MemoryMarshal.Write(bytes.AsSpan((int)(Offset + (ulong)i * Stride)), in Boxes[i]);
        var aabbBuffer = scene.Own(device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = (ulong)bytes.Length, usage = BufferUsageBits.ACCELERATION_STRUCTURE_BUILD_INPUT }));
        bytes.CopyTo(aabbBuffer.Map());
        aabbBuffer.Unmap();

        var aabbs = new NriBottomLevelAabbs(aabbBuffer, Offset, (uint)Boxes.Length, Stride);
        var geometry = NriBottomLevelGeometry.FromAabbs(aabbs, BottomLevelGeometryBits.OPAQUE_GEOMETRY);
        Assert.Equal(BottomLevelGeometryType.AABBS, geometry.Type);
        Assert.Equal(aabbs, geometry.Aabbs);

        var blas = scene.Own(rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, new NriAccelerationStructureDesc
        {
            Type = AccelerationStructureType.BOTTOM_LEVEL,
            Geometries = [NriBottomLevelGeometry.FromAabbs(aabbs with { Buffer = NriBuffer.HasBuffer }, BottomLevelGeometryBits.OPAQUE_GEOMETRY)],
            Flags = AccelerationStructureBits.PREFER_FAST_TRACE,
        }));
        var blasScratch = scene.Scratch(blas.BuildScratchBufferSize);
        var tlas = scene.Tlas(blas);
        var set = scene.Output(tlas.Tlas);
        var query = scene.Own(device.CreateComputePipeline(new NriComputePipelineDesc
        {
            PipelineLayout = scene.Layout,
            Shader = new NriShaderDesc(StageBits.COMPUTE_SHADER, RayScene.Shader(device, "AabbQuery.cs"), "csMain"),
        }));

        Run(device, cmd =>
        {
            scene.BuildBlas(cmd, blas, blasScratch, geometry);
            scene.BuildTlas(cmd, tlas);
            scene.TraceInline(cmd, set, query);
            scene.CopyOutputs(cmd);
        }, scene.Pool);

        var map = scene.Read()[0];
        var expected = new uint[RayScene.Width * RayScene.Width];
        for (var y = 0; y < RayScene.Width; y++)
        for (var x = 0; x < RayScene.Width; x++)
        {
            var (u, v) = RayScene.RayOrigin(x, y);
            for (var b = 0; b < Boxes.Length; b++)
                if (u >= Boxes[b].minX && u <= Boxes[b].maxX && v >= Boxes[b].minY && v <= Boxes[b].maxY) expected[y * RayScene.Width + x] = (uint)b + 1;
        }
        Record($"{backend}: {map.Count(v => v == 1)} rays hit box 0, {map.Count(v => v == 2)} box 1\n{RayScene.Map(map)}");
        Assert.True(map.SequenceEqual(expected), $"{backend}: the hit map is not the two boxes\n{RayScene.Map(map)}\nexpected\n{RayScene.Map(expected)}");
        Assert.Equal(32, map.Count(v => v == 1));
        Assert.Equal(16, map.Count(v => v == 2));
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
