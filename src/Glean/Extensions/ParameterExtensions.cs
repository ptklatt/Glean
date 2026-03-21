using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Extensions;

/// <summary>
/// Extension methods for <see cref="Parameter"/>.
/// </summary>
/// <remarks>
/// This extension class targets callers working directly
/// with System.Reflection.Metadata <see cref="Parameter"/> structs.
/// </remarks>
public static class ParameterExtensions
{
    /// <summary>
    /// Checks if the parameter is marked as [In].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsIn(this Parameter parameter) => (parameter.Attributes & ParameterAttributes.In) != 0;

    /// <summary>
    /// Checks if the parameter is marked as [Out].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOut(this Parameter parameter) => (parameter.Attributes & ParameterAttributes.Out) != 0;

    /// <summary>
    /// Checks if the parameter is optional.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOptional(this Parameter parameter) => (parameter.Attributes & ParameterAttributes.Optional) != 0;

    /// <summary>
    /// Checks if the parameter has a default value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasDefault(this Parameter parameter) => (parameter.Attributes & ParameterAttributes.HasDefault) != 0;

    /// <summary>
    /// Checks if the parameter has field marshaling info.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasFieldMarshal(this Parameter parameter) => (parameter.Attributes & ParameterAttributes.HasFieldMarshal) != 0;

    // == Metadata access =====================================================

    /// <summary>
    /// Gets the default value for the parameter (if it has one).
    /// </summary>
    /// <remarks>
    /// This method boxes primitive values. Use <see cref="TryGetDefaultValue{T}"/> to avoid boxing.
    /// </remarks>
    public static object? GetDefaultValue(this Parameter parameter, MetadataReader reader)
    {
        if (!parameter.HasDefault()) { return null; }

        var constantHandle = parameter.GetDefaultValue();
        if (constantHandle.IsNil) { return null; }

        var constant = reader.GetConstant(constantHandle);
        return ReadConstantValue(reader, constant);
    }

    /// <summary>
    /// Tries to get the default value of a parameter without boxing.
    /// </summary>
    /// <typeparam name="T">The expected value type (int, bool, etc.).</typeparam>
    /// <param name="parameter">The parameter.</param>
    /// <param name="reader">The metadata reader.</param>
    /// <param name="value">The output value if successful.</param>
    /// <returns>True if the parameter has a default value of type T; otherwise false.</returns>
    public static bool TryGetDefaultValue<T>(this Parameter parameter, MetadataReader reader, out T value)
        where T : struct
    {
        if (!parameter.HasDefault())
        {
            value = default;
            return false;
        }

        var constantHandle = parameter.GetDefaultValue();
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
    /// Gets the marshaling descriptor blob for the parameter.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BlobHandle GetMarshalingDescriptor(this Parameter parameter)
    {
        return parameter.GetMarshallingDescriptor();
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
