using Godot;

public static class UIThemeFactory
{
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

        theme.SetStylebox("normal", "Button", Box(new Color(0.075f, 0.025f, 0.035f, 0.96f), new Color(0.34f, 0.12f, 0.15f), 1));
        theme.SetStylebox("hover", "Button", Box(new Color(0.16f, 0.035f, 0.05f, 0.98f), gold, 2));
        theme.SetStylebox("pressed", "Button", Box(new Color(0.26f, 0.025f, 0.045f, 1.0f), new Color(0.95f, 0.2f, 0.18f), 2));
        theme.SetStylebox("focus", "Button", Box(Colors.Transparent, gold, 2));
        theme.SetStylebox("disabled", "Button", Box(new Color(0.035f, 0.025f, 0.03f, 0.8f), new Color(0.12f, 0.1f, 0.11f), 1));
        theme.SetStylebox("panel", "PanelContainer", Box(new Color(0.025f, 0.012f, 0.018f, 0.94f), new Color(0.42f, 0.15f, 0.17f), 2, 10));
        theme.SetStylebox("normal", "LineEdit", Box(new Color(0.025f, 0.015f, 0.02f, 1), new Color(0.32f, 0.18f, 0.18f), 1));
        theme.SetStylebox("normal", "OptionButton", Box(new Color(0.06f, 0.025f, 0.035f, 1), new Color(0.42f, 0.18f, 0.18f), 1));
        theme.SetStylebox("hover", "OptionButton", Box(new Color(0.13f, 0.035f, 0.05f, 1), gold, 2));
        theme.SetStylebox("slider", "HSlider", new StyleBoxFlat { BgColor = new Color(0.16f, 0.08f, 0.09f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        theme.SetStylebox("grabber_area", "HSlider", new StyleBoxFlat { BgColor = new Color(0.72f, 0.06f, 0.09f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 });
        return theme;
    }

    public static StyleBoxFlat HudPanel(float alpha = 0.84f) => Box(new Color(0.018f, 0.008f, 0.012f, alpha), new Color(0.42f, 0.10f, 0.13f, 0.9f), 1, 8);

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
