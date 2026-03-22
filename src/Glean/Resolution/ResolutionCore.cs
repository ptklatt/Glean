using System.Reflection.Metadata;

namespace Glean.Resolution;

/// <summary>
/// Shared lookup builders used by <see cref="AssemblySet"/>.
/// </summary>
internal static class ResolutionCore
{
    /// <summary>
    /// Builds a (namespace, name) to <see cref="TypeDefinitionHandle"/> lookup for the top level
    /// types in <paramref name="reader"/>. Nested types are excluded because TypeRef resolution
    /// names the enclosing type, not the nested type directly.
    /// </summary>
    internal static Dictionary<(string, string), TypeDefinitionHandle> BuildTypeLookup(MetadataReader reader)
    {
        var lookup = new Dictionary<(string, string), TypeDefinitionHandle>();
        foreach (var typeDefHandle in reader.TypeDefinitions)
        {
            var typeDef = reader.GetTypeDefinition(typeDefHandle);
            if (typeDef.IsNested) { continue; }
            
            var ns = reader.GetString(typeDef.Namespace);
            var name = reader.GetString(typeDef.Name);
            lookup[(ns, name)] = typeDefHandle;
        }

        return lookup;
    }

    /// <summary>
    /// Builds a (namespace, name) to <see cref="ExportedTypeHandle"/> lookup for forwarded types
    /// exported by <paramref name="reader"/>.
    /// </summary>
    internal static Dictionary<(string, string), ExportedTypeHandle> BuildForwarderLookup(MetadataReader reader)
    {
        var lookup = new Dictionary<(string, string), ExportedTypeHandle>();
        foreach (var exportedTypeHandle in reader.ExportedTypes)
        {
            var exportedType = reader.GetExportedType(exportedTypeHandle);
            var ns = reader.GetString(exportedType.Namespace);
            var name = reader.GetString(exportedType.Name);
            lookup[(ns, name)] = exportedTypeHandle;
        }

        return lookup;
    }
}
