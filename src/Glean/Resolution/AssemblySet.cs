using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;

using static Glean.Resolution.SignatureComparison;

namespace Glean.Resolution;

/// <summary>
/// Registry and resolver for cross assembly type and member lookups.
/// </summary>
/// <remarks>
/// Resolution uses analysis semantics over the readers registered with <see cref="Add"/> or
/// <see cref="AddRange"/> and any optional resolvers. It does not model <c>deps.json</c>,
/// runtime binding redirects, or load contexts.
/// <para/>
/// When an exact assembly identity is not available, the set falls back to the highest
/// registered version with the same simple name and public key token.
/// <para/>
/// This type is not thread safe. Call <see cref="ClearCaches"/> to drop warmed lookup state
/// without removing registered assemblies.
/// </remarks>
public sealed partial class AssemblySet : IDisposable
{
    private const int FirstMetadataRowId     = 1;
    private const int MaxForwarderChainDepth = 64;

    // Pre registered readers keyed by full assembly identity.
    private readonly Dictionary<AssemblyIdentityKey, MetadataReader> _assemblyByIdentity = new();

    // Simple name index to support identity aware fallback scans.
    private readonly Dictionary<string, HashSet<AssemblyIdentityKey>> _assemblyKeysBySimpleName = new(StringComparer.OrdinalIgnoreCase);

    // Resolution cache: (requesting reader, handle row number) > (target reader, target handle row number)
    private readonly Dictionary<MetadataReader, Dictionary<int, TypeResolutionCacheValue>> _typeResolutionCacheByRequestingReader =
        new(ReferenceEqualityComparer<MetadataReader>.Instance);

    private MetadataReader? _lastTypeResolutionCacheReader;
    private Dictionary<int, TypeResolutionCacheValue>? _lastTypeResolutionCache;

    private readonly Dictionary<MetadataReader, MetadataReader?[]> _assemblyReferenceCacheByRequestingReader =
        new(ReferenceEqualityComparer<MetadataReader>.Instance);

    private MetadataReader? _lastAssemblyReferenceCacheReader;
    private MetadataReader?[]? _lastAssemblyReferenceCache;

    private readonly Dictionary<MemberResolutionCacheKey, MemberResolutionCacheValue> _memberResolutionCache = new();

    // Per assembly type definition lookup keyed by (namespace, name).
    // Built lazily on first query per assembly.
    private readonly Dictionary<MetadataReader, Dictionary<(string, string), TypeDefinitionHandle>> _typeLookupByReader =
        new(ReferenceEqualityComparer<MetadataReader>.Instance);

    // Per assembly exported type lookup keyed by (namespace, name).
    private readonly Dictionary<MetadataReader, Dictionary<(string, string), ExportedTypeHandle>> _forwarderLookupByReader =
        new(ReferenceEqualityComparer<MetadataReader>.Instance);

    // Scratch buffer for forwarder cycle detection. This instance is not thread safe.
    private ForwarderVisitedKey[]? _forwarderVisitedKeys;

    // Resolved modules are scoped to the requesting assembly to avoid same name netmodule collisions.
    private readonly Dictionary<MetadataReader, Dictionary<string, MetadataReader>> _moduleByNameByRequestingReader =
        new(ReferenceEqualityComparer<MetadataReader>.Instance);

    // Optional resolver callbacks.
    private readonly IAssemblyReferenceResolver? _resolver;
    private readonly IModuleResolver? _moduleResolver;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssemblySet"/> class.
    /// </summary>
    /// <param name="resolver">Optional resolver for loading assemblies not pre registered.</param>
    public AssemblySet(IAssemblyReferenceResolver? resolver = null)
        : this(resolver, moduleResolver: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssemblySet"/> class.
    /// </summary>
    /// <param name="resolver">Optional resolver for loading assemblies not pre registered.</param>
    /// <param name="moduleResolver">Optional resolver for multi module assembly references.</param>
    public AssemblySet(
        IAssemblyReferenceResolver? resolver,
        IModuleResolver? moduleResolver)
    {
        _resolver = resolver;
        _moduleResolver = moduleResolver;
    }

    /// <summary>
    /// Registers a pre loaded reader.
    /// </summary>
    /// <param name="reader">The MetadataReader to register.</param>
    public void Add(MetadataReader reader)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (_disposed)      { throw new ObjectDisposedException(nameof(AssemblySet)); }

        if (!reader.IsAssembly) { throw new ArgumentException("MetadataReader must represent an assembly.", nameof(reader)); }

        var assemblyDef = reader.GetAssemblyDefinition();
        var identityKey = AssemblyIdentityKey.FromDefinition(assemblyDef, reader);

        _assemblyByIdentity[identityKey] = reader;
        if (!_assemblyKeysBySimpleName.TryGetValue(identityKey.Name, out var identities))
        {
            identities = new HashSet<AssemblyIdentityKey>();
            _assemblyKeysBySimpleName[identityKey.Name] = identities;
        }

        identities.Add(identityKey);

    }

    /// <summary>
    /// Registers all readers from the specified sequence.
    /// </summary>
    /// <param name="readers">The readers to register.</param>
    public void AddRange(IEnumerable<MetadataReader> readers)
    {
        if (readers == null) { throw new ArgumentNullException(nameof(readers)); }

        foreach (var r in readers)
        {
            Add(r);
        }
    }

    /// <summary>
    /// Gets the collection of currently registered <see cref="MetadataReader"/> instances.
    /// </summary>
    /// <remarks>
    /// The returned collection reflects the current state of the set.
    /// </remarks>
    public IReadOnlyCollection<MetadataReader> RegisteredAssemblies => _assemblyByIdentity.Values;

    /// <summary>
    /// Clears internal resolution and lookup caches while keeping registered assemblies.
    /// </summary>
    /// <remarks>
    /// Use this to release warmed lookup state in long lived sessions.
    /// </remarks>
    public void ClearCaches()
    {
        if (_disposed) { throw new ObjectDisposedException(nameof(AssemblySet)); }

        _typeResolutionCacheByRequestingReader.Clear();
        _lastTypeResolutionCacheReader = null;
        _lastTypeResolutionCache = null;
        _assemblyReferenceCacheByRequestingReader.Clear();
        _lastAssemblyReferenceCacheReader = null;
        _lastAssemblyReferenceCache = null;
        _memberResolutionCache.Clear();
        _typeLookupByReader.Clear();
        _forwarderLookupByReader.Clear();
        _moduleByNameByRequestingReader.Clear();
    }

    /// <summary>
    /// Resolves a TypeReference to its definition across assemblies.
    /// </summary>
    /// <param name="reader">The MetadataReader containing the reference.</param>
    /// <param name="typeRefHandle">The TypeReferenceHandle to resolve.</param>
    /// <param name="targetReader">The MetadataReader containing the resolved definition.</param>
    /// <param name="targetHandle">The TypeDefinitionHandle of the resolved type.</param>
    /// <param name="ct">Token to cancel the resolution.</param>
    /// <returns>True if resolution succeeded; false otherwise.</returns>
    /// <remarks>
    /// First queries may build internal lookup state. Repeated queries are served from cache.
    /// When no exact version match is found, the highest available version with matching name and
    /// public key token is used as a fallback.
    /// </remarks>
    public bool TryResolveType(
        MetadataReader reader,
        TypeReferenceHandle typeRefHandle,
        out MetadataReader targetReader,
        out TypeDefinitionHandle targetHandle,
        CancellationToken ct = default)
    {
        return TryResolveType(reader, typeRefHandle, out targetReader, out targetHandle, out _, ct);
    }

    /// <summary>
    /// Resolves a TypeReference to its definition across assemblies, returning a failure reason on false.
    /// </summary>
    public bool TryResolveType(
        MetadataReader reader,
        TypeReferenceHandle typeRefHandle,
        out MetadataReader targetReader,
        out TypeDefinitionHandle targetHandle,
        out ResolutionFailureReason reason,
        CancellationToken ct = default)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (_disposed)      { throw new ObjectDisposedException(nameof(AssemblySet)); }

        ct.ThrowIfCancellationRequested();

        reason = ResolutionFailureReason.None;

        int handleRow = MetadataTokens.GetRowNumber(typeRefHandle);
        var typeCache = GetOrCreateTypeResolutionCache(reader);

        if (typeCache.TryGetValue(handleRow, out var cached))
        {
            targetReader = cached.TargetReader;
            targetHandle = MetadataTokens.TypeDefinitionHandle(cached.TypeDefRow);
            return true;
        }

        var typeRef = reader.GetTypeReference(typeRefHandle);
        var resolutionScope = typeRef.ResolutionScope;

        MetadataReader assemblyReader;

        if (resolutionScope.Kind == HandleKind.AssemblyReference)
        {
            if (!TryResolveAssemblyReference(reader, (AssemblyReferenceHandle)resolutionScope, out var resolvedAssemblyReader))
            {
                reason = ResolutionFailureReason.AssemblyNotFound;
                targetReader = null!;
                targetHandle = default;
                return false;
            }

            assemblyReader = resolvedAssemblyReader;
        }
        else if (resolutionScope.Kind == HandleKind.ModuleReference)
        {
            // Same assembly, different module (multi module assemblies).
            if (!TryResolveModuleReference(reader, (ModuleReferenceHandle)resolutionScope, out assemblyReader))
            {
                reason = ResolutionFailureReason.ModuleNotFound;
                targetReader = null!;
                targetHandle = default;
                return false;
            }
        }
        else if (resolutionScope.Kind == HandleKind.ModuleDefinition)
        {
            // Same module: ResolutionScope is the ModuleDefinition row.
            assemblyReader = reader;
        }
        else if (resolutionScope.Kind == HandleKind.TypeReference)
        {
            // Nested type: ResolutionScope is the enclosing TypeReference.
            // Recursively resolve the enclosing type, then search its nested types.
            var enclosingTypeRefHandle = (TypeReferenceHandle)resolutionScope;
            if (!TryResolveType(reader, enclosingTypeRefHandle, out var enclosingReader, out var enclosingTypeHandle, out reason, ct))
            {
                targetReader = null!;
                targetHandle = default;
                return false;
            }

            var enclosingType = enclosingReader.GetTypeDefinition(enclosingTypeHandle);
            var nestedName = reader.GetString(typeRef.Name);

            foreach (var nestedHandle in enclosingType.GetNestedTypes())
            {
                var nestedType = enclosingReader.GetTypeDefinition(nestedHandle);
                var name = enclosingReader.GetString(nestedType.Name);

                if (string.Equals(name, nestedName, StringComparison.Ordinal))
                {
                    int nestedRow = MetadataTokens.GetRowNumber(nestedHandle);
                    typeCache[handleRow] = new TypeResolutionCacheValue(enclosingReader, nestedRow);

                    targetReader = enclosingReader;
                    targetHandle = nestedHandle;
                    return true;
                }
            }

            targetReader = null!;
            targetHandle = default;
            reason = ResolutionFailureReason.NestedTypeNotFound;
            return false;
        }
        else
        {
            targetReader = null!;
            targetHandle = default;
            reason = ResolutionFailureReason.UnsupportedResolutionScope;
            return false;
        }

        var targetNamespace = reader.GetString(typeRef.Namespace);
        var targetName = reader.GetString(typeRef.Name);

        var typeLookup = GetTypeLookup(assemblyReader);
        if (typeLookup.TryGetValue((targetNamespace, targetName), out var foundHandle))
        {
            int targetRow = MetadataTokens.GetRowNumber(foundHandle);
            typeCache[handleRow] = new TypeResolutionCacheValue(assemblyReader, targetRow);

            targetReader = assemblyReader;
            targetHandle = foundHandle;
            return true;
        }

        if (TryResolveForwardedType(assemblyReader, targetNamespace, targetName, typeCache, handleRow, ref reason, ct, out targetReader, out targetHandle))
        {
            return true;
        }

        targetReader = null!;
        targetHandle = default;
        if (reason == ResolutionFailureReason.None)
        {
            reason = ResolutionFailureReason.TypeNotFound;
        }
        return false;
    }

    private bool TryResolveForwardedType(
        MetadataReader assemblyReader,
        string targetNamespace,
        string targetName,
        Dictionary<int, TypeResolutionCacheValue> requestingCache,
        int requestingTypeRefRow,
        ref ResolutionFailureReason reason,
        CancellationToken ct,
        out MetadataReader targetReader,
        out TypeDefinitionHandle targetHandle)
    {
        var forwarderLookup = GetForwarderLookup(assemblyReader);
        if (!forwarderLookup.TryGetValue((targetNamespace, targetName), out var exportedTypeHandle))
        {
            targetReader = null!;
            targetHandle = default;
            return false;
        }

        _forwarderVisitedKeys ??= new ForwarderVisitedKey[MaxForwarderChainDepth];
        return TryResolveForwardedTypeCore(
            assemblyReader,
            exportedTypeHandle,
            targetNamespace,
            targetName,
            requestingCache,
            requestingTypeRefRow,
            _forwarderVisitedKeys,
            visitedCount: 0,
            depth: 0,
            ref reason,
            ct,
            out targetReader,
            out targetHandle);
    }

    private bool TryResolveForwardedTypeCore(
        MetadataReader forwarderOwnerReader,
        ExportedTypeHandle exportedTypeHandle,
        string targetNamespace,
        string targetName,
        Dictionary<int, TypeResolutionCacheValue> requestingCache,
        int requestingTypeRefRow,
        ForwarderVisitedKey[] visited,
        int visitedCount,
        int depth,
        ref ResolutionFailureReason reason,
        CancellationToken ct,
        out MetadataReader targetReader,
        out TypeDefinitionHandle targetHandle)
    {
        ct.ThrowIfCancellationRequested();

        if (depth >= MaxForwarderChainDepth)
        {
            targetReader = null!;
            targetHandle = default;
            reason = ResolutionFailureReason.ForwarderChainTooDeep;
            return false;
        }

        int exportedRow = MetadataTokens.GetRowNumber(exportedTypeHandle);
        var visitedKey = new ForwarderVisitedKey(forwarderOwnerReader, exportedRow);
        if (ContainsVisited(visited, visitedCount, visitedKey))
        {
            targetReader = null!;
            targetHandle = default;
            reason = ResolutionFailureReason.ForwarderCycleDetected;
            return false;
        }

        visited[visitedCount++] = visitedKey;

        var exportedType = forwarderOwnerReader.GetExportedType(exportedTypeHandle);
        var implementation = exportedType.Implementation;

        if (implementation.Kind == HandleKind.AssemblyReference)
        {
            if (!TryResolveAssemblyReference(forwarderOwnerReader, (AssemblyReferenceHandle)implementation, out var forwardReader))
            {
                targetReader = null!;
                targetHandle = default;
                reason = ResolutionFailureReason.AssemblyNotFound;
                return false;
            }

            if (TryResolveTypeInAssembly(forwardReader, targetNamespace, targetName, requestingCache, requestingTypeRefRow, out targetReader, out targetHandle))
            {
                return true;
            }

            return TryResolveForwardedTypeByNameCore(
                forwardReader,
                targetNamespace,
                targetName,
                requestingCache,
                requestingTypeRefRow,
                visited,
                visitedCount,
                depth + 1,
                ref reason,
                ct,
                out targetReader,
                out targetHandle);
        }

        if (implementation.Kind == HandleKind.AssemblyFile)
        {
            var fileHandle = (AssemblyFileHandle)implementation;
            var file = forwarderOwnerReader.GetAssemblyFile(fileHandle);
            var moduleName = forwarderOwnerReader.GetString(file.Name);

            if (!TryResolveModuleByName(forwarderOwnerReader, moduleName, out var moduleReader))
            {
                targetReader = null!;
                targetHandle = default;
                reason = ResolutionFailureReason.ModuleNotFound;
                return false;
            }

            return TryResolveTypeInAssembly(moduleReader, targetNamespace, targetName, requestingCache, requestingTypeRefRow, out targetReader, out targetHandle);
        }

        if (implementation.Kind == HandleKind.ExportedType)
        {
            return TryResolveForwardedTypeCore(
                forwarderOwnerReader,
                (ExportedTypeHandle)implementation,
                targetNamespace,
                targetName,
                requestingCache,
                requestingTypeRefRow,
                visited,
                visitedCount,
                depth + 1,
                ref reason,
                ct,
                out targetReader,
                out targetHandle);
        }

        targetReader = null!;
        targetHandle = default;
        reason = ResolutionFailureReason.TypeForwarderBroken;
        return false;
    }

    private bool TryResolveForwardedTypeByNameCore(
        MetadataReader assemblyReader,
        string targetNamespace,
        string targetName,
        Dictionary<int, TypeResolutionCacheValue> requestingCache,
        int requestingTypeRefRow,
        ForwarderVisitedKey[] visited,
        int visitedCount,
        int depth,
        ref ResolutionFailureReason reason,
        CancellationToken ct,
        out MetadataReader targetReader,
        out TypeDefinitionHandle targetHandle)
    {
        var forwarderLookup = GetForwarderLookup(assemblyReader);
        if (!forwarderLookup.TryGetValue((targetNamespace, targetName), out var exportedTypeHandle))
        {
            targetReader = null!;
            targetHandle = default;
            if (reason == ResolutionFailureReason.None)
            {
                reason = ResolutionFailureReason.TypeNotFound;
            }
            return false;
        }

        return TryResolveForwardedTypeCore(
            assemblyReader,
            exportedTypeHandle,
            targetNamespace,
            targetName,
            requestingCache,
            requestingTypeRefRow,
            visited,
            visitedCount,
            depth,
            ref reason,
            ct,
            out targetReader,
            out targetHandle);
    }

    /// <summary>
    /// Gets or builds the lazy type definition lookup dictionary for an assembly.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Dictionary<(string, string), TypeDefinitionHandle> GetTypeLookup(MetadataReader assemblyReader)
    {
        if (!_typeLookupByReader.TryGetValue(assemblyReader, out var lookup))
        {
            lookup = ResolutionCore.BuildTypeLookup(assemblyReader);
            _typeLookupByReader[assemblyReader] = lookup;
        }
        return lookup;
    }

    /// <summary>
    /// Gets or builds the lazy exported type (forwarder) lookup dictionary for an assembly.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Dictionary<(string, string), ExportedTypeHandle> GetForwarderLookup(MetadataReader assemblyReader)
    {
        if (!_forwarderLookupByReader.TryGetValue(assemblyReader, out var lookup))
        {
            lookup = ResolutionCore.BuildForwarderLookup(assemblyReader);
            _forwarderLookupByReader[assemblyReader] = lookup;
        }
        return lookup;
    }

    private bool TryResolveAssemblyReference(
        MetadataReader requestingReader,
        AssemblyReferenceHandle asmRefHandle,
        out MetadataReader resolvedReader)
    {
        int asmRefRow = MetadataTokens.GetRowNumber(asmRefHandle);
        var refCache = GetOrCreateAssemblyReferenceCache(requestingReader);
        if ((uint)asmRefRow < (uint)refCache.Length)
        {
            var cached = refCache[asmRefRow];
            if (cached != null)
            {
                resolvedReader = cached;
                return true;
            }
        }

        var asmRef = requestingReader.GetAssemblyReference(asmRefHandle);
        var asmIdentity = AssemblyIdentityKey.FromReference(asmRef, requestingReader);

        if (_assemblyByIdentity.TryGetValue(asmIdentity, out resolvedReader!))
        {
            if ((uint)asmRefRow < (uint)refCache.Length)
            {
                refCache[asmRefRow] = resolvedReader;
            }
            return true;
        }

        var asmName = requestingReader.GetString(asmRef.Name);
        if ((_resolver != null) && _resolver.TryResolve(asmRefHandle, requestingReader, out var dynamicReader))
        {
            if (dynamicReader != null)
            {
                Add(dynamicReader);
                resolvedReader = dynamicReader;
                if ((uint)asmRefRow < (uint)refCache.Length)
                {
                    refCache[asmRefRow] = resolvedReader;
                }
                return true;
            }
        }

        // Loose match fallback: same name + public key token, ignores version.
        // Prefers the highest available version among matching candidates.
        if (_assemblyKeysBySimpleName.TryGetValue(asmName, out var looseCandidates))
        {
            MetadataReader? bestReader = null;
            Version? bestVersion = null;

            foreach (var candidate in looseCandidates)
            {
                if (asmIdentity.MatchesLoosely(candidate) &&
                    _assemblyByIdentity.TryGetValue(candidate, out var candidateReader))
                {
                    if ((bestVersion == null) || (candidate.Version.CompareTo(bestVersion) > 0))
                    {
                        bestVersion = candidate.Version;
                        bestReader = candidateReader;
                    }
                }
            }

            if (bestReader != null)
            {
                resolvedReader = bestReader;
                if ((uint)asmRefRow < (uint)refCache.Length)
                {
                    refCache[asmRefRow] = resolvedReader;
                }
                return true;
            }
        }

        resolvedReader = null!;
        return false;
    }

    private bool TryResolveModuleReference(
        MetadataReader requestingReader,
        ModuleReferenceHandle modRefHandle,
        out MetadataReader resolvedReader)
    {
        var modRef = requestingReader.GetModuleReference(modRefHandle);
        var moduleName = requestingReader.GetString(modRef.Name);
        return TryResolveModuleByName(requestingReader, moduleName, out resolvedReader);
    }

    private bool TryResolveModuleByName(
        MetadataReader requestingReader,
        string moduleName,
        out MetadataReader resolvedReader)
    {
        if (!_moduleByNameByRequestingReader.TryGetValue(requestingReader, out var scopedModules))
        {
            scopedModules = new Dictionary<string, MetadataReader>(StringComparer.OrdinalIgnoreCase);
            _moduleByNameByRequestingReader[requestingReader] = scopedModules;
        }

        if (scopedModules.TryGetValue(moduleName, out resolvedReader!))
        {
            return true;
        }

        if (_moduleResolver == null)
        {
            resolvedReader = null!;
            return false;
        }

        int moduleRefRowCount = requestingReader.GetTableRowCount(TableIndex.ModuleRef);
        for (int row = FirstMetadataRowId; row <= moduleRefRowCount; row++)
        {
            var candidateModRefHandle = MetadataTokens.ModuleReferenceHandle(row);
            var candidateModRef = requestingReader.GetModuleReference(candidateModRefHandle);
            if (!string.Equals(requestingReader.GetString(candidateModRef.Name), moduleName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (_moduleResolver.TryResolve(candidateModRefHandle, requestingReader, out var moduleReader) && (moduleReader != null))
            {
                scopedModules[moduleName] = moduleReader;
                resolvedReader = moduleReader;
                return true;
            }
        }

        resolvedReader = null!;
        return false;
    }

    /// <summary>
    /// Searches for a type definition by namespace and name in a specific assembly.
    /// </summary>
    private bool TryResolveTypeInAssembly(
        MetadataReader assemblyReader,
        string targetNamespace,
        string targetName,
        Dictionary<int, TypeResolutionCacheValue> requestingCache,
        int requestingTypeRefRow,
        out MetadataReader targetReader,
        out TypeDefinitionHandle targetHandle)
    {
        var typeLookup = GetTypeLookup(assemblyReader);
        if (typeLookup.TryGetValue((targetNamespace, targetName), out var foundHandle))
        {
            int row = MetadataTokens.GetRowNumber(foundHandle);
            requestingCache[requestingTypeRefRow] = new TypeResolutionCacheValue(assemblyReader, row);

            targetReader = assemblyReader;
            targetHandle = foundHandle;
            return true;
        }

        targetReader = null!;
        targetHandle = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MetadataReader?[] GetOrCreateAssemblyReferenceCache(MetadataReader requestingReader)
    {
        if (ReferenceEquals(_lastAssemblyReferenceCacheReader, requestingReader))
        {
            return _lastAssemblyReferenceCache!;
        }

        if (!_assemblyReferenceCacheByRequestingReader.TryGetValue(requestingReader, out var cache))
        {
            int rowCount = requestingReader.GetTableRowCount(TableIndex.AssemblyRef);
            cache = new MetadataReader?[rowCount + FirstMetadataRowId];
            _assemblyReferenceCacheByRequestingReader[requestingReader] = cache;
        }

        _lastAssemblyReferenceCacheReader = requestingReader;
        _lastAssemblyReferenceCache = cache;
        return cache;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Dictionary<int, TypeResolutionCacheValue> GetOrCreateTypeResolutionCache(MetadataReader requestingReader)
    {
        if (ReferenceEquals(_lastTypeResolutionCacheReader, requestingReader))
        {
            return _lastTypeResolutionCache!;
        }

        if (!_typeResolutionCacheByRequestingReader.TryGetValue(requestingReader, out var cache))
        {
            cache = new Dictionary<int, TypeResolutionCacheValue>();
            _typeResolutionCacheByRequestingReader[requestingReader] = cache;
        }

        _lastTypeResolutionCacheReader = requestingReader;
        _lastTypeResolutionCache = cache;
        return cache;
    }

    /// <summary>
    /// Disposes the AssemblySet.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _assemblyByIdentity.Clear();
            _assemblyKeysBySimpleName.Clear();
            _typeResolutionCacheByRequestingReader.Clear();
            _lastTypeResolutionCacheReader = null;
            _lastTypeResolutionCache = null;
            _assemblyReferenceCacheByRequestingReader.Clear();
            _lastAssemblyReferenceCacheReader = null;
            _lastAssemblyReferenceCache = null;
            _memberResolutionCache.Clear();
            _typeLookupByReader.Clear();
            _forwarderLookupByReader.Clear();
            _moduleByNameByRequestingReader.Clear();
            _disposed = true;
        }
    }

}
