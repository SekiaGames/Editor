using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace WorldEdit.Assets;

public static class TerrainLabExporter
{
    private const string DemoShaderRoot = "res://Demo/shader/";

    private const int UnitSpan = 3;

    private static readonly string[] Tiles = { "Ldrt", "Ldro", "Ldrg", "Lrok", "Lgrs", "Lgrd" };

    private static readonly string[] AtlasTiles = { "Ldrt", "Lrok", "Lgrs", "Lgrd" };

    private const int Dirt = 0;
    private const int Rock = 3;
    private const int Grass = 4;
    private const int DarkGrass = 5;

    private sealed class LabUnit
    {
        public string Key;
        public string Title;
        public Func<int, int, int> TileAt = (x, y) => Grass;
        public Func<int, int, int> VariationAt = (x, y) => 0;
    }

    public static void Export(string sceneDirectory)
    {
        DirAccess.MakeDirRecursiveAbsolute(sceneDirectory);
        DirAccess.MakeDirRecursiveAbsolute(sceneDirectory + "/shared");

        List<LabUnit> units = BuildUnits();
        string sharedMaterialPath = null;
        int written = 0;

        foreach (LabUnit unit in units)
        {
            W3eTerrain terrain = BuildTerrain(unit);
            var view = new ViewportPanel();
            Node3D root = view.BuildMapTree(terrain, null, false, null, null);
            root.Name = unit.Key;
            root.SetMeta("map_width", UnitSpan);
            root.SetMeta("map_height", UnitSpan);
            root.SetMeta("view_dist", ViewportPanel.CellSize * 5f);
            root.SetMeta("view_target", new Vector3(
                -ViewportPanel.CellSize * UnitSpan / 2f, 0f, ViewportPanel.CellSize * UnitSpan / 2f));

            if (sharedMaterialPath == null)
            {
                sharedMaterialPath = ExportSharedGround(root, sceneDirectory + "/shared");
                DumpLayerCombinations(root, unit);
            }
            else
            {
                ApplySharedGround(root, sharedMaterialPath);
            }

            AddFrameAndLabel(root, unit);

            SetOwner(root, root);
            var scene = new PackedScene();
            Error packed = scene.Pack(root);
            if (packed != Error.Ok)
            {
                GD.PushError($"[ARTLAB] {unit.Key} 打包失败: {packed}");
                root.QueueFree();
                view.QueueFree();
                continue;
            }

            string scenePath = $"{sceneDirectory}/{unit.Key}.tscn";
            Error saved = ResourceSaver.Save(scene, scenePath);
            if (saved == Error.Ok)
            {
                written++;
            }
            else
            {
                GD.PushError($"[ARTLAB] {unit.Key} 保存失败: {saved}");
            }

            root.QueueFree();
            view.QueueFree();
        }

        GD.Print($"[ARTLAB] {written}/{units.Count} 个场景 → {sceneDirectory}（共享材质 {sharedMaterialPath}）");
    }

    private static W3eTerrain BuildTerrain(LabUnit unit)
    {
        W3eTerrain terrain = W3eTerrain.Create(UnitSpan + 1, UnitSpan + 1, 'L', Tiles, AtlasTiles);
        for (int y = 0; y <= UnitSpan; y++)
        {
            for (int x = 0; x <= UnitSpan; x++)
            {
                terrain.SetCorner(x, y, W3eTerrain.FlatCorner(unit.TileAt(x, y), unit.VariationAt(x, y)));
            }
        }
        return terrain;
    }

    private static List<LabUnit> BuildUnits()
    {
        var units = new List<LabUnit>
        {
            Solid("pure_dirt", "pure dirt (control)", Dirt),
            Solid("pure_grass", "pure grass (control)", Grass),
        };

        for (int mask = 0; mask < 16; mask++)
        {
            int bits = mask;
            units.Add(new LabUnit
            {
                Key = $"mask_{mask:00}_{MaskSlug(bits)}",
                Title = $"mask {mask:00} {MaskLabel(bits)}",
                TileAt = (x, y) => CenterBit(bits, x, y) ? Dirt : Grass,
            });
        }

        foreach ((string key, string title, int cellX, int cellY) in RingDirections)
        {
            int targetX = cellX;
            int targetY = cellY;
            units.Add(new LabUnit
            {
                Key = key,
                Title = title,
                TileAt = (x, y) => InCell(x, y, 1, 1) || InCell(x, y, targetX, targetY) ? Dirt : Grass,
            });
        }

        units.Add(Shape("block_plus", "block plus (4 edges)", (1, 1), (1, 0), (1, 2), (0, 1), (2, 1)));
        units.Add(Shape("block_diamond", "block diamond (4 corners)", (1, 1), (0, 0), (2, 0), (0, 2), (2, 2)));
        units.Add(Shape("block_2x2_corner", "block 2x2 corner", (0, 0), (1, 0), (0, 1), (1, 1)));
        units.Add(Shape("block_line3", "block line 3", (0, 1), (1, 1), (2, 1)));

        units.Add(new LabUnit
        {
            Key = "tri_3dirt_1rock",
            Title = "tri 3 dirt + 1 rock",
            TileAt = (x, y) => CornerEquals(x, y, 2, 1) ? Rock : Dirt,
        });
        units.Add(new LabUnit
        {
            Key = "tri_3rock_1dirt",
            Title = "tri 3 rock + 1 dirt",
            TileAt = (x, y) => CornerEquals(x, y, 1, 1) ? Dirt : Rock,
        });
        units.Add(new LabUnit
        {
            Key = "quad_4grounds",
            Title = "quad 4 grounds",
            TileAt = (x, y) => (x, y) switch
            {
                (1, 1) => Dirt,
                (2, 1) => Grass,
                (1, 2) => Rock,
                (2, 2) => DarkGrass,
                _ => Grass,
            },
        });

        units.Add(new LabUnit
        {
            Key = "variation_step",
            Title = "variation step 0/8/16/24",
            TileAt = (x, y) => Dirt,
            VariationAt = (x, y) => (x + y) % 4 * 8,
        });
        units.Add(new LabUnit
        {
            Key = "variation_quad",
            Title = "variation quad on center",
            TileAt = (x, y) => Dirt,
            VariationAt = (x, y) => (x, y) switch
            {
                (1, 1) => 0,
                (2, 1) => 8,
                (1, 2) => 16,
                (2, 2) => 24,
                _ => 0,
            },
        });

        return units;
    }

    private static LabUnit Solid(string key, string title, int tile)
    {
        return new LabUnit { Key = key, Title = title, TileAt = (x, y) => tile };
    }

    private static LabUnit Shape(string key, string title, params (int X, int Y)[] cells)
    {
        return new LabUnit
        {
            Key = key,
            Title = title,
            TileAt = (x, y) =>
            {
                foreach ((int cellX, int cellY) in cells)
                {
                    if (InCell(x, y, cellX, cellY))
                    {
                        return Dirt;
                    }
                }
                return Grass;
            },
        };
    }

    private static readonly (string Key, string Title, int X, int Y)[] RingDirections =
    {
        ("ring_e_px", "ring E +x", 2, 1),
        ("ring_w_mx", "ring W -x", 0, 1),
        ("ring_n_py", "ring N +y", 1, 2),
        ("ring_s_my", "ring S -y", 1, 0),
        ("ring_ne", "ring NE", 2, 2),
        ("ring_nw", "ring NW", 0, 2),
        ("ring_se", "ring SE", 2, 0),
        ("ring_sw", "ring SW", 0, 0),
    };

    private static bool CenterBit(int mask, int x, int y)
    {
        if (x < 1 || x > 2 || y < 1 || y > 2)
        {
            return false;
        }
        bool br = x == 2 && y == 1;
        bool bl = x == 1 && y == 1;
        bool tr = x == 2 && y == 2;
        bool tl = x == 1 && y == 2;
        return (br && (mask & 1) != 0)
               || (bl && (mask & 2) != 0)
               || (tr && (mask & 4) != 0)
               || (tl && (mask & 8) != 0);
    }

    private static bool CornerEquals(int x, int y, int cx, int cy)
    {
        return x == cx && y == cy;
    }

    private static bool InCell(int cornerX, int cornerY, int cellX, int cellY)
    {
        return (cornerX == cellX || cornerX == cellX + 1) && (cornerY == cellY || cornerY == cellY + 1);
    }

    private static string MaskLabel(int mask)
    {
        if (mask == 0)
        {
            return "none";
        }
        if (mask == 15)
        {
            return "all";
        }
        var parts = new List<string>();
        if ((mask & 1) != 0) parts.Add("br");
        if ((mask & 2) != 0) parts.Add("bl");
        if ((mask & 4) != 0) parts.Add("tr");
        if ((mask & 8) != 0) parts.Add("tl");
        return string.Join("+", parts);
    }

    private static string MaskSlug(int mask)
    {
        if (mask == 0)
        {
            return "none";
        }
        if (mask == 15)
        {
            return "all";
        }
        var parts = new List<string>();
        if ((mask & 1) != 0) parts.Add("br");
        if ((mask & 2) != 0) parts.Add("bl");
        if ((mask & 4) != 0) parts.Add("tr");
        if ((mask & 8) != 0) parts.Add("tl");
        return string.Join("_", parts);
    }

    private static string ExportSharedGround(Node3D root, string sharedDirectory)
    {
        string materialPath = sharedDirectory + "/ground_material.tres";
        foreach (Node child in root.GetChildren())
        {
            if (child is not MeshInstance3D instance || instance.Name.ToString() != "MapTerrain")
            {
                continue;
            }
            if (instance.MaterialOverride is not ShaderMaterial material)
            {
                continue;
            }
            if (material.GetShaderParameter("ground_texture").Obj is not Texture2DArray array)
            {
                GD.PushWarning("[ARTLAB] 地表材质上没有 Texture2DArray，跳过图集导出");
                return null;
            }

            var images = new Godot.Collections.Array<Image>();
            for (int i = 0; i < array.GetLayers(); i++)
            {
                images.Add(array.GetLayerData(i));
            }
            Texture2D atlas = LayerAtlas.Build(images, out int layers);
            if (atlas == null)
            {
                GD.PushWarning("[ARTLAB] 图集构建失败（headless 下拿不到像素？必须带渲染器跑）");
                return null;
            }

            string asset = sharedDirectory + "/ground_atlas.res";
            Error saveError = ResourceSaver.Save(atlas, asset);
            if (saveError != Error.Ok)
            {
                GD.PushWarning($"[ARTLAB] 图集保存失败 {asset}: {saveError}");
            }

            Shader shader = GD.Load<Shader>(DemoShaderRoot + "terrain_map.gdshader");
            if (shader != null)
            {
                material.Shader = shader;
                material.SetShaderParameter("ground_layers", (float)layers);
            }
            material.SetShaderParameter("ground_texture", ResourceLoader.Load<Texture2D>(asset));

            Error savedMaterial = ResourceSaver.Save(material, materialPath);
            if (savedMaterial != Error.Ok)
            {
                GD.PushWarning($"[ARTLAB] 材质保存失败 {materialPath}: {savedMaterial}");
            }
            instance.MaterialOverride = ResourceLoader.Load<Material>(materialPath);

            GD.Print($"[ARTLAB] 共享地表图集 {layers} 层 → {asset}");
            return materialPath;
        }
        return null;
    }

    private static void ApplySharedGround(Node3D root, string materialPath)
    {
        Material material = ResourceLoader.Load<Material>(materialPath);
        if (material == null)
        {
            GD.PushWarning($"[ARTLAB] 共享材质加载失败 {materialPath}");
            return;
        }
        foreach (Node child in root.GetChildren())
        {
            if (child is MeshInstance3D instance && instance.Name.ToString() == "MapTerrain")
            {
                instance.MaterialOverride = material;
            }
        }
    }

    private static void DumpLayerCombinations(Node3D root, LabUnit unit)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is not MeshInstance3D instance || instance.Name.ToString() != "MapTerrain" || instance.Mesh == null)
            {
                continue;
            }

            var arrays = instance.Mesh.SurfaceGetArrays(0);
            var custom = arrays[(int)Mesh.ArrayType.Custom0].AsFloat32Array();
            var combinations = new Dictionary<string, int>();
            for (int i = 0; i + 3 < custom.Length; i += 4)
            {
                string key = $"({custom[i]:0},{custom[i + 1]:0},{custom[i + 2]:0},{custom[i + 3]:0})";
                combinations[key] = combinations.GetValueOrDefault(key) + 1;
            }

            GD.Print($"[ARTLAB] 样张 {unit.Key}：顶点 {custom.Length / 4} 个，四层组合 {combinations.Count} 种");
            foreach (KeyValuePair<string, int> pair in combinations)
            {
                GD.Print($"[ARTLAB]   层号 {pair.Key} × {pair.Value}");
            }
            return;
        }
    }

    private static void AddFrameAndLabel(Node3D root, LabUnit unit)
    {
        float center = ViewportPanel.CellSize * UnitSpan / 2f;
        var origin = new Vector3(-center, 0f, center);

        var label = new Label3D
        {
            Name = "Title",
            Text = unit.Title,
            FontSize = 64,
            PixelSize = 0.5f,
            Modulate = new Color(1f, 1f, 1f),
            OutlineModulate = new Color(0f, 0f, 0f),
            OutlineSize = 16,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,

            Position = origin + new Vector3(0f, ViewportPanel.CellSize * 0.85f, ViewportPanel.CellSize * 1.7f),
        };
        root.AddChild(label);

        root.AddChild(new MeshInstance3D
        {
            Name = "UnitFrame",
            Mesh = BuildRectLines(UnitSpan, 1.6f),
            MaterialOverride = LineMaterial(new Color(1f, 1f, 1f, 0.45f)),
            Position = origin,
        });
        root.AddChild(new MeshInstance3D
        {
            Name = "CellFrame",
            Mesh = BuildRectLines(1, 1.2f),
            MaterialOverride = LineMaterial(new Color(1f, 0.85f, 0.2f, 0.9f)),
            Position = origin,
        });
    }

    private static ArrayMesh BuildRectLines(float span, float height)
    {
        float half = span * ViewportPanel.CellSize / 2f;
        Vector3[] corners =
        {
            new(-half, 0f, -half),
            new(half, 0f, -half),
            new(half, 0f, half),
            new(-half, 0f, half),
        };

        var vertices = new List<Vector3>();
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = corners[i];
            Vector3 b = corners[(i + 1) % 4];
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(new Vector3(a.X, height, a.Z));
            vertices.Add(new Vector3(b.X, height, b.Z));
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Lines, arrays);
        return mesh;
    }

    private static Material LineMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = color,
            NoDepthTest = true,
        };
    }

    private static void SetOwner(Node node, Node owner)
    {
        foreach (Node child in node.GetChildren())
        {
            child.Owner = owner;
            SetOwner(child, owner);
        }
    }
}
