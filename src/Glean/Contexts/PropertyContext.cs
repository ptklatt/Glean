using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Extensions;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for PropertyDefinition.
/// </summary>
public readonly struct PropertyContext : IEquatable<PropertyContext>
{
    private readonly MetadataReader _reader;
    private readonly PropertyDefinitionHandle _handle;
    private readonly PropertyDefinition _definition;

    /// <summary>
    /// Gets the declaring type context for this property.
    /// </summary>
    public TypeContext DeclaringType
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => TypeContext.Create(_reader, _definition.GetDeclaringType());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PropertyContext Create(MetadataReader reader, PropertyDefinitionHandle handle)
    {
        if (reader is null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil) {throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new PropertyContext(reader, handle, reader.GetPropertyDefinition(handle));
    }

    /// <summary>
    /// Internal unchecked factory used by enumerators. Reader and handle are pre-validated by the enumerator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PropertyContext UnsafeCreate(MetadataReader reader, PropertyDefinitionHandle handle)
        => new PropertyContext(reader, handle, reader.GetPropertyDefinition(handle));

    private PropertyContext(MetadataReader reader, PropertyDefinitionHandle handle, PropertyDefinition definition)
    {
        _reader = reader;
        _handle = handle;
        _definition = definition;
    }

    public PropertyDefinitionHandle Handle => _handle;
    public PropertyDefinition Definition => _definition;
    public MetadataReader Reader => _reader;

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader is not null && !_handle.IsNil;
    }

    /// <summary>
    /// Gets the property name.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_definition.Name);
    }

    /// <summary>
    /// Checks whether the property name matches the specified name string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NameIs(string name)
    {
        return _reader.StringComparer.Equals(_definition.Name, name);
    }

    /// <summary>
    /// Gets the property name handle for zero-allocation comparisons.
    /// </summary>
    public StringHandle NameHandle => _definition.Name;

    public PropertyAttributes Attributes => _definition.Attributes;

    /// <summary>
    /// Gets the property signature blob handle.
    /// </summary>
    /// <remarks>
    /// Use DecodeSignature() to decode this blob into a property signature.
    /// We expose the handle (not the decoded signature) to keep the struct lightweight.
    /// </remarks>
    public BlobHandle SignatureHandle => _definition.Signature;

    /// <summary>
    /// Gets the getter method context. Returns a default context if no getter is defined.
    /// </summary>
    public MethodContext Getter
    {
        get
        {
            var accessors = _definition.GetAccessors();
            var getterHandle = accessors.Getter;

            if (getterHandle.IsNil)
                return default;

            return MethodContext.Create(_reader, getterHandle);
        }
    }

    /// <summary>
    /// Gets the setter method context. Returns a default context if no setter is defined.
    /// </summary>
    public MethodContext Setter
    {
        get
        {
            var accessors = _definition.GetAccessors();
            var setterHandle = accessors.Setter;

            if (setterHandle.IsNil)
                return default;

            return MethodContext.Create(_reader, setterHandle);
        }
    }

    /// <summary>
    /// True if this property is marked with <c>[CompilerGenerated]</c>.
    /// </summary>
    public bool IsCompilerGenerated
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => HasAttribute(WellKnownTypes.SystemRuntimeCompilerServicesNs, WellKnownTypes.CompilerGeneratedAttribute);
    }

    /// <summary>
    /// Returns true if this property has index parameters (i.e., is an indexer such as <c>this[int index]</c>).
    /// Zero-allocation: reads the signature blob shape without decoding the full signature graph.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsIndexer()
    {
        return SignatureInspector.TryGetPropertyParameterCount(_reader, _definition.Signature, out var count) && count > 0;
    }

    /// <summary>
    /// Returns the number of index parameters for this property (0 for simple properties, ≥1 for indexers).
    /// Zero-allocation: reads the signature blob shape without decoding the full signature graph.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndexParameterCount()
    {
        return SignatureInspector.TryGetPropertyParameterCount(_reader, _definition.Signature, out var count) ? count : 0;
    }

    /// <summary>
    /// Decodes the property signature using the provided signature type provider.
    /// </summary>
    /// <typeparam name="TType">The type returned by the signature provider.</typeparam>
    /// <typeparam name="TSignatureDecodeContext">The generic context type.</typeparam>
    /// <param name="provider">The signature type provider.</param>
    /// <param name="genericContext">The generic context for type parameter resolution.</param>
    /// <returns>The decoded property signature (header + parameters + type).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MethodSignature<TType> DecodeSignature<TType, TSignatureDecodeContext>(
        ISignatureTypeProvider<TType, TSignatureDecodeContext> provider,
        TSignatureDecodeContext genericContext)
    {
        return _definition.DecodeSignature(provider, genericContext);
    }

    /// <summary>Returns true if a custom attribute of the specified type is present.</summary>
    /// <remarks>Zero-allocation.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAttribute(string ns, string name)
    {
        return _definition.GetCustomAttributes().TryFindAttribute(_reader, ns, name, out _);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(PropertyContext other)
    {
        return _handle == other._handle &&
               ReferenceEquals(_reader, other._reader);
    }

    public override bool Equals(object? obj)
    {
        return obj is PropertyContext other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    public static bool operator ==(PropertyContext left, PropertyContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PropertyContext left, PropertyContext right)
    {
        return !left.Equals(right);
    }

    private string DebuggerDisplay
    {
        get
        {
            try
            {
                return string.Concat("Property: ", Name, " (Handle: 0x", _handle.GetHashCode().ToString("X8"), ")");
            }
            catch (Exception)
            {
                return string.Concat("Property: <invalid> (Handle: 0x", _handle.GetHashCode().ToString("X8"), ")");
            }
        }
    }
}
