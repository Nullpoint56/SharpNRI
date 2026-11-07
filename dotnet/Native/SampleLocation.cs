namespace SharpNRI.Native;

public partial struct SampleLocation
{
    [NativeTypeName("int8_t")]
    public sbyte x;

    /// <summary>[-8; 7]</summary>
    [NativeTypeName("int8_t")]
    public sbyte y;
}
