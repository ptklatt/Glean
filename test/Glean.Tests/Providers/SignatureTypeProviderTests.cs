using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Xunit;

using Glean.Providers;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Providers;

public class SignatureTypeProviderTests
{
    private static readonly SignatureTypeProvider Provider = SignatureTypeProvider.Instance;

    // == GetPrimitiveType ====================================================

    [Fact]
    public void GetPrimitiveType_Int32_ReturnsPrimitiveSignatureWithCorrectCode()
    {
        var sig  = Provider.GetPrimitiveType(PrimitiveTypeCode.Int32);
        var prim = Assert.IsType<PrimitiveTypeSignature>(sig);
        Assert.Equal(PrimitiveTypeCode.Int32, prim.TypeCode);
    }

    [Fact]
    public void GetPrimitiveType_SameCode_ReturnsSameInstance()
    {
        var a = Provider.GetPrimitiveType(PrimitiveTypeCode.Boolean);
        var b = Provider.GetPrimitiveType(PrimitiveTypeCode.Boolean);
        Assert.Same(a, b);
    }

    // == GetTypeFromDefinition ===============================================

    [Fact]
    public void GetTypeFromDefinition_ReturnsTypeDefinitionSignatureWithCorrectIdentity()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var reader = metadata.Reader;

        var handle = FindTypeDefinition(reader, "System", "String");
        Assert.False(handle.IsNil, "Could not locate System.String TypeDefinition in CoreLib.");

        var sig     = Provider.GetTypeFromDefinition(reader, handle, 0);
        var typeDef = Assert.IsType<TypeDefinitionSignature>(sig);
        Assert.Equal(TypeSignatureKind.TypeDefinition, typeDef.Kind);
        Assert.True(typeDef.Is("System", "String"));
    }

    // == GetTypeFromReference ================================================

    [Fact]
    public void GetTypeFromReference_ReturnsTypeReferenceSignatureWithCorrectIdentity()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeSignature).Assembly);
        var reader = metadata.Reader;

        var handle = FindTypeReference(reader, "System", "Object");
        Assert.False(handle.IsNil, "Could not locate System.Object TypeReference.");

        var sig     = Provider.GetTypeFromReference(reader, handle, 0);
        var typeRef = Assert.IsType<TypeReferenceSignature>(sig);
        Assert.Equal(TypeSignatureKind.TypeReference, typeRef.Kind);
        Assert.True(typeRef.Is("System", "Object"));
    }

    // == GetSZArrayType ======================================================

    [Fact]
    public void GetSZArrayType_WrapsElementType()
    {
        var elem  = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        var sig   = Provider.GetSZArrayType(elem);
        var szArr = Assert.IsType<SZArraySignature>(sig);
        Assert.Same(elem, szArr.ElementType);
    }

    // == GetArrayType ========================================================

    [Fact]
    public void GetArrayType_PreservesRankInShape()
    {
        var elem  = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        var shape = new ArrayShape(rank: 3, sizes: ImmutableArray<int>.Empty, lowerBounds: ImmutableArray<int>.Empty);
        var sig   = Provider.GetArrayType(elem, shape);
        var arr   = Assert.IsType<ArraySignature>(sig);
        Assert.Same(elem, arr.ElementType);
        Assert.Equal(3, arr.Shape.Rank);
    }

    // == GetByReferenceType ==================================================

    [Fact]
    public void GetByReferenceType_WrapsElementType()
    {
        var elem  = PrimitiveTypeSignature.Get(PrimitiveTypeCode.String);
        var sig   = Provider.GetByReferenceType(elem);
        var byRef = Assert.IsType<ByRefSignature>(sig);
        Assert.Same(elem, byRef.ElementType);
    }

    // == GetPointerType ======================================================

    [Fact]
    public void GetPointerType_WrapsElementType()
    {
        var elem = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Byte);
        var sig  = Provider.GetPointerType(elem);
        var ptr  = Assert.IsType<PointerSignature>(sig);
        Assert.Same(elem, ptr.ElementType);
    }

    // == GetGenericInstantiation =============================================

    [Fact]
    public void GetGenericInstantiation_ReturnsGenericInstanceWithArguments()
    {
        var openType = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Object);
        var args     = ImmutableArray.Create<TypeSignature>(
            PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32),
            PrimitiveTypeSignature.Get(PrimitiveTypeCode.String));

        var sig     = Provider.GetGenericInstantiation(openType, args);
        var genInst = Assert.IsType<GenericInstanceSignature>(sig);
        Assert.Same(openType, genInst.GenericType);
        Assert.Equal(2, genInst.Arguments.Length);
        Assert.Same(args[0], genInst.Arguments[0]);
        Assert.Same(args[1], genInst.Arguments[1]);
    }

    // == GetGenericTypeParameter =============================================

    [Fact]
    public void GetGenericTypeParameter_EmptyContext_ReturnsParameterSignatureAtIndex()
    {
        var sig   = Provider.GetGenericTypeParameter(SignatureDecodeContext.Empty, 2);
        var param = Assert.IsType<GenericTypeParameterSignature>(sig);
        Assert.Equal(2, param.Index);
    }

    [Fact]
    public void GetGenericTypeParameter_WithTypeArguments_SubstitutesCorrectArgument()
    {
        var int32 = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        var str   = PrimitiveTypeSignature.Get(PrimitiveTypeCode.String);
        var ctx   = new SignatureDecodeContext(typeArguments: ImmutableArray.Create<TypeSignature>(int32, str));
        var sig   = Provider.GetGenericTypeParameter(ctx, 1);
        Assert.Same(str, sig);
    }

    [Fact]
    public void GetGenericTypeParameter_IndexOutOfRange_ReturnsParameterSignature()
    {
        var int32 = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        var ctx   = new SignatureDecodeContext(typeArguments: ImmutableArray.Create<TypeSignature>(int32));
        var sig   = Provider.GetGenericTypeParameter(ctx, 5);
        var param = Assert.IsType<GenericTypeParameterSignature>(sig);
        Assert.Equal(5, param.Index);
    }

    // == GetGenericMethodParameter ===========================================

    [Fact]
    public void GetGenericMethodParameter_EmptyContext_ReturnsMethodParameterSignatureAtIndex()
    {
        var sig   = Provider.GetGenericMethodParameter(SignatureDecodeContext.Empty, 0);
        var param = Assert.IsType<GenericMethodParameterSignature>(sig);
        Assert.Equal(0, param.Index);
    }

    [Fact]
    public void GetGenericMethodParameter_WithMethodArguments_SubstitutesCorrectArgument()
    {
        var int32 = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        var str   = PrimitiveTypeSignature.Get(PrimitiveTypeCode.String);
        var ctx   = new SignatureDecodeContext(methodArguments: ImmutableArray.Create<TypeSignature>(int32, str));
        var sig   = Provider.GetGenericMethodParameter(ctx, 0);
        Assert.Same(int32, sig);
    }

    [Fact]
    public void GetGenericMethodParameter_IndexOutOfRange_ReturnsMethodParameterSignature()
    {
        var int32 = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        var ctx   = new SignatureDecodeContext(methodArguments: ImmutableArray.Create<TypeSignature>(int32));
        var sig   = Provider.GetGenericMethodParameter(ctx, 3);
        var param = Assert.IsType<GenericMethodParameterSignature>(sig);
        Assert.Equal(3, param.Index);
    }

    // == GetPinnedType =======================================================

    [Fact]
    public void GetPinnedType_WrapsElementType()
    {
        var elem   = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Byte);
        var sig    = Provider.GetPinnedType(elem);
        var pinned = Assert.IsType<PinnedTypeSignature>(sig);
        Assert.Same(elem, pinned.ElementType);
    }

    // == GetSentinelType =====================================================

    [Fact]
    public void GetSentinelType_ReturnsSingletonInstance()
    {
        Assert.Same(SentinelTypeSignature.Instance, Provider.GetSentinelType());
    }

    // == GetFunctionPointerType ==============================================

    [Fact]
    public void GetFunctionPointerType_WrapsMethodSignature()
    {
        var innerSig = new MethodSignature<TypeSignature>(
            header: default,
            returnType: PrimitiveTypeSignature.Get(PrimitiveTypeCode.Void),
            requiredParameterCount: 0,
            genericParameterCount: 0,
            parameterTypes: ImmutableArray<TypeSignature>.Empty);

        var fp = Assert.IsType<FunctionPointerSignature>(Provider.GetFunctionPointerType(innerSig));
        Assert.Same(PrimitiveTypeSignature.Get(PrimitiveTypeCode.Void), fp.Signature.ReturnType);
    }

    // == GetModifiedType =====================================================

    [Fact]
    public void GetModifiedType_Required_OnUnmodifiedType_SetsRequiredModifier()
    {
        var modifier = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        var baseType = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Boolean);
        var sig      = Provider.GetModifiedType(modifier, baseType, isRequired: true);
        var mod      = Assert.IsType<ModifiedTypeSignature>(sig);
        Assert.Same(baseType, mod.UnmodifiedType);
        Assert.Same(modifier, Assert.Single(mod.RequiredModifiers));
        Assert.Empty(mod.OptionalModifiers);
    }

    [Fact]
    public void GetModifiedType_Optional_OnUnmodifiedType_SetsOptionalModifier()
    {
        var modifier = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        var baseType = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Boolean);
        var sig      = Provider.GetModifiedType(modifier, baseType, isRequired: false);
        var mod      = Assert.IsType<ModifiedTypeSignature>(sig);
        Assert.Empty(mod.RequiredModifiers);
        Assert.Same(modifier, Assert.Single(mod.OptionalModifiers));
    }

    [Fact]
    public void GetModifiedType_Required_OnAlreadyModified_AccumulatesRequired()
    {
        var mod1     = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        var mod2     = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Byte);
        var baseType = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Boolean);

        var firstPass  = Provider.GetModifiedType(mod1, baseType,      isRequired: true);
        var secondPass = Provider.GetModifiedType(mod2, firstPass, isRequired: true);

        var result = Assert.IsType<ModifiedTypeSignature>(secondPass);
        Assert.Same(baseType, result.UnmodifiedType);
        Assert.Equal(2, result.RequiredModifiers.Length);
        Assert.Same(mod1, result.RequiredModifiers[0]);
        Assert.Same(mod2, result.RequiredModifiers[1]);
        Assert.Empty(result.OptionalModifiers);
    }

    [Fact]
    public void GetModifiedType_Optional_OnAlreadyModified_AccumulatesOptional()
    {
        var mod1     = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        var mod2     = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Byte);
        var baseType = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Boolean);

        var firstPass  = Provider.GetModifiedType(mod1, baseType,  isRequired: false);
        var secondPass = Provider.GetModifiedType(mod2, firstPass, isRequired: false);

        var result = Assert.IsType<ModifiedTypeSignature>(secondPass);
        Assert.Same(baseType, result.UnmodifiedType);
        Assert.Empty(result.RequiredModifiers);
        Assert.Equal(2, result.OptionalModifiers.Length);
        Assert.Same(mod1, result.OptionalModifiers[0]);
        Assert.Same(mod2, result.OptionalModifiers[1]);
    }

    [Fact]
    public void GetModifiedType_Optional_OnRequiredModifiedType_KeepsSeparate()
    {
        var reqMod   = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        var optMod   = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Byte);
        var baseType = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Boolean);

        var firstPass  = Provider.GetModifiedType(reqMod, baseType,  isRequired: true);
        var secondPass = Provider.GetModifiedType(optMod, firstPass, isRequired: false);

        var result = Assert.IsType<ModifiedTypeSignature>(secondPass);
        Assert.Same(baseType, result.UnmodifiedType);
        Assert.Same(reqMod, Assert.Single(result.RequiredModifiers));
        Assert.Same(optMod, Assert.Single(result.OptionalModifiers));
    }

    // == Integration: Real BCL metadata ======================================

    private static TypeDefinitionHandle FindTypeDefinition(MetadataReader reader, string ns, string name)
    {
        foreach (var handle in reader.TypeDefinitions)
        {
            var typeDef = reader.GetTypeDefinition(handle);
            if ((reader.GetString(typeDef.Namespace) == ns) &&
                (reader.GetString(typeDef.Name) == name))
            {
                return handle;
            }
        }
        return default;
    }

    private static TypeReferenceHandle FindTypeReference(MetadataReader reader, string ns, string name)
    {
        int count = reader.GetTableRowCount(TableIndex.TypeRef);
        for (int row = 1; row <= count; row++)
        {
            var handle  = MetadataTokens.TypeReferenceHandle(row);
            var typeRef = reader.GetTypeReference(handle);
            if ((reader.GetString(typeRef.Namespace) == ns) &&
                (reader.GetString(typeRef.Name) == name))
            {
                return handle;
            }
        }
        return default;
    }

    private static MethodDefinition GetMethodDefinition(MetadataReader reader, MethodInfo method)
    {
        return reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(method.MetadataToken));
    }

    [Fact]
    public void Decode_StringContains_ReturnTypeIsPrimitiveBool_ParamIsPrimitiveString()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var reader = metadata.Reader;

        var methodInfo = typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) });
        Assert.NotNull(methodInfo);

        var sig = GetMethodDefinition(reader, methodInfo!).DecodeSignature(Provider, SignatureDecodeContext.Empty);

        var ret = Assert.IsType<PrimitiveTypeSignature>(sig.ReturnType);
        Assert.Equal(PrimitiveTypeCode.Boolean, ret.TypeCode);

        var param = Assert.IsType<PrimitiveTypeSignature>(Assert.Single(sig.ParameterTypes));
        Assert.Equal(PrimitiveTypeCode.String, param.TypeCode);
    }

    [Fact]
    public void Decode_ListAdd_ParameterIsGenericTypeParameterIndex0()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var reader = metadata.Reader;

        var itemType = typeof(List<>).GetGenericArguments()[0];
        var methodInfo = typeof(List<>).GetMethod("Add", new[] { itemType });
        Assert.NotNull(methodInfo);

        var sig   = GetMethodDefinition(reader, methodInfo!).DecodeSignature(Provider, SignatureDecodeContext.Empty);
        var param = Assert.IsType<GenericTypeParameterSignature>(Assert.Single(sig.ParameterTypes));
        Assert.Equal(0, param.Index);
    }

    [Fact]
    public void Decode_ListAdd_WithIntContext_SubstitutesIntForTypeParameter()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var reader = metadata.Reader;

        var itemType = typeof(List<>).GetGenericArguments()[0];
        var methodInfo = typeof(List<>).GetMethod("Add", new[] { itemType });
        Assert.NotNull(methodInfo);

        var intSig = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        var ctx    = new SignatureDecodeContext(
            typeArguments: ImmutableArray.Create<TypeSignature>(intSig));

        var sig = GetMethodDefinition(reader, methodInfo!).DecodeSignature(Provider, ctx);
        Assert.Same(intSig, Assert.Single(sig.ParameterTypes));
    }

    [Fact]
    public void Decode_DictionaryTryGetValue_OutParamIsRefGenericTypeParameter()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var reader = metadata.Reader;

        var genericArgs = typeof(Dictionary<,>).GetGenericArguments();
        var methodInfo = typeof(Dictionary<,>).GetMethod("TryGetValue", new[] { genericArgs[0], genericArgs[1].MakeByRefType() });
        Assert.NotNull(methodInfo);

        var sig = GetMethodDefinition(reader, methodInfo!).DecodeSignature(Provider, SignatureDecodeContext.Empty);

        var ret = Assert.IsType<PrimitiveTypeSignature>(sig.ReturnType);
        Assert.Equal(PrimitiveTypeCode.Boolean, ret.TypeCode);

        // Last parameter is `out TValue` > ByRef(GenericTypeParameter(1))
        var lastParam = sig.ParameterTypes[sig.ParameterTypes.Length - 1];
        var byRef     = Assert.IsType<ByRefSignature>(lastParam);
        var typeParam = Assert.IsType<GenericTypeParameterSignature>(byRef.ElementType);
        Assert.Equal(1, typeParam.Index);
    }

    [Fact]
    public void Decode_ListGetRange_ReturnTypeIsGenericInstanceViaTypeSpec()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var reader = metadata.Reader;

        var methodInfo = typeof(List<>).GetMethod("GetRange", new[] { typeof(int), typeof(int) });
        Assert.NotNull(methodInfo);

        var sig = GetMethodDefinition(reader, methodInfo!).DecodeSignature(Provider, SignatureDecodeContext.Empty);

        var genInst = Assert.IsType<GenericInstanceSignature>(sig.ReturnType);
        Assert.Equal(TypeSignatureKind.TypeDefinition, genInst.GenericType.Kind);
        var arg = Assert.IsType<GenericTypeParameterSignature>(Assert.Single(genInst.Arguments));
        Assert.Equal(0, arg.Index);
    }

    [Fact]
    public void Decode_ListGetRange_WithIntContext_SubstitutesIntIntoReturnTypeViaTypeSpec()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var reader = metadata.Reader;

        var methodInfo = typeof(List<>).GetMethod("GetRange", new[] { typeof(int), typeof(int) });
        Assert.NotNull(methodInfo);

        var intSig = PrimitiveTypeSignature.Get(PrimitiveTypeCode.Int32);
        var ctx = new SignatureDecodeContext(
            typeArguments: ImmutableArray.Create<TypeSignature>(intSig));

        var sig = GetMethodDefinition(reader, methodInfo!).DecodeSignature(Provider, ctx);

        var genInst = Assert.IsType<GenericInstanceSignature>(sig.ReturnType);
        Assert.Equal(TypeSignatureKind.TypeDefinition, genInst.GenericType.Kind);
        Assert.Same(intSig, Assert.Single(genInst.Arguments));
    }
}
