using System.ComponentModel;
using System.Reflection.Metadata;

namespace Glean.Resolution;

/// <summary>
/// Resolves assemblies and modules by probing directories.
/// </summary>
/// <remarks>
/// Prefer <see cref="AssemblyClosure.Load(string, string[])"/> for the common case. Use this
/// type directly when you need custom probing logic backed by file system directories.
/// <para/>
/// When a target framework moniker is supplied, this resolver also probes common NuGet package
/// layouts such as <c>lib/{tfm}/</c>, <c>ref/{tfm}/</c>, and <c>runtimes/*/lib/{tfm}/</c>.
/// Opened scopes and resolved readers are cached for reuse. This type is not thread safe.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class DirectoryAssemblyResolver : IAssemblyReferenceResolver, IModuleResolver, IDisposable
{
    private readonly List<string> _searchPaths = new();
    private readonly HashSet<string> _searchPathSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _targetFrameworkMoniker;
    private readonly Dictionary<AssemblyIdentityKey, MetadataReader> _assemblyCache = new();
    private readonly Dictionary<MetadataReader, Dictionary<string, MetadataReader>> _moduleCacheByRequestingReader =
        new(ReferenceEqualityComparer<MetadataReader>.Instance);
    private readonly Dictionary<MetadataReader, string> _pathByReader =
        new(ReferenceEqualityComparer<MetadataReader>.Instance);
    private readonly Dictionary<string, MetadataFactory.FileScope> _scopeByPath = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectoryAssemblyResolver"/> class.
    /// </summary>
    /// <param name="searchPaths">Initial search paths to probe for assemblies.</param>
    public DirectoryAssemblyResolver(params string[] searchPaths)
        : this(targetFrameworkMoniker: null, searchPaths)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectoryAssemblyResolver"/> class with NuGet layout probing.
    /// </summary>
    /// <param name="targetFrameworkMoniker">
    /// TFM to probe under NuGet layout subdirectories, or <see langword="null"/> to disable
    /// NuGet layout probing.
    /// </param>
    /// <param name="searchPaths">Initial search paths to probe for assemblies.</param>
    public DirectoryAssemblyResolver(string? targetFrameworkMoniker, string[] searchPaths)
    {
        ArgumentNullException.ThrowIfNull(searchPaths);

        _targetFrameworkMoniker = string.IsNullOrWhiteSpace(targetFrameworkMoniker)
            ? null
            : targetFrameworkMoniker;

        foreach (var path in searchPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                AddSearchPathCore(path);
            }
        }
    }

    /// <summary>
    /// Adds a search path to probe for assemblies.
    /// </summary>
    /// <param name="directory">The directory path to add.</param>
    public void AddSearchPath(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) { throw new ArgumentException("Directory path cannot be null or empty.", nameof(directory)); }
        if (_disposed) { throw new ObjectDisposedException(nameof(DirectoryAssemblyResolver)); }

        AddSearchPathCore(directory);
    }

    /// <summary>
    /// Attempts to resolve an assembly reference to a loaded assembly.
    /// </summary>
    public bool TryResolve(AssemblyReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader)
    {
        if (requestingReader == null) { throw new ArgumentNullException(nameof(requestingReader)); }
        if (_disposed)                { throw new ObjectDisposedException(nameof(DirectoryAssemblyResolver)); }

        var assemblyReference = requestingReader.GetAssemblyReference(reference);
        var requestedIdentity = AssemblyIdentityKey.FromReference(assemblyReference, requestingReader);
        if (_assemblyCache.TryGetValue(requestedIdentity, out resolvedReader))
        {
            return true;
        }

        var assemblyName = requestingReader.GetString(assemblyReference.Name);
        foreach (var candidatePath in EnumerateAssemblyCandidatePaths(assemblyName))
        {
            if (TryLoadAssemblyCandidate(candidatePath, requestedIdentity, out resolvedReader))
            {
                return true;
            }
        }

        resolvedReader = null;
        return false;
    }

    /// <summary>
    /// Attempts to resolve a module reference by probing directories for matching module files.
    /// </summary>
    public bool TryResolve(ModuleReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader)
    {
        if (requestingReader == null) { throw new ArgumentNullException(nameof(requestingReader)); }
        if (_disposed)                { throw new ObjectDisposedException(nameof(DirectoryAssemblyResolver)); }

        var moduleReference = requestingReader.GetModuleReference(reference);
        var moduleName = requestingReader.GetString(moduleReference.Name);
        var moduleCache = GetOrCreateModuleCache(requestingReader);
        if (moduleCache.TryGetValue(moduleName, out resolvedReader))
        {
            return true;
        }

        foreach (var candidatePath in EnumerateModuleCandidatePaths(requestingReader, moduleName))
        {
            if (TryLoadModuleCandidate(candidatePath, moduleCache, moduleName, out resolvedReader))
            {
                return true;
            }
        }

        resolvedReader = null;
        return false;
    }

    /// <summary>
    /// Disposes all metadata scopes created during resolution.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) { return; }

        foreach (var scope in _scopeByPath.Values)
        {
            scope.Dispose();
        }

        _scopeByPath.Clear();
        _assemblyCache.Clear();
        _moduleCacheByRequestingReader.Clear();
        _pathByReader.Clear();
        _searchPaths.Clear();
        _searchPathSet.Clear();
        _disposed = true;
    }

    private void AddSearchPathCore(string directory)
    {
        var normalizedPath = Path.GetFullPath(directory);
        if (_searchPathSet.Add(normalizedPath))
        {
            _searchPaths.Add(normalizedPath);
        }
    }

    private IEnumerable<string> EnumerateAssemblyCandidatePaths(string assemblyName)
    {
        foreach (var searchPath in _searchPaths)
        {
            yield return Path.Combine(searchPath, assemblyName + ".dll");
            yield return Path.Combine(searchPath, assemblyName + ".exe");

            if (_targetFrameworkMoniker == null)
            {
                continue;
            }

            yield return Path.Combine(searchPath, "lib", _targetFrameworkMoniker, assemblyName + ".dll");
            yield return Path.Combine(searchPath, "ref", _targetFrameworkMoniker, assemblyName + ".dll");

            var runtimesDirectory = Path.Combine(searchPath, "runtimes");
            if (!Directory.Exists(runtimesDirectory))
            {
                continue;
            }

            foreach (var runtimeDirectory in Directory.EnumerateDirectories(runtimesDirectory))
            {
                yield return Path.Combine(runtimeDirectory, "lib", _targetFrameworkMoniker, assemblyName + ".dll");
            }
        }
    }

    private IEnumerable<string> EnumerateModuleCandidatePaths(MetadataReader requestingReader, string moduleName)
    {
        string? requestingDirectory = null;
        if (TryGetRequestingDirectory(requestingReader, out var knownDirectory))
        {
            requestingDirectory = knownDirectory;
            foreach (var candidatePath in EnumerateModuleCandidatePathsFromDirectory(knownDirectory!, moduleName))
            {
                yield return candidatePath;
            }
        }

        foreach (var searchPath in _searchPaths)
        {
            if ((requestingDirectory != null) &&
                string.Equals(searchPath, requestingDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var candidatePath in EnumerateModuleCandidatePathsFromDirectory(searchPath, moduleName))
            {
                yield return candidatePath;
            }
        }
    }

    private bool TryLoadAssemblyCandidate(string candidatePath, in AssemblyIdentityKey requestedIdentity, out MetadataReader? resolvedReader)
    {
        if (!TryGetOrLoadReader(candidatePath, out var candidateReader))
        {
            resolvedReader = null;
            return false;
        }

        if (!candidateReader.IsAssembly)
        {
            resolvedReader = null;
            return false;
        }

        var candidateIdentity = AssemblyIdentityKey.FromDefinition(candidateReader.GetAssemblyDefinition(), candidateReader);
        _assemblyCache[candidateIdentity] = candidateReader;
        if (!candidateIdentity.Equals(requestedIdentity))
        {
            resolvedReader = null;
            return false;
        }

        resolvedReader = candidateReader;
        return true;
    }

    private bool TryLoadModuleCandidate(
        string candidatePath,
        Dictionary<string, MetadataReader> moduleCache,
        string moduleName,
        out MetadataReader? resolvedReader)
    {
        if (!TryGetOrLoadReader(candidatePath, out var candidateReader))
        {
            resolvedReader = null;
            return false;
        }

        if (candidateReader.IsAssembly)
        {
            resolvedReader = null;
            return false;
        }

        moduleCache[moduleName] = candidateReader;
        resolvedReader = candidateReader;
        return true;
    }

    private bool TryGetOrLoadReader(string candidatePath, out MetadataReader reader)
    {
        if (!File.Exists(candidatePath))
        {
            reader = null!;
            return false;
        }

        var fullPath = Path.GetFullPath(candidatePath);
        if (_scopeByPath.TryGetValue(fullPath, out var cachedScope))
        {
            reader = cachedScope.Reader;
            _pathByReader[reader] = fullPath;
            return true;
        }

        try
        {
            var scope = MetadataFactory.CreateFromFile(fullPath);
            _scopeByPath[fullPath] = scope;
            reader = scope.Reader;
            _pathByReader[reader] = fullPath;
            return true;
        }
        catch (BadImageFormatException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        reader = null!;
        return false;
    }

    private Dictionary<string, MetadataReader> GetOrCreateModuleCache(MetadataReader requestingReader)
    {
        if (!_moduleCacheByRequestingReader.TryGetValue(requestingReader, out var moduleCache))
        {
            moduleCache = new Dictionary<string, MetadataReader>(StringComparer.OrdinalIgnoreCase);
            _moduleCacheByRequestingReader[requestingReader] = moduleCache;
        }

        return moduleCache;
    }

    private bool TryGetRequestingDirectory(MetadataReader requestingReader, out string? directory)
    {
        if (_pathByReader.TryGetValue(requestingReader, out var path))
        {
            directory = Path.GetDirectoryName(path);
            return !string.IsNullOrEmpty(directory);
        }

        directory = null;
        return false;
    }

    private static IEnumerable<string> EnumerateModuleCandidatePathsFromDirectory(string directory, string moduleName)
    {
        var candidatePath = Path.Combine(directory, moduleName);
        yield return candidatePath;

        if (Path.GetExtension(candidatePath).Length == 0)
        {
            yield return Path.Combine(directory, moduleName + ".netmodule");
        }
    }
}
