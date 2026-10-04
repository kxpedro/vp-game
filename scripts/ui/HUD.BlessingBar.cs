using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class HUD
{
	private void RefreshBlessingHud()
	{
		if (_blessingHudList == null) return;
		RefreshFuryVisibility();
		foreach (var child in _blessingHudList.GetChildren()) child.QueueFree();
		const int visibleBlessingLimit = 7;
		var visibleCount = Mathf.Min(RunState.RunBlessings.Count, visibleBlessingLimit);
		for (var i = 0; i < visibleCount; i++)
		{
			var record = RunState.RunBlessings[i];
			if (!BloodBlessingCatalog.TryGet(record.Id, out var blessing)) continue;
			var iconContainer = new Control
			{
				CustomMinimumSize = new Vector2(44.0f, 44.0f),
				TooltipText = FullBlessingTooltip(blessing, record.Rarity, record.EffectivePowerMultiplier, record.Level),
				MouseFilter = Control.MouseFilterEnum.Pass,
				MouseDefaultCursorShape = Control.CursorShape.Help
			};
			iconContainer.AddChild(new TextureRect
			{
				Texture = ResourceLoader.Load<Texture2D>(UIThemeFactory.SimplePanelPath),
				AnchorRight = 1.0f,
				AnchorBottom = 1.0f,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				Modulate = new Color(0.12f, 0.08f, 0.09f, 0.72f),
				MouseFilter = Control.MouseFilterEnum.Ignore
			});
			var icon = new TextureRect
			{
				Texture = ResourceLoader.Load<Texture2D>(blessing.IconPath),
				AnchorRight = 1.0f, AnchorBottom = 1.0f,
				OffsetLeft = 4.0f, OffsetTop = 4.0f, OffsetRight = -4.0f, OffsetBottom = -4.0f,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			iconContainer.AddChild(icon);
			var blessingLevel = record.Level;
			if (blessingLevel > 1)
			{
				var levelBadge = new Label
				{
					Text = $"N{blessingLevel}",
					AnchorLeft = 1.0f,
					AnchorTop = 1.0f,
					AnchorRight = 1.0f,
					AnchorBottom = 1.0f,
					OffsetLeft = -24.0f,
					OffsetTop = -18.0f,
					OffsetRight = 1.0f,
					OffsetBottom = 1.0f,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					MouseFilter = Control.MouseFilterEnum.Ignore
				};
				levelBadge.AddThemeFontSizeOverride("font_size", 13);
				levelBadge.AddThemeColorOverride("font_color", Colors.White);
				levelBadge.AddThemeColorOverride("font_shadow_color", Colors.Black);
				levelBadge.AddThemeConstantOverride("shadow_offset_x", 2);
				levelBadge.AddThemeConstantOverride("shadow_offset_y", 2);
				iconContainer.AddChild(levelBadge);
			}
			_blessingHudList.AddChild(iconContainer);
		}

		if (RunState.RunBlessings.Count <= visibleBlessingLimit)
			return;

		var hiddenCount = RunState.RunBlessings.Count - visibleBlessingLimit;
		var more = new Label
		{
			Text = "+",
			TooltipText = $"Mais {hiddenCount} bênção(ões). Pressione C para ver todas.",
			CustomMinimumSize = new Vector2(44.0f, 40.0f),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Pass,
			MouseDefaultCursorShape = Control.CursorShape.Help,
			Modulate = new Color(1.0f, 0.3f, 0.34f)
		};
		more.AddThemeFontSizeOverride("font_size", 30);
		_blessingHudList.AddChild(more);
	}

	public void RefreshBlessingDisplays()
	{
		RefreshBlessingHud();
		RefreshCharacterSheet();
	}
}
