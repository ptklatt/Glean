using System.ComponentModel;

using Glean.Contexts;
using Glean.Resolution;
using Glean.Signatures;

namespace Glean.Extensions;

/// <summary>
/// Resolution helpers for rich signature objects.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class TypeSignatureResolutionExtensions
{
    /// <summary>
    /// Resolves a named type reference signature to its definition.
    /// </summary>
    public static bool TryResolve(
        this TypeReferenceSignature typeReference,
        AssemblySet assemblySet,
        out TypeContext resolved)
    {
        if (typeReference == null)
        {
            throw new ArgumentNullException(nameof(typeReference));
        }

        var context = TypeReferenceContext.Create(typeReference.Reader, typeReference.Handle);
        return context.TryResolve(assemblySet, out resolved);
    }

    /// <summary>
    /// Resolves a named type reference signature and preserves the failure reason on false.
    /// </summary>
    public static bool TryResolve(
        this TypeReferenceSignature typeReference,
        AssemblySet assemblySet,
        out TypeContext resolved,
        out ResolutionFailureReason reason)
    {
        if (typeReference == null)
        {
            throw new ArgumentNullException(nameof(typeReference));
        }

        var context = TypeReferenceContext.Create(typeReference.Reader, typeReference.Handle);
        return context.TryResolve(assemblySet, out resolved, out reason);
    }

    /// <summary>
    /// Resolves whether a type reference signature points to a value type.
    /// </summary>
    public static bool? ResolveIsValueType(
        this TypeReferenceSignature typeReference,
        AssemblySet assemblySet)
    {
        if (typeReference.TryResolve(assemblySet, out var resolved))
        {
            return resolved.IsValueType;
        }

        return null;
    }
}
