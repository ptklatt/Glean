using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
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
    NotAssembly,
    /// <summary>The candidate assembly does not match the requested name, version, culture, or public key token.</summary>
    IdentityMismatch
}

/// <summary>
/// Describes a single dependency that was skipped during assembly closure resolution.
/// </summary>
public readonly struct AssemblyDependencyLoadFailure
{
    /// <summary>Creates a dependency load failure record.</summary>
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
/// Binding requires an exact identity by default. Use <see cref="AssemblyLoadOptions"/>
/// to explicitly allow version substitutions after all exact candidates have been probed.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class AssemblyLoader : IAssemblyReferenceResolver, IModuleResolver, IModuleNameResolver, IAssemblyPathProvider, IDisposable
{
    private readonly Dictionary<string, MetadataReader> _loadedReaders = new(FileSystemPathComparison.Comparer);
    private readonly Dictionary<MetadataReader, (string Path, PEReader PeReader)> _filesByReader =
        new(ReferenceEqualityComparer<MetadataReader>.Instance);
    private readonly Dictionary<MetadataReader, Dictionary<string, MetadataReader>> _moduleCacheByRequestingReader =
        new(ReferenceEqualityComparer<MetadataReader>.Instance);
    private readonly List<string> _searchPaths = new();
    private readonly HashSet<string> _searchPathSet = new(FileSystemPathComparison.Comparer);
    private readonly string? _targetFrameworkMoniker;
    private readonly AssemblyBindingPolicy _bindingPolicy = AssemblyBindingPolicy.Exact;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssemblyLoader"/> class.
    /// </summary>
    /// <param name="searchPaths">Directories to probe when resolving dependencies.</param>
    public AssemblyLoader(params string[] searchPaths)
        : this(targetFrameworkMoniker: null, searchPaths)
    {
    }

    /// <summary>
    /// Initializes a loader that also probes NuGet layouts for the specified target framework.
    /// </summary>
    /// <param name="targetFrameworkMoniker">The target framework, or null to disable NuGet layout probing.</param>
    /// <param name="searchPaths">Directories to probe when resolving dependencies.</param>
    public AssemblyLoader(string? targetFrameworkMoniker, string[] searchPaths)
    {
        ArgumentNullException.ThrowIfNull(searchPaths);
        _targetFrameworkMoniker = string.IsNullOrWhiteSpace(targetFrameworkMoniker) ? null : targetFrameworkMoniker;
        foreach (var path in searchPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                AddSearchPathCore(path);
            }
        }
    }

    /// <summary>Initializes a loader with explicit probing and binding options.</summary>
    /// <param name="options">Options copied by this loader.</param>
    public AssemblyLoader(AssemblyLoadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.SearchPaths);
        if (!Enum.IsDefined(options.BindingPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Unknown assembly binding policy.");
        }

        _bindingPolicy = options.BindingPolicy;
        _targetFrameworkMoniker = string.IsNullOrWhiteSpace(options.TargetFrameworkMoniker)
            ? null : options.TargetFrameworkMoniker;
        foreach (var path in options.SearchPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Search paths cannot contain null or empty directories.", nameof(options));
            }

            AddSearchPathCore(path);
        }
    }

    /// <summary>Gets the assembly identity matching policy used by this loader.</summary>
    public AssemblyBindingPolicy BindingPolicy => _bindingPolicy;

    /// <summary>
    /// Adds a directory to probe when resolving dependencies.
    /// </summary>
    /// <param name="directory">The directory path to add.</param>
    public void AddSearchPath(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) { throw new ArgumentException("Directory path cannot be null or empty.", nameof(directory)); }
        if (_disposed) { throw new ObjectDisposedException(nameof(AssemblyLoader)); }

        AddSearchPathCore(directory);
    }

    /// <summary>
    /// Gets the set of full paths for all assemblies successfully loaded by this loader.
    /// </summary>
    public IReadOnlyCollection<string> LoadedPaths => _loadedReaders.Keys;

    /// <summary>
    /// Gets all <see cref="MetadataReader"/> instances loaded by this loader, keyed by full path.
    /// </summary>
    public IReadOnlyDictionary<string, MetadataReader> LoadedAssemblies => _loadedReaders;

    /// <summary>Gets the borrowed PE reader for metadata loaded by this instance.</summary>
    /// <returns>False if <paramref name="reader"/> was not loaded by this instance.</returns>
    /// <remarks>Do not dispose the returned reader. It and spans from it require this loader to remain alive.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">This loader has been disposed.</exception>
    public bool TryGetPeReader(MetadataReader reader, [NotNullWhen(true)] out PEReader? peReader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_filesByReader.TryGetValue(reader, out var file))
        {
            peReader = file.PeReader;
            return true;
        }

        peReader = null;
        return false;
    }

    internal PEReader GetOwnedPeReader(MetadataReader reader) => _filesByReader[reader].PeReader;

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
        return LoadStream(fullPath, stream);
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

        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) { throw new FileNotFoundException($"Assembly file not found: {path}", path); }

        if (_loadedReaders.TryGetValue(fullPath, out var existing))
        {
            return existing;
        }

        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
        var stream = new MemoryStream(bytes, writable: false);
        return LoadStream(fullPath, stream);
    }

    private MetadataReader LoadStream(string fullPath, Stream stream)
    {
        PEReader? peReader = null;
        try
        {
            peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                throw new BadImageFormatException($"File has no metadata: {fullPath}");
            }

            var reader = peReader.GetMetadataReader();
            _filesByReader.Add(reader, (fullPath, peReader));
            _loadedReaders[fullPath] = reader;
            return reader;
        }
        catch
        {
            peReader?.Dispose();
            stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Attempts to resolve an assembly reference using the configured binding policy.
    /// </summary>
    public bool TryResolve(AssemblyReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader)
    {
        if (requestingReader == null) { throw new ArgumentNullException(nameof(requestingReader)); }
        if (_disposed) { throw new ObjectDisposedException(nameof(AssemblyLoader)); }

        var identity = AssemblyIdentityKey.FromReference(requestingReader.GetAssemblyReference(reference), requestingReader);
        var result = TryLoadDependency(identity, _searchPaths, CancellationToken.None);
        resolvedReader = result.Reader;
        return result.IsSuccess;
    }

    string? IAssemblyPathProvider.GetAssemblyPath(MetadataReader reader) => GetAssemblyPath(reader);

    internal string? GetAssemblyPath(MetadataReader reader)
    {
        return _filesByReader.TryGetValue(reader, out var file) ? file.Path : null;
    }

    /// <summary>
    /// Attempts to resolve a module, preferring the requesting reader's directory.
    /// </summary>
    public bool TryResolve(ModuleReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader)
    {
        if (requestingReader == null) { throw new ArgumentNullException(nameof(requestingReader)); }
        if (_disposed) { throw new ObjectDisposedException(nameof(AssemblyLoader)); }

        var moduleName = requestingReader.GetString(requestingReader.GetModuleReference(reference).Name);
        return TryResolveModuleByName(moduleName, requestingReader, out resolvedReader);
    }

    bool IModuleNameResolver.TryResolve(string moduleName, MetadataReader requestingReader, out MetadataReader? resolvedReader)
    {
        if (requestingReader == null) { throw new ArgumentNullException(nameof(requestingReader)); }
        if (_disposed) { throw new ObjectDisposedException(nameof(AssemblyLoader)); }

        return TryResolveModuleByName(moduleName, requestingReader, out resolvedReader);
    }

    private bool TryResolveModuleByName(string moduleName, MetadataReader requestingReader, out MetadataReader? resolvedReader)
    {
        if (!_moduleCacheByRequestingReader.TryGetValue(requestingReader, out var cache))
        {
            cache = new Dictionary<string, MetadataReader>(FileSystemPathComparison.Comparer);
            _moduleCacheByRequestingReader[requestingReader] = cache;
        }

        if (cache.TryGetValue(moduleName, out resolvedReader))
        {
            return true;
        }

        if (TryLoadModule(requestingReader, moduleName, _searchPaths, out resolvedReader))
        {
            cache[moduleName] = resolvedReader!;
            return true;
        }

        return false;
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
        var (effectiveSearchPaths, set, queue, visited) = InitializeClosureLoad(path, entryReader);

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = queue.Dequeue();
            foreach (var asmRefHandle in current.AssemblyReferences)
            {
                var asmRef = current.GetAssemblyReference(asmRefHandle);
                var requestedIdentity = AssemblyIdentityKey.FromReference(asmRef, current);
                var asmName = requestedIdentity.Name;

                var loadResult = TryLoadDependency(
                    requestedIdentity,
                    effectiveSearchPaths,
                    cancellationToken);
                if (!loadResult.IsSuccess)
                {
                    ReportSkippedDependency(asmName, loadResult, onSkippedDependency);
                    continue;
                }

                set.RecordBindingSubstitution(requestedIdentity, loadResult.Reader!, loadResult.Path);

                if (visited.Add(loadResult.Reader!))
                {
                    set.Add(loadResult.Reader!);
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
        var (effectiveSearchPaths, set, queue, visited) = InitializeClosureLoad(path, entryReader);

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = queue.Dequeue();
            foreach (var asmRefHandle in current.AssemblyReferences)
            {
                var asmRef = current.GetAssemblyReference(asmRefHandle);
                var requestedIdentity = AssemblyIdentityKey.FromReference(asmRef, current);
                var asmName = requestedIdentity.Name;

                var loadResult = await TryLoadDependencyAsync(
                    requestedIdentity,
                    effectiveSearchPaths,
                    cancellationToken).ConfigureAwait(false);
                if (!loadResult.IsSuccess)
                {
                    ReportSkippedDependency(asmName, loadResult, onSkippedDependency);
                    continue;
                }

                set.RecordBindingSubstitution(requestedIdentity, loadResult.Reader!, loadResult.Path);

                if (visited.Add(loadResult.Reader!))
                {
                    set.Add(loadResult.Reader!);
                    queue.Enqueue(loadResult.Reader!);
                }
            }
        }

        return (entryReader, set);
    }

    private (List<string> SearchPaths, AssemblySet Set, Queue<MetadataReader> Queue, HashSet<MetadataReader> Visited) InitializeClosureLoad(
        string entryAssemblyPath,
        MetadataReader entryReader)
    {
        var searchPaths = BuildEffectiveSearchPaths(entryAssemblyPath);
        var resolver = new ClosureAssemblyResolver(this, searchPaths);
        var set = new AssemblySet(_bindingPolicy, resolver, resolver);
        set.Add(entryReader);

        var queue = new Queue<MetadataReader>();
        queue.Enqueue(entryReader);
        var visited = new HashSet<MetadataReader>(ReferenceEqualityComparer<MetadataReader>.Instance) { entryReader };

        return (searchPaths, set, queue, visited);
    }

    private List<string> BuildEffectiveSearchPaths(string entryAssemblyPath)
    {
        var effectiveSearchPaths = new List<string>(_searchPaths);
        var entryDir = Path.GetDirectoryName(Path.GetFullPath(entryAssemblyPath));
        if (!string.IsNullOrEmpty(entryDir))
        {
            var normalizedEntryDir = FileSystemPathComparison.NormalizeDirectory(entryDir);
            bool found = false;
            foreach (var sp in effectiveSearchPaths)
            {
                if (string.Equals(sp, normalizedEntryDir, FileSystemPathComparison.Comparison))
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
        AssemblyIdentityKey requestedIdentity,
        IReadOnlyList<string> searchPaths,
        CancellationToken cancellationToken)
    {
        DependencyLoadResult lastFailure = default;
        DependencyLoadResult compatibleCandidate = default;
        int compatibleSearchPathIndex = int.MaxValue;

        int probePasses = OperatingSystem.IsWindows() ? 1 : 2;
        for (int pass = 0; pass < probePasses; pass++)
        {
            var paths = pass == 0
                ? EnumerateCandidatePaths(requestedIdentity.Name, searchPaths)
                : EnumerateCaseVariantCandidatePaths(requestedIdentity.Name, searchPaths, cancellationToken);
            foreach (var (candidatePath, searchPathIndex) in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var candidate = await TryLoadCandidateAsync(candidatePath, cancellationToken).ConfigureAwait(false);
                var loadResult = ValidateDependencyCandidate(candidate, requestedIdentity);
                if (loadResult.IsSuccess)
                {
                    return loadResult;
                }

                if (loadResult.FailureKind == AssemblyDependencyLoadFailureKind.IdentityMismatch)
                {
                    CaptureCompatibleCandidate(candidate, requestedIdentity, searchPathIndex,
                        ref compatibleCandidate, ref compatibleSearchPathIndex);
                }

                if (loadResult.HasFailure)
                {
                    lastFailure = loadResult;
                }
            }
        }

        if (compatibleCandidate.IsSuccess)
        {
            return compatibleCandidate;
        }

        return lastFailure.HasFailure
            ? lastFailure
            : DependencyLoadResult.Failed(AssemblyDependencyLoadFailureKind.NotFound, null);
    }

    private DependencyLoadResult TryLoadDependency(
        AssemblyIdentityKey requestedIdentity,
        IReadOnlyList<string> searchPaths,
        CancellationToken cancellationToken)
    {
        DependencyLoadResult lastFailure = default;
        DependencyLoadResult compatibleCandidate = default;
        int compatibleSearchPathIndex = int.MaxValue;

        int probePasses = OperatingSystem.IsWindows() ? 1 : 2;
        for (int pass = 0; pass < probePasses; pass++)
        {
            var paths = pass == 0
                ? EnumerateCandidatePaths(requestedIdentity.Name, searchPaths)
                : EnumerateCaseVariantCandidatePaths(requestedIdentity.Name, searchPaths, cancellationToken);
            foreach (var (candidatePath, searchPathIndex) in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var candidate = TryLoadCandidate(candidatePath, cancellationToken);
                var loadResult = ValidateDependencyCandidate(candidate, requestedIdentity);
                if (loadResult.IsSuccess)
                {
                    return loadResult;
                }

                if (loadResult.FailureKind == AssemblyDependencyLoadFailureKind.IdentityMismatch)
                {
                    CaptureCompatibleCandidate(candidate, requestedIdentity, searchPathIndex,
                        ref compatibleCandidate, ref compatibleSearchPathIndex);
                }

                if (loadResult.HasFailure)
                {
                    lastFailure = loadResult;
                }
            }
        }

        if (compatibleCandidate.IsSuccess)
        {
            return compatibleCandidate;
        }

        return lastFailure.HasFailure
            ? lastFailure
            : DependencyLoadResult.Failed(AssemblyDependencyLoadFailureKind.NotFound, null);
    }

    private void CaptureCompatibleCandidate(
        DependencyLoadResult candidate,
        AssemblyIdentityKey requestedIdentity,
        int searchPathIndex,
        ref DependencyLoadResult compatibleCandidate,
        ref int compatibleSearchPathIndex)
    {
        if ((_bindingPolicy == AssemblyBindingPolicy.ExactThenCompatible) &&
            (searchPathIndex < compatibleSearchPathIndex) && (candidate.IsSuccess) && (candidate.Reader!.IsAssembly) &&
            requestedIdentity.MatchesExceptVersion(AssemblyIdentityKey.FromDefinition(candidate.Reader.GetAssemblyDefinition(), candidate.Reader)))
        {
            compatibleCandidate = candidate;
            compatibleSearchPathIndex = searchPathIndex;
        }
    }

    private static DependencyLoadResult ValidateDependencyCandidate(
        DependencyLoadResult candidate,
        AssemblyIdentityKey requestedIdentity)
    {
        if (!candidate.IsSuccess)
        {
            return candidate;
        }

        if (!candidate.Reader!.IsAssembly)
        {
            return DependencyLoadResult.Failed(AssemblyDependencyLoadFailureKind.NotAssembly, candidate.Path);
        }

        try
        {
            if (!requestedIdentity.MatchesDefinition(candidate.Reader))
            {
                return DependencyLoadResult.Failed(AssemblyDependencyLoadFailureKind.IdentityMismatch, candidate.Path);
            }
        }
        catch (BadImageFormatException)
        {
            return DependencyLoadResult.Failed(AssemblyDependencyLoadFailureKind.BadImageFormat, candidate.Path);
        }

        return candidate;
    }

    private async Task<DependencyLoadResult> TryLoadCandidateAsync(string candidatePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(candidatePath)) { return default; }

        var fullPath = Path.GetFullPath(candidatePath);
        if (_loadedReaders.TryGetValue(fullPath, out var existing))
        {
            return DependencyLoadResult.Loaded(existing, fullPath);
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
        catch (UnauthorizedAccessException)
        {
            return DependencyLoadResult.Failed(AssemblyDependencyLoadFailureKind.IoError, fullPath);
        }
    }

    private DependencyLoadResult TryLoadCandidate(string candidatePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(candidatePath)) { return default; }

        var fullPath = Path.GetFullPath(candidatePath);
        if (_loadedReaders.TryGetValue(fullPath, out var existing))
        {
            return DependencyLoadResult.Loaded(existing, fullPath);
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
        catch (UnauthorizedAccessException)
        {
            return DependencyLoadResult.Failed(AssemblyDependencyLoadFailureKind.IoError, fullPath);
        }
    }

    private IEnumerable<(string Path, int SearchPathIndex)> EnumerateCandidatePaths(
        string assemblyName, IReadOnlyList<string> searchPaths)
    {
        for (int searchPathIndex = 0; searchPathIndex < searchPaths.Count; searchPathIndex++)
        {
            var searchPath = searchPaths[searchPathIndex];
            yield return (Path.Combine(searchPath, assemblyName + ".dll"), searchPathIndex);
            yield return (Path.Combine(searchPath, assemblyName + ".exe"), searchPathIndex);

            if (_targetFrameworkMoniker == null)
            {
                continue;
            }

            yield return (Path.Combine(searchPath, "lib", _targetFrameworkMoniker, assemblyName + ".dll"), searchPathIndex);
            yield return (Path.Combine(searchPath, "ref", _targetFrameworkMoniker, assemblyName + ".dll"), searchPathIndex);

            var runtimesDirectory = Path.Combine(searchPath, "runtimes");
            if (Directory.Exists(runtimesDirectory))
            {
                foreach (var runtimeDirectory in Directory.EnumerateDirectories(runtimesDirectory))
                {
                    yield return (Path.Combine(runtimeDirectory, "lib", _targetFrameworkMoniker, assemblyName + ".dll"), searchPathIndex);
                }
            }
        }
    }

    private IEnumerable<(string Path, int SearchPathIndex)> EnumerateCaseVariantCandidatePaths(
        string assemblyName,
        IReadOnlyList<string> searchPaths,
        CancellationToken cancellationToken)
    {
        // Only scan directories after exact filename probing has found no exact assembly identity.
        foreach (var (exactPath, searchPathIndex) in EnumerateCandidatePaths(assemblyName, searchPaths))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = Path.GetDirectoryName(exactPath)!;
            if (!Directory.Exists(directory))
            {
                continue;
            }

            var name = Path.GetFileName(exactPath);
            var matches = new List<string>();
            try
            {
                foreach (var path in Directory.EnumerateFiles(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!string.Equals(path, exactPath, FileSystemPathComparison.Comparison) &&
                        string.Equals(Path.GetFileName(path), name, StringComparison.OrdinalIgnoreCase))
                    {
                        matches.Add(path);
                    }
                }
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            matches.Sort(StringComparer.Ordinal);
            foreach (var path in matches)
            {
                yield return (path, searchPathIndex);
            }
        }
    }

    private void AddSearchPathCore(string directory)
    {
        var fullPath = FileSystemPathComparison.NormalizeDirectory(directory);
        if (_searchPathSet.Add(fullPath))
        {
            _searchPaths.Add(fullPath);
        }
    }

    private bool TryLoadModule(
        MetadataReader requestingReader,
        string moduleName,
        IReadOnlyList<string> searchPaths,
        out MetadataReader? resolvedReader)
    {
        foreach (var path in EnumerateModuleCandidatePaths(requestingReader, moduleName, searchPaths))
        {
            var candidate = TryLoadCandidate(path, CancellationToken.None);
            if (candidate.IsSuccess && !candidate.Reader!.IsAssembly)
            {
                resolvedReader = candidate.Reader;
                return true;
            }
        }

        resolvedReader = null;
        return false;
    }

    private IEnumerable<string> EnumerateModuleCandidatePaths(
        MetadataReader requestingReader,
        string moduleName,
        IReadOnlyList<string> searchPaths)
    {
        string? requestingDirectory = null;
        if (_filesByReader.TryGetValue(requestingReader, out var file))
        {
            requestingDirectory = Path.GetDirectoryName(file.Path);
            if (!string.IsNullOrEmpty(requestingDirectory))
            {
                requestingDirectory = FileSystemPathComparison.NormalizeDirectory(requestingDirectory);
                foreach (var path in EnumerateModuleCandidatePathsFromDirectory(requestingDirectory, moduleName))
                {
                    yield return path;
                }
            }
        }

        foreach (var directory in searchPaths)
        {
            if (string.Equals(directory, requestingDirectory, FileSystemPathComparison.Comparison))
            {
                continue;
            }

            foreach (var path in EnumerateModuleCandidatePathsFromDirectory(directory, moduleName))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> EnumerateModuleCandidatePathsFromDirectory(string directory, string moduleName)
    {
        yield return Path.Combine(directory, moduleName);
        if (Path.GetExtension(moduleName).Length == 0)
        {
            yield return Path.Combine(directory, moduleName + ".netmodule");
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

    /// <summary>
    /// Disposes all opened file handles and PEReaders.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) { return; }

        foreach (var file in _filesByReader.Values)
        {
            file.PeReader.Dispose();
        }

        _filesByReader.Clear();
        _loadedReaders.Clear();
        _moduleCacheByRequestingReader.Clear();
        _searchPaths.Clear();
        _searchPathSet.Clear();
        _disposed = true;
    }

    private sealed class ClosureAssemblyResolver : IAssemblyReferenceResolver, IModuleResolver, IModuleNameResolver, IAssemblyPathProvider
    {
        private readonly AssemblyLoader _loader;
        private readonly IReadOnlyList<string> _searchPaths;

        public ClosureAssemblyResolver(AssemblyLoader loader, IReadOnlyList<string> searchPaths)
        {
            _loader = loader;
            _searchPaths = searchPaths;
        }

        public bool TryResolve(AssemblyReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader)
        {
            ObjectDisposedException.ThrowIf(_loader._disposed, _loader);
            var identity = AssemblyIdentityKey.FromReference(requestingReader.GetAssemblyReference(reference), requestingReader);
            var result = _loader.TryLoadDependency(identity, _searchPaths, CancellationToken.None);
            resolvedReader = result.Reader;
            return result.IsSuccess;
        }

        public bool TryResolve(ModuleReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader)
        {
            if (requestingReader == null) { throw new ArgumentNullException(nameof(requestingReader)); }
            ObjectDisposedException.ThrowIf(_loader._disposed, _loader);

            var moduleName = requestingReader.GetString(requestingReader.GetModuleReference(reference).Name);
            return TryResolveModuleByName(moduleName, requestingReader, out resolvedReader);
        }

        private bool TryResolveModuleByName(string moduleName, MetadataReader requestingReader, out MetadataReader? resolvedReader)
        {
            return _loader.TryLoadModule(requestingReader, moduleName, _searchPaths, out resolvedReader);
        }

        bool IModuleNameResolver.TryResolve(string moduleName, MetadataReader requestingReader, out MetadataReader? resolvedReader)
        {
            if (requestingReader == null) { throw new ArgumentNullException(nameof(requestingReader)); }
            ObjectDisposedException.ThrowIf(_loader._disposed, _loader);

            return TryResolveModuleByName(moduleName, requestingReader, out resolvedReader);
        }

        public string? GetAssemblyPath(MetadataReader reader) => _loader.GetAssemblyPath(reader);
    }

    private readonly struct DependencyLoadResult
    {
        private DependencyLoadResult(
            MetadataReader? reader,
            string? path,
            AssemblyDependencyLoadFailureKind failureKind,
            bool hasFailure)
        {
            Reader = reader;
            Path = path;
            FailureKind = failureKind;
            HasFailure = hasFailure;
        }

        public MetadataReader? Reader { get; }

        public string? Path { get; }

        public AssemblyDependencyLoadFailureKind FailureKind { get; }

        public bool HasFailure { get; }

        public bool IsSuccess => Reader != null;

        public static DependencyLoadResult Loaded(MetadataReader reader, string path)
        {
            return new DependencyLoadResult(reader, path, default, hasFailure: false);
        }

        public static DependencyLoadResult Failed(AssemblyDependencyLoadFailureKind failureKind, string? path)
        {
            return new DependencyLoadResult(reader: null, path, failureKind, hasFailure: true);
        }
    }
}
