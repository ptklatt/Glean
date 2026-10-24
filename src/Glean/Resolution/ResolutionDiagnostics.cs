using System.ComponentModel;

namespace Glean.Resolution;

/// <summary>
/// Human readable descriptions for <see cref="ResolutionFailureReason"/> values.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class ResolutionDiagnostics
{
    /// <summary>
    /// Returns a short actionable message for a resolution failure.
    /// </summary>
    public static string GetActionableMessage(ResolutionFailureReason reason)
    {
        return reason switch
        {
            ResolutionFailureReason.None =>
                "Resolution succeeded; no failure occurred.",

            ResolutionFailureReason.AssemblyNotFound =>
                "The referenced assembly could not be located. Register it with AssemblySet.Add() or supply an IAssemblyReferenceResolver.",

            ResolutionFailureReason.ModuleNotFound =>
                "The referenced module could not be located. Ensure all modules for the assembly are available to the resolver.",

            ResolutionFailureReason.TypeNotFound =>
                "The target type was not found in the resolved scope. It may have been renamed, removed, or moved.",

            ResolutionFailureReason.TypeForwarderBroken =>
                "A type forwarder was present but the forwarded type could not be resolved. Check the forwarded to assembly set.",

            ResolutionFailureReason.ForwarderChainTooDeep =>
                "The type forwarder chain exceeded the maximum depth. This usually means a cycle or an unexpectedly deep forwarding chain.",

            ResolutionFailureReason.ForwarderCycleDetected =>
                "A cycle was detected while following type forwarders. Inspect the forwarding chain for a loop.",

            ResolutionFailureReason.NestedTypeNotFound =>
                "The nested type was not found inside the resolved enclosing type. It may have been renamed or removed.",

            ResolutionFailureReason.UnsupportedResolutionScope =>
                "The TypeReference resolution scope is not supported by this resolver. Only module, assembly, and enclosing type scopes are handled.",

            ResolutionFailureReason.ParentTypeNotFound =>
                "The containing type for the member reference could not be resolved. Resolve the parent type first or inspect why it is missing.",

            ResolutionFailureReason.MemberNotFound =>
                "No matching member was found on the resolved parent type. The member may have been renamed, removed, or had its signature changed.",

            ResolutionFailureReason.MemberAmbiguous =>
                "Multiple candidate members matched the reference. Narrow the search or reuse a MemberResolutionIndex to inspect candidates.",

            ResolutionFailureReason.UnsupportedParentKind =>
                "The member reference parent kind is not supported. Only type definitions, type references, and supported generic type specifications are handled.",

            _ =>
                $"Unknown failure reason ({(int)reason})."
        };
    }
}
