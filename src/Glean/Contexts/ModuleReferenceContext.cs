using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for ModuleReference.
/// </summary>
/// <remarks>
/// This context borrows its reader. Keep the reader's owner alive while accessing metadata.
/// </remarks>
public readonly struct ModuleReferenceContext : IEquatable<ModuleReferenceContext>
{
    private readonly MetadataReader _reader;
    private readonly ModuleReferenceHandle _handle;
    private readonly ModuleReference _reference;

    /// <summary>Creates a context over the specified metadata reader and row handle.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="handle"/> is nil.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ModuleReferenceContext Create(MetadataReader reader, ModuleReferenceHandle handle)
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        if (handle.IsNil)
        {
            throw new ArgumentException("Handle cannot be nil.", nameof(handle));
        }

        return new ModuleReferenceContext(reader, handle, reader.GetModuleReference(handle));
    }

    private ModuleReferenceContext(MetadataReader reader, ModuleReferenceHandle handle, ModuleReference reference)
    {
        _reader = reader;
        _handle = handle;
        _reference = reference;
    }

    /// <summary>Gets the row handle within this context's reader.</summary>
    public ModuleReferenceHandle Handle => _handle;
    /// <summary>Gets the underlying metadata reference row.</summary>
    public ModuleReference Reference => _reference;
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
    /// Gets the module reference name.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString(_reference.Name);
    }

    /// <summary>
    /// Gets the name handle for zero allocation comparisons.
    /// </summary>
    public StringHandle NameHandle => _reference.Name;

    /// <summary>Checks whether both contexts refer to the same reader and row handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ModuleReferenceContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return (obj is ModuleReferenceContext other) && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    /// <summary>Checks whether the contexts refer to the same metadata.</summary>
    public static bool operator ==(ModuleReferenceContext left, ModuleReferenceContext right)
    {
        return left.Equals(right);
    }

    /// <summary>Checks whether the contexts refer to different metadata.</summary>
    public static bool operator !=(ModuleReferenceContext left, ModuleReferenceContext right)
    {
        return !left.Equals(right);
    }
}
