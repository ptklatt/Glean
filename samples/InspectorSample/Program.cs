using System.Reflection.Metadata;

using Glean;
using Glean.Contexts;
using Glean.Decoding;
using Glean.Extensions;
using Glean.Providers;
using Glean.Resolution;
using Glean.Signatures;

var options = ParseArguments(args);
if (options == null)
{
    PrintUsage();
    return 1;
}

using var scope = AssemblyScope.Open(options.AssemblyPath);
var selectedType = FindSelectedType(scope.Context, options.TypeName);
if (!selectedType.IsValid)
{
    Console.Error.WriteLine(options.TypeName == null
        ? "No inspectable type was found in the assembly."
        : $"Type '{options.TypeName}' was not found.");
    return 1;
}

PrintAssemblySummary(scope);
Console.WriteLine();
PrintTypeSummary(scope, selectedType);
Console.WriteLine();
PrintTypeAttributes(scope, selectedType);
Console.WriteLine();
PrintMethodSummary(scope, selectedType);
Console.WriteLine();
PrintPropertySummary(selectedType);
Console.WriteLine();
PrintEventSummary(scope.Reader, selectedType);

if (options.SearchPaths.Length > 0)
{
    Console.WriteLine();
    PrintResolutionSummary(options, selectedType.FullName);
}

return 0;

static SampleOptions? ParseArguments(string[] args)
{
    if (args.Length == 0)
    {
        return null;
    }

    string assemblyPath = args[0];
    string? typeName = null;
    var searchPaths = new List<string>();

    for (int i = 1; i < args.Length; i++)
    {
        var arg = args[i];
        if (arg == "--type")
        {
            if ((i + 1) >= args.Length)
            {
                return null;
            }

            typeName = args[++i];
            continue;
        }

        if (arg == "--search")
        {
            if ((i + 1) >= args.Length)
            {
                return null;
            }

            searchPaths.Add(args[++i]);
            continue;
        }

        return null;
    }

    return new SampleOptions(assemblyPath, typeName, searchPaths.ToArray());
}

static void PrintUsage()
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/InspectorSample -- <assembly-path> [--type <full-type-name>] [--search <directory>]...");
    Console.Error.WriteLine("Pass dependency directories with --search if you want the optional resolution section to bind external references.");
}

static TypeContext FindSelectedType(AssemblyContext assembly, string? requestedTypeName)
{
    TypeContext fallback = default;

    foreach (var type in assembly.EnumerateTypes())
    {
        if (type.NameIs("<Module>"))
        {
            continue;
        }

        if ((requestedTypeName != null) && (type.FullName == requestedTypeName))
        {
            return type;
        }

        if ((requestedTypeName == null) && !type.IsNested)
        {
            if (type.IsPublic)
            {
                return type;
            }

            if (!fallback.IsValid)
            {
                fallback = type;
            }
        }
    }

    return fallback;
}

static void PrintAssemblySummary(AssemblyScope scope)
{
    var assembly = scope.Context;

    Console.WriteLine($"Assembly: {assembly.Name} {assembly.Version}");
    Console.WriteLine($"Assembly references: {scope.Reader.AssemblyReferences.Count}");
    Console.WriteLine($"Type rows: {scope.Reader.TypeDefinitions.Count}");
}

static void PrintTypeSummary(AssemblyScope scope, TypeContext type)
{
    Console.WriteLine($"Type: {type.FullName}");
    Console.WriteLine($"Kind: {DescribeTypeKind(type)}");
    Console.WriteLine($"Base type: {DescribeBaseType(type)}");
    Console.WriteLine($"Interfaces: {type.InterfaceCount}");
    Console.WriteLine($"Methods: {type.MethodCount}");
    Console.WriteLine($"Properties: {type.PropertyCount}");
    Console.WriteLine($"Events: {type.EventCount}");
}

static void PrintTypeAttributes(AssemblyScope scope, TypeContext type)
{
    Console.WriteLine("Attributes:");

    Console.WriteLine($"  CompilerGenerated: {type.IsCompilerGenerated}");
    Console.WriteLine($"  Serializable: {type.HasAttribute("System", "SerializableAttribute")}");
    Console.WriteLine($"  Obsolete: {type.HasAttribute("System", "ObsoleteAttribute")}");

    int decodedCount = 0;
    foreach (var attribute in type.EnumerateCustomAttributes())
    {
        try
        {
            var decoded = CustomAttributeDecoder.Decode(scope.Reader, attribute.Handle);
            Console.WriteLine($"  Decoded: {decoded.AttributeType}");

            foreach (var argument in decoded.FixedArguments)
            {
                Console.WriteLine($"    ctor: {argument.Type} = {FormatAttributeArgument(argument)}");
            }
        }
        catch (BadImageFormatException ex)
        {
            Console.WriteLine($"  Decoded: <failed: {ex.Message}>");
        }

        decodedCount++;
        if (decodedCount == 2)
        {
            break;
        }
    }

    if (decodedCount == 0)
    {
        Console.WriteLine("  <none>");
    }
}

static void PrintMethodSummary(AssemblyScope scope, TypeContext type)
{
    Console.WriteLine("Methods:");

    int printed = 0;
    foreach (var method in type.EnumerateMethods())
    {
        if (!method.IsPublic || method.IsConstructor)
        {
            continue;
        }

        var signature = method.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
        var ilSize = method.Definition.GetILSpan(scope.PeReader).Length;
        var flags = GetMethodFlags(method);

        Console.WriteLine($"  {FormatMethodSignature(method.Name, signature)}");
        Console.WriteLine($"    IL bytes: {ilSize}");
        Console.WriteLine($"    Flags: {flags}");

        printed++;
        if (printed == 5)
        {
            break;
        }
    }

    if (printed == 0)
    {
        Console.WriteLine("  <none>");
    }
}

static void PrintPropertySummary(TypeContext type)
{
    Console.WriteLine("Properties:");

    int printed = 0;
    foreach (var property in type.EnumerateProperties())
    {
        var signature = property.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
        var accessors = GetPropertyAccessors(property);
        var suffix = property.IsIndexer() ? $" indexer({property.GetIndexParameterCount()})" : string.Empty;

        Console.WriteLine($"  {signature.ReturnType} {property.Name}{suffix}");
        Console.WriteLine($"    Accessors: {accessors}");

        printed++;
        if (printed == 3)
        {
            break;
        }
    }

    if (printed == 0)
    {
        Console.WriteLine("  <none>");
    }
}

static void PrintEventSummary(MetadataReader reader, TypeContext type)
{
    Console.WriteLine("Events:");

    int printed = 0;
    foreach (var eventContext in type.EnumerateEvents())
    {
        Console.WriteLine($"  {eventContext.Name}: {DescribeTypeHandle(reader, eventContext.EventType)}");

        printed++;
        if (printed == 3)
        {
            break;
        }
    }

    if (printed == 0)
    {
        Console.WriteLine("  <none>");
    }
}

static void PrintResolutionSummary(SampleOptions options, string selectedTypeName)
{
    Console.WriteLine("Resolution:");

    using var closure = AssemblyClosure.Load(options.AssemblyPath, options.SearchPaths);
    var selectedType = FindSelectedType(closure.EntryContext, selectedTypeName);
    if (!selectedType.IsValid)
    {
        Console.WriteLine("  Selected type could not be reloaded in the closure.");
        return;
    }

    Console.WriteLine($"  Loaded assemblies: {closure.LoadedAssemblies.Count}");
    if (closure.SkippedDependencies.Count > 0)
    {
        Console.WriteLine("  Resolution is partial. Add more --search directories to improve external binding coverage.");
        foreach (var skipped in closure.SkippedDependencies.Take(3))
        {
            Console.WriteLine($"  Skipped: {skipped.AssemblySimpleName} ({skipped.Kind})");
        }
    }

    PrintResolvedBaseType(closure, selectedType);
    PrintResolvedInterfaces(closure, selectedType);
    PrintResolvedAttributeConstructors(closure, selectedType);
    PrintResolvedMemberReferences(closure);
}

static void PrintResolvedBaseType(AssemblyClosure closure, TypeContext type)
{
    Console.WriteLine("  Base type resolution:");

    if (!type.HasBaseType)
    {
        Console.WriteLine("    <none>");
        return;
    }

    if (type.TryGetBaseTypeDefinition(out var baseTypeDefinition))
    {
        Console.WriteLine($"    local -> {baseTypeDefinition.FullName}");
        return;
    }

    if (type.TryGetBaseTypeReference(out var baseTypeReference))
    {
        if (closure.Set.TryResolveType(
            closure.EntryReader,
            baseTypeReference.Handle,
            out var targetReader,
            out var targetHandle,
            out var reason))
        {
            var resolvedType = targetReader.GetTypeDefinition(targetHandle);
            Console.WriteLine($"    resolved -> {resolvedType.ToFullNameString(targetReader)}");
        }
        else
        {
            Console.WriteLine($"    failed -> {reason}");
        }

        return;
    }

    if (type.TryGetBaseTypeSpecification(out _))
    {
        Console.WriteLine($"    signature -> {type.DecodeBaseType()}");
        return;
    }

    Console.WriteLine("    <unsupported>");
}

static void PrintResolvedInterfaces(AssemblyClosure closure, TypeContext type)
{
    Console.WriteLine("  Interface resolution:");

    int printed = 0;
    foreach (var interfaceHandle in type.EnumerateInterfaceTypes())
    {
        if (interfaceHandle.Kind == HandleKind.TypeDefinition)
        {
            var localType = closure.EntryReader.GetTypeDefinition((TypeDefinitionHandle)interfaceHandle);
            Console.WriteLine($"    local -> {localType.ToFullNameString(closure.EntryReader)}");
        }
        else if (interfaceHandle.Kind == HandleKind.TypeReference)
        {
            if (closure.Set.TryResolveType(
                closure.EntryReader,
                (TypeReferenceHandle)interfaceHandle,
                out var targetReader,
                out var targetHandle,
                out var reason))
            {
                var resolvedType = targetReader.GetTypeDefinition(targetHandle);
                Console.WriteLine($"    resolved -> {resolvedType.ToFullNameString(targetReader)}");
            }
            else
            {
                var unresolvedType = closure.EntryReader.GetTypeReference((TypeReferenceHandle)interfaceHandle);
                Console.WriteLine($"    {unresolvedType.ToFullNameString(closure.EntryReader)} -> {reason}");
            }
        }
        else
        {
            Console.WriteLine($"    {DescribeTypeHandle(closure.EntryReader, interfaceHandle)}");
        }

        printed++;
        if (printed == 3)
        {
            break;
        }
    }

    if (printed == 0)
    {
        Console.WriteLine("    <none>");
    }
}

static void PrintResolvedAttributeConstructors(AssemblyClosure closure, TypeContext type)
{
    Console.WriteLine("  Attribute constructor resolution:");

    int printed = 0;
    foreach (var attribute in type.EnumerateCustomAttributes())
    {
        if (!attribute.TryGetConstructorReference(out var constructorReference))
        {
            continue;
        }

        if (closure.Set.TryResolveMember(
            closure.EntryReader,
            constructorReference.Handle,
            out var targetReader,
            out var targetHandle,
            out var reason))
        {
            Console.WriteLine($"    {constructorReference.Name} -> {FormatResolvedMember(targetReader, targetHandle)}");
        }
        else
        {
            Console.WriteLine($"    {constructorReference.Name} -> {reason}");
        }

        printed++;
        if (printed == 3)
        {
            break;
        }
    }

    if (printed == 0)
    {
        Console.WriteLine("    <none>");
    }
}

static void PrintResolvedMemberReferences(AssemblyClosure closure)
{
    Console.WriteLine("  External member references:");

    int printed = 0;
    var index = new MemberResolutionIndex(cacheSemanticSignatures: false);

    foreach (var memberReference in closure.EntryReader.EnumerateMemberReferences())
    {
        if (memberReference.Parent.Kind == HandleKind.TypeDefinition)
        {
            continue;
        }

        if (!closure.Set.TryResolveMember(
            closure.EntryReader,
            memberReference.Handle,
            index,
            out var targetReader,
            out var targetHandle,
            out _))
        {
            continue;
        }

        Console.WriteLine($"    {memberReference.Name} -> {FormatResolvedMember(targetReader, targetHandle)}");
        printed++;
        if (printed == 5)
        {
            break;
        }
    }

    if (printed == 0)
    {
        Console.WriteLine("    <none>");
    }
}

static string DescribeTypeKind(TypeContext type)
{
    if (type.IsEnum)
    {
        return "enum";
    }

    if (type.IsInterface)
    {
        return "interface";
    }

    if (type.IsDelegate)
    {
        return "delegate";
    }

    if (type.IsStaticClass)
    {
        return "static class";
    }

    if (type.IsValueType)
    {
        return "struct";
    }

    return "class";
}

static string DescribeTypeHandle(MetadataReader reader, EntityHandle handle)
{
    if (handle.IsNil)
    {
        return "<none>";
    }

    if (handle.Kind == HandleKind.TypeDefinition)
    {
        var typeDefinition = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
        return typeDefinition.ToFullNameString(reader);
    }

    if (handle.Kind == HandleKind.TypeReference)
    {
        var typeReference = reader.GetTypeReference((TypeReferenceHandle)handle);
        return typeReference.ToFullNameString(reader);
    }

    return $"<{handle.Kind}>";
}

static string DescribeBaseType(TypeContext type)
{
    if (!type.HasBaseType)
    {
        return "<none>";
    }

    var baseTypeName = type.GetBaseTypeName();
    if (baseTypeName != null)
    {
        return baseTypeName;
    }

    return type.DecodeBaseType()?.ToString() ?? "<unknown>";
}

static string FormatMethodSignature(string name, MethodSignature<TypeSignature> signature)
{
    var parameters = new string[signature.ParameterTypes.Length];
    for (int i = 0; i < signature.ParameterTypes.Length; i++)
    {
        parameters[i] = signature.ParameterTypes[i].ToString();
    }

    return $"{signature.ReturnType} {name}({string.Join(", ", parameters)})";
}

static string GetMethodFlags(MethodContext method)
{
    var flags = new List<string>();
    if (method.IsStatic)
    {
        flags.Add("static");
    }

    if (method.IsVirtual)
    {
        flags.Add("virtual");
    }

    if (method.IsAbstract)
    {
        flags.Add("abstract");
    }

    if (method.HasAttribute("System.Runtime.CompilerServices", "AsyncStateMachineAttribute"))
    {
        flags.Add("async");
    }

    if (flags.Count == 0)
    {
        return "<none>";
    }

    return string.Join(", ", flags);
}

static string GetPropertyAccessors(PropertyContext property)
{
    var accessors = new List<string>();
    if (property.Getter.IsValid)
    {
        accessors.Add("get");
    }

    if (property.Setter.IsValid)
    {
        accessors.Add("set");
    }

    if (accessors.Count == 0)
    {
        return "<none>";
    }

    return string.Join("/", accessors);
}

static string FormatAttributeArgument(DecodedCustomAttributeArgument argument)
{
    if (argument.IsArray)
    {
        var elements = argument.GetArrayElements();
        var values = new string[elements.Length];
        for (int i = 0; i < elements.Length; i++)
        {
            values[i] = FormatAttributeArgument(elements[i]);
        }

        return $"[{string.Join(", ", values)}]";
    }

    return argument.Value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        _ => argument.Value.ToString() ?? string.Empty
    };
}

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

sealed record SampleOptions(string AssemblyPath, string? TypeName, string[] SearchPaths);
