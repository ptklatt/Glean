using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Enumerators;
using Glean.Extensions;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for Parameter.
/// </summary>
public readonly struct ParameterContext : IEquatable<ParameterContext>
{
    private readonly MetadataReader _reader;
    private readonly ParameterHandle _handle;
    private readonly Parameter _parameter;
    private readonly MethodDefinitionHandle _declaringMethod;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ParameterContext Create(MetadataReader reader, ParameterHandle handle)
    {
        if (reader is null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil) { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new ParameterContext(reader, handle, reader.GetParameter(handle), default);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ParameterContext Create(MetadataReader reader, ParameterHandle handle, MethodDefinitionHandle declaringMethod)
    {
        if (reader is null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil) { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new ParameterContext(reader, handle, reader.GetParameter(handle), declaringMethod);
    }

    /// <summary>
    /// Internal unchecked factory used by enumerators. Reader and handle are pre validated by the enumerator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ParameterContext UnsafeCreate(MetadataReader reader, ParameterHandle handle, MethodDefinitionHandle declaringMethod)
        => new ParameterContext(reader, handle, reader.GetParameter(handle), declaringMethod);

    private ParameterContext(MetadataReader reader, ParameterHandle handle, Parameter parameter, MethodDefinitionHandle declaringMethod)
    {
        _reader = reader;
        _handle = handle;
        _parameter = parameter;
        _declaringMethod = declaringMethod;
    }

    public ParameterHandle Handle => _handle;
    public Parameter Parameter => _parameter;
    public MetadataReader Reader => _reader;

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader is not null && !_handle.IsNil;
    }

    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name
    {
        get
        {
            var nameHandle = _parameter.Name;
            return nameHandle.IsNil ? string.Empty : _reader.GetString(nameHandle);
        }
    }

    /// <summary>
    /// Gets the parameter name handle for zero allocation comparisons.
    /// </summary>
    public StringHandle NameHandle => _parameter.Name;

    public ParameterAttributes Attributes => _parameter.Attributes;

    public int SequenceNumber => _parameter.SequenceNumber;

    /// <summary>True if the parameter is marked [In].</summary>
    public bool IsIn
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _parameter.IsIn();
    }

    /// <summary>True if the parameter is marked [Out].</summary>
    public bool IsOut
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _parameter.IsOut();
    }

    /// <summary>True if the parameter is optional.</summary>
    public bool IsOptional
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _parameter.IsOptional();
    }

    /// <summary>True if the parameter has a default value.</summary>
    public bool HasDefault
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _parameter.HasDefault();
    }

    /// <summary>
    /// Gets the declaring method context.
    /// Returns an invalid (default) context if this parameter was constructed without a declaring method handle.
    /// </summary>
    public MethodContext DeclaringMethod
    {
        get
        {
            if (_declaringMethod.IsNil) { return default; }

            return MethodContext.Create(_reader, _declaringMethod);
        }
    }

    /// <summary>
    /// Enumerates only attributes whose type matches the specified namespace and name.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FilteredCustomAttributeEnumerator EnumerateAttributes(string ns, string name)
    {
        return FilteredCustomAttributeEnumerator.Create(_reader, _parameter.GetCustomAttributes(), ns, name);
    }

    /// <summary>Returns true if a custom attribute of the specified type is present.</summary>
    /// <remarks>Zero allocation.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAttribute(string ns, string name)
    {
        return _parameter.GetCustomAttributes().TryFindAttribute(_reader, ns, name, out _);
    }

    /// <summary>Finds a custom attribute by namespace and name; returns the context if found.</summary>
    /// <remarks>Zero allocation.</remarks>
    public bool TryFindAttribute(string ns, string name, out CustomAttributeContext attribute)
    {
        var attributes = _parameter.GetCustomAttributes();
        if (!attributes.TryFindAttributeHandle(_reader, ns, name, out var handle))
        {
            attribute = default;
            return false;
        }

        attribute = CustomAttributeContext.Create(_reader, handle);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ParameterContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    public override bool Equals(object? obj)
    {
        return obj is ParameterContext other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    public static bool operator ==(ParameterContext left, ParameterContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ParameterContext left, ParameterContext right)
    {
        return !left.Equals(right);
    }

}
