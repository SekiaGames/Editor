using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace WorldEdit.Core.Formats.W3x;

public sealed class W3mPathing
{
    public const byte Unwalkable = 0x02;
    public const byte Unflyable = 0x04;
    public const byte Unbuildable = 0x08;

    public int Version { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public byte[] Cells { get; private set; } = Array.Empty<byte>();
    public byte[] RawBytes { get; private set; }

    public byte At(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            return 0;
        }
        return Cells[y * Width + x];
    }

    public int Count(byte flag)
    {
        int total = 0;
        foreach (byte cell in Cells)
        {
            if ((cell & flag) != 0)
            {
                total++;
            }
        }
        return total;
    }

    public static W3mPathing Parse(byte[] data)
    {
        if (data == null || data.Length < 16 || Encoding.ASCII.GetString(data, 0, 4) != "MP3W")
        {
            throw new InvalidDataException("不是 war3map.wpm");
        }

        var pathing = new W3mPathing
        {
            RawBytes = data,
            Version = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4)),
            Width = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(8)),
            Height = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(12)),
        };
        if (pathing.Width <= 0 || pathing.Height <= 0
            || pathing.Width > 8192 || pathing.Height > 8192)
        {
            throw new InvalidDataException($"寻路图尺寸异常: {pathing.Width}x{pathing.Height}");
        }

        int expected = pathing.Width * pathing.Height;
        int available = Math.Min(expected, data.Length - 16);
        pathing.Cells = new byte[expected];
        Array.Copy(data, 16, pathing.Cells, 0, available);
        return pathing;
    }

    public byte[] ToBytes()
    {
        throw new NotSupportedException("war3map.wpm 写入尚未实现");
    }
}
