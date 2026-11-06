// Pipeline statistics on a Vulkan device with mesh shaders but without mesh-shader queries. The task
// and mesh counters need VkPhysicalDeviceMeshShaderFeaturesEXT::meshShaderQueries
// (VUID-VkQueryPoolCreateInfo-meshShaderQueries-07069), which NRI reports as
// features.meshShaderPipelineStats; QueryPoolVK added them, and sized each query for them, on
// features.meshShader instead (patches/0042; AGENTS.md: Multisampling, copies and queries).
//
// Every device here has both bits, so the device is made to report meshShaderQueries = false by the
// Khronos Profiles layer (VK_LAYER_KHRONOS_profiles, Vulkan SDK) below the validation layer, with a
// profile that changes that one feature and simulates nothing else. Layers come from the
// environment, so the case runs in a child process with its own environment; it skips where the
// layer is not installed (the child then sees meshShaderQueries unchanged).

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed class MeshShaderQueryTests(ITestOutputHelper output)
{
    private const string ChildVariable = "SHARPNRI_TEST_CHILD";
    private const string ChildName = "vk-no-mesh-shader-queries";
    private const string ProfileName = "VP_SHARPNRI_no_mesh_shader_queries";
    private const int SkipCode = 3;

    private const string Profile = """
        {
            "$schema": "https://schema.khronos.org/vulkan/profiles-0.8-latest.json#",
            "capabilities": {
                "noMeshShaderQueries": {
                    "features": {
                        "VkPhysicalDeviceMeshShaderFeaturesEXT": { "meshShaderQueries": false }
                    }
                }
            },
            "profiles": {
                "VP_SHARPNRI_no_mesh_shader_queries": {
                    "version": 1,
                    "api-version": "1.3.0",
                    "label": "No mesh shader queries",
                    "description": "The device as it is, reporting meshShaderQueries = false",
                    "capabilities": [ "noMeshShaderQueries" ]
                }
            }
        }
        """;

    /// <summary>
    /// With mesh shaders and without mesh-shader queries, a PIPELINE_STATISTICS pool is created
    /// without an ERROR (no task or mesh counter is asked for) and its queries are the 11 counters
    /// without the task and mesh ones, 88 bytes; a pool's query size follows
    /// features.meshShaderPipelineStats, not features.meshShader.
    /// </summary>
    [Fact]
    public void A_pipeline_statistics_pool_leaves_out_the_mesh_counters_without_mesh_shader_queries()
    {
        var backend = new Backend(GraphicsAPI.VK);
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using (var device = TestDevice.Create(GraphicsAPI.VK, messages))
        {
            Assert.SkipWhen(device.Desc.features.pipelineStatistics == 0, $"{backend}: features.pipelineStatistics is false");
            Assert.SkipWhen(device.Desc.features.meshShader == 0, $"{backend}: features.meshShader is false, so no device here has mesh shaders without mesh-shader queries");
        }

        var directory = Path.Combine(Path.GetTempPath(), "sharpnri-profiles-" + Environment.ProcessId);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, ProfileName + ".json"), Profile);
        try
        {
            var (code, lines) = RunChild(directory);
            foreach (var line in lines.Where(l => l.StartsWith("RESULT ", StringComparison.Ordinal))) GpuWork.Record(line);
            Assert.SkipWhen(code == SkipCode, "VK_LAYER_KHRONOS_profiles (Vulkan SDK) is not installed: the device still reports meshShaderQueries");
            Assert.True(code == 0, $"the {ChildName} child exited with 0x{code:X8}: " + string.Join(" | ", lines.Where(l => l.StartsWith("FAIL ", StringComparison.Ordinal))));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private (int Code, string[] Lines) RunChild(string profileDirectory)
    {
        var self = Environment.ProcessPath!;
        var start = new ProcessStartInfo(self) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
        start.Environment[ChildVariable] = ChildName;
        // Validation first (nearest the application), the Profiles layer below it, so validation
        // checks against the profile's features. The layer's settings by environment
        // (VK_KHRONOS_PROFILES_<SETTING>): features only, no API version, no portability emulation.
        start.Environment["VK_INSTANCE_LAYERS"] = "VK_LAYER_KHRONOS_validation" + Path.PathSeparator + "VK_LAYER_KHRONOS_profiles";
        start.Environment["VK_KHRONOS_PROFILES_PROFILE_DIRS"] = profileDirectory;
        start.Environment["VK_KHRONOS_PROFILES_PROFILE_NAME"] = ProfileName;
        start.Environment["VK_KHRONOS_PROFILES_SIMULATE_CAPABILITIES"] = "SIMULATE_FEATURES_BIT";
        start.Environment["VK_KHRONOS_PROFILES_EMULATE_PORTABILITY"] = "false";
        start.Environment["VK_KHRONOS_PROFILES_DEBUG_REPORTS"] = "DEBUG_REPORT_ERROR_BIT";

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
        return (child.ExitCode, lines);
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
        using var device = TestDevice.Create(GraphicsAPI.VK, messages);
        var features = device.Desc.features;
        Console.WriteLine($"RESULT features: meshShader {features.meshShader}, meshShaderPipelineStats {features.meshShaderPipelineStats}, pipelineStatistics {features.pipelineStatistics}");
        if (features.meshShader == 0)
        {
            Console.WriteLine("FAIL the profile removed mesh shaders, not only mesh-shader queries");
            return 1;
        }
        if (features.meshShaderPipelineStats != 0)
            return SkipCode;

        var failures = 0;
        var start = messages.Count;
        using (var pool = device.CreateQueryPool(QueryType.PIPELINE_STATISTICS, 2))
        {
            Console.WriteLine($"RESULT PIPELINE_STATISTICS query size {pool.QuerySize} bytes ({pool.QuerySize / 8} counters)");
            if (pool.QuerySize != 11 * sizeof(ulong))
            {
                Console.WriteLine($"FAIL query size {pool.QuerySize}, expected {11 * sizeof(ulong)} (11 counters, no task or mesh counter)");
                failures++;
            }
        }
        foreach (var m in messages.Skip(start).Where(m => m.IsError))
        {
            Console.WriteLine($"FAIL ERROR message: {m.Text}");
            failures++;
        }
        foreach (var m in messages.Take(start).Where(m => m.IsError))
        {
            Console.WriteLine($"FAIL ERROR message during device creation: {m.Text}");
            failures++;
        }
        return failures == 0 ? 0 : 1;
    }
}
