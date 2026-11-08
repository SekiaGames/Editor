using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.Slk;

namespace WorldEdit.Assets;

public sealed class DoodadDefinition
{
    public string File = "";
    public string TextureFile = "";
    public int Variations = 1;
    public float BaseScale = 1f;
    public bool IsDestructable;
}

public sealed class DoodadModel
{
    public PackedScene Scene;
    public string ScenePath = "";
    public float BaseScale = 1f;
    public string TextureFile = "";
}

public static class DoodadMeshLibrary
{
    private static readonly Dictionary<string, DoodadModel> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, DoodadDefinition> Definitions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> TextureApplied = new(StringComparer.OrdinalIgnoreCase);

    public static int Count => Cache.Count;

    public static DoodadDefinition Describe(string id)
    {
        if (Definitions.TryGetValue(id, out DoodadDefinition cached))
        {
            return cached;
        }

        DoodadDefinition definition = Resolve(id);
        Definitions[id] = definition;
        return definition;
    }

    public static DoodadModel Get(string id, uint variation)
    {
        string key = id + ":" + variation;
        if (Cache.TryGetValue(key, out DoodadModel cached))
        {
            return cached;
        }

        DoodadModel model = Load(id, variation);
        Cache[key] = model;
        return model;
    }

    public static int ApplyReplaceableTexture(Node instance, DoodadModel model)
    {
        if (string.IsNullOrEmpty(model.TextureFile) || !TextureApplied.Add(model.ScenePath))
        {
            return 0;
        }

        string path = ModelLibrary.FindImage(model.TextureFile);
        if (path == null)
        {
            GD.PushWarning($"[DOODAD] 缺少替换贴图 {model.TextureFile}");
            return 0;
        }

        var texture = GD.Load<Texture2D>(path);
        if (texture == null)
        {
            GD.PushWarning($"[DOODAD] 贴图加载失败 {path}");
            return 0;
        }

        int changed = 0;
        foreach (Node node in instance.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh == null)
            {
                continue;
            }
            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                Material material = mesh.MaterialOverride
                    ?? mesh.GetSurfaceOverrideMaterial(surface)
                    ?? mesh.Mesh.SurfaceGetMaterial(surface);
                if (material is not ShaderMaterial shader)
                {
                    continue;
                }
                if (shader.GetShaderParameter("use_base_texture").AsBool())
                {
                    continue;
                }
                shader.SetShaderParameter("base_texture", texture);
                shader.SetShaderParameter("use_base_texture", true);
                changed++;
            }
        }
        return changed;
    }

    private static DoodadDefinition Resolve(string id)
    {
        SlkTable destructables = Generated.War3Tables.DestructableData();
        int row = destructables.FindRow("DestructableID", id);
        if (row >= 0)
        {
            var definition = new DoodadDefinition
            {
                File = destructables.Get(row, "file"),
                TextureFile = destructables.Get(row, "texFile"),
                IsDestructable = true,
            };
            definition.Variations = ParseInt(destructables.Get(row, "numVar"), 1);
            if (definition.TextureFile == "_")
            {
                definition.TextureFile = "";
            }
            return definition;
        }

        SlkTable doodads = Generated.War3Tables.Doodads();
        row = doodads.FindRow("doodID", id);
        if (row < 0)
        {
            return null;
        }

        var doodad = new DoodadDefinition
        {
            File = doodads.Get(row, "file"),
            BaseScale = ParseFloat(doodads.Get(row, "defScale"), 1f),
        };
        doodad.Variations = ParseInt(doodads.Get(row, "numVar"), 1);
        return doodad;
    }

    private static DoodadModel Load(string id, uint variation)
    {
        DoodadDefinition definition = Describe(id);
        if (definition == null)
        {
            GD.PushWarning($"[DOODAD] DestructableData / Doodads 里都没有 {id}");
            return null;
        }
        if (string.IsNullOrWhiteSpace(definition.File))
        {
            GD.PushWarning($"[DOODAD] {id} 的 file 列是空的");
            return null;
        }

        string path = null;
        if (variation < definition.Variations)
        {
            path = ModelLibrary.Find(definition.File + variation);
        }
        path ??= ModelLibrary.Find(definition.File);
        path ??= ModelLibrary.Find(definition.File + "0");
        if (path == null)
        {
            GD.PushWarning($"[DOODAD] 缺少模型 {id} -> {definition.File}{variation}");
            return null;
        }

        var scene = GD.Load<PackedScene>(path);
        if (scene == null)
        {
            GD.PushWarning($"[DOODAD] 加载失败 {path}");
            return null;
        }

        return new DoodadModel
        {
            Scene = scene,
            ScenePath = path,
            BaseScale = definition.BaseScale > 0f ? definition.BaseScale : 1f,
            TextureFile = definition.TextureFile,
        };
    }

    private static int ParseInt(string text, int fallback)
    {
        return int.TryParse(text, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out int value) && value > 0 ? value : fallback;
    }

    private static float ParseFloat(string text, float fallback)
    {
        return float.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float value) && value > 0f ? value : fallback;
    }
}
