using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Xunit;

using Glean.Extensions;
using Glean.Providers;
using Glean.Signatures;
using Glean.Tests.Utility;

namespace Glean.Tests.Extensions;

public class MethodDefinitionExtensionsTests
{
    // == Visibility checks ===================================================

    [Fact]
    public void VisibilityChecks_InstanceMethods_ReportExpectedFlags()
    {
        using var metadata = MethodMetadataScope.Open(typeof(MethodDefinitionExtensionsTests).Assembly);
        var reader = metadata.Reader;

        var publicMethod = GetMethodDefinition(
            reader,
            typeof(MethodVisibilityFixture).GetMethod(
                nameof(MethodVisibilityFixture.PublicMethod),
                BindingFlags.Instance | BindingFlags.Public)!);
        var privateMethod = GetMethodDefinition(
            reader,
            typeof(MethodVisibilityFixture).GetMethod(
                "PrivateMethod",
                BindingFlags.Instance | BindingFlags.NonPublic)!);
        var familyMethod = GetMethodDefinition(
            reader,
            typeof(MethodVisibilityFixture).GetMethod(
                "FamilyMethod",
                BindingFlags.Instance | BindingFlags.NonPublic)!);
        var internalMethod = GetMethodDefinition(
            reader,
            typeof(MethodVisibilityFixture).GetMethod(
                nameof(MethodVisibilityFixture.InternalMethod),
                BindingFlags.Instance | BindingFlags.NonPublic)!);

        Assert.True(publicMethod.IsPublic());
        Assert.False(publicMethod.IsPrivate());
        Assert.False(publicMethod.IsFamily());
        Assert.False(publicMethod.IsInternal());

        Assert.True(privateMethod.IsPrivate());
        Assert.False(privateMethod.IsPublic());

        Assert.True(familyMethod.IsFamily());
        Assert.False(familyMethod.IsPublic());

        Assert.True(internalMethod.IsInternal());
        Assert.False(internalMethod.IsPublic());
    }

    // == Method kind checks ==================================================

    [Fact]
    public void MethodKindChecks_ReportExpectedFlags()
    {
        using var metadata = MethodMetadataScope.Open(typeof(MethodDefinitionExtensionsTests).Assembly);
        var reader = metadata.Reader;

        var staticMethod = GetMethodDefinition(
            reader,
            typeof(MethodKindFixture).GetMethod(
                nameof(MethodKindFixture.StaticMethod),
                BindingFlags.Static | BindingFlags.Public)!);
        var abstractMethod = GetMethodDefinition(
            reader,
            typeof(MethodKindBaseFixture).GetMethod(
                nameof(MethodKindBaseFixture.AbstractMethod),
                BindingFlags.Instance | BindingFlags.Public)!);
        var virtualMethod = GetMethodDefinition(
            reader,
            typeof(MethodKindFixture).GetMethod(
                nameof(MethodKindFixture.NewSlotVirtualMethod),
                BindingFlags.Instance | BindingFlags.Public)!);
        var finalMethod = GetMethodDefinition(
            reader,
            typeof(MethodKindDerivedFixture).GetMethod(
                nameof(MethodKindBaseFixture.OverridableMethod),
                BindingFlags.Instance | BindingFlags.Public)!);
        var genericMethod = GetMethodDefinition(
            reader,
            typeof(MethodKindFixture).GetMethod(
                nameof(MethodKindFixture.GenericMethod),
                BindingFlags.Instance | BindingFlags.Public)!);
        var plainMethod = GetMethodDefinition(
            reader,
            typeof(MethodKindFixture).GetMethod(
                nameof(MethodKindFixture.PlainMethod),
                BindingFlags.Instance | BindingFlags.Public)!);

        Assert.True(staticMethod.IsStatic());
        Assert.False(staticMethod.IsVirtual());

        Assert.True(abstractMethod.IsAbstract());
        Assert.True(abstractMethod.IsVirtual());

        Assert.True(virtualMethod.IsVirtual());
        Assert.True(virtualMethod.IsNewSlot());
        Assert.False(virtualMethod.IsFinal());

        Assert.True(finalMethod.IsVirtual());
        Assert.True(finalMethod.IsFinal());
        Assert.False(finalMethod.IsNewSlot());

        Assert.True(genericMethod.IsGenericMethodDefinition());
        Assert.True(plainMethod.IsHideBySig());
    }

    // == Special method checks ===============================================

    [Fact]
    public void SpecialMethodChecks_ConstructorsAndAccessors_ReportExpectedFlags()
    {
        using var metadata = MethodMetadataScope.Open(typeof(MethodDefinitionExtensionsTests).Assembly);
        var reader = metadata.Reader;

        var instanceConstructor = GetMethodDefinition(reader, typeof(AccessorFixture).GetConstructor(Type.EmptyTypes)!);
        var staticConstructor = GetMethodDefinition(reader, typeof(AccessorFixture).TypeInitializer!);
        var getter = GetMethodDefinition(
            reader,
            typeof(AccessorFixture).GetProperty(nameof(AccessorFixture.Number))!.GetMethod!);

        Assert.True(instanceConstructor.IsRTSpecialName());
        Assert.True(instanceConstructor.IsConstructor(reader));
        Assert.False(instanceConstructor.IsStaticConstructor(reader));

        Assert.True(staticConstructor.IsRTSpecialName());
        Assert.True(staticConstructor.IsConstructor(reader));
        Assert.True(staticConstructor.IsStatic());
        Assert.True(staticConstructor.IsStaticConstructor(reader));

        Assert.True(getter.IsSpecialName());
        Assert.False(getter.IsRTSpecialName());
        Assert.False(getter.IsConstructor(reader));
    }

    // == Implementation flags ================================================

    [Fact]
    public void ImplementationFlagChecks_DynamicFixture_ReportExpectedFlags()
    {
        using var metadata = CreateImplementationFlagMetadata();
        var reader = metadata.Reader;
        var typeDef = GetTypeDefinition(reader, "Glean.Tests.Dynamic", "ImplementationFlagFixture");

        var aggressiveInlining = GetMethodDefinition(reader, typeDef, "AggressiveInliningMethod");
        var noInlining = GetMethodDefinition(reader, typeDef, "NoInliningMethod");
        var synchronized = GetMethodDefinition(reader, typeDef, "SynchronizedMethod");
        var internalCall = GetMethodDefinition(reader, typeDef, "InternalCallMethod");
        var forwardRef = GetMethodDefinition(reader, typeDef, "ForwardRefMethod");
        var pInvoke = GetMethodDefinition(reader, typeDef, "Beep");
        var plainMethod = GetMethodDefinition(reader, typeDef, "PlainMethod");

        Assert.True(aggressiveInlining.IsAggressiveInlining());
        Assert.False(aggressiveInlining.IsNoInlining());

        Assert.True(noInlining.IsNoInlining());
        Assert.False(noInlining.IsAggressiveInlining());

        Assert.True(synchronized.IsSynchronized());
        Assert.True(internalCall.IsInternalCall());
        Assert.True(forwardRef.IsForwardRef());
        Assert.True(pInvoke.IsPInvoke());

        Assert.False(plainMethod.IsAggressiveInlining());
        Assert.False(plainMethod.IsNoInlining());
        Assert.False(plainMethod.IsSynchronized());
        Assert.False(plainMethod.IsInternalCall());
        Assert.False(plainMethod.IsForwardRef());
        Assert.False(plainMethod.IsPInvoke());
    }

    // == Identity checks =====================================================

    [Fact]
    public void IdentityAndSignatureChecks_ReportExpectedValues()
    {
        using var metadata = MethodMetadataScope.Open(typeof(MethodDefinitionExtensionsTests).Assembly);
        var reader = metadata.Reader;
        var method = GetMethodDefinition(
            reader,
            typeof(AccessorFixture).GetMethod(
                nameof(AccessorFixture.SignatureMethod),
                BindingFlags.Instance | BindingFlags.Public)!);

        Assert.True(method.NameIs(reader, nameof(AccessorFixture.SignatureMethod)));
        Assert.False(method.NameIs(reader, "MissingMethod"));

        var signature = method.DecodeRawSignature(reader);
        var returnType = Assert.IsType<PrimitiveTypeSignature>(signature.ReturnType);
        var firstParameter = Assert.IsType<PrimitiveTypeSignature>(signature.ParameterTypes[0]);
        var secondParameter = Assert.IsType<GenericMethodParameterSignature>(signature.ParameterTypes[1]);

        Assert.Equal(1, signature.GenericParameterCount);
        Assert.Equal(PrimitiveTypeCode.Int32, returnType.TypeCode);
        Assert.Equal(PrimitiveTypeCode.Int32, firstParameter.TypeCode);
        Assert.Equal(0, secondParameter.Index);
    }

    // == Metadata access =====================================================

    [Fact]
    public void MetadataAccess_PropertyAndEventAccessors_ReturnAssociatedHandles()
    {
        using var metadata = MethodMetadataScope.Open(typeof(MethodDefinitionExtensionsTests).Assembly);
        var reader = metadata.Reader;

        var property = typeof(AccessorFixture).GetProperty(nameof(AccessorFixture.Number))!;
        var eventInfo = typeof(AccessorFixture).GetEvent(nameof(AccessorFixture.Changed))!;
        var plainMethodInfo = typeof(AccessorFixture).GetMethod(
            nameof(AccessorFixture.PlainMethod),
            BindingFlags.Instance | BindingFlags.Public)!;

        var getterHandle = MetadataTokens.MethodDefinitionHandle(property.GetMethod!.MetadataToken);
        var setterHandle = MetadataTokens.MethodDefinitionHandle(property.SetMethod!.MetadataToken);
        var addHandle = MetadataTokens.MethodDefinitionHandle(eventInfo.AddMethod!.MetadataToken);
        var removeHandle = MetadataTokens.MethodDefinitionHandle(eventInfo.RemoveMethod!.MetadataToken);
        var plainHandle = MetadataTokens.MethodDefinitionHandle(plainMethodInfo.MetadataToken);

        var getter = reader.GetMethodDefinition(getterHandle);
        var setter = reader.GetMethodDefinition(setterHandle);
        var addMethod = reader.GetMethodDefinition(addHandle);
        var removeMethod = reader.GetMethodDefinition(removeHandle);
        var plainMethod = reader.GetMethodDefinition(plainHandle);

        Assert.True(getter.TryGetAssociatedProperty(reader, getterHandle, out var getterPropertyHandle));
        Assert.True(setter.TryGetAssociatedProperty(reader, setterHandle, out var setterPropertyHandle));
        Assert.Equal(nameof(AccessorFixture.Number), reader.GetString(reader.GetPropertyDefinition(getterPropertyHandle).Name));
        Assert.Equal(getterPropertyHandle, setterPropertyHandle);
        Assert.Equal(getterPropertyHandle, getter.GetAssociatedProperty(reader, getterHandle));

        Assert.True(addMethod.TryGetAssociatedEvent(reader, addHandle, out var addEventHandle));
        Assert.True(removeMethod.TryGetAssociatedEvent(reader, removeHandle, out var removeEventHandle));
        Assert.Equal(nameof(AccessorFixture.Changed), reader.GetString(reader.GetEventDefinition(addEventHandle).Name));
        Assert.Equal(addEventHandle, removeEventHandle);
        Assert.Equal(addEventHandle, addMethod.GetAssociatedEvent(reader, addHandle));

        Assert.False(plainMethod.TryGetAssociatedProperty(reader, plainHandle, out var plainPropertyHandle));
        Assert.True(plainPropertyHandle.IsNil);
        Assert.True(plainMethod.GetAssociatedProperty(reader, plainHandle).IsNil);

        Assert.False(plainMethod.TryGetAssociatedEvent(reader, plainHandle, out var plainEventHandle));
        Assert.True(plainEventHandle.IsNil);
        Assert.True(plainMethod.GetAssociatedEvent(reader, plainHandle).IsNil);
    }

    // == IL body access ======================================================

    [Fact]
    public void ILBodyAccessors_MethodWithBody_ReturnExpectedData()
    {
        using var metadata = MethodMetadataScope.Open(typeof(MethodDefinitionExtensionsTests).Assembly);
        var reader = metadata.Reader;
        var method = GetMethodDefinition(
            reader,
            typeof(AccessorFixture).GetMethod(
                nameof(AccessorFixture.BodyWithLocalsAndException),
                BindingFlags.Instance | BindingFlags.Public)!);

        var body = method.GetMethodBody(metadata.PEReader);
        var ilBytes = method.DecodeILBytes(metadata.PEReader);
        var ilSpan = method.GetILSpan(metadata.PEReader);
        var localSignature = method.GetLocalSignature(metadata.PEReader);
        var localTypes = method.DecodeLocalVariableTypes(
            metadata.PEReader,
            reader,
            SignatureTypeProvider.Instance,
            SignatureDecodeContext.Empty);
        var exceptionRegions = method.DecodeExceptionRegions(metadata.PEReader);
        var bodyInfo = method.DecodeMethodBodyInfo(metadata.PEReader);

        Assert.NotNull(body);
        Assert.NotEmpty(ilBytes);
        Assert.Equal(ilBytes, ilSpan.ToArray());
        Assert.True(method.HasMethodBody());
        Assert.True(method.GetMaxStack(metadata.PEReader) > 0);
        Assert.True(method.GetLocalVariablesInitialized(metadata.PEReader));
        Assert.False(localSignature.IsNil);
        Assert.Contains(localTypes, type => type is PrimitiveTypeSignature primitive &&
            (primitive.TypeCode == PrimitiveTypeCode.Int32));
        Assert.NotEmpty(exceptionRegions);

        Assert.True(bodyInfo.HasValue);
        Assert.Equal(ilBytes, bodyInfo.Value.ILBytes);
        Assert.Equal(ilBytes.Length, bodyInfo.Value.ILSize);
        Assert.Equal(method.GetMaxStack(metadata.PEReader), bodyInfo.Value.MaxStack);
        Assert.True(bodyInfo.Value.LocalsInitialized);
        Assert.Equal(localSignature, bodyInfo.Value.LocalSignature);
        Assert.Equal(exceptionRegions.Length, bodyInfo.Value.ExceptionRegionCount);
        Assert.True(bodyInfo.Value.HasLocals);
        Assert.True(bodyInfo.Value.HasExceptionHandlers);
    }

    [Fact]
    public void ILBodyAccessors_AbstractMethod_ReturnEmptyOrDefaultValues()
    {
        using var metadata = MethodMetadataScope.Open(typeof(MethodDefinitionExtensionsTests).Assembly);
        var reader = metadata.Reader;
        var method = GetMethodDefinition(
            reader,
            typeof(MethodKindBaseFixture).GetMethod(
                nameof(MethodKindBaseFixture.AbstractMethod),
                BindingFlags.Instance | BindingFlags.Public)!);

        var localTypes = method.DecodeLocalVariableTypes(
            metadata.PEReader,
            reader,
            SignatureTypeProvider.Instance,
            SignatureDecodeContext.Empty);

        Assert.Null(method.GetMethodBody(metadata.PEReader));
        Assert.Empty(method.DecodeILBytes(metadata.PEReader));
        Assert.True(method.GetILSpan(metadata.PEReader).IsEmpty);
        Assert.False(method.HasMethodBody());
        Assert.Equal(0, method.GetMaxStack(metadata.PEReader));
        Assert.False(method.GetLocalVariablesInitialized(metadata.PEReader));
        Assert.True(method.GetLocalSignature(metadata.PEReader).IsNil);
        Assert.Empty(localTypes);
        Assert.Empty(method.DecodeExceptionRegions(metadata.PEReader));
        Assert.Null(method.DecodeMethodBodyInfo(metadata.PEReader));
    }

    private static MethodMetadataScope CreateImplementationFlagMetadata()
    {
        return MethodMetadataScope.Build(
            """
            using System;
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;

            namespace Glean.Tests.Dynamic;

            public sealed class ImplementationFlagFixture
            {
                public void PlainMethod()
                {
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public int AggressiveInliningMethod(int value)
                {
                    return value + 1;
                }

                [MethodImpl(MethodImplOptions.NoInlining)]
                public int NoInliningMethod(int value)
                {
                    return value + 1;
                }

                [MethodImpl(MethodImplOptions.Synchronized)]
                public void SynchronizedMethod()
                {
                }

                [MethodImpl(MethodImplOptions.InternalCall)]
                internal static extern void InternalCallMethod();

                [MethodImpl(MethodImplOptions.ForwardRef)]
                internal static extern void ForwardRefMethod();

                [DllImport("kernel32.dll")]
                public static extern bool Beep(int frequency, int duration);
            }
            """);
    }

    private static MethodDefinition GetMethodDefinition(MetadataReader reader, MethodBase method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return reader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(method.MetadataToken));
    }

    private static TypeDefinition GetTypeDefinition(MetadataReader reader, string ns, string name)
    {
        foreach (var handle in reader.TypeDefinitions)
        {
            var typeDef = reader.GetTypeDefinition(handle);
            if (reader.StringComparer.Equals(typeDef.Namespace, ns) &&
                reader.StringComparer.Equals(typeDef.Name, name))
            {
                return typeDef;
            }
        }

        throw new InvalidOperationException($"Could not locate {ns}.{name} in metadata.");
    }

    private static MethodDefinition GetMethodDefinition(MetadataReader reader, TypeDefinition typeDef, string name)
    {
        return reader.GetMethodDefinition(GetMethodDefinitionHandle(reader, typeDef, name));
    }

    private static MethodDefinitionHandle GetMethodDefinitionHandle(MetadataReader reader, TypeDefinition typeDef, string name)
    {
        foreach (var handle in typeDef.GetMethods())
        {
            var method = reader.GetMethodDefinition(handle);
            if (reader.StringComparer.Equals(method.Name, name))
            {
                return handle;
            }
        }

        throw new InvalidOperationException($"Could not locate method {name}.");
    }

    private sealed class MethodMetadataScope : IDisposable
    {
        private readonly Stream _stream;
        private readonly PEReader _peReader;

        private MethodMetadataScope(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            _stream = stream;
            _peReader = new PEReader(stream);
            Reader = _peReader.GetMetadataReader();
        }

        public MetadataReader Reader { get; }

        public PEReader PEReader => _peReader;

        public static MethodMetadataScope Open(Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            return new MethodMetadataScope(File.OpenRead(assembly.Location));
        }

        public static MethodMetadataScope Build(params string[] sources)
        {
            var builder = new TestAssemblyBuilder();
            foreach (var source in sources)
            {
                builder.WithSource(source);
            }

            return new MethodMetadataScope(builder.BuildPEStream());
        }

        public void Dispose()
        {
            _peReader.Dispose();
            _stream.Dispose();
        }
    }
}

public class MethodVisibilityFixture
{
    public void PublicMethod()
    {
    }

    private void PrivateMethod()
    {
    }

    protected void FamilyMethod()
    {
    }

    internal void InternalMethod()
    {
    }
}

public abstract class MethodKindBaseFixture
{
    public virtual void OverridableMethod()
    {
    }

    public abstract void AbstractMethod();
}

public class MethodKindFixture : MethodKindBaseFixture
{
    public static void StaticMethod()
    {
    }

    public virtual void NewSlotVirtualMethod()
    {
    }

    public T GenericMethod<T>(T value)
    {
        return value;
    }

    public void PlainMethod()
    {
    }

    public override void AbstractMethod()
    {
    }
}

public sealed class MethodKindDerivedFixture : MethodKindFixture
{
    public sealed override void OverridableMethod()
    {
    }
}

public class AccessorFixture
{
    static AccessorFixture()
    {
        CachedValue = 7;
    }

    public AccessorFixture()
    {
        Number = CachedValue;
    }

    public static int CachedValue { get; }

    public int Number { get; set; }

    public event EventHandler? Changed;

    public int PlainMethod()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        return Number;
    }

    public int SignatureMethod<TMethod>(int value, TMethod item)
    {
        _ = item;
        return value;
    }

    public int BodyWithLocalsAndException(int value)
    {
        int local = value + 1;

        try
        {
            return local;
        }
        catch (Exception)
        {
            return local - 1;
        }
    }
}
