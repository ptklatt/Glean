using System.ComponentModel;
using System.Reflection.Metadata;

using Glean.Signatures;

namespace Glean.Providers;

/// <summary>
/// Resolves enum underlying types for external type references.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public interface IEnumResolver
{
    /// <summary>
    /// Resolves the underlying type of an enum type reference.
    /// </summary>
    /// <param name="typeRef">The type reference to resolve.</param>
    /// <returns>The primitive type code of the enum's underlying type, or null if not resolvable.</returns>
    PrimitiveTypeCode? Resolve(TypeReferenceSignature typeRef);
}
