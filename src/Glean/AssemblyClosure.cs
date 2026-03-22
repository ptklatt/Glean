using System.Reflection.Metadata;

using Glean.Contexts;
using Glean.Resolution;

namespace Glean;

/// <summary>
/// Lifetime owning wrapper that loads an entry assembly and its transitive dependencies.
/// </summary>
/// <remarks>
/// Use this for cross assembly analysis. For single assembly analysis, use
/// <see cref="AssemblyScope"/>. Missing dependencies are recorded in
/// <see cref="SkippedDependencies"/>. Call <see cref="ThrowIfPartial"/> when a partial
/// closure is not acceptable. Readers and contexts are invalid after disposal.
/// </remarks>
public sealed class AssemblyClosure : IDisposable
{
    private readonly AssemblyLoader _loader;
    private readonly MetadataReader _entryReader;
    private readonly AssemblyContext _entryContext;
    private readonly AssemblySet _set;
    private bool _disposed;

    private AssemblyClosure(
        AssemblyLoader loader,
        MetadataReader entryReader,
        AssemblySet set,
        List<AssemblyDependencyLoadFailure> skipped)
    {
        _loader = loader;
        _entryReader = entryReader;
        _entryContext = AssemblyContext.Create(entryReader);
        _set = set;
        SkippedDependencies = skipped;
    }

    /// <summary>
    /// Loads an entry assembly and its transitive dependencies from the given search paths.
    /// </summary>
    /// <param name="path">Path to the entry point .dll or .exe.</param>
    /// <param name="searchPaths">Directories to probe when resolving dependencies.</param>
    /// <returns>An <see cref="AssemblyClosure"/> that owns all loaded file handles.</returns>
    /// <exception cref="System.IO.FileNotFoundException">Entry file does not exist.</exception>
    /// <exception cref="BadImageFormatException">Entry file is not a valid PE or has no metadata.</exception>
    public static AssemblyClosure Load(string path, params string[] searchPaths)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }

        var loader = new AssemblyLoader(searchPaths);
        try
        {
            var skipped = new List<AssemblyDependencyLoadFailure>();
            var (entryReader, set) = loader.LoadWithClosure(path, skipped.Add);
            return new AssemblyClosure(loader, entryReader, set, skipped);
        }
        catch
        {
            loader.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Loads an entry assembly and its transitive dependencies from the given search paths.
    /// </summary>
    /// <param name="path">Path to the entry point .dll or .exe.</param>
    /// <param name="onSkippedDependency">Optional callback invoked for skipped dependencies.</param>
    /// <param name="searchPaths">Directories to probe when resolving dependencies.</param>
    /// <returns>An <see cref="AssemblyClosure"/> that owns all loaded file handles.</returns>
    /// <exception cref="System.IO.FileNotFoundException">Entry file does not exist.</exception>
    /// <exception cref="BadImageFormatException">Entry file is not a valid PE or has no metadata.</exception>
    public static AssemblyClosure Load(
        string path,
        Action<AssemblyDependencyLoadFailure>? onSkippedDependency,
        params string[] searchPaths)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }

        var loader = new AssemblyLoader(searchPaths);
        try
        {
            var skipped = new List<AssemblyDependencyLoadFailure>();
            void capture(AssemblyDependencyLoadFailure f)
            {
                skipped.Add(f);
                onSkippedDependency?.Invoke(f);
            }
            var (entryReader, set) = loader.LoadWithClosure(path, capture);
            return new AssemblyClosure(loader, entryReader, set, skipped);
        }
        catch
        {
            loader.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the metadata reader for the entry assembly.
    /// </summary>
    public MetadataReader EntryReader
    {
        get { ObjectDisposedException.ThrowIf(_disposed, this); return _entryReader; }
    }

    /// <summary>
    /// Gets the context for the entry assembly.
    /// </summary>
    public AssemblyContext EntryContext
    {
        get { ObjectDisposedException.ThrowIf(_disposed, this); return _entryContext; }
    }

    /// <summary>
    /// Gets the assembly set used for cross assembly resolution.
    /// </summary>
    public AssemblySet Set
    {
        get { ObjectDisposedException.ThrowIf(_disposed, this); return _set; }
    }

    /// <summary>
    /// Gets loaded metadata readers keyed by full path.
    /// </summary>
    public IReadOnlyDictionary<string, MetadataReader> LoadedAssemblies
    {
        get { ObjectDisposedException.ThrowIf(_disposed, this); return _loader.LoadedAssemblies; }
    }

    /// <summary>
    /// Gets the dependencies skipped during closure loading.
    /// </summary>
    public IReadOnlyList<AssemblyDependencyLoadFailure> SkippedDependencies { get; }

    /// <summary>
    /// Throws <see cref="PartialClosureException"/> when any transitive dependency was skipped.
    /// Returns <see langword="this"/> for fluent use.
    /// </summary>
    /// <returns>This <see cref="AssemblyClosure"/>.</returns>
    /// <exception cref="PartialClosureException">One or more transitive dependencies could not be loaded.</exception>
    public AssemblyClosure ThrowIfPartial()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (SkippedDependencies.Count > 0) { throw new PartialClosureException(SkippedDependencies); }
        
        return this;
    }

    /// <summary>Releases all file handles and associated resources.</summary>
    public void Dispose()
    {
        if (_disposed) { return; }

        _set.Dispose();
        _loader.Dispose();
        _disposed = true;
    }
}
