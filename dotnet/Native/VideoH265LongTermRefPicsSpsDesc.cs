using System.Runtime.CompilerServices;

namespace SharpNRI.Native;

public partial struct VideoH265LongTermRefPicsSpsDesc
{
    [NativeTypeName("uint32_t")]
    public uint usedByCurrPicLtSpsFlag;

    [NativeTypeName("uint32_t[32]")]
    public _ltRefPicPocLsbSps_e__FixedBuffer ltRefPicPocLsbSps;

    [InlineArray(32)]
    public partial struct _ltRefPicPocLsbSps_e__FixedBuffer
    {
        public uint e0;
    }
}
