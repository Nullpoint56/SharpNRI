// The interop layer's own paths (L1), each measured by its effect: a message handler or a host
// allocator that throws, which text an exception carries, reallocation, the scratch arena growing,
// the managed argument checks that stand between a short span and NRI, the shared handle's Dispose,
// and the WrapperVK mappings from Vulkan enums to the managed BindPoint and QueryType.
//
// Not here, because nothing reaches it without crashing: the allocation bridge's catch in
// OnAllocate (an allocator that throws makes the bridge return null, and NRI v190 calls Create on
// the null it gets back from Allocate: an access violation in CreateFence, 0xC0000005, on Vulkan
// and D3D12, measured in a child process).

using System.Runtime.InteropServices;
using SharpNRI.Native;
using Vortice.Vulkan;
using Xunit;
using static SharpNRI.Tests.GpuWork;

namespace SharpNRI.Tests;

public sealed unsafe class InteropTests
{
    private const uint Bytes = 256;

    private static int Count(List<NriMessage> messages)
    {
        lock (messages) return messages.Count;
    }

    private static List<NriMessage> Since(List<NriMessage> messages, int start)
    {
        lock (messages) return messages.Skip(start).ToList();
    }

    // ---------------------------------------------------------------- the message bridge

    /// <summary>
    /// A handler that throws loses that one message and nothing else (MessageBridge.OnMessage catches;
    /// no managed exception crosses NRI's frame): two NRI validation errors in a row, the handler
    /// throwing on the first, still delivers the second, and the device's own record, taken before the
    /// handler runs, has each in turn as LastError.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_message_handler_that_throws_loses_only_that_message(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var delivered = new List<NriMessage>();
        var errorsSeen = 0;
        using var device = NriDevice.Create(new NriDeviceOptions
        {
            GraphicsApi = backend.Api,
            Adapter = TestDevice.AdapterFor(backend.Api),
            EnableNriValidation = true,
            EnableGraphicsApiValidation = true,
            MessageHandler = (in NriMessage m) =>
            {
                if (m.IsError && Interlocked.Increment(ref errorsSeen) == 1) throw new InvalidOperationException("the handler fails on the first error");
                lock (delivered) delivered.Add(m);
            },
        });
        Assert.NotEmpty(delivered); // device creation's own messages went through before any throw

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var cmd = allocator.CreateCommandBuffer();
        cmd.Begin();
        var start = Count(delivered);
        cmd.Draw(3);                                  // outside a render pass: refused, handler throws
        var afterFirst = device.LastError;
        cmd.SetRootConstants(0, 0u);                  // no pipeline layout set: refused, delivered
        var afterSecond = device.LastError;
        cmd.End();
        var after = Since(delivered, start);
        foreach (var m in after) Record("delivered: " + m);
        Record($"LastError after the first: {afterFirst}");

        Assert.Equal(2, errorsSeen);
        Assert.Contains("must be called inside 'CmdBeginRendering/CmdEndRendering'", afterFirst, StringComparison.Ordinal);
        var second = Assert.Single(after, m => m.IsError);
        Assert.Contains("'SetPipelineLayout' has not been called", second.Text, StringComparison.Ordinal);
        Assert.Equal(second.Text, afterSecond);
    }

    /// <summary>
    /// An NriException carries the error text reported on its own thread since the binding last
    /// checked a result there, which is the failing call's own, and nothing older; device.LastError
    /// stays the device's last error from any thread. A CreateBuffer of size 0, refused by NRI
    /// validation, carries "'size' is 0". GetQueue with an index past the device's queues, which NRI
    /// refuses with INVALID_ARGUMENT and no message, then carries no text; it used to carry
    /// LastError, the CreateBuffer text (as a silent FAILURE of GetVideoAV1EncodeDecodeInfo carried
    /// an earlier EncodeVideo message). A Draw outside a render pass refused on another thread,
    /// never checked there, is not this thread's either. A Draw refused on this thread (Cmd* return
    /// nothing, so nothing checks them) is attached to the next failure here: no check came between.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void An_exception_carries_only_the_text_reported_for_its_own_call(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        const string sizeZero = "'size' is 0";
        const string outsidePass = "must be called inside 'CmdBeginRendering/CmdEndRendering'";

        var refused = Assert.Throws<NriException>(() => device.CreateBuffer(new BufferDesc { size = 0 }));
        Record($"{backend}: {refused.Message}");
        Assert.Equal(Result.INVALID_ARGUMENT, refused.Result);
        Assert.Contains(sizeZero, refused.Message, StringComparison.Ordinal);

        var silent = Assert.Throws<NriException>(() => device.GetQueue(QueueType.GRAPHICS, 1000));
        Assert.Equal("GetQueue: INVALID_ARGUMENT", silent.Message);
        Assert.Contains(sizeZero, device.LastError, StringComparison.Ordinal);

        using (var elsewhere = allocator.CreateCommandBuffer())
        {
            var thread = new Thread(() =>
            {
                elsewhere.Begin();
                elsewhere.Draw(3);
            });
            thread.Start();
            thread.Join();
            Assert.Contains(outsidePass, device.LastError, StringComparison.Ordinal);
            var afterOtherThread = Assert.Throws<NriException>(() => device.GetQueue(QueueType.GRAPHICS, 1000));
            Assert.Equal("GetQueue: INVALID_ARGUMENT", afterOtherThread.Message);
            elsewhere.End();
        }

        using (var here = allocator.CreateCommandBuffer())
        {
            here.Begin();
            here.Draw(3);
            var afterDraw = Assert.Throws<NriException>(() => device.GetQueue(QueueType.GRAPHICS, 1000));
            Record($"{backend}: {afterDraw.Message}");
            Assert.Equal(Result.INVALID_ARGUMENT, afterDraw.Result);
            Assert.Contains(outsidePass, afterDraw.Message, StringComparison.Ordinal);
            here.End();
        }

        List<NriMessage> errors;
        lock (messages) errors = [.. messages.Where(m => m.IsError)];
        Assert.Equal(3, errors.Count);
        Assert.All(errors, m => Assert.True(m.Text.Contains(sizeZero, StringComparison.Ordinal) || m.Text.Contains(outsidePass, StringComparison.Ordinal), m.ToString()));
    }

    // ---------------------------------------------------------------- the allocation bridge

    /// <summary>Counts through NriCountingAllocator; Free frees and then throws.</summary>
    private sealed class FreeThrows : INriAllocator
    {
        public readonly NriCountingAllocator Inner = new();
        public int Thrown;

        public void* Allocate(nuint size, nuint alignment) => Inner.Allocate(size, alignment);
        public void* Reallocate(void* memory, nuint size, nuint alignment) => Inner.Reallocate(memory, size, alignment);

        public void Free(void* memory)
        {
            Inner.Free(memory);
            if (memory is not null) Interlocked.Increment(ref Thrown);
            throw new InvalidOperationException("Free fails after freeing");
        }
    }

    /// <summary>
    /// An allocator whose Free throws: the bridge swallows each exception (AllocatorBridge.OnFree), NRI
    /// carries on, objects and the device dispose, and every allocation is freed. NONE included: this is
    /// the bridge's bookkeeping, which NONE exercises at device creation and destruction.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void An_allocator_whose_Free_throws_still_frees_everything(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var allocator = new FreeThrows();
        var messages = new List<NriMessage>();
        var device = TestDevice.Create(backend.Api, messages, b => b.Allocator = allocator);
        using (var fence = device.CreateFence())
        using (var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = Bytes }))
        {
            Assert.Equal(0UL, fence.Value);
        }
        device.Dispose();
        Record($"{backend}: {allocator.Inner.Allocations} allocations, {allocator.Inner.Frees} frees, {allocator.Thrown} of them threw");

        Assert.True(device.IsDisposed);
        Assert.True(allocator.Thrown > 0, "NRI freed nothing through the allocator");
        Assert.Equal(allocator.Inner.Frees, allocator.Thrown);
        Assert.Equal(0L, allocator.Inner.Live);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// NriCountingAllocator.Reallocate called directly: it grows a block and keeps its contents,
    /// honours the alignment, counts a reallocation, and with no block allocates (counted as an
    /// allocation, as Vulkan's pfnReallocation defines it for a null original).
    /// </summary>
    [Fact]
    public void NriCountingAllocator_Reallocate_grows_a_block_and_keeps_its_contents()
    {
        var allocator = new NriCountingAllocator();
        const nuint alignment = 64;
        var block = (byte*)allocator.Allocate(16, alignment);
        for (var i = 0; i < 16; i++) block[i] = (byte)(0xA0 + i);

        var grown = (byte*)allocator.Reallocate(block, 4096, alignment);
        Assert.True(grown is not null);
        Assert.Equal(0, (long)((nuint)grown % alignment));
        for (var i = 0; i < 16; i++) Assert.Equal((byte)(0xA0 + i), grown[i]);
        grown[4095] = 0x5A; // the whole new size is writable
        Assert.Equal((1L, 1L, 0L), (allocator.Allocations, allocator.Reallocations, allocator.Frees));

        var fresh = allocator.Reallocate(null, 32, alignment);
        Assert.True(fresh is not null);
        Assert.Equal((2L, 1L, 0L), (allocator.Allocations, allocator.Reallocations, allocator.Frees));

        allocator.Free(grown);
        allocator.Free(fresh);
        allocator.Free(null);
        Assert.Equal((2L, 1L, 2L, 0L), (allocator.Allocations, allocator.Reallocations, allocator.Frees, allocator.Live));
    }

    /// <summary>
    /// NriCountingAllocator.Reallocate to size 0, called directly, frees the block and returns null,
    /// counted as a free and not as a reallocation, so Live is back to 0 with no Free call: Vulkan's
    /// pfnReallocation contract, the one NRI forwards to the host allocator, and NRI's own Windows
    /// default (_aligned_realloc) both define size 0 so. Before, it kept a 1-byte block that nobody
    /// would free.
    /// </summary>
    [Fact]
    public void NriCountingAllocator_Reallocate_to_size_0_frees_the_block()
    {
        var allocator = new NriCountingAllocator();
        var block = (byte*)allocator.Allocate(64, 16);
        Assert.True(block is not null);
        block[63] = 0x5A;
        Assert.Equal(1L, allocator.Live);

        var result = allocator.Reallocate(block, 0, 16);

        Assert.True(result is null, $"Reallocate to size 0 returned a block at 0x{(nuint)result:X}");
        Assert.Equal((1L, 0L, 1L, 0L), (allocator.Allocations, allocator.Reallocations, allocator.Frees, allocator.Live));
    }

    /// <summary>
    /// ForwardAllocatorToThirdParty routes the third-party allocators through the host allocator too
    /// (VMA and the Vulkan loader, layers and driver through VkAllocationCallbacks; D3D12MA): the same
    /// work makes more host allocations with it than without, and device disposal frees every one,
    /// reallocations included (measured on the discrete GPU for a device and one buffer: Vulkan 31
    /// allocations without forwarding, 10,840 and 210 reallocations with it, none of size 0; D3D12 18
    /// and 26, no reallocations).
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Forwarded_third_party_allocations_are_counted_and_freed_with_the_device(Backend backend)
    {
        TestDevice.RequireAvailable(backend);

        NriCountingAllocator Run(bool forward)
        {
            var allocator = new NriCountingAllocator();
            var messages = new List<NriMessage>();
            using (var device = TestDevice.Create(backend.Api, messages, b => { b.Allocator = allocator; b.ForwardAllocatorToThirdParty = forward; }))
            using (var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = Bytes }))
            {
                Assert.Same(allocator, device.Allocator);
            }
            Assert.DoesNotContain(messages, m => m.IsError);
            return allocator;
        }

        var own = Run(forward: false);
        var forwarded = Run(forward: true);
        Record($"{backend}: NRI's own {own.Allocations} allocations / {own.Reallocations} reallocations; forwarded {forwarded.Allocations} / {forwarded.Reallocations}");

        Assert.True(forwarded.Allocations > own.Allocations, $"forwarding made {forwarded.Allocations} allocations, NRI alone {own.Allocations}");
        Assert.Equal(0L, own.Live);
        Assert.Equal(0L, forwarded.Live);
    }

    // ---------------------------------------------------------------- the scratch arena and argument checks

    /// <summary>
    /// UploadData marshals its descs through the per-thread scratch arena: 1,024 subresources (one
    /// R32_UINT texel in each layer of a 1,024-layer array) need 24 KiB of
    /// TextureSubresourceUploadDesc, more than the arena's first 16 KiB chunk, so the arena moves to a
    /// larger chunk. Every layer reads back its own value.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void UploadData_marshals_a_thousand_subresources_through_a_grown_scratch_chunk(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        const int layers = 1024;
        Assert.SkipWhen(device.Desc.dimensions.textureLayerMaxNum < layers, $"dimensions.textureLayerMaxNum = {device.Desc.dimensions.textureLayerMaxNum}, under {layers}");

        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.R32_UINT,
            width = 1, height = 1, depth = 1, mipNum = 1, layerNum = layers, sampleNum = 1,
        });
        var subresources = new NriTextureSubresourceUpload[layers];
        for (var i = 0; i < layers; i++) subresources[i] = new NriTextureSubresourceUpload(BitConverter.GetBytes(0xA1000000u | (uint)i), 1, 4, 4);
        device.GetQueue(QueueType.GRAPHICS).UploadData([new NriTextureUpload(texture, subresources, TextureCopySource)], []);

        var rowPitch = Align(4, device.Desc.memoryAlignment.uploadBufferTextureRow);
        var slicePitch = Align(rowPitch, device.Desc.memoryAlignment.uploadBufferTextureSlice);
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = layers * slicePitch });
        Run(device, cmd =>
        {
            for (var i = 0; i < layers; i++)
            {
                cmd.ReadbackTextureToBuffer(readback, new TextureDataLayoutDesc { offset = (ulong)i * slicePitch, rowPitch = (uint)rowPitch, slicePitch = (uint)slicePitch }, texture,
                    new TextureRegionDesc { width = 1, height = 1, depth = 1, layerOffset = (ushort)i, planes = PlaneBits.ALL });
            }
        });

        var bytes = readback.Map().ToArray();
        readback.Unmap();
        var wrong = Enumerable.Range(0, layers).Where(i => BitConverter.ToUInt32(bytes, (int)((ulong)i * slicePitch)) != (0xA1000000u | (uint)i)).ToList();
        var first = BitConverter.ToUInt32(bytes, 0);
        Assert.True(wrong.Count == 0, $"{wrong.Count} layers read back wrong, the first {string.Join(", ", wrong.Take(5))}; layer 0 holds 0x{first:X8}");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// UploadData refuses data shorter than NRI will read, before NRI is called: a subresource list
    /// shorter than the texture's layers times mips, a subresource with fewer bytes than its slices
    /// span, a buffer upload shorter than the buffer. Each throws ArgumentException naming the
    /// parameter, and the host allocator sees nothing (NRI's UploadData allocates its staging objects
    /// through it, which the valid call at the end shows).
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void UploadData_refuses_short_data_before_NRI_reads_it(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        var allocator = new NriCountingAllocator();
        using var device = TestDevice.Create(backend.Api, messages, b => b.Allocator = allocator);
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.RGBA8_UNORM,
            width = 2, height = 2, depth = 1, mipNum = 2, layerNum = 1, sampleNum = 1,
        });
        using var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = Bytes });
        var mip0 = new NriTextureSubresourceUpload(new byte[16], 1, 8, 16);
        var mip1 = new NriTextureSubresourceUpload(new byte[4], 1, 4, 4);
        var shortMip1 = new NriTextureSubresourceUpload(new byte[3], 1, 4, 4);

        var before = (allocator.Allocations, Count(messages));
        var tooFew = Assert.Throws<ArgumentException>(() => queue.UploadData([new NriTextureUpload(texture, [mip0], TextureCopySource)], []));
        var tooShort = Assert.Throws<ArgumentException>(() => queue.UploadData([new NriTextureUpload(texture, [mip0, shortMip1], TextureCopySource)], []));
        var shortBuffer = Assert.Throws<ArgumentException>(() => queue.UploadData([], [new NriBufferUpload(buffer, new byte[Bytes - 1], CopySource)]));
        var after = (allocator.Allocations, Count(messages));

        Assert.Equal("textures", tooFew.ParamName);
        Assert.Contains("1 subresources for 1 layers x 2 mips", tooFew.Message, StringComparison.Ordinal);
        Assert.Equal("textures", tooShort.ParamName);
        Assert.Contains("textures[0].Subresources[1]: 3 bytes for 1 slices at pitch 4", tooShort.Message, StringComparison.Ordinal);
        Assert.Equal("buffers", shortBuffer.ParamName);
        Assert.Contains($"buffers[0]: {Bytes - 1} bytes for a {Bytes}-byte buffer", shortBuffer.Message, StringComparison.Ordinal);
        Assert.Equal(before, after);

        queue.UploadData([new NriTextureUpload(texture, [mip0, mip1], TextureCopySource)], [new NriBufferUpload(buffer, new byte[Bytes], CopySource)]);
        Assert.True(allocator.Allocations > after.Allocations, "a valid UploadData allocated nothing through the host allocator, so the check above shows nothing");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    // ---------------------------------------------------------------- shared handles

    [DllImport("kernel32", SetLastError = true)]
    private static extern int GetHandleInformation(nint handle, out uint flags);

    [DllImport("kernel32", SetLastError = true)]
    private static extern nint CreateEventW(nint attributes, int manualReset, int initialState, nint name);

    [DllImport("kernel32", SetLastError = true)]
    private static extern int CloseHandle(nint handle);

    /// <summary>
    /// NriSharedHandle.Dispose closes the OS handle through NRI's DestroySharedHandle, once:
    /// GetHandleInformation accepts the exported fence handle before and refuses it after with
    /// ERROR_INVALID_HANDLE, and the disposed object reads as NRI's cleared struct (handle 0, type
    /// NONE) and invalid, in its properties and in ToString (what a log or debugger shows).
    /// Windows hands a closed value to the next handle the process opens
    /// (measured outside the suite: every time in 20,000 close-and-create pairs, also with four
    /// threads opening and closing handles meanwhile), so the events opened next get it back, and a
    /// second Dispose leaves them all open; the record struct NriSharedHandle was before closed the
    /// value again. A handle of 0 or -1 is not valid and Dispose leaves it alone.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Disposing_a_shared_handle_closes_it_once(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        Assert.SkipUnless(OperatingSystem.IsWindows(), "GetHandleInformation is Windows' (the handle is a file descriptor elsewhere)");
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.sharedFences == 0, "features.sharedFences is false: the adapter lacks VK_KHR_external_semaphore_win32 or exports no semaphore type");

        using var fence = device.CreateSharedFence();
        var handle = fence.GetSharedHandle();
        var value = handle.Handle;
        var type = handle.Type;
        Assert.True(handle.IsValid);
        Assert.Contains($"Handle = {value}, Type = {type}", handle.ToString(), StringComparison.Ordinal);
        Assert.NotEqual(0, GetHandleInformation(value, out _));

        handle.Dispose();
        Assert.Equal(0, GetHandleInformation(value, out _));
        Assert.Equal(6, Marshal.GetLastPInvokeError()); // ERROR_INVALID_HANDLE
        Assert.False(handle.IsValid);
        Assert.Equal(0, handle.Handle);
        Assert.Equal(SharedHandleType.NONE, handle.Type);
        Assert.Contains("Handle = 0, Type = NONE,", handle.ToString(), StringComparison.Ordinal);
        Assert.EndsWith("IsValid = False }", handle.ToString(), StringComparison.Ordinal);

        // New handles, kept open, until one has the closed value; then the second Dispose.
        var events = new List<nint>();
        try
        {
            while (events.Count < 256 && (events.Count == 0 || events[^1] != value))
            {
                var e = CreateEventW(0, 1, 0, 0);
                Assert.NotEqual(0, e);
                events.Add(e);
            }
            Record($"{backend}: the closed value 0x{value:X} came back as new handle {events.Count}");
            Assert.True(events[^1] == value, $"none of {events.Count} new handles took the closed value 0x{value:X}, so a second close could not be seen");

            handle.Dispose();
            Assert.All(events, e => Assert.NotEqual(0, GetHandleInformation(e, out _)));
        }
        finally
        {
            foreach (var e in events) CloseHandle(e);
        }

        Assert.False(new NriSharedHandle(device, 0, type, 0).IsValid);
        Assert.False(new NriSharedHandle(device, -1, type, 0).IsValid);
        new NriSharedHandle(device, 0, type, 0).Dispose();
        new NriSharedHandle(device, -1, type, 0).Dispose();
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// NriSharedHandle.Detach gives the handle up without closing it, for an import that takes
    /// ownership (NRI's header: "After an FD import takes ownership, clear SharedHandle instead of
    /// destroying it"): it returns the value, the object then reads as disposed, a later Dispose
    /// leaves the handle open (GetHandleInformation still accepts it, and the caller's CloseHandle
    /// succeeds), and a second Detach throws. File descriptor 0 is valid (NRI's header says so), an
    /// NT handle 0 is not; the FD-0 object is detached, not disposed, so stdin stays open.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Detaching_a_shared_handle_gives_it_up_without_closing_it(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        Assert.SkipUnless(OperatingSystem.IsWindows(), "GetHandleInformation is Windows' (the handle is a file descriptor elsewhere)");
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.sharedFences == 0, "features.sharedFences is false: the adapter lacks VK_KHR_external_semaphore_win32 or exports no semaphore type");

        using var fence = device.CreateSharedFence();
        var handle = fence.GetSharedHandle();
        var value = handle.Handle;
        Assert.True(handle.IsValid);

        Assert.Equal(value, handle.Detach());
        Assert.False(handle.IsValid);
        Assert.Equal(0, handle.Handle);
        Assert.Equal(SharedHandleType.NONE, handle.Type);
        Assert.NotEqual(0, GetHandleInformation(value, out _));

        handle.Dispose();
        Assert.NotEqual(0, GetHandleInformation(value, out _));
        Assert.Throws<ObjectDisposedException>(() => handle.Detach());
        Assert.NotEqual(0, CloseHandle(value));

        var disposed = fence.GetSharedHandle();
        disposed.Dispose();
        Assert.Throws<ObjectDisposedException>(() => disposed.Detach());

        var fd0 = new NriSharedHandle(device, 0, SharedHandleType.OPAQUE_FD, 0);
        Assert.True(fd0.IsValid);
        Assert.Equal(0, fd0.Detach());
        Assert.False(new NriSharedHandle(device, 0, SharedHandleType.OPAQUE_WIN32, 0).IsValid);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// DestroySharedHandle takes no device (NRI f18c6df9: a helper that closes the handle by its
    /// type), so a handle exported by a device that is gone still closes on Dispose: the fence and
    /// the device are disposed first, the handle stays open, and Dispose closes it.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_shared_handle_outlives_its_device_and_still_closes(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        Assert.SkipUnless(OperatingSystem.IsWindows(), "GetHandleInformation is Windows' (the handle is a file descriptor elsewhere)");
        var messages = new List<NriMessage>();
        NriSharedHandle handle;
        using (var device = TestDevice.Create(backend.Api, messages))
        {
            Assert.SkipWhen(device.Desc.features.sharedFences == 0, "features.sharedFences is false: the adapter lacks VK_KHR_external_semaphore_win32 or exports no semaphore type");
            using var fence = device.CreateSharedFence();
            handle = fence.GetSharedHandle();
        }

        var value = handle.Handle;
        Assert.True(handle.IsValid);
        Assert.NotEqual(0, GetHandleInformation(value, out _));
        handle.Dispose();
        Assert.Equal(0, GetHandleInformation(value, out _));
        Assert.Equal(6, Marshal.GetLastPInvokeError()); // ERROR_INVALID_HANDLE
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    // ---------------------------------------------------------------- WrapperVK mappings

    private static bool HasDeviceExtension(VkInstanceApi instanceApi, VkPhysicalDevice physicalDevice, string name)
    {
        uint count = 0;
        instanceApi.vkEnumerateDeviceExtensionProperties(physicalDevice, null, &count, null);
        var properties = new VkExtensionProperties[count];
        fixed (VkExtensionProperties* p = properties) instanceApi.vkEnumerateDeviceExtensionProperties(physicalDevice, null, &count, p);
        foreach (var property in properties)
        {
            var copy = property;
            if (Marshal.PtrToStringUTF8((nint)copy.extensionName) == name) return true;
        }
        return false;
    }

    private static void Check(VkResult result, string call) => Assert.True(result == VkResult.Success, $"{call}: {result}");


    /// <summary>
    /// CreateQueryPoolVK maps the VkQueryType of a raw pool made on NRI's own VkDevice to the
    /// QueryType the wrapper reports, and NRI agrees: the query size of the wrapped pool is that of a
    /// pool NRI creates of the same type (88 or 104 bytes for pipeline statistics, 8 otherwise). Every
    /// type the device can create: occlusion, pipeline statistics, timestamp, the acceleration-structure
    /// compacted size and size (VK_KHR_ray_tracing_maintenance1), the micromap compacted size. A type
    /// the device advertises but whose pool the driver refuses is recorded and left out (measured on
    /// the development machine: VK_QUERY_TYPE_MICROMAP_COMPACTED_SIZE_EXT, vkCreateQueryPool returns
    /// VK_ERROR_UNKNOWN with tiers.rayTracing = 3, for NRI's own pool and a raw one alike, and the
    /// Khronos layer says nothing). Before the binding's fix the size and micromap types were mapped
    /// from the neighbouring registry values (1000386000 and 1000396000, the serialization queries), and
    /// a raw VK_QUERY_TYPE_ACCELERATION_STRUCTURE_SIZE_KHR pool was reported as TIMESTAMP.
    /// </summary>
    [Fact]
    public void CreateQueryPoolVK_maps_each_Vulkan_query_type()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.VK, messages);
        var vk = device.WrapperVK;
        var instance = new VkInstance(vk.GetInstanceVK());
        var api = VulkanApi.For(instance, new Vortice.Vulkan.VkDevice(device.GetNativeObject()));
        ref readonly var desc = ref device.Desc;
        var maintenance1 = HasDeviceExtension(VulkanApi.For(instance), new VkPhysicalDevice(vk.GetPhysicalDeviceVK()), "VK_KHR_ray_tracing_maintenance1");

        // The Vulkan values from Vortice's VkQueryType (the registry's), not from the binding.
        (int VkType, QueryType Expected, bool Supported)[] cases =
        [
            ((int)VkQueryType.Occlusion, QueryType.OCCLUSION, desc.features.occlusion != 0),
            ((int)VkQueryType.PipelineStatistics, QueryType.PIPELINE_STATISTICS, desc.features.pipelineStatistics != 0),
            ((int)VkQueryType.Timestamp, QueryType.TIMESTAMP, desc.features.timestamp != 0),
            ((int)VkQueryType.AccelerationStructureCompactedSizeKHR, QueryType.ACCELERATION_STRUCTURE_COMPACTED_SIZE, desc.tiers.rayTracing != 0),
            ((int)VkQueryType.AccelerationStructureSizeKHR, QueryType.ACCELERATION_STRUCTURE_SIZE, desc.tiers.rayTracing != 0 && maintenance1),
            ((int)VkQueryType.MicromapCompactedSizeEXT, QueryType.MICROMAP_COMPACTED_SIZE, desc.tiers.rayTracing >= 3),
        ];
        var mapped = new List<QueryType>();
        var refusals = new List<NriMessage>();
        foreach (var (vkType, expected, supported) in cases)
        {
            if (!supported)
            {
                Record($"{expected}: not supported by the device, not created");
                continue;
            }
            var start = Count(messages);
            NriQueryPool own;
            try
            {
                own = device.CreateQueryPool(expected, 2);
            }
            catch (NriException e) when (e.Result == Result.FAILURE)
            {
                var refusal = Since(messages, start);
                Assert.All(refusal, m => Assert.Contains("vkCreateQueryPool(): failed", m.Text, StringComparison.Ordinal));
                refusals.AddRange(refusal);
                Record($"{expected}: the driver refuses the pool NRI creates ({e.Message}); nothing to wrap");
                continue;
            }
            using var ownPool = own;
            var info = new VkQueryPoolCreateInfo
            {
                queryType = (VkQueryType)vkType,
                queryCount = 2,
                // NRI's own set for its pipeline-statistics pools (QueryPoolVK.hpp), without the mesh bits.
                pipelineStatistics = vkType == 1 ? (VkQueryPipelineStatisticFlags)0x7FF : 0,
            };
            VkQueryPool pool;
            Check(api.vkCreateQueryPool(&info, null, &pool), $"vkCreateQueryPool({expected})");
            try
            {
                using var wrapped = vk.CreateQueryPoolVK(new QueryPoolVKDesc { vkQueryPool = pool.Handle, vkQueryType = vkType }, 2);
                Record($"{expected} (VkQueryType {vkType}): wrapped {wrapped.Type}, query size {wrapped.QuerySize}; NRI's own pool {ownPool.QuerySize}");
                Assert.Equal(expected, wrapped.Type);
                Assert.Equal(2u, wrapped.Capacity);
                Assert.Equal(ownPool.QuerySize, wrapped.QuerySize);
                mapped.Add(expected);
            }
            finally
            {
                api.vkDestroyQueryPool(pool, null);
            }
        }
        Assert.Contains(QueryType.TIMESTAMP, mapped);
        List<NriMessage> all;
        lock (messages) all = [.. messages];
        Assert.DoesNotContain(all.Except(refusals), m => m.IsError);
    }

    /// <summary>
    /// CreatePipelineVK with a graphics VkPipeline made on NRI's VkDevice (Triangle.hlsl, dynamic
    /// rendering, the viewport and scissor state NRI's own pipelines declare) reports the GRAPHICS bind
    /// point, and NRI draws with it: the triangle is in the readback and the corners keep the clear colour.
    /// </summary>
    [Fact]
    public void CreatePipelineVK_maps_a_graphics_VkPipeline_and_NRI_draws_with_it()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.VK, messages);
        var vk = device.WrapperVK;
        var api = VulkanApi.For(new VkInstance(vk.GetInstanceVK()), new Vortice.Vulkan.VkDevice(device.GetNativeObject()));
        const ushort size = 64;

        var cleanup = new List<Action>();
        try
        {
            VkShaderModule Module(string name)
            {
                var spirv = PipelineTests.Bytecode(device, name);
                VkShaderModule module;
                fixed (byte* code = spirv)
                {
                    var info = new VkShaderModuleCreateInfo { codeSize = (nuint)spirv.Length, pCode = (uint*)code };
                    Check(api.vkCreateShaderModule(&info, null, &module), "vkCreateShaderModule");
                }
                { var copy = module; cleanup.Add(() => api.vkDestroyShaderModule(copy, null)); }
                return module;
            }

            var vertexModule = Module("Triangle.vs");
            var fragmentModule = Module("Triangle.ps");
            var layoutInfo = new VkPipelineLayoutCreateInfo();
            VkPipelineLayout vkLayout;
            Check(api.vkCreatePipelineLayout(&layoutInfo, null, &vkLayout), "vkCreatePipelineLayout");
            { var copy = vkLayout; cleanup.Add(() => api.vkDestroyPipelineLayout(copy, null)); }

            var extended = device.Desc.features.extendedDynamicState != 0;
            VkPipeline vkPipeline;
            fixed (byte* vsMain = "vsMain"u8)
            fixed (byte* psMain = "psMain"u8)
            {
                var stages = stackalloc VkPipelineShaderStageCreateInfo[2];
                stages[0] = new VkPipelineShaderStageCreateInfo { stage = VkShaderStageFlags.Vertex, module = vertexModule, pName = vsMain };
                stages[1] = new VkPipelineShaderStageCreateInfo { stage = VkShaderStageFlags.Fragment, module = fragmentModule, pName = psMain };
                var vertexInput = new VkPipelineVertexInputStateCreateInfo();
                var inputAssembly = new VkPipelineInputAssemblyStateCreateInfo { topology = VkPrimitiveTopology.TriangleList };
                // NRI's pipelines declare the counts dynamic with extendedDynamicState (PipelineVK.hpp) and
                // its SetViewports/SetScissors call the *WithCount forms then.
                var viewport = new VkPipelineViewportStateCreateInfo { viewportCount = extended ? 0u : 1u, scissorCount = extended ? 0u : 1u };
                var rasterization = new VkPipelineRasterizationStateCreateInfo { polygonMode = VkPolygonMode.Fill, cullMode = VkCullModeFlags.None, frontFace = VkFrontFace.Clockwise, lineWidth = 1 };
                var multisample = new VkPipelineMultisampleStateCreateInfo { rasterizationSamples = VkSampleCountFlags.Count1 };
                var attachment = new VkPipelineColorBlendAttachmentState { colorWriteMask = VkColorComponentFlags.R | VkColorComponentFlags.G | VkColorComponentFlags.B | VkColorComponentFlags.A };
                var blend = new VkPipelineColorBlendStateCreateInfo { attachmentCount = 1, pAttachments = &attachment };
                var dynamicStates = stackalloc VkDynamicState[2];
                dynamicStates[0] = extended ? VkDynamicState.ViewportWithCount : VkDynamicState.Viewport;
                dynamicStates[1] = extended ? VkDynamicState.ScissorWithCount : VkDynamicState.Scissor;
                var dynamicState = new VkPipelineDynamicStateCreateInfo { dynamicStateCount = 2, pDynamicStates = dynamicStates };
                var format = VkFormat.R8G8B8A8Unorm;
                var rendering = new VkPipelineRenderingCreateInfo { colorAttachmentCount = 1, pColorAttachmentFormats = &format };
                var info = new VkGraphicsPipelineCreateInfo
                {
                    pNext = &rendering,
                    stageCount = 2,
                    pStages = stages,
                    pVertexInputState = &vertexInput,
                    pInputAssemblyState = &inputAssembly,
                    pViewportState = &viewport,
                    pRasterizationState = &rasterization,
                    pMultisampleState = &multisample,
                    pColorBlendState = &blend,
                    pDynamicState = &dynamicState,
                    layout = vkLayout,
                };
                Check(api.vkCreateGraphicsPipelines(VkPipelineCache.Null, 1, &info, null, &vkPipeline), "vkCreateGraphicsPipelines");
            }
            { var copy = vkPipeline; cleanup.Add(() => api.vkDestroyPipeline(copy, null)); }

            var pipeline = vk.CreatePipelineVK(new PipelineVKDesc { vkPipeline = vkPipeline.Handle, vkPipelineBindPoint = (int)VkPipelineBindPoint.Graphics });
            cleanup.Add(pipeline.Dispose);
            Assert.Equal(BindPoint.GRAPHICS, pipeline.BindPoint);

            var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc { ShaderStages = StageBits.VERTEX_SHADER | StageBits.FRAGMENT_SHADER });
            cleanup.Add(layout.Dispose);
            var target = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
            {
                type = TextureType.TEXTURE_2D, usage = TextureUsageBits.COLOR_ATTACHMENT, format = Format.RGBA8_UNORM,
                width = size, height = size, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
            });
            cleanup.Add(target.Dispose);
            var view = target.CreateView(TextureView.COLOR_ATTACHMENT, Format.RGBA8_UNORM);
            cleanup.Add(view.Dispose);
            var rowPitch = (uint)Align(size * 4u, device.Desc.memoryAlignment.uploadBufferTextureRow);
            var slicePitch = (uint)Align((ulong)rowPitch * size, device.Desc.memoryAlignment.uploadBufferTextureSlice);
            var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = slicePitch });
            cleanup.Add(readback.Dispose);

            Run(device, cmd =>
            {
                cmd.Barrier(new NriTextureBarrier(target, Undefined, ColorAttachment));
                var clear = new ClearValue();
                clear.color.f = new Color32f { x = 0, y = 0, z = 0, w = 1 };
                cmd.BeginRendering([new NriAttachment(view, clear, LoadOp.CLEAR, StoreOp.STORE)]);
                cmd.SetViewport(new Viewport { width = size, height = size, depthMax = 1 });
                cmd.SetScissor(new Rect { width = size, height = size });
                cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
                cmd.SetPipeline(pipeline);
                cmd.Draw(3);
                cmd.EndRendering();
                cmd.Barrier(new NriTextureBarrier(target, ColorAttachment, TextureCopySource));
                cmd.ReadbackTextureToBuffer(readback, new TextureDataLayoutDesc { rowPitch = rowPitch, slicePitch = slicePitch }, target,
                    new TextureRegionDesc { width = size, height = size, depth = 1, planes = PlaneBits.COLOR });
            });

            var pixels = readback.Map().ToArray();
            readback.Unmap();
            (byte R, byte G, byte B, byte A) Pixel(int x, int y)
            {
                var i = (int)(y * rowPitch) + x * 4;
                return (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
            }
            Assert.Equal(((byte)0, (byte)0, (byte)0, (byte)255), Pixel(0, 0));
            Assert.Equal(((byte)0, (byte)0, (byte)0, (byte)255), Pixel(size - 1, size - 1));
            var top = Pixel(32, 20);
            Assert.True(top.R > top.G && top.R > top.B, $"near the red vertex: {top}");
            var centre = Pixel(32, 36);
            Assert.True(centre.R + centre.G + centre.B > 150, $"centre: {centre}");
            device.WaitIdle();
        }
        finally
        {
            for (var i = cleanup.Count - 1; i >= 0; i--) cleanup[i]();
        }
        Assert.Equal(0, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// CreatePipelineVK with a ray-tracing VkPipeline made on NRI's VkDevice (RayPipeline.hlsl's three
    /// entry points in three groups) reports the RAY_TRACING bind point, and NRI reads its shader group
    /// identifiers: WriteShaderGroupIdentifiers on the wrapper, packed and at a wider stride, gives the
    /// bytes vkGetRayTracingShaderGroupHandlesKHR gives for the raw pipeline.
    /// </summary>
    [Fact]
    public void CreatePipelineVK_maps_a_ray_tracing_VkPipeline_and_NRI_reads_its_identifiers()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.VK, messages);
        Assert.SkipWhen(device.Desc.tiers.rayTracing < 2, $"tiers.rayTracing = {device.Desc.tiers.rayTracing}: no ray-tracing pipelines (needs 2)");
        var vk = device.WrapperVK;
        var api = VulkanApi.For(new VkInstance(vk.GetInstanceVK()), new Vortice.Vulkan.VkDevice(device.GetNativeObject()));
        var identifierSize = (int)device.Desc.shaderStage.rayTracing.shaderGroupIdentifierSize;
        const int groups = 3;

        var cleanup = new List<Action>();
        try
        {
            // RayPipeline.hlsl: set 0, binding 0 the TLAS and binding 1 the hit buffer, ray generation only.
            var bindings = stackalloc VkDescriptorSetLayoutBinding[2];
            bindings[0] = new VkDescriptorSetLayoutBinding { binding = 0, descriptorType = VkDescriptorType.AccelerationStructureKHR, descriptorCount = 1, stageFlags = VkShaderStageFlags.RaygenKHR };
            bindings[1] = new VkDescriptorSetLayoutBinding { binding = 1, descriptorType = VkDescriptorType.StorageBuffer, descriptorCount = 1, stageFlags = VkShaderStageFlags.RaygenKHR };
            var setLayoutInfo = new VkDescriptorSetLayoutCreateInfo { bindingCount = 2, pBindings = bindings };
            VkDescriptorSetLayout setLayout;
            Check(api.vkCreateDescriptorSetLayout(&setLayoutInfo, null, &setLayout), "vkCreateDescriptorSetLayout");
            { var copy = setLayout; cleanup.Add(() => api.vkDestroyDescriptorSetLayout(copy, null)); }
            var layoutInfo = new VkPipelineLayoutCreateInfo { setLayoutCount = 1, pSetLayouts = &setLayout };
            VkPipelineLayout vkLayout;
            Check(api.vkCreatePipelineLayout(&layoutInfo, null, &vkLayout), "vkCreatePipelineLayout");
            { var copy = vkLayout; cleanup.Add(() => api.vkDestroyPipelineLayout(copy, null)); }

            var spirv = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", "RayPipeline.lib.spv"));
            VkShaderModule module;
            fixed (byte* code = spirv)
            {
                var moduleInfo = new VkShaderModuleCreateInfo { codeSize = (nuint)spirv.Length, pCode = (uint*)code };
                Check(api.vkCreateShaderModule(&moduleInfo, null, &module), "vkCreateShaderModule");
            }
            { var copy = module; cleanup.Add(() => api.vkDestroyShaderModule(copy, null)); }

            VkPipeline vkPipeline;
            fixed (byte* rayGen = "rayGen"u8)
            fixed (byte* miss = "miss"u8)
            fixed (byte* closestHit = "closestHit"u8)
            {
                var stages = stackalloc VkPipelineShaderStageCreateInfo[3];
                stages[0] = new VkPipelineShaderStageCreateInfo { stage = VkShaderStageFlags.RaygenKHR, module = module, pName = rayGen };
                stages[1] = new VkPipelineShaderStageCreateInfo { stage = VkShaderStageFlags.MissKHR, module = module, pName = miss };
                stages[2] = new VkPipelineShaderStageCreateInfo { stage = VkShaderStageFlags.ClosestHitKHR, module = module, pName = closestHit };
                const uint unused = ~0u; // VK_SHADER_UNUSED_KHR
                var shaderGroups = stackalloc VkRayTracingShaderGroupCreateInfoKHR[groups];
                shaderGroups[0] = new VkRayTracingShaderGroupCreateInfoKHR { type = VkRayTracingShaderGroupTypeKHR.General, generalShader = 0, closestHitShader = unused, anyHitShader = unused, intersectionShader = unused };
                shaderGroups[1] = new VkRayTracingShaderGroupCreateInfoKHR { type = VkRayTracingShaderGroupTypeKHR.General, generalShader = 1, closestHitShader = unused, anyHitShader = unused, intersectionShader = unused };
                shaderGroups[2] = new VkRayTracingShaderGroupCreateInfoKHR { type = VkRayTracingShaderGroupTypeKHR.TrianglesHitGroup, generalShader = unused, closestHitShader = 2, anyHitShader = unused, intersectionShader = unused };
                var info = new VkRayTracingPipelineCreateInfoKHR
                {
                    stageCount = 3,
                    pStages = stages,
                    groupCount = groups,
                    pGroups = shaderGroups,
                    maxPipelineRayRecursionDepth = 1,
                    layout = vkLayout,
                };
                Check(api.vkCreateRayTracingPipelinesKHR(VkDeferredOperationKHR.Null, VkPipelineCache.Null, 1, &info, null, &vkPipeline), "vkCreateRayTracingPipelinesKHR");
            }
            { var copy = vkPipeline; cleanup.Add(() => api.vkDestroyPipeline(copy, null)); }

            var expected = new byte[groups * identifierSize];
            fixed (byte* p = expected) Check(api.vkGetRayTracingShaderGroupHandlesKHR(vkPipeline, 0, groups, (nuint)expected.Length, p), "vkGetRayTracingShaderGroupHandlesKHR");

            var pipeline = vk.CreatePipelineVK(new PipelineVKDesc { vkPipeline = vkPipeline.Handle, vkPipelineBindPoint = (int)VkPipelineBindPoint.RayTracingKHR });
            cleanup.Add(pipeline.Dispose);
            Assert.Equal(BindPoint.RAY_TRACING, pipeline.BindPoint);

            var packed = new byte[groups * identifierSize];
            device.RayTracing.WriteShaderGroupIdentifiers(pipeline, 0, groups, (uint)identifierSize, packed);
            Assert.Equal(expected, packed);
            var stride = identifierSize + 32;
            var strided = new byte[groups * stride];
            device.RayTracing.WriteShaderGroupIdentifiers(pipeline, 0, groups, (uint)stride, strided);
            for (var g = 0; g < groups; g++)
                Assert.Equal(expected.AsSpan(g * identifierSize, identifierSize).ToArray(), strided.AsSpan(g * stride, identifierSize).ToArray());
        }
        finally
        {
            for (var i = cleanup.Count - 1; i >= 0; i--) cleanup[i]();
        }
        Assert.Equal(0, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
