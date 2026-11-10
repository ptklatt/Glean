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

    /// <inheritdoc/>
    public override TypeSignatureKind Kind => TypeSignatureKind.ByRef;

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
        => (other is ByRefSignature b) && (ElementType.Equals(b.ElementType));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Accept<TVisitor, TState>(ref TVisitor visitor, TState state)
    {
        visitor.VisitByRef(this, state);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Kind, ElementType);

    /// <inheritdoc/>
    public override void FormatTo(StringBuilder sb)
    {
        sb.Append("ref ");
        ElementType.FormatTo(sb);
    }
}
