// Adapter enumeration by graphics API. nriEnumerateAdapters runs every backend's pass unless asked
// otherwise: a Vulkan instance, a DXGI factory, D3D12 probes and devices on the inbox runtime. With
// a backend mask (NriAdapter.Enumerate(GraphicsAPI); NRI main 962615b, master's patch 0024) only
// the asked passes run, and nriCreateDevice with no adapter enumerates Vulkan only for a Vulkan
// device (patch 0024). AGENTS.md: Device
// creation and callbacks.
//
// Which DLLs a process has loaded can only be measured in a process that has done nothing else, so
// the module half runs in child processes (this test executable started again with
// SHARPNRI_TEST_CHILD set; the module initializer below runs the case and exits before xunit
// starts): one enumerates Vulkan only and then creates a Vulkan device with no adapter, the control
// enumerates everything, and on Windows a third creates one Vulkan instance through the loader
// with no NRI call at all, to tell what the Vulkan loader loads by itself from what NRI loads.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed class AdapterEnumerationTests(ITestOutputHelper output)
{
    private const string ChildVariable = "SHARPNRI_TEST_CHILD";
    private const string ChildVulkanOnly = "adapters-vk-only";
    private const string ChildAll = "adapters-all";
    private const string ChildLoader = "adapters-vk-loader";
    private const string ChildTestingProbe = "adapters-testing-vk-probe";
    private static readonly string[] D3DModules = ["d3d12.dll", "dxgi.dll", "D3D12Core.dll"];

    /// <summary>
    /// Loading NRI.dll loads none of the D3D modules: d3d12.dll and dxgi.dll are delay-loaded
    /// (tools/cmake/delayload.cmake). A Vulkan-only enumeration and a Vulkan device created with no
    /// adapter then load neither d3d12.dll nor the D3D12 runtime, D3D12Core.dll; dxgi.dll they load
    /// exactly when a bare Vulkan instance does, with no NRI in the process (the Windows Vulkan
    /// loader loads it itself to order the adapters: measured with every driver and implicit layer
    /// disabled). An unfiltered enumeration loads all three (the control). The children print how
    /// long their first and second enumeration took. The module half is Windows-only (the names
    /// are Windows DLLs); the timings are recorded everywhere.
    /// </summary>
    [Fact]
    public void A_Vulkan_only_enumeration_and_device_load_no_D3D_module_of_NRI()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        var vulkanOnly = RunChild(ChildVulkanOnly);
        var all = RunChild(ChildAll);
        foreach (var line in vulkanOnly.Concat(all).Where(l => l.StartsWith("RESULT ", StringComparison.Ordinal))) GpuWork.Record(line);
        Assert.Contains(vulkanOnly, l => l.StartsWith("RESULT Vulkan device created", StringComparison.Ordinal));

        Assert.SkipUnless(OperatingSystem.IsWindows(), "the D3D12 runtime modules (d3d12.dll, dxgi.dll, D3D12Core.dll) are Windows DLLs; the timings above are the measurement here");
        var loader = RunChild(ChildLoader);
        foreach (var line in loader.Where(l => l.StartsWith("RESULT ", StringComparison.Ordinal))) GpuWork.Record(line);
        static bool Has(string[] lines, string after, string module) =>
            lines.Single(l => l.StartsWith("RESULT modules " + after + ": ", StringComparison.Ordinal))[("RESULT modules ".Length + after.Length + 2)..]
                .Split(", ").Contains(module, StringComparer.OrdinalIgnoreCase);
        foreach (var module in D3DModules)
        {
            Assert.False(Has(vulkanOnly, "before any enumeration", module), $"{module} is loaded once NRI.dll is, before any enumeration (Vulkan-only child)");
            Assert.False(Has(all, "before any enumeration", module), $"{module} is loaded once NRI.dll is, before any enumeration (unfiltered child)");
            Assert.True(Has(all, "after the unfiltered enumeration", module), $"{module} is not loaded after an unfiltered enumeration: the control does not show the difference");
            // What a bare Vulkan instance loads is the loader's; NRI must add nothing to it.
            var byLoader = Has(loader, "after a Vulkan instance with no NRI", module);
            Assert.True(byLoader == Has(vulkanOnly, "after the Vulkan-only enumeration", module),
                $"{module} after a Vulkan-only enumeration: loaded {!byLoader}, while a bare Vulkan instance leaves it {(byLoader ? "loaded" : "unloaded")}");
            Assert.True(byLoader == Has(vulkanOnly, "after creating the Vulkan device with no adapter", module),
                $"{module} after creating a Vulkan device with no adapter: loaded {!byLoader}, while a bare Vulkan instance leaves it {(byLoader ? "loaded" : "unloaded")}");
        }
        Assert.False(Has(loader, "after a Vulkan instance with no NRI", "d3d12.dll"), "a bare Vulkan instance loads d3d12.dll: the Vulkan-only child cannot show that NRI does not");
        Assert.False(Has(loader, "after a Vulkan instance with no NRI", "D3D12Core.dll"), "a bare Vulkan instance loads D3D12Core.dll: the Vulkan-only child cannot show that NRI does not");
    }

    /// <summary>
    /// SharpNRI.Testing asked whether Vulkan is available enumerates Vulkan only and creates only the
    /// Vulkan probe device: d3d12.dll and D3D12Core.dll stay unloaded (the probe used to enumerate
    /// every backend, which creates D3D12 devices, and to probe D3D12 as well).
    /// </summary>
    [Fact]
    public void SharpNRI_Testing_probing_Vulkan_loads_no_D3D12_module()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        Assert.SkipUnless(OperatingSystem.IsWindows(), "d3d12.dll and D3D12Core.dll are Windows DLLs");
        var probe = RunChild(ChildTestingProbe);
        foreach (var line in probe.Where(l => l.StartsWith("RESULT ", StringComparison.Ordinal))) GpuWork.Record(line);
        var modules = probe.Single(l => l.StartsWith("RESULT modules after the Vulkan probe: ", StringComparison.Ordinal))["RESULT modules after the Vulkan probe: ".Length..].Split(", ");
        Assert.DoesNotContain("d3d12.dll", modules, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("D3D12Core.dll", modules, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData(null, "true", true)]
    [InlineData("1", null, true)]
    [InlineData("true", null, true)]
    [InlineData("0", "true", false)]
    [InlineData("false", "true", false)]
    [InlineData(" FALSE ", "true", false)]
    public void SHARPNRI_TEST_SOFTWARE_decides_and_GitHub_Actions_is_only_the_default(string? software, string? githubActions, bool expected) =>
        Assert.Equal(expected, SharpNRI.Testing.NriTestDevice.PreferSoftwareFrom(software, githubActions));

    /// <summary>The probe's reason for a validation layer older than NRI's Vulkan headers, from the message it reports.</summary>
    [Fact]
    public void A_validation_layer_that_reports_NRI_structures_as_unknown_is_a_reason_not_a_failure()
    {
        var stale = new NriMessage(Message.ERROR, null, 0,
            "vkCreateDevice(): pCreateInfo->pNext chain includes a structure with unknown VkStructureType (1000620000); Allowed structures are [...]. This error is based on the Valid Usage documentation for version 275 of the Vulkan header.");
        var reason = SharpNRI.Testing.NriTestDevice.StaleValidationLayer([stale]);
        Assert.NotNull(reason);
        Assert.Contains("older than the Vulkan headers NRI was built with", reason, StringComparison.Ordinal);
        Assert.Contains("1000620000", reason, StringComparison.Ordinal);
        Assert.Null(SharpNRI.Testing.NriTestDevice.StaleValidationLayer([new NriMessage(Message.ERROR, null, 0, "VUID-vkCmdDraw-None-08600: something else")]));
        Assert.Null(SharpNRI.Testing.NriTestDevice.StaleValidationLayer([stale with { Type = Message.WARNING }]));
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
        if (name is not (ChildVulkanOnly or ChildAll or ChildLoader or ChildTestingProbe)) return;
        int code;
        try
        {
            code = name switch { ChildVulkanOnly => VulkanOnly(), ChildAll => All(), ChildTestingProbe => TestingProbe(), _ => LoaderOnly() };
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
        return loaded.Count == 0 ? "none" : string.Join(", ", loaded);
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

    private static int TestingProbe()
    {
        Baseline();
        var reason = SharpNRI.Testing.NriTestDevice.Unavailable(GraphicsAPI.VK);
        Console.WriteLine($"RESULT Vulkan probe: {reason ?? "available"}");
        Console.WriteLine($"RESULT modules after the Vulkan probe: {LoadedD3DModules()}");
        return reason is null ? 0 : 1;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct VkInstanceCreateInfo
    {
        public int sType;
        public nint pNext;
        public uint flags;
        public nint pApplicationInfo;
        public uint enabledLayerCount;
        public nint ppEnabledLayerNames;
        public uint enabledExtensionCount;
        public nint ppEnabledExtensionNames;
    }

    [DllImport("vulkan-1", ExactSpelling = true)]
    private static extern int vkCreateInstance(in VkInstanceCreateInfo createInfo, nint allocator, out nint instance);

    [DllImport("vulkan-1", ExactSpelling = true)]
    private static extern void vkDestroyInstance(nint instance, nint allocator);

    /// <summary>One Vulkan 1.0 instance through the loader, created and destroyed, NRI never called.</summary>
    private static int LoaderOnly()
    {
        var result = vkCreateInstance(new VkInstanceCreateInfo { sType = 1 /* VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO */ }, 0, out var instance);
        if (result == 0) vkDestroyInstance(instance, 0);
        Console.WriteLine($"RESULT bare Vulkan instance: vkCreateInstance {result}");
        Console.WriteLine($"RESULT modules after a Vulkan instance with no NRI: {LoadedD3DModules()}");
        return result == 0 ? 0 : 1;
    }
}
