// L1 — NRI's message callback, bridged to managed code.
//
// CallbackInterface is the one place NRI calls back into the host on its own initiative: every
// validation message, every failed call's reason, every device-lost report comes through
// MessageCallback. The function pointers must be [UnmanagedCallersOnly] thunks and the managed
// receiver has to be recovered from `userArg`, which is a GCHandle here.
//
// Lifetime: NRI copies the CallbackInterface struct at device creation and may call it until
// nriDestroyDevice returns — including from inside nriDestroyDevice, for leaked objects. So the
// bridge is freed by NriDevice after the native device is gone, never before.
//
// Thunks never let a managed exception escape: unwinding through a native frame is undefined and
// terminates the process on Linux. A handler that throws loses that one message, nothing else.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PrismNRI.Native;

namespace PrismNRI;

/// <summary>One message from NRI: its own validation, a backend validation layer, or a failed call.</summary>
/// <param name="Type">Info, warning or error, as NRI classified it.</param>
/// <param name="File">The NRI source file that reported it, when NRI provides one.</param>
/// <param name="Line">The line in that file, 0 when not provided.</param>
/// <param name="Text">The message.</param>
public readonly record struct NriMessage(Message Type, string? File, uint Line, string Text)
{
    public bool IsError => Type == Message.ERROR;
    public bool IsWarning => Type == Message.WARNING;

    public override string ToString() =>
        File is null ? $"[{Type}] {Text}" : $"[{Type}] {File}:{Line}: {Text}";
}

/// <summary>Receives every <see cref="NriMessage"/> a device produces.</summary>
public delegate void NriMessageHandler(in NriMessage message);

internal sealed unsafe class MessageBridge : IDisposable
{
    private readonly NriMessageHandler _handler;
    private readonly bool _breakOnError;
    private GCHandle _self;

    internal MessageBridge(NriMessageHandler handler, bool breakOnError)
    {
        _handler = handler;
        _breakOnError = breakOnError;
        _self = GCHandle.Alloc(this, GCHandleType.Normal);
    }

    /// <summary>The struct to place in a device creation desc.</summary>
    /// <remarks>
    /// AbortExecution is always supplied. When it is null NRI installs its own handler, which
    /// calls <c>DebugBreak()</c> on every error (Creation.cpp, CheckAndSetDefaultCallbacks); with
    /// no debugger attached that terminates the process with 0x80000003 before the message
    /// handler's record is ever read. Measured by the test suite; CLAUDE.md: Device creation and callbacks.
    /// </remarks>
    internal CallbackInterface Native => new()
    {
        MessageCallback = &OnMessage,
        AbortExecution = &OnAbort,
        userArg = (void*)GCHandle.ToIntPtr(_self),
    };

    public void Dispose()
    {
        if (_self.IsAllocated) _self.Free();
    }

    private static MessageBridge? FromUserArg(void* userArg)
    {
        if (userArg is null) return null;
        var handle = GCHandle.FromIntPtr((nint)userArg);
        return handle.IsAllocated ? handle.Target as MessageBridge : null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnMessage(Message type, sbyte* file, uint line, sbyte* text, void* userArg)
    {
        try
        {
            var bridge = FromUserArg(userArg);
            if (bridge is null) return;
            var message = new NriMessage(type, Interop.Utf8.Read(file), line, Interop.Utf8.Read(text) ?? "");
            bridge._handler(in message);
        }
        catch
        {
            // Dropped on purpose; see the file comment.
        }
    }

    // NRI calls this after reporting an ERROR. Breaking into an attached debugger is the useful
    // thing when asked for; otherwise it is a no-op rather than a crash, so the message handler
    // stays the record of what happened and the failed call returns its Result.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnAbort(void* userArg)
    {
        try
        {
            var bridge = FromUserArg(userArg);
            if (bridge is { _breakOnError: true } && System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debugger.Break();
        }
        catch
        {
        }
    }
}
