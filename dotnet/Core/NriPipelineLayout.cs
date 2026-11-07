// L2 — a pipeline layout.
//
// The one place in the core where a managed desc is marshalled into a tree of native arrays. The
// arrays are pinned for the duration of CreatePipelineLayout only; NRI copies what it needs.

using System.Runtime.InteropServices;
using SharpNRI.Native;

namespace SharpNRI;

/// <summary>A pipeline layout: root constants, root descriptors, root samplers and descriptor sets.</summary>
public sealed unsafe class NriPipelineLayout : NriObject
{
    internal NriPipelineLayout(NriDevice device, PipelineLayout* handle, NriPipelineLayoutDesc desc) : base(handle, "NriPipelineLayout")
    {
        Device = device;
        Desc = desc;
        device.ChildCreated();
    }

    public override NriDevice Device { get; }

    /// <summary>The desc this layout was created from.</summary>
    public NriPipelineLayoutDesc Desc { get; }

    internal PipelineLayout* Raw => (PipelineLayout*)Handle;

    internal static NriPipelineLayout Create(NriDevice device, NriPipelineLayoutDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);
        var sets = desc.DescriptorSets;
        var nativeSets = new DescriptorSetDesc[sets.Length];
        var pins = new GCHandle[sets.Length];

        try
        {
            for (var i = 0; i < sets.Length; i++)
            {
                var ranges = sets[i].Ranges;
                pins[i] = GCHandle.Alloc(ranges, GCHandleType.Pinned);
                nativeSets[i] = new DescriptorSetDesc
                {
                    registerSpace = sets[i].RegisterSpace,
                    ranges = ranges.Length == 0 ? null : (DescriptorRangeDesc*)pins[i].AddrOfPinnedObject(),
                    rangeNum = (uint)ranges.Length,
                };
            }

            fixed (RootConstantDesc* rootConstants = desc.RootConstants)
            fixed (RootDescriptorDesc* rootDescriptors = desc.RootDescriptors)
            fixed (RootSamplerDesc* rootSamplers = desc.RootSamplers)
            fixed (DescriptorSetDesc* descriptorSets = nativeSets)
            {
                var native = new PipelineLayoutDesc
                {
                    rootRegisterSpace = desc.RootRegisterSpace,
                    rootConstants = rootConstants,
                    rootConstantNum = (uint)desc.RootConstants.Length,
                    rootDescriptors = rootDescriptors,
                    rootDescriptorNum = (uint)desc.RootDescriptors.Length,
                    rootSamplers = rootSamplers,
                    rootSamplerNum = (uint)desc.RootSamplers.Length,
                    descriptorSets = descriptorSets,
                    descriptorSetNum = (uint)nativeSets.Length,
                    shaderStages = desc.ShaderStages,
                    flags = desc.Flags,
                };

                PipelineLayout* handle = null;
                NriException.Check(device.Core.CreatePipelineLayout(device.Raw, &native, &handle), "CreatePipelineLayout");
                return new NriPipelineLayout(device, handle, desc);
            }
        }
        finally
        {
            foreach (var pin in pins) if (pin.IsAllocated) pin.Free();
        }
    }

    private protected override void Destroy(void* handle) => Device.Core.DestroyPipelineLayout((PipelineLayout*)handle);
}
