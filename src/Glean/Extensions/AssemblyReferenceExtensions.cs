using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Extension methods for <see cref="AssemblyReference"/>.
/// Zero allocation identity checks and metadata access.
/// </summary>
/// <remarks>
/// These extensions operate directly on the System.Reflection.Metadata <see cref="AssemblyReference"/> struct.
/// Identity checks use MetadataReader.StringComparer to avoid allocating strings.
/// </remarks>
public static class AssemblyReferenceExtensions
{
    // == Identity checks =====================================================

    /// <summary>
    /// Checks if the assembly reference name matches.
    /// Zero allocation identity check using MetadataReader.StringComparer.
    /// </summary>
    /// <param name="assemblyRef">The assembly reference.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <param name="name">The assembly name to check (e.g., "System.Runtime", "mscorlib").</param>
    /// <returns>True if the name matches.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NameIs(this AssemblyReference assemblyRef, MetadataReader reader, string name)
    {
        return reader.StringComparer.Equals(assemblyRef.Name, name);
    }

    /// <summary>
    /// Checks if the assembly reference matches the specified name and optionally version.
    /// Zero allocation identity check using MetadataReader.StringComparer.
    /// </summary>
    /// <param name="assemblyRef">The assembly reference.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <param name="name">The assembly name to check (e.g., "System.Runtime").</param>
    /// <param name="version">Optional version to match. If null, only name is checked.</param>
    /// <returns>True if the name (and version, if specified) match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Is(this AssemblyReference assemblyRef, MetadataReader reader, string name, Version? version = null)
    {
        if (!reader.StringComparer.Equals(assemblyRef.Name, name)) { return false; }

        if (version != null)
        {
            return assemblyRef.Version == version;
        }

        return true;
    }
}
