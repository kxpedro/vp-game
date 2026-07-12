using Godot;
using System.Collections.Generic;

public partial class MinimapControl : Control
{
    private readonly HashSet<Vector2I> _revealed = new();
    private RoomGenerator _generator;
    private Node3D _player;
    private bool _expanded;

    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; CallDeferred(nameof(BindWorld)); }

    public override void _Process(double delta)
    {
        if (_generator == null || _player == null) { BindWorld(); return; }
        var playerCell = WorldToCell(_player.GlobalPosition);
        foreach (var cell in _generator.WalkableCells)
            if (cell.DistanceTo(playerCell) <= 5.5f) _revealed.Add(cell);
        QueueRedraw();
    }

    public void SetExpanded(bool expanded)
    {
        _expanded = expanded;
        AnchorLeft = expanded ? 0 : 1; AnchorTop = 0; AnchorRight = 1; AnchorBottom = expanded ? 1 : 0;
        OffsetLeft = expanded ? 0 : -260; OffsetTop = expanded ? 0 : 24; OffsetRight = expanded ? 0 : -24; OffsetBottom = expanded ? 0 : 260;
        Modulate = new Color(1, 1, 1, expanded ? 0.48f : 0.9f);
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.015f, 0.01f, 0.02f, _expanded ? 0.42f : 0.78f));
        if (_generator == null || _revealed.Count == 0) return;
        var bounds = GetBounds();
        var scale = Mathf.Min(Size.X / Mathf.Max(bounds.Size.X + 2, 1), Size.Y / Mathf.Max(bounds.Size.Y + 2, 1));
        var center = Size * 0.5f;
        foreach (var cell in _revealed)
        {
            var p = ToMap(cell, bounds, scale, center);
            DrawRect(new Rect2(p - Vector2.One * scale * 0.45f, Vector2.One * scale * 0.9f), new Color(0.42f, 0.42f, 0.46f));
        }
        DrawMarker(_player.GlobalPosition, Colors.White, scale, bounds, center);
        DrawMarker(_generator.ExitPortalPosition, new Color(0.95f, 0.05f, 0.05f), scale, bounds, center);
    }

    private void DrawMarker(Vector3 world, Color color, float scale, Rect2I bounds, Vector2 center)
    {
        var cell = WorldToCell(world); if (!_revealed.Contains(cell)) return;
        DrawCircle(ToMap(cell, bounds, scale, center), Mathf.Max(scale * 0.32f, 3), color);
    }

    private static Vector2 ToMap(Vector2I cell, Rect2I bounds, float scale, Vector2 center) => center + new Vector2(cell.X - bounds.Position.X - bounds.Size.X * 0.5f, cell.Y - bounds.Position.Y - bounds.Size.Y * 0.5f) * scale;
    private void BindWorld() { _generator = GetTree().GetFirstNodeInGroup("room_generator") as RoomGenerator; _player = GetTree().GetFirstNodeInGroup("player") as Node3D; }
    private Vector2I WorldToCell(Vector3 world) => new(Mathf.RoundToInt(world.X / (_generator?.MapCellSize ?? 2.05f)), Mathf.RoundToInt(world.Z / (_generator?.MapCellSize ?? 2.05f)));
    private Rect2I GetBounds()
    {
        var first = true; var min = Vector2I.Zero; var max = Vector2I.Zero;
        foreach (var cell in _generator.WalkableCells) { if (first) { min = max = cell; first = false; } min = new(Mathf.Min(min.X, cell.X), Mathf.Min(min.Y, cell.Y)); max = new(Mathf.Max(max.X, cell.X), Mathf.Max(max.Y, cell.Y)); }
        return new Rect2I(min, max - min + Vector2I.One);
    }
}
