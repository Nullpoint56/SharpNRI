// API changes between two sets of packages (ApiDiff.csproj): every public or protected type and
// member of the lib/ assemblies of each package id present on both sides, as one line each with its
// signature (and a constant's or enum member's value), compared by identity. A changed value, return
// type, field type or base list is "changed"; an overload with other parameters is a removal plus an
// addition. What changed is stated, not why.
//
//   dotnet run --project tools/apidiff -- <old nupkg dir> <new nupkg dir> <out.md> <baseline label>

using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using System.Text;

if (args.Length != 4)
{
    Console.Error.WriteLine("usage: ApiDiff <old nupkg dir> <new nupkg dir> <out.md> <baseline label>");
    return 2;
}
var (oldDir, newDir, outFile, baseline) = (args[0], args[1], args[2], args[3]);
var work = Directory.CreateTempSubdirectory("sharpnri-apidiff");
try
{
    var oldSide = Extract(oldDir, Path.Combine(work.FullName, "old"));
    var newSide = Extract(newDir, Path.Combine(work.FullName, "new"));
    var md = new StringBuilder();
    md.AppendLine("## API changes");
    md.AppendLine(CultureInfo.InvariantCulture, $"Public surface against {baseline}, per package.");
    var any = false;
    foreach (var (id, newPackage) in newSide.OrderBy(p => p.Key, StringComparer.Ordinal))
    {
        if (newPackage.Assemblies.Count == 0) continue;
        if (!oldSide.TryGetValue(id, out var oldPackage))
        {
            md.AppendLine(CultureInfo.InvariantCulture, $"- {id} {newPackage.Version}: new package");
            continue;
        }
        var before = Surface(oldPackage, oldSide);
        var after = Surface(newPackage, newSide);
        var added = after.Keys.Except(before.Keys).Order(StringComparer.Ordinal).Select(k => $"`{after[k]}`").ToList();
        var removed = before.Keys.Except(after.Keys).Order(StringComparer.Ordinal).Select(k => $"`{before[k]}`").ToList();
        var changed = after.Keys.Intersect(before.Keys).Where(k => after[k] != before[k]).Order(StringComparer.Ordinal)
            .Select(k => $"`{before[k]}` → `{after[k]}`").ToList();
        var version = oldPackage.Version == newPackage.Version ? newPackage.Version : $"{oldPackage.Version} → {newPackage.Version}";
        if (added.Count + removed.Count + changed.Count == 0)
        {
            md.AppendLine(CultureInfo.InvariantCulture, $"- {id} {version}: unchanged");
            continue;
        }
        any = true;
        md.AppendLine(CultureInfo.InvariantCulture, $"- {id} {version}: {removed.Count} removed, {changed.Count} changed, {added.Count} added");
        List("Removed", removed);
        List("Changed", changed);
        List("Added", added);
    }
    if (!any) md.AppendLine("- No public API changed");
    md.AppendLine();
    File.WriteAllText(outFile, md.ToString());
    Console.Write(md.ToString());
    return 0;

    void List(string heading, List<string> items)
    {
        if (items.Count == 0) return;
        md.AppendLine(CultureInfo.InvariantCulture, $"  <details><summary>{heading} ({items.Count})</summary>");
        md.AppendLine();
        foreach (var i in items) md.AppendLine(CultureInfo.InvariantCulture, $"  - {i}");
        md.AppendLine();
        md.AppendLine("  </details>");
    }
}
finally
{
    work.Delete(recursive: true);
}

// Every package in the directory: id, version and its lib/ assemblies, extracted.
static Dictionary<string, Package> Extract(string dir, string into)
{
    var packages = new Dictionary<string, Package>(StringComparer.OrdinalIgnoreCase);
    foreach (var nupkg in Directory.EnumerateFiles(dir, "*.nupkg"))
    {
        using var zip = ZipFile.OpenRead(nupkg);
        var nuspec = zip.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) && !e.FullName.Contains('/', StringComparison.Ordinal));
        using var reader = new StreamReader(nuspec.Open());
        var xml = System.Xml.Linq.XDocument.Parse(reader.ReadToEnd());
        var metadata = xml.Root!.Elements().Single(e => e.Name.LocalName == "metadata");
        var id = metadata.Elements().Single(e => e.Name.LocalName == "id").Value;
        var version = metadata.Elements().Single(e => e.Name.LocalName == "version").Value;
        var target = Path.Combine(into, id);
        Directory.CreateDirectory(target);
        var assemblies = new List<string>();
        foreach (var e in zip.Entries.Where(e => e.FullName.StartsWith("lib/", StringComparison.Ordinal) && e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            var path = Path.Combine(target, e.Name);
            e.ExtractToFile(path, overwrite: true);
            assemblies.Add(path);
        }
        packages[id] = new Package(id, version, assemblies);
    }
    return packages;
}

// identity -> display line, for every public or protected type and member of the package's assemblies
static Dictionary<string, string> Surface(Package package, Dictionary<string, Package> side)
{
    var paths = Directory.GetFiles(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")
        .Concat(side.Values.SelectMany(p => p.Assemblies));
    using var context = new MetadataLoadContext(new PathAssemblyResolver(paths));
    var surface = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var path in package.Assemblies)
    {
        foreach (var type in context.LoadFromAssemblyPath(path).GetTypes().Where(Visible))
        {
            var name = Name(type);
            surface[$"T {name}"] = TypeLine(type, name);
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var f in type.GetFields(all).Where(f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly))
            {
                if (type.IsEnum && f.IsSpecialName) continue;   // value__
                var key = $"F {name}.{f.Name}";
                surface[key] = type.IsEnum ? $"{name}.{f.Name} = {f.GetRawConstantValue()}"
                    : f.IsLiteral ? $"const {Name(f.FieldType)} {name}.{f.Name} = {Literal(f.GetRawConstantValue())}"
                    : $"{(f.IsStatic ? "static " : "")}{(f.IsInitOnly ? "readonly " : "")}{Name(f.FieldType)} {name}.{f.Name}";
            }
            foreach (var p in type.GetProperties(all))
            {
                var get = p.GetMethod is { } g && (g.IsPublic || g.IsFamily || g.IsFamilyOrAssembly);
                var set = p.SetMethod is { } s && (s.IsPublic || s.IsFamily || s.IsFamilyOrAssembly);
                if (!get && !set) continue;
                var index = p.GetIndexParameters();
                var key = $"P {name}.{p.Name}({string.Join(",", index.Select(i => Name(i.ParameterType)))})";
                var isStatic = (p.GetMethod ?? p.SetMethod)!.IsStatic;
                surface[key] = $"{(isStatic ? "static " : "")}{Name(p.PropertyType)} {name}.{p.Name}{(index.Length > 0 ? $"[{Parameters(index)}]" : "")} {{ {(get ? "get; " : "")}{(set ? (IsInit(p.SetMethod!) ? "init; " : "set; ") : "")}}}";
            }
            foreach (var m in type.GetMethods(all).Where(m => !m.IsSpecialName || m.Name.StartsWith("op_", StringComparison.Ordinal)).Where(m => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly))
            {
                var generic = m.IsGenericMethodDefinition ? $"<{string.Join(",", m.GetGenericArguments().Select(a => a.Name))}>" : "";
                var key = $"M {name}.{m.Name}{generic}({string.Join(",", m.GetParameters().Select(ParameterKey))})";
                surface[key] = $"{(m.IsStatic ? "static " : "")}{(m.IsAbstract && !type.IsInterface ? "abstract " : "")}{Name(m.ReturnType)} {name}.{m.Name}{generic}({Parameters(m.GetParameters())})";
            }
            foreach (var c in type.GetConstructors(all).Where(c => !c.IsStatic && (c.IsPublic || c.IsFamily || c.IsFamilyOrAssembly)))
            {
                var key = $"C {name}({string.Join(",", c.GetParameters().Select(ParameterKey))})";
                surface[key] = $"{name}({Parameters(c.GetParameters())})";
            }
            foreach (var e in type.GetEvents(all).Where(e => e.AddMethod is { } a && (a.IsPublic || a.IsFamily)))
                surface[$"E {name}.{e.Name}"] = $"event {Name(e.EventHandlerType!)} {name}.{e.Name}";
        }
    }
    return surface;
}

static bool Visible(Type t) => t.IsPublic || (t.IsNested && (t.IsNestedPublic || t.IsNestedFamily || t.IsNestedFamORAssem) && Visible(t.DeclaringType!));

static string TypeLine(Type t, string name)
{
    if (t.IsEnum) return $"enum {name} : {Name(t.GetEnumUnderlyingType())}{(t.CustomAttributes.Any(a => a.AttributeType.FullName == "System.FlagsAttribute") ? " [Flags]" : "")}";
    var kind = t.IsInterface ? "interface" : t.IsValueType ? (t.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.IsByRefLikeAttribute") ? "ref struct" : "struct")
        : t.BaseType?.FullName == "System.MulticastDelegate" ? "delegate"
        : t.IsAbstract && t.IsSealed ? "static class" : t.IsAbstract ? "abstract class" : t.IsSealed ? "sealed class" : "class";
    var readOnly = t.IsValueType && t.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute") ? "readonly " : "";
    var bases = new List<string>();
    if (t.BaseType is { } b && !t.IsValueType && b.FullName is not ("System.Object" or "System.MulticastDelegate")) bases.Add(Name(b));
    bases.AddRange(t.GetInterfaces().Where(i => i.IsPublic || i.IsNestedPublic).Select(Name).Order(StringComparer.Ordinal));
    return $"{readOnly}{kind} {name}{(bases.Count > 0 ? " : " + string.Join(", ", bases) : "")}";
}

static bool IsInit(MethodInfo set) => set.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");

static string ParameterKey(ParameterInfo p) => (p.IsOut ? "out " : p.ParameterType.IsByRef ? "ref " : "") + Name(p.ParameterType);

static string Parameters(ParameterInfo[] ps) => string.Join(", ", ps.Select(p =>
    $"{(p.IsOut ? "out " : p.ParameterType.IsByRef ? (p.IsIn ? "in " : "ref ") : "")}{Name(p.ParameterType)} {p.Name}{(p.HasDefaultValue ? " = " + Literal(p.RawDefaultValue) : "")}"));

static string Literal(object? value) => value switch
{
    null => "null",
    string s => $"\"{s}\"",
    bool b => b ? "true" : "false",
    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
    _ => value.ToString() ?? "",
};

static string Name(Type t)
{
    if (t.IsByRef) return Name(t.GetElementType()!);
    if (t.IsPointer) return Name(t.GetElementType()!) + "*";
    if (t.IsArray) return Name(t.GetElementType()!) + "[" + new string(',', t.GetArrayRank() - 1) + "]";
    if (t.IsGenericParameter) return t.Name;
    if (t.IsFunctionPointer)
        return $"delegate* unmanaged<{string.Join(", ", t.GetFunctionPointerParameterTypes().Select(Name).Append(Name(t.GetFunctionPointerReturnType())))}>";
    var simple = t.FullName switch
    {
        "System.Void" => "void",
        "System.Boolean" => "bool",
        "System.Byte" => "byte",
        "System.SByte" => "sbyte",
        "System.Int16" => "short",
        "System.UInt16" => "ushort",
        "System.Int32" => "int",
        "System.UInt32" => "uint",
        "System.Int64" => "long",
        "System.UInt64" => "ulong",
        "System.IntPtr" => "nint",
        "System.UIntPtr" => "nuint",
        "System.Single" => "float",
        "System.Double" => "double",
        "System.String" => "string",
        "System.Object" => "object",
        "System.Char" => "char",
        _ => null,
    };
    if (simple is not null) return simple;
    var name = t.IsNested ? Name(t.DeclaringType!) + "." + t.Name : (t.Namespace is { Length: > 0 } ns ? ns + "." : "") + t.Name;
    var tick = name.IndexOf('`', StringComparison.Ordinal);
    if (tick >= 0) name = name[..tick];
    if (t.IsGenericType) name += "<" + string.Join(", ", t.GetGenericArguments().Select(Name)) + ">";
    return name;
}

internal sealed record Package(string Id, string Version, List<string> Assemblies);
