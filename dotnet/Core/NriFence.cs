// L2 — a timeline fence.
//
// NRI's Fence is a timeline semaphore on Vulkan and an ID3D12Fence on D3D12: a monotonically
// increasing 64-bit value, waited on and signalled by value from both host and queues.

using SharpNRI.Native;

namespace SharpNRI;

/// <summary>A timeline fence. Signal and wait by value; the value never goes backwards.</summary>
public sealed unsafe class NriFence : NriObject
{
    internal NriFence(NriDevice device, Fence* handle) : base(handle, "NriFence")
    {
        Device = device;
        device.ChildCreated();
    }

    public override NriDevice Device { get; }

    internal Fence* Raw => (Fence*)Handle;

    /// <summary>The last value signalled so far.</summary>
    public ulong Value => Device.Core.GetFenceValue(Raw);

    /// <summary>
    /// A new handle to the fence, which the caller owns (<see cref="NriSharedHandle"/>). Throws with
    /// <see cref="Result.INVALID_ARGUMENT"/> for a fence not created shared (<see cref="NriDevice.CreateSharedFence"/>).
    /// </summary>
    public NriSharedHandle GetSharedHandle()
    {
        SharedHandle h;
        NriException.Check(Device.Core.GetFenceSharedHandle(Raw, &h), "GetFenceSharedHandle");
        return NriSharedHandle.From(Device, in h);
    }

    /// <summary>Blocks the calling thread until the fence reaches <paramref name="value"/>.</summary>
    /// <remarks>NRI waits at most 5 s (<c>NRI_TIMEOUT_FENCE</c>, Vulkan and D3D12), reports an ERROR
    /// message and returns; this method then throws instead of returning with the fence still short
    /// of the value. For work that may legitimately take longer, poll <see cref="Value"/>. On NONE
    /// the fence never advances and the wait returns at once.</remarks>
    /// <exception cref="TimeoutException">NRI gave up before the fence reached <paramref name="value"/>.</exception>
    public void Wait(ulong value)
    {
        Device.Core.Wait(Raw, value);
        if (Device.GraphicsApi == GraphicsAPI.NONE) return;
        var reached = Value;
        if (reached < value)
            throw new TimeoutException($"the fence did not reach {value} within NRI's wait limit (NRI_TIMEOUT_FENCE, 5 s); it is at {reached}. Poll NriFence.Value for work that takes longer.");
    }

    private protected override void Destroy(void* handle) => Device.Core.DestroyFence((Fence*)handle);
}

/// <summary>A fence to wait on or signal in a submit, with the value and the stages it covers.</summary>
/// <param name="Fence">The fence.</param>
/// <param name="Value">Wait until it reaches this, or signal this.</param>
/// <param name="Stages">Which stages wait or signal; <see cref="StageBits.ALL"/> is always valid.</param>
public readonly unsafe record struct NriFenceSubmit(NriFence Fence, ulong Value, StageBits Stages = StageBits.ALL)
{
    internal FenceSubmitDesc ToNative() => new() { fence = Fence.Raw, value = Value, stages = Stages };
}
