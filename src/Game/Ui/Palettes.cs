using Godot;

namespace WorldEdit;

public static class Palettes
{

    private static Button IconButton(string terrainIcon, string tip, bool toggle = true, int size = 48)
    {
        var b = new Button
        {
            Icon = W.TerrainIcon(terrainIcon),
            TooltipText = tip,
            ToggleMode = toggle,
            FocusMode = Control.FocusModeEnum.None,
            ExpandIcon = false,
            CustomMinimumSize = new Vector2(size, size),
        };
        b.AddThemeConstantOverride("icon_max_width", size - 6);
        return b;
    }

    private static void Exclusive(params Button[] buttons)
    {
        foreach (var b in buttons)
        {
            var self = b;
            self.Pressed += () =>
            {
                self.ButtonPressed = true;
                foreach (var other in buttons)
                {
                    if (other != self)
                    {
                        other.ButtonPressed = false;
                    }
                }
            };
        }
    }

    private static Control Header(string subtitle)
    {
        var l = W.Dim(subtitle);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return l;
    }

    public static Control BuildTerrain()
    {
        var root = W.Col(3);

        var textureCheck = W.Check("纹理", true);
        root.AddChild(textureCheck);

        var texRow = W.Row(4);
        var texGrid = W.Grid(4, 3);
        foreach (var (name, color) in Defs.GroundTextures)
        {
            texGrid.AddChild(W.TileSwatch(color, $"地面纹理：{name}（打桩）", 48));
        }
        texRow.AddChild(texGrid);

        var texSide = W.Col(3);
        var blight = new Button
        {
            Icon = W.Icon("water32x32"),
            TooltipText = "疫病地表",
            ToggleMode = true,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(48, 48),
        };
        blight.AddThemeConstantOverride("icon_max_width", 40);
        var addBoundary = IconButton("boundary_place", "放置地图边界", false);
        var removeBoundary = IconButton("boundary_remove", "移除地图边界", false);
        texSide.AddChild(blight);
        texSide.AddChild(addBoundary);
        texSide.AddChild(removeBoundary);
        texRow.AddChild(texSide);
        texRow.AddChild(W.Spacer());
        root.AddChild(texRow);

        var cliffCheck = W.Check("悬崖", false);
        root.AddChild(cliffCheck);

        var cliffOps = W.Row(3);
        var lower2 = IconButton("lower2", "悬崖下降两级");
        var lower1 = IconButton("lower1", "悬崖下降一级");
        var level = IconButton("level", "压平");
        var raise1 = IconButton("raise1", "悬崖上升一级");
        var raise2 = IconButton("raise2", "悬崖上升两级");
        Exclusive(lower2, lower1, level, raise1, raise2);
        level.ButtonPressed = true;
        cliffOps.AddChild(lower2);
        cliffOps.AddChild(lower1);
        cliffOps.AddChild(level);
        cliffOps.AddChild(raise1);
        cliffOps.AddChild(raise2);
        root.AddChild(cliffOps);

        var waterOps = W.Row(3);
        var deepWater = IconButton("deep_water", "深水悬崖");
        var shallowWater = IconButton("shallow_water", "浅水悬崖");
        var ramp = IconButton("ramp", "生成坡道");
        Exclusive(deepWater, shallowWater, ramp);
        waterOps.AddChild(deepWater);
        waterOps.AddChild(shallowWater);
        waterOps.AddChild(ramp);
        waterOps.AddChild(W.Spacer());
        root.AddChild(waterOps);

        var cliffTypes = W.Grid(4, 3);
        foreach (var (name, color) in Defs.CliffTypes)
        {

            var cell = W.Col(1);
            cell.AddChild(W.TileSwatch(color, $"悬崖类型：{name}（打桩）", 48));
            cell.AddChild(W.L(name.Replace("悬崖", ""), 9, UiTheme.TextDim));
            cliffTypes.AddChild(cell);
        }
        root.AddChild(cliffTypes);

        var deformCheck = W.Check("形变", true);
        root.AddChild(deformCheck);

        var deformOps = W.Row(3);
        var raise = IconButton("brush_raise", "抬高");
        var lower = IconButton("brush_lower", "降低");
        var ripple = IconButton("brush_ripple", "波纹");
        var plateau = IconButton("brush_plateau", "平台");
        var smooth = IconButton("brush_smooth", "平滑");
        Exclusive(raise, lower, ripple, plateau, smooth);
        raise.ButtonPressed = true;
        foreach (var b in new[] { raise, lower, ripple, plateau, smooth })
        {
            deformOps.AddChild(b);
        }
        root.AddChild(deformOps);

        root.AddChild(W.L("笔刷大小", 11, UiTheme.TextDim));
        var sizes = W.Row(3);
        var sizeButtons = new System.Collections.Generic.List<Button>();
        foreach (var s in new[] { 1, 3, 5, 7, 9, 11 })
        {
            var b = W.B(s.ToString(), null, $"笔刷大小 {s}", true);
            b.CustomMinimumSize = new Vector2(38, 24);
            sizeButtons.Add(b);
            sizes.AddChild(b);
        }
        Exclusive(sizeButtons.ToArray());
        sizes.AddChild(W.Spacer());
        root.AddChild(sizes);

        var sizeRow = W.Row(6);
        var spin = W.Spin(1, 1000, 1, 5);
        var slider = W.Slider(1, 1000, 1, 5);
        spin.ValueChanged += v => slider.Value = v;
        slider.ValueChanged += v => spin.Value = v;
        sizeRow.AddChild(spin);
        sizeRow.AddChild(slider);
        root.AddChild(sizeRow);

        root.AddChild(W.L("笔刷形状", 11, UiTheme.TextDim));
        var shapes = W.Row(4);
        var circle = BrushShape("circle", "圆形笔刷", true);
        var square = BrushShape("square", "方形笔刷", false);
        var diamond = BrushShape("diamond", "菱形笔刷", false);
        Exclusive(circle, square, diamond);
        shapes.AddChild(circle);
        shapes.AddChild(square);
        shapes.AddChild(diamond);
        shapes.AddChild(W.Spacer());
        root.AddChild(shapes);

        textureCheck.Toggled += on => Stub.Fire($"terrain.apply_texture={on}");
        cliffCheck.Toggled += on => Stub.Fire($"terrain.apply_cliff={on}");
        deformCheck.Toggled += on => Stub.Fire($"terrain.apply_height={on}");

        foreach (var b in new[] { lower2, lower1, level, raise1, raise2, deepWater, shallowWater, ramp })
        {
            var self = b;
            self.Pressed += () =>
            {
                cliffCheck.ButtonPressed = true;
                deformCheck.ButtonPressed = false;
                Stub.Fire($"terrain.cliff.{self.TooltipText}");
            };
        }
        foreach (var b in new[] { raise, lower, ripple, plateau, smooth })
        {
            var self = b;
            self.Pressed += () =>
            {
                deformCheck.ButtonPressed = true;
                cliffCheck.ButtonPressed = false;
                Stub.Fire($"terrain.deform.{self.TooltipText}");
            };
        }

        return root;
    }

    private static Button BrushShape(string icon, string tip, bool on)
    {
        var b = new Button
        {
            Icon = W.BrushIcon(icon),
            TooltipText = tip,
            ToggleMode = true,
            ButtonPressed = on,
            ExpandIcon = false,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(48, 40),
        };
        b.AddThemeConstantOverride("icon_max_width", 32);
        b.Pressed += () => Stub.Fire($"terrain.shape={tip}");
        return b;
    }

    public static Control BuildTerrainRibbon()
    {
        var row = W.Row(0);
        var general = W.Section("General",
            ToggleRibbon("强制水位限制", "water32x32", true, "Enforce Water Height Limit"),
            ToggleRibbon("更新装饰物 Z", "changeheight32x32", true, "Update Doodad Z"));
        var cliff = W.Section("Cliff",
            ToggleRibbon("相对高度", "changeheight32x32", false, "Relative Height（0.6 里是禁用状态）", disabled: true));
        var pathing = W.Section("Pathing",
            ToggleRibbon("悬崖寻路", "pathing32x32", true, "Cliff Pathing"),
            ToggleRibbon("瓦片寻路", "tileset32x32", true, "Tile Pathing"),
            ToggleRibbon("水面寻路", "water32x32", true, "Water Pathing"));
        row.AddChild(general);
        row.AddChild(W.VSep());
        row.AddChild(cliff);
        row.AddChild(W.VSep());
        row.AddChild(pathing);
        row.AddChild(W.Spacer());
        return W.Pad(row, 6, 4, 6, 2);
    }

    private static Button ToggleRibbon(string text, string icon, bool on, string tip, bool disabled = false)
    {
        var b = W.Big(text, icon, tip, true);
        b.ButtonPressed = on;
        b.Disabled = disabled;
        b.Toggled += v => Stub.Fire($"flag.{tip}={v}");
        return b;
    }

    public static Control BuildDoodads()
    {
        var root = W.Col(4);
        root.AddChild(W.Combo("洛丹伦的夏天", "灰谷", "菲拉斯", "北裂境", "城市"));
        root.AddChild(W.Combo("树木", "灌木", "岩石", "建筑", "道具", "装饰", "特效", "可破坏物"));
        root.AddChild(W.Edit("搜索…"));

        var list = new ItemList
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            AllowReselect = true,
            SameColumnWidth = true,
        };
        list.AddThemeFontSizeOverride("font_size", 12);
        foreach (var (cat, items) in Defs.DoodadTree)
        {
            foreach (var name in items)
            {
                list.AddItem(name);
            }
        }
        list.ItemSelected += idx => Stub.Fire($"doodad.select[{idx}]");
        root.AddChild(list);

        var brush = W.Col(4);
        var sizes = W.Row(3);
        foreach (var s in new[] { 1, 2, 3, 5, 8, 11 })
        {
            var b = W.B(s.ToString(), null, $"笔刷 {s}", true);
            b.CustomMinimumSize = new Vector2(34, 24);
            sizes.AddChild(b);
        }
        brush.AddChild(sizes);
        brush.AddChild(W.Slider(1, 32, 1, 2));
        var group = W.Group("Brush", brush);
        group.Visible = false;
        root.AddChild(group);

        return root;
    }

    public static Control BuildDoodadRibbon()
    {
        var row = W.Row(0);
        var sel = W.Section("Selection", ToggleRibbon("选择模式", "select32x32", false, "Selection Mode"));
        var place = W.Section("Placement",
            ToggleRibbon("随机旋转", "reset32x32", true, "Random Rotation"),
            ToggleRibbon("随机缩放", "scale32x32", true, "Random Scale"));
        var vari = W.Section("Variations", ToggleRibbon("随机变体", "variation32x32", true, "Random Variation"));
        var flags = W.Section("Flags", FlagGroup());
        var pathing = W.Section("Pathing", ToggleRibbon("寻路贴图", "pathing32x32", true, "Show Pathing Texture"));
        row.AddChild(sel);
        row.AddChild(W.VSep());
        row.AddChild(place);
        row.AddChild(W.VSep());
        row.AddChild(vari);
        row.AddChild(W.VSep());
        row.AddChild(flags);
        row.AddChild(W.VSep());
        row.AddChild(pathing);
        row.AddChild(W.Spacer());
        return W.Pad(row, 6, 4, 6, 2);
    }

    private static StyleBoxFlat Compact(Color bg)
    {
        var sb = UiTheme.Box(bg, UiTheme.Border, 3);
        sb.ContentMarginTop = 0;
        sb.ContentMarginBottom = 0;
        sb.ContentMarginLeft = 4;
        sb.ContentMarginRight = 4;
        return sb;
    }

    private static VBoxContainer FlagGroup()
    {
        var list = new System.Collections.Generic.List<Button>();
        var box = W.Col(0);

        Button Make(string text, bool on)
        {
            var b = new Button
            {
                Text = text,
                ToggleMode = true,
                ButtonPressed = on,
                CustomMinimumSize = new Vector2(112, 15),
                FocusMode = Control.FocusModeEnum.None,
            };
            b.AddThemeFontSizeOverride("font_size", 9);
            b.AddThemeStyleboxOverride("normal", Compact(UiTheme.PanelAlt));
            b.AddThemeStyleboxOverride("hover", Compact(Color.FromHtml("edf4fc")));
            b.AddThemeStyleboxOverride("pressed", Compact(UiTheme.AccentDark));
            b.Pressed += () =>
            {
                foreach (var other in list)
                {
                    if (other != b)
                    {
                        other.ButtonPressed = false;
                    }
                }
                Stub.Fire($"doodad.state={text}");
            };
            list.Add(b);
            return b;
        }

        box.AddChild(Make("不可见非实体", false));
        box.AddChild(Make("可见非实体", false));
        box.AddChild(Make("可见实体", true));
        return box;
    }

    public static Control BuildPathing()
    {
        var root = W.Col(2);
        root.AddChild(W.L("笔刷操作类型", 11, UiTheme.TextDim));
        var ops = W.Col(0);
        var replace = CompactCheck("替换", true);
        var add = CompactCheck("添加", false);
        var remove = CompactCheck("移除", false);
        Exclusive(replace, add, remove);
        ops.AddChild(replace);
        ops.AddChild(add);
        ops.AddChild(remove);
        root.AddChild(ops);

        root.AddChild(W.L("笔刷类型", 11, UiTheme.TextDim));
        var kinds = W.Row(3);
        var walk = PathingKind("walkable", "不可行走", true);
        var fly = PathingKind("flyable", "不可飞行", false);
        var build = PathingKind("buildable", "不可建造", false);
        Exclusive(walk, fly, build);
        kinds.AddChild(walk);
        kinds.AddChild(fly);
        kinds.AddChild(build);
        kinds.AddChild(W.Spacer());
        root.AddChild(kinds);

        root.AddChild(W.L("笔刷", 11, UiTheme.TextDim));
        var sizes = W.Row(2);
        var sizeButtons = new System.Collections.Generic.List<Button>();
        foreach (var s in new[] { 1, 3, 5, 7, 9, 11 })
        {
            var b = W.B(s.ToString(), null, $"笔刷 {s}", true);
            b.CustomMinimumSize = new Vector2(32, 24);
            sizeButtons.Add(b);
            sizes.AddChild(b);
        }
        Exclusive(sizeButtons.ToArray());
        root.AddChild(sizes);

        var sizeRow = W.Row(6);
        var spin = W.Spin(1, 1000, 1, 5);
        var slider = W.Slider(1, 1000, 1, 5);
        spin.ValueChanged += v => slider.Value = v;
        slider.ValueChanged += v => spin.Value = v;
        sizeRow.AddChild(spin);
        sizeRow.AddChild(slider);
        root.AddChild(sizeRow);

        var shapes = W.Row(3);
        var circle = BrushShape("circle", "圆形笔刷", true);
        var square = BrushShape("square", "方形笔刷", false);
        var diamond = BrushShape("diamond", "菱形笔刷", false);
        Exclusive(circle, square, diamond);
        shapes.AddChild(circle);
        shapes.AddChild(square);
        shapes.AddChild(diamond);
        shapes.AddChild(W.Spacer());
        root.AddChild(shapes);

        return root;
    }

    private static CheckBox CompactCheck(string text, bool on)
    {
        var b = new CheckBox { Text = text, ButtonPressed = on, CustomMinimumSize = new Vector2(0, 18) };
        b.AddThemeFontSizeOverride("font_size", 11);
        b.AddThemeStyleboxOverride("normal", Compact(new Color(0, 0, 0, 0)));
        b.AddThemeStyleboxOverride("hover", Compact(new Color(1, 1, 1, 0.06f)));
        b.AddThemeStyleboxOverride("pressed", Compact(new Color(1, 1, 1, 0.06f)));
        return b;
    }

    private static Button PathingKind(string icon, string tip, bool on)
    {
        var b = new Button
        {
            Icon = W.PathingIcon(icon),
            TooltipText = tip,
            ToggleMode = true,
            ButtonPressed = on,
            ExpandIcon = false,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(38, 36),
        };
        b.AddThemeConstantOverride("icon_max_width", 32);
        b.Pressed += () => Stub.Fire($"pathing.kind={tip}");
        return b;
    }

    public static Control BuildUnits()
    {
        var root = W.Col(6);
        root.AddChild(W.Dim("Unit Palette · 0.6 没有这块界面，占位"));
        root.AddChild(W.Combo("玩家 1（红色）", "玩家 2（蓝色）", "玩家 3（青色）", "中立被动"));
        root.AddChild(W.Combo("人类", "兽族", "不死族", "暗夜精灵", "中立"));
        root.AddChild(W.Edit("搜索单位…"));

        var list = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        foreach (var (name, id) in Defs.Units)
        {
            list.AddItem($"{name}   {id}");
        }
        list.ItemSelected += idx => Stub.Fire($"unit.select[{idx}]");
        root.AddChild(list);
        return root;
    }

    public static Control BuildRegions()
    {
        var root = W.Col(6);
        root.AddChild(W.Dim("Region · 0.6 只读写 w3r，没有界面"));
        var list = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        foreach (var (name, kind) in new[] { ("出生点_玩家1", "矩形"), ("刷怪区_北", "矩形"), ("Boss 房间", "圆形") })
        {
            list.AddItem($"{name}  ({kind})");
        }
        root.AddChild(list);

        var btns = W.Row(4);
        var add = W.B("新建", "new32x32");
        var del = W.B("删除", "exit32x32");
        add.Pressed += () => Stub.Fire("region.add");
        del.Pressed += () => Stub.Fire("region.remove");
        btns.AddChild(add);
        btns.AddChild(del);
        btns.AddChild(W.Spacer());
        root.AddChild(btns);

        var nameRow = W.Row(6);
        nameRow.AddChild(W.L("名称", 11, UiTheme.TextDim));
        nameRow.AddChild(W.Edit("region_1"));
        root.AddChild(nameRow);
        var colorRow = W.Row(6);
        colorRow.AddChild(W.L("颜色", 11, UiTheme.TextDim));
        colorRow.AddChild(W.Combo("蓝色", "红色", "绿色", "黄色"));
        root.AddChild(colorRow);
        return root;
    }
}
