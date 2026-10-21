// A validated NRI device per backend, or a reason there is not one.
//
// Every device here is created with NRI's own validation on and the backend's validation layer on
// (Vulkan validation layers, D3D12 debug layer), with every message recorded. A test asserts on the
// record: the validation layers are the party that decides whether what the code did was legal.
//
// Adapter choice: the software adapter (WARP, lavapipe) when PRISMNRI_TEST_SOFTWARE is set or
// under GitHub Actions, otherwise the first adapter NRI lists. The NONE backend needs no adapter
// and is always available; it validates nothing and allocates nothing (NRI never wraps it in its
// validation layer), which makes it the right device for lifetime and allocation-counting tests
// and the wrong one for anything that asks a question.
//
// Environment:
//   PRISMNRI_TEST_SOFTWARE=1   prefer the software adapter
//   PRISMNRI_TEST_BACKEND=VK   only this backend is available; the others report a reason
//   PRISMNRI_TRACE=1           echo every message to stderr as it arrives, which is the only
//                              record that survives a native crash

using PrismNRI.Native;

namespace PrismNRI.Testing;

/// <summary>Options a test may adjust before the device is created. Defaults are the validated ones.</summary>
public sealed class NriTestDeviceOptions
{
    public NriAdapter? Adapter { get; set; }
    public bool EnableNriValidation { get; set; } = true;
    public bool EnableGraphicsApiValidation { get; set; } = true;
    public bool BreakOnError { get; set; }
    public INriAllocator? Allocator { get; set; }
    public bool ForwardAllocatorToThirdParty { get; set; }
    public IReadOnlyList<NriQueueFamily> QueueFamilies { get; set; } = [];
    public VKBindingOffsets VkBindingOffsets { get; set; }
    public IReadOnlyList<string> VkInstanceExtensions { get; set; } = [];
    public IReadOnlyList<string> VkDeviceExtensions { get; set; } = [];
    public string? D3D12AgilitySdkPath { get; set; }

    /// <summary>Called for every message in addition to the device's own record.</summary>
    public NriMessageHandler? MessageHandler { get; set; }
}

/// <summary>A validated device and everything NRI said while it lived.</summary>
public sealed class NriTestDevice : IDisposable
{
    private readonly List<NriMessage> _messages = [];
    private readonly Lock _lock = new();

    private NriTestDevice(GraphicsAPI api)
    {
        Api = api;
        Device = null!;
    }

    public GraphicsAPI Api { get; }
    public NriDevice Device { get; private set; }

    /// <summary>Every message so far, in order.</summary>
    public IReadOnlyList<NriMessage> Messages
    {
        get { lock (_lock) return _messages.ToArray(); }
    }

    public IEnumerable<NriMessage> Errors => Messages.Where(m => m.IsError);
    public IEnumerable<NriMessage> Warnings => Messages.Where(m => m.IsWarning);

    /// <summary>Throws with every error message listed, for a test's final check.</summary>
    public void ThrowIfErrors()
    {
        var errors = Errors.ToList();
        if (errors.Count == 0) return;
        throw new NriValidationException(Api, errors);
    }

    public void Dispose() => Device?.Dispose();

    // ---------------------------------------------------------------- environment

    public static bool PreferSoftware =>
        Environment.GetEnvironmentVariable("PRISMNRI_TEST_SOFTWARE") is "1" or "true"
        || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") is "true";

    public static bool Trace => Environment.GetEnvironmentVariable("PRISMNRI_TRACE") is "1" or "true";

    public static string? OnlyBackend => Environment.GetEnvironmentVariable("PRISMNRI_TEST_BACKEND");

    /// <summary>The three backends a test can run on, in the order they are usually listed.</summary>
    public static IReadOnlyList<GraphicsAPI> Backends { get; } = [GraphicsAPI.NONE, GraphicsAPI.VK, GraphicsAPI.D3D12];

    /// <summary>The two backends with a GPU or software rasterizer behind them.</summary>
    public static IReadOnlyList<GraphicsAPI> GpuBackends { get; } = [GraphicsAPI.VK, GraphicsAPI.D3D12];

    // ---------------------------------------------------------------- availability

    private static readonly Lazy<IReadOnlyList<NriAdapter>> Adapters = new(() =>
    {
        try { return NriAdapter.Enumerate(); }
        catch (Exception) { return []; }
    });

    private static readonly Lazy<Dictionary<GraphicsAPI, string?>> Availability = new(Probe);

    /// <summary>Why <paramref name="api"/> cannot be used on this machine, or null if it can.</summary>
    public static string? Unavailable(GraphicsAPI api)
    {
        if (OnlyBackend is { } only && !string.Equals(only, api.ToString(), StringComparison.OrdinalIgnoreCase))
            return $"PRISMNRI_TEST_BACKEND={only} excludes {api}";
        return Availability.Value.TryGetValue(api, out var reason) ? reason : $"{api} is not a backend this fixture knows";
    }

    public static bool IsAvailable(GraphicsAPI api) => Unavailable(api) is null;

    /// <summary>The adapter a device on <paramref name="api"/> is created on, or null for NONE or when none supports it.</summary>
    public static NriAdapter? AdapterFor(GraphicsAPI api)
    {
        if (api == GraphicsAPI.NONE) return null;
        var candidates = Adapters.Value.Where(a => a.Supports(api)).ToList();
        if (candidates.Count == 0) return null;
        if (PreferSoftware && candidates.FirstOrDefault(a => a.IsSoftware) is { } software) return software;
        return candidates.FirstOrDefault(a => !a.IsSoftware) ?? candidates[0];
    }

    // ---------------------------------------------------------------- creation

    /// <summary>
    /// Creates a validated device. Throws <see cref="NriException"/> when creation fails; check
    /// <see cref="Unavailable"/> first to skip instead.
    /// </summary>
    public static NriTestDevice Create(GraphicsAPI api, Action<NriTestDeviceOptions>? configure = null)
    {
        var options = new NriTestDeviceOptions { Adapter = AdapterFor(api) };
        if (api == GraphicsAPI.NONE) options.EnableGraphicsApiValidation = false;
        configure?.Invoke(options);

        var fixture = new NriTestDevice(api);
        var forward = options.MessageHandler;
        fixture.Device = NriDevice.Create(new NriDeviceOptions
        {
            GraphicsApi = api,
            Adapter = options.Adapter,
            EnableNriValidation = options.EnableNriValidation,
            EnableGraphicsApiValidation = options.EnableGraphicsApiValidation,
            BreakOnError = options.BreakOnError,
            Allocator = options.Allocator,
            ForwardAllocatorToThirdParty = options.ForwardAllocatorToThirdParty,
            QueueFamilies = options.QueueFamilies,
            VkBindingOffsets = options.VkBindingOffsets,
            VkInstanceExtensions = options.VkInstanceExtensions,
            VkDeviceExtensions = options.VkDeviceExtensions,
            D3D12AgilitySdkPath = options.D3D12AgilitySdkPath,
            MessageHandler = (in NriMessage m) =>
            {
                if (Trace) Console.Error.WriteLine($"[nri {api}] {m}");
                lock (fixture._lock) fixture._messages.Add(m);
                forward?.Invoke(in m);
            },
        });
        return fixture;
    }

    private static Dictionary<GraphicsAPI, string?> Probe()
    {
        var result = new Dictionary<GraphicsAPI, string?> { [GraphicsAPI.NONE] = ProbeOne(GraphicsAPI.NONE) };

        if (Adapters.Value.Count == 0)
        {
            result[GraphicsAPI.VK] = "nriEnumerateAdapters found no adapters (no Vulkan loader or no D3D12 runtime?)";
            result[GraphicsAPI.D3D12] = result[GraphicsAPI.VK];
            return result;
        }

        foreach (var api in GpuBackends)
        {
            if (api == GraphicsAPI.D3D12 && !OperatingSystem.IsWindows())
            {
                result[api] = "D3D12 is Windows only";
                continue;
            }
            if (AdapterFor(api) is null)
            {
                result[api] = $"no adapter supports {api}";
                continue;
            }
            result[api] = ProbeOne(api);
        }
        return result;
    }

    private static string? ProbeOne(GraphicsAPI api)
    {
        try
        {
            using var probe = Create(api);
            return null;
        }
        catch (Exception e)
        {
            return $"creating a validated {api} device failed: {e.Message}";
        }
    }
}

/// <summary>The validation layers reported at least one error during a test.</summary>
public sealed class NriValidationException(GraphicsAPI api, IReadOnlyList<NriMessage> errors)
    : Exception($"{api}: {errors.Count} validation error(s):\n" + string.Join("\n", errors.Select(e => "  " + e)))
{
    public GraphicsAPI Api { get; } = api;
    public IReadOnlyList<NriMessage> Errors { get; } = errors;
}
