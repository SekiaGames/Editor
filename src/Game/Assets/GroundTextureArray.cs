using Godot;
using System;
using System.Collections.Generic;

namespace WorldEdit.Assets;

public sealed class GroundTextureArray
{
    public const int LayersPerTile = 32;

    public Texture2DArray Texture { get; private set; }

    public Dictionary<string, int> BaseLayer { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static GroundTextureArray Build(TerrainTileSet tiles, IReadOnlyList<string> tileIds, List<string> used)
    {
        var array = new GroundTextureArray();
        var images = new Godot.Collections.Array<Image>();
        int tileSize = 0;

        foreach (string id in tileIds)
        {
            Image source = tiles.Get(id);
            if (source == null)
            {
                continue;
            }

            int size = source.GetHeight() / 4;
            int columns = size > 0 ? source.GetWidth() / size : 0;
            if (size <= 0 || columns < 4)
            {
                GD.PushWarning($"[TEX] 地表贴图布局异常，跳过 {id}: {source.GetWidth()}x{source.GetHeight()}");
                continue;
            }
            if (tileSize == 0)
            {
                tileSize = size;
            }
            if (size != tileSize || source.GetHeight() % 4 != 0)
            {
                GD.PushWarning($"[TEX] 瓦片尺寸不一致，跳过 {id}: {size}x{size} vs {tileSize}x{tileSize}");
                continue;
            }

            if (images.Count == 0)
            {

                tileSize = size;
                Image blankCell = Image.CreateEmpty(tileSize, tileSize, false, Image.Format.Rgba8);
                blankCell.Fill(new Color(0f, 0f, 0f, 0f));

                blankCell.GenerateMipmaps();
                images.Add(blankCell);
            }

            int baseLayer = images.Count;
            for (int i = 0; i < LayersPerTile; i++)
            {

                int cell = i % 16;
                int cx = cell % 4;
                int cy = cell / 4;
                if (columns >= 8 && i >= 16)
                {
                    cx += 4;
                }
                Image cellImage = Slice(source, cx * tileSize, cy * tileSize, tileSize);
                cellImage.GenerateMipmaps();
                images.Add(cellImage);
            }

            array.BaseLayer[id] = baseLayer;
            used.Add(id);
        }

        if (images.Count <= 1)
        {
            return null;
        }

        array.Texture = new Texture2DArray();
        Error error = array.Texture.CreateFromImages(images);
        if (error != Error.Ok)
        {
            GD.PushError($"[TEX] Texture2DArray 创建失败: {error}（{images.Count} 层，{tileSize}x{tileSize}）");
            return null;
        }
        return array;
    }

    public static int VariationLayer(int variation)
    {
        if (variation <= 15)
        {
            return 16 + variation;
        }
        return variation == 16 ? 15 : 0;
    }

    public int LayerOf(string tileId, int variation)
    {
        if (!BaseLayer.TryGetValue(tileId, out int baseLayer))
        {
            return 0;
        }
        return baseLayer + VariationLayer(variation);
    }

    public const int BlankLayer = 0;

    private static Image Slice(Image source, int x, int y, int size)
    {
        var cell = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        cell.BlitRect(source, new Rect2I(x, y, size, size), Vector2I.Zero);
        return cell;
    }
}
