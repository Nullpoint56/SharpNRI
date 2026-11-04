// Validation-layer settings for one device's instance (patch 0038, DeviceCreationDesc::
// vkInstanceCreateInfoNext, NriDeviceOptions.VkInstanceCreateInfoNext). NRI creates the Vulkan
// instance itself; without a way to chain into VkInstanceCreateInfo, the Khronos layer is configured
// through environment variables only, which apply to the whole process and which .NET on Linux and
// macOS does not pass to native code when set at run time. AGENTS.md: Device creation and callbacks.

using System.Runtime.InteropServices;
using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed unsafe class LayerSettingsTests
{
    private const string BestPracticesSetting = "VK_KHRONOS_VALIDATION_VALIDATE_BEST_PRACTICES";
    private const string Marker = "small-dedicated-allocation";
    private const int VK_STRUCTURE_TYPE_LAYER_SETTINGS_CREATE_INFO_EXT = 1000496000;
    private const int VK_LAYER_SETTING_TYPE_BOOL32_EXT = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct VkLayerSettingEXT
    {
        public byte* pLayerName;
        public byte* pSettingName;
        public int type;
        public uint valueCount;
        public void* pValues;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct VkLayerSettingsCreateInfoEXT
    {
        public int sType;
        public void* pNext;
        public uint settingCount;
        public VkLayerSettingEXT* pSettings;
    }

    /// <summary>
    /// A VkLayerSettingsCreateInfoEXT setting the Khronos layer's "validate_best_practices", passed
    /// as the instance chain, switches the best-practices checks on for that device: a committed
    /// 64 KiB buffer then draws the layer's "small-dedicated-allocation" warning. A device created
    /// right before it without the chain is the control (no warning), so the checks are not on for
    /// some other reason. Upstream NRI has no such field; with this patch's header but NRI's
    /// instance creation unchanged the setting is ignored and no warning comes.
    /// </summary>
    [Fact]
    public void A_layer_setting_chained_into_the_instance_switches_on_best_practices_for_that_device()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        var preset = Environment.GetEnvironmentVariable(BestPracticesSetting);
        Assert.SkipWhen(!string.IsNullOrEmpty(preset),
            $"{BestPracticesSetting}={preset} is set in this process's environment, so the control device would have the checks on too");

        var control = MarkerWarnings(0);
        Assert.True(control.Count == 0, $"best-practices warning without the layer setting: {string.Join(" | ", control)}");

        var layerName = "VK_LAYER_KHRONOS_validation\0"u8;
        var settingName = "validate_best_practices\0"u8;
        uint enabled = 1; // VK_TRUE
        List<string> withSetting;
        fixed (byte* layer = layerName)
        fixed (byte* name = settingName)
        {
            var setting = new VkLayerSettingEXT
            {
                pLayerName = layer,
                pSettingName = name,
                type = VK_LAYER_SETTING_TYPE_BOOL32_EXT,
                valueCount = 1,
                pValues = &enabled,
            };
            var settings = new VkLayerSettingsCreateInfoEXT
            {
                sType = VK_STRUCTURE_TYPE_LAYER_SETTINGS_CREATE_INFO_EXT,
                settingCount = 1,
                pSettings = &setting,
            };
            withSetting = MarkerWarnings((nint)(&settings));
        }
        foreach (var text in withSetting) GpuWork.Record("with the layer setting: " + text);
        Assert.True(withSetting.Count > 0, $"no '{Marker}' warning for a committed 64 KiB buffer on a device whose instance chain sets validate_best_practices: the setting did not reach the layer");
        Assert.Contains("] BestPractices-", withSetting[0]); // the id name, forwarded by patch 0015
    }

    /// <summary>The marker warnings of one committed 64 KiB buffer on a validated VK device; fails on any ERROR.</summary>
    private static List<string> MarkerWarnings(nint instanceCreateInfoNext)
    {
        var messages = new List<NriMessage>();
        using (var device = TestDevice.Create(GraphicsAPI.VK, messages, b => b.VkInstanceCreateInfoNext = instanceCreateInfoNext))
        using (device.CreateCommittedBuffer(MemoryLocation.HOST_UPLOAD, new BufferDesc { size = 64 * 1024 }))
        {
        }
        Assert.DoesNotContain(messages, m => m.IsError);
        return messages.Where(m => m.Text.Contains(Marker, StringComparison.Ordinal)).Select(m => m.Text).ToList();
    }
}
