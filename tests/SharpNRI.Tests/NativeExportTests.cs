// Every nri* entry point the binding declares resolves in the library that actually loaded.
//
// The ABI oracle (NativeAbiTests) proves the signatures match the header; this proves the shipped
// payload exports each name, under the same probing DllImport uses, before any test happens to call
// it. A payload built with the wrong options, a stripped export table or a renamed library fails
// here with the list of missing names rather than as an EntryPointNotFoundException somewhere later.

using System.Reflection;
using System.Runtime.InteropServices;
using SharpNRI.Native;
using Xunit;

namespace SharpNRI.Tests;

public sealed class NativeExportTests
{
    [Fact]
    public void Every_DllImport_resolves_in_the_loaded_library()
    {
        var imports = typeof(NriApi)
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Select(m => (method: m, import: m.GetCustomAttribute<DllImportAttribute>()))
            .Where(x => x.import is not null)
            .ToList();
        Assert.NotEmpty(imports);
        Assert.All(imports, x => Assert.Equal("NRI", x.import!.Value));

        // The same resolution DllImport performs for this assembly: NRI.dll / libNRI.so beside it.
        var library = NativeLibrary.Load("NRI", typeof(NriApi).Assembly, null);
        try
        {
            var missing = imports
                .Select(x => x.import!.EntryPoint ?? x.method.Name)
                .Where(name => !NativeLibrary.TryGetExport(library, name, out _))
                .ToList();
            Assert.True(missing.Count == 0, $"{missing.Count} of {imports.Count} entry points missing from the loaded NRI library: {string.Join(", ", missing)}");
            GpuWork.Record($"{imports.Count} nri* entry points resolved");
        }
        finally
        {
            NativeLibrary.Free(library);
        }
    }
}
