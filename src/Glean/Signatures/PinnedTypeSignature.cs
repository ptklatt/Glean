using System.Runtime.CompilerServices;
using System.Text;

namespace Glean.Signatures;

/// <summary>
/// Represents a pinned type (used in local signatures).
/// </summary>
public sealed class PinnedTypeSignature : TypeSignature
{
    /// <summary>
    /// Gets the element type.
    /// </summary>
    public TypeSignature ElementType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PinnedTypeSignature"/> class.
    /// </summary>
    public PinnedTypeSignature(TypeSignature elementType)
    {
        ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
    }

    public override TypeSignatureKind Kind => TypeSignatureKind.Pinned;

    public override bool? IsValueType => ElementType.IsValueType;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        return ElementType.Is(ns, name, scope);
    }

    public override bool Equals(TypeSignature? other)
        => (other is PinnedTypeSignature p) && (ElementType.Equals(p.ElementType));

    public override int GetHashCode() => HashCode.Combine(Kind, ElementType);

    public override void FormatTo(StringBuilder sb)
    {
        sb.Append("pinned ");
        ElementType.FormatTo(sb);
    }
}
