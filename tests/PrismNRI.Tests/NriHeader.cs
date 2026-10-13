// Where the repo is, which NRI it pins, and where that NRI's headers live.
//
// Tests that read the generated sources or compile against the headers need all three, and need
// them to agree: a probe compiled against a submodule checked out at a different tag than the one
// Directory.Build.props pins would be measuring some other NRI and calling the result ground truth.

using System.Reflection;
using System.Text.RegularExpressions;

namespace PrismNRI.Tests;

/// <summary>The pinned NRI release and the paths derived from it.</summary>
public static partial class NriHeader
{
    private static readonly Lazy<string> LazyRoot = new(FindRoot);
    private static readonly Lazy<int> LazyPinned = new(ReadPin);

    /// <summary>The repository root: the directory holding <c>Directory.Build.props</c>.</summary>
    public static string RepoRoot() => LazyRoot.Value;

    /// <summary><c>$(NriVersion)</c> from <c>Directory.Build.props</c>.</summary>
    public static int PinnedVersion => LazyPinned.Value;

    /// <summary><c>nri/Include</c>, after checking it is the release the repo pins.</summary>
    public static string IncludeDirectory()
    {
        var include = Path.Combine(RepoRoot(), "nri", "Include");
        var nriH = Path.Combine(include, "NRI.h");

        if (!File.Exists(nriH))
            throw new InvalidOperationException(
                $"{nriH} does not exist. The nri submodule is not checked out: git submodule update --init");

        var m = VersionDefine().Match(File.ReadAllText(nriH));
        if (!m.Success)
            throw new InvalidOperationException($"no #define NRI_VERSION in {nriH}");

        var actual = int.Parse(m.Groups["v"].Value, System.Globalization.CultureInfo.InvariantCulture);
        if (actual != PinnedVersion)
            throw new InvalidOperationException(
                $"nri/ is at NRI_VERSION {actual} but Directory.Build.props pins {PinnedVersion}. "
                + "Move the submodule to the pinned tag before measuring anything against it.");

        return include;
    }

    /// <summary>The generated L0 sources, <c>dotnet/Native</c>.</summary>
    public static string NativeSourceDirectory() => Path.Combine(RepoRoot(), "dotnet", "Native");

    private static string FindRoot()
    {
        // The csproj stamps the root it was built from into the assembly. Preferred, because it
        // survives the test binaries being run from somewhere other than under the repo.
        var stamped = typeof(NriHeader).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "RepoRoot")?.Value;

        if (stamped is not null && File.Exists(Path.Combine(stamped, "Directory.Build.props")))
            return Path.GetFullPath(stamped);

        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Directory.Build.props")))
            dir = Path.GetDirectoryName(dir);

        return dir ?? throw new InvalidOperationException(
            "could not locate the repo root (no Directory.Build.props above the test binaries)");
    }

    private static int ReadPin()
    {
        var props = File.ReadAllText(Path.Combine(RepoRoot(), "Directory.Build.props"));
        var m = PinnedVersionElement().Match(props);
        if (!m.Success)
            throw new InvalidOperationException("<NriVersion> not found in Directory.Build.props");

        return int.Parse(m.Groups["v"].Value.Trim(), System.Globalization.CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"<NriVersion>\s*(?<v>\d+)\s*</NriVersion>")]
    private static partial Regex PinnedVersionElement();

    [GeneratedRegex(@"#define\s+NRI_VERSION\s+(?<v>\d+)")]
    private static partial Regex VersionDefine();
}
