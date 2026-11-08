using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.Slk;

namespace WorldEdit.Assets;

public sealed class CliffTextureSet
{

    private static SlkTable _table;
    private readonly Dictionary<string, int> _layer = new(StringComparer.OrdinalIgnoreCase);

    private static SlkTable Table => _table ??= Generated.War3Tables.CliffTypes();

    public Texture2DArray Texture { get; private set; }
    public int Count => _layer.Count;

    public static CliffTextureSet Build(IReadOnlyList<string> cliffIds)
    {
        var set = new CliffTextureSet();
        var images = new Godot.Collections.Array<Image>();
        int width = 0;
        int height = 0;

        foreach (string id in cliffIds)
        {
            string relative = PathFor(id);
            if (relative == null)
            {
                GD.PushWarning($"[CLIFF] CliffTypes.slk 里找不到 {id}");
                continue;
            }
            Image image;
            try
            {
                image = War3Assets.LoadImage(relative);
            }
            catch (Exception e)
            {
                GD.PushWarning($"[CLIFF] 载入 {relative} 失败: {e.Message}");
                continue;
            }

            if (images.Count == 0)
            {
                width = image.GetWidth();
                height = image.GetHeight();
            }
            if (image.GetWidth() != width || image.GetHeight() != height)
            {
                GD.PushWarning($"[CLIFF] 尺寸不一致，跳过 {id}: {image.GetWidth()}x{image.GetHeight()}");
                continue;
            }
            if (image.GetFormat() != Image.Format.Rgba8)
            {
                image.Convert(Image.Format.Rgba8);
            }
            image.GenerateMipmaps();
            set._layer[id] = images.Count;
            images.Add(image);
        }

        if (images.Count == 0)
        {
            return null;
        }

        set.Texture = new Texture2DArray();
        set.Texture.CreateFromImages(images);
        return set;
    }

    public static string PathFor(string cliffId)
    {
        int row = Table.FindRow("cliffID", cliffId);
        if (row < 0)
        {
            return null;
        }
        string dir = Table.Get(row, "texDir");
        string file = Table.Get(row, "texFile");
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(file))
        {
            return null;
        }
        return dir.Replace('\\', '/') + "/" + file + ".blp";
    }

    public static string GroundTileFor(string cliffId)
    {
        int row = Table.FindRow("cliffID", cliffId);
        return row < 0 ? null : Table.Get(row, "groundTile");
    }

    public int LayerOf(string cliffId)
    {
        return cliffId != null && _layer.TryGetValue(cliffId, out int layer) ? layer : 0;
    }
}
