// L1 — NRI's Result codes as exceptions.
//
// NRI reports failure two ways at once: a Result from the call, and a MessageCallback invocation
// with the reason, fired before the call returns. The exception carries the code; the text went to
// the message handler the device was created with. A handler that records is therefore the
// difference between "FAILURE" and knowing why, which is why NriDevice installs one by default.

using PrismNRI.Native;

namespace PrismNRI;

/// <summary>An NRI call returned something other than <see cref="Result.SUCCESS"/>.</summary>
public class NriException : Exception
{
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

    /// <summary>Throws for any result other than success.</summary>
    internal static void Check(Result result, string call)
    {
        if (result != Result.SUCCESS) throw For(result, call);
    }

    internal static NriException For(Result result, string call) => result switch
    {
        Result.DEVICE_LOST => new NriDeviceLostException(call),
        Result.UNSUPPORTED => new NriUnsupportedException(call),
        _ => new NriException(result, call),
    };
}

/// <summary>
/// The device is lost. Nothing on it can be used again; <c>NriDevice.ReportDeviceLostInfo</c> can
/// say why if the device was created with a <c>DeviceLostInfoLevel</c> above <c>NONE</c>.
/// </summary>
public sealed class NriDeviceLostException(string call)
    : NriException(Result.DEVICE_LOST, call);

/// <summary>
/// The device does not support what was asked: a queue type it has none of, an extension the
/// backend does not implement, a feature the adapter lacks. Check <c>DeviceDesc</c> first.
/// </summary>
public sealed class NriUnsupportedException(string call, string? detail = null)
    : NriException(Result.UNSUPPORTED, call, detail);
