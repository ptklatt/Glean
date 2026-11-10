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

    /// <inheritdoc/>
    public override TypeSignatureKind Kind => TypeSignatureKind.Pointer;

    /// <inheritdoc/>
    public override bool? IsValueType => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        return false;
    }

    /// <inheritdoc/>
    public override bool Equals(TypeSignature? other)
        => (other is PointerSignature p) && (ElementType.Equals(p.ElementType));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Accept<TVisitor, TState>(ref TVisitor visitor, TState state)
    {
        visitor.VisitPointer(this, state);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Kind, ElementType);

    /// <inheritdoc/>
    public override void FormatTo(StringBuilder sb)
    {
        ElementType.FormatTo(sb);
        sb.Append('*');
    }
}
