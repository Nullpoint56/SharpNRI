// L2 — a query pool.

using SharpNRI.Native;

namespace SharpNRI;

/// <summary>A pool of queries of one type. Results are copied to a buffer with <c>CopyQueries</c>.</summary>
public sealed unsafe class NriQueryPool : NriObject
{
    internal NriQueryPool(NriDevice device, QueryPool* handle, QueryType type, uint capacity) : base(handle, "NriQueryPool")
    {
        Device = device;
        Type = type;
        Capacity = capacity;
        device.ChildCreated();
    }

    public override NriDevice Device { get; }
    public QueryType Type { get; }
    public uint Capacity { get; }

    internal QueryPool* Raw => (QueryPool*)Handle;

    /// <summary>Bytes one query result occupies when copied out.</summary>
    public uint QuerySize => Device.Core.GetQuerySize(Raw);

    /// <summary>Resets queries on the host. The pool must not be in use by the GPU.</summary>
    public void Reset(uint offset, uint num) => Device.Core.ResetQueries(Raw, offset, num);

    private protected override void Destroy(void* handle) => Device.Core.DestroyQueryPool((QueryPool*)handle);
}
