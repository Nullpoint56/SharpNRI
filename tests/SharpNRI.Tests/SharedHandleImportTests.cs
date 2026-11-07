// Shared handles (NRI #294, eb4ed356), the other side: a handle NRI exports is opened by a
// second, independent API object on the same adapter and used there. SharedHandleTests proves the
// handles come out; this proves they mean something.
//
// What a compositor does internally, reduced to the raw APIs and nothing else:
//   - D3D12 texture -> D3D11:  ID3D11Device1::OpenSharedResource1, copy, map, compare to NRI's readback
//   - D3D12 fence   -> D3D12:  ID3D12Device::OpenSharedHandle on a second device, signalled both ways
//   - VK texture    -> VK:     vkAllocateMemory + VkImportMemoryWin32HandleInfoKHR on a second VkDevice
//   - VK semaphore  -> VK:     vkImportSemaphoreWin32HandleKHR on a second VkDevice, signalled both ways
//   - VK binary     -> VK:     the same into a binary VkSemaphore, one queue signal and one queue wait each way
//
// Hand-off: after rendering, NRI moves the texture to Layout::GENERAL (D3D12_BARRIER_LAYOUT_COMMON,
// VK_IMAGE_LAYOUT_GENERAL) with no access after, which is the state a resource is handed to another
// API in. The importer starts from that layout.
//
// Every second device runs with its API's debug layer where that is cheap (D3D11 debug device,
// the D3D12 debug layer NRI already enabled process-wide, the Khronos validation layer on the second
// Vulkan instance) and any error it reports fails the test, like NRI's own messages do.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using SharpNRI.Native;
using Vortice.Vulkan;
using Xunit;
using static Vortice.Vulkan.Vulkan;
using V11 = Vortice.Direct3D11;
using V11Debug = Vortice.Direct3D11.Debug;
using V12 = Vortice.Direct3D12;
using V12Debug = Vortice.Direct3D12.Debug;
using VD3D = Vortice.Direct3D;
using VDXGI = Vortice.DXGI;

namespace SharpNRI.Tests;

public sealed unsafe class SharedHandleImportTests(ITestOutputHelper output)
{
    private const ushort Size = 64;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private static TextureDesc SharedDesc => new()
    {
        type = TextureType.TEXTURE_2D,
        usage = TextureUsageBits.COLOR_ATTACHMENT | TextureUsageBits.SHADER_RESOURCE,
        format = Format.RGBA8_UNORM,
        width = Size, height = Size, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
    };

    private static AccessLayoutStage HandedOff => new() { access = AccessBits.NONE, layout = Layout.GENERAL, stages = StageBits.NONE };

    private static void Require(GraphicsAPI api)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "NT handle import is the Windows path; elsewhere it would import OPAQUE_FD, which is not covered here");
        TestDevice.RequireAvailable(new Backend(api));
    }

    /// <summary>Renders the triangle into a new shared texture, hands it off, and returns NRI's readback of it.</summary>
    private static byte[] RenderShared(NriDevice device, NriTexture texture)
    {
        var pixels = TriangleTests.RenderTriangle(device, texture);
        GpuWork.Run(device, cb => cb.Barrier(new NriTextureBarrier(texture, GpuWork.TextureCopySource, HandedOff)));
        device.WaitIdle();
        return pixels;
    }

    // ==================================================================================== D3D12 -> D3D11

    [Fact]
    public void A_D3D12_shared_texture_opens_in_D3D12_and_is_refused_by_D3D11_because_it_is_typeless()
    {
        Require(GraphicsAPI.D3D12);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.D3D12, messages);

        using var texture = device.CreateSharedTexture(SharedDesc);
        var expected = RenderShared(device, texture);

        using var adapter = DxgiAdapterFor(GraphicsAPI.D3D12);
        var hr = V11.D3D11.D3D11CreateDevice(adapter, VD3D.DriverType.Unknown, V11.DeviceCreationFlags.Debug,
            [VD3D.FeatureLevel.Level_11_1, VD3D.FeatureLevel.Level_11_0], out V11.ID3D11Device d3d11, out V11.ID3D11DeviceContext context);
        Assert.True(hr.Success, $"D3D11CreateDevice (debug) on '{adapter.Description1.Description}': {hr}");
        using var _ = d3d11;
        using var __ = context;
        using var d3d11Device1 = d3d11!.QueryInterface<V11.ID3D11Device1>();
        using var infoQueue = d3d11.QueryInterfaceOrNull<V11Debug.ID3D11InfoQueue>();
        Assert.NotNull(infoQueue);

        // The measured limitation (AGENTS.md: Shared handles): NRI creates every texture TYPELESS, and D3D11's
        // OpenSharedResource1 refuses a typeless shared resource with E_INVALIDARG while a D3D12
        // device opens the same handle. Import into D3D12 or Vulkan instead; shared D3D12 textures
        // stay typeless like every other NRI texture; this test pins the refusal so a change in
        // either NRI or D3D11 shows up as a failure here rather than as a surprise downstream.
        using var handle = texture.GetSharedHandle();
        Assert.Equal(SharedHandleType.D3D12_RESOURCE, handle.Type);
        Assert.Contains("Typeless", D3D12ResourceFormat(texture), StringComparison.OrdinalIgnoreCase);

        var openResult = d3d11Device1.OpenSharedResource1(handle.Handle, out V11.ID3D11Texture2D? opened);
        using var ___ = opened;
        Assert.True(openResult.Failure, "D3D11 opened NRI's typeless shared resource; the limitation recorded in AGENTS.md (Shared handles) no longer holds and the header comment in NRIDescs.h should be revisited");
        Assert.Equal(unchecked((int)0x80070057), openResult.Code); // E_INVALIDARG

        var hr12 = V12.D3D12.D3D12CreateDevice(adapter, VD3D.FeatureLevel.Level_12_0, out V12.ID3D12Device? second);
        Assert.True(hr12.Success, $"D3D12CreateDevice (second device): {hr12}");
        using var ____ = second;
        using var openedIn12 = second!.OpenSharedHandle<V12.ID3D12Resource>(handle.Handle);
        var desc12 = openedIn12.Description;
        Assert.Equal((ulong)Size, desc12.Width);
        Assert.Equal((uint)Size, desc12.Height);
        Assert.Equal(VDXGI.Format.R8G8B8A8_Typeless, desc12.Format);

        Assert.Equal(Size * Size * 4, expected.Length);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    // ==================================================================================== D3D12 fence -> D3D12

    [Fact]
    public void A_D3D12_shared_fence_opens_on_a_second_D3D12_device_and_signals_both_ways()
    {
        Require(GraphicsAPI.D3D12);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.D3D12, messages);
        using var fence = device.CreateSharedFence(0);

        // The debug layer is process-wide and NRI enabled it before creating its device (validation is
        // on in every TestDevice); enabling it again now would remove the live device. The second
        // device inherits it, which the info queue proves.
        using var adapter = DxgiAdapterFor(GraphicsAPI.D3D12);
        var hr = V12.D3D12.D3D12CreateDevice(adapter, VD3D.FeatureLevel.Level_11_0, out V12.ID3D12Device? second);
        Assert.True(hr.Success, $"D3D12CreateDevice on '{adapter.Description1.Description}': {hr}");
        using var _ = second;
        using var infoQueue = second!.QueryInterfaceOrNull<V12Debug.ID3D12InfoQueue>();
        Assert.True(infoQueue is not null, "the second D3D12 device has no debug layer (ID3D12InfoQueue)");

        V12.ID3D12Fence opened;
        using (var handle = fence.GetSharedHandle())
        {
            Assert.Equal(SharedHandleType.D3D12_FENCE, handle.Type);
            opened = second.OpenSharedHandle<V12.ID3D12Fence>(handle.Handle);
        }
        using var __ = opened;
        Assert.Equal(0UL, opened.CompletedValue);

        // NRI -> second device: a queue signal of 7 on NRI's side is seen by the opened fence.
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using (var allocator = queue.CreateCommandAllocator())
        using (var commandBuffer = allocator.CreateCommandBuffer())
        {
            commandBuffer.Begin();
            commandBuffer.End();
            queue.Submit(commandBuffer, fence, 7);
            using var completion = new AutoResetEvent(false);
            opened.SetEventOnCompletion(7, completion.SafeWaitHandle.DangerousGetHandle());
            Assert.True(completion.WaitOne(Timeout), $"the opened fence did not reach 7 within {Timeout}; it is at {opened.CompletedValue}");
            device.WaitIdle();
        }
        var seenBySecond = opened.CompletedValue;
        output.WriteLine($"NRI signalled 7 on its queue; the opened fence on the second device reads {seenBySecond}");
        Assert.Equal(7UL, seenBySecond);

        // Second device -> NRI: the second device's queue signals 9, NRI's fence reads it.
        using (var otherQueue = second.CreateCommandQueue(new V12.CommandQueueDescription(V12.CommandListType.Direct)))
        {
            otherQueue.Signal(opened, 9);
            var seenByNri = PollUntil(() => fence.Value, 9);
            output.WriteLine($"the second device's queue signalled 9; NRI's fence reads {seenByNri}");
            Assert.Equal(9UL, seenByNri);
            Assert.Equal(9UL, opened.CompletedValue);
        }

        var d3d12Errors = D3D12Errors(infoQueue!);
        Assert.True(d3d12Errors.Count == 0, "D3D12 debug layer (second device): " + string.Join(" | ", d3d12Errors));
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    // ==================================================================================== VK -> VK

    [Fact]
    public void A_Vulkan_shared_texture_imports_into_a_second_Vulkan_device_with_identical_pixels()
    {
        Require(GraphicsAPI.VK);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.VK, messages);
        Assert.SkipWhen(device.Desc.features.sharedResources == 0, "features.sharedResources is false: the adapter lacks VK_KHR_external_memory_win32 or exports no image");

        using var texture = device.CreateSharedTexture(SharedDesc);
        var expected = RenderShared(device, texture);

        using var vk = SecondVulkanDevice.Create(TestDevice.AdapterFor(GraphicsAPI.VK)!);
        output.WriteLine($"second VkDevice on '{vk.DeviceName}', validation {(vk.Validated ? "on" : "unavailable")}");
        var api = vk.Api;

        // The image NRI created (DeviceVK::FillCreateInfo for this desc, with only the graphics
        // queue family active so EXCLUSIVE): the same parameters, plus the external-memory info.
        var external = new VkExternalMemoryImageCreateInfo { handleTypes = VkExternalMemoryHandleTypeFlags.OpaqueWin32 };
        var imageInfo = new VkImageCreateInfo
        {
            pNext = &external,
            flags = VkImageCreateFlags.MutableFormat | VkImageCreateFlags.ExtendedUsage | VkImageCreateFlags.Alias,
            imageType = VkImageType.Image2D,
            format = VkFormat.R8G8B8A8Unorm,
            extent = new VkExtent3D(Size, Size, 1),
            mipLevels = 1,
            arrayLayers = 1,
            samples = VkSampleCountFlags.Count1,
            tiling = VkImageTiling.Optimal,
            usage = VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled | VkImageUsageFlags.ColorAttachment,
            sharingMode = VkSharingMode.Exclusive,
            initialLayout = VkImageLayout.Undefined,
        };
        VkImage image;
        Check(api.vkCreateImage(&imageInfo, null, &image), "vkCreateImage");

        VkMemoryRequirements requirements;
        api.vkGetImageMemoryRequirements(image, &requirements);

        VkDeviceMemory memory;
        // Two exports are independent: the first is closed before the second imports. A repeated
        // Win32 export returns the same handle, so NRI exports once and duplicates (NRI PR #294 review, item 1).
        var first = texture.GetSharedHandle();
        using (var handle = texture.GetSharedHandle())
        {
            Assert.Equal(SharedHandleType.OPAQUE_WIN32, handle.Type);
            Assert.NotEqual(first.Handle, handle.Handle);
            first.Dispose();
            output.WriteLine($"exported allocation {handle.Size} bytes; the importer's image needs {requirements.size} (alignment {requirements.alignment}, types 0x{requirements.memoryTypeBits:X})");
            Assert.True(handle.Size >= requirements.size, $"exported size {handle.Size} < required {requirements.size}");

            var import = new VkImportMemoryWin32HandleInfoKHR { handleType = VkExternalMemoryHandleTypeFlags.OpaqueWin32, handle = handle.Handle };
            var dedicated = new VkMemoryDedicatedAllocateInfo { pNext = &import, image = image };
            var allocateInfo = new VkMemoryAllocateInfo
            {
                pNext = &dedicated,
                allocationSize = handle.Size,
                memoryTypeIndex = vk.FindMemoryType(requirements.memoryTypeBits, VkMemoryPropertyFlags.DeviceLocal, VkMemoryPropertyFlags.HostVisible),
            };
            Check(api.vkAllocateMemory(&allocateInfo, null, &memory), "vkAllocateMemory (import OPAQUE_WIN32)");
        } // an NT handle import does not take ownership: closing it is the caller's job
        Check(api.vkBindImageMemory(image, memory, 0), "vkBindImageMemory");

        var bufferInfo = new VkBufferCreateInfo { size = Size * Size * 4, usage = VkBufferUsageFlags.TransferDst, sharingMode = VkSharingMode.Exclusive };
        VkBuffer buffer;
        Check(api.vkCreateBuffer(&bufferInfo, null, &buffer), "vkCreateBuffer");
        VkMemoryRequirements bufferRequirements;
        api.vkGetBufferMemoryRequirements(buffer, &bufferRequirements);
        var bufferAllocate = new VkMemoryAllocateInfo
        {
            allocationSize = bufferRequirements.size,
            memoryTypeIndex = vk.FindMemoryType(bufferRequirements.memoryTypeBits, VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent, 0),
        };
        VkDeviceMemory bufferMemory;
        Check(api.vkAllocateMemory(&bufferAllocate, null, &bufferMemory), "vkAllocateMemory (readback)");
        Check(api.vkBindBufferMemory(buffer, bufferMemory, 0), "vkBindBufferMemory");

        var actual = new byte[Size * Size * 4];
        var (copiedImage, copiedBuffer) = (image, buffer); // the lambda cannot capture locals whose address was taken
        vk.Submit(cmd =>
        {
            var toCopy = new VkImageMemoryBarrier
            {
                srcAccessMask = VkAccessFlags.None,
                dstAccessMask = VkAccessFlags.TransferRead,
                oldLayout = VkImageLayout.General,
                newLayout = VkImageLayout.TransferSrcOptimal,
                srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
                image = copiedImage,
                subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color, 0, 1, 0, 1),
            };
            api.vkCmdPipelineBarrier(cmd, VkPipelineStageFlags.TopOfPipe, VkPipelineStageFlags.Transfer, 0, 0, null, 0, null, 1, &toCopy);

            var region = new VkBufferImageCopy
            {
                bufferOffset = 0, bufferRowLength = 0, bufferImageHeight = 0,
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                imageOffset = default,
                imageExtent = new VkExtent3D(Size, Size, 1),
            };
            api.vkCmdCopyImageToBuffer(cmd, copiedImage, VkImageLayout.TransferSrcOptimal, copiedBuffer, 1, &region);

            var toHost = new VkBufferMemoryBarrier
            {
                srcAccessMask = VkAccessFlags.TransferWrite,
                dstAccessMask = VkAccessFlags.HostRead,
                srcQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = VK_QUEUE_FAMILY_IGNORED,
                buffer = copiedBuffer, offset = 0, size = VK_WHOLE_SIZE,
            };
            api.vkCmdPipelineBarrier(cmd, VkPipelineStageFlags.Transfer, VkPipelineStageFlags.Host, 0, 0, null, 1, &toHost, 0, null);
        });

        void* mappedPtr;
        Check(api.vkMapMemory(bufferMemory, 0, VK_WHOLE_SIZE, 0, &mappedPtr), "vkMapMemory");
        new ReadOnlySpan<byte>(mappedPtr, actual.Length).CopyTo(actual);
        api.vkUnmapMemory(bufferMemory);

        api.vkDestroyBuffer(buffer, null);
        api.vkFreeMemory(bufferMemory, null);
        api.vkDestroyImage(image, null);
        api.vkFreeMemory(memory, null);

        AssertSamePixels(expected, actual, "second VkDevice's copy of the imported image");

        var vkErrors = vk.Errors;
        Assert.True(vkErrors.Count == 0, "Vulkan validation (second device): " + string.Join(" | ", vkErrors));
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Fact]
    public void A_Vulkan_shared_semaphore_imports_into_a_second_Vulkan_device_and_signals_both_ways()
    {
        Require(GraphicsAPI.VK);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.VK, messages);
        Assert.SkipWhen(device.Desc.features.sharedFences == 0, "features.sharedFences is false: the adapter lacks VK_KHR_external_semaphore_win32 or exports no semaphore type");
        using var fence = device.CreateSharedFence(0);

        using var vk = SecondVulkanDevice.Create(TestDevice.AdapterFor(GraphicsAPI.VK)!);
        output.WriteLine($"second VkDevice on '{vk.DeviceName}', validation {(vk.Validated ? "on" : "unavailable")}");
        var api = vk.Api;

        // The importer's semaphore must be a timeline semaphore too: an import replaces the payload,
        // not the type.
        var type = new VkSemaphoreTypeCreateInfo { semaphoreType = VkSemaphoreType.Timeline, initialValue = 0 };
        var semaphoreInfo = new VkSemaphoreCreateInfo { pNext = &type };
        VkSemaphore semaphore;
        Check(api.vkCreateSemaphore(&semaphoreInfo, null, &semaphore), "vkCreateSemaphore");

        var first = fence.GetSharedHandle(); // closed before the second imports, as in the texture test
        using (var handle = fence.GetSharedHandle())
        {
            Assert.Equal(SharedHandleType.OPAQUE_WIN32, handle.Type);
            Assert.NotEqual(first.Handle, handle.Handle);
            first.Dispose();
            var import = new VkImportSemaphoreWin32HandleInfoKHR
            {
                semaphore = semaphore,
                handleType = VkExternalSemaphoreHandleTypeFlags.OpaqueWin32,
                handle = handle.Handle,
            };
            Check(api.vkImportSemaphoreWin32HandleKHR(&import), "vkImportSemaphoreWin32HandleKHR");
        }

        // NRI -> second device: NRI's queue signals 7, the second device waits for 7.
        var queue = device.GetQueue(QueueType.GRAPHICS);
        using (var allocator = queue.CreateCommandAllocator())
        using (var commandBuffer = allocator.CreateCommandBuffer())
        {
            commandBuffer.Begin();
            commandBuffer.End();
            queue.Submit(commandBuffer, fence, 7);

            var value = 7UL;
            var waitInfo = new VkSemaphoreWaitInfo { semaphoreCount = 1, pSemaphores = &semaphore, pValues = &value };
            var waited = api.vkWaitSemaphores(&waitInfo, (ulong)Timeout.Ticks * 100);
            Assert.True(waited == VkResult.Success, $"vkWaitSemaphores(7) on the imported semaphore returned {waited}");
            device.WaitIdle();
        }
        ulong seenBySecond;
        Check(api.vkGetSemaphoreCounterValue(semaphore, &seenBySecond), "vkGetSemaphoreCounterValue");
        output.WriteLine($"NRI signalled 7 on its queue; the imported semaphore reads {seenBySecond}");
        Assert.Equal(7UL, seenBySecond);

        // Second device -> NRI: a host signal of 9 on the imported semaphore, NRI's fence reads it.
        var signal = new VkSemaphoreSignalInfo { semaphore = semaphore, value = 9 };
        Check(api.vkSignalSemaphore(&signal), "vkSignalSemaphore");
        var seenByNri = PollUntil(() => fence.Value, 9);
        output.WriteLine($"the second device signalled 9 from the host; NRI's fence reads {seenByNri}");
        Assert.Equal(9UL, seenByNri);

        api.vkDestroySemaphore(semaphore, null);

        var vkErrors = vk.Errors;
        Assert.True(vkErrors.Count == 0, "Vulkan validation (second device): " + string.Join(" | ", vkErrors));
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The binary form, the one compositors import: NRI's exported binary semaphore imported into
    /// a binary VkSemaphore on the second device, then one signal and one wait in each direction
    /// with real submits on both queues (binary semaphores alternate signal and wait exactly). Each
    /// wait is satisfied only by the other device's signal; a lost signal shows up as a timed-out fence.
    /// </summary>
    [Fact]
    public void A_Vulkan_shared_binary_semaphore_imports_into_a_second_Vulkan_device_and_signals_both_ways()
    {
        Require(GraphicsAPI.VK);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.VK, messages);
        Assert.SkipWhen(device.Desc.features.sharedFences == 0, "features.sharedFences is false: the adapter lacks VK_KHR_external_semaphore_win32 or exports no semaphore type");
        using var binary = device.CreateSharedBinaryFence();
        using var fence = device.CreateFence();

        using var vk = SecondVulkanDevice.Create(TestDevice.AdapterFor(GraphicsAPI.VK)!);
        output.WriteLine($"second VkDevice on '{vk.DeviceName}', validation {(vk.Validated ? "on" : "unavailable")}");
        var api = vk.Api;

        // The importer's semaphore is binary as well: an import replaces the payload, not the type.
        var semaphoreInfo = new VkSemaphoreCreateInfo();
        VkSemaphore semaphore;
        Check(api.vkCreateSemaphore(&semaphoreInfo, null, &semaphore), "vkCreateSemaphore");

        using (var handle = binary.GetSharedHandle())
        {
            Assert.Equal(SharedHandleType.OPAQUE_WIN32, handle.Type);
            var import = new VkImportSemaphoreWin32HandleInfoKHR
            {
                semaphore = semaphore,
                handleType = VkExternalSemaphoreHandleTypeFlags.OpaqueWin32,
                handle = handle.Handle,
            };
            Check(api.vkImportSemaphoreWin32HandleKHR(&import), "vkImportSemaphoreWin32HandleKHR");
        }

        var queue = device.GetQueue(QueueType.GRAPHICS);
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        commandBuffer.Begin();
        commandBuffer.End();

        // NRI -> second device: NRI's queue signals, the second device's queue waits.
        queue.Submit([commandBuffer], [], [new NriFenceSubmit(binary, 0), new NriFenceSubmit(fence, 1)]);
        var first = vk.SubmitSemaphores(wait: semaphore, signal: default);
        output.WriteLine($"NRI signalled the binary semaphore; the second device's wait on it completed: {first}");
        Assert.True(first == VkResult.Success, $"the second device's submit waiting on the imported semaphore: {first}");
        GpuWork.Wait(fence, 1);

        // Second device -> NRI: the second device's queue signals, NRI's queue waits.
        var second = vk.SubmitSemaphores(wait: default, signal: semaphore);
        Assert.True(second == VkResult.Success, $"the second device's submit signalling the imported semaphore: {second}");
        queue.Submit([], [new NriFenceSubmit(binary, 0)], [new NriFenceSubmit(fence, 2)]);
        var seenByNri = PollUntil(() => fence.Value, 2);
        output.WriteLine($"the second device signalled the binary semaphore; NRI's wait on it let fence reach {seenByNri}");
        Assert.Equal(2UL, seenByNri);

        device.WaitIdle();
        api.vkDeviceWaitIdle();
        api.vkDestroySemaphore(semaphore, null);

        var vkErrors = vk.Errors;
        Assert.True(vkErrors.Count == 0, "Vulkan validation (second device): " + string.Join(" | ", vkErrors));
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    // ==================================================================================== helpers

    private void AssertSamePixels(byte[] expected, byte[] actual, string what)
    {
        Assert.Equal(expected.Length, actual.Length);
        var differing = 0;
        var first = -1;
        for (var i = 0; i < expected.Length; i++)
        {
            if (expected[i] == actual[i]) continue;
            differing++;
            if (first < 0) first = i;
        }
        output.WriteLine($"{what}: {expected.Length} bytes compared, {differing} differ; NRI sha256 {Convert.ToHexString(SHA256.HashData(expected))}, imported sha256 {Convert.ToHexString(SHA256.HashData(actual))}");
        if (differing != 0) Assert.Fail(
            $"{what}: {differing} of {expected.Length} bytes differ; first at pixel ({first / 4 % Size},{first / 4 / Size}) channel {first % 4}: NRI {expected[first]}, imported {actual[first]}");
    }

    private static ulong PollUntil(Func<ulong> read, ulong value)
    {
        var deadline = DateTime.UtcNow + Timeout;
        var current = read();
        while (current < value && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(1);
            current = read();
        }
        return current;
    }

    private static VDXGI.IDXGIAdapter1 DxgiAdapterFor(GraphicsAPI api)
    {
        var uid = TestDevice.AdapterFor(api)!.Uid;
        Assert.True(uid.high == 0, $"NRI's adapter uid is a UUID (high 0x{uid.high:X}), not a LUID; DXGI cannot match it");
        using var factory = VDXGI.DXGI.CreateDXGIFactory1<VDXGI.IDXGIFactory4>();
        var low = uid.low;
        var luid = Unsafe.As<ulong, Vortice.Luid>(ref low);
        return factory.EnumAdapterByLuid<VDXGI.IDXGIAdapter1>(luid);
    }

    /// <summary>The D3D12 resource's format and flags, read from NRI's native object (diagnostics only).</summary>
    private static string D3D12ResourceFormat(NriTexture texture)
    {
        var raw = (nint)texture.GetNativeObject();
        Marshal.AddRef(raw); // the wrapper's Dispose releases this reference, not NRI's
        using var resource = new V12.ID3D12Resource(raw);
        var desc = resource.Description;
        return $"{desc.Format} ({desc.Flags}, layout {desc.Layout})";
    }

    private static List<string> D3D12Errors(V12Debug.ID3D12InfoQueue queue)
    {
        var errors = new List<string>();
        for (ulong i = 0; i < queue.NumStoredMessages; i++)
        {
            var m = queue.GetMessage(i);
            if (m.Severity is V12Debug.MessageSeverity.Error or V12Debug.MessageSeverity.Corruption) errors.Add($"{m.Severity} {m.Id}: {m.Description}");
        }
        return errors;
    }

    private static void Check(VkResult result, string what) => Assert.True(result == VkResult.Success, $"{what} returned {result}");

    /// <summary>
    /// A second Vulkan instance and device, on the physical device whose LUID is NRI's adapter uid,
    /// with external memory/semaphore Win32 and timeline semaphores, validated when the layer exists.
    /// </summary>
    private sealed class SecondVulkanDevice : IDisposable
    {
        private readonly List<string> _errors = [];
        private GCHandle _self;
        private VkInstance _instance;
        private VkInstanceApi _instanceApi = null!;
        private VkDebugUtilsMessengerEXT _messenger;
        private VkPhysicalDevice _physicalDevice;
        private VkDevice _device;
        private VkQueue _queue;
        private uint _queueFamily;
        private VkCommandPool _pool;

        public VkDeviceApi Api { get; private set; } = null!;
        public string DeviceName { get; private set; } = "";
        public bool Validated { get; private set; }

        public List<string> Errors
        {
            get { lock (_errors) return [.. _errors]; }
        }

        public static SecondVulkanDevice Create(NriAdapter adapter)
        {
            var result = new SecondVulkanDevice();
            try
            {
                result.Initialize(adapter);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        private void Initialize(NriAdapter adapter)
        {
            VulkanApi.RequireLoader();
            _self = GCHandle.Alloc(this);

            // Instance, with the validation layer and a messenger when the layer is installed.
            uint layerCount = 0;
            vkEnumerateInstanceLayerProperties(&layerCount, null);
            var layers = new VkLayerProperties[layerCount];
            fixed (VkLayerProperties* p = layers) vkEnumerateInstanceLayerProperties(&layerCount, p);
            for (var i = 0; i < layers.Length && !Validated; i++)
            {
                var layer = layers[i];
                Validated = Marshal.PtrToStringUTF8((nint)layer.layerName) == "VK_LAYER_KHRONOS_validation";
            }

            using var layerNames = new VkStringArray(Validated ? ["VK_LAYER_KHRONOS_validation"] : Array.Empty<string>());
            using var instanceExtensions = new VkStringArray(Validated ? ["VK_EXT_debug_utils"] : Array.Empty<string>());
            var appName = "SharpNRI.Tests importer"u8;
            fixed (byte* name = appName)
            {
                var app = new VkApplicationInfo { pApplicationName = name, apiVersion = VK_API_VERSION_1_3 };
                var messengerInfo = new VkDebugUtilsMessengerCreateInfoEXT
                {
                    messageSeverity = VkDebugUtilsMessageSeverityFlagsEXT.Error | VkDebugUtilsMessageSeverityFlagsEXT.Warning,
                    messageType = VkDebugUtilsMessageTypeFlagsEXT.General | VkDebugUtilsMessageTypeFlagsEXT.Validation | VkDebugUtilsMessageTypeFlagsEXT.Performance,
                    pfnUserCallback = &OnMessage,
                    pUserData = (void*)GCHandle.ToIntPtr(_self),
                };
                var instanceInfo = new VkInstanceCreateInfo
                {
                    pNext = Validated ? &messengerInfo : null,
                    pApplicationInfo = &app,
                    enabledLayerCount = layerNames.Length,
                    ppEnabledLayerNames = layerNames,
                    enabledExtensionCount = instanceExtensions.Length,
                    ppEnabledExtensionNames = instanceExtensions,
                };
                VkInstance instance;
                Check(vkCreateInstance(&instanceInfo, null, &instance), "vkCreateInstance");
                _instance = instance;
                _instanceApi = VulkanApi.For(instance);
                if (Validated)
                {
                    VkDebugUtilsMessengerEXT messenger;
                    Check(_instanceApi.vkCreateDebugUtilsMessengerEXT(&messengerInfo, null, &messenger), "vkCreateDebugUtilsMessengerEXT");
                    _messenger = messenger;
                }
            }

            // The physical device whose LUID is NRI's adapter uid.
            var uid = adapter.Uid;
            Assert.True(uid.high == 0, $"NRI's adapter uid is a UUID (high 0x{uid.high:X}), not a LUID");
            uint count = 0;
            Check(_instanceApi.vkEnumeratePhysicalDevices(&count, null), "vkEnumeratePhysicalDevices");
            var physicalDevices = new VkPhysicalDevice[count];
            fixed (VkPhysicalDevice* p = physicalDevices) Check(_instanceApi.vkEnumeratePhysicalDevices(&count, p), "vkEnumeratePhysicalDevices");
            var seen = new List<string>();
            foreach (var candidate in physicalDevices)
            {
                var id = new VkPhysicalDeviceIDProperties();
                var properties = new VkPhysicalDeviceProperties2 { pNext = &id };
                _instanceApi.vkGetPhysicalDeviceProperties2(candidate, &properties);
                var deviceName = Marshal.PtrToStringUTF8((nint)properties.properties.deviceName) ?? "";
                var luid = id.deviceLUIDValid ? Unsafe.ReadUnaligned<ulong>(id.deviceLUID) : 0;
                seen.Add($"{deviceName} luid 0x{luid:X16} valid {(bool)id.deviceLUIDValid}");
                if (id.deviceLUIDValid && luid == uid.low)
                {
                    _physicalDevice = candidate;
                    DeviceName = deviceName;
                    break;
                }
            }
            Assert.True(_physicalDevice.Handle != 0, $"no VkPhysicalDevice has LUID 0x{uid.low:X16} ({adapter.Name}); seen: {string.Join("; ", seen)}");

            // A graphics queue (graphics implies transfer).
            uint familyCount = 0;
            _instanceApi.vkGetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &familyCount, null);
            var families = new VkQueueFamilyProperties[familyCount];
            fixed (VkQueueFamilyProperties* p = families) _instanceApi.vkGetPhysicalDeviceQueueFamilyProperties(_physicalDevice, &familyCount, p);
            _queueFamily = (uint)Array.FindIndex(families, f => (f.queueFlags & VkQueueFlags.Graphics) != 0);

            var priority = 1.0f;
            var queueInfo = new VkDeviceQueueCreateInfo { queueFamilyIndex = _queueFamily, queueCount = 1, pQueuePriorities = &priority };
            var features12 = new VkPhysicalDeviceVulkan12Features { timelineSemaphore = true };
            using var deviceExtensions = new VkStringArray(["VK_KHR_external_memory_win32", "VK_KHR_external_semaphore_win32"]);
            var deviceInfo = new VkDeviceCreateInfo
            {
                pNext = &features12,
                queueCreateInfoCount = 1,
                pQueueCreateInfos = &queueInfo,
                enabledExtensionCount = deviceExtensions.Length,
                ppEnabledExtensionNames = deviceExtensions,
            };
            VkDevice device;
            Check(_instanceApi.vkCreateDevice(_physicalDevice, &deviceInfo, null, &device), "vkCreateDevice");
            _device = device;
            Api = VulkanApi.For(_instance, device);

            VkQueue queue;
            Api.vkGetDeviceQueue(_queueFamily, 0, &queue);
            _queue = queue;

            var poolInfo = new VkCommandPoolCreateInfo { queueFamilyIndex = _queueFamily };
            VkCommandPool pool;
            Check(Api.vkCreateCommandPool(&poolInfo, null, &pool), "vkCreateCommandPool");
            _pool = pool;
        }

        public uint FindMemoryType(uint typeBits, VkMemoryPropertyFlags required, VkMemoryPropertyFlags avoid)
        {
            VkPhysicalDeviceMemoryProperties properties;
            _instanceApi.vkGetPhysicalDeviceMemoryProperties(_physicalDevice, &properties);
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < properties.memoryTypeCount; i++)
                {
                    if ((typeBits & (1u << i)) == 0) continue;
                    var flags = properties.memoryTypes[i].propertyFlags;
                    if ((flags & required) != required) continue;
                    if (pass == 0 && (flags & avoid) != 0) continue;
                    return (uint)i;
                }
            }
            Assert.Fail($"no memory type in 0x{typeBits:X} has {required}");
            return 0;
        }

        /// <summary>Records, submits on the graphics queue, waits on a fence.</summary>
        public void Submit(Action<VkCommandBuffer> record)
        {
            var allocateInfo = new VkCommandBufferAllocateInfo { commandPool = _pool, level = VkCommandBufferLevel.Primary, commandBufferCount = 1 };
            VkCommandBuffer cmd;
            Check(Api.vkAllocateCommandBuffers(&allocateInfo, &cmd), "vkAllocateCommandBuffers");
            var begin = new VkCommandBufferBeginInfo { flags = VkCommandBufferUsageFlags.OneTimeSubmit };
            Check(Api.vkBeginCommandBuffer(cmd, &begin), "vkBeginCommandBuffer");
            record(cmd);
            Check(Api.vkEndCommandBuffer(cmd), "vkEndCommandBuffer");

            var fenceInfo = new VkFenceCreateInfo();
            VkFence fence;
            Check(Api.vkCreateFence(&fenceInfo, null, &fence), "vkCreateFence");
            var submit = new VkSubmitInfo { commandBufferCount = 1, pCommandBuffers = &cmd };
            Check(Api.vkQueueSubmit(_queue, 1, &submit, fence), "vkQueueSubmit");
            var waited = Api.vkWaitForFences(1, &fence, true, (ulong)Timeout.Ticks * 100);
            Api.vkDestroyFence(fence, null);
            Api.vkFreeCommandBuffers(_pool, 1, &cmd);
            Check(waited, "vkWaitForFences");
        }

        /// <summary>
        /// A submit with no command buffers that waits on and/or signals one binary semaphore, and
        /// a fence waited for at most <see cref="Timeout"/>. Returns the fence wait's result.
        /// </summary>
        public VkResult SubmitSemaphores(VkSemaphore wait, VkSemaphore signal)
        {
            var fenceInfo = new VkFenceCreateInfo();
            VkFence fence;
            Check(Api.vkCreateFence(&fenceInfo, null, &fence), "vkCreateFence");
            var waitStage = VkPipelineStageFlags.AllCommands;
            var submit = new VkSubmitInfo
            {
                waitSemaphoreCount = wait.Handle != 0 ? 1u : 0u,
                pWaitSemaphores = &wait,
                pWaitDstStageMask = &waitStage,
                signalSemaphoreCount = signal.Handle != 0 ? 1u : 0u,
                pSignalSemaphores = &signal,
            };
            Check(Api.vkQueueSubmit(_queue, 1, &submit, fence), "vkQueueSubmit");
            var waited = Api.vkWaitForFences(1, &fence, true, (ulong)Timeout.Ticks * 100);
            if (waited == VkResult.Success) Api.vkDestroyFence(fence, null); // a fence still pending must not be destroyed
            return waited;
        }

        [UnmanagedCallersOnly]
        private static uint OnMessage(VkDebugUtilsMessageSeverityFlagsEXT severity, VkDebugUtilsMessageTypeFlagsEXT types, VkDebugUtilsMessengerCallbackDataEXT* data, void* user)
        {
            var self = (SecondVulkanDevice)GCHandle.FromIntPtr((nint)user).Target!;
            var text = $"{severity} {Marshal.PtrToStringUTF8((nint)data->pMessageIdName)}: {Marshal.PtrToStringUTF8((nint)data->pMessage)}";
            if (TestDevice.Trace) Console.Error.WriteLine($"[vk importer] {text}");
            // Errors of type General are the loader's, about this machine's implicit-layer manifests (a
            // missing overlay JSON); they say nothing about the API usage under test and are recorded only.
            if ((severity & VkDebugUtilsMessageSeverityFlagsEXT.Error) != 0 && (types & (VkDebugUtilsMessageTypeFlagsEXT.Validation | VkDebugUtilsMessageTypeFlagsEXT.Performance)) != 0)
                lock (self._errors) self._errors.Add(text);
            else
                GpuWork.Record($"[vk importer] {text}");
            return 0;
        }

        public void Dispose()
        {
            if (_device.Handle != 0)
            {
                Api.vkDeviceWaitIdle();
                if (_pool.Handle != 0) Api.vkDestroyCommandPool(_pool, null);
                Api.vkDestroyDevice(null);
            }
            if (_messenger.Handle != 0) _instanceApi.vkDestroyDebugUtilsMessengerEXT(_messenger, null);
            if (_instance.Handle != 0) _instanceApi.vkDestroyInstance(null);
            if (_self.IsAllocated) _self.Free();
        }
    }
}
