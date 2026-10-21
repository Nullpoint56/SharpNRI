// The WrapperVK and WrapperD3D12 extensions, and NriDevice.CreateFromVulkan / CreateFromD3D12.
//
// Acquisition: each wrapper exists only on its own backend (UNSUPPORTED, i.e. TryAcquire false,
// everywhere else, NONE included). Wrapped devices: a raw ID3D12Device made with Vortice.Direct3D12
// and a raw VkDevice made with Vortice.Vulkan (every supported Vulkan 1.0-1.3 core feature enabled,
// no extensions, the validation layer on when installed) are wrapped into NRI devices that must
// report the pinned NRI version, create and destroy a buffer, run a submit, and wrap a native fence
// created next to them. Vulkan validation messages for the caller-owned device are collected
// through a debug messenger and must contain no error.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PrismNRI.Native;
using Vortice.Vulkan;
using Xunit;
using static Vortice.Vulkan.Vulkan;
using V12 = Vortice.Direct3D12;
using VD3D = Vortice.Direct3D;
using VDXGI = Vortice.DXGI;

namespace PrismNRI.Tests;

public sealed unsafe partial class WrapperExtensionTests(ITestOutputHelper output)
{
    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void Each_wrapper_exists_only_on_its_own_backend(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        Assert.Equal(backend.Api == GraphicsAPI.VK, device.TryWrapperVK is not null);
        Assert.Equal(backend.Api == GraphicsAPI.D3D12, device.TryWrapperD3D12 is not null);
        if (backend.Api != GraphicsAPI.VK) Assert.Throws<NriUnsupportedException>(() => device.WrapperVK);
        if (backend.Api != GraphicsAPI.D3D12) Assert.Throws<NriUnsupportedException>(() => device.WrapperD3D12);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Fact]
    public void WrapperVK_hands_out_the_native_instance_device_and_loader()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.VK, messages);
        var vk = device.WrapperVK;

        Assert.NotEqual(0, vk.GetInstanceVK());
        Assert.NotEqual(0, vk.GetPhysicalDeviceVK());
        Assert.NotEqual(0, vk.GetInstanceProcAddrVK());
        Assert.NotEqual(0, vk.GetDeviceProcAddrVK());
        var family = vk.GetQueueFamilyIndexVK(device.GetQueue(QueueType.GRAPHICS));
        output.WriteLine($"graphics queue family {family}");

        // A timeline semaphore made on NRI's own VkDevice, wrapped back as a fence. NRI does not own it.
        // Resolved against this device now, not taken from Vortice's GetApi cache (VulkanApi.cs: a dead
        // earlier device with the same handle value was the Linux SIGSEGV).
        var api = VulkanApi.For(new VkInstance(vk.GetInstanceVK()), new VkDevice(device.GetNativeObject()));
        var typeInfo = new VkSemaphoreTypeCreateInfo { semaphoreType = VkSemaphoreType.Timeline, initialValue = 7 };
        var info = new VkSemaphoreCreateInfo { pNext = &typeInfo };
        VkSemaphore semaphore;
        Assert.Equal(VkResult.Success, api.vkCreateSemaphore(&info, null, &semaphore));
        using (var fence = vk.CreateFenceVK(semaphore.Handle))
        {
            Assert.Equal(7UL, fence.Value);
        }
        api.vkDestroySemaphore(semaphore, null);

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Fact]
    public void A_raw_D3D12_device_wraps_into_a_working_NRI_device()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.D3D12));
        using var adapter = DxgiAdapterFor(GraphicsAPI.D3D12);
        var hr = V12.D3D12.D3D12CreateDevice(adapter, VD3D.FeatureLevel.Level_12_0, out V12.ID3D12Device? d3d);
        Assert.True(hr.Success, $"D3D12CreateDevice: {hr}");
        using var _ = d3d;
        using var nativeFence = d3d!.CreateFence(7);
        using var nativeBuffer = d3d.CreateCommittedResource(
            new V12.HeapProperties(V12.HeapType.Default), V12.HeapFlags.None, V12.ResourceDescription.Buffer(4096), V12.ResourceStates.Common);

        var messages = new List<NriMessage>();
        using (var device = NriDevice.CreateFromD3D12(new NriD3D12DeviceOptions
        {
            D3D12Device = d3d.NativePointer,
            QueueFamilies = [new NriQueueFamilyD3D12Desc(null, 1, QueueType.GRAPHICS)], // NRI creates the queue
            EnableNriValidation = true,
            MessageHandler = (in NriMessage m) => { lock (messages) messages.Add(m); },
        }))
        {
            Assert.Equal(GraphicsAPI.D3D12, device.GraphicsApi);
            Assert.Equal(NriHeader.PinnedVersion, device.NriVersion);
            Assert.Equal(d3d.NativePointer, device.GetNativeObject());

            ExerciseWrappedDevice(device);

            var wrapper = device.WrapperD3D12;
            using (var fence = wrapper.CreateFenceD3D12(nativeFence.NativePointer)) Assert.Equal(7UL, fence.Value);
            using (var buffer = wrapper.CreateBufferD3D12(nativeBuffer.NativePointer, new BufferDesc { size = 4096 })) Assert.Equal(4096UL, buffer.Desc.size);

            Assert.Equal(0, device.LiveObjects);
        }

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Fact]
    public void A_raw_Vulkan_device_wraps_into_a_working_NRI_device()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        using var raw = RawVulkanDevice.Create(TestDevice.AdapterFor(GraphicsAPI.VK)!);
        output.WriteLine($"raw VkDevice on '{raw.DeviceName}', Vulkan 1.{raw.MinorVersion}, validation {(raw.Validated ? "on" : "unavailable")}");

        var messages = new List<NriMessage>();
        using (var device = NriDevice.CreateFromVulkan(new NriVulkanDeviceOptions
        {
            VkInstance = raw.Instance.Handle,
            VkDevice = raw.Device.Handle,
            VkPhysicalDevice = raw.PhysicalDevice.Handle,
            QueueFamilies = [new QueueFamilyVKDesc { queueNum = 1, queueType = QueueType.GRAPHICS, familyIndex = raw.QueueFamily }],
            MinorVersion = raw.MinorVersion,
            EnableNriValidation = true,
            MessageHandler = (in NriMessage m) => { lock (messages) messages.Add(m); },
        }))
        {
            Assert.Equal(GraphicsAPI.VK, device.GraphicsApi);
            Assert.Equal(NriHeader.PinnedVersion, device.NriVersion);
            Assert.Equal(raw.Device.Handle, device.GetNativeObject());
            Assert.Equal(raw.PhysicalDevice.Handle, device.WrapperVK.GetPhysicalDeviceVK());
            Assert.Equal(raw.QueueFamily, device.WrapperVK.GetQueueFamilyIndexVK(device.GetQueue(QueueType.GRAPHICS)));

            ExerciseWrappedDevice(device);

            var typeInfo = new VkSemaphoreTypeCreateInfo { semaphoreType = VkSemaphoreType.Timeline, initialValue = 7 };
            var info = new VkSemaphoreCreateInfo { pNext = &typeInfo };
            VkSemaphore semaphore;
            Assert.Equal(VkResult.Success, raw.Api.vkCreateSemaphore(&info, null, &semaphore));
            using (var fence = device.WrapperVK.CreateFenceVK(semaphore.Handle)) Assert.Equal(7UL, fence.Value);
            raw.Api.vkDestroySemaphore(semaphore, null);

            Assert.Equal(0, device.LiveObjects);
        }

        var vkErrors = raw.Errors;
        Assert.True(vkErrors.Count == 0, "Vulkan validation (wrapped device): " + string.Join(" | ", vkErrors));
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>A buffer created, bound and destroyed, and one submit through the wrapped device's queue.</summary>
    private static void ExerciseWrappedDevice(NriDevice device)
    {
        using (var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 1024, usage = BufferUsageBits.SHADER_RESOURCE }))
        {
            Assert.Equal(1024UL, buffer.Desc.size);
            Assert.Equal(1, device.LiveObjects);
        }

        using var upload = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 256 });
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = 256 });
        var bytes = upload.Map();
        for (var i = 0; i < 256; i++) bytes[i] = (byte)(255 - i);
        upload.Unmap();
        GpuWork.Run(device, cb =>
        {
            cb.Barrier([], [new NriBufferBarrier(upload, GpuWork.Nothing, GpuWork.CopySource), new NriBufferBarrier(readback, GpuWork.Nothing, GpuWork.CopyDestination)], []);
            cb.CopyBuffer(readback, 0, upload, 0, 256);
        });
        var copied = readback.Map().ToArray();
        readback.Unmap();
        Assert.Equal(Enumerable.Range(0, 256).Select(i => (byte)(255 - i)), copied);
        device.WaitIdle();
    }

    internal static VDXGI.IDXGIAdapter1 DxgiAdapterFor(GraphicsAPI api)
    {
        var uid = TestDevice.AdapterFor(api)!.Uid;
        Assert.True(uid.high == 0, $"NRI's adapter uid is a UUID (high 0x{uid.high:X}), not a LUID; DXGI cannot match it");
        using var factory = VDXGI.DXGI.CreateDXGIFactory1<VDXGI.IDXGIFactory4>();
        var low = uid.low;
        return factory.EnumAdapterByLuid<VDXGI.IDXGIAdapter1>(Unsafe.As<ulong, Vortice.Luid>(ref low));
    }

    /// <summary>
    /// A VkInstance and VkDevice the test owns: Vulkan 1.3 (or what the driver has, at least 1.2),
    /// every supported core feature enabled, no extensions beyond the ones asked for, one graphics
    /// queue, and the Khronos validation layer with an error-collecting messenger when installed.
    /// </summary>
    internal sealed class RawVulkanDevice : IDisposable
    {
        private readonly List<string> _errors = [];
        private GCHandle _self;
        private VkInstanceApi? _instanceApi;
        private VkDebugUtilsMessengerEXT _messenger;

        public VkInstance Instance { get; private set; }
        public VkPhysicalDevice PhysicalDevice { get; private set; }
        public VkDevice Device { get; private set; }
        public VkDeviceApi Api { get; private set; } = null!;
        public uint QueueFamily { get; private set; }
        public byte MinorVersion { get; private set; }
        public string DeviceName { get; private set; } = "";
        public bool Validated { get; private set; }

        public List<string> Errors
        {
            get { lock (_errors) return [.. _errors]; }
        }

        /// <param name="adapter">NRI's adapter; the physical device is matched by its uid.</param>
        /// <param name="instanceExtensions">Instance extensions enabled besides the ones this class needs.</param>
        /// <param name="deviceExtensions">Device extensions enabled besides VK_KHR_portability_subset.</param>
        public static RawVulkanDevice Create(NriAdapter adapter, string[]? instanceExtensions = null, string[]? deviceExtensions = null)
        {
            var result = new RawVulkanDevice();
            try
            {
                result.Initialize(adapter, instanceExtensions ?? [], deviceExtensions ?? []);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        private void Initialize(NriAdapter adapter, string[] extraInstanceExtensions, string[] extraDeviceExtensions)
        {
            VulkanApi.RequireLoader();
            _self = GCHandle.Alloc(this);

            uint layerCount = 0;
            vkEnumerateInstanceLayerProperties(&layerCount, null);
            var layers = new VkLayerProperties[layerCount];
            fixed (VkLayerProperties* p = layers) vkEnumerateInstanceLayerProperties(&layerCount, p);
            foreach (var layer in layers)
            {
                var l = layer;
                Validated |= Marshal.PtrToStringUTF8((nint)l.layerName) == "VK_LAYER_KHRONOS_validation";
            }

            // A portability driver (MoltenVK) is only enumerated by an instance that opts in with
            // VK_KHR_portability_enumeration and the matching flag; without it the loader hides
            // the device. Where no such driver exists the loader does not offer the extension.
            uint extensionCount = 0;
            vkEnumerateInstanceExtensionProperties(null, &extensionCount, null);
            var availableExtensions = new VkExtensionProperties[extensionCount];
            fixed (VkExtensionProperties* p = availableExtensions) vkEnumerateInstanceExtensionProperties(null, &extensionCount, p);
            var portability = availableExtensions.Any(e => Marshal.PtrToStringUTF8((nint)e.extensionName) == "VK_KHR_portability_enumeration");

            List<string> extensionList = [.. extraInstanceExtensions];
            if (Validated) extensionList.Add("VK_EXT_debug_utils");
            if (portability) extensionList.Add("VK_KHR_portability_enumeration");
            using var layerNames = new VkStringArray(Validated ? ["VK_LAYER_KHRONOS_validation"] : Array.Empty<string>());
            using var instanceExtensions = new VkStringArray(extensionList);
            var app = new VkApplicationInfo { apiVersion = VK_API_VERSION_1_3 };
            var messengerInfo = new VkDebugUtilsMessengerCreateInfoEXT
            {
                messageSeverity = VkDebugUtilsMessageSeverityFlagsEXT.Error,
                messageType = VkDebugUtilsMessageTypeFlagsEXT.General | VkDebugUtilsMessageTypeFlagsEXT.Validation,
                pfnUserCallback = &OnMessage,
                pUserData = (void*)GCHandle.ToIntPtr(_self),
            };
            var instanceInfo = new VkInstanceCreateInfo
            {
                pNext = Validated ? &messengerInfo : null,
                flags = portability ? VkInstanceCreateFlags.EnumeratePortabilityKHR : VkInstanceCreateFlags.None,
                pApplicationInfo = &app,
                enabledLayerCount = layerNames.Length,
                ppEnabledLayerNames = layerNames,
                enabledExtensionCount = instanceExtensions.Length,
                ppEnabledExtensionNames = instanceExtensions,
            };
            VkInstance instance;
            Check(vkCreateInstance(&instanceInfo, null, &instance), "vkCreateInstance");
            Instance = instance;
            _instanceApi = VulkanApi.For(instance);
            if (Validated)
            {
                VkDebugUtilsMessengerEXT messenger;
                Check(_instanceApi.vkCreateDebugUtilsMessengerEXT(&messengerInfo, null, &messenger), "vkCreateDebugUtilsMessengerEXT");
                _messenger = messenger;
            }

            // The physical device whose identity is NRI's adapter uid: the LUID when the driver has one
            // (Windows), otherwise the UUID in both halves, as NRI's ConstructUid builds it (Linux).
            var uid = adapter.Uid;
            uint count = 0;
            Check(_instanceApi.vkEnumeratePhysicalDevices(&count, null), "vkEnumeratePhysicalDevices");
            var physicalDevices = new VkPhysicalDevice[count];
            fixed (VkPhysicalDevice* p = physicalDevices) Check(_instanceApi.vkEnumeratePhysicalDevices(&count, p), "vkEnumeratePhysicalDevices");
            foreach (var candidate in physicalDevices)
            {
                var id = new VkPhysicalDeviceIDProperties();
                var properties = new VkPhysicalDeviceProperties2 { pNext = &id };
                _instanceApi.vkGetPhysicalDeviceProperties2(candidate, &properties);
                var (low, high) = id.deviceLUIDValid
                    ? (Unsafe.ReadUnaligned<ulong>(id.deviceLUID), 0UL)
                    : (Unsafe.ReadUnaligned<ulong>(id.deviceUUID), Unsafe.ReadUnaligned<ulong>(id.deviceUUID + 8));
                if (low == uid.low && high == uid.high)
                {
                    PhysicalDevice = candidate;
                    DeviceName = Marshal.PtrToStringUTF8((nint)properties.properties.deviceName) ?? "";
                    MinorVersion = (byte)Math.Min(3, (properties.properties.apiVersion >> 12) & 0x3FF);
                    break;
                }
            }
            Assert.True(PhysicalDevice.Handle != 0, $"no VkPhysicalDevice has uid {uid.high:X16}{uid.low:X16} ({adapter.Name})");
            Assert.True(MinorVersion >= 2, $"{DeviceName} supports Vulkan 1.{MinorVersion}; NRI needs 1.2");

            uint familyCount = 0;
            _instanceApi.vkGetPhysicalDeviceQueueFamilyProperties(PhysicalDevice, &familyCount, null);
            var families = new VkQueueFamilyProperties[familyCount];
            fixed (VkQueueFamilyProperties* p = families) _instanceApi.vkGetPhysicalDeviceQueueFamilyProperties(PhysicalDevice, &familyCount, p);
            QueueFamily = (uint)Array.FindIndex(families, f => (f.queueFlags & VkQueueFlags.Graphics) != 0);

            // Everything the physical device supports, so that whatever NRI decides to use from
            // its own feature query is enabled.
            var features13 = new VkPhysicalDeviceVulkan13Features();
            var features12 = new VkPhysicalDeviceVulkan12Features { pNext = MinorVersion >= 3 ? &features13 : null };
            var features11 = new VkPhysicalDeviceVulkan11Features { pNext = &features12 };
            var features = new VkPhysicalDeviceFeatures2 { pNext = &features11 };
            _instanceApi.vkGetPhysicalDeviceFeatures2(PhysicalDevice, &features);

            // A device that advertises VK_KHR_portability_subset must have it enabled
            // (VUID-VkDeviceCreateInfo-pProperties-04451); that and the caller's are the only extensions enabled here.
            uint deviceExtensionCount = 0;
            _instanceApi.vkEnumerateDeviceExtensionProperties(PhysicalDevice, null, &deviceExtensionCount, null);
            var deviceExtensions = new VkExtensionProperties[deviceExtensionCount];
            fixed (VkExtensionProperties* p = deviceExtensions) _instanceApi.vkEnumerateDeviceExtensionProperties(PhysicalDevice, null, &deviceExtensionCount, p);
            var subset = deviceExtensions.Any(e => Marshal.PtrToStringUTF8((nint)e.extensionName) == "VK_KHR_portability_subset");
            using var enabledDeviceExtensions = new VkStringArray([.. extraDeviceExtensions, .. (subset ? ["VK_KHR_portability_subset"] : Array.Empty<string>())]);

            var priority = 1.0f;
            var queueInfo = new VkDeviceQueueCreateInfo { queueFamilyIndex = QueueFamily, queueCount = 1, pQueuePriorities = &priority };
            var deviceInfo = new VkDeviceCreateInfo
            {
                pNext = &features,
                queueCreateInfoCount = 1,
                pQueueCreateInfos = &queueInfo,
                enabledExtensionCount = enabledDeviceExtensions.Length,
                ppEnabledExtensionNames = enabledDeviceExtensions,
            };
            VkDevice device;
            Check(_instanceApi.vkCreateDevice(PhysicalDevice, &deviceInfo, null, &device), "vkCreateDevice");
            Device = device;
            Api = VulkanApi.For(Instance, device);
        }

        [UnmanagedCallersOnly]
        private static uint OnMessage(VkDebugUtilsMessageSeverityFlagsEXT severity, VkDebugUtilsMessageTypeFlagsEXT types, VkDebugUtilsMessengerCallbackDataEXT* data, void* user)
        {
            if (GCHandle.FromIntPtr((nint)user).Target is RawVulkanDevice self && (severity & VkDebugUtilsMessageSeverityFlagsEXT.Error) != 0
                && (types & VkDebugUtilsMessageTypeFlagsEXT.Validation) != 0) // the loader reports missing layer manifests as General errors
            {
                var text = Marshal.PtrToStringUTF8((nint)data->pMessage) ?? "";
                lock (self._errors) self._errors.Add(text);
            }
            return 0; // VK_FALSE
        }

        private static void Check(VkResult result, string call) => Assert.True(result == VkResult.Success, $"{call}: {result}");

        public void Dispose()
        {
            if (Device.Handle != 0) Api.vkDestroyDevice(null);
            if (_messenger.Handle != 0) _instanceApi!.vkDestroyDebugUtilsMessengerEXT(_messenger, null);
            if (Instance.Handle != 0) _instanceApi!.vkDestroyInstance(null);
            if (_self.IsAllocated) _self.Free();
        }
    }
}
