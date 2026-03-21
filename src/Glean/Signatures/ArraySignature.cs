using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;

namespace Glean.Signatures;

/// <summary>
/// Represents a multi dimensional or non zero based array.
/// </summary>
public sealed class ArraySignature : TypeSignature
{
    /// <summary>
    /// Gets the element type.
    /// </summary>
    public TypeSignature ElementType { get; }

    /// <summary>
    /// Gets the array shape (dimensions, sizes, lower bounds).
    /// </summary>
    public ArrayShape Shape { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArraySignature"/> class.
    /// </summary>
    public ArraySignature(TypeSignature elementType, ArrayShape shape)
    {
        ElementType = elementType ?? throw new ArgumentNullException(nameof(elementType));
        Shape = shape;
    }

    public override TypeSignatureKind Kind => TypeSignatureKind.Array;

    public override bool? IsValueType => false; // Arrays are always reference types

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        return false; // Arrays never match simple type names
    }

    public override bool Equals(TypeSignature? other)
    {
        if (other is not ArraySignature a)                          { return false; }
        if (!ElementType.Equals(a.ElementType))                     { return false; }
        if (Shape.Rank != a.Shape.Rank)                             { return false; }
        if (Shape.Sizes.Length != a.Shape.Sizes.Length)             { return false; }
        if (Shape.LowerBounds.Length != a.Shape.LowerBounds.Length) { return false; }
        for (int i = 0; i < Shape.Sizes.Length; i++)
        {
            if (Shape.Sizes[i] != a.Shape.Sizes[i])                 { return false; }
        }
        for (int i = 0; i < Shape.LowerBounds.Length; i++)
        {
            if (Shape.LowerBounds[i] != a.Shape.LowerBounds[i])     { return false; }
        }
        return true;
    }

    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(Kind);
        hc.Add(ElementType);
        hc.Add(Shape.Rank);
        foreach (var s in Shape.Sizes)
        {
            hc.Add(s);
        }

        foreach (var lb in Shape.LowerBounds)
        {
            hc.Add(lb);
        }
        return hc.ToHashCode();
    }

    public override void FormatTo(StringBuilder sb)
    {
        ElementType.FormatTo(sb);
        sb.Append('[');

        int rank = Shape.Rank;
        for (int i = 0; i < rank; i++)
        {
            if (i > 0) { sb.Append(','); }

            // Show lower bound if specified
            if (i < Shape.LowerBounds.Length)
            {
                int lowerBound = Shape.LowerBounds[i];
                if (lowerBound != 0)
                {
                    sb.Append(lowerBound);
                    sb.Append("...");
                }
            }

            // Show size if specified
            if (i < Shape.Sizes.Length)
            {
                int size = Shape.Sizes[i];
                sb.Append(size);
            }
        }

        sb.Append(']');
    }
}
