// Presenting from an NRI-created Vulkan device under RivaTuner Statistics Server's implicit layer.
// VK_LAYER_RTSS maps each VkPhysicalDevice to its instance from vkEnumeratePhysicalDevices and uses
// that map when its overlay initialises at the first vkQueuePresentKHR. NRI used to find physical
// devices through vkEnumeratePhysicalDeviceGroups only (a leftover of the mGPU support dropped in
// v1.124), so the layer had no entry for NRI's device and the process died with 0xC0000409 inside
// the first present, with or without any validation (RTSS 7.3.5; AGENTS.md: Device creation and
// callbacks). Patch 0045 makes NRI enumerate physical devices; the binding makes no call of its own.
//
// The frames run in a child process (this test executable started again with SHARPNRI_TEST_CHILD),
// so a crash fails this test instead of ending the run. The layer is only active while RTSS runs,
// so the test skips unless the loader reports VK_LAYER_RTSS and an RTSS process is running.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed partial class OverlayLayerTests(ITestOutputHelper output)
{
    private const string ChildVariable = "SHARPNRI_TEST_CHILD";
    private const string ChildName = "vk-present-rtss";
    private const int Frames = 8;

    [Fact]
    public async Task An_NRI_Vulkan_device_presents_under_the_RivaTuner_Statistics_Server_layer()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "RivaTuner Statistics Server is Windows only");
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        Assert.SkipUnless(LoaderReportsLayer("VK_LAYER_RTSS"), "the Vulkan loader reports no VK_LAYER_RTSS (RivaTuner Statistics Server not installed, or DISABLE_RTSS_LAYER set)");
        Assert.SkipWhen(Process.GetProcessesByName("RTSS").Length == 0, "RivaTuner Statistics Server (RTSS.exe) is not running, so its layer hooks nothing");

        var self = Environment.ProcessPath!;
        var start = new ProcessStartInfo(self) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location); // "dotnet SharpNRI.Tests.dll"
        start.Environment[ChildVariable] = ChildName;

        using var child = Process.Start(start)!;
        var stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        if (!child.WaitForExit(GpuWork.GpuTimeout))
        {
            child.Kill(entireProcessTree: true);
            Assert.Fail($"the child did not finish within {GpuWork.GpuTimeout}");
        }
        var lines = (await stdout).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines) output.WriteLine(line);
        var errors = await stderr;
        if (errors.Length != 0) output.WriteLine("stderr: " + errors);

        var skip = lines.FirstOrDefault(l => l.StartsWith("SKIP ", StringComparison.Ordinal));
        Assert.SkipWhen(skip is not null && child.ExitCode == 0, skip?[5..] ?? "");
        var presented = lines.Count(l => l.StartsWith("PRESENTED ", StringComparison.Ordinal));
        Assert.True(child.ExitCode == 0,
            $"child exit code 0x{child.ExitCode:X8} after {presented} of {Frames} presents: " + string.Join(" | ", lines.Where(l => l.StartsWith("FAIL ", StringComparison.Ordinal))));
        Assert.Equal(Frames, presented);
        GpuWork.Record($"VK under VK_LAYER_RTSS: {presented} frames presented");
    }

    // ================================================================================= the child

    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "the child mode must run before xunit's entry point")]
    internal static void RunChildIfAsked()
    {
        if (Environment.GetEnvironmentVariable(ChildVariable) != ChildName) return;
        int code;
        try
        {
            code = Child();
        }
        catch (Exception e)
        {
            Console.WriteLine("FAIL " + e.ToString().ReplaceLineEndings(" / "));
            code = 1;
        }
        Console.Out.Flush();
        Environment.Exit(code);
    }

    private static int Child()
    {
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.VK, messages);
        if (device.Desc.features.swapChain == 0)
        {
            Console.WriteLine("SKIP VK: features.swapChain is false");
            return 0;
        }

        var hwnd = CreateWindowExW(0, "STATIC", "SharpNRI overlay layer", WS_POPUP, 0, 0, 64, 64, 0, 0, 0, 0);
        if (hwnd == 0) throw new InvalidOperationException($"CreateWindowEx failed with error {Marshal.GetLastPInvokeError()}");
        try
        {
            var queue = device.GetQueue(QueueType.GRAPHICS);
            using var swapChain = device.SwapChain.CreateSwapChain(new NriSwapChainDesc
            {
                Window = new NriWindow { Windows = new NriWindowsWindow(Hwnd: hwnd) },
                Queue = queue,
                Width = 64,
                Height = 64,
                TextureNum = 2,
                Format = SwapChainFormat.BT709_G22_8BIT,
            });
            using var acquire = device.CreateFence(NriSwapChainExtension.SwapChainSemaphore);
            var release = swapChain.Textures.Select(_ => device.CreateFence(NriSwapChainExtension.SwapChainSemaphore)).ToArray();
            using var allocator = queue.CreateCommandAllocator();
            using var commandBuffer = allocator.CreateCommandBuffer();
            var present = new AccessLayoutStage { access = AccessBits.NONE, layout = Layout.PRESENT, stages = StageBits.NONE };
            try
            {
                for (var frame = 0; frame < Frames; frame++)
                {
                    if (device.SwapChain.AcquireNextTexture(swapChain, acquire, out var index) == NriSwapChainStatus.OutOfDate)
                        throw new InvalidOperationException("AcquireNextTexture: OUT_OF_DATE on a window that never changes");
                    allocator.Reset();
                    commandBuffer.Begin();
                    commandBuffer.Barrier(new NriTextureBarrier(swapChain.Textures[(int)index], GpuWork.Undefined, present));
                    commandBuffer.End();
                    queue.Submit([commandBuffer], [new NriFenceSubmit(acquire, 0)], [new NriFenceSubmit(release[index], 0)]);
                    if (device.SwapChain.QueuePresent(swapChain, release[index]) == NriSwapChainStatus.OutOfDate)
                        throw new InvalidOperationException("QueuePresent: OUT_OF_DATE on a window that never changes");
                    Console.WriteLine($"PRESENTED {frame}");
                    Console.Out.Flush(); // the record that survives a crash in the next present
                    device.WaitIdle();
                }
                device.WaitIdle();
            }
            finally
            {
                foreach (var f in release) f.Dispose();
            }
        }
        finally
        {
            DestroyWindow(hwnd);
        }

        var ok = true;
        foreach (var m in messages)
        {
            Console.WriteLine($"{(m.IsError ? "FAIL " : "")}message {m}");
            ok &= !m.IsError;
        }
        return ok ? 0 : 1;
    }

    /// <summary>The loader's instance layers, implicit ones included, through vulkan-1.dll.</summary>
    private static unsafe bool LoaderReportsLayer(string name)
    {
        if (!NativeLibrary.TryLoad("vulkan-1.dll", out var loader)) return false;
        try
        {
            if (!NativeLibrary.TryGetExport(loader, "vkEnumerateInstanceLayerProperties", out var export)) return false;
            var enumerate = (delegate* unmanaged[Stdcall]<uint*, VkLayerProperties*, int>)export;
            uint count = 0;
            if (enumerate(&count, null) < 0 || count == 0) return false;
            var layers = new VkLayerProperties[count];
            fixed (VkLayerProperties* p = layers)
            {
                if (enumerate(&count, p) < 0) return false;
                for (var i = 0; i < count; i++)
                    if (Marshal.PtrToStringUTF8((nint)p[i].LayerName) == name) return true;
            }
            return false;
        }
        finally
        {
            NativeLibrary.Free(loader);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct VkLayerProperties
    {
        public fixed byte LayerName[256]; // VK_MAX_EXTENSION_NAME_SIZE
        public uint SpecVersion;
        public uint ImplementationVersion;
        public fixed byte Description[256]; // VK_MAX_DESCRIPTION_SIZE
    }

    private const uint WS_POPUP = 0x80000000; // never shown

    [LibraryImport("user32", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint CreateWindowExW(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint hwnd);
}
