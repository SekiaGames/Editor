using Godot;
using System.Collections.Generic;
using WorldEdit.Assets;
using WorldEdit.Core.Formats.W3x;

namespace WorldEdit;

public partial class MinimapPanel : PanelContainer
{
    private const int MapTexSize = 256;

    private TextureButton _view;
    private TerrainTileSet _tiles;

    public MinimapPanel()
    {
        CustomMinimumSize = new Vector2(MapTexSize, MapTexSize);
        SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        MouseFilter = Control.MouseFilterEnum.Stop;

        var sb = new StyleBoxFlat
        {
            BgColor = Colors.Black,
            BorderColor = Color.FromHtml("d2d2d2"),
        };
        sb.SetBorderWidthAll(1);
        sb.SetCornerRadiusAll(0);

        sb.ContentMarginLeft = 1;
        sb.ContentMarginRight = 1;
        sb.ContentMarginTop = 1;
        sb.ContentMarginBottom = 1;
        AddThemeStyleboxOverride("panel", sb);
    }

    public override void _Ready()
    {
        _view = new TextureButton
        {
            TextureNormal = BuildPlaceholder(),
            StretchMode = TextureButton.StretchModeEnum.Scale,
            IgnoreTextureSize = false,
            TooltipText = "小地图（点击移动相机，打桩）",
            CustomMinimumSize = new Vector2(MapTexSize, MapTexSize),
        };
        _view.Pressed += () => Stub.Fire("minimap.click");
        AddChild(_view);
    }

    public void LoadMap(W3eTerrain terrain)
    {
        if (_view == null)
        {
            return;
        }

        _tiles ??= new TerrainTileSet();
        _view.TextureNormal = BuildFromTerrain(terrain, _tiles);
    }

    public static Texture2D BuildFromTerrain(W3eTerrain terrain, TerrainTileSet tiles)
    {
        int tilesX = terrain.Width - 1;
        int tilesY = terrain.Height - 1;
        var image = Image.CreateEmpty(MapTexSize, MapTexSize, false, Image.Format.Rgb8);
        image.Fill(Colors.Black);

        float scale = Mathf.Min(MapTexSize / (float)tilesX, MapTexSize / (float)tilesY);
        int drawW = Mathf.Max(1, (int)(tilesX * scale));
        int drawH = Mathf.Max(1, (int)(tilesY * scale));
        int offsetX = (MapTexSize - drawW) / 2;
        int offsetY = (MapTexSize - drawH) / 2;

        var palette = new Dictionary<string, Color>();
        Color AverageColor(string tileId)
        {
            if (palette.TryGetValue(tileId, out Color cached))
            {
                return cached;
            }
            Color color = new Color(0.3f, 0.4f, 0.25f);
            Image source = tiles.Get(tileId);
            if (source != null)
            {
                Image small = (Image)source.Duplicate();
                small.Resize(1, 1, Image.Interpolation.Bilinear);
                color = small.GetPixel(0, 0);
            }
            palette[tileId] = color;
            return color;
        }

        var water = new Color(0.12f, 0.28f, 0.45f);
        var counts = new Dictionary<string, int>();

        for (int py = 0; py < drawH; py++)
        {

            int ty = (drawH - 1 - py) * tilesY / drawH;
            if (ty >= tilesY)
            {
                ty = tilesY - 1;
            }
            for (int px = 0; px < drawW; px++)
            {
                int tx = px * tilesX / drawW;

                W3eTerrain.Corner c00 = terrain.Corners[ty * terrain.Width + tx];
                W3eTerrain.Corner c10 = terrain.Corners[ty * terrain.Width + tx + 1];
                W3eTerrain.Corner c01 = terrain.Corners[(ty + 1) * terrain.Width + tx];
                W3eTerrain.Corner c11 = terrain.Corners[(ty + 1) * terrain.Width + tx + 1];

                int wet = (c00.HasWater ? 1 : 0) + (c10.HasWater ? 1 : 0) + (c01.HasWater ? 1 : 0) + (c11.HasWater ? 1 : 0);
                Color color;
                if (wet >= 3)
                {
                    color = water;
                }
                else
                {
                    counts.Clear();
                    Count(counts, terrain, c00.GroundTexture);
                    Count(counts, terrain, c10.GroundTexture);
                    Count(counts, terrain, c01.GroundTexture);
                    Count(counts, terrain, c11.GroundTexture);

                    string best = null;
                    int bestCount = 0;
                    foreach (var pair in counts)
                    {
                        if (pair.Value > bestCount)
                        {
                            bestCount = pair.Value;
                            best = pair.Key;
                        }
                    }
                    color = best != null ? AverageColor(best) : water;
                }

                image.SetPixel(offsetX + px, offsetY + py, color);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    private static void Count(Dictionary<string, int> counts, W3eTerrain terrain, int groundTexture)
    {
        if (groundTexture < 0 || groundTexture >= terrain.GroundTextures.Count)
        {
            return;
        }
        string id = terrain.GroundTextures[groundTexture];
        counts[id] = counts.GetValueOrDefault(id) + 1;
    }

    private static Texture2D BuildPlaceholder()
    {
        var img = Image.CreateEmpty(MapTexSize, MapTexSize, false, Image.Format.Rgb8);
        float world = ViewportPanel.Cells * ViewportPanel.CellSize;

        for (int y = 0; y < MapTexSize; y++)
        {
            for (int x = 0; x < MapTexSize; x++)
            {

                float wx = x / (float)MapTexSize * world;
                float wz = (MapTexSize - 1 - y) / (float)MapTexSize * world;
                float h = ViewportPanel.HeightAt(wx, wz);

                Color c;
                if (h < ViewportPanel.WaterLevel)
                {
                    float depth = Mathf.Clamp((ViewportPanel.WaterLevel - h) / 8f, 0f, 1f);
                    c = new Color(0.10f + 0.06f * depth, 0.22f + 0.08f * depth, 0.34f + 0.10f * depth);
                }
                else
                {
                    float t = Mathf.Clamp((h - ViewportPanel.WaterLevel) / 22f, 0f, 1f);
                    c = new Color(0.17f + 0.13f * t, 0.26f + 0.10f * t, 0.14f + 0.05f * t);
                }
                img.SetPixel(x, y, c);
            }
        }

        var gridColor = new Color(0, 0, 0, 0.18f);
        for (int i = 1; i < 8; i++)
        {
            int p = i * MapTexSize / 8;
            for (int k = 0; k < MapTexSize; k++)
            {
                img.SetPixel(p, k, img.GetPixel(p, k).Blend(gridColor));
                img.SetPixel(k, p, img.GetPixel(k, p).Blend(gridColor));
            }
        }

        return ImageTexture.CreateFromImage(img);
    }
}
