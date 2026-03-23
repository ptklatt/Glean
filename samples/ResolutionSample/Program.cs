using System.Reflection.Metadata;

using Glean;
using Glean.Extensions;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/ResolutionSample -- <entry-assembly-path> [search-path...]");
    return 1;
}

var entryPath = args[0];
var searchPaths = args.Skip(1).ToArray();

using var closure = AssemblyClosure.Load(entryPath, searchPaths);

Console.WriteLine($"Entry assembly: {closure.EntryContext.Name} {closure.EntryContext.Version}");
Console.WriteLine($"Loaded assemblies: {closure.LoadedAssemblies.Count}");

if (closure.SkippedDependencies.Count > 0)
{
    Console.WriteLine("Skipped dependencies:");
    foreach (var skipped in closure.SkippedDependencies.Take(5))
    {
        Console.WriteLine($"  {skipped.AssemblySimpleName} ({skipped.Kind})");
    }
}

Console.WriteLine();
Console.WriteLine("Resolved type references:");

int printedTypes = 0;
foreach (var typeReference in closure.EntryReader.EnumerateTypeReferences())
{
    if (!closure.Set.TryResolveType(
        closure.EntryReader,
        typeReference.Handle,
        out var targetReader,
        out var targetHandle,
        out _))
    {
        continue;
    }

    var targetAssembly = targetReader.GetAssemblyDefinition();
    var targetType = targetReader.GetTypeDefinition(targetHandle);

    Console.WriteLine(
        $"  {typeReference.FullName} -> {targetReader.GetString(targetAssembly.Name)}:{targetType.ToFullNameString(targetReader)}");

    printedTypes++;
    if (printedTypes == 10)
    {
        break;
    }
}

if (printedTypes == 0)
{
    Console.WriteLine("  <none>");
}

Console.WriteLine();
Console.WriteLine("Resolved member references:");

int printedMembers = 0;
foreach (var memberReference in closure.EntryReader.EnumerateMemberReferences())
{
    if (!closure.Set.TryResolveMember(
        closure.EntryReader,
        memberReference.Handle,
        out var targetReader,
        out var targetHandle,
        out _))
    {
        continue;
    }

    Console.WriteLine($"  {memberReference.Name} -> {FormatResolvedMember(targetReader, targetHandle)}");

    printedMembers++;
    if (printedMembers == 10)
    {
        break;
    }
}

if (printedMembers == 0)
{
    Console.WriteLine("  <none>");
}

return 0;

static string FormatResolvedMember(MetadataReader reader, EntityHandle handle)
{
    if (handle.Kind == HandleKind.MethodDefinition)
    {
        var method = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
        var declaringType = reader.GetTypeDefinition(method.GetDeclaringType());
        return $"{declaringType.ToFullNameString(reader)}.{reader.GetString(method.Name)} [method]";
    }

    if (handle.Kind == HandleKind.FieldDefinition)
    {
        var field = reader.GetFieldDefinition((FieldDefinitionHandle)handle);
        var declaringType = reader.GetTypeDefinition(field.GetDeclaringType());
        return $"{declaringType.ToFullNameString(reader)}.{reader.GetString(field.Name)} [field]";
    }

    return handle.Kind.ToString();
}
