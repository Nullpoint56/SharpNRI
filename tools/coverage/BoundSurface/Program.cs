// Bound surface: which of NRI's functions SharpNRI's own code calls, and which fields of NRI's
// native structs its API reaches.
//
// An NRI function is a function-pointer field of one of the interface tables nriGetInterface fills
// (the SharpNRI.Native.*Interface structs of SharpNRI.Descs.dll; CallbackInterface is left out: the
// application implements it, NRI calls it) or a flat export (a DllImport of SharpNRI.Native.NriApi
// in SharpNRI.dll). It is bound when a method of SharpNRI.dll outside NriApi loads that field or
// calls that export, read from the IL. Each such load or call is reported with the source line the
// PDB maps it to, so tools/coverage.ps1 can ask the test coverage whether that line ran. Struct
// fields: see "Desc fields" below.
//
//   BoundSurface <SharpNRI.Descs.dll> <SharpNRI.dll> <out.json>

using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;

if (args.Length != 3)
{
    Console.Error.WriteLine("usage: BoundSurface <SharpNRI.Descs.dll> <SharpNRI.dll> <out.json>");
    return 2;
}
var (descsPath, bindingPath, outPath) = (args[0], args[1], args[2]);

// The surface: every function-pointer field of every interface table, in declaration order
var tables = new List<(string Table, List<string> Functions)>();
using (var descs = new PEReader(File.OpenRead(descsPath)))
{
    var md = descs.GetMetadataReader();
    foreach (var typeHandle in md.TypeDefinitions)
    {
        var type = md.GetTypeDefinition(typeHandle);
        var name = md.GetString(type.Name);
        if (md.GetString(type.Namespace) != "SharpNRI.Native" || !name.EndsWith("Interface", StringComparison.Ordinal) || name == "CallbackInterface")
            continue;
        var functions = new List<string>();
        foreach (var fieldHandle in type.GetFields())
        {
            var field = md.GetFieldDefinition(fieldHandle);
            if ((field.Attributes & FieldAttributes.Static) == 0 && IsFunctionPointer(md, field.Signature))
                functions.Add(md.GetString(field.Name));
        }
        if (functions.Count > 0) tables.Add((name, functions));
    }
}
var tableNames = tables.Select(t => t.Table).ToHashSet(StringComparer.Ordinal);

// The calls: IL of every method of SharpNRI.dll outside NriApi
var callers = new Dictionary<string, List<Caller>>(StringComparer.Ordinal); // "Table.Function" or "nriExport" -> call sites
var exports = new List<string>();
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Select(f => (OpCode)f.GetValue(null)!)
    .ToDictionary(o => (ushort)o.Value);

using (var binding = new PEReader(File.OpenRead(bindingPath)))
{
    var md = binding.GetMetadataReader();
    var pdbPath = Path.ChangeExtension(bindingPath, ".pdb");
    using var pdbProvider = File.Exists(pdbPath) ? MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(pdbPath)) : null;
    var pdb = pdbProvider?.GetMetadataReader();

    var exportHandles = new Dictionary<MethodDefinitionHandle, string>();
    foreach (var typeHandle in md.TypeDefinitions)
    {
        var type = md.GetTypeDefinition(typeHandle);
        if (md.GetString(type.Name) != "NriApi" || md.GetString(type.Namespace) != "SharpNRI.Native") continue;
        foreach (var methodHandle in type.GetMethods())
        {
            var method = md.GetMethodDefinition(methodHandle);
            if ((method.Attributes & MethodAttributes.PinvokeImpl) == 0) continue;
            var exportName = md.GetString(method.Name);
            if (exportHandles.ContainsValue(exportName)) continue;
            exportHandles[methodHandle] = exportName;
            exports.Add(exportName);
        }
    }

    foreach (var typeHandle in md.TypeDefinitions)
    {
        var type = md.GetTypeDefinition(typeHandle);
        if (md.GetString(type.Name) == "NriApi") continue;
        foreach (var methodHandle in type.GetMethods())
        {
            var method = md.GetMethodDefinition(methodHandle);
            if (method.RelativeVirtualAddress == 0) continue;
            var il = binding.GetMethodBody(method.RelativeVirtualAddress).GetILReader();
            while (il.RemainingBytes > 0)
            {
                var offset = il.Offset;
                ushort code = il.ReadByte();
                if (code == 0xFE) code = (ushort)(0xFE00 | il.ReadByte());
                var opcode = opcodes[code];
                string? key = null;
                switch (opcode.OperandType)
                {
                    case OperandType.InlineField:
                        key = FieldKey(md, MetadataTokens.EntityHandle(il.ReadInt32()), tableNames);
                        break;
                    case OperandType.InlineMethod:
                        var target = MetadataTokens.EntityHandle(il.ReadInt32());
                        if (target.Kind == HandleKind.MethodDefinition && exportHandles.TryGetValue((MethodDefinitionHandle)target, out var export))
                            key = export;
                        break;
                    case OperandType.InlineSwitch:
                        il.Offset += 4 * il.ReadInt32();
                        break;
                    default:
                        il.Offset += OperandSize(opcode.OperandType);
                        break;
                }
                if (key is null) continue;
                var site = Site(md, pdb, methodHandle, typeHandle, offset);
                (callers.TryGetValue(key, out var list) ? list : callers[key] = []).Add(site);
            }
        }
    }
}

// Desc fields. A native struct that the idiomatic API takes or returns (public and protected
// members of public types, NriApi and the interface tables excluded: they are the raw escape
// hatches) is exposed with every field, and so is every struct reachable through its fields. The
// other native structs are built or read by the binding itself, and a field of theirs is bound only
// when binding code (SharpNRI.dll, or the hand-written forms of SharpNRI.Descs) reads or writes it.
var descsAssembly = Assembly.LoadFrom(Path.GetFullPath(descsPath));
var bindingAssembly = Assembly.LoadFrom(Path.GetFullPath(bindingPath));
bool IsNativeStruct(Type t) => t.Namespace == "SharpNRI.Native" && t.IsValueType && !t.IsEnum && !t.IsPrimitive
    && !tableNames.Contains(t.Name) && t.Name != "CallbackInterface" && !t.Name.EndsWith("_e__FixedBuffer", StringComparison.Ordinal);
string NativeName(Type t) => t.IsNested ? $"{NativeName(t.DeclaringType!)}.{t.Name}" : t.Name;

var exposed = new HashSet<Type>();
void Expose(Type? t)
{
    if (t is null) return;
    if (t.HasElementType) { Expose(t.GetElementType()); return; }
    if (t.IsGenericType) { foreach (var a in t.GetGenericArguments()) Expose(a); return; }
    if (!IsNativeStruct(t) || !exposed.Add(t)) return;
    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) Expose(f.FieldType);
}
const BindingFlags Visible = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
foreach (var type in bindingAssembly.GetExportedTypes().Concat(descsAssembly.GetExportedTypes()))
{
    if (type.Namespace == "SharpNRI.Native") continue; // the generated L0 and NriApi: raw surface, not the idiomatic one
    foreach (var member in type.GetMembers(Visible))
    {
        switch (member)
        {
            case MethodBase m when m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly:
                if (m is MethodInfo mi) Expose(mi.ReturnType);
                foreach (var p in m.GetParameters()) Expose(p.ParameterType);
                break;
            case PropertyInfo p when (p.GetMethod?.IsPublic ?? false) || (p.SetMethod?.IsPublic ?? false):
                Expose(p.PropertyType);
                break;
            case FieldInfo f when f.IsPublic || f.IsFamily:
                Expose(f.FieldType);
                break;
        }
    }
}

// Structs that no NRI function signature reaches, directly or through fields, are data layouts the
// application writes into GPU memory itself (indirect arguments, instances, AABBs, query results):
// their public C# type is their whole surface.
var inSignatures = new HashSet<Type>();
void Reach(Type? t)
{
    if (t is null) return;
    if (t.HasElementType) { Reach(t.GetElementType()); return; }
    if (!IsNativeStruct(t) || !inSignatures.Add(t)) return;
    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) Reach(f.FieldType);
}
foreach (var table in descsAssembly.GetTypes().Where(t => t.Namespace == "SharpNRI.Native" && tableNames.Contains(t.Name)))
    foreach (var f in table.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.FieldType.IsFunctionPointer))
    {
        Reach(f.FieldType.GetFunctionPointerReturnType());
        foreach (var p in f.FieldType.GetFunctionPointerParameterTypes()) Reach(p);
    }
foreach (var m in bindingAssembly.GetType("SharpNRI.Native.NriApi")!.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
{
    Reach(m.ReturnType);
    foreach (var p in m.GetParameters()) Reach(p.ParameterType);
}

// Field accesses by binding code. Generated helpers (the union and fixed-buffer accessors of L0) are
// followed: calling one counts as touching the fields it touches.
var helperFields = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal); // "Struct.method" -> fields
var accessed = new HashSet<string>(StringComparer.Ordinal); // "Struct.Nested.field"
foreach (var (path, generatedOnly) in new[] { (descsPath, true), (bindingPath, false), (descsPath, false) })
{
    using var pe = new PEReader(File.OpenRead(path));
    var md = pe.GetMetadataReader();
    foreach (var typeHandle in md.TypeDefinitions)
    {
        var type = md.GetTypeDefinition(typeHandle);
        var outerMost = type;
        while (outerMost.GetDeclaringType() is { IsNil: false } d) outerMost = md.GetTypeDefinition(d);
        var generated = md.GetString(outerMost.Namespace) == "SharpNRI.Native";
        if (md.GetString(type.Name) == "NriApi" || generated != generatedOnly) continue;
        var owner = generated ? TypeDefinitionName(md, typeHandle) : null;
        foreach (var methodHandle in type.GetMethods())
        {
            var method = md.GetMethodDefinition(methodHandle);
            if (method.RelativeVirtualAddress == 0) continue;
            var touched = new HashSet<string>(StringComparer.Ordinal);
            var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILReader();
            while (il.RemainingBytes > 0)
            {
                ushort code = il.ReadByte();
                if (code == 0xFE) code = (ushort)(0xFE00 | il.ReadByte());
                var opcode = opcodes[code];
                if (opcode.OperandType == OperandType.InlineField)
                {
                    if (NativeFieldName(md, MetadataTokens.EntityHandle(il.ReadInt32())) is { } fieldName) touched.Add(fieldName);
                }
                else if (opcode.OperandType == OperandType.InlineMethod)
                {
                    if (NativeMethodName(md, MetadataTokens.EntityHandle(il.ReadInt32())) is { } helper && helperFields.TryGetValue(helper, out var fields))
                        touched.UnionWith(fields);
                }
                else if (opcode.OperandType == OperandType.InlineSwitch) il.Offset += 4 * il.ReadInt32();
                else il.Offset += OperandSize(opcode.OperandType);
            }
            if (generatedOnly) { if (owner is not null) helperFields[$"{owner}.{md.GetString(method.Name)}"] = touched; }
            else accessed.UnionWith(touched);
        }
    }
}

var structs = descsAssembly.GetTypes().Where(IsNativeStruct).OrderBy(NativeName, StringComparer.Ordinal).Select(t => new
{
    name = NativeName(t),
    kind = exposed.Contains(t) ? "exposed" : inSignatures.Contains(t) ? "built" : "layout",
    fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        .Select(f => new { name = f.Name, bound = exposed.Contains(t) || !inSignatures.Contains(t) || accessed.Contains($"{NativeName(t)}.{f.Name}") })
        .ToList(),
}).Where(s => s.fields.Count > 0).ToList();

var report = new
{
    tables = tables.Select(t => new
    {
        name = t.Table,
        functions = t.Functions.Select(f => new { name = f, callers = Distinct(callers.GetValueOrDefault($"{t.Table}.{f}")) }),
    }),
    exports = exports.Select(e => new { name = e, callers = Distinct(callers.GetValueOrDefault(e)) }),
    structs,
};
File.WriteAllText(outPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
var functionCount = tables.Sum(t => t.Functions.Count) + exports.Count;
var boundCount = tables.Sum(t => t.Functions.Count(f => callers.ContainsKey($"{t.Table}.{f}"))) + exports.Count(callers.ContainsKey);
Console.WriteLine($"{tables.Count} interface tables, {tables.Sum(t => t.Functions.Count)} functions, {exports.Count} flat exports: {boundCount} of {functionCount} bound");
Console.WriteLine($"{structs.Count} native structs ({structs.Count(s => s.kind == "exposed")} exposed, {structs.Count(s => s.kind == "built")} built by the binding, {structs.Count(s => s.kind == "layout")} data layouts), " +
    $"{structs.Sum(s => s.fields.Count)} fields: {structs.Sum(s => s.fields.Count(f => f.bound))} bound");
return 0;

// "Struct.method" for a method of a generated native struct, from a method token
static string? NativeMethodName(MetadataReader md, EntityHandle handle)
{
    switch (handle.Kind)
    {
        case HandleKind.MemberReference:
            var member = md.GetMemberReference((MemberReferenceHandle)handle);
            return member.Parent.Kind == HandleKind.TypeReference && TypeReferenceName(md, (TypeReferenceHandle)member.Parent) is { } parent
                ? $"{parent}.{md.GetString(member.Name)}" : null;
        case HandleKind.MethodDefinition:
            var method = md.GetMethodDefinition((MethodDefinitionHandle)handle);
            return TypeDefinitionName(md, method.GetDeclaringType()) is { } owner ? $"{owner}.{md.GetString(method.Name)}" : null;
        default:
            return null;
    }
}

// "Struct.field" or "Struct.Nested.field" for a field of a native struct, from a field token
static string? NativeFieldName(MetadataReader md, EntityHandle handle)
{
    switch (handle.Kind)
    {
        case HandleKind.MemberReference:
            var member = md.GetMemberReference((MemberReferenceHandle)handle);
            return member.Parent.Kind == HandleKind.TypeReference && TypeReferenceName(md, (TypeReferenceHandle)member.Parent) is { } parent
                ? $"{parent}.{md.GetString(member.Name)}" : null;
        case HandleKind.FieldDefinition:
            var field = md.GetFieldDefinition((FieldDefinitionHandle)handle);
            return TypeDefinitionName(md, field.GetDeclaringType()) is { } owner ? $"{owner}.{md.GetString(field.Name)}" : null;
        default:
            return null;
    }
}

static string? TypeReferenceName(MetadataReader md, TypeReferenceHandle handle)
{
    var reference = md.GetTypeReference(handle);
    if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
        return TypeReferenceName(md, (TypeReferenceHandle)reference.ResolutionScope) is { } outer ? $"{outer}.{md.GetString(reference.Name)}" : null;
    return md.GetString(reference.Namespace) == "SharpNRI.Native" ? md.GetString(reference.Name) : null;
}

static string? TypeDefinitionName(MetadataReader md, TypeDefinitionHandle handle)
{
    var type = md.GetTypeDefinition(handle);
    if (type.GetDeclaringType() is { IsNil: false } declaring)
        return TypeDefinitionName(md, declaring) is { } outer ? $"{outer}.{md.GetString(type.Name)}" : null;
    return md.GetString(type.Namespace) == "SharpNRI.Native" ? md.GetString(type.Name) : null;
}

static List<Caller> Distinct(List<Caller>? sites) =>
    sites is null ? [] : sites.DistinctBy(s => (s.Method, s.File, s.Line)).OrderBy(s => s.Method, StringComparer.Ordinal).ThenBy(s => s.Line).ToList();

static bool IsFunctionPointer(MetadataReader md, BlobHandle signature)
{
    var blob = md.GetBlobReader(signature);
    blob.ReadSignatureHeader();
    while (true)
    {
        var code = blob.ReadSignatureTypeCode();
        if (code is SignatureTypeCode.RequiredModifier or SignatureTypeCode.OptionalModifier)
        {
            blob.ReadTypeHandle();
            continue;
        }
        return code == SignatureTypeCode.FunctionPointer;
    }
}

static string? FieldKey(MetadataReader md, EntityHandle handle, HashSet<string> tableNames)
{
    if (handle.Kind != HandleKind.MemberReference) return null; // the tables live in SharpNRI.Descs
    var member = md.GetMemberReference((MemberReferenceHandle)handle);
    if (member.Parent.Kind != HandleKind.TypeReference) return null;
    var parent = md.GetTypeReference((TypeReferenceHandle)member.Parent);
    var table = md.GetString(parent.Name);
    return md.GetString(parent.Namespace) == "SharpNRI.Native" && tableNames.Contains(table) ? $"{table}.{md.GetString(member.Name)}" : null;
}

static int OperandSize(OperandType type) => type switch
{
    OperandType.InlineNone => 0,
    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
    OperandType.InlineVar => 2,
    OperandType.InlineI8 or OperandType.InlineR => 8,
    _ => 4,
};

// The user-facing method a site belongs to (lambdas and local functions name their container), and
// the source line of the sequence point that covers the IL offset
static Caller Site(MetadataReader md, MetadataReader? pdb, MethodDefinitionHandle methodHandle, TypeDefinitionHandle typeHandle, int offset)
{
    var methodName = md.GetString(md.GetMethodDefinition(methodHandle).Name);
    if (methodName.StartsWith('<') && methodName.IndexOf('>') > 1) methodName = methodName[1..methodName.IndexOf('>')];
    var type = md.GetTypeDefinition(typeHandle);
    while (md.GetString(type.Name).StartsWith('<') && type.GetDeclaringType() is { IsNil: false } declaring)
        type = md.GetTypeDefinition(declaring);
    var typeName = md.GetString(type.Name);
    for (var outer = type.GetDeclaringType(); !outer.IsNil; outer = md.GetTypeDefinition(outer).GetDeclaringType())
        typeName = $"{md.GetString(md.GetTypeDefinition(outer).Name)}.{typeName}";

    string? file = null;
    var line = 0;
    if (pdb is not null)
    {
        var debug = pdb.GetMethodDebugInformation(methodHandle.ToDebugInformationHandle());
        foreach (var point in debug.GetSequencePoints())
        {
            if (point.Offset > offset) break;
            if (point.IsHidden) continue;
            file = pdb.GetString(pdb.GetDocument(point.Document).Name);
            line = point.StartLine;
        }
    }
    return new Caller($"{typeName}.{methodName}", file, line);
}

internal sealed record Caller(string Method, string? File, int Line);
