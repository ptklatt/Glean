using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Extension methods for <see cref="ModuleDefinition"/>.
/// </summary>
/// <remarks>
/// Module-level metadata analysis (name, MVID, EnC generation IDs).
/// </remarks>
public static class ModuleDefinitionExtensions
{
    // == Metadata access =====================================================

    /// <summary>
    /// Gets the module name string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetName(this ModuleDefinition module, MetadataReader reader)
    {
        return reader.GetString(module.Name);
    }

    /// <summary>
    /// Gets the module version ID (MVID).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid GetMvid(this ModuleDefinition module, MetadataReader reader)
    {
        return reader.GetGuid(module.Mvid);
    }

    /// <summary>
    /// Gets the generation ID (for EnC).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid GetGenerationId(this ModuleDefinition module, MetadataReader reader)
    {
        return reader.GetGuid(module.GenerationId);
    }

    /// <summary>
    /// Gets the base generation ID (for EnC).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Guid GetBaseGenerationId(this ModuleDefinition module, MetadataReader reader)
    {
        return reader.GetGuid(module.BaseGenerationId);
    }

    /// <summary>
    /// Checks if the module name matches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NameIs(this ModuleDefinition module, MetadataReader reader, string name)
    {
        return reader.StringComparer.Equals(module.Name, name);
    }

    /// <summary>
    /// Checks if this is the manifest module (has assembly definition).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsManifestModule(this ModuleDefinition module, MetadataReader reader)
    {
        return reader.IsAssembly;
    }
}
