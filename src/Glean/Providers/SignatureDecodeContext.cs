using System.ComponentModel;
using System.Collections.Immutable;

using Glean.Signatures;

namespace Glean.Providers;

/// <summary>
/// Represents the generic context for signature decoding.
/// </summary>
/// <remarks>
/// Most callers can use <see cref="Empty"/>. Populate this only when you are driving the raw System.Reflection.Metadata
/// decode APIs and need generic substitution.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public readonly struct SignatureDecodeContext
{
    /// <summary>
    /// Gets the type arguments (generic type parameters).
    /// </summary>
    public ImmutableArray<TypeSignature> TypeArguments { get; }

    /// <summary>
    /// Gets the method arguments (generic method parameters).
    /// </summary>
    public ImmutableArray<TypeSignature> MethodArguments { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SignatureDecodeContext"/> struct.
    /// </summary>
    public SignatureDecodeContext(
        ImmutableArray<TypeSignature> typeArguments = default,
        ImmutableArray<TypeSignature> methodArguments = default)
    {
        TypeArguments   = typeArguments.IsDefault   ? ImmutableArray<TypeSignature>.Empty : typeArguments;
        MethodArguments = methodArguments.IsDefault ? ImmutableArray<TypeSignature>.Empty : methodArguments;
    }

    /// <summary>
    /// Gets an empty generic context.
    /// </summary>
    public static SignatureDecodeContext Empty => default;
}
