using System.Reflection.Metadata;

namespace Glean.Tests.Resolution;

internal static class ResolutionTestHelpers
{
    internal static AssemblyReferenceHandle FindAssemblyReferenceHandle(MetadataReader reader, string assemblyName)
    {
        foreach (var handle in reader.AssemblyReferences)
        {
            var reference = reader.GetAssemblyReference(handle);
            if (string.Equals(reader.GetString(reference.Name), assemblyName, StringComparison.Ordinal))
            {
                return handle;
            }
        }

        throw new InvalidOperationException($"Could not find AssemblyReference '{assemblyName}'.");
    }

    internal static TypeReferenceHandle FindTypeReferenceHandle(MetadataReader reader, string nameSpace, string name)
    {
        foreach (var handle in reader.TypeReferences)
        {
            var reference = reader.GetTypeReference(handle);
            if (string.Equals(reader.GetString(reference.Namespace), nameSpace, StringComparison.Ordinal) &&
                string.Equals(reader.GetString(reference.Name), name, StringComparison.Ordinal))
            {
                return handle;
            }
        }

        throw new InvalidOperationException($"Could not find TypeReference '{nameSpace}.{name}'.");
    }

    internal static MemberReferenceHandle FindMemberReferenceHandle(
        MetadataReader reader,
        string declaringTypeNamespace,
        string declaringTypeName,
        string memberName)
    {
        foreach (var handle in reader.MemberReferences)
        {
            var reference = reader.GetMemberReference(handle);
            if (!string.Equals(reader.GetString(reference.Name), memberName, StringComparison.Ordinal))
            {
                continue;
            }

            if (ParentMatches(reader, reference.Parent, declaringTypeNamespace, declaringTypeName))
            {
                return handle;
            }
        }

        throw new InvalidOperationException(
            $"Could not find MemberReference '{declaringTypeNamespace}.{declaringTypeName}.{memberName}'.");
    }

    private static bool ParentMatches(
        MetadataReader reader,
        EntityHandle parent,
        string declaringTypeNamespace,
        string declaringTypeName)
    {
        if (parent.Kind == HandleKind.TypeReference)
        {
            var typeReference = reader.GetTypeReference((TypeReferenceHandle)parent);
            return string.Equals(reader.GetString(typeReference.Namespace), declaringTypeNamespace, StringComparison.Ordinal) &&
                   string.Equals(reader.GetString(typeReference.Name), declaringTypeName, StringComparison.Ordinal);
        }

        if (parent.Kind == HandleKind.TypeDefinition)
        {
            var typeDefinition = reader.GetTypeDefinition((TypeDefinitionHandle)parent);
            return string.Equals(reader.GetString(typeDefinition.Namespace), declaringTypeNamespace, StringComparison.Ordinal) &&
                   string.Equals(reader.GetString(typeDefinition.Name), declaringTypeName, StringComparison.Ordinal);
        }

        return false;
    }
}
