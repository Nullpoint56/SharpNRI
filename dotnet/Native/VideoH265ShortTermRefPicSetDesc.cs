using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

public partial struct VideoH265ShortTermRefPicSetDesc
{
    [NativeTypeName("nri::VideoH265ShortTermRefPicSetBits")]
    public VideoH265ShortTermRefPicSetBits flags;

    [NativeTypeName("uint8_t")]
    public byte numNegativePics;

    [NativeTypeName("uint8_t")]
    public byte numPositivePics;

    [NativeTypeName("uint32_t")]
    public uint deltaIdxMinus1;

    [NativeTypeName("uint16_t")]
    public ushort useDeltaFlag;

    [NativeTypeName("uint16_t")]
    public ushort absDeltaRpsMinus1;

    [NativeTypeName("uint16_t")]
    public ushort usedByCurrPicFlag;

    [NativeTypeName("uint16_t")]
    public ushort usedByCurrPicS0Flag;

    [NativeTypeName("uint16_t")]
    public ushort usedByCurrPicS1Flag;

    [NativeTypeName("uint16_t[16]")]
    public _deltaPocS0Minus1_e__FixedBuffer deltaPocS0Minus1;

    [NativeTypeName("uint16_t[16]")]
    public _deltaPocS1Minus1_e__FixedBuffer deltaPocS1Minus1;

    [InlineArray(16)]
    public partial struct _deltaPocS0Minus1_e__FixedBuffer
    {
        public ushort e0;
    }

    [InlineArray(16)]
    public partial struct _deltaPocS1Minus1_e__FixedBuffer
    {
        public ushort e0;
    }
}
