using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.Slk;

namespace WorldEdit.Assets;

public sealed class UnitModel
{
    public PackedScene Scene;
    public string ScenePath = "";
    public float Scale = 1f;
}

public static class UnitMeshLibrary
{
    private static readonly Dictionary<string, UnitModel> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static int Count => Cache.Count;

    public static UnitModel Get(string id, bool isItem)
    {
        string key = (isItem ? "item:" : "unit:") + id;
        if (Cache.TryGetValue(key, out UnitModel cached))
        {
            return cached;
        }

        UnitModel model = Load(id, isItem);
        Cache[key] = model;
        return model;
    }

    private static UnitModel Load(string id, bool isItem)
    {
        SlkTable table = isItem ? Generated.War3Tables.ItemData() : Generated.War3Tables.UnitUI();
        int row = table.FindRow(isItem ? "itemID" : "unitUIID", id);
        if (row < 0)
        {
            GD.PushWarning($"[UNIT] {(isItem ? "ItemData" : "UnitUI")} 里没有 {id}");
            return null;
        }

        string file = table.Get(row, "file");
        if (string.IsNullOrWhiteSpace(file))
        {
            GD.PushWarning($"[UNIT] {id} 的 file 列是空的");
            return null;
        }

        string path = ModelLibrary.Find(file);
        if (path == null)
        {
            GD.PushWarning($"[UNIT] 缺少模型 {id} -> {file}");
            return null;
        }

        var scene = GD.Load<PackedScene>(path);
        if (scene == null)
        {
            GD.PushWarning($"[UNIT] 加载失败 {path}");
            return null;
        }

        float scale = 1f;
        string scaleText = table.Get(row, isItem ? "scale" : "modelScale");
        if (!string.IsNullOrWhiteSpace(scaleText)
            && float.TryParse(scaleText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float parsed)
            && parsed > 0f)
        {
            scale = parsed;
        }

        return new UnitModel { Scene = scene, ScenePath = path, Scale = scale };
    }
}
