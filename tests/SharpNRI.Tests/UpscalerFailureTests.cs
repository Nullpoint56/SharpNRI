// A vendor upscaler that fails must say why, and must not be NRI's generic FAILURE (NRI PR #324, the
// binding's former patch 0049). NRI reports each failed vendor call as an ERROR naming the call and its
// result, and returns EXT_ERROR (a third-party failure, which never calls AbortExecution) unless the
// vendor's code maps to INVALID_ARGUMENT, UNSUPPORTED or OUT_OF_MEMORY. A missing FSR loader is
// EXT_ERROR (AGENTS.md: Upscalers and vendor runtimes).
//
// The creation case runs in a child process started from a copy of the test directory without the
// vendor runtimes, since NRI loads them by bare name and Windows searches the executable's directory
// first. The child calls NRI's CreateUpscaler through the raw table, past the binding's runtime-file
// guard (NriUpscalerExtension.CreateUpscaler refuses a missing file before NRI is called). XeSS is not
// part of it: NRI links libxess.dll at load time, and this repository's build delay-loads it, so a
// missing libxess.dll is the delay-load helper's exception (0xC06D007E) there, not an NRI result.
//
// Local only, like every upscaler test: it needs the Upscalers or upscalers-dlss payload
// (-p:SharpNRIDevPayload), and on the Base payload no vendor type is compiled in and it skips.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using SharpNRI.Native;
using SharpNRI.Testing;
using Xunit;

namespace SharpNRI.Tests;

public sealed class UpscalerFailureTests(ITestOutputHelper output)
{
    private const string ChildVariable = "SHARPNRI_TEST_CHILD";
    private const string ChildName = "upscaler-missing-runtime";
    private static readonly UpscalerType[] Vendor = [UpscalerType.FSR, UpscalerType.DLSR, UpscalerType.DLRR];

    // Every file NRI or a vendor loader may load by name; removed from the child's directory
    private static readonly string[] RuntimePrefixes = ["amd_fidelityfx_", "nvngx_", "libxess"];

    [Fact]
    public async Task Creating_a_vendor_upscaler_without_its_runtime_reports_why()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "the vendor runtimes are Windows DLLs here");
        var testDir = AppContext.BaseDirectory;
        Assert.SkipUnless(Directory.EnumerateFiles(testDir).Any(f => RuntimePrefixes.Any(p => Path.GetFileName(f).StartsWith(p, StringComparison.OrdinalIgnoreCase))),
            "no vendor runtime beside the tests: the Base payload has no vendor upscaler compiled in (-p:SharpNRIDevPayload=upscalers or upscalers-dlss)");

        // A copy of the test directory without the runtimes
        var copy = Path.Combine(Path.GetTempPath(), "sharpnri-upscaler-missing-runtime-" + Environment.ProcessId);
        if (Directory.Exists(copy)) Directory.Delete(copy, recursive: true);
        foreach (var dir in Directory.EnumerateDirectories(testDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(copy, Path.GetRelativePath(testDir, dir)));
        foreach (var file in Directory.EnumerateFiles(testDir, "*", SearchOption.AllDirectories))
        {
            if (RuntimePrefixes.Any(p => Path.GetFileName(file).StartsWith(p, StringComparison.OrdinalIgnoreCase))) continue;
            File.Copy(file, Path.Combine(copy, Path.GetRelativePath(testDir, file)));
        }

        try
        {
            var self = Environment.ProcessPath!;
            var dotnetHost = Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase);
            var start = new ProcessStartInfo(dotnetHost ? self : Path.Combine(copy, Path.GetFileName(self)))
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = copy,
            };
            if (dotnetHost) start.ArgumentList.Add(Path.Combine(copy, Path.GetFileName(Assembly.GetEntryAssembly()!.Location)));
            start.Environment[ChildVariable] = ChildName;

            using var child = Process.Start(start)!;
            var stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            if (!child.WaitForExit(GpuWork.GpuTimeout))
            {
                child.Kill(entireProcessTree: true);
                Assert.Fail($"the child did not finish within {GpuWork.GpuTimeout}");
            }
            var lines = (await stdout).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var line in lines) output.WriteLine(line);
            var errors = await stderr;
            if (errors.Length != 0) output.WriteLine("stderr: " + errors);

            var results = lines.Where(l => l.StartsWith("RESULT ", StringComparison.Ordinal)).ToList();
            Assert.SkipWhen(results.Count == 0 && child.ExitCode == 0, "no vendor upscaler type is compiled into the staged NRI.dll");
            Assert.True(child.ExitCode == 0, $"child exit code 0x{child.ExitCode:X8}: " + string.Join(" | ", lines.Where(l => l.StartsWith("FAIL ", StringComparison.Ordinal))));
            foreach (var result in results) GpuWork.Record(result);
        }
        finally
        {
            try { Directory.Delete(copy, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    // ================================================================================= the child

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

    private static unsafe int Child()
    {
        var failed = false;
        foreach (var api in NriTestDevice.GpuBackends)
        {
            if (!NriTestDevice.IsAvailable(api)) continue;
            var messages = new List<NriMessage>();
            using var device = TestDevice.Create(api, messages);
            if (device.TryUpscaler is null) continue;

            UpscalerInterface table;
            fixed (byte* name = "UpscalerInterface\0"u8)
            {
                var r = NriApi.nriGetInterface((Device*)device.NriHandle, (sbyte*)name, (nuint)sizeof(UpscalerInterface), &table);
                if (r != Result.SUCCESS) throw new InvalidOperationException($"nriGetInterface(UpscalerInterface): {r}");
            }

            foreach (var type in Vendor)
            {
                if (table.IsUpscalerSupported((Device*)device.NriHandle, type) == 0) continue;
                var desc = new UpscalerDesc { upscaleResolution = new Dim2_t { w = 1920, h = 1080 }, type = type, mode = UpscalerMode.QUALITY };
                Upscaler* upscaler = null;
                var start = messages.Count;
                var result = table.CreateUpscaler((Device*)device.NriHandle, &desc, &upscaler);
                if (upscaler is not null) table.DestroyUpscaler(upscaler);
                List<NriMessage> provoked;
                lock (messages) provoked = messages.Skip(start).ToList();
                var errors = provoked.Where(m => m.IsError).ToList();
                Console.WriteLine($"RESULT {api} {type} without its runtime: {result}; {(errors.Count == 0 ? "no message" : string.Join(" | ", errors.Select(m => m.Text)))}");
                // NGX also loads its feature DLLs from outside the application (measured: DLRR created here
                // with nvngx_dlssd.dll removed beside the tests, while DLSR failed), so a creation can succeed
                // without the runtime beside the executable: recorded, nothing to report then
                if (result == Result.SUCCESS)
                    continue;
                if (errors.Count == 0)
                {
                    Console.WriteLine($"FAIL {api} {type}: {result} with no message saying why");
                    failed = true;
                }
                if (result == Result.FAILURE || (type == UpscalerType.FSR && result != Result.EXT_ERROR))
                {
                    Console.WriteLine($"FAIL {api} {type}: {result}, expected EXT_ERROR for a missing vendor library{(type == UpscalerType.FSR ? "" : " or the vendor's mapped result")}");
                    failed = true;
                }
            }
        }
        return failed ? 1 : 0;
    }
}
