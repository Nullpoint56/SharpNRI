// L3 — the MeshShader extension (NRIMeshShader.h).
//
// Two draw commands. Everything else a mesh-shader pipeline needs is core: the MESH_SHADER and
// TASK_SHADER stage bits in a graphics pipeline with no vertex input. Available when
// DeviceDesc.features.meshShader is set; UNSUPPORTED otherwise.
//
// Not thread safe, per NRI: one thread records into one command buffer at a time.

using PrismNRI.Interop;
using PrismNRI.Native;

namespace PrismNRI;

/// <summary>NRI's <c>MeshShaderInterface</c>. Acquire through <see cref="NriDevice.MeshShader"/>.</summary>
public sealed unsafe class NriMeshShaderExtension
{
    private readonly MeshShaderInterface _table;

    internal NriMeshShaderExtension(NriDevice device, in MeshShaderInterface table)
    {
        Device = device;
        _table = table;
    }

    public NriDevice Device { get; }

    internal static bool TryAcquire(NriDevice device, out NriMeshShaderExtension? extension)
    {
        if (InterfaceTable.TryAcquire<MeshShaderInterface>(device.Raw, "MeshShaderInterface", out var table))
        {
            extension = new NriMeshShaderExtension(device, in table);
            return true;
        }
        extension = null;
        return false;
    }

    /// <summary><c>CmdDrawMeshTasks</c>: dispatches <c>x * y * z</c> task (or mesh) workgroups.</summary>
    public void DrawMeshTasks(NriCommandBuffer commandBuffer, in DrawMeshTasksDesc drawMeshTasksDesc)
    {
        fixed (DrawMeshTasksDesc* p = &drawMeshTasksDesc) _table.CmdDrawMeshTasks(commandBuffer.Raw, p);
    }

    /// <summary><c>CmdDrawMeshTasks</c> with the group counts inline.</summary>
    public void DrawMeshTasks(NriCommandBuffer commandBuffer, uint x, uint y = 1, uint z = 1) =>
        DrawMeshTasks(commandBuffer, new DrawMeshTasksDesc { x = x, y = y, z = z });

    /// <summary>
    /// <c>CmdDrawMeshTasksIndirect</c>: <paramref name="buffer"/> holds <c>DrawMeshTasksDesc</c>
    /// commands. With <paramref name="countBuffer"/>, the draw count is read from it (needs
    /// <c>features.drawIndirectCount</c>), capped at <paramref name="drawNum"/>.
    /// </summary>
    public void DrawMeshTasksIndirect(NriCommandBuffer commandBuffer, NriBuffer buffer, ulong offset, uint drawNum, uint stride, NriBuffer? countBuffer = null, ulong countBufferOffset = 0) =>
        _table.CmdDrawMeshTasksIndirect(commandBuffer.Raw, buffer.Raw, offset, drawNum, stride, countBuffer is null ? null : countBuffer.Raw, countBufferOffset);
}
