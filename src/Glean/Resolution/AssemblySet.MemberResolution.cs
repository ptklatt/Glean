using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;

using Glean.Decoding;
using Glean.Providers;
using Glean.Signatures;

using static Glean.Resolution.SignatureComparison;

namespace Glean.Resolution;

public sealed partial class AssemblySet
{
    private const int MethodDefinitionHandleFlag = int.MinValue;
    private const int MemberHandleRowMask = int.MaxValue;

    /// <summary>
    /// Resolves a member reference with a <see cref="MemberResolutionIndex"/> for candidate narrowing.
    /// </summary>
    /// <param name="reader">The MetadataReader containing the reference.</param>
    /// <param name="memberRefHandle">The MemberReferenceHandle to resolve.</param>
    /// <param name="index">The opt in member resolution index.</param>
    /// <param name="targetReader">The MetadataReader containing the resolved definition.</param>
    /// <param name="targetHandle">The EntityHandle of the resolved member.</param>
    /// <param name="ct">Token to cancel the resolution.</param>
    /// <returns>True if resolution succeeded; false otherwise.</returns>
    /// <remarks>
    /// This may populate caches in both <see cref="AssemblySet"/> and the provided
    /// <see cref="MemberResolutionIndex"/>.
    /// </remarks>
    public bool TryResolveMember(
        MetadataReader reader,
        MemberReferenceHandle memberRefHandle,
        MemberResolutionIndex index,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        CancellationToken ct = default)
    {
        return TryResolveMember(reader, memberRefHandle, index, out targetReader, out targetHandle, out _, ct);
    }

    /// <summary>
    /// Resolves a member reference with a <see cref="MemberResolutionIndex"/>, returning a failure reason on false.
    /// </summary>
    public bool TryResolveMember(
        MetadataReader reader,
        MemberReferenceHandle memberRefHandle,
        MemberResolutionIndex index,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason,
        CancellationToken ct = default)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (index == null) { throw new ArgumentNullException(nameof(index)); }
        if (_disposed) { throw new ObjectDisposedException(nameof(AssemblySet)); }

        ct.ThrowIfCancellationRequested();

        reason = ResolutionFailureReason.None;
        var cacheKey = CreateMemberResolutionCacheKey(reader, memberRefHandle);
        if (TryGetCachedMemberResolution(cacheKey, out targetReader, out targetHandle))
        {
            return true;
        }

        var memberRef = reader.GetMemberReference(memberRefHandle);
        if (!TryResolveParentType(reader, memberRef.Parent, ref reason, out var parentReader, out var parentTypeHandle))
        {
            targetReader = null!;
            targetHandle = default;
            return false;
        }

        switch (memberRef.GetKind())
        {
            case MemberReferenceKind.Method:
                return TryResolveIndexedMethodMember(
                    reader,
                    memberRefHandle,
                    memberRef,
                    parentReader,
                    parentTypeHandle,
                    index,
                    cacheKey,
                    out targetReader,
                    out targetHandle,
                    out reason);

            case MemberReferenceKind.Field:
                return TryResolveIndexedFieldMember(
                    reader,
                    memberRef,
                    parentReader,
                    parentTypeHandle,
                    index,
                    cacheKey,
                    out targetReader,
                    out targetHandle,
                    out reason);

            default:
                targetReader = null!;
                targetHandle = default;
                reason = ResolutionFailureReason.MemberNotFound;
                return false;
        }
    }

    /// <summary>
    /// Resolves a member reference by scanning the parent type.
    /// </summary>
    /// <param name="reader">The MetadataReader containing the reference.</param>
    /// <param name="memberRefHandle">The MemberReferenceHandle to resolve.</param>
    /// <param name="targetReader">The MetadataReader containing the resolved definition.</param>
    /// <param name="targetHandle">The EntityHandle of the resolved member (MethodDefinitionHandle or FieldDefinitionHandle).</param>
    /// <param name="ct">Token to cancel the resolution.</param>
    /// <returns>True if resolution succeeded; false otherwise.</returns>
    /// <remarks>
    /// For repeated member resolution or large types, build a <see cref="MemberResolutionIndex"/> and use
    /// <see cref="TryResolveMember(MetadataReader, MemberReferenceHandle, MemberResolutionIndex, out MetadataReader, out EntityHandle, CancellationToken)"/>
    /// to narrow the candidate set before semantic comparison.
    /// </remarks>
    public bool TryResolveMember(
        MetadataReader reader,
        MemberReferenceHandle memberRefHandle,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        CancellationToken ct = default)
    {
        return TryResolveMember(reader, memberRefHandle, out targetReader, out targetHandle, out _, ct);
    }

    /// <summary>
    /// Resolves a member reference by scanning the parent type, returning a failure reason on false.
    /// </summary>
    public bool TryResolveMember(
        MetadataReader reader,
        MemberReferenceHandle memberRefHandle,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason,
        CancellationToken ct = default)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }
        if (_disposed) { throw new ObjectDisposedException(nameof(AssemblySet)); }

        ct.ThrowIfCancellationRequested();

        reason = ResolutionFailureReason.None;
        var cacheKey = CreateMemberResolutionCacheKey(reader, memberRefHandle);
        if (TryGetCachedMemberResolution(cacheKey, out targetReader, out targetHandle))
        {
            return true;
        }

        var memberRef = reader.GetMemberReference(memberRefHandle);
        if (!TryResolveParentType(reader, memberRef.Parent, ref reason, out var parentReader, out var parentTypeHandle))
        {
            targetReader = null!;
            targetHandle = default;
            return false;
        }

        switch (memberRef.GetKind())
        {
            case MemberReferenceKind.Method:
                return TryResolveScannedMethodMember(
                    reader,
                    memberRef,
                    parentReader,
                    parentTypeHandle,
                    cacheKey,
                    out targetReader,
                    out targetHandle,
                    out reason);

            case MemberReferenceKind.Field:
                return TryResolveScannedFieldMember(
                    reader,
                    memberRef,
                    parentReader,
                    parentTypeHandle,
                    cacheKey,
                    out targetReader,
                    out targetHandle,
                    out reason);

            default:
                targetReader = null!;
                targetHandle = default;
                reason = ResolutionFailureReason.MemberNotFound;
                return false;
        }
    }

    private bool TryResolveIndexedMethodMember(
        MetadataReader reader,
        MemberReferenceHandle memberRefHandle,
        MemberReference memberRef,
        MetadataReader parentReader,
        TypeDefinitionHandle parentTypeHandle,
        MemberResolutionIndex index,
        MemberResolutionCacheKey cacheKey,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason)
    {
        var memberName = reader.GetString(memberRef.Name);
        var candidates = index.GetMethodCandidatesWithShapes(parentReader, parentTypeHandle, memberName);
        var hasReferenceShape = index.TryGetMemberReferenceMethodShape(reader, memberRefHandle, out var referenceShape);

        var resolution = new MethodResolutionState();
        MethodSignature<TypeSignature> decodedReferenceSignature = default;
        bool hasDecodedReferenceSignature = false;

        for (int i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            var match = EvaluateMethodCandidate(
                reader,
                memberRefHandle,
                memberRef,
                parentReader,
                candidate.Handle,
                candidate.Signature,
                hasReferenceShape,
                referenceShape,
                candidate.HasShape,
                candidate.Shape,
                index,
                ref decodedReferenceSignature,
                ref hasDecodedReferenceSignature);
            resolution.Record(match, candidate.Handle);
        }

        return TryFinalizeMethodResolution(
            resolution,
            cacheKey,
            parentReader,
            out targetReader,
            out targetHandle,
            out reason);
    }

    private bool TryResolveIndexedFieldMember(
        MetadataReader reader,
        MemberReference memberRef,
        MetadataReader parentReader,
        TypeDefinitionHandle parentTypeHandle,
        MemberResolutionIndex index,
        MemberResolutionCacheKey cacheKey,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason)
    {
        var memberName = reader.GetString(memberRef.Name);
        var candidates = index.GetFieldCandidates(parentReader, parentTypeHandle, memberName);
        if (candidates.Length > 0)
        {
            var fieldHandle = candidates[0];
            CacheResolvedField(cacheKey, parentReader, fieldHandle);
            targetReader = parentReader;
            targetHandle = fieldHandle;
            reason = ResolutionFailureReason.None;
            return true;
        }

        targetReader = null!;
        targetHandle = default;
        reason = ResolutionFailureReason.MemberNotFound;
        return false;
    }

    private bool TryResolveScannedMethodMember(
        MetadataReader reader,
        MemberReference memberRef,
        MetadataReader parentReader,
        TypeDefinitionHandle parentTypeHandle,
        MemberResolutionCacheKey cacheKey,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason)
    {
        var parentType = parentReader.GetTypeDefinition(parentTypeHandle);
        bool sameParentReader = ReferenceEquals(parentReader, reader);
        string? memberName = null;
        var resolution = new MethodResolutionState();
        MethodSignature<TypeSignature> decodedReferenceSignature = default;
        bool hasDecodedReferenceSignature = false;

        foreach (var methodHandle in parentType.GetMethods())
        {
            var method = parentReader.GetMethodDefinition(methodHandle);
            if (!MemberNameMatches(reader, memberRef, parentReader, method.Name, sameParentReader, ref memberName))
            {
                continue;
            }

            var match = EvaluateMethodCandidate(
                reader,
                memberRefHandle: default,
                memberRef,
                parentReader,
                methodHandle,
                method.Signature,
                hasReferenceShape: false,
                referenceShape: default,
                hasCandidateShape: false,
                candidateShape: default,
                index: null,
                ref decodedReferenceSignature,
                ref hasDecodedReferenceSignature);
            resolution.Record(match, methodHandle);
        }

        return TryFinalizeMethodResolution(
            resolution,
            cacheKey,
            parentReader,
            out targetReader,
            out targetHandle,
            out reason);
    }

    private bool TryResolveScannedFieldMember(
        MetadataReader reader,
        MemberReference memberRef,
        MetadataReader parentReader,
        TypeDefinitionHandle parentTypeHandle,
        MemberResolutionCacheKey cacheKey,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason)
    {
        var parentType = parentReader.GetTypeDefinition(parentTypeHandle);
        bool sameParentReader = ReferenceEquals(parentReader, reader);
        string? memberName = null;

        foreach (var fieldHandle in parentType.GetFields())
        {
            var field = parentReader.GetFieldDefinition(fieldHandle);
            if (!MemberNameMatches(reader, memberRef, parentReader, field.Name, sameParentReader, ref memberName))
            {
                continue;
            }

            CacheResolvedField(cacheKey, parentReader, fieldHandle);
            targetReader = parentReader;
            targetHandle = fieldHandle;
            reason = ResolutionFailureReason.None;
            return true;
        }

        targetReader = null!;
        targetHandle = default;
        reason = ResolutionFailureReason.MemberNotFound;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MemberResolutionCacheKey CreateMemberResolutionCacheKey(
        MetadataReader reader,
        MemberReferenceHandle memberRefHandle)
    {
        return new MemberResolutionCacheKey(reader, MetadataTokens.GetRowNumber(memberRefHandle));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetCachedMemberResolution(
        MemberResolutionCacheKey cacheKey,
        out MetadataReader targetReader,
        out EntityHandle targetHandle)
    {
        if (_memberResolutionCache.TryGetValue(cacheKey, out var cached))
        {
            targetReader = cached.TargetReader;
            int row = cached.RowAndKind & MemberHandleRowMask;
            if ((cached.RowAndKind & MethodDefinitionHandleFlag) != 0)
            {
                targetHandle = MetadataTokens.MethodDefinitionHandle(row);
            }
            else
            {
                targetHandle = MetadataTokens.FieldDefinitionHandle(row);
            }

            return true;
        }

        targetReader = null!;
        targetHandle = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CacheResolvedMethod(
        MemberResolutionCacheKey cacheKey,
        MetadataReader targetReader,
        MethodDefinitionHandle targetHandle)
    {
        int targetRow = MetadataTokens.GetRowNumber(targetHandle);
        _memberResolutionCache[cacheKey] = new MemberResolutionCacheValue(targetReader, targetRow | MethodDefinitionHandleFlag);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CacheResolvedField(
        MemberResolutionCacheKey cacheKey,
        MetadataReader targetReader,
        FieldDefinitionHandle targetHandle)
    {
        int targetRow = MetadataTokens.GetRowNumber(targetHandle);
        _memberResolutionCache[cacheKey] = new MemberResolutionCacheValue(targetReader, targetRow);
    }

    private bool TryFinalizeMethodResolution(
        in MethodResolutionState resolution,
        MemberResolutionCacheKey cacheKey,
        MetadataReader parentReader,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason)
    {
        if ((resolution.ExactMatchCount == 1) && resolution.ExactMatch.HasValue)
        {
            CacheResolvedMethod(cacheKey, parentReader, resolution.ExactMatch.Value);
            targetReader = parentReader;
            targetHandle = resolution.ExactMatch.Value;
            reason = ResolutionFailureReason.None;
            return true;
        }

        if (resolution.ExactMatchCount > 1)
        {
            targetReader = null!;
            targetHandle = default;
            reason = ResolutionFailureReason.MemberAmbiguous;
            return false;
        }

        if ((resolution.SemanticMatchCount == 1) && resolution.SemanticMatch.HasValue)
        {
            CacheResolvedMethod(cacheKey, parentReader, resolution.SemanticMatch.Value);
            targetReader = parentReader;
            targetHandle = resolution.SemanticMatch.Value;
            reason = ResolutionFailureReason.None;
            return true;
        }

        if (resolution.SemanticMatchCount > 1)
        {
            targetReader = null!;
            targetHandle = default;
            reason = ResolutionFailureReason.MemberAmbiguous;
            return false;
        }

        targetReader = null!;
        targetHandle = default;
        reason = ResolutionFailureReason.MemberNotFound;
        return false;
    }

    private static MethodCandidateMatch EvaluateMethodCandidate(
        MetadataReader referenceReader,
        MemberReferenceHandle memberRefHandle,
        MemberReference memberRef,
        MetadataReader definitionReader,
        MethodDefinitionHandle methodHandle,
        BlobHandle definitionSignature,
        bool hasReferenceShape,
        MemberResolutionIndex.MethodSignatureShape referenceShape,
        bool hasCandidateShape,
        MemberResolutionIndex.MethodSignatureShape candidateShape,
        MemberResolutionIndex? index,
        ref MethodSignature<TypeSignature> decodedReferenceSignature,
        ref bool hasDecodedReferenceSignature)
    {
        var referenceSignature = memberRef.Signature;
        if (hasReferenceShape && hasCandidateShape && (referenceShape != candidateShape))
        {
            return MethodCandidateMatch.None;
        }

        if (SignatureBlobsEqual(referenceReader, referenceSignature, definitionReader, definitionSignature))
        {
            return MethodCandidateMatch.Exact;
        }

        bool shapeMatches = hasReferenceShape && hasCandidateShape
            ? true
            : MethodSignatureHeaderAndCountsMatch(referenceReader, referenceSignature, definitionReader, definitionSignature);
        if (!shapeMatches)
        {
            return MethodCandidateMatch.None;
        }

        bool semanticMatches;
        if ((index != null) && index.CacheSemanticSignatures)
        {
            var referenceDecoded = index.GetOrDecodeMemberReferenceSignature(referenceReader, memberRefHandle);
            var definitionDecoded = index.GetOrDecodeMethodDefinitionSignature(definitionReader, methodHandle);
            semanticMatches = MethodSignaturesSemanticallyMatch(referenceDecoded, definitionDecoded);
        }
        else
        {
            if (!hasDecodedReferenceSignature)
            {
                decodedReferenceSignature = memberRef.DecodeMethodSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
                hasDecodedReferenceSignature = true;
            }

            var method = definitionReader.GetMethodDefinition(methodHandle);
            semanticMatches = MethodSignaturesSemanticallyMatch(decodedReferenceSignature, method);
        }

        return semanticMatches ? MethodCandidateMatch.Semantic : MethodCandidateMatch.None;
    }

    private static bool MemberNameMatches(
        MetadataReader referenceReader,
        MemberReference memberRef,
        MetadataReader definitionReader,
        StringHandle definitionName,
        bool sameReader,
        ref string? referenceMemberName)
    {
        if (sameReader)
        {
            return definitionName == memberRef.Name;
        }

        referenceMemberName ??= referenceReader.GetString(memberRef.Name);
        return definitionReader.StringComparer.Equals(definitionName, referenceMemberName);
    }

    private bool TryResolveTypeSpecificationParent(
        MetadataReader sourceReader,
        TypeSpecificationHandle parentTypeSpecification,
        ref ResolutionFailureReason reason,
        out MetadataReader parentReader,
        out TypeDefinitionHandle parentTypeHandle)
    {
        if (!TypeSpecificationSignatureDecoder.TryGetGenericTypeDefinitionHandle(sourceReader, parentTypeSpecification, out var genericTypeHandle))
        {
            parentReader = null!;
            parentTypeHandle = default;
            reason = ResolutionFailureReason.UnsupportedParentKind;
            return false;
        }

        if (genericTypeHandle.Kind == HandleKind.TypeDefinition)
        {
            parentReader = sourceReader;
            parentTypeHandle = (TypeDefinitionHandle)genericTypeHandle;
            return true;
        }

        if (genericTypeHandle.Kind == HandleKind.TypeReference)
        {
            return TryResolveType(sourceReader, (TypeReferenceHandle)genericTypeHandle, out parentReader, out parentTypeHandle, out reason);
        }

        parentReader = null!;
        parentTypeHandle = default;
        reason = ResolutionFailureReason.UnsupportedParentKind;
        return false;
    }

    private bool TryResolveParentType(
        MetadataReader reader,
        EntityHandle parent,
        ref ResolutionFailureReason reason,
        out MetadataReader parentReader,
        out TypeDefinitionHandle parentTypeHandle)
    {
        if (parent.Kind == HandleKind.TypeReference)
        {
            if (!TryResolveType(reader, (TypeReferenceHandle)parent, out parentReader, out parentTypeHandle, out reason))
            {
                if (reason == ResolutionFailureReason.TypeNotFound)
                {
                    reason = ResolutionFailureReason.ParentTypeNotFound;
                }

                return false;
            }

            return true;
        }

        if (parent.Kind == HandleKind.TypeDefinition)
        {
            parentReader = reader;
            parentTypeHandle = (TypeDefinitionHandle)parent;
            return true;
        }

        if (parent.Kind == HandleKind.TypeSpecification)
        {
            if (!TryResolveTypeSpecificationParent(reader, (TypeSpecificationHandle)parent, ref reason, out parentReader, out parentTypeHandle))
            {
                if (reason == ResolutionFailureReason.TypeNotFound)
                {
                    reason = ResolutionFailureReason.ParentTypeNotFound;
                }

                return false;
            }

            return true;
        }

        parentReader = null!;
        parentTypeHandle = default;
        reason = ResolutionFailureReason.UnsupportedParentKind;
        return false;
    }

    private enum MethodCandidateMatch
    {
        None = 0,
        Exact,
        Semantic
    }

    private struct MethodResolutionState
    {
        public MethodDefinitionHandle? ExactMatch { get; private set; }

        public int ExactMatchCount { get; private set; }

        public MethodDefinitionHandle? SemanticMatch { get; private set; }

        public int SemanticMatchCount { get; private set; }

        public void Record(MethodCandidateMatch match, MethodDefinitionHandle handle)
        {
            switch (match)
            {
                case MethodCandidateMatch.Exact:
                    ExactMatch = handle;
                    ExactMatchCount++;
                    break;

                case MethodCandidateMatch.Semantic:
                    SemanticMatch = handle;
                    SemanticMatchCount++;
                    break;
            }
        }
    }
}
