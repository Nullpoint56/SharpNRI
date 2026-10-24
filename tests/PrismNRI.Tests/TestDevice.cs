// The xunit face of PrismNRI.Testing: theory data per backend, skip-with-reason, and the
// `Create(api, messages, configure)` shape every test in this project uses. The fixture itself,
// adapter choice and environment switches live in the package so a downstream test project gets
// the same device.

using PrismNRI.Native;
using PrismNRI.Testing;
using Xunit;

namespace PrismNRI.Tests;

/// <summary>One backend a test can run on.</summary>
public sealed record Backend(GraphicsAPI Api)
{
    public override string ToString() => Api.ToString();
}

public static class TestDevice
{
    public static bool PreferSoftware => NriTestDevice.PreferSoftware;
    public static bool Trace => NriTestDevice.Trace;
    public static string? OnlyBackend => NriTestDevice.OnlyBackend;

    /// <summary>Every backend, for theories. Unavailable ones skip inside the test with a reason.</summary>
    public static TheoryData<Backend> AllBackends => new(NriTestDevice.Backends.Select(a => new Backend(a)));

    /// <summary>The two real backends, for tests that need a GPU or a software rasterizer.</summary>
    public static TheoryData<Backend> GpuBackends => new(NriTestDevice.GpuBackends.Select(a => new Backend(a)));

    public static string? Unavailable(GraphicsAPI api) => NriTestDevice.Unavailable(api);

    public static NriAdapter? AdapterFor(GraphicsAPI api) => NriTestDevice.AdapterFor(api);

    /// <summary>Creates a validated device with every message appended to <paramref name="messages"/>.</summary>
    public static NriDevice Create(GraphicsAPI api, List<NriMessage> messages, Action<NriDeviceOptionsBuilder>? configure = null)
    {
        var fixture = NriTestDevice.Create(api, o =>
        {
            o.MessageHandler = (in NriMessage m) => { lock (messages) messages.Add(m); };
            if (configure is not null)
            {
                var builder = new NriDeviceOptionsBuilder(o);
                configure(builder);
            }
        });
        // The test owns the device; the fixture object has nothing else to release.
        return fixture.Device;
    }

    /// <summary>
    /// The Vulkan adapter is MoltenVK on the paravirtualised Apple GPU of a macOS virtual machine
    /// (GitHub's macOS runners). Two driver defects measured there have no capability bit that
    /// predicts them (CLAUDE.md: MoltenVK and the paravirtual Mac); the tests that hit them skip by this name.
    /// </summary>
    public static bool IsAppleParavirtualVK(GraphicsAPI api) =>
        api == GraphicsAPI.VK && AdapterFor(GraphicsAPI.VK)?.Name == "Apple Paravirtual device";

    /// <summary>
    /// The Vulkan adapter is lavapipe, Mesa's CPU implementation and CI's Linux adapter. A known
    /// bug, to be removed when Mesa fixes it: lavapipe counts no clipping output primitives in
    /// pipeline statistics (measured 0 for two visible triangles, where Vulkan requires at least one
    /// per primitive with a vertex inside the clip volume). <c>QueryTests</c> asserts the wrong value
    /// there, so the fix shows up as a failure that names this exception (CLAUDE.md: Tests and skips).
    /// </summary>
    public static bool IsLavapipeVK(GraphicsAPI api) =>
        api == GraphicsAPI.VK && AdapterFor(GraphicsAPI.VK)?.Name.StartsWith("llvmpipe", StringComparison.Ordinal) == true;

    /// <summary>Marks the test skipped when the backend is unavailable; returns normally otherwise.</summary>
    public static void RequireAvailable(Backend backend)
    {
        var reason = Unavailable(backend.Api);
        Assert.SkipWhen(reason is not null, reason ?? "");
    }
}

/// <summary>Mutable options for <see cref="TestDevice.Create"/>, the same fields as the package's options.</summary>
public sealed class NriDeviceOptionsBuilder(NriTestDeviceOptions inner)
{
    public GraphicsAPI GraphicsApi { get; set; }
    public NriAdapter? Adapter { get => inner.Adapter; set => inner.Adapter = value; }
    public bool EnableNriValidation { get => inner.EnableNriValidation; set => inner.EnableNriValidation = value; }
    public bool EnableGraphicsApiValidation { get => inner.EnableGraphicsApiValidation; set => inner.EnableGraphicsApiValidation = value; }
    public INriAllocator? Allocator { get => inner.Allocator; set => inner.Allocator = value; }
    public bool ForwardAllocatorToThirdParty { get => inner.ForwardAllocatorToThirdParty; set => inner.ForwardAllocatorToThirdParty = value; }
    public IReadOnlyList<NriQueueFamily> QueueFamilies { get => inner.QueueFamilies; set => inner.QueueFamilies = value; }
    public VKBindingOffsets VkBindingOffsets { get => inner.VkBindingOffsets; set => inner.VkBindingOffsets = value; }
    public nint VkInstanceCreateInfoNext { get => inner.VkInstanceCreateInfoNext; set => inner.VkInstanceCreateInfoNext = value; }
}
