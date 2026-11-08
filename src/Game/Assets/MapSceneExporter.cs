using Godot;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;
using WorldEdit.Core.Map;

namespace WorldEdit.Assets;

public static class MapSceneExporter
{
    private static readonly Dictionary<string, (string Parameter, string File, string Shader)> LayerGroups = new()
    {
        ["mapterrain"] = ("ground_texture", "ground", "terrain_map"),
        ["mapcliffs"] = ("cliff_texture", "cliff", "cliff_map"),
        ["mapwater"] = ("water_texture", "water", "water_map"),
    };

    private const string DemoShaderRoot = "res://Demo/shader/";

    private const float DemoScale = 0.01f;

    public static void Export(string mapPath, string scenePath)
    {
        MapDocument document = DirAccess.DirExistsAbsolute(mapPath)
            ? MapDocument.LoadDirectory(mapPath)
            : MapDocument.Load(mapPath);

        W3eTerrain terrain = document.Terrain;
        if (terrain == null)
        {
            GD.PushError("[SCENE] 地图里没有 war3map.w3e，无法导出场景");
            return;
        }

        string sceneDirectory = scenePath.GetBaseDir();
        string sceneName = scenePath.GetFile().GetBaseName();
        string assetDirectory = sceneDirectory + "/" + sceneName;
        DirAccess.MakeDirRecursiveAbsolute(assetDirectory);

        var view = new ViewportPanel();
        Node3D root = view.BuildMapTree(terrain, document.Units, true, document.Doodads);
        root.Name = sceneName;
        root.SetMeta("map_width", terrain.Width - 1);
        root.SetMeta("map_height", terrain.Height - 1);
        root.Scale = new Vector3(DemoScale, DemoScale, DemoScale);

        var exported = new List<string>();
        var pendingImages = new List<string>();

        foreach (Node child in root.GetChildren())
        {
            if (child is not MeshInstance3D instance || instance.MaterialOverride is not ShaderMaterial material)
            {
                continue;
            }

            string node = instance.Name.ToString().ToLowerInvariant();
            if (!LayerGroups.TryGetValue(node, out var group))
            {
                continue;
            }

            if (material.GetShaderParameter(group.Parameter).Obj is Texture2DArray array)
            {
                var images = new Godot.Collections.Array<Image>();
                for (int i = 0; i < array.GetLayers(); i++)
                {
                    images.Add(array.GetLayerData(i));
                }

                Texture2D atlas = LayerAtlas.Build(images, out int layers);

                string asset = $"{assetDirectory}/atlas/{group.File}.res";
                DirAccess.MakeDirRecursiveAbsolute(asset.GetBaseDir());
                Error saveError = ResourceSaver.Save(atlas, asset);
                if (saveError != Error.Ok)
                {
                    GD.PushWarning($"[SCENE] 图集保存失败 {asset}: {saveError}");
                }

                Shader shader = GD.Load<Shader>($"{DemoShaderRoot}{group.Shader}.gdshader");
                if (shader != null)
                {
                    material.Shader = shader;
                    material.SetShaderParameter(group.File + "_layers", (float)layers);
                }

                Texture2D loaded = ResourceLoader.Load<Texture2D>(asset);
                if (loaded != null)
                {
                    material.SetShaderParameter(group.Parameter, loaded);
                }
                else
                {
                    material.SetShaderParameter(group.Parameter, (Texture2D)null);
                    pendingImages.Add(asset);
                }
                exported.Add(asset);
            }

            string materialPath = $"{assetDirectory}/{group.File}_material.tres";
            Error saved = ResourceSaver.Save(material, materialPath);
            if (saved != Error.Ok)
            {
                GD.PushWarning($"[SCENE] 材质保存失败 {materialPath}: {saved}");
            }
            instance.MaterialOverride = ResourceLoader.Load<Material>(materialPath);
        }

        SetOwner(root, root);
        var scene = new PackedScene();
        Error packed = scene.Pack(root);
        if (packed != Error.Ok)
        {
            GD.PushError($"[SCENE] 打包场景失败: {packed}");
            root.QueueFree();
            view.QueueFree();
            return;
        }

        Error result = ResourceSaver.Save(scene, scenePath);
        GD.Print($"[SCENE] {scenePath}：节点 {root.GetChildCount()} 个，图集 {exported.Count} 张，"
            + $"待导入 {pendingImages.Count} 张，保存 {(result == Error.Ok ? "成功" : result.ToString())}");

        if (pendingImages.Count > 0)
        {
            GD.Print("[SCENE] 有图层贴图还没被 Godot 导入，场景里的贴图引用是空的。"
                + "先跑一次编辑器导入（--editor --quit），再重新导出本场景。");
            foreach (string path in pendingImages)
            {
                GD.Print($"[SCENE]   待导入 {path}");
            }
        }

        foreach (Node child in root.GetChildren())
        {
            child.QueueFree();
        }
        root.QueueFree();
        view.QueueFree();
        document.Release();
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
