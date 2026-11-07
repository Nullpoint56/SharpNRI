using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

public partial struct ShaderGroupDesc
{
    /// <summary>Use cases: - general: RAYGEN_SHADER, MISS_SHADER or CALLABLE_SHADER - HitGroup: CLOSEST_HIT_SHADER and/or ANY_HIT_SHADER in any order - HitGroup with an intersection shader: INTERSECTION_SHADER + CLOSEST_HIT_SHADER and/or ANY_HIT_SHADER in any order in ShaderLibrary, starting from 1 (0 - unused)</summary>
    [NativeTypeName("uint32_t[3]")]
    public _shaderIndices_e__FixedBuffer shaderIndices;

    [InlineArray(3)]
    public partial struct _shaderIndices_e__FixedBuffer
    {
        public uint e0;
    }
}
