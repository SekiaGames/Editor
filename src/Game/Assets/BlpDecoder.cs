using Godot;
using System;
using System.IO;

namespace WorldEdit.Assets;

public static class BlpDecoder
{
    public static Image Decode(byte[] data)
    {
        if (data.Length < 156 || data[0] != (byte)'B' || data[1] != (byte)'L' || data[2] != (byte)'P')
        {
            throw new InvalidDataException("不是 BLP 文件");
        }

        return data[3] == (byte)'2' ? DecodeBlp2(data) : DecodeBlp1(data);
    }

    private static Image DecodeBlp1(byte[] data)
    {
        uint compression = BitConverter.ToUInt32(data, 4);
        uint alphaDepth = BitConverter.ToUInt32(data, 8);
        int width = (int)BitConverter.ToUInt32(data, 12);
        int height = (int)BitConverter.ToUInt32(data, 16);

        uint mipOffset = BitConverter.ToUInt32(data, 28);
        uint mipSize = BitConverter.ToUInt32(data, 92);
        if (mipSize == 0 || mipOffset + mipSize > data.Length)
        {
            throw new InvalidDataException($"BLP1 mip0 越界 offset={mipOffset} size={mipSize} len={data.Length}");
        }

        return compression == 0
            ? DecodeJpegMip(data, (int)mipOffset, (int)mipSize, width, height)
            : DecodePalettedMip(data, 156, (int)mipOffset, (int)mipSize, width, height, (int)alphaDepth);
    }

    private static Image DecodeJpegMip(byte[] data, int mipOffset, int mipSize, int width, int height)
    {
        int headerSize = BitConverter.ToInt32(data, 156);
        if (headerSize <= 0 || headerSize > 4096 || 160 + headerSize > data.Length)
        {
            throw new InvalidDataException($"BLP1 JPEG 头长度异常 {headerSize}");
        }

        var jpeg = new byte[headerSize + mipSize];
        Buffer.BlockCopy(data, 160, jpeg, 0, headerSize);
        Buffer.BlockCopy(data, mipOffset, jpeg, headerSize, mipSize);

        byte[] rgba = JpegBaselineDecoder.Decode(jpeg, out int decodedWidth, out int decodedHeight);
        Image image = Image.CreateFromData(decodedWidth, decodedHeight, false, Image.Format.Rgba8, rgba);
        if (decodedWidth != width || decodedHeight != height)
        {
            image.Resize(width, height, Image.Interpolation.Bilinear);
        }
        return image;
    }

    private static Image DecodePalettedMip(byte[] data, int paletteOffset, int indicesOffset, int size, int width, int height, int alphaDepth)
    {
        int pixels = width * height;
        if (size < pixels || indicesOffset + pixels > data.Length)
        {
            throw new InvalidDataException($"BLP1 调色板数据不足 size={size} 需要={pixels}");
        }

        var rgba = new byte[pixels * 4];
        for (int i = 0; i < pixels; i++)
        {
            int entry = paletteOffset + data[indicesOffset + i] * 4;
            rgba[i * 4] = data[entry + 2];
            rgba[i * 4 + 1] = data[entry + 1];
            rgba[i * 4 + 2] = data[entry];
            rgba[i * 4 + 3] = 255;
        }

        if (alphaDepth > 0 && alphaDepth is 1 or 4 or 8)
        {
            int alphaOffset = indicesOffset + pixels;
            int available = Math.Min(size - pixels, data.Length - alphaOffset);
            ApplyAlpha(rgba, data, alphaOffset, available, width, height, alphaDepth);
        }

        return Image.CreateFromData(width, height, false, Image.Format.Rgba8, rgba);
    }

    private static void ApplyAlpha(byte[] rgba, byte[] data, int offset, int available, int width, int height, int bits)
    {
        int pixels = width * height;
        if (bits == 8)
        {
            if (available < pixels)
            {
                return;
            }
            for (int i = 0; i < pixels; i++)
            {
                rgba[i * 4 + 3] = data[offset + i];
            }
            return;
        }

        int perByte = 8 / bits;
        int mask = (1 << bits) - 1;
        int scale = 255 / mask;
        for (int i = 0; i < pixels; i++)
        {
            int byteIndex = offset + i / perByte;
            if (byteIndex - offset >= available)
            {
                return;
            }
            int shift = 8 - bits * (i % perByte + 1);
            int value = (data[byteIndex] >> shift) & mask;
            rgba[i * 4 + 3] = (byte)(value * scale);
        }
    }

    private static Image DecodeBlp2(byte[] data)
    {
        int compression = data[8];
        int alphaDepth = data[9];
        int width = (int)BitConverter.ToUInt32(data, 12);
        int height = (int)BitConverter.ToUInt32(data, 16);
        uint mipOffset = BitConverter.ToUInt32(data, 20);
        uint mipSize = BitConverter.ToUInt32(data, 84);

        if (compression != 1)
        {
            throw new InvalidDataException($"BLP2 压缩类型 {compression} 暂不支持（只做了调色板）");
        }

        return DecodePalettedMip(data, (int)mipOffset, (int)mipOffset + 1024, (int)mipSize, width, height, alphaDepth);
    }
}
