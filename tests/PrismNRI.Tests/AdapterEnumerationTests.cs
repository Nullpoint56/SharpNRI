// Adapter enumeration by graphics API. nriEnumerateAdapters runs every backend's pass unless asked
// otherwise: a Vulkan instance, a DXGI factory, D3D12 probes and devices on the inbox runtime. With
// a backend mask (NriAdapter.Enumerate(GraphicsAPI); NRI main 962615b, master's patch 0024) only
// the asked passes run, and nriCreateDevice with no adapter enumerates Vulkan only for a Vulkan
// device (patch 0024). CLAUDE.md: Device
// creation and callbacks.
//
// Which DLLs a process has loaded can only be measured in a process that has done nothing else, so
// the module half runs in child processes (this test executable started again with
// PRISMNRI_TEST_CHILD set; the module initializer below runs the case and exits before xunit
// starts): one enumerates Vulkan only and then creates a Vulkan device with no adapter, the control
// enumerates everything.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using PrismNRI.Native;
using Xunit;

namespace PrismNRI.Tests;

public sealed class AdapterEnumerationTests(ITestOutputHelper output)
{
    private const string ChildVariable = "PRISMNRI_TEST_CHILD";
    private const string ChildVulkanOnly = "adapters-vk-only";
    private const string ChildAll = "adapters-all";
    private static readonly string[] D3DModules = ["d3d12.dll", "dxgi.dll", "D3D12Core.dll"];
    private const string Runtime = "D3D12Core.dll";

    /// <summary>
    /// A Vulkan-only enumeration and a Vulkan device created with no adapter leave the D3D12
    /// runtime, D3D12Core.dll, unloaded; an unfiltered enumeration loads it (the control).
    /// d3d12.dll and dxgi.dll are load-time imports of NRI.dll, so they are in every process that
    /// has called NRI, before any enumeration (recorded as the baseline). Both children print how long their first and second enumeration took. The module
    /// half is Windows-only (the names are Windows DLLs); the timings are recorded everywhere.
    /// </summary>
    [Fact]
    public void A_Vulkan_only_enumeration_and_device_load_no_D3D12_runtime()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        var vulkanOnly = RunChild(ChildVulkanOnly);
        var all = RunChild(ChildAll);
        foreach (var line in vulkanOnly.Concat(all).Where(l => l.StartsWith("RESULT ", StringComparison.Ordinal))) GpuWork.Record(line);
        Assert.Contains(vulkanOnly, l => l.StartsWith("RESULT Vulkan device created", StringComparison.Ordinal));

        Assert.SkipUnless(OperatingSystem.IsWindows(), "the D3D12 runtime modules (d3d12.dll, dxgi.dll, D3D12Core.dll) are Windows DLLs; the timings above are the measurement here");
        static bool HasRuntime(string[] lines, string after) =>
            lines.Single(l => l.StartsWith("RESULT modules " + after + ": ", StringComparison.Ordinal)).Contains(Runtime, StringComparison.OrdinalIgnoreCase);
        Assert.False(HasRuntime(vulkanOnly, "after the Vulkan-only enumeration"), "D3D12Core.dll is loaded after a Vulkan-only enumeration");
        Assert.False(HasRuntime(vulkanOnly, "after creating the Vulkan device with no adapter"), "D3D12Core.dll is loaded after creating a Vulkan device with no adapter");
        Assert.False(HasRuntime(all, "before any enumeration"), "D3D12Core.dll is loaded before any enumeration");
        Assert.True(HasRuntime(all, "after the unfiltered enumeration"), "D3D12Core.dll is not loaded after an unfiltered enumeration: the control does not show the difference");
    }

    /// <summary>
    /// The adapter NRI picks for a device created with no adapter is the first adapter of the
    /// filtered enumeration that supports the API, and the first of the unfiltered one: the filter
    /// changes which passes run, not which adapter comes first (the D3D pass only adds D3D bits to
    /// an adapter the Vulkan pass listed, and the sort reads memory sizes and architecture only).
    /// Recorded: the three uids and names, and for D3D12 the queue counts of the filtered and the
    /// unfiltered desc (the unfiltered one is the Vulkan pass's desc where Vulkan lists the adapter).
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void The_adapter_picked_with_no_adapter_is_the_first_of_the_filtered_enumeration(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var filtered = NriAdapter.Enumerate(backend.Api).Where(a => a.Supports(backend.Api)).ToList();
        var unfiltered = NriAdapter.Enumerate().Where(a => a.Supports(backend.Api)).ToList();
        Assert.NotEmpty(filtered);
        Assert.All(filtered, a => Assert.Equal((GraphicsAPI)0, a.SupportedGraphicsApis & ~(backend.Api | GraphicsAPI.NONE | (backend.Api == GraphicsAPI.D3D12 ? GraphicsAPI.D3D11 : 0))));

        var messages = new List<NriMessage>();
        Uid_t picked;
        using (var device = NriDevice.Create(new NriDeviceOptions
        {
            GraphicsApi = backend.Api,
            Adapter = null,
            EnableNriValidation = true,
            MessageHandler = (in NriMessage m) => { lock (messages) messages.Add(m); },
        }))
        {
            picked = device.Desc.adapterDesc.uid;
        }

        static string Uid(Uid_t u) => $"{u.high:X16}{u.low:X16}";
        var pickedName = unfiltered.FirstOrDefault(a => Uid(a.Uid) == Uid(picked))?.Name ?? "?";
        Record($"{backend}: picked with no adapter '{pickedName}' {Uid(picked)}; first filtered '{filtered[0].Name}' {Uid(filtered[0].Uid)}; " +
            $"first unfiltered '{unfiltered[0].Name}' {Uid(unfiltered[0].Uid)}; {filtered.Count} filtered, {unfiltered.Count} unfiltered supporting {backend}");
        if (backend.Api == GraphicsAPI.D3D12)
            Record($"D3D12 queue counts (GRAPHICS, COMPUTE, COPY): filtered {Counts(filtered[0])}, unfiltered {Counts(unfiltered[0])}");

        Assert.Equal(Uid(filtered[0].Uid), Uid(picked));
        Assert.Equal(Uid(unfiltered[0].Uid), Uid(picked));
        Assert.Equal(unfiltered.Select(a => Uid(a.Uid)), filtered.Select(a => Uid(a.Uid)));
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    private static string Counts(NriAdapter a) => $"{a.QueueCount(QueueType.GRAPHICS)}, {a.QueueCount(QueueType.COMPUTE)}, {a.QueueCount(QueueType.COPY)}";

    private static void Record(string line) => GpuWork.Record(line);

    private string[] RunChild(string name)
    {
        var self = Environment.ProcessPath!;
        var start = new ProcessStartInfo(self) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
        start.Environment[ChildVariable] = name;

        using var child = Process.Start(start)!;
        var stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        if (!child.WaitForExit(GpuWork.GpuTimeout))
        {
            child.Kill(entireProcessTree: true);
            Assert.Fail($"the {name} child did not finish within {GpuWork.GpuTimeout}");
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
        if (name is not (ChildVulkanOnly or ChildAll)) return;
        int code;
        try
        {
            code = name == ChildVulkanOnly ? VulkanOnly() : All();
        }
        catch (Exception e)
        {
            Console.WriteLine("FAIL " + e.ToString().ReplaceLineEndings(" / "));
            code = 1;
        }
        Console.Out.Flush();
        Environment.Exit(code);
    }

    private static string LoadedD3DModules()
    {
        using var process = Process.GetCurrentProcess();
        var loaded = process.Modules.Cast<ProcessModule>().Select(m => m.ModuleName)
            .Where(n => D3DModules.Contains(n, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return loaded.Count == 0 ? "none of " + string.Join(", ", D3DModules) : string.Join(", ", loaded);
    }

    /// <summary>The modules once NRI.dll is loaded (a flat export that enumerates nothing), before any enumeration.</summary>
    private static void Baseline()
    {
        Nri.GetGraphicsAPIString(GraphicsAPI.VK);
        Console.WriteLine($"RESULT modules before any enumeration: {LoadedD3DModules()}");
    }

    private static (int Count, double Milliseconds) Time(Func<IReadOnlyList<NriAdapter>> enumerate)
    {
        var clock = Stopwatch.StartNew();
        var adapters = enumerate();
        return (adapters.Count, clock.Elapsed.TotalMilliseconds);
    }

    private static int VulkanOnly()
    {
        Baseline();
        var first = Time(() => NriAdapter.Enumerate(GraphicsAPI.VK));
        var second = Time(() => NriAdapter.Enumerate(GraphicsAPI.VK));
        Console.WriteLine($"RESULT Vulkan-only enumeration: {first.Count} adapter(s), first call {first.Milliseconds:0.0} ms, second {second.Milliseconds:0.0} ms");
        Console.WriteLine($"RESULT modules after the Vulkan-only enumeration: {LoadedD3DModules()}");

        var messages = new List<NriMessage>();
        var clock = Stopwatch.StartNew();
        using (var device = NriDevice.Create(new NriDeviceOptions
        {
            GraphicsApi = GraphicsAPI.VK,
            Adapter = null,
            EnableNriValidation = true,
            MessageHandler = (in NriMessage m) => { lock (messages) messages.Add(m); },
        }))
        {
            Console.WriteLine($"RESULT Vulkan device created with no adapter in {clock.Elapsed.TotalMilliseconds:0.0} ms on '{device.Desc.adapterDesc.uid.high:X16}{device.Desc.adapterDesc.uid.low:X16}', supportedGraphicsAPIs {device.Desc.adapterDesc.supportedGraphicsAPIs}");
            Console.WriteLine($"RESULT modules after creating the Vulkan device with no adapter: {LoadedD3DModules()}");
        }
        foreach (var m in messages.Where(m => m.IsError)) Console.WriteLine("FAIL NRI error: " + m.Text);
        return messages.Any(m => m.IsError) ? 1 : 0;
    }

    private static int All()
    {
        Baseline();
        var first = Time(NriAdapter.Enumerate);
        var second = Time(NriAdapter.Enumerate);
        Console.WriteLine($"RESULT unfiltered enumeration: {first.Count} adapter(s), first call {first.Milliseconds:0.0} ms, second {second.Milliseconds:0.0} ms");
        Console.WriteLine($"RESULT modules after the unfiltered enumeration: {LoadedD3DModules()}");
        return 0;
    }
}
