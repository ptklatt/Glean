using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Enumerators;
using Glean.Extensions;
using Glean.Signatures;

namespace Glean.Contexts;

/// <summary>A generic parameter constraint row and its owning metadata reader.</summary>
/// <remarks>This context borrows its reader. Keep the reader's owner alive while accessing metadata.</remarks>
public readonly struct GenericParameterConstraintContext : IEquatable<GenericParameterConstraintContext>
{
    private readonly MetadataReader _reader;
    private readonly GenericParameterConstraintHandle _handle;
    private readonly GenericParameterConstraint _constraint;

    /// <summary>Creates a context over the specified metadata reader and row handle.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="handle"/> is nil.</exception>
    public static GenericParameterConstraintContext Create(MetadataReader reader, GenericParameterConstraintHandle handle)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil) { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new GenericParameterConstraintContext(reader, handle);
    }

    private GenericParameterConstraintContext(MetadataReader reader, GenericParameterConstraintHandle handle)
    {
        _reader = reader;
        _handle = handle;
        _constraint = reader.GetGenericParameterConstraint(handle);
    }

    /// <summary>Gets the row handle within this context's reader.</summary>
    public GenericParameterConstraintHandle Handle => _handle;
    /// <summary>Gets the underlying constraint row.</summary>
    public GenericParameterConstraint Constraint => _constraint;
    /// <summary>Gets the borrowed reader that contains this context's metadata.</summary>
    public MetadataReader Reader => _reader;
    /// <summary>Checks for a reader and a row handle that is not nil.</summary>
    /// <remarks>This does not validate metadata or check whether its owner has been disposed.</remarks>
    public bool IsValid => (_reader != null) && !_handle.IsNil;
    /// <summary>Gets the generic parameter constrained by this row.</summary>
    public GenericParameterHandle Parameter => _constraint.Parameter;
    /// <summary>Gets the constraint type handle without decoding its signature.</summary>
    public EntityHandle Type => _constraint.Type;

    /// <summary>Enumerates custom attributes attached to this constraint row.</summary>
    public CustomAttributeEnumerator EnumerateCustomAttributes()
        => CustomAttributeEnumerator.Create(_reader, _constraint.GetCustomAttributes());

    /// <summary>Decodes the constraint type using Glean's signature model. Decoding may allocate.</summary>
    public TypeSignature DecodeType() => _constraint.DecodeConstraintTypeSignature(_reader);

    /// <summary>Decodes the constraint type using the supplied provider and generic context. Decoding may allocate.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    public TType DecodeType<TType, TContext>(ISignatureTypeProvider<TType, TContext> provider, TContext genericContext)
        => _constraint.DecodeConstraintTypeSignature(_reader, provider, genericContext);

    /// <summary>Checks whether both contexts refer to the same reader and row handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(GenericParameterConstraintContext other)
        => (_handle == other._handle) && ReferenceEquals(_reader, other._reader);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => (obj is GenericParameterConstraintContext other) && Equals(other);
    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_reader, _handle);
    /// <summary>Checks whether the contexts refer to the same metadata.</summary>
    public static bool operator ==(GenericParameterConstraintContext left, GenericParameterConstraintContext right)
        => left.Equals(right);
    /// <summary>Checks whether the contexts refer to different metadata.</summary>
    public static bool operator !=(GenericParameterConstraintContext left, GenericParameterConstraintContext right)
        => !left.Equals(right);
}
