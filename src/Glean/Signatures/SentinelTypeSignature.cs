using System.Runtime.CompilerServices;
using System.Text;

namespace Glean.Signatures;

/// <summary>
/// Represents a sentinel marker in vararg signatures.
/// Implemented as a singleton.
/// </summary>
public sealed class SentinelTypeSignature : TypeSignature
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static SentinelTypeSignature Instance { get; } = new();

    private SentinelTypeSignature() { }

    /// <inheritdoc/>
    public override TypeSignatureKind Kind => TypeSignatureKind.Sentinel;

    /// <inheritdoc/>
    public override bool? IsValueType => null;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        return false;
    }

    /// <inheritdoc/>
    public override bool Equals(TypeSignature? other) => other is SentinelTypeSignature;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Accept<TVisitor, TState>(ref TVisitor visitor, TState state)
    {
        visitor.VisitSentinel(this, state);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => (int)Kind;

    /// <inheritdoc/>
    public override void FormatTo(StringBuilder sb)
    {
        sb.Append("...");
    }
}
