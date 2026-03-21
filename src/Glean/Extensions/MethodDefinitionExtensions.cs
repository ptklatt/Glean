using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Extension methods for <see cref="MethodDefinition"/>.
/// Primarily fast tier flag checks with additional IL/body helper APIs in companion partials.
/// </summary>
/// <remarks>
/// Allocation notes:
/// - flag/identity checks in this file are allocation free,
/// - companion IL/body helpers include both zero copy (`GetILSpan`) and allocating APIs
///   (`GetILBytes`, local signature/body materialization).
/// </remarks>
public static partial class MethodDefinitionExtensions
{
    // == Visibility checks ===================================================

    /// <summary>
    /// Checks if the method is public.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPublic(this MethodDefinition method)
    {
        return (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public;
    }

    /// <summary>
    /// Checks if the method is private.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPrivate(this MethodDefinition method)
    {
        return (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Private;
    }

    /// <summary>
    /// Checks if the method is family (protected).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFamily(this MethodDefinition method)
    {
        return (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Family;
    }

    /// <summary>
    /// Checks if the method is internal (assembly scoped).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInternal(this MethodDefinition method)
    {
        return (method.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Assembly;
    }

    // == Method kind checks ==================================================

    /// <summary>
    /// Checks if the method is static.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStatic(this MethodDefinition method)
    {
        return (method.Attributes & MethodAttributes.Static) != 0;
    }

    /// <summary>
    /// Checks if the method is virtual.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsVirtual(this MethodDefinition method)
    {
        return (method.Attributes & MethodAttributes.Virtual) != 0;
    }

    /// <summary>
    /// Checks if the method is abstract.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAbstract(this MethodDefinition method)
    {
        return (method.Attributes & MethodAttributes.Abstract) != 0;
    }

    /// <summary>
    /// Checks if the method is final (sealed).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFinal(this MethodDefinition method)
    {
        return (method.Attributes & MethodAttributes.Final) != 0;
    }

    /// <summary>
    /// True if this method has at least one generic type parameter (e.g., <c>void Foo&lt;T&gt;()</c>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsGenericMethodDefinition(this MethodDefinition method)
        => method.GetGenericParameters().Count > 0;

    /// <summary>
    /// Checks if the method hides by signature.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHideBySig(this MethodDefinition method)
    {
        return (method.Attributes & MethodAttributes.HideBySig) != 0;
    }

    /// <summary>
    /// Checks if the method is a new slot (doesn't override).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNewSlot(this MethodDefinition method)
    {
        return (method.Attributes & MethodAttributes.NewSlot) != 0;
    }

    // == Special method checks ===============================================

    /// <summary>
    /// Checks if the method has a special name (e.g., property accessor).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSpecialName(this MethodDefinition method)
    {
        return (method.Attributes & MethodAttributes.SpecialName) != 0;
    }

    /// <summary>
    /// Checks if the method has RTSpecialName (constructor, etc.).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRTSpecialName(this MethodDefinition method)
    {
        return (method.Attributes & MethodAttributes.RTSpecialName) != 0;
    }

    /// <summary>
    /// Checks if the method is a constructor (.ctor or .cctor).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsConstructor(this MethodDefinition method, MetadataReader reader)
    {
        if ((method.Attributes & MethodAttributes.RTSpecialName) == 0) { return false; }

        return reader.StringComparer.Equals(method.Name, ".ctor") ||
               reader.StringComparer.Equals(method.Name, ".cctor");
    }

    /// <summary>
    /// Checks if the method is a static constructor (.cctor).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStaticConstructor(this MethodDefinition method, MetadataReader reader)
    {
        if ((method.Attributes & (MethodAttributes.RTSpecialName | MethodAttributes.Static)) !=
            (MethodAttributes.RTSpecialName | MethodAttributes.Static))
        {
            return false;
        }

        return reader.StringComparer.Equals(method.Name, ".cctor");
    }

    // == Implementation flags ================================================

    /// <summary>
    /// Checks if the method has AggressiveInlining implementation flag.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAggressiveInlining(this MethodDefinition method)
    {
        return (method.ImplAttributes & MethodImplAttributes.AggressiveInlining) != 0;
    }

    /// <summary>
    /// Checks if the method has NoInlining implementation flag.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNoInlining(this MethodDefinition method)
    {
        return (method.ImplAttributes & MethodImplAttributes.NoInlining) != 0;
    }

    /// <summary>
    /// Checks if the method is an internal call.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInternalCall(this MethodDefinition method)
    {
        return (method.ImplAttributes & MethodImplAttributes.InternalCall) != 0;
    }

    /// <summary>
    /// Checks if the method is forward ref.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsForwardRef(this MethodDefinition method)
    {
        return (method.ImplAttributes & MethodImplAttributes.ForwardRef) != 0;
    }

    /// <summary>
    /// Checks if the method is synchronized.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSynchronized(this MethodDefinition method)
    {
        return (method.ImplAttributes & MethodImplAttributes.Synchronized) != 0;
    }

    /// <summary>
    /// Checks if the method is P/Invoke.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPInvoke(this MethodDefinition method)
    {
        return (method.Attributes & MethodAttributes.PinvokeImpl) != 0;
    }

    // == Identity checks =====================================================

    /// <summary>
    /// Checks if the method name matches.
    /// Zero allocation identity check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NameIs(this MethodDefinition method, MetadataReader reader, string name)
    {
        return reader.StringComparer.Equals(method.Name, name);
    }

    // == Metadata access =====================================================

    /// <summary>
    /// Tries to get the property associated with this method (if it's a getter/setter).
    /// Scans only properties of the declaring type (not the entire assembly).
    /// </summary>
    /// <param name="method">The method definition.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <param name="methodHandle">The method handle.</param>
    /// <param name="propertyHandle">The associated property handle on success.</param>
    /// <returns>True if an associated property was found; false otherwise.</returns>
    public static bool TryGetAssociatedProperty(
        this MethodDefinition method,
        MetadataReader reader,
        MethodDefinitionHandle methodHandle,
        out PropertyDefinitionHandle propertyHandle)
    {
        // Get the declaring type to limit search scope
        var declaringTypeHandle = method.GetDeclaringType();
        if (declaringTypeHandle.IsNil)
        {
            propertyHandle = default;
            return false;
        }

        var declaringType = reader.GetTypeDefinition(declaringTypeHandle);

        // Only scan properties of the declaring type
        foreach (var candidatePropertyHandle in declaringType.GetProperties())
        {
            var propertyDef = reader.GetPropertyDefinition(candidatePropertyHandle);
            var accessors = propertyDef.GetAccessors();

            if ((accessors.Getter == methodHandle) || (accessors.Setter == methodHandle))
            {
                propertyHandle = candidatePropertyHandle;
                return true;
            }
        }

        propertyHandle = default;
        return false;
    }

    /// <summary>
    /// Gets the property associated with this method (if it's a getter/setter).
    /// Scans only properties of the declaring type (not the entire assembly).
    /// </summary>
    /// <returns>The property handle if found, default if not a property accessor.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PropertyDefinitionHandle GetAssociatedProperty(
        this MethodDefinition method,
        MetadataReader reader,
        MethodDefinitionHandle methodHandle)
    {
        return method.TryGetAssociatedProperty(reader, methodHandle, out var propertyHandle)
            ? propertyHandle
            : default;
    }

    /// <summary>
    /// Tries to get the event associated with this method (if it's an add/remove/raise accessor).
    /// Scans only events of the declaring type (not the entire assembly).
    /// </summary>
    /// <param name="method">The method definition.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <param name="methodHandle">The method handle.</param>
    /// <param name="eventHandle">The associated event handle on success.</param>
    /// <returns>True if an associated event was found; false otherwise.</returns>
    public static bool TryGetAssociatedEvent(
        this MethodDefinition method,
        MetadataReader reader,
        MethodDefinitionHandle methodHandle,
        out EventDefinitionHandle eventHandle)
    {
        var declaringTypeHandle = method.GetDeclaringType();
        if (declaringTypeHandle.IsNil)
        {
            eventHandle = default;
            return false;
        }

        var declaringType = reader.GetTypeDefinition(declaringTypeHandle);

        foreach (var candidateEventHandle in declaringType.GetEvents())
        {
            var eventDef = reader.GetEventDefinition(candidateEventHandle);
            var accessors = eventDef.GetAccessors();

            if ((accessors.Adder   == methodHandle) ||
                (accessors.Remover == methodHandle) ||
                (accessors.Raiser  == methodHandle))
            {
                eventHandle = candidateEventHandle;
                return true;
            }
        }

        eventHandle = default;
        return false;
    }

    /// <summary>
    /// Gets the event associated with this method (if it's an add/remove/raise accessor).
    /// Scans only events of the declaring type (not the entire assembly).
    /// </summary>
    /// <returns>The event handle if found, default if not an event accessor.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EventDefinitionHandle GetAssociatedEvent(
        this MethodDefinition method,
        MetadataReader reader,
        MethodDefinitionHandle methodHandle)
    {
        return method.TryGetAssociatedEvent(reader, methodHandle, out var eventHandle)
            ? eventHandle
            : default;
    }

    /// <summary>
    /// Decodes the method signature to TypeSignature for advanced analysis.
    /// Uses Signatures infrastructure.
    /// </summary>
    public static MethodSignature<Signatures.TypeSignature> DecodeRawSignature(
        this MethodDefinition method,
        MetadataReader reader)
    {
        var provider = Providers.SignatureTypeProvider.Instance;
        var genericContext = Providers.SignatureDecodeContext.Empty;
        return method.DecodeSignature(provider, genericContext);
    }
}
