using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

/// <summary>
/// Stage masks that name every shader stage (<c>StageBits.ALL_SHADERS</c> in a barrier, a pipeline
/// layout whose stages are <c>ALL</c>) on a Vulkan device that lacks some of those stages. NRI v191
/// converts such a mask to every Vulkan stage bit (<c>ConversionVK.h</c>, <c>GetPipelineStageFlags</c>
/// and <c>GetShaderStageFlags</c>) whatever the device has, so a device without ray tracing, mesh
/// shaders, geometry or tessellation shaders gets stage bits its features or extensions do not
/// allow (AGENTS.md: Barriers). Here ray tracing is switched off (<c>DisableVkRayTracing</c>), which
/// leaves every adapter without the ray tracing stages; MoltenVK also lacks the mesh, task and
/// geometry stages.
/// </summary>
public sealed class StageMaskTests
{
    private static NriDevice CreateWithoutRayTracing(List<NriMessage> messages) => NriDevice.Create(new NriDeviceOptions
    {
        GraphicsApi = GraphicsAPI.VK,
        Adapter = TestDevice.AdapterFor(GraphicsAPI.VK),
        DisableVkRayTracing = true,
        EnableNriValidation = true,
        EnableGraphicsApiValidation = true,
        MessageHandler = (in NriMessage m) => { lock (messages) messages.Add(m); },
    });

    /// <summary>
    /// A global barrier from shader reads to shader reads on every shader stage records without a
    /// message: before NRI's fix, VUID-VkMemoryBarrier2-srcStageMask-07946 and -dstStageMask-07946 for
    /// <c>VK_PIPELINE_STAGE_2_RAY_TRACING_SHADER_BIT_KHR</c> (and 03929 / 03934 / 03935 for the
    /// geometry, mesh and task stages where the device lacks those).
    /// </summary>
    [Fact]
    public void A_barrier_naming_every_shader_stage_records_on_a_device_without_ray_tracing()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        var messages = new List<NriMessage>();
        using var device = CreateWithoutRayTracing(messages);
        Assert.Equal(0, device.Desc.tiers.rayTracing);

        var shaderReads = new AccessStage { access = AccessBits.SHADER_RESOURCE, stages = StageBits.ALL_SHADERS };
        GpuWork.Run(device, cmd => cmd.Barrier(new GlobalBarrierDesc { before = shaderReads, after = shaderReads }));

        Assert.DoesNotContain(messages, m => m.IsError);
    }

    /// <summary>
    /// A pipeline layout with stages <c>ALL</c> and a root constant visible to all of them is created
    /// and its constant set without a message: before NRI's fix,
    /// VUID-VkPushConstantRange-stageFlags-parameter at <c>vkCreatePipelineLayout</c> and
    /// VUID-VkPushConstantsInfo-stageFlags-parameter at <c>vkCmdPushConstants2</c>, for ray tracing
    /// shader stage bits whose extension the device does not have (task and mesh bits likewise).
    /// </summary>
    [Fact]
    public void A_root_constant_visible_to_every_stage_is_set_on_a_device_without_ray_tracing()
    {
        TestDevice.RequireAvailable(new Backend(GraphicsAPI.VK));
        var messages = new List<NriMessage>();
        using var device = CreateWithoutRayTracing(messages);
        Assert.Equal(0, device.Desc.tiers.rayTracing);

        using var layout = device.CreatePipelineLayout(new NriPipelineLayoutDesc
        {
            RootConstants = [new RootConstantDesc { registerIndex = 0, size = 16, shaderStages = StageBits.ALL }],
            ShaderStages = StageBits.ALL,
        });
        GpuWork.Run(device, cmd =>
        {
            cmd.SetPipelineLayout(BindPoint.GRAPHICS, layout);
            cmd.SetRootConstants(0, new byte[16]);
        });

        Assert.DoesNotContain(messages, m => m.IsError);
    }
}
