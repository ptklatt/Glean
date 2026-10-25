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
    private const int MemberHandleRowMask        = int.MaxValue;

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
    /// Fields and methods match by name and signature. Multiple matching definitions return
    /// <see cref="ResolutionFailureReason.MemberAmbiguous"/> from the overload with a failure reason.
    /// Class owners search base types; interface owners search parent interfaces by level.
    /// The first matching level wins. Constructors and type initializers inspect only the owner.
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
        int replacementVersion = _cacheVersion;
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

        return TryResolveMemberHierarchy(reader, memberRefHandle, memberRef, parentReader,
            parentTypeHandle, index, cacheKey, replacementVersion,
            out targetReader, out targetHandle, out reason, ct);
    }

    /// <summary>
    /// Resolves a member reference by scanning the owner and its base types or parent interfaces.
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
    /// Fields and methods match by name and signature. Multiple matching definitions return
    /// <see cref="ResolutionFailureReason.MemberAmbiguous"/> from the overload with a failure reason.
    /// Class owners search base types; interface owners search parent interfaces by level.
    /// The first matching level wins. Constructors and type initializers inspect only the owner.
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
    /// Resolves a member reference through its hierarchy, returning a failure reason on false.
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
        int replacementVersion = _cacheVersion;
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

        return TryResolveMemberHierarchy(reader, memberRefHandle, memberRef, parentReader,
            parentTypeHandle, null, cacheKey, replacementVersion,
            out targetReader, out targetHandle, out reason, ct);
    }

    private bool TryResolveIndexedMethodMember(
        MetadataReader reader,
        MemberReferenceHandle memberRefHandle,
        MemberReference memberRef,
        MetadataReader parentReader,
        TypeDefinitionHandle parentTypeHandle,
        MemberResolutionIndex index,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason,
        SignatureDecodeContext definitionContext)
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
                ref hasDecodedReferenceSignature,
                definitionContext);
            resolution.Record(match, candidate.Handle);
        }

        return TryFinalizeMethodResolution(
            resolution,
            parentReader,
            out targetReader,
            out targetHandle,
            out reason);
    }

    private bool TryResolveIndexedFieldMember(
        MetadataReader reader,
        MemberReferenceHandle memberRefHandle,
        MemberReference memberRef,
        MetadataReader parentReader,
        TypeDefinitionHandle parentTypeHandle,
        MemberResolutionIndex index,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason,
        SignatureDecodeContext definitionContext)
    {
        var memberName = reader.GetString(memberRef.Name);
        var candidates = index.GetFieldCandidates(parentReader, parentTypeHandle, memberName);
        TypeSignature? referenceSignature = null;
        FieldDefinitionHandle match = default;
        int matchCount = 0;

        foreach (var fieldHandle in candidates)
        {
            if (FieldSignaturesMatch(reader, memberRefHandle, memberRef, parentReader,
                fieldHandle, index, ref referenceSignature, definitionContext))
            {
                match = fieldHandle;
                matchCount++;
            }
        }

        return TryFinalizeFieldResolution(match, matchCount, parentReader,
            out targetReader, out targetHandle, out reason);
    }

    private bool TryResolveScannedMethodMember(
        MetadataReader reader,
        MemberReference memberRef,
        MetadataReader parentReader,
        TypeDefinitionHandle parentTypeHandle,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason,
        SignatureDecodeContext definitionContext)
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
                ref hasDecodedReferenceSignature,
                definitionContext);
            resolution.Record(match, methodHandle);
        }

        return TryFinalizeMethodResolution(
            resolution,
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
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason,
        SignatureDecodeContext definitionContext)
    {
        var parentType = parentReader.GetTypeDefinition(parentTypeHandle);
        bool sameParentReader = ReferenceEquals(parentReader, reader);
        string? memberName = null;
        TypeSignature? referenceSignature = null;
        FieldDefinitionHandle match = default;
        int matchCount = 0;

        foreach (var fieldHandle in parentType.GetFields())
        {
            var field = parentReader.GetFieldDefinition(fieldHandle);
            if (!MemberNameMatches(reader, memberRef, parentReader, field.Name, sameParentReader, ref memberName))
            {
                continue;
            }

            if (FieldSignaturesMatch(reader, default, memberRef, parentReader,
                fieldHandle, null, ref referenceSignature, definitionContext))
            {
                match = fieldHandle;
                matchCount++;
            }
        }

        return TryFinalizeFieldResolution(match, matchCount, parentReader,
            out targetReader, out targetHandle, out reason);
    }

    private bool FieldSignaturesMatch(
        MetadataReader referenceReader,
        MemberReferenceHandle memberRefHandle,
        MemberReference memberRef,
        MetadataReader definitionReader,
        FieldDefinitionHandle fieldHandle,
        MemberResolutionIndex? index,
        ref TypeSignature? referenceSignature,
        SignatureDecodeContext definitionContext)
    {
        var field = definitionReader.GetFieldDefinition(fieldHandle);
        if ((definitionContext.TypeArguments.IsDefaultOrEmpty) &&
            SignatureBlobsEqual(referenceReader, memberRef.Signature, definitionReader, field.Signature))
        {
            return true;
        }

        TypeSignature definitionSignature;
        if ((index != null) && index.CacheSemanticSignatures)
        {
            referenceSignature ??= index.GetOrDecodeMemberReferenceFieldSignature(referenceReader, memberRefHandle);
            definitionSignature = definitionContext.TypeArguments.IsDefaultOrEmpty
                ? index.GetOrDecodeFieldDefinitionSignature(definitionReader, fieldHandle)
                : field.DecodeSignature(SignatureTypeProvider.Instance, definitionContext);
        }
        else
        {
            referenceSignature ??= memberRef.DecodeFieldSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
            definitionSignature = field.DecodeSignature(SignatureTypeProvider.Instance, definitionContext);
        }

        return TypeSignaturesEquivalent(referenceSignature, definitionSignature, this);
    }

    private static bool TryFinalizeFieldResolution(
        FieldDefinitionHandle match,
        int matchCount,
        MetadataReader parentReader,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason)
    {
        if (matchCount == 1)
        {
            targetReader = parentReader;
            targetHandle = match;
            reason = ResolutionFailureReason.None;
            return true;
        }

        targetReader = null!;
        targetHandle = default;
        reason = matchCount > 1 ? ResolutionFailureReason.MemberAmbiguous : ResolutionFailureReason.MemberNotFound;
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
        if ((_bindingPolicy == AssemblyBindingPolicy.Exact) && _memberResolutionCache.TryGetValue(cacheKey, out var cached))
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
        MethodDefinitionHandle targetHandle,
        int replacementVersion)
    {
        if (replacementVersion != _cacheVersion)
        {
            return;
        }

        int targetRow = MetadataTokens.GetRowNumber(targetHandle);
        _memberResolutionCache[cacheKey] = new MemberResolutionCacheValue(targetReader, targetRow | MethodDefinitionHandleFlag);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CacheResolvedField(
        MemberResolutionCacheKey cacheKey,
        MetadataReader targetReader,
        FieldDefinitionHandle targetHandle,
        int replacementVersion)
    {
        if (replacementVersion != _cacheVersion)
        {
            return;
        }

        int targetRow = MetadataTokens.GetRowNumber(targetHandle);
        _memberResolutionCache[cacheKey] = new MemberResolutionCacheValue(targetReader, targetRow);
    }

    private static bool TryFinalizeMethodResolution(
        in MethodResolutionState resolution,
        MetadataReader parentReader,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason)
    {
        int matchCount = resolution.ExactMatchCount + resolution.SemanticMatchCount;
        if (matchCount == 1)
        {
            var match = resolution.ExactMatch ?? resolution.SemanticMatch.GetValueOrDefault();
            targetReader = parentReader;
            targetHandle = match;
            reason = ResolutionFailureReason.None;
            return true;
        }

        if (matchCount > 1)
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

    private MethodCandidateMatch EvaluateMethodCandidate(
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
        ref bool hasDecodedReferenceSignature,
        SignatureDecodeContext definitionContext)
    {
        var referenceSignature = memberRef.Signature;
        if (hasReferenceShape && hasCandidateShape && (referenceShape != candidateShape))
        {
            return MethodCandidateMatch.None;
        }

        if ((definitionContext.TypeArguments.IsDefaultOrEmpty) &&
            SignatureBlobsEqual(referenceReader, referenceSignature, definitionReader, definitionSignature))
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
            var definitionDecoded = definitionContext.TypeArguments.IsDefaultOrEmpty
                ? index.GetOrDecodeMethodDefinitionSignature(definitionReader, methodHandle)
                : definitionReader.GetMethodDefinition(methodHandle).DecodeSignature(SignatureTypeProvider.Instance, definitionContext);
            semanticMatches = MethodSignaturesSemanticallyMatch(referenceDecoded, definitionDecoded, this);
        }
        else
        {
            if (!hasDecodedReferenceSignature)
            {
                decodedReferenceSignature = memberRef.DecodeMethodSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
                hasDecodedReferenceSignature = true;
            }

            var method = definitionReader.GetMethodDefinition(methodHandle);
            var definitionDecoded = method.DecodeSignature(SignatureTypeProvider.Instance, definitionContext);
            semanticMatches = MethodSignaturesSemanticallyMatch(decodedReferenceSignature, definitionDecoded, this);
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
