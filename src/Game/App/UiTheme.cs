using Godot;

namespace WorldEdit;

public static class UiTheme
{
    public static readonly Color WindowBg = Color.FromHtml("ffffff");
    public static readonly Color PanelBg = Color.FromHtml("fbfcfd");
    public static readonly Color PanelAlt = Color.FromHtml("f5f6f7");
    public static readonly Color RibbonBg = Color.FromHtml("fafbfc");
    public static readonly Color Surface = Color.FromHtml("fafbfc");
    public static readonly Color Border = Color.FromHtml("dadbdc");
    public static readonly Color BorderLight = Color.FromHtml("c6c7c8");
    public static readonly Color Divider = Color.FromHtml("dcddde");
    public static readonly Color Text = Color.FromHtml("141414");
    public static readonly Color TextDim = Color.FromHtml("6e6e6e");
    public static readonly Color HeaderText = Color.FromHtml("3c3c3c");
    public static readonly Color Accent = Color.FromHtml("298ce1");
    public static readonly Color AccentDark = Color.FromHtml("1979ca");
    public static readonly Color AccentLight = Color.FromHtml("c9e0f7");
    public static readonly Color Warn = Color.FromHtml("b06e14");
    public static readonly Color BannerBg = Color.FromHtml("fff8e1");
    public static readonly Color BannerBorder = Color.FromHtml("e2c88c");
    public static readonly Color BannerText = Color.FromHtml("78551b");
    public static readonly Color ToastBg = Color.FromHtml("ffffffe6");

    private static SystemFont _font;

    public static Font Font
    {
        get
        {
            _font ??= new SystemFont
            {
                FontNames = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI", "sans-serif" },
                AllowSystemFallback = true,
            };
            return _font;
        }
    }

    public static StyleBoxFlat Box(Color bg, Color? border = null, int radius = 3, int borderWidth = 1)
    {
        var sb = new StyleBoxFlat { BgColor = bg };
        sb.SetBorderWidthAll(borderWidth);
        sb.BorderColor = border ?? Border;
        sb.SetCornerRadiusAll(radius);
        sb.ContentMarginLeft = 6;
        sb.ContentMarginRight = 6;
        sb.ContentMarginTop = 4;
        sb.ContentMarginBottom = 4;
        return sb;
    }

    public static StyleBoxFlat Padded(Color bg, Color border, int padH = 12, int padV = 4, int radius = 3)
    {
        var sb = Box(bg, border, radius);
        sb.ContentMarginLeft = padH;
        sb.ContentMarginRight = padH;
        sb.ContentMarginTop = padV;
        sb.ContentMarginBottom = padV;
        return sb;
    }

    public static Theme Build()
    {
        var t = new Theme
        {
            DefaultFont = Font,
            DefaultFontSize = 13,
        };

        t.SetColor("font_color", "Label", Text);

        foreach (var type in new[] { "Button", "OptionButton" })
        {
            t.SetStylebox("normal", type, Box(Surface, BorderLight, 3));
            t.SetStylebox("hover", type, Box(Color.FromHtml("edf4fc"), Color.FromHtml("a8d2fd"), 3));
            t.SetStylebox("pressed", type, Box(AccentLight, Color.FromHtml("62a2e4"), 3));
            t.SetStylebox("disabled", type, Box(Color.FromHtml("f2f2f2"), Color.FromHtml("dcdcdc"), 3));
            t.SetStylebox("focus", type, new StyleBoxEmpty());
            t.SetColor("font_color", type, Text);
            t.SetColor("font_hover_color", type, Text);
            t.SetColor("font_pressed_color", type, Text);
            t.SetColor("font_disabled_color", type, Color.FromHtml("aaaaaa"));
            t.SetFontSize("font_size", type, 12);
        }

        foreach (var st in new[] { "normal", "disabled" })
        {
            t.SetStylebox(st, "CheckBox", Box(new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), 0, 0));
        }
        foreach (var st in new[] { "hover", "pressed" })
        {
            t.SetStylebox(st, "CheckBox", Box(new Color(0.16f, 0.55f, 0.88f, 0.08f), new Color(0, 0, 0, 0), 3, 0));
        }
        t.SetStylebox("focus", "CheckBox", new StyleBoxEmpty());
        t.SetColor("font_color", "CheckBox", Text);
        t.SetColor("font_hover_color", "CheckBox", Text);
        t.SetColor("font_pressed_color", "CheckBox", Text);
        t.SetFontSize("font_size", "CheckBox", 12);

        t.SetStylebox("panel", "PanelContainer", Box(PanelBg, Border, 4));
        t.SetStylebox("panel", "Panel", Box(PanelBg, Border, 4));

        var tip = Box(Color.FromHtml("ffffff"), Color.FromHtml("b9babb"), 3);
        tip.ContentMarginLeft = 8;
        tip.ContentMarginRight = 8;
        tip.ContentMarginTop = 5;
        tip.ContentMarginBottom = 5;
        t.SetStylebox("panel", "TooltipPanel", tip);
        t.SetColor("font_color", "TooltipLabel", Text);
        t.SetFontSize("font_size", "TooltipLabel", 12);
        t.SetFont("font", "TooltipLabel", Font);

        foreach (var (name, bg) in new[]
                 {
                     ("tab_selected", PanelAlt),
                     ("tab_unselected", Colors.White),
                     ("tab_hovered", Color.FromHtml("fdfdfd")),
                 })
        {
            var tab = Box(bg, Border, 0, 0);
            tab.ContentMarginLeft = 12;
            tab.ContentMarginRight = 12;
            tab.ContentMarginTop = 3;
            tab.ContentMarginBottom = 3;
            t.SetStylebox(name, "TabContainer", tab);
        }
        var tabPanel = Box(RibbonBg, Border, 0);
        tabPanel.ContentMarginLeft = 0;
        tabPanel.ContentMarginRight = 0;
        tabPanel.ContentMarginTop = 0;
        tabPanel.ContentMarginBottom = 0;
        t.SetStylebox("panel", "TabContainer", tabPanel);
        t.SetStylebox("tabbar_background", "TabContainer", Box(Colors.White, Border, 0, 0));
        t.SetColor("font_selected_color", "TabContainer", Text);
        t.SetColor("font_unselected_color", "TabContainer", TextDim);
        t.SetColor("font_hovered_color", "TabContainer", Text);
        t.SetFontSize("font_size", "TabContainer", 11);
        t.SetConstant("side_margin", "TabContainer", 0);

        t.SetStylebox("separator", "HSeparator", Box(Divider, null, 0, 0));
        t.SetStylebox("separator", "VSeparator", Box(Divider, null, 0, 0));

        foreach (var type in new[] { "LineEdit", "TextEdit" })
        {
            t.SetStylebox("normal", type, Box(Surface, Color.FromHtml("dbdcdd"), 3));
            t.SetStylebox("focus", type, Box(Surface, Accent, 3));
            t.SetColor("font_color", type, Text);
            t.SetColor("font_placeholder_color", type, Color.FromHtml("9a9a9a"));
            t.SetFontSize("font_size", type, 12);
        }

        t.SetStylebox("normal", "SpinBox", Box(Surface, Color.FromHtml("dbdcdd"), 3));

        t.SetStylebox("panel", "PopupMenu", Box(Colors.White, Border, 3));
        t.SetColor("font_color", "PopupMenu", Text);

        t.SetStylebox("panel", "Tree", Box(Colors.White, Border, 2));
        t.SetColor("font_color", "Tree", Text);
        t.SetColor("font_selected_color", "Tree", Text);
        t.SetStylebox("selected", "Tree", Box(Color.FromHtml("e8f1fb"), Color.FromHtml("a8d2fd"), 2));
        t.SetStylebox("selected_focus", "Tree", Box(AccentLight, Color.FromHtml("62a2e4"), 2));
        t.SetStylebox("cursor", "Tree", Box(Color.FromHtml("eef4fb"), Border, 2));
        t.SetStylebox("cursor_unfocused", "Tree", Box(Color.FromHtml("f4f8fd"), Border, 2));
        t.SetColor("title_button_color", "Tree", HeaderText);
        t.SetStylebox("title_button_normal", "Tree", Box(PanelAlt, Border, 0, 0));
        t.SetStylebox("title_button_hover", "Tree", Box(Color.FromHtml("edf4fc"), Border, 0, 0));
        t.SetStylebox("title_button_pressed", "Tree", Box(AccentLight, Border, 0, 0));
        t.SetFontSize("font_size", "Tree", 12);

        t.SetStylebox("background", "ProgressBar", Box(PanelAlt, Border, 3));
        t.SetStylebox("fill", "ProgressBar", Box(Accent, AccentDark, 3));
        t.SetColor("font_color", "ProgressBar", Text);

        t.SetStylebox("slider", "HSlider", Box(Color.FromHtml("dcddde"), Border, 2, 0));
        t.SetStylebox("grabber_area", "HSlider", Box(Accent, null, 2, 0));
        t.SetStylebox("grabber_area_highlight", "HSlider", Box(AccentDark, null, 2, 0));

        return t;
    }
}
