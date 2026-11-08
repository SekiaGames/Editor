using System;
using System.IO;

namespace WorldEdit.Core.Formats.Mpq;

public static class AdpcmDecode
{
    private static readonly int[] StepTable =
    {
        0x0007, 0x0008, 0x0009, 0x000A, 0x000B, 0x000C, 0x000D, 0x000E,
        0x0010, 0x0011, 0x0013, 0x0015, 0x0017, 0x0019, 0x001C, 0x001F,
        0x0022, 0x0025, 0x0029, 0x002D, 0x0032, 0x0037, 0x003C, 0x0042,
        0x0049, 0x0050, 0x0058, 0x0061, 0x006B, 0x0076, 0x0082, 0x008F,
        0x009D, 0x00AD, 0x00BE, 0x00D1, 0x00E6, 0x00FD, 0x0117, 0x0133,
        0x0151, 0x0173, 0x0198, 0x01C1, 0x01EE, 0x0220, 0x0256, 0x0292,
        0x02D4, 0x031C, 0x036C, 0x03C3, 0x0424, 0x048E, 0x0502, 0x0583,
        0x0610, 0x06AB, 0x0756, 0x0812, 0x08E0, 0x09C3, 0x0ABD, 0x0BD0,
        0x0CFF, 0x0E4C, 0x0FBA, 0x114C, 0x1307, 0x14EE, 0x1706, 0x1954,
        0x1BDC, 0x1EA5, 0x21B6, 0x2515, 0x28CA, 0x2CDF, 0x315B, 0x364B,
        0x3BB9, 0x41B2, 0x4844, 0x4F7E, 0x5771, 0x602F, 0x69CE, 0x7462,
        0x7FFF,
    };

    private static readonly int[] AdjustTable =
    {
        -1, 0, -1, 4, -1, 2, -1, 6,
        -1, 1, -1, 5, -1, 3, -1, 7,
        -1, 1, -1, 5, -1, 3, -1, 7,
        -1, 2, -1, 4, -1, 6, -1, 8,
    };

    public static byte[] Decompress(byte[] data, int channels, int expected = 0)
    {
        if (data == null || data.Length < 2 + 2 * channels)
        {
            throw new InvalidDataException($"ADPCM 数据太短: {data?.Length ?? 0} 字节");
        }

        var stepIndex = new int[channels];
        var sample = new int[channels];
        for (int i = 0; i < channels; i++)
        {
            stepIndex[i] = 0x2C;
        }

        using var output = new MemoryStream(data.Length * 4);
        using var writer = new BinaryWriter(output);

        int position = 0;
        position++;
        int shift = data[position++];

        for (int i = 0; i < channels; i++)
        {
            int initial = (short)(data[position] | (data[position + 1] << 8));
            position += 2;
            sample[i] = initial;
            writer.Write((short)initial);
        }

        int channel = channels - 1;
        while (position < data.Length && (expected <= 0 || output.Length < expected))
        {
            byte value = data[position++];
            if (channels == 2)
            {
                channel = 1 - channel;
            }

            if ((value & 0x80) != 0)
            {
                switch (value & 0x7F)
                {
                    case 0:
                        if (stepIndex[channel] != 0)
                        {
                            stepIndex[channel]--;
                        }
                        writer.Write((short)sample[channel]);
                        break;
                    case 1:
                        stepIndex[channel] += 8;
                        if (stepIndex[channel] > 0x58)
                        {
                            stepIndex[channel] = 0x58;
                        }
                        if (channels == 2)
                        {
                            channel = 1 - channel;
                        }
                        break;
                    case 2:
                        break;
                    default:
                        stepIndex[channel] -= 8;
                        if (stepIndex[channel] < 0)
                        {
                            stepIndex[channel] = 0;
                        }
                        if (channels == 2)
                        {
                            channel = 1 - channel;
                        }
                        break;
                }
                continue;
            }

            int step = StepTable[stepIndex[channel]];
            int delta = step >> shift;
            if ((value & 1) != 0)
            {
                delta += step;
            }
            if ((value & 2) != 0)
            {
                delta += step >> 1;
            }
            if ((value & 4) != 0)
            {
                delta += step >> 2;
            }
            if ((value & 8) != 0)
            {
                delta += step >> 3;
            }
            if ((value & 0x10) != 0)
            {
                delta += step >> 4;
            }
            if ((value & 0x20) != 0)
            {
                delta += step >> 5;
            }

            int current = sample[channel];
            if ((value & 0x40) != 0)
            {
                current -= delta;
                if (current <= short.MinValue)
                {
                    current = short.MinValue;
                }
            }
            else
            {
                current += delta;
                if (current >= short.MaxValue)
                {
                    current = short.MaxValue;
                }
            }

            sample[channel] = current;
            writer.Write((short)current);

            stepIndex[channel] += AdjustTable[value & 0x1F];
            if (stepIndex[channel] < 0)
            {
                stepIndex[channel] = 0;
            }
            else if (stepIndex[channel] > 0x58)
            {
                stepIndex[channel] = 0x58;
            }
        }

        writer.Flush();
        return output.ToArray();
    }
}
