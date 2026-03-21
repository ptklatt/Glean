using System.Reflection.Metadata;

using Glean.Signatures;

namespace Glean.Resolution;

internal static class CustomAttributeTypeResolver
{
    public static bool TryGetAttributeTypeNameHandles(
        MetadataReader reader,
        EntityHandle constructor,
        out StringHandle ns,
        out StringHandle name)
    {
        bool result;
        if (!TryGetAttributeTypeHandle(reader, constructor, out var typeHandle))
        {
            ns = default;
            name = default;
            result = false;
        }
        else if (typeHandle.Kind == HandleKind.TypeDefinition)
        {
            var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)typeHandle);
            ns = typeDef.Namespace;
            name = typeDef.Name;
            result = true;
        }
        else if (typeHandle.Kind == HandleKind.TypeReference)
        {
            var typeRef = reader.GetTypeReference((TypeReferenceHandle)typeHandle);
            ns = typeRef.Namespace;
            name = typeRef.Name;
            result = true;
        }
        else
        {
            ns = default;
            name = default;
            result = false;
        }

        return result;
    }

    public static TypeSignature GetAttributeType(MetadataReader reader, EntityHandle constructor)
    {
        if (!TryGetAttributeTypeHandle(reader, constructor, out var typeHandle))
        {
            throw new BadImageFormatException($"Unexpected attribute constructor handle kind: {constructor.Kind}");
        }

        if (typeHandle.Kind == HandleKind.TypeDefinition)
        {
            return new TypeDefinitionSignature(reader, (TypeDefinitionHandle)typeHandle);
        }

        if (typeHandle.Kind == HandleKind.TypeReference)
        {
            return new TypeReferenceSignature(reader, (TypeReferenceHandle)typeHandle);
        }

        throw new BadImageFormatException($"Unexpected attribute type handle kind: {typeHandle.Kind}");
    }

    private static bool TryGetAttributeTypeHandle(
        MetadataReader reader,
        EntityHandle constructor,
        out EntityHandle typeHandle)
    {
        bool result;
        if (constructor.Kind == HandleKind.MethodDefinition)
        {
            var methodDef = reader.GetMethodDefinition((MethodDefinitionHandle)constructor);
            typeHandle = methodDef.GetDeclaringType();
            result =  true;
        }
        else if (constructor.Kind == HandleKind.MemberReference)
        {
            var memberRef = reader.GetMemberReference((MemberReferenceHandle)constructor);
            typeHandle = memberRef.Parent;
            result = true;
        }
        else
        {
            typeHandle = default;
            result = false;
        }

        return result;
    }
}
