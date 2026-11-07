using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

public partial struct DLRRSettings
{
    /// <summary>{Xx, Yx, Zx, 0, Xy, Yy, Zy, 0, Xz, Yz, Zz, 0, Tx, Ty, Tz, 1}, where {X, Y, Z} - axes, T - translation</summary>
    [NativeTypeName("float[16]")]
    public _worldToViewMatrix_e__FixedBuffer worldToViewMatrix;

    /// <summary>{-, -, -, 0, -, -, -, 0, -, -, -, A, -, -, -, B}, where {A; B} = {0; 1} for ortho or {-1/+1; 0} for perspective projections</summary>
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
