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

    /// <inheritdoc/>
    public override TypeSignatureKind Kind => TypeSignatureKind.SZArray;

    /// <inheritdoc/>
    public override bool? IsValueType => false; // Arrays are always reference types

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        return false; // Arrays never match simple type names
    }

    /// <inheritdoc/>
    public override bool Equals(TypeSignature? other)
        => (other is SZArraySignature s) && ElementType.Equals(s.ElementType);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Accept<TVisitor, TState>(ref TVisitor visitor, TState state)
    {
        visitor.VisitSZArray(this, state);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Kind, ElementType);

    /// <inheritdoc/>
    public override void FormatTo(StringBuilder sb)
    {
        ElementType.FormatTo(sb);
        sb.Append("[]");
    }
}
