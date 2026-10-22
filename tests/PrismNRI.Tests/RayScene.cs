// The scene of RayTracingTests, shared by the ray tracing tests that build on it: one triangle on
// z = 1 covering the middle of a 16x16 field of rays cast from z = 0 towards +z. Holds the vertex
// buffer, a pipeline layout both the ray query (compute) and the ray tracing pipeline (raygen) bind
// with, the RayQuery.cs pipeline, a descriptor pool, and everything the test creates through it,
// disposed in reverse order of creation.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PrismNRI.Native;
using Xunit;

namespace PrismNRI.Tests;

internal sealed class RayScene : IDisposable
{
    public const int Width = 16;
    public const ulong OutputSize = Width * Width * 4;

    private readonly List<IDisposable> _owned = [];
    private readonly NriPipeline? _rayQuery;

    public RayScene(NriDevice device, int outputNum)
    {
        Device = device;
        float[] vertices = [-0.5f, -0.5f, 1f, 0.5f, -0.5f, 1f, 0f, 0.5f, 1f];
        Vertices = Own(device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 36, usage = BufferUsageBits.ACCELERATION_STRUCTURE_BUILD_INPUT }));
        MemoryMarshal.AsBytes(vertices.AsSpan()).CopyTo(Vertices.Map());
        Vertices.Unmap();

        // Set 0 = { t0: TLAS, u1: one uint per ray }, visible to the ray query and to raygen.
        const StageBits stages = StageBits.COMPUTE_SHADER | StageBits.RAYGEN_SHADER;
        Layout = Own(device.CreatePipelineLayout(new NriPipelineLayoutDesc
        {
            DescriptorSets =
            [
                new NriDescriptorSetDesc(0,
                [
                    new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 1, descriptorType = DescriptorType.ACCELERATION_STRUCTURE, shaderStages = stages },
                    new DescriptorRangeDesc { baseRegisterIndex = 1, descriptorNum = 1, descriptorType = DescriptorType.STORAGE_STRUCTURED_BUFFER, shaderStages = stages },
                ]),
            ],
            ShaderStages = stages,
        }));
        Pool = Own(device.CreateDescriptorPool(new DescriptorPoolDesc
        {
            descriptorSetMaxNum = (uint)outputNum,
            accelerationStructureMaxNum = (uint)outputNum,
            storageStructuredBufferMaxNum = (uint)outputNum,
        }));
        Readback = Own(device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = OutputSize * (ulong)outputNum }));

        // Ray query needs DXR 1.1 / tiers.rayTracing >= 2.
        if (device.Desc.tiers.rayTracing >= 2)
        {
            _rayQuery = Own(device.CreateComputePipeline(new NriComputePipelineDesc
            {
                PipelineLayout = Layout,
                Shader = new NriShaderDesc(StageBits.COMPUTE_SHADER, Shader(device, "RayQuery.cs"), "csMain"),
            }));
        }
    }

    public NriDevice Device { get; }
    public NriRayTracingExtension RayTracing => Device.RayTracing;
    public NriBuffer Vertices { get; }
    public NriPipelineLayout Layout { get; }
    public NriDescriptorPool Pool { get; }
    public NriBuffer Readback { get; }
    public List<NriBuffer> Outputs { get; } = [];

    /// <summary>The scene's bytecode for this backend: <c>Shaders/&lt;name&gt;.spv</c> or <c>.dxil</c>.</summary>
    public static byte[] Shader(NriDevice device, string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", name + (device.GraphicsApi == GraphicsAPI.VK ? ".spv" : ".dxil")));

    public static NriBottomLevelTriangles Triangles(NriBuffer vertexBuffer, NriBottomLevelTrianglesMicromap? micromap = null) =>
        new(vertexBuffer, 0, 3, 12, Format.RGB32_SFLOAT, Micromap: micromap);

    /// <summary>The BLAS desc: one triangle, buffers named at build time.</summary>
    public static NriAccelerationStructureDesc BlasDesc(AccelerationStructureBits flags = AccelerationStructureBits.PREFER_FAST_TRACE,
        BottomLevelGeometryBits geometryFlags = BottomLevelGeometryBits.OPAQUE_GEOMETRY, NriBottomLevelTrianglesMicromap? micromap = null, ulong optimizedSize = 0) => new()
    {
        Type = AccelerationStructureType.BOTTOM_LEVEL,
        Geometries = [NriBottomLevelGeometry.FromTriangles(Triangles(NriBuffer.HasBuffer, micromap), geometryFlags)],
        Flags = flags,
        OptimizedSize = optimizedSize,
    };

    public static NriAccelerationStructureDesc TlasDesc => new()
    {
        Type = AccelerationStructureType.TOP_LEVEL,
        InstanceNum = 1,
        Flags = AccelerationStructureBits.PREFER_FAST_TRACE,
    };

    public NriBottomLevelGeometry Geometry(BottomLevelGeometryBits geometryFlags = BottomLevelGeometryBits.OPAQUE_GEOMETRY, NriBottomLevelTrianglesMicromap? micromap = null) =>
        NriBottomLevelGeometry.FromTriangles(Triangles(Vertices, micromap), geometryFlags);

    public T Own<T>(T disposable) where T : IDisposable
    {
        _owned.Add(disposable);
        return disposable;
    }

    /// <summary>Owned and disposed after everything else: memory that resources are bound into.</summary>
    public T OwnBeneath<T>(T disposable) where T : IDisposable
    {
        _owned.Insert(0, disposable);
        return disposable;
    }

    public NriBuffer Scratch(ulong size) =>
        Own(Device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = Math.Max(size, 1), usage = BufferUsageBits.SCRATCH }));

    /// <summary>A committed TLAS over one identity instance of <paramref name="blas"/>, and its build inputs.</summary>
    public (NriAccelerationStructure Tlas, NriBuffer Instances, NriBuffer Scratch) Tlas(NriAccelerationStructure blas) =>
        TlasInputs(Own(RayTracing.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, TlasDesc)), blas);

    /// <summary>The build inputs of <paramref name="tlas"/> (created by the caller) over one identity instance of <paramref name="blas"/>.</summary>
    public (NriAccelerationStructure Tlas, NriBuffer Instances, NriBuffer Scratch) TlasInputs(NriAccelerationStructure tlas, NriAccelerationStructure blas)
    {
        var instance = new TopLevelInstance { mask = 0xFF, accelerationStructureHandle = blas.GpuHandle };
        instance.transform[0] = 1; instance.transform[5] = 1; instance.transform[10] = 1;
        var instances = Own(Device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = (ulong)Unsafe.SizeOf<TopLevelInstance>(), usage = BufferUsageBits.ACCELERATION_STRUCTURE_BUILD_INPUT }));
        MemoryMarshal.AsBytes(new ReadOnlySpan<TopLevelInstance>(in instance)).CopyTo(instances.Map());
        instances.Unmap();
        return (tlas, instances, Scratch(tlas.BuildScratchBufferSize));
    }

    public static AccessStage AsWrite => new() { access = AccessBits.ACCELERATION_STRUCTURE_WRITE, stages = StageBits.ACCELERATION_STRUCTURE };
    public static AccessStage AsRead => new() { access = AccessBits.ACCELERATION_STRUCTURE_READ, stages = StageBits.ACCELERATION_STRUCTURE | StageBits.COMPUTE_SHADER | StageBits.RAYGEN_SHADER };

    public void BuildBlas(NriCommandBuffer cmd, NriAccelerationStructure blas, NriBuffer scratch, NriBottomLevelGeometry? geometry = null, NriAccelerationStructure? src = null)
    {
        RayTracing.BuildBottomLevelAccelerationStructure(cmd, new NriBuildBottomLevelAccelerationStructure(blas, [geometry ?? Geometry()], scratch, 0, src));
        cmd.Barrier(new NriBufferBarrier(blas.Buffer, AsWrite, AsRead));
    }

    public void BuildTlas(NriCommandBuffer cmd, (NriAccelerationStructure Tlas, NriBuffer Instances, NriBuffer Scratch) tlas)
    {
        RayTracing.BuildTopLevelAccelerationStructure(cmd, new NriBuildTopLevelAccelerationStructure(tlas.Tlas, 1, tlas.Instances, 0, tlas.Scratch));
        cmd.Barrier(new NriBufferBarrier(tlas.Tlas.Buffer, AsWrite, AsRead));
    }

    /// <summary>A new output buffer and a descriptor set naming it and <paramref name="tlas"/>.</summary>
    public NriDescriptorSet Output(NriAccelerationStructure tlas)
    {
        var output = Own(Device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = OutputSize, structureStride = 4, usage = BufferUsageBits.SHADER_RESOURCE_STORAGE }));
        var view = Own(output.CreateView(BufferView.STORAGE_STRUCTURED_BUFFER));
        var tlasView = Own(tlas.CreateDescriptor());
        var set = Pool.AllocateDescriptorSet(Layout, 0);
        set.Update(0, 0, tlasView);
        set.Update(1, 0, view);
        Outputs.Add(output);
        return set;
    }

    /// <summary>Records the RayQuery.cs dispatch: 0 for a miss, 1 for a hit, per ray.</summary>
    public void TraceInline(NriCommandBuffer cmd, NriDescriptorSet set)
    {
        Assert.NotNull(_rayQuery);
        TraceInline(cmd, set, _rayQuery);
    }

    /// <summary>Records a dispatch of <paramref name="rayQuery"/>, a compute pipeline on <see cref="Layout"/> with 8x8 threads per group.</summary>
    public void TraceInline(NriCommandBuffer cmd, NriDescriptorSet set, NriPipeline rayQuery)
    {
        cmd.SetPipelineLayout(BindPoint.COMPUTE, Layout);
        cmd.SetDescriptorSet(0, set, BindPoint.COMPUTE);
        cmd.SetPipeline(rayQuery);
        cmd.Dispatch(Width / 8, Width / 8);
    }

    /// <summary>The ray of thread (x, y) starts at (u, v, 0): the field [-1, 1]^2 sampled at 16x16 cell centres.</summary>
    public static (float U, float V) RayOrigin(int x, int y) => ((x + 0.5f) / Width * 2 - 1, (y + 0.5f) / Width * 2 - 1);

    // ---------------------------------------------------------------- devices and micromaps

    /// <summary>
    /// A validated device with ray queries (tiers.rayTracing 2 or more), or a skip naming the tier.
    /// </summary>
    public static NriDevice CreateDevice(Backend backend, List<NriMessage> messages, Action<NriDeviceOptionsBuilder>? configure = null)
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

    /// <summary>
    /// The two-state micromap of the micromap tests: two micro-triangles at subdivision level 0,
    /// index 0 transparent, index 1 opaque.
    /// </summary>
    public static NriMicromapDesc MicromapDesc(MicromapBits flags = MicromapBits.PREFER_FAST_TRACE, ulong optimizedSize = 0) => new()
    {
        Usages = [new MicromapUsageDesc { triangleNum = 2, subdivisionLevel = 0, format = MicromapFormat.OPACITY_2_STATE }],
        Flags = flags,
        OptimizedSize = optimizedSize,
    };

    /// <summary>
    /// The build inputs of <see cref="MicromapDesc"/>: the opacity bits (one byte per micro-triangle,
    /// 0 transparent, 1 opaque), the two <c>MicromapTriangle</c> entries, and a micromap index buffer
    /// holding 0 then 1, so offset 0 maps the triangle to the transparent entry and offset 4 to the opaque one.
    /// </summary>
    public (NriBuffer Data, NriBuffer Triangles, NriBuffer Indices) MicromapInputs()
    {
        var data = Own(Device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 2, usage = BufferUsageBits.MICROMAP_BUILD_INPUT }));
        new byte[] { 0, 1 }.CopyTo(data.Map());
        data.Unmap();
        MicromapTriangle[] triangles =
        [
            new() { dataOffset = 0, subdivisionLevel = 0, format = MicromapFormat.OPACITY_2_STATE },
            new() { dataOffset = 1, subdivisionLevel = 0, format = MicromapFormat.OPACITY_2_STATE },
        ];
        var triangleBuffer = Own(Device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = (ulong)(triangles.Length * Marshal.SizeOf<MicromapTriangle>()), usage = BufferUsageBits.MICROMAP_BUILD_INPUT }));
        MemoryMarshal.AsBytes(triangles.AsSpan()).CopyTo(triangleBuffer.Map());
        triangleBuffer.Unmap();
        var indices = Own(Device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 8, usage = BufferUsageBits.ACCELERATION_STRUCTURE_BUILD_INPUT }));
        MemoryMarshal.AsBytes<uint>([0, 1]).CopyTo(indices.Map());
        indices.Unmap();
        return (data, triangleBuffer, indices);
    }

    /// <summary>
    /// The before side of a barrier after a micromap build or copy. The stages are ALL, not
    /// MICROMAP: for a MICROMAP (or ACCELERATION_STRUCTURE) stage side NRI's D3D12 backend adds
    /// D3D12_BARRIER_SYNC_EMIT_RAYTRACING_ACCELERATION_STRUCTURE_POSTBUILD_INFO unless the access holds
    /// ACCELERATION_STRUCTURE_WRITE (GetBarrierSyncFlags, CommandBufferD3D12.hpp:142), while it maps
    /// MICROMAP_WRITE to D3D12_BARRIER_ACCESS_RAYTRACING_ACCELERATION_STRUCTURE_WRITE (:184). The
    /// debug layer refuses that pair ("[1331] ID3D12CommandList::Barrier: SyncBefore bits
    /// D3D12_BARRIER_SYNC_EMIT_RAYTRACING_ACCELERATION_STRUCTURE_POSTBUILD_INFO are incompatible with
    /// AccessBefore bits D3D12_BARRIER_ACCESS_RAYTRACING_ACCELERATION_STRUCTURE_WRITE in buffer
    /// barrier ..."), and Close fails with E_INVALIDARG, with or without the layer (measured).
    /// RayTracingPipelineTests.An_opacity_micromap_decides_whether_the_ray_query_hits keeps the
    /// MICROMAP spelling and skips on that failure.
    /// </summary>
    public static AccessStage MicromapWritten => new() { access = AccessBits.MICROMAP_WRITE, stages = StageBits.ALL };
    public static AccessStage MicromapRead => new() { access = AccessBits.MICROMAP_READ, stages = StageBits.MICROMAP | StageBits.ACCELERATION_STRUCTURE };

    /// <summary>Copies every output into <see cref="Readback"/>, in creation order.</summary>
    public void CopyOutputs(NriCommandBuffer cmd)
    {
        var storage = new AccessStage { access = AccessBits.SHADER_RESOURCE_STORAGE, stages = StageBits.COMPUTE_SHADER | StageBits.RAYGEN_SHADER };
        for (var i = 0; i < Outputs.Count; i++)
        {
            cmd.Barrier(new NriBufferBarrier(Outputs[i], storage, GpuWork.CopySource));
            cmd.CopyBuffer(Readback, (ulong)i * OutputSize, Outputs[i], 0, OutputSize);
        }
    }

    /// <summary>The outputs as read back, one array per output.</summary>
    public uint[][] Read()
    {
        var all = MemoryMarshal.Cast<byte, uint>(Readback.Map()).ToArray();
        Readback.Unmap();
        return [.. Enumerable.Range(0, Outputs.Count).Select(i => all.AsSpan(i * Width * Width, Width * Width).ToArray())];
    }

    public static string Map(uint[] values) => string.Join("\n", Enumerable.Range(0, Width).Select(y =>
        string.Concat(Enumerable.Range(0, Width).Select(x => values[y * Width + x] switch { 0 => '.', 1 => '#', 2 => '@', _ => '?' }))));

    /// <summary>
    /// The triangle's footprint: <paramref name="hit"/> at the centre and inside, <paramref name="miss"/>
    /// in the corners, 30 to 130 hits. A wrong map fails on every adapter, software included; the
    /// map is in the log and in the failure message (CLAUDE.md: CI runners and flakes, item 1).
    /// </summary>
    public void ExpectTriangle(uint[] values, uint hit, uint miss, string what)
    {
        var hitCount = values.Count(v => v == hit);
        var map = Map(values);
        GpuWork.Record($"{Device.GraphicsApi} {what}: {hitCount} hits\n{map}");
        void Expect(uint value, int x, int y)
        {
            var got = values[y * Width + x];
            if (got == value) return;
            Assert.Fail($"{Device.GraphicsApi} {what}: ({x},{y}) expected {value}, got {got}; {hitCount} hits of {Width * Width}\n{map}");
        }
        Expect(hit, 8, 8);
        Expect(hit, 8, 6);
        Expect(miss, 0, 0);
        Expect(miss, Width - 1, 0);
        Expect(miss, 0, Width - 1);
        Expect(miss, Width - 1, Width - 1);
        Assert.All(values, v => Assert.True(v == hit || v == miss, $"{what}: value {v} is neither hit {hit} nor miss {miss}\n{map}"));
        Assert.True(hitCount is >= 30 and <= 130, $"{Device.GraphicsApi} {what}: {hitCount} hits of {Width * Width}, expected 30 to 130\n{map}");
    }

    public void Dispose()
    {
        for (var i = _owned.Count - 1; i >= 0; i--) _owned[i].Dispose();
        _owned.Clear();
    }
}
