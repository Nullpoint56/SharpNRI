// CUDA imports NRI's shared buffer and shared fence (NRI #294, eb4ed356) through the CUDA driver API,
// nvcuda.dll, which the NVIDIA driver installs: no toolkit, no runtime, nothing shipped. The
// structs are declared here with the offsets cuda.h 12.9 gives them (measured with a C++ probe,
// AGENTS.md: Shared handles); cuImportExternalMemory and cuImportExternalSemaphore take exactly the
// handle kinds GetBufferSharedHandle and GetFenceSharedHandle return.
//
// The proof: NRI writes the buffer and signals the fence; CUDA waits on the fence, reads NRI's
// bytes, writes its own and signals; NRI's queue waits on CUDA's signal and copies the buffer out.
// Both directions of the memory and of the synchronisation are then measured in one test.

using System.Runtime.InteropServices;
using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed class CudaInteropTests
{
    private const int Size = 4096;
    private const byte NriPattern = 0x11;
    private const byte CudaPattern = 0x5A;

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public unsafe void CUDA_reads_what_NRI_wrote_and_NRI_reads_what_CUDA_wrote_through_a_shared_buffer_and_fence(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        Assert.SkipUnless(OperatingSystem.IsWindows(), "the proof opens nvcuda.dll and passes NT handles; on Linux it would be libcuda.so.1 and file descriptors, not measured");
        Assert.SkipUnless(NativeLibrary.TryLoad("nvcuda.dll", out var nvcuda), "nvcuda.dll (the CUDA driver API, installed by the NVIDIA driver) is not present");
        NativeLibrary.Free(nvcuda);

        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var adapter = device.Desc.adapterDesc;
        Assert.SkipWhen(adapter.vendor != Vendor.NVIDIA, $"{backend}: the adapter is {adapter.vendor}'s, not NVIDIA's (CUDA)");
        Assert.SkipWhen(device.Desc.features.sharedResources == 0 || device.Desc.features.sharedFences == 0, $"{backend}: features.sharedResources / sharedFences are off");

        // The CUDA device whose LUID is the adapter's (uid.low, high 0, on Windows).
        Cuda.Check(Cuda.cuInit(0), "cuInit");
        int deviceCount;
        Cuda.Check(Cuda.cuDeviceGetCount(&deviceCount), "cuDeviceGetCount");
        var cudaDevice = -1;
        for (var i = 0; i < deviceCount && cudaDevice < 0; i++)
        {
            int candidate;
            Cuda.Check(Cuda.cuDeviceGet(&candidate, i), "cuDeviceGet");
            ulong luid;
            uint nodeMask;
            if (Cuda.cuDeviceGetLuid((byte*)&luid, &nodeMask, candidate) == 0 && adapter.uid.high == 0 && luid == adapter.uid.low)
                cudaDevice = candidate;
        }
        Assert.SkipWhen(cudaDevice < 0, $"{backend}: none of the {deviceCount} CUDA devices has the adapter's LUID");
        var name = stackalloc byte[256];
        Cuda.Check(Cuda.cuDeviceGetName(name, 256, cudaDevice), "cuDeviceGetName");
        GpuWork.Record($"{backend}: CUDA device {cudaDevice} '{Marshal.PtrToStringUTF8((nint)name)}' matches adapter '{device.Desc.adapterDesc.name.ToString()}' by LUID");

        nint context;
        Cuda.Check(Cuda.cuCtxCreate_v2(&context, 0, cudaDevice), "cuCtxCreate");
        try
        {
            using var buffer = device.CreateSharedBuffer(new BufferDesc { size = Size, usage = BufferUsageBits.SHADER_RESOURCE });
            using var fence = device.CreateSharedFence();
            using var upload = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = Size });
            using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = Size });
            upload.Map().Fill(NriPattern);
            upload.Unmap();

            var queue = device.GetQueue(QueueType.GRAPHICS);
            using var allocator = queue.CreateCommandAllocator();
            using var write = allocator.CreateCommandBuffer();
            using var read = allocator.CreateCommandBuffer();

            // 1. NRI fills the shared buffer and signals 1.
            write.Begin();
            write.Barrier(new NriBufferBarrier(buffer, GpuWork.Nothing, GpuWork.CopyDestination));
            write.CopyBuffer(buffer, 0, upload, 0, Size);
            // Release to the external queue family before CUDA takes the buffer (Vulkan needs the
            // transfer even on a CONCURRENT buffer; D3D12 ignores the queues).
            write.Barrier(new NriBufferBarrier(buffer, GpuWork.CopyDestination, GpuWork.CopyDestination, SrcQueue: queue, DstQueue: NriQueue.External));
            write.End();
            queue.Submit([write], [], [new NriFenceSubmit(fence, 1)]);

            // 2. CUDA imports both, waits for 1, reads NRI's bytes, writes its own, signals 2.
            using var bufferHandle = buffer.GetSharedHandle();
            using var fenceHandle = fence.GetSharedHandle();
            GpuWork.Record($"{backend}: importing {bufferHandle} and {fenceHandle}");
            var memoryDesc = new Cuda.ExternalMemoryHandleDesc
            {
                type = bufferHandle.Type switch
                {
                    SharedHandleType.OPAQUE_WIN32 => Cuda.MemoryHandleOpaqueWin32,
                    SharedHandleType.D3D12_RESOURCE => Cuda.MemoryHandleD3D12Resource,
                    _ => throw new InvalidOperationException($"unexpected handle type {bufferHandle.Type}"),
                },
                handle = bufferHandle.Handle,
                size = bufferHandle.Size,
                flags = bufferHandle.Type == SharedHandleType.D3D12_RESOURCE ? Cuda.MemoryDedicated : 0, // CUDA requires it for a D3D12 resource
            };
            nint externalMemory;
            Cuda.Check(Cuda.cuImportExternalMemory(&externalMemory, &memoryDesc), "cuImportExternalMemory");
            var semaphoreDesc = new Cuda.ExternalSemaphoreHandleDesc
            {
                type = fenceHandle.Type switch
                {
                    SharedHandleType.OPAQUE_WIN32 => Cuda.SemaphoreHandleTimelineWin32, // a Vulkan timeline semaphore
                    SharedHandleType.D3D12_FENCE => Cuda.SemaphoreHandleD3D12Fence,
                    _ => throw new InvalidOperationException($"unexpected handle type {fenceHandle.Type}"),
                },
                handle = fenceHandle.Handle,
            };
            nint externalSemaphore;
            Cuda.Check(Cuda.cuImportExternalSemaphore(&externalSemaphore, &semaphoreDesc), "cuImportExternalSemaphore");
            ulong devicePointer;
            try
            {
                var bufferDesc = new Cuda.ExternalMemoryBufferDesc { offset = 0, size = Size };
                Cuda.Check(Cuda.cuExternalMemoryGetMappedBuffer(&devicePointer, externalMemory, &bufferDesc), "cuExternalMemoryGetMappedBuffer");
                try
                {
                    var wait = new Cuda.ExternalSemaphoreWaitParams { value = 1 };
                    Cuda.Check(Cuda.cuWaitExternalSemaphoresAsync(&externalSemaphore, &wait, 1, 0), "cuWaitExternalSemaphoresAsync");

                    var seenByCuda = new byte[Size];
                    fixed (byte* p = seenByCuda) Cuda.Check(Cuda.cuMemcpyDtoHAsync_v2(p, devicePointer, Size, 0), "cuMemcpyDtoHAsync");
                    Cuda.Check(Cuda.cuStreamSynchronize(0), "cuStreamSynchronize");
                    Assert.True(seenByCuda.All(b => b == NriPattern), $"CUDA read {Convert.ToHexString(seenByCuda.AsSpan(0, 16))}... where NRI wrote 0x{NriPattern:X2}");

                    var fromCuda = new byte[Size];
                    Array.Fill(fromCuda, CudaPattern);
                    fixed (byte* p = fromCuda) Cuda.Check(Cuda.cuMemcpyHtoDAsync_v2(devicePointer, p, Size, 0), "cuMemcpyHtoDAsync");
                    var signal = new Cuda.ExternalSemaphoreSignalParams { value = 2 };
                    Cuda.Check(Cuda.cuSignalExternalSemaphoresAsync(&externalSemaphore, &signal, 1, 0), "cuSignalExternalSemaphoresAsync");

                    // 3. NRI's queue waits for CUDA's 2, copies the buffer out and signals 3.
                    read.Begin();
                    // Acquire from the external queue family: the same hand-off the release named, then the copy.
                    read.Barrier(new NriBufferBarrier(buffer, GpuWork.CopyDestination, GpuWork.CopySource, SrcQueue: NriQueue.External, DstQueue: queue));
                    read.CopyBuffer(readback, 0, buffer, 0, Size);
                    read.End();
                    queue.Submit([read], [new NriFenceSubmit(fence, 2)], [new NriFenceSubmit(fence, 3)]);
                    GpuWork.Wait(fence, 3);
                    Assert.Equal(3UL, fence.Value);

                    var seenByNri = readback.Map().ToArray();
                    readback.Unmap();
                    Assert.True(seenByNri.All(b => b == CudaPattern), $"NRI read {Convert.ToHexString(seenByNri.AsSpan(0, 16))}... where CUDA wrote 0x{CudaPattern:X2}");
                    GpuWork.Record($"{backend}: CUDA saw NRI's 0x{NriPattern:X2}, NRI saw CUDA's 0x{CudaPattern:X2}, fence at {fence.Value}");
                }
                finally
                {
                    Cuda.cuStreamSynchronize(0);
                    Cuda.cuMemFree_v2(devicePointer);
                }
            }
            finally
            {
                Cuda.cuDestroyExternalSemaphore(externalSemaphore);
                Cuda.cuDestroyExternalMemory(externalMemory);
            }
            device.WaitIdle();
        }
        finally
        {
            Cuda.cuCtxDestroy_v2(context);
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// The texture path: NRI renders the triangle into a shared texture, releases it to the external
    /// queue family and signals; CUDA imports the memory as a mipmapped array (the image's format,
    /// size and colour-attachment flag restated in the array desc), waits, copies level 0 to the host
    /// and signals; NRI acquires the texture back on CUDA's signal. CUDA's copy is compared byte for
    /// byte with NRI's own readback of the render.
    /// </summary>
    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public unsafe void CUDA_reads_a_shared_texture_NRI_rendered_as_a_mipmapped_array(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        Assert.SkipUnless(OperatingSystem.IsWindows(), "the proof opens nvcuda.dll and passes NT handles; on Linux it would be libcuda.so.1 and file descriptors, not measured");
        Assert.SkipUnless(NativeLibrary.TryLoad("nvcuda.dll", out var nvcuda), "nvcuda.dll (the CUDA driver API, installed by the NVIDIA driver) is not present");
        NativeLibrary.Free(nvcuda);

        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        var adapter = device.Desc.adapterDesc;
        Assert.SkipWhen(adapter.vendor != Vendor.NVIDIA, $"{backend}: the adapter is {adapter.vendor}'s, not NVIDIA's (CUDA)");
        Assert.SkipWhen(device.Desc.features.sharedResources == 0 || device.Desc.features.sharedFences == 0, $"{backend}: features.sharedResources / sharedFences are off");

        Cuda.Check(Cuda.cuInit(0), "cuInit");
        var cudaDevice = Cuda.DeviceWithLuid(adapter.uid);
        Assert.SkipWhen(cudaDevice < 0, $"{backend}: no CUDA device has the adapter's LUID");
        nint context;
        Cuda.Check(Cuda.cuCtxCreate_v2(&context, 0, cudaDevice), "cuCtxCreate");
        try
        {
            const int Width = 64, Height = 64, Pitch = Width * 4;
            using var texture = device.CreateSharedTexture(new TextureDesc
            {
                type = TextureType.TEXTURE_2D, usage = TextureUsageBits.COLOR_ATTACHMENT | TextureUsageBits.SHADER_RESOURCE,
                format = Format.RGBA8_UNORM, width = Width, height = Height,
            });
            using var fence = device.CreateSharedFence();
            var queue = device.GetQueue(QueueType.GRAPHICS);

            // 1. NRI renders, releases the texture to the external family and signals 1.
            var expected = TriangleTests.RenderTriangle(device, texture);
            Assert.Equal(Width * Height * 4, expected.Length);
            GpuWork.Run(device, cb => cb.Barrier(new NriTextureBarrier(texture, GpuWork.TextureCopySource, GpuWork.TextureCopySource, SrcQueue: queue, DstQueue: NriQueue.External)));
            queue.Submit([], [], [new NriFenceSubmit(fence, 1)]);

            // 2. CUDA imports the memory as a mipmapped array and the fence, waits for 1, copies level 0 out, signals 2.
            using var textureHandle = texture.GetSharedHandle();
            using var fenceHandle = fence.GetSharedHandle();
            GpuWork.Record($"{backend}: importing {textureHandle} and {fenceHandle}");
            var memoryDesc = new Cuda.ExternalMemoryHandleDesc
            {
                type = textureHandle.Type switch
                {
                    SharedHandleType.OPAQUE_WIN32 => Cuda.MemoryHandleOpaqueWin32,
                    SharedHandleType.D3D12_RESOURCE => Cuda.MemoryHandleD3D12Resource,
                    _ => throw new InvalidOperationException($"unexpected handle type {textureHandle.Type}"),
                },
                handle = textureHandle.Handle,
                size = textureHandle.Size,
                flags = Cuda.MemoryDedicated, // a dedicated allocation on both backends
            };
            nint externalMemory;
            Cuda.Check(Cuda.cuImportExternalMemory(&externalMemory, &memoryDesc), "cuImportExternalMemory");
            var semaphoreDesc = new Cuda.ExternalSemaphoreHandleDesc
            {
                type = fenceHandle.Type == SharedHandleType.D3D12_FENCE ? Cuda.SemaphoreHandleD3D12Fence : Cuda.SemaphoreHandleTimelineWin32,
                handle = fenceHandle.Handle,
            };
            nint externalSemaphore;
            Cuda.Check(Cuda.cuImportExternalSemaphore(&externalSemaphore, &semaphoreDesc), "cuImportExternalSemaphore");
            try
            {
                var arrayDesc = new Cuda.ExternalMemoryMipmappedArrayDesc
                {
                    width = Width, height = Height, depth = 0, format = Cuda.ArrayFormatUnsignedInt8, numChannels = 4,
                    flags = Cuda.Array3DColorAttachment, // the image was created as a colour attachment
                    numLevels = 1,
                };
                nint mipmappedArray;
                Cuda.Check(Cuda.cuExternalMemoryGetMappedMipmappedArray(&mipmappedArray, externalMemory, &arrayDesc), "cuExternalMemoryGetMappedMipmappedArray");
                try
                {
                    nint level0;
                    Cuda.Check(Cuda.cuMipmappedArrayGetLevel(&level0, mipmappedArray, 0), "cuMipmappedArrayGetLevel");

                    var wait = new Cuda.ExternalSemaphoreWaitParams { value = 1 };
                    Cuda.Check(Cuda.cuWaitExternalSemaphoresAsync(&externalSemaphore, &wait, 1, 0), "cuWaitExternalSemaphoresAsync");

                    var seenByCuda = new byte[Width * Height * 4];
                    fixed (byte* p = seenByCuda)
                    {
                        var copy = new Cuda.Memcpy2D
                        {
                            srcMemoryType = Cuda.MemoryTypeArray, srcArray = level0,
                            dstMemoryType = Cuda.MemoryTypeHost, dstHost = p, dstPitch = Pitch,
                            widthInBytes = Pitch, height = Height,
                        };
                        Cuda.Check(Cuda.cuMemcpy2D_v2(&copy), "cuMemcpy2D");
                    }
                    var signal = new Cuda.ExternalSemaphoreSignalParams { value = 2 };
                    Cuda.Check(Cuda.cuSignalExternalSemaphoresAsync(&externalSemaphore, &signal, 1, 0), "cuSignalExternalSemaphoresAsync");

                    var differing = 0;
                    for (var i = 0; i < expected.Length; i++) if (expected[i] != seenByCuda[i]) differing++;
                    GpuWork.Record($"{backend}: CUDA's copy of level 0: {seenByCuda.Length} bytes compared with NRI's readback, {differing} differ; NRI sha256 {Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(expected))}, CUDA sha256 {Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(seenByCuda))}");
                    Assert.True(differing == 0, $"{differing} of {expected.Length} bytes differ between NRI's readback and CUDA's copy of the shared texture");

                    // 3. NRI acquires the texture back on CUDA's 2 and signals 3.
                    using var allocator = queue.CreateCommandAllocator();
                    using var acquire = allocator.CreateCommandBuffer();
                    acquire.Begin();
                    acquire.Barrier(new NriTextureBarrier(texture, GpuWork.TextureCopySource, GpuWork.TextureCopySource, SrcQueue: NriQueue.External, DstQueue: queue));
                    acquire.End();
                    queue.Submit([acquire], [new NriFenceSubmit(fence, 2)], [new NriFenceSubmit(fence, 3)]);
                    GpuWork.Wait(fence, 3);
                    Assert.Equal(3UL, fence.Value);
                }
                finally
                {
                    Cuda.cuStreamSynchronize(0);
                    Cuda.cuMipmappedArrayDestroy(mipmappedArray);
                }
            }
            finally
            {
                Cuda.cuDestroyExternalSemaphore(externalSemaphore);
                Cuda.cuDestroyExternalMemory(externalMemory);
            }
            device.WaitIdle();
        }
        finally
        {
            Cuda.cuCtxDestroy_v2(context);
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>The CUDA driver API, as far as the proof needs it. Layouts and values from cuda.h 12.9 (x64).</summary>
    private static unsafe class Cuda
    {
        private const string Library = "nvcuda";

        public const uint MemoryHandleOpaqueFd = 1, MemoryHandleOpaqueWin32 = 2, MemoryHandleD3D12Heap = 4, MemoryHandleD3D12Resource = 5;
        public const uint MemoryDedicated = 1;
        public const uint SemaphoreHandleOpaqueFd = 1, SemaphoreHandleOpaqueWin32 = 2, SemaphoreHandleD3D12Fence = 4, SemaphoreHandleTimelineFd = 9, SemaphoreHandleTimelineWin32 = 10;
        public const uint ArrayFormatUnsignedInt8 = 1, Array3DColorAttachment = 0x20, MemoryTypeHost = 1, MemoryTypeArray = 3;

        /// <summary>The CUDA device whose LUID is the adapter's (uid.low, high 0, on Windows), or -1.</summary>
        public static int DeviceWithLuid(Uid_t uid)
        {
            int deviceCount;
            Check(cuDeviceGetCount(&deviceCount), "cuDeviceGetCount");
            for (var i = 0; i < deviceCount; i++)
            {
                int candidate;
                Check(cuDeviceGet(&candidate, i), "cuDeviceGet");
                ulong luid;
                uint nodeMask;
                if (cuDeviceGetLuid((byte*)&luid, &nodeMask, candidate) == 0 && uid.high == 0 && luid == uid.low)
                    return candidate;
            }
            return -1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 120)]
        public struct ExternalMemoryMipmappedArrayDesc
        {
            [FieldOffset(0)] public ulong offset;
            [FieldOffset(8)] public nuint width;
            [FieldOffset(16)] public nuint height;
            [FieldOffset(24)] public nuint depth;
            [FieldOffset(32)] public uint format;
            [FieldOffset(36)] public uint numChannels;
            [FieldOffset(40)] public uint flags;
            [FieldOffset(48)] public uint numLevels;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        public struct Memcpy2D
        {
            [FieldOffset(0)] public nuint srcXInBytes;
            [FieldOffset(8)] public nuint srcY;
            [FieldOffset(16)] public uint srcMemoryType;
            [FieldOffset(24)] public void* srcHost;
            [FieldOffset(32)] public ulong srcDevice;
            [FieldOffset(40)] public nint srcArray;
            [FieldOffset(48)] public nuint srcPitch;
            [FieldOffset(56)] public nuint dstXInBytes;
            [FieldOffset(64)] public nuint dstY;
            [FieldOffset(72)] public uint dstMemoryType;
            [FieldOffset(80)] public void* dstHost;
            [FieldOffset(88)] public ulong dstDevice;
            [FieldOffset(96)] public nint dstArray;
            [FieldOffset(104)] public nuint dstPitch;
            [FieldOffset(112)] public nuint widthInBytes;
            [FieldOffset(120)] public nuint height;
        }

        [StructLayout(LayoutKind.Explicit, Size = 104)]
        public struct ExternalMemoryHandleDesc
        {
            [FieldOffset(0)] public uint type;
            [FieldOffset(8)] public int fd;
            [FieldOffset(8)] public nint handle;
            [FieldOffset(16)] public nint name;
            [FieldOffset(24)] public ulong size;
            [FieldOffset(32)] public uint flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 88)]
        public struct ExternalMemoryBufferDesc
        {
            [FieldOffset(0)] public ulong offset;
            [FieldOffset(8)] public ulong size;
            [FieldOffset(16)] public uint flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 96)]
        public struct ExternalSemaphoreHandleDesc
        {
            [FieldOffset(0)] public uint type;
            [FieldOffset(8)] public int fd;
            [FieldOffset(8)] public nint handle;
            [FieldOffset(16)] public nint name;
            [FieldOffset(24)] public uint flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 144)]
        public struct ExternalSemaphoreSignalParams
        {
            [FieldOffset(0)] public ulong value;
            [FieldOffset(72)] public uint flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 144)]
        public struct ExternalSemaphoreWaitParams
        {
            [FieldOffset(0)] public ulong value;
            [FieldOffset(72)] public uint flags;
        }

        public static void Check(int result, string call)
        {
            if (result == 0) return;
            byte* text = null;
            cuGetErrorName(result, &text);
            throw new InvalidOperationException($"{call}: CUDA error {result} ({(text is null ? "?" : Marshal.PtrToStringUTF8((nint)text))})");
        }

        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuInit(uint flags);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuDeviceGetCount(int* count);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuDeviceGet(int* device, int ordinal);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuDeviceGetLuid(byte* luid, uint* deviceNodeMask, int device);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuDeviceGetName(byte* name, int length, int device);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuCtxCreate_v2(nint* context, uint flags, int device);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuCtxDestroy_v2(nint context);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuImportExternalMemory(nint* externalMemory, ExternalMemoryHandleDesc* desc);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuExternalMemoryGetMappedBuffer(ulong* devicePointer, nint externalMemory, ExternalMemoryBufferDesc* desc);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuDestroyExternalMemory(nint externalMemory);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuImportExternalSemaphore(nint* externalSemaphore, ExternalSemaphoreHandleDesc* desc);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuDestroyExternalSemaphore(nint externalSemaphore);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuSignalExternalSemaphoresAsync(nint* semaphores, ExternalSemaphoreSignalParams* parameters, uint count, nint stream);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuWaitExternalSemaphoresAsync(nint* semaphores, ExternalSemaphoreWaitParams* parameters, uint count, nint stream);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuMemcpyHtoDAsync_v2(ulong destination, void* source, nuint bytes, nint stream);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuMemcpyDtoHAsync_v2(void* destination, ulong source, nuint bytes, nint stream);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuStreamSynchronize(nint stream);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuMemFree_v2(ulong devicePointer);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuGetErrorName(int error, byte** name);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuExternalMemoryGetMappedMipmappedArray(nint* mipmappedArray, nint externalMemory, ExternalMemoryMipmappedArrayDesc* desc);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuMipmappedArrayGetLevel(nint* level, nint mipmappedArray, uint levelIndex);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuMipmappedArrayDestroy(nint mipmappedArray);
        [DllImport(Library, CallingConvention = CallingConvention.StdCall)] public static extern int cuMemcpy2D_v2(Memcpy2D* copy);
    }
}
