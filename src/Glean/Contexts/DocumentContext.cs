using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Contexts;

/// <summary>
/// Zero allocation context for a portable PDB document.
/// </summary>
/// <remarks>
/// This context borrows its reader. Keep the reader's owner alive while accessing metadata.
/// </remarks>
public readonly struct DocumentContext : IEquatable<DocumentContext>
{
    private readonly MetadataReader _reader;
    private readonly DocumentHandle _handle;
    private readonly Document _document;

    /// <summary>Creates a context over the specified metadata reader and row handle.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="handle"/> is nil.</exception>
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

    /// <summary>Gets the row handle within this context's reader.</summary>
    public DocumentHandle Handle => _handle;

    /// <summary>Gets the borrowed reader that contains this context's metadata.</summary>
    public MetadataReader Reader => _reader;

    /// <summary>Gets the underlying portable PDB document row.</summary>
    public Document Document => _document;

    /// <summary>Checks for a reader and a row handle that is not nil.</summary>
    /// <remarks>This does not validate the metadata or check whether its owner has been disposed.</remarks>
    public bool IsValid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_reader != null) && !_handle.IsNil;
    }

    /// <summary>Gets the source language identifier from the portable PDB.</summary>
    public Guid Language
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetGuid(_document.Language);
    }

    /// <summary>Gets the identifier of the algorithm used for the document checksum.</summary>
    public Guid HashAlgorithm
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetGuid(_document.HashAlgorithm);
    }

    /// <summary>Gets the blob handle containing the document checksum.</summary>
    public BlobHandle Hash => _document.Hash;

    /// <summary>Gets the portable PDB handle for the document path.</summary>
    public DocumentNameBlobHandle NameHandle => (DocumentNameBlobHandle)_document.Name;

    /// <summary>
    /// Gets the decoded source document path.
    /// </summary>
    public string Name
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.GetString((DocumentNameBlobHandle)_document.Name);
    }

    /// <summary>Checks whether both contexts refer to the same reader and row handle.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(DocumentContext other)
    {
        return (_handle == other._handle) && ReferenceEquals(_reader, other._reader);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return (obj is DocumentContext other) && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(_reader, _handle);
    }

    /// <summary>Checks whether the contexts refer to the same metadata.</summary>
    public static bool operator ==(DocumentContext left, DocumentContext right)
    {
        return left.Equals(right);
    }

    /// <summary>Checks whether the contexts refer to different metadata.</summary>
    public static bool operator !=(DocumentContext left, DocumentContext right)
    {
        return !left.Equals(right);
    }
}
