using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;

using Xunit;

using Glean.Extensions;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class TypeDefinitionExtensionsTests
{
    // == Visibility checks ===================================================

    [Fact]
    public void VisibilityChecks_TopLevelAndNestedTypes_ReportExpectedFlags()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeDefinitionExtensionsTests).Assembly);

        var publicType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "PublicVisibleFixture");
        var internalType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "InternalVisibleFixture");
        var nestedPublic = metadata.GetTypeDefinition(typeof(VisibilityContainerFixture.NestedPublicFixture));
        var nestedPrivate = metadata.GetNestedTypeDefinition(typeof(VisibilityContainerFixture), "NestedPrivateFixture");
        var nestedFamily = metadata.GetNestedTypeDefinition(typeof(VisibilityContainerFixture), "NestedFamilyFixture");
        var nestedAssembly = metadata.GetNestedTypeDefinition(typeof(VisibilityContainerFixture), "NestedAssemblyFixture");

        Assert.True(publicType.IsPublic());
        Assert.False(publicType.IsInternal());
        Assert.False(publicType.IsNested());

        Assert.True(internalType.IsInternal());
        Assert.False(internalType.IsPublic());
        Assert.False(internalType.IsNested());

        Assert.True(nestedPublic.IsNestedPublic());
        Assert.True(nestedPublic.IsNested());

        Assert.True(nestedPrivate.IsNestedPrivate());
        Assert.True(nestedPrivate.IsNested());

        Assert.True(nestedFamily.IsNestedFamily());
        Assert.True(nestedFamily.IsNested());

        Assert.True(nestedAssembly.IsNestedAssembly());
        Assert.True(nestedAssembly.IsNested());
    }

    // == Type kind checks ====================================================

    [Fact]
    public void TypeKindChecks_ReportExpectedFlags()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeDefinitionExtensionsTests).Assembly);

        var interfaceType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "InterfaceFixture");
        var abstractType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "AbstractFixture");
        var sealedType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "SealedFixture");
        var staticType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "StaticFixture");
        var genericType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "GenericFixture`1");

        Assert.True(interfaceType.IsInterface());
        Assert.True(interfaceType.IsAbstract());

        Assert.True(abstractType.IsAbstract());
        Assert.False(abstractType.IsSealed());
        Assert.False(abstractType.IsStaticClass());

        Assert.True(sealedType.IsSealed());
        Assert.False(sealedType.IsAbstract());
        Assert.False(sealedType.IsStaticClass());

        Assert.True(staticType.IsAbstract());
        Assert.True(staticType.IsSealed());
        Assert.True(staticType.IsStaticClass());

        Assert.True(genericType.IsGenericTypeDefinition());
    }

    [Fact]
    public void ValueKindChecks_TestAssemblyTypeReferences_ReportExpectedResults()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeDefinitionExtensionsTests).Assembly);

        var structType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "ValueTypeFixture");
        var enumType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "EnumFixture");
        var delegateType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "DelegateFixture");
        var classType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "PublicVisibleFixture");

        Assert.True(structType.IsValueType(metadata.Reader));
        Assert.False(structType.IsEnum(metadata.Reader));
        Assert.False(structType.IsDelegate(metadata.Reader));

        Assert.True(enumType.IsValueType(metadata.Reader));
        Assert.True(enumType.IsEnum(metadata.Reader));

        Assert.True(delegateType.IsDelegate(metadata.Reader));
        Assert.False(delegateType.IsValueType(metadata.Reader));

        Assert.False(classType.IsValueType(metadata.Reader));
        Assert.False(classType.IsEnum(metadata.Reader));
        Assert.False(classType.IsDelegate(metadata.Reader));
    }

    [Fact]
    public void ValueKindChecks_CoreLibTypeDefinitions_ReportExpectedResults()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();

        var int32Type = metadata.GetTypeDefinition("System", "Int32");
        var dayOfWeekType = metadata.GetTypeDefinition("System", "DayOfWeek");
        var actionType = metadata.GetTypeDefinition("System", "Action");
        var valueType = metadata.GetTypeDefinition("System", "ValueType");
        var enumType = metadata.GetTypeDefinition("System", "Enum");

        Assert.True(int32Type.IsValueType(metadata.Reader));
        Assert.True(dayOfWeekType.IsEnum(metadata.Reader));
        Assert.True(actionType.IsDelegate(metadata.Reader));

        Assert.False(valueType.IsValueType(metadata.Reader));
        Assert.False(enumType.IsValueType(metadata.Reader));
    }

    // == Special attributes ==================================================

    [Fact]
    public void SpecialAttributeChecks_ReportExpectedFlags()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeDefinitionExtensionsTests).Assembly);

        var serializableType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "SerializableFixture");
        var importType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "ImportedFixture");
        var sequentialType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "SequentialLayoutFixture");
        var explicitType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "ExplicitLayoutFixture");
        var plainType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "PublicVisibleFixture");

        Assert.True(serializableType.IsSerializable());
        Assert.True(importType.IsImport());
        Assert.True(sequentialType.IsSequentialLayout());
        Assert.True(explicitType.IsExplicitLayout());

        Assert.False(plainType.IsSerializable());
        Assert.False(plainType.IsImport());
        Assert.False(plainType.IsExplicitLayout());
    }

    [Fact]
    public void IsSpecialName_SyntheticSpecialNameType_ReturnsTrue()
    {
        using var provider = CreateMetadataWithSpecialNameType(out var handle);
        var typeDef = provider.GetMetadataReader().GetTypeDefinition(handle);

        Assert.True(typeDef.IsSpecialName());
    }

    // == Identity checks =====================================================

    [Fact]
    public void IdentityChecks_PublicFixture_MatchExpectedIdentity()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeDefinitionExtensionsTests).Assembly);
        var typeDef = metadata.GetTypeDefinition("Glean.Tests.Extensions", "PublicVisibleFixture");
        var internalType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "InternalVisibleFixture");

        Assert.True(typeDef.Is(metadata.Reader, "Glean.Tests.Extensions", "PublicVisibleFixture"));
        Assert.False(typeDef.Is(metadata.Reader, "Glean.Tests.Extensions", "InternalVisibleFixture"));

        Assert.True(typeDef.Is(typeDef.Namespace, typeDef.Name));
        Assert.False(typeDef.Is(typeDef.Namespace, internalType.Name));

        Assert.True(typeDef.NameIs(metadata.Reader, "PublicVisibleFixture"));
        Assert.False(typeDef.NameIs(metadata.Reader, "InternalVisibleFixture"));
        Assert.True(typeDef.NameIs(typeDef.Name));

        Assert.True(typeDef.NamespaceIs(typeDef.Namespace));
        Assert.Equal("Glean.Tests.Extensions.PublicVisibleFixture", typeDef.ToFullNameString(metadata.Reader));
    }

    // == Metadata access =====================================================

    [Fact]
    public void ImplementsInterfaceDirectly_DistinguishesDirectInheritedAndGenericCases()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeDefinitionExtensionsTests).Assembly);

        var directType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "DirectInterfaceImplFixture");
        var indirectType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "IndirectInterfaceImplDerivedFixture");
        var genericType = metadata.GetTypeDefinition("Glean.Tests.Extensions", "GenericDirectInterfaceImplFixture");

        Assert.True(directType.ImplementsInterfaceDirectly(
            metadata.Reader,
            "Glean.Tests.Extensions",
            "IDirectInterfaceFixture"));

        Assert.False(indirectType.ImplementsInterfaceDirectly(
            metadata.Reader,
            "Glean.Tests.Extensions",
            "IDirectInterfaceFixture"));

        Assert.False(genericType.ImplementsInterfaceDirectly(
            metadata.Reader,
            "Glean.Tests.Extensions",
            "IGenericDirectInterfaceFixture`1"));
    }

    [Fact]
    public void BaseTypeHelpers_TypeReferenceBase_ReturnExpectedNameAndSignature()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeDefinitionExtensionsTests).Assembly);
        var typeDef = metadata.GetTypeDefinition("Glean.Tests.Extensions", "PublicVisibleFixture");

        string? baseTypeName = typeDef.GetBaseTypeName(metadata.Reader);
        TypeSignature? baseTypeSignature = typeDef.GetBaseTypeSignature(metadata.Reader);

        Assert.Equal("System.Object", baseTypeName);

        var typeRefSignature = Assert.IsType<TypeReferenceSignature>(baseTypeSignature);
        Assert.True(typeRefSignature.Is("System", "Object"));
    }

    [Fact]
    public void BaseTypeHelpers_TypeDefinitionBase_ReturnExpectedNameAndSignature()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeDefinitionExtensionsTests).Assembly);
        var typeDef = metadata.GetTypeDefinition("Glean.Tests.Extensions", "DerivedFromLocalBaseFixture");

        string? baseTypeName = typeDef.GetBaseTypeName(metadata.Reader);
        TypeSignature? baseTypeSignature = typeDef.GetBaseTypeSignature(metadata.Reader);

        Assert.Equal("Glean.Tests.Extensions.LocalBaseFixture", baseTypeName);

        var typeDefSignature = Assert.IsType<TypeDefinitionSignature>(baseTypeSignature);
        Assert.True(typeDefSignature.Is("Glean.Tests.Extensions", "LocalBaseFixture"));
    }

    [Fact]
    public void BaseTypeHelpers_GenericBaseType_ReturnsDecodedSignature()
    {
        using var metadata = TestUtility.OpenMetadata(typeof(TypeDefinitionExtensionsTests).Assembly);
        var typeDef = metadata.GetTypeDefinition("Glean.Tests.Extensions", "DerivedFromGenericBaseFixture");

        TypeSignature? baseTypeSignature = typeDef.GetBaseTypeSignature(metadata.Reader);

        var genericSignature = Assert.IsType<GenericInstanceSignature>(baseTypeSignature);
        Assert.True(genericSignature.GenericType.Is("System.Collections.Generic", "List`1"));

        var argument = Assert.IsType<PrimitiveTypeSignature>(Assert.Single(genericSignature.Arguments));
        Assert.Equal(PrimitiveTypeCode.Int32, argument.TypeCode);
    }

    [Fact]
    public void BaseTypeHelpers_SystemObject_ReturnNull()
    {
        using var metadata = TestUtility.OpenCoreLibMetadata();
        var objectType = metadata.GetTypeDefinition("System", "Object");

        Assert.Null(objectType.GetBaseTypeName(metadata.Reader));
        Assert.Null(objectType.GetBaseTypeSignature(metadata.Reader));
    }

    private static MetadataReaderProvider CreateMetadataWithSpecialNameType(out TypeDefinitionHandle handle)
    {
        var metadata = CreateSyntheticMetadataBuilder();
        handle = metadata.AddTypeDefinition(
            TypeAttributes.Public | TypeAttributes.SpecialName,
            metadata.GetOrAddString("Glean.Tests.Extensions"),
            metadata.GetOrAddString("SyntheticSpecialNameFixture"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));

        return CreateMetadataReaderProvider(metadata);
    }

    private static MetadataBuilder CreateSyntheticMetadataBuilder()
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(
            0,
            metadata.GetOrAddString("Synthetic.dll"),
            metadata.GetOrAddGuid(Guid.NewGuid()),
            default,
            default);
        metadata.AddAssembly(
            metadata.GetOrAddString("Synthetic"),
            new Version(1, 0, 0, 0),
            default,
            default,
            0,
            0);
        metadata.AddTypeDefinition(
            0,
            default,
            metadata.GetOrAddString("<Module>"),
            default,
            MetadataTokens.FieldDefinitionHandle(1),
            MetadataTokens.MethodDefinitionHandle(1));
        return metadata;
    }

    private static MetadataReaderProvider CreateMetadataReaderProvider(MetadataBuilder metadata)
    {
        var root = new MetadataRootBuilder(metadata);
        var image = new BlobBuilder();
        root.Serialize(image, 0, 0);
        return MetadataReaderProvider.FromMetadataImage(image.ToImmutableArray());
    }
}

public sealed class PublicVisibleFixture
{
}

internal sealed class InternalVisibleFixture
{
}

public class VisibilityContainerFixture
{
    public sealed class NestedPublicFixture
    {
    }

    private sealed class NestedPrivateFixture
    {
    }

    protected sealed class NestedFamilyFixture
    {
    }

    internal sealed class NestedAssemblyFixture
    {
    }
}

public interface InterfaceFixture
{
}

public abstract class AbstractFixture
{
}

public sealed class SealedFixture
{
}

public static class StaticFixture
{
}

public sealed class GenericFixture<T>
{
}

public struct ValueTypeFixture
{
    public int Value;
}

public enum EnumFixture
{
    Value
}

public delegate void DelegateFixture();

[Serializable]
public sealed class SerializableFixture
{
}

[ComImport]
[Guid("90E86C35-8F4D-4D6F-9D1A-8B3CF25A3158")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ImportedFixture
{
}

[StructLayout(LayoutKind.Sequential)]
public sealed class SequentialLayoutFixture
{
    public int Value;
}

[StructLayout(LayoutKind.Explicit)]
public struct ExplicitLayoutFixture
{
    [FieldOffset(0)]
    public int Value;
}

public interface IDirectInterfaceFixture
{
}

public interface IGenericDirectInterfaceFixture<T>
{
}

public sealed class DirectInterfaceImplFixture : IDirectInterfaceFixture
{
}

public class IndirectInterfaceImplBaseFixture : IDirectInterfaceFixture
{
}

public sealed class IndirectInterfaceImplDerivedFixture : IndirectInterfaceImplBaseFixture
{
}

public sealed class GenericDirectInterfaceImplFixture : IGenericDirectInterfaceFixture<int>
{
}

public class LocalBaseFixture
{
}

public sealed class DerivedFromLocalBaseFixture : LocalBaseFixture
{
}

public sealed class DerivedFromGenericBaseFixture : List<int>
{
}
