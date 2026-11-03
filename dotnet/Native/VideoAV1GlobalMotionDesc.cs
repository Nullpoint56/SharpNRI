using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

public partial struct VideoAV1GlobalMotionDesc
{
    [NativeTypeName("uint8_t[8]")]
    public _type_e__FixedBuffer type;

    [NativeTypeName("uint8_t[8]")]
    public _invalid_e__FixedBuffer invalid;

    [NativeTypeName("int32_t[8][6]")]
    public _params_e__FixedBuffer @params;

    [InlineArray(8)]
    public partial struct _type_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8)]
    public partial struct _invalid_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8 * 6)]
    public partial struct _params_e__FixedBuffer
    {
        public int e0_0;
    }
}
