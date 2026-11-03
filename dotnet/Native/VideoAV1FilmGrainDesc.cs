using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

public partial struct VideoAV1FilmGrainDesc
{
    [NativeTypeName("uint8_t")]
    public byte chromaScalingFromLuma;

    [NativeTypeName("uint8_t")]
    public byte overlapFlag;

    [NativeTypeName("uint8_t")]
    public byte clipToRestrictedRange;

    [NativeTypeName("uint8_t")]
    public byte updateGrain;

    [NativeTypeName("uint8_t")]
    public byte matrixCoeffIsIdentity;

    [NativeTypeName("uint8_t")]
    public byte grainScalingMinus8;

    [NativeTypeName("uint8_t")]
    public byte arCoeffLag;

    [NativeTypeName("uint8_t")]
    public byte arCoeffShiftMinus6;

    [NativeTypeName("uint8_t")]
    public byte grainScaleShift;

    [NativeTypeName("uint8_t")]
    public byte filmGrainParamsRefIdx;

    [NativeTypeName("uint16_t")]
    public ushort grainSeed;

    [NativeTypeName("uint8_t")]
    public byte numYPoints;

    [NativeTypeName("uint8_t[14]")]
    public _pointYValue_e__FixedBuffer pointYValue;

    [NativeTypeName("uint8_t[14]")]
    public _pointYScaling_e__FixedBuffer pointYScaling;

    [NativeTypeName("uint8_t")]
    public byte numCbPoints;

    [NativeTypeName("uint8_t[10]")]
    public _pointCbValue_e__FixedBuffer pointCbValue;

    [NativeTypeName("uint8_t[10]")]
    public _pointCbScaling_e__FixedBuffer pointCbScaling;

    [NativeTypeName("uint8_t")]
    public byte numCrPoints;

    [NativeTypeName("uint8_t[10]")]
    public _pointCrValue_e__FixedBuffer pointCrValue;

    [NativeTypeName("uint8_t[10]")]
    public _pointCrScaling_e__FixedBuffer pointCrScaling;

    [NativeTypeName("uint8_t[24]")]
    public _arCoeffsYPlus128_e__FixedBuffer arCoeffsYPlus128;

    [NativeTypeName("uint8_t[25]")]
    public _arCoeffsCbPlus128_e__FixedBuffer arCoeffsCbPlus128;

    [NativeTypeName("uint8_t[25]")]
    public _arCoeffsCrPlus128_e__FixedBuffer arCoeffsCrPlus128;

    [NativeTypeName("uint8_t")]
    public byte cbMult;

    [NativeTypeName("uint8_t")]
    public byte cbLumaMult;

    [NativeTypeName("int16_t")]
    public short cbOffset;

    [NativeTypeName("uint8_t")]
    public byte crMult;

    [NativeTypeName("uint8_t")]
    public byte crLumaMult;

    [NativeTypeName("int16_t")]
    public short crOffset;

    [InlineArray(14)]
    public partial struct _pointYValue_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(14)]
    public partial struct _pointYScaling_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(10)]
    public partial struct _pointCbValue_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(10)]
    public partial struct _pointCbScaling_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(10)]
    public partial struct _pointCrValue_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(10)]
    public partial struct _pointCrScaling_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(24)]
    public partial struct _arCoeffsYPlus128_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(25)]
    public partial struct _arCoeffsCbPlus128_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(25)]
    public partial struct _arCoeffsCrPlus128_e__FixedBuffer
    {
        public byte e0;
    }
}
