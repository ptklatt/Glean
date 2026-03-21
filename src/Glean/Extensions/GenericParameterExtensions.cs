using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Extension methods for <see cref="GenericParameter"/>.
/// </summary>
/// <remarks>
/// Generic parameter analysis (variance, constraints, constraint signature decoding) is an
/// advanced scenario. Constraint signature methods bridge to the Advanced Signatures hierarchy
/// and may allocate.
/// </remarks>
public static class GenericParameterExtensions
{
    // == Variance flags ======================================================

    /// <summary>
    /// Checks if the generic parameter is covariant (out).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCovariant(this GenericParameter genericParameter)
        => (genericParameter.Attributes & GenericParameterAttributes.VarianceMask) == GenericParameterAttributes.Covariant;

    /// <summary>
    /// Checks if the generic parameter is contravariant (in).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsContravariant(this GenericParameter genericParameter)
        => (genericParameter.Attributes & GenericParameterAttributes.VarianceMask) == GenericParameterAttributes.Contravariant;

    /// <summary>
    /// Checks if the generic parameter is invariant (no variance).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInvariant(this GenericParameter genericParameter)
        => (genericParameter.Attributes & GenericParameterAttributes.VarianceMask) == GenericParameterAttributes.None;

    // == Constraint flags ====================================================

    /// <summary>
    /// Checks if the generic parameter has the reference type constraint (class).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasReferenceTypeConstraint(this GenericParameter genericParameter)
        => (genericParameter.Attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0;

    /// <summary>
    /// Checks if the generic parameter has the value type constraint (struct).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasNotNullableValueTypeConstraint(this GenericParameter genericParameter)
        => (genericParameter.Attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0;

    /// <summary>
    /// Checks if the generic parameter has the default constructor constraint (new()).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasDefaultConstructorConstraint(this GenericParameter genericParameter)
        => (genericParameter.Attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0;

    /// <summary>
    /// Checks if the generic parameter has special constraint flags.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasSpecialConstraint(this GenericParameter genericParameter)
        => (genericParameter.Attributes & GenericParameterAttributes.SpecialConstraintMask) != 0;

    // == Metadata access =====================================================

    /// <summary>
    /// Checks if the generic parameter name matches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NameIs(this GenericParameter genericParameter, MetadataReader reader, string name)
    {
        return reader.StringComparer.Equals(genericParameter.Name, name);
    }

    /// <summary>
    /// Gets raw GenericParamConstraint row handles.
    /// This is a fast tier API that does not decode signatures.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GenericParameterConstraintHandleCollection GetConstraintHandles(this GenericParameter genericParameter)
    {
        return genericParameter.GetConstraints();
    }
}
