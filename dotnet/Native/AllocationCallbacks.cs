namespace PrismNRI.Native;

public unsafe partial struct AllocationCallbacks
{
    [NativeTypeName("void *(*)(void *, size_t, size_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<void*, nuint, nuint, void*> Allocate;

    [NativeTypeName("void *(*)(void *, void *, size_t, size_t) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<void*, void*, nuint, nuint, void*> Reallocate;

    [NativeTypeName("void (*)(void *, void *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<void*, void*, void> Free;

    public void* userArg;

    [NativeTypeName("bool")]
    public byte disable3rdPartyAllocationCallbacks;
}
