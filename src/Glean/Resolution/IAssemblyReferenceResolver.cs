using System.ComponentModel;
using System.Reflection.Metadata;

namespace Glean.Resolution;

/// <summary>
/// Resolves assembly references to metadata readers.
/// Implement this to provide custom assembly loading strategies.
/// </summary>
/// <seealso cref="DirectoryAssemblyResolver"/>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface IAssemblyReferenceResolver
{
    /// <summary>
    /// Attempts to resolve an assembly reference to a loaded assembly.
    /// </summary>
    /// <param name="reference">The assembly reference to resolve.</param>
    /// <param name="requestingReader">The MetadataReader that contains the reference.</param>
    /// <param name="resolvedReader">
    /// The resolved MetadataReader, or null if not found. Implementations must keep the underlying
    /// metadata alive for as long as callers may use the returned reader.
    /// </param>
    /// <returns>True if the assembly was resolved; otherwise false.</returns>
    bool TryResolve(AssemblyReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader);
}
