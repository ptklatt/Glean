using System.Runtime.CompilerServices;
using System.Text;

namespace Glean.Signatures;

/// <summary>
/// Represents a single dimensional zero based array (e.g., int[]).
/// </summary>
public sealed class SZArraySignature : TypeSignature
{
    /// <summary>
    /// Gets the element type.
    /// </summary>
    public TypeSignature ElementType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SZArraySignature"/> class.
    /// </summary>
    public SZArraySignature(TypeSignature elementType)
    {
        ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
    }

    public override TypeSignatureKind Kind => TypeSignatureKind.SZArray;

    public override bool? IsValueType => false; // Arrays are always reference types

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        return false; // Arrays never match simple type names
    }

    public override bool Equals(TypeSignature? other)
        => (other is SZArraySignature s) && ElementType.Equals(s.ElementType);

    public override int GetHashCode() => HashCode.Combine(Kind, ElementType);

    public override void FormatTo(StringBuilder sb)
    {
        ElementType.FormatTo(sb);
        sb.Append("[]");
    }
}
