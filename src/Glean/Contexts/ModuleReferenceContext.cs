using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for ModuleReference.
/// </summary>
public readonly struct ModuleReferenceContext : IEquatable<ModuleReferenceContext>
{
    private readonly MetadataReader _reader;
    private readonly ModuleReferenceHandle _handle;
    private readonly ModuleReference _reference;

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

    public ModuleReferenceHandle Handle => _handle;
    public ModuleReference Reference => _reference;
    public MetadataReader Reader => _reader;

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ModuleReferenceContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    public override bool Equals(object? obj)
    {
        return (obj is ModuleReferenceContext other) && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    public static bool operator ==(ModuleReferenceContext left, ModuleReferenceContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ModuleReferenceContext left, ModuleReferenceContext right)
    {
        return !left.Equals(right);
    }
}
