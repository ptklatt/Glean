using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Glean.Enumerators;

/// <summary>
/// Experimental IL instruction enumerator over a read only span.
/// </summary>
internal ref struct ILInstructionSpanEnumerator
{
    private readonly ReadOnlySpan<byte> _il;
    private int _offset;
    private ILInstruction _current;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ILInstructionSpanEnumerator Create(ReadOnlySpan<byte> il)
    {
        return new ILInstructionSpanEnumerator(il);
    }

    private ILInstructionSpanEnumerator(ReadOnlySpan<byte> il)
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
        if (_offset >= _il.Length)
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
    public ILInstructionSpanEnumerator GetEnumerator()
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
