// L3 — the RayTracing extension.
//
// Acceleration structures and micromaps are NRI objects destroyed through this table, so their
// wrappers are NriObjects that hold the extension. The descs that name buffers (geometries,
// builds, dispatch) have managed forms with the same field names; everything leaf-level
// (TopLevelInstance, TransformMatrix, BottomLevelAabb, ShaderGroupDesc, MicromapUsageDesc,
// MicromapTriangle) is the generated struct, used as is: those are the data layouts the GPU reads.

using System.Runtime.InteropServices;
using SharpNRI.Interop;
using SharpNRI.Native;
using Buffer = SharpNRI.Native.Buffer;

namespace SharpNRI;

/// <summary><c>BottomLevelTrianglesDesc</c> naming the wrappers.</summary>
/// <remarks>
/// For an <see cref="NriAccelerationStructureDesc"/>, NRI needs only to know whether a buffer is
/// present; pass <see cref="NriBuffer.HasBuffer"/> there and the real buffers at build time.
/// </remarks>
public readonly record struct NriBottomLevelTriangles(
    NriBuffer VertexBuffer,
    ulong VertexOffset,
    uint VertexNum,
    ushort VertexStride,
    Format VertexFormat,
    NriBuffer? IndexBuffer = null,
    ulong IndexOffset = 0,
    uint IndexNum = 0,
    IndexType IndexType = IndexType.UINT32,
    NriBuffer? TransformBuffer = null,
    ulong TransformOffset = 0,
    NriBottomLevelTrianglesMicromap? Micromap = null);

/// <summary><c>BottomLevelTrianglesMicromapDesc</c> naming the wrappers.</summary>
public readonly record struct NriBottomLevelTrianglesMicromap(
    NriMicromap? Micromap,
    NriBuffer? IndexBuffer = null,
    ulong IndexOffset = 0,
    uint BaseTriangle = 0,
    IndexType IndexType = IndexType.UINT32);

/// <summary><c>BottomLevelAabbsDesc</c> naming the wrapper. The buffer holds <c>BottomLevelAabb</c> entries.</summary>
public readonly record struct NriBottomLevelAabbs(NriBuffer Buffer, ulong Offset, uint Num, uint Stride);

/// <summary><c>BottomLevelGeometryDesc</c>: triangles or AABBs, with flags.</summary>
public readonly struct NriBottomLevelGeometry
{
    public BottomLevelGeometryBits Flags { get; init; }
    public BottomLevelGeometryType Type { get; private init; }
    public NriBottomLevelTriangles Triangles { get; private init; }
    public NriBottomLevelAabbs Aabbs { get; private init; }

    public static NriBottomLevelGeometry FromTriangles(in NriBottomLevelTriangles triangles, BottomLevelGeometryBits flags = BottomLevelGeometryBits.NONE) =>
        new() { Type = BottomLevelGeometryType.TRIANGLES, Triangles = triangles, Flags = flags };

    public static NriBottomLevelGeometry FromAabbs(in NriBottomLevelAabbs aabbs, BottomLevelGeometryBits flags = BottomLevelGeometryBits.NONE) =>
        new() { Type = BottomLevelGeometryType.AABBS, Aabbs = aabbs, Flags = flags };
}

/// <summary><c>AccelerationStructureDesc</c> in managed form.</summary>
public sealed class NriAccelerationStructureDesc
{
    public required AccelerationStructureType Type { get; init; }

    /// <summary>Bottom level: the geometries (buffers may be <see cref="NriBuffer.HasBuffer"/>). Top level: unused.</summary>
    public NriBottomLevelGeometry[] Geometries { get; init; } = [];

    /// <summary>Top level: the instance count. Bottom level: ignored, the geometry count is used.</summary>
    public uint InstanceNum { get; init; }

    public AccelerationStructureBits Flags { get; init; }

    /// <summary>From <c>CmdWriteAccelerationStructureSizes</c>, for a compacted copy.</summary>
    public ulong OptimizedSize { get; init; }
}

/// <summary><c>MicromapDesc</c> in managed form.</summary>
public sealed class NriMicromapDesc
{
    public required MicromapUsageDesc[] Usages { get; init; }
    public MicromapBits Flags { get; init; }
    public ulong OptimizedSize { get; init; }
}

/// <summary><c>RayTracingPipelineDesc</c> in managed form.</summary>
public sealed class NriRayTracingPipelineDesc
{
    public required NriPipelineLayout PipelineLayout { get; init; }

    /// <summary>The shader library; <see cref="ShaderGroups"/> index into it starting from 1.</summary>
    public required NriShaderDesc[] Shaders { get; init; }

    public required ShaderGroupDesc[] ShaderGroups { get; init; }
    public uint RecursionMaxDepth { get; init; } = 1;
    public uint RayPayloadMaxSize { get; init; }
    public uint RayHitAttributeMaxSize { get; init; }
    public RayTracingPipelineBits Flags { get; init; }
    public Robustness Robustness { get; init; }
    public NriPipelineCache? Cache { get; init; }
}

/// <summary><c>BuildTopLevelAccelerationStructureDesc</c> naming the wrappers.</summary>
public readonly record struct NriBuildTopLevelAccelerationStructure(
    NriAccelerationStructure Dst,
    uint InstanceNum,
    NriBuffer InstanceBuffer,
    ulong InstanceOffset,
    NriBuffer ScratchBuffer,
    ulong ScratchOffset = 0,
    NriAccelerationStructure? Src = null);

/// <summary><c>BuildBottomLevelAccelerationStructureDesc</c> naming the wrappers.</summary>
public readonly record struct NriBuildBottomLevelAccelerationStructure(
    NriAccelerationStructure Dst,
    NriBottomLevelGeometry[] Geometries,
    NriBuffer ScratchBuffer,
    ulong ScratchOffset = 0,
    NriAccelerationStructure? Src = null);

/// <summary><c>BuildMicromapDesc</c> naming the wrappers.</summary>
public readonly record struct NriBuildMicromap(
    NriMicromap Dst,
    NriBuffer DataBuffer,
    ulong DataOffset,
    NriBuffer TriangleBuffer,
    ulong TriangleOffset,
    NriBuffer ScratchBuffer,
    ulong ScratchOffset = 0);

/// <summary><c>StridedBufferRegion</c> naming the wrapper. A null buffer is an absent table.</summary>
public readonly record struct NriStridedBufferRegion(NriBuffer? Buffer, ulong Offset = 0, ulong Size = 0, ulong Stride = 0)
{
    internal unsafe StridedBufferRegion ToNative() => new() { buffer = Buffer is null ? null : Buffer.Raw, offset = Offset, size = Size, stride = Stride };
}

/// <summary><c>DispatchRaysDesc</c> naming the wrappers.</summary>
public readonly record struct NriDispatchRays(
    NriStridedBufferRegion RaygenShaderRecord,
    NriStridedBufferRegion MissShaderBindingTable,
    NriStridedBufferRegion HitShaderBindingTable,
    NriStridedBufferRegion CallableShaderBindingTable,
    uint Width,
    uint Height = 1,
    uint Depth = 1);

/// <summary>A bottom- or top-level acceleration structure.</summary>
public sealed unsafe class NriAccelerationStructure : NriObject
{
    private NriMemory.Holders? _memory; // of the memory bound into or placed in through the binding, which refuses disposal while set
    private NriBuffer? _buffer; // the backing buffer's wrapper, made on first use, so a barrier per frame allocates nothing

    internal NriAccelerationStructure(NriRayTracingExtension extension, AccelerationStructure* handle, AccelerationStructureType type) : base(handle, "NriAccelerationStructure")
    {
        Extension = extension;
        Type = type;
        extension.Device.ChildCreated();
    }

    public NriRayTracingExtension Extension { get; }
    public override NriDevice Device => Extension.Device;
    public AccelerationStructureType Type { get; }

    internal AccelerationStructure* Raw => (AccelerationStructure*)Handle;

    /// <summary>For <c>TopLevelInstance.accelerationStructureHandle</c>.</summary>
    public ulong GpuHandle => Extension.Table.GetAccelerationStructureHandle(Raw);

    public ulong BuildScratchBufferSize => Extension.Table.GetAccelerationStructureBuildScratchBufferSize(Raw);
    public ulong UpdateScratchBufferSize => Extension.Table.GetAccelerationStructureUpdateScratchBufferSize(Raw);

    /// <summary>The buffer backing the structure, for barriers. Owned by the structure; do not dispose.</summary>
    public NriBuffer Buffer => NonOwned(ref _buffer, Device, Extension.Table.GetAccelerationStructureBuffer(Raw));

    public MemoryDesc GetMemoryDesc(MemoryLocation location)
    {
        MemoryDesc desc;
        Extension.Table.GetAccelerationStructureMemoryDesc(Raw, location, &desc);
        return desc;
    }

    /// <summary>A descriptor for binding as <c>ACCELERATION_STRUCTURE</c>.</summary>
    public NriDescriptor CreateDescriptor()
    {
        Descriptor* descriptor = null;
        NriException.Check(Extension.Table.CreateAccelerationStructureDescriptor(Raw, &descriptor), "CreateAccelerationStructureDescriptor");
        return new NriDescriptor(Device, descriptor, NriDescriptorKind.TextureView);
    }

    /// <summary><c>ID3D12Resource*</c> or <c>VkAccelerationStructureKHR</c>. Last resort.</summary>
    public ulong GetNativeObject() => Extension.Table.GetAccelerationStructureNativeObject(Raw);

    /// <summary>Bound into or placed in <paramref name="memory"/>, which then outlives this structure.</summary>
    internal void BoundTo(NriMemory memory) => NriMemory.Track(ref _memory, memory);

    /// <summary>The cached non-owning wrapper of <paramref name="buffer"/>, made again only if NRI hands out another buffer.</summary>
    internal static NriBuffer NonOwned(ref NriBuffer? cache, NriDevice device, Buffer* buffer)
    {
        var cached = Volatile.Read(ref cache);
        if (cached is not null && cached.Raw == buffer) return cached;
        var wrapped = new NriBuffer(device, buffer, owned: false);
        Volatile.Write(ref cache, wrapped);
        return wrapped;
    }

    private protected override void Destroy(void* handle) => Extension.Table.DestroyAccelerationStructure((AccelerationStructure*)handle);

    private protected override void OnDestroyed()
    {
        NriMemory.Release(ref _memory);
        _buffer?.Dispose(); // the buffer is gone with the object; its wrapper says so
        base.OnDestroyed();
    }
}

/// <summary>An opacity micromap.</summary>
public sealed unsafe class NriMicromap : NriObject
{
    private NriMemory.Holders? _memory; // of the memory bound into or placed in through the binding, which refuses disposal while set
    private NriBuffer? _buffer; // the backing buffer's wrapper, made on first use, so a barrier per frame allocates nothing

    internal NriMicromap(NriRayTracingExtension extension, Micromap* handle) : base(handle, "NriMicromap")
    {
        Extension = extension;
        extension.Device.ChildCreated();
    }

    public NriRayTracingExtension Extension { get; }
    public override NriDevice Device => Extension.Device;

    internal Micromap* Raw => (Micromap*)Handle;

    public ulong BuildScratchBufferSize => Extension.Table.GetMicromapBuildScratchBufferSize(Raw);

    /// <summary>The buffer backing the micromap, for barriers. Owned by the micromap; do not dispose.</summary>
    public NriBuffer Buffer => NriAccelerationStructure.NonOwned(ref _buffer, Device, Extension.Table.GetMicromapBuffer(Raw));

    public MemoryDesc GetMemoryDesc(MemoryLocation location)
    {
        MemoryDesc desc;
        Extension.Table.GetMicromapMemoryDesc(Raw, location, &desc);
        return desc;
    }

    public ulong GetNativeObject() => Extension.Table.GetMicromapNativeObject(Raw);

    /// <summary>Bound into or placed in <paramref name="memory"/>, which then outlives this micromap.</summary>
    internal void BoundTo(NriMemory memory) => NriMemory.Track(ref _memory, memory);

    private protected override void Destroy(void* handle) => Extension.Table.DestroyMicromap((Micromap*)handle);

    private protected override void OnDestroyed()
    {
        NriMemory.Release(ref _memory);
        _buffer?.Dispose(); // the buffer is gone with the object; its wrapper says so
        base.OnDestroyed();
    }
}

/// <summary>NRI's <c>RayTracingInterface</c>. Acquire through <see cref="NriDevice.RayTracing"/>; requires <c>features.rayTracing</c>.</summary>
public sealed unsafe class NriRayTracingExtension
{
    internal readonly RayTracingInterface Table;

    internal NriRayTracingExtension(NriDevice device, in RayTracingInterface table)
    {
        Device = device;
        Table = table;
    }

    public NriDevice Device { get; }

    internal static bool TryAcquire(NriDevice device, out NriRayTracingExtension? extension)
    {
        if (InterfaceTable.TryAcquire<RayTracingInterface>(device.Raw, "RayTracingInterface", out var table))
        {
            extension = new NriRayTracingExtension(device, in table);
            return true;
        }
        extension = null;
        return false;
    }

    // ---------------------------------------------------------------- pipeline

    public NriPipeline CreateRayTracingPipeline(NriRayTracingPipelineDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);
        using var shaders = new NriPipeline.ShaderArray(desc.Shaders);

        var library = new ShaderLibraryDesc { shaders = shaders.Pointer, shaderNum = shaders.Count };
        fixed (ShaderGroupDesc* groups = desc.ShaderGroups)
        {
            var native = new RayTracingPipelineDesc
            {
                pipelineLayout = desc.PipelineLayout.Raw,
                shaderLibrary = &library,
                shaderGroups = groups,
                shaderGroupNum = (uint)desc.ShaderGroups.Length,
                recursionMaxDepth = desc.RecursionMaxDepth,
                rayPayloadMaxSize = desc.RayPayloadMaxSize,
                rayHitAttributeMaxSize = desc.RayHitAttributeMaxSize,
                flags = desc.Flags,
                robustness = desc.Robustness,
                cache = desc.Cache is null ? null : desc.Cache.Raw,
            };

            Pipeline* handle = null;
            NriException.Check(Table.CreateRayTracingPipeline(Device.Raw, &native, &handle), "CreateRayTracingPipeline");
            return new NriPipeline(Device, handle, BindPoint.RAY_TRACING);
        }
    }

    /// <summary>
    /// Writes shader group identifiers for the shader binding table. <paramref name="dstStride"/>
    /// must be at least <c>shaderStage.rayTracing.shaderGroupIdentifierSize</c>; bytes between
    /// identifiers are preserved.
    /// </summary>
    public void WriteShaderGroupIdentifiers(NriPipeline pipeline, uint baseShaderGroupIndex, uint shaderGroupNum, uint dstStride, Span<byte> dst)
    {
        var needed = (ulong)shaderGroupNum * dstStride; // 64-bit: a 32-bit product wraps and lets a short span through
        if ((ulong)dst.Length < needed)
            throw new ArgumentException($"dst holds {dst.Length} bytes; {shaderGroupNum} groups at stride {dstStride} need {needed}", nameof(dst));
        fixed (byte* p = dst)
        {
            NriException.Check(Table.WriteShaderGroupIdentifiers(pipeline.Raw, baseShaderGroupIndex, shaderGroupNum, dstStride, p), "WriteShaderGroupIdentifiers");
        }
    }

    // ---------------------------------------------------------------- acceleration structures

    /// <summary>Unbound; bind with <see cref="BindAccelerationStructureMemory(NriAccelerationStructure, NriMemory, ulong)"/>.</summary>
    public NriAccelerationStructure CreateAccelerationStructure(NriAccelerationStructureDesc desc)
    {
        using var native = new AccelerationStructureDescNative(desc);
        AccelerationStructure* handle = null;
        NriException.Check(Table.CreateAccelerationStructure(Device.Raw, native.Pointer, &handle), "CreateAccelerationStructure");
        return new NriAccelerationStructure(this, handle, desc.Type);
    }

    public NriAccelerationStructure CreateCommittedAccelerationStructure(MemoryLocation location, NriAccelerationStructureDesc desc, float priority = 0)
    {
        using var native = new AccelerationStructureDescNative(desc);
        AccelerationStructure* handle = null;
        NriException.Check(Table.CreateCommittedAccelerationStructure(Device.Raw, location, priority, native.Pointer, &handle), "CreateCommittedAccelerationStructure");
        return new NriAccelerationStructure(this, handle, desc.Type);
    }

    /// <summary>A structure placed into <paramref name="memory"/> at an offset; the memory refuses disposal until the structure is disposed.</summary>
    public NriAccelerationStructure CreatePlacedAccelerationStructure(NriMemory memory, ulong offset, NriAccelerationStructureDesc desc)
    {
        using var native = new AccelerationStructureDescNative(desc);
        AccelerationStructure* handle = null;
        NriException.Check(Table.CreatePlacedAccelerationStructure(Device.Raw, memory.Raw, offset, native.Pointer, &handle), "CreatePlacedAccelerationStructure");
        var structure = new NriAccelerationStructure(this, handle, desc.Type);
        structure.BoundTo(memory);
        return structure;
    }

    /// <summary>Requirements for a structure that does not exist yet. Requires <c>features.getMemoryDesc2</c>.</summary>
    public MemoryDesc GetAccelerationStructureMemoryDesc(NriAccelerationStructureDesc desc, MemoryLocation location)
    {
        using var native = new AccelerationStructureDescNative(desc);
        MemoryDesc memory;
        Table.GetAccelerationStructureMemoryDesc2(Device.Raw, native.Pointer, location, &memory);
        return memory;
    }

    /// <summary>Binds each structure into its memory; each memory refuses disposal until its structures are disposed.</summary>
    public void BindAccelerationStructureMemory(ReadOnlySpan<(NriAccelerationStructure Structure, NriMemory Memory, ulong Offset)> bindings)
    {
        var native = stackalloc BindAccelerationStructureMemoryDesc[bindings.Length];
        for (var i = 0; i < bindings.Length; i++)
            native[i] = new BindAccelerationStructureMemoryDesc { accelerationStructure = bindings[i].Structure.Raw, memory = bindings[i].Memory.Raw, offset = bindings[i].Offset };
        NriException.Check(Table.BindAccelerationStructureMemory(native, (uint)bindings.Length), "BindAccelerationStructureMemory");
        foreach (var (structure, memory, _) in bindings) structure.BoundTo(memory);
    }

    public void BindAccelerationStructureMemory(NriAccelerationStructure structure, NriMemory memory, ulong offset = 0) =>
        BindAccelerationStructureMemory([(structure, memory, offset)]);

    // ---------------------------------------------------------------- micromaps

    public NriMicromap CreateMicromap(NriMicromapDesc desc)
    {
        fixed (MicromapUsageDesc* usages = desc.Usages)
        {
            var native = ToNative(desc, usages);
            Micromap* handle = null;
            NriException.Check(Table.CreateMicromap(Device.Raw, &native, &handle), "CreateMicromap");
            return new NriMicromap(this, handle);
        }
    }

    public NriMicromap CreateCommittedMicromap(MemoryLocation location, NriMicromapDesc desc, float priority = 0)
    {
        fixed (MicromapUsageDesc* usages = desc.Usages)
        {
            var native = ToNative(desc, usages);
            Micromap* handle = null;
            NriException.Check(Table.CreateCommittedMicromap(Device.Raw, location, priority, &native, &handle), "CreateCommittedMicromap");
            return new NriMicromap(this, handle);
        }
    }

    /// <summary>A micromap placed into <paramref name="memory"/> at an offset; the memory refuses disposal until the micromap is disposed.</summary>
    public NriMicromap CreatePlacedMicromap(NriMemory memory, ulong offset, NriMicromapDesc desc)
    {
        fixed (MicromapUsageDesc* usages = desc.Usages)
        {
            var native = ToNative(desc, usages);
            Micromap* handle = null;
            NriException.Check(Table.CreatePlacedMicromap(Device.Raw, memory.Raw, offset, &native, &handle), "CreatePlacedMicromap");
            var micromap = new NriMicromap(this, handle);
            micromap.BoundTo(memory);
            return micromap;
        }
    }

    public MemoryDesc GetMicromapMemoryDesc(NriMicromapDesc desc, MemoryLocation location)
    {
        fixed (MicromapUsageDesc* usages = desc.Usages)
        {
            var native = ToNative(desc, usages);
            MemoryDesc memory;
            Table.GetMicromapMemoryDesc2(Device.Raw, &native, location, &memory);
            return memory;
        }
    }

    /// <summary>Binds each micromap into its memory; each memory refuses disposal until its micromaps are disposed.</summary>
    public void BindMicromapMemory(ReadOnlySpan<(NriMicromap Micromap, NriMemory Memory, ulong Offset)> bindings)
    {
        var native = stackalloc BindMicromapMemoryDesc[bindings.Length];
        for (var i = 0; i < bindings.Length; i++)
            native[i] = new BindMicromapMemoryDesc { micromap = bindings[i].Micromap.Raw, memory = bindings[i].Memory.Raw, offset = bindings[i].Offset };
        NriException.Check(Table.BindMicromapMemory(native, (uint)bindings.Length), "BindMicromapMemory");
        foreach (var (micromap, memory, _) in bindings) micromap.BoundTo(memory);
    }

    private static MicromapDesc ToNative(NriMicromapDesc desc, MicromapUsageDesc* usages) => new()
    {
        optimizedSize = desc.OptimizedSize,
        usages = usages,
        usageNum = (uint)desc.Usages.Length,
        flags = desc.Flags,
    };

    // ---------------------------------------------------------------- commands

    public void BuildMicromaps(NriCommandBuffer commandBuffer, ReadOnlySpan<NriBuildMicromap> builds)
    {
        var native = stackalloc BuildMicromapDesc[builds.Length];
        for (var i = 0; i < builds.Length; i++)
        {
            ref readonly var b = ref builds[i];
            native[i] = new BuildMicromapDesc
            {
                dst = b.Dst.Raw,
                dataBuffer = b.DataBuffer.Raw, dataOffset = b.DataOffset,
                triangleBuffer = b.TriangleBuffer.Raw, triangleOffset = b.TriangleOffset,
                scratchBuffer = b.ScratchBuffer.Raw, scratchOffset = b.ScratchOffset,
            };
        }
        Table.CmdBuildMicromaps(commandBuffer.Raw, native, (uint)builds.Length);
    }

    public void WriteMicromapSizes(NriCommandBuffer commandBuffer, ReadOnlySpan<NriMicromap> micromaps, NriQueryPool queryPool, uint queryPoolOffset)
    {
        var raw = stackalloc Micromap*[micromaps.Length];
        for (var i = 0; i < micromaps.Length; i++) raw[i] = micromaps[i].Raw;
        Table.CmdWriteMicromapSizes(commandBuffer.Raw, raw, (uint)micromaps.Length, queryPool.Raw, queryPoolOffset);
    }

    public void CopyMicromap(NriCommandBuffer commandBuffer, NriMicromap dst, NriMicromap src, CopyMode mode) =>
        Table.CmdCopyMicromap(commandBuffer.Raw, dst.Raw, src.Raw, mode);

    public void BuildTopLevelAccelerationStructures(NriCommandBuffer commandBuffer, ReadOnlySpan<NriBuildTopLevelAccelerationStructure> builds)
    {
        var native = stackalloc BuildTopLevelAccelerationStructureDesc[builds.Length];
        for (var i = 0; i < builds.Length; i++)
        {
            ref readonly var b = ref builds[i];
            native[i] = new BuildTopLevelAccelerationStructureDesc
            {
                dst = b.Dst.Raw,
                src = b.Src is null ? null : b.Src.Raw,
                instanceNum = b.InstanceNum,
                instanceBuffer = b.InstanceBuffer.Raw, instanceOffset = b.InstanceOffset,
                scratchBuffer = b.ScratchBuffer.Raw, scratchOffset = b.ScratchOffset,
            };
        }
        Table.CmdBuildTopLevelAccelerationStructures(commandBuffer.Raw, native, (uint)builds.Length);
    }

    public void BuildTopLevelAccelerationStructure(NriCommandBuffer commandBuffer, in NriBuildTopLevelAccelerationStructure build) =>
        BuildTopLevelAccelerationStructures(commandBuffer, new ReadOnlySpan<NriBuildTopLevelAccelerationStructure>(in build));

    /// <summary>
    /// Builds bottom-level structures. The geometry arrays are marshalled on the stack per call;
    /// keep the total geometry count per call modest (hundreds, not tens of thousands).
    /// </summary>
    public void BuildBottomLevelAccelerationStructures(NriCommandBuffer commandBuffer, ReadOnlySpan<NriBuildBottomLevelAccelerationStructure> builds)
    {
        var total = 0;
        foreach (ref readonly var b in builds) total += b.Geometries.Length;

        var geometries = stackalloc BottomLevelGeometryDesc[total];
        var micromaps = stackalloc BottomLevelTrianglesMicromapDesc[total];
        var native = stackalloc BuildBottomLevelAccelerationStructureDesc[builds.Length];

        var next = 0;
        for (var i = 0; i < builds.Length; i++)
        {
            ref readonly var b = ref builds[i];
            var first = next;
            foreach (ref readonly var g in b.Geometries.AsSpan())
            {
                geometries[next] = ToNative(in g, &micromaps[next], forCreation: false);
                next++;
            }
            native[i] = new BuildBottomLevelAccelerationStructureDesc
            {
                dst = b.Dst.Raw,
                src = b.Src is null ? null : b.Src.Raw,
                geometries = geometries + first,
                geometryNum = (uint)b.Geometries.Length,
                scratchBuffer = b.ScratchBuffer.Raw, scratchOffset = b.ScratchOffset,
            };
        }
        Table.CmdBuildBottomLevelAccelerationStructures(commandBuffer.Raw, native, (uint)builds.Length);
    }

    public void BuildBottomLevelAccelerationStructure(NriCommandBuffer commandBuffer, in NriBuildBottomLevelAccelerationStructure build) =>
        BuildBottomLevelAccelerationStructures(commandBuffer, new ReadOnlySpan<NriBuildBottomLevelAccelerationStructure>(in build));

    public void WriteAccelerationStructureSizes(NriCommandBuffer commandBuffer, ReadOnlySpan<NriAccelerationStructure> structures, NriQueryPool queryPool, uint queryPoolOffset)
    {
        var raw = stackalloc AccelerationStructure*[structures.Length];
        for (var i = 0; i < structures.Length; i++) raw[i] = structures[i].Raw;
        Table.CmdWriteAccelerationStructureSizes(commandBuffer.Raw, raw, (uint)structures.Length, queryPool.Raw, queryPoolOffset);
    }

    public void CopyAccelerationStructure(NriCommandBuffer commandBuffer, NriAccelerationStructure dst, NriAccelerationStructure src, CopyMode mode) =>
        Table.CmdCopyAccelerationStructure(commandBuffer.Raw, dst.Raw, src.Raw, mode);

    public void DispatchRays(NriCommandBuffer commandBuffer, in NriDispatchRays desc)
    {
        var native = new DispatchRaysDesc
        {
            raygenShaderRecord = desc.RaygenShaderRecord.ToNative(),
            missShaderBindingTable = desc.MissShaderBindingTable.ToNative(),
            hitShaderBindingTable = desc.HitShaderBindingTable.ToNative(),
            callableShaderBindingTable = desc.CallableShaderBindingTable.ToNative(),
            width = desc.Width, height = desc.Height, depth = desc.Depth,
        };
        Table.CmdDispatchRays(commandBuffer.Raw, &native);
    }

    /// <summary>The buffer holds <c>DispatchRaysIndirectDesc</c> commands.</summary>
    public void DispatchRaysIndirect(NriCommandBuffer commandBuffer, NriBuffer buffer, ulong offset) =>
        Table.CmdDispatchRaysIndirect(commandBuffer.Raw, buffer.Raw, offset);

    // ---------------------------------------------------------------- marshalling

    private static Buffer* RawOrNull(NriBuffer? buffer) => buffer is null ? null : buffer.Raw;

    internal static BottomLevelGeometryDesc ToNative(in NriBottomLevelGeometry g, BottomLevelTrianglesMicromapDesc* micromapSlot, bool forCreation)
    {
        var native = new BottomLevelGeometryDesc { flags = g.Flags, type = g.Type };
        if (g.Type == BottomLevelGeometryType.AABBS)
        {
            native.aabbs = new BottomLevelAabbsDesc { buffer = g.Aabbs.Buffer.Raw, offset = g.Aabbs.Offset, num = g.Aabbs.Num, stride = g.Aabbs.Stride };
            return native;
        }

        var t = g.Triangles;
        BottomLevelTrianglesMicromapDesc* micromap = null;
        if (t.Micromap is { } m)
        {
            *micromapSlot = new BottomLevelTrianglesMicromapDesc
            {
                micromap = m.Micromap is null ? null : m.Micromap.Raw,
                indexBuffer = RawOrNull(m.IndexBuffer),
                indexOffset = m.IndexOffset,
                baseTriangle = m.BaseTriangle,
                indexType = m.IndexType,
            };
            micromap = micromapSlot;
        }

        native.triangles = new BottomLevelTrianglesDesc
        {
            vertexBuffer = t.VertexBuffer.Raw, vertexOffset = t.VertexOffset, vertexNum = t.VertexNum, vertexStride = t.VertexStride, vertexFormat = t.VertexFormat,
            indexBuffer = RawOrNull(t.IndexBuffer), indexOffset = t.IndexOffset, indexNum = t.IndexNum, indexType = t.IndexType,
            transformBuffer = RawOrNull(t.TransformBuffer), transformOffset = t.TransformOffset,
            micromap = micromap,
        };
        _ = forCreation;
        return native;
    }

    /// <summary>A pinned native <c>AccelerationStructureDesc</c> for the duration of one call.</summary>
    private sealed class AccelerationStructureDescNative : IDisposable
    {
        private readonly BottomLevelGeometryDesc[] _geometries;
        private readonly BottomLevelTrianglesMicromapDesc[] _micromaps;
        private GCHandle _geometryPin;
        private GCHandle _micromapPin;
        private readonly AccelerationStructureDesc* _desc;

        public AccelerationStructureDescNative(NriAccelerationStructureDesc desc)
        {
            ArgumentNullException.ThrowIfNull(desc);
            var n = desc.Type == AccelerationStructureType.BOTTOM_LEVEL ? desc.Geometries.Length : 0;
            _geometries = new BottomLevelGeometryDesc[n];
            _micromaps = new BottomLevelTrianglesMicromapDesc[n];
            _micromapPin = GCHandle.Alloc(_micromaps, GCHandleType.Pinned);
            var micromaps = (BottomLevelTrianglesMicromapDesc*)_micromapPin.AddrOfPinnedObject();
            for (var i = 0; i < n; i++) _geometries[i] = ToNative(in desc.Geometries[i], micromaps + i, forCreation: true);
            _geometryPin = GCHandle.Alloc(_geometries, GCHandleType.Pinned);

            _desc = (AccelerationStructureDesc*)NativeMemory.Alloc((nuint)sizeof(AccelerationStructureDesc));
            *_desc = new AccelerationStructureDesc
            {
                optimizedSize = desc.OptimizedSize,
                geometries = n == 0 ? null : (BottomLevelGeometryDesc*)_geometryPin.AddrOfPinnedObject(),
                geometryOrInstanceNum = desc.Type == AccelerationStructureType.BOTTOM_LEVEL ? (uint)n : desc.InstanceNum,
                flags = desc.Flags,
                type = desc.Type,
            };
        }

        public AccelerationStructureDesc* Pointer => _desc;

        public void Dispose()
        {
            NativeMemory.Free(_desc);
            if (_geometryPin.IsAllocated) _geometryPin.Free();
            if (_micromapPin.IsAllocated) _micromapPin.Free();
        }
    }
}
