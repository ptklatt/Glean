using System.Reflection.Metadata;

using Glean.Contexts;
using Glean.Resolution;

namespace Glean.Extensions;

/// <summary>
/// Resolution helpers for <see cref="MemberReferenceContext"/>.
/// </summary>
public static class MemberReferenceResolutionExtensions
{
    /// <summary>
    /// Resolves a member reference across the registered assembly set.
    /// </summary>
    public static bool TryResolve(
        this MemberReferenceContext memberReference,
        AssemblySet assemblySet,
        out MetadataReader targetReader,
        out EntityHandle targetHandle)
    {
        if (assemblySet == null)
        {
            throw new ArgumentNullException(nameof(assemblySet));
        }

        return assemblySet.TryResolveMember(
            memberReference.Reader,
            memberReference.Handle,
            out targetReader,
            out targetHandle);
    }

    /// <summary>
    /// Resolves a member reference across the registered assembly set and returns a failure reason on false.
    /// </summary>
    public static bool TryResolve(
        this MemberReferenceContext memberReference,
        AssemblySet assemblySet,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason)
    {
        if (assemblySet == null)
        {
            throw new ArgumentNullException(nameof(assemblySet));
        }

        return assemblySet.TryResolveMember(
            memberReference.Reader,
            memberReference.Handle,
            out targetReader,
            out targetHandle,
            out reason);
    }

    /// <summary>
    /// Resolves a member reference across the registered assembly set using a warmed member index.
    /// </summary>
    public static bool TryResolve(
        this MemberReferenceContext memberReference,
        AssemblySet assemblySet,
        MemberResolutionIndex index,
        out MetadataReader targetReader,
        out EntityHandle targetHandle)
    {
        if (assemblySet == null)
        {
            throw new ArgumentNullException(nameof(assemblySet));
        }

        if (index == null)
        {
            throw new ArgumentNullException(nameof(index));
        }

        return assemblySet.TryResolveMember(
            memberReference.Reader,
            memberReference.Handle,
            index,
            out targetReader,
            out targetHandle);
    }

    /// <summary>
    /// Resolves a member reference across the registered assembly set using a warmed member index and returns a failure reason on false.
    /// </summary>
    public static bool TryResolve(
        this MemberReferenceContext memberReference,
        AssemblySet assemblySet,
        MemberResolutionIndex index,
        out MetadataReader targetReader,
        out EntityHandle targetHandle,
        out ResolutionFailureReason reason)
    {
        if (assemblySet == null)
        {
            throw new ArgumentNullException(nameof(assemblySet));
        }

        if (index == null)
        {
            throw new ArgumentNullException(nameof(index));
        }

        return assemblySet.TryResolveMember(
            memberReference.Reader,
            memberReference.Handle,
            index,
            out targetReader,
            out targetHandle,
            out reason);
    }
}
