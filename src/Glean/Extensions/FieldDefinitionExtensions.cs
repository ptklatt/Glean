using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

// Disable obsolete warning for enums as this is a usability extension
#pragma warning disable SYSLIB0050

namespace Glean.Extensions;

/// <summary>
/// Extension methods for <see cref="FieldDefinition"/>.
/// Zero allocation flag checks and metadata access.
/// </summary>
public static class FieldDefinitionExtensions
{
    // == Visibility checks ===================================================

    /// <summary>
    /// Checks if the field is public.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPublic(this FieldDefinition field)
    {
        return (field.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
    }

    /// <summary>
    /// Checks if the field is private.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPrivate(this FieldDefinition field)
    {
        return (field.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Private;
    }

    /// <summary>
    /// Checks if the field is family (protected).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFamily(this FieldDefinition field)
    {
        return (field.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Family;
    }

    /// <summary>
    /// Checks if the field is internal (assembly scoped).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInternal(this FieldDefinition field)
    {
        return (field.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Assembly;
    }

    // == Field kind checks ===================================================

    /// <summary>
    /// Checks if the field is static.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStatic(this FieldDefinition field)
    {
        return (field.Attributes & FieldAttributes.Static) != 0;
    }

    /// <summary>
    /// Checks if the field is read only (initonly).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInitOnly(this FieldDefinition field)
    {
        return (field.Attributes & FieldAttributes.InitOnly) != 0;
    }

    /// <summary>
    /// Checks if the field is a literal (const).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLiteral(this FieldDefinition field)
    {
        return (field.Attributes & FieldAttributes.Literal) != 0;
    }

    /// <summary>
    /// Checks if the field is not serialized.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNotSerialized(this FieldDefinition field)
    {
        return (field.Attributes & FieldAttributes.NotSerialized) != 0;
    }

    /// <summary>
    /// Checks if the field has a special name.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSpecialName(this FieldDefinition field)
    {
        return (field.Attributes & FieldAttributes.SpecialName) != 0;
    }

    /// <summary>
    /// Checks if the field has a default value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasDefault(this FieldDefinition field)
    {
        return (field.Attributes & FieldAttributes.HasDefault) != 0;
    }

    // == Value access ===================================================

    /// <summary>
    /// Gets the default value of a literal or has default field.
    /// </summary>
    /// <remarks>
    /// This method boxes primitive values. Use <see cref="TryGetDefaultValue{T}"/> to avoid boxing.
    /// </remarks>
    public static object? GetDefaultValue(this FieldDefinition field, MetadataReader reader)
    {
        var constantHandle = field.GetDefaultValue();
        if (constantHandle.IsNil) { return null; }

        var constant = reader.GetConstant(constantHandle);
        return ReadConstantValue(reader, constant);
    }

    /// <summary>
    /// Tries to get the default value of a literal or has default field without boxing.
    /// </summary>
    /// <typeparam name="T">The expected value type (int, bool, etc.).</typeparam>
    /// <param name="field">The field definition.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <param name="value">The output value if successful.</param>
    /// <returns>True if the field has a default value of type T; otherwise false.</returns>
    public static bool TryGetDefaultValue<T>(this FieldDefinition field, MetadataReader reader, out T value)
        where T : struct
    {
        var constantHandle = field.GetDefaultValue();
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
    /// Gets the field offset for explicit layout types.
    /// Returns null if not specified.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int? GetFieldOffset(this FieldDefinition field)
    {
        var offset = field.GetOffset();
        return offset == -1 ? null : offset;
    }

    // == Identity checks =====================================================

    /// <summary>
    /// Checks if the field name matches.
    /// Zero allocation identity check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NameIs(this FieldDefinition field, MetadataReader reader, string name)
    {
        return reader.StringComparer.Equals(field.Name, name);
    }

    /// <summary>
    /// Gets the marshaling descriptor blob for the field.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BlobHandle GetMarshalingDescriptor(this FieldDefinition field)
    {
        return field.GetMarshallingDescriptor();
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
