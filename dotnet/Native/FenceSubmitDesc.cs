namespace SharpNRI.Native;

public unsafe partial struct FenceSubmitDesc
{
    [NativeTypeName("nri::Fence *")]
    public Fence* fence;

    [NativeTypeName("uint64_t")]
    public ulong value;

    [NativeTypeName("nri::StageBits")]
    public StageBits stages;
}
