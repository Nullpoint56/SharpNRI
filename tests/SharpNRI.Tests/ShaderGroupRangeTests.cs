// WriteShaderGroupIdentifiers with a group range past the pipeline's groups. NRI validation checked
// the stride only (PipelineVal.hpp), so the range reached the backend: D3D12 indexes its group names
// without a bound (PipelineD3D12.hpp), Vulkan hands the range to vkGetRayTracingShaderGroupHandlesKHR,
// which the layer refuses. Validation now refuses it (patches/0041; AGENTS.md: Ray tracing).
//
// The unpatched D3D12 path reads past an array, which may end the process, so each case runs in a
// child process (this test executable started again with SHARPNRI_TEST_CHILD set).

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed class ShaderGroupRangeTests(ITestOutputHelper output)
{
    private const string ChildVariable = "SHARPNRI_TEST_CHILD";
    private const string ChildName = "rt-group-range";
    private const string ApiVariable = "SHARPNRI_TEST_CHILD_API";

    /// <summary>
    /// On the three-group pipeline of <see cref="RayTracingPipelineTests"/>, groups 1 and 2 are
    /// written; groups 2 and 3 (one past the last), and group 3 alone, are refused by NRI validation
    /// with INVALID_ARGUMENT, before the backend is called: no other message at ERROR, and the
    /// destination bytes are untouched.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void WriteShaderGroupIdentifiers_refuses_a_group_range_past_the_pipelines_groups(Backend backend)
    {
        var messages = new List<NriMessage>();
        RayScene.CreateDevice(backend, messages).Dispose(); // skips naming the missing ray tracing tier
        var lines = RunChild(backend.Api);
        foreach (var line in lines.Where(l => l.StartsWith("RESULT ", StringComparison.Ordinal))) GpuWork.Record($"{backend}: {line}");
        Assert.Contains(lines, l => l == "RESULT in range: written");
    }

    private string[] RunChild(GraphicsAPI api)
    {
        var self = Environment.ProcessPath!;
        var start = new ProcessStartInfo(self) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
        start.Environment[ChildVariable] = ChildName;
        start.Environment[ApiVariable] = api.ToString();

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
        foreach (var line in lines) output.WriteLine($"{ChildName}: {line}");
        if (stderr.Result.Length != 0) output.WriteLine($"{ChildName} stderr: {stderr.Result}");
        Assert.True(child.ExitCode == 0, $"the {ChildName} child ({api}) exited with 0x{child.ExitCode:X8}: " + string.Join(" | ", lines.Where(l => l.StartsWith("FAIL ", StringComparison.Ordinal))));
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
            code = Child(Enum.Parse<GraphicsAPI>(Environment.GetEnvironmentVariable(ApiVariable)!));
        }
        catch (Exception e)
        {
            Console.WriteLine("FAIL " + e.ToString().ReplaceLineEndings(" / "));
            code = 1;
        }
        Console.Out.Flush();
        Environment.Exit(code);
    }

    private static int Child(GraphicsAPI api)
    {
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(api, messages);
        var rt = device.RayTracing;
        using var scene = new RayScene(device, outputNum: 1);
        using var pipeline = RayTracingPipelineTests.CreatePipeline(device, scene);
        var stride = device.Desc.shaderStage.rayTracing.shaderGroupIdentifierSize;
        var failures = 0;

        var inRange = new byte[2 * stride];
        rt.WriteShaderGroupIdentifiers(pipeline, 1, 2, stride, inRange);
        Console.WriteLine("RESULT in range: written");

        foreach (var (first, count) in new[] { (2u, 2u), (3u, 1u) })
        {
            var start = messages.Count;
            var dst = Enumerable.Repeat((byte)0xCD, (int)(count * stride)).ToArray();
            Console.Out.Flush(); // the unpatched D3D12 read may end the process here
            try
            {
                rt.WriteShaderGroupIdentifiers(pipeline, first, count, stride, dst);
                Console.WriteLine($"FAIL groups {first}..{first + count - 1} of 3: written (SUCCESS)");
                failures++;
            }
            catch (NriException e) when (e.Result == Result.INVALID_ARGUMENT)
            {
                Console.WriteLine($"RESULT groups {first}..{first + count - 1} of 3: {e.Message}");
            }
            catch (NriException e)
            {
                Console.WriteLine($"FAIL groups {first}..{first + count - 1} of 3: {e.Result}, not INVALID_ARGUMENT: {e.Message}");
                failures++;
            }
            if (dst.Any(b => b != 0xCD))
            {
                Console.WriteLine($"FAIL groups {first}..{first + count - 1} of 3: the destination was written");
                failures++;
            }
            var errors = messages.Skip(start).Where(m => m.IsError).ToList();
            if (errors.Count != 1)
            {
                foreach (var m in errors) Console.WriteLine($"FAIL groups {first}..{first + count - 1} of 3: ERROR message: {m.Text}");
                if (errors.Count == 0) Console.WriteLine($"FAIL groups {first}..{first + count - 1} of 3: no ERROR message");
                failures++;
            }
        }
        foreach (var m in messages.Where(m => m.IsError && !m.Text.Contains("WriteShaderGroupIdentifiers", StringComparison.Ordinal)))
        {
            Console.WriteLine($"FAIL ERROR message outside the refused calls: {m.Text}");
            failures++;
        }
        return failures == 0 ? 0 : 1;
    }
}
