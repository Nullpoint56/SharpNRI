// The wrapper extensions' Create*VK / Create*D3D12 imports, each used for real work: native objects
// made with Vortice on the NRI device's own VkDevice or ID3D12Device, imported, and driven through
// NRI. One flow per backend runs the Fill.hlsl dispatch, copies its output through imported
// objects, and checks every byte; acceleration structures built by the RayTracing extension are
// imported back and traced.

using System.Runtime.InteropServices;
using SharpNRI.Native;
using Vortice.Vulkan;
using Xunit;
using static SharpNRI.Tests.GpuWork;
using static Vortice.Vulkan.Vulkan;
using V12 = Vortice.Direct3D12;
using VDXGI = Vortice.DXGI;

namespace SharpNRI.Tests;

public sealed unsafe partial class WrapperExtensionTests
{
    private const int FillWords = 64;
    private const int FillBytes = FillWords * 4;   // 4x4 RGBA8 texels exactly

    /// <summary>
    /// Imports VkBuffer, VkImage, VkDeviceMemory, VkCommandPool, VkCommandBuffer, VkDescriptorPool,
    /// VkPipeline and VkQueryPool. Submission 1, on a command buffer from the imported pool: the
    /// imported compute pipeline writes the Fill pattern into the imported buffer through a set
    /// from the imported descriptor pool; the buffer is copied into an NRI buffer bound to the
    /// imported memory and uploaded into the imported image. Submission 2, on the imported
    /// VkCommandBuffer: the image is read back, bracketed by two timestamps in the imported pool.
    /// </summary>
    [Fact]
    public void Imported_Vulkan_objects_dispatch_copy_and_time_through_NRI()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.VK, messages);
        var vk = device.WrapperVK;
        var instance = new VkInstance(vk.GetInstanceVK());
        var instanceApi = VulkanApi.For(instance);
        var api = VulkanApi.For(instance, new VkDevice(device.GetNativeObject()));
        var physicalDevice = new VkPhysicalDevice(vk.GetPhysicalDeviceVK());
        VkPhysicalDeviceMemoryProperties memoryProperties = default;
        instanceApi.vkGetPhysicalDeviceMemoryProperties(physicalDevice, &memoryProperties);
        var typeFlags = new VkMemoryPropertyFlags[memoryProperties.memoryTypeCount];
        var types = (VkMemoryType*)&memoryProperties.memoryTypes;
        for (var i = 0; i < typeFlags.Length; i++) typeFlags[i] = types[i].propertyFlags;
        var queue = device.GetQueue(QueueType.GRAPHICS);

        uint MemoryType(uint bits, VkMemoryPropertyFlags flags)
        {
            for (var i = 0u; i < typeFlags.Length; i++)
                if ((bits & (1u << (int)i)) != 0 && (typeFlags[i] & flags) == flags) return i;
            Assert.Fail($"no memory type in 0x{bits:X} with {flags}");
            return 0;
        }

        // NRI creates its buffers with device-address usage where the device has it, so memory
        // they bind into is allocated with the device-address flag.
        VkDeviceMemory Allocate(VkMemoryRequirements requirements, VkMemoryPropertyFlags flags)
        {
            var allocateFlags = new VkMemoryAllocateFlagsInfo { flags = VkMemoryAllocateFlags.DeviceAddress };
            var info = new VkMemoryAllocateInfo { pNext = &allocateFlags, allocationSize = requirements.size, memoryTypeIndex = MemoryType(requirements.memoryTypeBits, flags) };
            VkDeviceMemory memory;
            Check(api.vkAllocateMemory(&info, null, &memory), "vkAllocateMemory");
            return memory;
        }

        var cleanup = new List<Action>();
        try
        {
            // The storage buffer: VkBuffer and its DEVICE_LOCAL memory, both made and bound here.
            var bufferInfo = new VkBufferCreateInfo
            {
                size = FillBytes,
                usage = VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.ShaderDeviceAddress,
                sharingMode = VkSharingMode.Exclusive,
            };
            VkBuffer vkBuffer;
            Check(api.vkCreateBuffer(&bufferInfo, null, &vkBuffer), "vkCreateBuffer");
            { var copy = vkBuffer; cleanup.Add(() => api.vkDestroyBuffer(copy, null)); }
            VkMemoryRequirements bufferRequirements;
            api.vkGetBufferMemoryRequirements(vkBuffer, &bufferRequirements);
            var bufferMemory = Allocate(bufferRequirements, VkMemoryPropertyFlags.DeviceLocal);
            { var copy = bufferMemory; cleanup.Add(() => api.vkFreeMemory(copy, null)); }
            Check(api.vkBindBufferMemory(vkBuffer, bufferMemory, 0), "vkBindBufferMemory");
            var storage = vk.CreateBufferVK(vkBuffer.Handle, FillBytes, structureStride: 4);
            cleanup.Add(storage.Dispose);
            var storageView = storage.CreateView(BufferView.STORAGE_STRUCTURED_BUFFER);
            cleanup.Add(storageView.Dispose);
            Assert.Equal((ulong)FillBytes, storage.Desc.size);
            Assert.Equal(BufferUsageBits.NONE, storage.Desc.usage); // BufferVKDesc has no usage; NRI records none

            // The image: VkImage RGBA8 4x4 and its memory, made and bound here.
            var imageInfo = new VkImageCreateInfo
            {
                imageType = VkImageType.Image2D,
                format = VkFormat.R8G8B8A8Unorm,
                extent = new VkExtent3D(4, 4, 1),
                mipLevels = 1,
                arrayLayers = 1,
                samples = VkSampleCountFlags.Count1,
                tiling = VkImageTiling.Optimal,
                usage = VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst,
                sharingMode = VkSharingMode.Exclusive,
                initialLayout = VkImageLayout.Undefined,
            };
            VkImage vkImage;
            Check(api.vkCreateImage(&imageInfo, null, &vkImage), "vkCreateImage");
            { var copy = vkImage; cleanup.Add(() => api.vkDestroyImage(copy, null)); }
            VkMemoryRequirements imageRequirements;
            api.vkGetImageMemoryRequirements(vkImage, &imageRequirements);
            var imageMemory = Allocate(imageRequirements, VkMemoryPropertyFlags.DeviceLocal);
            { var copy = imageMemory; cleanup.Add(() => api.vkFreeMemory(copy, null)); }
            Check(api.vkBindImageMemory(vkImage, imageMemory, 0), "vkBindImageMemory");
            var texture = vk.CreateTextureVK(new TextureVKDesc
            {
                vkImage = vkImage.Handle,
                vkFormat = (int)VkFormat.R8G8B8A8Unorm,
                vkImageType = (int)VkImageType.Image2D,
                vkImageUsageFlags = (uint)imageInfo.usage,
                width = 4, height = 4, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
            });
            cleanup.Add(texture.Dispose);
            Assert.Equal(TextureType.TEXTURE_2D, texture.Desc.type);
            Assert.Equal(Format.RGBA8_UNORM, texture.Desc.format);    // NRI's mapping of VK_FORMAT_R8G8B8A8_UNORM
            Assert.Equal((ushort)4, texture.Desc.width);
            Assert.Equal((ushort)4, texture.Desc.height);

            // The readback buffer is NRI's, bound into a HOST_VISIBLE VkDeviceMemory imported with
            // no mapping, which NRI maps itself.
            var layout = new CopyLayout(device);
            var readback = device.CreateBuffer(new BufferDesc { size = layout.ReadbackSize });
            VkMemoryRequirements readbackRequirements;
            api.vkGetBufferMemoryRequirements(new VkBuffer(readback.GetNativeObject()), &readbackRequirements);
            var hostType = MemoryType(readbackRequirements.memoryTypeBits, VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent);
            var hostMemory = Allocate(readbackRequirements, VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent);
            { var copy = hostMemory; cleanup.Add(() => api.vkFreeMemory(copy, null)); }
            var importedMemory = vk.CreateMemoryVK(hostMemory.Handle, 0, 0, readbackRequirements.size, hostType);
            cleanup.Add(importedMemory.Dispose);
            cleanup.Add(readback.Dispose);                                      // before the memory it is bound to
            device.BindBufferMemory(readback, importedMemory);
            var staging = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = layout.SlicePitch });
            cleanup.Add(staging.Dispose);

            // The NRI layout the sets come from, and a VkPipelineLayout defined identically for the
            // pipeline: one STORAGE_BUFFER at binding 0 of set 0, compute only, binding flags 0.
            var nriLayout = device.CreatePipelineLayout(FillLayout());
            cleanup.Add(nriLayout.Dispose);
            var binding = new VkDescriptorSetLayoutBinding { binding = 0, descriptorType = VkDescriptorType.StorageBuffer, descriptorCount = 1, stageFlags = VkShaderStageFlags.Compute };
            var bindingFlags = VkDescriptorBindingFlags.None;
            var bindingFlagsInfo = new VkDescriptorSetLayoutBindingFlagsCreateInfo { bindingCount = 1, pBindingFlags = &bindingFlags };
            var setLayoutInfo = new VkDescriptorSetLayoutCreateInfo { pNext = &bindingFlagsInfo, bindingCount = 1, pBindings = &binding };
            VkDescriptorSetLayout setLayout;
            Check(api.vkCreateDescriptorSetLayout(&setLayoutInfo, null, &setLayout), "vkCreateDescriptorSetLayout");
            { var copy = setLayout; cleanup.Add(() => api.vkDestroyDescriptorSetLayout(copy, null)); }
            var pipelineLayoutInfo = new VkPipelineLayoutCreateInfo { setLayoutCount = 1, pSetLayouts = &setLayout };
            VkPipelineLayout pipelineLayout;
            Check(api.vkCreatePipelineLayout(&pipelineLayoutInfo, null, &pipelineLayout), "vkCreatePipelineLayout");
            { var copy = pipelineLayout; cleanup.Add(() => api.vkDestroyPipelineLayout(copy, null)); }

            var spirv = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", "Fill.cs.spv"));
            VkShaderModule module;
            fixed (byte* code = spirv)
            {
                var moduleInfo = new VkShaderModuleCreateInfo { codeSize = (nuint)spirv.Length, pCode = (uint*)code };
                Check(api.vkCreateShaderModule(&moduleInfo, null, &module), "vkCreateShaderModule");
            }
            { var copy = module; cleanup.Add(() => api.vkDestroyShaderModule(copy, null)); }
            VkPipeline vkPipeline;
            fixed (byte* entry = "main"u8)
            {
                var pipelineInfo = new VkComputePipelineCreateInfo
                {
                    stage = new VkPipelineShaderStageCreateInfo { stage = VkShaderStageFlags.Compute, module = module, pName = entry },
                    layout = pipelineLayout,
                };
                Check(api.vkCreateComputePipelines(VkPipelineCache.Null, 1, &pipelineInfo, null, &vkPipeline), "vkCreateComputePipelines");
            }
            { var copy = vkPipeline; cleanup.Add(() => api.vkDestroyPipeline(copy, null)); }
            var pipeline = vk.CreatePipelineVK(new PipelineVKDesc { vkPipeline = vkPipeline.Handle, vkPipelineBindPoint = (int)VkPipelineBindPoint.Compute });
            cleanup.Add(pipeline.Dispose);
            Assert.Equal(BindPoint.COMPUTE, pipeline.BindPoint);

            // Descriptor pool: room for the one set.
            var poolSize = new VkDescriptorPoolSize { type = VkDescriptorType.StorageBuffer, descriptorCount = 1 };
            var descriptorPoolInfo = new VkDescriptorPoolCreateInfo { maxSets = 1, poolSizeCount = 1, pPoolSizes = &poolSize };
            VkDescriptorPool vkDescriptorPool;
            Check(api.vkCreateDescriptorPool(&descriptorPoolInfo, null, &vkDescriptorPool), "vkCreateDescriptorPool");
            { var copy = vkDescriptorPool; cleanup.Add(() => api.vkDestroyDescriptorPool(copy, null)); }
            var descriptorPool = vk.CreateDescriptorPoolVK(new DescriptorPoolVKDesc { vkDescriptorPool = vkDescriptorPool.Handle, descriptorSetMaxNum = 1 });
            cleanup.Add(descriptorPool.Dispose);
            var set = descriptorPool.AllocateDescriptorSet(nriLayout, 0);
            set.Update(0, 0, storageView);

            // Command pool for the graphics family, one NRI command buffer from it and one raw.
            var commandPoolInfo = new VkCommandPoolCreateInfo { flags = VkCommandPoolCreateFlags.ResetCommandBuffer, queueFamilyIndex = vk.GetQueueFamilyIndexVK(queue) };
            VkCommandPool vkCommandPool;
            Check(api.vkCreateCommandPool(&commandPoolInfo, null, &vkCommandPool), "vkCreateCommandPool");
            { var copy = vkCommandPool; cleanup.Add(() => api.vkDestroyCommandPool(copy, null)); }
            var allocator = vk.CreateCommandAllocatorVK(new CommandAllocatorVKDesc { vkCommandPool = vkCommandPool.Handle, queueType = QueueType.GRAPHICS });
            cleanup.Add(allocator.Dispose);
            var first = allocator.CreateCommandBuffer();
            cleanup.Add(first.Dispose);
            var commandBufferInfo = new VkCommandBufferAllocateInfo { commandPool = vkCommandPool, level = VkCommandBufferLevel.Primary, commandBufferCount = 1 };
            VkCommandBuffer vkCommandBuffer;
            Check(api.vkAllocateCommandBuffers(&commandBufferInfo, &vkCommandBuffer), "vkAllocateCommandBuffers");
            var second = vk.CreateCommandBufferVK(vkCommandBuffer.Handle, QueueType.GRAPHICS);
            cleanup.Add(second.Dispose);
            Assert.Null(second.Allocator);

            // Timestamp pool.
            var queryPoolInfo = new VkQueryPoolCreateInfo { queryType = VkQueryType.Timestamp, queryCount = 2 };
            VkQueryPool vkQueryPool;
            Check(api.vkCreateQueryPool(&queryPoolInfo, null, &vkQueryPool), "vkCreateQueryPool");
            { var copy = vkQueryPool; cleanup.Add(() => api.vkDestroyQueryPool(copy, null)); }
            var queries = vk.CreateQueryPoolVK(new QueryPoolVKDesc { vkQueryPool = vkQueryPool.Handle, vkQueryType = (int)VkQueryType.Timestamp }, 2);
            cleanup.Add(queries.Dispose);
            Assert.Equal(QueryType.TIMESTAMP, queries.Type);
            Assert.Equal(8u, queries.QuerySize);

            var fence = device.CreateFence();
            cleanup.Add(fence.Dispose);
            first.Begin(descriptorPool);
            RecordFill(first, nriLayout, set, pipeline, storage, staging, texture, readback, layout);
            first.End();
            queue.Submit(first, fence, 1);
            GpuWork.Wait(fence, 1);

            // NRI's validation counts a wrapped command buffer as recording from the start and refuses
            // Begin ("already in the recording state"): the owner begins it natively, NRI records and ends.
            var beginInfo = new VkCommandBufferBeginInfo { flags = VkCommandBufferUsageFlags.OneTimeSubmit };
            Check(api.vkBeginCommandBuffer(vkCommandBuffer, &beginInfo), "vkBeginCommandBuffer");
            second.ResetQueries(queries, 0, 2);
            second.EndQuery(queries, 0);
            second.Barrier(new NriTextureBarrier(texture, TextureCopyDestination, TextureCopySource));
            second.ReadbackTextureToBuffer(readback, layout.TextureLayout, texture, CopyLayout.Region);
            second.EndQuery(queries, 1);
            second.CopyQueries(queries, 0, 2, readback, layout.QueryOffset);
            second.End();
            queue.Submit(second, fence, 2);
            GpuWork.Wait(fence, 2);

            var ticks = layout.CheckReadback(readback);
            Record($"VK imported timestamps {ticks.First} -> {ticks.Second}");
            device.WaitIdle();
            Assert.DoesNotContain(messages, m => m.IsError);
            // The copy, upload and readback above are already checked byte-exact; only the timestamps
            // are left. The paravirtual Mac writes them after the submission that copies them
            // (CLAUDE.md: MoltenVK and the paravirtual Mac), the same defect QueryTests skips on.
            if (TestDevice.IsAppleParavirtualVK(GraphicsAPI.VK) && ticks.First == 0 && ticks.Second == 0)
                Assert.Skip("Apple Paravirtual device: timestamps copied in the same submission read 0 (CLAUDE.md: MoltenVK and the paravirtual Mac); the imported objects' copies and readback passed");
            Assert.NotEqual(0UL, ticks.First);
            Assert.True(ticks.Second >= ticks.First, $"second timestamp {ticks.Second} < first {ticks.First}");
            device.WaitIdle();
            Assert.DoesNotContain(messages, m => m.IsError);
        }
        finally
        {
            for (var i = cleanup.Count - 1; i >= 0; i--) cleanup[i]();
        }
        Assert.Equal(0, device.LiveObjects);

        static void Check(VkResult result, string call) => Assert.True(result == VkResult.Success, $"{call}: {result}");
    }

    /// <summary>
    /// Imports ID3D12Heap (memory), ID3D12Resource (texture), a pair of shader-visible
    /// ID3D12DescriptorHeap (descriptor pool) and an ID3D12GraphicsCommandList with its allocator
    /// (command buffer). The Fill dispatch runs on the imported command list with a set from the
    /// imported heaps; its output is copied into an NRI buffer placed in the imported readback
    /// heap and through the imported texture, and read back byte for byte.
    /// </summary>
    [Fact]
    public void Imported_D3D12_objects_dispatch_and_copy_through_NRI()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.D3D12));
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.D3D12, messages);
        var wrapper = device.WrapperD3D12;
        Marshal.AddRef(device.GetNativeObject()); // the Vortice wrapper's Dispose releases this reference, not NRI's
        using var d3d = new V12.ID3D12Device(device.GetNativeObject());
        var queue = device.GetQueue(QueueType.GRAPHICS);
        var layout = new CopyLayout(device);

        using var heap = d3d.CreateHeap<V12.ID3D12Heap>(new V12.HeapDescription(65536, V12.HeapType.Readback, 0, V12.HeapFlags.AllowOnlyBuffers));
        using var nativeTexture = d3d.CreateCommittedResource(new V12.HeapProperties(V12.HeapType.Default), V12.HeapFlags.None,
            V12.ResourceDescription.Texture2D(VDXGI.Format.R8G8B8A8_UNorm, 4, 4, 1, 1), V12.ResourceStates.Common);
        using var resourceHeap = d3d.CreateDescriptorHeap(new V12.DescriptorHeapDescription(V12.DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView, 16, V12.DescriptorHeapFlags.ShaderVisible));
        using var samplerHeap = d3d.CreateDescriptorHeap(new V12.DescriptorHeapDescription(V12.DescriptorHeapType.Sampler, 16, V12.DescriptorHeapFlags.ShaderVisible));
        using var commandAllocator = d3d.CreateCommandAllocator(V12.CommandListType.Direct);
        using var commandList = d3d.CreateCommandList<V12.ID3D12GraphicsCommandList>(V12.CommandListType.Direct, commandAllocator);
        // Left open: NRI's validation counts a wrapped command list as recording from the start and
        // refuses Begin. The descriptor pool is set by SetDescriptorPool instead of Begin.

        using (var memory = wrapper.CreateMemoryD3D12(heap.NativePointer))
        using (var readback = device.CreateBuffer(new BufferDesc { size = layout.ReadbackSize }))
        using (var texture = wrapper.CreateTextureD3D12(nativeTexture.NativePointer))
        using (var pool = wrapper.CreateDescriptorPoolD3D12(resourceHeap.NativePointer, samplerHeap.NativePointer, 1))
        using (var commandBuffer = wrapper.CreateCommandBufferD3D12(commandList.NativePointer, commandAllocator.NativePointer))
        using (var nriLayout = device.CreatePipelineLayout(FillLayout()))
        using (var pipeline = device.CreateComputePipeline(new NriComputePipelineDesc
        {
            PipelineLayout = nriLayout,
            Shader = new NriShaderDesc(StageBits.COMPUTE_SHADER, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Shaders", "Fill.cs.dxil")), "main"),
        }))
        using (var storage = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = FillBytes, structureStride = 4, usage = BufferUsageBits.SHADER_RESOURCE_STORAGE }))
        using (var storageView = storage.CreateView(BufferView.STORAGE_STRUCTURED_BUFFER))
        using (var staging = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = layout.SlicePitch }))
        using (var fence = device.CreateFence())
        {
            device.BindBufferMemory(readback, memory);
            Assert.Equal(TextureType.TEXTURE_2D, texture.Desc.type);
            Assert.Equal(Format.RGBA8_UNORM, texture.Desc.format);    // read back from the resource's DXGI format
            Assert.Equal((ushort)4, texture.Desc.width);
            Assert.Equal((ushort)4, texture.Desc.height);
            Assert.Equal(nativeTexture.NativePointer, (nint)texture.GetNativeObject());
            Assert.Null(commandBuffer.Allocator);

            var set = pool.AllocateDescriptorSet(nriLayout, 0);
            set.Update(0, 0, storageView);

            commandBuffer.SetDescriptorPool(pool);
            RecordFill(commandBuffer, nriLayout, set, pipeline, storage, staging, texture, readback, layout);
            commandBuffer.Barrier(new NriTextureBarrier(texture, TextureCopyDestination, TextureCopySource));
            commandBuffer.ReadbackTextureToBuffer(readback, layout.TextureLayout, texture, CopyLayout.Region);
            commandBuffer.End();
            queue.Submit(commandBuffer, fence, 1);
            GpuWork.Wait(fence, 1);

            layout.CheckReadback(readback, withQueries: false);
            device.WaitIdle();
        }

        Assert.Equal(0, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// A BLAS built through the RayTracing extension, imported back by its native object
    /// (<c>VkAccelerationStructureKHR</c> and its <c>VkBuffer</c>, or the <c>ID3D12Resource</c>)
    /// through <c>CreateAccelerationStructureVK</c> / <c>D3D12</c>, must report the same GPU
    /// handle, buffer and scratch sizes, and must trace the same rays when a TLAS instances it.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_built_BLAS_imports_back_through_the_wrapper_and_traces_the_same(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.tiers.rayTracing < 2, device.Desc.tiers.rayTracing == 0
            ? $"{backend} reports no ray tracing"
            : $"{backend}: tiers.rayTracing = 1, no ray query to trace the import with (needs 2)");
        var rt = device.RayTracing;
        using var scene = new RayScene(device, outputNum: 2);

        var blas = scene.Own(rt.CreateCommittedAccelerationStructure(MemoryLocation.DEVICE, RayScene.BlasDesc(AccelerationStructureBits.PREFER_FAST_TRACE | AccelerationStructureBits.ALLOW_UPDATE)));
        var scratch = scene.Scratch(blas.BuildScratchBufferSize);
        Run(device, cmd => scene.BuildBlas(cmd, blas, scratch));

        var native = blas.GetNativeObject();
        Assert.NotEqual(0UL, native);
        var backing = blas.Buffer;
        var imported = scene.Own(backend.Api == GraphicsAPI.VK
            ? device.WrapperVK.CreateAccelerationStructureVK(new AccelerationStructureVKDesc
            {
                vkAccelerationStructure = native,
                vkBuffer = backing.GetNativeObject(),
                bufferSize = backing.Desc.size,
                buildScratchSize = blas.BuildScratchBufferSize,
                updateScratchSize = blas.UpdateScratchBufferSize,
                flags = AccelerationStructureBits.PREFER_FAST_TRACE | AccelerationStructureBits.ALLOW_UPDATE,
            }, AccelerationStructureType.BOTTOM_LEVEL)
            : device.WrapperD3D12.CreateAccelerationStructureD3D12(new AccelerationStructureD3D12Desc
            {
                d3d12Resource = (ID3D12Resource*)native,
                size = backing.Desc.size,
                buildScratchSize = blas.BuildScratchBufferSize,
                updateScratchSize = blas.UpdateScratchBufferSize,
                flags = AccelerationStructureBits.PREFER_FAST_TRACE | AccelerationStructureBits.ALLOW_UPDATE,
            }, AccelerationStructureType.BOTTOM_LEVEL));

        Record($"{backend}: BLAS native 0x{native:X}, handle 0x{blas.GpuHandle:X}, buffer {backing.Desc.size} B; imported handle 0x{imported.GpuHandle:X}, native 0x{imported.GetNativeObject():X}");
        Assert.Equal(AccelerationStructureType.BOTTOM_LEVEL, imported.Type);
        Assert.Equal(blas.GpuHandle, imported.GpuHandle);
        Assert.Equal(native, imported.GetNativeObject());
        Assert.Equal(blas.BuildScratchBufferSize, imported.BuildScratchBufferSize);
        Assert.Equal(blas.UpdateScratchBufferSize, imported.UpdateScratchBufferSize);
        Assert.Equal(backing.GetNativeObject(), imported.Buffer.GetNativeObject());

        // A TLAS over the original and one over the import: the same rays hit.
        var original = scene.Tlas(blas);
        var viaImport = scene.Tlas(imported);
        var sets = new[] { scene.Output(original.Tlas), scene.Output(viaImport.Tlas) };
        Run(device, cmd =>
        {
            scene.BuildTlas(cmd, original);
            scene.BuildTlas(cmd, viaImport);
            foreach (var s in sets) scene.TraceInline(cmd, s);
            scene.CopyOutputs(cmd);
        }, scene.Pool);
        var maps = scene.Read();
        scene.ExpectTriangle(maps[0], hit: 1, miss: 0, "original BLAS");
        Assert.Equal(maps[0], maps[1]);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    private static NriPipelineLayoutDesc FillLayout() => new()
    {
        DescriptorSets =
        [
            new NriDescriptorSetDesc(0,
            [
                new DescriptorRangeDesc { baseRegisterIndex = 0, descriptorNum = 1, descriptorType = DescriptorType.STORAGE_STRUCTURED_BUFFER, shaderStages = StageBits.COMPUTE_SHADER },
            ]),
        ],
        ShaderStages = StageBits.COMPUTE_SHADER,
    };

    /// <summary>
    /// Fill into <paramref name="storage"/>; its 256 bytes copied whole into the readback buffer
    /// and, row by row through a pitched staging buffer, into the 4x4 texture.
    /// </summary>
    private static void RecordFill(NriCommandBuffer cmd, NriPipelineLayout layout, NriDescriptorSet set, NriPipeline pipeline,
        NriBuffer storage, NriBuffer staging, NriTexture texture, NriBuffer readback, CopyLayout copy)
    {
        cmd.SetPipelineLayout(BindPoint.COMPUTE, layout);
        cmd.SetDescriptorSet(0, set);
        cmd.SetPipeline(pipeline);
        cmd.Dispatch(1);
        // A global barrier for the Fill output: a VkBuffer imported by CreateBufferVK has usage NONE
        // (BufferVKDesc carries none), and NRI's validation refuses a buffer barrier whose access the
        // usage does not allow.
        cmd.Barrier([new GlobalBarrierDesc { before = ComputeStorage, after = CopySource }], [new NriBufferBarrier(staging, Nothing, CopyDestination), new NriBufferBarrier(readback, Nothing, CopyDestination)], []);
        cmd.CopyBuffer(readback, 0, storage, 0, FillBytes);
        for (var row = 0u; row < 4; row++) cmd.CopyBuffer(staging, row * copy.RowPitch, storage, row * 16, 16);
        cmd.Barrier([], [new NriBufferBarrier(staging, CopyDestination, CopySource)], [new NriTextureBarrier(texture, Undefined, TextureCopyDestination)]);
        cmd.UploadBufferToTexture(texture, CopyLayout.Region, staging, new TextureDataLayoutDesc { rowPitch = copy.RowPitch, slicePitch = copy.SlicePitch });
    }

    /// <summary>
    /// Where things land in the readback buffer: the Fill words at 0, the texture's rows at a
    /// slice-aligned offset with the device's row pitch, two timestamps after that.
    /// </summary>
    private sealed class CopyLayout
    {
        public static readonly TextureRegionDesc Region = new() { width = 4, height = 4, depth = 1, planes = PlaneBits.ALL };

        public CopyLayout(NriDevice device)
        {
            var row = device.Desc.memoryAlignment.uploadBufferTextureRow;
            var slice = device.Desc.memoryAlignment.uploadBufferTextureSlice;
            RowPitch = (uint)Align(16, row);
            SlicePitch = (uint)Align(4UL * RowPitch, slice);
            TextureOffset = Align(FillBytes, slice);
            QueryOffset = Align(TextureOffset + SlicePitch, 8);
            ReadbackSize = QueryOffset + 16;
        }

        public uint RowPitch { get; }
        public uint SlicePitch { get; }
        public ulong TextureOffset { get; }
        public ulong QueryOffset { get; }
        public ulong ReadbackSize { get; }

        public TextureDataLayoutDesc TextureLayout => new() { offset = TextureOffset, rowPitch = RowPitch, slicePitch = SlicePitch };

        /// <summary>Checks the Fill words and the texture rows; returns the two timestamps.</summary>
        public (ulong First, ulong Second) CheckReadback(NriBuffer readback, bool withQueries = true)
        {
            var bytes = readback.Map().ToArray();
            readback.Unmap();
            var expected = Enumerable.Range(0, FillWords).Select(i => 0xC0FFEE00u + (uint)i).ToArray();
            Assert.Equal(expected, MemoryMarshal.Cast<byte, uint>(bytes.AsSpan(0, FillBytes)).ToArray());
            var expectedBytes = MemoryMarshal.AsBytes(expected.AsSpan()).ToArray();
            for (var row = 0; row < 4; row++)
                Assert.Equal(expectedBytes.AsSpan(row * 16, 16).ToArray(), bytes.AsSpan((int)(TextureOffset + (ulong)row * RowPitch), 16).ToArray());
            return withQueries
                ? (MemoryMarshal.Read<ulong>(bytes.AsSpan((int)QueryOffset)), MemoryMarshal.Read<ulong>(bytes.AsSpan((int)QueryOffset + 8)))
                : (0, 0);
        }
    }
}
