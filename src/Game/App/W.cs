using Godot;

namespace WorldEdit;

public static class W
{
    public static Texture2D Icon(string name)
    {
        var path = $"res://assets/icons/ribbon/{name}.png";
        if (!ResourceLoader.Exists(path))
        {
            return null;
        }
        return GD.Load<Texture2D>(path);
    }

    public static Texture2D TerrainIcon(string name)
    {
        var path = $"res://assets/icons/terrain/{name}.png";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    public static Texture2D BrushIcon(string name)
    {
        var path = $"res://assets/icons/brush/{name}.png";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    public static Texture2D PathingIcon(string name)
    {
        var path = $"res://assets/icons/pathing/{name}.png";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    private static readonly System.Collections.Generic.HashSet<string> PaleIcons = new()
    {
        "new32x32", "test32x32", "exit32x32",
    };

    private static void TintIfPale(Button b, string icon)
    {
        if (icon == null || !PaleIcons.Contains(icon))
        {
            return;
        }
        var tint = Color.FromHtml("556070");
        foreach (var slot in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_focus_color" })
        {
            b.AddThemeColorOverride(slot, tint);
        }
    }

    public static Label L(string text, int size = 13, Color? color = null)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color ?? UiTheme.Text);
        return l;
    }

    public static Label Dim(string text, int size = 11) => L(text, size, UiTheme.TextDim);

    public static Label SectionTitle(string text)
    {
        var l = L(text, 12, UiTheme.Text);
        return l;
    }

    public static Button B(string text, string icon = null, string tip = null, bool toggle = false)
    {
        var b = new Button
        {
            Text = text,
            ToggleMode = toggle,
            TooltipText = tip ?? text,
            FocusMode = Control.FocusModeEnum.None,
        };
        if (icon != null)
        {
            b.Icon = Icon(icon);
            TintIfPale(b, icon);
        }
        b.AddThemeConstantOverride("icon_max_width", 18);
        b.CustomMinimumSize = new Vector2(0, 26);
        return b;
    }

    public static Button Big(string text, string icon, string tip = null, bool toggle = false, int width = 0)
    {
        var b = new Button
        {
            Text = text,
            ToggleMode = toggle,
            TooltipText = tip ?? text,
            FocusMode = Control.FocusModeEnum.None,
            ExpandIcon = false,
            IconAlignment = HorizontalAlignment.Center,
            VerticalIconAlignment = VerticalAlignment.Top,
            ClipText = false,
            CustomMinimumSize = new Vector2(Mathf.Max(width, 54), 58),
        };

        b.AddThemeStyleboxOverride("normal", UiTheme.Padded(UiTheme.Surface, UiTheme.BorderLight));
        b.AddThemeStyleboxOverride("hover", UiTheme.Padded(Color.FromHtml("edf4fc"), Color.FromHtml("a8d2fd")));
        b.AddThemeStyleboxOverride("pressed", UiTheme.Padded(UiTheme.AccentLight, Color.FromHtml("62a2e4")));
        b.AddThemeStyleboxOverride("disabled", UiTheme.Padded(Color.FromHtml("f2f2f2"), Color.FromHtml("dcdcdc")));
        var tex = Icon(icon);
        if (tex != null)
        {
            b.Icon = tex;
            TintIfPale(b, icon);
        }
        b.AddThemeConstantOverride("icon_max_width", 32);
        b.AddThemeFontSizeOverride("font_size", 10);
        return b;
    }

    public static VBoxContainer Section(string title, params Control[] items)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 1);
        box.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        foreach (var it in items)
        {
            row.AddChild(it);
        }

        box.AddChild(row);
        var caption = L(title, 9, UiTheme.TextDim);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        caption.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        box.AddChild(caption);
        return box;
    }

    public static Control VSep(int height = 62)
    {
        var c = new Control { CustomMinimumSize = new Vector2(9, height) };
        var line = new ColorRect
        {
            Color = UiTheme.Border,
            CustomMinimumSize = new Vector2(1, height - 8),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        line.SetAnchorsPreset(Control.LayoutPreset.CenterLeft);
        line.Position = new Vector2(4, 4);
        c.AddChild(line);
        return c;
    }

    public static HBoxContainer Row(int sep = 6)
    {
        var r = new HBoxContainer();
        r.AddThemeConstantOverride("separation", sep);
        return r;
    }

    public static VBoxContainer Col(int sep = 6)
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", sep);
        return v;
    }

    public static MarginContainer Pad(Control child, int l = 8, int t = 8, int r = 8, int b = 8)
    {
        var m = new MarginContainer();
        m.AddThemeConstantOverride("margin_left", l);
        m.AddThemeConstantOverride("margin_top", t);
        m.AddThemeConstantOverride("margin_right", r);
        m.AddThemeConstantOverride("margin_bottom", b);
        m.AddChild(child);
        return m;
    }

    public static PanelContainer Group(string title, Control body)
    {
        var panel = new PanelContainer();
        var sb = UiTheme.Box(UiTheme.PanelBg, UiTheme.Border, 4);
        sb.ContentMarginLeft = 8;
        sb.ContentMarginRight = 8;
        sb.ContentMarginTop = 6;
        sb.ContentMarginBottom = 8;
        panel.AddThemeStyleboxOverride("panel", sb);

        var col = Col(6);
        var head = L(title, 12, UiTheme.HeaderText);
        col.AddChild(head);
        var line = new ColorRect { Color = UiTheme.Divider, CustomMinimumSize = new Vector2(0, 1) };
        col.AddChild(line);
        col.AddChild(body);
        panel.AddChild(col);
        return panel;
    }

    public static Button Swatch(Color color, string label, string tip, bool toggle = true, int size = 46)
    {
        var b = new Button
        {
            TooltipText = tip,
            ToggleMode = toggle,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(size, size),
        };
        b.AddThemeStyleboxOverride("normal", UiTheme.Box(color, UiTheme.Border, 2));
        b.AddThemeStyleboxOverride("hover", UiTheme.Box(color.Lightened(0.12f), UiTheme.Accent, 2, 2));
        b.AddThemeStyleboxOverride("pressed", UiTheme.Box(color.Lightened(0.06f), UiTheme.Accent, 2, 2));

        var text = new Label
        {
            Text = label,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        text.AddThemeFontSizeOverride("font_size", 9);
        text.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.85f));
        text.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        text.AddThemeConstantOverride("outline_size", 3);
        text.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        b.AddChild(text);
        return b;
    }

    public static Button TileSwatch(Color color, string tip, int size = 48, bool toggle = true)
    {
        var b = new Button
        {
            TooltipText = tip,
            ToggleMode = toggle,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(size, size),
        };
        b.AddThemeStyleboxOverride("normal", UiTheme.Box(color, UiTheme.Border, 2));
        b.AddThemeStyleboxOverride("hover", UiTheme.Box(color.Lightened(0.10f), UiTheme.Accent, 2, 2));
        b.AddThemeStyleboxOverride("pressed", UiTheme.Box(color.Lightened(0.04f), UiTheme.Accent, 2, 2));
        return b;
    }

    public static Button TileSwatch(Color color, string label, string tip, int size, bool shortLabel = false, bool toggle = true)
    {
        var b = TileSwatch(color, tip, size, toggle);
        var text = new Label
        {
            Text = label,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        text.AddThemeFontSizeOverride("font_size", shortLabel ? 9 : 10);
        text.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.92f));
        text.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.85f));
        text.AddThemeConstantOverride("outline_size", 3);
        text.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        b.AddChild(text);
        return b;
    }

    public static GridContainer Grid(int columns, int sep = 4)
    {
        var g = new GridContainer { Columns = columns };
        g.AddThemeConstantOverride("h_separation", sep);
        g.AddThemeConstantOverride("v_separation", sep);
        return g;
    }

    public static HSeparator HLine()
    {
        var s = new HSeparator { CustomMinimumSize = new Vector2(0, 8) };
        return s;
    }

    public static Control Spacer(int h = 0, int w = 0)
    {
        var c = new Control { CustomMinimumSize = new Vector2(w, h), MouseFilter = Control.MouseFilterEnum.Ignore };
        if (w > 0) c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return c;
    }

    public static OptionButton Combo(params string[] items)
    {
        var c = new OptionButton { CustomMinimumSize = new Vector2(0, 26) };
        foreach (var i in items)
        {
            c.AddItem(i);
        }
        c.AddThemeFontSizeOverride("font_size", 12);
        return c;
    }

    public static LineEdit Edit(string placeholder = "", string text = "")
    {
        return new LineEdit { PlaceholderText = placeholder, Text = text, CustomMinimumSize = new Vector2(0, 26) };
    }

    public static SpinBox Spin(double min, double max, double step, double value)
    {
        return new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(90, 26) };
    }

    public static HSlider Slider(double min, double max, double step, double value)
    {
        return new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            CustomMinimumSize = new Vector2(0, 18),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
    }

    public static CheckBox Check(string text, bool on = false)
    {
        return new CheckBox { Text = text, ButtonPressed = on, FocusMode = Control.FocusModeEnum.None };
    }

    public static Tree MakeTree(int columns, params string[] titles)
    {
        var tree = new Tree
        {
            Columns = columns,
            HideRoot = true,
            ColumnTitlesVisible = true,
            CustomMinimumSize = new Vector2(0, 120),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        for (int i = 0; i < titles.Length && i < columns; i++)
        {
            tree.SetColumnTitle(i, titles[i]);
        }
        return tree;
    }
}
