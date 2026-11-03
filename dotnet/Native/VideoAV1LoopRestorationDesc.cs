using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

public partial struct VideoAV1LoopRestorationDesc
{
    [NativeTypeName("uint8_t[3]")]
    public _frameRestorationType_e__FixedBuffer frameRestorationType;

    [NativeTypeName("uint8_t")]
    public byte lrUnitShift;

    [NativeTypeName("uint8_t")]
    public byte lrUvShift;

    [InlineArray(3)]
    public partial struct _frameRestorationType_e__FixedBuffer
    {
        public byte e0;
    }
}
