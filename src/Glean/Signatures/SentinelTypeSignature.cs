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

    public override TypeSignatureKind Kind => TypeSignatureKind.Sentinel;

    public override bool? IsValueType => null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        return false;
    }

    public override bool Equals(TypeSignature? other) => other is SentinelTypeSignature;

    public override int GetHashCode() => (int)Kind;

    public override void FormatTo(StringBuilder sb)
    {
        sb.Append("...");
    }
}
