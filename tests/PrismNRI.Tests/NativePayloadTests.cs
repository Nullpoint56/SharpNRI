// The staged native payloads (tools/build-native.ps1) are part of the package contract
// (docs/design.md §3.6): every file below ships, and nothing else does. The lists mirror the summary
// tables build-native.ps1 prints; change both together.
//
//   Base       runtimes/<rid>/native             -> PrismNRI
//   Upscalers  runtimes-upscalers/<rid>/native   -> PrismNRI.Native.Upscalers
//   Agility    runtimes-agility/<rid>/native     -> PrismNRI.Native.Agility
//
// Base is the default and is always expected; the other two skip with the command that stages them.
// No vendor upscaler runtime (nvngx_*, amd_fidelityfx_*, libxess) is in any of them: the application
// ships those. The -WithDlss build stages into runtimes-upscalers-dlss/ and has no contract here.

using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace PrismNRI.Tests;

public sealed class NativePayloadTests
{
    static readonly string RepoRoot = typeof(NativePayloadTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "RepoRoot").Value!;

    static string Rid => (OperatingSystem.IsWindows() ? "win-" : OperatingSystem.IsLinux() ? "linux-" : "unsupported-")
        + (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64");

    static string Dir(string stagingRoot) => Path.GetFullPath(Path.Combine(RepoRoot, stagingRoot, Rid, "native"));

    static readonly string[] CommonLicenses =
    [
        "licenses/THIRD-PARTY-NOTICES.md",
        "licenses/NRI/LICENSE.txt",
        "licenses/NVTX/LICENSE.txt",
        "licenses/Vulkan-Headers/LICENSE.md",
        "licenses/Vulkan-Headers/Apache-2.0.txt",
        "licenses/Vulkan-Headers/MIT.txt",
        "licenses/VulkanMemoryAllocator/LICENSE.txt",
    ];

    static readonly string[] WindowsBase =
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

    static readonly string[] NisLicenses =
    [
        "licenses/NVIDIA-Image-Scaling/LICENSE.txt",
        "licenses/ShaderMake/LICENSE.txt",
    ];

    static readonly Dictionary<string, string[]> Base = new()
    {
        ["win-x64"] = WindowsBase,
        ["linux-x64"] = ["libNRI.so", .. CommonLicenses],
    };

    static readonly Dictionary<string, string[]> Upscalers = new()
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

    static readonly Dictionary<string, string[]> Agility = new()
    {
        ["win-x64"] =
        [
            "AgilitySDK/D3D12Core.dll",
            "AgilitySDK/d3d12SDKLayers.dll",
            "AgilitySDK/LICENSE.txt",
        ],
    };

    // Vendor upscaler runtimes: the application's to ship, under the vendors' terms. In no package.
    static readonly string[] VendorRuntimePrefixes = ["nvngx", "libnvidia-ngx", "amd_fidelityfx", "libxess", "libxell"];

    static string? SkipReason(Dictionary<string, string[]> contract, string stagingRoot, string command) =>
        !contract.ContainsKey(Rid) ? $"No {stagingRoot} payload contract for {Rid}."
        : !Directory.Exists(Dir(stagingRoot)) ? $"{Dir(stagingRoot)} is absent. Run: pwsh tools/build-native.ps1 -Rid {Rid}{command}"
        : null;

    sealed class BaseFactAttribute : FactAttribute
    {
        public BaseFactAttribute() => Skip = SkipReason(Base, "runtimes", "");
    }

    sealed class UpscalersFactAttribute : FactAttribute
    {
        public UpscalersFactAttribute() => Skip = SkipReason(Upscalers, "runtimes-upscalers", " -Variant Upscalers");
    }

    sealed class AgilityFactAttribute : FactAttribute
    {
        public AgilityFactAttribute() => Skip = SkipReason(Agility, "runtimes-agility", "");
    }

    static void AssertExactly(string dir, string[] expected)
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

    [AgilityFact]
    public void Agility_is_exactly_the_contract() => AssertExactly(Dir("runtimes-agility"), Agility[Rid]);

    // Neither NRI.dll may need a vendor runtime to load: a load-time import of one would make the
    // whole library fail to load on a machine without it. libxess.dll is delay-loaded in Upscalers.
    [BaseFact]
    public void Base_NRI_imports_no_vendor_runtime()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "PE imports are a Windows question");
        var (imports, delayed) = ReadImports(Path.Combine(Dir("runtimes"), "NRI.dll"));
        Assert.DoesNotContain(imports.Concat(delayed), IsVendorRuntime);
    }

    [UpscalersFact]
    public void Upscalers_NRI_delay_loads_libxess_and_imports_no_vendor_runtime()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "PE imports are a Windows question");
        var (imports, delayed) = ReadImports(Path.Combine(Dir("runtimes-upscalers"), "NRI.dll"));
        Assert.DoesNotContain(imports, IsVendorRuntime);
        Assert.Contains("libxess.dll", delayed, StringComparer.OrdinalIgnoreCase);
    }

    static bool IsVendorRuntime(string dll) => VendorRuntimePrefixes.Any(p => dll.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    // Import directory: 20-byte descriptors, name RVA at +12. Delay-import directory: 32-byte
    // descriptors, name RVA at +4. Both end with an all-zero descriptor.
    static (List<string> Imports, List<string> Delayed) ReadImports(string path)
    {
        using var pe = new PEReader(File.OpenRead(path));
        var headers = pe.PEHeaders.PEHeader!;
        return (Names(pe, headers.ImportTableDirectory, 20, 12), Names(pe, headers.DelayImportTableDirectory, 32, 4));
    }

    static List<string> Names(PEReader pe, DirectoryEntry directory, int stride, int nameOffset)
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
