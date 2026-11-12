// L1 — NRI's Result codes as exceptions.
//
// NRI reports failure two ways at once: a Result from the call, and a MessageCallback invocation
// with the reason, fired before the call returns. The exception carries the code and, as its
// detail, the reason: the last error text reported on the calling thread since the binding last
// checked a result on that thread. That is the failing call's own text, unless a call nothing
// checks (a Cmd*, which returns nothing) reported one on this thread since. NRI and the validation
// layers report synchronously, on the thread of the call, so a text reported on another thread, or
// before an earlier check on this one, belongs to another call and is never attached; when nothing
// was reported there is no detail. The text also went to the message handler the device was
// created with, which sees every message on every thread.

using SharpNRI.Native;

namespace SharpNRI;

/// <summary>An NRI call returned something other than <see cref="Result.SUCCESS"/>.</summary>
/// <remarks>
/// The message is <c>"{call}: {result}"</c>, followed by the last error text NRI or a validation
/// layer reported on the calling thread since the binding last checked a result there: the failing
/// call's own, or that of an unchecked call before it on this thread (a refused <c>Cmd*</c>). Text
/// reported on another thread, or before that check, is never attached; with none, there is no detail.
/// </remarks>
public class NriException : Exception
{
    // The last ERROR text reported on this thread since the binding last checked a result here.
    // Written by MessageBridge, taken (and cleared) by every check, so it never outlives the next
    // check on its thread. A thread-static reference: no lock and no allocation on the success path.
    [ThreadStatic] private static string? t_reported;

    /// <summary>The code NRI returned.</summary>
    public Result Result { get; }

    /// <summary>The call that failed, as named in the NRI header.</summary>
    public string Call { get; }

    public NriException(Result result, string call, string? detail = null)
        : base(detail is null ? $"{call}: {result}" : $"{call}: {result} — {detail}")
    {
        Result = result;
        Call = call;
    }

    /// <summary>Records an ERROR message reported on the calling thread, for the check that follows.</summary>
    internal static void Reported(string text) => t_reported = text;

    /// <summary>Forgets what was reported on the calling thread: a check that found success.</summary>
    internal static void ClearReported() => t_reported = null;

    /// <summary>Throws for any result other than success, with the text reported on this thread since the last check.</summary>
    internal static void Check(Result result, string call)
    {
        if (result != Result.SUCCESS) throw For(result, call);
        t_reported = null;
    }

    /// <summary>The exception for a failed result, taking the text reported on this thread since the last check.</summary>
    internal static NriException For(Result result, string call)
    {
        var detail = t_reported;
        t_reported = null;
        return result switch
        {
            Result.DEVICE_LOST => new NriDeviceLostException(call, detail),
            Result.UNSUPPORTED => new NriUnsupportedException(call, detail),
            Result.EXT_ERROR => new NriExternalException(call, detail),
            _ => new NriException(result, call, detail),
        };
    }
}

/// <summary>
/// The device is lost. Nothing on it can be used again; <c>NriDevice.ReportDeviceLostInfo</c> can
/// say why if the device was created with a <c>DeviceLostInfoLevel</c> above <c>NONE</c>. The
/// message carries what the failing call reported, as for every <see cref="NriException"/> (D3D12,
/// <c>QueueSubmit</c> after a removal: <c>Submit(): failed, result = 0x887A0005 (-2005270523)!</c>).
/// </summary>
public sealed class NriDeviceLostException(string call, string? detail = null)
    : NriException(Result.DEVICE_LOST, call, detail);

/// <summary>
/// The device does not support what was asked: a queue type it has none of, an extension the
/// backend does not implement, a feature the adapter lacks. Check <c>DeviceDesc</c> first.
/// </summary>
public sealed class NriUnsupportedException(string call, string? detail = null)
    : NriException(Result.UNSUPPORTED, call, detail);

/// <summary>
/// A third-party library NRI calls failed (<c>Result.EXT_ERROR</c>): an upscaler SDK (FSR, XeSS,
/// DLSS, a missing or outdated runtime among the causes) or NVIDIA Reflex. The device is still
/// usable, so this is the point to fall back, for example to another upscaler. The message names the
/// vendor call and its code, as NRI reported it; the code itself is not available as a value. A vendor
/// failure whose code means unsupported, an invalid argument or out of memory arrives as
/// <see cref="NriUnsupportedException"/> or an <see cref="NriException"/> with that result instead.
/// </summary>
public sealed class NriExternalException(string call, string? detail = null)
    : NriException(Result.EXT_ERROR, call, detail);
