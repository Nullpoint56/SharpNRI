// The RayTracing extension's D3D12-style and Vulkan-style memory paths that the committed objects
// of RayTracingTests and RayTracingPipelineTests do not take: acceleration structures placed into
// one allocation at aligned offsets, and micromaps created unbound and bound, or placed, measured
// with CmdWriteMicromapSizes and copied with CmdCopyMicromap. Every structure is traced with the
// RayScene ray query and checked against a committed one on the same device.

using System.Runtime.InteropServices;
using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

public sealed class RayTracingMemoryTests
{
    /// <summary>
    /// A BLAS at offset 0 and a TLAS at an odd multiple of its predicted alignment, both in one
    /// allocation sized from NRI's own prediction (<c>GetAccelerationStructureMemoryDesc2</c>),
    /// through <c>CreatePlacedAccelerationStructure</c>. The placed pair answers every ray as a
    /// committed pair does. NRI's validation checks each placement against the same prediction
    /// (alignment and range), so a wrong offset is an error message and fails the test.
    /// </summary>
    /// <remarks>
    /// A null memory is also legal in NRI (<c>NriOptional</c>): validation then reads the offset as a
    /// <c>MemoryLocation</c> ("'offset' is not a valid 'MemoryLocation'") and the backends create a
    /// committed structure there. The binding's spelling of that is
    /// <see cref="NriRayTracingExtension.CreateCommittedAccelerationStructure"/>, so its placed
    /// overload takes a memory.
    /// </remarks>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_BLAS_and_a_TLAS_placed_in_one_allocation_answer_the_rays_of_committed_ones(Backend backend)
    {
        var messages = new List<NriMessage>();
        using var device = RayScene.CreateDevice(backend, messages);
        Assert.SkipWhen(device.Desc.features.getMemoryDesc2 == 0, $"{backend}: features.getMemoryDesc2 is false, no prediction to size the shared allocation from");
        var rt = device.RayTracing;
        using var scene = new RayScene(device, outputNum: 2);

        var blasDesc = RayScene.BlasDesc();
        var blasMemory = rt.GetAccelerationStructureMemoryDesc(blasDesc, MemoryLocation.DEVICE);
        var tlasMemory = rt.GetAccelerationStructureMemoryDesc(RayScene.TlasDesc, MemoryLocation.DEVICE);
        Record($"{backend}: BLAS {blasMemory.size} B (alignment {blasMemory.alignment}, type 0x{blasMemory.type:X}, dedicated {blasMemory.mustBeDedicated != 0}), "
            + $"TLAS {tlasMemory.size} B (alignment {tlasMemory.alignment}, type 0x{tlasMemory.type:X}, dedicated {tlasMemory.mustBeDedicated != 0})");
        Assert.Equal(blasMemory.type, tlasMemory.type);
        Assert.True(blasMemory.mustBeDedicated == 0 && tlasMemory.mustBeDedicated == 0, $"{backend}: an acceleration structure needs a dedicated allocation, so two cannot share one");

        // The TLAS goes after the BLAS at an odd multiple of the alignment its memory desc reports,
        // the least aligned offset that desc allows: an alignment too small for an acceleration
        // structure then shows. Before patch 0031 NRI's D3D12 backend reported the tight-placement
        // alignment of a plain buffer (128, measured) while acceleration structure addresses must be
        // 256-aligned (N7): the TLAS placed at 2688 passed NRI's validation and its descriptor was
        // refused ("[31] ID3D12Device::TryCreateShaderResourceView: For
        // D3D12_SRV_DIMENSION_RAYTRACING_ACCELERATION_STRUCTURE, the address (...) must be aligned to 256 bytes.").
        // On Vulkan the desc is the requirement of the structure's buffer (256 on NVIDIA, 64 on
        // lavapipe, measured), which NRI binds at the offset; the spec's 256 bytes, the device's
        // memoryAlignment.accelerationStructureOffset there, is for the structure's offset inside
        // that buffer, which NRI keeps 0.
        var tlasAlignment = Math.Max(1u, tlasMemory.alignment);
        var tlasOffset = Align(blasMemory.size, tlasAlignment);
        if (tlasOffset / tlasAlignment % 2 == 0) tlasOffset += tlasAlignment;
        var memory = scene.OwnBeneath(device.AllocateMemory(new AllocateMemoryDesc { size = tlasOffset + tlasMemory.size, type = blasMemory.type }));
        var placedBlas = scene.Own(rt.CreatePlacedAccelerationStructure(memory, 0, blasDesc));
        var placedTlas = scene.Own(rt.CreatePlacedAccelerationStructure(memory, tlasOffset, RayScene.TlasDesc));
        Record($"{backend}: TLAS placed at offset {tlasOffset} of a {tlasOffset + tlasMemory.size} B allocation");
        Assert.NotEqual(0UL, placedBlas.GpuHandle);
        Assert.NotEqual(0UL, placedBlas.GetNativeObject());
        Assert.NotEqual(0UL, placedTlas.GetNativeObject());

        var committedBlas = scene.Own(rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, blasDesc));
        var committedTlas = scene.Tlas(committedBlas);
        var placed = scene.TlasInputs(placedTlas, placedBlas);
        var committedSet = scene.Output(committedTlas.Tlas);
        var placedSet = scene.Output(placed.Tlas);
        var committedScratch = scene.Scratch(committedBlas.BuildScratchBufferSize);
        var placedScratch = scene.Scratch(placedBlas.BuildScratchBufferSize);

        Run(device, cmd =>
        {
            scene.BuildBlas(cmd, committedBlas, committedScratch);
            scene.BuildBlas(cmd, placedBlas, placedScratch);
            scene.BuildTlas(cmd, committedTlas);
            scene.BuildTlas(cmd, placed);
            scene.TraceInline(cmd, committedSet);
            scene.TraceInline(cmd, placedSet);
            scene.CopyOutputs(cmd);
        }, scene.Pool);

        var maps = scene.Read();
        scene.ExpectTriangle(maps[0], hit: 1, miss: 0, "committed BLAS and TLAS");
        scene.ExpectTriangle(maps[1], hit: 1, miss: 0, "BLAS and TLAS placed in one allocation");
        Assert.Equal(maps[0], maps[1]);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The micromap memory paths: one micromap created unbound (<c>CreateMicromap</c>), measured
    /// (<c>GetMicromapMemoryDesc</c>, and where <c>features.getMemoryDesc2</c> NRI's prediction
    /// <c>GetMicromapMemoryDesc2</c>, which must agree) and bound at offset 0
    /// (<c>BindMicromapMemory</c>); a second placed after it in the same allocation
    /// (<c>CreatePlacedMicromap</c>); a <c>CLONE</c> copy of the first and a <c>COMPACT</c> copy of
    /// the second (<c>CmdCopyMicromap</c>, into committed micromaps of the same desc). Each of the
    /// four is linked into two BLASes: through the transparent micro-triangle the non-opaque
    /// triangle disappears from the ray query, through the opaque one it answers exactly where a
    /// BLAS without a micromap does.
    /// </summary>
    /// <remarks>
    /// D3D12 runs without the D3D12 debug layer (NRI's validation stays on), as
    /// <c>RayTracingPipelineTests.An_opacity_micromap_decides_whether_the_ray_query_hits</c> does:
    /// NRI's micromap prebuild query passes no input buffer and no per-OMM descs, which the debug
    /// layer refuses with error 1162 (CLAUDE.md: Ray tracing). The barriers after the builds and
    /// copies name ALL stages; see <see cref="RayScene.MicromapWritten"/>.
    /// </remarks>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Micromaps_bound_placed_cloned_and_compacted_decide_the_ray_query_like_a_committed_one(Backend backend)
    {
        var messages = new List<NriMessage>();
        using var device = CreateMicromapDevice(backend, messages);
        using var scene = new RayScene(device, outputNum: 9);
        var set = new MicromapSet(scene, backend, measureSizes: false);

        Assert.Equal(set.Requirement.size, set.Placed.GetMemoryDesc(MemoryLocation.DEVICE).size);
        Assert.All(set.All, m => Assert.NotEqual(0UL, m.Micromap.GetNativeObject()));
        TraceThrough(scene, set, backend);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// <c>CmdWriteMicromapSizes</c> into a <c>MICROMAP_COMPACTED_SIZE</c> pool: the bound and the
    /// placed micromap of the same inputs report the same compacted size, more than 0 and at most
    /// the unbound size. A <c>COMPACT</c> copy into a micromap created with that size
    /// (<c>optimizedSize</c>) reports no more than it, a <c>CLONE</c> copy reports its source's, and
    /// both decide the ray query as their sources do.
    /// </summary>
    /// <remarks>
    /// On Vulkan the driver may refuse the pool: measured on the development machine's GPU (Vulkan
    /// 1.4, Khronos layer 1.4.341, silent), vkCreateQueryPool returns VK_ERROR_UNKNOWN (-13) for
    /// VK_QUERY_TYPE_MICROMAP_COMPACTED_SIZE_EXT and ..._SERIALIZATION_SIZE_EXT whatever
    /// pipelineStatistics holds, while acceleration-structure size pools create; no feature bit
    /// predicts it. That one failure skips, quoting it; anything else fails.
    /// </remarks>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Micromaps_report_their_compacted_size_and_a_COMPACT_copy_into_it_answers_the_same_rays(Backend backend)
    {
        var messages = new List<NriMessage>();
        using var device = CreateMicromapDevice(backend, messages);
        using var scene = new RayScene(device, outputNum: 9);
        var set = new MicromapSet(scene, backend, measureSizes: true);
        Assert.SkipWhen(set.SizesRefused is not null, set.SizesRefused ?? "");

        foreach (var (name, size) in set.CompactedSizes) Record($"{backend}: {name}: compacted size {size} B");
        Assert.All(set.CompactedSizes, s => Assert.InRange(s.Size, 1UL, set.Requirement.size));
        Assert.Equal(set.CompactedSizes[0].Size, set.CompactedSizes[1].Size); // the same inputs, bound and placed
        Assert.Equal(set.CompactedSizes[0].Size, set.CompactedSizes[2].Size); // CLONE of the bound one
        Assert.InRange(set.CompactedSizes[3].Size, 1UL, set.CompactedSizes[1].Size); // COMPACT of the placed one, into its compacted size
        TraceThrough(scene, set, backend);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// NriMemory refuses disposal while an acceleration structure bound into it
    /// (<c>BindAccelerationStructureMemory</c>) or placed in it
    /// (<c>CreatePlacedAccelerationStructure</c>) is alive: a BLAS created unbound and bound at
    /// offset 0, and a second placed after it in the same allocation. Disposing the allocation
    /// throws InvalidOperationException naming 2 before NRI is called (no message; the device still
    /// counts it), and 1 once the placed one is disposed; with both gone it disposes.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Memory_refuses_disposal_while_acceleration_structures_are_bound(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.TryRayTracing is null, $"{backend}: tiers.rayTracing = {device.Desc.tiers.rayTracing}, no RayTracingInterface");
        var rt = device.RayTracing;
        var desc = RayScene.BlasDesc();

        var bound = rt.CreateAccelerationStructure(desc);
        NriAccelerationStructure? placed = null;
        NriMemory? memory = null;
        try
        {
            var requirement = bound.GetMemoryDesc(MemoryLocation.DEVICE);
            Assert.True(requirement.mustBeDedicated == 0, $"{backend}: an acceleration structure needs a dedicated allocation, so two cannot share one");
            var offset = Align(requirement.size, Math.Max(1u, requirement.alignment));
            memory = device.AllocateMemory(new AllocateMemoryDesc { size = offset + requirement.size, type = requirement.type });
            rt.BindAccelerationStructureMemory(bound, memory);
            placed = rt.CreatePlacedAccelerationStructure(memory, offset, desc);
            AssertRefusals(device, messages, memory, placed, backend);
            bound.Dispose();
            memory.Dispose();
            Assert.True(memory.IsDisposed);
        }
        finally
        {
            placed?.Dispose();
            bound.Dispose();
            memory?.Dispose();
        }
        Assert.Equal(0, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// As <see cref="Memory_refuses_disposal_while_acceleration_structures_are_bound"/> for
    /// micromaps: one created unbound and bound at offset 0 (<c>BindMicromapMemory</c>), a second
    /// placed after it (<c>CreatePlacedMicromap</c>). D3D12 runs without the debug layer, NRI's
    /// validation on, as the other micromap tests do.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Memory_refuses_disposal_while_micromaps_are_bound(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages, b => b.EnableGraphicsApiValidation = backend.Api != GraphicsAPI.D3D12);
        Assert.SkipWhen(device.Desc.tiers.rayTracing < 3, $"{backend}: tiers.rayTracing = {device.Desc.tiers.rayTracing}, no micromaps (needs 3)");
        var rt = device.RayTracing;
        var desc = RayScene.MicromapDesc();

        var bound = rt.CreateMicromap(desc);
        NriMicromap? placed = null;
        NriMemory? memory = null;
        try
        {
            var requirement = bound.GetMemoryDesc(MemoryLocation.DEVICE);
            Assert.True(requirement.mustBeDedicated == 0, $"{backend}: a micromap needs a dedicated allocation, so two cannot share one");
            var offset = Align(requirement.size, Math.Max(1u, requirement.alignment));
            memory = device.AllocateMemory(new AllocateMemoryDesc { size = offset + requirement.size, type = requirement.type });
            rt.BindMicromapMemory([(bound, memory, 0UL)]);
            placed = rt.CreatePlacedMicromap(memory, offset, desc);
            AssertRefusals(device, messages, memory, placed, backend);
            bound.Dispose();
            memory.Dispose();
            Assert.True(memory.IsDisposed);
        }
        finally
        {
            placed?.Dispose();
            bound.Dispose();
            memory?.Dispose();
        }
        Assert.Equal(0, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// <paramref name="memory"/> holds two objects, <paramref name="placed"/> one of them: disposing
    /// it is refused with the count, before NRI is called, and again with 1 after
    /// <paramref name="placed"/> is disposed.
    /// </summary>
    private static void AssertRefusals(NriDevice device, List<NriMessage> messages, NriMemory memory, NriObject placed, Backend backend)
    {
        // The count first: a wrong one would let Dispose free bound memory in this process.
        Assert.Equal(2, memory.BoundResources);
        int start;
        lock (messages) start = messages.Count;
        var live = device.LiveObjects;

        var refused = Assert.Throws<InvalidOperationException>(memory.Dispose);
        Record($"{backend}: {refused.Message}");
        Assert.Contains("with 2 resource(s) still bound", refused.Message, StringComparison.Ordinal);
        Assert.False(memory.IsDisposed);
        Assert.Equal(live, device.LiveObjects);
        lock (messages) Assert.Equal(start, messages.Count);

        placed.Dispose();
        Assert.Equal(1, memory.BoundResources);
        var stillBound = Assert.Throws<InvalidOperationException>(memory.Dispose);
        Assert.Contains("with 1 resource(s) still bound", stillBound.Message, StringComparison.Ordinal);
        Assert.False(memory.IsDisposed);
    }

    /// <summary>A device with micromaps (tiers.rayTracing 3), the D3D12 debug layer off (see the remarks above).</summary>
    private static NriDevice CreateMicromapDevice(Backend backend, List<NriMessage> messages)
    {
        var device = RayScene.CreateDevice(backend, messages, b => b.EnableGraphicsApiValidation = backend.Api != GraphicsAPI.D3D12);
        if (device.Desc.tiers.rayTracing < 3)
        {
            var tier = device.Desc.tiers.rayTracing;
            device.Dispose();
            Assert.Skip($"{backend}: tiers.rayTracing = {tier}, no micromaps (needs 3)");
        }
        return device;
    }

    /// <summary>
    /// Links each micromap of <paramref name="set"/> into a BLAS twice, through the transparent and
    /// through the opaque micro-triangle, traces all of them and a BLAS without a micromap, and
    /// checks every map against that one.
    /// </summary>
    private static void TraceThrough(RayScene scene, MicromapSet set, Backend backend)
    {
        var rt = scene.RayTracing;
        NriBottomLevelTrianglesMicromap Link(NriMicromap micromap, NriBuffer indexBuffer, ulong offset) => new(micromap, indexBuffer, offset, 0, IndexType.UINT32);
        var plain = scene.Own(rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, RayScene.BlasDesc()));
        var linked = new List<(string Name, NriAccelerationStructure Blas, NriBottomLevelGeometry Geometry, bool Opaque)>();
        foreach (var (name, micromap) in set.All)
        {
            foreach (var (state, offset) in new[] { ("transparent", 0UL), ("opaque", 4UL) })
            {
                var blas = scene.Own(rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, RayScene.BlasDesc(geometryFlags: BottomLevelGeometryBits.NONE, micromap: Link(micromap, NriBuffer.HasBuffer, offset))));
                linked.Add(($"{name}, {state}", blas, scene.Geometry(BottomLevelGeometryBits.NONE, Link(micromap, set.Inputs.Indices, offset)), offset != 0));
            }
        }
        var scratches = linked.Select(l => scene.Scratch(l.Blas.BuildScratchBufferSize)).ToArray();
        var plainScratch = scene.Scratch(plain.BuildScratchBufferSize);
        var tlases = new[] { scene.Tlas(plain) }.Concat(linked.Select(l => scene.Tlas(l.Blas))).ToArray();
        var sets = tlases.Select(t => scene.Output(t.Tlas)).ToArray();

        Run(scene.Device, cmd =>
        {
            scene.BuildBlas(cmd, plain, plainScratch);
            for (var i = 0; i < linked.Count; i++) scene.BuildBlas(cmd, linked[i].Blas, scratches[i], linked[i].Geometry);
            foreach (var t in tlases) scene.BuildTlas(cmd, t);
            foreach (var s in sets) scene.TraceInline(cmd, s);
            scene.CopyOutputs(cmd);
        }, scene.Pool);

        var maps = scene.Read();
        scene.ExpectTriangle(maps[0], hit: 1, miss: 0, "no micromap");
        for (var i = 0; i < linked.Count; i++)
        {
            var map = maps[i + 1];
            Record($"{backend} {linked[i].Name}: {map.Count(v => v == 1)} hits");
            if (!linked[i].Opaque) Assert.True(map.All(v => v == 0), $"{linked[i].Name}: the transparent micro-triangle let rays hit\n{RayScene.Map(map)}");
            else Assert.True(map.SequenceEqual(maps[0]), $"{linked[i].Name}: the opaque micro-triangle answered differently from the triangle without a micromap\n{RayScene.Map(map)}\nexpected\n{RayScene.Map(maps[0])}");
        }
    }

    /// <summary>
    /// The micromaps of both micromap tests, built: "bound" (unbound, then bound at offset 0),
    /// "placed" (placed after it in the same allocation), "CLONE copy" of the bound one and
    /// "COMPACT copy" of the placed one. With <c>measureSizes</c>, the compacted size of each, and
    /// the COMPACT copy is made into the placed one's compacted size; without, into a micromap of
    /// the full size.
    /// </summary>
    private sealed class MicromapSet
    {
        public MicromapSet(RayScene scene, Backend backend, bool measureSizes)
        {
            var device = scene.Device;
            var rt = scene.RayTracing;
            var desc = RayScene.MicromapDesc(MicromapBits.PREFER_FAST_TRACE | MicromapBits.ALLOW_COMPACTION);
            Inputs = scene.MicromapInputs();

            var bound = scene.Own(rt.CreateMicromap(desc));
            Requirement = bound.GetMemoryDesc(MemoryLocation.DEVICE);
            Record($"{backend}: unbound micromap: {Requirement.size} B, alignment {Requirement.alignment}, type 0x{Requirement.type:X}, dedicated {Requirement.mustBeDedicated != 0}");
            Assert.True(Requirement.size > 0, "the micromap needs no memory");
            if (device.Desc.features.getMemoryDesc2 != 0)
            {
                var predicted = rt.GetMicromapMemoryDesc(desc, MemoryLocation.DEVICE);
                Record($"{backend}: GetMicromapMemoryDesc2 predicted {predicted.size} B, alignment {predicted.alignment}, type 0x{predicted.type:X}");
                Assert.Equal(Requirement.size, predicted.size);
                Assert.Equal(Requirement.type, predicted.type);
            }

            // Both in one allocation, the placed one at the first aligned offset after the bound one;
            // two allocations where NRI asks for a dedicated one.
            var dedicated = Requirement.mustBeDedicated != 0;
            var placedOffset = dedicated ? 0 : Align(Requirement.size, Math.Max(1u, Requirement.alignment));
            var memory = scene.OwnBeneath(device.AllocateMemory(new AllocateMemoryDesc { size = dedicated ? Requirement.size : placedOffset + Requirement.size, type = Requirement.type }));
            var placedMemory = dedicated ? scene.OwnBeneath(device.AllocateMemory(new AllocateMemoryDesc { size = Requirement.size, type = Requirement.type })) : memory;
            rt.BindMicromapMemory([(bound, memory, 0UL)]);
            Placed = scene.Own(rt.CreatePlacedMicromap(placedMemory, placedOffset, desc));
            Record($"{backend}: placed micromap at offset {placedOffset}");

            var boundScratch = scene.Scratch(bound.BuildScratchBufferSize);
            var placedScratch = scene.Scratch(Placed.BuildScratchBufferSize);
            void Built(NriCommandBuffer cmd, params NriMicromap[] micromaps) =>
                cmd.Barrier([], [.. micromaps.Select(m => new NriBufferBarrier(m.Buffer, RayScene.MicromapWritten, RayScene.MicromapRead))], []);

            NriQueryPool? sizes = null;
            if (measureSizes)
            {
                try
                {
                    sizes = scene.Own(device.CreateQueryPool(QueryType.MICROMAP_COMPACTED_SIZE, 4));
                }
                catch (NriException e) when (backend.Api == GraphicsAPI.VK && e.Result == Result.FAILURE && e.Message.Contains("vkCreateQueryPool(): failed, result = 0xFFFFFFF3", StringComparison.Ordinal))
                {
                    SizesRefused = $"VK: tiers.rayTracing = 3, but the driver refuses a MICROMAP_COMPACTED_SIZE query pool with VK_ERROR_UNKNOWN: {e.Message}";
                    All = [];
                    CompactedSizes = [];
                    return;
                }
            }
            var sizeReadback = scene.Own(device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = 32 * 4 }));

            Run(device, cmd =>
            {
                rt.BuildMicromaps(cmd, [new NriBuildMicromap(bound, Inputs.Data, 0, Inputs.Triangles, 0, boundScratch), new NriBuildMicromap(Placed, Inputs.Data, 0, Inputs.Triangles, 0, placedScratch)]);
                Built(cmd, bound, Placed);
                if (sizes is null) return;
                cmd.ResetQueries(sizes, 0, 4);
                rt.WriteMicromapSizes(cmd, [bound, Placed], sizes, 0);
                cmd.CopyQueries(sizes, 0, 2, sizeReadback, 0);
            });
            var placedCompacted = sizes is null ? 0 : ReadSize(sizeReadback, sizes, 1);

            var clone = scene.Own(rt.CreateCommittedMicromap(MemoryLocation.DEVICE, desc));
            var compact = scene.Own(rt.CreateCommittedMicromap(MemoryLocation.DEVICE, RayScene.MicromapDesc(desc.Flags, optimizedSize: placedCompacted)));
            Run(device, cmd =>
            {
                rt.CopyMicromap(cmd, clone, bound, CopyMode.CLONE);
                rt.CopyMicromap(cmd, compact, Placed, CopyMode.COMPACT);
                Built(cmd, clone, compact);
                if (sizes is null) return;
                rt.WriteMicromapSizes(cmd, [clone, compact], sizes, 2);
                cmd.CopyQueries(sizes, 2, 2, sizeReadback, 2 * sizes.QuerySize);
            });

            All = [("bound", bound), ("placed", Placed), ("CLONE copy", clone), ("COMPACT copy", compact)];
            CompactedSizes = sizes is null ? [] : [.. Enumerable.Range(0, 4).Select(i => (All[i].Name, ReadSize(sizeReadback, sizes, i)))];
        }

        public (NriBuffer Data, NriBuffer Triangles, NriBuffer Indices) Inputs { get; }
        public MemoryDesc Requirement { get; }
        public NriMicromap Placed { get; } = null!;
        public string? SizesRefused { get; }
        public (string Name, NriMicromap Micromap)[] All { get; }
        public (string Name, ulong Size)[] CompactedSizes { get; }

        private static ulong ReadSize(NriBuffer readback, NriQueryPool pool, int index)
        {
            var size = MemoryMarshal.Read<ulong>(readback.Map()[(index * (int)pool.QuerySize)..]);
            readback.Unmap();
            return size;
        }
    }
}
