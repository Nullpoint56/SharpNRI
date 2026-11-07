// L2 — pipelines and the pipeline cache.
//
// GraphicsPipelineDesc carries five pointers into arrays and one into a layout; the managed desc
// below holds the same things as arrays and objects, and Create pins them for the call.

using System.Runtime.InteropServices;
using SharpNRI.Interop;
using SharpNRI.Native;

namespace SharpNRI;

/// <summary><c>OutputMergerDesc</c> with its colour attachments as an array.</summary>
public sealed class NriOutputMergerDesc
{
    public ColorAttachmentDesc[] Colors { get; init; } = [];
    public DepthAttachmentDesc Depth { get; init; }
    public StencilAttachmentDesc Stencil { get; init; }
    public Format DepthStencilFormat { get; init; }
    public LogicOp LogicOp { get; init; }
    public uint ViewMask { get; init; }
    public Multiview Multiview { get; init; }
}

/// <summary><c>GraphicsPipelineDesc</c> in managed form.</summary>
public sealed class NriGraphicsPipelineDesc
{
    public required NriPipelineLayout PipelineLayout { get; init; }
    public NriVertexInputDesc? VertexInput { get; init; }
    public InputAssemblyDesc InputAssembly { get; init; }
    public RasterizationDesc Rasterization { get; init; }
    public MultisampleDesc? Multisample { get; init; }
    public NriOutputMergerDesc OutputMerger { get; init; } = new();
    public required NriShaderDesc[] Shaders { get; init; }
    public GraphicsPipelineBits Flags { get; init; }
    public Robustness Robustness { get; init; }
    public NriPipelineCache? Cache { get; init; }
}

/// <summary><c>ComputePipelineDesc</c> in managed form.</summary>
public sealed class NriComputePipelineDesc
{
    public required NriPipelineLayout PipelineLayout { get; init; }
    public required NriShaderDesc Shader { get; init; }
    public ComputePipelineBits Flags { get; init; }
    public Robustness Robustness { get; init; }
    public NriPipelineCache? Cache { get; init; }
}

/// <summary>A graphics, compute or ray-tracing pipeline.</summary>
public sealed unsafe class NriPipeline : NriObject
{
    internal NriPipeline(NriDevice device, Pipeline* handle, BindPoint bindPoint) : base(handle, "NriPipeline")
    {
        Device = device;
        BindPoint = bindPoint;
        device.ChildCreated();
    }

    public override NriDevice Device { get; }

    /// <summary>Graphics, compute or ray tracing.</summary>
    public BindPoint BindPoint { get; }

    internal Pipeline* Raw => (Pipeline*)Handle;

    internal static NriPipeline CreateGraphics(NriDevice device, NriGraphicsPipelineDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);
        using var shaders = new ShaderArray(desc.Shaders);
        using var vertexInput = new VertexInputArrays(desc.VertexInput);

        var multisample = desc.Multisample ?? default;

        fixed (ColorAttachmentDesc* colors = desc.OutputMerger.Colors)
        {
            var native = new GraphicsPipelineDesc
            {
                pipelineLayout = desc.PipelineLayout.Raw,
                vertexInput = vertexInput.Pointer,
                inputAssembly = desc.InputAssembly,
                rasterization = desc.Rasterization,
                multisample = desc.Multisample is null ? null : &multisample,
                outputMerger = new OutputMergerDesc
                {
                    colors = colors,
                    colorNum = (uint)desc.OutputMerger.Colors.Length,
                    depth = desc.OutputMerger.Depth,
                    stencil = desc.OutputMerger.Stencil,
                    depthStencilFormat = desc.OutputMerger.DepthStencilFormat,
                    logicOp = desc.OutputMerger.LogicOp,
                    viewMask = desc.OutputMerger.ViewMask,
                    multiview = desc.OutputMerger.Multiview,
                },
                shaders = shaders.Pointer,
                shaderNum = shaders.Count,
                flags = desc.Flags,
                robustness = desc.Robustness,
                cache = desc.Cache is null ? null : desc.Cache.Raw,
            };

            Pipeline* handle = null;
            NriException.Check(device.Core.CreateGraphicsPipeline(device.Raw, &native, &handle), "CreateGraphicsPipeline");
            return new NriPipeline(device, handle, BindPoint.GRAPHICS);
        }
    }

    internal static NriPipeline CreateCompute(NriDevice device, NriComputePipelineDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);
        using var shaders = new ShaderArray([desc.Shader]);

        var native = new ComputePipelineDesc
        {
            pipelineLayout = desc.PipelineLayout.Raw,
            shader = shaders.Pointer[0],
            flags = desc.Flags,
            robustness = desc.Robustness,
            cache = desc.Cache is null ? null : desc.Cache.Raw,
        };

        Pipeline* handle = null;
        NriException.Check(device.Core.CreateComputePipeline(device.Raw, &native, &handle), "CreateComputePipeline");
        return new NriPipeline(device, handle, BindPoint.COMPUTE);
    }

    private protected override void Destroy(void* handle) => Device.Core.DestroyPipeline((Pipeline*)handle);

    /// <summary>Pinned bytecode and allocated entry-point names for the duration of a create call.</summary>
    internal sealed class ShaderArray : IDisposable
    {
        private readonly GCHandle[] _pins;
        private readonly sbyte*[] _names;
        private readonly ShaderDesc[] _descs;
        private GCHandle _descPin;

        public ShaderArray(NriShaderDesc[] shaders)
        {
            _pins = new GCHandle[shaders.Length];
            _names = new sbyte*[shaders.Length];
            _descs = new ShaderDesc[shaders.Length];
            for (var i = 0; i < shaders.Length; i++)
            {
                var s = shaders[i];
                _pins[i] = GCHandle.Alloc(s.Bytecode, GCHandleType.Pinned);
                _names[i] = Utf8.Alloc(s.EntryPointName);
                _descs[i] = new ShaderDesc
                {
                    stage = s.Stage,
                    bytecode = (void*)_pins[i].AddrOfPinnedObject(),
                    size = (ulong)s.Bytecode.Length,
                    entryPointName = _names[i],
                    threadGroupSizeX = s.ThreadGroupSizeX,
                    threadGroupSizeY = s.ThreadGroupSizeY,
                    threadGroupSizeZ = s.ThreadGroupSizeZ,
                };
            }
            _descPin = GCHandle.Alloc(_descs, GCHandleType.Pinned);
        }

        public ShaderDesc* Pointer => (ShaderDesc*)_descPin.AddrOfPinnedObject();
        public uint Count => (uint)_descs.Length;

        public void Dispose()
        {
            if (_descPin.IsAllocated) _descPin.Free();
            foreach (var pin in _pins) if (pin.IsAllocated) pin.Free();
            foreach (var name in _names) Utf8.Free(name);
        }
    }

    internal sealed class VertexInputArrays : IDisposable
    {
        private readonly sbyte*[] _semantics = [];
        private GCHandle _attributes;
        private GCHandle _streams;
        private readonly VertexInputDesc* _desc;

        public VertexInputArrays(NriVertexInputDesc? input)
        {
            if (input is null) return;

            var attributes = new VertexAttributeDesc[input.Attributes.Length];
            _semantics = new sbyte*[attributes.Length];
            for (var i = 0; i < attributes.Length; i++)
            {
                var a = input.Attributes[i];
                _semantics[i] = Utf8.Alloc(a.SemanticName);
                attributes[i] = new VertexAttributeDesc
                {
                    d3d = new VertexAttributeD3D { semanticName = _semantics[i], semanticIndex = a.SemanticIndex },
                    vk = new VertexAttributeVK { location = a.Location },
                    offset = a.Offset,
                    format = a.Format,
                    streamIndex = a.StreamIndex,
                };
            }

            _attributes = GCHandle.Alloc(attributes, GCHandleType.Pinned);
            _streams = GCHandle.Alloc(input.Streams, GCHandleType.Pinned);
            _desc = (VertexInputDesc*)NativeMemory.Alloc((nuint)sizeof(VertexInputDesc));
            *_desc = new VertexInputDesc
            {
                attributes = attributes.Length == 0 ? null : (VertexAttributeDesc*)_attributes.AddrOfPinnedObject(),
                attributeNum = checked((byte)attributes.Length),
                streams = input.Streams.Length == 0 ? null : (VertexStreamDesc*)_streams.AddrOfPinnedObject(),
                streamNum = checked((byte)input.Streams.Length),
            };
        }

        public VertexInputDesc* Pointer => _desc;

        public void Dispose()
        {
            if (_desc is not null) NativeMemory.Free(_desc);
            if (_attributes.IsAllocated) _attributes.Free();
            if (_streams.IsAllocated) _streams.Free();
            foreach (var s in _semantics) Utf8.Free(s);
        }
    }
}

/// <summary>A pipeline cache: PSO blobs persisted across runs.</summary>
public sealed unsafe class NriPipelineCache : NriObject
{
    internal NriPipelineCache(NriDevice device, PipelineCache* handle) : base(handle, "NriPipelineCache")
    {
        Device = device;
        device.ChildCreated();
    }

    public override NriDevice Device { get; }

    internal PipelineCache* Raw => (PipelineCache*)Handle;

    /// <summary>
    /// The cache contents, to persist. Not thread safe: call after every pipeline creation using
    /// this cache has completed.
    /// </summary>
    public byte[] GetData()
    {
        ulong size = 0;
        NriException.Check(Device.Core.GetPipelineCacheData(Raw, null, &size), "GetPipelineCacheData");
        var data = new byte[size];
        if (size == 0) return data;
        fixed (byte* p = data)
        {
            NriException.Check(Device.Core.GetPipelineCacheData(Raw, p, &size), "GetPipelineCacheData");
        }
        return data;
    }

    private protected override void Destroy(void* handle) => Device.Core.DestroyPipelineCache((PipelineCache*)handle);
}
