using System.ComponentModel;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Extension methods for <see cref="ExportedType"/>.
/// </summary>
/// <remarks>
/// Advanced: Exported type forwarding is a niche scenario needed primarily when
/// scanning multi assembly closures or type forwarder chains.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class ExportedTypeExtensions
{
    private const int MaxForwardingDepth = 16;

    /// <summary>
    /// Tries to get the final forwarded resolution scope by following exported type links.
    /// </summary>
    public static bool TryGetForwardedResolutionScope(
        this ExportedType exportedType,
        MetadataReader reader,
        out EntityHandle finalScope)
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        return TryGetForwardedResolutionScopeCore(exportedType.Implementation, reader, 0, out finalScope);
    }

    private static bool TryGetForwardedResolutionScopeCore(
        EntityHandle scope,
        MetadataReader reader,
        int depth,
        out EntityHandle finalScope)
    {
        if (scope.IsNil || (depth >= MaxForwardingDepth))
        {
            finalScope = default;
            return false;
        }

        if (scope.Kind != HandleKind.ExportedType)
        {
            finalScope = scope;
            return true;
        }

        var nextExportedType = reader.GetExportedType((ExportedTypeHandle)scope);
        return TryGetForwardedResolutionScopeCore(
            nextExportedType.Implementation,
            reader,
            depth + 1,
            out finalScope);
    }
}
