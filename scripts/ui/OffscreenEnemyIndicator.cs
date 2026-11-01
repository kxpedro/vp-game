using Godot;
using System.Collections.Generic;

// Indicador de inimigos fora da tela (conceito A2): brilho pontilhado de "calor" na borda do lado onde o inimigo está,
// mais forte e laranja quanto mais perto. Só cores da AAP-64. Parâmetros em tuning (hud.offscreen.*, painel F1).
public partial class OffscreenEnemyIndicator : Control
{
	private static readonly Color[] Heat =
	{
		new("73172d"), new("b4202a"), new("df3e23"), new("fa6a0a")
	};
	private static readonly float[,] Bayer =
	{
		{ 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 }
	};
	private const float LogicalWidth = 480.0f; // resolução lógica do mockup: 1 "pixel" = Size.X / 480
	private const int CellSize = 2;            // em pixels lógicos (dither mais grosso = menos retângulos)

	private readonly List<(Vector2 dir, float strength)> _sources = new();

	public override void _Ready()
	{
		Name = "OffscreenEnemyIndicator";
		MouseFilter = MouseFilterEnum.Ignore;
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public override void _Process(double delta)
	{
		_sources.Clear();
		if (CompendiumTuning.GetInt("hud.offscreen.enabled", 1) != 0)
			CollectSources();
		QueueRedraw();
	}

	private void CollectSources()
	{
		var viewport = GetViewport();
		var camera = viewport?.GetCamera3D();
		var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		if (camera == null || player == null)
			return;
		var rect = new Rect2(Vector2.Zero, viewport.GetVisibleRect().Size);
		var center = rect.Size * 0.5f;
		var near = CompendiumTuning.GetFloat("hud.offscreen.near_distance", 10.0f);
		var far = CompendiumTuning.GetFloat("hud.offscreen.far_distance", 40.0f);
		var maxCount = CompendiumTuning.GetInt("hud.offscreen.max_count", 3);
		var candidates = new List<(Vector2 dir, float dist)>();
		foreach (var node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node3D enemy || !enemy.IsInsideTree() || !enemy.Visible)
				continue;
			if (enemy.GetNodeOrNull<Health>("Health")?.IsDead == true)
				continue;
			var world = enemy.GlobalPosition;
			var behind = camera.IsPositionBehind(world);
			var screen = camera.UnprojectPosition(world);
			if (!behind && rect.Grow(-4.0f).HasPoint(screen))
				continue; // visível
			var dir = screen - center;
			if (behind)
				dir = -dir;
			if (dir.LengthSquared() < 1.0f)
				continue;
			candidates.Add((dir.Normalized(), world.DistanceTo(player.GlobalPosition)));
		}
		candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
		for (var i = 0; i < candidates.Count && i < maxCount; i++)
		{
			var t = Mathf.Clamp((candidates[i].dist - near) / Mathf.Max(0.1f, far - near), 0.0f, 1.0f);
			_sources.Add((candidates[i].dir, Mathf.Lerp(1.0f, 0.4f, t)));
		}
	}

	public override void _Draw()
	{
		if (_sources.Count == 0)
			return;
		var size = Size;
		var unit = size.X / LogicalWidth;
		var w = Mathf.RoundToInt(LogicalWidth / CellSize);
		var h = Mathf.RoundToInt(size.Y / unit / CellSize);
		var depthCells = Mathf.Max(1, Mathf.RoundToInt(CompendiumTuning.GetFloat("hud.offscreen.depth", 22.0f) / CellSize));
		var sharp = CompendiumTuning.GetFloat("hud.offscreen.sharpness", 9.0f);
		var gain = CompendiumTuning.GetFloat("hud.offscreen.intensity", 1.0f);
		var cell = unit * CellSize;
		for (var y = 0; y < h; y++)
		{
			for (var x = 0; x < w; x++)
			{
				var e = Mathf.Min(Mathf.Min(x, y), Mathf.Min(w - 1 - x, h - 1 - y));
				if (e >= depthCells)
					continue;
				var nx = (x + 0.5f - w * 0.5f) / (w * 0.5f);
				var ny = (y + 0.5f - h * 0.5f) / (h * 0.5f);
				var len = Mathf.Sqrt(nx * nx + ny * ny);
				if (len < 0.0001f)
					continue;
				var best = 0.0f;
				foreach (var (dir, strength) in _sources)
				{
					var align = (nx * dir.X + ny * dir.Y) / len;
					if (align <= 0.0f)
						continue;
					var v = Mathf.Pow(align, sharp) * (1.0f - (float)e / depthCells) * strength * gain;
					if (v > best)
						best = v;
				}
				best += Bayer[y & 3, x & 3] / 16.0f * 0.25f - 0.12f;
				if (best <= 0.12f)
					continue;
				var idx = best < 0.3f ? 0 : best < 0.55f ? 1 : best < 0.8f ? 2 : 3;
				DrawRect(new Rect2(x * cell, y * cell, cell, cell), Heat[idx]);
			}
		}
	}
}
