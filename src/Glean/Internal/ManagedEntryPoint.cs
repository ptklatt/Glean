using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Glean.Contexts;

namespace Glean.Internal;

internal static class ManagedEntryPoint
{
    internal static bool TryGetMethod(PEReader peReader, MetadataReader reader, out MethodContext method)
    {
        method = default;
        var header = peReader.PEHeaders.CorHeader;
        if ((header == null) || ((header.Flags & CorFlags.NativeEntryPoint) != 0))
        {
            return false;
        }

        EntityHandle handle;
        try
        {
            handle = MetadataTokens.EntityHandle(header.EntryPointTokenOrRelativeVirtualAddress);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (handle.Kind != HandleKind.MethodDefinition)
        {
            return false;
        }

        int rowNumber = MetadataTokens.GetRowNumber(handle);
        if ((rowNumber <= 0) || (rowNumber > reader.MethodDefinitions.Count))
        {
            return false;
        }

        method = MethodContext.Create(reader, MetadataTokens.MethodDefinitionHandle(rowNumber));
        return true;
    }
}
