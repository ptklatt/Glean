using System.Collections.Immutable;
using System.Reflection.Metadata;

using Glean.Providers;
using Glean.Resolution;
using Glean.Signatures;

namespace Glean.Decoding;

/// <summary>
/// Decodes custom attributes into <see cref="DecodedCustomAttribute"/> results.
/// </summary>
public static class CustomAttributeDecoder
{
    /// <summary>
    /// Decodes a custom attribute from a handle.
    /// </summary>
    /// <remarks>
    /// This API allocates decoded object graphs.
    /// </remarks>
    public static DecodedCustomAttribute Decode(
        MetadataReader reader,
        CustomAttributeHandle handle,
        IEnumResolver? enumResolver = null)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil) { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        var attribute = reader.GetCustomAttribute(handle);

        var attributeType = CustomAttributeTypeResolver.GetAttributeType(reader, attribute.Constructor);

        var provider = new CustomAttributeTypeProvider(reader, enumResolver);
        var value = attribute.DecodeValue(provider);

        var fixedArgs = ConvertFixedArguments(value.FixedArguments);
        var namedArgs = ConvertNamedArguments(value.NamedArguments);

        return new DecodedCustomAttribute(attributeType, fixedArgs, namedArgs);
    }

    private static ImmutableArray<DecodedCustomAttributeArgument> ConvertFixedArguments(
        ImmutableArray<CustomAttributeTypedArgument<TypeSignature>> args)
    {
        if (args.IsDefaultOrEmpty) { return ImmutableArray<DecodedCustomAttributeArgument>.Empty; }

        var builder = ImmutableArray.CreateBuilder<DecodedCustomAttributeArgument>(args.Length);
        foreach (var arg in args)
        {
            builder.Add(ConvertTypedArgument(arg));
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<DecodedCustomAttributeNamedArgument> ConvertNamedArguments(
        ImmutableArray<CustomAttributeNamedArgument<TypeSignature>> args)
    {
        if (args.IsDefaultOrEmpty) { return ImmutableArray<DecodedCustomAttributeNamedArgument>.Empty; }

        var builder = ImmutableArray.CreateBuilder<DecodedCustomAttributeNamedArgument>(args.Length);
        foreach (var arg in args)
        {
            var value = ConvertTypedArgument(new CustomAttributeTypedArgument<TypeSignature>(arg.Type, arg.Value));
            builder.Add(new DecodedCustomAttributeNamedArgument(arg.Kind, arg.Name ?? string.Empty, arg.Type, value));
        }

        return builder.MoveToImmutable();
    }

    private static DecodedCustomAttributeArgument ConvertTypedArgument(CustomAttributeTypedArgument<TypeSignature> srmArg)
    {
        DecodedCustomAttributeArgument result;
        if (srmArg.Value is ImmutableArray<CustomAttributeTypedArgument<TypeSignature>> srmArray)
        {
            var builder = ImmutableArray.CreateBuilder<DecodedCustomAttributeArgument>(srmArray.Length);
            foreach (var item in srmArray)
            {
                builder.Add(ConvertTypedArgument(item));
            }

            result = new DecodedCustomAttributeArgument(srmArg.Type, builder.MoveToImmutable());
        }
        else
        {
            result = new DecodedCustomAttributeArgument(srmArg.Type, srmArg.Value);
        }

        return result;
    }
}
