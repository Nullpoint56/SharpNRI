// L3 — the WrapperVK extension (NRIWrapperVK.h). Last resort.
//
// Wraps Vulkan objects the caller created into NRI objects on a Vulkan device, and hands out the
// device's VkInstance, VkPhysicalDevice and loader entry points. The wrapped objects are ordinary
// wrappers (device children, Dispose destroys the NRI object); NRI never takes ownership of the
// Vulkan handle inside (m_OwnsNativeObjects = false), the caller destroys it. Handles are nint (dispatchable,
// VKHandle) and ulong (non-dispatchable, VKNonDispatchableHandle); VkEnum is int, VkFlags uint.
// Available on Vulkan devices only; UNSUPPORTED elsewhere. Threadsafe per NRI.
//
// CreateAccelerationStructureVK returns the RayTracing extension's NriAccelerationStructure, which
// is destroyed through that extension's table, so it needs the extension; the caller names the
// structure type, which AccelerationStructureVKDesc does not carry. Device creation from a
// VkDevice is NriDevice.CreateFromVulkan.

using SharpNRI.Interop;
using SharpNRI.Native;
using Buffer = SharpNRI.Native.Buffer;

namespace SharpNRI;

/// <summary>NRI's <c>WrapperVKInterface</c>. Acquire through <see cref="NriDevice.WrapperVK"/>.</summary>
public sealed unsafe class NriWrapperVKExtension
{
    private readonly WrapperVKInterface _table;

    internal NriWrapperVKExtension(NriDevice device, in WrapperVKInterface table)
    {
        Device = device;
        _table = table;
    }

    public NriDevice Device { get; }

    internal static bool TryAcquire(NriDevice device, out NriWrapperVKExtension? extension)
    {
        if (InterfaceTable.TryAcquire<WrapperVKInterface>(device.Raw, "WrapperVKInterface", out var table))
        {
            extension = new NriWrapperVKExtension(device, in table);
            return true;
        }
        extension = null;
        return false;
    }

    /// <summary>
    /// Wraps a <c>VkCommandPool</c> created for <c>queueType</c>'s family. The wrapper is attributed
    /// to the device's first queue of that type, which must exist.
    /// </summary>
    public NriCommandAllocator CreateCommandAllocatorVK(in CommandAllocatorVKDesc commandAllocatorVKDesc)
    {
        var queue = Device.GetQueue(commandAllocatorVKDesc.queueType);
        CommandAllocator* handle = null;
        fixed (CommandAllocatorVKDesc* p = &commandAllocatorVKDesc) NriException.Check(_table.CreateCommandAllocatorVK(Device.Raw, p, &handle), "CreateCommandAllocatorVK");
        return new NriCommandAllocator(queue, handle);
    }

    /// <summary>
    /// Wraps a <c>VkCommandBuffer</c> for a queue of <paramref name="queueType"/>. No NRI allocator
    /// backs it. NRI's validation counts it as recording from the start and refuses
    /// <see cref="NriCommandBuffer.Begin"/>: begin it with <c>vkBeginCommandBuffer</c>, then record and
    /// <see cref="NriCommandBuffer.End"/> through NRI.
    /// </summary>
    public NriCommandBuffer CreateCommandBufferVK(nint vkCommandBuffer, QueueType queueType)
    {
        var desc = new CommandBufferVKDesc { vkCommandBuffer = (void*)vkCommandBuffer, queueType = queueType };
        CommandBuffer* handle = null;
        NriException.Check(_table.CreateCommandBufferVK(Device.Raw, &desc, &handle), "CreateCommandBufferVK");
        return new NriCommandBuffer(Device, handle);
    }

    /// <summary>Wraps a <c>VkDescriptorPool</c>; <c>descriptorSetMaxNum</c> is the allocation limit NRI enforces.</summary>
    public NriDescriptorPool CreateDescriptorPoolVK(in DescriptorPoolVKDesc descriptorPoolVKDesc)
    {
        DescriptorPool* handle = null;
        fixed (DescriptorPoolVKDesc* p = &descriptorPoolVKDesc) NriException.Check(_table.CreateDescriptorPoolVK(Device.Raw, p, &handle), "CreateDescriptorPoolVK");
        return new NriDescriptorPool(Device, handle);
    }

    /// <summary>
    /// Wraps a <c>VkBuffer</c>. <paramref name="structureStride"/> must be given if it is used as a
    /// structured or raw buffer; <paramref name="mappedMemory"/> if its memory is mapped;
    /// <paramref name="vkDeviceMemory"/> only if that mapped memory is not HOST_COHERENT;
    /// <paramref name="deviceAddress"/> for ray tracing. NRI records no usage for it
    /// (<c>Desc.usage</c> is <c>NONE</c>), so its validation refuses buffer barriers on it that name
    /// shader access; use a global barrier.
    /// </summary>
    public NriBuffer CreateBufferVK(ulong vkBuffer, ulong size, uint structureStride = 0, nint mappedMemory = 0, ulong vkDeviceMemory = 0, ulong deviceAddress = 0)
    {
        var desc = new BufferVKDesc
        {
            vkBuffer = vkBuffer,
            size = size,
            structureStride = structureStride,
            mappedMemory = (byte*)mappedMemory,
            vkDeviceMemory = vkDeviceMemory,
            deviceAddress = deviceAddress,
        };
        Buffer* handle = null;
        NriException.Check(_table.CreateBufferVK(Device.Raw, &desc, &handle), "CreateBufferVK");
        return new NriBuffer(Device, handle);
    }

    /// <summary>Wraps a <c>VkImage</c> described by its Vulkan format, type, usage and dimensions.</summary>
    public NriTexture CreateTextureVK(in TextureVKDesc textureVKDesc)
    {
        Texture* handle = null;
        fixed (TextureVKDesc* p = &textureVKDesc) NriException.Check(_table.CreateTextureVK(Device.Raw, p, &handle), "CreateTextureVK");
        return new NriTexture(Device, handle);
    }

    /// <summary>
    /// Wraps a <c>VkDeviceMemory</c> range. <paramref name="mappedMemory"/>, optional, points at
    /// <paramref name="offset"/> within the mapping.
    /// </summary>
    public NriMemory CreateMemoryVK(ulong vkDeviceMemory, ulong offset, nint mappedMemory, ulong size, uint memoryTypeIndex)
    {
        var desc = new MemoryVKDesc
        {
            vkDeviceMemory = vkDeviceMemory,
            offset = offset,
            mappedMemory = (void*)mappedMemory,
            size = size,
            memoryTypeIndex = memoryTypeIndex,
        };
        Memory* handle = null;
        NriException.Check(_table.CreateMemoryVK(Device.Raw, &desc, &handle), "CreateMemoryVK");
        return new NriMemory(Device, handle);
    }

    /// <summary>Wraps a <c>VkPipeline</c> for <c>vkPipelineBindPoint</c> (graphics, compute or ray tracing).</summary>
    public NriPipeline CreatePipelineVK(in PipelineVKDesc pipelineVKDesc)
    {
        Pipeline* handle = null;
        fixed (PipelineVKDesc* p = &pipelineVKDesc) NriException.Check(_table.CreatePipelineVK(Device.Raw, p, &handle), "CreatePipelineVK");
        var bindPoint = pipelineVKDesc.vkPipelineBindPoint switch
        {
            0 => BindPoint.GRAPHICS,           // VK_PIPELINE_BIND_POINT_GRAPHICS
            1 => BindPoint.COMPUTE,            // VK_PIPELINE_BIND_POINT_COMPUTE
            1000165000 => BindPoint.RAY_TRACING, // VK_PIPELINE_BIND_POINT_RAY_TRACING_KHR
            _ => BindPoint.INHERIT,
        };
        return new NriPipeline(Device, handle, bindPoint);
    }

    /// <summary>
    /// Wraps a <c>VkQueryPool</c> of <c>vkQueryType</c>. NRI does not need the pool's size;
    /// <paramref name="capacity"/> is recorded on the wrapper only.
    /// </summary>
    public NriQueryPool CreateQueryPoolVK(in QueryPoolVKDesc queryPoolVKDesc, uint capacity)
    {
        QueryPool* handle = null;
        fixed (QueryPoolVKDesc* p = &queryPoolVKDesc) NriException.Check(_table.CreateQueryPoolVK(Device.Raw, p, &handle), "CreateQueryPoolVK");
        var type = queryPoolVKDesc.vkQueryType switch
        {
            0 => QueryType.OCCLUSION,                                  // VK_QUERY_TYPE_OCCLUSION
            1 => QueryType.PIPELINE_STATISTICS,                        // VK_QUERY_TYPE_PIPELINE_STATISTICS
            1000150000 => QueryType.ACCELERATION_STRUCTURE_COMPACTED_SIZE, // VK_QUERY_TYPE_ACCELERATION_STRUCTURE_COMPACTED_SIZE_KHR
            1000386001 => QueryType.ACCELERATION_STRUCTURE_SIZE,       // VK_QUERY_TYPE_ACCELERATION_STRUCTURE_SIZE_KHR (1000386000 is ..._SERIALIZATION_BOTTOM_LEVEL_POINTERS_KHR)
            1000396001 => QueryType.MICROMAP_COMPACTED_SIZE,           // VK_QUERY_TYPE_MICROMAP_COMPACTED_SIZE_EXT (1000396000 is ..._MICROMAP_SERIALIZATION_SIZE_EXT)
            _ => QueryType.TIMESTAMP,                                  // VK_QUERY_TYPE_TIMESTAMP (2)
        };
        return new NriQueryPool(Device, handle, type, capacity);
    }

    /// <summary>
    /// Wraps a <c>VkAccelerationStructureKHR</c> and the <c>VkBuffer</c> it lives in as an
    /// acceleration structure of <paramref name="type"/>. NRI queries the device address itself;
    /// the scratch sizes are what it reports back. Needs the device's RayTracing extension, which
    /// destroys the wrapper (never the Vulkan objects).
    /// </summary>
    /// <exception cref="NriUnsupportedException">When the device has no RayTracing extension.</exception>
    public NriAccelerationStructure CreateAccelerationStructureVK(in AccelerationStructureVKDesc accelerationStructureVKDesc, AccelerationStructureType type)
    {
        var rayTracing = Device.RayTracing;
        AccelerationStructure* handle = null;
        fixed (AccelerationStructureVKDesc* p = &accelerationStructureVKDesc) NriException.Check(_table.CreateAccelerationStructureVK(Device.Raw, p, &handle), "CreateAccelerationStructureVK");
        return new NriAccelerationStructure(rayTracing, handle, type);
    }

    /// <summary>Wraps a timeline <c>VkSemaphore</c> as a fence.</summary>
    public NriFence CreateFenceVK(ulong vkTimelineSemaphore)
    {
        var desc = new FenceVKDesc { vkTimelineSemaphore = vkTimelineSemaphore };
        Fence* handle = null;
        NriException.Check(_table.CreateFenceVK(Device.Raw, &desc, &handle), "CreateFenceVK");
        return new NriFence(Device, handle);
    }

    /// <summary>The Vulkan queue family index of <paramref name="queue"/>.</summary>
    public uint GetQueueFamilyIndexVK(NriQueue queue) => _table.GetQueueFamilyIndexVK(queue.Raw);

    /// <summary>The device's <c>VkPhysicalDevice</c>.</summary>
    public nint GetPhysicalDeviceVK() => (nint)_table.GetPhysicalDeviceVK(Device.Raw);

    /// <summary>The device's <c>VkInstance</c>.</summary>
    public nint GetInstanceVK() => (nint)_table.GetInstanceVK(Device.Raw);

    /// <summary>The <c>vkGetInstanceProcAddr</c> NRI loaded.</summary>
    public nint GetInstanceProcAddrVK() => (nint)_table.GetInstanceProcAddrVK(Device.Raw);

    /// <summary>The <c>vkGetDeviceProcAddr</c> NRI loaded.</summary>
    public nint GetDeviceProcAddrVK() => (nint)_table.GetDeviceProcAddrVK(Device.Raw);
}
