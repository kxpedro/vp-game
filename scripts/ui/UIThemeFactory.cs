using Godot;

public static class UIThemeFactory
{
    public const string SimplePanelPath = "res://assets/ui/kenney_hud/simple_panel.png";
    public const string SimpleDividerPath = "res://assets/ui/kenney_hud/simple_divider.png";

    public static Theme Create()
    {
        var theme = new Theme { DefaultFontSize = 17 };
        var text = new Color(0.92f, 0.88f, 0.80f);
        var muted = new Color(0.68f, 0.62f, 0.57f);
        var gold = new Color(0.82f, 0.64f, 0.30f);
        theme.SetColor("font_color", "Label", text);
        theme.SetColor("font_color", "RichTextLabel", text);
        theme.SetColor("default_color", "RichTextLabel", text);
        theme.SetColor("font_color", "Button", text);
        theme.SetColor("font_hover_color", "Button", Colors.White);
        theme.SetColor("font_pressed_color", "Button", gold);
        theme.SetColor("font_disabled_color", "Button", muted.Darkened(0.35f));
        theme.SetColor("font_color", "OptionButton", text);
        theme.SetColor("font_color", "CheckButton", text);
        theme.SetConstant("outline_size", "Label", 3);
        theme.SetColor("font_outline_color", "Label", new Color(0.02f, 0.005f, 0.008f, 0.9f));

        theme.SetStylebox("normal", "Button", Box(new Color(0.055f, 0.025f, 0.032f, 0.96f), new Color(0.22f, 0.16f, 0.17f), 1, 4));
        theme.SetStylebox("hover", "Button", Box(new Color(0.11f, 0.035f, 0.045f, 0.98f), gold.Darkened(0.22f), 1, 4));
        theme.SetStylebox("pressed", "Button", Box(new Color(0.17f, 0.025f, 0.038f, 1.0f), new Color(0.72f, 0.16f, 0.17f), 1, 4));
        theme.SetStylebox("focus", "Button", Box(Colors.Transparent, gold.Darkened(0.15f), 1, 4));
        theme.SetStylebox("disabled", "Button", Box(new Color(0.035f, 0.025f, 0.03f, 0.8f), new Color(0.12f, 0.1f, 0.11f), 1));
        theme.SetStylebox("panel", "PanelContainer", Box(new Color(0.016f, 0.011f, 0.014f, 0.94f), new Color(0.19f, 0.13f, 0.14f), 1, 4));
        theme.SetStylebox("normal", "LineEdit", Box(new Color(0.025f, 0.015f, 0.02f, 1), new Color(0.32f, 0.18f, 0.18f), 1));
        theme.SetStylebox("normal", "OptionButton", Box(new Color(0.06f, 0.025f, 0.035f, 1), new Color(0.42f, 0.18f, 0.18f), 1));
        theme.SetStylebox("hover", "OptionButton", Box(new Color(0.13f, 0.035f, 0.05f, 1), gold, 2));
        theme.SetStylebox("slider", "HSlider", new StyleBoxFlat { BgColor = new Color(0.16f, 0.08f, 0.09f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        theme.SetStylebox("grabber_area", "HSlider", new StyleBoxFlat { BgColor = new Color(0.72f, 0.06f, 0.09f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        return theme;
    }

    public static StyleBoxFlat HudPanel(float alpha = 0.84f) => Box(
        new Color(0.012f, 0.009f, 0.011f, alpha),
        new Color(0.28f, 0.16f, 0.17f, 0.72f),
        1,
        4);

    public static StyleBoxFlat ChoiceCard(Color accent, float alpha = 0.98f) => Box(
        new Color(0.022f, 0.006f, 0.012f, alpha),
        accent,
        3,
        2);

    public static NinePatchRect AddSimpleFrame(
        Control parent,
        string name,
        Vector4 anchors,
        Vector4 offsets,
        Color tint,
        int patchMargin = 10)
    {
        var frame = new NinePatchRect
        {
            Name = name,
            Texture = ResourceLoader.Load<Texture2D>(SimplePanelPath),
            AnchorLeft = anchors.X,
            AnchorTop = anchors.Y,
            AnchorRight = anchors.Z,
            AnchorBottom = anchors.W,
            OffsetLeft = offsets.X,
            OffsetTop = offsets.Y,
            OffsetRight = offsets.Z,
            OffsetBottom = offsets.W,
            PatchMarginLeft = patchMargin,
            PatchMarginTop = patchMargin,
            PatchMarginRight = patchMargin,
            PatchMarginBottom = patchMargin,
            DrawCenter = true,
            Modulate = tint,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(frame);
        return frame;
    }

    private static StyleBoxFlat Box(Color background, Color border, int borderWidth, int radius = 7)
    {
        return new StyleBoxFlat
        {
            BgColor = background, BorderColor = border,
            BorderWidthLeft = borderWidth, BorderWidthTop = borderWidth, BorderWidthRight = borderWidth, BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10
        };
    }
}
