using Glean;
using Glean.Contexts;

namespace CustomIrSample;

internal static class Program
{
    private const string Usage = "Usage: dotnet run --project samples/CustomIrSample -- <assembly-path> [full-type-name] [--resolve] [--search <directory>]...";

    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    internal static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if ((args.Length == 1) && (args[0] == "--help"))
        {
            output.WriteLine(Usage);
            return 0;
        }

        if (args.Length == 0)
        {
            error.WriteLine(Usage);
            return 1;
        }

        string? typeName = null;
        bool resolve = false;
        var searchPaths = new List<string>();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--resolve")
            {
                resolve = true;
            }
            else if ((args[i] == "--search") && (i + 1 < args.Length) &&
                !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                searchPaths.Add(args[++i]);
                resolve = true;
            }
            else if (!args[i].StartsWith("--", StringComparison.Ordinal) && (typeName == null))
            {
                typeName = args[i];
            }
            else
            {
                error.WriteLine(Usage);
                return 1;
            }
        }

        if (resolve)
        {
            using var closure = AssemblyClosure.Load(args[0], searchPaths.ToArray());
            return Write(closure.EntryContext, new IrTypeProvider(closure.Set), typeName, output, error);
        }

        using var scope = AssemblyScope.Open(args[0]);
        return Write(scope.Context, new IrTypeProvider(), typeName, output, error);
    }

    private static int Write(AssemblyContext assembly, IrTypeProvider provider, string? requestedType,
        TextWriter output, TextWriter error)
    {
        var type = FindType(assembly, requestedType);
        if (!type.IsValid)
        {
            error.WriteLine(requestedType == null ? "No type definitions found." : $"Type '{requestedType}' was not found.");
            return 1;
        }

        output.WriteLine($"Type: {IrTypeProvider.DefinitionIdentity(type.Reader, type.Handle).FullName}");
        foreach (var method in type.EnumerateMethods())
        {
            MethodReport.Write(method, provider, output);
        }

        return 0;
    }

    private static TypeContext FindType(AssemblyContext assembly, string? requestedType)
    {
        TypeContext fallback = default;
        foreach (var type in assembly.EnumerateTypes())
        {
            if (type.NameIs("<Module>"))
            {
                continue;
            }

            if (requestedType != null)
            {
                if (IrTypeProvider.DefinitionIdentity(type.Reader, type.Handle).FullName == requestedType)
                {
                    return type;
                }
            }
            else
            {
                if (type.IsPublic && !type.IsNested)
                {
                    return type;
                }

                if (!fallback.IsValid)
                {
                    fallback = type;
                }
            }
        }

        return requestedType == null ? fallback : default;
    }
}
