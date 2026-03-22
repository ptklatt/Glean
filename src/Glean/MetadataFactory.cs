using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Glean;

/// <summary>
/// Advanced factory for creating metadata readers from metadata blobs, PE files, and PDBs.
/// </summary>
/// <remarks>
/// Prefer <see cref="AssemblyScope.Open"/> or <see cref="AssemblyClosure.Load(string, string[])"/>
/// for typical analysis. Use this API when you need custom ownership or already have metadata in memory.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class MetadataFactory
{
    /// <summary>
    /// Lifetime owning wrapper for a metadata reader backed by pinned managed memory.
    /// </summary>
    public sealed class ManagedScope : IDisposable
    {
        private GCHandle _pinnedHandle;
        private bool _disposed;

        internal unsafe ManagedScope(
            byte[] data,
            MetadataReaderOptions options,
            MetadataStringDecoder? utf8Decoder)
        {
            _pinnedHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            var pointer = (byte*)_pinnedHandle.AddrOfPinnedObject();
            Reader = utf8Decoder == null
                ? new MetadataReader(pointer, data.Length, options)
                : new MetadataReader(pointer, data.Length, options, utf8Decoder);
        }

        /// <summary>
        /// Gets the metadata reader.
        /// </summary>
        public MetadataReader Reader { get; }

        /// <summary>
        /// Releases the pinned buffer.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) { return; }

            if (_pinnedHandle.IsAllocated)
            {
                _pinnedHandle.Free();
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~ManagedScope()
        {
            if (_pinnedHandle.IsAllocated)
            {
                _pinnedHandle.Free();
            }
        }
    }

    /// <summary>
    /// Creates a metadata reader from unmanaged data without copying.
    /// </summary>
    /// <param name="pointer">Pointer to the start of a metadata blob.</param>
    /// <param name="length">Length of the data in bytes.</param>
    /// <returns>A MetadataReader instance.</returns>
    /// <remarks>
    /// The pointer must remain valid and reference immutable metadata for the
    /// lifetime of the returned <see cref="MetadataReader"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe MetadataReader CreateFromPointer(byte* pointer, int length)
    {
        ValidatePointer(pointer, length);
        return new MetadataReader(pointer, length);
    }

    /// <summary>
    /// Creates a metadata reader from unmanaged data with additional options.
    /// </summary>
    /// <param name="pointer">Pointer to the start of a metadata blob.</param>
    /// <param name="length">Length of the data in bytes.</param>
    /// <param name="options">Reader options.</param>
    /// <returns>A MetadataReader instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe MetadataReader CreateFromPointer(byte* pointer, int length, MetadataReaderOptions options)
    {
        ValidatePointer(pointer, length);
        return new MetadataReader(pointer, length, options);
    }

    /// <summary>
    /// Creates a metadata reader from unmanaged data with a string decoder.
    /// </summary>
    /// <param name="pointer">Pointer to the start of a metadata blob.</param>
    /// <param name="length">Length of the data in bytes.</param>
    /// <param name="options">Reader options.</param>
    /// <param name="utf8Decoder">Optional UTF8 decoder for string heap entries.</param>
    /// <returns>A MetadataReader instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe MetadataReader CreateFromPointer(
        byte* pointer,
        int length,
        MetadataReaderOptions options,
        MetadataStringDecoder? utf8Decoder)
    {
        ValidatePointer(pointer, length);
        return new MetadataReader(pointer, length, options, utf8Decoder);
    }

    private static unsafe void ValidatePointer(byte* pointer, int length)
    {
        if (pointer == null) { throw new ArgumentNullException(nameof(pointer)); }
        if (length < 0) { throw new ArgumentOutOfRangeException(nameof(length), "Length must be non negative"); }
        if ((nuint)pointer + (nuint)length < (nuint)pointer) { throw new ArgumentOutOfRangeException(nameof(length), "Pointer + length overflows address space"); }
    }

    /// <summary>
    /// Creates a lifetime owning reader from a managed metadata blob.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ManagedScope CreateOwnedFromArray(
        byte[] data,
        MetadataReaderOptions options = MetadataReaderOptions.None,
        MetadataStringDecoder? utf8Decoder = null)
    {
        if (data == null) { throw new ArgumentNullException(nameof(data)); }

        return new ManagedScope(data, options, utf8Decoder);
    }

    /// <summary>
    /// Copies a metadata blob into owned managed memory and returns a lifetime owning reader.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ManagedScope CreateOwnedFromSpan(
        ReadOnlySpan<byte> data,
        MetadataReaderOptions options = MetadataReaderOptions.None,
        MetadataStringDecoder? utf8Decoder = null)
    {
        return new ManagedScope(data.ToArray(), options, utf8Decoder);
    }

    /// <summary>
    /// Opens a PE file with a memory mapped file and returns a scope that owns the mapping.
    /// </summary>
    /// <param name="path">Path to the .dll or .exe file.</param>
    /// <returns>A <see cref="FileScope"/> whose <see cref="FileScope.Reader"/> is ready to use.</returns>
    /// <remarks>
    /// The file is opened read only. Prefer <see cref="AssemblyScope.Open"/> unless you need
    /// direct control of the returned reader or <see cref="PEReader"/>.
    /// </remarks>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="BadImageFormatException">Thrown when the file is not a valid PE or has no metadata.</exception>
    public static FileScope CreateFromFile(string path)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) { throw new FileNotFoundException($"File not found: {fullPath}", fullPath); }

        var mmf = MemoryMappedFile.CreateFromFile(fullPath, FileMode.Open, mapName: null, capacity: 0, MemoryMappedFileAccess.Read);
        Stream? viewStream = null;
        PEReader? peReader = null;
        try
        {
            viewStream = mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
            peReader = new PEReader(viewStream);
            viewStream = null;

            if (!peReader.HasMetadata)
            {
                peReader.Dispose();
                mmf.Dispose();
                throw new BadImageFormatException($"File has no metadata: {path}");
            }

            var metadataReader = peReader.GetMetadataReader();
            return new FileScope(mmf, peReader, metadataReader);
        }
        catch (Exception)
        {
            peReader?.Dispose();
            viewStream?.Dispose();
            mmf.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens a portable PDB file and returns a scope that owns the reader.
    /// </summary>
    /// <param name="path">Path to the <c>.pdb</c> file.</param>
    /// <returns>A <see cref="PdbScope"/> whose <see cref="PdbScope.Reader"/> is ready to use.</returns>
    /// <remarks>
    /// Use this for standalone portable PDB files. For embedded PDB data, use <c>PEReader</c>.
    /// </remarks>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="BadImageFormatException">Thrown when the file is not a valid portable PDB.</exception>
    public static PdbScope CreateFromPdb(string path)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) { throw new FileNotFoundException($"PDB file not found: {fullPath}", fullPath); }

        Stream? stream = File.OpenRead(fullPath);
        MetadataReaderProvider? provider = null;
        try
        {
            provider = MetadataReaderProvider.FromPortablePdbStream(stream);
            stream = null;
            var reader = provider.GetMetadataReader();
            return new PdbScope(provider, reader);
        }
        catch (Exception)
        {
            provider?.Dispose();
            stream?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Lifetime owning scope for a portable PDB reader.
    /// </summary>
    public sealed class PdbScope : IDisposable
    {
        private readonly MetadataReaderProvider _provider;
        private bool _disposed;

        internal PdbScope(MetadataReaderProvider provider, MetadataReader reader)
        {
            _provider = provider;
            Reader = reader;
        }

        /// <summary>
        /// Gets the PDB metadata reader.
        /// </summary>
        public MetadataReader Reader { get; }

        /// <summary>
        /// Releases the PDB reader.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) { return; }

            _provider.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Lifetime owning scope for a metadata reader backed by a memory mapped PE file.
    /// </summary>
    public sealed class FileScope : IDisposable
    {
        private readonly MemoryMappedFile _mmf;
        private readonly PEReader _peReader;
        private bool _disposed;

        internal FileScope(MemoryMappedFile mmf, PEReader peReader, MetadataReader reader)
        {
            _mmf = mmf;
            _peReader = peReader;
            Reader = reader;
        }

        /// <summary>
        /// Gets the metadata reader.
        /// </summary>
        public MetadataReader Reader { get; }

        /// <summary>
        /// Gets the PE reader.
        /// </summary>
        public PEReader PeReader => _peReader;

        /// <summary>
        /// Releases the mapping and the PE reader.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) { return; }

            _peReader.Dispose();
            _mmf.Dispose();
            _disposed = true;
        }
    }
}
