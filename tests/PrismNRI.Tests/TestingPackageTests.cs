// PrismNRI.Testing, used the way a downstream test project would use it.

using PrismNRI.Native;
using PrismNRI.Testing;
using Xunit;

namespace PrismNRI.Tests;

public sealed class TestingPackageTests
{
    [Fact]
    public void NONE_is_always_available_and_records_nothing_bad()
    {
        Assert.Null(NriTestDevice.Unavailable(GraphicsAPI.NONE));
        using var fixture = NriTestDevice.Create(GraphicsAPI.NONE);
        Assert.Equal(GraphicsAPI.NONE, fixture.Api);
        Assert.Equal(GraphicsAPI.NONE, fixture.Device.GraphicsApi);
        fixture.ThrowIfErrors();
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_validation_error_is_listed_by_ThrowIfErrors(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        using var fixture = NriTestDevice.Create(backend.Api);

        // NRI's validation rejects a HOST_READ access in a buffer barrier (CLAUDE.md: Memory and resources); it
        // reports, returns, and the process keeps running (CLAUDE.md: Device creation and callbacks).
        var queue = fixture.Device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        using var buffer = fixture.Device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = 64 });
        commandBuffer.Begin();
        commandBuffer.Barrier(new NriBufferBarrier(buffer,
            new AccessStage { access = AccessBits.COPY_DESTINATION, stages = StageBits.COPY },
            new AccessStage { access = AccessBits.HOST_READ, stages = StageBits.HOST }));
        commandBuffer.End();

        var e = Assert.Throws<NriValidationException>(fixture.ThrowIfErrors);
        Assert.Equal(backend.Api, e.Api);
        Assert.NotEmpty(e.Errors);
        Assert.Contains("ValidateBufferBarrierDesc", e.Message);
        Assert.Contains(fixture.Messages, m => m.IsError);
    }

    [Fact]
    public void Unavailable_reasons_are_sentences()
    {
        foreach (var api in NriTestDevice.Backends)
        {
            var reason = NriTestDevice.Unavailable(api);
            if (reason is not null) Assert.False(string.IsNullOrWhiteSpace(reason));
            Assert.Equal(reason is null, NriTestDevice.IsAvailable(api));
        }
    }
}
