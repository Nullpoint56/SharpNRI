// NriBuffer: committed creation in every memory location, map/write/unmap, a GPU copy read back
// through a HOST_READBACK buffer, the desc NRI keeps, memory requirements, views, device address
// and the live-object count.

using System.Numerics;
using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

public sealed class BufferTests
{
    public static TheoryData<Backend, MemoryLocation> GpuBackendsAndLocations
    {
        get
        {
            var data = new TheoryData<Backend, MemoryLocation>();
            foreach (var api in new[] { GraphicsAPI.VK, GraphicsAPI.D3D12 })
            foreach (var location in new[] { MemoryLocation.DEVICE, MemoryLocation.DEVICE_UPLOAD, MemoryLocation.HOST_UPLOAD, MemoryLocation.HOST_READBACK })
            {
                data.Add(new Backend(api), location);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(GpuBackendsAndLocations))]
    public void Committed_buffer_is_created_in_every_memory_location(Backend backend, MemoryLocation location)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var usage = location == MemoryLocation.DEVICE ? BufferUsageBits.SHADER_RESOURCE : BufferUsageBits.NONE;
        using var buffer = device.CreateCommittedBuffer(location, new BufferDesc { size = 1024, usage = usage });

        Assert.Equal(1024UL, buffer.Desc.size);
        Assert.Equal(1, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Desc_round_trips_size_usage_and_stride(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var desc = new BufferDesc
        {
            size = 1000,
            structureStride = 8,
            usage = BufferUsageBits.SHADER_RESOURCE | BufferUsageBits.SHADER_RESOURCE_STORAGE | BufferUsageBits.VERTEX,
            byteAddress = 1,
        };
        using var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, desc);

        var held = buffer.Desc;

        // Measured: VK and D3D12 keep the desc exactly as given; the size is not rounded up to
        // any alignment (that is GetMemoryDesc's job).
        Assert.Equal(desc.size, held.size);
        Assert.Equal(desc.usage, held.usage);
        Assert.Equal(desc.structureStride, held.structureStride);
        Assert.Equal(desc.byteAddress, held.byteAddress);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Records NONE's dummy behaviour, not a feature: NONE answers GetBufferDesc with one static
    /// "{ size = 1 }" for every buffer, everything else zero (ImplNONE.cpp; docs/findings.md 7.3).
    /// NONE has no validation layer, so there are no messages to check.
    /// </summary>
    [Fact]
    public void NONE_answers_one_static_desc_for_every_buffer()
    {
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.NONE, messages);
        using var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc
        {
            size = 1000,
            structureStride = 8,
            usage = BufferUsageBits.SHADER_RESOURCE | BufferUsageBits.SHADER_RESOURCE_STORAGE | BufferUsageBits.VERTEX,
            byteAddress = 1,
        });

        Assert.Equal(new BufferDesc { size = 1 }, buffer.Desc);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Map_write_unmap_then_copy_to_a_readback_buffer_round_trips(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        const int size = 256;
        using var upload = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = size });
        using var readback = device.CreateCommittedBuffer(MemoryLocation.HOST_READBACK, new BufferDesc { size = size });

        var expected = new byte[size];
        for (var i = 0; i < size; i++) expected[i] = (byte)(i * 7 + 3);

        var mapped = upload.Map();
        Assert.Equal(size, mapped.Length);
        expected.CopyTo(mapped);
        upload.Unmap();

        // No barrier to the host after the copy: NRI validation rejects HOST_READ in a buffer
        // barrier, and the fence wait in Run is what makes the copy visible to the host.
        Run(device, cmd => cmd.CopyBuffer(readback, 0, upload, 0, size));

        var actual = readback.Map().ToArray();
        readback.Unmap();
        Assert.Equal(expected, actual);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Map_of_a_sub_range_has_the_requested_length(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        using var upload = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 512 });

        var whole = upload.Map();
        whole.Fill(0);
        upload.Unmap();

        var part = upload.Map(128, 64);
        Assert.Equal(64, part.Length);
        part.Fill(0xAB);
        upload.Unmap();

        var check = upload.Map();
        Assert.Equal(0, check[127]);
        Assert.Equal(0xAB, check[128]);
        Assert.Equal(0xAB, check[191]);
        Assert.Equal(0, check[192]);
        upload.Unmap();
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void GetMemoryDesc_reports_a_size_and_an_alignment(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        using var buffer = device.CreateBuffer(new BufferDesc { size = 1000, usage = BufferUsageBits.SHADER_RESOURCE });

        // Measured for 1000 bytes: VK size 1008, alignment 16; D3D12 size 65536, alignment 65536
        // on the inbox runtime (ID3D12Device14) and size 1024, alignment 128 with the Agility SDK
        // 1.619 (ID3D12Device15, tight buffer alignment). Hence only ">= size" and "power of two".
        var memory = buffer.GetMemoryDesc(MemoryLocation.DEVICE);
        Record($"{backend}: buffer of 1000 bytes needs size={memory.size} alignment={memory.alignment} type=0x{memory.type:X} dedicated={memory.mustBeDedicated}");

        Assert.True(memory.size >= 1000, $"size {memory.size}");
        Assert.NotEqual(0u, memory.alignment);
        Assert.True(BitOperations.IsPow2(memory.alignment), $"alignment {memory.alignment}");
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Every_buffer_view_type_the_usage_allows_is_created(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        using var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc
        {
            size = 4096,
            structureStride = 16,
            usage = BufferUsageBits.SHADER_RESOURCE | BufferUsageBits.SHADER_RESOURCE_STORAGE | BufferUsageBits.CONSTANT,
            byteAddress = 1,
        });

        using var typed = buffer.CreateView(BufferView.BUFFER, format: Format.R32_UINT);
        using var structured = buffer.CreateView(BufferView.STRUCTURED_BUFFER, structureStride: 16);
        using var byteAddress = buffer.CreateView(BufferView.BYTE_ADDRESS_BUFFER);
        using var storageTyped = buffer.CreateView(BufferView.STORAGE_BUFFER, format: Format.R32_UINT);
        using var storageStructured = buffer.CreateView(BufferView.STORAGE_STRUCTURED_BUFFER, structureStride: 16);
        using var storageByteAddress = buffer.CreateView(BufferView.STORAGE_BYTE_ADDRESS_BUFFER);
        using var constant = buffer.CreateView(BufferView.CONSTANT_BUFFER, 0, 256);

        NriDescriptor[] views = [typed, structured, byteAddress, storageTyped, storageStructured, storageByteAddress, constant];
        Assert.All(views, v => Assert.Equal(NriDescriptorKind.BufferView, v.Kind));
        Assert.Equal(1 + views.Length, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void DeviceAddress_is_non_zero_on_GPU_backends(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        using var buffer = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 256, usage = BufferUsageBits.SHADER_RESOURCE_STORAGE });

        // NRI.h: "Device address (aka GPU virtual address or 0 if unsupported)". DeviceDesc has no
        // feature bit for it, so "0 if unsupported" is the only signal; both real backends support
        // it on this hardware (VK 1.2 bufferDeviceAddress, D3D12 GetGPUVirtualAddress).
        var address = buffer.DeviceAddress;
        Record($"{backend}: device address 0x{address:X}");
        Assert.NotEqual(0UL, address);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Disposal_is_counted_per_buffer_and_view(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var a = device.CreateCommittedBuffer(MemoryLocation.DEVICE, new BufferDesc { size = 256, usage = BufferUsageBits.CONSTANT });
        var b = device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 256 });
        var view = a.CreateView(BufferView.CONSTANT_BUFFER, 0, 256);
        Assert.Equal(3, device.LiveObjects);

        view.Dispose();
        Assert.Equal(2, device.LiveObjects);
        b.Dispose();
        b.Dispose();
        Assert.Equal(1, device.LiveObjects);
        Assert.Throws<ObjectDisposedException>(() => b.Desc.size);
        a.Dispose();
        Assert.Equal(0, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
