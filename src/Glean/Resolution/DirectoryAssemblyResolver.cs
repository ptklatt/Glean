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
/// Opened readers are cached and remain valid until this resolver is disposed.
/// This type is not thread safe.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class DirectoryAssemblyResolver : IAssemblyReferenceResolver, IModuleResolver, IModuleNameResolver, IAssemblyPathProvider, IDisposable
{
    private readonly AssemblyLoader _loader;
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
        _loader = new AssemblyLoader(targetFrameworkMoniker, searchPaths);
    }

    /// <summary>Initializes a resolver with explicit probing and binding options.</summary>
    /// <param name="options">Options copied by this resolver.</param>
    public DirectoryAssemblyResolver(AssemblyLoadOptions options)
    {
        _loader = new AssemblyLoader(options);
    }

    /// <summary>Gets the assembly identity matching policy used by this resolver.</summary>
    public AssemblyBindingPolicy BindingPolicy => _loader.BindingPolicy;

    string? IAssemblyPathProvider.GetAssemblyPath(MetadataReader reader) => _loader.GetAssemblyPath(reader);

    /// <summary>
    /// Adds a search path to probe for assemblies.
    /// </summary>
    /// <param name="directory">The directory path to add.</param>
    public void AddSearchPath(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) { throw new ArgumentException("Directory path cannot be null or empty.", nameof(directory)); }
        if (_disposed) { throw new ObjectDisposedException(nameof(DirectoryAssemblyResolver)); }

        _loader.AddSearchPath(directory);
    }

    /// <summary>
    /// Attempts to resolve an assembly reference to a loaded assembly.
    /// </summary>
    public bool TryResolve(AssemblyReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader)
    {
        if (requestingReader == null) { throw new ArgumentNullException(nameof(requestingReader)); }
        if (_disposed) { throw new ObjectDisposedException(nameof(DirectoryAssemblyResolver)); }

        return _loader.TryResolve(reference, requestingReader, out resolvedReader);
    }

    /// <summary>
    /// Attempts to resolve a module reference by probing directories for matching module files.
    /// </summary>
    public bool TryResolve(ModuleReferenceHandle reference, MetadataReader requestingReader, out MetadataReader? resolvedReader)
    {
        if (requestingReader == null) { throw new ArgumentNullException(nameof(requestingReader)); }
        if (_disposed) { throw new ObjectDisposedException(nameof(DirectoryAssemblyResolver)); }

        return _loader.TryResolve(reference, requestingReader, out resolvedReader);
    }

    bool IModuleNameResolver.TryResolve(string moduleName, MetadataReader requestingReader, out MetadataReader? resolvedReader)
    {
        if (requestingReader == null) { throw new ArgumentNullException(nameof(requestingReader)); }
        if (_disposed) { throw new ObjectDisposedException(nameof(DirectoryAssemblyResolver)); }

        return ((IModuleNameResolver)_loader).TryResolve(moduleName, requestingReader, out resolvedReader);
    }

    /// <summary>
    /// Disposes all readers created during resolution.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) { return; }

        _loader.Dispose();
        _disposed = true;
    }
}
