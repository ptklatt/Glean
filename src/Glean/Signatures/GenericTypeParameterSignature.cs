using System.Runtime.CompilerServices;
using System.Text;

namespace Glean.Signatures;

/// <summary>
/// Represents a generic type parameter (!0, !1, etc.).
/// </summary>
public sealed class GenericTypeParameterSignature : TypeSignature
{
    /// <summary>
    /// Gets the parameter index.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericTypeParameterSignature"/> class.
    /// </summary>
    public GenericTypeParameterSignature(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, 
                "Generic type parameter index must be non-negative.");
        }

        Index = index;
    }

    public override TypeSignatureKind Kind => TypeSignatureKind.GenericTypeParameter;

    public override bool? IsValueType => null; // Cannot determine without context

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        return false;
    }

    public override bool Equals(TypeSignature? other)
        => other is GenericTypeParameterSignature g && g.Index == Index;

    public override int GetHashCode() => HashCode.Combine(Kind, Index);

    public override void FormatTo(StringBuilder sb)
    {
        sb.Append('!');
        sb.Append(Index);
    }
}
