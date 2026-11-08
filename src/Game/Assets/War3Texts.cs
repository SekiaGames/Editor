using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace WorldEdit.Assets.Generated;

public static class War3Texts
{
    private const string Root = "res://extract/Texts/";

    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string> Get(string name)
    {
        if (Cache.TryGetValue(name, out IReadOnlyDictionary<string, string> cached))
        {
            return cached;
        }

        IReadOnlyDictionary<string, string> map = Load(name);
        Cache[name] = map;
        return map;
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

    private static IReadOnlyDictionary<string, string> Load(string name)
    {
        string path = Root + name + ".json";
        using Godot.FileAccess file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushError($"[TEXT] 打不开 {path}：先跑 --text-export，或确认解压树已生成");
            return new Dictionary<string, string>();
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(file.GetAsText());
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (document.RootElement.TryGetProperty("keys", out JsonElement keys))
            {
                foreach (JsonProperty pair in keys.EnumerateObject())
                {
                    map[pair.Name] = pair.Value.GetString() ?? "";
                }
            }
            return map;
        }
        catch (Exception error)
        {
            GD.PushError($"[TEXT] 解析 {path} 失败: {error.Message}");
            return new Dictionary<string, string>();
        }
    }
}
