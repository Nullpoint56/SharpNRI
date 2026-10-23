using System.Runtime.InteropServices;
using static PrismNRI.Native.Format;
using static PrismNRI.Native.FormatSupportBits;
using static PrismNRI.Native.GraphicsAPI;

namespace PrismNRI.Native;

public static unsafe partial class NriApi
{
    [NativeTypeName("const uint32_t")]
    public const uint BGRA_UNUSED = 0;

    [NativeTypeName("const uint32_t")]
    public const uint ALL = 0;

    [NativeTypeName("const Dim_t")]
    public const ushort WHOLE_SIZE = 0;

    [NativeTypeName("const Dim_t")]
    public const ushort REMAINING = 0;

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("nri::Result")]
    public static extern Result nriGetInterface([NativeTypeName("const Device &")] Device* device, [NativeTypeName("const char *")] sbyte* interfaceName, [NativeTypeName("size_t")] nuint interfaceSize, void* interfacePtr);

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void nriBeginAnnotation([NativeTypeName("const char *")] sbyte* name, [NativeTypeName("uint32_t")] uint bgra);

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void nriEndAnnotation();

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void nriAnnotation([NativeTypeName("const char *")] sbyte* name, [NativeTypeName("uint32_t")] uint bgra);

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void nriSetThreadName([NativeTypeName("const char *")] sbyte* name);

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("nri::Result")]
    public static extern Result nriEnumerateAdapters([NativeTypeName("nri::AdapterDesc *")] AdapterDesc* adapterDescs, [NativeTypeName("uint32_t &")] uint* adapterDescNum, [NativeTypeName("nri::GraphicsAPI")] GraphicsAPI desiredGraphicsAPIs = ALL);

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("nri::Result")]
    public static extern Result nriCreateDevice([NativeTypeName("const DeviceCreationDesc &")] DeviceCreationDesc* deviceCreationDesc, [NativeTypeName("Device *&")] Device** device);

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void nriDestroyDevice([NativeTypeName("nri::Device *")] Device* device);

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    public static extern void nriReportLiveObjects();

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("nri::Result")]
    public static extern Result nriReportDeviceLostInfo([NativeTypeName("nri::Device &")] Device* device, [NativeTypeName("nri::DeviceLostDump &")] DeviceLostDump* deviceLostDump);

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("nri::Format")]
    public static extern Format nriConvertDXGIFormatToNRI([NativeTypeName("uint32_t")] uint dxgiFormat);

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("nri::Format")]
    public static extern Format nriConvertVKFormatToNRI([NativeTypeName("uint32_t")] uint vkFormat);

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint nriConvertNRIFormatToDXGI([NativeTypeName("nri::Format")] Format format);

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("uint32_t")]
    public static extern uint nriConvertNRIFormatToVK([NativeTypeName("nri::Format")] Format format);

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("const FormatProps *")]
    public static extern FormatProps* nriGetFormatProps([NativeTypeName("nri::Format")] Format format);

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("const char *")]
    public static extern sbyte* nriGetGraphicsAPIString([NativeTypeName("nri::GraphicsAPI")] GraphicsAPI graphicsAPI);

    [return: NativeTypeName("nri::Format")]
    public static Format GetSupportedDepthFormat([NativeTypeName("const CoreInterface &")] CoreInterface* coreInterface, [NativeTypeName("const Device &")] Device* device, [NativeTypeName("uint32_t")] uint minBits, bool stencil)
    {
        if (minBits <= 16 && !stencil)
        {
            if (((coreInterface)->GetFormatSupport(device, D16_UNORM) & DEPTH_STENCIL_ATTACHMENT) != 0)
            {
                return D16_UNORM;
            }
        }

        if (minBits <= 24)
        {
            if (((coreInterface)->GetFormatSupport(device, D24_UNORM_S8_UINT) & DEPTH_STENCIL_ATTACHMENT) != 0)
            {
                return D24_UNORM_S8_UINT;
            }
        }

        if (minBits <= 32 && !stencil)
        {
            if (((coreInterface)->GetFormatSupport(device, D32_SFLOAT) & DEPTH_STENCIL_ATTACHMENT) != 0)
            {
                return D32_SFLOAT;
            }
        }

        if (((coreInterface)->GetFormatSupport(device, D32_SFLOAT_S8_UINT) & DEPTH_STENCIL_ATTACHMENT) != 0)
        {
            return D32_SFLOAT_S8_UINT;
        }

        return UNKNOWN;
    }

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("nri::PipelineLayoutSettingsDesc")]
    public static extern PipelineLayoutSettingsDesc nriFitPipelineLayoutSettingsIntoDeviceLimits([NativeTypeName("const DeviceDesc &")] DeviceDesc* deviceDesc, [NativeTypeName("const PipelineLayoutSettingsDesc &")] PipelineLayoutSettingsDesc* pipelineLayoutSettingsDesc);

    public static void* _NriCastFloatToVoidPtr(float f)
    {
        return *(void**)(&f);
    }

    [NativeTypeName("const Buffer *")]
    public static Buffer* HAS_BUFFER = (Buffer*)(1);

    [NativeTypeName("const uint64_t")]
    public const ulong SWAPCHAIN_SEMAPHORE = unchecked((ulong)(-1));

    [NativeTypeName("const Queue *")]
    public static Queue* EXTERNAL_QUEUE = (Queue*)(1);

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("nri::Result")]
    public static extern Result nriCreateDeviceFromVKDevice([NativeTypeName("const DeviceCreationVKDesc &")] DeviceCreationVKDesc* deviceDesc, [NativeTypeName("Device *&")] Device** device);

    [DllImport("NRI", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    [return: NativeTypeName("nri::Result")]
    public static extern Result nriCreateDeviceFromD3D12Device([NativeTypeName("const DeviceCreationD3D12Desc &")] DeviceCreationD3D12Desc* deviceDesc, [NativeTypeName("Device *&")] Device** device);
}
