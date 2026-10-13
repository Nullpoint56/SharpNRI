// L3 — the Upscaler extension (NRIUpscaler.h).
//
// One interface over NIS (built into NRI), FSR, XeSS, DLSS super resolution and ray
// reconstruction, and MetalFX. Which ones exist on a device is IsUpscalerSupported's answer: the
// SDKs are optional at NRI's build time and vendor-bound at run time. The upscaler is an object the
// extension creates and destroys.
//
// DispatchUpscaleDesc holds two unions, "guides" and "settings", each "chosen based on UpscalerType
// passed during creation". The managed form holds every alternative side by side and the dispatch
// writes the one the upscaler's type selects:
//
//   guides.upscaler   FSR, XESS, DLSR, METALFX_TEMPORAL
//   guides.denoiser   DLRR, METALFX_DENOISED
//   settings.nis      NIS
//   settings.fsr      FSR
//   settings.dlrr     DLRR, METALFX_DENOISED
//
// The dispatch "changes descriptor pool, pipeline layout and pipeline"; barriers are the caller's.
// Threadsafe per NRI.
//
// Vendor runtimes. No PrismNRI package ships them (docs/design.md §3.6): the PrismNRI.Native.Upscalers
// build of NRI compiles FSR and XeSS in, and the application puts the vendor's DLLs beside its
// executable. NRI loads FSR by name and fails with a Result when it is absent, but XeSS is a
// delay-loaded import whose absence is a native SEH exception inside xessD3D12CreateContext
// (docs/findings.md §7.9). So CreateUpscaler checks the files first, for every type that needs one,
// and loads them by full path so NRI's bare-name LoadLibrary finds them under any host;
// IsRuntimeInstalled lets a caller ask before trying.

using PrismNRI.Interop;
using PrismNRI.Native;

namespace PrismNRI;

/// <summary><c>UpscalerDesc</c> with the optional command buffer as a wrapper.</summary>
public sealed class NriUpscalerDesc
{
    /// <summary>Output resolution.</summary>
    public required Dim2_t UpscaleResolution { get; init; }

    public required UpscalerType Type { get; init; }

    /// <summary>Not needed for NIS.</summary>
    public UpscalerMode Mode { get; init; }

    public UpscalerBits Flags { get; init; }

    /// <summary>Optional preset for DLSR or XESS: 0 default, above 1 presets A, B, C...</summary>
    public byte Preset { get; init; }

    /// <summary>Needed only for NIS, when <c>shaderFeatures.storageWriteWithoutFormat</c> is unsupported.</summary>
    public Format OutputFormat { get; init; }

    /// <summary>
    /// Optional: a non-copy command buffer in the recording state that receives the setup work;
    /// the caller submits it. Without one, NRI waits for idle during creation.
    /// </summary>
    public NriCommandBuffer? CommandBuffer { get; init; }
}

/// <summary><c>UpscalerResource</c> naming the wrappers. A default value means "not provided".</summary>
/// <param name="Texture">The texture, in the state the dispatch requires.</param>
/// <param name="Descriptor">A <c>SHADER_RESOURCE</c> view for inputs and guides, <c>SHADER_RESOURCE_STORAGE</c> for the output.</param>
public readonly unsafe record struct NriUpscalerResource(NriTexture? Texture, NriDescriptor? Descriptor)
{
    internal UpscalerResource ToNative() => new()
    {
        texture = Texture is null ? null : Texture.Raw,
        descriptor = Descriptor is null ? null : Descriptor.Raw,
    };
}

/// <summary><c>UpscalerGuides</c>: for FSR, XESS, DLSR, METALFX_TEMPORAL.</summary>
public readonly record struct NriUpscalerGuides
{
    /// <summary>.xy: surface motion.</summary>
    public NriUpscalerResource Mv { get; init; }

    /// <summary>.x: HW depth.</summary>
    public NriUpscalerResource Depth { get; init; }

    /// <summary>Optional, .x: 1x1 exposure (with <c>USE_EXPOSURE</c>).</summary>
    public NriUpscalerResource Exposure { get; init; }

    /// <summary>Optional, .x: bias towards input (with <c>USE_REACTIVE</c>).</summary>
    public NriUpscalerResource Reactive { get; init; }

    internal UpscalerGuides ToNative() => new()
    {
        mv = Mv.ToNative(),
        depth = Depth.ToNative(),
        exposure = Exposure.ToNative(),
        reactive = Reactive.ToNative(),
    };
}

/// <summary><c>DenoiserGuides</c>: for DLRR, METALFX_DENOISED.</summary>
public readonly record struct NriDenoiserGuides
{
    /// <summary>.xy: surface motion.</summary>
    public NriUpscalerResource Mv { get; init; }

    /// <summary>.x: HW or linear depth.</summary>
    public NriUpscalerResource Depth { get; init; }

    /// <summary>.xyz: world-space normal (not encoded), .w: linear roughness.</summary>
    public NriUpscalerResource NormalRoughness { get; init; }

    /// <summary>.xyz: diffuse albedo (LDR sky colour for sky).</summary>
    public NriUpscalerResource DiffuseAlbedo { get; init; }

    /// <summary>.xyz: specular albedo (environment BRDF).</summary>
    public NriUpscalerResource SpecularAlbedo { get; init; }

    /// <summary>.xy: specular virtual motion (with <c>USE_SPECULAR_MOTION</c>), otherwise .x: specular hit distance.</summary>
    public NriUpscalerResource SpecularMvOrHitT { get; init; }

    /// <summary>Optional, .x: 1x1 exposure.</summary>
    public NriUpscalerResource Exposure { get; init; }

    /// <summary>Optional, .x: bias towards input.</summary>
    public NriUpscalerResource Reactive { get; init; }

    /// <summary>Optional, .xyz: colour before transparency.</summary>
    public NriUpscalerResource PreTransparency { get; init; }

    /// <summary>Optional, .x: subsurface scattering, <c>Luminance(colorAfterSSS - colorBeforeSSS)</c>.</summary>
    public NriUpscalerResource Sss { get; init; }

    internal DenoiserGuides ToNative() => new()
    {
        mv = Mv.ToNative(),
        depth = Depth.ToNative(),
        normalRoughness = NormalRoughness.ToNative(),
        diffuseAlbedo = DiffuseAlbedo.ToNative(),
        specularAlbedo = SpecularAlbedo.ToNative(),
        specularMvOrHitT = SpecularMvOrHitT.ToNative(),
        exposure = Exposure.ToNative(),
        reactive = Reactive.ToNative(),
        preTransparency = PreTransparency.ToNative(),
        sss = Sss.ToNative(),
    };
}

/// <summary>
/// The <c>guides</c> union of <c>DispatchUpscaleDesc</c>. Set the member the upscaler's type
/// reads; the dispatch passes only that one.
/// </summary>
public readonly record struct NriDispatchUpscaleGuides
{
    /// <summary>FSR, XESS, DLSR, METALFX_TEMPORAL.</summary>
    public NriUpscalerGuides Upscaler { get; init; }

    /// <summary>DLRR, METALFX_DENOISED (sRGB not supported).</summary>
    public NriDenoiserGuides Denoiser { get; init; }
}

/// <summary>
/// The <c>settings</c> union of <c>DispatchUpscaleDesc</c>. Set the member the upscaler's type
/// reads; the dispatch passes only that one.
/// </summary>
public readonly record struct NriDispatchUpscaleSettings
{
    /// <summary>NIS.</summary>
    public NISSettings Nis { get; init; }

    /// <summary>FSR.</summary>
    public FSRSettings Fsr { get; init; }

    /// <summary>DLRR and METALFX_DENOISED.</summary>
    public DLRRSettings Dlrr { get; init; }
}

/// <summary><c>DispatchUpscaleDesc</c> naming the wrappers, with its unions resolved by upscaler type.</summary>
public readonly record struct NriDispatchUpscaleDesc
{
    /// <summary>.xyz: upscaled RGB colour. <c>SHADER_RESOURCE_STORAGE</c> state and descriptor.</summary>
    public NriUpscalerResource Output { get; init; }

    /// <summary>.xyz: input RGB colour. <c>SHADER_RESOURCE</c> state and descriptor.</summary>
    public NriUpscalerResource Input { get; init; }

    /// <summary>Guides, <c>SHADER_RESOURCE</c> states and descriptors.</summary>
    public NriDispatchUpscaleGuides Guides { get; init; }

    public NriDispatchUpscaleSettings Settings { get; init; }

    /// <summary>Render resolution of inputs and guides: <c>renderResolutionMin &lt;= currentResolution &lt;= renderResolution</c>.</summary>
    public Dim2_t CurrentResolution { get; init; }

    /// <summary>Pointing towards the pixel centre, in [-0.5; 0.5].</summary>
    public Float2_t CameraJitter { get; init; }

    /// <summary>Converts motion vectors to pixel space.</summary>
    public Float2_t MvScale { get; init; }

    /// <summary>Optional multiplier already applied to the input colour; 0 means 1. FSR, XESS, DLSR, DLRR, METALFX_TEMPORAL, METALFX_DENOISED.</summary>
    public float PreExposure { get; init; }

    public DispatchUpscaleBits Flags { get; init; }

    internal DispatchUpscaleDesc ToNative(UpscalerType type)
    {
        var native = new DispatchUpscaleDesc
        {
            output = Output.ToNative(),
            input = Input.ToNative(),
            currentResolution = CurrentResolution,
            cameraJitter = CameraJitter,
            mvScale = MvScale,
            preExposure = PreExposure,
            flags = Flags,
        };

        switch (type)
        {
            case UpscalerType.FSR or UpscalerType.XESS or UpscalerType.DLSR or UpscalerType.METALFX_TEMPORAL:
                native.guides.upscaler = Guides.Upscaler.ToNative();
                break;
            case UpscalerType.DLRR or UpscalerType.METALFX_DENOISED:
                native.guides.denoiser = Guides.Denoiser.ToNative();
                break;
        }

        switch (type)
        {
            case UpscalerType.NIS:
                native.settings.nis = Settings.Nis;
                break;
            case UpscalerType.FSR:
                native.settings.fsr = Settings.Fsr;
                break;
            case UpscalerType.DLRR or UpscalerType.METALFX_DENOISED:
                native.settings.dlrr = Settings.Dlrr;
                break;
        }

        return native;
    }
}

/// <summary>An upscaler. Created by <see cref="NriUpscalerExtension.CreateUpscaler"/>.</summary>
public sealed unsafe class NriUpscaler : NriObject
{
    private readonly NriUpscalerExtension _extension;

    internal NriUpscaler(NriUpscalerExtension extension, Upscaler* handle, UpscalerType type) : base(handle, "NriUpscaler")
    {
        _extension = extension;
        Type = type;
        extension.Device.ChildCreated();
    }

    public override NriDevice Device => _extension.Device;

    /// <summary>The type it was created with; selects the guides and settings a dispatch passes.</summary>
    public UpscalerType Type { get; }

    internal Upscaler* Raw => (Upscaler*)Handle;

    private protected override void Destroy(void* handle) => _extension.DestroyUpscaler((Upscaler*)handle);
}

/// <summary>NRI's <c>UpscalerInterface</c>. Acquire through <see cref="NriDevice.Upscaler"/>.</summary>
public sealed unsafe class NriUpscalerExtension
{
    private readonly UpscalerInterface _table;

    internal NriUpscalerExtension(NriDevice device, in UpscalerInterface table)
    {
        Device = device;
        _table = table;
    }

    public NriDevice Device { get; }

    internal static bool TryAcquire(NriDevice device, out NriUpscalerExtension? extension)
    {
        if (InterfaceTable.TryAcquire<UpscalerInterface>(device.Raw, "UpscalerInterface", out var table))
        {
            extension = new NriUpscalerExtension(device, in table);
            return true;
        }
        extension = null;
        return false;
    }

    /// <summary>
    /// An upscaler. MetalFX is created on the first dispatch (a hitch), after which resource sizes
    /// and formats must not change; its output needs <c>COLOR_ATTACHMENT</c> usage.
    /// </summary>
    public NriUpscaler CreateUpscaler(NriUpscalerDesc upscalerDesc)
    {
        ArgumentNullException.ThrowIfNull(upscalerDesc);
        var desc = new UpscalerDesc
        {
            upscaleResolution = upscalerDesc.UpscaleResolution,
            type = upscalerDesc.Type,
            mode = upscalerDesc.Mode,
            flags = upscalerDesc.Flags,
            preset = upscalerDesc.Preset,
            outputFormat = upscalerDesc.OutputFormat,
            commandBuffer = upscalerDesc.CommandBuffer is null ? null : upscalerDesc.CommandBuffer.Raw,
        };

        // Only for a type this NRI has: otherwise NRI answers UNSUPPORTED itself.
        if (IsUpscalerSupported(upscalerDesc.Type))
        {
            if (FindMissingRuntimeFile(upscalerDesc.Type, Device.GraphicsApi) is { } missing)
                throw new NriUnsupportedException("CreateUpscaler",
                    $"{upscalerDesc.Type} needs the vendor runtime '{missing}' beside the application ({AppContext.BaseDirectory}). PrismNRI does not ship it; the application does.");
            PreloadRuntime(upscalerDesc.Type, Device.GraphicsApi);
        }

        Upscaler* handle = null;
        Device.Check(_table.CreateUpscaler(Device.Raw, &desc, &handle), "CreateUpscaler");
        return new NriUpscaler(this, handle, upscalerDesc.Type);
    }

    internal void DestroyUpscaler(Upscaler* upscaler) => _table.DestroyUpscaler(upscaler);

    /// <summary>
    /// The vendor runtime files <paramref name="type"/> loads on <paramref name="graphicsApi"/>, which
    /// the application ships beside its executable. Empty for NIS and MetalFX, which are built into
    /// NRI. On Linux the DLSS names are patterns (<c>*</c> stands for the NGX version).
    /// </summary>
    public static IReadOnlyList<string> GetRuntimeFiles(UpscalerType type, GraphicsAPI graphicsApi)
    {
        var linux = OperatingSystem.IsLinux();
        return type switch
        {
            UpscalerType.DLSR => [linux ? "libnvidia-ngx-dlss.so.*" : "nvngx_dlss.dll"],
            UpscalerType.DLRR => [linux ? "libnvidia-ngx-dlssd.so.*" : "nvngx_dlssd.dll"],
            UpscalerType.XESS => ["libxess.dll"],
            // NRI loads the loader by name on D3D12; the loader loads the upscaler.
            UpscalerType.FSR when graphicsApi == GraphicsAPI.D3D12 => ["amd_fidelityfx_loader_dx12.dll", "amd_fidelityfx_upscaler_dx12.dll"],
            UpscalerType.FSR => ["amd_fidelityfx_vk.dll"],
            _ => [],
        };
    }

    /// <summary>
    /// Whether every vendor runtime file <paramref name="type"/> needs on <paramref name="graphicsApi"/>
    /// is present beside the application (<see cref="AppContext.BaseDirectory"/>). True for types that
    /// need none. Says nothing about whether NRI was built with the SDK or the adapter supports it:
    /// that is <see cref="IsUpscalerSupported"/>.
    /// </summary>
    public static bool IsRuntimeInstalled(UpscalerType type, GraphicsAPI graphicsApi) => FindMissingRuntimeFile(type, graphicsApi) is null;

    /// <summary><see cref="IsRuntimeInstalled(UpscalerType, GraphicsAPI)"/> for this device's backend.</summary>
    public bool IsRuntimeInstalled(UpscalerType type) => IsRuntimeInstalled(type, Device.GraphicsApi);

    // NRI (and the XeSS delay-load stub) load these with LoadLibrary by bare name, which searches the
    // directory of the process executable: right for an apphost, wrong for "dotnet app.dll", where
    // FSR then fails and XeSS raises an unhandled 0xC06D007E (docs/findings.md §7.9). Loading each
    // file by full path from beside the application first makes the bare-name load resolve to the
    // module already in the process, under any host. Never unloaded.
    private static readonly HashSet<string> s_preloaded = new(StringComparer.OrdinalIgnoreCase);

    private static void PreloadRuntime(UpscalerType type, GraphicsAPI graphicsApi)
    {
        if (!OperatingSystem.IsWindows()) return;
        lock (s_preloaded)
        {
            foreach (var file in GetRuntimeFiles(type, graphicsApi))
            {
                if (s_preloaded.Contains(file)) continue;
                var path = Path.Combine(AppContext.BaseDirectory, file);
                if (!System.Runtime.InteropServices.NativeLibrary.TryLoad(path, out _))
                    throw new NriUnsupportedException("CreateUpscaler", $"{type} needs '{file}', which is beside the application but failed to load ({path}).");
                s_preloaded.Add(file);
            }
        }
    }

    private static string? FindMissingRuntimeFile(UpscalerType type, GraphicsAPI graphicsApi)
    {
        var directory = AppContext.BaseDirectory;
        foreach (var file in GetRuntimeFiles(type, graphicsApi))
        {
            var present = file.Contains('*')
                ? Directory.Exists(directory) && Directory.EnumerateFiles(directory, file).Any()
                : File.Exists(Path.Combine(directory, file));
            if (!present) return file;
        }
        return null;
    }

    /// <summary>Whether <paramref name="type"/> can be created on this device: built into NRI and supported by the adapter.</summary>
    public bool IsUpscalerSupported(UpscalerType type) => _table.IsUpscalerSupported(Device.Raw, type) != 0;

    /// <summary>Scaling factor, mip bias, optimal and minimal render resolution, and jitter phase count.</summary>
    public UpscalerProps GetUpscalerProps(NriUpscaler upscaler)
    {
        UpscalerProps props;
        _table.GetUpscalerProps(upscaler.Raw, &props);
        return props;
    }

    /// <summary>
    /// <c>CmdDispatchUpscale</c>. Changes the descriptor pool, pipeline layout and pipeline bound
    /// on <paramref name="commandBuffer"/>; barriers are the caller's. No heap allocation.
    /// </summary>
    public void DispatchUpscale(NriCommandBuffer commandBuffer, NriUpscaler upscaler, in NriDispatchUpscaleDesc dispatchUpscaleDesc)
    {
        var native = dispatchUpscaleDesc.ToNative(upscaler.Type);
        _table.CmdDispatchUpscale(commandBuffer.Raw, upscaler.Raw, &native);
    }
}
