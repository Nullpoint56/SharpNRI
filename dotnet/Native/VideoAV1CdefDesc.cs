using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

public partial struct VideoAV1CdefDesc
{
    [NativeTypeName("uint8_t[8]")]
    public _yPrimaryStrength_e__FixedBuffer yPrimaryStrength;

    [NativeTypeName("uint8_t[8]")]
    public _ySecondaryStrength_e__FixedBuffer ySecondaryStrength;

    [NativeTypeName("uint8_t[8]")]
    public _uvPrimaryStrength_e__FixedBuffer uvPrimaryStrength;

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
