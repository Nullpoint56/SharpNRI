namespace SharpNRI.Native;

[NativeTypeName("uint8_t")]
public enum BufferView : byte
{
    /// <summary>Shader resources // HLSL type Compatible "DescriptorType" Buffer BUFFER</summary>
    BUFFER,
    /// <summary>StructuredBuffer STRUCTURED_BUFFER</summary>
    STRUCTURED_BUFFER,
    /// <summary>ByteAddressBuffer STRUCTURED_BUFFER</summary>
    BYTE_ADDRESS_BUFFER,
    /// <summary>RWBuffer STORAGE_BUFFER</summary>
    STORAGE_BUFFER,
    /// <summary>RWStructuredBuffer STORAGE_STRUCTURED_BUFFER</summary>
    STORAGE_STRUCTURED_BUFFER,
    /// <summary>RWByteAddressBuffer STORAGE_STRUCTURED_BUFFER</summary>
    STORAGE_BYTE_ADDRESS_BUFFER,
    /// <summary>ConstantBuffer CONSTANT_BUFFER</summary>
    CONSTANT_BUFFER,
    MAX_NUM,
}
