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
    /// Resolves whether a type reference signature points to a value type.
    /// </summary>
    public static bool? ResolveIsValueType(
        this TypeReferenceSignature typeReference,
        AssemblySet assemblySet)
    {
        if (assemblySet == null)
        {
            throw new ArgumentNullException(nameof(assemblySet));
        }

        var context = TypeReferenceContext.Create(typeReference.Reader, typeReference.Handle);
        if (context.TryResolve(assemblySet, out var resolved))
        {
            return resolved.IsValueType;
        }

        return null;
    }
}
