using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Glean.Resolution;

/// <summary>
/// Describes the reason an assembly dependency could not be loaded during closure resolution.
/// </summary>
public enum AssemblyDependencyLoadFailureKind
{
    /// <summary>The assembly file was not found in any of the configured search paths.</summary>
    NotFound,
    /// <summary>An I/O error occurred while trying to open or read the file (e.g. file locked, access denied).</summary>
    IoError,
    /// <summary>The file exists but is not a valid PE image or has no metadata.</summary>
    BadImageFormat,
    /// <summary>The file was loaded successfully but does not contain an assembly manifest (e.g. a .netmodule).</summary>
    NotAssembly
}

/// <summary>
/// Describes a single dependency that was skipped during assembly closure resolution.
/// </summary>
public readonly struct AssemblyDependencyLoadFailure
{
    public AssemblyDependencyLoadFailure(string assemblySimpleName, AssemblyDependencyLoadFailureKind kind, string? path)
    {
        AssemblySimpleName = assemblySimpleName;
        Kind = kind;
        Path = path;
    }

    /// <summary>The simple name of the assembly that could not be loaded (e.g. <c>"System.Data"</c>).</summary>
    public string AssemblySimpleName { get; }

    /// <summary>The reason the assembly was skipped.</summary>
    public AssemblyDependencyLoadFailureKind Kind { get; }

    /// <summary>
    /// The full path that was probed, or <see langword="null"/> if no candidate file was found.
    /// </summary>
    public string? Path { get; }
}

/// <summary>
/// Loads assemblies and dependency closures and owns the opened readers.
/// </summary>
/// <remarks>
/// Prefer <see cref="AssemblyScope"/> or <see cref="AssemblyClosure"/> for typical use. Use this
/// directly for async loading, a shared loader lifetime, or incremental loading into an existing
/// <see cref="AssemblySet"/>. This type is not thread safe.
/// </remarks>
public sealed class AssemblyLoader : IDisposable
{
    private readonly List<PEReader> _ownedPEReaders = new();
    private readonly Dictionary<string, MetadataReader> _loadedReaders = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] _searchPaths;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssemblyLoader"/> class.
    /// </summary>
    /// <param name="searchPaths">Directories to probe when resolving dependencies.</param>
    public AssemblyLoader(params string[] searchPaths)
    {
        ArgumentNullException.ThrowIfNull(searchPaths);
        _searchPaths = searchPaths;
    }

    /// <summary>
    /// Gets the set of full paths for all assemblies successfully loaded by this loader.
    /// </summary>
    public IReadOnlyCollection<string> LoadedPaths => _loadedReaders.Keys;

    /// <summary>
    /// Gets all <see cref="MetadataReader"/> instances loaded by this loader, keyed by full path.
    /// </summary>
    public IReadOnlyDictionary<string, MetadataReader> LoadedAssemblies => _loadedReaders;

    /// <summary>
    /// Loads a single assembly from a file path.
    /// </summary>
    /// <param name="path">The path to a .dll or .exe file.</param>
    /// <param name="cancellationToken">Token to cancel before the file is opened.</param>
    /// <returns>A <see cref="MetadataReader"/> for the assembly. The loader owns the underlying file handle.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="BadImageFormatException">Thrown when the file is not a valid PE or has no metadata.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public MetadataReader LoadFile(string path, CancellationToken cancellationToken = default)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        if (_disposed)    { throw new ObjectDisposedException(nameof(AssemblyLoader)); }

        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) { throw new FileNotFoundException($"Assembly file not found: {path}", path); }

        if (_loadedReaders.TryGetValue(fullPath, out var existing))
        {
            return existing;
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        PEReader peReader;
        try
        {
            peReader = new PEReader(stream);
        }
        catch (Exception)
        {
            stream.Dispose();
            throw;
        }

        if (!peReader.HasMetadata)
        {
            peReader.Dispose();
            throw new BadImageFormatException($"File has no metadata: {path}");
        }

        var reader = peReader.GetMetadataReader();
        _ownedPEReaders.Add(peReader);
        _loadedReaders[fullPath] = reader;
        return reader;
    }

    /// <summary>
    /// Asynchronously loads a single assembly from a file path.
    /// </summary>
    /// <param name="path">The path to a .dll or .exe file.</param>
    /// <param name="cancellationToken">Token to cancel the async file read.</param>
    /// <returns>A <see cref="MetadataReader"/> for the assembly. The loader owns the underlying data.</returns>
    /// <remarks>
    /// This method reads the file into managed memory before parsing. File I/O is asynchronous;
    /// PE and metadata parsing are still synchronous.
    /// </remarks>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="BadImageFormatException">Thrown when the file is not a valid PE or has no metadata.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public async Task<MetadataReader> LoadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        if (_disposed)    { throw new ObjectDisposedException(nameof(AssemblyLoader)); }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) { throw new FileNotFoundException($"Assembly file not found: {path}", path); }

        if (_loadedReaders.TryGetValue(fullPath, out var existing))
        {
            return existing;
        }

        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);

        var stream = new MemoryStream(bytes, writable: false);
        PEReader peReader;
        try
        {
            peReader = new PEReader(stream);
        }
        catch (Exception)
        {
            stream.Dispose();
            throw;
        }

        if (!peReader.HasMetadata)
        {
            peReader.Dispose();
            throw new BadImageFormatException($"File has no metadata: {path}");
        }

        var reader = peReader.GetMetadataReader();
        _ownedPEReaders.Add(peReader);
        _loadedReaders[fullPath] = reader;
        return reader;
    }

    /// <summary>
    /// Loads an assembly and creates an <see cref="AssemblySet"/> with all its transitive dependencies resolved.
    /// Dependencies are discovered by probing the loader search paths and the directory that contains <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path to the entry point .dll or .exe file.</param>
    /// <param name="cancellationToken">Token to cancel the closure load between dependency loads.</param>
    /// <returns>
    /// A tuple of (entryReader, assemblySet). The <see cref="AssemblySet"/> contains all resolved
    /// transitive dependencies and is ready for cross assembly queries. The loader owns all file handles;
    /// dispose the loader when done.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when the entry point file does not exist.</exception>
    /// <exception cref="BadImageFormatException">Thrown when the entry point file is not a valid PE.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public (MetadataReader EntryReader, AssemblySet Set) LoadWithClosure(string path, CancellationToken cancellationToken = default)
    {
        return LoadWithClosure(path, onSkippedDependency: null, cancellationToken);
    }

    /// <summary>
    /// Loads an assembly and creates an <see cref="AssemblySet"/> with all its transitive dependencies resolved.
    /// </summary>
    /// <param name="path">The path to the entry point .dll or .exe file.</param>
    /// <param name="onSkippedDependency">Optional callback invoked for skipped dependencies.</param>
    /// <param name="cancellationToken">Token to cancel the closure load between dependency loads.</param>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public (MetadataReader EntryReader, AssemblySet Set) LoadWithClosure(
        string path,
        Action<AssemblyDependencyLoadFailure>? onSkippedDependency,
        CancellationToken cancellationToken = default)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        if (_disposed)    { throw new ObjectDisposedException(nameof(AssemblyLoader)); }

        var entryReader = LoadFile(path, cancellationToken);
        var (effectiveSearchPaths, set, queue) = InitializeClosureLoad(path, entryReader);

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = queue.Dequeue();
            foreach (var asmRefHandle in current.AssemblyReferences)
            {
                var asmRef = current.GetAssemblyReference(asmRefHandle);
                var asmName = current.GetString(asmRef.Name);

                var loadResult = TryLoadDependency(
                    asmName,
                    effectiveSearchPaths,
                    cancellationToken);
                if (!loadResult.IsSuccess)
                {
                    ReportSkippedDependency(asmName, loadResult, onSkippedDependency);
                    continue;
                }

                if (!TryAddLoadedDependency(set, loadResult.Reader!, asmName, loadResult.Path, onSkippedDependency))
                {
                    continue;
                }

                if (!loadResult.AlreadyLoaded)
                {
                    queue.Enqueue(loadResult.Reader!);
                }
            }
        }

        return (entryReader, set);
    }

    /// <summary>
    /// Asynchronously loads an assembly and creates an <see cref="AssemblySet"/> with all its transitive dependencies resolved.
    /// </summary>
    /// <param name="path">The path to the entry point .dll or .exe file.</param>
    /// <param name="cancellationToken">Token to cancel the async file reads.</param>
    /// <returns>
    /// A tuple of (entryReader, assemblySet). The loader owns all file data; dispose the loader when done.
    /// </returns>
    /// <remarks>
    /// File bytes are read asynchronously for each assembly. PE and metadata parsing are still synchronous.
    /// </remarks>
    /// <exception cref="FileNotFoundException">Thrown when the entry point file does not exist.</exception>
    /// <exception cref="BadImageFormatException">Thrown when the entry point file is not a valid PE.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public Task<(MetadataReader EntryReader, AssemblySet Set)> LoadWithClosureAsync(string path, CancellationToken cancellationToken = default)
    {
        return LoadWithClosureAsync(path, onSkippedDependency: null, cancellationToken);
    }

    /// <summary>
    /// Asynchronously loads an assembly and creates an <see cref="AssemblySet"/> with all its transitive dependencies resolved.
    /// </summary>
    /// <param name="path">The path to the entry point .dll or .exe file.</param>
    /// <param name="onSkippedDependency">Optional callback invoked for skipped dependencies.</param>
    /// <param name="cancellationToken">Token to cancel the async file reads.</param>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public async Task<(MetadataReader EntryReader, AssemblySet Set)> LoadWithClosureAsync(
        string path,
        Action<AssemblyDependencyLoadFailure>? onSkippedDependency,
        CancellationToken cancellationToken = default)
    {
        if (path == null) { throw new ArgumentNullException(nameof(path)); }
        if (_disposed)    { throw new ObjectDisposedException(nameof(AssemblyLoader)); }

        var entryReader = await LoadFileAsync(path, cancellationToken).ConfigureAwait(false);
        var (effectiveSearchPaths, set, queue) = InitializeClosureLoad(path, entryReader);

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = queue.Dequeue();
            foreach (var asmRefHandle in current.AssemblyReferences)
            {
                var asmRef = current.GetAssemblyReference(asmRefHandle);
                var asmName = current.GetString(asmRef.Name);

                var loadResult = await TryLoadDependencyAsync(
                    asmName,
                    effectiveSearchPaths,
                    cancellationToken).ConfigureAwait(false);
                if (!loadResult.IsSuccess)
                {
                    ReportSkippedDependency(asmName, loadResult, onSkippedDependency);
                    continue;
                }

                if (!TryAddLoadedDependency(set, loadResult.Reader!, asmName, loadResult.Path, onSkippedDependency))
                {
                    continue;
                }

                if (!loadResult.AlreadyLoaded)
                {
                    queue.Enqueue(loadResult.Reader!);
                }
            }
        }

        return (entryReader, set);
    }

    private (List<string> SearchPaths, AssemblySet Set, Queue<MetadataReader> Queue) InitializeClosureLoad(
        string entryAssemblyPath,
        MetadataReader entryReader)
    {
        var set = new AssemblySet();
        set.Add(entryReader);

        var queue = new Queue<MetadataReader>();
        queue.Enqueue(entryReader);

        return (BuildEffectiveSearchPaths(entryAssemblyPath), set, queue);
    }

    private List<string> BuildEffectiveSearchPaths(string entryAssemblyPath)
    {
        var effectiveSearchPaths = new List<string>(_searchPaths);
        var entryDir = Path.GetDirectoryName(Path.GetFullPath(entryAssemblyPath));
        if (!string.IsNullOrEmpty(entryDir))
        {
            var normalizedEntryDir = Path.GetFullPath(entryDir);
            bool found = false;
            foreach (var sp in effectiveSearchPaths)
            {
                if (string.Equals(Path.GetFullPath(sp), normalizedEntryDir, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                effectiveSearchPaths.Add(normalizedEntryDir);
            }
        }

        return effectiveSearchPaths;
    }

    private async Task<DependencyLoadResult> TryLoadDependencyAsync(
        string assemblyName,
        IReadOnlyList<string> searchPaths,
        CancellationToken cancellationToken)
    {
        DependencyLoadResult lastFailure = default;

        foreach (var candidatePath in EnumerateCandidatePaths(assemblyName, searchPaths))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var loadResult = await TryLoadCandidateAsync(candidatePath, cancellationToken).ConfigureAwait(false);
            if (loadResult.IsSuccess)
            {
                return loadResult;
            }

            if (loadResult.HasFailure)
            {
                lastFailure = loadResult;
            }
        }

        return lastFailure.HasFailure
            ? lastFailure
            : DependencyLoadResult.Failed(AssemblyDependencyLoadFailureKind.NotFound, null);
    }

    private DependencyLoadResult TryLoadDependency(
        string assemblyName,
        IReadOnlyList<string> searchPaths,
        CancellationToken cancellationToken)
    {
        DependencyLoadResult lastFailure = default;

        foreach (var candidatePath in EnumerateCandidatePaths(assemblyName, searchPaths))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var loadResult = TryLoadCandidate(candidatePath, cancellationToken);
            if (loadResult.IsSuccess)
            {
                return loadResult;
            }

            if (loadResult.HasFailure)
            {
                lastFailure = loadResult;
            }
        }

        return lastFailure.HasFailure
            ? lastFailure
            : DependencyLoadResult.Failed(AssemblyDependencyLoadFailureKind.NotFound, null);
    }

    private async Task<DependencyLoadResult> TryLoadCandidateAsync(string candidatePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(candidatePath)) { return default; }

        var fullPath = Path.GetFullPath(candidatePath);
        if (_loadedReaders.TryGetValue(fullPath, out var existing))
        {
            return DependencyLoadResult.FromAlreadyLoaded(existing, fullPath);
        }

        try
        {
            var reader = await LoadFileAsync(fullPath, cancellationToken).ConfigureAwait(false);
            return DependencyLoadResult.Loaded(reader, fullPath);
        }
        catch (IOException)
        {
            return DependencyLoadResult.Failed(AssemblyDependencyLoadFailureKind.IoError, fullPath);
        }
        catch (BadImageFormatException)
        {
            return DependencyLoadResult.Failed(AssemblyDependencyLoadFailureKind.BadImageFormat, fullPath);
        }
    }

    private DependencyLoadResult TryLoadCandidate(string candidatePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(candidatePath)) { return default; }

        var fullPath = Path.GetFullPath(candidatePath);
        if (_loadedReaders.TryGetValue(fullPath, out var existing))
        {
            return DependencyLoadResult.FromAlreadyLoaded(existing, fullPath);
        }

        try
        {
            var reader = LoadFile(fullPath, cancellationToken);
            return DependencyLoadResult.Loaded(reader, fullPath);
        }
        catch (IOException)
        {
            return DependencyLoadResult.Failed(AssemblyDependencyLoadFailureKind.IoError, fullPath);
        }
        catch (BadImageFormatException)
        {
            return DependencyLoadResult.Failed(AssemblyDependencyLoadFailureKind.BadImageFormat, fullPath);
        }
    }

    private static IEnumerable<string> EnumerateCandidatePaths(string assemblyName, IReadOnlyList<string> searchPaths)
    {
        foreach (var searchPath in searchPaths)
        {
            yield return Path.Combine(searchPath, assemblyName + ".dll");
            yield return Path.Combine(searchPath, assemblyName + ".exe");
        }
    }

    private static void ReportSkippedDependency(
        string assemblyName,
        DependencyLoadResult loadResult,
        Action<AssemblyDependencyLoadFailure>? onSkippedDependency)
    {
        if (onSkippedDependency == null) { return; }

        onSkippedDependency(new AssemblyDependencyLoadFailure(
            assemblyName,
            loadResult.FailureKind,
            loadResult.Path));
    }

    private static bool TryAddLoadedDependency(
        AssemblySet set,
        MetadataReader dependencyReader,
        string assemblyName,
        string? loadedPath,
        Action<AssemblyDependencyLoadFailure>? onSkippedDependency)
    {
        // AssemblySet.Add silently overwrites on identity collision.
        // It throws ArgumentException only for non assembly metadata such as netmodules.
        try
        {
            set.Add(dependencyReader);
            return true;
        }
        catch (ArgumentException)
        {
            onSkippedDependency?.Invoke(new AssemblyDependencyLoadFailure(
                assemblyName,
                AssemblyDependencyLoadFailureKind.NotAssembly,
                loadedPath));
            return false;
        }
    }

    /// <summary>
    /// Disposes all opened file handles and PEReaders.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) { return; }

        foreach (var peReader in _ownedPEReaders)
        {
            peReader.Dispose();
        }

        _ownedPEReaders.Clear();
        _loadedReaders.Clear();
        _disposed = true;
    }

    private readonly struct DependencyLoadResult
    {
        private DependencyLoadResult(
            MetadataReader? reader,
            string? path,
            bool alreadyLoaded,
            AssemblyDependencyLoadFailureKind failureKind,
            bool hasFailure)
        {
            Reader = reader;
            Path = path;
            AlreadyLoaded = alreadyLoaded;
            FailureKind = failureKind;
            HasFailure = hasFailure;
        }

        public MetadataReader? Reader { get; }

        public string? Path { get; }

        public bool AlreadyLoaded { get; }

        public AssemblyDependencyLoadFailureKind FailureKind { get; }

        public bool HasFailure { get; }

        public bool IsSuccess => Reader != null;

        public static DependencyLoadResult FromAlreadyLoaded(MetadataReader reader, string path)
        {
            return new DependencyLoadResult(reader, path, alreadyLoaded: true, default, hasFailure: false);
        }

        public static DependencyLoadResult Loaded(MetadataReader reader, string path)
        {
            return new DependencyLoadResult(reader, path, alreadyLoaded: false, default, hasFailure: false);
        }

        public static DependencyLoadResult Failed(AssemblyDependencyLoadFailureKind failureKind, string? path)
        {
            return new DependencyLoadResult(reader: null, path, alreadyLoaded: false, failureKind, hasFailure: true);
        }
    }
}
