using System.Runtime.CompilerServices;
using System.Text;

namespace Glean.Signatures;

/// <summary>
/// Represents an unmanaged pointer type (e.g., int*).
/// </summary>
public sealed class PointerSignature : TypeSignature
{
    /// <summary>
    /// Gets the element type.
    /// </summary>
    public TypeSignature ElementType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PointerSignature"/> class.
    /// </summary>
    public PointerSignature(TypeSignature elementType)
    {
        ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
    }

    public override TypeSignatureKind Kind => TypeSignatureKind.Pointer;

    public override bool? IsValueType => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        return false;
    }

    public override bool Equals(TypeSignature? other)
        => (other is PointerSignature p) && (ElementType.Equals(p.ElementType));

    public override int GetHashCode() => HashCode.Combine(Kind, ElementType);

    public override void FormatTo(StringBuilder sb)
    {
        ElementType.FormatTo(sb);
        sb.Append('*');
    }
}
