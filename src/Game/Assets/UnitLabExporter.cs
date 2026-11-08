using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.Slk;

namespace WorldEdit.Assets;

public static class UnitLabExporter
{
    private const int Columns = 8;

    private const float Spacing = 380f;

    private const int MinFamilySize = 4;
    private const int MiscGroupSize = 25;

    private sealed class UnitEntry
    {
        public string Id;
        public string Race;
        public string Class;
        public bool Building;
    }

    private sealed class UnitGroup
    {
        public string Key;
        public string Title;
        public List<UnitEntry> Units = new();
    }

    public static void Export(string sceneDirectory)
    {
        DirAccess.MakeDirRecursiveAbsolute(sceneDirectory);
        List<UnitGroup> groups = BuildGroups();

        int written = 0;
        int totalUnits = 0;
        foreach (UnitGroup group in groups)
        {
            if (ExportGroup(group, sceneDirectory))
            {
                written++;
                totalUnits += group.Units.Count;
            }
        }

        GD.Print($"[UNITLAB] {written}/{groups.Count} 个场景，{totalUnits} 个单元 → {sceneDirectory}");
    }

    private static List<UnitGroup> BuildGroups()
    {
        List<UnitEntry> entries = ReadEntries();
        var groups = new List<UnitGroup>();
        var byKey = new Dictionary<string, UnitGroup>(StringComparer.Ordinal);

        UnitGroup GroupFor(string key, string title)
        {
            if (!byKey.TryGetValue(key, out UnitGroup group))
            {
                group = new UnitGroup { Key = key, Title = title };
                byKey[key] = group;
                groups.Add(group);
            }
            return group;
        }

        var familySize = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (UnitEntry entry in entries)
        {
            if (entry.Race == "creeps" && !string.IsNullOrWhiteSpace(entry.Class))
            {
                familySize[entry.Class] = familySize.GetValueOrDefault(entry.Class) + 1;
            }
        }

        var misc = new List<UnitEntry>();
        foreach (UnitEntry entry in entries)
        {
            entry.Class = (entry.Class ?? "").Trim();
            switch (entry.Race)
            {
                case "human":
                case "orc":
                case "undead":
                case "nightelf":
                case "naga":
                case "demon":
                case "commoner":
                case "critters":
                    if (entry.Building)
                    {
                        GroupFor($"bldg_{entry.Race}", $"{entry.Race} buildings").Units.Add(entry);
                    }
                    else
                    {
                        GroupFor($"unit_{entry.Race}", $"{entry.Race} units").Units.Add(entry);
                    }
                    break;

                case "creeps":
                    if (!entry.Building && familySize.GetValueOrDefault(entry.Class) >= MinFamilySize)
                    {
                        GroupFor($"creep_{entry.Class}", $"creeps: {entry.Class}").Units.Add(entry);
                    }
                    else
                    {
                        misc.Add(entry);
                    }
                    break;

                default:
                    if (entry.Building)
                    {
                        GroupFor("bldg_other", "other buildings").Units.Add(entry);
                    }
                    else
                    {
                        GroupFor("unit_other", "other units").Units.Add(entry);
                    }
                    break;
            }
        }

        for (int i = 0; i < misc.Count; i += MiscGroupSize)
        {
            int index = i / MiscGroupSize + 1;
            List<UnitEntry> slice = misc.GetRange(i, Math.Min(MiscGroupSize, misc.Count - i));
            UnitGroup group = GroupFor($"creep_misc_{index}", $"creeps: misc {index}");
            group.Units.AddRange(slice);
        }

        foreach (UnitGroup group in groups)
        {
            group.Units.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }
        return groups;
    }

    private static List<UnitEntry> ReadEntries()
    {
        SlkTable ui = Generated.War3Tables.UnitUI();
        SlkTable data = Generated.War3Tables.UnitData();
        SlkTable balance = Generated.War3Tables.Get("UnitBalance");

        var entries = new List<UnitEntry>();
        foreach (int row in ui.DataRows)
        {
            string id = ui.Get(row, "unitUIID");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            int dataRow = data.FindRow("unitID", id);
            int balanceRow = balance.FindRow("unitBalanceID", id);
            entries.Add(new UnitEntry
            {
                Id = id,
                Race = dataRow >= 0 ? (data.Get(dataRow, "race") ?? "").Trim() : "",
                Class = ui.Get(row, "unitClass"),
                Building = balanceRow >= 0 && balance.Get(balanceRow, "isbldg") == "1",
            });
        }
        return entries;
    }

    private static bool ExportGroup(UnitGroup group, string sceneDirectory)
    {
        int rows = (group.Units.Count + Columns - 1) / Columns;
        int usedColumns = Math.Min(group.Units.Count, Columns);
        float width = usedColumns * Spacing;
        float depth = rows * Spacing;

        var root = new Node3D { Name = group.Key };
        root.SetMeta("map_width", Columns);
        root.SetMeta("map_height", rows);

        root.SetMeta("view_dist", Mathf.Max(Mathf.Max(width * 0.7f, depth * 1.15f), 700f));
        root.SetMeta("view_target", new Vector3(-width / 2f, 0f, depth / 2f));

        AddBackground(root, width, depth);

        int placed = 0;
        int missing = 0;
        var missingIds = new List<string>();

        for (int i = 0; i < group.Units.Count; i++)
        {
            UnitEntry entry = group.Units[i];
            UnitModel model = UnitMeshLibrary.Get(entry.Id, false);
            if (model?.Scene == null)
            {
                missing++;
                missingIds.Add(entry.Id);
                continue;
            }

            var instance = model.Scene.Instantiate<Node3D>();
            int column = i % Columns;
            int row = i / Columns;
            instance.Name = entry.Id;
            instance.Position = new Vector3(-(column + 0.5f) * Spacing, 0f, (row + 0.5f) * Spacing);
            instance.Scale = Vector3.One * model.Scale;

            instance.Rotation = new Vector3(0f, Mathf.Pi, 0f);
            ModelLibrary.ApplyMissingMaterials(instance, model.ScenePath);
            root.AddChild(instance);

            var label = new Label3D
            {
                Name = $"label_{entry.Id}",
                Text = string.IsNullOrEmpty(entry.Class) ? entry.Id : $"{entry.Id}  {entry.Class}",
                FontSize = 40,
                PixelSize = 0.32f,
                Modulate = new Color(1f, 1f, 1f),
                OutlineModulate = new Color(0f, 0f, 0f),
                OutlineSize = 12,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                Position = new Vector3(-(column + 0.5f) * Spacing, 120f, (row + 0.5f) * Spacing + 130f),
            };
            root.AddChild(label);
            placed++;
        }

        if (placed == 0)
        {
            GD.PushWarning($"[UNITLAB] {group.Key} 一个都没加载出来，跳过");
            root.QueueFree();
            return false;
        }

        SetOwner(root, root);
        var scene = new PackedScene();
        Error packed = scene.Pack(root);
        if (packed != Error.Ok)
        {
            GD.PushError($"[UNITLAB] {group.Key} 打包失败: {packed}");
            root.QueueFree();
            return false;
        }

        string path = $"{sceneDirectory}/{group.Key}.tscn";
        Error saved = ResourceSaver.Save(scene, path);
        root.QueueFree();
        if (saved != Error.Ok)
        {
            GD.PushError($"[UNITLAB] {group.Key} 保存失败: {saved}");
            return false;
        }

        string note = missing > 0 ? $"，缺 {missing}（{string.Join(",", missingIds)}）" : "";
        GD.Print($"[UNITLAB] {group.Key}：{placed} 个{note}");
        return true;
    }

    private static void AddBackground(Node3D root, float width, float depth)
    {
        var plane = new PlaneMesh { Size = new Vector2(width, depth) };
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.18f, 0.19f, 0.21f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        root.AddChild(new MeshInstance3D
        {
            Name = "Ground",
            Mesh = plane,
            MaterialOverride = material,
            Position = new Vector3(-width / 2f, -0.5f, depth / 2f),
        });
    }

    private static void SetOwner(Node node, Node owner)
    {
        foreach (Node child in node.GetChildren())
        {
            child.Owner = owner;
            if (child.SceneFilePath.Length == 0)
            {
                SetOwner(child, owner);
            }
        }
    }
}
