// D3D12 on the inbox runtime with a library built for the Agility SDK (NRI_ENABLE_AGILITY_SDK_SUPPORT,
// as every payload here is). NRI queries the device and command lists down to what the runtime has
// ("Using ID3D12Device14" on the inbox runtime); resources must be created through an interface the
// runtime has too. NRI main 3dd4cbfa made ID3D12ResourceBest ID3D12Resource3, which the inbox
// runtime lacks, so every committed, bound and placed resource failed with E_NOINTERFACE
// (patches/0040; AGENTS.md: D3D12 and the Agility SDK).
//
// The inbox runtime is reachable only before a process applies an Agility device factory
// (ApplyToGlobalState is process-wide), so the case runs in a child process whose first D3D12
// device is the inbox one.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed class InboxRuntimeTests(ITestOutputHelper output)
{
    private const string ChildVariable = "SHARPNRI_TEST_CHILD";
    private const string ChildInbox = "d3d12-inbox-resources";

    private static readonly BufferDesc StorageBuffer = new() { size = 256, usage = BufferUsageBits.SHADER_RESOURCE_STORAGE };
    private static readonly TextureDesc SampledTexture = new()
    {
        type = TextureType.TEXTURE_2D, format = Format.RGBA8_UNORM, width = 64, height = 64, usage = TextureUsageBits.SHADER_RESOURCE,
    };

    /// <summary>
    /// A D3D12 device on the inbox runtime creates a committed buffer and texture, binds a buffer and
    /// a texture to allocated memory, and places a buffer and a texture: every resource creation path
    /// NRI has, each through the interface it asks the runtime for. No message at ERROR.
    /// </summary>
    [Fact]
    public void A_device_on_the_inbox_runtime_creates_every_kind_of_resource()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.D3D12));
        var lines = RunChild(ChildInbox);
        foreach (var line in lines.Where(l => l.StartsWith("RESULT ", StringComparison.Ordinal))) GpuWork.Record(line);
        Assert.Contains(lines, l => l.StartsWith("RESULT runtime: inbox", StringComparison.Ordinal));
    }

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
        if (Environment.GetEnvironmentVariable(ChildVariable) != ChildInbox) return;
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
        using var device = NriDevice.Create(new NriDeviceOptions
        {
            GraphicsApi = GraphicsAPI.D3D12,
            Adapter = TestDevice.AdapterFor(GraphicsAPI.D3D12),
            EnableNriValidation = true,
            EnableGraphicsApiValidation = true,
            D3D12AgilitySdkPath = "",
            MessageHandler = (in NriMessage m) => { lock (messages) messages.Add(m); },
        });
        lock (messages)
        {
            if (messages.Any(m => m.Text.Contains("Using Agility SDK", StringComparison.Ordinal)))
            {
                Console.WriteLine("FAIL the device runs on the Agility SDK, not the inbox runtime");
                return 1;
            }
            var version = messages.FirstOrDefault(m => m.Text.Contains("Using ID3D12Device", StringComparison.Ordinal)).Text;
            Console.WriteLine($"RESULT runtime: inbox, NRI says: {version}");
        }

        var failures = 0;
        void Try(string what, Action action)
        {
            try
            {
                action();
                Console.WriteLine($"RESULT {what}: created");
            }
            catch (NriException e)
            {
                Console.WriteLine($"FAIL {what}: {e.Message}");
                failures++;
            }
        }

        Try("committed buffer", () => device.CreateCommittedBuffer(MemoryLocation.DEVICE, StorageBuffer).Dispose());
        Try("committed texture", () => device.CreateCommittedTexture(MemoryLocation.DEVICE, SampledTexture).Dispose());
        Try("bound buffer", () =>
        {
            var requirements = device.GetBufferMemoryDesc(StorageBuffer, MemoryLocation.DEVICE);
            using var memory = device.AllocateMemory(new AllocateMemoryDesc { size = requirements.size, type = requirements.type });
            using var buffer = device.CreateBuffer(StorageBuffer);
            device.BindBufferMemory(buffer, memory);
        });
        Try("bound texture", () =>
        {
            var requirements = device.GetTextureMemoryDesc(SampledTexture, MemoryLocation.DEVICE);
            using var memory = device.AllocateMemory(new AllocateMemoryDesc { size = requirements.size, type = requirements.type });
            using var texture = device.CreateTexture(SampledTexture);
            device.BindTextureMemory(texture, memory);
        });
        Try("placed buffer and texture", () =>
        {
            var bufferRequirements = device.GetBufferMemoryDesc(StorageBuffer, MemoryLocation.DEVICE);
            using (var memory = device.AllocateMemory(new AllocateMemoryDesc { size = bufferRequirements.size, type = bufferRequirements.type }))
                device.CreatePlacedBuffer(memory, 0, StorageBuffer).Dispose();
            var textureRequirements = device.GetTextureMemoryDesc(SampledTexture, MemoryLocation.DEVICE);
            using (var memory = device.AllocateMemory(new AllocateMemoryDesc { size = textureRequirements.size, type = textureRequirements.type }))
                device.CreatePlacedTexture(memory, 0, SampledTexture).Dispose();
        });

        lock (messages)
            foreach (var m in messages.Where(m => m.IsError))
            {
                Console.WriteLine($"FAIL ERROR message: {m.Text}");
                failures++;
            }
        return failures == 0 ? 0 : 1;
    }
}
