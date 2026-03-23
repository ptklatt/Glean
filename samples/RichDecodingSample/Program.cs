using Glean;
using Glean.Contexts;
using Glean.Decoding;

if ((args.Length < 1) || (args.Length > 2))
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/RichDecodingSample -- <assembly-path> [full-type-name]");
    return 1;
}

string? requestedTypeName = args.Length == 2 ? args[1] : null;

using var scope = AssemblyScope.Open(args[0]);
var selectedType = FindType(scope.Context, requestedTypeName);
if (!selectedType.IsValid)
{
    Console.Error.WriteLine(requestedTypeName == null
        ? "No type definitions were found in the assembly."
        : $"Type '{requestedTypeName}' was not found.");
    return 1;
}

Console.WriteLine($"Type: {selectedType.FullName}");

var underlyingType = selectedType.DecodeEnumUnderlyingType();
if (underlyingType != null)
{
    Console.WriteLine($"Enum underlying type: {underlyingType}");
}

Console.WriteLine();
Console.WriteLine("Methods:");

int printedMethods = 0;
foreach (var method in selectedType.EnumerateMethods())
{
    if (method.IsConstructor)
    {
        continue;
    }

    Console.WriteLine($"  {method.DecodeReturnType()} {method.Name}");
    printedMethods++;
    if (printedMethods == 5)
    {
        break;
    }
}

if (printedMethods == 0)
{
    Console.WriteLine("  <none>");
}

Console.WriteLine();
Console.WriteLine("Custom attributes:");

int printedAttributes = 0;
foreach (var attribute in selectedType.EnumerateCustomAttributes())
{
    printedAttributes++;

    try
    {
        var decoded = CustomAttributeDecoder.Decode(scope.Reader, attribute.Handle);
        PrintDecodedAttribute(decoded);
    }
    catch (BadImageFormatException ex)
    {
        Console.WriteLine($"  <decode failed: {ex.Message}>");
    }

    if (printedAttributes == 3)
    {
        break;
    }
}

if (printedAttributes == 0)
{
    Console.WriteLine("  <none>");
}

return 0;

static TypeContext FindType(AssemblyContext assembly, string? requestedTypeName)
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

static void PrintDecodedAttribute(DecodedCustomAttribute decoded)
{
    Console.WriteLine($"  {decoded.AttributeType}");

    foreach (var argument in decoded.FixedArguments)
    {
        Console.WriteLine($"    ctor: {argument.Type} = {FormatArgument(argument)}");
    }

    foreach (var argument in decoded.NamedArguments)
    {
        Console.WriteLine($"    {argument.Kind} {argument.Name}: {FormatArgument(argument.Value)}");
    }
}

static string FormatArgument(DecodedCustomAttributeArgument argument)
{
    if (argument.IsArray)
    {
        var elements = argument.GetArrayElements();
        if (elements.Length == 0)
        {
            return "[]";
        }

        var parts = new string[elements.Length];
        for (int i = 0; i < elements.Length; i++)
        {
            parts[i] = FormatArgument(elements[i]);
        }

        return $"[{string.Join(", ", parts)}]";
    }

    return argument.Value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        _ => argument.Value.ToString() ?? string.Empty
    };
}
