using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

public partial struct VideoAV1SegmentationDesc
{
    [NativeTypeName("uint8_t[8]")]
    public _featureEnabled_e__FixedBuffer featureEnabled;

    [NativeTypeName("int16_t[8][8]")]
    public _featureData_e__FixedBuffer featureData;

    [InlineArray(8)]
    public partial struct _featureEnabled_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8 * 8)]
    public partial struct _featureData_e__FixedBuffer
    {
        public short e0_0;
    }
}
