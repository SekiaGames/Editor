using Godot;
using System;
using System.IO;
using WorldEdit.Core.Map;

namespace WorldEdit;

public partial class Main : Control
{
    private Ribbon _ribbon;
    private ViewportPanel _view;
    private Window _paletteWindow;
    private Window _currentWindow;
    private MinimapPanel _minimap;

    private Label _lblMap;
    private Label _lblBrush;
    private Label _lblCoord;
    private Label _lblFps;
    private Label _lblHint;

    private PanelContainer _toast;
    private Label _toastLabel;
    private double _toastLeft;

    private string _paletteId;
    private double _fpsTimer;
    private int _fpsFrames;
    private int _frames;

    private bool _cameraTest;
    private bool _tooltipProbe;
    private string _startupMap;
    private bool _smokePending;
    private string _startupScene;
    private bool _noBrush;
    private string _pendingWindow;
    private float _startupDistance = -1f;
    private Vector2 _startupFocus = new Vector2(-1f, -1f);
    private Vector3? _startupCamera;
    private float _startupYaw = float.NaN;
    private float _startupPitch = float.NaN;
    private bool _startupMinimap = true;
    private bool _startupLighting = true;
    private float _startupFov;
    private string _reopenPath;
    private MapDocument _currentMap;
    private string _mapDisplayName = "未打开地图";
    private string _lastTitle;
    private bool _checkTabs;
    private int _probeFrame;
    private int _probeTab;
    private string _shotPath;
    private bool _shotView;
    private Vector2I _viewSize;
    private int _shotFrames = 24;

    public override void _Ready()
    {

        Assets.ExternalAssets.Mount();

        ThemeDB.FallbackFont = UiTheme.Font;
        ThemeDB.FallbackFontSize = 12;

        Theme = UiTheme.Build();
        BuildUi();

        Stub.Fired += OnStubFired;

        ParseCommandLine();

        if (_tooltipProbe)
        {
            BuildTooltipProbe();
        }

        if (_shotPath != null)
        {
            GetTree().CreateTimer(30).Timeout += () =>
            {
                GD.Print("[SHOT] watchdog timeout, quitting");
                GetTree().Quit();
            };
        }
    }

    private void BuildTooltipProbe()
    {

        Button owner = null;
        foreach (var n in _ribbon.FindChildren("*", "Button", true, false))
        {
            if (n is Button b && !string.IsNullOrEmpty(b.TooltipText))
            {
                owner = b;
                break;
            }
        }
        if (owner == null)
        {
            GD.Print("[TIP] 没找到带 Tooltip 的按钮");
            return;
        }

        var popup = new PopupPanel { ThemeTypeVariation = "TooltipPanel", Visible = false };
        var text = new Label { ThemeTypeVariation = "TooltipLabel", Text = "新建空白地图（打桩）" };
        popup.AddChild(text);
        owner.AddChild(popup);

        var sb = popup.GetThemeStylebox("panel");
        string panel = sb is StyleBoxFlat flat
            ? $"bg={flat.BgColor} border={flat.BorderColor}"
            : sb?.GetType().Name ?? "null";
        var font = text.GetThemeFont("font");
        GD.Print($"[TIP] 弹出层解析: panel {panel} | text={text.GetThemeColor("font_color")} | font={font?.GetFontName()} 有中文={font?.HasChar('新')}");

        var visual = new PanelContainer { ThemeTypeVariation = "TooltipPanel", Position = new Vector2(430, 250) };
        AddChild(visual);
        visual.AddChild(new Label { ThemeTypeVariation = "TooltipLabel", Text = "新建空白地图（打桩）" });
    }

    private void BuildUi()
    {
        var bg = new ColorRect { Color = UiTheme.WindowBg, MouseFilter = MouseFilterEnum.Ignore };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 0);
        AddChild(root);

        _ribbon = new Ribbon();
        root.AddChild(_ribbon);

        var mainArea = new HBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        mainArea.AddThemeConstantOverride("separation", 0);
        root.AddChild(mainArea);

        var viewWrap = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
        };
        mainArea.AddChild(viewWrap);

        _view = new ViewportPanel();
        _view.SetAnchorsPreset(LayoutPreset.FullRect);
        _view.CursorMoved += OnCursorMoved;
        _ribbon.MinimapToggled += SetMinimapVisible;
        _ribbon.LightingToggled += _view.SetLighting;
        _ribbon.UnitsToggled += on =>
        {
            _view.SetUnitsVisible(on);
            ShowToast(on ? "显示单位" : "隐藏单位");
        };
        _ribbon.DoodadsToggled += on =>
        {
            _view.SetDoodadsVisible(on);
            ShowToast(on ? "显示装饰物" : "隐藏装饰物");
        };
        _ribbon.PathingToggled += (layer, on) =>
        {
            _view.SetPathingVisible(layer, on);
            ShowToast($"{ViewportPanel.LayerName(layer)}寻路 {(on ? "开" : "关")}");
        };
        _ribbon.BrushToggled += _view.SetBrushVisible;
        _ribbon.WireframeToggled += _view.SetWireframe;
        _view.CameraChanged += UpdateWindowTitle;
        viewWrap.AddChild(_view);
        _ribbon.SetUnitsToggle(_view.UnitsVisible);
        _ribbon.SetDoodadsToggle(_view.DoodadsVisible);
        _ribbon.SetPathingToggle(PathingLayer.Ground, _view.PathingVisible(PathingLayer.Ground));
        _ribbon.SetPathingToggle(PathingLayer.Building, _view.PathingVisible(PathingLayer.Building));
        _ribbon.SetPathingToggle(PathingLayer.Flying, _view.PathingVisible(PathingLayer.Flying));
        _ribbon.SetBrushToggle(_view.BrushVisible);
        _ribbon.SetWireframeToggle(_view.Wireframe);

        _minimap = new MinimapPanel();
        _minimap.SetAnchorsPreset(LayoutPreset.TopLeft);
        _minimap.Position = new Vector2(10, 10);
        viewWrap.AddChild(_minimap);

        BuildStatusBar(root);
        BuildToast(viewWrap);

    }

    private static readonly bool ShowStatusBar = false;

    private void BuildStatusBar(Control parent)
    {
        if (!ShowStatusBar)
        {
            return;
        }
        var bar = new PanelContainer { CustomMinimumSize = new Vector2(0, 26) };
        var sb = UiTheme.Box(UiTheme.PanelAlt, UiTheme.Border, 0);
        sb.ContentMarginLeft = 8;
        sb.ContentMarginRight = 8;
        sb.ContentMarginTop = 2;
        sb.ContentMarginBottom = 2;
        bar.AddThemeStyleboxOverride("panel", sb);

        var row = W.Row(14);
        _lblMap = W.L("地图：未打开（打桩）", 11, UiTheme.TextDim);
        _lblBrush = W.L("笔刷：圆形 · 5", 11, UiTheme.TextDim);
        _lblCoord = W.L("坐标：—", 11, UiTheme.TextDim);
        _lblHint = W.L("就绪", 11, Color.FromHtml("3d7a35"));
        _lblFps = W.L("60 FPS", 11, UiTheme.TextDim);

        row.AddChild(_lblMap);
        row.AddChild(W.Dim("│", 11));
        row.AddChild(_lblBrush);
        row.AddChild(W.Dim("│", 11));
        row.AddChild(_lblCoord);
        row.AddChild(W.Dim("│", 11));
        row.AddChild(_lblHint);
        row.AddChild(W.Spacer());
        row.AddChild(_lblFps);

        bar.AddChild(row);
        parent.AddChild(bar);
    }

    private void BuildToast(Control parent)
    {
        _toast = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        var sb = UiTheme.Box(Color.FromHtml("fffef7"), UiTheme.BorderLight, 4);
        sb.ContentMarginLeft = 14;
        sb.ContentMarginRight = 14;
        sb.ContentMarginTop = 7;
        sb.ContentMarginBottom = 7;
        _toast.AddThemeStyleboxOverride("panel", sb);

        _toastLabel = W.L("", 12);
        _toast.AddChild(_toastLabel);
        _toast.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _toast.GrowHorizontal = GrowDirection.Both;
        _toast.GrowVertical = GrowDirection.Begin;
        _toast.OffsetBottom = -28;
        _toast.OffsetTop = -60;
        parent.AddChild(_toast);
    }

    private static (string Title, Vector2I Size, System.Func<Control> Body, System.Func<Control> Strip) PaletteDef(string id)
    {
        return id switch
        {
            "terrain" => ("地形", new Vector2I(284, 592), Palettes.BuildTerrain, Palettes.BuildTerrainRibbon),
            "doodads" => ("装饰物", new Vector2I(244, 560), Palettes.BuildDoodads, Palettes.BuildDoodadRibbon),
            "pathing" => ("寻路", new Vector2I(242, 265), Palettes.BuildPathing, () => null),
            "units" => ("单位", new Vector2I(300, 520), Palettes.BuildUnits, () => null),
            "regions" => ("区域", new Vector2I(300, 460), Palettes.BuildRegions, () => null),
            _ => (id, new Vector2I(280, 400), () => null, () => null),
        };
    }

    private void OpenPalette(string id)
    {
        if (_paletteId == id)
        {
            ClosePalette();
            return;
        }

        ClosePalette();

        var def = PaletteDef(id);
        var body = def.Body();
        if (body == null)
        {
            return;
        }

        _paletteId = id;

        var win = new Window
        {
            Title = def.Title + " Palette",
            Size = def.Size,
            Transient = true,
            Exclusive = false,
            Theme = UiTheme.Build(),
        };
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        var sb = UiTheme.Box(UiTheme.PanelBg, UiTheme.Border, 0);
        sb.ContentMarginLeft = 6;
        sb.ContentMarginRight = 6;
        sb.ContentMarginTop = 6;
        sb.ContentMarginBottom = 6;
        panel.AddThemeStyleboxOverride("panel", sb);
        panel.AddChild(body);
        win.AddChild(panel);
        win.CloseRequested += ClosePalette;

        AddChild(win);

        var main = GetWindow();
        win.Position = new Vector2I(
            main.Position.X + main.Size.X - win.Size.X - 10,
            main.Position.Y + 116 + 29);

        _paletteWindow = win;

        var strip = def.Strip();
        if (strip != null && !_ribbon.HasPaletteTab(def.Title))
        {
            _ribbon.AddPaletteTab(def.Title, strip);
        }
    }

    private void ClosePalette()
    {
        if (_paletteId == null)
        {
            return;
        }

        _ribbon.RemovePaletteTab(PaletteDef(_paletteId).Title);
        _paletteId = null;

        if (_paletteWindow != null)
        {
            var win = _paletteWindow;
            _paletteWindow = null;
            RemoveChild(win);
            win.QueueFree();
        }
    }

    private void OpenWindow(string id)
    {
        CloseWindow();

        Window host;
        switch (id)
        {
            case "mapinfo":
            {
                Windows.MapInfoView view = Windows.MapInfo(_currentMap);
                host = Windows.Host("地图信息", new Vector2I(720, 560), view.Root,
                    Windows.Footer(() => { CommitMapName(view); CloseWindow(); }, CloseWindow), CloseWindow);
                break;
            }
            case "trigger":
            {
                var body = Windows.TriggerEditor();
                host = Windows.Host("触发器编辑器", new Vector2I(1000, 640), body, null, CloseWindow, modal: false);
                break;
            }
            case "import":
            {
                var body = Windows.ImportManager();
                host = Windows.Host("导入管理器", new Vector2I(900, 600), body, null, CloseWindow, modal: false);
                break;
            }
            case "settings":
            {
                var body = Windows.Settings(_view);
                host = Windows.Host("设置", new Vector2I(560, 470), body,
                    Windows.Footer(() => { ShowToast("设置已应用"); CloseWindow(); }, CloseWindow), CloseWindow);
                break;
            }
            case "tilesetter":
            {
                var body = Windows.TileSetter();
                host = Windows.Host("修改地形集", new Vector2I(595, 290), body,
                    Windows.Footer(() => { Stub.Fire("tilesetter.ok"); CloseWindow(); }, CloseWindow), CloseWindow);
                break;
            }
            case "tilepather":
            {
                var body = Windows.TilePather();
                host = Windows.Host("瓦片寻路", new Vector2I(590, 200), body,
                    Windows.Footer(() => { Stub.Fire("tilepather.ok"); CloseWindow(); }, CloseWindow), CloseWindow);
                break;
            }
            case "tilepicker":
            {
                var body = Windows.TilePicker();
                host = Windows.Host("替换瓦片", new Vector2I(595, 225), body,
                    Windows.Footer(() => { Stub.Fire("tilepicker.ok"); CloseWindow(); }, CloseWindow), CloseWindow);
                break;
            }
            case "objecteditor":
            {
                var body = Windows.ObjectEditor();
                host = Windows.Host("对象编辑器（规划中）", new Vector2I(940, 560), body, null, CloseWindow, modal: false);
                break;
            }
            case "players":
            {
                var body = Windows.Players();
                host = Windows.Host("玩家与队伍（规划中）", new Vector2I(760, 540), body, null, CloseWindow, modal: false);
                break;
            }
            case "model":
            {
                var body = Windows.ModelPreview();
                host = Windows.Host("模型预览（规划中）", new Vector2I(860, 620), body, null, CloseWindow, modal: false);
                break;
            }
            default:
                return;
        }

        AddChild(host);
        CenterOnMainWindow(host);
        _currentWindow = host;

        if (OS.GetEnvironment("WE_LAYOUT") == "1")
        {
            CallDeferred(nameof(DumpLayout));
        }
    }

    private void CenterOnMainWindow(Window win)
    {
        var main = GetWindow();
        var offset = (main.Size - win.Size) / 2;
        win.Position = main.Position + offset;
    }

    private void DumpLayout()
    {
        if (_currentWindow == null)
        {
            return;
        }
        GD.Print($"[LAYOUT] window={_currentWindow.Size}");
        foreach (var n in _currentWindow.FindChildren("*", "Control", true, false))
        {
            if (n is Control c && (c is Container || c is TabContainer || c.Name == "MapInfoTabs"))
            {
                GD.Print($"[LAYOUT] {c.GetType().Name,-16} name={c.Name,-14} size={c.Size} min={c.GetCombinedMinimumSize()} parent={c.GetParent()?.GetType().Name}");
            }
        }
    }

    private void CloseWindow()
    {
        if (_currentWindow == null)
        {
            return;
        }
        var win = _currentWindow;
        _currentWindow = null;
        RemoveChild(win);
        win.QueueFree();
    }

    private void OnStubFired(string id)
    {
        switch (id)
        {
            case "ui.terrain":
                OpenPalette("terrain");
                return;
            case "ui.doodads":
                OpenPalette("doodads");
                return;
            case "ui.pathing":
                OpenPalette("pathing");
                return;
            case "ui.units":
                OpenPalette("units");
                return;
            case "ui.regions":
                OpenPalette("regions");
                return;
            case "ui.mapinfo":
                OpenWindow("mapinfo");
                return;
            case "ui.trigger":
                OpenWindow("trigger");
                return;
            case "ui.import":
                OpenWindow("import");
                return;
            case "ui.settings":
                OpenWindow("settings");
                return;
            case "ui.tilesetter":
                OpenWindow("tilesetter");
                return;
            case "ui.tilepather":
                OpenWindow("tilepather");
                return;
            case "ui.tilepicker":
                OpenWindow("tilepicker");
                return;
            case "ui.objecteditor":
                OpenWindow("objecteditor");
                return;
            case "ui.players":
                OpenWindow("players");
                return;
            case "ui.model":
                OpenWindow("model");
                return;
            case "file.open":
                ShowOpenMapDialog();
                return;
            case "file.save":
                SaveCurrentMap(false);
                return;
            case "file.save_as":
                SaveCurrentMap(true);
                return;
            case "file.exit":
                GetTree().Quit();
                return;
            case "view.reset_camera":
                _view.ResetCamera();
                ShowToast("相机已重置");
                return;
            case "view.switch_camera":
                ShowToast("切换相机：打桩（当前只有轨道相机）");
                return;
        }

        ShowToast($"[打桩] {id}");
    }

    private void OpenMapFile(string path)
    {
        try
        {

            MapDocument document = Directory.Exists(path) ? MapDocument.LoadDirectory(path) : MapDocument.Load(path);
            MapDocument previous = _currentMap;
            _currentMap = document;
            _view.LoadMap(document);

            previous?.Release();
            if (document.Terrain != null)
            {
                _minimap.LoadMap(document.Terrain);
            }

            string name = document.MapName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = Path.GetFileNameWithoutExtension(path);
            }
            ApplyMapName(name);

            GD.Print($"[MAP] 打开 {path} 名称='{name}' 文件数={document.Entries.Count} 尺寸={document.Terrain?.Width}x{document.Terrain?.Height}");
            foreach (string warning in document.Warnings)
            {
                GD.PushWarning($"[MAP] 条目未能载入 {warning}");
            }
            ShowToast($"已打开 {name}");

            ApplyStartupView();

            if (_smokePending)
            {
                _smokePending = false;
                RunSmokeTest();
            }
        }
        catch (Exception e)
        {
            GD.PushError($"[MAP] 打开失败 {path}: {e.GetType().Name} {e.Message}");
            ShowToast($"打开失败：{e.Message}");
        }
    }

    private void OpenSceneFile(string path)
    {
        _view.LoadScene(path);
        ApplyMapName(Path.GetFileNameWithoutExtension(path));
        ApplyStartupView();
    }

    private void ApplyStartupView()
    {
        if (!float.IsNaN(_startupYaw) || !float.IsNaN(_startupPitch))
        {
            _view.SetOrbit(
                float.IsNaN(_startupYaw) ? _view.Yaw : _startupYaw,
                float.IsNaN(_startupPitch) ? _view.Pitch : _startupPitch);
        }

        if (_startupCamera.HasValue)
        {

            _view.SetCameraPosition(_startupCamera.Value);
        }
        else if (_startupFocus.X >= 0f)
        {
            _view.FocusOn(_startupFocus);
        }
        if (_startupDistance > 0f)
        {
            _view.SetDistance(_startupDistance);
        }

        if (_cameraTest)
        {
            _view.RunCameraSelfTest();
            GetTree().Quit();
        }

        if (_pendingWindow != null)
        {
            string pending = _pendingWindow;
            _pendingWindow = null;
            OpenWindow(pending);
        }
    }

    private void ShowOpenMapDialog()
    {
        var dialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Title = "打开地图",
            Size = new Vector2I(900, 600),
            UseNativeDialog = false,
            Exclusive = false,
            Theme = UiTheme.Build(),
        };
        dialog.AddFilter("*.w3m,*.w3x", "War3 地图");

        string documents = OS.GetSystemDir(OS.SystemDir.Documents);
        string maps = Path.Combine(documents, "Warcraft III", "Maps");
        dialog.CurrentDir = Directory.Exists(maps) ? maps : documents;

        dialog.FileSelected += path =>
        {
            OpenMapFile(path);
            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void SaveCurrentMap(bool saveAs)
    {
        if (_currentMap == null)
        {
            ShowToast("还没有打开地图");
            return;
        }

        if (saveAs)
        {
            var dialog = new FileDialog
            {
                FileMode = FileDialog.FileModeEnum.SaveFile,
                Access = FileDialog.AccessEnum.Filesystem,
                Title = "地图另存为",
                Size = new Vector2I(900, 600),
                UseNativeDialog = false,
                Exclusive = false,
                Theme = UiTheme.Build(),
                CurrentFile = Path.GetFileName(_currentMap.SourcePath),
            };
            dialog.AddFilter("*.w3m,*.w3x", "War3 地图");
            dialog.FileSelected += path =>
            {
                WriteMap(path);
                dialog.QueueFree();
            };
            dialog.Canceled += () => dialog.QueueFree();
            AddChild(dialog);
            dialog.PopupCentered();
            return;
        }

        WriteMap(_currentMap.SourcePath);
    }

    private void WriteMap(string path)
    {
        try
        {
            _currentMap.Save(path);
            ShowToast($"已保存到 {Path.GetFileName(path)}");
            GD.Print($"[MAP] 保存 {path}");
        }
        catch (Exception e)
        {
            GD.PushError($"[MAP] 保存失败 {path}: {e.Message}");
            ShowToast($"保存失败：{e.Message}");
        }
    }

    private void CommitMapName(Windows.MapInfoView view)
    {
        if (_currentMap == null || view?.Name == null)
        {
            return;
        }

        string name = view.Name.Text.Trim();
        if (string.IsNullOrEmpty(name) || name == _currentMap.MapName)
        {
            return;
        }

        try
        {
            _currentMap.SetMapName(name);
            ApplyMapName(name);
            ShowToast($"地图已改名为 {name}（保存后生效）");
            GD.Print($"[MAP] 改名 -> {name}");
        }
        catch (Exception e)
        {
            GD.PushError($"[MAP] 改名失败: {e.Message}");
            ShowToast($"改名失败：{e.Message}");
        }
    }

    private void ApplyMapName(string name)
    {
        _mapDisplayName = string.IsNullOrEmpty(name) ? "未打开地图" : name;
        if (_lblMap != null)
        {
            _lblMap.Text = _mapDisplayName;
        }
        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        if (_view == null)
        {
            return;
        }
        Vector3 cam = _view.CameraPosition;
        string title = $"WorldEditor - {_mapDisplayName} - 相机坐标：({cam.X:0}, {cam.Y:0}, {cam.Z:0})";
        if (title == _lastTitle)
        {
            return;
        }
        _lastTitle = title;
        GetWindow().Title = title;
    }

    private void SetMinimapVisible(bool visible)
    {
        if (_minimap != null)
        {
            _minimap.Visible = visible;
        }
    }

    private void ToggleUnitsVisible()
    {
        bool next = !_view.UnitsVisible;
        _view.SetUnitsVisible(next);
        _ribbon.SetUnitsToggle(next);
        ShowToast(next ? "显示单位" : "隐藏单位");
    }

    private void ToggleDoodadsVisible()
    {
        bool next = !_view.DoodadsVisible;
        _view.SetDoodadsVisible(next);
        _ribbon.SetDoodadsToggle(next);
        ShowToast(next ? "显示装饰物" : "隐藏装饰物");
    }

    private void TogglePathingLayer(PathingLayer layer)
    {
        bool next = !_view.PathingVisible(layer);
        _view.SetPathingVisible(layer, next);
        _ribbon.SetPathingToggle(layer, next);
        ShowToast($"{ViewportPanel.LayerName(layer)}寻路 {(next ? "开" : "关")}");
    }

    private void ToggleWireframe()
    {
        bool next = !_view.Wireframe;
        _view.SetWireframe(next);
        _ribbon.SetWireframeToggle(next);
        ShowToast(next ? "线框开" : "线框关");
    }

    private void OnCursorMoved(Vector2 cell, float height)
    {
        if (_lblCoord != null)
        {
            _lblCoord.Text = $"坐标：{cell.X:0.0}, {cell.Y:0.0}  高度 {height:0.0}";
        }
    }

    private void ShowToast(string message)
    {
        if (_toast == null)
        {
            return;
        }
        _toastLabel.Text = message;
        _toast.Visible = true;
        _toastLeft = 2.4;
    }

    public override void _Process(double delta)
    {
        if (_toastLeft > 0)
        {
            _toastLeft -= delta;
            if (_toastLeft <= 0)
            {
                _toast.Visible = false;
            }
        }

        _fpsTimer += delta;
        _fpsFrames++;
        if (_fpsTimer >= 0.5)
        {
            if (_lblFps != null)
            {
                _lblFps.Text = $"{_fpsFrames / _fpsTimer:0} FPS";
            }
            _fpsTimer = 0;
            _fpsFrames = 0;
        }

        if (_shotPath != null && ++_frames >= _shotFrames)
        {

            var img = _shotView ? _view.CaptureView() : GetViewport().GetTexture().GetImage();
            img.SavePng(_shotPath);
            GD.Print($"[SHOT] {_shotPath} {img.GetWidth()}x{img.GetHeight()} title='{GetWindow().Title}'");

            if (_paletteWindow != null)
            {
                var pal = _paletteWindow.GetTexture().GetImage();
                string palPath = _shotPath.Replace(".png", "-palette.png");
                pal.SavePng(palPath);
                GD.Print($"[SHOT] {palPath} {pal.GetWidth()}x{pal.GetHeight()} at {_paletteWindow.Position}");
            }

            int extra = 0;
            foreach (Node child in GetChildren())
            {
                if (child is Window w && w.Visible && w != _paletteWindow && w != _currentWindow)
                {
                    var shot = w.GetTexture().GetImage();
                    string path = _shotPath.Replace(".png", $"-win{extra}.png");
                    shot.SavePng(path);
                    GD.Print($"[SHOT] {path} {shot.GetWidth()}x{shot.GetHeight()} title='{w.Title}'");
                    extra++;
                }
            }

            if (_currentWindow != null)
            {
                var sub = _currentWindow.GetTexture().GetImage();
                string subPath = _shotPath.Replace(".png", "-window.png");
                sub.SavePng(subPath);
                GD.Print($"[SHOT] {subPath} {sub.GetWidth()}x{sub.GetHeight()} at {_currentWindow.Position}");
            }

            GetTree().Quit();
        }
    }

    private bool ProbeRibbonHeight()
    {
        _probeFrame++;
        int count = _ribbon.GetTabCount();

        if (_probeFrame % 4 == 0 && _probeTab < count)
        {
            _ribbon.CurrentTab = _probeTab;
            return false;
        }

        if (_probeFrame % 4 == 2 && _probeTab < count)
        {
            GD.Print($"[TABS] {_probeTab} {_ribbon.GetTabTitle(_probeTab)} ribbonHeight={_ribbon.Size.Y} contentHeight={((Control)_ribbon.GetChild(_probeTab)).Size.Y}");
            _probeTab++;
            if (_probeTab >= count)
            {
                GD.Print("[TABS] done");
                GetTree().Quit();
                return true;
            }
        }
        return false;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey k || !k.Pressed || k.Echo)
        {
            return;
        }

        bool ctrl = k.CtrlPressed;
        bool shift = k.ShiftPressed;

        switch (k.Keycode)
        {
            case Key.O when ctrl:
                Stub.Fire("file.open");
                break;
            case Key.S when ctrl && shift:
                Stub.Fire("file.save_as");
                break;
            case Key.S when ctrl:
                Stub.Fire("file.save");
                break;
            case Key.T when ctrl:
                Stub.Fire("file.test");
                break;
            case Key.Z when ctrl:
                Stub.Fire("edit.undo");
                break;
            case Key.Y when ctrl:
                Stub.Fire("edit.redo");
                break;
            case Key.F1:
                Stub.Fire("view.switch_camera");
                break;
            case Key.F3:
                Stub.Fire("view.debug");
                break;
            case Key.F5:
                Stub.Fire("editor.reload_theme");
                break;
            case Key.U when !ctrl:
                ToggleUnitsVisible();
                break;
            case Key.D when !ctrl:
                ToggleDoodadsVisible();
                break;
            case Key.P when !ctrl:
                TogglePathingLayer(PathingLayer.Ground);
                break;
            case Key.T when !ctrl:
                ToggleWireframe();
                break;
            case Key.Escape:
                if (_currentWindow != null)
                {
                    CloseWindow();
                }
                else
                {
                    ClosePalette();
                }
                break;
        }
    }

    private void ParseCommandLine()
    {
        var args = new System.Collections.Generic.List<string>();
        args.AddRange(OS.GetCmdlineUserArgs());
        args.AddRange(OS.GetCmdlineArgs());

        string palette = null;
        string window = null;
        bool smoke = false;

        foreach (var a in args)
        {
            if (a.StartsWith("--shot="))
            {
                _shotPath = a.Substring("--shot=".Length);
            }
            else if (a == "--shot-view")
            {

                _shotView = true;
            }
            else if (a.StartsWith("--minimap-dump="))
            {

                string[] parts = a.Substring("--minimap-dump=".Length).Split('|');
                if (parts.Length == 2)
                {
                    MapDocument doc = MapDocument.Load(parts[0]);
                    Texture2D tex = MinimapPanel.BuildFromTerrain(doc.Terrain, new WorldEdit.Assets.TerrainTileSet());
                    tex.GetImage().SavePng(parts[1]);
                    GD.Print($"[MINIMAP] {parts[1]} {tex.GetWidth()}x{tex.GetHeight()}");
                    GetTree().Quit();
                    return;
                }
            }
            else if (a.StartsWith("--yaw=") && float.TryParse(a.Substring("--yaw=".Length), out float yaw))
            {
                _startupYaw = yaw;
            }
            else if (a.StartsWith("--pitch=") && float.TryParse(a.Substring("--pitch=".Length), out float pitch))
            {
                _startupPitch = pitch;
            }
            else if (a == "--no-minimap")
            {
                _startupMinimap = false;
            }
            else if (a == "--no-lighting")
            {
                _startupLighting = false;
            }
            else if (a.StartsWith("--cam="))
            {
                string[] xyz = a.Substring("--cam=".Length).Split(',');
                if (xyz.Length == 3
                    && float.TryParse(xyz[0], out float cx)
                    && float.TryParse(xyz[1], out float cy)
                    && float.TryParse(xyz[2], out float cz))
                {
                    _startupCamera = new Vector3(cx, cy, cz);
                }
            }
            else if (a.StartsWith("--fov=") && float.TryParse(a.Substring("--fov=".Length), out float fov))
            {
                _startupFov = fov;
            }
            else if (a.StartsWith("--view-size="))
            {
                string[] wh = a.Substring("--view-size=".Length).Split('x');
                if (wh.Length == 2 && int.TryParse(wh[0], out int vw) && int.TryParse(wh[1], out int vh))
                {
                    _viewSize = new Vector2I(vw, vh);
                }
            }
            else if (a.StartsWith("--frames=") && int.TryParse(a.Substring("--frames=".Length), out int f))
            {
                _shotFrames = f;
            }
            else if (a.StartsWith("--palette="))
            {
                palette = a.Substring("--palette=".Length);
            }
            else if (a.StartsWith("--window="))
            {
                window = a.Substring("--window=".Length);
            }
            else if (a == "--smoke")
            {
                smoke = true;
            }
            else if (a == "--camera-test")
            {
                _cameraTest = true;
            }
            else if (a == "--tooltip-probe")
            {
                _tooltipProbe = true;
            }
            else if (a.StartsWith("--focus="))
            {
                string[] xy = a.Substring("--focus=".Length).Split(',');
                if (xy.Length == 2 && float.TryParse(xy[0], out float fx) && float.TryParse(xy[1], out float fz))
                {
                    _startupFocus = new Vector2(fx, fz);
                }
            }
            else if (a.StartsWith("--reopen="))
            {

                _reopenPath = a.Substring("--reopen=".Length);
            }
            else if (a.StartsWith("--dist=") && float.TryParse(a.Substring("--dist=".Length), out float dist))
            {
                _startupDistance = dist;
            }
            else if (a.StartsWith("--settings-tab=") && int.TryParse(a.Substring("--settings-tab=".Length), out int settingsTab))
            {
                Windows.SettingsTab = settingsTab;
            }
            else if (a == "--map-dialog")
            {
                CallDeferred(nameof(ShowOpenMapDialog));
            }
            else if (a.StartsWith("--map="))
            {
                _startupMap = a.Substring("--map=".Length);
            }
            else if (a.StartsWith("--export-scene="))
            {

                string[] parts = a.Substring("--export-scene=".Length).Split('|');
                if (parts.Length == 2)
                {
                    WorldEdit.Assets.MapSceneExporter.Export(parts[0], parts[1]);
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--scene="))
            {
                _startupScene = a.Substring("--scene=".Length);
            }
            else if (a == "--no-brush")
            {
                _noBrush = true;
            }
            else if (a.StartsWith("--art-lab-terrain="))
            {

                WorldEdit.Assets.TerrainLabExporter.Export(a.Substring("--art-lab-terrain=".Length));
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--art-lab-units="))
            {

                WorldEdit.Assets.UnitLabExporter.Export(a.Substring("--art-lab-units=".Length));
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--game-dir="))
            {
                WorldEdit.Assets.War3Assets.SetGameRoot(a.Substring("--game-dir=".Length));
            }
            else if (a.StartsWith("--shot-diff="))
            {
                string[] parts = a.Substring("--shot-diff=".Length).Split('|');
                if (parts.Length >= 2)
                {
                    int block = parts.Length > 2 && int.TryParse(parts[2], out int parsed) ? parsed : 64;
                    WorldEdit.Core.Tools.ShotCli.Diff(parts[0], parts[1], block);
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--shot-mirror="))
            {
                string[] parts = a.Substring("--shot-mirror=".Length).Split('|');
                if (parts.Length == 2)
                {
                    WorldEdit.Core.Tools.ShotCli.Mirror(parts[0], parts[1]);
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--shot-water="))
            {
                string[] parts = a.Substring("--shot-water=".Length).Split('|');
                if (parts.Length == 2)
                {
                    WorldEdit.Core.Tools.ShotCli.Water(parts[0], parts[1]);
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--crop="))
            {

                string[] parts = a.Substring("--crop=".Length).Split('|');
                if (parts.Length >= 3)
                {
                    string[] box = parts[2].Split(',');
                    if (box.Length == 4)
                    {
                        int scale = parts.Length > 3 && int.TryParse(parts[3], out int parsed) ? parsed : 3;
                        WorldEdit.Core.Tools.ShotCli.Crop(
                            parts[0], parts[1],
                            int.Parse(box[0]), int.Parse(box[1]),
                            int.Parse(box[2]), int.Parse(box[3]), scale);
                    }
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--w3e-probe="))
            {

                string[] parts = a.Substring("--w3e-probe=".Length).Split('|');
                if (parts.Length >= 2)
                {
                    WorldEdit.Core.Tools.TerraCli.Probe(parts[0], parts[1], parts.Length > 2 ? parts[2] : "");
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--draw-tiles="))
            {

                string[] parts = a.Substring("--draw-tiles=".Length).Split('|');
                if (parts.Length >= 2)
                {
                    WorldEdit.Core.Tools.TerraCli.DrawTiles(
                        parts[0], parts[1],
                        parts.Length > 2 ? parts[2] : "",
                        parts.Length > 3 ? parts[3] : "",
                        parts.Length > 4 ? parts[4] : "");
                }
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--unit-dump="))
            {
                WorldEdit.Core.Tools.TerraCli.DumpUnits(a.Substring("--unit-dump=".Length));
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--doodad-dump="))
            {
                WorldEdit.Core.Tools.TerraCli.DumpDoodads(a.Substring("--doodad-dump=".Length));
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--paint-map="))
            {

                string[] parts = a.Substring("--paint-map=".Length).Split('|');
                WorldEdit.Core.Tools.TerraCli.PaintMap(
                    parts[0],
                    parts.Length > 1 ? parts[1] : "",
                    parts.Length > 2 ? parts[2] : "");
                GetTree().Quit();
                return;
            }
            else if (a.StartsWith("--map-saveas="))
            {
                string[] parts = a.Substring("--map-saveas=".Length).Split('|');
                if (parts.Length == 3)
                {
                    WorldEdit.Core.Tools.MapCli.SaveAs(parts[0], parts[1], parts[2]);
                }
                GetTree().Quit();
                return;            }
            else if (a.StartsWith("--map-report="))
            {
                WorldEdit.Core.Tools.MapCli.Report(a.Substring("--map-report=".Length));
                GetTree().Quit();
                return;
            }
            else if (a == "--check-tabs")
            {
                _checkTabs = true;
            }
            else if (a.StartsWith("--tab=") && int.TryParse(a.Substring("--tab=".Length), out int tab))
            {
                _ribbon.CurrentTab = tab;
            }
        }

        SetMinimapVisible(_startupMinimap);
        _view.SetLighting(_startupLighting);

        if (_startupFov > 0f)
        {
            _view.SetFov(_startupFov);
        }

        if (_viewSize.X > 0 && _viewSize.Y > 0)
        {
            _view.SetViewSize(_viewSize);
        }

        if (palette != null)
        {
            if (palette == "none")
            {
                ClosePalette();
            }
            else
            {
                ClosePalette();
                OpenPalette(palette);
            }
        }

        if (window != null)
        {
            if (_startupMap != null || _startupScene != null)
            {
                _pendingWindow = window;
            }
            else
            {
                OpenWindow(window);
            }
        }

        if (_startupScene != null)
        {

            if (_noBrush)
            {
                _view.SetBrushVisible(false);
            }
            CallDeferred(nameof(OpenSceneFile), _startupScene);
        }
        else if (_startupMap != null)
        {
            CallDeferred(nameof(OpenMapFile), _startupMap);
            if (_reopenPath != null)
            {
                string second = _reopenPath;
                _reopenPath = null;
                GetTree().CreateTimer(0.8).Timeout += () => OpenMapFile(second);
            }
        }

        if (smoke)
        {

            if (_startupMap != null)
            {
                _smokePending = true;
            }
            else
            {
                RunSmokeTest();
            }
        }
    }

    private void RunSmokeTest()
    {
        string[] ids =
        {
            "file.new", "file.open", "file.save", "file.save_as", "file.test", "ui.mapinfo", "ui.settings",
            "ui.terrain", "ui.terrain", "ui.doodads", "ui.doodads", "ui.pathing", "ui.pathing",
            "ui.units", "ui.units", "ui.regions", "ui.regions",
            "ui.trigger", "ui.import", "ui.tilesetter", "ui.tilepather", "ui.tilepicker",
            "ui.objecteditor", "ui.players", "ui.model",
            "edit.undo", "edit.redo", "view.reset_camera", "view.switch_camera",
            "terrain.cliff.压平", "terrain.deform.抬升", "doodad.select", "region.add",
        };

        int ok = 0;
        foreach (var id in ids)
        {
            OnStubFired(id);
            ok++;
        }
        ToggleDoodadsVisible();
        ToggleDoodadsVisible();
        ToggleUnitsVisible();
        ToggleUnitsVisible();
        TogglePathingLayer(PathingLayer.Ground);
        TogglePathingLayer(PathingLayer.Building);
        TogglePathingLayer(PathingLayer.Flying);
        TogglePathingLayer(PathingLayer.Building);
        TogglePathingLayer(PathingLayer.Flying);
        TogglePathingLayer(PathingLayer.Ground);
        ToggleWireframe();
        ToggleWireframe();
        GD.Print($"[SMOKE] {ok}/{ids.Length} actions ok, palette={_paletteId ?? "none"}, window={_currentWindow?.Title ?? "none"}");
        GetTree().Quit();
    }
}
