// The SwapChain and LowLatency extensions. No test can create a window in CI, so on the GPU
// backends only acquisition is checked (against features.swapChain and features.lowLatency), with
// NONE's answer recorded alongside as part of the availability table. The NONE backend creates a
// swap chain for any non-null window handle, which is used for one thing it can measure: the
// lifetime of the swap chain's non-owning textures and the disposal order. NONE is not validated
// (AGENTS.md: Extensions), so nothing there says the calls themselves are legal.

using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed class SwapChainExtensionTests
{
    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void SwapChain_acquisition_follows_features_swapChain(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var expected = backend.Api == GraphicsAPI.NONE || device.Desc.features.swapChain != 0;
        GpuWork.Record($"{backend}: features.swapChain={device.Desc.features.swapChain}, acquired={device.TrySwapChain is not null}");
        Assert.Equal(expected, device.TrySwapChain is not null);
        if (expected) Assert.Same(device.SwapChain, device.TrySwapChain);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    [Theory]
    [MemberData(nameof(TestDevice.AllBackends), MemberType = typeof(TestDevice))]
    public void LowLatency_acquisition_follows_features_lowLatency(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);

        var acquired = device.TryLowLatency is not null;
        GpuWork.Record($"{backend}: features.lowLatency={device.Desc.features.lowLatency}, acquired={acquired}");
        Assert.Equal(device.Desc.features.lowLatency != 0, acquired);
        if (!acquired) Assert.Throws<NriUnsupportedException>(() => device.LowLatency);
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// Object lifetime, which is what NONE is for: the swap chain's textures are not the caller's,
    /// are not counted as children, and die with the swap chain. The frame in the middle only puts
    /// the textures through acquire and present; NONE validates none of it.
    /// </summary>
    [Fact]
    public void A_swap_chain_on_NONE_runs_a_frame_and_owns_its_textures()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "NRI reads the Windows member of Window only on Windows");
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.NONE, messages);
        var ext = device.SwapChain;
        var queue = device.GetQueue(QueueType.GRAPHICS);

        using var acquire = device.CreateFence(NriSwapChainExtension.SwapChainSemaphore);
        using var release = device.CreateFence(NriSwapChainExtension.SwapChainSemaphore);

        IReadOnlyList<NriTexture> textures;
        using (var swapChain = ext.CreateSwapChain(new NriSwapChainDesc
        {
            Window = new NriWindow { Windows = new NriWindowsWindow(Hwnd: 0x1234) }, // NONE never touches it
            Queue = queue,
            Width = 64,
            Height = 64,
            TextureNum = 2,
            Format = SwapChainFormat.BT709_G22_8BIT,
            Flags = SwapChainBits.VSYNC | SwapChainBits.WAITABLE,
        }))
        {
            Assert.Same(queue, swapChain.Queue);
            textures = ext.GetSwapChainTextures(swapChain);
            Assert.NotEmpty(textures);
            Assert.Same(swapChain.Textures, textures);
            Assert.All(textures, t => Assert.False(t.IsOwned));
            Assert.Equal(3, device.LiveObjects); // two fences and the swap chain; its textures are not children

            textures[0].Dispose(); // a no-op: the swap chain owns it
            Assert.Equal(3, device.LiveObjects);

            Assert.Equal(NriSwapChainStatus.Ok, ext.AcquireNextTexture(swapChain, acquire, out var index));
            Assert.InRange(index, 0u, (uint)textures.Count - 1);
            Assert.Equal(NriSwapChainStatus.Ok, ext.QueuePresent(swapChain, release, presentId: 1));
            ext.WaitForPresent(swapChain, 1);
            _ = ext.GetDisplayDesc(swapChain);
        }

        Assert.All(textures, t => Assert.True(t.IsDisposed));
        Assert.Equal(2, device.LiveObjects);
        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
