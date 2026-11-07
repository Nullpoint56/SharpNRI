// The staged native payloads (tools/build-native.ps1) are part of the package contract
// (AGENTS.md: Native packages): every file below ships, and nothing else does. The lists mirror the summary
// tables build-native.ps1 prints; change both together. osx-arm64 has a Base contract only: the
// script builds no other variant there.
//
//   Base       runtimes/<rid>/native             -> SharpNRI
//   Upscalers  runtimes-upscalers/<rid>/native   -> SharpNRI.Native.Upscalers
//   Agility    runtimes-agility/<rid>/native     -> no package: the repository's tests only
//
// Base is the default and is always expected; the other two skip with the command that stages them.
// No vendor upscaler runtime (nvngx_*, amd_fidelityfx_*, libxess) is in any of them: the application
// ships those. The -WithDlss build stages into runtimes-upscalers-dlss/ and has no contract here.

using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace SharpNRI.Tests;

public sealed class NativePayloadTests
{
    private static readonly string RepoRoot = typeof(NativePayloadTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "RepoRoot").Value!;

    private static string Rid => (OperatingSystem.IsWindows() ? "win-" : OperatingSystem.IsLinux() ? "linux-" : OperatingSystem.IsMacOS() ? "osx-" : "unsupported-")
        + (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64");

    private static string Dir(string stagingRoot) => Path.GetFullPath(Path.Combine(RepoRoot, stagingRoot, Rid, "native"));

    private static readonly string[] CommonLicenses =
    [
        "licenses/THIRD-PARTY-NOTICES.md",
        "licenses/NRI/LICENSE.txt",
        "licenses/NVTX/LICENSE.txt",
        "licenses/Vulkan-Headers/LICENSE.md",
        "licenses/Vulkan-Headers/Apache-2.0.txt",
        "licenses/Vulkan-Headers/MIT.txt",
        "licenses/VulkanMemoryAllocator/LICENSE.txt",
    ];

    private static readonly string[] WindowsBase =
    [
        "NRI.dll",
        "amd_ags_x64.dll",
        .. CommonLicenses,
        "licenses/D3D12MemoryAllocator/LICENSE.txt",
        "licenses/D3D12MemoryAllocator/NOTICES.txt",
        "licenses/DirectX-AgilitySDK/LICENSE-CODE.txt",
        "licenses/NVAPI/License.txt",
        "licenses/AMD-AGS/LICENSE.txt",
    ];

    private static readonly string[] NisLicenses =
    [
        "licenses/NVIDIA-Image-Scaling/LICENSE.txt",
        "licenses/ShaderMake/LICENSE.txt",
    ];

    private static readonly Dictionary<string, string[]> Base = new()
    {
        ["win-x64"] = WindowsBase,
        ["linux-x64"] = ["libNRI.so", .. CommonLicenses],
        ["osx-arm64"] = ["libNRI.dylib", .. CommonLicenses],
    };

    private static readonly Dictionary<string, string[]> Upscalers = new()
    {
        ["win-x64"] =
        [
            .. WindowsBase,
            .. NisLicenses,
            "licenses/AMD-FSR-SDK/license.md",
            "licenses/AMD-FSR-SDK/3rdpartynotice.md",
            "licenses/Intel-XeSS/LICENSE.txt",
            "licenses/Intel-XeSS/third-party-programs.txt",
        ],
        ["linux-x64"] = ["libNRI.so", .. CommonLicenses, .. NisLicenses],
    };

    private static readonly Dictionary<string, string[]> Agility = new()
    {
        ["win-x64"] =
        [
            "AgilitySDK/D3D12Core.dll",
            "AgilitySDK/d3d12SDKLayers.dll",
            "AgilitySDK/LICENSE.txt",
        ],
    };

    // Vendor upscaler runtimes: the application's to ship, under the vendors' terms. In no package.
    private static readonly string[] VendorRuntimePrefixes = ["nvngx", "libnvidia-ngx", "amd_fidelityfx", "libxess", "libxell"];

    private static string? SkipReason(Dictionary<string, string[]> contract, string stagingRoot, string command) =>
        !contract.ContainsKey(Rid) ? $"No {stagingRoot} payload contract for {Rid}."
        : !Directory.Exists(Dir(stagingRoot)) ? $"{Dir(stagingRoot)} is absent. Run: pwsh tools/build-native.ps1 -Rid {Rid}{command}"
        : null;

    private sealed class BaseFactAttribute : FactAttribute
    {
        public BaseFactAttribute([System.Runtime.CompilerServices.CallerFilePath] string? sourceFilePath = null, [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber) => Skip = SkipReason(Base, "runtimes", "");
    }

    private sealed class UpscalersFactAttribute : FactAttribute
    {
        public UpscalersFactAttribute([System.Runtime.CompilerServices.CallerFilePath] string? sourceFilePath = null, [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber) => Skip = SkipReason(Upscalers, "runtimes-upscalers", " -Variant Upscalers");
    }

    private sealed class AgilityFactAttribute : FactAttribute
    {
        public AgilityFactAttribute([System.Runtime.CompilerServices.CallerFilePath] string? sourceFilePath = null, [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = -1)
            : base(sourceFilePath, sourceLineNumber) => Skip = SkipReason(Agility, "runtimes-agility", "");
    }

    private static void AssertExactly(string dir, string[] expected)
    {
        var missing = expected
            .Where(f => !File.Exists(Path.Combine(dir, f)) || new FileInfo(Path.Combine(dir, f)).Length == 0)
            .ToList();
        Assert.True(missing.Count == 0, $"missing or empty in {dir}: {string.Join(", ", missing)}");

        var actual = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(dir, f).Replace('\\', '/'))
            .ToList();
        var extra = actual.Except(expected).ToList();
        Assert.True(extra.Count == 0, $"staged in {dir} but not in the payload contract: {string.Join(", ", extra)}");
        Assert.DoesNotContain(actual, f => VendorRuntimePrefixes.Any(p => Path.GetFileName(f).StartsWith(p, StringComparison.OrdinalIgnoreCase)));
    }

    [BaseFact]
    public void Base_is_exactly_the_contract() => AssertExactly(Dir("runtimes"), Base[Rid]);

    [UpscalersFact]
    public void Upscalers_is_exactly_the_contract() => AssertExactly(Dir("runtimes-upscalers"), Upscalers[Rid]);

    /// <summary>
    /// upscaler-runtimes.json beside the Upscalers payload (the package carries it, the release notes
    /// list it) names each upscaler SDK at NRI's pin, and on Windows each runtime NRI's build fetched,
    /// with a file version and SHA-256.
    /// </summary>
    [UpscalersFact]
    public void Upscalers_records_the_vendor_runtimes_it_was_built_against()
    {
        var path = Path.Combine(RepoRoot, "runtimes-upscalers", Rid, "upscaler-runtimes.json");
        Assert.True(File.Exists(path), $"{path} is absent. Run: pwsh tools/build-native.ps1 -Rid {Rid} -Variant Upscalers");
        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var root = manifest.RootElement;
        Assert.Equal(NriHeader.PinnedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), root.GetProperty("nri").GetString());
        Assert.Equal(Rid, root.GetProperty("rid").GetString());

        var cmake = File.ReadAllText(Path.Combine(RepoRoot, "nri", "CMakeLists.txt"));
        string Pin(string pattern) => System.Text.RegularExpressions.Regex.Match(cmake, pattern).Groups[1].Value;
        var sdks = root.GetProperty("sdks").EnumerateArray().ToDictionary(s => s.GetProperty("name").GetString()!);
        Assert.Equal(Pin(@"set\(NGX_VERSION ""([\d.]+)""\)"), sdks["NVIDIA DLSS (NGX)"].GetProperty("version").GetString());
        Assert.Contains("NVIDIA Image Scaling", sdks.Keys);
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(Pin(@"FidelityFX-SDK/archive/refs/tags/v([\d.]+)\.zip"), sdks["AMD FidelityFX SDK"].GetProperty("version").GetString());
            Assert.Equal(Pin(@"intel/xess/archive/refs/tags/v([\d.]+)\.zip"), sdks["Intel XeSS SDK"].GetProperty("version").GetString());
            // What NRI loads (NriUpscalerExtension.GetRuntimeFiles) is what the manifest records
            string[] Files(string sdk) => [.. sdks[sdk].GetProperty("runtimes").EnumerateArray().Select(r => r.GetProperty("file").GetString()!)];
            foreach (var api in new[] { SharpNRI.Native.GraphicsAPI.D3D12, SharpNRI.Native.GraphicsAPI.VK })
                Assert.Subset(Files("AMD FidelityFX SDK").ToHashSet(), NriUpscalerExtension.GetRuntimeFiles(SharpNRI.Native.UpscalerType.FSR, api).ToHashSet());
            Assert.Subset(Files("Intel XeSS SDK").ToHashSet(), NriUpscalerExtension.GetRuntimeFiles(SharpNRI.Native.UpscalerType.XESS, SharpNRI.Native.GraphicsAPI.D3D12).ToHashSet());
        }
        foreach (var runtime in sdks.Values.SelectMany(s => s.GetProperty("runtimes").EnumerateArray()))
        {
            Assert.Matches(@"^\d+(\.\d+)+$", runtime.GetProperty("version").GetString());
            Assert.Matches("^[0-9a-f]{64}$", runtime.GetProperty("sha256").GetString());
        }
        GpuWork.Record(string.Join("; ", sdks.Values.Select(s => $"{s.GetProperty("name").GetString()} {s.GetProperty("version").GetString()}: "
            + string.Join(", ", s.GetProperty("runtimes").EnumerateArray().Select(r => $"{r.GetProperty("file").GetString()} {r.GetProperty("version").GetString()}")))));
    }

    [AgilityFact]
    public void Agility_is_exactly_the_contract() => AssertExactly(Dir("runtimes-agility"), Agility[Rid]);

    // What each NRI.dll loads with it: the system, the VC++ runtime (required, not shipped) and the
    // UCRT, nothing else. A load-time import of a vendor runtime would make the whole library fail
    // to load on a machine without it. d3d12.dll and dxgi.dll are delay-loaded in both variants
    // (tools/cmake/delayload.cmake), so a Vulkan-only process never loads them
    // (AdapterEnumerationTests); libxess.dll is delay-loaded in Upscalers.
    private static readonly string[] LoadTimeImports =
    [
        "KERNEL32.dll",
        "USER32.dll",
        "MSVCP140.dll",
        "VCRUNTIME140.dll",
        "VCRUNTIME140_1.dll",
        "api-ms-win-crt-string-l1-1-0.dll",
        "api-ms-win-crt-heap-l1-1-0.dll",
        "api-ms-win-crt-utility-l1-1-0.dll",
        "api-ms-win-crt-environment-l1-1-0.dll",
        "api-ms-win-crt-convert-l1-1-0.dll",
        "api-ms-win-crt-stdio-l1-1-0.dll",
        "api-ms-win-crt-math-l1-1-0.dll",
        "api-ms-win-crt-runtime-l1-1-0.dll",
    ];

    private static readonly string[] BaseDelayLoaded = ["d3d12.dll", "dxgi.dll"];

    [BaseFact]
    public void Base_NRI_imports_exactly_the_contract_and_delay_loads_D3D()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "PE imports are a Windows question");
        var (imports, delayed) = ReadImports(Path.Combine(Dir("runtimes"), "NRI.dll"));
        AssertSameNames(LoadTimeImports, imports, "load-time imports");
        AssertSameNames(BaseDelayLoaded, delayed, "delay-loaded imports");
        Assert.DoesNotContain(imports.Concat(delayed), IsVendorRuntime);
    }

    [UpscalersFact]
    public void Upscalers_NRI_imports_exactly_the_contract_and_delay_loads_D3D_and_libxess()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "PE imports are a Windows question");
        var (imports, delayed) = ReadImports(Path.Combine(Dir("runtimes-upscalers"), "NRI.dll"));
        AssertSameNames(LoadTimeImports, imports, "load-time imports");
        AssertSameNames([.. BaseDelayLoaded, "libxess.dll"], delayed, "delay-loaded imports");
        Assert.DoesNotContain(imports, IsVendorRuntime);
    }

    private static void AssertSameNames(string[] expected, List<string> actual, string what)
    {
        var missing = expected.Except(actual, StringComparer.OrdinalIgnoreCase).ToList();
        var extra = actual.Except(expected, StringComparer.OrdinalIgnoreCase).ToList();
        Assert.True(missing.Count == 0 && extra.Count == 0,
            $"NRI.dll {what}: missing {string.Join(", ", missing)}; not in the contract {string.Join(", ", extra)} (all: {string.Join(", ", actual)})");
    }

    private static bool IsVendorRuntime(string dll) => VendorRuntimePrefixes.Any(p => dll.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    // Import directory: 20-byte descriptors, name RVA at +12. Delay-import directory: 32-byte
    // descriptors, name RVA at +4. Both end with an all-zero descriptor.
    private static (List<string> Imports, List<string> Delayed) ReadImports(string path)
    {
        using var pe = new PEReader(File.OpenRead(path));
        var headers = pe.PEHeaders.PEHeader!;
        return (Names(pe, headers.ImportTableDirectory, 20, 12), Names(pe, headers.DelayImportTableDirectory, 32, 4));
    }

    private static List<string> Names(PEReader pe, DirectoryEntry directory, int stride, int nameOffset)
    {
        var names = new List<string>();
        if (directory.RelativeVirtualAddress == 0) return names;
        var table = pe.GetSectionData(directory.RelativeVirtualAddress).GetReader();
        for (var offset = 0; offset + stride <= table.Length; offset += stride)
        {
            table.Offset = offset + nameOffset;
            var nameRva = table.ReadInt32();
            if (nameRva == 0) break;
            var name = pe.GetSectionData(nameRva).GetReader();
            var bytes = new List<byte>();
            for (byte b; name.RemainingBytes > 0 && (b = name.ReadByte()) != 0;) bytes.Add(b);
            names.Add(Encoding.ASCII.GetString([.. bytes]));
        }
        return names;
    }
}
