using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class HUD
{
	private void BuildCompendiumOverlay(Control root)
	{
		_compendiumOverlay = new ColorRect
		{
			Name = "CompendiumOverlay",
			Color = new Color(0.015f, 0.0f, 0.0f, 0.94f),
			AnchorLeft = 0.0f,
			AnchorTop = 0.0f,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			Visible = false,
			ProcessMode = ProcessModeEnum.Always
		};
		root.AddChild(_compendiumOverlay);

		var panel = new VBoxContainer
		{
			Name = "CompendiumPanel",
			AnchorLeft = 0.04f,
			AnchorTop = 0.05f,
			AnchorRight = 0.96f,
			AnchorBottom = 0.95f,
			Alignment = BoxContainer.AlignmentMode.Begin
		};
		_compendiumOverlay.AddChild(panel);

		var title = new Label
		{
			Name = "CompendiumTitle",
			Text = "DICIONARIO",
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(0.0f, 42.0f)
		};
		panel.AddChild(title);

		_compendiumStatusLabel = new Label
		{
			Name = "CompendiumStatus",
			Text = "Conteudo disponivel no jogo.",
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0.0f, 32.0f)
		};
		panel.AddChild(_compendiumStatusLabel);

		var tabRow = new HBoxContainer
		{
			Name = "CompendiumTabs",
			Alignment = BoxContainer.AlignmentMode.Center,
			CustomMinimumSize = new Vector2(0.0f, 52.0f)
		};
		panel.AddChild(tabRow);

		AddCompendiumTab(tabRow, "Artifacts", "Artefatos");
		AddCompendiumTab(tabRow, "Blessings", "Bênçãos");
		AddCompendiumTab(tabRow, "Skills", "Habilidades");
		AddCompendiumTab(tabRow, "Attacks", "Ataques");
		AddCompendiumTab(tabRow, "Enemies", "Inimigos");

		var toolRow = new HBoxContainer
		{
			Name = "CompendiumTools",
			Alignment = BoxContainer.AlignmentMode.Center,
			CustomMinimumSize = new Vector2(0.0f, 48.0f)
		};
		panel.AddChild(toolRow);

		var resetButton = new Button
		{
			Name = "ResetCompendiumValuesButton",
			Text = "Redefinir valores originais",
			CustomMinimumSize = new Vector2(280.0f, 40.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		resetButton.Pressed += ResetCompendiumValues;
		toolRow.AddChild(resetButton);

		var scroll = new ScrollContainer
		{
			Name = "CompendiumScroll",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 520.0f)
		};
		panel.AddChild(scroll);

		_compendiumList = new VBoxContainer
		{
			Name = "CompendiumList",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		scroll.AddChild(_compendiumList);

		var closeButton = new Button
		{
			Name = "CloseCompendiumButton",
			Text = "Fechar",
			CustomMinimumSize = new Vector2(220.0f, 44.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		closeButton.Pressed += CloseCompendium;
		panel.AddChild(closeButton);
	}

	private void AddCompendiumTab(HBoxContainer tabRow, string tabId, string label)
	{
		var button = new Button
		{
			Name = $"Compendium{tabId}Tab",
			Text = label,
			CustomMinimumSize = new Vector2(180.0f, 40.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
		};
		button.Pressed += () => SelectCompendiumTab(tabId);
		tabRow.AddChild(button);
		_compendiumTabButtons[tabId] = button;
	}

	private void OpenCompendium()
	{
		if (_compendiumOverlay == null)
			return;

		_compendiumOverlay.Visible = true;
		GetTree().Paused = true;
		RefreshCompendium();
		ControllerUiSupport.FocusFirst(_compendiumOverlay);
	}

	private void CloseCompendium()
	{
		if (_compendiumOverlay != null)
			_compendiumOverlay.Visible = false;
		RefreshModalPause();
	}

	private void SelectCompendiumTab(string tabId)
	{
		_selectedCompendiumTab = tabId;
		RefreshCompendium();
	}

	private void RefreshCompendium()
	{
		if (_compendiumList == null)
			return;

		foreach (var pair in _compendiumTabButtons)
			pair.Value.Modulate = pair.Key == _selectedCompendiumTab ? new Color(1.0f, 0.35f, 0.32f) : Colors.White;

		foreach (var child in _compendiumList.GetChildren())
			child.QueueFree();

		switch (_selectedCompendiumTab)
		{
			case "Skills":
				RefreshSkillCompendium();
				break;
			case "Attacks":
				RefreshAttackCompendium();
				break;
			case "Enemies":
				RefreshEnemyCompendium();
				break;
			case "Blessings":
				RefreshBlessingCompendium();
				break;
			default:
				RefreshArtifactCompendium();
				break;
		}
	}

	private void RefreshArtifactCompendium()
	{
		SetCompendiumStatus($"{ArtifactCatalog.All.Count} artefatos cadastrados em data/artifacts/artifacts.json.");

		foreach (var artifact in ArtifactCatalog.All)
		{
			var entry = AddCompendiumEntry(
				artifact.DisplayName,
				$"{artifact.Rarity} | {artifact.Slot} | peso {GetCompendiumNumber($"{artifact.Id}.weight", artifact.Weight):0.##} | {artifact.Id}",
				$"{artifact.Description}\n{artifact.FlavorText}\nEfeitos: {FormatArtifactEffects(artifact)}\nTags: {FormatTags(artifact.Tags)}",
				artifact.IconPath
			);
			AddCompendiumNumberEditor(entry, "Peso de drop", $"{artifact.Id}.weight", artifact.Weight);

			for (var i = 0; i < artifact.Effects.Length; i++)
			{
				var effect = artifact.Effects[i];
				AddCompendiumNumberEditor(entry, $"{effect.Type} ({effect.Operation})", $"{artifact.Id}.effect.{i}.value", effect.Value, 0.01, 999.0);
			}
		}
	}

	private void RefreshBlessingCompendium()
	{
		SetCompendiumStatus($"{BloodBlessingCatalog.All.Count} bencaos em {BloodBlessingCatalog.AllFamilies.Count} linhagens. Stirps possui raridade propria e exige pre-condicoes.");
		var rarityText = "Raridades: " + string.Join(" | ", BloodBlessingCatalog.AllRarities.Select(r => $"{r.DisplayName} x{r.PowerMultiplier:0.##}"));
		foreach (var blessing in BloodBlessingCatalog.All)
		{
			var isStirps = blessing.Rarity.Equals("Stirps", StringComparison.OrdinalIgnoreCase);
			AddCompendiumEntry(
				blessing.DisplayName,
				$"{FormatBlessingLineage(blessing)} | {(isStirps ? "Raridade Stirps" : "Todas as raridades")} | {blessing.Id}",
				$"{blessing.Description}\n{blessing.FlavorText}\n{(isStirps ? "Poder fixo Stirps" : "Base Common")}: {FormatBloodBlessingEffects(blessing)}\n{(isStirps ? "Desbloqueada apenas quando suas condicoes forem atendidas." : rarityText)}",
				blessing.IconPath
			);
		}
	}

	private void RefreshSkillCompendium()
	{
		var skills = new List<SkillDefinition>(SkillCatalog.All);
		SetCompendiumStatus($"{skills.Count} habilidades cadastradas no SkillCatalog.");

		foreach (var skill in skills)
		{
			var cost = GetCompendiumNumber($"{skill.Id}.cost", skill.Cost);
			var maxRank = GetCompendiumNumber($"{skill.Id}.max_rank", skill.MaxRank);
			var entry = AddCompendiumEntry(
				skill.DisplayName,
				$"{skill.Tree} | Passiva | custo {cost:0} | {skill.Id}",
				$"{skill.Description}\nRank maximo: {maxRank:0}\nPre-requisitos: {FormatPrerequisites(skill)}",
				"res://images/generic.png"
			);
			AddCompendiumNumberEditor(entry, "Custo", $"{skill.Id}.cost", skill.Cost);
			AddCompendiumNumberEditor(entry, "Rank maximo", $"{skill.Id}.max_rank", skill.MaxRank);
		}
	}

	private void RefreshEnemyCompendium()
	{
		SetCompendiumStatus("Inimigos cadastrados a partir dos scripts e cenas atuais.");

		var basic = AddCompendiumEntry(
			"Inimigo Basico",
			"EnemyBasic.tscn | patrulha, detecta, persegue e ataca em area travada",
			"Valores atuais das salas comuns. A dificuldade ainda escala por sala no RunManager.",
			"res://images/generic.png"
		);
		AddCompendiumNumberEditor(basic, "Vida base", "enemy.basic.health", 80.0);
		AddCompendiumNumberEditor(basic, "Dano base", "enemy.basic.damage", 18.0);
		AddCompendiumNumberEditor(basic, "Velocidade", "enemy.basic.move_speed", 3.2, 0.1, 100.0);
		AddCompendiumNumberEditor(basic, "Recompensa minima", "enemy.basic.min_blood_coins", 1.0);
		AddCompendiumNumberEditor(basic, "Recompensa maxima", "enemy.basic.max_blood_coins", 10.0);
		AddCompendiumNumberEditor(basic, "Furia ao morrer", "enemy.basic.fury_on_death", 12.0);

		var boss = AddCompendiumEntry(
			"Chefes (base)",
			"Dano, velocidade e alcance base aplicados aos chefes no RunManager. A vida vem do BossCatalog.",
			"Ao vencer, os artefatos temporarios sao confirmados.",
			"res://images/generic.png"
		);
		AddCompendiumNumberEditor(boss, "Dano", "enemy.blood_mage.damage", 30.0);
		AddCompendiumNumberEditor(boss, "Velocidade", "enemy.blood_mage.move_speed", 2.7, 0.1, 100.0);
		AddCompendiumNumberEditor(boss, "Alcance", "enemy.blood_mage.attack_range", 2.25, 0.05, 100.0);
		AddCompendiumNumberEditor(boss, "Recompensa minima", "enemy.blood_mage.min_blood_coins", 8.0);
		AddCompendiumNumberEditor(boss, "Recompensa maxima", "enemy.blood_mage.max_blood_coins", 10.0);
	}

	private void RefreshAttackCompendium()
	{
		SetCompendiumStatus("Ataques atuais do jogador definidos em PlayerCombat.");

		var lightOne = AddCompendiumEntry(
			"Ataque leve I",
			"Mouse esquerdo | combo de espada longa",
			"Primeiro golpe do combo leve.",
			"res://images/generic.png"
		);
		AddAttackEditors(lightOne, "attack.light_1", 32.0, 0.68, 0.32, 1.55, 1.65, 4.2);

		var lightTwo = AddCompendiumEntry(
			"Ataque leve II",
			"Mouse esquerdo em sequencia | combo de espada longa",
			"Segundo golpe do combo leve.",
			"res://images/generic.png"
		);
		AddAttackEditors(lightTwo, "attack.light_2", 38.0, 0.70, 0.30, 1.75, 1.75, 4.8);

		var lightThree = AddCompendiumEntry(
			"Ataque leve III",
			"Mouse esquerdo em sequencia | finalizador do combo",
			"Finalizador do combo leve.",
			"res://images/generic.png"
		);
		AddAttackEditors(lightThree, "attack.light_3", 52.0, 0.72, 0.28, 2.05, 1.95, 6.4);

		var special = AddCompendiumEntry(
			"Ataque especial",
			"Q | ataque especial da arma equipada",
			"Interrompe imediatamente o combo basico atual.",
			"res://images/generic.png"
		);
		AddAttackEditors(special, "attack.special", 56.0, 0.28, 0.42, 3.15, 3.15, 11.0);

		var dash = AddCompendiumEntry(
			"Ataque rápido",
			"Dash + ataque imediato | golpe rápido ao finalizar o dash",
			"Cancela o ataque padrao, preserva invencibilidade e executa dano em circulo ao redor do personagem.",
			"res://images/generic.png"
		);
		AddAttackEditors(dash, "attack.dash", 68.0, 0.12, 0.18, 2.25, 2.05, 7.4);
		AddCompendiumNumberEditor(dash, "Multiplicador de dano", "attack.dash.damage_multiplier", 1.35, 0.05, 20.0);

		var fury = AddCompendiumEntry(
			"Furia Bestial",
			"F | requer uma Bencao de Sangue que desperte a Furia",
			"Consome a barra cheia e ativa temporariamente o estado de dano aumentado.",
			"res://images/generic.png"
		);
		AddCompendiumNumberEditor(fury, "Multiplicador de dano", "attack.fury.damage_multiplier", 1.75, 0.05, 20.0);
		AddCompendiumNumberEditor(fury, "Consumo por segundo", "attack.fury.drain_per_second", 10.0, 0.1, 999.0);
		AddCompendiumNumberEditor(fury, "Furia maxima", "attack.fury.max_fury", 100.0);
	}

	private void AddAttackEditors(VBoxContainer entry, string keyPrefix, double damage, double hitDelay, double recovery, double range, double width, double knockback)
	{
		AddCompendiumNumberEditor(entry, "Dano", $"{keyPrefix}.damage", damage, 1.0, 9999.0);
		AddCompendiumNumberEditor(entry, "Recuperacao", $"{keyPrefix}.recovery", recovery, 0.01, 1.0);
		AddCompendiumNumberEditor(entry, "Alcance", $"{keyPrefix}.range", range, 0.05, 100.0);
		AddCompendiumNumberEditor(entry, "Largura", $"{keyPrefix}.width", width, 0.05, 100.0);
		AddCompendiumNumberEditor(entry, "Knockback", $"{keyPrefix}.knockback", knockback, 0.1, 999.0);
	}

	private VBoxContainer AddCompendiumEntry(string title, string subtitle, string body, string iconPath)
	{
		var card = new PanelContainer
		{
			Name = $"{title.Replace(" ", "")}Entry",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 96.0f)
		};
		_compendiumList.AddChild(card);

		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		card.AddChild(row);

		var icon = new TextureRect
		{
			CustomMinimumSize = new Vector2(48.0f, 48.0f),
			Size = new Vector2(48.0f, 48.0f),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
		};
		if (!string.IsNullOrWhiteSpace(iconPath))
			icon.Texture = GD.Load<Texture2D>(iconPath);
		row.AddChild(icon);

		var textColumn = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		row.AddChild(textColumn);

		textColumn.AddChild(new Label
		{
			Text = title,
			CustomMinimumSize = new Vector2(0.0f, 28.0f)
		});

		textColumn.AddChild(new Label
		{
			Text = subtitle,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0.0f, 28.0f)
		});

		textColumn.AddChild(new Label
		{
			Text = body,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		});

		return textColumn;
	}

	private void AddCompendiumNumberEditor(VBoxContainer parent, string label, string key, double originalValue, double step = 1.0, double maxValue = 9999.0)
	{
		var row = new HBoxContainer
		{
			Name = $"{key.Replace(".", "")}Editor",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 34.0f)
		};
		parent.AddChild(row);

		row.AddChild(new Label
		{
			Text = label,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(220.0f, 0.0f)
		});

		var valueInput = new SpinBox
		{
			MinValue = 0.0,
			MaxValue = maxValue,
			Step = step,
			Value = GetCompendiumNumber(key, originalValue),
			CustomMinimumSize = new Vector2(120.0f, 32.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd
		};
		valueInput.ValueChanged += value =>
		{
			CompendiumTuning.SetNumber(key, value);
			RunState.RecalculateArtifactBonuses();
			GetTree().CallGroup("player", "ApplyRuntimeTuning");
			RefreshCharacterSheet();
			SetCompendiumStatus($"Valor customizado: {label} = {value:0.##}");
		};
		row.AddChild(valueInput);
	}

	private double GetCompendiumNumber(string key, double originalValue)
	{
		return CompendiumTuning.GetNumber(key, originalValue);
	}

	private void ResetCompendiumValues()
	{
		CompendiumTuning.Reset();
		RunState.RecalculateArtifactBonuses();
		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		SetCompendiumStatus("Valores originais restaurados.");
		RefreshCompendium();
	}

	private string FormatArtifactEffects(ArtifactDefinition artifact)
	{
		if (artifact.Effects.Length == 0)
			return "nenhum";

		var text = "";
		for (var i = 0; i < artifact.Effects.Length; i++)
		{
			var effect = artifact.Effects[i];
			var value = GetCompendiumNumber($"{artifact.Id}.effect.{i}.value", effect.Value) * ArtifactCatalog.BossDropPowerMultiplier;
			text += $"{ArtifactEffectRegistry.FriendlyName(effect.Type)}: {FormatArtifactOperation(effect.Operation, value)}";

			if (i < artifact.Effects.Length - 1)
				text += ", ";
		}

		return text;
	}

	private static string FormatArtifactOperation(string operation, double value)
	{
		if (operation.Contains("Multiplier")) return $"{value * 100:+0;-0;0}%";
		if (operation is "Set" or "SetBool") return value > 0 ? "ativo" : "inativo";
		return $"{value:+0.##;-0.##;0}";
	}

	private string FormatTags(string[] tags)
	{
		return tags.Length == 0 ? "nenhuma" : string.Join(", ", tags);
	}

	private void SetCompendiumStatus(string text)
	{
		if (_compendiumStatusLabel != null)
			_compendiumStatusLabel.Text = text;
	}
}
