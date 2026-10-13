// Small helpers shared by the per-object L2 tests: record one command buffer, submit it on the
// graphics queue with a fence, and wait. Plus the access/layout/stage triples the tests use, named
// after what they mean so a barrier reads as "from X to Y".

using PrismNRI.Native;
using Xunit;

namespace PrismNRI.Tests;

internal static class GpuWork
{
    /// <summary>Records <paramref name="record"/> into a fresh command buffer, submits it, waits for it.</summary>
    public static void Run(NriDevice device, Action<NriCommandBuffer> record, NriDescriptorPool? pool = null)
    {
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        using var fence = device.CreateFence();

        commandBuffer.Begin(pool);
        record(commandBuffer);
        commandBuffer.End();
        queue.Submit(commandBuffer, fence, 1);
        fence.Wait(1);
    }

    public static ulong Align(ulong value, ulong alignment) =>
        alignment <= 1 ? value : (value + alignment - 1) / alignment * alignment;

    /// <summary>Writes a line to the test's output, where a measured fact is recorded.</summary>
    public static void Record(string line) => TestContext.Current.TestOutputHelper?.WriteLine(line);

    // ---------------------------------------------------------------- access/stage pairs (buffers)
    // No HOST_READ/HOST_WRITE pair: NRI validation rejects host access in any buffer barrier
    // (IsAccessMaskSupported(BufferDesc) in Validation/CommandBufferVal.hpp); the fence wait is the
    // host synchronisation for buffers.

    public static AccessStage Nothing => new() { access = AccessBits.NONE, stages = StageBits.NONE };
    public static AccessStage CopySource => new() { access = AccessBits.COPY_SOURCE, stages = StageBits.COPY };
    public static AccessStage CopyDestination => new() { access = AccessBits.COPY_DESTINATION, stages = StageBits.COPY };
    public static AccessStage ComputeStorage => new() { access = AccessBits.SHADER_RESOURCE_STORAGE, stages = StageBits.COMPUTE_SHADER };

    // ---------------------------------------------------------------- access/layout/stage triples (textures)

    public static AccessLayoutStage Undefined => new() { access = AccessBits.NONE, layout = Layout.UNDEFINED, stages = StageBits.NONE };
    public static AccessLayoutStage TextureCopyDestination => new() { access = AccessBits.COPY_DESTINATION, layout = Layout.COPY_DESTINATION, stages = StageBits.COPY };
    public static AccessLayoutStage TextureCopySource => new() { access = AccessBits.COPY_SOURCE, layout = Layout.COPY_SOURCE, stages = StageBits.COPY };
    public static AccessLayoutStage ColorAttachment => new() { access = AccessBits.COLOR_ATTACHMENT, layout = Layout.COLOR_ATTACHMENT, stages = StageBits.COLOR_ATTACHMENT };
}
