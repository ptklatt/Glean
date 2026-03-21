using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Glean.Tests.Utility;

public sealed class MetadataScope : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _ownsStream;
    private readonly PEReader _peReader;

    public MetadataScope(string assemblyPath) : this(File.OpenRead(assemblyPath), ownsStream: true) { }

    public MetadataScope(Stream stream, bool ownsStream = true)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _stream = stream;
        _ownsStream = ownsStream;
        _peReader = new PEReader(stream);
        Reader = _peReader.GetMetadataReader();
    }

    public MetadataReader Reader { get; }

    public TypeDefinitionHandle FindTypeDefinitionHandle(string ns, string name)
    {
        foreach (var handle in Reader.TypeDefinitions)
        {
            var typeDef = Reader.GetTypeDefinition(handle);
            if ((Reader.GetString(typeDef.Namespace) == ns) &&
                (Reader.GetString(typeDef.Name) == name))
            {
                return handle;
            }
        }

        return default;
    }

    public TypeDefinition GetTypeDefinition(string ns, string name)
    {
        var handle = FindTypeDefinitionHandle(ns, name);
        if (handle.IsNil)
        {
            throw new InvalidOperationException($"Could not locate {ns}.{name} in metadata.");
        }

        return Reader.GetTypeDefinition(handle);
    }

    public TypeDefinition GetTypeDefinition(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var handle = MetadataTokens.EntityHandle(type.MetadataToken);
        if (handle.Kind != HandleKind.TypeDefinition)
        {
            throw new InvalidOperationException($"Metadata token {type.MetadataToken} is not a TypeDefinition.");
        }

        return Reader.GetTypeDefinition((TypeDefinitionHandle)handle);
    }

    public TypeDefinition GetNestedTypeDefinition(Type declaringType, string nestedTypeName)
    {
        ArgumentNullException.ThrowIfNull(declaringType);
        ArgumentNullException.ThrowIfNull(nestedTypeName);

        var nestedType = declaringType.GetNestedType(nestedTypeName, BindingFlags.Public | BindingFlags.NonPublic);
        if (nestedType is null)
        {
            throw new InvalidOperationException($"Could not locate nested type {nestedTypeName} on {declaringType.FullName}.");
        }

        return GetTypeDefinition(nestedType);
    }

    public TypeReferenceHandle FindTypeReferenceHandle(string ns, string name)
    {
        foreach (var handle in Reader.TypeReferences)
        {
            var typeRef = Reader.GetTypeReference(handle);
            if ((Reader.GetString(typeRef.Namespace) == ns) &&
                (Reader.GetString(typeRef.Name) == name))
            {
                return handle;
            }
        }

        return default;
    }

    public TypeReference GetTypeReference(string ns, string name)
    {
        var handle = FindTypeReferenceHandle(ns, name);
        if (handle.IsNil)
        {
            throw new InvalidOperationException($"Could not locate {ns}.{name} TypeReference in metadata.");
        }

        return Reader.GetTypeReference(handle);
    }

    public EntityHandle GetBaseTypeHandle(string ns, string name)
    {
        return GetTypeDefinition(ns, name).BaseType;
    }

    public TypeReference GetBaseTypeReference(string ns, string name)
    {
        var baseType = GetBaseTypeHandle(ns, name);
        if (baseType.Kind != HandleKind.TypeReference)
        {
            throw new InvalidOperationException($"Expected {ns}.{name} base type to be a TypeReference but found {baseType.Kind}.");
        }

        return Reader.GetTypeReference((TypeReferenceHandle)baseType);
    }

    public TypeSpecificationHandle GetBaseTypeSpecificationHandle(string ns, string name)
    {
        var baseType = GetBaseTypeHandle(ns, name);
        if (baseType.Kind != HandleKind.TypeSpecification)
        {
            throw new InvalidOperationException($"Expected {ns}.{name} base type to be a TypeSpecification but found {baseType.Kind}.");
        }

        return (TypeSpecificationHandle)baseType;
    }

    public void Dispose()
    {
        _peReader.Dispose();
        if (_ownsStream)
        {
            _stream.Dispose();
        }
    }
}
