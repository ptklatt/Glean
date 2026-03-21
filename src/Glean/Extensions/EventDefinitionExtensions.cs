using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Extension methods for <see cref="EventDefinition"/>.
/// </summary>
/// <remarks>
/// This extension class is provided for callers working directly with System.Reflection.Metadata structs.
/// </remarks>
public static class EventDefinitionExtensions
{
    // == Flag checks =========================================================

    /// <summary>
    /// Checks if the event has a special name.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSpecialName(this EventDefinition eventDef) => (eventDef.Attributes & EventAttributes.SpecialName) != 0;

    /// <summary>
    /// Checks if the event has RTSpecialName.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRTSpecialName(this EventDefinition eventDef) => (eventDef.Attributes & EventAttributes.RTSpecialName) != 0;

    // == Metadata access =========================================================

    /// <summary>
    /// Gets the event adder method.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodDefinitionHandle GetAdder(this EventDefinition eventDef)
    {
        var accessors = eventDef.GetAccessors();
        return accessors.Adder;
    }

    /// <summary>
    /// Gets the event remover method.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodDefinitionHandle GetRemover(this EventDefinition eventDef)
    {
        var accessors = eventDef.GetAccessors();
        return accessors.Remover;
    }

    /// <summary>
    /// Gets the event raiser method (if any).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodDefinitionHandle GetRaiser(this EventDefinition eventDef)
    {
        var accessors = eventDef.GetAccessors();
        return accessors.Raiser;
    }

    /// <summary>
    /// Checks if the event has an adder.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasAdder(this EventDefinition eventDef)
    {
        var accessors = eventDef.GetAccessors();
        return !accessors.Adder.IsNil;
    }

    /// <summary>
    /// Checks if the event has a remover.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasRemover(this EventDefinition eventDef)
    {
        var accessors = eventDef.GetAccessors();
        return !accessors.Remover.IsNil;
    }

    /// <summary>
    /// Checks if the event has a raiser.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasRaiser(this EventDefinition eventDef)
    {
        var accessors = eventDef.GetAccessors();
        return !accessors.Raiser.IsNil;
    }

    /// <summary>
    /// Checks if the event name matches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NameIs(this EventDefinition eventDef, MetadataReader reader, string name)
    {
        return reader.StringComparer.Equals(eventDef.Name, name);
    }
}
