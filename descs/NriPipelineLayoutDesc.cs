// Managed forms of the NRI descs that carry pointers to arrays.
//
// NRI's PipelineLayoutDesc points at arrays of RootConstantDesc, RootDescriptorDesc, RootSamplerDesc
// and DescriptorSetDesc, and each DescriptorSetDesc points at an array of DescriptorRangeDesc. A C#
// caller cannot hold a stable pointer to a managed array, so these are the same descs with arrays
// where NRI has pointer-plus-count, and nothing else changed: the field names are NRI's and the
// leaf structs are the generated ones. SharpNRI pins and marshals at the create call.
//
// They live in SharpNRI.Descs because a library that projects shader reflection into a pipeline
// layout has to construct them without a device or the native library in hand.

using SharpNRI.Native;

namespace SharpNRI;

/// <summary><c>DescriptorSetDesc</c> with its ranges as an array.</summary>
/// <param name="RegisterSpace">Must be unique across the layout; avoid big gaps.</param>
/// <param name="Ranges">The descriptor ranges in this set.</param>
public sealed record NriDescriptorSetDesc(
    uint RegisterSpace,
    DescriptorRangeDesc[] Ranges);

/// <summary><c>PipelineLayoutDesc</c> with its arrays as arrays.</summary>
public sealed class NriPipelineLayoutDesc
{
    /// <summary>Register space of the root constants, descriptors and samplers.</summary>
    public uint RootRegisterSpace { get; init; }

    public RootConstantDesc[] RootConstants { get; init; } = [];
    public RootDescriptorDesc[] RootDescriptors { get; init; } = [];
    public RootSamplerDesc[] RootSamplers { get; init; } = [];
    public NriDescriptorSetDesc[] DescriptorSets { get; init; } = [];

    /// <summary>
    /// The shader stages the layout serves (NRI v191, its validation and both backends):
    /// <list type="bullet">
    /// <item><c>StageBits.ALL</c> (0, the barrier default) or <c>ALL_SHADERS</c> is every shader stage;
    /// any other value must be shader stages only (else "'shaderStages' must be 'ALL' or a shader stage
    /// mask").</item>
    /// <item>On a descriptor range, root constant, root descriptor or root sampler, <c>ALL</c> and
    /// <c>ALL_SHADERS</c> mean this value; explicit stages must be shader stages, and a range's must lie
    /// within this value ("is not compatible with 'shaderStages'").</item>
    /// <item>Every shader of a pipeline made with the layout must have its stage here ("'shaders[i].stage'
    /// is not enabled in the pipeline layout").</item>
    /// <item>Draw-parameter and draw-index emulation (D3D12) apply only with <c>VERTEX_SHADER</c> here.</item>
    /// </list>
    /// </summary>
    public StageBits ShaderStages { get; init; }

    public PipelineLayoutBits Flags { get; init; }
}

/// <summary><c>ShaderDesc</c> with its bytecode as bytes.</summary>
/// <param name="Stage">Exactly one stage bit.</param>
/// <param name="Bytecode">SPIR-V on Vulkan, DXIL on D3D12; see <c>DeviceDesc.shaderFeatures</c> for what the device accepts.</param>
/// <param name="EntryPointName">Optional; NRI defaults to <c>main</c>.</param>
/// <param name="ThreadGroupSizeX">Metal only; ignored on Vulkan and D3D12.</param>
/// <param name="ThreadGroupSizeY">Metal only; ignored on Vulkan and D3D12.</param>
/// <param name="ThreadGroupSizeZ">Metal only; ignored on Vulkan and D3D12.</param>
public sealed record NriShaderDesc(
    StageBits Stage,
    byte[] Bytecode,
    string? EntryPointName = null,
    ushort ThreadGroupSizeX = 0,
    ushort ThreadGroupSizeY = 0,
    ushort ThreadGroupSizeZ = 0);

/// <summary><c>VertexAttributeDesc</c> with the D3D semantic as a string.</summary>
/// <param name="SemanticName">D3D: the HLSL semantic. Ignored on Vulkan.</param>
/// <param name="SemanticIndex">D3D: the semantic's index.</param>
/// <param name="Location">Vulkan: the <c>location</c> decoration. Ignored on D3D.</param>
/// <param name="Offset">Byte offset within the vertex.</param>
/// <param name="Format">The attribute's format.</param>
/// <param name="StreamIndex">Index into <see cref="NriVertexInputDesc.Streams"/>.</param>
public readonly record struct NriVertexAttributeDesc(
    string? SemanticName,
    uint SemanticIndex,
    uint Location,
    uint Offset,
    Format Format,
    ushort StreamIndex);

/// <summary><c>VertexInputDesc</c> with its arrays as arrays.</summary>
public sealed class NriVertexInputDesc
{
    public NriVertexAttributeDesc[] Attributes { get; init; } = [];
    public VertexStreamDesc[] Streams { get; init; } = [];
}
