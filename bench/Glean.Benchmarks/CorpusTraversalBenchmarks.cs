using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using BenchmarkDotNet.Attributes;

using Glean.Contexts;
using Glean.Providers;

namespace Glean.Benchmarks;

public enum MetadataWorkload
{
    Fields, Properties, Events, Parameters, Generics, Relationships,
    Attributes, References, Accessors, Flags, NameChecks, Names, RichSignatures
}

[MemoryDiagnoser]
[MedianColumn]
[JsonExporterAttribute.Full]
[BenchmarkCategory("Spectrum.Metadata")]
public class CorpusTraversalBenchmarks : IDisposable
{
    private CorpusFixture _corpus = null!;
    private string _namespace = "";
    private string _typeName = "";

    [Params("Fixture", "Linq", "CoreLib")]
    public string Corpus { get; set; } = "Fixture";

    [ParamsAllValues]
    public MetadataWorkload Workload { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _corpus = new CorpusFixture(Corpus);
        var firstType = _corpus.Reader.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(2));
        _namespace = _corpus.Reader.GetString(firstType.Namespace);
        _typeName = _corpus.Reader.GetString(firstType.Name);
        MetadataFixture.Require(Srm() == Glean(), $"{Corpus}/{Workload} equivalence");
        Console.WriteLine($"Corpus={_corpus.Path} SHA256={_corpus.Sha256} Workload={Workload} Checksum={Srm()}");
    }

    [Benchmark(Baseline = true)]
    public long Srm()
    {
        var reader = _corpus.Reader;
        long sum = 0;
        if (Workload == MetadataWorkload.Attributes)
        {
            foreach (var handle in reader.CustomAttributes)
            {
                var attribute = reader.GetCustomAttribute(handle);
                sum += MetadataTokens.GetToken(attribute.Constructor) + (long)MetadataTokens.GetToken(attribute.Parent);
            }
            return sum;
        }
        if (Workload == MetadataWorkload.References)
        {
            foreach (var handle in reader.TypeReferences)
            {
                var reference = reader.GetTypeReference(handle);
                sum += MetadataTokens.GetToken(reference.ResolutionScope) + (long)MetadataTokens.GetHeapOffset(reference.Name);
            }
            foreach (var handle in reader.MemberReferences)
            {
                var reference = reader.GetMemberReference(handle);
                sum += MetadataTokens.GetToken(reference.Parent) + (long)reference.GetKind();
            }
            return sum;
        }
        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(typeHandle);
            switch (Workload)
            {
                case MetadataWorkload.Fields:
                    foreach (var handle in type.GetFields())
                    {
                        sum += MetadataTokens.GetRowNumber(handle) + (int)reader.GetFieldDefinition(handle).Attributes;
                    }
                    break;
                case MetadataWorkload.Properties:
                    foreach (var handle in type.GetProperties())
                    {
                        sum += MetadataTokens.GetRowNumber(handle) + (int)reader.GetPropertyDefinition(handle).Attributes;
                    }
                    break;
                case MetadataWorkload.Events:
                    foreach (var handle in type.GetEvents())
                    {
                        var value = reader.GetEventDefinition(handle);
                        sum += MetadataTokens.GetRowNumber(handle) + (int)value.Attributes + (long)MetadataTokens.GetToken(value.Type);
                    }
                    break;
                case MetadataWorkload.Parameters:
                    foreach (var method in type.GetMethods())
                    {
                        foreach (var handle in reader.GetMethodDefinition(method).GetParameters())
                        {
                            var parameter = reader.GetParameter(handle);
                            sum += MetadataTokens.GetRowNumber(handle) + parameter.SequenceNumber + (int)parameter.Attributes;
                        }
                    }
                    break;
                case MetadataWorkload.Generics:
                    foreach (var handle in type.GetGenericParameters())
                    {
                        sum += ReadGeneric(reader, reader.GetGenericParameter(handle));
                    }
                    foreach (var method in type.GetMethods())
                    {
                        foreach (var handle in reader.GetMethodDefinition(method).GetGenericParameters())
                        {
                            sum += ReadGeneric(reader, reader.GetGenericParameter(handle));
                        }
                    }
                    break;
                case MetadataWorkload.Relationships:
                    foreach (var handle in type.GetNestedTypes())
                    {
                        sum += MetadataTokens.GetRowNumber(handle) + (int)reader.GetTypeDefinition(handle).Attributes;
                    }
                    foreach (var handle in type.GetInterfaceImplementations())
                    {
                        sum += MetadataTokens.GetToken(reader.GetInterfaceImplementation(handle).Interface);
                    }
                    foreach (var handle in type.GetMethodImplementations())
                    {
                        var value = reader.GetMethodImplementation(handle);
                        sum += MetadataTokens.GetToken(value.MethodBody) + (long)MetadataTokens.GetToken(value.MethodDeclaration);
                    }
                    break;
                case MetadataWorkload.Accessors:
                    foreach (var handle in type.GetProperties())
                    {
                        var value = reader.GetPropertyDefinition(handle).GetAccessors();
                        sum += ReadAccessor(reader, value.Getter) + ReadAccessor(reader, value.Setter);
                    }
                    foreach (var handle in type.GetEvents())
                    {
                        var value = reader.GetEventDefinition(handle).GetAccessors();
                        sum += ReadAccessor(reader, value.Adder) + ReadAccessor(reader, value.Remover) + ReadAccessor(reader, value.Raiser);
                    }
                    break;
                case MetadataWorkload.Flags:
                    var visibility = type.Attributes & System.Reflection.TypeAttributes.VisibilityMask;
                    sum += visibility == System.Reflection.TypeAttributes.Public ? 1 : 0;
                    sum += (type.Attributes & System.Reflection.TypeAttributes.Interface) != 0 ? 2 : 0;
                    sum += (type.Attributes & System.Reflection.TypeAttributes.Abstract) != 0 ? 4 : 0;
                    sum += (type.Attributes & System.Reflection.TypeAttributes.Sealed) != 0 ? 8 : 0;
                    foreach (var handle in type.GetMethods())
                    {
                        var flags = reader.GetMethodDefinition(handle).Attributes;
                        var access = flags & System.Reflection.MethodAttributes.MemberAccessMask;
                        sum += access == System.Reflection.MethodAttributes.Public ? 1 : 0;
                        sum += access == System.Reflection.MethodAttributes.Private ? 2 : 0;
                        sum += (flags & System.Reflection.MethodAttributes.Static) != 0 ? 4 : 0;
                        sum += (flags & System.Reflection.MethodAttributes.Virtual) != 0 ? 8 : 0;
                        sum += (flags & System.Reflection.MethodAttributes.Abstract) != 0 ? 16 : 0;
                        sum += (flags & System.Reflection.MethodAttributes.Final) != 0 ? 32 : 0;
                    }
                    break;
                case MetadataWorkload.NameChecks:
                    if (reader.StringComparer.Equals(type.Namespace, _namespace) && reader.StringComparer.Equals(type.Name, _typeName))
                    {
                        sum += MetadataTokens.GetRowNumber(typeHandle);
                    }
                    foreach (var handle in type.GetMethods())
                    {
                        if (reader.StringComparer.Equals(reader.GetMethodDefinition(handle).Name, "Echo"))
                        {
                            sum += MetadataTokens.GetRowNumber(handle);
                        }
                    }
                    break;
                case MetadataWorkload.Names:
                    sum += reader.GetString(type.Name).Length + reader.GetString(type.Namespace).Length;
                    foreach (var handle in type.GetMethods())
                    {
                        sum += reader.GetString(reader.GetMethodDefinition(handle).Name).Length;
                    }
                    break;
                case MetadataWorkload.RichSignatures:
                    foreach (var handle in type.GetMethods())
                    {
                        var value = reader.GetMethodDefinition(handle).DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
                        sum += value.ParameterTypes.Length + (int)value.ReturnType.Kind + value.GenericParameterCount;
                    }
                    foreach (var handle in type.GetFields())
                    {
                        sum += (int)reader.GetFieldDefinition(handle).DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty).Kind;
                    }
                    break;
            }
        }
        return sum;
    }

    [Benchmark]
    public long Glean()
    {
        var reader = _corpus.Reader;
        long sum = 0;
        if (Workload == MetadataWorkload.Attributes)
        {
            foreach (var handle in reader.CustomAttributes)
            {
                var attribute = CustomAttributeContext.Create(reader, handle);
                sum += MetadataTokens.GetToken(attribute.Constructor) + (long)MetadataTokens.GetToken(attribute.Parent);
            }
            return sum;
        }
        if (Workload == MetadataWorkload.References)
        {
            foreach (var handle in reader.TypeReferences)
            {
                var reference = TypeReferenceContext.Create(reader, handle);
                sum += MetadataTokens.GetToken(reference.ResolutionScope) + (long)MetadataTokens.GetHeapOffset(reference.NameHandle);
            }
            foreach (var handle in reader.MemberReferences)
            {
                var reference = MemberReferenceContext.Create(reader, handle);
                sum += MetadataTokens.GetToken(reference.Parent) + (long)reference.Kind;
            }
            return sum;
        }
        foreach (var type in _corpus.Assembly.EnumerateTypes())
        {
            switch (Workload)
            {
                case MetadataWorkload.Fields:
                    foreach (var field in type.EnumerateFields())
                    {
                        sum += MetadataTokens.GetRowNumber(field.Handle) + (int)field.Attributes;
                    }
                    break;
                case MetadataWorkload.Properties:
                    foreach (var property in type.EnumerateProperties())
                    {
                        sum += MetadataTokens.GetRowNumber(property.Handle) + (int)property.Attributes;
                    }
                    break;
                case MetadataWorkload.Events:
                    foreach (var value in type.EnumerateEvents())
                    {
                        sum += MetadataTokens.GetRowNumber(value.Handle) + (int)value.Attributes + (long)MetadataTokens.GetToken(value.EventType);
                    }
                    break;
                case MetadataWorkload.Parameters:
                    foreach (var method in type.EnumerateMethods())
                    {
                        foreach (var parameter in method.EnumerateParameters())
                        {
                            sum += MetadataTokens.GetRowNumber(parameter.Handle) + parameter.SequenceNumber + (int)parameter.Attributes;
                        }
                    }
                    break;
                case MetadataWorkload.Generics:
                    foreach (var parameter in type.EnumerateGenericParameters())
                    {
                        sum += ReadGeneric(reader, parameter.GenericParameter);
                    }
                    foreach (var method in type.EnumerateMethods())
                    {
                        foreach (var parameter in method.EnumerateGenericParameters())
                        {
                            sum += ReadGeneric(reader, parameter.GenericParameter);
                        }
                    }
                    break;
                case MetadataWorkload.Relationships:
                    foreach (var nested in type.EnumerateNestedTypes())
                    {
                        sum += MetadataTokens.GetRowNumber(nested.Handle) + (int)nested.Attributes;
                    }
                    foreach (var handle in type.EnumerateInterfaceTypes())
                    {
                        sum += MetadataTokens.GetToken(handle);
                    }
                    foreach (var value in type.EnumerateMethodImplementations())
                    {
                        sum += MetadataTokens.GetToken(value.MethodBody) + (long)MetadataTokens.GetToken(value.MethodDeclaration);
                    }
                    break;
                case MetadataWorkload.Accessors:
                    foreach (var property in type.EnumerateProperties())
                    {
                        sum += ReadAccessor(property.Getter) + ReadAccessor(property.Setter);
                    }
                    foreach (var value in type.EnumerateEvents())
                    {
                        sum += ReadAccessor(value.AddMethod) + ReadAccessor(value.RemoveMethod) + ReadAccessor(value.RaiseMethod);
                    }
                    break;
                case MetadataWorkload.Flags:
                    sum += type.IsPublic ? 1 : 0;
                    sum += type.IsInterface ? 2 : 0;
                    sum += type.IsAbstract ? 4 : 0;
                    sum += type.IsSealed ? 8 : 0;
                    foreach (var method in type.EnumerateMethods())
                    {
                        sum += method.IsPublic ? 1 : 0;
                        sum += method.IsPrivate ? 2 : 0;
                        sum += method.IsStatic ? 4 : 0;
                        sum += method.IsVirtual ? 8 : 0;
                        sum += method.IsAbstract ? 16 : 0;
                        sum += method.IsFinal ? 32 : 0;
                    }
                    break;
                case MetadataWorkload.NameChecks:
                    if (type.Is(_namespace, _typeName))
                    {
                        sum += MetadataTokens.GetRowNumber(type.Handle);
                    }
                    foreach (var method in type.EnumerateMethods())
                    {
                        if (method.NameIs("Echo"))
                        {
                            sum += MetadataTokens.GetRowNumber(method.Handle);
                        }
                    }
                    break;
                case MetadataWorkload.Names:
                    sum += type.Name.Length + type.Namespace.Length;
                    foreach (var method in type.EnumerateMethods())
                    {
                        sum += method.Name.Length;
                    }
                    break;
                case MetadataWorkload.RichSignatures:
                    foreach (var method in type.EnumerateMethods())
                    {
                        var value = method.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty);
                        sum += value.ParameterTypes.Length + (int)value.ReturnType.Kind + value.GenericParameterCount;
                    }
                    foreach (var field in type.EnumerateFields())
                    {
                        sum += (int)field.Definition.DecodeSignature(SignatureTypeProvider.Instance, SignatureDecodeContext.Empty).Kind;
                    }
                    break;
            }
        }
        return sum;
    }

    private static long ReadGeneric(MetadataReader reader, GenericParameter value)
    {
        long sum = value.Index + (int)value.Attributes;
        foreach (var constraint in value.GetConstraints())
        {
            sum += MetadataTokens.GetToken(reader.GetGenericParameterConstraint(constraint).Type);
        }
        return sum;
    }

    private static long ReadAccessor(MetadataReader reader, MethodDefinitionHandle handle)
        => handle.IsNil ? 0 : MetadataTokens.GetRowNumber(handle) + (int)reader.GetMethodDefinition(handle).Attributes;

    private static long ReadAccessor(MethodContext method)
        => method.IsValid ? MetadataTokens.GetRowNumber(method.Handle) + (int)method.Attributes : 0;

    public void Dispose() => _corpus.Dispose();
}
