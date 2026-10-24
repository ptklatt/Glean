using System.ComponentModel;

using Glean.Contexts;
using Glean.Resolution;

namespace Glean.Extensions;

/// <summary>
/// Resolution helpers for <see cref="TypeContext"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class TypeContextResolutionExtensions
{
    /// <summary>
    /// Resolves the base type, following cross assembly references when needed.
    /// </summary>
    public static bool TryGetBaseType(
        this TypeContext type,
        AssemblySet assemblySet,
        out TypeContext baseType)
    {
        if (assemblySet is null)
        {
            throw new ArgumentNullException(nameof(assemblySet));
        }

        if (!type.HasBaseType)
        {
            baseType = default;
            return false;
        }

        return ResolutionTypeHelpers.TryResolveTypeHandle(
            type.Reader,
            type.BaseType,
            assemblySet,
            out baseType);
    }

    /// <summary>
    /// Resolves the base type, following cross assembly references when needed, and returns a failure reason on false.
    /// </summary>
    public static bool TryGetBaseType(
        this TypeContext type,
        AssemblySet assemblySet,
        out TypeContext baseType,
        out ResolutionFailureReason reason)
    {
        if (assemblySet == null)
        {
            throw new ArgumentNullException(nameof(assemblySet));
        }

        if (!type.HasBaseType)
        {
            baseType = default;
            reason = ResolutionFailureReason.None;
            return false;
        }

        if (type.TryGetBaseTypeDefinition(out baseType))
        {
            reason = ResolutionFailureReason.None;
            return true;
        }

        if (type.TryGetBaseTypeReference(out var baseTypeReference))
        {
            return baseTypeReference.TryResolve(assemblySet, out baseType, out reason);
        }

        if (type.TryGetBaseTypeSpecification(out var typeSpecificationHandle))
        {
            if (ResolutionTypeHelpers.TryResolveTypeHandle(
                type.Reader,
                typeSpecificationHandle,
                assemblySet,
                out baseType,
                out reason))
            {
                return true;
            }

            return false;
        }

        baseType = default;
        reason = ResolutionFailureReason.UnsupportedParentKind;
        return false;
    }

    /// <summary>
    /// Enumerates the base type chain, resolving across the registered assembly set.
    /// </summary>
    public static BaseTypeChainEnumerator EnumerateBaseTypes(
        this TypeContext type,
        AssemblySet assemblySet)
    {
        if (assemblySet == null)
        {
            throw new ArgumentNullException(nameof(assemblySet));
        }

        return BaseTypeChainEnumerator.Create(type, assemblySet);
    }

    /// <summary>
    /// Enumerates direct interfaces, their parent interfaces, and interfaces inherited from base types.
    /// </summary>
    /// <remarks>
    /// This resolution traversal builds a queue and visited set, deduplicates resolved type
    /// definitions by reader and handle, and terminates cyclic interface or base type graphs.
    /// </remarks>
    public static InterfaceTypeChainEnumerator EnumerateAllInterfaces(
        this TypeContext type,
        AssemblySet assemblySet)
    {
        if (assemblySet == null)
        {
            throw new ArgumentNullException(nameof(assemblySet));
        }

        return InterfaceTypeChainEnumerator.Create(type, assemblySet);
    }

    /// <summary>
    /// Returns true when any resolved base type in the chain matches the requested metadata identity.
    /// </summary>
    public static bool IsSubclassOf(
        this TypeContext type,
        string ns,
        string name,
        AssemblySet assemblySet)
    {
        if (ns == null)
        {
            throw new ArgumentNullException(nameof(ns));
        }

        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (assemblySet == null)
        {
            throw new ArgumentNullException(nameof(assemblySet));
        }

        foreach (var baseType in type.EnumerateBaseTypes(assemblySet))
        {
            if (baseType.Is(ns, name))
            {
                return true;
            }
        }

        return false;
    }
}
