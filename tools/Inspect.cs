using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

public static class Inspect
{
    static CSharpCompilation comp;
    static IAssemblySymbol asm;

    static void Init(string managed)
    {
        if (comp != null) return;
        var refs = new List<MetadataReference>();
        foreach (var f in Directory.GetFiles(managed, "*.dll")) { try { refs.Add(MetadataReference.CreateFromFile(f)); } catch { } }
        comp = CSharpCompilation.Create("x", null, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithMetadataImportOptions(MetadataImportOptions.All));
        asm = comp.References.Select(r => comp.GetAssemblyOrModuleSymbol(r)).OfType<IAssemblySymbol>().First(a => a.Name == "Assembly-CSharp");
    }

    static IEnumerable<INamedTypeSymbol> All(INamespaceOrTypeSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers()) { yield return t; foreach (var n in All(t)) yield return n; }
        if (ns is INamespaceSymbol n2) foreach (var c in n2.GetNamespaceMembers()) foreach (var x in All(c)) yield return x;
    }

    public static string Find(string managed, string part)
    {
        Init(managed);
        return string.Join("\n", All(asm.GlobalNamespace).Where(t => t.ToDisplayString().IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(t => t.ToDisplayString() + " : " + t.BaseType));
    }

    public static string Members(string managed, string typeName)
    {
        Init(managed);
        var t = comp.GetTypeByMetadataName(typeName);
        if (t == null) return "?? " + typeName;
        var lines = new List<string> { "=== " + t.ToDisplayString() + " : " + t.BaseType };
        foreach (var m in t.GetMembers())
        {
            if (m is IMethodSymbol ms && (ms.MethodKind == MethodKind.PropertyGet || ms.MethodKind == MethodKind.PropertySet || ms.MethodKind == MethodKind.EventAdd || ms.MethodKind == MethodKind.EventRemove)) continue;
            if (m.Name.Contains("k__BackingField")) continue;
            string extra = m is IFieldSymbol f ? " : " + f.Type + (f.IsStatic ? " [static]" : "") : m is IPropertySymbol p ? " : " + p.Type : (m is IMethodSymbol mm && (mm.IsVirtual || mm.IsOverride) ? " [virtual]" : "");
            lines.Add(string.Format("  {0,-9} {1}{2}", m.DeclaredAccessibility, m.ToDisplayString(), extra));
        }
        return string.Join("\n", lines);
    }

    public static string Enum(string managed, string typeName)
    {
        Init(managed);
        var t = comp.GetTypeByMetadataName(typeName);
        if (t == null) return "?? " + typeName;
        return typeName + ": " + string.Join(", ", t.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).Select(f => f.Name + "=" + f.ConstantValue));
    }
}
