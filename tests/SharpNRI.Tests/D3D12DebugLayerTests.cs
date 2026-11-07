// The D3D12 debug layer's two device-creation switches, patches 0018 and 0019.
//
// Break on error (0018): NRI calls ID3D12InfoQueue::SetBreakOnSeverity(ERROR and CORRUPTION) on
// every device created with enableGraphicsAPIValidation; with the Agility SDK it also registers
// an info-queue message callback that reports every debug-layer error as an NRI ERROR message.
// With the break on and no debugger, the first debug-layer error is a DebugBreak inside the NRI
// call. The binding asks for the break only with BreakOnError and a debugger attached, so here
// the error arrives as a message.
//
// GPU-based validation (0019): ID3D12Debug1::SetEnableGPUBasedValidation before the device is
// created. Process-global and never switched off again, so each case runs in a child process
// (this test executable started again with SHARPNRI_TEST_CHILD set; the module initializer below
// runs the case and exits before xunit starts): every D3D12 device created later in the test
// process would otherwise run under GPU-based validation.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using SharpNRI.Native;
using SharpNRI.Testing;
using Xunit;
using static SharpNRI.Tests.GpuWork;
using V12 = Vortice.Direct3D12;
using V12Debug = Vortice.Direct3D12.Debug;

namespace SharpNRI.Tests;

public sealed class D3D12DebugLayerTests(ITestOutputHelper output)
{
    private const string ChildVariable = "SHARPNRI_TEST_CHILD";
    private const string GbvOn = "d3d12-gbv-on";
    private const string GbvOff = "d3d12-gbv-off";
    private const int ThreadCount = 64; // [numthreads(64, 1, 1)] in GpuValidation.hlsl

    /// <summary>Why the device has no debug layer (no ID3D12InfoQueue), or null when it has one.</summary>
    private static string? NoDebugLayer(NriDevice device)
    {
        System.Runtime.InteropServices.Marshal.AddRef(device.GetNativeObject()); // released by the Vortice wrapper's Dispose
        using var d3d = new V12.ID3D12Device(device.GetNativeObject());
        using var infoQueue = d3d.QueryInterfaceOrNull<V12Debug.ID3D12InfoQueue>();
        return infoQueue is null ? "the D3D12 device has no ID3D12InfoQueue: the debug layer (d3d12SDKLayers.dll) did not load" : null;
    }

    /// <summary>
    /// With NRI's validation off and the debug layer on, a CopyBufferRegion of 512 bytes into a
    /// 256-byte buffer is the debug layer's to refuse, as message 841 ("The destination region
    /// extends past the end of the destination resource"). It arrives as an NRI ERROR message in
    /// NRI's "[id] text" form and the copy returns: the binding passes disableD3D12BreakOnError
    /// (patch 0018) because no debugger is attached. With the break on (measured once by forcing
    /// it) the same copy threw SEHException out of CopyBuffer, after the message. The list is then
    /// unusable either way: Close returns E_INVALIDARG.
    /// </summary>
    [Fact]
    public void A_D3D12_debug_layer_error_arrives_as_a_message_without_a_break()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.D3D12));
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.D3D12, messages, o => o.EnableNriValidation = false);
        var noDebugLayer = NoDebugLayer(device);
        Assert.SkipWhen(noDebugLayer is not null, noDebugLayer ?? "");

        using var source = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 512 });
        using var destination = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 256 });
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        commandBuffer.Begin();
        var before = messages.Count;
        commandBuffer.CopyBuffer(destination, 0, source, 0, 512);
        var provoked = messages.Skip(before).ToList();
        foreach (var m in provoked) Record("message: " + m);

        var error = Assert.Single(provoked, m => m.IsError);
        Assert.Contains("[841] ID3D12CommandList::CopyBufferRegion: The destination region extends past the end of the destination resource", error.Text, StringComparison.Ordinal);

        var close = Assert.Throws<NriException>(commandBuffer.End);
        Record("End: " + close.Message);
        Assert.Equal(Result.INVALID_ARGUMENT, close.Result);
    }

    /// <summary>
    /// GPU-based validation is on when asked for (patch 0019): a PARTIALLY_BOUND descriptor table
    /// range of two storage buffers with only slot 0 written, and a dispatch that writes through
    /// both (Shaders/GpuValidation.hlsl). PARTIALLY_BOUND makes the range DESCRIPTORS_VOLATILE, so
    /// the debug layer's CPU-side checks leave it alone (on a static range they report the
    /// never-written slot at SetComputeRootDescriptorTable, [646], and at Dispatch, [1023], with or
    /// without GPU-based validation); only GPU-based validation, which instruments the shader, sees
    /// the access through slot 1 when the work executes: "[938] GPU-BASED VALIDATION: Dispatch,
    /// Uninitialized descriptor accessed: ... Descriptor Heap Index FromTableStart: [1]" (NVIDIA and
    /// WARP, Agility 1.619). The positive control is the same
    /// work in a second child with the option off: no error. Each runs in its own process because
    /// the setting is process-global.
    /// </summary>
    [Fact]
    public void GPU_based_validation_reports_a_never_written_descriptor_and_only_when_asked_for()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.D3D12));

        var on = RunChild(GbvOn);
        var off = RunChild(GbvOff);
        var skip = on.Concat(off).FirstOrDefault(l => l.StartsWith("SKIP ", StringComparison.Ordinal));
        Assert.SkipWhen(skip is not null, skip?[5..] ?? "");

        foreach (var line in on.Concat(off).Where(l => l.StartsWith("RESULT ", StringComparison.Ordinal))) Record(line);
        var onErrors = on.Where(l => l.StartsWith("ERROR ", StringComparison.Ordinal)).ToList();
        var offErrors = off.Where(l => l.StartsWith("ERROR ", StringComparison.Ordinal)).ToList();
        Assert.True(offErrors.Count == 0, "GPU-based validation off, yet: " + string.Join(" | ", offErrors));
        Assert.True(onErrors.Count > 0, "GPU-based validation on and no error for the access through the never-written descriptor");
        Assert.All(onErrors, l => Assert.Contains("[938] GPU-BASED VALIDATION: Dispatch, Uninitialized descriptor accessed", l, StringComparison.Ordinal));
    }

    // ---------------------------------------------------------------- what the debug layer refuses in barriers and recording

    private static AccessLayoutStage Triple(AccessBits access, Layout layout, StageBits stages) => new() { access = access, layout = layout, stages = stages };
    private static readonly AccessLayoutStage ShaderRead = Triple(AccessBits.SHADER_RESOURCE, Layout.SHADER_RESOURCE, StageBits.ALL_SHADERS);

    /// <summary>The six texture barriers measured against message 1331 (AGENTS.md: Barriers), by name.</summary>
    private static readonly Dictionary<string, (AccessLayoutStage Before, AccessLayoutStage After)> NoAccessBarriers = new()
    {
        ["before NONE access, COPY_DESTINATION layout, COPY stages"] = (Triple(AccessBits.NONE, Layout.COPY_DESTINATION, StageBits.COPY), ShaderRead),
        ["before NONE access, COPY_DESTINATION layout, NONE stages"] = (Triple(AccessBits.NONE, Layout.COPY_DESTINATION, StageBits.NONE), ShaderRead),
        ["after NONE access, SHADER_RESOURCE layout, ALL_SHADERS stages"] = (TextureCopyDestination, Triple(AccessBits.NONE, Layout.SHADER_RESOURCE, StageBits.ALL_SHADERS)),
        ["after NONE access, SHADER_RESOURCE layout, NONE stages"] = (TextureCopyDestination, Triple(AccessBits.NONE, Layout.SHADER_RESOURCE, StageBits.NONE)),
        ["before NONE access, GENERAL layout, COPY stages; GENERAL after"] = (Triple(AccessBits.NONE, Layout.GENERAL, StageBits.COPY), Triple(AccessBits.SHADER_RESOURCE, Layout.GENERAL, StageBits.ALL_SHADERS)),
        ["before NONE access, SHADER_RESOURCE layout, ALL_SHADERS stages; SHADER_RESOURCE after"] = (Triple(AccessBits.NONE, Layout.SHADER_RESOURCE, StageBits.ALL_SHADERS), ShaderRead),
    };

    public static TheoryData<Backend, string> NoAccessBarrierCases
    {
        get
        {
            var data = new TheoryData<Backend, string>();
            foreach (var api in new[] { GraphicsAPI.VK, GraphicsAPI.D3D12 })
                foreach (var name in NoAccessBarriers.Keys)
                {
                    data.Add(new Backend(api), name);
                }
            return data;
        }
    }

    /// <summary>
    /// A texture barrier side with access NONE and a layout other than UNDEFINED. The debug layer
    /// refuses NO_ACCESS with a defined layout and any sync but SYNC_NONE: "[1331]
    /// ID3D12CommandList::Barrier: Invalid LayoutBefore value ... LayoutBefore must be
    /// LAYOUT_UNDEFINED or SyncBefore must be SYNC_NONE when AccessBefore is ACCESS_NO_ACCESS",
    /// then Close fails with E_INVALIDARG; NRI v190 derived the sync from the stages alone and drew
    /// that for the four shapes here with stages. Since 48a14c3 NRI records such a side with stages
    /// as the conservative COMMON access and the stages' sync, and one without stages as SYNC_NONE /
    /// NO_ACCESS (measured on next with Agility 1.619: all six clean, not even a warning). On
    /// Vulkan the same barrier is an execution dependency with an empty access mask, legal as it
    /// is. One barrier on a 4x4 RGBA8 texture per command buffer, with NRI validation and the
    /// graphics API's layer on; nothing is submitted. D3D12 skips without the debug layer: NRI
    /// validation does not check this rule, so a clean run without the layer would show nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(NoAccessBarrierCases))]
    public void A_texture_barrier_whose_side_has_no_access_records_on_both_backends(Backend backend, string shape)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        if (backend.Api == GraphicsAPI.D3D12)
        {
            var noDebugLayer = NoDebugLayer(device);
            Assert.SkipWhen(noDebugLayer is not null, noDebugLayer ?? "");
        }

        var (before, after) = NoAccessBarriers[shape];
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D,
            usage = TextureUsageBits.SHADER_RESOURCE,
            format = Format.RGBA8_UNORM,
            width = 4,
            height = 4,
            depth = 1,
            mipNum = 1,
            layerNum = 1,
            sampleNum = 1,
        });
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        var start = messages.Count;
        commandBuffer.Begin();
        commandBuffer.Barrier(new NriTextureBarrier(texture, before, after));
        var end = Xunit.Record.Exception(commandBuffer.End);
        var provoked = messages.Skip(start).ToList();
        foreach (var m in provoked) Record("message: " + m);
        Record($"{backend}, {shape}: {provoked.Count} message(s), {provoked.Count(m => m.IsError)} error(s), End {(end is null ? "succeeded" : "threw " + end.Message)}");

        Assert.DoesNotContain(provoked, m => m.IsError);
        Assert.Null(end);
    }

    /// <summary>
    /// What a before side may claim after an access in the same command list, on D3D12. In one
    /// list: {UNDEFINED} -> {COPY_DESTINATION}, UploadBufferToTexture, then the barrier under test
    /// -> {SHADER_RESOURCE}. A side naming the real access, {COPY_DESTINATION, COPY_DESTINATION,
    /// COPY}, is clean. A side with access NONE and stages, {NONE, COPY_DESTINATION, COPY}, is
    /// recorded as COMMON access with the stages' sync (NRI 48a14c3) and is clean too. A side with
    /// access NONE and no stages, {NONE, COPY_DESTINATION, NONE}, is layout-only: SYNC_NONE /
    /// NO_ACCESS says the subresource was not accessed earlier in the same ExecuteCommandLists, and
    /// the debug layer refuses it with 1417 (D3D12_MESSAGE_ID_INVALID_BARRIER_ACCESS; text in
    /// AGENTS.md: Barriers), as NRIDescs.h now says ("StageBits::NONE requires no accesses or
    /// barriers to the affected subresources on that side within the same QueueSubmit scope").
    /// With NRI v190 and patch 0020 the side with stages was the one refused, since 0020 recorded it
    /// as SYNC_NONE.
    /// </summary>
    [Theory]
    [InlineData(BeforeSide.NamesTheRealAccess)]
    [InlineData(BeforeSide.NoAccessWithStages)]
    [InlineData(BeforeSide.NoAccessNoStages)]
    public void After_an_access_in_the_same_list_only_a_before_side_without_stages_is_refused_on_D3D12(BeforeSide side)
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.D3D12));
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.D3D12, messages);
        var noDebugLayer = NoDebugLayer(device);
        Assert.SkipWhen(noDebugLayer is not null, noDebugLayer ?? "");

        const uint size = 4;
        var rowPitch = (uint)Align(size * 4, device.Desc.memoryAlignment.uploadBufferTextureRow);
        var slicePitch = (uint)Align((ulong)rowPitch * size, device.Desc.memoryAlignment.uploadBufferTextureSlice);
        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D,
            usage = TextureUsageBits.SHADER_RESOURCE,
            format = Format.RGBA8_UNORM,
            width = (ushort)size,
            height = (ushort)size,
            depth = 1,
            mipNum = 1,
            layerNum = 1,
            sampleNum = 1,
        });
        using var upload = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = slicePitch });
        var region = new TextureRegionDesc { width = (ushort)size, height = (ushort)size, depth = 1, planes = PlaneBits.ALL };
        var layout = new TextureDataLayoutDesc { offset = 0, rowPitch = rowPitch, slicePitch = slicePitch };
        var before = side switch
        {
            BeforeSide.NamesTheRealAccess => TextureCopyDestination,
            BeforeSide.NoAccessWithStages => Triple(AccessBits.NONE, Layout.COPY_DESTINATION, StageBits.COPY),
            _ => Triple(AccessBits.NONE, Layout.COPY_DESTINATION, StageBits.NONE),
        };

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        using var fence = device.CreateFence();
        var start = messages.Count;
        commandBuffer.Begin();
        commandBuffer.Barrier(new NriTextureBarrier(texture, Undefined, TextureCopyDestination));
        commandBuffer.UploadBufferToTexture(texture, region, upload, layout);
        commandBuffer.Barrier(new NriTextureBarrier(texture, before, ShaderRead));
        var end = Xunit.Record.Exception(commandBuffer.End);
        if (end is null)
        {
            queue.Submit(commandBuffer, fence, 1);
            Wait(fence, 1);
        }
        List<NriMessage> provoked;
        lock (messages) provoked = messages.Skip(start).ToList();
        foreach (var m in provoked) Record("message: " + m);
        Record($"D3D12, before side {{{before.access}, {before.layout}, {before.stages}}} after an upload in the same list: " +
            $"{provoked.Count(m => m.IsError)} error(s), End {(end is null ? "succeeded" : "threw " + end.Message)}");

        if (side != BeforeSide.NoAccessNoStages)
        {
            Assert.DoesNotContain(messages, m => m.IsError);
            Assert.Null(end);
            return;
        }
        Assert.Contains(provoked, m => m.IsError && m.Text.Contains("[1417]", StringComparison.Ordinal));
    }

    /// <summary>The before sides <see cref="After_an_access_in_the_same_list_only_a_before_side_without_stages_is_refused_on_D3D12"/> tries.</summary>
    public enum BeforeSide
    {
        NamesTheRealAccess,
        NoAccessWithStages,
        NoAccessNoStages,
    }

    /// <summary>
    /// D3D12 allows one command list in the recording state per command allocator: Begin is
    /// ID3D12GraphicsCommandList::Reset(allocator), which the debug layer refuses for a second list
    /// while the first is open ([540] COMMAND_ALLOCATOR_CONTENTION). Vulkan allows several command
    /// buffers from one externally synchronized pool to record at once, and NRI validation tracks
    /// the recording state per command buffer only, so neither says anything there. Measured here
    /// with NRI validation on: two command buffers from one allocator, the first begun, then the
    /// second. The portable rule is one recording command buffer per allocator at a time
    /// (AGENTS.md: Barriers).
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_second_command_buffer_of_one_allocator_cannot_begin_while_the_first_records_on_D3D12(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        if (backend.Api == GraphicsAPI.D3D12)
        {
            var noDebugLayer = NoDebugLayer(device);
            Assert.SkipWhen(noDebugLayer is not null, noDebugLayer ?? "");
        }

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var first = allocator.CreateCommandBuffer();
        using var second = allocator.CreateCommandBuffer();
        first.Begin();
        var start = messages.Count;
        var begin = Xunit.Record.Exception(() => second.Begin());
        var provoked = messages.Skip(start).ToList();
        foreach (var m in provoked) Record("message: " + m);
        Record($"{backend}: second Begin {(begin is null ? "succeeded" : $"threw {begin.GetType().Name}: {begin.Message}")}; {provoked.Count(m => m.IsError)} error message(s)");
        first.End();

        if (backend.Api == GraphicsAPI.VK)
        {
            Assert.Null(begin);
            second.End();
            Assert.DoesNotContain(messages, m => m.IsError);
            return;
        }
        Assert.Contains(provoked, m => m.IsError && m.Text.Contains("[540]", StringComparison.Ordinal));
    }

    private string[] RunChild(string name)
    {
        var self = Environment.ProcessPath!;
        var start = new ProcessStartInfo(self) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location); // "dotnet SharpNRI.Tests.dll"
        start.Environment[ChildVariable] = name;

        using var child = Process.Start(start)!;
        var stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        if (!child.WaitForExit(GpuTimeout))
        {
            child.Kill(entireProcessTree: true);
            Assert.Fail($"the {name} child did not finish within {GpuTimeout}");
        }
        child.WaitForExit();
        var lines = stdout.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines) output.WriteLine($"{name}: {line}");
        if (stderr.Result.Length != 0) output.WriteLine($"{name} stderr: {stderr.Result}");
        Assert.True(child.ExitCode == 0, $"the {name} child exited with {child.ExitCode}: " + string.Join(" | ", lines.Where(l => l.StartsWith("FAIL ", StringComparison.Ordinal))));
        return lines;
    }

    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "the child mode must run before xunit's entry point")]
    internal static void RunChildIfAsked()
    {
        var name = Environment.GetEnvironmentVariable(ChildVariable);
        if (name is not (GbvOn or GbvOff)) return;
        int code;
        try
        {
            code = Child(name == GbvOn);
        }
        catch (Exception e)
        {
            Console.WriteLine("FAIL " + e.ToString().ReplaceLineEndings(" / "));
            code = 1;
        }
        Console.Out.Flush();
        Environment.Exit(code);
    }

    private static int Child(bool gpuBasedValidation)
    {
        var messages = new List<NriMessage>();
        var adapter = NriTestDevice.AdapterFor(GraphicsAPI.D3D12);
        using var device = NriDevice.Create(new NriDeviceOptions
        {
            GraphicsApi = GraphicsAPI.D3D12,
            Adapter = adapter,
            EnableNriValidation = true,
            EnableGraphicsApiValidation = true,
            EnableD3D12GpuBasedValidation = gpuBasedValidation,
            MessageHandler = (in NriMessage m) => { lock (messages) messages.Add(m); },
        });
        if (NoDebugLayer(device) is { } reason)
        {
            Console.WriteLine("SKIP " + reason);
            return 0;
        }
        if (device.Desc.tiers.resourceBinding == 0)
        {
            Console.WriteLine("SKIP tiers.resourceBinding is 0: no PARTIALLY_BOUND descriptor ranges, which keep the never-written descriptor away from the debug layer's CPU-side checks");
            return 0;
        }

        const ulong size = ThreadCount * sizeof(uint);
        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
        {
            DescriptorSets =
            [
                new NriDescriptorSetDesc(0,
                [
                    new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 2, descriptorType = DescriptorType.STORAGE_STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER, flags = DescriptorRangeBits.PARTIALLY_BOUND },
                ]),
            ],
            ShaderStages = StageBits.COMPUTE_SHADER,
        });
        using var pipeline = device.CreateComputePipeline(new NriComputePipelineDesc
        {
            PipelineLayout = layout,
            Shader = new NriShaderDesc(StageBits.COMPUTE_SHADER, PipelineTests.Bytecode(device, "GpuValidation.cs"), "main"),
        });
        using var pool = device.CreateDescriptorPool(new DescriptorPoolDesc { descriptorSetMaxNum = 1, storageStructuredBufferMaxNum = 2 });
        using var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = size, structureStride = sizeof(uint), usage = BufferUsageBits.SHADER_RESOURCE_STORAGE });
        using var view = buffer.CreateView(BufferView.STORAGE_STRUCTURED_BUFFER, structureStride: sizeof(uint));
        var set = pool.AllocateDescriptorSet(layout, 0);
        set.Update(0, 0, view); // slot 1 is never written

        int created;
        lock (messages) created = messages.Count;
        var clock = Stopwatch.StartNew();
        Run(device, cmd =>
        {
            cmd.Barrier(new NriBufferBarrier(buffer, Nothing, ComputeStorage));
            cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);
            cmd.SetPipeline(pipeline);
            cmd.SetDescriptorSet(0, set, BindPoint.COMPUTE);
            cmd.Dispatch(1);
        }, pool);
        device.WaitIdle();

        List<NriMessage> before, after;
        lock (messages)
        {
            before = messages.Take(created).ToList();
            after = messages.Skip(created).ToList();
        }
        foreach (var m in before.Where(m => m.IsError)) Console.WriteLine($"FAIL during creation: {m}");
        foreach (var m in after) Console.WriteLine($"{(m.IsError ? "ERROR " : "MESSAGE ")}{m}");
        Console.WriteLine($"RESULT GPU-based validation {(gpuBasedValidation ? "on" : "off")}, '{adapter?.Name}': " +
            $"one dispatch through a range of 2 with slot 0 written, {clock.ElapsedMilliseconds} ms, {after.Count(m => m.IsError)} error message(s)");
        return before.Any(m => m.IsError) ? 1 : 0;
    }
}
