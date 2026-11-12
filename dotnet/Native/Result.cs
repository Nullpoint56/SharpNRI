namespace SharpNRI.Native;

[NativeTypeName("int8_t")]
public enum Result : sbyte
{
    /// <summary>All bad, but optionally require an action ("callbackInterface.AbortExecution" is not triggered) a potentially recoverable 3rd party SDK failure; details are reported</summary>
    EXT_ERROR = -5,
    /// <summary>VK: zero surface extent during swap chain creation; WGPU: no swap chain texture available yet. Process window events and retry</summary>
    NOT_READY = -4,
    /// <summary>may be returned by "QueueSubmit*", "*WaitIdle", "AcquireNextTexture", "QueuePresent", "WaitForPresent"</summary>
    DEVICE_LOST = -3,
    /// <summary>VK/WGPU: swap chain is out of date, can be triggered if "features.resizableSwapChain" is not supported; D3D12: shader cache is stale</summary>
    OUT_OF_DATE = -2,
    /// <summary>D3D12: some interfaces are missing (potential reasons: unable to load "D3D12Core.dll", version or SDK mismatch, developer mode is not enabled)</summary>
    INVALID_SDK = -1,
    /// <summary>All good</summary>
    SUCCESS = 0,
    /// <summary>All bad, most likely a crash or a validation error will happen next ("callbackInterface.AbortExecution" is triggered)</summary>
    FAILURE = 1,
    INVALID_ARGUMENT = 2,
    OUT_OF_MEMORY = 3,
    /// <summary>if enabled, NRI validation can promote some to "INVALID_ARGUMENT"</summary>
    UNSUPPORTED = 4,
    MAX_NUM,
}
