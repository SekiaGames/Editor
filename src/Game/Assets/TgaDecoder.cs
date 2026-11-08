using Godot;
using System;
using System.IO;

namespace WorldEdit.Assets;

public static class TgaDecoder
{
    public static Image Decode(byte[] data)
    {
        if (data.Length < 18)
        {
            throw new InvalidDataException("TGA 头不足 18 字节");
        }

        int idLength = data[0];
        int colorMapType = data[1];
        int imageType = data[2];
        int colorMapFirst = data[3] | (data[4] << 8);
        int colorMapLength = data[5] | (data[6] << 8);
        int colorMapBits = data[7];
        int width = data[12] | (data[13] << 8);
        int height = data[14] | (data[15] << 8);
        int bitsPerPixel = data[16];
        int descriptor = data[17];
        bool topOrigin = (descriptor & 0x20) != 0;

        int offset = 18 + idLength;

        byte[] palette = null;
        if (colorMapType == 1)
        {
            palette = new byte[colorMapLength * 4];
            for (int i = 0; i < colorMapLength; i++)
            {
                int at = offset + i * ((colorMapBits + 7) / 8);
                WriteColor(palette, i * 4, data, at, colorMapBits, 255);
            }
            offset += colorMapLength * ((colorMapBits + 7) / 8);
        }

        int bytesPerPixel = (bitsPerPixel + 7) / 8;
        int pixels = width * height;
        var rgba = new byte[pixels * 4];
        bool rle = imageType is 9 or 10 or 11;

        int written = 0;
        while (written < pixels)
        {
            int count = 1;
            bool repeat = false;
            if (rle)
            {
                int packet = data[offset++];
                count = (packet & 0x7F) + 1;
                repeat = (packet & 0x80) != 0;
            }

            offset = ReadPixel(data, offset, imageType, bytesPerPixel, bitsPerPixel, palette, colorMapFirst,
                rgba, written * 4);
            written++;

            int copyFrom = (written - 1) * 4;
            for (int i = 1; i < count && written < pixels; i++)
            {
                if (repeat)
                {
                    rgba[written * 4] = rgba[copyFrom];
                    rgba[written * 4 + 1] = rgba[copyFrom + 1];
                    rgba[written * 4 + 2] = rgba[copyFrom + 2];
                    rgba[written * 4 + 3] = rgba[copyFrom + 3];
                }
                else
                {
                    offset = ReadPixel(data, offset, imageType, bytesPerPixel, bitsPerPixel, palette, colorMapFirst,
                        rgba, written * 4);
                }
                written++;
            }
        }

        if (!topOrigin)
        {
            FlipVertically(rgba, width, height);
        }
        return Image.CreateFromData(width, height, false, Image.Format.Rgba8, rgba);
    }

    private static int ReadPixel(byte[] data, int offset, int imageType, int bytesPerPixel, int bitsPerPixel,
        byte[] palette, int colorMapFirst, byte[] rgba, int target)
    {
        if (imageType is 1 or 9)
        {
            int index = data[offset] - colorMapFirst;
            int at = index * 4;
            rgba[target] = palette[at];
            rgba[target + 1] = palette[at + 1];
            rgba[target + 2] = palette[at + 2];
            rgba[target + 3] = palette[at + 3];
            return offset + 1;
        }

        if (imageType is 3 or 11)
        {
            byte grey = data[offset];
            rgba[target] = grey;
            rgba[target + 1] = grey;
            rgba[target + 2] = grey;
            rgba[target + 3] = 255;
            return offset + bytesPerPixel;
        }

        WriteColor(rgba, target, data, offset, bitsPerPixel, 255);
        return offset + bytesPerPixel;
    }

    private static void WriteColor(byte[] rgba, int target, byte[] data, int offset, int bitsPerPixel, byte forcedAlpha)
    {
        switch (bitsPerPixel)
        {
            case 32:
                rgba[target] = data[offset + 2];
                rgba[target + 1] = data[offset + 1];
                rgba[target + 2] = data[offset];
                rgba[target + 3] = data[offset + 3];
                break;
            case 24:
                rgba[target] = data[offset + 2];
                rgba[target + 1] = data[offset + 1];
                rgba[target + 2] = data[offset];
                rgba[target + 3] = forcedAlpha;
                break;
            case 16:
                int short_ = data[offset] | (data[offset + 1] << 8);
                rgba[target] = (byte)(((short_ >> 10) & 0x1F) * 255 / 31);
                rgba[target + 1] = (byte)(((short_ >> 5) & 0x1F) * 255 / 31);
                rgba[target + 2] = (byte)((short_ & 0x1F) * 255 / 31);
                rgba[target + 3] = forcedAlpha;
                break;
            case 15:
                int word = data[offset] | (data[offset + 1] << 8);
                rgba[target] = (byte)(((word >> 10) & 0x1F) * 255 / 31);
                rgba[target + 1] = (byte)(((word >> 5) & 0x1F) * 255 / 31);
                rgba[target + 2] = (byte)((word & 0x1F) * 255 / 31);
                rgba[target + 3] = (byte)((word & 0x8000) != 0 ? 255 : 0);
                break;
            default:
                throw new InvalidDataException($"TGA 不支持的色深 {bitsPerPixel}");
        }
    }

    private static void FlipVertically(byte[] rgba, int width, int height)
    {
        int stride = width * 4;
        var row = new byte[stride];
        for (int y = 0; y < height / 2; y++)
        {
            int top = y * stride;
            int bottom = (height - 1 - y) * stride;
            Buffer.BlockCopy(rgba, top, row, 0, stride);
            Buffer.BlockCopy(rgba, bottom, rgba, top, stride);
            Buffer.BlockCopy(row, 0, rgba, bottom, stride);
        }
    }
}
