using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Text;

using Glean.Internal;

namespace Glean.Signatures;

/// <summary>
/// Represents a primitive type signature (Boolean, Char, I1, U1, I2, U2, I4, U4, I8, U8, R4, R8, I, U, Object, String).
/// Implemented as singletons for zero-allocation identity checks.
/// </summary>
public sealed class PrimitiveTypeSignature : TypeSignature
{
    private static readonly ConcurrentDictionary<PrimitiveTypeCode, PrimitiveTypeSignature> _cache = new();

    /// <summary>
    /// Gets the primitive type code.
    /// </summary>
    public PrimitiveTypeCode TypeCode { get; }

    private PrimitiveTypeSignature(PrimitiveTypeCode typeCode)
    {
        TypeCode = typeCode;
    }

    /// <summary>
    /// Gets or creates a primitive type signature for the specified type code.
    /// Thread-safe singleton access.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PrimitiveTypeSignature Get(PrimitiveTypeCode typeCode)
    {
        return _cache.GetOrAdd(typeCode, static tc => new PrimitiveTypeSignature(tc));
    }

    public override TypeSignatureKind Kind => TypeSignatureKind.Primitive;

    public override bool? IsValueType => TypeCode switch
    {
        PrimitiveTypeCode.Object or PrimitiveTypeCode.String => false,
        _ => true
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Is(string ns, string name, string? scope = null)
    {
        if (!string.Equals(ns, WellKnownTypes.SystemNs, StringComparison.Ordinal))
        {
            return false;
        }

        return TypeCode switch
        {
            PrimitiveTypeCode.Boolean        => name == "Boolean",
            PrimitiveTypeCode.Char           => name == "Char",
            PrimitiveTypeCode.SByte          => name == "SByte",
            PrimitiveTypeCode.Byte           => name == "Byte",
            PrimitiveTypeCode.Int16          => name == "Int16",
            PrimitiveTypeCode.UInt16         => name == "UInt16",
            PrimitiveTypeCode.Int32          => name == "Int32",
            PrimitiveTypeCode.UInt32         => name == "UInt32",
            PrimitiveTypeCode.Int64          => name == "Int64",
            PrimitiveTypeCode.UInt64         => name == "UInt64",
            PrimitiveTypeCode.Single         => name == "Single",
            PrimitiveTypeCode.Double         => name == "Double",
            PrimitiveTypeCode.IntPtr         => name == "IntPtr",
            PrimitiveTypeCode.UIntPtr        => name == "UIntPtr",
            PrimitiveTypeCode.Object         => name == "Object",
            PrimitiveTypeCode.String         => name == "String",
            PrimitiveTypeCode.TypedReference => name == "TypedReference",
            _                                => false
        };
    }

    public override bool Equals(TypeSignature? other)
        => (other is PrimitiveTypeSignature p) && (p.TypeCode == TypeCode);

    public override int GetHashCode() => HashCode.Combine(Kind, TypeCode);

    public override void FormatTo(StringBuilder sb)
    {
        sb.Append(TypeCode switch
        {
            PrimitiveTypeCode.Boolean        => "bool",
            PrimitiveTypeCode.Char           => "char",
            PrimitiveTypeCode.SByte          => "sbyte",
            PrimitiveTypeCode.Byte           => "byte",
            PrimitiveTypeCode.Int16          => "short",
            PrimitiveTypeCode.UInt16         => "ushort",
            PrimitiveTypeCode.Int32          => "int",
            PrimitiveTypeCode.UInt32         => "uint",
            PrimitiveTypeCode.Int64          => "long",
            PrimitiveTypeCode.UInt64         => "ulong",
            PrimitiveTypeCode.Single         => "float",
            PrimitiveTypeCode.Double         => "double",
            PrimitiveTypeCode.IntPtr         => "nint",
            PrimitiveTypeCode.UIntPtr        => "nuint",
            PrimitiveTypeCode.Object         => "object",
            PrimitiveTypeCode.String         => "string",
            PrimitiveTypeCode.TypedReference => "System.TypedReference",
            _                                => $"<primitive:{TypeCode}>"
        });
    }
}
