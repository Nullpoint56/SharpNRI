namespace SharpNRI.Native;

/// <summary>https://learn.microsoft.com/en-us/windows/win32/api/d3d12/ne-d3d12-d3d12_heap_type</summary>
[NativeTypeName("uint8_t")]
public enum MemoryLocation : byte
{
    DEVICE,
    /// <summary>soft fallback to "HOST_UPLOAD" if "deviceUploadHeapSize = 0"</summary>
    DEVICE_UPLOAD,
    HOST_UPLOAD,
    HOST_READBACK,
    MAX_NUM,
}
