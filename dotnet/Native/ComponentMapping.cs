namespace SharpNRI.Native;

public partial struct ComponentMapping
{
    /// <summary>Only for non-"STORAGE" views</summary>
    [NativeTypeName("nri::ComponentSwizzle")]
    public ComponentSwizzle r;

    [NativeTypeName("nri::ComponentSwizzle")]
    public ComponentSwizzle g;

    [NativeTypeName("nri::ComponentSwizzle")]
    public ComponentSwizzle b;

    [NativeTypeName("nri::ComponentSwizzle")]
    public ComponentSwizzle a;
}
