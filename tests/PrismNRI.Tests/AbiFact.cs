// Gate for tests that need a C++ compiler.
//
// The layout oracle compiles a probe against the pinned NRI headers. On a machine with no C++
// compiler that is not a failure of the binding, so it skips with a reason that says what to install
// rather than failing with a Win32Exception that reads like a broken build.

using Xunit;

namespace PrismNRI.Tests;

/// <summary>A fact needing a C++ compiler; says where to get one when there is none.</summary>
public sealed class AbiFactAttribute : FactAttribute
{
    public AbiFactAttribute()
    {
        if (NativeAbi.Compiler is null) Skip = NativeAbi.Unavailable;
    }
}
