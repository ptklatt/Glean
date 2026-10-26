using System.Reflection.Metadata;

namespace Glean.Benchmarks;

// This baseline supports the fixture's exact assembly binding, top level types,
// and non generic method overloads with primitive signatures only.
internal sealed class SrmResolutionIndex
{
    private readonly MetadataReader _target;
    private readonly AssemblyIdentity _identity;
    private readonly Dictionary<(string Namespace, string Name), TypeDefinitionHandle> _types = new();
    private readonly Dictionary<(MetadataReader, AssemblyReferenceHandle), bool> _assemblyBindings = new();
    private readonly Dictionary<(MetadataReader, TypeReferenceHandle), TypeDefinitionHandle> _resolvedTypes = new();
    private readonly Dictionary<(MetadataReader, MemberReferenceHandle), EntityHandle> _resolvedMembers = new();
    private readonly Dictionary<TypeDefinitionHandle, Dictionary<string, List<MethodDefinitionHandle>>> _methods = new();
    private readonly Dictionary<(MetadataReader, MemberReferenceHandle), string> _memberNames = new();

    public SrmResolutionIndex(MetadataReader target)
    {
        _target = target;
        var definition = target.GetAssemblyDefinition();
        _identity = Identity(target, definition.Name, definition.Version, definition.Culture, definition.PublicKey);
        foreach (var handle in target.TypeDefinitions)
        {
            var type = target.GetTypeDefinition(handle);
            _types.Add((target.GetString(type.Namespace), target.GetString(type.Name)), handle);
        }
    }

    public bool TryResolveType(MetadataReader source, TypeReferenceHandle handle,
        out MetadataReader target, out TypeDefinitionHandle result)
    {
        target = _target;
        if (_resolvedTypes.TryGetValue((source, handle), out result))
        {
            return true;
        }

        var reference = source.GetTypeReference(handle);
        if (reference.ResolutionScope.Kind != HandleKind.AssemblyReference)
        {
            result = default;
            return false;
        }

        if (!TryBindAssembly(source, (AssemblyReferenceHandle)reference.ResolutionScope) ||
            !_types.TryGetValue((source.GetString(reference.Namespace), source.GetString(reference.Name)), out result))
        {
            result = default;
            return false;
        }

        _resolvedTypes.Add((source, handle), result);
        return true;
    }

    public bool TryResolveMember(MetadataReader source, MemberReferenceHandle handle,
        out MetadataReader target, out EntityHandle result)
    {
        target = _target;
        if (_resolvedMembers.TryGetValue((source, handle), out result))
        {
            return true;
        }

        var reference = source.GetMemberReference(handle);
        result = default;
        if ((reference.Parent.Kind != HandleKind.TypeReference) ||
            !TryResolveType(source, (TypeReferenceHandle)reference.Parent, out target, out var type))
        {
            return false;
        }

        if (!_memberNames.TryGetValue((source, handle), out var name))
        {
            name = source.GetString(reference.Name);
            _memberNames.Add((source, handle), name);
        }

        if (!GetMethods(type).TryGetValue(name, out var candidates))
        {
            return false;
        }

        int matches = 0;
        foreach (var candidate in candidates)
        {
            var definition = target.GetMethodDefinition(candidate);
            if (SignaturesEqual(source, reference.Signature, target, definition.Signature))
            {
                matches++;
                result = candidate;
            }
        }

        if (matches != 1)
        {
            result = default;
            return false;
        }

        _resolvedMembers.Add((source, handle), result);
        return true;
    }

    private bool TryBindAssembly(MetadataReader source, AssemblyReferenceHandle handle)
    {
        if (_assemblyBindings.TryGetValue((source, handle), out bool matches))
        {
            return matches;
        }

        var reference = source.GetAssemblyReference(handle);
        var identity = Identity(source, reference.Name, reference.Version, reference.Culture, reference.PublicKeyOrToken);
        matches = _identity == identity;
        _assemblyBindings.Add((source, handle), matches);
        return matches;
    }

    private Dictionary<string, List<MethodDefinitionHandle>> GetMethods(TypeDefinitionHandle type)
    {
        if (_methods.TryGetValue(type, out var methods))
        {
            return methods;
        }

        methods = new Dictionary<string, List<MethodDefinitionHandle>>(StringComparer.Ordinal);
        foreach (var handle in _target.GetTypeDefinition(type).GetMethods())
        {
            string name = _target.GetString(_target.GetMethodDefinition(handle).Name);
            if (!methods.TryGetValue(name, out var candidates))
            {
                candidates = new List<MethodDefinitionHandle>();
                methods.Add(name, candidates);
            }

            candidates.Add(handle);
        }

        _methods.Add(type, methods);
        return methods;
    }

    private static bool SignaturesEqual(MetadataReader source, BlobHandle sourceHandle,
        MetadataReader target, BlobHandle targetHandle)
    {
        var left = source.GetBlobReader(sourceHandle);
        var right = target.GetBlobReader(targetHandle);
        if (left.Length != right.Length)
        {
            return false;
        }

        while (left.RemainingBytes > 0)
        {
            if (left.ReadByte() != right.ReadByte())
            {
                return false;
            }
        }

        return true;
    }

    private static AssemblyIdentity Identity(MetadataReader reader, StringHandle name,
        Version version, StringHandle culture, BlobHandle publicKey)
    {
        MetadataFixture.Require(publicKey.IsNil, "The SRM baseline requires unsigned fixture assemblies.");
        return new AssemblyIdentity(reader.GetString(name).ToUpperInvariant(), version,
            reader.GetString(culture).ToUpperInvariant());
    }

    private readonly record struct AssemblyIdentity(string Name, Version Version, string Culture);
}
