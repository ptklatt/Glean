using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using Glean.Contexts;

namespace Glean;

/// <summary>
/// Lifetime owning wrapper for single assembly analysis.
/// </summary>
/// <remarks>
/// Use this when you only need one assembly. For cross assembly analysis, use
/// <see cref="AssemblyClosure"/>. The preferred fast tier entry point is
/// <see cref="Context"/>. Use <see cref="Reader"/> when you want to stay on the raw
/// System.Reflection.Metadata surface. The reader, PE reader, and context are invalid after disposal.
/// </remarks>
public sealed class AssemblyScope : IDisposable
{
    private readonly MetadataFactory.FileScope _scope;
    private readonly MetadataReader _reader;
    private readonly PEReader _peReader;
    private readonly AssemblyContext _context;
    private bool _disposed;

    private AssemblyScope(MetadataFactory.FileScope scope)
    {
        _scope = scope;
        _reader = scope.Reader;
        _peReader = scope.PeReader;
        _context = AssemblyContext.Create(scope.Reader);
    }

    /// <summary>
    /// Opens a .dll or .exe file for metadata analysis.
    /// </summary>
    /// <param name="path">Path to the assembly file.</param>
    /// <returns>An <see cref="AssemblyScope"/> that owns the file handle.</returns>
    /// <exception cref="System.IO.FileNotFoundException">File does not exist.</exception>
    /// <exception cref="BadImageFormatException">File is not a valid PE or has no metadata.</exception>
    public static AssemblyScope Open(string path)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }

        var scope = MetadataFactory.CreateFromFile(path);
        return new AssemblyScope(scope);
    }

    /// <summary>
    /// Gets the raw metadata reader.
    /// </summary>
    /// <remarks>
    /// This is the System.Reflection.Metadata native escape hatch. For the primary fast tier entry point, prefer
    /// <see cref="Context"/>.
    /// </remarks>
    public MetadataReader Reader
    {
        get { ObjectDisposedException.ThrowIf(_disposed, this); return _reader; }
    }

    /// <summary>
    /// Gets the PE reader for IL access.
    /// </summary>
    public PEReader PeReader
    {
        get { ObjectDisposedException.ThrowIf(_disposed, this); return _peReader; }
    }

    /// <summary>
    /// Gets the assembly context for zero allocation traversal.
    /// </summary>
    /// <remarks>
    /// This is the primary entry point for fast tier use. Drop down to <see cref="Reader"/>
    /// whenever you need raw System.Reflection.Metadata access.
    /// </remarks>
    public AssemblyContext Context
    {
        get { ObjectDisposedException.ThrowIf(_disposed, this); return _context; }
    }

    /// <summary>Releases the memory mapped file handle and all associated resources.</summary>
    public void Dispose()
    {
        if (_disposed) { return; }

        _scope.Dispose();
        _disposed = true;
    }
}
