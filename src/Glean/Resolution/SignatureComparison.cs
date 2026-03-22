using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

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
        MethodSignature<TypeSignature> definitionSignature)
    {
        if (referenceSignature.Header.RawValue != definitionSignature.Header.RawValue) { return false; }
        if (referenceSignature.GenericParameterCount != definitionSignature.GenericParameterCount) { return false; }
        if (referenceSignature.ParameterTypes.Length != definitionSignature.ParameterTypes.Length) { return false; }
        if (!TypeSignaturesEquivalent(referenceSignature.ReturnType, definitionSignature.ReturnType)) { return false; }
        
        for (int i = 0; i < referenceSignature.ParameterTypes.Length; i++)
        {
            if (!TypeSignaturesEquivalent(referenceSignature.ParameterTypes[i], definitionSignature.ParameterTypes[i]))
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
    internal static bool TypeSignaturesEquivalent(TypeSignature left, TypeSignature right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (TryGetNamedTypeIdentity(left, out var leftIdentity) &&
            TryGetNamedTypeIdentity(right, out var rightIdentity))
        {
            return leftIdentity.Matches(rightIdentity);
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
                TypeSignaturesEquivalent(l.ElementType, r.ElementType),

            ArraySignature l when right is ArraySignature r =>
                ArrayShapesEquivalent(l.Shape, r.Shape) &&
                TypeSignaturesEquivalent(l.ElementType, r.ElementType),

            PointerSignature l when right is PointerSignature r =>
                TypeSignaturesEquivalent(l.ElementType, r.ElementType),

            ByRefSignature l when right is ByRefSignature r =>
                TypeSignaturesEquivalent(l.ElementType, r.ElementType),

            PinnedTypeSignature l when right is PinnedTypeSignature r =>
                TypeSignaturesEquivalent(l.ElementType, r.ElementType),

            GenericInstanceSignature l when right is GenericInstanceSignature r =>
                GenericInstancesEquivalent(l, r),

            ModifiedTypeSignature l when right is ModifiedTypeSignature r =>
                ModifiedTypesEquivalent(l, r),

            FunctionPointerSignature l when right is FunctionPointerSignature r =>
                FunctionPointerSignaturesEquivalent(l, r),

            SentinelTypeSignature _ when right is SentinelTypeSignature =>
                true,

            SerializedTypeNameSignature l when right is SerializedTypeNameSignature r =>
                string.Equals(l.SerializedName, r.SerializedName, StringComparison.Ordinal),

            _ => false
        };
    }

    internal static bool TryGetNamedTypeIdentity(TypeSignature signature, out NamedTypeIdentity identity)
    {
        switch (signature)
        {
            case TypeDefinitionSignature def:
                {
                    identity = CreateTypeDefinitionIdentity(def);
                    return true;
                }

            case TypeReferenceSignature typeRef:
                {
                    return TryCreateTypeReferenceIdentity(typeRef, out identity);
                }

            default:
                identity = default;
                return false;
        }
    }

    private static NamedTypeIdentity CreateTypeDefinitionIdentity(TypeDefinitionSignature signature)
    {
        var reader = signature.Reader;
        var handle = signature.Handle;
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
        return new NamedTypeIdentity(nameSpace, names.ToArray(), CreateDefinitionScope(reader));
    }

    private static bool TryCreateTypeReferenceIdentity(
        TypeReferenceSignature signature,
        out NamedTypeIdentity identity)
    {
        var names = new List<string>();
        if (!TryPopulateTypeReferenceIdentity(signature.Reader, signature.Handle, names, out var nameSpace, out var scope))
        {
            identity = default;
            return false;
        }

        identity = new NamedTypeIdentity(nameSpace, names.ToArray(), scope);
        return true;
    }

    private static bool TryPopulateTypeReferenceIdentity(
        MetadataReader reader,
        TypeReferenceHandle handle,
        List<string> names,
        out string nameSpace,
        out NamedTypeScopeIdentity scope)
    {
        var typeReference = reader.GetTypeReference(handle);
        if (typeReference.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            if (!TryPopulateTypeReferenceIdentity(
                    reader,
                    (TypeReferenceHandle)typeReference.ResolutionScope,
                    names,
                    out nameSpace,
                    out scope))
            {
                return false;
            }

            names.Add(reader.GetString(typeReference.Name));
            return true;
        }

        names.Add(reader.GetString(typeReference.Name));
        nameSpace = reader.GetString(typeReference.Namespace);
        scope = CreateReferenceScope(reader, typeReference.ResolutionScope);
        return scope.Kind != NamedTypeScopeKind.Unknown;
    }

    private static NamedTypeScopeIdentity CreateDefinitionScope(MetadataReader reader)
    {
        if (reader.IsAssembly)
        {
            return NamedTypeScopeIdentity.CreateAssembly(
                reader,
                AssemblyIdentityKey.FromDefinition(reader.GetAssemblyDefinition(), reader));
        }

        return NamedTypeScopeIdentity.CreateCurrentModule(reader, GetModuleName(reader));
    }

    private static NamedTypeScopeIdentity CreateReferenceScope(MetadataReader reader, EntityHandle resolutionScope)
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
                return NamedTypeScopeIdentity.CreateCurrentModule(reader, GetModuleName(reader));

            case HandleKind.ModuleReference:
                {
                    var moduleReference = reader.GetModuleReference((ModuleReferenceHandle)resolutionScope);
                    return NamedTypeScopeIdentity.CreateModuleReference(reader.GetString(moduleReference.Name));
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

    internal static bool GenericInstancesEquivalent(GenericInstanceSignature left, GenericInstanceSignature right)
    {
        if (!TypeSignaturesEquivalent(left.GenericType, right.GenericType)) { return false; }
        if (left.Arguments.Length != right.Arguments.Length) { return false; }

        for (int i = 0; i < left.Arguments.Length; i++)
        {
            if (!TypeSignaturesEquivalent(left.Arguments[i], right.Arguments[i]))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool ModifiedTypesEquivalent(ModifiedTypeSignature left, ModifiedTypeSignature right)
    {
        if (!TypeSignaturesEquivalent(left.UnmodifiedType, right.UnmodifiedType)) { return false; }

        if ((left.RequiredModifiers.Length != right.RequiredModifiers.Length) ||
            (left.OptionalModifiers.Length != right.OptionalModifiers.Length))
        {
            return false;
        }

        for (int i = 0; i < left.RequiredModifiers.Length; i++)
        {
            if (!TypeSignaturesEquivalent(left.RequiredModifiers[i], right.RequiredModifiers[i]))
            {
                return false;
            }
        }

        for (int i = 0; i < left.OptionalModifiers.Length; i++)
        {
            if (!TypeSignaturesEquivalent(left.OptionalModifiers[i], right.OptionalModifiers[i]))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool FunctionPointerSignaturesEquivalent(FunctionPointerSignature left, FunctionPointerSignature right)
    {
        var l = left.Signature;
        var r = right.Signature;

        if (l.Header.RawValue != r.Header.RawValue)                         { return false; }
        if (l.GenericParameterCount != r.GenericParameterCount)             { return false; }
        if (l.ParameterTypes.Length != r.ParameterTypes.Length)             { return false; }
        if (!TypeSignaturesEquivalent(l.ReturnType, r.ReturnType)) { return false; }

        for (int i = 0; i < l.ParameterTypes.Length; i++)
        {
            if (!TypeSignaturesEquivalent(l.ParameterTypes[i], r.ParameterTypes[i]))
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
    public readonly string[] NamePath;
    public readonly NamedTypeScopeIdentity Scope;

    public NamedTypeIdentity(string nameSpace, string[] namePath, NamedTypeScopeIdentity scope)
    {
        Namespace = nameSpace;
        NamePath = namePath;
        Scope = scope;
    }

    public bool Matches(in NamedTypeIdentity other)
    {
        if (!string.Equals(Namespace, other.Namespace, StringComparison.Ordinal))
        {
            return false;
        }

        if (NamePath.Length != other.NamePath.Length)
        {
            return false;
        }

        for (int i = 0; i < NamePath.Length; i++)
        {
            if (!string.Equals(NamePath[i], other.NamePath[i], StringComparison.Ordinal))
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

    public static NamedTypeScopeIdentity CreateModuleReference(string moduleName)
    {
        return new NamedTypeScopeIdentity(
            NamedTypeScopeKind.ModuleReference,
            reader: null,
            default,
            moduleName);
    }

    public bool Matches(in NamedTypeScopeIdentity other)
    {
        if ((Kind == NamedTypeScopeKind.Assembly) && (other.Kind == NamedTypeScopeKind.Assembly))
        {
            return AssemblyIdentity.MatchesLoosely(other.AssemblyIdentity);
        }

        if ((Kind == NamedTypeScopeKind.Assembly) && (other.Kind == NamedTypeScopeKind.CurrentModule))
        {
            return ReferenceEquals(Reader, other.Reader);
        }

        if ((Kind == NamedTypeScopeKind.CurrentModule) && (other.Kind == NamedTypeScopeKind.Assembly))
        {
            return ReferenceEquals(Reader, other.Reader);
        }

        if ((Kind == NamedTypeScopeKind.CurrentModule) && (other.Kind == NamedTypeScopeKind.CurrentModule))
        {
            return ReferenceEquals(Reader, other.Reader);
        }

        if ((Kind == NamedTypeScopeKind.ModuleReference) && (other.Kind == NamedTypeScopeKind.ModuleReference))
        {
            return string.Equals(ModuleName, other.ModuleName, StringComparison.OrdinalIgnoreCase);
        }

        if ((Kind == NamedTypeScopeKind.ModuleReference) && (other.Kind == NamedTypeScopeKind.CurrentModule))
        {
            return string.Equals(ModuleName, other.ModuleName, StringComparison.OrdinalIgnoreCase);
        }

        if ((Kind == NamedTypeScopeKind.CurrentModule) && (other.Kind == NamedTypeScopeKind.ModuleReference))
        {
            return string.Equals(ModuleName, other.ModuleName, StringComparison.OrdinalIgnoreCase);
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
