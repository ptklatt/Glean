using System.Reflection.Metadata;

using Xunit;

using Glean.Extensions;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class FieldDefinitionExtensionsTests
{
    private const string FieldDefinitionSource = """
        using System;
        using System.Runtime.InteropServices;

        namespace Glean.Tests.Extensions;

        internal class FieldVisibilityFixture
        {
            public int PublicField;
            private int PrivateField;
            protected int FamilyField;
            internal int InternalField;
        }

        internal class FieldKindFixture
        {
            public static readonly int StaticInitOnlyField = 7;

            [NonSerialized]
            public int NotSerializedField;

            public const int LiteralField = 42;

            [MarshalAs(UnmanagedType.LPWStr)]
            public string MarshaledField;

            public string PlainField;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct FieldOffsetFixture
        {
            [FieldOffset(4)]
            public int OffsetField;
        }

        internal enum SpecialNameEnumFixture
        {
            Value = 1
        }
        """;

    // == Visibility checks ===================================================

    [Fact]
    public void VisibilityChecks_ReportExpectedFlags()
    {
        using var metadata = TestUtility.BuildMetadata(FieldDefinitionSource);

        var publicField = GetFieldDefinition(metadata, "Glean.Tests.Extensions", "FieldVisibilityFixture", "PublicField");
        var privateField = GetFieldDefinition(metadata, "Glean.Tests.Extensions", "FieldVisibilityFixture", "PrivateField");
        var familyField = GetFieldDefinition(metadata, "Glean.Tests.Extensions", "FieldVisibilityFixture", "FamilyField");
        var internalField = GetFieldDefinition(metadata, "Glean.Tests.Extensions", "FieldVisibilityFixture", "InternalField");

        Assert.True(publicField.IsPublic());
        Assert.False(publicField.IsPrivate());
        Assert.False(publicField.IsFamily());
        Assert.False(publicField.IsInternal());

        Assert.True(privateField.IsPrivate());
        Assert.False(privateField.IsPublic());

        Assert.True(familyField.IsFamily());
        Assert.False(familyField.IsPublic());

        Assert.True(internalField.IsInternal());
        Assert.False(internalField.IsPublic());
    }

    // == Field kind checks ===================================================

    [Fact]
    public void FieldKindChecks_ReportExpectedFlags()
    {
        using var metadata = TestUtility.BuildMetadata(FieldDefinitionSource);

        var staticInitOnlyField = GetFieldDefinition(metadata, "Glean.Tests.Extensions", "FieldKindFixture", "StaticInitOnlyField");
        var notSerializedField = GetFieldDefinition(metadata, "Glean.Tests.Extensions", "FieldKindFixture", "NotSerializedField");
        var literalField = GetFieldDefinition(metadata, "Glean.Tests.Extensions", "FieldKindFixture", "LiteralField");
        var specialNameField = GetFieldDefinition(metadata, "Glean.Tests.Extensions", "SpecialNameEnumFixture", "value__");
        var plainField = GetFieldDefinition(metadata, "Glean.Tests.Extensions", "FieldKindFixture", "PlainField");

        Assert.True(staticInitOnlyField.IsStatic());
        Assert.True(staticInitOnlyField.IsInitOnly());
        Assert.False(staticInitOnlyField.IsLiteral());

        Assert.True(notSerializedField.IsNotSerialized());
        Assert.False(notSerializedField.IsLiteral());

        Assert.True(literalField.IsLiteral());
        Assert.True(literalField.HasDefault());
        Assert.False(literalField.IsInitOnly());

        Assert.True(specialNameField.IsSpecialName());
        Assert.True(specialNameField.NameIs(metadata.Reader, "value__"));

        Assert.False(plainField.IsStatic());
        Assert.False(plainField.IsInitOnly());
        Assert.False(plainField.IsLiteral());
        Assert.False(plainField.IsNotSerialized());
        Assert.False(plainField.IsSpecialName());
        Assert.False(plainField.HasDefault());
    }

    // == Value access ========================================================

    [Fact]
    public void DefaultValueAccessors_LiteralAndPlainFields_ReturnExpectedValues()
    {
        using var metadata = TestUtility.BuildMetadata(FieldDefinitionSource);

        var literalField = GetFieldDefinition(metadata, "Glean.Tests.Extensions", "FieldKindFixture", "LiteralField");
        var plainField = GetFieldDefinition(metadata, "Glean.Tests.Extensions", "FieldKindFixture", "PlainField");

        Assert.Equal(42, literalField.GetDefaultValue(metadata.Reader));
        Assert.Null(plainField.GetDefaultValue(metadata.Reader));

        Assert.True(literalField.TryGetDefaultValue<int>(metadata.Reader, out int value));
        Assert.Equal(42, value);

        Assert.False(literalField.TryGetDefaultValue<bool>(metadata.Reader, out bool wrongType));
        Assert.False(wrongType);

        Assert.False(plainField.TryGetDefaultValue<int>(metadata.Reader, out int missingValue));
        Assert.Equal(0, missingValue);
    }

    // == Metadata access =====================================================

    [Fact]
    public void MetadataAccess_OffsetsAndMarshalingDescriptors_ReportExpectedValues()
    {
        using var metadata = TestUtility.BuildMetadata(FieldDefinitionSource);

        var offsetField = GetFieldDefinition(metadata, "Glean.Tests.Extensions", "FieldOffsetFixture", "OffsetField");
        var marshaledField = GetFieldDefinition(metadata, "Glean.Tests.Extensions", "FieldKindFixture", "MarshaledField");
        var plainField = GetFieldDefinition(metadata, "Glean.Tests.Extensions", "FieldKindFixture", "PlainField");

        Assert.Equal(4, offsetField.GetFieldOffset());
        Assert.Null(plainField.GetFieldOffset());

        Assert.False(marshaledField.GetMarshalingDescriptor().IsNil);
        Assert.True(plainField.GetMarshalingDescriptor().IsNil);
    }

    private static FieldDefinition GetFieldDefinition(
        MetadataScope metadata,
        string ns,
        string typeName,
        string fieldName)
    {
        var typeDefinition = metadata.GetTypeDefinition(ns, typeName);
        foreach (var handle in typeDefinition.GetFields())
        {
            var fieldDefinition = metadata.Reader.GetFieldDefinition(handle);
            if (fieldDefinition.NameIs(metadata.Reader, fieldName))
            {
                return fieldDefinition;
            }
        }

        throw new InvalidOperationException($"Could not locate field {typeName}.{fieldName}.");
    }
}
