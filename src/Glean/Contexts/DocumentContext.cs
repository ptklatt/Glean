using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for a portable PDB document.
/// </summary>
public readonly struct DocumentContext : IEquatable<DocumentContext>
{
    private readonly MetadataReader _reader;
    private readonly DocumentHandle _handle;
    private readonly Document _document;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DocumentContext Create(MetadataReader reader, DocumentHandle handle)
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        if (handle.IsNil)
        {
            throw new ArgumentException("Handle cannot be nil.", nameof(handle));
        }

        return new DocumentContext(reader, handle, reader.GetDocument(handle));
    }

    private DocumentContext(MetadataReader reader, DocumentHandle handle, Document document)
    {
        _reader = reader;
        _handle = handle;
        _document = document;
    }

    public DocumentHandle Handle => _handle;

    public MetadataReader Reader => _reader;

    public Document Document => _document;

    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_reader != null) && !_handle.IsNil;
    }

    public Guid Language
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetGuid(_document.Language);
    }

    public Guid HashAlgorithm
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetGuid(_document.HashAlgorithm);
    }

    public BlobHandle Hash => _document.Hash;

    public DocumentNameBlobHandle NameHandle => (DocumentNameBlobHandle)_document.Name;

    /// <summary>
    /// Gets the decoded source document path.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString((DocumentNameBlobHandle)_document.Name);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(DocumentContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    public override bool Equals(object? obj)
    {
        return (obj is DocumentContext other) && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    public static bool operator ==(DocumentContext left, DocumentContext right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(DocumentContext left, DocumentContext right)
    {
        return !left.Equals(right);
    }
}
