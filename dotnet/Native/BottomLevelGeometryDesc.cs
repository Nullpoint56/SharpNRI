using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace PrismNRI.Native;

public partial struct BottomLevelGeometryDesc
{
    [NativeTypeName("nri::BottomLevelGeometryBits")]
    public BottomLevelGeometryBits flags;

    [NativeTypeName("nri::BottomLevelGeometryType")]
    public BottomLevelGeometryType type;

    [NativeTypeName("__AnonymousRecord_NRIRayTracing_L182_C5")]
    public _Anonymous_e__Union Anonymous;

    [UnscopedRef]
    public ref BottomLevelTrianglesDesc triangles
    {
        get
        {
            return ref Anonymous.triangles;
        }
    }

    [UnscopedRef]
    public ref BottomLevelAabbsDesc aabbs
    {
        get
        {
            return ref Anonymous.aabbs;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public partial struct _Anonymous_e__Union
    {
        [FieldOffset(0)]
        [NativeTypeName("nri::BottomLevelTrianglesDesc")]
        public BottomLevelTrianglesDesc triangles;

        [FieldOffset(0)]
        [NativeTypeName("nri::BottomLevelAabbsDesc")]
        public BottomLevelAabbsDesc aabbs;
    }
}
