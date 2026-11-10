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

    /// <inheritdoc/>
    public override TypeSignatureKind Kind => TypeSignatureKind.Pinned;

    /// <inheritdoc/>
    public override bool? IsValueType => ElementType.IsValueType;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        return ElementType.Is(ns, name, scope);
    }

    /// <inheritdoc/>
    public override bool Equals(TypeSignature? other)
        => (other is PinnedTypeSignature p) && (ElementType.Equals(p.ElementType));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Accept<TVisitor, TState>(ref TVisitor visitor, TState state)
    {
        visitor.VisitPinnedType(this, state);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Kind, ElementType);

    /// <inheritdoc/>
    public override void FormatTo(StringBuilder sb)
    {
        sb.Append("pinned ");
        ElementType.FormatTo(sb);
    }
}
