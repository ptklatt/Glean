using System.Collections.Immutable;

using Glean.Signatures;

namespace Glean.Decoding;

/// <summary>
/// Represents a typed argument in a decoded custom attribute.
 /// </summary>
public readonly struct DecodedCustomAttributeArgument
{
    /// <summary>
    /// Gets the type of the argument.
    /// </summary>
    public TypeSignature Type { get; }

    /// <summary>
    /// Gets the value of the argument.
    /// Can be a primitive value, string, TypeSignature, enum, or ImmutableArray{DecodedCustomAttributeArgument} for arrays.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecodedCustomAttributeArgument"/> struct.
    /// </summary>
    /// <remarks>
    /// Value can be:
    /// - Primitive types (int, bool, byte, etc.)
    /// - String
    /// - Type (represented as TypeSignature)
    /// - Enum value
    /// - ImmutableArray&lt;DecodedCustomAttributeArgument&gt; for arrays
    /// </remarks>
    public DecodedCustomAttributeArgument(TypeSignature type, object? value)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Value = value;
    }

    /// <summary>
    /// Gets whether this argument is an array.
    /// </summary>
    public bool IsArray => Value is ImmutableArray<DecodedCustomAttributeArgument>;

    /// <summary>
    /// Gets the array elements (if this is an array argument).
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the argument is not an array. Check <see cref="IsArray"/> first.</exception>
    public ImmutableArray<DecodedCustomAttributeArgument> GetArrayElements()
    {
        if (Value is ImmutableArray<DecodedCustomAttributeArgument> array)
        {
            return array;
        }
       
        throw new InvalidOperationException(
            $"Cannot get array elements: argument type is '{Type}', but Value is not an array. Check IsArray property before calling GetArrayElements().");
   }
}
