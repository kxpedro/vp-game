using Godot;

// StyleBox do HUD em pixel art (modo 2D): fundo chapado por dentro da moldura e a moldura 9-slice por cima, tingida
// (Tint) com a cor do estado ou da raridade. Criado só por UiPixelKit (Frame/Ring).
public partial class PixelFrameBox : StyleBox
{
    public Texture2D Frame { get; set; }
    public int Margin { get; set; }               // borda 9-slice da textura (px do PNG)
    public int BackgroundInset { get; set; }      // o fundo começa por dentro do contorno (cantos arredondados)
    public Color Tint { get; set; } = Colors.White;
    public Color Background { get; set; } = Colors.Transparent;
    public int Expand { get; set; }               // anel de foco: desenha por fora do controle

    public override void _Draw(Rid toCanvasItem, Rect2 rect)
    {
        rect = rect.Grow(Expand);
        if (Background.A > 0)
        {
            var inner = rect.Grow(-BackgroundInset);
            if (inner.Size.X > 0 && inner.Size.Y > 0)
                RenderingServer.CanvasItemAddRect(toCanvasItem, inner, Background);
        }
        if (Frame == null || Tint.A <= 0 || rect.Size.X < Margin * 2 || rect.Size.Y < Margin * 2)
            return;
        var margin = new Vector2(Margin, Margin);
        RenderingServer.CanvasItemAddNinePatch(toCanvasItem, rect, new Rect2(Vector2.Zero, Frame.GetSize()), Frame.GetRid(),
            margin, margin, RenderingServer.NinePatchAxisMode.Stretch, RenderingServer.NinePatchAxisMode.Stretch, false, Tint);
    }

    public override Rect2 _GetDrawRect(Rect2 rect) => rect.Grow(Expand);
}
