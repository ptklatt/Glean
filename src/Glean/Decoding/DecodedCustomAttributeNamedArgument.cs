using System.Reflection.Metadata;

using Glean.Signatures;

namespace Glean.Decoding;

/// <summary>
/// Represents a named argument (field or property) in a decoded custom attribute.
/// </summary>
public readonly struct DecodedCustomAttributeNamedArgument
{
    /// <summary>
    /// Gets the argument kind (field or property).
    /// </summary>
    public CustomAttributeNamedArgumentKind Kind { get; }

    /// <summary>
    /// Gets the name of the field or property.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type of the field or property.
    /// </summary>
    public TypeSignature Type { get; }

    /// <summary>
    /// Gets the value of the argument.
    /// </summary>
    public DecodedCustomAttributeArgument Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecodedCustomAttributeNamedArgument"/> struct.
    /// </summary>
    public DecodedCustomAttributeNamedArgument(
        CustomAttributeNamedArgumentKind kind,
        string name,
        TypeSignature type,
        DecodedCustomAttributeArgument value)
    {
        Kind = kind;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Value = value;
    }
}
