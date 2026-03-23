using Glean;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/FastTraversalSample -- <assembly-path>");
    return 1;
}

using var scope = AssemblyScope.Open(args[0]);
var assembly = scope.Context;

Console.WriteLine($"Assembly: {assembly.Name} {assembly.Version}");
Console.WriteLine($"Type rows: {scope.Reader.TypeDefinitions.Count}");
Console.WriteLine();

int printedTypes = 0;
foreach (var type in assembly.EnumerateTypes())
{
    if (type.IsNested || !type.IsPublic)
    {
        continue;
    }

    Console.WriteLine(type.FullName);

    int printedMethods = 0;
    foreach (var method in type.EnumerateMethods())
    {
        if (!method.IsPublic || method.IsConstructor)
        {
            continue;
        }

        Console.WriteLine($"  {method.Name}");
        printedMethods++;
        if (printedMethods == 5)
        {
            break;
        }
    }

    printedTypes++;
    if (printedTypes == 10)
    {
        break;
    }
}

if (printedTypes == 0)
{
    Console.WriteLine("<no public top level types>");
}

return 0;
