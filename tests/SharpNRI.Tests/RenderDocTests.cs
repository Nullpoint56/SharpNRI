// A D3D12 frame capture through RenderDoc's in-application API, with NRI's device and its newest
// interface. NRI queries ID3D12Device15 down to ID3D12Device and keeps the first that succeeds
// (QueryLatestInterface, DeviceD3D12.hpp); a capturer that wraps the device must hand back its own
// wrapper for every version it answers, or the device NRI records with bypasses it. This measures
// whether it does: COM identity (the IUnknown) of each ID3D12DeviceN queried from NRI's device, and
// a capture of one submit made on that device, on the inbox runtime and with the Agility SDK.
// CLAUDE.md: D3D12 and the Agility SDK.
//
// The capture needs RenderDoc injected at process start, so the case runs in a child process (this
// test executable started by "renderdoccmd capture" with SHARPNRI_TEST_CHILD set; the module
// initializer below runs it and exits before xunit starts). The child writes its lines to the file
// named by SHARPNRI_TEST_RESULT_FILE, since a process launched by renderdoccmd does not share the
// parent's pipes. The control runs the same child without RenderDoc: renderdoc.dll is absent and
// the child says so.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpNRI.Native;
using SharpNRI.Testing;
using Xunit;

namespace SharpNRI.Tests;

public sealed unsafe partial class RenderDocTests(ITestOutputHelper output)
{
    private const string ChildVariable = "SHARPNRI_TEST_CHILD";
    private const string ResultFileVariable = "SHARPNRI_TEST_RESULT_FILE";
    private const string ChildAgility = "renderdoc-agility";
    private const string ChildInbox = "renderdoc-inbox";
    private const int ApiVersion160 = 10600; // eRENDERDOC_API_Version_1_6_0

    /// <summary>renderdoccmd.exe in RenderDoc's default install directory or on PATH, or null.</summary>
    private static string? FindRenderDocCmd()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RenderDoc", "renderdoccmd.exe");
        if (File.Exists(installed)) return installed;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim('"'), "renderdoccmd.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <summary>
    /// Under RenderDoc (renderdoccmd capture), with the inbox runtime and with the Agility SDK: every
    /// ID3D12DeviceN that NRI's device answers has the same IUnknown as the device; on the inbox
    /// runtime a frame capture around one submit on NRI's graphics queue is made, with the Agility
    /// SDK RenderDoc 1.44 does not wrap the device and captures nothing (asserted, see below).
    /// The control without RenderDoc reports renderdoc.dll absent. Skips without renderdoccmd.exe,
    /// which CI runners do not have.
    /// </summary>
    [Fact]
    public void A_D3D12_frame_capture_under_RenderDoc_records_NRIs_device()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.D3D12));
        var renderDoc = FindRenderDocCmd();
        Assert.SkipWhen(renderDoc is null, "RenderDoc is not installed: no renderdoccmd.exe in Program Files\\RenderDoc or on PATH");

        var control = RunChild(ChildInbox, null);
        Assert.Contains(control, l => l.StartsWith("RESULT renderdoc.dll is not in the process", StringComparison.Ordinal));

        var runs = new[] { ChildAgility, ChildInbox }.Select(name => (Name: name, Lines: RunChild(name, renderDoc))).ToList();
        foreach (var (name, lines) in runs)
        {
            Assert.True(lines.Any(l => l.StartsWith("RESULT capture ", StringComparison.Ordinal)), $"{name}: the child under RenderDoc reported no capture: " + string.Join(" | ", lines));
            Assert.DoesNotContain(lines, l => l.StartsWith("FAIL ", StringComparison.Ordinal));
            // QueryLatestInterface keeps COM identity under RenderDoc on both routes.
            Assert.DoesNotContain(lines, l => l.Contains("IUnknown differs", StringComparison.Ordinal));
        }

        // Inbox runtime: RenderDoc wraps the device (its vtable is in renderdoc.dll), answers up to
        // ID3D12Device14 (NRI reports "Using ID3D12Device14"), and the capture is made.
        var inbox = runs.Single(r => r.Name == ChildInbox).Lines;
        Assert.Contains(inbox, l => l.StartsWith("RESULT NRI's device ", StringComparison.Ordinal) && l.Contains("(vtable in renderdoc.dll)", StringComparison.Ordinal));
        Assert.Contains(inbox, l => l.StartsWith("RESULT capture EndFrameCapture=1", StringComparison.Ordinal));

        // Agility SDK applied through ID3D12DeviceFactory::ApplyToGlobalState (patch 0001): measured
        // with RenderDoc 1.44, the device D3D12CreateDevice returns is D3D12Core.dll's own, for the
        // base ID3D12Device as for ID3D12Device15, so RenderDoc never sees it and EndFrameCapture
        // returns 0. Asserted so that a RenderDoc that captures this route fails here and the record
        // (CLAUDE.md: D3D12 and the Agility SDK) is revisited; capture with D3D12AgilitySdkPath = "".
        var agility = runs.Single(r => r.Name == ChildAgility).Lines;
        Assert.Contains(agility, l => l.StartsWith("RESULT NRI's device ", StringComparison.Ordinal) && l.Contains("(vtable in D3D12Core.dll)", StringComparison.Ordinal));
        Assert.DoesNotContain(agility, l => l.StartsWith("RESULT capture EndFrameCapture=1", StringComparison.Ordinal));
    }

    private string[] RunChild(string name, string? renderDoc)
    {
        var self = Environment.ProcessPath!;
        var resultFile = Path.Combine(Path.GetTempPath(), $"sharpnri-{name}-{Environment.ProcessId}-{Guid.NewGuid():N}.txt");
        var captureDir = Path.Combine(Path.GetTempPath(), $"sharpnri-rdoc-{Guid.NewGuid():N}");
        var start = new ProcessStartInfo(renderDoc ?? self) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (renderDoc is not null)
        {
            Directory.CreateDirectory(captureDir);
            foreach (var arg in new[] { "capture", "--wait-for-exit", "--working-dir", AppContext.BaseDirectory, "--capture-file", Path.Combine(captureDir, "cap"), self })
                start.ArgumentList.Add(arg);
        }
        if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
        start.Environment[ChildVariable] = name;
        start.Environment[ResultFileVariable] = resultFile;

        try
        {
            using var child = Process.Start(start)!;
            var stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            var stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            if (!child.WaitForExit(GpuWork.GpuTimeout))
            {
                child.Kill(entireProcessTree: true);
                Assert.Fail($"the {name} child did not finish within {GpuWork.GpuTimeout}");
            }
            child.WaitForExit();
            var label = renderDoc is null ? name : name + " under RenderDoc";
            foreach (var line in stdout.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) output.WriteLine($"{label} stdout: {line}");
            if (stderr.Result.Length != 0) output.WriteLine($"{label} stderr: {stderr.Result}");
            Assert.True(File.Exists(resultFile), $"the {label} child wrote no result file (exit code {child.ExitCode})");
            var lines = File.ReadAllLines(resultFile);
            foreach (var line in lines.Where(l => l.StartsWith("RESULT ", StringComparison.Ordinal) || l.StartsWith("FAIL ", StringComparison.Ordinal)))
                GpuWork.Record($"{label}: {line}");
            Assert.True(child.ExitCode == 0, $"the {label} child exited with {child.ExitCode}: " + string.Join(" | ", lines.Where(l => l.StartsWith("FAIL ", StringComparison.Ordinal))));
            return lines;
        }
        finally
        {
            try { File.Delete(resultFile); } catch (IOException) { }
            try { if (Directory.Exists(captureDir)) Directory.Delete(captureDir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    // ---------------------------------------------------------------- the child

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandle(string name);

    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "the child mode must run before xunit's entry point")]
    internal static void RunChildIfAsked()
    {
        var name = Environment.GetEnvironmentVariable(ChildVariable);
        if (name is not (ChildAgility or ChildInbox)) return;
        var lines = new List<string>();
        void Write(string line)
        {
            lines.Add(line);
            Console.WriteLine(line);
        }
        int code;
        try
        {
            code = Child(name == ChildAgility, Write);
        }
        catch (Exception e)
        {
            Write("FAIL " + e.ToString().ReplaceLineEndings(" / "));
            code = 1;
        }
        Console.Out.Flush();
        if (Environment.GetEnvironmentVariable(ResultFileVariable) is { Length: > 0 } file) File.WriteAllLines(file, lines);
        Environment.Exit(code);
    }

    private static int Child(bool agility, Action<string> write)
    {
        var module = OperatingSystem.IsWindows() ? GetModuleHandle("renderdoc.dll") : 0;
        if (module == 0)
        {
            write("RESULT renderdoc.dll is not in the process: RenderDoc did not inject it, so there is no in-application API and nothing to capture");
            return 0;
        }
        if (!NativeLibrary.TryGetExport(module, "RENDERDOC_GetAPI", out var getApiExport))
        {
            write("FAIL renderdoc.dll has no RENDERDOC_GetAPI export");
            return 1;
        }
        void** api = null;
        var ok = ((delegate* unmanaged<int, void***, int>)getApiExport)(ApiVersion160, &api);
        if (ok != 1 || api == null)
        {
            write($"FAIL RENDERDOC_GetAPI(1.6.0) returned {ok}");
            return 1;
        }
        int major, minor, patch;
        ((delegate* unmanaged<int*, int*, int*, void>)api[0])(&major, &minor, &patch);
        var fileVersion = FileVersionInfo.GetVersionInfo(Process.GetCurrentProcess().Modules.Cast<ProcessModule>().First(m => m.ModuleName.Equals("renderdoc.dll", StringComparison.OrdinalIgnoreCase)).FileName).FileVersion;
        write($"RESULT RenderDoc in-application API {major}.{minor}.{patch}, renderdoc.dll {fileVersion}");

        var messages = new List<NriMessage>();
        using var device = NriDevice.Create(new NriDeviceOptions
        {
            GraphicsApi = GraphicsAPI.D3D12,
            Adapter = NriTestDevice.AdapterFor(GraphicsAPI.D3D12),
            EnableNriValidation = true,
            EnableGraphicsApiValidation = false,
            D3D12AgilitySdkPath = agility ? null : "",
            MessageHandler = (in NriMessage m) => { lock (messages) messages.Add(m); },
        });
        lock (messages)
            foreach (var m in messages.Where(m => m.Text.Contains("Using ID3D12Device", StringComparison.Ordinal) || m.Text.Contains("Agility SDK", StringComparison.Ordinal)))
                write($"RESULT {(agility ? "Agility" : "inbox")}: NRI says: {m.Text}");

        // COM identity: every interface queried from one object must give the same IUnknown.
        var native = device.GetNativeObject();
        var unknownIid = new Guid("00000000-0000-0000-C000-000000000046");
        var identity = QueryUnknown(native, unknownIid);
        write($"RESULT NRI's device 0x{native:X} (vtable in {VtableModule(native)}), its IUnknown 0x{identity:X}");
        var asm = typeof(Vortice.Direct3D12.ID3D12Device).Assembly;
        for (var n = 15; n >= 0; n--)
        {
            var type = asm.GetType(n == 0 ? "Vortice.Direct3D12.ID3D12Device" : $"Vortice.Direct3D12.ID3D12Device{n}");
            Guid iid;
            if (type is not null)
                iid = type.GUID;
            else if (n == 15)
                iid = new Guid("76cff76f-1e9b-4450-8cdc-34f1af788e5b"); // d3d12.h, Agility SDK 1.619
            else
            {
                write($"RESULT ID3D12Device{(n == 0 ? "" : n)}: no IID known to Vortice.Direct3D12, not queried");
                continue;
            }
            if (Marshal.QueryInterface(native, in iid, out var versioned) != 0 || versioned == 0)
            {
                write($"RESULT ID3D12Device{(n == 0 ? "" : n)}: E_NOINTERFACE");
                continue;
            }
            var versionedIdentity = QueryUnknown(versioned, unknownIid);
            write($"RESULT ID3D12Device{(n == 0 ? "" : n)}: 0x{versioned:X} (vtable in {VtableModule(versioned)}), IUnknown 0x{versionedIdentity:X}, " +
                (versionedIdentity == identity ? "same IUnknown" : "IUnknown differs from the device's"));
            Marshal.Release(versioned);
        }

        // One submit inside a frame capture, on the device NRI records with.
        var start = ((delegate* unmanaged<void*, void*, void>)api[19]);
        var end = ((delegate* unmanaged<void*, void*, uint>)api[21]);
        var count = ((delegate* unmanaged<int>)api[13]);
        foreach (var withDevice in new[] { false, true })
        {
            var devicePointer = withDevice ? (void*)native : null;
            start(devicePointer, null);
            using (var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 256, usage = BufferUsageBits.SHADER_RESOURCE_STORAGE }))
            {
                var queue = device.GetQueue(QueueType.GRAPHICS);
                using var allocator = queue.CreateCommandAllocator();
                using var commandBuffer = allocator.CreateCommandBuffer();
                using var fence = device.CreateFence();
                commandBuffer.Begin();
                commandBuffer.ZeroBuffer(buffer, 0, 256);
                commandBuffer.End();
                queue.Submit(commandBuffer, fence, 1);
                var clock = Stopwatch.StartNew();
                while (fence.Value < 1 && clock.Elapsed < GpuWork.GpuTimeout) Thread.Sleep(1);
            }
            var captured = end(devicePointer, null);
            write($"RESULT capture EndFrameCapture={captured}, GetNumCaptures={count()} ({(agility ? "Agility" : "inbox")}, StartFrameCapture({(withDevice ? "NRI's device" : "NULL")}, NULL))");
        }
        lock (messages)
            foreach (var m in messages.Where(m => m.IsError)) write("FAIL NRI error: " + m.Text);
        return 0;
    }

    /// <summary>The module that holds the first vtable entry (QueryInterface) of a COM object.</summary>
    private static string VtableModule(nint pointer)
    {
        var queryInterface = (nint)(*(void***)pointer)[0];
        foreach (ProcessModule m in Process.GetCurrentProcess().Modules)
            if (queryInterface >= m.BaseAddress && queryInterface < m.BaseAddress + m.ModuleMemorySize)
                return m.ModuleName;
        return "no module";
    }

    private static nint QueryUnknown(nint pointer, Guid unknownIid)
    {
        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(pointer, in unknownIid, out var unknown));
        Marshal.Release(unknown); // the object stays alive through the caller's reference; only the address is compared
        return unknown;
    }
}
