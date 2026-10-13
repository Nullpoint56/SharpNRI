// L2 — a timeline fence.
//
// NRI's Fence is a timeline semaphore on Vulkan and an ID3D12Fence on D3D12: a monotonically
// increasing 64-bit value, waited on and signalled by value from both host and queues.

using PrismNRI.Native;

namespace PrismNRI;

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

    /// <summary>Blocks the calling thread until the fence reaches <paramref name="value"/>.</summary>
    public void Wait(ulong value) => Device.Core.Wait(Raw, value);

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
