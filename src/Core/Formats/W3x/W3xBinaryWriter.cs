using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace WorldEdit.Core.Formats.W3x;

public sealed class W3xBinaryWriter
{
    private byte[] _buffer = new byte[256];
    private int _length;
    private Encoding _textEncoding = Encoding.UTF8;

    public int Length => _length;

    public W3xBinaryWriter UseTextEncoding(Encoding encoding)
    {
        _textEncoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        return this;
    }

    public W3xBinaryWriter FourCc(string value)
    {
        byte[] bytes = new byte[4];
        if (value != null)
        {
            Encoding.Latin1.GetBytes(value, 0, Math.Min(4, value.Length), bytes, 0);
        }
        return Bytes(bytes);
    }

    public W3xBinaryWriter Text(string value, int count)
    {
        byte[] bytes = new byte[count];
        if (value != null)
        {
            Encoding.Latin1.GetBytes(value, 0, Math.Min(count, value.Length), bytes, 0);
        }
        return Bytes(bytes);
    }

    public W3xBinaryWriter NullString(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Bytes(_textEncoding.GetBytes(value));
        }
        return U8(0);
    }

    public W3xBinaryWriter NullStringBytes(byte[] value)
    {
        if (value != null)
        {
            Bytes(value);
        }
        return U8(0);
    }

    public W3xBinaryWriter U8(byte value)
    {
        Ensure(1);
        _buffer[_length++] = value;
        return this;
    }

    public W3xBinaryWriter U16(ushort value)
    {
        Ensure(2);
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(_length), value);
        _length += 2;
        return this;
    }

    public W3xBinaryWriter I16(short value)
    {
        Ensure(2);
        BinaryPrimitives.WriteInt16LittleEndian(_buffer.AsSpan(_length), value);
        _length += 2;
        return this;
    }

    public W3xBinaryWriter I32(int value)
    {
        Ensure(4);
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_length), value);
        _length += 4;
        return this;
    }

    public W3xBinaryWriter U32(uint value)
    {
        Ensure(4);
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(_length), value);
        _length += 4;
        return this;
    }

    public W3xBinaryWriter F32(float value)
    {
        Ensure(4);
        BinaryPrimitives.WriteSingleLittleEndian(_buffer.AsSpan(_length), value);
        _length += 4;
        return this;
    }

    public W3xBinaryWriter Bytes(byte[] value)
    {
        if (value == null || value.Length == 0)
        {
            return this;
        }
        Ensure(value.Length);
        value.CopyTo(_buffer, _length);
        _length += value.Length;
        return this;
    }

    public W3xBinaryWriter Skip(int count)
    {
        Ensure(count);
        Array.Clear(_buffer, _length, count);
        _length += count;
        return this;
    }

    public byte[] ToArray()
    {
        return _buffer.AsSpan(0, _length).ToArray();
    }

    private void Ensure(int count)
    {
        if (count <= 0)
        {
            return;
        }
        int required = _length + count;
        if (required <= _buffer.Length)
        {
            return;
        }
        int capacity = _buffer.Length;
        while (capacity < required)
        {
            capacity *= 2;
        }
        Array.Resize(ref _buffer, capacity);
    }
}
