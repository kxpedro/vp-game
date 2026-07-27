using Godot;

public partial class GameCursor : Node
{
    private const string PointerPath = "res://assets/ui/kenney_hud/cursor_pointer.png";
    private const string PointingHandPath = "res://assets/ui/kenney_hud/cursor_gauntlet.png";

    public override void _Ready()
    {
        var pointer = ResourceLoader.Load<Texture2D>(PointerPath);
        var pointingHand = ResourceLoader.Load<Texture2D>(PointingHandPath);
        if (pointer != null)
        {
            Input.SetCustomMouseCursor(pointer, Input.CursorShape.Arrow, new Vector2(5.0f, 4.0f));
            Input.SetCustomMouseCursor(pointer, Input.CursorShape.Help, new Vector2(5.0f, 4.0f));
        }
        if (pointingHand != null)
            Input.SetCustomMouseCursor(pointingHand, Input.CursorShape.PointingHand, new Vector2(14.0f, 5.0f));
    }
}
