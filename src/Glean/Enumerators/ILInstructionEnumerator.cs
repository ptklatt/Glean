using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Enumerators;

/// <summary>
/// An experimental decoded IL instruction.
/// </summary>
/// <remarks>
/// The Int32 operand does not preserve eight byte values or switch target lists.
/// This incomplete operand model remains internal.
/// </remarks>
internal readonly struct ILInstruction
{
    internal ILInstruction(int offset, ILOpCode opCode, int operandToken)
    {
        Offset = offset;
        OpCode = opCode;
        OperandToken = operandToken;
    }

    public int Offset { get; }

    public ILOpCode OpCode { get; }

    public int OperandToken { get; }

    public ILOperandKind OperandKind
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            int op = (int)OpCode;
            if (op >= 0xFE00)
            {
                int secondByte = op & 0xFF;
                if (secondByte < TwoByteKinds.Length)
                {
                    return (ILOperandKind)TwoByteKinds[secondByte];
                }

                return ILOperandKind.None;
            }

            return (ILOperandKind)SingleByteKinds[op & 0xFF];
        }
    }

    private static ReadOnlySpan<byte> SingleByteKinds => new byte[256]
    {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        7, 7, 7, 7, 7, 7,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        3, 3,
        4, 5, 6,
        0, 0, 0,
        1, 1, 1,
        0,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        8,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 1, 1, 1,
        0, 0, 0,
        1,
        0,
        1, 1, 1, 1, 1, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 1,
        0,
        1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0,
        1,
        0,
        1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,
        2, 2,
        0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,
        0,
    };

    private static ReadOnlySpan<byte> TwoByteKinds => new byte[32]
    {
        0, 0, 0, 0, 0, 0,
        1, 1,
        0,
        7, 7, 7, 7, 7, 7,
        0,
        0,
        0,
        0,
        0,
        1,
        1,
        0, 0,
        0,
        0,
        0,
        1,
        0,
        0,
        0,
        0,
    };
}

/// <summary>
/// Experimental IL instruction enumerator with one method body copy.
/// </summary>
internal struct ILInstructionEnumerator : IStructEnumerator<ILInstruction>
{
    private readonly byte[] _il;
    private int _offset;
    private ILInstruction _current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ILInstructionEnumerator Create(byte[] il)
    {
        return new ILInstructionEnumerator(il);
    }

    private ILInstructionEnumerator(byte[] il)
    {
        _il = il;
        _offset = 0;
        _current = default;
    }

    public ILInstruction Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _current;
    }

    public bool MoveNext()
    {
        if ((_il == null) || (_offset >= _il.Length))
        {
            return false;
        }

        int startOffset = _offset;
        byte firstByte = _il[_offset++];

        ILOpCode opCode;
        byte operandSizeEntry;

        if ((firstByte == 0xFE) && (_offset < _il.Length))
        {
            byte secondByte = _il[_offset++];
            opCode = (ILOpCode)(0xFE00 | secondByte);
            if (secondByte < TwoByteOperandSizes.Length)
            {
                operandSizeEntry = TwoByteOperandSizes[secondByte];
            }
            else
            {
                operandSizeEntry = 0;
            }
        }
        else
        {
            opCode = (ILOpCode)firstByte;
            operandSizeEntry = SingleByteOperandSizes[firstByte];
        }

        int operandToken = 0;

        if (operandSizeEntry == 255)
        {
            if ((_offset + 4) <= _il.Length)
            {
                int count = ReadInt32(_offset);
                operandToken = count;
                int skip = 4 + (count * 4);
                _offset += skip;
                if (_offset > _il.Length)
                {
                    _offset = _il.Length;
                }
            }
        }
        else
        {
            int operandSize = operandSizeEntry;
            if ((_offset + operandSize) <= _il.Length)
            {
                operandToken = operandSize switch
                {
                    1 => (sbyte)_il[_offset],
                    2 => ReadInt16(_offset),
                    4 => ReadInt32(_offset),
                    _ => 0,
                };

                _offset += operandSize;
            }
        }

        _current = new ILInstruction(startOffset, opCode, operandToken);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ILInstructionEnumerator GetEnumerator()
    {
        return this;
    }

    public void Dispose()
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadInt32(int offset)
    {
        return _il[offset]
            | (_il[offset + 1] << 8)
            | (_il[offset + 2] << 16)
            | (_il[offset + 3] << 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadInt16(int offset)
    {
        return _il[offset] | (_il[offset + 1] << 8);
    }

    private static ReadOnlySpan<byte> SingleByteOperandSizes => new byte[256]
    {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1, 1, 1, 1, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        1,
        4, 8, 4, 8,
        0, 0, 0,
        4, 4, 4,
        0,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4,
        255,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        4, 4, 4, 4, 4, 4, 4,
        0, 0, 0,
        4,
        0,
        4, 4, 4, 4, 4, 4, 4,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        4, 4,
        0,
        4,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        4, 4, 4,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0,
        4,
        0,
        4,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        4,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,
        4, 1,
        0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0,
        0,
    };

    private static ReadOnlySpan<byte> TwoByteOperandSizes => new byte[32]
    {
        0, 0, 0, 0, 0, 0,
        4, 4,
        0,
        2, 2, 2, 2, 2, 2,
        0,
        0,
        0,
        1,
        0,
        4,
        4,
        0, 0,
        1,
        0,
        0,
        4,
        0,
        0,
        0,
        0,
    };
}
