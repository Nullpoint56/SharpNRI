// A lost device, end to end on D3D12: ID3D12Device5::RemoveDevice puts the device NRI created into
// the removed state, the next NRI call that checks the device answers DEVICE_LOST and the binding
// throws NriDeviceLostException naming that call, and nriReportDeviceLostInfo
// (NriDevice.ReportDeviceLostInfo) then reports DXGI's removal reason and DRED's record through the
// message callback.
//
// It runs in a child process (this test executable started again with SHARPNRI_TEST_CHILD set; the
// module initializer below runs the case and exits before xunit starts): DeviceLostInfoLevel is
// process-global on D3D12 (it switches DRED on for every later device), and a removed device is not
// something to leave in the test process. RemoveDevice removes only that device; no GPU fault, no
// reset of the adapter for other processes. Vulkan has no such call: losing a Vulkan device takes a
// GPU fault, which resets the GPU for every process using it.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using SharpNRI.Native;
using SharpNRI.Testing;
using Xunit;
using V12 = Vortice.Direct3D12;

namespace SharpNRI.Tests;

public sealed class DeviceLostTests(ITestOutputHelper output)
{
    private const string ChildVariable = "SHARPNRI_TEST_CHILD";
    private const string ChildName = "d3d12-device-lost";
    private const string DeviceRemoved = "0x887A0005"; // DXGI_ERROR_DEVICE_REMOVED

    /// <summary>
    /// A command buffer recorded while the device lives is submitted after RemoveDevice:
    /// <c>QueueSubmit</c> answers DEVICE_LOST (NRI's D3D12 queue checks GetDeviceRemovedReason after
    /// ExecuteCommandLists and reports "Submit(): failed, result = 0x887A0005") and the binding
    /// throws <see cref="NriDeviceLostException"/> with <c>Call</c> "QueueSubmit" and that text in
    /// its message. A creation call and a wait on the removed device answer DEVICE_LOST too, each
    /// exception carrying its own call's report (D3D12MA's resource creation, the wait's
    /// CreateFence), not the submit's. Then <see cref="NriDevice.ReportDeviceLostInfo"/> succeeds
    /// and NRI's report arrives through the message callback: "[DeviceLost]
    /// GetDeviceRemovedReason: 0x887A0005" first, then what DRED recorded (the device was created
    /// with DeviceLostInfoLevel VERBOSE, which forces DRED's breadcrumbs and page-fault tracking on).
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_removed_D3D12_device_throws_NriDeviceLostException_and_reports_why(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        Assert.SkipWhen(backend.Api == GraphicsAPI.VK, "VK: Vulkan has no call that removes a device; losing one takes a GPU fault, which resets the GPU for every process using it");

        var lines = RunChild();
        var skip = lines.FirstOrDefault(l => l.StartsWith("SKIP ", StringComparison.Ordinal));
        Assert.SkipWhen(skip is not null, skip?[5..] ?? "");
        foreach (var line in lines.Where(l => l.StartsWith("RESULT ", StringComparison.Ordinal))) GpuWork.Record(line);

        Assert.Contains("THROWN QueueSubmit NriDeviceLostException DEVICE_LOST", lines);
        Assert.Contains(lines, l => l.StartsWith("MESSAGE ERROR ", StringComparison.Ordinal) && l.Contains($"Submit(): failed, result = {DeviceRemoved}", StringComparison.Ordinal));

        // Each exception's message carries its own call's report, and only that.
        string Thrown(string what) => Assert.Single(lines, l => l.StartsWith($"RESULT {what} after RemoveDevice threw ", StringComparison.Ordinal));
        var submit = Thrown("QueueSubmit");
        Assert.StartsWith("RESULT QueueSubmit after RemoveDevice threw NriDeviceLostException: QueueSubmit: DEVICE_LOST — ", submit, StringComparison.Ordinal);
        Assert.Contains($"Submit(): failed, result = {DeviceRemoved}", submit, StringComparison.Ordinal);
        foreach (var later in new[] { Thrown("CreateCommittedBuffer"), Thrown("DeviceWaitIdle") })
        {
            Assert.Contains($"failed, result = {DeviceRemoved}", later, StringComparison.Ordinal);
            Assert.DoesNotContain("Submit()", later, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("CreateResource3", Thrown("DeviceWaitIdle"), StringComparison.Ordinal);

        var report = lines.Where(l => l.StartsWith("REPORT ", StringComparison.Ordinal)).ToList();
        Assert.True(report.Count > 0, "nriReportDeviceLostInfo reported nothing through the message callback");
        Assert.Contains($"[DeviceLost] GetDeviceRemovedReason: {DeviceRemoved}", report[0], StringComparison.Ordinal);
        Assert.All(report, l => Assert.Contains("[DeviceLost]", l, StringComparison.Ordinal));
        Assert.Contains("RESULT ReportDeviceLostInfo succeeded", lines.First(l => l.StartsWith("RESULT ReportDeviceLostInfo", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    private string[] RunChild()
    {
        var self = Environment.ProcessPath!;
        var start = new ProcessStartInfo(self) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location); // "dotnet SharpNRI.Tests.dll"
        start.Environment[ChildVariable] = ChildName;

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
        foreach (var line in lines) output.WriteLine(line);
        if (stderr.Result.Length != 0) output.WriteLine("stderr: " + stderr.Result);
        Assert.True(child.ExitCode == 0, $"the {ChildName} child exited with {child.ExitCode}: " + string.Join(" | ", lines.Where(l => l.StartsWith("FAIL ", StringComparison.Ordinal))));
        return lines;
    }

    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "the child mode must run before xunit's entry point")]
    internal static void RunChildIfAsked()
    {
        if (Environment.GetEnvironmentVariable(ChildVariable) != ChildName) return;
        int code;
        try
        {
            code = Child();
        }
        catch (Exception e)
        {
            Console.WriteLine("FAIL " + e.ToString().ReplaceLineEndings(" / "));
            code = 1;
        }
        Console.Out.Flush();
        Environment.Exit(code);
    }

    private static int Child()
    {
        var messages = new List<NriMessage>();
        var adapter = NriTestDevice.AdapterFor(GraphicsAPI.D3D12);
        using var device = NriDevice.Create(new NriDeviceOptions
        {
            GraphicsApi = GraphicsAPI.D3D12,
            Adapter = adapter,
            EnableNriValidation = true,
            EnableGraphicsApiValidation = true,
            DeviceLostInfoLevel = DeviceLostInfoLevel.VERBOSE,
            MessageHandler = (in NriMessage m) => { lock (messages) messages.Add(m); },
        });
        List<NriMessage> Since(int start) { lock (messages) return messages.Skip(start).ToList(); }
        var created = Since(0);
        // Any error or warning during creation fails: a DRED warning means no device lost diagnostics
        var unexpected = created.Where(m => m.IsError || m.IsWarning).ToList();
        foreach (var m in unexpected) Console.WriteLine($"FAIL during creation: {m}");
        if (unexpected.Count > 0) return 1;

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var beforeRemoval = allocator.CreateCommandBuffer();
        using var afterRemoval = queue.CreateCommandAllocator();
        using var pending = afterRemoval.CreateCommandBuffer();
        using var fence = device.CreateFence();
        using var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 256 });

        // Work the device ran, then a command buffer recorded but not yet submitted.
        beforeRemoval.Begin();
        beforeRemoval.ZeroBuffer(buffer, 0, 256);
        beforeRemoval.End();
        queue.Submit(beforeRemoval, fence, 1);
        var clock = Stopwatch.StartNew();
        while (fence.Value < 1)
        {
            if (clock.Elapsed > GpuWork.GpuTimeout) throw new TimeoutException("the first submit did not finish");
            Thread.Sleep(1);
        }
        pending.Begin();
        pending.ZeroBuffer(buffer, 0, 256);
        pending.End();

        var beforeRemovalErrors = Since(0).Where(m => m.IsError).ToList();
        foreach (var m in beforeRemovalErrors) Console.WriteLine($"FAIL before RemoveDevice: {m}");
        if (beforeRemovalErrors.Count != 0) return 1;

        var removal = Since(0).Count;
        System.Runtime.InteropServices.Marshal.AddRef(device.GetNativeObject()); // released by the Vortice wrapper's Dispose
        using (var d3d = new V12.ID3D12Device(device.GetNativeObject()))
        using (var d3d5 = d3d.QueryInterfaceOrNull<V12.ID3D12Device5>())
        {
            if (d3d5 is null)
            {
                Console.WriteLine("SKIP the D3D12 device has no ID3D12Device5, so no RemoveDevice");
                return 0;
            }
            d3d5.RemoveDevice();
            Console.WriteLine($"RESULT RemoveDevice: GetDeviceRemovedReason 0x{d3d.DeviceRemovedReason.Code:X8}");
        }
        foreach (var m in Since(removal)) Console.WriteLine($"MESSAGE {m.Type} {m.Text}");

        void Try(string what, Action action)
        {
            var start = Since(0).Count;
            try
            {
                action();
                Console.WriteLine($"RESULT {what} returned");
            }
            catch (NriException e)
            {
                Console.WriteLine($"THROWN {e.Call} {e.GetType().Name} {e.Result}");
                Console.WriteLine($"RESULT {what} threw {e.GetType().Name}: {e.Message.ReplaceLineEndings(" / ")}");
            }
            foreach (var m in Since(start)) Console.WriteLine($"MESSAGE {m.Type} {m.Text}");
        }

        Try("QueueSubmit after RemoveDevice", () => queue.Submit(pending, fence, 2));
        Try("CreateCommittedBuffer after RemoveDevice", () => device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 256 }).Dispose());
        Try("DeviceWaitIdle after RemoveDevice", device.WaitIdle);
        Console.WriteLine($"RESULT fence value after RemoveDevice: 0x{fence.Value:X}");

        var reportStart = Since(0).Count;
        var dump = device.ReportDeviceLostInfo();
        var report = Since(reportStart);
        Console.WriteLine($"RESULT ReportDeviceLostInfo succeeded: {report.Count} message(s), dump of {dump.Length} bytes");
        foreach (var m in report) Console.WriteLine($"REPORT {m.Type} {m.Text}");
        return 0;
    }
}
