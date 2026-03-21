using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Extensions;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for FieldDefinition.
/// </summary>
public readonly struct FieldContext : IEquatable<FieldContext>
{
    private readonly MetadataReader _reader;
    private readonly FieldDefinitionHandle _handle;
    private readonly FieldDefinition _definition;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FieldContext Create(MetadataReader reader, FieldDefinitionHandle handle)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil) { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new FieldContext(reader, handle, reader.GetFieldDefinition(handle));
    }

    /// <summary>
    /// Internal unchecked factory used by enumerators. Reader and handle are pre validated by the enumerator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FieldContext UnsafeCreate(MetadataReader reader, FieldDefinitionHandle handle)
        => new FieldContext(reader, handle, reader.GetFieldDefinition(handle));

    private FieldContext(MetadataReader reader, FieldDefinitionHandle handle, FieldDefinition definition)
    {
        _reader = reader;
        _handle = handle;
        _definition = definition;
    }

    public FieldDefinitionHandle Handle => _handle;
    public FieldDefinition Definition => _definition;
    public MetadataReader Reader => _reader;

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader is not null && !_handle.IsNil;
    }

    /// <summary>
    /// Gets the field name.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_definition.Name);
    }

    /// <summary>
    /// Checks whether the field name matches the specified name string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NameIs(string name)
    {
        return _reader.StringComparer.Equals(_definition.Name, name);
    }

    /// <summary>
    /// Gets the field name handle for zero allocation comparisons.
    /// </summary>
    public StringHandle NameHandle => _definition.Name;

    public FieldAttributes Attributes => _definition.Attributes;

    /// <summary>
    /// Gets the field offset for explicit layout fields.
    /// </summary>
    /// <remarks>
    /// Returns the offset in bytes from the start of the containing type.
    /// Only valid for fields in types with Explicit layout.
    /// </remarks>
    public int Offset
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _definition.GetOffset();
    }

    /// <summary>
    /// Gets the field signature blob handle.
    /// </summary>
    /// <remarks>
    /// Use DecodeSignature() to decode this blob into a type signature.
    /// We expose the handle (not the decoded signature) to keep the struct lightweight.
    /// </remarks>
    public BlobHandle SignatureHandle => _definition.Signature;

    /// <summary>
    /// Gets the marshal descriptor blob handle.
    /// </summary>
    /// <remarks>
    /// Use DecodeMarshalDescriptor() to decode marshaling information.
    /// Returns default(BlobHandle) if the field has no marshaling descriptor.
    /// </remarks>
    public BlobHandle MarshalDescriptorHandle
    {
        get
        {
            var marshallingDesc = _definition.GetMarshallingDescriptor();
            return marshallingDesc;
        }
    }

    public TypeContext DeclaringType
    {
        get
        {
            var declaringTypeHandle = _definition.GetDeclaringType();
            return TypeContext.Create(_reader, declaringTypeHandle);
        }
    }

    /// <summary>
    /// True if this field is marked with <c>[CompilerGenerated]</c>.
    /// </summary>
    public bool IsCompilerGenerated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => HasAttribute(WellKnownTypes.SystemRuntimeCompilerServicesNs, WellKnownTypes.CompilerGeneratedAttribute);
    }

    /// <summary>
    /// True if this field is an auto property backing field (compiler generated, name ends with <c>k__BackingField</c>).
    /// </summary>
    /// <remarks>
    /// Allocates; calls <see cref="MetadataReader.GetString(StringHandle)"/> to materialize the field name for suffix comparison.
    /// Unlike other <c>Is*</c> properties (which are zero allocation flag checks), this property performs a string allocation.
    /// Use <see cref="NameIs"/> for zero allocation name comparisons.
    /// </remarks>
    public bool IsBackingField
    {
        get
        {
            if (!IsCompilerGenerated) return false;
            var name = _reader.GetString(_definition.Name);
            return name.EndsWith("k__BackingField", StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Decodes the field signature using the provided signature type provider.
    /// </summary>
    /// <typeparam name="TType">The type returned by the signature provider.</typeparam>
    /// <typeparam name="TSignatureDecodeContext">The generic context type.</typeparam>
    /// <param name="provider">The signature type provider.</param>
    /// <param name="genericContext">The generic context for type parameter resolution.</param>
    /// <returns>The decoded field type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TType DecodeSignature<TType, TSignatureDecodeContext>(
        ISignatureTypeProvider<TType, TSignatureDecodeContext> provider,
        TSignatureDecodeContext genericContext)
    {
        return _definition.DecodeSignature(provider, genericContext);
    }

    /// <summary>
    /// Materializes the marshal descriptor bytes, if present.
    /// </summary>
    /// <returns>The marshal descriptor bytes, or <see cref="ImmutableArray{Byte}.Empty"/> if not present.</returns>
    /// <remarks>
    /// Rich tier: allocates an <see cref="ImmutableArray{Byte}"/> from the blob heap.
    /// For zero allocation access, use <see cref="GetMarshalDescriptorSpan"/> instead.
    /// </remarks>
    public ImmutableArray<byte> DecodeMarshalDescriptor()
    {
        var handle = MarshalDescriptorHandle;
        if (handle.IsNil) { return ImmutableArray<byte>.Empty; }

        return _reader.GetBlobBytes(handle).ToImmutableArray();
    }

    /// <summary>
    /// Gets the marshal descriptor bytes as a zero allocation span.
    /// </summary>
    /// <returns>The descriptor bytes as a span, or empty if not present.</returns>
    /// <remarks>
    /// The returned span is valid as long as the underlying MetadataReader is alive.
    /// Prefer this over <see cref="DecodeMarshalDescriptor"/> on hot paths.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe ReadOnlySpan<byte> GetMarshalDescriptorSpan()
    {
        var handle = MarshalDescriptorHandle;
        if (handle.IsNil) { return ReadOnlySpan<byte>.Empty; }

        var blobReader = _reader.GetBlobReader(handle);
        return new ReadOnlySpan<byte>(blobReader.StartPointer, blobReader.Length);
    }

    /// <summary>Returns true if a custom attribute of the specified type is present.</summary>
    /// <remarks>Zero allocation.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAttribute(string ns, string name)
    {
        return _definition.GetCustomAttributes().TryFindAttribute(_reader, ns, name, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(FieldContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    public override bool Equals(object? obj)
    {
        return obj is FieldContext other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    public static bool operator ==(FieldContext left, FieldContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(FieldContext left, FieldContext right)
    {
        return !left.Equals(right);
    }
}
