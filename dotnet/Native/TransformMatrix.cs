using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

/// <summary>Data layout</summary>
public partial struct TransformMatrix
{
    /// <summary>3x4 row-major affine transformation matrix, the first three columns of matrix must define an invertible 3x3 matrix</summary>
    [NativeTypeName("float[3][4]")]
    public _matrix_e__FixedBuffer matrix;

    [InlineArray(3 * 4)]
    public partial struct _matrix_e__FixedBuffer
    {
        public float e0_0;
    }
}
