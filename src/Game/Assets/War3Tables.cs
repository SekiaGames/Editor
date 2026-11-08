using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using WorldEdit.Core.Formats.Slk;

namespace WorldEdit.Assets.Generated;

public static class War3Tables
{
    private const string Root = "res://assets/converted/Tables/";

    private static readonly Dictionary<string, SlkTable> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static SlkTable Get(string name)
    {
        if (Cache.TryGetValue(name, out SlkTable cached))
        {
            return cached;
        }

        SlkTable table = Load(name);
        Cache[name] = table;
        return table;
    }

    public static IReadOnlyList<string> Names()
    {
        var names = new List<string>();
        foreach (string file in DirAccess.GetFilesAt(Root))
        {
            if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                names.Add(file[..^5]);
            }
        }
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static SlkTable Load(string name)
    {
        string path = Root + name + ".json";
        using Godot.FileAccess file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"[TABLE] 打不开 {path}：先跑 --table-export，或确认解压树已生成");
            return SlkTable.FromRows(Array.Empty<string>(), Array.Empty<string[]>());
        }

        try
        {
            return Parse(name, file.GetAsText());
        }
        catch (Exception error)
        {
            GD.PushError($"[TABLE] 解析 {path} 失败: {error.Message}");
            return SlkTable.FromRows(Array.Empty<string>(), Array.Empty<string[]>());
        }
    }

    private static SlkTable Parse(string name, string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        var columns = new List<string>();
        if (root.TryGetProperty("columns", out JsonElement columnArray))
        {
            foreach (JsonElement column in columnArray.EnumerateArray())
            {
                columns.Add(column.GetString() ?? "");
            }
        }

        var rows = new List<string[]>();
        if (root.TryGetProperty("rows", out JsonElement rowArray))
        {
            foreach (JsonElement row in rowArray.EnumerateArray())
            {
                var cells = new string[row.GetArrayLength()];
                int index = 0;
                foreach (JsonElement cell in row.EnumerateArray())
                {
                    cells[index++] = cell.GetString() ?? "";
                }
                rows.Add(cells);
            }
        }

        if (columns.Count == 0)
        {
            GD.PushWarning($"[TABLE] {name}.json 没有列定义");
        }
        return SlkTable.FromRows(columns.ToArray(), rows.ToArray());
    }

    public static SlkTable Terrain() => Get("Terrain");

    public static SlkTable CliffTypes() => Get("CliffTypes");

    public static SlkTable Water() => Get("Water");

    public static SlkTable UnitData() => Get("UnitData");

    public static SlkTable UnitUI() => Get("UnitUI");

    public static SlkTable ItemData() => Get("ItemData");

    public static SlkTable Doodads() => Get("Doodads");

    public static SlkTable DestructableData() => Get("DestructableData");
}
