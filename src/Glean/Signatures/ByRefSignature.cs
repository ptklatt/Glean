using System.Runtime.CompilerServices;
using System.Text;

namespace Glean.Signatures;

/// <summary>
/// Represents a managed reference type (e.g., ref int).
/// </summary>
public sealed class ByRefSignature : TypeSignature
{
    /// <summary>
    /// Gets the element type.
    /// </summary>
    public TypeSignature ElementType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ByRefSignature"/> class.
    /// </summary>
    public ByRefSignature(TypeSignature elementType)
    {
        ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
    }

    public override TypeSignatureKind Kind => TypeSignatureKind.ByRef;

    public override bool? IsValueType => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        return false;
    }

    public override bool Equals(TypeSignature? other)
        => (other is ByRefSignature b) && (ElementType.Equals(b.ElementType));

    public override int GetHashCode() => HashCode.Combine(Kind, ElementType);

    public override void FormatTo(StringBuilder sb)
    {
        sb.Append("ref ");
        ElementType.FormatTo(sb);
    }
}
