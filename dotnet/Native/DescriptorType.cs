namespace PrismNRI.Native;

[NativeTypeName("uint8_t")]
public enum DescriptorType : byte
{
    SAMPLER,
    MUTABLE,
    TEXTURE,
    STORAGE_TEXTURE,
    INPUT_ATTACHMENT,
    BUFFER,
    STORAGE_BUFFER,
    CONSTANT_BUFFER,
    STRUCTURED_BUFFER,
    STORAGE_STRUCTURED_BUFFER,
    ACCELERATION_STRUCTURE,
    MAX_NUM,
}
