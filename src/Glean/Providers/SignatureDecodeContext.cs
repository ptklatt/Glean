using System.Collections.Immutable;
using Glean.Signatures;

namespace Glean.Providers;

/// <summary>
/// Represents the generic context for signature decoding.
/// </summary>
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
