using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for MethodImplementation (explicit interface implementation).
/// </summary>
/// <remarks>
/// This context borrows its reader. Keep the reader's owner alive while accessing metadata.
/// </remarks>
public readonly struct MethodImplementationContext : IEquatable<MethodImplementationContext>
{
    private readonly MetadataReader _reader;
    private readonly MethodImplementationHandle _handle;
    private readonly MethodImplementation _implementation;

    /// <summary>Creates a context over the specified metadata reader and row handle.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="handle"/> is nil.</exception>
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

    /// <summary>Gets the row handle within this context's reader.</summary>
    public MethodImplementationHandle Handle => _handle;
    /// <summary>Gets the underlying method implementation row.</summary>
    public MethodImplementation Implementation => _implementation;
    /// <summary>Gets the borrowed reader that contains this context's metadata.</summary>
    public MetadataReader Reader => _reader;

    /// <summary>Checks for a reader and a row handle that is not nil.</summary>
    /// <remarks>This does not validate the metadata or check whether its owner has been disposed.</remarks>
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

    /// <summary>Checks whether both contexts refer to the same reader and row handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(MethodImplementationContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is MethodImplementationContext other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    /// <summary>Checks whether the contexts refer to the same metadata.</summary>
    public static bool operator ==(MethodImplementationContext left, MethodImplementationContext right)
    {
        return left.Equals(right);
    }

    /// <summary>Checks whether the contexts refer to different metadata.</summary>
    public static bool operator !=(MethodImplementationContext left, MethodImplementationContext right)
    {
        return !left.Equals(right);
    }
}
