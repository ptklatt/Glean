namespace Glean.Signatures;

/// <summary>
/// Specifies the kind of a type signature.
/// </summary>
public enum TypeSignatureKind
{
    /// <summary>Primitive type (Boolean, Char, I1, U1, I2, U2, I4, U4, I8, U8, R4, R8, I, U, Object, String).</summary>
    Primitive,

    /// <summary>Type definition from the current module.</summary>
    TypeDefinition,

    /// <summary>Type reference to an external type.</summary>
    TypeReference,

    /// <summary>Generic type instantiation (e.g., List{int}).</summary>
    GenericInstance,

    /// <summary>Single-dimensional zero-based array (e.g., int[]).</summary>
    SZArray,

    /// <summary>Multi-dimensional or non-zero-based array.</summary>
    Array,

    /// <summary>Unmanaged pointer (e.g., int*).</summary>
    Pointer,

    /// <summary>Managed reference (e.g., ref int).</summary>
    ByRef,

    /// <summary>Pinned type (used in local signatures).</summary>
    Pinned,

    /// <summary>Type with custom modifiers (modopt/modreq).</summary>
    Modified,

    /// <summary>Function pointer type.</summary>
    FunctionPointer,

    /// <summary>Generic type parameter (!0, !1, etc.).</summary>
    GenericTypeParameter,

    /// <summary>Generic method parameter (!!0, !!1, etc.).</summary>
    GenericMethodParameter,

    /// <summary>Serialized type name (used in custom attributes).</summary>
    SerializedTypeName,

    /// <summary>Sentinel marker in vararg signatures.</summary>
    Sentinel
}