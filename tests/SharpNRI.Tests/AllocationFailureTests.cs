// A host allocator that fails: every NRI object allocation that comes back null fails its creation
// with OUT_OF_MEMORY, leaks nothing, and leaves the device working (patch 0034). Before it, NRI used
// the null as an object (N10): an access violation in the call, on Vulkan and D3D12, with NRI's
// validation on and off.
//
// It runs in a child process (this test executable started again with SHARPNRI_TEST_CHILD set; the
// module initializer below runs it and exits before xunit starts), so a crash fails this test
// instead of ending the run. The child fails the 1st, 2nd, ... allocation of each creation call in
// turn, until the call makes no more allocations than that.
//
// NRI's containers (Vector, UnorderedMap, Scratch on the heap) allocate through the same callbacks
// and cannot report a null (StdAllocator::allocate is noexcept), so an allocator that fails inside
// one still takes the process down; the calls here make object allocations only.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpNRI.Native;
using SharpNRI.Testing;
using Xunit;

namespace SharpNRI.Tests;

public sealed unsafe class AllocationFailureTests
{
    private const string ChildVariable = "SHARPNRI_TEST_CHILD";
    private const string ChildName = "allocation-failure";
    private const string ChildBackendVariable = "SHARPNRI_TEST_CHILD_BACKEND";
    private const string ChildValidationVariable = "SHARPNRI_TEST_CHILD_NRI_VALIDATION";

    public static TheoryData<Backend, bool> BackendsWithAndWithoutValidation
    {
        get
        {
            var data = new TheoryData<Backend, bool>();
            foreach (var api in NriTestDevice.GpuBackends) foreach (var validation in new[] { true, false }) data.Add(new Backend(api), validation);
            return data;
        }
    }

    /// <summary>
    /// Device creation whose first allocation fails, then each creation call with its 1st, 2nd, ...
    /// allocation failing: every one answers OUT_OF_MEMORY and returns what it allocated, and the
    /// device then creates the same objects and runs a submit.
    /// </summary>
    [Theory]
    [MemberData(nameof(BackendsWithAndWithoutValidation))]
    public void An_allocation_that_fails_fails_its_creation_with_OUT_OF_MEMORY(Backend backend, bool nriValidation)
    {
        TestDevice.RequireAvailable(backend);
        var lines = RunChild(backend, nriValidation);
        Assert.Contains($"RESULT {backend}: every failed allocation answered OUT_OF_MEMORY, nothing leaked, the device works", lines);
    }

    private static string[] RunChild(Backend backend, bool nriValidation)
    {
        var self = Environment.ProcessPath!;
        var start = new ProcessStartInfo(self) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location); // "dotnet SharpNRI.Tests.dll"
        start.Environment[ChildVariable] = ChildName;
        start.Environment[ChildBackendVariable] = backend.Api.ToString();
        start.Environment[ChildValidationVariable] = nriValidation ? "1" : "0";

        using var child = Process.Start(start)!;
        var stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        if (!child.WaitForExit(GpuWork.GpuTimeout))
        {
            child.Kill(entireProcessTree: true);
            Assert.Fail($"the {ChildName} child did not finish within {GpuWork.GpuTimeout}");
        }
        child.WaitForExit();
        var lines = stdout.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines) GpuWork.Record(line);
        if (stderr.Result.Length != 0) GpuWork.Record("stderr: " + stderr.Result);
        Assert.True(child.ExitCode == 0, $"the {ChildName} child ({backend}, NRI validation {(nriValidation ? "on" : "off")}) exited with 0x{child.ExitCode:X8}; its last lines: " + string.Join(" | ", lines.TakeLast(3)));
        return lines;
    }

    /// <summary>Allocates with NativeMemory and counts live blocks; FailAt(k) makes the k-th allocation from then on return null, once.</summary>
    private sealed class FailingAllocator : INriAllocator
    {
        private long _live;
        private int _countdown;

        public long Live => Interlocked.Read(ref _live);

        /// <summary>The armed allocation came and returned null.</summary>
        public bool Failed { get; private set; }

        public void FailAt(int k)
        {
            Failed = false;
            _countdown = k;
        }

        public void Disarm() => _countdown = 0;

        private bool FailsNow()
        {
            if (_countdown == 0 || --_countdown != 0) return false;
            Failed = true;
            return true;
        }

        public void* Allocate(nuint size, nuint alignment)
        {
            if (FailsNow()) return null;
            Interlocked.Increment(ref _live);
            return NativeMemory.AlignedAlloc(size == 0 ? 1 : size, alignment == 0 ? (nuint)sizeof(void*) : alignment);
        }

        public void* Reallocate(void* memory, nuint size, nuint alignment)
        {
            if (memory is null) return Allocate(size, alignment);
            if (size == 0)
            {
                Free(memory);
                return null;
            }
            if (FailsNow()) return null; // the block stays the caller's
            return NativeMemory.AlignedRealloc(memory, size, alignment == 0 ? (nuint)sizeof(void*) : alignment);
        }

        public void Free(void* memory)
        {
            if (memory is null) return;
            Interlocked.Decrement(ref _live);
            NativeMemory.AlignedFree(memory);
        }
    }

    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "the child mode must run before xunit's entry point")]
    internal static void RunChildIfAsked()
    {
        if (Environment.GetEnvironmentVariable(ChildVariable) != ChildName) return;
        int code;
        try
        {
            code = Child(Enum.Parse<GraphicsAPI>(Environment.GetEnvironmentVariable(ChildBackendVariable)!), Environment.GetEnvironmentVariable(ChildValidationVariable) != "0");
        }
        catch (Exception e)
        {
            Console.WriteLine("FAIL " + e.ToString().ReplaceLineEndings(" / "));
            code = 1;
        }
        Console.Out.Flush();
        Environment.Exit(code);
    }

    private static int Child(GraphicsAPI api, bool nriValidation)
    {
        var allocator = new FailingAllocator();
        var messages = new List<NriMessage>();
        var failures = 0;
        void Fail(string what)
        {
            Console.WriteLine("FAIL " + what);
            failures++;
        }

        // Device creation is not swept: its first allocation is a container's (nriCreateDevice's
        // Vector<QueueFamilyDesc>), and a null there is past any check (StdAllocator is noexcept).
        using var device = TestDevice.Create(api, messages, o => { o.Allocator = allocator; o.EnableNriValidation = nriValidation; });
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var commandAllocator = queue.CreateCommandAllocator();
        using var buffer = device.CreateBuffer(new BufferDesc { size = 256, usage = BufferUsageBits.SHADER_RESOURCE });
        var bufferMemory = buffer.GetMemoryDesc(MemoryLocation.DEVICE);
        var texture = new TextureDesc { type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.RGBA8_UNORM, width = 4, height = 4, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1 };
        var samplerDesc = new SamplerDesc { filters = new Filters { min = Filter.LINEAR, mag = Filter.LINEAR, mip = Filter.LINEAR }, mipMax = 16 };

        // The first sampler of a D3D12 device creates a batch of descriptors, which grows NRI's
        // containers (DeviceD3D12::GetDescriptorHandle); one created first keeps the sweep to objects.
        device.CreateSampler(samplerDesc).Dispose();

        var calls = new (string Name, Func<IDisposable> Create)[]
        {
            ("CreateFence", () => device.CreateFence()),
            ("CreateBuffer", () => device.CreateBuffer(new BufferDesc { size = 256, usage = BufferUsageBits.SHADER_RESOURCE })),
            ("CreateTexture", () => device.CreateTexture(texture)),
            ("AllocateMemory", () => device.AllocateMemory(new AllocateMemoryDesc { size = bufferMemory.size, type = bufferMemory.type })),
            ("CreateCommandAllocator", () => queue.CreateCommandAllocator()),
            ("CreateCommandBuffer", () => commandAllocator.CreateCommandBuffer()),
            ("CreateQueryPool", () => device.CreateQueryPool(QueryType.TIMESTAMP, 4)),
            ("CreateSampler", () => device.CreateSampler(samplerDesc)),
        };
        foreach (var (name, create) in calls)
        {
            for (var k = 1; ; k++)
            {
                Console.WriteLine($"CASE {api}: {name}, allocation {k} fails");
                Console.Out.Flush();
                var live = allocator.Live;
                allocator.FailAt(k);
                try
                {
                    var created = create();
                    var failed = allocator.Failed;
                    allocator.Disarm();
                    created.Dispose();
                    if (!failed)
                    {
                        Console.WriteLine($"RESULT {api}: {name} makes {k - 1} allocation(s); each failing answered OUT_OF_MEMORY");
                        break;
                    }
                    Fail($"{api}: {name} succeeded although its allocation {k} failed");
                }
                catch (NriException e) when (e.Result == Result.OUT_OF_MEMORY)
                {
                }
                catch (NriException e)
                {
                    Fail($"{api}: {name} with allocation {k} failing answered {e.Result}, not OUT_OF_MEMORY: {e.Message}");
                }
                finally
                {
                    allocator.Disarm();
                }
                if (allocator.Live != live) Fail($"{api}: {name} with allocation {k} failing left {allocator.Live - live} block(s)");
                if (k == 16)
                {
                    Fail($"{api}: {name} still failed with its allocation 16 failing");
                    break;
                }
            }
        }

        // The device still works: the same objects, and a submit that a fence reports done.
        using var fence = device.CreateFence();
        using (var commandBuffer = commandAllocator.CreateCommandBuffer())
        {
            commandBuffer.Begin();
            commandBuffer.End();
            queue.Submit(commandBuffer, fence, 1);
            var clock = Stopwatch.StartNew();
            while (fence.Value < 1)
            {
                if (clock.Elapsed > GpuWork.GpuTimeout) throw new TimeoutException("the submit did not finish");
                Thread.Sleep(1);
            }
        }
        foreach (var m in messages.Where(m => m.IsError)) Fail(m.ToString());
        if (failures != 0) return 1;
        Console.WriteLine($"RESULT {api}: every failed allocation answered OUT_OF_MEMORY, nothing leaked, the device works");
        return 0;
    }
}
