using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WorldEdit.Core.Formats.W3x;

public sealed class W3eTerrain
{
    public readonly record struct Corner(ushort Height, ushort WaterAndEdge, byte TextureFlags, byte Variation, byte Misc)
    {
        public float HeightWorld => (Height - 8192f) / 512f;
        public float WaterWorld => ((WaterAndEdge & 0x3FFF) - 8192f) / 512f;
        public bool MapEdge => (WaterAndEdge & 0x4000) != 0;
        public int GroundTexture => TextureFlags & 0x0F;
        public bool Ramp => (TextureFlags & 0x10) != 0;
        public bool Blight => (TextureFlags & 0x20) != 0;
        public bool HasWater => (TextureFlags & 0x40) != 0;
        public bool Boundary => (TextureFlags & 0x80) != 0;
        public int VariationIndex => Variation & 0x1F;
        public int CliffVariation => (Variation & 0xE0) >> 5;
        public int CliffTexture => (Misc & 0xF0) >> 4;
        public int LayerHeight => Misc & 0x0F;
    }

    public int Version { get; private set; }
    public char Tileset { get; private set; }
    public bool HasCustomTileset { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public float OffsetX { get; private set; }
    public float OffsetY { get; private set; }
    public List<string> GroundTextures { get; } = new();
    public List<string> CliffTextures { get; } = new();
    public Corner[] Corners { get; private set; }
    public byte[] RawBytes { get; private set; }

    public static W3eTerrain Create(int width, int height, char tileset, IReadOnlyList<string> groundTextures,
        IReadOnlyList<string> fixedGroundOrder = null)
    {
        var terrain = new W3eTerrain
        {
            Synthetic = true,
            Version = 11,
            Tileset = tileset,
            Width = width,
            Height = height,
            Corners = new Corner[width * height],
            FixedGroundOrder = fixedGroundOrder,
        };
        foreach (string tileId in groundTextures)
        {
            terrain.GroundTextures.Add(tileId);
        }
        return terrain;
    }

    public bool Synthetic { get; private set; }

    public IReadOnlyList<string> FixedGroundOrder { get; private set; }

    public void SetCorner(int x, int y, Corner corner)
    {
        Corners[y * Width + x] = corner;
    }

    public static Corner FlatCorner(int texture, int variation = 0, int layer = 2, bool ramp = false, bool blight = false)
    {
        int flags = texture & 0x0F;
        if (ramp)
        {
            flags |= 0x10;
        }
        if (blight)
        {
            flags |= 0x20;
        }
        return new Corner(8192, 8192, (byte)flags, (byte)(variation & 0x1F), (byte)(layer & 0x0F));
    }

    public float HeightAt(int x, int y)
    {
        return Corners[y * Width + x].HeightWorld;
    }

    public static W3eTerrain Parse(byte[] data)
    {
        if (data.Length < 69 || Encoding.ASCII.GetString(data, 0, 4) != "W3E!")
        {
            throw new InvalidDataException("war3map.w3e 头不正确");
        }

        var terrain = new W3eTerrain
        {
            RawBytes = data,
            Version = BitConverter.ToInt32(data, 4),
            Tileset = (char)data[8],
            HasCustomTileset = BitConverter.ToInt32(data, 9) != 0,
        };

        int pos = 13;
        int textureCount = BitConverter.ToInt32(data, pos);
        pos += 4;
        for (int i = 0; i < textureCount; i++)
        {
            terrain.GroundTextures.Add(Encoding.ASCII.GetString(data, pos, 4));
            pos += 4;
        }

        int cliffCount = BitConverter.ToInt32(data, pos);
        pos += 4;
        for (int i = 0; i < cliffCount; i++)
        {
            terrain.CliffTextures.Add(Encoding.ASCII.GetString(data, pos, 4));
            pos += 4;
        }

        terrain.Width = BitConverter.ToInt32(data, pos);
        terrain.Height = BitConverter.ToInt32(data, pos + 4);
        terrain.OffsetX = BitConverter.ToSingle(data, pos + 8);
        terrain.OffsetY = BitConverter.ToSingle(data, pos + 12);
        pos += 16;

        int count = terrain.Width * terrain.Height;
        var corners = new Corner[count];
        for (int i = 0; i < count; i++)
        {
            corners[i] = new Corner(
                BitConverter.ToUInt16(data, pos),
                BitConverter.ToUInt16(data, pos + 2),
                data[pos + 4],
                data[pos + 5],
                data[pos + 6]);
            pos += 7;
        }
        terrain.Corners = corners;
        return terrain;
    }

    public byte[] ToBytes()
    {
        throw new NotSupportedException("war3map.w3e 写入尚未实现");
    }
}
