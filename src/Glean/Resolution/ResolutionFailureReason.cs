namespace Glean.Resolution;

/// <summary>
/// Provides a coarse reason for why resolution failed.
/// </summary>
public enum ResolutionFailureReason
{
    /// <summary>
    /// No failure (resolution succeeded).
    /// </summary>
    None = 0,

    /// <summary>
    /// The referenced assembly could not be located.
    /// </summary>
    AssemblyNotFound,

    /// <summary>
    /// The referenced module could not be located.
    /// </summary>
    ModuleNotFound,

    /// <summary>
    /// The referenced type could not be found in the resolved scope.
    /// </summary>
    TypeNotFound,

    /// <summary>
    /// A type forwarder was present but could not be followed to a concrete type.
    /// </summary>
    TypeForwarderBroken,

    /// <summary>
    /// Type forwarder chain exceeded the configured maximum depth.
    /// </summary>
    ForwarderChainTooDeep,

    /// <summary>
    /// A cycle was detected while following type forwarders.
    /// </summary>
    ForwarderCycleDetected,

    /// <summary>
    /// A nested type name could not be found within the resolved enclosing type.
    /// </summary>
    NestedTypeNotFound,

    /// <summary>
    /// The resolution scope kind is not supported.
    /// </summary>
    UnsupportedResolutionScope,

    /// <summary>
    /// The parent type for a member could not be resolved.
    /// </summary>
    ParentTypeNotFound,

    /// <summary>
    /// The referenced member could not be found.
    /// </summary>
    MemberNotFound,

    /// <summary>
    /// Multiple candidate members matched the reference.
    /// </summary>
    MemberAmbiguous,

    /// <summary>
    /// The member reference parent kind is not supported.
    /// </summary>
    UnsupportedParentKind,
}

