using System.Reflection.Metadata;
using Xunit;

using Glean.Extensions;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class ParameterExtensionsTests
{
    private const string ParameterSource = """
        using System.Runtime.InteropServices;

        namespace Glean.Tests.Extensions;

        internal static class ParameterFixture
        {
            internal static void AttributeFlags([In] int inValue, [Out] string outValue)
            {
            }

            internal static void DefaultValues(int number = 42, bool enabled = true, string text = "hello")
            {
            }

            internal static void Marshaled([MarshalAs(UnmanagedType.LPWStr)] string value)
            {
            }

            internal static void Plain(int value)
            {
            }
        }
        """;

    // == Flag checks ========================================================

    [Fact]
    public void FlagChecks_Parameters_ReportExpectedValues()
    {
        using var metadata = TestUtility.BuildMetadata(ParameterSource);

        var inParameter = GetParameter(metadata, "Glean.Tests.Extensions", "ParameterFixture", "AttributeFlags", "inValue");
        var outParameter = GetParameter(metadata, "Glean.Tests.Extensions", "ParameterFixture", "AttributeFlags", "outValue");
        var defaultParameter = GetParameter(metadata, "Glean.Tests.Extensions", "ParameterFixture", "DefaultValues", "number");
        var marshaledParameter = GetParameter(metadata, "Glean.Tests.Extensions", "ParameterFixture", "Marshaled", "value");
        var plainParameter = GetParameter(metadata, "Glean.Tests.Extensions", "ParameterFixture", "Plain", "value");

        Assert.True(inParameter.IsIn());
        Assert.False(inParameter.IsOut());

        Assert.True(outParameter.IsOut());
        Assert.False(outParameter.IsIn());

        Assert.True(defaultParameter.IsOptional());
        Assert.True(defaultParameter.HasDefault());
        Assert.False(defaultParameter.HasFieldMarshal());

        Assert.True(marshaledParameter.HasFieldMarshal());
        Assert.False(marshaledParameter.HasDefault());

        Assert.False(plainParameter.IsIn());
        Assert.False(plainParameter.IsOut());
        Assert.False(plainParameter.IsOptional());
        Assert.False(plainParameter.HasDefault());
        Assert.False(plainParameter.HasFieldMarshal());
    }

    // == Default values =====================================================

    [Fact]
    public void DefaultValueAccessors_DefaultedParameters_ReturnExpectedValues()
    {
        using var metadata = TestUtility.BuildMetadata(ParameterSource);

        var numberParameter = GetParameter(metadata, "Glean.Tests.Extensions", "ParameterFixture", "DefaultValues", "number");
        var enabledParameter = GetParameter(metadata, "Glean.Tests.Extensions", "ParameterFixture", "DefaultValues", "enabled");
        var textParameter = GetParameter(metadata, "Glean.Tests.Extensions", "ParameterFixture", "DefaultValues", "text");
        var plainParameter = GetParameter(metadata, "Glean.Tests.Extensions", "ParameterFixture", "Plain", "value");

        Assert.Equal(42, numberParameter.GetDefaultValue(metadata.Reader));
        Assert.Equal(true, enabledParameter.GetDefaultValue(metadata.Reader));
        Assert.Equal("hello", textParameter.GetDefaultValue(metadata.Reader));
        Assert.Null(plainParameter.GetDefaultValue(metadata.Reader));

        Assert.True(numberParameter.TryGetDefaultValue<int>(metadata.Reader, out int number));
        Assert.Equal(42, number);

        Assert.True(enabledParameter.TryGetDefaultValue<bool>(metadata.Reader, out bool enabled));
        Assert.True(enabled);

        Assert.False(numberParameter.TryGetDefaultValue<bool>(metadata.Reader, out bool wrongType));
        Assert.False(wrongType);

        Assert.False(plainParameter.TryGetDefaultValue<int>(metadata.Reader, out int missingValue));
        Assert.Equal(0, missingValue);
    }

    // == Marshaling =========================================================

    [Fact]
    public void GetMarshalingDescriptor_MarshaledParameter_ReturnsExpectedHandleState()
    {
        using var metadata = TestUtility.BuildMetadata(ParameterSource);

        var marshaledParameter = GetParameter(metadata, "Glean.Tests.Extensions", "ParameterFixture", "Marshaled", "value");
        var plainParameter = GetParameter(metadata, "Glean.Tests.Extensions", "ParameterFixture", "Plain", "value");

        Assert.False(marshaledParameter.GetMarshalingDescriptor().IsNil);
        Assert.True(plainParameter.GetMarshalingDescriptor().IsNil);
    }

    private static Parameter GetParameter(
        MetadataScope metadata,
        string ns,
        string typeName,
        string methodName,
        string parameterName)
    {
        var typeDefinition = metadata.GetTypeDefinition(ns, typeName);
        foreach (var methodHandle in typeDefinition.GetMethods())
        {
            var methodDefinition = metadata.Reader.GetMethodDefinition(methodHandle);
            if (!methodDefinition.NameIs(metadata.Reader, methodName))
            {
                continue;
            }

            foreach (var parameterHandle in methodDefinition.GetParameters())
            {
                var parameter = metadata.Reader.GetParameter(parameterHandle);
                if (metadata.Reader.GetString(parameter.Name) == parameterName)
                {
                    return parameter;
                }
            }
        }

        throw new InvalidOperationException($"Could not locate parameter {typeName}.{methodName}({parameterName}).");
    }
}
