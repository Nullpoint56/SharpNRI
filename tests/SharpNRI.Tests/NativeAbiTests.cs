// Does the C# layout match what a C++ compiler makes of the same NRI headers?
//
// Nothing else can see a layout error. If ClangSharp put a field at the wrong offset, sized a struct
// wrongly, packed a bitfield the other way round, or dropped a parameter from a function pointer,
// the code compiles, the library loads, the dispatch table fills in, and NRI reads a different field
// than the one that was written. Regenerating cannot notice: it compares the generator against
// itself.
//
// The only party that can settle it is a C++ compiler reading the same headers. So this generates a
// probe from the C# sources, compiles it against the pinned nri/Include in C++ mode (the mode the
// L0 layer was generated in), runs it, and compares:
//
//   * sizeof and alignof for every generated struct and union, including the anonymous records
//     nested inside DeviceDesc and friends, reached through the member that declares them
//   * offsetof for every named field, members of anonymous unions included
//   * sizeof of every fixed buffer, and sizeof/signedness of every enum's underlying type
//   * the exact bytes each bitfield property writes, byte for byte, against the C++ bitfield
//   * every function-pointer field and every DllImport, as a static_assert that the C++ declared
//     type, reduced to its ABI shape, is the type the C# signature implies
//
// The C# side is read as source text where it carries information reflection cannot see:
// NativeTypeNameAttribute is [Conditional("DEBUG")], so in a Release build the annotations that
// mark a property as a bitfield are not in the assembly at all.
//
// Skips with a reason where no C++ compiler is installed.

using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

/// <summary>Finds a C++ compiler and runs a generated probe through it.</summary>
public static class NativeAbi
{
    /// <summary>Path to a usable C++ compiler, or null.</summary>
    public static string? Compiler => LazyCompiler.Value;

    public static string Unavailable =>
        "no C++ compiler found. Tried clang++, g++ and c++ on PATH plus the default LLVM install "
        + "directory. Install one (winget install LLVM.LLVM, apt install clang, or Xcode command "
        + "line tools) to check the binding's struct layout against the NRI headers.";

    /// <summary>Where generated probes and their binaries go. Never inside nri/.</summary>
    public static string WorkDirectory => Path.Combine(Path.GetTempPath(), "sharpnri-abi");

    private static readonly Lazy<string?> LazyCompiler = new(Find);

    private static bool IsClang => Compiler is { } c
        && Path.GetFileName(c).Contains("clang", StringComparison.OrdinalIgnoreCase);

    private static string? Find()
    {
        var candidates = new List<string> { "clang++", "g++", "c++" };

        if (OperatingSystem.IsWindows())
        {
            candidates.Add(@"C:\Program Files\LLVM\bin\clang++.exe");
            candidates.Add(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "LLVM", "bin", "clang++.exe"));
        }

        // By running it, not by looking for the file: a broken shim on PATH and an absent compiler
        // are the same thing to this test, and only one of them is visible to File.Exists.
        foreach (var candidate in candidates)
        {
            try
            {
                using var probe = Process.Start(new ProcessStartInfo(candidate, "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                });

                if (probe is null) continue;
                probe.StandardOutput.ReadToEnd();
                probe.StandardError.ReadToEnd();
                probe.WaitForExit(20_000);
                if (probe.ExitCode == 0) return candidate;
            }
            catch (Exception)
            {
                // Not present, or not executable. Try the next one.
            }
        }

        return null;
    }

    /// <summary>Compile and run one probe source; returns its stdout, or null on failure.</summary>
    /// <param name="source">The C++ to build.</param>
    /// <param name="name">Base name for the generated files, inside <see cref="WorkDirectory"/>.</param>
    /// <param name="diagnostics">The compiler's output, whether or not it succeeded.</param>
    public static string? Build(string source, string name, out string diagnostics)
    {
        Directory.CreateDirectory(WorkDirectory);

        var cpp = Path.Combine(WorkDirectory, $"{name}.cpp");
        var exe = Path.Combine(WorkDirectory, OperatingSystem.IsWindows() ? $"{name}.exe" : name);
        File.WriteAllText(cpp, source);
        if (File.Exists(exe)) File.Delete(exe);

        // C++ mode, NRI's default when __cplusplus is defined; NRI_FORCE_C deliberately absent,
        // because the L0 layer was generated with `--language c++` and C mode is a different
        // surface (enums become integer typedefs). No windows.h: NRIWrapperD3D12.h forward-declares
        // the ID3D12* types itself, and windows.h would only bring in macros that collide.
        List<string> arguments = ["-std=c++17", "-I", NriHeader.IncludeDirectory()];

        // Report every failed static_assert, not the first twenty: the point is one complete list.
        arguments.Add(IsClang ? "-ferror-limit=0" : "-fmax-errors=0");

        // NRI_CALL is __stdcall on _WIN32, which x64 ignores and clang says so once per use.
        if (IsClang) arguments.Add("-Wno-ignored-attributes");

        arguments.AddRange([cpp, "-o", exe]);

        var compile = Run(Compiler!, arguments);
        diagnostics = compile.Output;
        if (compile.ExitCode != 0 || !File.Exists(exe)) return null;

        var run = Run(exe, []);
        if (run.ExitCode != 0)
        {
            diagnostics = $"the probe compiled but exited {run.ExitCode}:\n{run.Output}";
            return null;
        }

        return run.Output;
    }

    private static (int ExitCode, string Output) Run(string file, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in arguments) info.ArgumentList.Add(a);

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"could not start {file}");

        // Both streams drained concurrently: a probe that fills one pipe while this blocks on the
        // other would deadlock, and the signature probes can produce a lot of stderr.
        var stderr = process.StandardError.ReadToEndAsync();
        var stdout = process.StandardOutput.ReadToEnd();

        if (!process.WaitForExit(300_000))
        {
            process.Kill(entireProcessTree: true);
            return (-1, "timed out after 300s");
        }

        return (process.ExitCode, stdout + stderr.GetAwaiter().GetResult());
    }
}

public sealed partial class NativeAbiTests(ITestOutputHelper output)
{
    // ================================================================ what to probe

    /// <summary>One struct or union to measure: its C++ spelling and its C# type.</summary>
    /// <param name="Key">How it is named in reports: a type name, or a member path for an anonymous record.</param>
    /// <param name="Cpp">A C++ type-id for it. Anonymous records are reached as <c>decltype(Outer::member)</c>.</param>
    private sealed record StructProbe(string Key, string Cpp, Type Type);

    /// <summary>One named field and where C# puts it, relative to the struct it is measured against.</summary>
    private sealed record FieldProbe(StructProbe Owner, string Name, long CsOffset);

    /// <summary>One <c>[InlineArray]</c> fixed buffer and the C++ array it stands for.</summary>
    private sealed record FixedBufferProbe(string Key, string Cpp, Type Type);

    private sealed record Surface(
        List<StructProbe> Structs,
        List<FieldProbe> Fields,
        List<FixedBufferProbe> FixedBuffers,
        List<Type> Enums);

    private static readonly Lazy<Surface> LazySurface = new(Enumerate);

    private static Surface Probed => LazySurface.Value;

    private static IEnumerable<Type> GeneratedTypes() =>
        typeof(Device).Assembly.GetTypes()
            .Where(t => t.Namespace == "SharpNRI.Native" && !t.IsNested)
            .OrderBy(t => t.Name, StringComparer.Ordinal);

    private static List<FieldInfo> Fields(Type t) =>
        [.. t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)];

    private static bool IsInlineArray(Type t) => t.GetCustomAttribute<InlineArrayAttribute>() is not null;

    /// <summary>
    /// ClangSharp's name for an anonymous union or struct that C++ declares without a member name.
    /// </summary>
    /// <remarks>
    /// <c>BottomLevelGeometryDesc</c> has one: in C++ <c>triangles</c> and <c>aabbs</c> are members
    /// of the outer struct, so they are measured as such, at the anonymous member's offset plus
    /// their own. A named anonymous record (<c>DeviceDesc::viewport</c>) is different: it has a
    /// member name, so it is measured as <c>decltype(DeviceDesc::viewport)</c>.
    /// </remarks>
    private static bool IsAnonymousMember(FieldInfo f) =>
        f.Name.StartsWith("Anonymous", StringComparison.Ordinal);

    private static Surface Enumerate()
    {
        var surface = new Surface([], [], [], []);

        foreach (var type in GeneratedTypes())
        {
            if (type.IsEnum)
            {
                surface.Enums.Add(type);
                continue;
            }

            // A struct with no fields is an opaque handle (Device, Buffer, ID3D12Device, ...):
            // declared and never defined in the headers, so sizeof on it does not compile.
            if (!type.IsValueType || IsInlineArray(type) || Fields(type).Count == 0) continue;

            Walk(surface, type, type.Name, $"nri::{type.Name}");
        }

        return surface;
    }

    private static void Walk(Surface surface, Type type, string key, string cpp)
    {
        var owner = new StructProbe(key, cpp, type);
        surface.Structs.Add(owner);
        AddFields(surface, owner, type, 0);
    }

    private static void AddFields(Surface surface, StructProbe owner, Type type, long baseOffset)
    {
        foreach (var field in Fields(type))
        {
            // _bitfieldN is the storage word ClangSharp synthesises; C++ has no member of that
            // name. The bits are checked directly, below, and the word's position by the struct's
            // size and the next field's offset.
            if (field.Name.StartsWith("_bitfield", StringComparison.Ordinal)) continue;

            var offset = baseOffset + (long)Marshal.OffsetOf(type, field.Name);

            if (IsAnonymousMember(field))
            {
                AddFields(surface, owner, field.FieldType, offset);
                continue;
            }

            surface.Fields.Add(new FieldProbe(owner, field.Name, offset));

            var fieldType = field.FieldType;
            if (!fieldType.IsNested || fieldType.Namespace != "SharpNRI.Native") continue;

            var nestedKey = $"{owner.Key}.{field.Name}";
            var nestedCpp = $"decltype({owner.Cpp}::{field.Name})";

            if (IsInlineArray(fieldType))
                surface.FixedBuffers.Add(new FixedBufferProbe(nestedKey, nestedCpp, fieldType));
            else
                Walk(surface, fieldType, nestedKey, nestedCpp);
        }
    }

    // ---------------------------------------------------------------- bitfields, from the source

    /// <summary>Bitfield properties of a top-level type, with the width the header declares.</summary>
    /// <remarks>
    /// ClangSharp turns <c>uint32_t instanceId : 24</c> into a <c>_bitfield1</c> word plus a property
    /// doing its own shifting and masking, annotated <c>[NativeTypeName("uint32_t : 24")]</c>. That
    /// hand-rolled arithmetic is the most error-prone thing in the generated layer, and the struct
    /// stays the right size whichever way the bits go. Read from source, not metadata, because the
    /// annotation is <c>[Conditional("DEBUG")]</c> and absent from a Release assembly.
    /// </remarks>
    private static IReadOnlyList<(string Name, int Width)> Bitfields(Type t) =>
        LazyBitfields.Value.TryGetValue(t.Name, out var found) ? found : [];

    private static readonly Lazy<Dictionary<string, List<(string, int)>>> LazyBitfields = new(() =>
    {
        var result = new Dictionary<string, List<(string, int)>>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(NriHeader.NativeSourceDirectory(), "*.cs"))
        {
            // ClangSharp's file=multi mode names each file after the type it declares.
            var type = Path.GetFileNameWithoutExtension(file);

            foreach (Match m in BitfieldProperty().Matches(File.ReadAllText(file)))
            {
                if (!result.TryGetValue(type, out var list)) result[type] = list = [];
                list.Add((m.Groups["name"].Value,
                    int.Parse(m.Groups["width"].Value, CultureInfo.InvariantCulture)));
            }
        }

        return result;
    });

    [GeneratedRegex(@"\[NativeTypeName\(""[^""]*:\s*(?<width>\d+)""\)\]\s*public\s+[\w\.]+\s+(?<name>\w+)\s*\r?\n\s*\{")]
    private static partial Regex BitfieldProperty();

    // ================================================================ the layout probe

    private static string LayoutProbe()
    {
        var cpp = new StringBuilder();
        cpp.AppendLine("// Generated by SharpNRI.Tests/NativeAbiTests. Ground truth for the binding's layout.");
        AppendIncludes(cpp);
        cpp.AppendLine("#include <stdio.h>");
        cpp.AppendLine("#include <string.h>");
        cpp.AppendLine("#include <type_traits>");
        cpp.AppendLine("using namespace nri;");
        cpp.AppendLine("int main() {");

        foreach (var s in Probed.Structs)
        {
            cpp.AppendLine($"  {{ using T_ = {s.Cpp};");
            cpp.AppendLine($"    printf(\"S|{s.Key}|%zu|%zu\\n\", sizeof(T_), alignof(T_));");

            foreach (var f in Probed.Fields.Where(f => ReferenceEquals(f.Owner, s)))
                cpp.AppendLine($"    printf(\"F|{s.Key}|{f.Name}|%zu\\n\", offsetof(T_, {f.Name}));");

            cpp.AppendLine("  }");
        }

        foreach (var b in Probed.FixedBuffers)
            cpp.AppendLine($"  printf(\"A|{b.Key}|%zu\\n\", sizeof({b.Cpp}));");

        foreach (var e in Probed.Enums)
        {
            cpp.AppendLine($"  printf(\"E|{e.Name}|%zu|%d\\n\", sizeof({e.Name}), "
                + $"(int)std::is_signed<std::underlying_type<{e.Name}>::type>::value);");
        }

        // Bitfields: write all-ones into one, then print the struct's bytes. Where the bits land is
        // the whole question, and a byte pattern answers it without needing to know how the
        // compiler names or orders the storage.
        foreach (var s in Probed.Structs.Where(s => !s.Type.IsNested))
        {
            foreach (var (name, width) in Bitfields(s.Type))
            {
                var ones = width >= 64 ? ulong.MaxValue : (1UL << width) - 1;
                cpp.AppendLine($"  {{ {s.Cpp} v; memset(&v, 0, sizeof v); "
                    + $"v.{name} = (decltype(v.{name}))({ones}ull); "
                    + $"unsigned char b[sizeof v]; memcpy(b, &v, sizeof v); "
                    + $"printf(\"B|{s.Key}|{name}|\"); "
                    + "for (size_t i = 0; i < sizeof v; i++) printf(\"%02x\", b[i]); printf(\"\\n\"); }");
            }
        }

        // EXTERNAL_QUEUE must be assignable to the barrier descs' Queue* fields: as a pointer to a const
        // Queue it was not (MSVC C2440; NRI PR #294 review, item 3).
        cpp.AppendLine("  { Queue* externalQueue_ = EXTERNAL_QUEUE; (void)externalQueue_; }");
        cpp.AppendLine("  return 0;");
        cpp.AppendLine("}");
        return cpp.ToString();
    }

    /// <summary>The umbrella the generator used: tools/generate-native.ps1's $headers, in order.</summary>
    private static readonly string[] Headers =
    [
        "NRI.h",
        "Extensions/NRIDeviceCreation.h", "Extensions/NRIHelper.h", "Extensions/NRIDescriptorHeap.h",
        "Extensions/NRIImgui.h", "Extensions/NRILowLatency.h", "Extensions/NRIMeshShader.h",
        "Extensions/NRIRayTracing.h", "Extensions/NRIStreamer.h", "Extensions/NRISwapChain.h",
        "Extensions/NRIUpscaler.h", "Extensions/NRIVideo.h",
        "Extensions/NRIWrapperVK.h", "Extensions/NRIWrapperD3D12.h",
    ];

    private static void AppendIncludes(StringBuilder cpp)
    {
        cpp.AppendLine("#include <stddef.h>");
        cpp.AppendLine("#include <stdint.h>");
        foreach (var h in Headers) cpp.AppendLine($"#include \"{h}\"");
    }

    // ---------------------------------------------------------------- the measurement

    private sealed record Measured(
        Dictionary<string, (long Size, long Align)> Structs,
        Dictionary<string, long> Offsets,
        Dictionary<string, long> FixedBuffers,
        Dictionary<string, (long Size, bool Signed)> Enums,
        Dictionary<string, string> BitPatterns);

    private static readonly Lazy<(Measured? Truth, string Diagnostics)> LazyTruth = new(() =>
    {
        var stdout = NativeAbi.Build(LayoutProbe(), "layout", out var diagnostics);
        if (stdout is null) return (null, diagnostics);

        var m = new Measured(new(StringComparer.Ordinal), new(StringComparer.Ordinal),
            new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal));

        static long L(string s) => long.Parse(s, CultureInfo.InvariantCulture);

        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = line.Trim().Split('|');
            switch (p[0])
            {
                case "S" when p.Length == 4: m.Structs[p[1]] = (L(p[2]), L(p[3])); break;
                case "F" when p.Length == 4: m.Offsets[$"{p[1]}.{p[2]}"] = L(p[3]); break;
                case "A" when p.Length == 3: m.FixedBuffers[p[1]] = L(p[2]); break;
                case "E" when p.Length == 4: m.Enums[p[1]] = (L(p[2]), p[3] == "1"); break;
                case "B" when p.Length == 4: m.BitPatterns[$"{p[1]}.{p[2]}"] = p[3]; break;
            }
        }

        return (m, diagnostics);
    });

    private static Measured Truth =>
        LazyTruth.Value.Truth ?? throw new InvalidOperationException(
            "the layout probe did not build. It is in " + NativeAbi.WorkDirectory + ":\n"
            + LazyTruth.Value.Diagnostics);

    private static int ClrSize(Type t) =>
        (int)typeof(Unsafe).GetMethod(nameof(Unsafe.SizeOf))!.MakeGenericMethod(t).Invoke(null, null)!;

    // ================================================================ the tests: layout

    [AbiFact]
    public void The_probe_builds_and_measures_the_whole_generated_surface()
    {
        var p = Probed;
        var bitfields = p.Structs.Where(s => !s.Type.IsNested).Sum(s => Bitfields(s.Type).Count);
        var topLevel = p.Structs.Count(s => !s.Type.IsNested);
        var unions = p.Structs.Count(s => s.Type.StructLayoutAttribute?.Value == LayoutKind.Explicit);

        output.WriteLine($"compiler: {NativeAbi.Compiler}");
        output.WriteLine($"probe:    {NativeAbi.WorkDirectory}");
        output.WriteLine($"{p.Structs.Count} structs probed ({topLevel} top-level, "
            + $"{p.Structs.Count - topLevel} nested anonymous records, {unions} unions), "
            + $"{p.Fields.Count} field offsets, {p.FixedBuffers.Count} fixed buffers, "
            + $"{p.Enums.Count} enums, {bitfields} bitfields");
        output.WriteLine($"measured: {Truth.Structs.Count} structs, {Truth.Offsets.Count} offsets, "
            + $"{Truth.FixedBuffers.Count} fixed buffers, {Truth.Enums.Count} enums, "
            + $"{Truth.BitPatterns.Count} bitfields");

        var missing = new List<string>();
        missing.AddRange(p.Structs.Where(s => !Truth.Structs.ContainsKey(s.Key)).Select(s => $"struct {s.Key}"));
        missing.AddRange(p.Fields.Where(f => !Truth.Offsets.ContainsKey($"{f.Owner.Key}.{f.Name}"))
            .Select(f => $"field {f.Owner.Key}.{f.Name}"));
        missing.AddRange(p.FixedBuffers.Where(b => !Truth.FixedBuffers.ContainsKey(b.Key)).Select(b => $"buffer {b.Key}"));
        missing.AddRange(p.Enums.Where(e => !Truth.Enums.ContainsKey(e.Name)).Select(e => $"enum {e.Name}"));

        Assert.True(missing.Count == 0,
            $"{missing.Count} probed item(s) produced no measurement:\n"
            + string.Concat(missing.Select(m => $"    {m}\n")));

        // Floors, so a probe that quietly stopped finding things cannot pass by measuring nothing.
        // v190 has 274 generated structs including the anonymous records; the numbers are floors
        // rather than equalities so an NRI bump that adds a struct does not fail here first.
        Assert.True(topLevel >= 200, $"only {topLevel} top-level structs probed");
        Assert.True(p.Structs.Count - topLevel >= 20, $"only {p.Structs.Count - topLevel} nested records probed");
        Assert.True(unions >= 2, $"only {unions} unions probed; Color and ClearValue are both explicit-layout");
        Assert.True(p.Fields.Count >= 1000, $"only {p.Fields.Count} fields probed");
        Assert.True(p.Enums.Count >= 80, $"only {p.Enums.Count} enums probed");
        Assert.True(bitfields >= 4, $"only {bitfields} bitfields found; TopLevelInstance alone has four");
    }

    [AbiFact]
    public void Every_struct_is_the_size_and_alignment_the_C_plus_plus_compiler_gives_it()
    {
        var wrong = new List<string>();

        foreach (var s in Probed.Structs)
        {
            var (size, align) = Truth.Structs[s.Key];

            // Unsafe.SizeOf is the CLR's own size, which pointer arithmetic over arrays of these
            // uses; Marshal.SizeOf is the size Marshal.OffsetOf reports against. They coincide for
            // blittable sequential structs, and this checks that they do — if they ever diverge the
            // offsets below were read off a different layout than the one the binding uses.
            var clr = ClrSize(s.Type);
            var marshalled = Marshal.SizeOf(s.Type);

            if (clr != size)
                wrong.Add($"    {s.Key}: C++ sizeof {size}, C# {clr}\n");
            if (marshalled != clr)
                wrong.Add($"    {s.Key}: CLR size {clr} but marshalled size {marshalled}\n");

            // Alignment is not directly observable in C#. The consequence that matters is checked:
            // a size that is not a multiple of the C++ alignment cannot be right for an array.
            if (align > 0 && clr % align != 0)
                wrong.Add($"    {s.Key}: C# size {clr} is not a multiple of C++ alignof {align}\n");
        }

        foreach (var b in Probed.FixedBuffers)
        {
            var expected = Truth.FixedBuffers[b.Key];
            var actual = ClrSize(b.Type);
            if (actual != expected)
                wrong.Add($"    {b.Key} (fixed buffer): C++ sizeof {expected}, C# {actual}\n");
        }

        output.WriteLine($"{Probed.Structs.Count} struct sizes and {Probed.FixedBuffers.Count} fixed "
            + "buffer sizes compared");

        Assert.True(wrong.Count == 0,
            $"{wrong.Count} size mismatch(es) between the two compilers. Every one is a field read at "
            + "the wrong address:\n" + string.Concat(wrong));
    }

    [AbiFact]
    public void Every_field_is_at_the_offset_the_C_plus_plus_compiler_puts_it()
    {
        var wrong = new List<string>();

        foreach (var f in Probed.Fields)
        {
            var expected = Truth.Offsets[$"{f.Owner.Key}.{f.Name}"];
            if (f.CsOffset != expected)
                wrong.Add($"    {f.Owner.Key}.{f.Name}: C++ offsetof {expected}, C# {f.CsOffset}\n");
        }

        output.WriteLine($"{Probed.Fields.Count} field offsets compared");

        Assert.True(wrong.Count == 0,
            $"{wrong.Count} field(s) are at a different offset in the two layouts:\n" + string.Concat(wrong));
    }

    [AbiFact]
    public void Every_enum_has_the_underlying_type_the_C_plus_plus_compiler_gives_it()
    {
        // Struct offsets already catch a wrongly sized enum *field*. They cannot catch an enum
        // passed by value through a function pointer, where the signature probe below compares
        // names and a name is the same whatever its width. This is the other half.
        var wrong = new List<string>();

        foreach (var e in Probed.Enums)
        {
            var (size, signed) = Truth.Enums[e.Name];
            var underlying = Enum.GetUnderlyingType(e);
            var csSize = Marshal.SizeOf(underlying);
            var csSigned = underlying == typeof(sbyte) || underlying == typeof(short)
                || underlying == typeof(int) || underlying == typeof(long);

            if (csSize != size || csSigned != signed)
                wrong.Add($"    {e.Name}: C++ {(signed ? "signed" : "unsigned")} {size} byte(s), "
                    + $"C# {underlying.Name}\n");
        }

        output.WriteLine($"{Probed.Enums.Count} enum underlying types compared");

        Assert.True(wrong.Count == 0,
            $"{wrong.Count} enum(s) have a different underlying type in the two languages:\n"
            + string.Concat(wrong));
    }

    [AbiFact]
    public void A_bitfield_property_writes_the_bits_the_C_plus_plus_compiler_writes()
    {
        var wrong = new List<string>();
        var compared = 0;

        foreach (var s in Probed.Structs.Where(s => !s.Type.IsNested))
        {
            foreach (var (name, width) in Bitfields(s.Type))
            {
                var expected = Truth.BitPatterns[$"{s.Key}.{name}"];

                var property = s.Type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                Assert.True(property is not null,
                    $"{s.Key}.{name} is annotated as a bitfield in the source but is not a property");

                var ones = width >= 64 ? ulong.MaxValue : (1UL << width) - 1;
                var propertyType = property!.PropertyType;
                var value = ones & MaxOf(propertyType);

                // Same operation on the C# side: a zeroed struct, the property set to all ones,
                // then the raw bytes. SetValue on a box mutates the box.
                var box = Activator.CreateInstance(s.Type)!;
                property.SetValue(box, propertyType.IsEnum
                    ? Enum.ToObject(propertyType, value)
                    : Convert.ChangeType(value, propertyType, CultureInfo.InvariantCulture));

                var bytes = (byte[])typeof(NativeAbiTests)
                    .GetMethod(nameof(BytesOf), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(s.Type).Invoke(null, [box])!;

                var actual = Convert.ToHexString(bytes).ToLowerInvariant();
                compared++;

                output.WriteLine($"{s.Key}.{name} ({width} bits)\n  C++ {expected}\n  C#  {actual}");

                if (actual != expected)
                    wrong.Add($"    {s.Key}.{name} ({width} bits): C++ wrote {expected}, C# wrote {actual}\n");
            }
        }

        Assert.True(compared >= 4,
            $"only {compared} bitfield(s) compared. TopLevelInstance has four — either they are gone "
            + "upstream or the [NativeTypeName(\"... : n\")] annotation they are found by changed.");

        Assert.True(wrong.Count == 0,
            $"{wrong.Count} bitfield accessor(s) put the bits somewhere other than the C++ compiler "
            + "does. The struct is the right size and the reads are wrong:\n" + string.Concat(wrong));
    }

    private static byte[] BytesOf<T>(object boxed) where T : struct
    {
        var value = (T)boxed;
        return MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)).ToArray();
    }

    private static ulong MaxOf(Type t) =>
        Type.GetTypeCode(t) switch
        {
            TypeCode.Byte or TypeCode.SByte => byte.MaxValue,
            TypeCode.UInt16 or TypeCode.Int16 => ushort.MaxValue,
            TypeCode.UInt32 or TypeCode.Int32 => uint.MaxValue,
            _ => ulong.MaxValue,
        };

    // ================================================================ signatures

    /// <summary>One signature to assert: the C++ expression naming it, and the type C# implies.</summary>
    private sealed record Signature(string Label, string CppDeclared, string CsImplied);

    // A function-pointer field: everything from `delegate*` to the last token before the name.
    [GeneratedRegex(@"public\s+(?<type>delegate\s*\*[^;{]*?)\s+(?<name>\w+)\s*;")]
    private static partial Regex FunctionPointerField();

    // One DllImport, from the attribute to the closing paren. Optional [return:] in between.
    [GeneratedRegex(
        @"\[DllImport\([^\]]*\)\]\s*"
        + @"(?:\[return:\s*NativeTypeName\(""[^""]*""\)\]\s*)?"
        + @"public\s+static\s+extern\s+(?<ret>[\w\*]+)\s+(?<name>\w+)\s*\((?<args>[^;]*)\)\s*;",
        RegexOptions.Singleline)]
    private static partial Regex ExternDeclaration();

    [GeneratedRegex(@"\[NativeTypeName\(""[^""]*""\)\]")]
    private static partial Regex Annotation();

    [GeneratedRegex(@"(?<type>[\w\*]+)\s+@?(?<name>\w+)\s*(?:=[^,]*)?$", RegexOptions.Singleline)]
    private static partial Regex CsParameter();

    /// <summary>Every <c>delegate*</c> field in the generated sources, with the C++ type it implies.</summary>
    /// <remarks>
    /// All of them, not only those in the sixteen <c>*Interface</c> structs: the three in
    /// <c>AllocationCallbacks</c> are called by NRI into managed code, which is the direction where
    /// a wrong signature corrupts the caller's stack rather than the callee's.
    /// </remarks>
    private static List<Signature> FunctionPointerFields(out int declared, out List<string> unreadable)
    {
        var result = new List<Signature>();
        declared = 0;
        unreadable = [];

        foreach (var file in Directory.EnumerateFiles(NriHeader.NativeSourceDirectory(), "*.cs")
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            var owner = Path.GetFileNameWithoutExtension(file);
            var text = File.ReadAllText(file);
            declared += CountOccurrences(text, "delegate*");

            var ownerType = typeof(Device).Assembly.GetType($"SharpNRI.Native.{owner}");

            foreach (Match m in FunctionPointerField().Matches(text))
            {
                var name = m.Groups["name"].Value;
                var label = $"{owner}::{name}";

                // The field must be a direct member of the file's type, or the C++ spelling below
                // names something else. A nested struct with a function pointer would land here.
                if (ownerType?.GetField(name, BindingFlags.Public | BindingFlags.Instance) is null)
                {
                    unreadable.Add($"{label} (not a field of {owner})");
                    continue;
                }

                var implied = ToCpp(m.Groups["type"].Value);
                if (implied is null) { unreadable.Add($"{label} ({m.Groups["type"].Value})"); continue; }

                result.Add(new Signature(label, $"decltype(nri::{owner}::{name})", implied));
            }
        }

        return result;
    }

    private static List<Signature> Imports(out int declared, out List<string> unreadable)
    {
        var source = File.ReadAllText(Path.Combine(NriHeader.NativeSourceDirectory(), "NriApi.cs"));
        declared = CountOccurrences(source, "[DllImport(");
        unreadable = [];

        var result = new List<Signature>();

        foreach (Match m in ExternDeclaration().Matches(source))
        {
            var name = m.Groups["name"].Value;
            var ret = ToCpp(m.Groups["ret"].Value);
            var parameters = new List<string>();
            var ok = ret is not null;

            foreach (var raw in SplitTopLevel(m.Groups["args"].Value))
            {
                if (!ok) break;
                var p = Annotation().Replace(raw, "").Trim();
                if (p.Length == 0) continue;
                var mapped = ToCpp(CsParameter().Match(p).Groups["type"].Value);
                if (mapped is null) ok = false;
                else parameters.Add(mapped);
            }

            if (!ok) { unreadable.Add(name); continue; }

            result.Add(new Signature(name, $"decltype(&{name})", Fn(ret!, parameters)));
        }

        return result;
    }

    private static int CountOccurrences(string text, string what)
    {
        var n = 0;
        for (var i = text.IndexOf(what, StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(what, i + what.Length, StringComparison.Ordinal))
            n++;
        return n;
    }

    private static string Fn(string ret, IEnumerable<string> parameters) =>
        $"fn_<{string.Join(", ", parameters.Prepend(ret))}>";

    /// <summary>The C++ type a C# type spelling implies, or null where it cannot say.</summary>
    /// <remarks>
    /// <para>
    /// Derived from the C# type, not from <c>[NativeTypeName]</c>: the annotation is ClangSharp's
    /// copy of the header's spelling, so comparing it to the header compares the header with
    /// itself. What C# actually passes is what has to be right.
    /// </para>
    /// <para>
    /// Fixed-width spellings throughout. C# <c>ulong</c> is <c>uint64_t</c>, which is
    /// <c>unsigned long long</c> on Windows and <c>unsigned long</c> on LP64 Linux — distinct types
    /// to <c>is_same</c>, the same ABI. Spelling it <c>uint64_t</c> lets the compiler pick.
    /// </para>
    /// <para>
    /// The calling convention is dropped. <c>unmanaged[Stdcall]</c> matches NRI_CALL, which is
    /// <c>__stdcall</c> on _WIN32 and nothing elsewhere; on x64 and arm64 there is only one
    /// convention, so there is nothing to assert.
    /// </para>
    /// </remarks>
    private static string? ToCpp(string cs)
    {
        cs = cs.Trim();

        if (cs.StartsWith("delegate", StringComparison.Ordinal))
        {
            var open = cs.IndexOf('<');
            if (open < 0) return null;

            var depth = 0;
            var close = -1;
            for (var i = open; i < cs.Length && close < 0; i++)
            {
                if (cs[i] == '<') depth++;
                else if (cs[i] == '>' && --depth == 0) close = i;
            }

            if (close < 0) return null;

            var parts = SplitTopLevel(cs[(open + 1)..close]).Select(p => ToCpp(p)).ToList();
            if (parts.Count == 0 || parts.Any(p => p is null)) return null;

            var stars = cs[(close + 1)..].Trim();
            if (stars.Any(c => c != '*')) return null;

            // fn_<R, A...> is R(*)(A...); written as an alias so pointer-to-function-pointer
            // needs no declarator gymnastics.
            return Fn(parts[^1]!, parts.Take(parts.Count - 1)!) + stars;
        }

        var bare = cs.TrimEnd('*').Trim();
        var pointers = new string('*', cs.Length - cs.TrimEnd('*').Length);

        var mapped = bare switch
        {
            "void" => "void",
            "bool" => "bool",
            "byte" => "uint8_t",
            "sbyte" => "int8_t",
            "short" => "int16_t",
            "ushort" => "uint16_t",
            "int" => "int32_t",
            "uint" => "uint32_t",
            "long" => "int64_t",
            "ulong" => "uint64_t",
            "nint" => "intptr_t",
            "nuint" => "size_t",
            "float" => "float",
            "double" => "double",
            // A generated struct or enum: the same name in C++, found through `using namespace nri`
            // (or at global scope, for the NonNriForwardStruct ID3D12* / Im* / AGS handles).
            _ when bare.Length > 0 && bare.All(c => char.IsLetterOrDigit(c) || c == '_') => bare,
            _ => null,
        };

        return mapped is null ? null : mapped + pointers;
    }

    private static List<string> SplitTopLevel(string args)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is '<' or '(' or '[') depth++;
            else if (args[i] is '>' or ')' or ']') depth--;
            else if (args[i] == ',' && depth == 0)
            {
                result.Add(args[start..i]);
                start = i + 1;
            }
        }

        if (args[start..].Trim().Length > 0) result.Add(args[start..]);
        return result;
    }

    /// <summary>
    /// The C++ preamble that reduces a declared type to the shape C# can express.
    /// </summary>
    /// <remarks>
    /// Three rewrites, each one a thing C# has no way to say and that does not change the ABI:
    /// <list type="bullet">
    /// <item><c>T&amp;</c> becomes <c>T*</c>. NRI passes nearly everything by reference; a reference
    /// is a pointer at the call boundary.</item>
    /// <item><c>const</c> is dropped at every level. <c>const char*</c> and <c>char*</c> pass the
    /// same way.</item>
    /// <item><c>bool</c> becomes <c>uint8_t</c>, and <c>char</c> becomes <c>int8_t</c>: the
    /// generated layer spells C++ <c>bool</c> as <c>byte</c> and <c>char</c> as <c>sbyte</c>.</item>
    /// </list>
    /// Everything else must match exactly: the return type, the number of parameters, every
    /// parameter's type, the pointer depth, the enum or struct named.
    /// </remarks>
    private const string AbiPreamble = """
        template<class A, class B> struct same_ { static const bool value = false; };
        template<class A> struct same_<A, A> { static const bool value = true; };

        template<class R, class... A> struct fn_alias_ { typedef R (*type)(A...); };
        template<class R, class... A> using fn_ = typename fn_alias_<R, A...>::type;

        template<class T> struct abi_ { typedef T type; };
        template<class T> using abi_t = typename abi_<T>::type;
        template<class T> struct abi_<const T> { typedef abi_t<T> type; };
        template<class T> struct abi_<volatile T> { typedef abi_t<T> type; };
        template<class T> struct abi_<const volatile T> { typedef abi_t<T> type; };
        template<class T> struct abi_<T*> { typedef abi_t<T>* type; };
        template<class T> struct abi_<T&> { typedef abi_t<T>* type; };
        template<class T> struct abi_<T&&> { typedef abi_t<T>* type; };
        template<> struct abi_<bool> { typedef uint8_t type; };
        template<> struct abi_<char> { typedef int8_t type; };
        template<class R, class... A> struct abi_<R (*)(A...)> { typedef fn_<abi_t<R>, abi_t<A>...> type; };
        #if __cplusplus >= 201703L
        template<class R, class... A> struct abi_<R (*)(A...) noexcept> { typedef fn_<abi_t<R>, abi_t<A>...> type; };
        #endif
        """;

    private string? BuildSignatureProbe(string name, string what, IReadOnlyList<Signature> signatures)
    {
        var cpp = new StringBuilder();
        cpp.AppendLine($"// Generated by SharpNRI.Tests/NativeAbiTests. One assertion per {what}.");
        AppendIncludes(cpp);
        cpp.AppendLine(AbiPreamble);
        cpp.AppendLine("using namespace nri;");

        foreach (var s in signatures)
        {
            // Also the C# side's own spelling must canonicalise to itself — fn_ of canonical
            // types is canonical — so the comparison is abi_t<declared> against the implied type,
            // and clang prints both template arguments when it fails.
            cpp.AppendLine($"static_assert(same_<abi_t<{s.CppDeclared}>, {s.CsImplied}>::value,");
            cpp.AppendLine($"    \"{s.Label}: the binding declares {s.CsImplied.Replace("\"", "")}\");");
        }

        cpp.AppendLine("int main() { return 0; }");

        var result = NativeAbi.Build(cpp.ToString(), name, out var diagnostics);
        if (result is not null) return null;

        // Lift the failures out of the noise: one line each, naming the signature, plus the full
        // compiler output after them for the types.
        var failures = diagnostics.Split('\n')
            .Where(l => l.Contains("static assertion failed", StringComparison.Ordinal)
                     || l.Contains("error:", StringComparison.Ordinal))
            .Select(l => "    " + l.Trim())
            .ToList();

        return $"{failures.Count} error(s):\n{string.Join("\n", failures)}\n\nfull compiler output:\n{diagnostics}";
    }

    [AbiFact]
    public void Every_function_pointer_field_has_the_signature_the_header_declares()
    {
        var signatures = FunctionPointerFields(out var declared, out var unreadable);

        output.WriteLine($"{signatures.Count} of {declared} function-pointer fields reconstructed and checked "
            + $"(across {signatures.Select(s => s.Label.Split("::")[0]).Distinct().Count()} structs)");
        foreach (var u in unreadable) output.WriteLine($"  not checked: {u}");

        // Every one, not most of them: the C# side of a function pointer is plain pointers, integers
        // and generated names, so anything this cannot read is a new shape and should be looked at.
        Assert.True(unreadable.Count == 0,
            $"{unreadable.Count} function pointer(s) could not be reconstructed:\n"
            + string.Concat(unreadable.Select(u => $"    {u}\n")));
        Assert.True(signatures.Count == declared,
            $"{declared} `delegate*` in the sources, {signatures.Count} matched as fields. The field "
            + "pattern no longer matches how they are generated.");
        Assert.True(declared >= 200, $"only {declared} function pointers found");

        var failure = BuildSignatureProbe("function-pointers", "function-pointer field", signatures);
        Assert.True(failure is null,
            "the function-pointer probe did not build. Each failed assertion names Interface::Function; "
            + "clang prints the two types — the first is the header's, reduced to its ABI shape, the "
            + "second is what the binding calls through:\n" + failure);
    }

    [AbiFact]
    public void Every_DllImport_has_the_signature_the_header_declares()
    {
        var signatures = Imports(out var declared, out var unreadable);

        output.WriteLine($"{signatures.Count} of {declared} DllImports reconstructed and checked");
        foreach (var u in unreadable) output.WriteLine($"  not checked: {u}");

        Assert.True(unreadable.Count == 0,
            $"{unreadable.Count} DllImport(s) could not be reconstructed: {string.Join(", ", unreadable)}");
        Assert.True(signatures.Count == declared,
            $"{declared} [DllImport] in NriApi.cs, {signatures.Count} matched as declarations. The "
            + "declaration pattern no longer matches how they are generated.");
        Assert.True(declared >= 15, $"only {declared} DllImports found");

        var failure = BuildSignatureProbe("dllimports", "DllImport", signatures);
        Assert.True(failure is null,
            "the DllImport probe did not build. Each failed assertion names the export; clang prints "
            + "the two types — the first is the header's, reduced to its ABI shape, the second is "
            + "what the binding declares:\n" + failure);
    }
}
