namespace SharpNRI.Native;

public partial struct VideoPictureState
{
    [NativeTypeName("nri::AccessLayoutStage")]
    public AccessLayoutStage required;

    [NativeTypeName("nri::AccessLayoutStage")]
    public AccessLayoutStage videoQueueAfter;

    [NativeTypeName("nri::AccessLayoutStage")]
    public AccessLayoutStage consumerQueueBefore;

    [NativeTypeName("bool")]
    public byte transitionOnVideoQueue;
}
