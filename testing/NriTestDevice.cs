// A validated NRI device per backend, or a reason there is not one.
//
// Every device here is created with NRI's own validation on and the backend's validation layer on
// (Vulkan validation layers, D3D12 debug layer), with every message recorded. A test asserts on the
// record: the validation layers are the party that decides whether what the code did was legal.
//
// Adapter choice: the software adapter (WARP, lavapipe) when SHARPNRI_TEST_SOFTWARE says so, or by
// default under GitHub Actions, otherwise the first hardware adapter NRI lists for that API. Adapters
// are enumerated per API, so probing Vulkan never runs NRI's D3D pass (which creates D3D12 devices). The NONE backend needs no adapter
// and is always available; it validates nothing and allocates nothing (NRI never wraps it in its
// validation layer), which makes it the right device for lifetime and allocation-counting tests
// and the wrong one for anything that asks a question.
//
// Environment:
//   SHARPNRI_TEST_SOFTWARE=1   prefer the software adapter; =0 prefer hardware, also under GitHub Actions
//   SHARPNRI_TEST_BACKEND=VK   only this backend is available; the others report a reason
//   SHARPNRI_TRACE=1           echo every message to stderr as it arrives, which is the only
//                              record that survives a native crash

using SharpNRI.Native;

namespace SharpNRI.Testing;

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
    public nint VkInstanceCreateInfoNext { get; set; }
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

    /// <summary>
    /// Whether the software adapter is preferred: <c>SHARPNRI_TEST_SOFTWARE</c> <c>1</c> / <c>true</c> or
    /// <c>0</c> / <c>false</c> decides; unset, it is true under GitHub Actions (<c>GITHUB_ACTIONS=true</c>),
    /// whose hosted runners have no GPU.
    /// </summary>
    public static bool PreferSoftware =>
        PreferSoftwareFrom(Environment.GetEnvironmentVariable("SHARPNRI_TEST_SOFTWARE"), Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

    internal static bool PreferSoftwareFrom(string? software, string? githubActions) => software?.Trim().ToLowerInvariant() switch
    {
        "1" or "true" => true,
        "0" or "false" => false,
        _ => githubActions is "true",
    };

    public static bool Trace => Environment.GetEnvironmentVariable("SHARPNRI_TRACE") is "1" or "true";

    public static string? OnlyBackend => Environment.GetEnvironmentVariable("SHARPNRI_TEST_BACKEND");

    /// <summary>The three backends a test can run on, in the order they are usually listed.</summary>
    public static IReadOnlyList<GraphicsAPI> Backends { get; } = [GraphicsAPI.NONE, GraphicsAPI.VK, GraphicsAPI.D3D12];

    /// <summary>The two backends with a GPU or software rasterizer behind them.</summary>
    public static IReadOnlyList<GraphicsAPI> GpuBackends { get; } = [GraphicsAPI.VK, GraphicsAPI.D3D12];

    // ---------------------------------------------------------------- availability

    // Per API: nriEnumerateAdapters probes only the backends asked for (a Vulkan-only enumeration
    // creates no DXGI factory and no D3D12 device; AGENTS.md: Device creation and callbacks).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<GraphicsAPI, Lazy<IReadOnlyList<NriAdapter>>> AdaptersByApi = new();

    private static IReadOnlyList<NriAdapter> AdaptersFor(GraphicsAPI api) =>
        AdaptersByApi.GetOrAdd(api, a => new Lazy<IReadOnlyList<NriAdapter>>(() =>
        {
            try { return NriAdapter.Enumerate(a); }
            catch (Exception) { return []; }
        })).Value;

    // Per API and on first use: asking about Vulkan creates no D3D12 device, and the reverse.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<GraphicsAPI, Lazy<string?>> AvailabilityByApi = new();

    /// <summary>Why <paramref name="api"/> cannot be used on this machine, or null if it can. Probes only <paramref name="api"/>.</summary>
    public static string? Unavailable(GraphicsAPI api)
    {
        if (OnlyBackend is { } only && !string.Equals(only, api.ToString(), StringComparison.OrdinalIgnoreCase))
            return $"SHARPNRI_TEST_BACKEND={only} excludes {api}";
        if (!Backends.Contains(api)) return $"{api} is not a backend this fixture knows";
        return AvailabilityByApi.GetOrAdd(api, a => new Lazy<string?>(() => Probe(a))).Value;
    }

    public static bool IsAvailable(GraphicsAPI api) => Unavailable(api) is null;

    /// <summary>The adapter a device on <paramref name="api"/> is created on, or null for NONE or when none supports it.</summary>
    public static NriAdapter? AdapterFor(GraphicsAPI api)
    {
        if (api == GraphicsAPI.NONE) return null;
        var candidates = AdaptersFor(api).Where(a => a.Supports(api)).ToList();
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
            VkInstanceCreateInfoNext = options.VkInstanceCreateInfoNext,
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

    private static string? Probe(GraphicsAPI api)
    {
        if (api == GraphicsAPI.NONE) return ProbeOne(api);
        if (api == GraphicsAPI.D3D12 && !OperatingSystem.IsWindows()) return "D3D12 is Windows only";
        if (AdaptersFor(api).Count == 0)
            return api == GraphicsAPI.VK
                ? "nriEnumerateAdapters(VK) found no adapter (no Vulkan loader or driver?)"
                : "nriEnumerateAdapters(D3D12) found no adapter";
        if (AdapterFor(api) is null) return $"no adapter supports {api}";
        return ProbeOne(api);
    }

    private static string? ProbeOne(GraphicsAPI api)
    {
        try
        {
            using var probe = Create(api);
            return probe.Api == GraphicsAPI.VK ? StaleValidationLayer(probe.Errors) : null;
        }
        catch (Exception e)
        {
            return $"creating a validated {api} device failed: {e.Message}";
        }
    }

    /// <summary>
    /// A Khronos validation layer older than the Vulkan headers NRI was built with reports the feature
    /// structures NRI chains into <c>vkCreateDevice</c> as unknown, an ERROR on every device, so every
    /// Vulkan test would fail on the environment, not on the code (Ubuntu 24.04's layer 1.3.275 under
    /// NRI v191). The probe answers that as the reason instead.
    /// </summary>
    internal static string? StaleValidationLayer(IEnumerable<NriMessage> errors)
    {
        var unknown = errors.Where(m => m.IsError).Select(m => m.Text).FirstOrDefault(t => t.Contains("unknown VkStructureType", StringComparison.Ordinal));
        if (unknown is null) return null;
        return "the Vulkan validation layer is older than the Vulkan headers NRI was built with: it reports NRI's structures as unknown "
             + $"(\"{Shorten(unknown)}\"). Install a current layer (the Vulkan SDK's; SharpNRI's repository fetches one with tools/ci/fetch-vulkan-layer.sh) "
             + "or create devices with EnableGraphicsApiValidation = false";
    }

    private static string Shorten(string text) => text.Length <= 200 ? text : text[..200] + "...";
}

/// <summary>The validation layers reported at least one error during a test.</summary>
public sealed class NriValidationException(GraphicsAPI api, IReadOnlyList<NriMessage> errors)
    : Exception($"{api}: {errors.Count} validation error(s):\n" + string.Join("\n", errors.Select(e => "  " + e)))
{
    public GraphicsAPI Api { get; } = api;
    public IReadOnlyList<NriMessage> Errors { get; } = errors;
}
