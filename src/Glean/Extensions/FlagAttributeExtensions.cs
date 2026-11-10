using System.Reflection;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Tests metadata attribute flags with direct bit masks.
/// </summary>
/// <remarks>
/// For metadata rows, prefer the corresponding definition extensions, such as
/// <see cref="TypeDefinitionExtensions.IsPublic"/>.
/// </remarks>
public static class FlagAttributeExtensions
{
    // == TypeAttributes Extensions ===========================================

    /// <summary>Checks for public top level visibility.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPublic(this TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public;
    }

    /// <summary>Checks for nonpublic top level visibility.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInternal(this TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NotPublic;
    }

    /// <summary>Checks for public nested visibility.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNestedPublic(this TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NestedPublic;
    }

    /// <summary>Checks for private nested visibility.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNestedPrivate(this TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NestedPrivate;
    }

    /// <summary>Checks whether the <see cref="TypeAttributes.Interface"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInterface(this TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.Interface) != 0;
    }

    /// <summary>Checks whether the <see cref="TypeAttributes.Abstract"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAbstract(this TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.Abstract) != 0;
    }

    /// <summary>Checks whether the <see cref="TypeAttributes.Sealed"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSealed(this TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.Sealed) != 0;
    }

    /// <summary>Checks that the type is sealed and is not an interface.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSealedNonInterface(this TypeAttributes attributes)
    {
        return ((attributes & TypeAttributes.Sealed) != 0) &&
               ((attributes & TypeAttributes.Interface) == 0);
    }

    // == MethodAttributes Extensions =========================================

    /// <summary>Checks for public method access.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPublic(this MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
    }

    /// <summary>Checks for private method access.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPrivate(this MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private;
    }

    /// <summary>Checks whether the <see cref="MethodAttributes.Static"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStatic(this MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.Static) != 0;
    }

    /// <summary>Checks whether the <see cref="MethodAttributes.Final"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFinal(this MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.Final) != 0;
    }

    /// <summary>Checks whether the <see cref="MethodAttributes.Virtual"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsVirtual(this MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.Virtual) != 0;
    }

    /// <summary>Checks whether the <see cref="MethodAttributes.Abstract"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAbstract(this MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.Abstract) != 0;
    }

    /// <summary>Checks whether the <see cref="MethodAttributes.SpecialName"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSpecialName(this MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.SpecialName) != 0;
    }

    // == FieldAttributes Extensions ==========================================

    /// <summary>Checks for public field access.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPublic(this FieldAttributes attributes)
    {
        return (attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
    }

    /// <summary>Checks for private field access.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPrivate(this FieldAttributes attributes)
    {
        return (attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Private;
    }

    /// <summary>Checks whether the <see cref="FieldAttributes.Static"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStatic(this FieldAttributes attributes)
    {
        return (attributes & FieldAttributes.Static) != 0;
    }

    /// <summary>Checks whether the <see cref="FieldAttributes.InitOnly"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInitOnly(this FieldAttributes attributes)
    {
        return (attributes & FieldAttributes.InitOnly) != 0;
    }

    /// <summary>Checks whether the <see cref="FieldAttributes.Literal"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLiteral(this FieldAttributes attributes)
    {
        return (attributes & FieldAttributes.Literal) != 0;
    }

    // == PropertyAttributes Extensions =======================================

    /// <summary>Checks whether the <see cref="PropertyAttributes.SpecialName"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSpecialName(this PropertyAttributes attributes)
    {
        return (attributes & PropertyAttributes.SpecialName) != 0;
    }

    /// <summary>Checks whether the <see cref="PropertyAttributes.HasDefault"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasDefault(this PropertyAttributes attributes)
    {
        return (attributes & PropertyAttributes.HasDefault) != 0;
    }

    // == EventAttributes Extensions ==========================================

    /// <summary>Checks whether the <see cref="EventAttributes.SpecialName"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSpecialName(this EventAttributes attributes)
    {
        return (attributes & EventAttributes.SpecialName) != 0;
    }

    // == ParameterAttributes Extensions ======================================

    /// <summary>Checks whether the <see cref="ParameterAttributes.In"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsIn(this ParameterAttributes attributes)
    {
        return (attributes & ParameterAttributes.In) != 0;
    }

    /// <summary>Checks whether the <see cref="ParameterAttributes.Out"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOut(this ParameterAttributes attributes)
    {
        return (attributes & ParameterAttributes.Out) != 0;
    }

    /// <summary>Checks whether the <see cref="ParameterAttributes.Optional"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOptional(this ParameterAttributes attributes)
    {
        return (attributes & ParameterAttributes.Optional) != 0;
    }

    /// <summary>Checks whether the <see cref="ParameterAttributes.HasDefault"/> flag is set.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasDefault(this ParameterAttributes attributes)
    {
        return (attributes & ParameterAttributes.HasDefault) != 0;
    }
}
