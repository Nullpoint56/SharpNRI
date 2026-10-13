// L2 — acquisition of the RayTracing extension; see NriDevice.Extensions.cs for the pattern.

namespace PrismNRI;

public sealed unsafe partial class NriDevice
{
    private NriRayTracingExtension? _rayTracing;
    private bool _rayTracingProbed;

    /// <summary>The RayTracing extension, or null when the device lacks <c>features.rayTracing</c>.</summary>
    public NriRayTracingExtension? TryRayTracing
    {
        get
        {
            if (!_rayTracingProbed)
            {
                NriRayTracingExtension.TryAcquire(this, out var e);
                _rayTracing = e;
                _rayTracingProbed = true;
            }
            return _rayTracing;
        }
    }

    /// <summary>The RayTracing extension. Throws <see cref="NriUnsupportedException"/> when absent.</summary>
    public NriRayTracingExtension RayTracing =>
        TryRayTracing ?? throw new NriUnsupportedException("nriGetInterface(RayTracingInterface)");
}
