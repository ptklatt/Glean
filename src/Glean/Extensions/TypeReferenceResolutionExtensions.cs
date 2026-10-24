using System.Reflection.Metadata;

using Glean.Contexts;
using Glean.Resolution;

namespace Glean.Extensions;

/// <summary>
/// Resolution helpers for <see cref="TypeReferenceContext"/>.
/// </summary>
public static class TypeReferenceResolutionExtensions
{
    /// <summary>
    /// Resolves a type reference to its definition across the registered assembly set.
    /// </summary>
    public static bool TryResolve(
        this TypeReferenceContext typeReference,
        AssemblySet assemblySet,
        out TypeContext resolved)
    {
        if (assemblySet == null)
        {
            throw new ArgumentNullException(nameof(assemblySet));
        }

        if (assemblySet.TryResolveType(
            typeReference.Reader,
            typeReference.Handle,
            out var targetReader,
            out var targetHandle))
        {
            resolved = TypeContext.Create(targetReader, targetHandle);
            return true;
        }

        resolved = default;
        return false;
    }

    /// <summary>
    /// Resolves a type reference to its definition across the registered assembly set and returns a failure reason on false.
    /// </summary>
    public static bool TryResolve(
        this TypeReferenceContext typeReference,
        AssemblySet assemblySet,
        out TypeContext resolved,
        out ResolutionFailureReason reason)
    {
        if (assemblySet == null)
        {
            throw new ArgumentNullException(nameof(assemblySet));
        }

        if (assemblySet.TryResolveType(
            typeReference.Reader,
            typeReference.Handle,
            out var targetReader,
            out var targetHandle,
            out reason))
        {
            resolved = TypeContext.Create(targetReader, targetHandle);
            return true;
        }

        resolved = default;
        return false;
    }
}
