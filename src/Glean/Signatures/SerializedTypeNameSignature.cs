using System.Runtime.CompilerServices;
using System.Text;

namespace Glean.Signatures;

/// <summary>
/// Represents a serialized type name (used in custom attributes).
/// </summary>
public sealed class SerializedTypeNameSignature : TypeSignature
{
    /// <summary>
    /// Gets the serialized type name.
    /// </summary>
    public string SerializedName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SerializedTypeNameSignature"/> class.
    /// </summary>
    public SerializedTypeNameSignature(string serializedName)
    {
        SerializedName = serializedName ?? throw new ArgumentNullException(nameof(serializedName));
    }

    public override TypeSignatureKind Kind => TypeSignatureKind.SerializedTypeName;

    public override bool? IsValueType => null; // Cannot determine from name alone

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        // Simple heuristic: check if serialized name ends with the expected name
        var expectedFullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        return SerializedName.Contains(expectedFullName, StringComparison.Ordinal);
    }

    public override bool Equals(TypeSignature? other)
        => (other is SerializedTypeNameSignature s) &&
           string.Equals(SerializedName, s.SerializedName, StringComparison.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Kind, SerializedName);

    public override void FormatTo(StringBuilder sb)
    {
        sb.Append(SerializedName);
    }
}
