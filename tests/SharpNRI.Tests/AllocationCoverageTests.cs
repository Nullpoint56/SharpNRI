// Every call a frame can make is measured for allocations, and a new one cannot slip past.
//
// The zero-allocation frames (AllocationTests, ExtensionAllocationTests) prove what they call. This
// test reads their IL and holds the whole binding to it, default-deny: every public method and
// computed property of every public type in the SharpNRI assembly counts as per-frame, and must be
// called from one of those frames, unless it creates or destroys an object or is named below as not
// per-frame, with the reason. The rule for a name below: an application calls it once per object or
// at setup (requirements, capability queries, binding memory, exports, naming, enumeration), or it
// allocates by design and has a zero-allocation twin that is measured. A member added to the
// binding fails here until it is measured or excluded on purpose. Overloads count separately (a
// span overload and its single-element convenience are two calls to measure). Compiler-generated
// members (record equality, ToString, Deconstruct) are not calls into NRI and are not counted.
// SharpNRI.Descs is data: its marshalling is measured through the calls that take it.

using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Xunit;

namespace SharpNRI.Tests;

public sealed class AllocationCoverageTests
{
    /// <summary>Every public type of the binding (SharpNRI.Native, the generated layer, is called only through it).</summary>
    private static readonly Type[] PerFrameTypes =
        [.. typeof(NriDevice).Assembly.GetExportedTypes().Where(t => t.Namespace == "SharpNRI").OrderBy(t => t.Name)];

    /// <summary>Name prefixes that are never per-frame: object creation and the queries made before it.</summary>
    private static readonly string[] SetupPrefixes = ["Create", "TryCreate", "Dispose"];

    /// <summary>
    /// Members (by name, by name with parameters, or a whole type by its name) that are not per-frame
    /// calls, each with the reason.
    /// </summary>
    private static readonly Dictionary<string, string> NotPerFrame = new()
    {
        // Whole types
        ["NriAdapter"] = "adapter enumeration, before a device exists",
        ["INriAllocator"] = "the allocator interface NRI calls, not the application; its use is what AllocationTests count",
        ["NriCountingAllocator"] = "an allocator NRI calls, not the application; its use is what AllocationTests count",
        ["NriMessage"] = "built per reported message with its text; a clean frame reports none",
        ["NriMessageHandler"] = "the message callback's delegate type",
        ["NriSharedHandle"] = "an export, made once per shared object",
        ["NriWrapperVKExtension"] = "interop with the caller's Vulkan objects, at setup",

        // Setup: memory requirements and binding, capability queries, exports, names
        ["Nri.FitPipelineLayoutSettingsIntoDeviceLimits"] = "layout planning, before pipelines are made",
        ["Nri.GetFormatName"] = "a format's name as a new string, by design; GetFormatProps is the zero-allocation form",
        ["Nri.GetGraphicsAPIString"] = "the API's name as a new string, by design, for diagnostics",
        ["Nri.ReportLiveObjects"] = "diagnostics at shutdown",
        ["Nri.SetThreadName"] = "once per thread",
        ["NriDevice.AllocateMemory"] = "memory, allocated at load",
        ["NriDevice.AllocateAndBindMemory"] = "memory, allocated at load",
        ["NriDevice.BindBufferMemory"] = "binding memory, once per buffer",
        ["NriDevice.BindTextureMemory"] = "binding memory, once per texture",
        ["NriDevice.GetBufferMemoryDesc"] = "a memory requirement, asked before creation",
        ["NriDevice.GetTextureMemoryDesc"] = "a memory requirement, asked before creation",
        ["NriDevice.DefaultAgilitySdkPath"] = "device-creation setup",
        ["NriDevice.ReportDeviceLostInfo"] = "once, after the device is lost",
        ["NriDevice.WrapperVK"] = "interop with the caller's Vulkan objects, at setup; throws on another backend",
        ["NriDevice.WrapperD3D12"] = "interop with the caller's D3D12 objects, at setup; throws on another backend",
        ["NriObject.SetDebugName"] = "a name, given once",
        ["NriQueue.SetDebugName"] = "a name, given once",
        ["NriQueue.ToString"] = "diagnostics",
        ["NriQueue.UploadData"] = "a blocking whole-resource upload helper (it waits for the queue)",
        ["NriPipelineCache.GetData"] = "the cache's bytes as a new array, by design, saved once",
        ["NriBuffer.GetMemoryDesc"] = "a memory requirement, asked before binding",
        ["NriBuffer.GetSharedHandle"] = "an export, made once per shared object",
        ["NriBuffer.Desc"] = "returns a reference into NRI's own desc; no marshalling",
        ["NriTexture.GetMemoryDesc"] = "a memory requirement, asked before binding",
        ["NriTexture.GetSharedHandle"] = "an export, made once per shared object",
        ["NriFence.GetSharedHandle"] = "an export, made once per shared object",
        ["NriDescriptorPool.AllocateDescriptorSets(NriPipelineLayout, UInt32, UInt32, UInt32)"] = "returns a new array by design; the Span overload is the per-frame form",
        ["NriDescriptorSet.Equals"] = "value equality, no NRI call",
        ["NriDescriptorSet.GetHashCode"] = "value equality, no NRI call",
        ["NriDescriptorSet.op_Equality"] = "value equality, no NRI call",
        ["NriDescriptorSet.op_Inequality"] = "value equality, no NRI call",
        ["NriUpscalerExtension.GetRuntimeFiles"] = "a capability query, asked before creation",
        ["NriUpscalerExtension.IsRuntimeInstalled"] = "a capability query, asked before creation",
        ["NriUpscalerExtension.IsUpscalerSupported"] = "a capability query, asked before creation",
        ["NriUpscalerExtension.GetUpscalerProps"] = "the upscaler's fixed properties, read once after creation",
        ["NriVideoExtension.GetVideoCapabilities"] = "a capability query, asked before creation",
        ["NriVideoExtension.GetVideoAV1Capabilities"] = "a capability query, asked before creation",
        ["NriSwapChainExtension.GetDisplayDesc"] = "the display's fixed properties, read on creation or a move",
        ["NriRayTracingExtension.GetAccelerationStructureMemoryDesc"] = "a memory requirement, asked before creation",
        ["NriRayTracingExtension.GetMicromapMemoryDesc"] = "a memory requirement, asked before creation",
        ["NriRayTracingExtension.BindAccelerationStructureMemory"] = "binding memory, once per structure",
        ["NriRayTracingExtension.BindMicromapMemory"] = "binding memory, once per micromap",
        ["NriAccelerationStructure.GetMemoryDesc"] = "a memory requirement, asked before binding",
        ["NriMicromap.GetMemoryDesc"] = "a memory requirement, asked before binding",
    };

    [Fact]
    public void Every_per_frame_call_is_measured_by_a_zero_allocation_frame()
    {
        var called = new HashSet<MethodBase>();
        foreach (var frame in new[] { typeof(AllocationTests), typeof(ExtensionAllocationTests) })
        {
            // Only the measured loop bodies: the local functions named Frame and RecordStream (a call made
            // in setup or an assertion is not measured). The C# compiler names a local function
            // <Outer>g__Frame|n_m.
            foreach (var method in MethodsOf(frame).Where(IsMeasuredBody)) called.UnionWith(CallsIn(method));
        }

        var missing = new List<string>();
        var measured = 0;
        foreach (var type in PerFrameTypes)
        {
            foreach (var member in PublicSurface(type))
            {
                var name = $"{type.Name}.{NameOf(member)}";
                if (SetupPrefixes.Any(p => NameOf(member).StartsWith(p, StringComparison.Ordinal))) continue;
                if (NotPerFrame.ContainsKey(type.Name) || NotPerFrame.ContainsKey(name) || NotPerFrame.ContainsKey($"{name}({Parameters(member)})")) continue;
                if (called.Contains(member)) measured++;
                else missing.Add($"{name}({Parameters(member)})");
            }
        }

        GpuWork.Record($"{measured} per-frame calls measured; {NotPerFrame.Count} members named as not per-frame");
        Assert.True(missing.Count == 0,
            $"{missing.Count} per-frame calls are in no zero-allocation frame (call them from AllocationTests or ExtensionAllocationTests, or name them in NotPerFrame with the reason):\n  " +
            string.Join("\n  ", missing));
    }

    [Fact]
    public void Every_name_in_the_exclusion_list_exists()
    {
        var names = PerFrameTypes.SelectMany(t => PublicSurface(t).SelectMany(m => new[] { $"{t.Name}.{NameOf(m)}", $"{t.Name}.{NameOf(m)}({Parameters(m)})" }).Append(t.Name)).ToHashSet();
        var stale = NotPerFrame.Keys.Where(k => !names.Contains(k)).ToList();
        Assert.True(stale.Count == 0, "NotPerFrame names members that no longer exist: " + string.Join(", ", stale));
    }

    /// <summary>Public methods, and getters of public properties that compute (an auto-property's stored value is not a call).</summary>
    private static IEnumerable<MethodBase> PublicSurface(Type type)
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var method in type.GetMethods(Declared))
        {
            if (method.IsSpecialName && !method.Name.StartsWith("op_", StringComparison.Ordinal)) continue; // accessors come from properties
            if (method.IsDefined(typeof(CompilerGeneratedAttribute))) continue;
            yield return method.IsGenericMethod ? method.GetGenericMethodDefinition() : method;
        }
        foreach (var property in type.GetProperties(Declared))
        {
            var getter = property.GetGetMethod();
            if (getter is null || getter.IsDefined(typeof(CompilerGeneratedAttribute))) continue;
            if (getter.IsVirtual && getter.GetBaseDefinition() != getter) continue; // Device overrides: a field read
            yield return getter;
        }
    }

    private static string NameOf(MethodBase member) => member.Name.StartsWith("get_", StringComparison.Ordinal) ? member.Name[4..] : member.Name;

    private static string Parameters(MethodBase member) => string.Join(", ", member.GetParameters().Select(p => p.ParameterType.Name.TrimEnd('&')));

    private static bool IsMeasuredBody(MethodBase method) =>
        method.Name == "RecordStream" || method.Name.Contains("g__Frame|", StringComparison.Ordinal);

    /// <summary>The type's methods and those of its nested types, which hold the frames' closures.</summary>
    private static IEnumerable<MethodBase> MethodsOf(Type type)
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var method in type.GetMethods(All)) yield return method;
        foreach (var constructor in type.GetConstructors(All)) yield return constructor;
        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var method in MethodsOf(nested)) yield return method;
        }
    }

    private static readonly Dictionary<short, OpCode> OpCodesByValue =
        typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => o.Value);

    /// <summary>The methods a method body calls or takes the address of, generic instances reduced to their definitions.</summary>
    private static IEnumerable<MethodBase> CallsIn(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null) yield break;

        for (var i = 0; i < il.Length;)
        {
            var value = il[i] == 0xFE ? unchecked((short)(0xFE00 | il[i + 1])) : il[i];
            i += il[i] == 0xFE ? 2 : 1;
            var op = OpCodesByValue[value];
            if (op.OperandType == OperandType.InlineMethod)
            {
                var resolved = Resolve(method, BitConverter.ToInt32(il, i));
                if (resolved is not null) yield return resolved;
            }
            i += op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                _ => 4,
            };
        }
    }

    private static MethodBase? Resolve(MethodBase caller, int token)
    {
        MethodBase resolved;
        try
        {
            resolved = caller.Module.ResolveMethod(token,
                caller.DeclaringType is { IsGenericType: true } t ? t.GetGenericArguments() : null,
                caller.IsGenericMethod ? caller.GetGenericArguments() : null)!;
        }
        catch (ArgumentException)
        {
            return null;
        }
        return resolved is MethodInfo { IsGenericMethod: true } generic ? generic.GetGenericMethodDefinition() : resolved;
    }
}
