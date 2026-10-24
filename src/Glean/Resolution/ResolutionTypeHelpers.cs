using System.Reflection.Metadata;

using Glean.Contexts;
using Glean.Decoding;

namespace Glean.Resolution;

internal static class ResolutionTypeHelpers
{
    internal static bool TryResolveTypeHandle(
        MetadataReader sourceReader,
        EntityHandle handle,
        AssemblySet assemblySet,
        out TypeContext resolvedType)
    {
        return TryResolveTypeHandle(
            sourceReader,
            handle,
            assemblySet,
            out resolvedType,
            out _);
    }

    internal static bool TryResolveTypeHandle(
        MetadataReader sourceReader,
        EntityHandle handle,
        AssemblySet assemblySet,
        out TypeContext resolvedType,
        out ResolutionFailureReason reason)
    {
        if (handle.Kind == HandleKind.TypeDefinition)
        {
            resolvedType = TypeContext.Create(sourceReader, (TypeDefinitionHandle)handle);
            reason = ResolutionFailureReason.None;
            return true;
        }

        if (handle.Kind == HandleKind.TypeReference)
        {
            if (assemblySet.TryResolveType(
                sourceReader,
                (TypeReferenceHandle)handle,
                out var targetReader,
                out var targetHandle,
                out reason))
            {
                resolvedType = TypeContext.Create(targetReader, targetHandle);
                return true;
            }

            resolvedType = default;
            if (reason == ResolutionFailureReason.None)
            {
                reason = ResolutionFailureReason.TypeNotFound;
            }

            return false;
        }

        if (handle.Kind == HandleKind.TypeSpecification)
        {
            if (!TypeSpecificationSignatureDecoder.TryGetGenericTypeDefinitionHandle(
                sourceReader,
                (TypeSpecificationHandle)handle,
                out var genericTypeHandle))
            {
                resolvedType = default;
                reason = ResolutionFailureReason.UnsupportedParentKind;
                return false;
            }

            if (genericTypeHandle.Kind == HandleKind.TypeDefinition)
            {
                resolvedType = TypeContext.Create(sourceReader, (TypeDefinitionHandle)genericTypeHandle);
                reason = ResolutionFailureReason.None;
                return true;
            }

            if (genericTypeHandle.Kind == HandleKind.TypeReference)
            {
                if (assemblySet.TryResolveType(
                    sourceReader,
                    (TypeReferenceHandle)genericTypeHandle,
                    out var targetReader,
                    out var targetHandle,
                    out reason))
                {
                    resolvedType = TypeContext.Create(targetReader, targetHandle);
                    return true;
                }

                resolvedType = default;
                if (reason == ResolutionFailureReason.None)
                {
                    reason = ResolutionFailureReason.TypeNotFound;
                }

                return false;
            }
        }

        resolvedType = default;
        reason = ResolutionFailureReason.UnsupportedParentKind;
        return false;
    }
}
