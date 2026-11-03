// Managed form of NRI's QueueFamilyD3D12Desc (NRIWrapperD3D12.h).
//
// NRI's struct points at an optional array of ID3D12CommandQueue*; this form holds them as nint
// in an array, with NRI's field names and order. Pure data (COM pointers, no NRI objects), so it
// lives in SharpNRI.Descs.

using SharpNRI.Native;

namespace SharpNRI;

/// <summary><c>QueueFamilyD3D12Desc</c>: a collection of queues of the same type, with the queues as nint.</summary>
/// <param name="D3D12Queues">Optional <c>ID3D12CommandQueue*</c> each, <paramref name="QueueNum"/> of them; NRI creates the queues when null.</param>
/// <param name="QueueNum">How many queues of this type.</param>
/// <param name="QueueType">Graphics, compute, copy, video decode or encode.</param>
public sealed record NriQueueFamilyD3D12Desc(nint[]? D3D12Queues, uint QueueNum, QueueType QueueType);
