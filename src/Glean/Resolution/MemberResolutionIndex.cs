using System.ComponentModel;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Glean.Providers;
using Glean.Signatures;

namespace Glean.Resolution;

/// <summary>
/// Optional cache and index for repeated member resolution.
/// </summary>
/// <remarks>
/// Most callers should use <see cref="AssemblySet.TryResolveMember(MetadataReader, MemberReferenceHandle, out MetadataReader, out EntityHandle, CancellationToken)"/>
/// directly and only add this index when repeated member resolution becomes hot enough to justify
/// extra cache state.
/// <para/>
/// Caches candidate lists and optional decoded signatures. Call <see cref="Clear"/> to
/// release the cached state.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public sealed class MemberResolutionIndex
{
    private static readonly MethodDefinitionHandle[] EmptyMethodCandidates      = Array.Empty<MethodDefinitionHandle>();
    private static readonly MethodShapeCandidate[]   EmptyMethodShapeCandidates = Array.Empty<MethodShapeCandidate>();
    private static readonly FieldDefinitionHandle[]  EmptyFieldCandidates       = Array.Empty<FieldDefinitionHandle>();

    private readonly Dictionary<(MetadataReader Reader, int TypeRow),      TypeMemberIndex>                _typeIndex = new();
    private readonly Dictionary<(MetadataReader Reader, int MemberRefRow), MethodSignatureShape>           _memberReferenceMethodShapes = new();
    private readonly Dictionary<(MetadataReader Reader, int MethodRow),    MethodSignatureShape>           _methodDefinitionShapes = new();
    private readonly Dictionary<(MetadataReader Reader, int MemberRefRow), MethodSignature<TypeSignature>> _memberReferenceSignatures = new();
    private readonly Dictionary<(MetadataReader Reader, int MethodRow),    MethodSignature<TypeSignature>> _methodDefinitionSignatures = new();
    private readonly Dictionary<(MetadataReader Reader, int MemberRefRow), TypeSignature> _memberReferenceFieldSignatures = new();
    private readonly Dictionary<(MetadataReader Reader, int FieldRow), TypeSignature> _fieldDefinitionSignatures = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MemberResolutionIndex"/> class.
    /// </summary>
    /// <param name="cacheSemanticSignatures">True to cache decoded semantic signatures.</param>
    public MemberResolutionIndex(bool cacheSemanticSignatures = false)
    {
        CacheSemanticSignatures = cacheSemanticSignatures;
    }

    /// <summary>
    /// Gets a value indicating whether decoded semantic signatures are cached.
    /// </summary>
    public bool CacheSemanticSignatures { get; }

    /// <summary>
    /// Clears all cached index and signature state.
    /// </summary>
    /// <remarks>
    /// Use this to release cached state in long lived sessions.
    /// </remarks>
    public void Clear()
    {
        _typeIndex.Clear();
        _memberReferenceMethodShapes.Clear();
        _methodDefinitionShapes.Clear();
        _memberReferenceSignatures.Clear();
        _methodDefinitionSignatures.Clear();
        _memberReferenceFieldSignatures.Clear();
        _fieldDefinitionSignatures.Clear();
    }

    /// <summary>
    /// Returns candidate methods by member name for a parent type.
    /// </summary>
    /// <remarks>
    /// First call per (reader, type) allocates and caches the type member index.
    /// Subsequent calls reuse cached arrays.
    /// </remarks>
    public MethodDefinitionHandle[] GetMethodCandidates(
        MetadataReader reader,
        TypeDefinitionHandle typeHandle,
        string memberName)
    {
        if (reader == null)     { throw new ArgumentNullException(nameof(reader)); }
        if (memberName == null) { throw new ArgumentNullException(nameof(memberName)); }

        var index = GetOrCreateTypeMemberIndex(reader, typeHandle);

        if (index.HandlesByName == null)
        {
            var byName = index.MethodCandidatesByName;
            var result = new Dictionary<string, MethodDefinitionHandle[]>(byName.Count, StringComparer.Ordinal);
            foreach (var pair in byName)
            {
                var shaped = pair.Value;
                var handles = new MethodDefinitionHandle[shaped.Length];
                for (int i = 0; i < shaped.Length; i++)
                {
                    handles[i] = shaped[i].Handle;
                }
                result[pair.Key] = handles;
            }
            index.HandlesByName = result;
        }

        return index.HandlesByName.TryGetValue(memberName, out var candidates)
            ? candidates
            : EmptyMethodCandidates;
    }

    /// <summary>
    /// Returns candidate methods with pre read signature shape metadata by member name for a parent type.
    /// </summary>
    /// <remarks>
    /// First call per (reader, type) allocates and caches the type member index.
    /// Subsequent calls reuse cached arrays and avoid per candidate shape dictionary lookups.
    /// </remarks>
    public MethodShapeCandidate[] GetMethodCandidatesWithShapes(
        MetadataReader reader,
        TypeDefinitionHandle typeHandle,
        string memberName)
    {
        if (reader == null)     { throw new ArgumentNullException(nameof(reader)); }
        if (memberName == null) { throw new ArgumentNullException(nameof(memberName)); }

        var index = GetOrCreateTypeMemberIndex(reader, typeHandle);

        return index.MethodCandidatesByName.TryGetValue(memberName, out var candidates)
            ? candidates
            : EmptyMethodShapeCandidates;
    }

    /// <summary>
    /// Returns candidate fields by member name for a parent type.
    /// </summary>
    /// <remarks>
    /// First call per (reader, type) allocates and caches the type member index.
    /// Subsequent calls reuse cached arrays.
    /// </remarks>
    public FieldDefinitionHandle[] GetFieldCandidates(
        MetadataReader reader,
        TypeDefinitionHandle typeHandle,
        string memberName)
    {
        if (reader == null)     { throw new ArgumentNullException(nameof(reader)); }
        if (memberName == null) { throw new ArgumentNullException(nameof(memberName)); }

        var index = GetOrCreateTypeMemberIndex(reader, typeHandle);

        return index.FieldsByName.TryGetValue(memberName, out var candidates)
            ? candidates
            : EmptyFieldCandidates;
    }

    private TypeMemberIndex GetOrCreateTypeMemberIndex(MetadataReader reader, TypeDefinitionHandle typeHandle)
    {
        var key = (reader, MetadataTokens.GetRowNumber(typeHandle));
        if (!_typeIndex.TryGetValue(key, out var index))
        {
            index = BuildTypeMemberIndex(reader, typeHandle);
            _typeIndex[key] = index;
        }

        return index;
    }

    /// <summary>
    /// Tries to read and cache the lightweight method signature shape of a member reference.
    /// </summary>
    public bool TryGetMemberReferenceMethodShape(
        MetadataReader reader,
        MemberReferenceHandle memberReferenceHandle,
        out MethodSignatureShape shape)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }

        var key = (reader, MetadataTokens.GetRowNumber(memberReferenceHandle));
        if (_memberReferenceMethodShapes.TryGetValue(key, out shape))
        {
            return true;
        }

        var memberReference = reader.GetMemberReference(memberReferenceHandle);
        if (memberReference.GetKind() != MemberReferenceKind.Method)
        {
            shape = default;
            return false;
        }

        if (!TryReadMethodSignatureShape(reader, memberReference.Signature, out shape)) { return false; }

        _memberReferenceMethodShapes[key] = shape;
        return true;
    }

    /// <summary>
    /// Tries to read and cache the lightweight method signature shape of a method definition.
    /// </summary>
    public bool TryGetMethodDefinitionShape(
        MetadataReader reader,
        MethodDefinitionHandle methodDefinitionHandle,
        out MethodSignatureShape shape)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }

        var key = (reader, MetadataTokens.GetRowNumber(methodDefinitionHandle));
        if (_methodDefinitionShapes.TryGetValue(key, out shape))
        {
            return true;
        }

        var method = reader.GetMethodDefinition(methodDefinitionHandle);
        if (!TryReadMethodSignatureShape(reader, method.Signature, out shape)) { return false; }

        _methodDefinitionShapes[key] = shape;
        return true;
    }

    /// <summary>
    /// Returns a cached decoded member reference signature, decoding it once on first use.
    /// </summary>
    public MethodSignature<TypeSignature> GetOrDecodeMemberReferenceSignature(
        MetadataReader reader,
        MemberReferenceHandle memberReferenceHandle)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }

        var key = (reader, MetadataTokens.GetRowNumber(memberReferenceHandle));
        if (_memberReferenceSignatures.TryGetValue(key, out var signature))
        {
            return signature;
        }

        var memberReference = reader.GetMemberReference(memberReferenceHandle);
        signature = memberReference.DecodeMethodSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
        _memberReferenceSignatures[key] = signature;
        return signature;
    }

    /// <summary>
    /// Returns a cached decoded method definition signature, decoding it once on first use.
    /// </summary>
    public MethodSignature<TypeSignature> GetOrDecodeMethodDefinitionSignature(
        MetadataReader reader,
        MethodDefinitionHandle methodDefinitionHandle)
    {
        if (reader == null) { throw new ArgumentNullException(nameof(reader)); }

        var key = (reader, MetadataTokens.GetRowNumber(methodDefinitionHandle));
        if (_methodDefinitionSignatures.TryGetValue(key, out var signature))
        {
            return signature;
        }

        var method = reader.GetMethodDefinition(methodDefinitionHandle);
        signature = method.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
        _methodDefinitionSignatures[key] = signature;
        return signature;
    }

    internal TypeSignature GetOrDecodeMemberReferenceFieldSignature(
        MetadataReader reader,
        MemberReferenceHandle memberReferenceHandle)
    {
        var key = (reader, MetadataTokens.GetRowNumber(memberReferenceHandle));
        if (!_memberReferenceFieldSignatures.TryGetValue(key, out var signature))
        {
            signature = reader.GetMemberReference(memberReferenceHandle)
                .DecodeFieldSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
            _memberReferenceFieldSignatures[key] = signature;
        }

        return signature;
    }

    internal TypeSignature GetOrDecodeFieldDefinitionSignature(
        MetadataReader reader,
        FieldDefinitionHandle fieldDefinitionHandle)
    {
        var key = (reader, MetadataTokens.GetRowNumber(fieldDefinitionHandle));
        if (!_fieldDefinitionSignatures.TryGetValue(key, out var signature))
        {
            signature = reader.GetFieldDefinition(fieldDefinitionHandle)
                .DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
            _fieldDefinitionSignatures[key] = signature;
        }

        return signature;
    }

    private static TypeMemberIndex BuildTypeMemberIndex(MetadataReader reader, TypeDefinitionHandle typeHandle)
    {
        var type = reader.GetTypeDefinition(typeHandle);

        var methodCandidatesWithShapes = new Dictionary<string, CandidateBucket<MethodShapeCandidate>>(StringComparer.Ordinal);
        foreach (var methodHandle in type.GetMethods())
        {
            var method = reader.GetMethodDefinition(methodHandle);
            var name = reader.GetString(method.Name);
            bool hasShape = TryReadMethodSignatureShape(reader, method.Signature, out var shape);
            AddCandidate(methodCandidatesWithShapes, name, new MethodShapeCandidate(methodHandle, method.Signature, hasShape, shape));
        }

        var fieldCandidates = new Dictionary<string, CandidateBucket<FieldDefinitionHandle>>(StringComparer.Ordinal);
        foreach (var fieldHandle in type.GetFields())
        {
            var field = reader.GetFieldDefinition(fieldHandle);
            var name = reader.GetString(field.Name);
            AddCandidate(fieldCandidates, name, fieldHandle);
        }

        return new TypeMemberIndex(
            MaterializeCandidates(methodCandidatesWithShapes),
            MaterializeCandidates(fieldCandidates));
    }

    private static void AddCandidate<T>(Dictionary<string, CandidateBucket<T>> byName, string name, T candidate)
    {
        if (byName.TryGetValue(name, out var bucket))
        {
            if (bucket.Add(candidate))
            {
                byName[name] = bucket;
            }
        }
        else
        {
            byName.Add(name, new CandidateBucket<T>(candidate));
        }
    }

    private static Dictionary<string, T[]> MaterializeCandidates<T>(Dictionary<string, CandidateBucket<T>> byName)
    {
        var materialized = new Dictionary<string, T[]>(byName.Count, StringComparer.Ordinal);
        foreach (var pair in byName)
        {
            materialized[pair.Key] = pair.Value.ToArray();
        }
        return materialized;
    }

    private struct CandidateBucket<T>(T first)
    {
        private readonly T _first = first;
        private List<T>? _many;

        public bool Add(T value)
        {
            bool needsWriteback = _many == null;
            _many ??= new List<T> { _first };
            _many.Add(value);
            return needsWriteback;
        }

        public readonly T[] ToArray() => _many != null ? _many.ToArray() : [_first];
    }

    private static bool TryReadMethodSignatureShape(MetadataReader reader, BlobHandle signatureHandle, out MethodSignatureShape shape)
    {
        if (signatureHandle.IsNil)
        {
            shape = default;
            return false;
        }

        try
        {
            var blob = reader.GetBlobReader(signatureHandle);
            var header = blob.ReadSignatureHeader();
            int genericParameterCount = header.IsGeneric ? blob.ReadCompressedInteger() : 0;
            int parameterCount = blob.ReadCompressedInteger();

            shape = new MethodSignatureShape(header.RawValue, genericParameterCount, parameterCount);
            return true;
        }
        catch (BadImageFormatException)
        {
            shape = default;
            return false;
        }
    }

    private sealed class TypeMemberIndex(
        Dictionary<string, MethodShapeCandidate[]> methodCandidatesByName,
        Dictionary<string, FieldDefinitionHandle[]> fieldsByName)
    {
        public readonly Dictionary<string, MethodShapeCandidate[]> MethodCandidatesByName = methodCandidatesByName;
        public readonly Dictionary<string, FieldDefinitionHandle[]> FieldsByName = fieldsByName;
        public Dictionary<string, MethodDefinitionHandle[]>? HandlesByName;
    }

    /// <summary>
    /// Method candidate plus optional pre read method signature shape.
    /// </summary>
    public readonly record struct MethodShapeCandidate(
        MethodDefinitionHandle Handle,
        BlobHandle Signature,
        bool HasShape,
        MethodSignatureShape Shape);

    /// <summary>
    /// Lightweight method signature shape used for candidate pruning before semantic comparisons.
    /// </summary>
    public readonly record struct MethodSignatureShape(
        byte HeaderRawValue,
        int GenericParameterCount,
        int ParameterCount)
    {
        /// <summary>Returns true if this shape is compatible with <paramref name="other"/> (same header, generic arity, and parameter count).</summary>
        public bool Matches(MethodSignatureShape other) => this == other;
    }
}
