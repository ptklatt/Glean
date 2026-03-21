using System.Reflection;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// High performance extension methods for attribute flag bit manipulation.
/// </summary>
/// <remarks>
/// These methods use direct bit manipulation instead of Enum.HasFlag() for performance.
/// Enum.HasFlag() boxes the enum value (heap allocation), while these methods use inline bit ops.
/// For most callers, prefer the DefinitionExtensions (e.g., <c>TypeDefinitionExtensions.IsPublic</c>).
/// </remarks>
public static class FlagAttributeExtensions
{
    // == TypeAttributes Extensions ===========================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPublic(this TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.VisibilityMask) == TypeAttributes.Public;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInternal(this TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NotPublic;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNestedPublic(this TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NestedPublic;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNestedPrivate(this TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NestedPrivate;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInterface(this TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.Interface) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAbstract(this TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.Abstract) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSealed(this TypeAttributes attributes)
    {
        return (attributes & TypeAttributes.Sealed) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSealedNonInterface(this TypeAttributes attributes)
    {
        return ((attributes & TypeAttributes.Sealed) != 0) &&
               ((attributes & TypeAttributes.Interface) == 0);
    }

    // == MethodAttributes Extensions =========================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPublic(this MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPrivate(this MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStatic(this MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.Static) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFinal(this MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.Final) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsVirtual(this MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.Virtual) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAbstract(this MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.Abstract) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSpecialName(this MethodAttributes attributes)
    {
        return (attributes & MethodAttributes.SpecialName) != 0;
    }

    // == FieldAttributes Extensions ==========================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPublic(this FieldAttributes attributes)
    {
        return (attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPrivate(this FieldAttributes attributes)
    {
        return (attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Private;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStatic(this FieldAttributes attributes)
    {
        return (attributes & FieldAttributes.Static) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInitOnly(this FieldAttributes attributes)
    {
        return (attributes & FieldAttributes.InitOnly) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLiteral(this FieldAttributes attributes)
    {
        return (attributes & FieldAttributes.Literal) != 0;
    }

    // == PropertyAttributes Extensions =======================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSpecialName(this PropertyAttributes attributes)
    {
        return (attributes & PropertyAttributes.SpecialName) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasDefault(this PropertyAttributes attributes)
    {
        return (attributes & PropertyAttributes.HasDefault) != 0;
    }

    // == EventAttributes Extensions ==========================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSpecialName(this EventAttributes attributes)
    {
        return (attributes & EventAttributes.SpecialName) != 0;
    }

    // == ParameterAttributes Extensions ======================================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsIn(this ParameterAttributes attributes)
    {
        return (attributes & ParameterAttributes.In) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOut(this ParameterAttributes attributes)
    {
        return (attributes & ParameterAttributes.Out) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOptional(this ParameterAttributes attributes)
    {
        return (attributes & ParameterAttributes.Optional) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasDefault(this ParameterAttributes attributes)
    {
        return (attributes & ParameterAttributes.HasDefault) != 0;
    }
}
