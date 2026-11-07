using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

public partial struct VideoAV1CdefDesc
{
    /// <summary>0..15</summary>
    [NativeTypeName("uint8_t[8]")]
    public _yPrimaryStrength_e__FixedBuffer yPrimaryStrength;

    /// <summary>0..3 as coded in "cdef_y_sec_strength" (3 represents strength 4)</summary>
    [NativeTypeName("uint8_t[8]")]
    public _ySecondaryStrength_e__FixedBuffer ySecondaryStrength;

    /// <summary>0..15</summary>
    [NativeTypeName("uint8_t[8]")]
    public _uvPrimaryStrength_e__FixedBuffer uvPrimaryStrength;

    /// <summary>0..3 as coded in "cdef_uv_sec_strength"</summary>
    [NativeTypeName("uint8_t[8]")]
    public _uvSecondaryStrength_e__FixedBuffer uvSecondaryStrength;

    [InlineArray(8)]
    public partial struct _yPrimaryStrength_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8)]
    public partial struct _ySecondaryStrength_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8)]
    public partial struct _uvPrimaryStrength_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8)]
    public partial struct _uvSecondaryStrength_e__FixedBuffer
    {
        public byte e0;
    }
}
