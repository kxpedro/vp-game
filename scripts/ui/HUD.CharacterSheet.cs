using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class HUD
{
	private void BuildCharacterSheetOverlay(Control root)
	{
		_characterSheetOverlay = new ColorRect
		{
			Name = "CharacterSheetOverlay",
			Color = new Color(0.02f, 0.0f, 0.0f, 0.86f),
			AnchorLeft = 0.02f,
			AnchorTop = 0.04f,
			AnchorRight = 0.98f,
			AnchorBottom = 0.96f,
			Visible = false
		};
		root.AddChild(_characterSheetOverlay);

		var title = new Label
		{
			Name = "CharacterSheetTitle",
			Text = "FICHA DE PERSONAGEM",
			AnchorRight = 1.0f,
			OffsetTop = 18.0f,
			OffsetBottom = 56.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_characterSheetOverlay.AddChild(title);

		var columns = new HBoxContainer
		{
			Name = "CharacterSheetColumns",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = 34.0f,
			OffsetTop = 76.0f,
			OffsetRight = -34.0f,
			OffsetBottom = -28.0f
		};
		_characterSheetOverlay.AddChild(columns);

		var blessingScroll = new ScrollContainer
		{
			Name = "CharacterBlessingsScroll",
			CustomMinimumSize = new Vector2(230.0f, 0.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		_characterBlessingsColumn = CreateCharacterSheetColumn("Bênçãos equipadas", 230.0f);
		blessingScroll.AddChild(_characterBlessingsColumn);

		_characterAttributesColumn = CreateCharacterSheetColumn("Atributos", 220.0f);
		_characterInventoryColumn = CreateCharacterSheetColumn("Inventario", 270.0f);
		_characterLoadoutColumn = CreateCharacterSheetColumn("Habilidades e Artefatos", 270.0f);
		columns.AddChild(blessingScroll);
		columns.AddChild(_characterAttributesColumn);
		columns.AddChild(_characterInventoryColumn);
		columns.AddChild(_characterLoadoutColumn);
	}

	private VBoxContainer CreateCharacterSheetColumn(string title, float width)
	{
		var column = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(width, 0.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};

		column.AddChild(new Label
		{
			Text = title,
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(0.0f, 32.0f)
		});

		return column;
	}

	private void ToggleCharacterSheet()
	{
		if (_characterSheetOverlay == null)
			return;

		_characterSheetOverlay.Visible = !_characterSheetOverlay.Visible;
		if (_characterSheetOverlay.Visible)
		{
			SetModalPause(true);
			RefreshCharacterSheet();
			ControllerUiSupport.FocusFirst(_characterSheetOverlay);
		}
		else
			RefreshModalPause();
	}

	private void RefreshCharacterSheet()
	{
		if (_characterBlessingsColumn == null || _characterAttributesColumn == null || _characterInventoryColumn == null || _characterLoadoutColumn == null)
			return;

		ClearCharacterSheetColumn(_characterBlessingsColumn);
		ClearCharacterSheetColumn(_characterAttributesColumn);
		ClearCharacterSheetColumn(_characterInventoryColumn);
		ClearCharacterSheetColumn(_characterLoadoutColumn);

		AddCharacterSheetBlessings(_characterBlessingsColumn);

		AddCharacterSheetText(_characterAttributesColumn, $"Moedas de Sangue: {CharacterSheet.BloodCoins}");
		AddCharacterSheetText(_characterAttributesColumn, $"Forca: {CharacterSheet.GetAttribute(CharacterAttribute.Strength)}");
		AddCharacterSheetText(_characterAttributesColumn, $"Destreza: {CharacterSheet.GetAttribute(CharacterAttribute.Dexterity)}");
		AddCharacterSheetText(_characterAttributesColumn, $"Vitalidade: {CharacterSheet.GetAttribute(CharacterAttribute.Vitality)}");
		AddCharacterSheetText(_characterAttributesColumn, $"Determinacao: {CharacterSheet.GetAttribute(CharacterAttribute.Resolve)}");
		AddCharacterSheetText(_characterAttributesColumn, $"Sangue: {CharacterSheet.GetAttribute(CharacterAttribute.Bloodcraft)}");
		AddCharacterSheetSpacer(_characterAttributesColumn);
		AddCharacterSheetSection(_characterAttributesColumn, "Status detalhados");
		AddCharacterSheetText(_characterAttributesColumn, $"Dano atributos: x{CharacterSheet.GetDamageMultiplier():0.00}");
		AddCharacterSheetText(_characterAttributesColumn, $"Dano run: x{RunState.DamageMultiplier:0.00}");
		AddCharacterSheetText(_characterAttributesColumn, $"Dano tuning: x{GameTuning.GetDamageMultiplier():0.00}");
		AddCharacterSheetText(_characterAttributesColumn, $"Vida bonus atributos: +{CharacterSheet.GetMaxHealthBonus():0}");
		AddCharacterSheetText(_characterAttributesColumn, $"Vida bonus artefatos: +{RunState.MaxHealthBonus:0}");
		AddCharacterSheetText(_characterAttributesColumn, $"Velocidade: x{CharacterSheet.GetMoveSpeedMultiplier():0.00}");
		AddCharacterSheetText(_characterAttributesColumn, $"Furia: x{CharacterSheet.GetFuryMultiplier():0.00}");

		AddCharacterSheetSection(_characterInventoryColumn, "Artefatos da run");
		AddCharacterSheetLines(_characterInventoryColumn, FormatRunArtifacts());
		AddCharacterSheetSection(_characterInventoryColumn, "Colecao permanente");
		AddCharacterSheetLines(_characterInventoryColumn, FormatArtifacts());

		AddCharacterSheetSection(_characterLoadoutColumn, "Habilidades passivas");
		AddCharacterSheetSkills(_characterLoadoutColumn);
		AddCharacterSheetSection(_characterLoadoutColumn, "Artefatos equipados");
		AddCharacterSheetLines(_characterLoadoutColumn, FormatEquippedArtifacts());
		AddCharacterSheetSection(_characterLoadoutColumn, "Impacto dos artefatos");
		AddCharacterSheetLines(_characterLoadoutColumn, FormatArtifactImpacts());
	}

	private void ClearCharacterSheetColumn(VBoxContainer column)
	{
		for (var i = column.GetChildCount() - 1; i >= 1; i--)
			column.GetChild(i).QueueFree();
	}

	private void AddCharacterSheetSection(VBoxContainer column, string title)
	{
		AddCharacterSheetSpacer(column);
		AddCharacterSheetText(column, title, true);
	}

	private void AddCharacterSheetLines(VBoxContainer column, string lines)
	{
		foreach (var line in lines.Split('\n', StringSplitOptions.RemoveEmptyEntries))
			AddCharacterSheetText(column, line);
	}

	private void AddCharacterSheetBlessings(VBoxContainer column)
	{
		if (RunState.RunBlessings.Count == 0)
		{
			AddCharacterSheetText(column, "- nenhuma");
			return;
		}
		foreach (var record in RunState.RunBlessings)
		{
			if (!BloodBlessingCatalog.TryGet(record.Id, out var blessing)) continue;
			var row = new HBoxContainer
			{
				TooltipText = FullBlessingTooltip(blessing, record.Rarity, record.EffectivePowerMultiplier, record.Level),
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			};
			row.AddChild(new TextureRect
			{
				Texture = ResourceLoader.Load<Texture2D>(blessing.IconPath), CustomMinimumSize = new Vector2(38, 38),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = Control.MouseFilterEnum.Ignore
			});
			row.AddChild(new Label
			{
				Text = $"[{record.Rarity}] {record.Name} — N{record.Level}",
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				MouseFilter = Control.MouseFilterEnum.Ignore
			});
			column.AddChild(row);
		}
	}

	private void AddCharacterSheetText(VBoxContainer column, string text, bool centered = false)
	{
		column.AddChild(new Label
		{
			Text = text,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			HorizontalAlignment = centered ? HorizontalAlignment.Center : HorizontalAlignment.Left
		});
	}

	private void AddCharacterSheetSpacer(VBoxContainer column)
	{
		column.AddChild(new Control { CustomMinimumSize = new Vector2(0.0f, 10.0f) });
	}

	private void AddCharacterSheetSkills(VBoxContainer column)
	{
		if (CharacterSheet.AcquiredSkills.Count == 0)
		{
			AddCharacterSheetText(column, "- nenhuma");
			return;
		}

		foreach (var skillId in CharacterSheet.AcquiredSkills)
		{
			var rank = CharacterSheet.GetSkillRank(skillId);
			if (!SkillCatalog.TryGet(skillId, out var definition))
			{
				AddCharacterSheetText(column, $"• {skillId} nv. {rank}");
				continue;
			}
			var entry = new Button
			{
				Text = $"{definition.IconText}  {definition.DisplayName}  {rank}/{definition.MaxRank}",
				TooltipText = $"{definition.DisplayName}\n{definition.Description}\n\n{definition.FlavorText}",
				Alignment = HorizontalAlignment.Left,
				Flat = true,
				MouseDefaultCursorShape = Control.CursorShape.Help,
				CustomMinimumSize = new Vector2(0.0f, 38.0f)
			};
			column.AddChild(entry);
		}
	}

	private string FormatArtifacts()
	{
		if (CharacterSheet.FoundArtifacts.Count == 0)
			return "- nenhum\n";

		var text = "";
		foreach (var artifact in CharacterSheet.FoundArtifacts)
			text += $"- {artifact.Name} ({artifact.Effect} +{artifact.Amount:0.##})\n";

		return text;
	}

	private string FormatRunArtifacts()
	{
		if (RunState.RunArtifacts.Count == 0)
			return "- nenhum\n";

		var text = "";
		foreach (var artifact in RunState.RunArtifacts)
			text += $"- {artifact.Name} ({artifact.Effect} +{artifact.Amount:0.##})\n";

		return text;
	}

	private string FormatArtifactImpacts()
	{
		if (RunState.RunArtifacts.Count == 0)
			return "- nenhum\n";

		var text = "";
		foreach (var artifact in RunState.RunArtifacts)
			text += $"- {artifact.Name}: {artifact.Effect} +{artifact.Amount:0.##}\n";

		return text;
	}

	private string FormatEquippedArtifacts()
	{
		var text = "";
		for (var i = 0; i < CharacterSheet.EquippedArtifactSlotCount; i++)
		{
			var artifactName = CharacterSheet.GetEquippedArtifactSlot(i);
			text += $"{i + 1}: {(string.IsNullOrWhiteSpace(artifactName) ? "vazio" : artifactName)}\n";
		}

		return text;
	}
}
