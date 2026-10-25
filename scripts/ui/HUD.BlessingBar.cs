using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// Slots de herança do HUD em jogo, em coluna na borda esquerda (como os slots de bênção do Hades).
// Um slot fixo por tipo de ataque (básico, rápido, especial, esquiva e Fúria Bestial): vazio mostra o ícone apagado,
// preenchido mostra o ícone na cor da família da herança. As demais heranças ficam só na ficha (tecla C); quando há
// alguma, um "+" abaixo do último slot avisa que há mais coisa lá.
// Não é focável: o acesso completo por controle fica na ficha do personagem (aba Bênçãos, §8.13).
public partial class HUD
{
	// Losango do slot (caixa de 56 px) com o ícone 32x32 em 1x no centro, para o pixel art ficar nítido.
	private const int InheritanceSlotSize = 56;
	private const int InheritanceSlotIconSize = 32;
	// ~360 px de largura máxima do tooltip (guia §3.6) a 15 pt.
	private const int BlessingTooltipWrapColumns = 46;

	// Ordem da coluna, de cima para baixo. Group = exclusiveGroup das heranças em blood_blessings.json (uma por slot).
	// Ainda não há herança de esquiva: o grupo dash_status fica reservado e o slot aparece sempre vazio.
	private static readonly (string Group, string Icon, string LabelKey)[] InheritanceSlots =
	{
		("basic_attack_status", "slot_basic_attack.png", "ui.attack.basic"),
		("quick_attack_status", "slot_quick_attack.png", "ui.attack.quick"),
		("special_attack_status", "slot_special_attack.png", "ui.attack.special"),
		("dash_status", "slot_dash.png", "ui.attack.dash"),
		("bestial_fury_mode", "slot_bestial_fury.png", "blessing.keyword.fury.term")
	};

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

	// Pré-visualização (debug): básico e especial preenchidos, os demais slots vazios e 3 heranças fora dos slots (o "+").
	// Não toca no RunState nem em save; o próximo RefreshBlessingHud volta a mostrar a run real.
	internal void PreviewBlessingBar()
	{
		if (_blessingHudList == null) return;
		var records = new List<BloodBlessingRecord>();
		foreach (var group in new[] { "basic_attack_status", "special_attack_status" })
		{
			var definition = BloodBlessingCatalog.All.FirstOrDefault(candidate => SameGroup(candidate.ExclusiveGroup, group));
			if (definition != null)
				records.Add(new BloodBlessingRecord(definition.Id, definition.DisplayName, 1, definition.Rarity, 1.0f, 2));
		}
		foreach (var definition in BloodBlessingCatalog.All.Where(candidate => InheritanceSlotIndex(candidate) < 0).Take(3))
			records.Add(new BloodBlessingRecord(definition.Id, definition.DisplayName, 1, definition.Rarity, 1.0f, 1));
		RenderBlessingBar(records);
	}

	private void RenderBlessingBar(IReadOnlyList<BloodBlessingRecord> blessings)
	{
		ClearBlessingBar();
		// A coluna cresce para cima e para baixo e continua centrada na altura; zerar os offsets a recolhe ao conteúdo novo.
		_blessingHudList.GrowVertical = Control.GrowDirection.Both;
		_blessingHudList.OffsetTop = 0.0f;
		_blessingHudList.OffsetBottom = 0.0f;
		_blessingHudList.AddThemeConstantOverride("separation", UiMetrics.S1);

		var filled = new BloodBlessingRecord[InheritanceSlots.Length];
		var others = new List<BloodBlessingRecord>();
		foreach (var record in blessings ?? Array.Empty<BloodBlessingRecord>())
		{
			var index = BloodBlessingCatalog.TryGet(record.Id, out var definition) ? InheritanceSlotIndex(definition) : -1;
			if (index >= 0 && filled[index] == null)
				filled[index] = record;
			else
				others.Add(record);
		}

		for (var i = 0; i < InheritanceSlots.Length; i++)
			_blessingHudList.AddChild(CreateInheritanceSlot(i, filled[i]));

		if (others.Count > 0)
			_blessingHudList.AddChild(CreateBlessingOverflowSlot(others));
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

	private static int InheritanceSlotIndex(BloodBlessingDefinition definition)
	{
		if (definition == null || string.IsNullOrWhiteSpace(definition.ExclusiveGroup)) return -1;
		for (var i = 0; i < InheritanceSlots.Length; i++)
			if (SameGroup(definition.ExclusiveGroup, InheritanceSlots[i].Group))
				return i;
		return -1;
	}

	private static bool SameGroup(string a, string b)
		=> !string.IsNullOrWhiteSpace(a) && a.Equals(b, StringComparison.OrdinalIgnoreCase);

	// Cor da família no slot preenchido (Bathory vermelho, Orlok verde, Karnstein rosa, Dracul roxo).
	private static Color InheritanceFamilyColor(string family) => (family ?? "").Trim().ToLowerInvariant() switch
	{
		"bathory" => new Color(0.93f, 0.25f, 0.27f),
		"orlok" => new Color(0.47f, 0.85f, 0.34f),
		"karnstein" => new Color(0.98f, 0.52f, 0.74f),
		"dracul" => new Color(0.70f, 0.44f, 0.98f),
		"stirps" => UiPalette.RarityStirps,
		_ => UiPalette.LifeHighlight
	};

	// Losango desenhado (fundo do HUD + borda) com o ícone do tipo de ataque no centro.
	private static Control CreateInheritanceSlot(int index, BloodBlessingRecord record)
	{
		var info = InheritanceSlots[index];
		var isFilled = record != null;
		var familyColor = UiPalette.GoldDim;
		if (isFilled && BloodBlessingCatalog.TryGet(record.Id, out var definition))
			familyColor = InheritanceFamilyColor(definition.Family);

		var slot = new Control
		{
			Name = $"InheritanceSlot{index}",
			CustomMinimumSize = new Vector2(InheritanceSlotSize, InheritanceSlotSize),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
			MouseFilter = Control.MouseFilterEnum.Pass,
			MouseDefaultCursorShape = Control.CursorShape.Help
		};
		var border = isFilled ? familyColor : UiPalette.Border;
		var fill = isFilled ? UiPalette.PanelHud : new Color(UiPalette.PanelHud, 0.60f);
		slot.Draw += () =>
		{
			var half = InheritanceSlotSize * 0.5f;
			var points = new[]
			{
				new Vector2(half, 1), new Vector2(InheritanceSlotSize - 1, half),
				new Vector2(half, InheritanceSlotSize - 1), new Vector2(1, half)
			};
			slot.DrawColoredPolygon(points, fill);
			slot.DrawPolyline(new[] { points[0], points[1], points[2], points[3], points[0] }, border, isFilled ? 3.0f : 2.0f);
		};

		var texture = UiPixelKit.Texture(info.Icon);
		if (texture != null)
		{
			var offset = (InheritanceSlotSize - InheritanceSlotIconSize) / 2;
			var icon = new TextureRect
			{
				Texture = texture,
				Position = new Vector2(offset, offset),
				Size = new Vector2(InheritanceSlotIconSize, InheritanceSlotIconSize),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				// Ícone em tons neutros: tingido com a cor da família, ou apagado quando o slot está vazio.
				Modulate = isFilled ? familyColor.Lightened(0.15f) : new Color(0.40f, 0.36f, 0.38f, 0.85f)
			};
			slot.AddChild(icon);
		}

		slot.TooltipText = isFilled
			? $"{Localization.Text(info.LabelKey)}\n\n{BlessingBarTooltip(record)}"
			: $"{Localization.Text(info.LabelKey)}\n{WrapBlessingTooltip(Localization.Text("hud.inheritance_slot.empty"))}";
		return slot;
	}

	// "+" abaixo do último slot: há heranças fora dos slots, que só aparecem na ficha. O glifo da ação Ficha ao lado
	// mostra o caminho; o tooltip lista as heranças escondidas.
	private static Control CreateBlessingOverflowSlot(IReadOnlyList<BloodBlessingRecord> blessings)
	{
		var row = new HBoxContainer
		{
			Name = "InheritanceOverflow",
			CustomMinimumSize = new Vector2(InheritanceSlotSize, 0),
			MouseFilter = Control.MouseFilterEnum.Pass,
			MouseDefaultCursorShape = Control.CursorShape.Help,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
		};
		row.AddThemeConstantOverride("separation", UiMetrics.S1);

		var label = UiComponents.MakeLabel("+", UiVariation.HudValueLabel);
		label.CustomMinimumSize = new Vector2(InheritanceSlotSize, 0);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		label.AddThemeColorOverride("font_color", UiPalette.Gold);
		label.AddThemeConstantOverride("outline_size", 4);
		label.AddThemeColorOverride("font_outline_color", UiPalette.TextOutline);
		row.AddChild(label);
		row.AddChild(CreateCharacterSheetHint());

		var text = new StringBuilder();
		text.Append(blessings.Count == 1 ? Localization.Text("hud.blessing_bar.more_one") : Localization.TextFormat("hud.blessing_bar.more_other", blessings.Count));
		foreach (var record in blessings)
		{
			text.Append('\n').Append(Localization.TextFormat("hud.blessing_bar.overflow_entry",
				BlessingBarName(record), UiComponents.RarityLabel(record.Rarity), record.Level));
		}
		text.Append("\n\n").Append(Localization.Text("hud.blessing_bar.all_in_sheet"));
		row.TooltipText = text.ToString();
		return row;
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
