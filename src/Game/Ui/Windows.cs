using Godot;
using WorldEdit.Core.Map;

namespace WorldEdit;

public static class Windows
{
    public static Window Host(string title, Vector2I size, Control body, Control footer, System.Action onClose, bool modal = true)
    {
        var win = new Window
        {
            Title = title,
            Size = size,
            MinSize = new Vector2I(360, 240),
            Transient = true,
            Exclusive = modal,
            Unresizable = false,
            Theme = UiTheme.Build(),
        };
        win.CloseRequested += onClose;

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var sb = UiTheme.Box(UiTheme.PanelBg, UiTheme.Border, 0);
        sb.ContentMarginLeft = 0;
        sb.ContentMarginRight = 0;
        sb.ContentMarginTop = 0;
        sb.ContentMarginBottom = 0;
        panel.AddThemeStyleboxOverride("panel", sb);

        var col = W.Col(0);
        var bodyWrap = W.Pad(body, 10, 10, 10, 8);
        bodyWrap.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(bodyWrap);

        if (footer != null)
        {
            col.AddChild(new HSeparator());
            col.AddChild(W.Pad(footer, 10, 6, 10, 8));
        }

        panel.AddChild(col);
        win.AddChild(panel);
        return win;
    }

    public static Control Footer(System.Action ok, System.Action cancel)
    {
        var row = W.Row(6);
        row.Alignment = BoxContainer.AlignmentMode.End;
        var okBtn = W.B("确定");
        okBtn.CustomMinimumSize = new Vector2(80, 26);
        okBtn.Pressed += ok;
        var cancelBtn = W.B("取消");
        cancelBtn.CustomMinimumSize = new Vector2(80, 26);
        cancelBtn.Pressed += cancel;
        row.AddChild(okBtn);
        row.AddChild(cancelBtn);
        return row;
    }

    private static HBoxContainer Field(string label, Control input, int labelWidth = 96)
    {
        var row = W.Row(8);
        var l = W.L(label, 12, UiTheme.TextDim);
        l.CustomMinimumSize = new Vector2(labelWidth, 0);
        l.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(l);
        input.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(input);
        return row;
    }

    public static Control Note(string text)
    {
        var l = W.L(text, 11, UiTheme.Warn);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return l;
    }

    private static Control Banner(string text)
    {
        var panel = new PanelContainer();
        var sb = UiTheme.Box(UiTheme.BannerBg, UiTheme.BannerBorder, 4);
        panel.AddThemeStyleboxOverride("panel", sb);
        var l = W.L(text, 11, UiTheme.BannerText);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        panel.AddChild(l);
        return panel;
    }

    public sealed class MapInfoView
    {
        public Control Root;
        public LineEdit Name;
    }

    public static MapInfoView MapInfo(MapDocument document)
    {
        var view = new MapInfoView();
        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        tabs.CustomMinimumSize = new Vector2(680, 440);

        var desc = W.Col(8);
        var nameEdit = W.Edit("", document?.MapName ?? "");
        nameEdit.Editable = document != null;
        nameEdit.TooltipText = document == null ? "先打开一张地图" : "改名会写进 war3map.wts，保存后生效";
        view.Name = nameEdit;
        desc.AddChild(Field("地图名称", nameEdit));
        desc.AddChild(Field("建议玩家", W.Edit("", "2")));
        var descBox = new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 110),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Text = "[打桩] 地图描述占位文本。",
        };
        descBox.AddThemeFontSizeOverride("font_size", 12);
        desc.AddChild(W.L("描述", 12, UiTheme.TextDim));
        desc.AddChild(descBox);
        desc.AddChild(Field("作者", W.Edit("", "Sekia")));
        var meta = W.Row(24);
        meta.AddChild(W.L("地图版本：28", 12, UiTheme.TextDim));
        meta.AddChild(W.L("编辑器版本：6061", 12, UiTheme.TextDim));
        desc.AddChild(meta);
        tabs.AddChild(desc);
        tabs.SetTabTitle(0, "描述");

        var load = W.Col(8);
        var radios = W.Col(4);
        radios.AddChild(new CheckBox { Text = "使用默认画面", ButtonPressed = true });
        radios.AddChild(new CheckBox { Text = "使用战役画面" });
        radios.AddChild(new CheckBox { Text = "使用导入的文件" });
        load.AddChild(radios);
        load.AddChild(Field("战役画面", W.Combo("(无)", "人类战役", "兽族战役", "不死族战役")));
        load.AddChild(Field("导入文件", W.Combo("(无)")));
        load.AddChild(Field("标题", W.Edit("", "洛丹伦的夏天")));
        load.AddChild(Field("副标题", W.Edit("", "")));
        load.AddChild(W.L("载入文本", 12, UiTheme.TextDim));
        load.AddChild(new TextEdit
        {
            CustomMinimumSize = new Vector2(0, 80),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Text = "[打桩]",
        });
        tabs.AddChild(load);
        tabs.SetTabTitle(1, "载入画面");

        var opts = W.Col(8);
        var flags = W.Col(2);
        flags.AddChild(new CheckBox { Text = "近战地图" });
        flags.AddChild(new CheckBox { Text = "预览时隐藏小地图" });
        flags.AddChild(new CheckBox { Text = "遮蔽区域半可见" });
        flags.AddChild(new CheckBox { Text = "悬崖岸边显示水波", ButtonPressed = true });
        flags.AddChild(new CheckBox { Text = "缓坡显示水波", ButtonPressed = true });
        flags.AddChild(new CheckBox { Text = "使用物品分类系统" });
        opts.AddChild(flags);

        var fog = W.Col(6);
        var fogRow1 = W.Row(10);
        fogRow1.AddChild(Field("风格", W.Combo("线性", "指数", "指数平方"), 48));
        fogRow1.AddChild(Field("颜色", W.B("选择颜色…"), 48));
        fog.AddChild(fogRow1);
        var fogRow2 = W.Row(10);
        fogRow2.AddChild(Field("Z 起点", W.Spin(-10000, 10000, 1, 0), 56));
        fogRow2.AddChild(Field("Z 终点", W.Spin(-10000, 10000, 1, 5000), 56));
        fogRow2.AddChild(Field("密度", W.Spin(0, 1, 0.01, 0.5), 40));
        fog.AddChild(fogRow2);
        opts.AddChild(W.Group("地形雾", fog));

        var env = W.Col(6);
        env.AddChild(Field("全局天气", W.Combo("无", "小雨", "暴雨", "雪")));
        env.AddChild(Field("声音环境", W.Combo("默认", "森林", "洞穴", "城市")));
        env.AddChild(Field("光照环境", W.Combo("默认", "白天", "黄昏", "夜晚")));
        env.AddChild(Field("数据集", W.Combo("标准", "资料片")));
        env.AddChild(Field("水体着色", W.B("选择颜色…")));
        opts.AddChild(W.Group("环境", env));
        opts.AddChild(W.Spacer());
        tabs.AddChild(opts);
        tabs.SetTabTitle(2, "选项");

        var size = W.Col(8);
        size.AddChild(Banner("HiveWE 0.6 的这个页签只有一个「?」标签，尺寸与相机边界没有实现。这里是按官方 WE 的字段预排的界面。"));
        var grid = W.Grid(2, 8);
        grid.AddChild(W.L("左边界", 12, UiTheme.TextDim));
        grid.AddChild(W.Spin(0, 480, 32, 32));
        grid.AddChild(W.L("下边界", 12, UiTheme.TextDim));
        grid.AddChild(W.Spin(0, 480, 32, 32));
        grid.AddChild(W.L("右边界", 12, UiTheme.TextDim));
        grid.AddChild(W.Spin(0, 480, 32, 32));
        grid.AddChild(W.L("上边界", 12, UiTheme.TextDim));
        grid.AddChild(W.Spin(0, 480, 32, 32));
        grid.AddChild(W.L("可玩区域 左", 12, UiTheme.TextDim));
        grid.AddChild(W.Spin(0, 480, 16, 0));
        grid.AddChild(W.L("可玩区域 下", 12, UiTheme.TextDim));
        grid.AddChild(W.Spin(0, 480, 16, 0));
        grid.AddChild(W.L("可玩区域 右", 12, UiTheme.TextDim));
        grid.AddChild(W.Spin(0, 480, 16, 0));
        grid.AddChild(W.L("可玩区域 上", 12, UiTheme.TextDim));
        grid.AddChild(W.Spin(0, 480, 16, 0));
        size.AddChild(grid);
        size.AddChild(W.Spacer());
        tabs.AddChild(size);
        tabs.SetTabTitle(3, "尺寸与边界");

        view.Root = tabs;
        return view;
    }

    public static Control TriggerEditor()
    {
        var split = W.Row(0);
        split.CustomMinimumSize = new Vector2(960, 520);

        var left = W.Col(6);
        left.CustomMinimumSize = new Vector2(250, 0);
        left.AddChild(W.Edit("搜索触发器…"));
        var tree = W.MakeTree(1, "触发器");
        var rootItem = tree.CreateItem();
        var mapCat = tree.CreateItem(rootItem);
        mapCat.SetText(0, "地图");
        foreach (var t in Defs.TriggerTree)
        {
            var it = tree.CreateItem(mapCat);
            it.SetText(0, t);
        }
        var varCat = tree.CreateItem(rootItem);
        varCat.SetText(0, "变量（211）");
        tree.ItemSelected += () => Stub.Fire("trigger.select");
        left.AddChild(tree);
        split.AddChild(left);

        var right = W.Col(4);
        right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var toolbar = W.Row(4);
        var newGui = W.B("新建触发器", "new32x32");
        var newScript = W.B("新建脚本", "triggereditor32x32");
        var comment = W.B("注释", "description32x32");
        var generate = W.B("生成脚本", "save32x32");
        newGui.Pressed += () => Stub.Fire("trigger.new");
        newScript.Pressed += () => Stub.Fire("trigger.new_script");
        comment.Pressed += () => Stub.Fire("trigger.new_comment");
        generate.Pressed += () => Stub.Fire("trigger.generate");
        toolbar.AddChild(newGui);
        toolbar.AddChild(newScript);
        toolbar.AddChild(comment);
        toolbar.AddChild(W.Spacer());
        toolbar.AddChild(generate);
        right.AddChild(toolbar);

        var tabs = new TabContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var eca = W.Row(0);
        var ecaTree = W.MakeTree(2, "事件 / 条件 / 动作", "参数");
        ecaTree.CustomMinimumSize = new Vector2(420, 0);
        var ecaRoot = ecaTree.CreateItem();
        var ev = ecaTree.CreateItem(ecaRoot);
        ev.SetText(0, "事件");
        var evItem = ecaTree.CreateItem(ev);
        evItem.SetText(0, "地图初始化");
        var cd = ecaTree.CreateItem(ecaRoot);
        cd.SetText(0, "条件");
        var ac = ecaTree.CreateItem(ecaRoot);
        ac.SetText(0, "动作");
        var a1 = ecaTree.CreateItem(ac);
        a1.SetText(0, "设置天气效果");
        a1.SetText(1, "小雨");
        var a2 = ecaTree.CreateItem(ac);
        a2.SetText(0, "创建单位");
        a2.SetText(1, "步兵 @ 出生点");
        eca.AddChild(ecaTree);

        var param = W.Col(6);
        param.CustomMinimumSize = new Vector2(240, 0);
        param.AddChild(W.L("参数", 12, UiTheme.HeaderText));
        param.AddChild(Field("天气", W.Combo("无", "小雨", "暴雨"), 48));
        param.AddChild(Field("单位", W.Combo("步兵", "弓箭手", "骑士"), 48));
        param.AddChild(W.Spacer());
        eca.AddChild(param);
        tabs.AddChild(eca);
        tabs.SetTabTitle(0, "触发编辑器");

        var script = new TextEdit
        {
            Text = "function Trig_Melee_Initialization_Actions takes nothing returns nothing\n    call MeleeStartingVisibility(  )\n    call MeleeStartingHeroLimit(  )\n    call MeleeGrantItemsToHero(  )\nendfunction\n\n//===========================================================================\nfunction InitTrig_Melee_Initialization takes nothing returns nothing\n    set gg_trg_Melee_Initialization = CreateTrigger(  )\n    call TriggerAddAction( gg_trg_Melee_Initialization, function Trig_Melee_Initialization_Actions )\nendfunction\n",
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Editable = true,
        };
        script.AddThemeFontSizeOverride("font_size", 12);
        tabs.AddChild(script);
        tabs.SetTabTitle(1, "脚本");

        right.AddChild(tabs);
        split.AddChild(right);
        return split;
    }

    public static Control ImportManager()
    {
        var col = W.Col(8);
        col.CustomMinimumSize = new Vector2(760, 430);

        var row = W.Row(6);
        var newFolder = W.B("新建文件夹", "new32x32");
        var import = W.B("导入文件", "open32x32");
        var exportAll = W.B("导出全部", "saveas32x32");
        var openExplorer = W.B("在资源管理器中打开");
        newFolder.Pressed += () => Stub.Fire("import.new_folder");
        import.Pressed += () => Stub.Fire("import.import_files");
        exportAll.Pressed += () => Stub.Fire("import.export_all");
        openExplorer.Pressed += () => Stub.Fire("import.open_explorer");
        row.AddChild(newFolder);
        row.AddChild(import);
        row.AddChild(exportAll);
        row.AddChild(W.Spacer());
        row.AddChild(openExplorer);
        col.AddChild(row);

        col.AddChild(W.Dim("0.6 的导入管理器已经退化成文件浏览器，导入/导出按钮的连接被注释掉了。"));

        var tree = W.MakeTree(3, "名称", "大小", "来源");
        var root = tree.CreateItem();
        foreach (var (name, size) in new[]
                 {
                     ("war3map.j", "42.1 KB"), ("war3map.w3e", "32.7 KB"), ("war3map.doo", "18.4 KB"),
                     ("war3mapUnits.doo", "2.1 KB"), ("war3map.w3i", "1.2 KB"), ("war3map.wpm", "48.0 KB"),
                     ("war3map.wtg", "26.8 KB"), ("war3map.wct", "1.0 KB"), ("war3map.wts", "3.4 KB"),
                     ("_Locales/enUS.w3mod/war3map.wts", "3.4 KB"), ("_Locales/zhCN.w3mod/war3map.wts", "3.4 KB"),
                 })
        {
            var it = tree.CreateItem(root);
            it.SetText(0, name);
            it.SetText(1, size);
            it.SetText(2, "地图");
        }
        tree.ItemSelected += () => Stub.Fire("import.select");
        col.AddChild(tree);
        return col;
    }

    public static int SettingsTab;

    public static Control Settings(ViewportPanel view)
    {
        var tabs = new TabContainer { CustomMinimumSize = new Vector2(500, 330), SizeFlagsVertical = Control.SizeFlags.ExpandFill };

        var general = W.Col(10);
        general.AddChild(Field("主题", W.Combo("暗色", "亮色")));
        general.AddChild(new CheckBox { Text = "生成脚本时写入注释" });
        general.AddChild(W.Spacer());
        tabs.AddChild(general);
        tabs.SetTabTitle(0, "常规");

        var camera = W.Col(8);
        camera.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var fov = W.Spin(20, 120, 0.1, view.Fov);
        fov.ValueChanged += value => view.SetFov((float)value);
        camera.AddChild(Field("视野 FOV", fov, 116));
        camera.AddChild(W.Dim("垂直视角（度）。默认 50.705° 是 HiveWE 的实际视角（它把度数当弧度传了）；官方 WE 更接近 70°。"));

        var distance = W.Spin(0.5, 1000, 0.1, view.Distance / ViewportPanel.CellSize);
        distance.ValueChanged += value => view.SetDistance((float)value * ViewportPanel.CellSize);
        camera.AddChild(Field("相机距离（格）", distance, 116));

        var yaw = W.Spin(-180, 180, 1, view.Yaw);
        var pitch = W.Spin(-88, -6, 0.1, view.Pitch);
        yaw.ValueChanged += value => view.SetOrbit((float)value, (float)pitch.Value);
        pitch.ValueChanged += value => view.SetOrbit((float)yaw.Value, (float)value);
        camera.AddChild(Field("水平角", yaw, 116));
        camera.AddChild(Field("俯角", pitch, 116));
        camera.AddChild(W.Dim("初始朝向：水平角 180°、俯角 -55.98°（= HiveWE 的 vertical_angle -0.977）。"));

        var targetHeight = W.Spin(-200, 30000, 10, view.TargetHeight);
        targetHeight.ValueChanged += value => view.SetTargetHeight((float)value);
        camera.AddChild(Field("视觉中心高度", targetHeight, 116));

        var maxHeight = W.Spin(500, 30000, 100, view.MaxCameraHeight);
        maxHeight.ValueChanged += value => view.MaxCameraHeight = (float)value;
        camera.AddChild(Field("相机最高高度", maxHeight, 116));
        camera.AddChild(W.Dim("相机能抬到的最高世界单位（1 格 = 128）。滚轮、旋转、Shift 升降都受它约束。"));

        foreach (Control spin in new Control[] { fov, distance, yaw, pitch, targetHeight, maxHeight })
        {
            spin.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            spin.CustomMinimumSize = new Vector2(120, 26);
        }

        Label live = W.L("", 11, UiTheme.TextDim);
        System.Action refresh = () =>
        {
            if (!GodotObject.IsInstanceValid(live))
            {
                return;
            }
            Vector2 tile = view.TargetTile;
            live.Text = $"当前：相机 ({view.CameraPosition.X:0}, {view.CameraPosition.Y:0}, {view.CameraPosition.Z:0})"
                        + $"  中心格 ({tile.X:0.0}, {tile.Y:0.0})  FOV {view.Fov:0}";
        };
        view.CameraChanged += refresh;
        live.TreeExiting += () => view.CameraChanged -= refresh;
        refresh();
        camera.AddChild(live);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        scroll.AddChild(camera);
        tabs.AddChild(scroll);
        tabs.SetTabTitle(1, "相机");

        var test = W.Col(10);
        test.AddChild(Field("测试参数", W.Edit("-window")));
        test.AddChild(W.Dim("启动客户端时追加到命令行。"));
        test.AddChild(W.Spacer());
        tabs.AddChild(test);
        tabs.SetTabTitle(2, "测试");

        tabs.CurrentTab = SettingsTab;
        return tabs;
    }

    private static Button TileCell(Color color, string label, string tip, int size = 64)
    {
        var b = W.TileSwatch(color, label, tip, size, shortLabel: true);
        b.CustomMinimumSize = new Vector2(size, size);
        return b;
    }

    private static Button TileOp(string text, string icon = null)
    {
        var b = W.B(text, icon);
        b.CustomMinimumSize = new Vector2(0, 24);
        return b;
    }

    public static Control TileSetter()
    {
        var col = W.Col(4);
        col.CustomMinimumSize = new Vector2(555, 150);

        col.AddChild(W.L("Selected Tiles", 12, UiTheme.HeaderText));
        var row = W.Row(6);
        var grid = W.Grid(8, 3);
        foreach (var (name, color) in Defs.GroundTextures)
        {
            var b = TileCell(color, name, $"已选瓦片：{name}（打桩）", 56);
            b.Pressed += () => Stub.Fire($"tileset.select={name}");
            grid.AddChild(b);
        }
        row.AddChild(grid);
        var ops = W.Col(3);
        ops.AddChild(TileOp("左移"));
        ops.AddChild(TileOp("右移"));
        ops.AddChild(TileOp("移除"));
        row.AddChild(ops);
        row.AddChild(W.Spacer());
        col.AddChild(row);

        col.AddChild(W.L("Additional Tiles", 12, UiTheme.HeaderText));
        var addRow = W.Row(6);
        var tilesetCombo = W.Combo("洛丹伦的夏天", "灰谷", "菲拉斯");
        tilesetCombo.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        addRow.AddChild(tilesetCombo);
        var addGrid = W.Grid(4, 3);
        foreach (var (name, color) in Defs.GroundTextures)
        {
            addGrid.AddChild(TileCell(color, name, $"附加瓦片：{name}（打桩）", 44));
        }
        addRow.AddChild(addGrid);
        var add = TileOp("添加");
        add.CustomMinimumSize = new Vector2(56, 26);
        add.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        add.Pressed += () => Stub.Fire("tileset.add");
        addRow.AddChild(add);
        addRow.AddChild(W.Spacer());
        col.AddChild(addRow);

        return col;
    }

    public static Control TilePather()
    {
        var col = W.Col(4);
        col.CustomMinimumSize = new Vector2(550, 120);

        var row = W.Row(10);
        var left = W.Col(4);
        left.AddChild(W.L("Pathing Tile", 12, UiTheme.HeaderText));
        left.AddChild(TileCell(Color.FromHtml("475c2c"), "草地", "当前瓦片（打桩）"));
        row.AddChild(left);

        var right = W.Col(4);
        var flags = W.Row(4);
        flags.AddChild(PathingIcon("walkable", "不可行走", false));
        flags.AddChild(PathingIcon("flyable", "不可飞行", true));
        flags.AddChild(PathingIcon("buildable", "不可建造", false));
        flags.AddChild(W.Spacer());
        right.AddChild(flags);

        var ops = W.Row(10);
        ops.AddChild(W.L("操作类型", 11, UiTheme.TextDim));
        var replace = new CheckBox { Text = "替换", ButtonPressed = true };
        var add = new CheckBox { Text = "添加" };
        var remove = new CheckBox { Text = "移除" };
        replace.Pressed += () => { replace.ButtonPressed = true; add.ButtonPressed = false; remove.ButtonPressed = false; };
        add.Pressed += () => { add.ButtonPressed = true; replace.ButtonPressed = false; remove.ButtonPressed = false; };
        remove.Pressed += () => { remove.ButtonPressed = true; add.ButtonPressed = false; replace.ButtonPressed = false; };
        ops.AddChild(replace);
        ops.AddChild(add);
        ops.AddChild(remove);
        right.AddChild(ops);
        right.AddChild(new CheckBox { Text = "应用到已放置的瓦片" });
        row.AddChild(right);
        row.AddChild(W.Spacer());
        col.AddChild(row);

        return col;
    }

    private static Button PathingIcon(string icon, string tip, bool on)
    {
        var b = new Button
        {
            Icon = W.PathingIcon(icon),
            TooltipText = tip,
            ToggleMode = true,
            ButtonPressed = on,
            ExpandIcon = false,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(48, 48),
        };
        b.AddThemeConstantOverride("icon_max_width", 40);
        b.Pressed += () => Stub.Fire($"tilepathing.flag={tip}");
        return b;
    }

    public static Control TilePicker()
    {
        var col = W.Col(4);
        col.CustomMinimumSize = new Vector2(555, 100);

        var top = W.Row(8);
        top.AddChild(W.L("Replace Tile:", 12, UiTheme.HeaderText));
        top.AddChild(TileCell(Color.FromHtml("475c2c"), "草地", "源瓦片"));
        top.AddChild(W.L("→", 14, UiTheme.TextDim));
        top.AddChild(TileCell(Color.FromHtml("736244"), "泥土", "目标瓦片"));
        top.AddChild(W.Spacer());
        col.AddChild(top);

        var bottom = W.Row(8);
        bottom.AddChild(W.L("By Tile:", 12, UiTheme.HeaderText));
        var grid = W.Grid(8, 4);
        foreach (var (name, color) in Defs.GroundTextures)
        {
            var b = TileCell(color, name, $"按瓦片：{name}（打桩）", 48);
            b.Pressed += () => Stub.Fire($"tilepicker.replace={name}");
            grid.AddChild(b);
        }
        bottom.AddChild(grid);
        bottom.AddChild(W.Spacer());
        col.AddChild(bottom);

        return col;
    }

    public static Control ObjectEditor()
    {
        var col = W.Col(8);
        col.CustomMinimumSize = new Vector2(900, 500);
        col.AddChild(Banner("对象编辑器尚未实现。HiveWE 0.6 没有这块界面；后续要按 SLK 元数据表（UnitMetaData 等）自动生成字段表单。"));
        var split = W.Row(0);
        split.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

        var tree = W.MakeTree(1, "对象");
        tree.CustomMinimumSize = new Vector2(260, 0);
        var r = tree.CreateItem();
        foreach (var cat in new[] { "单位", "物品", "装饰物", "可破坏物", "技能", "升级", "Buff" })
        {
            var it = tree.CreateItem(r);
            it.SetText(0, cat);
        }
        split.AddChild(tree);

        var fields = W.MakeTree(2, "字段", "值");
        fields.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        fields.CustomMinimumSize = new Vector2(420, 0);
        var fr = fields.CreateItem();
        foreach (var (k, v) in new[]
                 {
                     ("Name", "步兵"), ("HP", "420"), ("armor", "2"), ("dmgplus1", "11"),
                     ("moveSpeed", "270"), ("modelFile", "Units\\Human\\Footman\\Footman.mdx"),
                 })
        {
            var it = fields.CreateItem(fr);
            it.SetText(0, k);
            it.SetText(1, v);
        }
        split.AddChild(fields);
        col.AddChild(split);
        return col;
    }

    public static Control Players()
    {
        var col = W.Col(8);
        col.CustomMinimumSize = new Vector2(720, 470);
        col.AddChild(Banner("玩家与队伍（Forces）在 0.6 里只有数据读写，没有编辑界面。这里按官方 WE 的「玩家属性」预排。"));
        var tree = W.MakeTree(4, "槽位", "控制者", "种族", "固定出生点");
        tree.CustomMinimumSize = new Vector2(0, 380);
        var r = tree.CreateItem();
        for (int i = 0; i < 12; i++)
        {
            var it = tree.CreateItem(r);
            it.SetText(0, $"玩家 {i + 1}");
            it.SetText(1, i == 0 ? "用户" : "计算机");
            it.SetText(2, "人类");
            it.SetText(3, i == 0 ? "是" : "否");
        }
        col.AddChild(tree);
        return col;
    }

    public static Control ModelPreview()
    {
        var col = W.Col(8);
        col.CustomMinimumSize = new Vector2(820, 560);
        col.AddChild(Banner("模型预览尚未实现。后续要解析 MDX 的 GEOS / MTLS / BONE / SEQS，直接在 Godot 里搭骨骼与材质。"));
        var preview = new PanelContainer { CustomMinimumSize = new Vector2(0, 360), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var sb = UiTheme.Box(Color.FromHtml("1b2026"), Color.FromHtml("3a3a3a"), 4);
        preview.AddThemeStyleboxOverride("panel", sb);
        var l = W.L("（模型预览占位）", 13, UiTheme.TextDim);
        l.HorizontalAlignment = HorizontalAlignment.Center;
        l.VerticalAlignment = VerticalAlignment.Center;
        preview.AddChild(l);
        col.AddChild(preview);
        var seq = W.Row(6);
        seq.AddChild(W.L("序列", 12, UiTheme.TextDim));
        seq.AddChild(W.Combo("Stand", "Walk", "Attack", "Death"));
        seq.AddChild(W.Slider(0, 100, 1, 0));
        col.AddChild(seq);
        return col;
    }
}
