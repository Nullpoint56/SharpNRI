using System.Runtime.CompilerServices;

namespace PrismNRI.Native;

public partial struct VideoAV1LoopFilterDesc
{
    [NativeTypeName("uint8_t[4]")]
    public _level_e__FixedBuffer level;

    [NativeTypeName("uint8_t")]
    public byte sharpness;

    [NativeTypeName("uint8_t")]
    public byte deltaEnabled;

    [NativeTypeName("uint8_t")]
    public byte deltaUpdate;

    [NativeTypeName("uint8_t")]
    public byte updateModeDelta;

    [NativeTypeName("int8_t[8]")]
    public _refDeltas_e__FixedBuffer refDeltas;

    [NativeTypeName("int8_t[2]")]
    public _modeDeltas_e__FixedBuffer modeDeltas;

    [InlineArray(4)]
    public partial struct _level_e__FixedBuffer
    {
        public byte e0;
    }

    [InlineArray(8)]
    public partial struct _refDeltas_e__FixedBuffer
    {
        public sbyte e0;
    }

    [InlineArray(2)]
    public partial struct _modeDeltas_e__FixedBuffer
    {
        public sbyte e0;
    }
}
