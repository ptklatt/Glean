using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Enumerators;
using Glean.Extensions;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for GenericParameter.
/// </summary>
public readonly struct GenericParameterContext : IEquatable<GenericParameterContext>
{
    private readonly MetadataReader _reader;
    private readonly GenericParameterHandle _handle;
    private readonly GenericParameter _genericParameter;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GenericParameterContext Create(MetadataReader reader, GenericParameterHandle handle)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil) { throw new ArgumentException("Handle cannot be nil.", nameof(handle));}

        return new GenericParameterContext(reader, handle, reader.GetGenericParameter(handle));
    }

    /// <summary>
    /// Internal unchecked factory used by enumerators. Reader and handle are pre validated by the enumerator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static GenericParameterContext UnsafeCreate(MetadataReader reader, GenericParameterHandle handle)
        => new GenericParameterContext(reader, handle, reader.GetGenericParameter(handle));

    private GenericParameterContext(MetadataReader reader, GenericParameterHandle handle, GenericParameter genericParameter)
    {
        _reader = reader;
        _handle = handle;
        _genericParameter = genericParameter;
    }

    public GenericParameterHandle Handle => _handle;
    public GenericParameter GenericParameter => _genericParameter;
    public MetadataReader Reader => _reader;

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader is not null && !_handle.IsNil;
    }

    /// <summary>
    /// Gets the generic parameter name.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_genericParameter.Name);
    }

    public GenericParameterAttributes Attributes => _genericParameter.Attributes;
    
    public int Index => _genericParameter.Index;

    /// <summary>
    /// Gets the declaring type context if this generic parameter belongs to a type definition; default otherwise.
    /// </summary>
    public TypeContext DeclaringType
    {
        get
        {
            var parent = _genericParameter.Parent;
            if (parent.Kind != HandleKind.TypeDefinition) { return default; }

            return TypeContext.Create(_reader, (TypeDefinitionHandle)parent);
        }
    }

    /// <summary>
    /// Gets the declaring method context if this generic parameter belongs to a method definition; default otherwise.
    /// </summary>
    public MethodContext DeclaringMethod
    {
        get
        {
            var parent = _genericParameter.Parent;
            if (parent.Kind != HandleKind.MethodDefinition) { return default; }

            return MethodContext.Create(_reader, (MethodDefinitionHandle)parent);
        }
    }

    /// <summary>
    /// Enumerates this generic parameter's custom attributes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CustomAttributeEnumerator EnumerateCustomAttributes()
    {
        return CustomAttributeEnumerator.Create(_reader, _genericParameter.GetCustomAttributes());
    }

    /// <summary>
    /// Enumerates only attributes whose type matches the specified namespace and name.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilteredCustomAttributeEnumerator EnumerateAttributes(string ns, string name)
    {
        return FilteredCustomAttributeEnumerator.Create(_reader, _genericParameter.GetCustomAttributes(), ns, name);
    }

    /// <summary>Returns true if a custom attribute of the specified type is present.</summary>
    /// <remarks>Zero allocation.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAttribute(string ns, string name)
    {
        return _genericParameter.GetCustomAttributes().TryFindAttribute(_reader, ns, name, out _);
    }

    /// <summary>Finds a custom attribute by namespace and name; returns the context if found.</summary>
    /// <remarks>Zero allocation.</remarks>
    public bool TryFindAttribute(string ns, string name, out CustomAttributeContext attribute)
    {
        var attributes = _genericParameter.GetCustomAttributes();
        if (!attributes.TryFindAttributeHandle(_reader, ns, name, out var handle))
        {
            attribute = default;
            return false;
        }

        attribute = CustomAttributeContext.Create(_reader, handle);
        return true;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(GenericParameterContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    public override bool Equals(object? obj)
    {
        return obj is GenericParameterContext other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    public static bool operator ==(GenericParameterContext left, GenericParameterContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(GenericParameterContext left, GenericParameterContext right)
    {
        return !left.Equals(right);
    }
}
