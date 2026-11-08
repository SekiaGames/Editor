using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Assets;
using WorldEdit.Core.Formats.W3x;
using WorldEdit.Core.Map;

namespace WorldEdit;

[Flags]
public enum PathingLayer
{
    None = 0,
    Ground = 1,
    Building = 2,
    Flying = 4,
}

public partial class ViewportPanel : SubViewportContainer
{
    public const int Cells = 48;
    public const float CellSize = 128f;
    public const float WaterLevel = 14f;

    public float MaxCameraHeight { get; set; } = 12000f;

    public event Action CameraChanged;

    public float Fov => _fov;
    public float Yaw => _yaw;
    public float Pitch => _pitch;
    public float Distance => _dist;

    public Vector3 CameraPosition => _cam?.Position ?? Vector3.Zero;

    public Vector2 TargetTile => WorldToMap(_target);

    public float TargetHeight => _target.Y;

    public bool UnitsVisible => _unitsVisible;
    public bool DoodadsVisible => _doodadsVisible;
    public bool PathingVisible(PathingLayer layer) => (_pathingLayers & layer) != 0;
    public bool BrushVisible => _brushVisible;
    public bool Wireframe => _wireframe;

    public event Action<Vector2, float> CursorMoved;

    private SubViewport _vp;
    private Camera3D _cam;
    private Node3D _ring;
    private TorusMesh _ringMesh;
    private StandardMaterial3D _ringMat;

    private Node3D _mapRoot;
    private Node3D _unitsRoot;
    private Node3D _doodadsRoot;
    private Node3D _pathingRoot;
    private W3mPathing _pathing;
    private ShaderMaterial _pathingMaterial;
    private bool _unitsVisible = Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-units") < 0;
    private bool _doodadsVisible = Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-doodads") < 0;
    private PathingLayer _pathingLayers = ParsePathingLayers();
    private bool _brushVisible = true;
    private bool _wireframe = Array.IndexOf(OS.GetCmdlineUserArgs(), "--wireframe") >= 0;

    private bool[,] _cliffTransitions;
    private Vector2 _mapSize = new Vector2(Cells, Cells);
    private float _waterY = WaterLevel;
    private W3eTerrain _terrain;
    private ShaderMaterial _waterMaterial;
    private int _waterFrameCount = 1;
    private int _waterFrameRate = 1;
    private float _waterElapsed;
    private GroundTextureArray _groundLayers;
    private List<string> _groundOrder = new();

    private readonly List<ShaderMaterial> _shadedMaterials = new();
    private bool _lighting = true;

    public const float HiveWeFov = 50.705f;

    private float _fov = HiveWeFov;
    private float _yaw = 35f;
    private float _pitch = -46f;
    private float _dist = 2900f;
    private Vector3 _target = new Vector3(-Cells * CellSize * 0.5f, 30f, Cells * CellSize * 0.5f);
    private float _brushSize = 5f;

    public ViewportPanel()
    {
        Stretch = true;
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        MouseFilter = Control.MouseFilterEnum.Stop;
        ClipContents = true;
    }

    public void SetViewSize(Vector2I size)
    {
        Stretch = false;
        _vp.Size = size;
    }

    public void SetFov(float fov)
    {
        _fov = Mathf.Clamp(fov, 10f, 140f);
        if (_cam != null)
        {
            _cam.Fov = _fov;
        }
        CameraChanged?.Invoke();
    }

    public void SetOrbit(float yaw, float pitch)
    {
        _yaw = yaw;
        _pitch = Mathf.Clamp(pitch, -88f, -6f);
        UpdateCamera();
    }

    public void SetTargetHeight(float height)
    {
        _target = new Vector3(_target.X, Mathf.Clamp(height, -200f, MaxCameraHeight), _target.Z);
        ClampTarget();
        UpdateCamera();
    }

    public void SetCameraPosition(Vector3 position)
    {
        float yaw = Mathf.DegToRad(_yaw);
        float pitch = Mathf.DegToRad(_pitch);
        var forward = new Vector3(
            -Mathf.Cos(pitch) * Mathf.Sin(yaw),
            Mathf.Sin(pitch),
            -Mathf.Cos(pitch) * Mathf.Cos(yaw));
        _target = position + forward * _dist;
        UpdateCamera();
    }

    public void MoveTargetToTile(Vector2 tile)
    {
        Vector2 world = MapToWorld(tile);
        _target = new Vector3(world.X, _target.Y, world.Y);
        ClampTarget();
        UpdateCamera();
    }

    public Image CaptureView()
    {
        return _vp.GetTexture().GetImage();
    }

    private Node3D _placeholder;

    private Node3D PlaceholderRoot()
    {
        if (_placeholder == null)
        {
            _placeholder = new Node3D { Name = "Placeholder" };
            _vp.AddChild(_placeholder);
        }
        return _placeholder;
    }

    public override void _Ready()
    {
        _vp = new SubViewport
        {
            Size = new Vector2I(1280, 720),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Disabled,
        };
        AddChild(_vp);

        BuildEnvironment();
        BuildTerrain();
        BuildWater();
        BuildRing();
        if (_wireframe)
        {
            SetWireframe(true);
        }
        UpdateCamera();
    }

    public static float HeightAt(float x, float z)
    {
        float h = 26f * Mathf.Sin(x * 0.0016f) * Mathf.Cos(z * 0.0014f)
                  + 13f * Mathf.Sin((x + z) * 0.0027f)
                  + 8f * Mathf.Cos(x * 0.0043f - z * 0.0037f);
        return h + 30f;
    }

    public static string BackgroundHtml { get; private set; } = "000000";

    private static string ReadBackgroundFromCommandLine()
    {
        var args = new System.Collections.Generic.List<string>();
        args.AddRange(OS.GetCmdlineUserArgs());
        args.AddRange(OS.GetCmdlineArgs());
        foreach (string argument in args)
        {
            if (argument.StartsWith("--bg=") && argument.Length > "--bg=".Length)
            {
                return argument.Substring("--bg=".Length);
            }
        }
        return null;
    }

    private void BuildEnvironment()
    {
        BackgroundHtml = ReadBackgroundFromCommandLine() ?? BackgroundHtml;
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = Color.FromHtml(BackgroundHtml),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Color.FromHtml("9fb4c8"),
            AmbientLightEnergy = 0.62f,

            TonemapMode = Godot.Environment.ToneMapper.Linear,
            FogEnabled = false,
        };
        var we = new WorldEnvironment { Environment = env };
        _vp.AddChild(we);

        var sun = new DirectionalLight3D
        {
            LightEnergy = 1.45f,
            LightColor = Color.FromHtml("ffe9c8"),
            RotationDegrees = new Vector3(-51, -38, 0),
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 4000f,
        };
        _vp.AddChild(sun);
    }

    private void BuildTerrain()
    {
        var shader = GD.Load<Shader>("res://assets/shaders/terrain_mock.gdshader");
        var mat = new ShaderMaterial { Shader = shader };

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        for (int z = 0; z < Cells; z++)
        {
            for (int x = 0; x < Cells; x++)
            {

                Vector3 p00 = ToWorld(new Vector3(x * CellSize, HeightAt(x * CellSize, z * CellSize), z * CellSize));
                Vector3 p10 = ToWorld(new Vector3((x + 1) * CellSize, HeightAt((x + 1) * CellSize, z * CellSize), z * CellSize));
                Vector3 p01 = ToWorld(new Vector3(x * CellSize, HeightAt(x * CellSize, (z + 1) * CellSize), (z + 1) * CellSize));
                Vector3 p11 = ToWorld(new Vector3((x + 1) * CellSize, HeightAt((x + 1) * CellSize, (z + 1) * CellSize), (z + 1) * CellSize));

                st.AddVertex(p00);
                st.AddVertex(p11);
                st.AddVertex(p01);
                st.AddVertex(p00);
                st.AddVertex(p10);
                st.AddVertex(p11);
            }
        }

        st.GenerateNormals();
        var mesh = st.Commit();

        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat, Name = "MockTerrain" };
        PlaceholderRoot().AddChild(mi);
    }

    private void BuildWater()
    {
        var shader = GD.Load<Shader>("res://assets/shaders/water_mock.gdshader");
        var mat = new ShaderMaterial { Shader = shader };
        var plane = new PlaneMesh
        {
            Size = new Vector2(Cells * CellSize * 1.6f, Cells * CellSize * 1.6f),
            SubdivideWidth = 1,
            SubdivideDepth = 1,
        };
        var mi = new MeshInstance3D
        {
            Mesh = plane,
            MaterialOverride = mat,
            Position = new Vector3(-Cells * CellSize * 0.5f, WaterLevel, Cells * CellSize * 0.5f),
            Name = "MockWater",
        };
        PlaceholderRoot().AddChild(mi);
    }

    public void LoadMap(MapDocument document)
    {
        W3eTerrain terrain = document.Terrain;
        _terrain = terrain;
        if (terrain == null)
        {
            GD.PushWarning("[MAP] 地图里没有 war3map.w3e，无法显示地形");
            return;
        }

        ClearMap();
        _terrain = terrain;

        var root = new Node3D { Name = "MapRoot" };
        _vp.AddChild(root);
        _mapRoot = root;
        _mapSize = new Vector2(terrain.Width - 1, terrain.Height - 1);

        bool[,] covered = BuildMapCliffs(root, terrain);
        BuildMapTerrain(root, terrain, covered);
        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--no-water") < 0)
        {
            BuildMapWater(root, terrain);
        }
        BuildMapUnits(root, terrain, document.Units);
        BuildMapDoodads(root, terrain, document.Doodads);
        _pathing = document.Pathing;
        if (_pathingLayers != PathingLayer.None)
        {
            EnsurePathingOverlay();
        }
        FrameCamera(terrain);

        int blight = 0, ramp = 0, wet = 0;
        foreach (W3eTerrain.Corner c in terrain.Corners)
        {
            if (c.Blight) blight++;
            if (c.Ramp) ramp++;
            if (c.HasWater) wet++;
        }
        GD.Print($"[MAP] 地形已生成: {terrain.Width}x{terrain.Height} 角点, {_mapSize.X * _mapSize.Y:0} tile, 水面 y={_waterY:0.0}, 疫病角点={blight}, 斜坡角点={ramp}, 水域角点={wet}");
    }

    private void ClearMap()
    {
        if (_mapRoot != null)
        {

            _vp.RemoveChild(_mapRoot);
            _mapRoot.QueueFree();
            _mapRoot = null;
        }

        if (_placeholder != null)
        {
            _vp.RemoveChild(_placeholder);
            _placeholder.QueueFree();
            _placeholder = null;
        }

        _terrain = null;
        _unitsRoot = null;
        _doodadsRoot = null;
        _pathingRoot = null;
        _pathing = null;
        _pathingMaterial = null;
        _groundLayers = null;
        _groundOrder.Clear();
        _shadedMaterials.Clear();
        _waterMaterial = null;
        _waterFrameCount = 1;
        _waterFrameRate = 1;
        _waterElapsed = 0f;
        _waterY = WaterLevel;
        _mapSize = new Vector2(Cells, Cells);
    }

    private void RegisterShadedMaterial(ShaderMaterial material)
    {
        material.SetShaderParameter("lighting_enabled", _lighting);
        _shadedMaterials.Add(material);
    }

    public void SetLighting(bool on)
    {
        _lighting = on;
        foreach (ShaderMaterial material in _shadedMaterials)
        {
            material.SetShaderParameter("lighting_enabled", on);
        }
        GD.Print($"[VIEW] 光照={(on ? "开" : "关")}");
    }

    public void SetUnitsVisible(bool visible)
    {
        _unitsVisible = visible;
        if (_unitsRoot != null && IsInstanceValid(_unitsRoot))
        {
            _unitsRoot.Visible = visible;
        }
        GD.Print($"[VIEW] 单位={(visible ? "显示" : "隐藏")}（{_unitsRoot?.GetChildCount() ?? 0} 个）");
    }

    public void SetDoodadsVisible(bool visible)
    {
        _doodadsVisible = visible;
        if (_doodadsRoot != null && IsInstanceValid(_doodadsRoot))
        {
            _doodadsRoot.Visible = visible;
        }
        GD.Print($"[VIEW] 装饰物={(visible ? "显示" : "隐藏")}（{_doodadsRoot?.GetChildCount() ?? 0} 个）");
    }

    public void SetPathingVisible(PathingLayer layer, bool visible)
    {
        _pathingLayers = visible ? _pathingLayers | layer : _pathingLayers & ~layer;
        if (_pathingLayers != PathingLayer.None)
        {
            EnsurePathingOverlay();
            RefreshPathingTexture();
        }
        if (_pathingRoot != null && IsInstanceValid(_pathingRoot))
        {
            _pathingRoot.Visible = _pathingLayers != PathingLayer.None;
        }
        GD.Print($"[VIEW] 寻路 {LayerName(layer)}={(visible ? "开" : "关")}，"
            + $"当前 {_pathingLayers}{(_pathing == null ? "（这张图没有 war3map.wpm）" : "")}");
    }

    public static string LayerName(PathingLayer layer)
    {
        return layer switch
        {
            PathingLayer.Ground => "地面",
            PathingLayer.Building => "建筑",
            PathingLayer.Flying => "飞行",
            _ => layer.ToString(),
        };
    }

    private void RefreshPathingTexture()
    {
        if (_pathingMaterial == null || !IsInstanceValid(_pathingMaterial) || _pathing == null)
        {
            return;
        }
        Image image = BuildPathingImage(_pathing, _pathingLayers, out _, out _, out _);
        _pathingMaterial.SetShaderParameter("pathing_texture", ImageTexture.CreateFromImage(image));
    }

    private static PathingLayer ParsePathingLayers()
    {
        PathingLayer layers = PathingLayer.None;
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--pathing")
            {
                layers |= PathingLayer.Ground;
            }
            else if (arg.StartsWith("--pathing=", StringComparison.Ordinal))
            {
                foreach (string name in arg["--pathing=".Length..].Split(','))
                {
                    if (Enum.TryParse(name.Trim(), true, out PathingLayer layer))
                    {
                        layers |= layer;
                    }
                }
            }
        }
        return layers;
    }

    public void SetBrushVisible(bool visible)
    {
        _brushVisible = visible;
        if (!visible && _ring != null && IsInstanceValid(_ring))
        {
            _ring.Visible = false;
        }
        GD.Print($"[VIEW] 笔刷高亮={(visible ? "显示" : "隐藏")}");
    }

    public void SetWireframe(bool visible)
    {
        _wireframe = visible;
        if (visible)
        {
            RenderingServer.SetDebugGenerateWireframes(true);
        }
        if (_vp != null)
        {
            _vp.DebugDraw = visible ? Viewport.DebugDrawEnum.Wireframe : Viewport.DebugDrawEnum.Disabled;
        }
        GD.Print($"[VIEW] 线框={(visible ? "开" : "关")}");
    }

    private void BuildMapTerrain(Node3D root, W3eTerrain terrain, bool[,] covered)
    {
        TerrainTileSet.SetBlightTileset(terrain.Tileset);

        var order = new List<string>();
        if (terrain.FixedGroundOrder != null)
        {

            order.AddRange(terrain.FixedGroundOrder);
        }
        else
        {
            var used = new HashSet<string>();
            foreach (W3eTerrain.Corner corner in terrain.Corners)
            {
                int id = corner.GroundTexture;
                if (id >= 0 && id < terrain.GroundTextures.Count)
                {
                    used.Add(terrain.GroundTextures[id]);
                }
            }

            foreach (string tileId in terrain.GroundTextures)
            {
                if (used.Contains(tileId))
                {
                    order.Add(tileId);
                }
            }
        }

        foreach (string cliffId in terrain.CliffTextures)
        {
            string cliffGround = CliffTextureSet.GroundTileFor(cliffId);
            if (!string.IsNullOrEmpty(cliffGround) && !order.Contains(cliffGround))
            {
                order.Add(cliffGround);
            }
        }

        if (System.Array.Exists(terrain.Corners, c => c.Blight))
        {
            order.Add(TerrainTileSet.BlightId);
        }

        var tiles = new TerrainTileSet();
        var loaded = new List<string>();
        GroundTextureArray ground = GroundTextureArray.Build(tiles, order, loaded);
        GD.Print($"[MAP] 地表贴图 {loaded.Count}/{order.Count} 张: {string.Join(",", loaded)}");
        foreach (string cliffId in terrain.CliffTextures)
        {
            GD.Print($"[MAP] 悬崖 {cliffId} 脚下地表 = {CliffTextureSet.GroundTileFor(cliffId)}");
        }

        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/terrain_map.gdshader"),
        };
        if (ground?.Texture != null)
        {
            material.SetShaderParameter("ground_texture", ground.Texture);
        }
        RegisterShadedMaterial(material);

        _groundLayers = ground;
        _groundOrder = loaded;

        int width = terrain.Width;
        int height = terrain.Height;
        int culled = 0;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        st.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbaFloat);

        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {

                bool transition = _cliffTransitions != null && _cliffTransitions[x, y];
                if (!transition && !IsRampEntrance(terrain, x, y) && (IsCliffTile(terrain, x, y) || covered[x, y]))
                {
                    culled++;
                    continue;
                }

                W3eTerrain.Corner c00 = terrain.Corners[y * width + x];
                W3eTerrain.Corner c10 = terrain.Corners[y * width + x + 1];
                W3eTerrain.Corner c01 = terrain.Corners[(y + 1) * width + x];
                W3eTerrain.Corner c11 = terrain.Corners[(y + 1) * width + x + 1];

                var layers = TileLayers(
                    RealTileTexture(terrain, x, y),
                    RealTileTexture(terrain, x + 1, y),
                    RealTileTexture(terrain, x, y + 1),
                    RealTileTexture(terrain, x + 1, y + 1),
                    c00);

                float x0 = x * CellSize;
                float x1 = (x + 1) * CellSize;
                float z0 = y * CellSize;
                float z1 = (y + 1) * CellSize;

                var p00 = new Vector3(x0, CornerWorld(terrain, x, y), z0);
                var p10 = new Vector3(x1, CornerWorld(terrain, x + 1, y), z0);
                var p01 = new Vector3(x0, CornerWorld(terrain, x, y + 1), z1);
                var p11 = new Vector3(x1, CornerWorld(terrain, x + 1, y + 1), z1);

                Vector3 n00 = CornerNormal(terrain, x, y);
                Vector3 n10 = CornerNormal(terrain, x + 1, y);
                Vector3 n01 = CornerNormal(terrain, x, y + 1);
                Vector3 n11 = CornerNormal(terrain, x + 1, y + 1);

                var uv00 = new Vector2(0f, 1f);
                var uv10 = new Vector2(1f, 1f);
                var uv11 = new Vector2(1f, 0f);
                var uv01 = new Vector2(0f, 0f);

                AddVertex(st, p11, uv11, layers, n11);
                AddVertex(st, p10, uv10, layers, n10);
                AddVertex(st, p01, uv01, layers, n01);
                AddVertex(st, p01, uv01, layers, n01);
                AddVertex(st, p10, uv10, layers, n10);
                AddVertex(st, p00, uv00, layers, n00);
            }
        }

        GD.Print($"[MAP] 地面格抠掉 {culled}（悬崖+斜坡过渡）");
        var instance = new MeshInstance3D
        {
            Mesh = st.Commit(),
            MaterialOverride = material,
            Name = "MapTerrain",
        };
        root.AddChild(instance);
    }

    private Vector4 TileLayers(string t00, string t10, string t01, string t11, W3eTerrain.Corner c00)
    {

        int bl = TextureIndex(_groundOrder, t00);
        int br = TextureIndex(_groundOrder, t10);
        int tl = TextureIndex(_groundOrder, t01);
        int tr = TextureIndex(_groundOrder, t11);

        var ids = new List<int> { bl, br, tl, tr };
        ids.Sort();
        var distinct = new List<int>();
        foreach (int id in ids)
        {
            if (!distinct.Contains(id))
            {
                distinct.Add(id);
            }
        }

        int baseId = distinct[0];

        float LayerWithMask(int textureId)
        {
            int mask = 0;
            if (br == textureId) mask |= 1;
            if (bl == textureId) mask |= 2;
            if (tr == textureId) mask |= 4;
            if (tl == textureId) mask |= 8;
            return BaseLayerOf(textureId) + mask;
        }

        float baseLayer = LayerOf(c00, baseId);

        float b = distinct.Count > 1 ? LayerWithMask(distinct[1]) : GroundTextureArray.BlankLayer;
        float g = distinct.Count > 2 ? LayerWithMask(distinct[2]) : GroundTextureArray.BlankLayer;
        float r = distinct.Count > 3 ? LayerWithMask(distinct[3]) : GroundTextureArray.BlankLayer;
        return new Vector4(baseLayer, b, g, r);
    }

    private float BaseLayerOf(int textureIndex)
    {
        if (_groundLayers == null || textureIndex < 0 || textureIndex >= _groundOrder.Count)
        {
            return 0f;
        }
        return _groundLayers.BaseLayer[_groundOrder[textureIndex]];
    }

    private float LayerOf(W3eTerrain.Corner corner, int textureIndex)
    {
        if (_groundLayers == null || textureIndex < 0 || textureIndex >= _groundOrder.Count)
        {
            return 0f;
        }
        return _groundLayers.LayerOf(_groundOrder[textureIndex], corner.VariationIndex);
    }

    private static float GroundWorld(W3eTerrain.Corner corner)
    {
        return (corner.HeightWorld + corner.LayerHeight - 2f) * CellSize;
    }

    private static float CornerWorld(W3eTerrain terrain, int x, int y)
    {
        return GroundWorld(terrain.Corners[y * terrain.Width + x]) + RampLift(terrain, x, y) * CellSize;
    }

    private static float RampLift(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        int height = terrain.Height;
        int layer = terrain.Corners[y * width + x].LayerHeight;

        for (int ox = -1; ox <= 0; ox++)
        {
            for (int oy = -1; oy <= 0; oy++)
            {
                int tx = x + ox;
                int ty = y + oy;
                if (tx < 0 || ty < 0 || tx >= width - 1 || ty >= height - 1)
                {
                    continue;
                }
                if (!IsRampEntrance(terrain, tx, ty))
                {
                    continue;
                }

                int baseLayer = Mathf.Min(
                    Mathf.Min(terrain.Corners[ty * width + tx].LayerHeight, terrain.Corners[ty * width + tx + 1].LayerHeight),
                    Mathf.Min(terrain.Corners[(ty + 1) * width + tx].LayerHeight, terrain.Corners[(ty + 1) * width + tx + 1].LayerHeight));
                if (layer == baseLayer)
                {
                    return 0.5f;
                }
            }
        }
        return 0f;
    }

    private static void AddVertex(SurfaceTool st, Vector3 position, Vector2 uv, Vector4 layers, Vector3 normal)
    {
        st.SetUV(uv);

        st.SetNormal(new Vector3(-normal.X, normal.Y, normal.Z));
        st.SetCustom(0, new Color(layers.X, layers.Y, layers.Z, layers.W));
        st.AddVertex(new Vector3(-position.X, position.Y, position.Z));
    }

    private static Vector3 CornerNormal(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        int height = terrain.Height;
        int x0 = Mathf.Max(0, x - 1);
        int x1 = Mathf.Min(width - 1, x + 1);
        int y0 = Mathf.Max(0, y - 1);
        int y1 = Mathf.Min(height - 1, y + 1);

        float nx = (terrain.Corners[y * width + x0].HeightWorld - terrain.Corners[y * width + x1].HeightWorld) / CellSize;
        float nz = (terrain.Corners[y0 * width + x].HeightWorld - terrain.Corners[y1 * width + x].HeightWorld) / CellSize;
        return new Vector3(nx, 2f, nz).Normalized();
    }

    private static int TextureIndex(List<string> used, string tileId)
    {
        if (string.IsNullOrEmpty(tileId))
        {
            return 0;
        }
        int index = used.IndexOf(tileId);
        return index < 0 ? 0 : index;
    }

    private static string RealTileTexture(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        int height = terrain.Height;

        bool plainRampCell = false;
        for (int i = -1; i < 1 && !plainRampCell; i++)
        {
            for (int j = -1; j < 1 && !plainRampCell; j++)
            {
                int cx = x + i;
                int cy = y + j;
                if (cx < 0 || cx >= width || cy < 0 || cy >= height)
                {
                    continue;
                }

                W3eTerrain.Corner corner = terrain.Corners[cy * width + cx];
                bool cliff = cx < width - 1 && cy < height - 1 && IsCliffTile(terrain, cx, cy);
                if (!cliff)
                {
                    continue;
                }

                W3eTerrain.Corner br = terrain.Corners[cy * width + cx + 1];
                W3eTerrain.Corner tl = terrain.Corners[(cy + 1) * width + cx];
                W3eTerrain.Corner tr = terrain.Corners[(cy + 1) * width + cx + 1];

                if (corner.Ramp && tl.Ramp && br.Ramp && tr.Ramp)
                {
                    plainRampCell = true;
                    continue;
                }

                int texture = corner.CliffTexture;
                if (texture == 15)
                {
                    texture -= 14;
                }
                if (texture >= 0 && texture < terrain.CliffTextures.Count)
                {
                    string ground = CliffTextureSet.GroundTileFor(terrain.CliffTextures[texture]);
                    if (!string.IsNullOrEmpty(ground))
                    {
                        return ground;
                    }
                }
            }
        }

        if (terrain.Corners[y * width + x].Blight)
        {
            return TerrainTileSet.BlightId;
        }

        int id = terrain.Corners[y * width + x].GroundTexture;
        return id >= 0 && id < terrain.GroundTextures.Count ? terrain.GroundTextures[id] : null;
    }

    private static bool IsCliffTile(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        int bl = terrain.Corners[y * width + x].LayerHeight;
        int br = terrain.Corners[y * width + x + 1].LayerHeight;
        int tl = terrain.Corners[(y + 1) * width + x].LayerHeight;
        int tr = terrain.Corners[(y + 1) * width + x + 1].LayerHeight;
        return bl != br || bl != tl || bl != tr;
    }

    private static bool IsRampEntrance(W3eTerrain terrain, int x, int y)
    {
        int width = terrain.Width;
        W3eTerrain.Corner bl = terrain.Corners[y * width + x];
        W3eTerrain.Corner br = terrain.Corners[y * width + x + 1];
        W3eTerrain.Corner tl = terrain.Corners[(y + 1) * width + x];
        W3eTerrain.Corner tr = terrain.Corners[(y + 1) * width + x + 1];
        return bl.Ramp && tl.Ramp && br.Ramp && tr.Ramp
               && !(bl.LayerHeight == tr.LayerHeight && tl.LayerHeight == br.LayerHeight);
    }

    private static readonly Vector3 CliffLightDirection = new Vector3(-0.6095f, 0.5079f, -0.6095f);

    private static int ApplyUnitTextureFilter(Node node)
    {
        int changed = 0;
        if (node is MeshInstance3D instance && instance.Mesh != null)
        {
            for (int surface = 0; surface < instance.Mesh.GetSurfaceCount(); surface++)
            {
                Material material = instance.MaterialOverride
                    ?? instance.GetSurfaceOverrideMaterial(surface)
                    ?? instance.Mesh.SurfaceGetMaterial(surface);
                if (material is BaseMaterial3D base3D)
                {

                    base3D.TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear;
                    base3D.TextureRepeat = false;
                    changed++;
                }
            }
        }
        foreach (Node child in node.GetChildren())
        {
            changed += ApplyUnitTextureFilter(child);
        }
        return changed;
    }

    private int BuildMapUnits(Node3D root, W3eTerrain terrain, W3oUnits units)
    {
        if (units == null)
        {
            return 0;
        }

        var holder = new Node3D { Name = "MapUnits" };
        root.AddChild(holder);
        _unitsRoot = holder;
        holder.Visible = _unitsVisible;

        float offsetX = terrain.OffsetX;
        float offsetY = terrain.OffsetY;
        int placed = 0;
        int skipped = 0;
        int materialSlots = 0;

        foreach (UnitPlacement entry in units.Entries)
        {
            if (!entry.IsItem && entry.Id == "sloc")
            {
                skipped++;
                continue;
            }

            UnitModel model = UnitMeshLibrary.Get(entry.Id, entry.IsItem);
            if (model?.Scene == null)
            {
                skipped++;
                continue;
            }

            var instance = model.Scene.Instantiate<Node3D>();

            instance.Position = new Vector3(
                -(entry.X - offsetX),
                entry.Z,
                entry.Y - offsetY);

            instance.Rotation = new Vector3(0f, Mathf.Pi + entry.Angle, 0f);
            instance.Scale = Vector3.One * model.Scale;
            instance.Name = $"{entry.Id}_{placed}";
            ModelLibrary.ApplyMissingMaterials(instance, model.ScenePath);
            materialSlots += ApplyUnitTextureFilter(instance);
            holder.AddChild(instance);
            placed++;
        }

        GD.Print($"[UNIT] 单位 {placed} 个（模型 {UnitMeshLibrary.Count} 种），跳过 {skipped} 个，材质槽 {materialSlots} 个");
        return placed;
    }

    private int BuildMapDoodads(Node3D root, W3eTerrain terrain, W3oDoodads doodads)
    {
        if (doodads == null)
        {
            return 0;
        }

        var holder = new Node3D { Name = "MapDoodads" };
        root.AddChild(holder);
        _doodadsRoot = holder;
        holder.Visible = _doodadsVisible;

        float offsetX = terrain.OffsetX;
        float offsetY = terrain.OffsetY;
        int placed = 0;
        int hidden = 0;
        int dead = 0;
        int missing = 0;
        int materialSlots = 0;
        int textureSlots = 0;

        foreach (DoodadPlacement entry in doodads.Entries)
        {
            if (entry.Hidden)
            {
                hidden++;
                continue;
            }
            if (entry.Dead)
            {
                dead++;
                continue;
            }

            DoodadModel model = DoodadMeshLibrary.Get(entry.Id, entry.Variation);
            if (model?.Scene == null)
            {
                missing++;
                continue;
            }

            var instance = model.Scene.Instantiate<Node3D>();
            instance.Position = new Vector3(
                -(entry.X - offsetX),
                entry.Z,
                entry.Y - offsetY);
            instance.Rotation = new Vector3(0f, Mathf.Pi + entry.Angle, 0f);
            instance.Scale = DoodadScale(model.BaseScale, entry);
            instance.Name = $"{entry.Id}_{placed}";
            ModelLibrary.ApplyMissingMaterials(instance, model.ScenePath);
            textureSlots += DoodadMeshLibrary.ApplyReplaceableTexture(instance, model);
            materialSlots += ApplyUnitTextureFilter(instance);
            holder.AddChild(instance);
            placed++;
        }

        GD.Print($"[DOODAD] 装饰物 {placed} 个（模型 {DoodadMeshLibrary.Count} 种），"
            + $"跳过 隐藏 {hidden} / 死亡 {dead} / 缺模型 {missing}，"
            + $"补齐材质 {materialSlots} 个 / 替换贴图 {textureSlots} 个");
        return placed;
    }

    private static Vector3 DoodadScale(float baseScale, DoodadPlacement entry)
    {
        float sx = entry.ScaleX > 0f ? entry.ScaleX : 1f;
        float sy = entry.ScaleY > 0f ? entry.ScaleY : 1f;
        float sz = entry.ScaleZ > 0f ? entry.ScaleZ : 1f;
        return new Vector3(baseScale - 1f + sx, baseScale - 1f + sy, baseScale - 1f + sz);
    }

    private void EnsurePathingOverlay()
    {
        if (_pathingRoot != null && IsInstanceValid(_pathingRoot))
        {
            return;
        }
        if (_pathing == null || _terrain == null || _mapRoot == null)
        {
            return;
        }

        _pathingRoot = BuildPathingOverlay(_mapRoot, _terrain, _pathing, _pathingLayers);
        _pathingRoot.Visible = _pathingLayers != PathingLayer.None;
    }

    private static Image BuildPathingImage(W3mPathing pathing, PathingLayer layers, out int ground, out int building, out int flying)
    {
        var groundColor = new Color(1f, 0.12f, 0.08f, 0.55f);
        var buildingColor = new Color(0.2f, 0.45f, 1f, 0.45f);
        var flyingColor = new Color(1f, 0.85f, 0.1f, 0.4f);

        var image = Image.CreateEmpty(pathing.Width, pathing.Height, false, Image.Format.Rgba8);
        ground = 0;
        building = 0;
        flying = 0;
        for (int y = 0; y < pathing.Height; y++)
        {
            for (int x = 0; x < pathing.Width; x++)
            {
                byte cell = pathing.At(x, y);
                bool blockedGround = (cell & W3mPathing.Unwalkable) != 0;
                bool blockedBuilding = (cell & W3mPathing.Unbuildable) != 0;
                bool blockedFlying = (cell & W3mPathing.Unflyable) != 0;
                if (blockedGround)
                {
                    ground++;
                }
                if (blockedBuilding)
                {
                    building++;
                }
                if (blockedFlying)
                {
                    flying++;
                }

                Color color = Colors.Transparent;
                if (blockedFlying && (layers & PathingLayer.Flying) != 0)
                {
                    color = flyingColor;
                }
                if (blockedBuilding && (layers & PathingLayer.Building) != 0)
                {
                    color = buildingColor;
                }
                if (blockedGround && (layers & PathingLayer.Ground) != 0)
                {
                    color = groundColor;
                }
                image.SetPixel(x, y, color);
            }
        }
        return image;
    }

    private Node3D BuildPathingOverlay(Node3D root, W3eTerrain terrain, W3mPathing pathing, PathingLayer layers)
    {
        var holder = new Node3D { Name = "MapPathing" };
        root.AddChild(holder);

        Image image = BuildPathingImage(pathing, layers, out int ground, out int building, out int flying);

        _pathingMaterial = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/pathing_overlay.gdshader"),
        };
        _pathingMaterial.SetShaderParameter("pathing_texture", ImageTexture.CreateFromImage(image));
        var material = _pathingMaterial;

        int width = terrain.Width;
        int height = terrain.Height;
        const float lift = 2f;
        float spanX = Mathf.Max(1, width - 1);
        float spanY = Mathf.Max(1, height - 1);

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                float x0 = x * CellSize;
                float x1 = (x + 1) * CellSize;
                float z0 = y * CellSize;
                float z1 = (y + 1) * CellSize;

                var p00 = new Vector3(-x0, CornerWorld(terrain, x, y) + lift, z0);
                var p10 = new Vector3(-x1, CornerWorld(terrain, x + 1, y) + lift, z0);
                var p01 = new Vector3(-x0, CornerWorld(terrain, x, y + 1) + lift, z1);
                var p11 = new Vector3(-x1, CornerWorld(terrain, x + 1, y + 1) + lift, z1);

                var uv00 = new Vector2(x / spanX, y / spanY);
                var uv10 = new Vector2((x + 1) / spanX, y / spanY);
                var uv01 = new Vector2(x / spanX, (y + 1) / spanY);
                var uv11 = new Vector2((x + 1) / spanX, (y + 1) / spanY);

                AddOverlayVertex(st, p11, uv11);
                AddOverlayVertex(st, p10, uv10);
                AddOverlayVertex(st, p01, uv01);
                AddOverlayVertex(st, p01, uv01);
                AddOverlayVertex(st, p10, uv10);
                AddOverlayVertex(st, p00, uv00);
            }
        }

        holder.AddChild(new MeshInstance3D
        {
            Mesh = st.Commit(),
            MaterialOverride = material,
            Name = "Pathing",
        });
        GD.Print($"[PATH] 寻路图 {pathing.Width}x{pathing.Height} 单元，图层 {layers}："
            + $"不可走 {ground}，不可建造 {building}，不可飞 {flying}");
        return holder;
    }

    private static void AddOverlayVertex(SurfaceTool st, Vector3 position, Vector2 uv)
    {
        st.SetUV(uv);
        st.AddVertex(position);
    }

    public Node3D BuildMapTree(W3eTerrain terrain, W3oUnits units = null, bool withWater = true, W3oDoodads doodads = null, W3mPathing pathing = null, PathingLayer pathingLayer = PathingLayer.Ground)
    {
        _terrain = terrain;
        _mapSize = new Vector2(terrain.Width - 1, terrain.Height - 1);

        var root = new Node3D { Name = "MapRoot" };
        bool[,] covered = BuildMapCliffs(root, terrain);
        BuildMapTerrain(root, terrain, covered);
        if (withWater)
        {
            BuildMapWater(root, terrain);
        }
        BuildMapUnits(root, terrain, units);
        BuildMapDoodads(root, terrain, doodads);
        if (pathing != null)
        {
            BuildPathingOverlay(root, terrain, pathing, pathingLayer);
        }
        return root;
    }

    public void LoadScene(string scenePath)
    {
        ClearMap();
        var packed = GD.Load<PackedScene>(scenePath);
        if (packed == null)
        {
            GD.PushError($"[SCENE] 加载失败: {scenePath}");
            return;
        }
        var instance = packed.Instantiate<Node3D>();
        _vp.AddChild(instance);
        _mapRoot = instance;
        float width = instance.HasMeta("map_width") ? instance.GetMeta("map_width").AsSingle() : _mapSize.X;
        float height = instance.HasMeta("map_height") ? instance.GetMeta("map_height").AsSingle() : _mapSize.Y;

        float scale = instance.Scale.X <= 0f ? 1f : instance.Scale.X;
        _mapSize = new Vector2(width * scale, height * scale);
        _dist = 20f * CellSize * scale;
        _yaw = 180f;
        _pitch = Mathf.RadToDeg(-0.977f);

        if (instance.HasMeta("view_dist"))
        {
            _dist = instance.GetMeta("view_dist").AsSingle();
        }
        if (instance.HasMeta("view_target"))
        {
            _target = instance.GetMeta("view_target").AsVector3();
        }
        SetRingVisible(false);
        GD.Print($"[SCENE] 已加载 {scenePath}，节点 {CountNodes(instance)} 个，地图 {width:0}x{height:0} 格");
    }

    private static int CountNodes(Node node)
    {
        int count = 1;
        foreach (Node child in node.GetChildren())
        {
            count += CountNodes(child);
        }
        return count;
    }

    private bool[,] BuildMapCliffs(Node3D root, W3eTerrain terrain)
    {
        int width = terrain.Width;
        int height = terrain.Height;

        bool[,] cliffCorner = new bool[width, height];
        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                cliffCorner[x, y] = IsCliffTile(terrain, x, y);
            }
        }

        bool[,] covered = new bool[width, height];
        _cliffTransitions = new bool[width, height];

        CliffTextureSet cliffSet = CliffTextureSet.Build(terrain.CliffTextures);
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/cliff_map.gdshader"),
        };
        if (cliffSet?.Texture != null)
        {
            material.SetShaderParameter("cliff_texture", cliffSet.Texture);
        }
        RegisterShadedMaterial(material);

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbaFloat);

        int tiles = 0;
        int trans = 0;
        int firstTransX = -1, firstTransZ = -1;
        var missing = new Dictionary<string, int>();

        for (int x = 0; x < width - 1; x++)
        {
            for (int y = 0; y < height - 1; y++)
            {
                if (!cliffCorner[x, y])
                {
                    continue;
                }

                W3eTerrain.Corner bl = terrain.Corners[y * width + x];
                W3eTerrain.Corner br = terrain.Corners[y * width + x + 1];
                W3eTerrain.Corner tl = terrain.Corners[(y + 1) * width + x];
                W3eTerrain.Corner tr = terrain.Corners[(y + 1) * width + x + 1];

                bool facingDown = tl.LayerHeight >= bl.LayerHeight && tr.LayerHeight >= br.LayerHeight;
                bool facingLeft = br.LayerHeight >= bl.LayerHeight && tr.LayerHeight >= tl.LayerHeight;
                if ((facingDown && y == 0) || (!facingDown && y >= height - 2)
                    || (facingLeft && x == 0) || (!facingLeft && x >= width - 2))
                {
                    continue;
                }

                int baseLayer = Math.Min(Math.Min(bl.LayerHeight, br.LayerHeight), Math.Min(tl.LayerHeight, tr.LayerHeight));

                int sideX = x + (facingLeft ? -1 : 1);
                int sideZ = y + (facingDown ? -1 : 1);
                bool alongX = bl.Ramp != br.Ramp && tl.Ramp != tr.Ramp
                              && !CornerFlag(cliffCorner, x + (br.Ramp ? 1 : 0), sideZ);
                bool alongZ = bl.Ramp != tl.Ramp && br.Ramp != tr.Ramp
                              && !CornerFlag(cliffCorner, sideX, y + (tl.Ramp ? 1 : 0));
                if (alongX || alongZ)
                {
                    string variant = string.Concat(
                        RampChar(bl, baseLayer),
                        RampChar(tl, baseLayer),
                        RampChar(tr, baseLayer),
                        RampChar(br, baseLayer));
                    string transModel = CliffMeshFactory.Find("CliffTrans" + variant + "0");
                    if (transModel != null)
                    {
                        int px = x + (alongZ && !facingLeft ? 1 : 0);
                        int pz = y - (alongX && facingDown ? 1 : 0);
                        MarkCovered(st, terrain, transModel, px, pz, baseLayer, cliffSet, bl, covered, cullGround: false);

                        _cliffTransitions[x, y] = true;
                        _cliffTransitions[px, pz] = true;
                        tiles++;
                        trans++;
                        if (firstTransX < 0)
                        {
                            firstTransX = x;
                            firstTransZ = y;
                        }
                        continue;
                    }
                    missing["CliffTrans" + variant] = missing.GetValueOrDefault("CliffTrans" + variant) + 1;
                }

                if (IsRampEntrance(terrain, x, y))
                {
                    continue;
                }

                string cliffVariant = string.Concat(
                    (char)('A' + bl.LayerHeight - baseLayer),
                    (char)('A' + tl.LayerHeight - baseLayer),
                    (char)('A' + tr.LayerHeight - baseLayer),
                    (char)('A' + br.LayerHeight - baseLayer));
                if (cliffVariant == "AAAA")
                {
                    continue;
                }

                string model = CliffMeshFactory.ResolveCliff(cliffVariant, bl.CliffVariation);
                if (model == null)
                {
                    missing[cliffVariant] = missing.GetValueOrDefault(cliffVariant) + 1;
                    continue;
                }

                MarkCovered(st, terrain, model, x, y, baseLayer, cliffSet, bl, covered);
                tiles++;
            }
        }

        if (missing.Count > 0)
        {
            GD.PushWarning($"[CLIFF] 缺少模型: {string.Join(",", missing.Keys)}");
        }

        if (tiles > 0)
        {
            root.AddChild(new MeshInstance3D
            {
                Mesh = st.Commit(),
                MaterialOverride = material,
                Name = "MapCliffs",
            });
        }

        GD.Print($"[CLIFF] 悬崖格 {tiles} 个（其中斜坡过渡 {trans}@({firstTransX},{firstTransZ})）, 贴图集合 {terrain.CliffTextures.Count} 组: {string.Join(",", terrain.CliffTextures)}");
        return covered;
    }

    private void MarkCovered(SurfaceTool st, W3eTerrain terrain, string model, int x, int y, int baseLayer,
        CliffTextureSet cliffSet, W3eTerrain.Corner bl, bool[,] covered, bool cullGround = true)
    {
        CliffGeometry geometry = CliffMeshFactory.Get(model);
        Vector3 normal = (
            CornerNormal(terrain, x, y) + CornerNormal(terrain, x + 1, y) +
            CornerNormal(terrain, x, y + 1) + CornerNormal(terrain, x + 1, y + 1)).Normalized();
        float layer = CliffLayer(cliffSet, terrain, bl.CliffTexture);

        Vector3 worldNormal = new Vector3(-normal.X, normal.Y, normal.Z);
        float shade = Mathf.Clamp(worldNormal.Dot(CliffLightDirection) + 0.45f, 0f, 1f);
        var offset = new Vector3((x + 1) * CellSize, 0f, y * CellSize);

        var triangles = new List<Vector3>();
        for (int i = 0; i + 2 < geometry.Indices.Length; i += 3)
        {

            var tri = new Vector3[3];
            for (int k = 0; k < 3; k++)
            {
                int index = geometry.Indices[i + (k == 1 ? 2 : (k == 2 ? 1 : 0))];
                Vector3 vertex = geometry.Vertices[index] + offset;
                vertex.Y = GroundSurface(terrain, x, y, vertex.X, vertex.Z, baseLayer) + geometry.Vertices[index].Y;
                tri[k] = vertex;
                AddVertex(st, vertex, geometry.Uvs[index], new Vector4(layer, shade, 0f, 0f), normal);
            }
            triangles.Add(tri[0]);
            triangles.Add(tri[1]);
            triangles.Add(tri[2]);
        }

        if (cullGround)
        {
            MarkFullyCoveredTiles(terrain, triangles, covered);
        }
    }

    private static bool TriangleHeightAt(List<Vector3> triangles, int start, float x, float z, out float y)
    {
        Vector3 a = triangles[start];
        Vector3 b = triangles[start + 1];
        Vector3 c = triangles[start + 2];

        float area = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
        if (Mathf.Abs(area) < 1e-4f)
        {

            y = 0f;
            return false;
        }

        float l1 = ((b.Z - c.Z) * (x - c.X) + (c.X - b.X) * (z - c.Z)) / area;
        float l2 = ((c.Z - a.Z) * (x - c.X) + (a.X - c.X) * (z - c.Z)) / area;
        float l3 = 1f - l1 - l2;
        if (l1 < -0.02f || l2 < -0.02f || l3 < -0.02f)
        {
            y = 0f;
            return false;
        }

        y = l1 * a.Y + l2 * b.Y + l3 * c.Y;
        return true;
    }

    private static void MarkFullyCoveredTiles(W3eTerrain terrain, List<Vector3> triangles, bool[,] covered)
    {
        int width = terrain.Width;
        int height = terrain.Height;

        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < triangles.Count; i++)
        {
            minX = Mathf.Min(minX, triangles[i].X);
            maxX = Mathf.Max(maxX, triangles[i].X);
            minZ = Mathf.Min(minZ, triangles[i].Z);
            maxZ = Mathf.Max(maxZ, triangles[i].Z);
        }
        if (triangles.Count == 0)
        {
            return;
        }

        int tx0 = Mathf.Max(0, Mathf.FloorToInt(minX / CellSize));
        int tx1 = Mathf.Min(width - 2, Mathf.FloorToInt(maxX / CellSize - 0.001f));
        int tz0 = Mathf.Max(0, Mathf.FloorToInt(minZ / CellSize));
        int tz1 = Mathf.Min(height - 2, Mathf.FloorToInt(maxZ / CellSize - 0.001f));

        for (int tz = tz0; tz <= tz1; tz++)
        {
            for (int tx = tx0; tx <= tx1; tx++)
            {
                if (covered[tx, tz])
                {
                    continue;
                }

                bool all = true;
                for (int sz = 1; sz <= 4 && all; sz++)
                {
                    for (int sx = 1; sx <= 4 && all; sx++)
                    {
                        float wx = (tx + sx * 0.2f) * CellSize;
                        float wz = (tz + sz * 0.2f) * CellSize;
                        float ground = InterpolatedHeight(terrain, wx / CellSize, wz / CellSize);

                        bool hit = false;
                        for (int t = 0; t + 2 < triangles.Count; t += 3)
                        {
                            if (TriangleHeightAt(triangles, t, wx, wz, out float surface) && surface >= ground - 2f)
                            {
                                hit = true;
                                break;
                            }
                        }
                        all = hit;
                    }
                }
                covered[tx, tz] = all;
            }
        }
    }

    private static bool CornerFlag(bool[,] grid, int x, int y)
    {
        return x >= 0 && y >= 0 && x < grid.GetLength(0) && y < grid.GetLength(1) && grid[x, y];
    }

    private static char RampChar(W3eTerrain.Corner corner, int baseLayer)
    {
        int delta = corner.LayerHeight - baseLayer;
        if (!corner.Ramp)
        {
            return (char)('A' + delta);
        }
        return delta switch
        {
            0 => 'L',
            1 => 'H',
            _ => 'X',
        };
    }

    private static float InterpolatedHeight(W3eTerrain terrain, float x, float y)
    {
        int width = terrain.Width;
        int height = terrain.Height;
        float cx = Mathf.Clamp(x, 0f, width - 1.01f);
        float cy = Mathf.Clamp(y, 0f, height - 1.01f);
        int x0 = (int)cx;
        int y0 = (int)cy;
        int x1 = Mathf.Min(x0 + 1, width - 1);
        int y1 = Mathf.Min(y0 + 1, height - 1);
        float tx = cx - x0;
        float ty = cy - y0;

        float h00 = GroundWorld(terrain.Corners[y0 * width + x0]);
        float h10 = GroundWorld(terrain.Corners[y0 * width + x1]);
        float h01 = GroundWorld(terrain.Corners[y1 * width + x0]);
        float h11 = GroundWorld(terrain.Corners[y1 * width + x1]);
        return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
    }

    private static float GroundSurface(W3eTerrain terrain, int x, int y, float worldX, float worldZ, int baseLayer)
    {
        float tx = Mathf.Clamp((worldX - x * CellSize) / CellSize, 0f, 1f);
        float tz = Mathf.Clamp((worldZ - y * CellSize) / CellSize, 0f, 1f);

        float Corner(int cx, int cz)
        {
            return (terrain.Corners[cz * terrain.Width + cx].HeightWorld + baseLayer - 2f) * CellSize
                + RampLift(terrain, cx, cz) * CellSize;
        }

        float h00 = Corner(x, y);
        float h10 = Corner(x + 1, y);
        float h01 = Corner(x, y + 1);
        float h11 = Corner(x + 1, y + 1);

        if (tx + tz >= 1f)
        {
            return h11 * (tx + tz - 1f) + h10 * (1f - tz) + h01 * (1f - tx);
        }
        return h00 * (1f - tx - tz) + h10 * tx + h01 * tz;
    }

    private static float CliffLayer(CliffTextureSet set, W3eTerrain terrain, int cliffTexture)
    {
        if (set == null || cliffTexture < 0 || cliffTexture >= terrain.CliffTextures.Count)
        {
            return 0f;
        }
        return set.LayerOf(terrain.CliffTextures[cliffTexture]);
    }

    private void BuildMapWater(Node3D root, W3eTerrain terrain)
    {
        WaterTileSet water = WaterTileSet.Load(terrain.Tileset);
        if (water == null)
        {
            return;
        }

        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/water_map.gdshader"),
        };
        if (water.Texture != null)
        {
            material.SetShaderParameter("water_texture", water.Texture);
        }
        _waterMaterial = material;
        _waterFrameCount = Mathf.Max(1, water.FrameCount);
        _waterFrameRate = Mathf.Max(1, water.FrameRate);

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        int width = terrain.Width;
        int height = terrain.Height;
        int tiles = 0;
        var levels = new Dictionary<int, int>();

        for (int y = 0; y < height - 1; y++)
        {
            for (int x = 0; x < width - 1; x++)
            {
                W3eTerrain.Corner c00 = terrain.Corners[y * width + x];
                W3eTerrain.Corner c10 = terrain.Corners[y * width + x + 1];
                W3eTerrain.Corner c01 = terrain.Corners[(y + 1) * width + x];
                W3eTerrain.Corner c11 = terrain.Corners[(y + 1) * width + x + 1];
                if (!c00.HasWater && !c10.HasWater && !c01.HasWater && !c11.HasWater)
                {
                    continue;
                }

                float x0 = x * CellSize;
                float x1 = (x + 1) * CellSize;
                float z0 = y * CellSize;
                float z1 = (y + 1) * CellSize;

                var p00 = new Vector3(x0, WaterWorld(c00, water), z0);
                var p10 = new Vector3(x1, WaterWorld(c10, water), z0);
                var p01 = new Vector3(x0, WaterWorld(c01, water), z1);
                var p11 = new Vector3(x1, WaterWorld(c11, water), z1);
                CountLevel(levels, c00);
                CountLevel(levels, c10);
                CountLevel(levels, c01);
                CountLevel(levels, c11);

                var uv00 = new Vector2(x, 1 - y);
                var uv10 = new Vector2(x + 1, 1 - y);
                var uv01 = new Vector2(x, -y);
                var uv11 = new Vector2(x + 1, -y);

                AddWaterVertex(st, p00, uv00, WaterColor(water, terrain, x, y));
                AddWaterVertex(st, p11, uv11, WaterColor(water, terrain, x + 1, y + 1));
                AddWaterVertex(st, p10, uv10, WaterColor(water, terrain, x + 1, y));
                AddWaterVertex(st, p00, uv00, WaterColor(water, terrain, x, y));
                AddWaterVertex(st, p01, uv01, WaterColor(water, terrain, x, y + 1));
                AddWaterVertex(st, p11, uv11, WaterColor(water, terrain, x + 1, y + 1));
                tiles++;
            }
        }

        _waterY = MostCommonLevel(levels, water);
        if (tiles == 0)
        {
            return;
        }

        root.AddChild(new MeshInstance3D
        {
            Mesh = st.Commit(),
            MaterialOverride = material,
            Name = "MapWater",
        });
        GD.Print($"[WATER] 水面格 {tiles} 个, 最高 y={_waterY:0.0}, 帧数 {water.FrameCount}");
    }

    private void CountLevel(Dictionary<int, int> levels, W3eTerrain.Corner corner)
    {
        if (!corner.HasWater)
        {
            return;
        }
        int key = (int)Mathf.Round(corner.WaterWorld * 100f);
        levels[key] = levels.GetValueOrDefault(key) + 1;
    }

    private static float MostCommonLevel(Dictionary<int, int> levels, WaterTileSet water)
    {
        int bestKey = int.MinValue;
        int bestCount = 0;
        foreach (var pair in levels)
        {
            if (pair.Value > bestCount)
            {
                bestCount = pair.Value;
                bestKey = pair.Key;
            }
        }
        return bestCount == 0 ? WaterLevel : (bestKey / 100f + water.HeightOffset) * CellSize;
    }

    private static float WaterWorld(W3eTerrain.Corner corner, WaterTileSet water)
    {
        return (corner.WaterWorld + water.HeightOffset) * CellSize;
    }

    private static Color WaterColor(WaterTileSet water, W3eTerrain terrain, int x, int y)
    {
        const float MinDepth = 10f / 128f;
        const float DeepLevel = 64f / 128f;
        const float MaxDepth = 72f / 128f;

        W3eTerrain.Corner corner = terrain.Corners[y * terrain.Width + x];
        float waterY = corner.WaterWorld + water.HeightOffset;
        float groundY = CornerWorld(terrain, x, y) / CellSize;
        float depth = Mathf.Clamp(waterY - groundY, 0f, 1f);

        if (depth <= DeepLevel)
        {
            float t = Mathf.Clamp((depth - MinDepth) / (DeepLevel - MinDepth), 0f, 1f);
            return water.ShallowMin.Lerp(water.ShallowMax, t);
        }

        float u = Mathf.Clamp((depth - DeepLevel) / (MaxDepth - DeepLevel), 0f, 1f);
        return water.DeepMin.Lerp(water.DeepMax, u);
    }

    private void UpdateWaterFrame(float delta)
    {
        if (_waterMaterial == null)
        {
            return;
        }
        _waterElapsed += Mathf.Max(0f, delta);
        int frame = Mathf.FloorToInt(1f + _waterElapsed * _waterFrameRate) % _waterFrameCount;
        _waterMaterial.SetShaderParameter("frame", (float)frame);
    }

    private static void AddWaterVertex(SurfaceTool st, Vector3 position, Vector2 uv, Color color)
    {
        st.SetUV(uv);
        st.SetColor(color);
        st.AddVertex(new Vector3(-position.X, position.Y, position.Z));
    }

    private void FrameCamera(W3eTerrain terrain)
    {
        _terrain = terrain;

        int centerX = terrain.Width / 2;
        int centerZ = terrain.Height / 2;
        _target = new Vector3(
            -centerX * CellSize,
            InterpolatedHeight(terrain, centerX, centerZ),
            centerZ * CellSize);
        _dist = 20f * CellSize;
        _yaw = 180f;
        _pitch = Mathf.RadToDeg(-0.977f);
        SetRingVisible(false);
        UpdateCamera();
        GD.Print($"[CAM] reset target=({_target.X:0},{_target.Y:0},{_target.Z:0}) dist={_dist:0} yaw={_yaw:0} pitch={_pitch:0.0} cam=({_cam.Position.X:0},{_cam.Position.Y:0},{_cam.Position.Z:0}) fov={_cam.Fov:0}");
    }

    private void BuildRing()
    {
        _ringMesh = new TorusMesh
        {
            InnerRadius = 1f,
            OuterRadius = 1.12f,
            Rings = 48,
            RingSegments = 6,
        };
        _ringMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(1f, 0.83f, 0.25f, 0.95f),
            NoDepthTest = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        var mi = new MeshInstance3D
        {
            Mesh = _ringMesh,
            MaterialOverride = _ringMat,
            Name = "BrushRing",
            RotationDegrees = new Vector3(-90, 0, 0),
        };
        _ring = new Node3D { Name = "BrushCursor" };
        _ring.AddChild(mi);
        _ring.Position = new Vector3(-Cells * CellSize * 0.5f, WaterLevel + 1f, Cells * CellSize * 0.5f);
        _ring.Visible = false;
        _vp.AddChild(_ring);
        SetBrushSize(5f);
    }

    public void SetBrushSize(float cells)
    {
        _brushSize = Mathf.Max(1f, cells);
        if (_ringMesh == null)
        {
            return;
        }
        float radius = _brushSize * 0.5f * CellSize;
        _ringMesh.InnerRadius = radius * 0.94f;
        _ringMesh.OuterRadius = radius;
    }

    public void SetRingVisible(bool visible)
    {
        if (_ring != null)
        {
            _ring.Visible = visible;
        }
    }

    private void UpdateCamera()
    {
        float yaw = Mathf.DegToRad(_yaw);
        float pitch = Mathf.DegToRad(_pitch);
        var offset = new Vector3(
            Mathf.Cos(pitch) * Mathf.Sin(yaw),
            -Mathf.Sin(pitch),
            Mathf.Cos(pitch) * Mathf.Cos(yaw)) * _dist;

        if (offset.Y > 0.01f && _target.Y + offset.Y > MaxCameraHeight)
        {
            float allowed = Mathf.Max(1f, MaxCameraHeight - _target.Y);
            float scale = Mathf.Clamp(allowed / offset.Y, 0.001f, 1f);
            _dist = Mathf.Max(1f, _dist * scale);
            offset *= scale;
        }

        _cam ??= new Camera3D { Current = true, Fov = _fov, Near = 0.05f * CellSize, Far = 2000f * CellSize };
        if (_cam.GetParent() == null)
        {
            _vp.AddChild(_cam);
        }
        _cam.Position = _target + offset;
        _cam.LookAt(_target, Vector3.Up);
        CameraChanged?.Invoke();
    }

    public void RunCameraSelfTest()
    {
        float wpp = WorldPerPixel();
        var before = _target;
        float yaw0 = _yaw;

        PanBy(new Vector2(120f, 0f));
        var pan = _target - before;
        float yawRad = Mathf.DegToRad(yaw0);
        var right = new Vector3(Mathf.Cos(yawRad), 0, -Mathf.Sin(yawRad));
        float dot = pan.X * right.X + pan.Z * right.Z;

        GD.Print($"[CAM] worldPerPixel={wpp:0.00} panLen={pan.Length():0.00} expect={120f * wpp:0.00} dotRight={dot:0.00} (应为负)");

        var afterPan = _target;
        OrbitBy(new Vector2(90f, 0f));
        GD.Print($"[CAM] yaw {yaw0:0.0} -> {_yaw:0.0}, targetMovedByOrbit={(_target - afterPan).Length():0.000} (应为 0)");

        var beforeV = _target;
        PanBy(new Vector2(0f, 120f));
        var panV = _target - beforeV;
        var forward = new Vector3(Mathf.Sin(yawRad), 0, Mathf.Cos(yawRad));
        float dotF = panV.X * forward.X + panV.Z * forward.Z;
        GD.Print($"[CAM] panDown120 len={panV.Length():0.00} dotForward={dotF:0.00} (应为负)");

        float startX = _target.X;
        float prevX = startX;
        bool monotonic = true;
        for (int i = 0; i < 30; i++)
        {
            PanBy(new Vector2(40f, 0f));
            if (_target.X < prevX - 0.01f)
            {
                monotonic = false;
            }
            prevX = _target.X;
        }
        GD.Print($"[CAM] panRight30 x {startX:0} -> {_target.X:0} (上界 0, 单调={monotonic})");

        _target = new Vector3(0f, _target.Y, _target.Z);
        for (int i = 0; i < 60; i++)
        {
            PanBy(new Vector2(-40f, 0f));
        }
        GD.Print($"[CAM] panLeft60 x -> {_target.X:0} (下界 {- _mapSize.X * CellSize:0})");

        var y0 = _target.Y;
        var cam0 = _cam.Position.Y;
        HeightBy(new Vector2(0f, -100f));
        UpdateCamera();
        GD.Print($"[CAM] heightUp100 targetY {y0:0.0} -> {_target.Y:0.0}, camY {cam0:0.0} -> {_cam.Position.Y:0.0} (应升高)");
    }

    public void FocusOn(Vector2 tile)
    {
        Vector2 world = MapToWorld(tile);
        _target = new Vector3(world.X, _target.Y, world.Y);
        ClampTarget();
        UpdateCamera();
    }

    public void SetDistance(float distance)
    {
        _dist = Mathf.Max(1f, distance);
        UpdateCamera();
        GD.Print($"[CAM] dist={_dist:0} camY={_cam.Position.Y:0} limit={MaxCameraHeight:0}");
    }

    public void ResetCamera()
    {
        if (_terrain != null)
        {
            FrameCamera(_terrain);
            return;
        }

        _yaw = 180f;
        _pitch = Mathf.RadToDeg(-0.977f);
        _dist = 20f * CellSize;
        _target = new Vector3(-Cells * CellSize * 0.5f, 30f, Cells * CellSize * 0.5f);
        UpdateCamera();
    }

    public override void _Process(double delta)
    {
        UpdateWaterFrame((float)delta);

        var local = GetLocalMousePosition();
        var rect = new Rect2(Vector2.Zero, Size);
        if (!rect.HasPoint(local) || _cam == null)
        {
            if (_ring != null)
            {
                _ring.Visible = false;
            }
            return;
        }

        Vector2 p = local * (_vp.Size / Size);
        Vector3 o = _cam.ProjectRayOrigin(p);
        Vector3 d = _cam.ProjectRayNormal(p);
        if (Mathf.Abs(d.Y) < 0.0001f)
        {
            return;
        }

        float t = (_waterY - o.Y) / d.Y;
        if (t <= 0f)
        {
            return;
        }

        Vector3 hit = o + d * t;
        _ring.Visible = _brushVisible;
        _ring.Position = new Vector3(hit.X, hit.Y + 0.4f, hit.Z);
        CursorMoved?.Invoke(WorldToMap(hit), hit.Y);
    }

    private float WorldPerPixel()
    {

        float h = _vp != null && _vp.Size.Y > 8 ? _vp.Size.Y : (Size.Y > 8 ? Size.Y : 720f);
        return 2f * _dist * Mathf.Tan(Mathf.DegToRad(_cam.Fov * 0.5f)) / h;
    }

    public void PanBy(Vector2 screenDelta)
    {
        float yaw = Mathf.DegToRad(_yaw);
        var right = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));
        var forward = new Vector3(Mathf.Sin(yaw), 0, Mathf.Cos(yaw));
        float k = WorldPerPixel();

        _target += (-right * screenDelta.X - forward * screenDelta.Y) * k;
        ClampTarget();
    }

    public void HeightBy(Vector2 screenDelta)
    {
        var look = (_target - _cam.Position).Normalized();
        float k = WorldPerPixel() * 0.35f;
        _target += -look * (-screenDelta.Y * k);

        float headroom = MaxCameraHeight - -Mathf.Sin(Mathf.DegToRad(_pitch)) * _dist;
        _target = new Vector3(_target.X, Mathf.Clamp(_target.Y, -200f, Mathf.Max(-200f, headroom)), _target.Z);
        ClampTarget();
    }

    public void OrbitBy(Vector2 screenDelta)
    {
        _yaw -= screenDelta.X * 0.25f;
        _pitch = Mathf.Clamp(_pitch - screenDelta.Y * 0.25f, -88f, -6f);
    }

    public static Vector2 MapToWorld(Vector2 tile)
    {
        return new Vector2(-tile.X * CellSize, tile.Y * CellSize);
    }

    private static Vector3 ToWorld(Vector3 mapPoint)
    {
        return new Vector3(-mapPoint.X, mapPoint.Y, mapPoint.Z);
    }

    public static Vector2 WorldToMap(Vector3 world)
    {
        return new Vector2(-world.X / CellSize, world.Z / CellSize);
    }

    private void ClampTarget()
    {

        _target = new Vector3(
            Mathf.Clamp(_target.X, -_mapSize.X * CellSize, 0f),
            _target.Y,
            Mathf.Clamp(_target.Z, 0f, _mapSize.Y * CellSize));
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed:
                _dist = Mathf.Clamp(_dist * 0.887f, 0.001f * CellSize, 1000f * CellSize);
                UpdateCamera();
                AcceptEvent();
                break;
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed:
                _dist = Mathf.Clamp(_dist * 1.127f, 0.001f * CellSize, 1000f * CellSize);
                UpdateCamera();
                AcceptEvent();
                break;

            case InputEventMouseMotion mm when (mm.ButtonMask & MouseButtonMask.Right) != 0:
                if (mm.ShiftPressed)
                {
                    HeightBy(mm.Relative);
                }
                else if (mm.CtrlPressed)
                {
                    OrbitBy(mm.Relative);
                }
                else
                {
                    PanBy(mm.Relative);
                }
                UpdateCamera();
                AcceptEvent();
                break;
            case InputEventMouseMotion mm when (mm.ButtonMask & MouseButtonMask.Middle) != 0:
                PanBy(mm.Relative);
                UpdateCamera();
                AcceptEvent();
                break;
        }
    }
}
