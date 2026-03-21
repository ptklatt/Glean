using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Enumerators;

namespace Glean.Extensions;

/// <summary>
/// Extensions for enumerating reference tables (AssemblyReference, TypeReference, MemberReference).
/// </summary>
/// <remarks>
/// Direct raw table enumeration (<c>MetadataReader.AssemblyReferences</c>, <c>TypeReferences</c>, <c>MemberReferences</c>).
/// </remarks>
public static class ReferenceTableExtensions
{
    /// <summary>
    /// Enumerates all assembly references in the metadata.
    /// Returns struct enumerator for zero allocation iteration.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AssemblyReferenceEnumerator EnumerateAssemblyReferences(this MetadataReader reader)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }

        return AssemblyReferenceEnumerator.Create(reader, reader.AssemblyReferences);
    }

    /// <summary>
    /// Enumerates all type references in the metadata.
    /// Returns struct enumerator for zero allocation iteration.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TypeReferenceEnumerator EnumerateTypeReferences(this MetadataReader reader)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }

        return TypeReferenceEnumerator.Create(reader, reader.TypeReferences);
    }

    /// <summary>
    /// Enumerates all member references in the metadata.
    /// Returns struct enumerator for zero allocation iteration.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemberReferenceEnumerator EnumerateMemberReferences(this MetadataReader reader)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }

        return MemberReferenceEnumerator.Create(reader, reader.MemberReferences);
    }
}
