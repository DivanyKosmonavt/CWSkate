using System;
using System.IO;
using Mono.Cecil;

// Makes every type/method/field public so the mod can compile against internals.
class Program
{
    static void Main(string[] args)
    {
        string input = args[0], output = args[1];
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(input));
        var asm = AssemblyDefinition.ReadAssembly(input, new ReaderParameters { AssemblyResolver = resolver });
        foreach (var module in asm.Modules)
        foreach (var type in module.GetTypes())
        {
            if (type.IsNested) type.IsNestedPublic = true; else type.IsPublic = true;
            foreach (var m in type.Methods) m.IsPublic = true;
            foreach (var f in type.Fields)
            {
                // skip event backing fields to avoid name clashes with events
                bool clash = false;
                foreach (var e in type.Events) if (e.Name == f.Name) clash = true;
                if (!clash) f.IsPublic = true;
            }
        }
        asm.Write(output);
        Console.WriteLine("Publicized -> " + output);
    }
}
