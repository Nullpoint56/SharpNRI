// The RayTracing extension past the inline ray query: a ray tracing pipeline with a shader binding
// table, dispatched directly and indirectly; acceleration structures created unbound, placed into
// memory, updated, cloned and compacted; and opacity micromaps. Every path traces the RayScene
// triangle and is checked against the map the ray query produces on the same device.

using System.Runtime.InteropServices;
using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

public sealed class RayTracingPipelineTests
{
    private static NriDevice CreateRayTracingDevice(Backend backend, List<NriMessage> messages, Action<NriDeviceOptionsBuilder>? configure = null)
    {
        TestDevice.RequireAvailable(backend);
        var device = TestDevice.Create(backend.Api, messages, configure);
        if (device.Desc.tiers.rayTracing < 2)
        {
            var tier = device.Desc.tiers.rayTracing;
            device.Dispose();
            Assert.Skip(tier == 0
                ? $"{backend} reports no ray tracing"
                : $"{backend}: tiers.rayTracing = 1, no ray query to compare against (needs 2)");
        }
        return device;
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_ray_tracing_pipeline_traces_what_the_ray_query_sees_directly_and_indirectly(Backend backend)
    {
        var messages = new List<NriMessage>();
        using var device = CreateRayTracingDevice(backend, messages);
        var rt = device.RayTracing;
        using var scene = new RayScene(device, outputNum: 3);

        var blas = scene.Own(rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, RayScene.BlasDesc()));
        var blasScratch = scene.Scratch(blas.BuildScratchBufferSize);
        var tlas = scene.Tlas(blas);
        var querySet = scene.Output(tlas.Tlas);
        var directSet = scene.Output(tlas.Tlas);
        var indirectSet = scene.Output(tlas.Tlas);

        // Groups index the shader library from 1: raygen, miss, closest hit. On Vulkan the three
        // entry points share one SPIR-V module. On D3D12 each NriShaderDesc becomes a DXIL library
        // subobject exporting everything in it, so each entry point is its own library (one shared
        // library exported every name three times, and CreateStateObject faulted).
        byte[] Library(string entry) => RayScene.Shader(device, device.GraphicsApi == GraphicsAPI.VK ? "RayPipeline.lib" : $"RayPipeline.{entry}");
        using var pipeline = rt.CreateRayTracingPipeline(new NriRayTracingPipelineDesc
        {
            PipelineLayout = scene.Layout,
            Shaders =
            [
                new NriShaderDesc(StageBits.RAYGEN_SHADER, Library("rayGen"), "rayGen"),
                new NriShaderDesc(StageBits.MISS_SHADER, Library("miss"), "miss"),
                new NriShaderDesc(StageBits.CLOSEST_HIT_SHADER, Library("closestHit"), "closestHit"),
            ],
            ShaderGroups = [Group(1), Group(2), Group(3)],
            RecursionMaxDepth = 1,
            RayPayloadMaxSize = 4,
            RayHitAttributeMaxSize = 8,
        });

        // Shader binding table: one record per group, each at the table alignment.
        var identifierSize = device.Desc.shaderStage.rayTracing.shaderGroupIdentifierSize;
        var stride = Align(identifierSize, device.Desc.memoryAlignment.shaderBindingTable);
        Record($"{backend}: shaderGroupIdentifierSize={identifierSize} shaderBindingTable alignment={device.Desc.memoryAlignment.shaderBindingTable} -> record stride {stride}");
        using var table = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 3 * stride, usage = BufferUsageBits.SHADER_BINDING_TABLE });
        var tableBytes = table.Map();
        tableBytes.Clear();
        rt.WriteShaderGroupIdentifiers(pipeline, 0, 3, (uint)stride, tableBytes);
        var identifiers = new string[3];
        for (var i = 0; i < 3; i++) identifiers[i] = Convert.ToHexString(tableBytes.Slice(i * (int)stride, (int)identifierSize));
        table.Unmap();
        Assert.Equal(3, identifiers.Distinct().Count());
        Assert.DoesNotContain(identifiers, id => id.All(c => c == '0'));

        var raygen = new NriStridedBufferRegion(table, 0, stride, stride);
        var miss = new NriStridedBufferRegion(table, stride, stride, stride);
        var hit = new NriStridedBufferRegion(table, 2 * stride, stride, stride);

        // The same dispatch as DispatchRaysIndirectDesc, by device address.
        var address = table.DeviceAddress;
        var arguments = new DispatchRaysIndirectDesc
        {
            raygenShaderRecordAddress = address, raygenShaderRecordSize = stride,
            missShaderBindingTableAddress = address + stride, missShaderBindingTableSize = stride, missShaderBindingTableStride = stride,
            hitShaderBindingTableAddress = address + 2 * stride, hitShaderBindingTableSize = stride, hitShaderBindingTableStride = stride,
            width = RayScene.Width, height = RayScene.Width, depth = 1,
        };
        using var argumentBuffer = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = (ulong)Marshal.SizeOf<DispatchRaysIndirectDesc>(), usage = BufferUsageBits.ARGUMENT });
        MemoryMarshal.AsBytes(new ReadOnlySpan<DispatchRaysIndirectDesc>(in arguments)).CopyTo(argumentBuffer.Map());
        argumentBuffer.Unmap();

        Run(device, cmd =>
        {
            scene.BuildBlas(cmd, blas, blasScratch);
            scene.BuildTlas(cmd, tlas);
            scene.TraceInline(cmd, querySet);

            cmd.SetPipelineLayout(BindPoint.RAY_TRACING, scene.Layout);
            cmd.SetPipeline(pipeline);
            cmd.SetDescriptorSet(0, directSet, BindPoint.RAY_TRACING);
            rt.DispatchRays(cmd, new NriDispatchRays(raygen, miss, hit, default, RayScene.Width, RayScene.Width));
            cmd.SetDescriptorSet(0, indirectSet, BindPoint.RAY_TRACING);
            rt.DispatchRaysIndirect(cmd, argumentBuffer, 0);

            scene.CopyOutputs(cmd);
        }, scene.Pool);

        var maps = scene.Read();
        scene.ExpectTriangle(maps[0], hit: 1, miss: 0, "ray query");
        scene.ExpectTriangle(maps[1], hit: 2, miss: 1, "DispatchRays");
        scene.ExpectTriangle(maps[2], hit: 2, miss: 1, "DispatchRaysIndirect");
        // The pipeline answers each ray as the query does: hit 2 where the query hit, miss 1 where it missed.
        Assert.Equal(maps[0].Select(v => v + 1), maps[1]);
        Assert.Equal(maps[1], maps[2]);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Placed_updated_cloned_and_compacted_BLASes_answer_the_same_rays(Backend backend)
    {
        var messages = new List<NriMessage>();
        using var device = CreateRayTracingDevice(backend, messages);
        var rt = device.RayTracing;
        using var scene = new RayScene(device, outputNum: 4);
        const AccelerationStructureBits flags = AccelerationStructureBits.PREFER_FAST_TRACE | AccelerationStructureBits.ALLOW_UPDATE | AccelerationStructureBits.ALLOW_COMPACTION;
        var desc = RayScene.BlasDesc(flags);

        // Unbound, measured, bound: the Vulkan-style path.
        var placed = scene.Own(rt.CreateAccelerationStructure(desc));
        var requirement = placed.GetMemoryDesc(MemoryLocation.DEVICE);
        if (device.Desc.features.getMemoryDesc2 != 0)
        {
            var predicted = rt.GetAccelerationStructureMemoryDesc(desc, MemoryLocation.DEVICE);
            Record($"{backend}: BLAS memory {requirement.size} B (alignment {requirement.alignment}); GetMemoryDesc2 predicted {predicted.size} B");
            Assert.Equal(requirement.size, predicted.size);
            Assert.Equal(requirement.type, predicted.type);
        }
        var memory = scene.OwnBeneath(device.AllocateMemory(new AllocateMemoryDesc { size = requirement.size, type = requirement.type }));
        rt.BindAccelerationStructureMemory(placed, memory);

        var build = placed.BuildScratchBufferSize;
        var update = placed.UpdateScratchBufferSize;
        Record($"{backend}: scratch build {build} B, update {update} B");
        Assert.True(build > 0, "build scratch is 0");
        // Not "update > 0": WARP answers 0 for an ALLOW_UPDATE BLAS (measured), and the update below
        // still runs and traces the same with a 1-byte scratch.
        Assert.True(update <= build, $"update scratch {update} > build scratch {build}");

        var scratch = scene.Scratch(build);
        var updateScratch = scene.Scratch(update);
        using var sizes = device.CreateQueryPool(QueryType.ACCELERATION_STRUCTURE_COMPACTED_SIZE, 1);
        using var sizeReadback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = sizes.QuerySize });

        Run(device, cmd =>
        {
            scene.BuildBlas(cmd, placed, scratch);
            // An in-place update with the update scratch, from the structure to itself.
            cmd.Barrier(new NriBufferBarrier(placed.Buffer, RayScene.AsRead, RayScene.AsWrite));
            scene.BuildBlas(cmd, placed, updateScratch, src: placed);
            cmd.ResetQueries(sizes, 0, 1);
            rt.WriteAccelerationStructureSizes(cmd, [placed], sizes, 0);
            cmd.CopyQueries(sizes, 0, 1, sizeReadback, 0);
        });
        var compactedSize = MemoryMarshal.Read<ulong>(sizeReadback.Map());
        sizeReadback.Unmap();
        Record($"{backend}: compacted size {compactedSize} B of {requirement.size} B");
        Assert.InRange(compactedSize, 1UL, requirement.size);

        var clone = scene.Own(rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, desc));
        var compact = scene.Own(rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, RayScene.BlasDesc(flags, optimizedSize: compactedSize)));
        var committed = scene.Own(rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, desc));
        var committedScratch = scene.Scratch(committed.BuildScratchBufferSize);
        var tlases = new[] { scene.Tlas(committed), scene.Tlas(placed), scene.Tlas(clone), scene.Tlas(compact) };
        var sets = tlases.Select(t => scene.Output(t.Tlas)).ToArray();

        Run(device, cmd =>
        {
            scene.BuildBlas(cmd, committed, committedScratch);
            rt.CopyAccelerationStructure(cmd, clone, placed, CopyMode.CLONE);
            rt.CopyAccelerationStructure(cmd, compact, placed, CopyMode.COMPACT);
            cmd.Barrier([], [new NriBufferBarrier(clone.Buffer, RayScene.AsWrite, RayScene.AsRead), new NriBufferBarrier(compact.Buffer, RayScene.AsWrite, RayScene.AsRead)], []);
            foreach (var t in tlases) scene.BuildTlas(cmd, t);
            foreach (var s in sets) scene.TraceInline(cmd, s);
            scene.CopyOutputs(cmd);
        }, scene.Pool);

        var maps = scene.Read();
        string[] names = ["committed", "placed and updated", "CLONE copy", "COMPACT copy"];
        for (var i = 0; i < maps.Length; i++) scene.ExpectTriangle(maps[i], hit: 1, miss: 0, names[i]);
        for (var i = 1; i < maps.Length; i++) Assert.Equal(maps[0], maps[i]);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// A two-state micromap with two micro-triangles at subdivision level 0, one transparent and one
    /// opaque, attached to the non-opaque triangle through a micromap index buffer. The ray query
    /// commits only opaque hits (it never commits a candidate), so the transparent micro-triangle
    /// must hide the triangle and the opaque one must show it exactly where the plain BLAS hits.
    /// </summary>
    /// <remarks>
    /// On D3D12 the device runs without the D3D12 debug layer (NRI's validation stays on): NRI's
    /// micromap prebuild query passes no input buffer and no per-OMM descs, the Agility SDK 1.619
    /// debug layer reports that as error 1162, and NRI's break-on-error turns it into an SEH
    /// exception out of <c>CreateMicromap</c>. Without the layer the micromap is created and built,
    /// but the first BLAS build that links it makes <c>Close</c> fail with <c>E_INVALIDARG</c>, with
    /// or without a micromap index buffer (measured on the RTX 4070 SUPER, driver and Agility SDK
    /// 1.619 as staged). That one measured failure skips, quoting it; anything else fails.
    /// </remarks>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void An_opacity_micromap_decides_whether_the_ray_query_hits(Backend backend)
    {
        var messages = new List<NriMessage>();
        using var device = CreateRayTracingDevice(backend, messages, b => b.EnableGraphicsApiValidation = backend.Api != GraphicsAPI.D3D12);
        Assert.SkipWhen(device.Desc.tiers.rayTracing < 3, $"{backend}: tiers.rayTracing = {device.Desc.tiers.rayTracing}, no micromaps (needs 3)");
        var rt = device.RayTracing;
        using var scene = new RayScene(device, outputNum: 3);

        var micromapDesc = new NriMicromapDesc
        {
            Usages = [new MicromapUsageDesc { triangleNum = 2, subdivisionLevel = 0, format = MicromapFormat.OPACITY_2_STATE }],
            Flags = MicromapBits.PREFER_FAST_TRACE,
        };
        var micromap = scene.Own(rt.CreateCommittedMicromap(MemoryLocation.DEVICE, micromapDesc));

        // Level 0 holds one micro-triangle: one bit, 0 = transparent, 1 = opaque. One byte each.
        var data = scene.Own(device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 2, usage = BufferUsageBits.MICROMAP_BUILD_INPUT }));
        new byte[] { 0, 1 }.CopyTo(data.Map());
        data.Unmap();
        MicromapTriangle[] triangles =
        [
            new() { dataOffset = 0, subdivisionLevel = 0, format = MicromapFormat.OPACITY_2_STATE },
            new() { dataOffset = 1, subdivisionLevel = 0, format = MicromapFormat.OPACITY_2_STATE },
        ];
        var triangleBuffer = scene.Own(device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = (ulong)(triangles.Length * Marshal.SizeOf<MicromapTriangle>()), usage = BufferUsageBits.MICROMAP_BUILD_INPUT }));
        MemoryMarshal.AsBytes(triangles.AsSpan()).CopyTo(triangleBuffer.Map());
        triangleBuffer.Unmap();
        var micromapScratch = scene.Scratch(micromap.BuildScratchBufferSize);

        // The triangle's micromap index: 0 (transparent) or 1 (opaque), one uint each. Without an
        // index buffer NRI passes VK_INDEX_TYPE_NONE_KHR with a zero address, which is what
        // VUID-vkCmdBuildAccelerationStructuresKHR-pInfos-10904 asks for, and validation layer
        // 1.4.341 still reports it as that VUID's error; the index buffer route is clean.
        var indices = scene.Own(device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 8, usage = BufferUsageBits.ACCELERATION_STRUCTURE_BUILD_INPUT }));
        MemoryMarshal.AsBytes<uint>([0, 1]).CopyTo(indices.Map());
        indices.Unmap();
        NriBottomLevelTrianglesMicromap Map(NriBuffer indexBuffer, ulong offset) => new(micromap, indexBuffer, offset, 0, IndexType.UINT32);
        var transparentMap = Map(indices, 0);
        var opaqueMap = Map(indices, 4);

        var plain = scene.Own(rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, RayScene.BlasDesc()));
        var transparent = scene.Own(rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, RayScene.BlasDesc(geometryFlags: BottomLevelGeometryBits.NONE, micromap: Map(NriBuffer.HasBuffer, 0))));
        var opaque = scene.Own(rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, RayScene.BlasDesc(geometryFlags: BottomLevelGeometryBits.NONE, micromap: Map(NriBuffer.HasBuffer, 4))));
        var scratches = new[] { plain, transparent, opaque }.Select(b => scene.Scratch(b.BuildScratchBufferSize)).ToArray();
        var tlases = new[] { scene.Tlas(plain), scene.Tlas(transparent), scene.Tlas(opaque) };
        var sets = tlases.Select(t => scene.Output(t.Tlas)).ToArray();

        try
        {
            Run(device, cmd =>
            {
                rt.BuildMicromaps(cmd, [new NriBuildMicromap(micromap, data, 0, triangleBuffer, 0, micromapScratch)]);
                cmd.Barrier(new NriBufferBarrier(micromap.Buffer,
                    new AccessStage { access = AccessBits.MICROMAP_WRITE, stages = StageBits.MICROMAP },
                    new AccessStage { access = AccessBits.MICROMAP_READ, stages = StageBits.ACCELERATION_STRUCTURE }));
                scene.BuildBlas(cmd, transparent, scratches[1], scene.Geometry(BottomLevelGeometryBits.NONE, transparentMap));
            });
        }
        catch (NriException e) when (backend.Api == GraphicsAPI.D3D12 && e.Result == Result.INVALID_ARGUMENT && e.Call == "EndCommandBuffer")
        {
            Assert.Skip($"D3D12: tiers.rayTracing = 3, but the command list that builds a BLAS linking a micromap fails to close: {e.Message}");
        }

        Run(device, cmd =>
        {
            rt.BuildMicromaps(cmd, [new NriBuildMicromap(micromap, data, 0, triangleBuffer, 0, micromapScratch)]);
            cmd.Barrier(new NriBufferBarrier(micromap.Buffer,
                new AccessStage { access = AccessBits.MICROMAP_WRITE, stages = StageBits.MICROMAP },
                new AccessStage { access = AccessBits.MICROMAP_READ, stages = StageBits.ACCELERATION_STRUCTURE }));
            scene.BuildBlas(cmd, plain, scratches[0]);
            scene.BuildBlas(cmd, transparent, scratches[1], scene.Geometry(BottomLevelGeometryBits.NONE, transparentMap));
            scene.BuildBlas(cmd, opaque, scratches[2], scene.Geometry(BottomLevelGeometryBits.NONE, opaqueMap));
            foreach (var t in tlases) scene.BuildTlas(cmd, t);
            foreach (var s in sets) scene.TraceInline(cmd, s);
            scene.CopyOutputs(cmd);
        }, scene.Pool);

        var maps = scene.Read();
        scene.ExpectTriangle(maps[0], hit: 1, miss: 0, "no micromap");
        Record($"{backend} transparent micromap:\n{RayScene.Map(maps[1])}");
        Assert.All(maps[1], v => Assert.Equal(0u, v));
        Assert.Equal(maps[0], maps[2]);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    private static ShaderGroupDesc Group(uint shaderIndex)
    {
        var group = new ShaderGroupDesc();
        group.shaderIndices[0] = shaderIndex;
        return group;
    }
}
