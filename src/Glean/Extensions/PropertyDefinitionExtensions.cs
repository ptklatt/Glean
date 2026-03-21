using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Extension methods for <see cref="PropertyDefinition"/>.
/// </summary>
public static class PropertyDefinitionExtensions
{
    // == Flag checks =============================================================

    /// <summary>
    /// Checks if the property has a special name.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSpecialName(this PropertyDefinition property)
        => (property.Attributes & PropertyAttributes.SpecialName) != 0;

    /// <summary>
    /// Checks if the property has RTSpecialName.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsRTSpecialName(this PropertyDefinition property)
        => (property.Attributes & PropertyAttributes.RTSpecialName) != 0;

    /// <summary>
    /// Checks if the property has a default value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasDefault(this PropertyDefinition property)
        => (property.Attributes & PropertyAttributes.HasDefault) != 0;

    // == Metadata access =============================================================

    /// <summary>
    /// Gets the property getter method (if any).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodDefinitionHandle GetGetter(this PropertyDefinition property)
    {
        var accessors = property.GetAccessors();
        return accessors.Getter;
    }

    /// <summary>
    /// Gets the property setter method (if any).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MethodDefinitionHandle GetSetter(this PropertyDefinition property)
    {
        var accessors = property.GetAccessors();
        return accessors.Setter;
    }

    /// <summary>
    /// Checks if the property has a getter.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasGetter(this PropertyDefinition property)
    {
        var accessors = property.GetAccessors();
        return !accessors.Getter.IsNil;
    }

    /// <summary>
    /// Checks if the property has a setter.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasSetter(this PropertyDefinition property)
    {
        var accessors = property.GetAccessors();
        return !accessors.Setter.IsNil;
    }

    /// <summary>
    /// Checks if the property is read only (has getter but no setter).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsReadOnly(this PropertyDefinition property)
    {
        var accessors = property.GetAccessors();
        return !accessors.Getter.IsNil && accessors.Setter.IsNil;
    }

    /// <summary>
    /// Checks if the property is write only (has setter but no getter).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsWriteOnly(this PropertyDefinition property)
    {
        var accessors = property.GetAccessors();
        return accessors.Getter.IsNil && !accessors.Setter.IsNil;
    }

    /// <summary>
    /// Gets the default value of the property (if HasDefault is true).
    /// </summary>
    public static object? GetDefaultValue(this PropertyDefinition property, MetadataReader reader)
    {
        var constantHandle = property.GetDefaultValue();
        if (constantHandle.IsNil) { return null; }

        var constant = reader.GetConstant(constantHandle);
        return ReadConstantValue(reader, constant);
    }

    /// <summary>
    /// Tries to get the default value of the property as the specified type.
    /// This is the zero allocation alternative to GetDefaultValue for primitive types.
    /// </summary>
    /// <typeparam name="T">The value type to retrieve.</typeparam>
    /// <param name="property">The property definition.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <param name="value">The output value if successful.</param>
    /// <returns>True if the property has a default value of type T; otherwise false.</returns>
    public static bool TryGetDefaultValue<T>(this PropertyDefinition property, MetadataReader reader, out T value)
        where T : struct
    {
        var constantHandle = property.GetDefaultValue();
        if (constantHandle.IsNil)
        {
            value = default;
            return false;
        }

        var constant = reader.GetConstant(constantHandle);
        var blobReader = reader.GetBlobReader(constant.Value);

        // Match type code to T
        var typeCode = typeof(T) == typeof(bool)   ? ConstantTypeCode.Boolean :
                       typeof(T) == typeof(char)   ? ConstantTypeCode.Char :
                       typeof(T) == typeof(sbyte)  ? ConstantTypeCode.SByte :
                       typeof(T) == typeof(byte)   ? ConstantTypeCode.Byte :
                       typeof(T) == typeof(short)  ? ConstantTypeCode.Int16 :
                       typeof(T) == typeof(ushort) ? ConstantTypeCode.UInt16 :
                       typeof(T) == typeof(int)    ? ConstantTypeCode.Int32 :
                       typeof(T) == typeof(uint)   ? ConstantTypeCode.UInt32 :
                       typeof(T) == typeof(long)   ? ConstantTypeCode.Int64 :
                       typeof(T) == typeof(ulong)  ? ConstantTypeCode.UInt64 :
                       typeof(T) == typeof(float)  ? ConstantTypeCode.Single :
                       typeof(T) == typeof(double) ? ConstantTypeCode.Double :
                       (ConstantTypeCode)255;

        if (constant.TypeCode != typeCode)
        {
            value = default;
            return false;
        }

        value = constant.TypeCode switch
        {
            ConstantTypeCode.Boolean => (T)(object)blobReader.ReadBoolean(),
            ConstantTypeCode.Char    => (T)(object)blobReader.ReadChar(),
            ConstantTypeCode.SByte   => (T)(object)blobReader.ReadSByte(),
            ConstantTypeCode.Byte    => (T)(object)blobReader.ReadByte(),
            ConstantTypeCode.Int16   => (T)(object)blobReader.ReadInt16(),
            ConstantTypeCode.UInt16  => (T)(object)blobReader.ReadUInt16(),
            ConstantTypeCode.Int32   => (T)(object)blobReader.ReadInt32(),
            ConstantTypeCode.UInt32  => (T)(object)blobReader.ReadUInt32(),
            ConstantTypeCode.Int64   => (T)(object)blobReader.ReadInt64(),
            ConstantTypeCode.UInt64  => (T)(object)blobReader.ReadUInt64(),
            ConstantTypeCode.Single  => (T)(object)blobReader.ReadSingle(),
            ConstantTypeCode.Double  => (T)(object)blobReader.ReadDouble(),
            _ => throw new InvalidOperationException($"Cannot convert {constant.TypeCode} to {typeof(T)}")
        };

        return true;
    }

    /// <summary>
    /// Checks if the property name matches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NameIs(this PropertyDefinition property, MetadataReader reader, string name)
    {
        return reader.StringComparer.Equals(property.Name, name);
    }

    // == Helpers =============================================================

    private static object? ReadConstantValue(MetadataReader reader, Constant constant)
    {
        var blobReader = reader.GetBlobReader(constant.Value);

        return constant.TypeCode switch
        {
            ConstantTypeCode.Boolean => blobReader.ReadBoolean(),
            ConstantTypeCode.Char    => blobReader.ReadChar(),
            ConstantTypeCode.SByte   => blobReader.ReadSByte(),
            ConstantTypeCode.Byte    => blobReader.ReadByte(),
            ConstantTypeCode.Int16   => blobReader.ReadInt16(),
            ConstantTypeCode.UInt16  => blobReader.ReadUInt16(),
            ConstantTypeCode.Int32   => blobReader.ReadInt32(),
            ConstantTypeCode.UInt32  => blobReader.ReadUInt32(),
            ConstantTypeCode.Int64   => blobReader.ReadInt64(),
            ConstantTypeCode.UInt64  => blobReader.ReadUInt64(),
            ConstantTypeCode.Single  => blobReader.ReadSingle(),
            ConstantTypeCode.Double  => blobReader.ReadDouble(),
            ConstantTypeCode.String  => blobReader.ReadUTF16(blobReader.Length),
            ConstantTypeCode.NullReference => null,
            _ => throw new BadImageFormatException($"Unsupported constant type code: {constant.TypeCode}")
        };
    }
}
