using System.Reflection.Metadata;

namespace Glean.Resolution;

/// <summary>Describes an assembly reference bound to a different version.</summary>
public readonly record struct AssemblyBindingSubstitution
{
    internal AssemblyBindingSubstitution(string requestedIdentity, string resolvedIdentity, string? path)
    {
        RequestedIdentity = requestedIdentity;
        ResolvedIdentity = resolvedIdentity;
        Path = path;
    }

    /// <summary>Gets the requested full assembly identity.</summary>
    public string RequestedIdentity { get; }

    /// <summary>Gets the resolved full assembly identity.</summary>
    public string ResolvedIdentity { get; }

    /// <summary>
    /// Gets the resolved file path, or null when a manually registered reader or custom
    /// resolver does not supply a path.
    /// </summary>
    public string? Path { get; }
}

internal interface IAssemblyPathProvider
{
    string? GetAssemblyPath(MetadataReader reader);
}
