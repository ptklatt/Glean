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
        bool canReuseNamespace = ReferenceEquals(reader.UTF8Decoder, MetadataStringDecoder.DefaultUTF8);
        StringHandle previousNamespaceHandle = default;
        string previousNamespace = string.Empty;
        foreach (var typeDefHandle in reader.TypeDefinitions)
        {
            var typeDef = reader.GetTypeDefinition(typeDefHandle);
            if (typeDef.IsNested) { continue; }
            
            var ns = (typeDef.Namespace == previousNamespaceHandle) && canReuseNamespace
                ? previousNamespace : reader.GetString(typeDef.Namespace);
            previousNamespaceHandle = typeDef.Namespace;
            previousNamespace = ns;
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
        bool canReuseNamespace = ReferenceEquals(reader.UTF8Decoder, MetadataStringDecoder.DefaultUTF8);
        StringHandle previousNamespaceHandle = default;
        string previousNamespace = string.Empty;
        foreach (var exportedTypeHandle in reader.ExportedTypes)
        {
            var exportedType = reader.GetExportedType(exportedTypeHandle);
            var ns = (exportedType.Namespace == previousNamespaceHandle) && canReuseNamespace
                ? previousNamespace : reader.GetString(exportedType.Namespace);
            previousNamespaceHandle = exportedType.Namespace;
            previousNamespace = ns;
            var name = reader.GetString(exportedType.Name);
            lookup[(ns, name)] = exportedTypeHandle;
        }

        return lookup;
    }
}
