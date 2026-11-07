namespace SharpNRI.Native;

public unsafe partial struct CallbackInterface
{
    [NativeTypeName("void (*)(Message, const char *, uint32_t, const char *, void *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<Message, sbyte*, uint, sbyte*, void*, void> MessageCallback;

    /// <summary>break on "Message::ERROR" if provided</summary>
    [NativeTypeName("void (*)(void *) __attribute__((stdcall))")]
    public delegate* unmanaged[Stdcall]<void*, void> AbortExecution;

    public void* userArg;
}
