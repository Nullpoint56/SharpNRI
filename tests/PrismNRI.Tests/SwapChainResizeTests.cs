// OUT_OF_DATE for real: a Vulkan swap chain on a Win32 window that is resized between frames, so
// that vkAcquireNextImage2KHR or vkQueuePresentKHR can answer VK_ERROR_OUT_OF_DATE_KHR. The binding
// returns it as a value (AcquireNextTexture / QueuePresent return NriSwapChainStatus.OutOfDate) and patch 0009 makes
// NRI return it without an ERROR message; this measures both on whatever the driver does.
//
// Two devices, because NRI itself avoids OUT_OF_DATE where it can: on a device with
// VK_KHR/EXT_swapchain_maintenance1 (features.resizableSwapChain) it creates the swap chain with
// present scaling, and the driver then keeps presenting through a resize. The second device is a
// raw VkDevice with VK_KHR_swapchain and nothing else, wrapped into NRI, so the swap chain has no
// scaling and a resize leaves it out of date.
//
// Presenting from Vulkan runs in a child process (this test executable, started again with
// PRISMNRI_TEST_CHILD set; the module initializer below runs the frames and exits before xunit
// starts). Two measured reasons (CLAUDE.md: Extensions, Reflex): on the development machine
// RivaTuner's implicit VK_LAYER_RTSS killed the process at the first vkQueuePresentKHR, so the
// child runs with implicit layers disabled (VK_LOADER_LAYERS_DISABLE=~implicit~; the explicit
// Khronos validation layer stays on), and presenting from Vulkan made later D3D12CreateDevice calls
// in the same process fail with DXGI_ERROR_DEVICE_RESET, which a separate process keeps away from
// the other tests.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PrismNRI.Native;
using Xunit;

namespace PrismNRI.Tests;

public sealed partial class SwapChainResizeTests(ITestOutputHelper output)
{
    private const string ChildVariable = "PRISMNRI_TEST_CHILD";
    private const string ChildName = "swapchain-resize";

    [Fact]
    public void Resizing_the_window_under_a_Vulkan_swap_chain_returns_OUT_OF_DATE_as_a_value_without_an_error()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "the test window is a Win32 window; no other window system here");
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));

        var self = Environment.ProcessPath!;
        var start = new ProcessStartInfo(self) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location); // "dotnet PrismNRI.Tests.dll"
        start.Environment[ChildVariable] = ChildName;
        start.Environment["VK_LOADER_LAYERS_DISABLE"] = "~implicit~";

        using var child = Process.Start(start)!;
        var stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        if (!child.WaitForExit(GpuWork.GpuTimeout))
        {
            child.Kill(entireProcessTree: true);
            Assert.Fail($"the child did not finish within {GpuWork.GpuTimeout}");
        }
        var lines = stdout.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines) output.WriteLine(line);
        if (stderr.Result.Length != 0) output.WriteLine("stderr: " + stderr.Result);

        var skip = lines.FirstOrDefault(l => l.StartsWith("SKIP ", StringComparison.Ordinal));
        Assert.SkipWhen(skip is not null && child.ExitCode == 0, skip?[5..] ?? "");
        // The child fails itself on an exception (any result other than SUCCESS and OUT_OF_DATE
        // throws) and on any ERROR message from NRI or the validation layer.
        Assert.True(child.ExitCode == 0, $"child exit code {child.ExitCode}: " + string.Join(" | ", lines.Where(l => l.StartsWith("FAIL ", StringComparison.Ordinal))));
        foreach (var result in lines.Where(l => l.StartsWith("RESULT ", StringComparison.Ordinal))) GpuWork.Record(result);
        Assert.Equal(2, lines.Count(l => l.StartsWith("RESULT ", StringComparison.Ordinal)));
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

    // Client sizes the window goes through, a few frames each; 0x0 is a window with no client area.
    private static readonly (int Width, int Height)[] Sizes = [(160, 96), (64, 64), (0, 0), (200, 120), (48, 80), (0, 0), (128, 128), (96, 160)];
    private const int FramesPerSize = 4;

    private static int Child()
    {
        var failed = false;

        // 1. NRI's own device: present scaling where the device has swapchain_maintenance1.
        var messages = new List<NriMessage>();
        using (var device = TestDevice.Create(GraphicsAPI.VK, messages))
        {
            if (device.Desc.features.swapChain == 0)
            {
                Console.WriteLine("SKIP VK: features.swapChain is false");
                return 0;
            }
            if (!RunFrames(device, "NRI-created VK device", messages, [])) failed = true;
        }

        // 2. A raw VkDevice with VK_KHR_swapchain only, wrapped: no present scaling.
        string[] instanceExtensions = ["VK_KHR_surface", "VK_KHR_win32_surface", "VK_KHR_get_surface_capabilities2"]; // NRI's swap chain calls vkGetPhysicalDeviceSurfaceCapabilities2KHR unconditionally
        string[] deviceExtensions = ["VK_KHR_swapchain"];
        using var raw = WrapperExtensionTests.RawVulkanDevice.Create(TestDevice.AdapterFor(GraphicsAPI.VK)!, instanceExtensions, deviceExtensions);
        var wrappedMessages = new List<NriMessage>();
        using (var wrapped = NriDevice.CreateFromVulkan(new NriVulkanDeviceOptions
        {
            VkInstance = raw.Instance.Handle,
            VkDevice = raw.Device.Handle,
            VkPhysicalDevice = raw.PhysicalDevice.Handle,
            QueueFamilies = [new QueueFamilyVKDesc { queueNum = 1, queueType = QueueType.GRAPHICS, familyIndex = raw.QueueFamily }],
            MinorVersion = raw.MinorVersion,
            VkInstanceExtensions = instanceExtensions,
            VkDeviceExtensions = deviceExtensions,
            EnableNriValidation = true,
            MessageHandler = (in NriMessage m) => { lock (wrappedMessages) wrappedMessages.Add(m); },
        }))
        {
            if (!RunFrames(wrapped, $"wrapped VK device (VK_KHR_swapchain only, validation {(raw.Validated ? "on" : "unavailable")})", wrappedMessages, raw.Errors)) failed = true;
        }
        foreach (var e in raw.Errors)
        {
            Console.WriteLine($"FAIL Vulkan validation (wrapped device): {e}");
            failed = true;
        }

        return failed ? 1 : 0;
    }

    /// <summary>Frames across <see cref="Sizes"/>; false when an ERROR was reported.</summary>
    private static bool RunFrames(NriDevice device, string label, List<NriMessage> messages, List<string> layerErrors)
    {
        Console.WriteLine($"{label}: '{TestDevice.AdapterFor(GraphicsAPI.VK)?.Name}', features.resizableSwapChain={device.Desc.features.resizableSwapChain}");
        var hwnd = CreateWindowExW(0, "STATIC", "PrismNRI resize", WS_POPUP, 0, 0, 64, 64, 0, 0, 0, 0);
        if (hwnd == 0) throw new InvalidOperationException($"CreateWindowEx failed with error {Marshal.GetLastPInvokeError()}");

        int acquireOutOfDate = 0, presentOutOfDate = 0, recreated = 0, frames = 0, presented = 0;
        var retired = new List<NriFence>();
        try
        {
            var ext = device.SwapChain;
            var queue = device.GetQueue(QueueType.GRAPHICS);
            using var acquire = device.CreateFence(NriSwapChainExtension.SwapChainSemaphore);
            using var allocator = queue.CreateCommandAllocator();
            using var commandBuffer = allocator.CreateCommandBuffer();

            NriSwapChain? swapChain = null;
            NriFence[] release = [];
            void Recreate()
            {
                device.WaitIdle();
                if (swapChain is not null) recreated++;
                swapChain?.Dispose();
                retired.AddRange(release); // a present may still wait on them; destroyed at the end
                release = [];
                swapChain = null;
                GetClientRect(hwnd, out var rect);
                int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
                if (width == 0 || height == 0) return; // no swap chain until the window has a size
                swapChain = ext.CreateSwapChain(new NriSwapChainDesc
                {
                    Window = new NriWindow { Windows = new NriWindowsWindow(Hwnd: hwnd) },
                    Queue = queue,
                    Width = (ushort)width,
                    Height = (ushort)height,
                    TextureNum = 2,
                    Format = SwapChainFormat.BT709_G22_8BIT,
                });
                release = swapChain.Textures.Select(_ => device.CreateFence(NriSwapChainExtension.SwapChainSemaphore)).ToArray();
            }

            Recreate();
            var present = new AccessLayoutStage { access = AccessBits.NONE, layout = Layout.PRESENT, stages = StageBits.NONE };
            for (var step = 0; step <= Sizes.Length; step++)
            {
                if (step > 0)
                {
                    var (w, h) = Sizes[step - 1];
                    SetWindowPos(hwnd, 0, 0, 0, w, h, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
                }

                for (var frame = 0; frame < FramesPerSize; frame++)
                {
                    frames++;
                    if (swapChain is null)
                    {
                        Recreate();
                        if (swapChain is null) continue;
                    }

                    if (ext.AcquireNextTexture(swapChain, acquire, out var index) == NriSwapChainStatus.OutOfDate)
                    {
                        acquireOutOfDate++;
                        Recreate();
                        continue;
                    }

                    allocator.Reset();
                    commandBuffer.Begin();
                    commandBuffer.Barrier(new NriTextureBarrier(swapChain.Textures[(int)index], GpuWork.Undefined, present));
                    commandBuffer.End();
                    queue.Submit([commandBuffer], [new NriFenceSubmit(acquire, 0)], [new NriFenceSubmit(release[index], 0)]);
                    if (ext.QueuePresent(swapChain, release[index]) == NriSwapChainStatus.OutOfDate)
                    {
                        presentOutOfDate++;
                        Recreate();
                        continue;
                    }
                    presented++;
                    device.WaitIdle();
                }
            }

            device.WaitIdle();
            swapChain?.Dispose();
            foreach (var f in release) f.Dispose();
        }
        finally
        {
            foreach (var f in retired) f.Dispose();
            DestroyWindow(hwnd);
        }

        Console.WriteLine($"RESULT {label}, features.resizableSwapChain={device.Desc.features.resizableSwapChain}: {Sizes.Length} resizes, {frames} frames, {presented} presented; " +
            $"acquire OUT_OF_DATE {acquireOutOfDate}, present OUT_OF_DATE {presentOutOfDate}, swap chain recreated {recreated} times");
        var ok = true;
        foreach (var m in messages)
        {
            Console.WriteLine($"{(m.IsError ? "FAIL " : "")}message {m}");
            ok &= !m.IsError;
        }
        return ok && layerErrors.Count == 0;
    }

    private const uint WS_POPUP = 0x80000000; // never shown
    private const uint SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [LibraryImport("user32", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial nint CreateWindowExW(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint hwnd);

    [LibraryImport("user32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(nint hwnd, out RECT rect);
}
