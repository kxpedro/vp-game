using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// Barra de bênçãos do HUD em jogo (guia de UI §8.6).
// Não é focável: o acesso completo por controle fica na ficha do personagem (aba Bênçãos, §8.13).
// O tooltip por mouse só repete o que a ficha mostra; a dica "[C/View/Create] Ficha" ao lado da barra indica o caminho.
public partial class HUD
{
	// Slots visíveis no total (ícones + "+N"), em coluna na borda esquerda: 7 slots + dica cabem com folga em 720 px de altura.
	private const int BlessingBarSlotLimit = 7;
	// ~360 px de largura máxima do tooltip (guia §3.6) a 15 pt.
	private const int BlessingTooltipWrapColumns = 46;

	private void RefreshBlessingHud()
	{
		if (_blessingHudList == null) return;
		RefreshFuryVisibility();
		RenderBlessingBar(RunState.RunBlessings);
	}

	public void RefreshBlessingDisplays()
	{
		RefreshBlessingHud();
		RefreshCharacterSheet();
	}

	// Pré-visualização (debug): 8 bênçãos fictícias com raridades variadas e níveis 1–3.
	// Não toca no RunState nem em save; o próximo RefreshBlessingHud volta a mostrar a run real.
	internal void PreviewBlessingBar()
	{
		if (_blessingHudList == null) return;
		var rarities = new[] { "Common", "Uncommon", "Rare", "Epic", "Legendary", "Stirps", "Common", "Rare" };
		var levels = new[] { 1, 2, 3, 1, 2, 1, 3, 2 };
		var records = new List<BloodBlessingRecord>();
		var definitions = BloodBlessingCatalog.All;
		for (var i = 0; i < rarities.Length; i++)
		{
			if (definitions.Count > 0)
			{
				var definition = definitions[i % definitions.Count];
				records.Add(new BloodBlessingRecord(definition.Id, definition.DisplayName, 1, rarities[i], 1.0f, levels[i]));
			}
			else
				records.Add(new BloodBlessingRecord($"preview_{i}", $"Bênção {i + 1}", 1, rarities[i], 1.0f, levels[i]));
		}
		RenderBlessingBar(records);
	}

	private void RenderBlessingBar(IReadOnlyList<BloodBlessingRecord> blessings)
	{
		ClearBlessingBar();
		// A coluna cresce para cima e para baixo e continua centrada na altura; zerar os offsets a recolhe ao conteúdo novo.
		_blessingHudList.GrowVertical = Control.GrowDirection.Both;
		_blessingHudList.OffsetTop = 0.0f;
		_blessingHudList.OffsetBottom = 0.0f;
		var count = blessings?.Count ?? 0;
		if (count == 0)
			return;

		var visibleCount = count > BlessingBarSlotLimit ? BlessingBarSlotLimit - 1 : count;
		for (var i = 0; i < visibleCount; i++)
			_blessingHudList.AddChild(CreateBlessingBarSlot(blessings[i]));

		if (count > visibleCount)
			_blessingHudList.AddChild(CreateBlessingOverflowSlot(blessings, visibleCount));

		_blessingHudList.AddChild(CreateCharacterSheetHint());
	}

	// RemoveChild + QueueFree: os nós antigos saem do contêiner no mesmo quadro (guia §5.1, regra 10).
	private void ClearBlessingBar()
	{
		foreach (var child in _blessingHudList.GetChildren())
		{
			_blessingHudList.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static Control CreateBlessingBarSlot(BloodBlessingRecord record)
	{
		var slot = UiComponents.BlessingIcon(record, UiMetrics.IconHud, true);
		slot.MouseFilter = Control.MouseFilterEnum.Pass;
		slot.MouseDefaultCursorShape = Control.CursorShape.Help;
		slot.TooltipText = BlessingBarTooltip(record);
		return slot;
	}

	// Excedente "+N" no mesmo formato de slot (PanelInset, borda 2), em vez do "+" solto.
	private static Control CreateBlessingOverflowSlot(IReadOnlyList<BloodBlessingRecord> blessings, int firstHidden)
	{
		var hiddenCount = blessings.Count - firstHidden;
		var slot = new Panel
		{
			CustomMinimumSize = new Vector2(UiMetrics.IconHud, UiMetrics.IconHud),
			MouseFilter = Control.MouseFilterEnum.Pass,
			MouseDefaultCursorShape = Control.CursorShape.Help
		};
		var style = new StyleBoxFlat { BgColor = UiPalette.PanelInset, BorderColor = UiPalette.GoldDim };
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(UiMetrics.RadiusBadge);
		slot.AddThemeStyleboxOverride("panel", style);

		var label = UiComponents.MakeLabel($"+{hiddenCount}", UiVariation.HudValueLabel);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		label.AddThemeColorOverride("font_color", UiPalette.Gold);
		label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		slot.AddChild(label);

		var text = new StringBuilder();
		text.Append(hiddenCount == 1 ? Localization.Text("hud.blessing_bar.more_one") : Localization.TextFormat("hud.blessing_bar.more_other", hiddenCount));
		for (var i = firstHidden; i < blessings.Count; i++)
		{
			var record = blessings[i];
			text.Append('\n').Append(Localization.TextFormat("hud.blessing_bar.overflow_entry",
				BlessingBarName(record), UiComponents.RarityLabel(record.Rarity), record.Level));
		}
		text.Append("\n\n").Append(Localization.Text("hud.blessing_bar.all_in_sheet"));
		slot.TooltipText = text.ToString();
		return slot;
	}

	// Dica do caminho por controle/teclado: glifo da ação Ficha (acompanha o último dispositivo) + rótulo.
	private static Control CreateCharacterSheetHint()
	{
		var hint = new HBoxContainer
		{
			Name = "BlessingBarSheetHint",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		hint.AddThemeConstantOverride("separation", UiMetrics.S1);
		var glyph = UiComponents.ButtonGlyph(UiAction.CharacterSheet);
		glyph.MouseFilter = Control.MouseFilterEnum.Ignore;
		glyph.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		hint.AddChild(glyph);
		var label = UiComponents.MakeLabel(Localization.Text("hud.blessing_bar.sheet_hint"), UiVariation.SecondaryLabel);
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AddThemeConstantOverride("outline_size", 4);
		label.AddThemeColorOverride("font_outline_color", UiPalette.TextOutline);
		hint.AddChild(label);
		return hint;
	}

	private static string BlessingBarName(BloodBlessingRecord record)
	{
		if (BloodBlessingCatalog.TryGet(record.Id, out var definition) && !string.IsNullOrEmpty(definition.DisplayName))
			return definition.DisplayName;
		return string.IsNullOrEmpty(record.Name) ? record.Id : record.Name;
	}

	// Tooltip no padrão do guia (§3.6): nome; raridade · nível; descrição com valores efetivos; flavor.
	// Texto simples (o TooltipLabel do tema não interpreta BBCode), quebrado em ~360 px.
	private static string BlessingBarTooltip(BloodBlessingRecord record)
	{
		var meta = $"{UiComponents.RarityLabel(record.Rarity)} · {Localization.TextFormat("ui.common.level", record.Level)}";
		if (!BloodBlessingCatalog.TryGet(record.Id, out var definition))
			return $"{BlessingBarName(record)}\n{meta}";

		var lines = new StringBuilder();
		lines.Append(BlessingBarName(record)).Append('\n').Append(meta);
		lines.Append('\n').Append(WrapBlessingTooltip(FormatBlessingLineage(definition)));
		var description = FormatBloodBlessingDescription(definition, record.EffectivePowerMultiplier);
		if (!string.IsNullOrWhiteSpace(description))
			lines.Append("\n\n").Append(WrapBlessingTooltip(description));
		if (!string.IsNullOrWhiteSpace(definition.FlavorText))
			lines.Append("\n\n").Append(WrapBlessingTooltip(definition.FlavorText));
		return lines.ToString();
	}

	private static string WrapBlessingTooltip(string text)
	{
		if (string.IsNullOrEmpty(text)) return "";
		var result = new StringBuilder();
		var paragraphs = text.Replace("\r", "").Split('\n');
		for (var p = 0; p < paragraphs.Length; p++)
		{
			if (p > 0) result.Append('\n');
			var lineLength = 0;
			foreach (var word in Regex.Split(paragraphs[p].Trim(), @"\s+"))
			{
				if (word.Length == 0) continue;
				if (lineLength > 0 && lineLength + 1 + word.Length > BlessingTooltipWrapColumns)
				{
					result.Append('\n');
					lineLength = 0;
				}
				else if (lineLength > 0)
				{
					result.Append(' ');
					lineLength++;
				}
				result.Append(word);
				lineLength += word.Length;
			}
		}
		return result.ToString();
	}
}
