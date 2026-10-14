// The LowLatency extension past acquisition (SwapChainExtensionTests checks that): a low-latency
// swap chain on a hidden Win32 window, 200 frames marked, slept and presented, and the latency
// report read back. NVIDIA Reflex through NVAPI on D3D12, VK_NV_low_latency2 on Vulkan.

using System.Runtime.InteropServices;
using PrismNRI.Native;
using Xunit;
using static PrismNRI.Tests.GpuWork;

namespace PrismNRI.Tests;

public sealed partial class LowLatencyExtensionTests
{
    // Both backends report from a 64-frame history and read its last slot (NvAPI_D3D_GetLatency,
    // vkGetLatencyTimingsNV with 64 entries). Measured on D3D12: after 72 frames it is still zero,
    // after 200 it is filled.
    private const int FrameNum = 200;

    [Theory]
    [MemberData(nameof(TestDevice.GpuBackends), MemberType = typeof(TestDevice))]
    public void Frames_sleep_mark_and_report_on_a_low_latency_swap_chain(Backend backend)
    {
        TestDevice.RequireAvailable(backend);
        Assert.SkipUnless(OperatingSystem.IsWindows(), "the test window is a Win32 window; no other window system here");
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(backend.Api, messages);
        Assert.SkipWhen(device.Desc.features.lowLatency == 0, $"{backend}: features.lowLatency is false (no NVIDIA Reflex through NVAPI on D3D12, no VK_NV_low_latency2 on Vulkan)");
        Assert.SkipWhen(device.Desc.features.swapChain == 0, $"{backend}: features.swapChain is false");
        // Vulkan presents nowhere else in this process, and here it cost two things on the
        // development machine (RTX 4070 SUPER, Windows 11): RivaTuner Statistics Server's implicit
        // VK_LAYER_RTSS killed the process at the first vkQueuePresentKHR; with implicit layers
        // disabled the Vulkan case passed and reported every marker, but afterwards the next
        // D3D12CreateDevice calls in the process failed with DXGI_ERROR_DEVICE_RESET (0x887A0007),
        // failing unrelated D3D12 tests, with or without ALLOW_LOW_LATENCY and VSYNC. Until that is
        // understood the Vulkan case does not run in the shared test process.
        Assert.SkipWhen(backend.Api == GraphicsAPI.VK,
            "VK: presenting from Vulkan in the test process makes later D3D12CreateDevice calls fail with DXGI_ERROR_DEVICE_RESET (measured, CLAUDE.md: Extensions)");

        var hwnd = CreateWindowExW(0, "STATIC", "PrismNRI low latency", WS_POPUP, 0, 0, 64, 64, 0, 0, 0, 0);
        Assert.SkipWhen(hwnd == 0, $"no desktop to create a window on (CreateWindowEx error {Marshal.GetLastPInvokeError()})");
        try
        {
            Run(device, hwnd);
        }
        finally
        {
            DestroyWindow(hwnd);
        }
        Assert.DoesNotContain(messages, m => m.IsError);
    }

    private static void Run(NriDevice device, nint hwnd)
    {
        var ext = device.SwapChain;
        var lowLatency = device.LowLatency;
        var queue = device.GetQueue(QueueType.GRAPHICS);

        using var swapChain = ext.CreateSwapChain(new NriSwapChainDesc
        {
            Window = new NriWindow { Windows = new NriWindowsWindow(Hwnd: hwnd) },
            Queue = queue,
            Width = 64,
            Height = 64,
            TextureNum = 2,
            Format = SwapChainFormat.BT709_G22_8BIT,
            Flags = SwapChainBits.ALLOW_LOW_LATENCY,
        });
        var textures = swapChain.Textures;
        var acquire = textures.Select(_ => device.CreateFence(NriSwapChainExtension.SwapChainSemaphore)).ToArray();
        var release = textures.Select(_ => device.CreateFence(NriSwapChainExtension.SwapChainSemaphore)).ToArray();
        using var frameFence = device.CreateFence();
        using var allocator = queue.CreateCommandAllocator();
        using var commandBuffer = allocator.CreateCommandBuffer();
        try
        {
            lowLatency.SetLatencySleepMode(swapChain, minIntervalUs: 0, lowLatencyMode: true, lowLatencyBoost: false);

            var present = new AccessLayoutStage { access = AccessBits.NONE, layout = Layout.PRESENT, stages = StageBits.NONE };
            for (var frame = 0; frame < FrameNum; frame++)
            {
                var presentId = (ulong)frame + 1;
                GpuWork.Wait(frameFence, (ulong)frame);           // one frame in flight
                lowLatency.LatencySleep(swapChain, presentId);
                lowLatency.SetLatencyMarker(swapChain, presentId, LatencyMarker.INPUT_SAMPLE);
                lowLatency.SetLatencyMarker(swapChain, presentId, LatencyMarker.SIMULATION_START);
                lowLatency.SetLatencyMarker(swapChain, presentId, LatencyMarker.SIMULATION_END);
                lowLatency.SetLatencyMarker(swapChain, presentId, LatencyMarker.RENDER_SUBMIT_START);

                var acquired = acquire[frame % acquire.Length];
                var index = ext.AcquireNextTexture(swapChain, acquired);
                allocator.Reset();
                commandBuffer.Begin();
                commandBuffer.Barrier(new NriTextureBarrier(textures[(int)index], Undefined, present));
                commandBuffer.End();
                queue.Submit([commandBuffer], [new NriFenceSubmit(acquired, 0, StageBits.ALL)],
                    [new NriFenceSubmit(release[index], 0), new NriFenceSubmit(frameFence, presentId)], swapChain, presentId);

                lowLatency.SetLatencyMarker(swapChain, presentId, LatencyMarker.RENDER_SUBMIT_END);
                ext.QueuePresent(swapChain, release[index], presentId);
            }
            GpuWork.Wait(frameFence, FrameNum);
            device.WaitIdle();

            Thread.Sleep(100); // the driver fills the history after the GPU is done
            var report = lowLatency.GetLatencyReport(swapChain);
            ulong[] stamps =
            [
                report.inputSampleTimeUs, report.simulationStartTimeUs, report.simulationEndTimeUs,
                report.renderSubmitStartTimeUs, report.renderSubmitEndTimeUs, report.presentStartTimeUs, report.presentEndTimeUs,
            ];
            Record($"{device.GraphicsApi}: latency report (us) input {stamps[0]} sim {stamps[1]}..{stamps[2]} submit {stamps[3]}..{stamps[4]} present {stamps[5]}..{stamps[6]} "
                + $"driver {report.driverStartTimeUs}..{report.driverEndTimeUs} os queue {report.osRenderQueueStartTimeUs}..{report.osRenderQueueEndTimeUs} gpu {report.gpuRenderStartTimeUs}..{report.gpuRenderEndTimeUs}");

            // The report describes one recent frame: every marker the test set has a time, and the
            // times follow the order of the frame: simulation, submit, present. INPUT_SAMPLE is
            // recorded, not checked: NvAPI reported 0 for it in one run and a time in the others.
            Assert.All(stamps[1..7], t => Assert.NotEqual(0UL, t));
            for (var i = 2; i < 7; i++) Assert.True(stamps[i] >= stamps[i - 1], $"marker {i} at {stamps[i]} us precedes marker {i - 1} at {stamps[i - 1]} us");
        }
        finally
        {
            foreach (var f in acquire) f.Dispose();
            foreach (var f in release) f.Dispose();
        }
    }

    private const uint WS_POPUP = 0x80000000; // never shown

    [LibraryImport("user32", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint CreateWindowExW(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint hwnd);
}
