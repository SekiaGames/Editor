using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.Slk;

namespace WorldEdit.Assets;

public sealed class WaterTileSet
{

    public float HeightOffset { get; private set; }
    public int FrameCount { get; private set; }
    public int FrameRate { get; private set; }
    public Color ShallowMin { get; private set; } = new Color(1f, 1f, 1f, 1f);
    public Color ShallowMax { get; private set; } = new Color(1f, 1f, 1f, 1f);
    public Color DeepMin { get; private set; } = new Color(1f, 1f, 1f, 1f);
    public Color DeepMax { get; private set; } = new Color(1f, 1f, 1f, 1f);
    public Texture2DArray Texture { get; private set; }

    public static WaterTileSet Load(char tileset)
    {
        SlkTable table = Generated.War3Tables.Water();
        string rowId = tileset + "Sha";
        int row = table.FindRow("waterID", rowId);
        if (row < 0)
        {
            GD.PushWarning($"[WATER] Water.slk 里没有 {rowId}");
            return null;
        }

        var water = new WaterTileSet
        {
            HeightOffset = table.GetFloat(row, "height"),
            FrameCount = Mathf.Max(0, table.GetInt(row, "numTex")),
            FrameRate = Mathf.Max(1, table.GetInt(row, "texRate")),
            ShallowMin = ReadColor(table, row, "Smin"),
            ShallowMax = ReadColor(table, row, "Smax"),
            DeepMin = ReadColor(table, row, "Dmin"),
            DeepMax = ReadColor(table, row, "Dmax"),
        };

        water.Texture = BuildTexture(table.Get(row, "texFile"), water.FrameCount);
        return water;
    }

    private static Color ReadColor(SlkTable table, int row, string prefix)
    {
        return new Color(
            table.GetInt(row, prefix + "_R") / 255f,
            table.GetInt(row, prefix + "_G") / 255f,
            table.GetInt(row, prefix + "_B") / 255f,
            table.GetInt(row, prefix + "_A") / 255f);
    }

    private static Texture2DArray BuildTexture(string texFile, int frameCount)
    {
        if (string.IsNullOrEmpty(texFile) || frameCount <= 0)
        {
            return null;
        }

        string prefix = texFile.Replace('\\', '/');
        var images = new Godot.Collections.Array<Image>();
        for (int i = 0; i < frameCount; i++)
        {
            string path = $"{prefix}{(i < 10 ? "0" : "")}{i}.blp";
            if (!War3Assets.Exists(path))
            {
                GD.PushWarning($"[WATER] 缺少波纹贴图 {path}");
                continue;
            }
            Image image = War3Assets.LoadImage(path);
            if (image.GetFormat() != Image.Format.Rgba8)
            {
                image.Convert(Image.Format.Rgba8);
            }
            image.GenerateMipmaps();
            images.Add(image);
        }

        if (images.Count == 0)
        {
            return null;
        }

        var texture = new Texture2DArray();
        texture.CreateFromImages(images);
        return texture;
    }
}
