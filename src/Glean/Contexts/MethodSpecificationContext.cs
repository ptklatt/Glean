using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Enumerators;
using Glean.Providers;
using Glean.Signatures;

namespace Glean.Contexts;

/// <summary>A generic method instantiation row and its owning metadata reader.</summary>
/// <remarks>This context borrows its reader. Keep the reader's owner alive while accessing metadata.</remarks>
public readonly struct MethodSpecificationContext : IEquatable<MethodSpecificationContext>
{
    private readonly MetadataReader _reader;
    private readonly MethodSpecificationHandle _handle;
    private readonly MethodSpecification _specification;

    /// <summary>Creates a context over the specified metadata reader and row handle.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="handle"/> is nil.</exception>
    public static MethodSpecificationContext Create(MetadataReader reader, MethodSpecificationHandle handle)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil) { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new MethodSpecificationContext(reader, handle);
    }

    private MethodSpecificationContext(MetadataReader reader, MethodSpecificationHandle handle)
    {
        _reader = reader;
        _handle = handle;
        _specification = reader.GetMethodSpecification(handle);
    }

    /// <summary>Gets the row handle within this context's reader.</summary>
    public MethodSpecificationHandle Handle => _handle;
    /// <summary>Gets the underlying method specification row.</summary>
    public MethodSpecification Specification => _specification;
    /// <summary>Gets the borrowed reader that contains this context's metadata.</summary>
    public MetadataReader Reader => _reader;
    /// <summary>Checks for a reader and a row handle that is not nil.</summary>
    /// <remarks>This does not validate metadata or check whether its owner has been disposed.</remarks>
    public bool IsValid => (_reader != null) && !_handle.IsNil;
    /// <summary>Gets the method definition or member reference being instantiated.</summary>
    public EntityHandle Method => _specification.Method;
    /// <summary>Gets the instantiation signature blob handle without decoding it.</summary>
    public BlobHandle SignatureHandle => _specification.Signature;

    /// <summary>Enumerates custom attributes attached to this instantiation row.</summary>
    public CustomAttributeEnumerator EnumerateCustomAttributes()
        => CustomAttributeEnumerator.Create(_reader, _specification.GetCustomAttributes());

    /// <summary>Decodes the instantiation's type arguments using Glean's model. Decoding may allocate.</summary>
    /// <remarks>Open parameters remain symbolic. This does not resolve the target method or decode its parameter and return types.</remarks>
    /// <exception cref="BadImageFormatException">The metadata reader cannot decode the instantiation signature.</exception>
    public ImmutableArray<TypeSignature> DecodeSignature()
        => _specification.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);

    /// <summary>Decodes the instantiation's type arguments using the supplied provider and context. Decoding may allocate.</summary>
    /// <remarks>This does not resolve the target method or decode its parameter and return types.</remarks>
    /// <param name="provider">Produces the decoded type arguments.</param>
    /// <param name="genericContext">The referring type and method's context, used to interpret open generic parameters in the argument blob.</param>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    /// <exception cref="BadImageFormatException">The metadata reader cannot decode the instantiation signature.</exception>
    public ImmutableArray<TType> DecodeSignature<TType, TContext>(ISignatureTypeProvider<TType, TContext> provider, TContext genericContext)
    {
        if (provider == null) { throw new ArgumentNullException(nameof(provider)); }

        return _specification.DecodeSignature(provider, genericContext);
    }

    /// <summary>Checks whether both contexts refer to the same reader and row handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(MethodSpecificationContext other)
        => (_handle == other._handle) && ReferenceEquals(_reader, other._reader);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => (obj is MethodSpecificationContext other) && Equals(other);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_reader, _handle);
    /// <summary>Checks whether the contexts refer to the same metadata.</summary>
    public static bool operator ==(MethodSpecificationContext left, MethodSpecificationContext right) => left.Equals(right);
    /// <summary>Checks whether the contexts refer to different metadata.</summary>
    public static bool operator !=(MethodSpecificationContext left, MethodSpecificationContext right) => !left.Equals(right);
}
