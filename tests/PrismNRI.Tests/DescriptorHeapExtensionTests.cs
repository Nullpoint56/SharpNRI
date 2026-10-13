// The DescriptorHeap extension: acquisition follows features.descriptorHeap, and where it is
// available a heap is created, written from the host with a texture view and a sampler, bound in a
// command buffer that is submitted, and destroyed; validation watches all of it.

using PrismNRI.Native;
using Xunit;

namespace PrismNRI.Tests;

public sealed class DescriptorHeapExtensionTests
{
    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void Acquisition_follows_features_descriptorHeap(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var expected = backend.Api == GraphicsAPI.NONE || device.Desc.features.descriptorHeap != 0;
        GpuWork.Record($"{backend}: features.descriptorHeap={device.Desc.features.descriptorHeap}, acquired={device.TryDescriptorHeap is not null}");
        Assert.Equal(expected, device.TryDescriptorHeap is not null);
        if (!expected) Assert.Throws<NriUnsupportedException>(() => device.DescriptorHeap);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void A_heap_is_created_written_bound_and_destroyed(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.TryDescriptorHeap is null, $"{backend}: no descriptor heaps on this adapter");
        var heaps = device.DescriptorHeap;

        using var texture = device.CreateCommittedTexture(MemoryLocation.DEVICE, new TextureDesc
        {
            type = TextureType.TEXTURE_2D, usage = TextureUsageBits.SHADER_RESOURCE, format = Format.RGBA8_UNORM,
            width = 4, height = 4, depth = 1, mipNum = 1, layerNum = 1, sampleNum = 1,
        });
        using var view = texture.CreateView(TextureView.TEXTURE, Format.RGBA8_UNORM);
        using var sampler = device.CreateSampler(new SamplerDesc());

        using (var heap = heaps.CreateDescriptorHeap(16, 4))
        {
            Assert.Equal(16u, heap.Desc.resourceDescriptorNum);
            Assert.Equal(4u, heap.Desc.samplerDescriptorNum);
            Assert.Equal(4, device.LiveObjects);

            heaps.WriteResourceDescriptors(heap, [new NriWriteResourceDescriptorsDesc(view, 0), new NriWriteResourceDescriptorsDesc(view, 15)]);
            heaps.WriteSamplerDescriptors(heap, [new NriWriteSamplerDescriptorsDesc(sampler, 3)]);

            GpuWork.Run(device, cb => heaps.SetDescriptorHeap(cb, heap));
        }

        Assert.Equal(3, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
