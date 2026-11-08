using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WorldEdit.Core.Formats.W3x;

public sealed class DoodadPlacement
{
    public string Id = "";
    public uint Variation;
    public float X;
    public float Y;
    public float Z;
    public float Angle;
    public float ScaleX = 1f;
    public float ScaleY = 1f;
    public float ScaleZ = 1f;
    public byte Flags;
    public byte Life;
    public int ItemTablePointer = -1;
    public int CreationNumber;

    public bool Hidden => (Flags & 0x03) == 0;

    public bool Dead => Life == 0;

    public float UniformScale => (ScaleX + ScaleY + ScaleZ) / 3f;
}

public sealed class W3oDoodads
{
    public int Version { get; private set; }
    public int SubVersion { get; private set; }
    public int DeclaredCount { get; private set; }
    public int Stride { get; private set; }
    public int Trailing { get; private set; }
    public byte[] TrailingBytes { get; private set; } = Array.Empty<byte>();
    public byte[] RawBytes { get; private set; }
    public readonly List<DoodadPlacement> Entries = new();

    public static W3oDoodads Parse(byte[] data)
    {
        if (data == null || data.Length < 16 || Encoding.ASCII.GetString(data, 0, 4) != "W3do")
        {
            throw new InvalidDataException("不是 war3map.doo");
        }

        var result = new W3oDoodads
        {
            RawBytes = data,
            Version = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4)),
            SubVersion = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(8)),
            DeclaredCount = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(12)),
        };
        if (result.DeclaredCount <= 0 || result.DeclaredCount > 200000)
        {
            return result;
        }

        result.Stride = (data.Length - 16) / result.DeclaredCount;
        result.Trailing = data.Length - 16 - result.Stride * result.DeclaredCount;
        if (result.Trailing > 0)
        {
            result.TrailingBytes = data.AsSpan(data.Length - result.Trailing, result.Trailing).ToArray();
        }
        if (result.Stride < 26)
        {
            throw new InvalidDataException($"记录步长异常: {result.Stride}（{data.Length} 字节 / {result.DeclaredCount} 条）");
        }

        for (int i = 0; i < result.DeclaredCount; i++)
        {
            int at = 16 + result.Stride * i;
            var entry = new DoodadPlacement
            {
                Id = Encoding.Latin1.GetString(data, at, 4).TrimEnd('\0'),
                Variation = ReadU32(data, at + 4),
                X = ReadF32(data, at + 8),
                Y = ReadF32(data, at + 12),
                Z = ReadF32(data, at + 16),
                Angle = ReadF32(data, at + 20),
            };
            if (result.Stride >= 42)
            {
                entry.ScaleX = ReadF32(data, at + 24);
                entry.ScaleY = ReadF32(data, at + 28);
                entry.ScaleZ = ReadF32(data, at + 32);
                entry.Flags = ReadU8(data, at + 36);
                entry.Life = ReadU8(data, at + 37);
                if (result.Stride >= 50)
                {
                    entry.ItemTablePointer = ReadI32(data, at + 38);
                }
                entry.CreationNumber = result.Version >= 8 ? ReadI32(data, at + 46) : ReadI32(data, at + 38);
            }
            else
            {
                entry.Flags = ReadU8(data, at + 24);
                entry.Life = ReadU8(data, at + 25);
            }
            result.Entries.Add(entry);
        }

        return result;
    }

    private static byte ReadU8(byte[] data, int at)
    {
        return at >= 0 && at < data.Length ? data[at] : (byte)0;
    }

    private static uint ReadU32(byte[] data, int at)
    {
        return at >= 0 && at + 4 <= data.Length ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at)) : 0u;
    }

    private static int ReadI32(byte[] data, int at)
    {
        return at >= 0 && at + 4 <= data.Length ? BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(at)) : 0;
    }

    private static float ReadF32(byte[] data, int at)
    {
        return at >= 0 && at + 4 <= data.Length ? BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(at)) : 0f;
    }

    public byte[] ToBytes()
    {
        throw new NotSupportedException("war3map.doo 写入尚未实现");
    }
}
