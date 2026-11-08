using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Glean.Contexts;
using Glean.Providers;
using Glean.Signatures;

namespace Glean.Resolution;

/// <summary>
/// Shared signature comparison helpers used by <see cref="AssemblySet"/>.
/// Internal: callers within the assembly import via <c>using static</c>.
/// </summary>
internal static class SignatureComparison
{
    /// <summary>
    /// Compares two method signature blobs for exact byte for byte equality.
    /// </summary>
    /// <remarks>
    /// Fast path for within assembly matches. For cross assembly matching where tokens may differ
    /// (TypeRef vs TypeDef encodings), fall back to
    /// <see cref="MethodSignaturesSemanticallyMatch(MethodSignature{TypeSignature}, MethodDefinition)"/>.
    /// </remarks>
    internal static bool SignatureBlobsEqual(
        MetadataReader referenceReader,
        BlobHandle referenceSignature,
        MetadataReader definitionReader,
        BlobHandle definitionSignature)
    {
        if (!ReferenceEquals(referenceReader, definitionReader))
        {
            return false;
        }

        if (referenceSignature.IsNil || definitionSignature.IsNil) { return false; }

        var refReader = referenceReader.GetBlobReader(referenceSignature);
        var defReader = definitionReader.GetBlobReader(definitionSignature);

        if ((refReader.Length != defReader.Length) || (refReader.Length == 0))
        {
            return false;
        }

        for (int i = 0; i < refReader.Length; i++)
        {
            if (refReader.ReadByte() != defReader.ReadByte())
            {
                return false;
            }
        }

        return true;
    }

    internal static bool MethodSignatureHeaderAndCountsMatch(
        MetadataReader referenceReader,
        BlobHandle referenceSignature,
        MetadataReader definitionReader,
        BlobHandle definitionSignature)
    {
        if (referenceSignature.IsNil || definitionSignature.IsNil) { return false; }

        try
        {
            var refReader = referenceReader.GetBlobReader(referenceSignature);
            var defReader = definitionReader.GetBlobReader(definitionSignature);

            var refHeader = refReader.ReadSignatureHeader();
            var defHeader = defReader.ReadSignatureHeader();

            if (refHeader.RawValue != defHeader.RawValue) { return false; }

            int refGenericCount = refHeader.IsGeneric ? refReader.ReadCompressedInteger() : 0;
            int defGenericCount = defHeader.IsGeneric ? defReader.ReadCompressedInteger() : 0;
            if (refGenericCount != defGenericCount) { return false; }

            int refParamCount = refReader.ReadCompressedInteger();
            int defParamCount = defReader.ReadCompressedInteger();
            if (refParamCount != defParamCount) { return false; }

            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    internal static bool MethodSignaturesSemanticallyMatch(
        MethodSignature<TypeSignature> referenceSignature,
        MethodSignature<TypeSignature> definitionSignature,
        AssemblySet? assemblySet = null)
    {
        if (referenceSignature.Header.RawValue != definitionSignature.Header.RawValue) { return false; }
        if (referenceSignature.GenericParameterCount != definitionSignature.GenericParameterCount) { return false; }
        if (referenceSignature.RequiredParameterCount != definitionSignature.RequiredParameterCount) { return false; }
        if (referenceSignature.ParameterTypes.Length != definitionSignature.ParameterTypes.Length) { return false; }
        if (!TypeSignaturesEquivalent(referenceSignature.ReturnType, definitionSignature.ReturnType, assemblySet)) { return false; }
        
        for (int i = 0; i < referenceSignature.ParameterTypes.Length; i++)
        {
            if (!TypeSignaturesEquivalent(referenceSignature.ParameterTypes[i], definitionSignature.ParameterTypes[i], assemblySet))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool MethodSignaturesSemanticallyMatch(
        MethodSignature<TypeSignature> referenceSignature,
        MethodDefinition methodDefinition)
    {
        var definitionSignature = methodDefinition.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
        return MethodSignaturesSemanticallyMatch(referenceSignature, definitionSignature);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TypeSignaturesEquivalent(TypeSignature left, TypeSignature right, AssemblySet? assemblySet = null)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (TryGetNamedTypeIdentity(left, out var leftIdentity, assemblySet) &&
            TryGetNamedTypeIdentity(right, out var rightIdentity, assemblySet))
        {
            if (leftIdentity.Matches(rightIdentity))
            {
                return true;
            }

            if ((assemblySet != null) &&
                TryResolveNamedType(left, assemblySet, out var leftType) &&
                TryResolveNamedType(right, assemblySet, out var rightType))
            {
                return leftType.Equals(rightType) ||
                    CreateTypeDefinitionIdentity(new TypeDefinitionSignature(leftType.Reader, leftType.Handle), assemblySet)
                        .Matches(CreateTypeDefinitionIdentity(new TypeDefinitionSignature(rightType.Reader, rightType.Handle), assemblySet));
            }

            return false;
        }

        if (left.Kind != right.Kind) { return false; }

        return left switch
        {
            PrimitiveTypeSignature l when right is PrimitiveTypeSignature r =>
                l.TypeCode == r.TypeCode,

            GenericTypeParameterSignature l when right is GenericTypeParameterSignature r =>
                l.Index == r.Index,

            GenericMethodParameterSignature l when right is GenericMethodParameterSignature r =>
                l.Index == r.Index,

            SZArraySignature l when right is SZArraySignature r =>
                TypeSignaturesEquivalent(l.ElementType, r.ElementType, assemblySet),

            ArraySignature l when right is ArraySignature r =>
                ArrayShapesEquivalent(l.Shape, r.Shape) &&
                TypeSignaturesEquivalent(l.ElementType, r.ElementType, assemblySet),

            PointerSignature l when right is PointerSignature r =>
                TypeSignaturesEquivalent(l.ElementType, r.ElementType, assemblySet),

            ByRefSignature l when right is ByRefSignature r =>
                TypeSignaturesEquivalent(l.ElementType, r.ElementType, assemblySet),

            PinnedTypeSignature l when right is PinnedTypeSignature r =>
                TypeSignaturesEquivalent(l.ElementType, r.ElementType, assemblySet),

            GenericInstanceSignature l when right is GenericInstanceSignature r =>
                GenericInstancesEquivalent(l, r, assemblySet),

            ModifiedTypeSignature l when right is ModifiedTypeSignature r =>
                ModifiedTypesEquivalent(l, r, assemblySet),

            FunctionPointerSignature l when right is FunctionPointerSignature r =>
                FunctionPointerSignaturesEquivalent(l, r, assemblySet),

            SentinelTypeSignature _ when right is SentinelTypeSignature =>
                true,

            SerializedTypeNameSignature l when right is SerializedTypeNameSignature r =>
                string.Equals(l.SerializedName, r.SerializedName, StringComparison.Ordinal),

            _ => false
        };
    }

    private static bool TryResolveNamedType(TypeSignature signature, AssemblySet assemblySet, out TypeContext type)
    {
        if (signature is TypeDefinitionSignature definition)
        {
            type = TypeContext.Create(definition.Reader, definition.Handle);
            return true;
        }

        if ((signature is TypeReferenceSignature reference) &&
            assemblySet.TryResolveType(reference.Reader, reference.Handle, out var reader, out var handle))
        {
            type = TypeContext.Create(reader, handle);
            return true;
        }

        type = default;
        return false;
    }

    internal static bool TryGetNamedTypeIdentity(TypeSignature signature, out NamedTypeIdentity identity, AssemblySet? assemblySet = null)
    {
        switch (signature)
        {
            case TypeDefinitionSignature def:
                {
                    identity = CreateTypeDefinitionIdentity(def, assemblySet);
                    return true;
                }

            case TypeReferenceSignature typeRef:
                {
                    return TryCreateTypeReferenceIdentity(typeRef, out identity, assemblySet);
                }

            default:
                identity = default;
                return false;
        }
    }

    private static NamedTypeIdentity CreateTypeDefinitionIdentity(TypeDefinitionSignature signature, AssemblySet? assemblySet = null)
    {
        var reader = signature.Reader;
        var handle = signature.Handle;
        if (!handle.IsNil)
        {
            var definition = reader.GetTypeDefinition(handle);
            if (!definition.IsNested)
            {
                string name = reader.GetString(definition.Name);
                string topLevelNamespace = reader.GetString(definition.Namespace);
                return new NamedTypeIdentity(topLevelNamespace, name, CreateDefinitionScope(reader, assemblySet));
            }
        }

        var names = new List<string>();
        string nameSpace = string.Empty;

        while (!handle.IsNil)
        {
            var typeDefinition = reader.GetTypeDefinition(handle);
            names.Add(reader.GetString(typeDefinition.Name));

            if (!typeDefinition.IsNested)
            {
                nameSpace = reader.GetString(typeDefinition.Namespace);
                break;
            }

            handle = typeDefinition.GetDeclaringType();
        }

        names.Reverse();
        return new NamedTypeIdentity(nameSpace, names.ToArray(), CreateDefinitionScope(reader, assemblySet));
    }

    private static bool TryCreateTypeReferenceIdentity(
        TypeReferenceSignature signature,
        out NamedTypeIdentity identity,
        AssemblySet? assemblySet)
    {
        var reader = signature.Reader;
        var typeReference = reader.GetTypeReference(signature.Handle);
        if (typeReference.ResolutionScope.Kind != HandleKind.TypeReference)
        {
            string name = reader.GetString(typeReference.Name);
            string topLevelNamespace = reader.GetString(typeReference.Namespace);
            var topLevelScope = CreateReferenceScope(reader, typeReference.ResolutionScope, assemblySet);
            if (topLevelScope.Kind == NamedTypeScopeKind.Unknown)
            {
                identity = default;
                return false;
            }

            identity = new NamedTypeIdentity(topLevelNamespace, name, topLevelScope);
            return true;
        }

        var handles = new List<TypeReferenceHandle>();
        var handle = signature.Handle;
        while (true)
        {
            // A valid enclosing chain cannot exceed the number of TypeRef rows.
            if (handles.Count >= reader.TypeReferences.Count)
            {
                throw new BadImageFormatException("Type reference resolution scope contains a cycle.");
            }

            handles.Add(handle);
            if (typeReference.ResolutionScope.Kind != HandleKind.TypeReference)
            {
                break;
            }

            handle = (TypeReferenceHandle)typeReference.ResolutionScope;
            typeReference = reader.GetTypeReference(handle);
        }

        string outerName = reader.GetString(typeReference.Name);
        string nameSpace = reader.GetString(typeReference.Namespace);
        var scope = CreateReferenceScope(reader, typeReference.ResolutionScope, assemblySet);
        if (scope.Kind == NamedTypeScopeKind.Unknown)
        {
            identity = default;
            return false;
        }

        var names = new string[handles.Count];
        names[0] = outerName;
        for (int i = 1; i < names.Length; i++)
        {
            var nestedReference = reader.GetTypeReference(handles[handles.Count - i - 1]);
            names[i] = reader.GetString(nestedReference.Name);
        }

        identity = new NamedTypeIdentity(nameSpace, names, scope);
        return true;
    }

    private static NamedTypeScopeIdentity CreateDefinitionScope(MetadataReader reader, AssemblySet? assemblySet = null)
    {
        if (reader.IsAssembly)
        {
            var definition = reader.GetAssemblyDefinition();
            string name = reader.GetString(definition.Name);
            if ((assemblySet != null) &&
                ReferenceEquals(reader.UTF8Decoder, MetadataStringDecoder.DefaultUTF8) &&
                assemblySet.TryGetRegisteredAssemblyIdentity(reader, name, out var identity))
            {
                return NamedTypeScopeIdentity.CreateAssembly(reader, identity);
            }

            return NamedTypeScopeIdentity.CreateAssembly(
                reader,
                AssemblyIdentityKey.FromDefinition(definition, reader, name));
        }

        return NamedTypeScopeIdentity.CreateCurrentModule(reader, GetModuleName(reader));
    }

    private static NamedTypeScopeIdentity CreateReferenceScope(MetadataReader reader, EntityHandle resolutionScope, AssemblySet? assemblySet)
    {
        switch (resolutionScope.Kind)
        {
            case HandleKind.AssemblyReference:
                {
                    var assemblyReference = reader.GetAssemblyReference((AssemblyReferenceHandle)resolutionScope);
                    return NamedTypeScopeIdentity.CreateAssembly(
                        reader,
                        AssemblyIdentityKey.FromReference(assemblyReference, reader));
                }

            case HandleKind.ModuleDefinition:
                return CreateDefinitionScope(reader, assemblySet);

            case HandleKind.ModuleReference:
                {
                    var moduleReference = reader.GetModuleReference((ModuleReferenceHandle)resolutionScope);
                    return NamedTypeScopeIdentity.CreateModuleReference(reader, reader.GetString(moduleReference.Name));
                }

            default:
                return NamedTypeScopeIdentity.Unknown;
        }
    }

    private static string GetModuleName(MetadataReader reader)
    {
        return reader.GetString(reader.GetModuleDefinition().Name);
    }

    internal static bool ArrayShapesEquivalent(ArrayShape left, ArrayShape right)
    {
        if (left.Rank != right.Rank) { return false; }

        if ((left.Sizes.Length != right.Sizes.Length) || 
            (left.LowerBounds.Length != right.LowerBounds.Length))
        {
            return false;
        }

        for (int i = 0; i < left.Sizes.Length; i++)
        {
            if (left.Sizes[i] != right.Sizes[i])
            {
                return false;
            }
        }

        for (int i = 0; i < left.LowerBounds.Length; i++)
        {
            if (left.LowerBounds[i] != right.LowerBounds[i])
            {
                return false;
            }
        }

        return true;
    }

    internal static bool GenericInstancesEquivalent(GenericInstanceSignature left, GenericInstanceSignature right, AssemblySet? assemblySet = null)
    {
        if (!TypeSignaturesEquivalent(left.GenericType, right.GenericType, assemblySet)) { return false; }
        if (left.Arguments.Length != right.Arguments.Length) { return false; }

        for (int i = 0; i < left.Arguments.Length; i++)
        {
            if (!TypeSignaturesEquivalent(left.Arguments[i], right.Arguments[i], assemblySet))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool ModifiedTypesEquivalent(ModifiedTypeSignature left, ModifiedTypeSignature right, AssemblySet? assemblySet = null)
    {
        if (!TypeSignaturesEquivalent(left.UnmodifiedType, right.UnmodifiedType, assemblySet)) { return false; }

        if ((left.RequiredModifiers.Length != right.RequiredModifiers.Length) ||
            (left.OptionalModifiers.Length != right.OptionalModifiers.Length))
        {
            return false;
        }

        for (int i = 0; i < left.RequiredModifiers.Length; i++)
        {
            if (!TypeSignaturesEquivalent(left.RequiredModifiers[i], right.RequiredModifiers[i], assemblySet))
            {
                return false;
            }
        }

        for (int i = 0; i < left.OptionalModifiers.Length; i++)
        {
            if (!TypeSignaturesEquivalent(left.OptionalModifiers[i], right.OptionalModifiers[i], assemblySet))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool FunctionPointerSignaturesEquivalent(FunctionPointerSignature left, FunctionPointerSignature right, AssemblySet? assemblySet = null)
    {
        var l = left.Signature;
        var r = right.Signature;

        if (l.Header.RawValue != r.Header.RawValue)                         { return false; }
        if (l.GenericParameterCount != r.GenericParameterCount)             { return false; }
        if (l.RequiredParameterCount != r.RequiredParameterCount) { return false; }
        if (l.ParameterTypes.Length != r.ParameterTypes.Length)             { return false; }
        if (!TypeSignaturesEquivalent(l.ReturnType, r.ReturnType, assemblySet)) { return false; }

        for (int i = 0; i < l.ParameterTypes.Length; i++)
        {
            if (!TypeSignaturesEquivalent(l.ParameterTypes[i], r.ParameterTypes[i], assemblySet))
            {
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool ContainsVisited(
        ForwarderVisitedKey[] visited,
        int visitedCount,
        in ForwarderVisitedKey candidate)
    {
        for (int i = 0; i < visitedCount; i++)
        {
            if (visited[i].Equals(candidate))
            {
                return true;
            }
        }

        return false;
    }
}

internal readonly struct NamedTypeIdentity
{
    public readonly string Namespace;
    private readonly string? _singleName;
    private readonly string[]? _namePath;
    public readonly NamedTypeScopeIdentity Scope;

    public NamedTypeIdentity(string nameSpace, string[] namePath, NamedTypeScopeIdentity scope)
    {
        Namespace = nameSpace;
        _singleName = null;
        _namePath = namePath;
        Scope = scope;
    }

    public NamedTypeIdentity(string nameSpace, string name, NamedTypeScopeIdentity scope)
    {
        Namespace = nameSpace;
        _singleName = name;
        _namePath = null;
        Scope = scope;
    }

    public bool Matches(in NamedTypeIdentity other)
    {
        if (!string.Equals(Namespace, other.Namespace, StringComparison.Ordinal))
        {
            return false;
        }

        if ((_singleName != null) && (other._singleName != null))
        {
            return string.Equals(_singleName, other._singleName, StringComparison.Ordinal) && Scope.Matches(other.Scope);
        }

        int length = _singleName != null ? 1 : _namePath!.Length;
        int otherLength = other._singleName != null ? 1 : other._namePath!.Length;
        if (length != otherLength)
        {
            return false;
        }

        for (int i = 0; i < length; i++)
        {
            string name = _singleName ?? _namePath![i];
            string otherName = other._singleName ?? other._namePath![i];
            if (!string.Equals(name, otherName, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return Scope.Matches(other.Scope);
    }
}

internal readonly struct NamedTypeScopeIdentity
{
    public static readonly NamedTypeScopeIdentity Unknown = default;

    public readonly NamedTypeScopeKind Kind;
    public readonly MetadataReader? Reader;
    public readonly AssemblyIdentityKey AssemblyIdentity;
    public readonly string? ModuleName;

    private NamedTypeScopeIdentity(
        NamedTypeScopeKind kind,
        MetadataReader? reader,
        AssemblyIdentityKey assemblyIdentity,
        string? moduleName)
    {
        Kind = kind;
        Reader = reader;
        AssemblyIdentity = assemblyIdentity;
        ModuleName = moduleName;
    }

    public static NamedTypeScopeIdentity CreateAssembly(MetadataReader reader, AssemblyIdentityKey assemblyIdentity)
    {
        return new NamedTypeScopeIdentity(NamedTypeScopeKind.Assembly, reader, assemblyIdentity, moduleName: null);
    }

    public static NamedTypeScopeIdentity CreateCurrentModule(MetadataReader reader, string moduleName)
    {
        return new NamedTypeScopeIdentity(
            NamedTypeScopeKind.CurrentModule,
            reader,
            default,
            moduleName);
    }

    public static NamedTypeScopeIdentity CreateModuleReference(MetadataReader reader, string moduleName)
    {
        return new NamedTypeScopeIdentity(
            NamedTypeScopeKind.ModuleReference,
            reader,
            default,
            moduleName);
    }

    public bool Matches(in NamedTypeScopeIdentity other)
    {
        if ((Kind == NamedTypeScopeKind.Assembly) && (other.Kind == NamedTypeScopeKind.Assembly))
        {
            return AssemblyIdentity.Equals(other.AssemblyIdentity);
        }

        if ((Kind == NamedTypeScopeKind.CurrentModule) && (other.Kind == NamedTypeScopeKind.CurrentModule))
        {
            return ReferenceEquals(Reader, other.Reader);
        }

        if ((Kind == NamedTypeScopeKind.ModuleReference) && (other.Kind == NamedTypeScopeKind.ModuleReference))
        {
            return ReferenceEquals(Reader, other.Reader) &&
                string.Equals(ModuleName, other.ModuleName, FileSystemPathComparison.Comparison);
        }

        return false;
    }
}

internal enum NamedTypeScopeKind
{
    Unknown = 0,
    Assembly,
    CurrentModule,
    ModuleReference
}

internal readonly struct TypeResolutionCacheValue
{
    public readonly MetadataReader TargetReader;
    public readonly int TypeDefRow;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TypeResolutionCacheValue(MetadataReader targetReader, int typeDefRow)
    {
        TargetReader = targetReader;
        TypeDefRow = typeDefRow;
    }
}

internal readonly struct MemberResolutionCacheKey : IEquatable<MemberResolutionCacheKey>
{
    private const int HashCodeMultiplier = 397;

    public readonly MetadataReader RequestingReader;
    public readonly int HandleRow;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MemberResolutionCacheKey(MetadataReader requestingReader, int handleRow)
    {
        RequestingReader = requestingReader;
        HandleRow = handleRow;
    }

    public bool Equals(MemberResolutionCacheKey other) =>
        ReferenceEquals(RequestingReader, other.RequestingReader) && (HandleRow == other.HandleRow);

    public override bool Equals(object? obj) => obj is MemberResolutionCacheKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (RuntimeHelpers.GetHashCode(RequestingReader) * HashCodeMultiplier) ^ HandleRow;
        }
    }
}

internal readonly struct MemberResolutionCacheValue
{
    public readonly MetadataReader TargetReader;
    public readonly int RowAndKind;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MemberResolutionCacheValue(MetadataReader targetReader, int rowAndKind)
    {
        TargetReader = targetReader;
        RowAndKind = rowAndKind;
    }
}

internal readonly struct ForwarderVisitedKey : IEquatable<ForwarderVisitedKey>
{
    private const int HashCodeMultiplier = 397;

    public readonly MetadataReader Reader;
    public readonly int ExportedTypeRow;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ForwarderVisitedKey(MetadataReader reader, int exportedTypeRow)
    {
        Reader = reader;
        ExportedTypeRow = exportedTypeRow;
    }

    public bool Equals(ForwarderVisitedKey other) =>
        ReferenceEquals(Reader, other.Reader) && (ExportedTypeRow == other.ExportedTypeRow);

    public override bool Equals(object? obj) => obj is ForwarderVisitedKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (RuntimeHelpers.GetHashCode(Reader) * HashCodeMultiplier) ^ ExportedTypeRow;
        }
    }
}
