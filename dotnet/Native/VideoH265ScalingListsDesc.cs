using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

public partial struct VideoH265ScalingListsDesc
{
    [NativeTypeName("uint8_t[6][16]")]
    public _scalingList4x4_e__FixedBuffer scalingList4x4;

    [NativeTypeName("uint8_t[6][64]")]
    public _scalingList8x8_e__FixedBuffer scalingList8x8;

    [NativeTypeName("uint8_t[6][64]")]
    public _scalingList16x16_e__FixedBuffer scalingList16x16;

    [NativeTypeName("uint8_t[2][64]")]
    public _scalingList32x32_e__FixedBuffer scalingList32x32;

    [NativeTypeName("uint8_t[6]")]
    public _scalingListDCCoef16x16_e__FixedBuffer scalingListDCCoef16x16;

    [NativeTypeName("uint8_t[2]")]
    public _scalingListDCCoef32x32_e__FixedBuffer scalingListDCCoef32x32;

    [InlineArray(6 * 16)]
    public partial struct _scalingList4x4_e__FixedBuffer
    {
        public byte e0_0;
    }

    [InlineArray(6 * 64)]
    public partial struct _scalingList8x8_e__FixedBuffer
    {
        public byte e0_0;
    }

    [InlineArray(6 * 64)]
    public partial struct _scalingList16x16_e__FixedBuffer
    {
        public byte e0_0;
    }

    [InlineArray(2 * 64)]
    public partial struct _scalingList32x32_e__FixedBuffer
    {
        public byte e0_0;
    }

    [InlineArray(6)]
    public partial struct _scalingListDCCoef16x16_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(2)]
    public partial struct _scalingListDCCoef32x32_e__FixedBuffer
    {
        public byte e0;
    }
}
