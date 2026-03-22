using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for MethodImplementation (explicit interface implementation).
/// </summary>
public readonly struct MethodImplementationContext : IEquatable<MethodImplementationContext>
{
    private readonly MetadataReader _reader;
    private readonly MethodImplementationHandle _handle;
    private readonly MethodImplementation _implementation;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodImplementationContext Create(MetadataReader reader, MethodImplementationHandle handle)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (handle.IsNil) { throw new ArgumentException("Handle cannot be nil.", nameof(handle)); }

        return new MethodImplementationContext(reader, handle, reader.GetMethodImplementation(handle));
    }

    private MethodImplementationContext(MetadataReader reader, MethodImplementationHandle handle, MethodImplementation implementation)
    {
        _reader = reader;
        _handle = handle;
        _implementation = implementation;
    }

    public MethodImplementationHandle Handle => _handle;
    public MethodImplementation Implementation => _implementation;
    public MetadataReader Reader => _reader;

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader is not null && !_handle.IsNil;
    }

    /// <summary>
    /// Gets the type that contains this method implementation.
    /// </summary>
    public TypeDefinitionHandle Type => _implementation.Type;

    /// <summary>
    /// Gets the method body (the implementing method).
    /// </summary>
    public EntityHandle MethodBody => _implementation.MethodBody;

    /// <summary>
    /// Gets the method declaration (the interface method being implemented).
    /// </summary>
    public EntityHandle MethodDeclaration => _implementation.MethodDeclaration;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(MethodImplementationContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    public override bool Equals(object? obj)
    {
        return obj is MethodImplementationContext other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    public static bool operator ==(MethodImplementationContext left, MethodImplementationContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(MethodImplementationContext left, MethodImplementationContext right)
    {
        return !left.Equals(right);
    }
}
