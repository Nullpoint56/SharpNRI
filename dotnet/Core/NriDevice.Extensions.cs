// L2 — the device's extension acquisitions.
//
// Each NRI extension is acquired at most once per device, on first use, by name through
// nriGetInterface. UNSUPPORTED means the backend does not implement it on this device, which is
// a capability answer: the Try* form reports it as false, the property form throws
// NriUnsupportedException.

namespace PrismNRI;

public sealed unsafe partial class NriDevice
{
    private NriSharedHandleExtension? _sharedHandles;
    private bool _sharedHandlesProbed;

    /// <summary>The SharedHandle extension (patch 0002), or null when the backend lacks it.</summary>
    public NriSharedHandleExtension? TrySharedHandles
    {
        get
        {
            if (!_sharedHandlesProbed)
            {
                NriSharedHandleExtension.TryAcquire(this, out var e);
                _sharedHandles = e;
                _sharedHandlesProbed = true;
            }
            return _sharedHandles;
        }
    }

    /// <summary>The SharedHandle extension. Throws <see cref="NriUnsupportedException"/> when absent.</summary>
    public NriSharedHandleExtension SharedHandles =>
        TrySharedHandles ?? throw new NriUnsupportedException("nriGetInterface(SharedHandleInterface)");

    private NriMeshShaderExtension? _meshShader;
    private bool _meshShaderProbed;

    /// <summary>The MeshShader extension, or null when the backend lacks it. Needs <c>features.meshShader</c>.</summary>
    public NriMeshShaderExtension? TryMeshShader
    {
        get
        {
            if (!_meshShaderProbed)
            {
                NriMeshShaderExtension.TryAcquire(this, out var e);
                _meshShader = e;
                _meshShaderProbed = true;
            }
            return _meshShader;
        }
    }

    /// <summary>The MeshShader extension. Throws <see cref="NriUnsupportedException"/> when absent.</summary>
    public NriMeshShaderExtension MeshShader =>
        TryMeshShader ?? throw new NriUnsupportedException("nriGetInterface(MeshShaderInterface)");

    private NriDescriptorHeapExtension? _descriptorHeap;
    private bool _descriptorHeapProbed;

    /// <summary>The DescriptorHeap extension, or null when the backend lacks it. Needs <c>features.descriptorHeap</c>.</summary>
    public NriDescriptorHeapExtension? TryDescriptorHeap
    {
        get
        {
            if (!_descriptorHeapProbed)
            {
                NriDescriptorHeapExtension.TryAcquire(this, out var e);
                _descriptorHeap = e;
                _descriptorHeapProbed = true;
            }
            return _descriptorHeap;
        }
    }

    /// <summary>The DescriptorHeap extension. Throws <see cref="NriUnsupportedException"/> when absent.</summary>
    public NriDescriptorHeapExtension DescriptorHeap =>
        TryDescriptorHeap ?? throw new NriUnsupportedException("nriGetInterface(DescriptorHeapInterface)");

    private NriStreamerExtension? _streamer;
    private bool _streamerProbed;

    /// <summary>The Streamer extension, or null when the backend lacks it.</summary>
    public NriStreamerExtension? TryStreamer
    {
        get
        {
            if (!_streamerProbed)
            {
                NriStreamerExtension.TryAcquire(this, out var e);
                _streamer = e;
                _streamerProbed = true;
            }
            return _streamer;
        }
    }

    /// <summary>The Streamer extension. Throws <see cref="NriUnsupportedException"/> when absent.</summary>
    public NriStreamerExtension Streamer =>
        TryStreamer ?? throw new NriUnsupportedException("nriGetInterface(StreamerInterface)");

    private NriSwapChainExtension? _swapChain;
    private bool _swapChainProbed;

    /// <summary>The SwapChain extension, or null when the backend lacks it. Needs <c>features.swapChain</c>.</summary>
    public NriSwapChainExtension? TrySwapChain
    {
        get
        {
            if (!_swapChainProbed)
            {
                NriSwapChainExtension.TryAcquire(this, out var e);
                _swapChain = e;
                _swapChainProbed = true;
            }
            return _swapChain;
        }
    }

    /// <summary>The SwapChain extension. Throws <see cref="NriUnsupportedException"/> when absent.</summary>
    public NriSwapChainExtension SwapChain =>
        TrySwapChain ?? throw new NriUnsupportedException("nriGetInterface(SwapChainInterface)");

    private NriLowLatencyExtension? _lowLatency;
    private bool _lowLatencyProbed;

    /// <summary>The LowLatency extension, or null when the backend lacks it. Needs <c>features.lowLatency</c>.</summary>
    public NriLowLatencyExtension? TryLowLatency
    {
        get
        {
            if (!_lowLatencyProbed)
            {
                NriLowLatencyExtension.TryAcquire(this, out var e);
                _lowLatency = e;
                _lowLatencyProbed = true;
            }
            return _lowLatency;
        }
    }

    /// <summary>The LowLatency extension. Throws <see cref="NriUnsupportedException"/> when absent.</summary>
    public NriLowLatencyExtension LowLatency =>
        TryLowLatency ?? throw new NriUnsupportedException("nriGetInterface(LowLatencyInterface)");

    private NriUpscalerExtension? _upscaler;
    private bool _upscalerProbed;

    /// <summary>The Upscaler extension, or null when the backend lacks it.</summary>
    public NriUpscalerExtension? TryUpscaler
    {
        get
        {
            if (!_upscalerProbed)
            {
                NriUpscalerExtension.TryAcquire(this, out var e);
                _upscaler = e;
                _upscalerProbed = true;
            }
            return _upscaler;
        }
    }

    /// <summary>The Upscaler extension. Throws <see cref="NriUnsupportedException"/> when absent.</summary>
    public NriUpscalerExtension Upscaler =>
        TryUpscaler ?? throw new NriUnsupportedException("nriGetInterface(UpscalerInterface)");

    private NriImguiExtension? _imgui;
    private bool _imguiProbed;

    /// <summary>The Imgui extension, or null when the backend lacks it.</summary>
    public NriImguiExtension? TryImgui
    {
        get
        {
            if (!_imguiProbed)
            {
                NriImguiExtension.TryAcquire(this, out var e);
                _imgui = e;
                _imguiProbed = true;
            }
            return _imgui;
        }
    }

    /// <summary>The Imgui extension. Throws <see cref="NriUnsupportedException"/> when absent.</summary>
    public NriImguiExtension Imgui =>
        TryImgui ?? throw new NriUnsupportedException("nriGetInterface(ImguiInterface)");

    private NriVideoExtension? _video;
    private bool _videoProbed;

    /// <summary>The Video extension, or null when the backend lacks it. Needs <c>features.video</c>.</summary>
    public NriVideoExtension? TryVideo
    {
        get
        {
            if (!_videoProbed)
            {
                NriVideoExtension.TryAcquire(this, out var e);
                _video = e;
                _videoProbed = true;
            }
            return _video;
        }
    }

    /// <summary>The Video extension. Throws <see cref="NriUnsupportedException"/> when absent.</summary>
    public NriVideoExtension Video =>
        TryVideo ?? throw new NriUnsupportedException("nriGetInterface(VideoInterface)");

    private NriWrapperVKExtension? _wrapperVK;
    private bool _wrapperVKProbed;

    /// <summary>The WrapperVK extension, or null when the device is not a Vulkan device. Last resort.</summary>
    public NriWrapperVKExtension? TryWrapperVK
    {
        get
        {
            if (!_wrapperVKProbed)
            {
                NriWrapperVKExtension.TryAcquire(this, out var e);
                _wrapperVK = e;
                _wrapperVKProbed = true;
            }
            return _wrapperVK;
        }
    }

    /// <summary>The WrapperVK extension. Throws <see cref="NriUnsupportedException"/> when absent.</summary>
    public NriWrapperVKExtension WrapperVK =>
        TryWrapperVK ?? throw new NriUnsupportedException("nriGetInterface(WrapperVKInterface)");

    private NriWrapperD3D12Extension? _wrapperD3D12;
    private bool _wrapperD3D12Probed;

    /// <summary>The WrapperD3D12 extension, or null when the device is not a D3D12 device. Last resort.</summary>
    public NriWrapperD3D12Extension? TryWrapperD3D12
    {
        get
        {
            if (!_wrapperD3D12Probed)
            {
                NriWrapperD3D12Extension.TryAcquire(this, out var e);
                _wrapperD3D12 = e;
                _wrapperD3D12Probed = true;
            }
            return _wrapperD3D12;
        }
    }

    /// <summary>The WrapperD3D12 extension. Throws <see cref="NriUnsupportedException"/> when absent.</summary>
    public NriWrapperD3D12Extension WrapperD3D12 =>
        TryWrapperD3D12 ?? throw new NriUnsupportedException("nriGetInterface(WrapperD3D12Interface)");
}
