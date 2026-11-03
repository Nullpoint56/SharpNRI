// A Vulkan swap chain under the Khronos best-practices checks. With swapchain_maintenance1, NRI
// creates the swap chain with VkSwapchainPresentScalingCreateInfoKHR, and the best-practices
// checks then also expect VkSwapchainPresentModesCreateInfoKHR, which tells the implementation the
// application knows about the maintenance1 present-mode rules (a swap chain without it behaves as
// if it listed its own presentMode only). Patch 0037 chains it with that one mode. CLAUDE.md:
// Extensions.
//
// Presents run in a child process for the reason SwapChainResizeTests gives, and the checks are
// switched on through VK_KHRONOS_VALIDATION_VALIDATE_BEST_PRACTICES in the child's environment,
// which the layer reads at instance creation; a committed 64 KiB buffer's
// "small-dedicated-allocation" warning is the marker that they are active.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed partial class SwapChainResizeTests
{
    private const string BestPracticesChildName = "swapchain-best-practices";
    private const string BestPracticesSetting = "VK_KHRONOS_VALIDATION_VALIDATE_BEST_PRACTICES";
    private const string PresentModesStruct = "PresentModesCreateInfo";

    /// <summary>
    /// A Vulkan swap chain on a device with swapchain_maintenance1 (features.resizableSwapChain)
    /// draws no best-practices warning about a missing VkSwapchainPresentModesCreateInfoKHR, and
    /// presents two frames without an ERROR.
    /// </summary>
    [Fact]
    public void A_Vulkan_swap_chain_with_present_scaling_lists_its_present_modes()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "the test window is a Win32 window; no other window system here");
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));

        var self = Environment.ProcessPath!;
        var start = new ProcessStartInfo(self) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (Path.GetFileNameWithoutExtension(self).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
        start.Environment[ChildVariable] = BestPracticesChildName;
        start.Environment[BestPracticesSetting] = "true";

        using var child = Process.Start(start)!;
        var stderr = child.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        var stdout = child.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        if (!child.WaitForExit(GpuWork.GpuTimeout))
        {
            child.Kill(entireProcessTree: true);
            Assert.Fail($"the child did not finish within {GpuWork.GpuTimeout}");
        }
        child.WaitForExit();
        var lines = stdout.Result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines) output.WriteLine(line);
        if (stderr.Result.Length != 0) output.WriteLine("stderr: " + stderr.Result);

        var skip = lines.FirstOrDefault(l => l.StartsWith("SKIP ", StringComparison.Ordinal));
        Assert.SkipWhen(skip is not null && child.ExitCode == 0, skip?[5..] ?? "");
        foreach (var result in lines.Where(l => l.StartsWith("RESULT ", StringComparison.Ordinal))) GpuWork.Record(result);
        Assert.True(child.ExitCode == 0, $"child exit code {child.ExitCode}: " + string.Join(" | ", lines.Where(l => l.StartsWith("FAIL ", StringComparison.Ordinal))));
        Assert.Single(lines, l => l.StartsWith("RESULT ", StringComparison.Ordinal));
    }

    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "the child mode must run before xunit's entry point")]
    internal static void RunBestPracticesChildIfAsked()
    {
        if (Environment.GetEnvironmentVariable(ChildVariable) != BestPracticesChildName) return;
        int code;
        try
        {
            code = BestPracticesChild();
        }
        catch (Exception e)
        {
            Console.WriteLine("FAIL " + e.ToString().ReplaceLineEndings(" / "));
            code = 1;
        }
        Console.Out.Flush();
        Environment.Exit(code);
    }

    private static int BestPracticesChild()
    {
        var messages = new List<NriMessage>();
        using var device = TestDevice.Create(GraphicsAPI.VK, messages);
        if (device.Desc.features.swapChain == 0)
        {
            Console.WriteLine("SKIP VK: features.swapChain is false");
            return 0;
        }
        if (device.Desc.features.resizableSwapChain == 0)
        {
            Console.WriteLine("SKIP VK: features.resizableSwapChain is false (no VK_KHR/EXT_swapchain_maintenance1, so no present scaling and no present-modes rule)");
            return 0;
        }

        const string marker = "small-dedicated-allocation";
        using (device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 64 * 1024 }))
        {
        }
        if (!messages.Any(m => m.Text.Contains(marker, StringComparison.Ordinal) || m.Text.Contains("fully consumed by the buffer", StringComparison.Ordinal)))
        {
            Console.WriteLine($"SKIP the Khronos best-practices checks did not activate ({BestPracticesSetting} is read from the process environment at instance creation; no '{marker}' warning for a committed 64 KiB buffer)");
            return 0;
        }

        var hwnd = CreateWindowExW(0, "STATIC", "SharpNRI best practices", WS_POPUP, 0, 0, 64, 64, 0, 0, 0, 0);
        if (hwnd == 0) throw new InvalidOperationException($"CreateWindowEx failed with error {Marshal.GetLastPInvokeError()}");
        var first = messages.Count;
        var presented = 0;
        try
        {
            GetClientRect(hwnd, out var rect);
            var queue = device.GetQueue(QueueType.GRAPHICS);
            using var swapChain = device.SwapChain.CreateSwapChain(new NriSwapChainDesc
            {
                Window = new NriWindow { Windows = new NriWindowsWindow(Hwnd: hwnd) },
                Queue = queue,
                Width = (ushort)(rect.Right - rect.Left),
                Height = (ushort)(rect.Bottom - rect.Top),
                TextureNum = 2,
                Format = SwapChainFormat.BT709_G22_8BIT,
            });
            using var acquire = device.CreateFence(NriSwapChainExtension.SwapChainSemaphore);
            using var allocator = queue.CreateCommandAllocator();
            using var commandBuffer = allocator.CreateCommandBuffer();
            var release = swapChain.Textures.Select(_ => device.CreateFence(NriSwapChainExtension.SwapChainSemaphore)).ToArray();
            var present = new AccessLayoutStage { access = AccessBits.NONE, layout = Layout.PRESENT, stages = StageBits.NONE };
            for (var frame = 0; frame < 2; frame++)
            {
                if (device.SwapChain.AcquireNextTexture(swapChain, acquire, out var index) == NriSwapChainStatus.OutOfDate) break;
                allocator.Reset();
                commandBuffer.Begin();
                commandBuffer.Barrier(new NriTextureBarrier(swapChain.Textures[(int)index], GpuWork.Undefined, present));
                commandBuffer.End();
                queue.Submit([commandBuffer], [new NriFenceSubmit(acquire, 0)], [new NriFenceSubmit(release[index], 0)]);
                if (device.SwapChain.QueuePresent(swapChain, release[index]) == NriSwapChainStatus.OutOfDate) break;
                presented++;
                device.WaitIdle();
            }
            device.WaitIdle();
            foreach (var f in release) f.Dispose();
        }
        finally
        {
            DestroyWindow(hwnd);
        }

        var during = messages.Skip(first).ToList();
        var presentModes = during.Where(m => m.Text.Contains(PresentModesStruct, StringComparison.Ordinal)).ToList();
        var bestPractices = during.Count(m => m.Text.Contains("BestPractices-", StringComparison.Ordinal));
        Console.WriteLine($"RESULT swap chain under best practices, features.resizableSwapChain=1: {presented} of 2 frames presented, " +
            $"{bestPractices} best-practices message(s), {presentModes.Count} naming {PresentModesStruct}");
        var ok = presented == 2;
        foreach (var m in during)
        {
            var bad = m.IsError || m.Text.Contains(PresentModesStruct, StringComparison.Ordinal);
            Console.WriteLine($"{(bad ? "FAIL " : "")}message {m}");
            ok &= !bad;
        }
        return ok ? 0 : 1;
    }
}
