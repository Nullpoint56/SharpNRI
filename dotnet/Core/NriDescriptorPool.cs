// L2 — descriptor pools and the sets allocated from them.
//
// Descriptor sets are not destroyed individually: ResetDescriptorPool wipes every set the pool
// has handed out, and destroying the pool does the same. So NriDescriptorSet is a plain handle
// that is valid until its pool is reset or disposed, and nothing here tries to track that. It is
// a struct so that a frame which resets its pool and allocates its sets again allocates nothing
// on the managed heap: AllocateDescriptorSets(..., Span<NriDescriptorSet>) and
// AllocateDescriptorSet write into caller-owned or stack storage.

using SharpNRI.Native;

namespace SharpNRI;

/// <summary>A pool descriptor sets are allocated from.</summary>
public sealed unsafe class NriDescriptorPool : NriObject
{
    internal NriDescriptorPool(NriDevice device, DescriptorPool* handle) : base(handle, "NriDescriptorPool")
    {
        Device = device;
        device.ChildCreated();
    }

    public override NriDevice Device { get; }

    internal DescriptorPool* Raw => (DescriptorPool*)Handle;

    /// <summary>
    /// Allocates <paramref name="instanceNum"/> sets with the layout of set
    /// <paramref name="setIndex"/> in <paramref name="pipelineLayout"/>.
    /// <paramref name="variableDescriptorNum"/> sizes a <c>VARIABLE_SIZED_ARRAY</c> range.
    /// Allocates the returned array; per frame, use
    /// <see cref="AllocateDescriptorSets(NriPipelineLayout, uint, Span{NriDescriptorSet}, uint)"/>.
    /// </summary>
    public NriDescriptorSet[] AllocateDescriptorSets(NriPipelineLayout pipelineLayout, uint setIndex, uint instanceNum = 1, uint variableDescriptorNum = 0)
    {
        var sets = new NriDescriptorSet[checked((int)instanceNum)];
        AllocateDescriptorSets(pipelineLayout, setIndex, sets, variableDescriptorNum);
        return sets;
    }

    /// <summary>
    /// Allocates <c>sets.Length</c> sets with the layout of set <paramref name="setIndex"/> in
    /// <paramref name="pipelineLayout"/> into <paramref name="sets"/>. No heap allocation: the
    /// per-frame form. <paramref name="variableDescriptorNum"/> sizes a
    /// <c>VARIABLE_SIZED_ARRAY</c> range. An empty span calls nothing.
    /// </summary>
    public void AllocateDescriptorSets(NriPipelineLayout pipelineLayout, uint setIndex, Span<NriDescriptorSet> sets, uint variableDescriptorNum = 0)
    {
        ArgumentNullException.ThrowIfNull(pipelineLayout);
        if (sets.IsEmpty) return;
        var handles = stackalloc DescriptorSet*[sets.Length];
        NriException.Check(Device.Core.AllocateDescriptorSets(Raw, pipelineLayout.Raw, setIndex, handles, (uint)sets.Length, variableDescriptorNum), "AllocateDescriptorSets");
        for (var i = 0; i < sets.Length; i++) sets[i] = new NriDescriptorSet(this, handles[i]);
    }

    /// <summary>
    /// One set; see <see cref="AllocateDescriptorSets(NriPipelineLayout, uint, uint, uint)"/>.
    /// No heap allocation.
    /// </summary>
    public NriDescriptorSet AllocateDescriptorSet(NriPipelineLayout pipelineLayout, uint setIndex, uint variableDescriptorNum = 0)
    {
        ArgumentNullException.ThrowIfNull(pipelineLayout);
        var handles = stackalloc DescriptorSet*[1];
        NriException.Check(Device.Core.AllocateDescriptorSets(Raw, pipelineLayout.Raw, setIndex, handles, 1, variableDescriptorNum), "AllocateDescriptorSets");
        return new NriDescriptorSet(this, handles[0]);
    }

    /// <summary>Invalidates every set allocated from this pool.</summary>
    public void Reset() => Device.Core.ResetDescriptorPool(Raw);

    private protected override void Destroy(void* handle) => Device.Core.DestroyDescriptorPool((DescriptorPool*)handle);
}

/// <summary>A descriptor set. Valid until its pool is reset or disposed.</summary>
/// <remarks>
/// A value: the NRI handle and the pool it came from, nothing to dispose. Equality is by handle
/// (and pool), so copies of one set are equal. <c>default</c> is not a set: it has no pool, and NRI
/// validation rejects its null handle wherever it is passed.
/// </remarks>
public readonly unsafe struct NriDescriptorSet : IEquatable<NriDescriptorSet>
{
    private readonly DescriptorSet* _handle;

    internal NriDescriptorSet(NriDescriptorPool pool, DescriptorSet* handle)
    {
        Pool = pool;
        _handle = handle;
    }

    public NriDescriptorPool Pool { get; }
    public NriDevice Device => Pool.Device;

    internal DescriptorSet* Raw => _handle;

    /// <summary>NRI's <c>nri::DescriptorSet*</c>; see <see cref="NriObject.NriHandle"/>. Last resort.</summary>
    public nint NriHandle => (nint)_handle;

    /// <summary>
    /// Writes <paramref name="descriptors"/> into range <paramref name="rangeIndex"/> starting at
    /// <paramref name="baseDescriptor"/>. All descriptors must be of the range's type.
    /// </summary>
    public void UpdateRange(uint rangeIndex, uint baseDescriptor, ReadOnlySpan<NriDescriptor> descriptors)
    {
        var raw = stackalloc Descriptor*[descriptors.Length];
        for (var i = 0; i < descriptors.Length; i++) raw[i] = descriptors[i].Raw;

        var desc = new UpdateDescriptorRangeDesc
        {
            descriptorSet = _handle,
            rangeIndex = rangeIndex,
            baseDescriptor = baseDescriptor,
            descriptors = raw,
            descriptorNum = (uint)descriptors.Length,
        };
        Device.Core.UpdateDescriptorRanges(&desc, 1);
    }

    /// <summary>One descriptor at one slot.</summary>
    public void Update(uint rangeIndex, uint descriptorIndex, NriDescriptor descriptor)
    {
        var raw = descriptor.Raw;
        var desc = new UpdateDescriptorRangeDesc
        {
            descriptorSet = _handle,
            rangeIndex = rangeIndex,
            baseDescriptor = descriptorIndex,
            descriptors = &raw,
            descriptorNum = 1,
        };
        Device.Core.UpdateDescriptorRanges(&desc, 1);
    }

    /// <summary>
    /// Copies descriptors from a set allocated with <see cref="DescriptorPoolBits.COPY_SOURCE"/>.
    /// </summary>
    public void CopyRange(uint dstRangeIndex, uint dstBaseDescriptor, NriDescriptorSet source, uint srcRangeIndex, uint srcBaseDescriptor, uint descriptorNum)
    {
        var desc = new CopyDescriptorRangeDesc
        {
            dstDescriptorSet = _handle,
            dstRangeIndex = dstRangeIndex,
            dstBaseDescriptor = dstBaseDescriptor,
            srcDescriptorSet = source._handle,
            srcRangeIndex = srcRangeIndex,
            srcBaseDescriptor = srcBaseDescriptor,
            descriptorNum = descriptorNum,
        };
        Device.Core.CopyDescriptorRanges(&desc, 1);
    }

    /// <summary>
    /// D3D12: this set's offsets in the resource and sampler descriptor heaps, for shaders that
    /// index heaps directly. Vulkan: both 0.
    /// </summary>
    public (uint ResourceHeap, uint SamplerHeap) GetOffsets()
    {
        uint resource, sampler;
        Device.Core.GetDescriptorSetOffsets(_handle, &resource, &sampler);
        return (resource, sampler);
    }

    /// <summary>Same handle from the same pool.</summary>
    public bool Equals(NriDescriptorSet other) => _handle == other._handle && ReferenceEquals(Pool, other.Pool);

    public override bool Equals(object? obj) => obj is NriDescriptorSet other && Equals(other);

    public override int GetHashCode() => ((nint)_handle).GetHashCode();

    public static bool operator ==(NriDescriptorSet left, NriDescriptorSet right) => left.Equals(right);

    public static bool operator !=(NriDescriptorSet left, NriDescriptorSet right) => !left.Equals(right);
}
