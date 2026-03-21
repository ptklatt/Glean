using System.Reflection;

using Xunit;

using Glean.Extensions;

namespace Glean.Tests.Extensions;

public class FlagAttributeExtensionsTests
{
    // == Type attributes =====================================================

    [Fact]
    public void TypeAttributeHelpers_ReportExpectedValues()
    {
        TypeAttributes publicAbstractInterface = TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Interface;
        TypeAttributes nestedPrivateSealed = TypeAttributes.NestedPrivate | TypeAttributes.Sealed;
        TypeAttributes internalType = TypeAttributes.NotPublic;

        Assert.True(publicAbstractInterface.IsPublic());
        Assert.False(publicAbstractInterface.IsInternal());
        Assert.False(publicAbstractInterface.IsNestedPublic());
        Assert.True(publicAbstractInterface.IsInterface());
        Assert.True(publicAbstractInterface.IsAbstract());
        Assert.False(publicAbstractInterface.IsSealed());
        Assert.False(publicAbstractInterface.IsSealedNonInterface());

        Assert.True(nestedPrivateSealed.IsNestedPrivate());
        Assert.False(nestedPrivateSealed.IsNestedPublic());
        Assert.True(nestedPrivateSealed.IsSealed());
        Assert.True(nestedPrivateSealed.IsSealedNonInterface());
        Assert.False(nestedPrivateSealed.IsInterface());

        Assert.True(internalType.IsInternal());
        Assert.False(internalType.IsPublic());
    }

    // == Method attributes ===================================================

    [Fact]
    public void MethodAttributeHelpers_ReportExpectedValues()
    {
        MethodAttributes publicStaticSpecial = MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName;
        MethodAttributes privateVirtualFinal = MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.Final;
        MethodAttributes abstractMethod = MethodAttributes.Public | MethodAttributes.Abstract;

        Assert.True(publicStaticSpecial.IsPublic());
        Assert.False(publicStaticSpecial.IsPrivate());
        Assert.True(publicStaticSpecial.IsStatic());
        Assert.False(publicStaticSpecial.IsFinal());
        Assert.False(publicStaticSpecial.IsVirtual());
        Assert.False(publicStaticSpecial.IsAbstract());
        Assert.True(publicStaticSpecial.IsSpecialName());

        Assert.True(privateVirtualFinal.IsPrivate());
        Assert.False(privateVirtualFinal.IsPublic());
        Assert.True(privateVirtualFinal.IsVirtual());
        Assert.True(privateVirtualFinal.IsFinal());
        Assert.False(privateVirtualFinal.IsStatic());

        Assert.True(abstractMethod.IsAbstract());
        Assert.False(abstractMethod.IsSpecialName());
    }

    // == Field attributes ====================================================

    [Fact]
    public void FieldAttributeHelpers_ReportExpectedValues()
    {
        FieldAttributes publicStaticInitOnly = FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly;
        FieldAttributes privateLiteral = FieldAttributes.Private | FieldAttributes.Literal;

        Assert.True(publicStaticInitOnly.IsPublic());
        Assert.False(publicStaticInitOnly.IsPrivate());
        Assert.True(publicStaticInitOnly.IsStatic());
        Assert.True(publicStaticInitOnly.IsInitOnly());
        Assert.False(publicStaticInitOnly.IsLiteral());

        Assert.True(privateLiteral.IsPrivate());
        Assert.False(privateLiteral.IsPublic());
        Assert.False(privateLiteral.IsStatic());
        Assert.False(privateLiteral.IsInitOnly());
        Assert.True(privateLiteral.IsLiteral());
    }

    // == Property, event, and parameter attributes ===========================

    [Fact]
    public void PropertyEventAndParameterAttributeHelpers_ReportExpectedValues()
    {
        PropertyAttributes propertyAttributes = PropertyAttributes.SpecialName | PropertyAttributes.HasDefault;
        EventAttributes eventAttributes = EventAttributes.SpecialName;
        ParameterAttributes parameterAttributes = ParameterAttributes.In |
                                                  ParameterAttributes.Out |
                                                  ParameterAttributes.Optional |
                                                  ParameterAttributes.HasDefault;
        ParameterAttributes plainParameter = 0;

        Assert.True(propertyAttributes.IsSpecialName());
        Assert.True(propertyAttributes.HasDefault());

        Assert.True(eventAttributes.IsSpecialName());

        Assert.True(parameterAttributes.IsIn());
        Assert.True(parameterAttributes.IsOut());
        Assert.True(parameterAttributes.IsOptional());
        Assert.True(parameterAttributes.HasDefault());

        Assert.False(plainParameter.IsIn());
        Assert.False(plainParameter.IsOut());
        Assert.False(plainParameter.IsOptional());
        Assert.False(plainParameter.HasDefault());
    }
}
