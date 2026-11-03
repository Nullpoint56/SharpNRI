namespace SharpNRI.Native;

public unsafe partial struct CommandBufferD3D12Desc
{
    public ID3D12CommandList* d3d12CommandList;

    public ID3D12CommandAllocator* d3d12CommandAllocator;
}
