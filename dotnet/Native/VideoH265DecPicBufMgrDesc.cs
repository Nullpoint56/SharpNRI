using System.Runtime.CompilerServices;

namespace PrismNRI.Native;

public partial struct VideoH265DecPicBufMgrDesc
{
    [NativeTypeName("uint8_t[8]")]
    public _maxDecPicBufferingMinus1_e__FixedBuffer maxDecPicBufferingMinus1;

    [NativeTypeName("uint8_t[8]")]
    public _maxNumReorderPics_e__FixedBuffer maxNumReorderPics;

    [NativeTypeName("uint32_t[8]")]
    public _maxLatencyIncreasePlus1_e__FixedBuffer maxLatencyIncreasePlus1;

    [InlineArray(8)]
    public partial struct _maxDecPicBufferingMinus1_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8)]
    public partial struct _maxNumReorderPics_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8)]
    public partial struct _maxLatencyIncreasePlus1_e__FixedBuffer
    {
        public uint e0;
    }
}
