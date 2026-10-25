using System.Collections.Immutable;
using System.Reflection.Metadata;

using Glean.Contexts;
using Glean.Providers;
using Glean.Signatures;

namespace Glean.Resolution;

public sealed partial class AssemblySet
{
    private bool TryResolveMemberHierarchy(
        MetadataReader reader,
        MemberReferenceHandle referenceHandle,
        MemberReference reference,
        MetadataReader ownerReader,
        TypeDefinitionHandle ownerHandle,
        MemberResolutionIndex? index,
        MemberResolutionCacheKey cacheKey,
        int cacheVersion,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason,
        CancellationToken ct)
    {
        var owner = new MemberSearchType(TypeContext.Create(ownerReader, ownerHandle), default, null);
        int registrationCount = _assemblyByIdentity.Count;
        bool resolved = TryResolveDeclaredMember(reader, referenceHandle, reference, owner, index,
            out targetReader, out targetHandle, out reason);
        ct.ThrowIfCancellationRequested();
        if (resolved)
        {
            CacheHierarchyMember(cacheKey, targetReader, targetHandle, cacheVersion, registrationCount);
            return true;
        }

        if (reason != ResolutionFailureReason.MemberNotFound)
        {
            return false;
        }

        var name = reader.GetString(reference.Name);
        if ((reference.GetKind() == MemberReferenceKind.Method) && ((name == ".ctor") || (name == ".cctor")))
        {
            return false;
        }

        bool searchInterfaces = owner.Type.IsInterface;
        var level = new List<MemberSearchType> { owner };
        var visited = new HashSet<MemberSearchType> { owner };
        while (true)
        {
            var next = new List<MemberSearchType>();
            foreach (var type in level)
            {
                ct.ThrowIfCancellationRequested();
                if (searchInterfaces)
                {
                    foreach (var implementationHandle in type.Type.Definition.GetInterfaceImplementations())
                    {
                        var implementation = type.Type.Reader.GetInterfaceImplementation(implementationHandle);
                        if (!TryAddMemberSearchType(type, implementation.Interface, visited, next, out reason, ct))
                        {
                            targetReader = null!;
                            targetHandle = default;
                            return false;
                        }
                    }
                }
                else
                {
                    var baseType = type.Type.Definition.BaseType;
                    if ((!baseType.IsNil) && !TryAddMemberSearchType(type, baseType, visited, next, out reason, ct))
                    {
                        targetReader = null!;
                        targetHandle = default;
                        return false;
                    }
                }
            }

            if (next.Count == 0)
            {
                targetReader = null!;
                targetHandle = default;
                reason = ResolutionFailureReason.MemberNotFound;
                return false;
            }

            MetadataReader? matchReader = null;
            EntityHandle matchHandle = default;
            registrationCount = _assemblyByIdentity.Count;
            foreach (var type in next)
            {
                ct.ThrowIfCancellationRequested();
                if (TryResolveDeclaredMember(reader, referenceHandle, reference, type, index,
                    out var candidateReader, out var candidateHandle, out reason))
                {
                    if ((matchReader != null) &&
                        (!ReferenceEquals(matchReader, candidateReader) || (matchHandle != candidateHandle)))
                    {
                        targetReader = null!;
                        targetHandle = default;
                        reason = ResolutionFailureReason.MemberAmbiguous;
                        return false;
                    }

                    matchReader = candidateReader;
                    matchHandle = candidateHandle;
                }
                else if (reason != ResolutionFailureReason.MemberNotFound)
                {
                    targetReader = null!;
                    targetHandle = default;
                    return false;
                }
            }

            ct.ThrowIfCancellationRequested();
            if (matchReader != null)
            {
                CacheHierarchyMember(cacheKey, matchReader, matchHandle, cacheVersion, registrationCount);

                targetReader = matchReader;
                targetHandle = matchHandle;
                reason = ResolutionFailureReason.None;
                return true;
            }

            level = next;
        }
    }

    private void CacheHierarchyMember(MemberResolutionCacheKey cacheKey, MetadataReader reader,
        EntityHandle handle, int cacheVersion, int registrationCount)
    {
        // New dependencies can change which signatures matched earlier in this level.
        if (_assemblyByIdentity.Count != registrationCount)
        {
            return;
        }

        if (handle.Kind == HandleKind.MethodDefinition)
        {
            CacheResolvedMethod(cacheKey, reader, (MethodDefinitionHandle)handle, cacheVersion);
        }
        else
        {
            CacheResolvedField(cacheKey, reader, (FieldDefinitionHandle)handle, cacheVersion);
        }
    }

    private bool TryResolveDeclaredMember(
        MetadataReader reader,
        MemberReferenceHandle referenceHandle,
        MemberReference reference,
        MemberSearchType type,
        MemberResolutionIndex? index,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason)
    {
        var context = new SignatureDecodeContext(type.Arguments);
        switch (reference.GetKind())
        {
            case MemberReferenceKind.Method:
                return index != null
                    ? TryResolveIndexedMethodMember(reader, referenceHandle, reference,
                        type.Type.Reader, type.Type.Handle, index,
                        out targetReader, out targetHandle, out reason, context)
                    : TryResolveScannedMethodMember(reader, reference, type.Type.Reader, type.Type.Handle,
                        out targetReader, out targetHandle, out reason, context);

            case MemberReferenceKind.Field:
                return index != null
                    ? TryResolveIndexedFieldMember(reader, referenceHandle, reference,
                        type.Type.Reader, type.Type.Handle, index,
                        out targetReader, out targetHandle, out reason, context)
                    : TryResolveScannedFieldMember(reader, reference, type.Type.Reader, type.Type.Handle,
                        out targetReader, out targetHandle, out reason, context);

            default:
                targetReader = null!;
                targetHandle = default;
                reason = ResolutionFailureReason.MemberNotFound;
                return false;
        }
    }

    private bool TryAddMemberSearchType(
        MemberSearchType parent,
        EntityHandle handle,
        HashSet<MemberSearchType> visited,
        List<MemberSearchType> next,
        out ResolutionFailureReason reason,
        CancellationToken ct)
    {
        var reader = parent.Type.Reader;
        ImmutableArray<TypeSignature> arguments = default;
        if (handle.Kind == HandleKind.TypeSpecification)
        {
            var signature = reader.GetTypeSpecification((TypeSpecificationHandle)handle)
                .DecodeSignature(SignatureTypeProvider.Instance, new SignatureDecodeContext(parent.Arguments));
            if (signature is not GenericInstanceSignature instance)
            {
                reason = ResolutionFailureReason.UnsupportedParentKind;
                return false;
            }

            arguments = instance.Arguments;
            if (instance.GenericType is TypeDefinitionSignature definition)
            {
                reader = definition.Reader;
                handle = definition.Handle;
            }
            else if (instance.GenericType is TypeReferenceSignature reference)
            {
                reader = reference.Reader;
                handle = reference.Handle;
            }
            else
            {
                reason = ResolutionFailureReason.UnsupportedParentKind;
                return false;
            }
        }

        reason = ResolutionFailureReason.None;
        TypeContext type;
        if (handle.Kind == HandleKind.TypeDefinition)
        {
            type = TypeContext.Create(reader, (TypeDefinitionHandle)handle);
        }
        else if ((handle.Kind == HandleKind.TypeReference) &&
            TryResolveType(reader, (TypeReferenceHandle)handle, out var resolvedReader, out var resolvedHandle, out reason, ct))
        {
            type = TypeContext.Create(resolvedReader, resolvedHandle);
        }
        else
        {
            reason = handle.Kind == HandleKind.TypeReference ? reason : ResolutionFailureReason.UnsupportedParentKind;
            return false;
        }

        // Reject cycles by definition along this path, including expanding generic cycles.
        for (MemberSearchType? ancestor = parent; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ancestor.Type.Equals(type))
            {
                reason = ResolutionFailureReason.None;
                return true;
            }
        }

        var candidate = new MemberSearchType(type, arguments, parent);
        if (visited.Add(candidate))
        {
            next.Add(candidate);
        }

        reason = ResolutionFailureReason.None;
        return true;
    }

    private sealed class MemberSearchType : IEquatable<MemberSearchType>
    {
        public MemberSearchType(TypeContext type, ImmutableArray<TypeSignature> arguments, MemberSearchType? parent)
        {
            Type = type;
            Arguments = arguments.IsDefault ? ImmutableArray<TypeSignature>.Empty : arguments;
            Parent = parent;
        }

        public TypeContext Type { get; }
        public ImmutableArray<TypeSignature> Arguments { get; }
        public MemberSearchType? Parent { get; }

        public bool Equals(MemberSearchType? other)
        {
            if ((other == null) || !Type.Equals(other.Type) || (Arguments.Length != other.Arguments.Length))
            {
                return false;
            }

            for (int i = 0; i < Arguments.Length; i++)
            {
                if (!Arguments[i].Equals(other.Arguments[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => (obj is MemberSearchType other) && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Type);
            foreach (var argument in Arguments)
            {
                hash.Add(argument);
            }

            return hash.ToHashCode();
        }
    }
}
