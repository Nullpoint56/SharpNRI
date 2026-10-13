// NriQueryPool: timestamps written around a trivial piece of GPU work, copied into a readback
// buffer, and ordered; and the size of one timestamp result.

using System.Runtime.InteropServices;
using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

public sealed class QueryTests
{
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Timestamp_query_size_is_8_bytes(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        using var pool = device.CreateQueryPool(QueryType.TIMESTAMP, 2);

        Assert.Equal(QueryType.TIMESTAMP, pool.Type);
        Assert.Equal(2u, pool.Capacity);

        // NRIDescs.h: TIMESTAMP is "uint64_t".
        Assert.Equal(8u, pool.QuerySize);
        Assert.Equal(1, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Two_timestamps_around_GPU_work_are_ordered(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.timestamp == 0, $"{backend}: features.timestamp is false");

        using var pool = device.CreateQueryPool(QueryType.TIMESTAMP, 2);
        var querySize = pool.QuerySize;
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = 2 * querySize });
        using var source = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 65536 });
        using var destination = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 65536 });

        Run(device, cmd =>
        {
            cmd.ResetQueries(pool, 0, 2);
            cmd.EndQuery(pool, 0);
            cmd.CopyBuffer(destination, 0, source, 0, 65536);
            cmd.EndQuery(pool, 1);
            cmd.CopyQueries(pool, 0, 2, readback, 0);
        });

        var ticks = MemoryMarshal.Cast<byte, ulong>(readback.Map()).ToArray();
        readback.Unmap();

        var frequency = device.Desc.other.timestampFrequencyHz;
        Record($"{backend}: timestamps {ticks[0]} -> {ticks[1]} ({ticks[1] - ticks[0]} ticks at {frequency} Hz = {(ticks[1] - ticks[0]) * 1e9 / frequency:F0} ns)");

        // MoltenVK on the paravirtual Apple GPU reports timestampValidBits and writes zeros when
        // the copy is recorded in the same submission as the writes (findings 9.6). Measured here
        // rather than assumed: the same queries copied again by a later submission are recorded,
        // and the test skips only while the in-submission copy still reads zero there.
        if (TestDevice.IsAppleParavirtualVK(backend.Api) && ticks[0] == 0 && ticks[1] == 0)
        {
            Run(device, cmd => cmd.CopyQueries(pool, 0, 2, readback, 0));
            var later = MemoryMarshal.Cast<byte, ulong>(readback.Map()).ToArray();
            readback.Unmap();
            Record($"{backend}: the same queries copied by a later submission: {later[0]} -> {later[1]}");
            Assert.Skip($"MoltenVK on the Apple Paravirtual device writes 0 for both timestamps when they are copied in the submission that "
                + $"wrote them; a later submission copies {later[0]} -> {later[1]}");
        }

        Assert.NotEqual(0UL, ticks[0]);
        Assert.True(ticks[1] >= ticks[0], $"second timestamp {ticks[1]} < first {ticks[0]}");
        Assert.NotEqual(0UL, frequency);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Host_reset_of_an_idle_pool_is_accepted(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        using var pool = device.CreateQueryPool(QueryType.TIMESTAMP, 4);

        pool.Reset(0, 4);

        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
