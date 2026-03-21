using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for CustomAttribute.
/// </summary>
public readonly struct CustomAttributeContext : IEquatable<CustomAttributeContext>
{
    private readonly MetadataReader _reader;
    private readonly CustomAttributeHandle _handle;
    private readonly CustomAttribute _attribute;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CustomAttributeContext Create(MetadataReader reader, CustomAttributeHandle handle)
    {
        if (reader is null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil) { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new CustomAttributeContext(reader, handle, reader.GetCustomAttribute(handle));
    }

    /// <summary>
    /// Internal unchecked factory used by enumerators. Reader and handle are pre validated by the enumerator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static CustomAttributeContext UnsafeCreate(MetadataReader reader, CustomAttributeHandle handle)
        => new CustomAttributeContext(reader, handle, reader.GetCustomAttribute(handle));

    private CustomAttributeContext(MetadataReader reader, CustomAttributeHandle handle, CustomAttribute attribute)
    {
        _reader = reader;
        _handle = handle;
        _attribute = attribute;
    }

    public CustomAttributeHandle Handle => _handle;
    public CustomAttribute Attribute => _attribute;
    public MetadataReader Reader => _reader;

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader is not null && !_handle.IsNil;
    }

    /// <summary>
    /// Gets the constructor handle (MethodDef or MemberRef).
    /// </summary>
    public EntityHandle Constructor => _attribute.Constructor;

    /// <summary>
    /// Gets the parent handle (the entity this attribute is attached to).
    /// </summary>
    public EntityHandle Parent => _attribute.Parent;

    /// <summary>
    /// Returns the constructor as a <see cref="MethodContext"/> when it resolves within the same module.
    /// </summary>
    /// <param name="method">The resolved method context when true is returned; default when false.</param>
    /// <returns>True when <see cref="Constructor"/> is a <see cref="MethodDefinitionHandle"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetConstructorDefinition(out MethodContext method)
    {
        if (_attribute.Constructor.Kind == HandleKind.MethodDefinition)
        {
            method = MethodContext.Create(_reader, (MethodDefinitionHandle)_attribute.Constructor);
            return true;
        }

        method = default;
        return false;
    }

    /// <summary>
    /// Returns the constructor as a <see cref="MemberReferenceContext"/> when it is a cross assembly reference.
    /// </summary>
    /// <param name="memberRef">The resolved member reference context when true is returned; default when false.</param>
    /// <returns>True when <see cref="Constructor"/> is a <see cref="MemberReferenceHandle"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetConstructorReference(out MemberReferenceContext memberRef)
    {
        if (_attribute.Constructor.Kind == HandleKind.MemberReference)
        {
            memberRef = MemberReferenceContext.Create(_reader, (MemberReferenceHandle)_attribute.Constructor);
            return true;
        }

        memberRef = default;
        return false;
    }

    /// <summary>
    /// Gets the attribute value blob handle.
    /// </summary>
    /// <remarks>
    /// Use DecodeValue() to decode this blob into constructor arguments.
    /// We expose the handle (not the decoded value) to keep the struct lightweight.
    /// </remarks>
    public BlobHandle ValueHandle => _attribute.Value;

    /// <summary>
    /// Decodes the custom attribute value using the provided type provider.
    /// </summary>
    /// <typeparam name="TType">The type returned by the provider.</typeparam>
    /// <param name="provider">The custom attribute type provider.</param>
    /// <returns>The decoded custom attribute value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CustomAttributeValue<TType> DecodeValue<TType>(ICustomAttributeTypeProvider<TType> provider)
    {
        return _attribute.DecodeValue(provider);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(CustomAttributeContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    public override bool Equals(object? obj)
    {
        return obj is CustomAttributeContext other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    public static bool operator ==(CustomAttributeContext left, CustomAttributeContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CustomAttributeContext left, CustomAttributeContext right)
    {
        return !left.Equals(right);
    }
}
