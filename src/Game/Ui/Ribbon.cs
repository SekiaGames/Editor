using Godot;
using System;

namespace WorldEdit;

public partial class Ribbon : TabContainer
{
    private const int BarHeight = 119;

    public event Action<bool> MinimapToggled;

    public event Action<bool> LightingToggled;

    public event Action<bool> UnitsToggled;
    public event Action<bool> DoodadsToggled;

    public event Action<PathingLayer, bool> PathingToggled;
    public event Action<bool> BrushToggled;
    public event Action<bool> WireframeToggled;

    private Button _unitsToggle;
    private Button _doodadsToggle;
    private Button _pathingGroundToggle;
    private Button _pathingBuildingToggle;
    private Button _pathingFlyingToggle;
    private Button _brushToggle;
    private Button _wireframeToggle;

    private const int PageHeight = 89;

    private static Control FixedPage(Control content)
    {
        var page = new MarginContainer
        {
            CustomMinimumSize = new Vector2(0, PageHeight),
            ClipContents = true,
        };
        page.AddChild(content);
        return page;
    }

    public Ribbon()
    {
        CustomMinimumSize = new Vector2(0, BarHeight);
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

        AddPage("File", BuildFilePage());
        AddPage("View", BuildViewPage());
        AddPage("Tools", BuildToolsPage());
        AddPage("Window", BuildWindowPage());
    }

    private void AddPage(string title, Control content)
    {
        AddChild(FixedPage(content));
        SetTabTitle(GetTabCount() - 1, title);
    }

    public int AddPaletteTab(string title, Control strip)
    {
        AddChild(FixedPage(strip));
        int idx = GetTabCount() - 1;
        SetTabTitle(idx, title);
        CurrentTab = idx;
        return idx;
    }

    public void RemovePaletteTab(string title)
    {
        for (int i = 0; i < GetTabCount(); i++)
        {
            if (GetTabTitle(i) == title)
            {
                var node = GetChild(i);
                RemoveChild(node);
                node.QueueFree();
                return;
            }
        }
    }

    public bool HasPaletteTab(string title)
    {
        for (int i = 0; i < GetTabCount(); i++)
        {
            if (GetTabTitle(i) == title)
            {
                return true;
            }
        }
        return false;
    }

    private static HBoxContainer Page()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);
        return row;
    }

    private static MarginContainer Wrap(HBoxContainer row)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 6);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_right", 6);
        margin.AddThemeConstantOverride("margin_bottom", 2);
        margin.AddChild(row);
        return margin;
    }

    private static Button Toggle(string text, string icon, string tip, bool on)
    {
        var b = W.Big(text, icon, tip, true);
        b.ButtonPressed = on;
        return b;
    }

    public void SetUnitsToggle(bool on)
    {
        _unitsToggle?.SetPressedNoSignal(on);
    }

    public void SetDoodadsToggle(bool on)
    {
        _doodadsToggle?.SetPressedNoSignal(on);
    }

    public void SetPathingToggle(PathingLayer layer, bool on)
    {
        Button target = layer switch
        {
            PathingLayer.Ground => _pathingGroundToggle,
            PathingLayer.Building => _pathingBuildingToggle,
            PathingLayer.Flying => _pathingFlyingToggle,
            _ => null,
        };
        target?.SetPressedNoSignal(on);
    }

    public void SetBrushToggle(bool on)
    {
        _brushToggle?.SetPressedNoSignal(on);
    }

    public void SetWireframeToggle(bool on)
    {
        _wireframeToggle?.SetPressedNoSignal(on);
    }

    private Control BuildFilePage()
    {
        var row = Page();

        var newMap = W.Big("新建地图", "new32x32", "新建空白地图（打桩）", false);
        newMap.Pressed += () => Stub.Fire("file.new");

        var open = W.Big("打开地图", "open32x32", "打开 .w3m / .w3x，把内容读进内存", false);
        open.Pressed += () => Stub.Fire("file.open");

        var save = W.Big("保存地图", "save32x32", "Ctrl+S", false);
        save.Pressed += () => Stub.Fire("file.save");

        var saveAs = W.Big("另存为", "saveas32x32", "Ctrl+Shift+S", false);
        saveAs.Pressed += () => Stub.Fire("file.save_as");

        var testMap = W.Big("测试地图", "test32x32", "Ctrl+T，保存后启动客户端", false);
        testMap.Pressed += () => Stub.Fire("file.test");

        var info = W.Big("地图信息", "description32x32", "地图属性对话框", false);
        info.Pressed += () => Stub.Fire("ui.mapinfo");

        var settings = W.Big("设置", "options32x32", "编辑器设置", false);
        settings.Pressed += () => Stub.Fire("ui.settings");

        var exit = W.Big("退出", "exit32x32", "退出编辑器", false);
        exit.Pressed += () => Stub.Fire("file.exit");

        var undo = W.Big("撤销", "undo32x32", "Ctrl+Z", false);
        undo.Pressed += () => Stub.Fire("edit.undo");

        var redo = W.Big("重做", "redo32x32", "Ctrl+Y", false);
        redo.Pressed += () => Stub.Fire("edit.redo");

        row.AddChild(W.Section("地图", newMap, open, save, saveAs));
        row.AddChild(W.VSep());
        row.AddChild(W.Section("游戏", testMap, info));
        row.AddChild(W.VSep());
        row.AddChild(W.Section("编辑器", settings, exit));
        row.AddChild(W.VSep());
        row.AddChild(W.Section("历史", undo, redo));
        row.AddChild(W.Spacer());
        return Wrap(row);
    }

    private Control BuildViewPage()
    {
        var row = Page();

        var units = Toggle("单位", "units32x32", "显示单位 (U)", true);
        units.Toggled += on => UnitsToggled?.Invoke(on);
        _unitsToggle = units;
        var doodads = Toggle("装饰物", "doodads32x32", "显示装饰物 (D)", true);
        doodads.Toggled += on => DoodadsToggled?.Invoke(on);
        _doodadsToggle = doodads;
        var pathingGround = Toggle("地面", "pathing32x32", "寻路：地面通行 (P)", false);
        pathingGround.Toggled += on => PathingToggled?.Invoke(PathingLayer.Ground, on);
        _pathingGroundToggle = pathingGround;
        var pathingBuilding = Toggle("建筑", "pathing32x32", "寻路：建筑放置", false);
        pathingBuilding.Toggled += on => PathingToggled?.Invoke(PathingLayer.Building, on);
        _pathingBuildingToggle = pathingBuilding;
        var pathingFlying = Toggle("飞行", "pathing32x32", "寻路：空中通行", false);
        pathingFlying.Toggled += on => PathingToggled?.Invoke(PathingLayer.Flying, on);
        _pathingFlyingToggle = pathingFlying;
        var brush = Toggle("笔刷", "brush32x32", "显示笔刷高亮", true);
        brush.Toggled += on => BrushToggled?.Invoke(on);
        _brushToggle = brush;
        var lighting = Toggle("光照", "lighting32x32", "地形光照 (L)", true);
        lighting.Toggled += on => LightingToggled?.Invoke(on);
        var wire = Toggle("线框", "wireframe32x32", "线框模式 (T)", false);
        wire.Toggled += on => WireframeToggled?.Invoke(on);
        _wireframeToggle = wire;
        var debug = Toggle("调试", "debug32x32", "物理调试绘制 (F3)", false);
        debug.Toggled += on => Stub.Fire($"view.debug={on}");
        var minimap = Toggle("小地图", "minimap32x32", "显示小地图", true);
        minimap.Toggled += on =>
        {
            GD.Print($"[VIEW] 小地图={(on ? "显示" : "隐藏")}");
            MinimapToggled?.Invoke(on);
        };

        var switchCam = W.Big("切换相机", "switch32x32", "第一人称 / 轨道 (F1)", false);
        switchCam.Pressed += () => Stub.Fire("view.switch_camera");
        var resetCam = W.Big("重置相机", "reset32x32", "Ctrl+Shift+C", false);
        resetCam.Pressed += () => Stub.Fire("view.reset_camera");

        row.AddChild(W.Section("可见", units, doodads, brush, lighting, wire, debug, minimap));
        row.AddChild(W.Section("寻路", pathingGround, pathingBuilding, pathingFlying));
        row.AddChild(W.VSep());
        row.AddChild(W.Section("相机", switchCam, resetCam));
        row.AddChild(W.Spacer());
        return Wrap(row);
    }

    private Control BuildToolsPage()
    {
        var row = Page();

        var heightmap = W.Big("导入高度图", "heightmap32x32", "读红通道映射到 -16..+16", false);
        heightmap.Pressed += () => Stub.Fire("tools.import_heightmap");

        var tileset = W.Big("更换地形集", "tileset32x32", "修改地图可用瓦片", false);
        tileset.Pressed += () => Stub.Fire("ui.tilesetter");

        var tilePathing = W.Big("瓦片寻路", "pathing32x32", "修改每种瓦片的默认可走性", false);
        tilePathing.Pressed += () => Stub.Fire("ui.tilepather");

        row.AddChild(W.Section("导入", heightmap));
        row.AddChild(W.VSep());
        row.AddChild(W.Section("地形集", tileset, tilePathing));
        row.AddChild(W.Spacer());
        return Wrap(row);
    }

    private Control BuildWindowPage()
    {
        var row = Page();

        var import = W.Big("导入管理器", "importmanager32x32", "地图内嵌资源", false);
        import.Pressed += () => Stub.Fire("ui.import");

        var trigger = W.Big("触发器编辑器", "triggereditor32x32", "触发器与 JASS 脚本", false);
        trigger.Pressed += () => Stub.Fire("ui.trigger");

        var terrain = W.Big("地形", "heightmap32x32", "地形调色板", false);
        terrain.Pressed += () => Stub.Fire("ui.terrain");

        var doodad = W.Big("装饰物", "doodads32x32", "装饰物调色板", false);
        doodad.Pressed += () => Stub.Fire("ui.doodads");

        var pathing = W.Big("寻路", "pathing32x32", "寻路调色板", false);
        pathing.Pressed += () => Stub.Fire("ui.pathing");

        var units = W.Big("单位", "units32x32", "单位调色板（规划中）", false);
        units.Pressed += () => Stub.Fire("ui.units");

        var regions = W.Big("区域", "description32x32", "区域编辑（规划中）", false);
        regions.Pressed += () => Stub.Fire("ui.regions");

        var objects = W.Big("对象编辑器", "tileset32x32", "对象字段编辑（规划中）", false);
        objects.Pressed += () => Stub.Fire("ui.objecteditor");

        var players = W.Big("玩家与队伍", "units32x32", "玩家槽位与队伍（规划中）", false);
        players.Pressed += () => Stub.Fire("ui.players");

        var model = W.Big("模型预览", "doodads32x32", "MDX 预览（规划中）", false);
        model.Pressed += () => Stub.Fire("ui.model");

        row.AddChild(W.Section("编辑器 / 查看器", import, trigger));
        row.AddChild(W.VSep());
        row.AddChild(W.Section("调色板", terrain, doodad, pathing, units, regions));
        row.AddChild(W.VSep());
        row.AddChild(W.Section("规划中", objects, players, model));
        row.AddChild(W.Spacer());
        return Wrap(row);
    }
}
