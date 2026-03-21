using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Extension methods for <see cref="TypeReference"/>.
/// Primarily fast tier identity checks with explicit rich formatting helpers.
/// </summary>
/// <remarks>
/// Allocation notes:
/// - Handle/comparer identity checks are fast tier and allocation free.
/// - <c>ToFullNameString</c> is rich tier and allocates managed strings.
/// </remarks>
public static class TypeReferenceExtensions
{
    // Identity checks

    /// <summary>
    /// Checks if the type reference matches the specified namespace and name.
    /// Zero allocation identity check using MetadataReader.StringComparer.
    /// </summary>
    /// <param name="typeRef">The type reference.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <param name="ns">The namespace to check (e.g., "System").</param>
    /// <param name="name">The name to check (e.g., "String", "List`1").</param>
    /// <returns>True if both namespace and name match.</returns>
    /// <remarks>
    /// For generic types, include the backtick and arity (e.g., "List`1").
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Is(this TypeReference typeRef, MetadataReader reader, string ns, string name)
    {
        return reader.StringComparer.Equals(typeRef.Namespace, ns) &&
               reader.StringComparer.Equals(typeRef.Name, name);
    }

    /// <summary>
    /// Checks if the type reference matches the specified namespace and name handles.
    /// Fast path identity check with no string materialization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Is(this TypeReference typeRef, StringHandle nameSpaceHandle, StringHandle nameHandle)
    {
        return (typeRef.Namespace == nameSpaceHandle) && (typeRef.Name == nameHandle);
    }

    /// <summary>
    /// Checks if the type reference name matches (ignores namespace).
    /// Zero allocation identity check.
    /// </summary>
    /// <param name="typeRef">The type reference.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <param name="name">The name to check (e.g., "String", "List`1").</param>
    /// <returns>True if the name matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NameIs(this TypeReference typeRef, MetadataReader reader, string name)
    {
        return reader.StringComparer.Equals(typeRef.Name, name);
    }

    /// <summary>
    /// Checks if the type reference name matches the specified name handle.
    /// Fast path identity check with no string materialization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NameIs(this TypeReference typeRef, StringHandle nameHandle)
    {
        return typeRef.Name == nameHandle;
    }

    /// <summary>
    /// Checks if the type reference namespace matches (ignores name).
    /// Zero allocation identity check.
    /// </summary>
    /// <param name="typeRef">The type reference.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <param name="ns">The namespace to check (e.g., "System").</param>
    /// <returns>True if the namespace matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NamespaceIs(this TypeReference typeRef, MetadataReader reader, string ns)
    {
        return reader.StringComparer.Equals(typeRef.Namespace, ns);
    }

    /// <summary>
    /// Checks if the type reference namespace matches the specified namespace handle.
    /// Fast path identity check with no string materialization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NamespaceIs(this TypeReference typeRef, StringHandle nameSpaceHandle)
    {
        return typeRef.Namespace == nameSpaceHandle;
    }

    /// <summary>
    /// Formats the type reference full name as a managed string.
    /// This is a rich tier formatting helper and allocates.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToFullNameString(this TypeReference typeRef, MetadataReader reader)
    {
        var ns = reader.GetString(typeRef.Namespace);
        var name = reader.GetString(typeRef.Name);
        return string.IsNullOrEmpty(ns) ? name : string.Concat(ns, ".", name);
    }
}
