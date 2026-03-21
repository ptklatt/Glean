using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Resolution;

namespace Glean.Extensions;

/// <summary>
/// Extension methods for <see cref="CustomAttribute"/>.
/// </summary>
public static class CustomAttributeExtensions
{
    /// <summary>
    /// Checks whether the attribute's declaring type matches the specified namespace and name.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAttributeType(
        this CustomAttribute attribute,
        MetadataReader reader,
        string ns,
        string name)
    {
        if (!CustomAttributeTypeResolver.TryGetAttributeTypeNameHandles(
                reader,
                attribute.Constructor,
                out var actualNs,
                out var actualName))
        {
            return false;
        }

        return reader.StringComparer.Equals(actualNs, ns) &&
               reader.StringComparer.Equals(actualName, name);
    }

    /// <summary>
    /// Searches a custom attribute handle collection for the first attribute whose type matches the specified name.
    /// </summary>
    public static bool TryFindAttribute(
        this CustomAttributeHandleCollection attributes,
        MetadataReader reader,
        string ns,
        string name,
        out CustomAttribute found)
    {
        foreach (var handle in attributes)
        {
            var attribute = reader.GetCustomAttribute(handle);
            if (attribute.IsAttributeType(reader, ns, name))
            {
                found = attribute;
                return true;
            }
        }

        found = default;
        return false;
    }

    /// <summary>
    /// Searches a custom attribute handle collection for the first attribute whose type matches the specified name.
    /// </summary>
    public static bool TryFindAttributeHandle(
        this CustomAttributeHandleCollection attributes,
        MetadataReader reader,
        string ns,
        string name,
        out CustomAttributeHandle foundHandle)
    {
        foreach (var handle in attributes)
        {
            var attribute = reader.GetCustomAttribute(handle);
            if (attribute.IsAttributeType(reader, ns, name))
            {
                foundHandle = handle;
                return true;
            }
        }

        foundHandle = default;
        return false;
    }

    /// <summary>
    /// Checks if the custom attribute constructor is a method definition.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMethodDefinition(this CustomAttribute attribute)
    {
        return attribute.Constructor.Kind == HandleKind.MethodDefinition;
    }

    /// <summary>
    /// Checks if the custom attribute constructor is a member reference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsMemberReference(this CustomAttribute attribute)
    {
        return attribute.Constructor.Kind == HandleKind.MemberReference;
    }

    /// <summary>
    /// Tries to resolve the attribute's declaring type namespace and name handles for zero allocation comparison.
    /// </summary>
    /// <param name="handle">The custom attribute handle.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <param name="ns">The namespace StringHandle of the attribute's declaring type.</param>
    /// <param name="name">The name StringHandle of the attribute's declaring type.</param>
    /// <returns>True if type name was resolved successfully; otherwise false.</returns>
    /// <remarks>
    /// Resolves the attribute constructor (MethodDefinition or MemberReference) to its declaring type
    /// (TypeDefinition or TypeReference), then extracts the namespace and name handles for zero allocation
    /// string comparison using <see cref="MetadataReader.StringComparer"/>.
    /// <code>
    /// if (attrHandle.TryGetAttributeTypeNameHandles(reader, out var ns, out var name))
    /// {
    ///     if (reader.StringComparer.Equals(ns, "System") &amp;&amp;
    ///         reader.StringComparer.Equals(name, "ObsoleteAttribute"))
    ///     {
    ///         // Found [Obsolete] attribute
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public static bool TryGetAttributeTypeNameHandles(
        this CustomAttributeHandle handle,
        MetadataReader reader,
        out StringHandle ns,
        out StringHandle name)
    {
        var attribute = reader.GetCustomAttribute(handle);
        return CustomAttributeTypeResolver.TryGetAttributeTypeNameHandles(
            reader,
            attribute.Constructor,
            out ns,
            out name);
    }
}
