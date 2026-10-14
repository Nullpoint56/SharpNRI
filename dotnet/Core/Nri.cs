// L2 — NRI's flat exports that need no device: format conversion and properties, the API name,
// pipeline layout fitting, live-object reporting, host annotations and thread names.
//
// One method per nri* export, same name without the prefix. All are thread safe per NRI.h; the
// annotations are thread-global (NVTX ranges on the calling thread), unlike the queue and command
// buffer annotations, which belong to their objects.

using PrismNRI.Interop;
using PrismNRI.Native;

namespace PrismNRI;

/// <summary>NRI's device-independent exports.</summary>
public static unsafe class Nri
{
    // ---------------------------------------------------------------- formats

    /// <summary>
    /// A <c>DXGI_FORMAT</c> as NRI's format; the best-matched typed format for a <c>TYPELESS</c> one,
    /// <c>UNKNOWN</c> when NRI has none. NRI v190 answers <c>RGB32_SINT</c> for
    /// <c>DXGI_FORMAT_R32G32_SINT</c> (18), a slip in its table (measured).
    /// </summary>
    public static Format ConvertDXGIFormatToNRI(uint dxgiFormat) => NriApi.nriConvertDXGIFormatToNRI(dxgiFormat);

    /// <summary>
    /// A <c>VkFormat</c> as NRI's format, <c>UNKNOWN</c> when NRI has none. The inverse of
    /// <see cref="ConvertNRIFormatToVK"/> for every NRI format, block-compressed ones included
    /// (patch 0008; NRI v190's table ended at <c>VK_FORMAT_D32_SFLOAT_S8_UINT</c>, 130). BC1's RGB
    /// variants (131, 132) stay <c>UNKNOWN</c>: NRI has only BC1 RGBA.
    /// </summary>
    public static Format ConvertVKFormatToNRI(uint vkFormat) => NriApi.nriConvertVKFormatToNRI(vkFormat);

    /// <summary>
    /// NRI's format as a <c>DXGI_FORMAT</c>; 0 (<c>DXGI_FORMAT_UNKNOWN</c>) when DXGI has none. A payload
    /// built without a D3D backend (Linux, macOS) answers 0 for every format and <c>UNKNOWN</c> the other
    /// way: NRI compiles its DXGI table only with D3D support.
    /// </summary>
    public static uint ConvertNRIFormatToDXGI(Format format) => NriApi.nriConvertNRIFormatToDXGI(Valid(format));

    /// <summary>NRI's format as a <c>VkFormat</c>; 0 (<c>VK_FORMAT_UNDEFINED</c>) when Vulkan has none.</summary>
    public static uint ConvertNRIFormatToVK(Format format) => NriApi.nriConvertNRIFormatToVK(Valid(format));

    /// <summary>Bits, block size and kind of <paramref name="format"/>, from NRI's static table.</summary>
    public static ref readonly FormatProps GetFormatProps(Format format) => ref *NriApi.nriGetFormatProps(Valid(format));

    /// <summary>NRI's name for <paramref name="format"/> (<c>FormatProps.name</c>).</summary>
    public static string GetFormatName(Format format) => Utf8.Read(NriApi.nriGetFormatProps(Valid(format))->name) ?? "";

    // NRI indexes its tables with the format unchecked; MAX_NUM and beyond read past them.
    private static Format Valid(Format format)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)format, (uint)Format.MAX_NUM, nameof(format));
        return format;
    }

    /// <summary>NRI's name for the API: "D3D12", "VK", "NONE", ...</summary>
    public static string GetGraphicsAPIString(GraphicsAPI graphicsAPI) => Utf8.Read(NriApi.nriGetGraphicsAPIString(graphicsAPI)) ?? "";

    // ---------------------------------------------------------------- pipeline layouts

    /// <summary>
    /// Clamps the counts and sizes in <paramref name="settings"/> to what <paramref name="deviceDesc"/>
    /// allows, in NRI's priority order (on D3D12, the 256-byte root signature budget).
    /// </summary>
    public static PipelineLayoutSettingsDesc FitPipelineLayoutSettingsIntoDeviceLimits(in DeviceDesc deviceDesc, in PipelineLayoutSettingsDesc settings)
    {
        fixed (DeviceDesc* d = &deviceDesc)
        fixed (PipelineLayoutSettingsDesc* s = &settings)
        {
            return NriApi.nriFitPipelineLayoutSettingsIntoDeviceLimits(d, s);
        }
    }

    // ---------------------------------------------------------------- diagnostics

    /// <summary>
    /// Asks the D3D debug layer (DXGI) to report live objects; a no-op where it is not loaded.
    /// Output goes to the debugger, not the message callback.
    /// </summary>
    public static void ReportLiveObjects() => NriApi.nriReportLiveObjects();

    /// <summary>Opens a named range on the calling thread (NVTX). Close it with <see cref="EndAnnotation"/>.</summary>
    public static void BeginAnnotation(string name, uint bgra = NriApi.BGRA_UNUSED)
    {
        using var utf8 = new Utf8.Scoped(name);
        NriApi.nriBeginAnnotation(utf8, bgra);
    }

    /// <summary>Closes the range the calling thread opened last.</summary>
    public static void EndAnnotation() => NriApi.nriEndAnnotation();

    /// <summary>A named instant event on the calling thread.</summary>
    public static void Annotation(string name, uint bgra = NriApi.BGRA_UNUSED)
    {
        using var utf8 = new Utf8.Scoped(name);
        NriApi.nriAnnotation(utf8, bgra);
    }

    /// <summary>
    /// <see cref="BeginAnnotation"/> now, <see cref="EndAnnotation"/> on dispose:
    /// <c>using (Nri.Annotate("upload")) { ... }</c>. Same thread for both.
    /// </summary>
    public static AnnotationScope Annotate(string name, uint bgra = NriApi.BGRA_UNUSED)
    {
        BeginAnnotation(name, bgra);
        return default;
    }

    /// <summary>Names the calling thread for profilers.</summary>
    public static void SetThreadName(string name)
    {
        using var utf8 = new Utf8.Scoped(name);
        NriApi.nriSetThreadName(utf8);
    }

    /// <summary>Ends the range <see cref="Annotate"/> opened.</summary>
    public readonly struct AnnotationScope : IDisposable
    {
        public void Dispose() => NriApi.nriEndAnnotation();
    }
}
