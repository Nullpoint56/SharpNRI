using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

public partial struct ShaderGroupDesc
{
    [NativeTypeName("uint32_t[3]")]
    public _shaderIndices_e__FixedBuffer shaderIndices;

    [InlineArray(3)]
    public partial struct _shaderIndices_e__FixedBuffer
    {
        public uint e0;
    }
}
