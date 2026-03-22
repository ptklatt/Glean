using System.Reflection.Metadata;
namespace Glean.Resolution;

/// <summary>
/// Resolves module references (multi module assemblies) to metadata readers.
/// </summary>
public interface IModuleResolver
{
    /// <summary>
    /// Attempts to resolve a module reference to a loaded module.
    /// </summary>
    /// <param name="reference">The module reference to resolve.</param>
    /// <param name="requestingReader">The metadata reader that contains the reference.</param>
    /// <param name="resolvedReader">
    /// The resolved metadata reader, or null if not found. Implementations must keep the underlying
    /// metadata alive for as long as callers may use the returned reader.
    /// </param>
    /// <returns>True if the module was resolved; otherwise false.</returns>
    bool TryResolve(ModuleReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader);
}
