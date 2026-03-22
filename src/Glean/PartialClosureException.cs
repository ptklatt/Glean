using System.Text;
using Glean.Resolution;

namespace Glean;

/// <summary>
/// Thrown by <see cref="AssemblyClosure.ThrowIfPartial"/> when one or more transitive
/// dependencies were skipped.
/// </summary>
/// <remarks>
/// Inspect <see cref="SkippedDependencies"/> for details. If a partial closure is acceptable,
/// inspect <see cref="AssemblyClosure.SkippedDependencies"/> instead of calling
/// <see cref="AssemblyClosure.ThrowIfPartial"/>.
/// </remarks>
public sealed class PartialClosureException : InvalidOperationException
{
    private const int PreviewDependencyCount = 5;

    /// <summary>Gets the list of dependencies that could not be loaded.</summary>
    public IReadOnlyList<AssemblyDependencyLoadFailure> SkippedDependencies { get; }

    internal PartialClosureException(IReadOnlyList<AssemblyDependencyLoadFailure> skipped)
        : base(BuildMessage(skipped))
    {
        SkippedDependencies = skipped;
    }

    private static string BuildMessage(IReadOnlyList<AssemblyDependencyLoadFailure> skipped)
    {
        var sb = new StringBuilder("Assembly closure is partial: ");
        sb.Append(skipped.Count);
        sb.Append(skipped.Count == 1 ? " dependency" : " dependencies");
        sb.Append(" could not be loaded (");
        int shown = Math.Min(skipped.Count, PreviewDependencyCount);
        for (int i = 0; i < shown; i++)
        {
            if (i > 0) { sb.Append(", "); }
            sb.Append(skipped[i].AssemblySimpleName);
        }
        if (skipped.Count > PreviewDependencyCount) { sb.Append(", ..."); }
        sb.Append("). Check SkippedDependencies for details.");
        return sb.ToString();
    }
}
