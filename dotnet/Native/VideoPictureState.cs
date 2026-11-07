namespace SharpNRI.Native;

public partial struct VideoPictureState
{
    /// <summary>state required while the picture participates in CmdDecodeVideo or CmdEncodeVideo</summary>
    [NativeTypeName("nri::AccessLayoutStage")]
    public AccessLayoutStage required;

    /// <summary>destination state of the optional post-video transition</summary>
    [NativeTypeName("nri::AccessLayoutStage")]
    public AccessLayoutStage videoQueueAfter;

    /// <summary>source state for the consumer queue transition</summary>
    [NativeTypeName("nri::AccessLayoutStage")]
    public AccessLayoutStage consumerQueueBefore;

    /// <summary>transition "required" -&gt; "videoQueueAfter" before cross-queue consumption</summary>
    [NativeTypeName("bool")]
    public byte transitionOnVideoQueue;
}
