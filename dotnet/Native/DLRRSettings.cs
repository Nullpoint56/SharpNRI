using System.Runtime.CompilerServices;

namespace PrismNRI.Native;

public partial struct DLRRSettings
{
    [NativeTypeName("float[16]")]
    public _worldToViewMatrix_e__FixedBuffer worldToViewMatrix;

    [NativeTypeName("float[16]")]
    public _viewToClipMatrix_e__FixedBuffer viewToClipMatrix;

    [InlineArray(16)]
    public partial struct _worldToViewMatrix_e__FixedBuffer
    {
        public float e0;
    }

    [InlineArray(16)]
    public partial struct _viewToClipMatrix_e__FixedBuffer
    {
        public float e0;
    }
}
