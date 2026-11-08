using System;
using System.IO;

namespace WorldEdit.Core.Formats.Mpq;

public sealed class LsbBitReader
{
    private readonly byte[] _data;
    private int _position;
    private int _current;
    private int _bitCount;

    public LsbBitReader(byte[] data, int start = 0)
    {
        _data = data;
        _position = start;
    }

    public int Position => _position;

    public bool HasMore => _bitCount > 0 || _position < _data.Length;

    public bool EnsureBits(int count)
    {
        while (_bitCount < count)
        {
            if (_position >= _data.Length)
            {
                return false;
            }
            _current |= _data[_position++] << _bitCount;
            _bitCount += 8;
        }
        return true;
    }

    public int ReadBits(int count)
    {
        if (count <= 0)
        {
            return 0;
        }
        if (!EnsureBits(count))
        {
            return -1;
        }
        int value = _current & ((1 << count) - 1);
        _current >>= count;
        _bitCount -= count;
        return value;
    }

    public int PeekByte()
    {
        return EnsureBits(8) ? _current & 0xFF : -1;
    }

    public void AlignToByte()
    {
        int drop = _bitCount % 8;
        _current >>= drop;
        _bitCount -= drop;
    }
}
