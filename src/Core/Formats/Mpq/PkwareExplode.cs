using System;
using System.IO;

namespace WorldEdit.Core.Formats.Mpq;

public static class PkwareExplode
{
    private static readonly byte[] LengthBits =
    {
        3, 2, 3, 3, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6, 7, 7,
    };

    private static readonly byte[] LengthCodes =
    {
        5, 3, 1, 6, 10, 2, 12, 20, 4, 24, 8, 48, 16, 32, 64, 0,
    };

    private static readonly byte[] ExtraLengthBits =
    {
        0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8,
    };

    private static readonly ushort[] LengthBase =
    {
        0x0000, 0x0001, 0x0002, 0x0003, 0x0004, 0x0005, 0x0006, 0x0007,
        0x0008, 0x000A, 0x000E, 0x0016, 0x0026, 0x0046, 0x0086, 0x0106,
    };

    private static readonly byte[] DistanceBits =
    {
        2, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6, 6, 6, 6, 6, 6,
        6, 6, 6, 6, 6, 6, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8,
    };

    private static readonly byte[] DistanceCodes =
    {
        0x03, 0x0D, 0x05, 0x19, 0x09, 0x11, 0x01, 0x3E, 0x1E, 0x2E, 0x0E, 0x36, 0x16, 0x26, 0x06, 0x3A,
        0x1A, 0x2A, 0x0A, 0x32, 0x12, 0x22, 0x42, 0x02, 0x7C, 0x3C, 0x5C, 0x1C, 0x6C, 0x2C, 0x4C, 0x0C,
        0x74, 0x34, 0x54, 0x14, 0x64, 0x24, 0x44, 0x04, 0x78, 0x38, 0x58, 0x18, 0x68, 0x28, 0x48, 0x08,
        0xF0, 0x70, 0xB0, 0x30, 0xD0, 0x50, 0x90, 0x10, 0xE0, 0x60, 0xA0, 0x20, 0xC0, 0x40, 0x80, 0x00,
    };

    private static readonly byte[] LengthLookup = BuildDecodeTable(LengthBits, LengthCodes);
    private static readonly byte[] DistanceLookup = BuildDecodeTable(DistanceBits, DistanceCodes);

    private static byte[] BuildDecodeTable(byte[] bits, byte[] codes)
    {
        var table = new byte[256];
        for (int i = bits.Length - 1; i >= 0; i--)
        {
            int start = codes[i];
            int step = 1 << bits[i];
            for (int index = start; index < 0x100; index += step)
            {
                table[index] = (byte)i;
            }
        }
        return table;
    }

    public static byte[] Explode(byte[] source, int expected)
    {
        if (source == null || source.Length < 2)
        {
            throw new InvalidDataException("implode 数据太短");
        }

        int textMode = source[0];
        int dictBits = source[1];
        if (textMode != 0)
        {
            throw new NotSupportedException($"PKWARE 文本模式暂不支持（{textMode}）");
        }
        if (dictBits < 4 || dictBits > 6)
        {
            throw new InvalidDataException($"implode 字典位数异常: {dictBits}");
        }

        var reader = new LsbBitReader(source, 2);
        var output = new byte[expected];
        int written = 0;
        while (written < expected)
        {
            int literal = ReadLiteral(reader);
            if (literal < 0)
            {
                break;
            }
            if (literal < 0x100)
            {
                output[written++] = (byte)literal;
                continue;
            }

            int copyLength = literal - 0xFE;
            int distance = ReadDistance(reader, dictBits, copyLength);
            if (distance <= 0)
            {
                break;
            }
            int from = written - distance;
            if (from < 0)
            {
                throw new InvalidDataException($"implode 回溯越界: {from}");
            }
            for (int i = 0; i < copyLength && written < output.Length; i++)
            {
                output[written] = output[from + i];
                written++;
            }
        }

        if (written == expected)
        {
            return output;
        }
        var trimmed = new byte[written];
        Array.Copy(output, trimmed, written);
        return trimmed;
    }

    private static int ReadLiteral(LsbBitReader reader)
    {
        int flag = reader.ReadBits(1);
        if (flag < 0)
        {
            return -1;
        }
        if (flag == 0)
        {
            int value = reader.ReadBits(8);
            return value < 0 ? -1 : value;
        }

        int peek = reader.PeekByte();
        if (peek < 0)
        {
            return -1;
        }
        int slot = LengthLookup[peek];
        if (reader.ReadBits(LengthBits[slot]) < 0)
        {
            return -1;
        }

        int extra = ExtraLengthBits[slot];
        int length = LengthBase[slot];
        if (extra != 0)
        {
            int bits = reader.ReadBits(extra);
            if (bits < 0)
            {
                return -1;
            }
            length += bits;
        }
        return length + 0x100;
    }

    private static int ReadDistance(LsbBitReader reader, int dictBits, int copyLength)
    {
        if (!reader.EnsureBits(8))
        {
            return 0;
        }
        int slot = DistanceLookup[reader.PeekByte()];
        int skip = DistanceBits[slot];
        if (reader.ReadBits(skip) < 0)
        {
            return 0;
        }

        if (copyLength == 2)
        {
            if (!reader.EnsureBits(2))
            {
                return 0;
            }
            slot = (slot << 2) | reader.ReadBits(2);
        }
        else
        {
            if (!reader.EnsureBits(dictBits))
            {
                return 0;
            }
            slot = (slot << dictBits) | reader.ReadBits(dictBits);
        }
        return slot + 1;
    }
}
