using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.Slk;

namespace WorldEdit.Assets;

public sealed class TerrainTileSet
{

    public const string BlightId = "@blight";

    private static string _blightPath = "TerrainArt/Blight/Ashen_Blight.blp";

    public static void SetBlightTileset(char tileset)
    {
        _blightPath = tileset switch
        {
            'A' => "TerrainArt/Blight/Ashen_Blight.blp",
            'B' => "TerrainArt/Blight/Barrens_Blight.blp",
            'D' => "TerrainArt/Blight/Cave_Blight.blp",
            'F' => "TerrainArt/Blight/Felwood_Blight.blp",
            'G' => "TerrainArt/Blight/G_Blight.blp",
            'L' => "TerrainArt/Blight/Lords_Blight.blp",
            'N' => "TerrainArt/Blight/North_Blight.blp",
            'X' => "TerrainArt/Blight/Village_Blight.blp",
            'Y' => "TerrainArt/Blight/VillageFall_Blight.blp",
            'Z' => "TerrainArt/Blight/Lordf_Blight.blp",
            _ => "TerrainArt/Blight/Ashen_Blight.blp",
        };
    }

    private static SlkTable _slk;
    private readonly Dictionary<string, Image> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);

    private static SlkTable Table => _slk ??= Generated.War3Tables.Terrain();

    public static string PathFor(string tileId)
    {
        int row = Table.FindRow("tileID", tileId);
        if (row < 0)
        {
            return null;
        }
        string dir = Table.Get(row, "dir");
        string file = Table.Get(row, "file");
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(file))
        {
            return null;
        }
        return dir.Replace('\\', '/') + "/" + file + ".blp";
    }

    public static string NameFor(string tileId)
    {
        int row = Table.FindRow("tileID", tileId);
        return row < 0 ? tileId : Table.Get(row, "comment") ?? tileId;
    }

    public Image Get(string tileId)
    {
        if (_images.TryGetValue(tileId, out Image cached))
        {
            return cached;
        }

        string relative = tileId == BlightId ? _blightPath : PathFor(tileId);
        if (relative == null || !War3Assets.Exists(relative))
        {
            GD.PushWarning($"[TEX] 找不到瓦片贴图: {tileId} -> {relative}");
            return null;
        }

        Image image = War3Assets.LoadImage(relative);
        _images[tileId] = image;
        _paths[tileId] = relative;
        return image;
    }

    public string PathOf(string tileId)
    {
        Get(tileId);
        return _paths.GetValueOrDefault(tileId);
    }
}
