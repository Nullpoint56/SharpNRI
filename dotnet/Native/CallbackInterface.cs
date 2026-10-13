namespace PrismNRI.Native;

public unsafe partial struct CallbackInterface
{
    [NativeTypeName("void (*)(Message, const char *, uint32_t, const char *, void *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Message, sbyte*, uint, sbyte*, void*, void> MessageCallback;

    [NativeTypeName("void (*)(void *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<void*, void> AbortExecution;

    public void* userArg;
}
