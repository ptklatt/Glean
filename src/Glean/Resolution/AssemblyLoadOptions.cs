namespace Glean.Resolution;

/// <summary>Controls how assembly references are matched to definitions.</summary>
public enum AssemblyBindingPolicy
{
    /// <summary>Requires the name, version, normalized culture, and public key token to match.</summary>
    Exact,

    /// <summary>
    /// Prefers an exact identity, then permits a different version with the same name,
    /// normalized culture, and public key token. Probe path order determines the fallback.
    /// </summary>
    ExactThenCompatible
}

/// <summary>Configures assembly probing and identity matching.</summary>
/// <remarks>Loaders copy these options and search paths when they are constructed.</remarks>
public sealed class AssemblyLoadOptions
{
    /// <summary>Gets the directories to probe, in priority order.</summary>
    public IReadOnlyList<string> SearchPaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the target framework to probe in NuGet layouts, or null to disable layout probing.
    /// </summary>
    public string? TargetFrameworkMoniker { get; init; }

    /// <summary>Gets the binding policy. The default requires an exact identity.</summary>
    public AssemblyBindingPolicy BindingPolicy { get; init; } = AssemblyBindingPolicy.Exact;
}
