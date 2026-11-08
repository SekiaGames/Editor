using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace WorldEdit.Core.Formats.W3x;

public sealed class W3xBinaryReader
{
    public W3xBinaryReader(byte[] data, int position = 0)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Position = position;
    }

    public byte[] Data { get; }
    public int Position { get; set; }
    public int Length => Data.Length;
    public int Remaining => Math.Max(0, Length - Position);
    public bool AtEnd => Position >= Length;

    public string FourCc()
    {
        return Text(4, Encoding.Latin1).TrimEnd('\0');
    }

    public string Text(int count, Encoding encoding)
    {
        Require(count);
        string value = encoding.GetString(Data, Position, count);
        Position += count;
        return value;
    }

    public string NullString(Encoding encoding)
    {
        int start = Position;
        while (Position < Length && Data[Position] != 0)
        {
            Position++;
        }
        string value = encoding.GetString(Data, start, Position - start);
        if (Position < Length)
        {
            Position++;
        }
        return value;
    }

    public byte[] NullStringBytes()
    {
        int start = Position;
        while (Position < Length && Data[Position] != 0)
        {
            Position++;
        }
        int count = Position - start;
        if (Position < Length)
        {
            Position++;
            count++;
        }
        return Data.AsSpan(start, count).ToArray();
    }

    public byte U8()
    {
        Require(1);
        return Data[Position++];
    }

    public ushort U16()
    {
        Require(2);
        ushort value = BinaryPrimitives.ReadUInt16LittleEndian(Data.AsSpan(Position));
        Position += 2;
        return value;
    }

    public short I16()
    {
        Require(2);
        short value = BinaryPrimitives.ReadInt16LittleEndian(Data.AsSpan(Position));
        Position += 2;
        return value;
    }

    public int I32()
    {
        Require(4);
        int value = BinaryPrimitives.ReadInt32LittleEndian(Data.AsSpan(Position));
        Position += 4;
        return value;
    }

    public uint U32()
    {
        Require(4);
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(Data.AsSpan(Position));
        Position += 4;
        return value;
    }

    public float F32()
    {
        Require(4);
        float value = BinaryPrimitives.ReadSingleLittleEndian(Data.AsSpan(Position));
        Position += 4;
        return value;
    }

    public byte[] Bytes(int count)
    {
        Require(count);
        byte[] value = Data.AsSpan(Position, count).ToArray();
        Position += count;
        return value;
    }

    public void Skip(int count)
    {
        Require(count);
        Position += count;
    }

    public int Count(int limit)
    {
        int value = I32();
        if (value < 0 || value > limit)
        {
            throw new InvalidDataException($"计数值异常 {value}（上限 {limit}，偏移 {Position - 4}）");
        }
        return value;
    }

    private void Require(int count)
    {
        if (count < 0 || Position < 0 || count > Length - Position)
        {
            throw new EndOfStreamException(
                $"数据在偏移 {Position} 处截断：需要 {count} 字节，只剩 {Remaining} 字节（总长 {Length}）");
        }
    }
}
