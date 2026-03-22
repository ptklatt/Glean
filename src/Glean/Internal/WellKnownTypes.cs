namespace Glean.Internal;

/// <summary>
/// Namespace and type name constants for well known framework types.
/// Centralizes literals that otherwise scatter across extension methods and context types.
/// </summary>
internal static class WellKnownTypes
{
    // Namespaces
    public const string SystemNs = "System";
    public const string SystemRuntimeCompilerServicesNs = "System.Runtime.CompilerServices";

    // System base types
    public const string ValueType = "ValueType";
    public const string Enum = "Enum";
    public const string Delegate = "Delegate";
    public const string MulticastDelegate = "MulticastDelegate";

    // Attributes
    public const string ObsoleteAttribute = "ObsoleteAttribute";
    public const string CompilerGeneratedAttribute = "CompilerGeneratedAttribute";
    public const string IsReadOnlyAttribute = "IsReadOnlyAttribute";
    public const string NullableAttribute = "NullableAttribute";
    public const string NullableContextAttribute = "NullableContextAttribute";
}
