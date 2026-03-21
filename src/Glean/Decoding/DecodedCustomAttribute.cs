using System.Collections.Immutable;

using Glean.Signatures;

namespace Glean.Decoding;

/// <summary>
/// Represents a decoded custom attribute.
/// </summary>
public sealed class DecodedCustomAttribute
{
    /// <summary>
    /// Gets the attribute type signature.
    /// </summary>
    public TypeSignature AttributeType { get; }

    /// <summary>
    /// Gets the fixed (positional) arguments.
    /// </summary>
    public ImmutableArray<DecodedCustomAttributeArgument> FixedArguments { get; }

    /// <summary>
    /// Gets the named arguments (fields and properties).
    /// </summary>
    public ImmutableArray<DecodedCustomAttributeNamedArgument> NamedArguments { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecodedCustomAttribute"/> class.
    /// </summary>
    public DecodedCustomAttribute(
        TypeSignature attributeType,
        ImmutableArray<DecodedCustomAttributeArgument> fixedArguments,
        ImmutableArray<DecodedCustomAttributeNamedArgument> namedArguments)
    {
        AttributeType = attributeType ?? throw new ArgumentNullException(nameof(attributeType));
        FixedArguments = fixedArguments;
        NamedArguments = namedArguments;
    }

    // Intentionally no static Decode method here. Use <see cref="CustomAttributeDecoder"/> for decoding entry points.
}
