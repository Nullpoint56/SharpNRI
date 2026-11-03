// Package consumption smoke test (SharpNRI.Consume.csproj). Exit code 0 when the NONE device and at
// least one GPU backend's validated device were created through the packages: WARP on Windows,
// lavapipe on Linux, MoltenVK on macOS.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using SharpNRI;
using SharpNRI.Native;
using SharpNRI.Testing;

Console.WriteLine($"SharpNRI {typeof(NriDevice).Assembly.GetName().Version} from {typeof(NriDevice).Assembly.Location}");
Console.WriteLine($"SharpNRI.Testing from {typeof(NriTestDevice).Assembly.Location}");
Console.WriteLine($"process RID {RuntimeInformation.RuntimeIdentifier}, base directory {AppContext.BaseDirectory}");

// Where the NRI library could come from: flat beside the app (RID-specific build) or under
// runtimes/<rid>/native (portable build, resolved through the deps.json).
var nriFiles = new[] { "NRI.dll", "libNRI.so", "libNRI.dylib" };
foreach (var f in Directory.EnumerateFiles(AppContext.BaseDirectory, "*", SearchOption.AllDirectories)
             .Where(f => nriFiles.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
             .Order())
    Console.WriteLine($"  on disk: {Path.GetRelativePath(AppContext.BaseDirectory, f)}");

// First call into the library: loads it the way every DllImport("NRI") does.
Console.WriteLine($"nriGetGraphicsAPIString(VK) = {Nri.GetGraphicsAPIString(GraphicsAPI.VK)}");
var loaded = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
    .Where(m => nriFiles.Contains(Path.GetFileName(m.FileName), StringComparer.OrdinalIgnoreCase))
    .Select(m => m.FileName).ToList();
Console.WriteLine(loaded.Count > 0 ? $"loaded NRI: {string.Join(", ", loaded)}" : "loaded NRI: not in the process module list");

bool noneOk = false;
var gpuOk = new List<GraphicsAPI>();

try
{
    using var none = NriTestDevice.Create(GraphicsAPI.NONE);
    Summary("NONE", none.Device);
    noneOk = true;
}
catch (Exception e)
{
    Console.WriteLine($"NONE: FAILED {e.GetType().Name}: {e.Message}");
}

foreach (var api in NriTestDevice.GpuBackends)
{
    if (NriTestDevice.Unavailable(api) is { } reason)
    {
        Console.WriteLine($"{api}: unavailable: {reason}");
        continue;
    }
    try
    {
        using var fixture = NriTestDevice.Create(api);
        var device = fixture.Device;
        Console.WriteLine($"{api}: adapter \"{NriTestDevice.AdapterFor(api)?.Name}\"");
        Summary(api.ToString(), device);

        using (var buffer = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 256 }))
            Console.WriteLine($"{api}: buffer created, size {buffer.Desc.size}");
        using (var fence = device.CreateFence(0))
            Console.WriteLine($"{api}: fence created, value {fence.Value}");
        Console.WriteLine($"{api}: live objects after disposal {device.LiveObjects}");

        var errors = fixture.Errors.ToList();
        foreach (var e in errors) Console.WriteLine($"{api}: validation error: {e}");
        if (errors.Count == 0 && device.LiveObjects == 0) gpuOk.Add(api);
        else Console.WriteLine($"{api}: FAILED ({errors.Count} validation errors, {device.LiveObjects} live objects)");
    }
    catch (Exception e)
    {
        Console.WriteLine($"{api}: FAILED {e.GetType().Name}: {e.Message}");
    }
}

bool ok = noneOk && gpuOk.Count > 0;
Console.WriteLine($"result: NONE {(noneOk ? "ok" : "failed")}, GPU backends ok: {(gpuOk.Count > 0 ? string.Join(", ", gpuOk) : "none")} -> {(ok ? "PASS" : "FAIL")}");
return ok ? 0 : 1;

static void Summary(string label, NriDevice device)
{
    ref readonly var desc = ref device.Desc;
    ReadOnlySpan<sbyte> raw = desc.adapterDesc.name;
    var bytes = MemoryMarshal.AsBytes(raw);
    var end = bytes.IndexOf((byte)0);
    var name = Encoding.UTF8.GetString(end < 0 ? bytes : bytes[..end]);
    Console.WriteLine($"{label}: device graphicsAPI {desc.graphicsAPI}, nriVersion {desc.nriVersion}, shaderModel {desc.shaderModel}, " +
                      $"adapter \"{name}\" ({desc.adapterDesc.vendor}, {desc.adapterDesc.architecture})");
}
