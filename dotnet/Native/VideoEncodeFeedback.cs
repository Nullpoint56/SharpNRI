namespace PrismNRI.Native;

public partial struct VideoEncodeFeedback
{
    [NativeTypeName("uint64_t")]
    public ulong errorFlags;

    [NativeTypeName("uint64_t")]
    public ulong averageQp;

    [NativeTypeName("uint64_t")]
    public ulong intraCodingUnitNum;

    [NativeTypeName("uint64_t")]
    public ulong interCodingUnitNum;

    [NativeTypeName("uint64_t")]
    public ulong skipCodingUnitNum;

    [NativeTypeName("uint64_t")]
    public ulong averageMotionEstimationX;

    [NativeTypeName("uint64_t")]
    public ulong averageMotionEstimationY;

    [NativeTypeName("uint64_t")]
    public ulong encodedBitstreamWrittenBytes;

    [NativeTypeName("uint64_t")]
    public ulong writtenSubregionNum;

    [NativeTypeName("uint64_t")]
    public ulong encodedBitstreamOffset;
}
