// Vortice.Vulkan function tables for one exact VkInstance / VkDevice, never from Vortice's cache.
//
// Vortice 3.3.0's Vulkan.GetApi(instance) and GetApi(instance, device) return tables from two
// process-wide ConcurrentDictionaries keyed by the handle value; an entry is dropped only when
// vkDestroyInstance / vkDestroyDevice is called through that very table. NRI destroys the instance
// and device it creates itself, so every GetApi on an NRI-owned device leaves an entry behind whose
// pointers came from vkGetDeviceProcAddr on a device that no longer exists, i.e. into the call chain
// (validation layer, driver) of an instance the loader has torn down, unloading the layer library
// with it. When a later device gets the same handle value (the allocator reuses the freed block),
// GetApi hands back that dead table: the 107th-test SIGSEGV on the Linux CI leg (CLAUDE.md).
// The public constructors resolve every pointer against the given objects at the time of the call.

using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace SharpNRI.Tests;

internal static class VulkanApi
{
    // Vortice's global vkGetInstanceProcAddr (the loader's export) is null until vkInitialize has
    // run, and the constructors below call it. Lazy and process-wide (measured: a null call on Linux
    // when a test that needed it ran before the one that used to initialise it).
    private static readonly Lazy<VkResult> Loader = new(() => vkInitialize());

    /// <summary>Loads the Vulkan loader into Vortice once; fails the test when it cannot.</summary>
    public static void RequireLoader() =>
        Xunit.Assert.True(Loader.Value == VkResult.Success, $"vkInitialize returned {Loader.Value}");

    /// <summary>Instance-level functions resolved against <paramref name="instance"/> now.</summary>
    public static VkInstanceApi For(VkInstance instance)
    {
        RequireLoader();
        return new VkInstanceApi(instance);
    }

    /// <summary>Device-level functions resolved against <paramref name="device"/> (through <paramref name="instance"/>) now.</summary>
    public static VkDeviceApi For(VkInstance instance, VkDevice device) => new(For(instance), device);
}
