using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class HUD
{
	private void BuildBloodFountainOverlay(Control root)
	{
		_bloodFountainOverlay = new ColorRect
		{
			Name = "BloodFountainOverlay",
			Color = new Color(0.02f, 0.0f, 0.0f, 0.88f),
			AnchorLeft = 0.0f,
			AnchorTop = 0.0f,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = 0.0f,
			OffsetTop = 0.0f,
			OffsetRight = 0.0f,
			OffsetBottom = 0.0f,
			Visible = false
		};
		root.AddChild(_bloodFountainOverlay);

		var title = new Label
		{
			Name = "BloodFountainTitle",
			Text = "FONTE DE SANGUE",
			AnchorLeft = 0.0f,
			AnchorRight = 1.0f,
			OffsetTop = 22.0f,
			OffsetBottom = 62.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_bloodFountainOverlay.AddChild(title);

		_bloodFountainStatusLabel = new Label
		{
			Name = "BloodFountainStatus",
			AnchorLeft = 0.0f,
			AnchorRight = 1.0f,
			OffsetTop = 66.0f,
			OffsetBottom = 98.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_bloodFountainOverlay.AddChild(_bloodFountainStatusLabel);

		_bloodCoinCheatButton = new Button
		{
			Name = "BloodCoinCheatButton",
			Text = "Cheat +500 moedas",
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			OffsetLeft = -100.0f,
			OffsetTop = 102.0f,
			OffsetRight = 100.0f,
			OffsetBottom = 138.0f,
			// Ferramenta de teste: fora do build de debug ela permitiria farmar moedas.
			Visible = OS.IsDebugBuild()
		};
		_bloodCoinCheatButton.Pressed += AddCheatBloodCoins;
		_bloodFountainOverlay.AddChild(_bloodCoinCheatButton);

		_skillTreeCanvas = new Control
		{
			Name = "SkillTreeCanvas",
			AnchorLeft = 0.0f,
			AnchorTop = 0.0f,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = 0.0f,
			OffsetTop = 0.0f,
			OffsetRight = 0.0f,
			OffsetBottom = 0.0f,
			PivotOffset = Vector2.Zero
		};
		_skillTreeCanvas.MouseFilter = Control.MouseFilterEnum.Ignore;
		_bloodFountainOverlay.AddChild(_skillTreeCanvas);

		var viewportWidth = Mathf.Max(GetViewport().GetVisibleRect().Size.X, 1152.0f);
		var leftBoard = Mathf.Max(80.0f, viewportWidth * 0.12f);
		var rightBoard = Mathf.Max(leftBoard + 500.0f, viewportWidth * 0.56f);
		BuildSkillTreeBoard("Bestialis", SkillTree.Bestialis, leftBoard);
		BuildSkillTreeBoard("Humanitas", SkillTree.Humanitas, rightBoard);

		BuildSkillDescriptionPopup();

		var closeButton = new Button
		{
			Name = "CloseBloodFountainButton",
			Text = "Fechar",
			AnchorLeft = 1.0f,
			AnchorTop = 0.0f,
			AnchorRight = 1.0f,
			AnchorBottom = 0.0f,
			OffsetLeft = -154.0f,
			OffsetTop = 22.0f,
			OffsetRight = -24.0f,
			OffsetBottom = 62.0f
		};
		closeButton.Pressed += CloseBloodFountain;
		_bloodFountainOverlay.AddChild(closeButton);

		_bloodFountainOverlay.MoveChild(_bloodCoinCheatButton, _bloodFountainOverlay.GetChildCount() - 1);
		_bloodFountainOverlay.MoveChild(closeButton, _bloodFountainOverlay.GetChildCount() - 1);
	}

	private void BuildSkillTreeBoard(string treeName, SkillTree tree, float offsetLeft)
	{
		var title = new Label
		{
			Name = $"{treeName}TreeTitle",
			Text = treeName,
			HorizontalAlignment = HorizontalAlignment.Center,
			OffsetLeft = offsetLeft,
			OffsetTop = 112.0f,
			OffsetRight = offsetLeft + 372.0f,
			OffsetBottom = 146.0f
		};
		_skillTreeCanvas.AddChild(title);

		var skills = new List<SkillDefinition>(SkillCatalog.GetTree(tree));
		for (var i = 0; i < skills.Count; i++)
		{
			var definition = skills[i];
			var nodePosition = GetSkillNodePosition(offsetLeft, definition);
			CreateSkillNode(definition, nodePosition);

			foreach (var prerequisiteId in definition.Prerequisites)
			{
				if (!SkillCatalog.TryGet(prerequisiteId, out var prerequisite))
					continue;

				if (prerequisite.Tree != tree)
					continue;

				CreateSkillLink(GetSkillNodePosition(offsetLeft, prerequisite), nodePosition);
			}
		}
	}

	private Vector2 GetSkillNodePosition(float treeOffsetLeft, SkillDefinition definition)
	{
		var x = treeOffsetLeft + 20.0f + definition.GridColumn * 130.0f;
		var y = 170.0f + definition.GridRow * 112.0f;
		return new Vector2(x, y);
	}

	private void CreateSkillNode(SkillDefinition definition, Vector2 position)
	{
		var button = new Button
		{
			Name = $"{definition.Id.Replace(".", "_")}Node",
			Text = definition.IconText,
			OffsetLeft = position.X,
			OffsetTop = position.Y,
			OffsetRight = position.X + 72.0f,
			OffsetBottom = position.Y + 72.0f,
			TooltipText = $"{definition.DisplayName} - clique direito para detalhes" +
				(definition.IsImplemented ? "" : "\nEm planejamento; compra indisponível.")
		};
		button.Pressed += () => BuySkill(definition.Id);
		button.GuiInput += @event =>
		{
			if (@event is not InputEventMouseButton mouse || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Right)
				return;

			ShowSkillDescription(definition);
			button.AcceptEvent();
		};
		_skillTreeCanvas.AddChild(button);
		_skillTreeButtons[definition.Id] = button;

		var label = new Label
		{
			Name = $"{definition.Id.Replace(".", "_")}Label",
			Text = definition.DisplayName,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			OffsetLeft = position.X - 56.0f,
			OffsetTop = position.Y + 76.0f,
			OffsetRight = position.X + 128.0f,
			OffsetBottom = position.Y + 122.0f
		};
		_skillTreeCanvas.AddChild(label);
	}

	private void CreateSkillLink(Vector2 from, Vector2 to)
	{
		var start = from + new Vector2(36.0f, 36.0f);
		var end = to + new Vector2(36.0f, 36.0f);
		var corner = new Vector2(end.X, start.Y);
		CreateSkillLinkSegment(start, corner);
		CreateSkillLinkSegment(corner, end);
	}

	private void CreateSkillLinkSegment(Vector2 from, Vector2 to)
	{
		var horizontal = Mathf.Abs(to.X - from.X) >= Mathf.Abs(to.Y - from.Y);
		var link = new ColorRect { Name = "SkillLink", Color = new Color(0.55f, 0.08f, 0.08f, 0.8f) };
		if (horizontal)
		{
			link.OffsetLeft = Mathf.Min(from.X, to.X);
			link.OffsetTop = from.Y - 2.0f;
			link.OffsetRight = Mathf.Max(from.X, to.X) + 4.0f;
			link.OffsetBottom = from.Y + 2.0f;
		}
		else
		{
			link.OffsetLeft = from.X - 2.0f;
			link.OffsetTop = Mathf.Min(from.Y, to.Y);
			link.OffsetRight = from.X + 2.0f;
			link.OffsetBottom = Mathf.Max(from.Y, to.Y) + 4.0f;
		}
		_skillTreeCanvas.AddChild(link);
	}

	private void BuySkill(string skillId)
	{
		if (!SkillCatalog.TryGet(skillId, out var definition))
		{
			SetBloodFountainStatus("Habilidade desconhecida.");
			return;
		}

		if (!definition.IsImplemented)
		{
			SetBloodFountainStatus($"{definition.DisplayName} ainda está em planejamento.");
			return;
		}

		var currentRank = CharacterSheet.GetSkillRank(skillId);
		var maxRank = GetSkillMaxRank(definition);
		if (currentRank >= maxRank)
		{
			SetBloodFountainStatus($"{definition.DisplayName} ja esta no nivel maximo ({maxRank}).");
			return;
		}

		if (!SkillCatalog.HasPrerequisites(definition))
		{
			SetBloodFountainStatus($"{definition.DisplayName} ainda esta bloqueada.");
			return;
		}

		if (SkillCatalog.HasExclusiveConflict(definition))
		{
			SetBloodFountainStatus($"{definition.DisplayName} conflita com outra escolha já adquirida.");
			return;
		}

		var cost = GetSkillUpgradeCost(definition);
		if (!CharacterSheet.SpendBloodCoins(cost))
		{
			SetBloodFountainStatus($"Moedas insuficientes para {definition.DisplayName}.");
			return;
		}

		if (!CharacterSheet.AddSkill(skillId))
		{
			CharacterSheet.AddBloodCoins(cost);
			SetBloodFountainStatus($"Nao foi possivel adquirir {definition.DisplayName}.");
			return;
		}

		_coinsLabel.Text = $"Moedas de Sangue: {CharacterSheet.BloodCoins}";
		RefreshCharacterSheet();
		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		RefreshBloodFountain();
		SetBloodFountainStatus($"{definition.DisplayName} aprimorada para o nivel {currentRank + 1}/{maxRank}.");
	}

	private static int GetSkillMaxRank(SkillDefinition definition)
	{
		return Mathf.Max(CompendiumTuning.GetInt($"{definition.Id}.max_rank", definition.MaxRank), 1);
	}

	private static int GetSkillUpgradeCost(SkillDefinition definition)
	{
		var baseCost = CompendiumTuning.GetInt($"{definition.Id}.cost", definition.Cost);
		var currentRank = CharacterSheet.GetSkillRank(definition.Id);
		return Mathf.RoundToInt(baseCost * (1.0f + currentRank * 0.5f));
	}

	private void AddCheatBloodCoins()
	{
		CharacterSheet.AddBloodCoins(500);
		OnBloodCoinsChanged(CharacterSheet.BloodCoins);
		SetBloodFountainStatus("+500 moedas adicionadas para teste.");
	}

	private void RefreshBloodFountain()
	{
		if (_bloodFountainStatusLabel == null)
			return;

		_bloodFountainStatusLabel.Text = $"Moedas de Sangue: {CharacterSheet.BloodCoins}";
		RefreshSkillTreeButtons();
	}

	private void RefreshSkillTreeButtons()
	{
		foreach (var pair in _skillTreeButtons)
		{
			if (!SkillCatalog.TryGet(pair.Key, out var definition))
				continue;

			var button = pair.Value;
			var currentRank = CharacterSheet.GetSkillRank(definition.Id);
			var maxRank = GetSkillMaxRank(definition);
			var hasSkill = currentRank > 0;
			var isMaxRank = currentRank >= maxRank;
			var unlocked = SkillCatalog.HasPrerequisites(definition);
			var exclusiveConflict = SkillCatalog.HasExclusiveConflict(definition);
			var cost = GetSkillUpgradeCost(definition);
			var canAfford = CharacterSheet.BloodCoins >= cost;

			button.Disabled = false;
			button.Text = $"{definition.IconText}\n{currentRank}/{maxRank}";

			if (!definition.IsImplemented)
				button.Modulate = new Color(0.48f, 0.34f, 0.58f);
			else if (isMaxRank)
				button.Modulate = new Color(0.75f, 1.0f, 0.78f);
			else if (hasSkill && canAfford)
				button.Modulate = new Color(0.9f, 0.72f, 1.0f);
			else if (exclusiveConflict)
				button.Modulate = new Color(0.32f, 0.28f, 0.34f);
			else if (!unlocked)
				button.Modulate = new Color(0.42f, 0.42f, 0.46f);
			else if (canAfford)
				button.Modulate = new Color(1.0f, 0.82f, 0.78f);
			else
				button.Modulate = new Color(0.78f, 0.58f, 0.56f);
		}
	}

	private void SetBloodFountainStatus(string text)
	{
		if (_bloodFountainStatusLabel != null)
			_bloodFountainStatusLabel.Text = $"{text}\nMoedas de Sangue: {CharacterSheet.BloodCoins}";
	}

	private void AdjustBloodFountainZoom(float delta)
	{
		_bloodFountainZoom = Mathf.Clamp(_bloodFountainZoom + delta, 0.65f, 1.65f);

		ApplySkillTreeTransform();

		if (_bloodFountainStatusLabel != null)
			_bloodFountainStatusLabel.Text = $"Moedas de Sangue: {CharacterSheet.BloodCoins} | Zoom {_bloodFountainZoom:0.00}x";
	}

	private void PanBloodFountain(Vector2 delta)
	{
		_skillTreePan += delta;
		_skillTreePan.X = Mathf.Clamp(_skillTreePan.X, -700.0f, 700.0f);
		_skillTreePan.Y = Mathf.Clamp(_skillTreePan.Y, -420.0f, 420.0f);
		ApplySkillTreeTransform();
	}

	private void ApplySkillTreeTransform()
	{
		if (_skillTreeCanvas == null)
			return;

		_skillTreeCanvas.Scale = Vector2.One * _bloodFountainZoom;
		_skillTreeCanvas.Position = _skillTreePan;
	}

	private void BuildSkillDescriptionPopup()
	{
		_skillDescriptionPopup = new ColorRect
		{
			Name = "SkillDescriptionPopup",
			Color = new Color(0.015f, 0.0f, 0.0f, 0.94f),
			AnchorLeft = 0.22f,
			AnchorTop = 0.14f,
			AnchorRight = 0.78f,
			AnchorBottom = 0.86f,
			OffsetLeft = 0.0f,
			OffsetTop = 0.0f,
			OffsetRight = 0.0f,
			OffsetBottom = 0.0f,
			Visible = false
		};
		_bloodFountainOverlay.AddChild(_skillDescriptionPopup);

		var descriptionScroll = new ScrollContainer
		{
			Name = "SkillDescriptionScroll",
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = 24.0f,
			OffsetTop = 22.0f,
			OffsetRight = -24.0f,
			OffsetBottom = -88.0f,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
		};
		_skillDescriptionPopup.AddChild(descriptionScroll);

		_skillDescriptionLabel = new Label
		{
			Name = "SkillDescriptionText",
			CustomMinimumSize = new Vector2(360.0f, 0.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		descriptionScroll.AddChild(_skillDescriptionLabel);

		var closeButton = new Button
		{
			Name = "CloseSkillDescription",
			Text = "Fechar",
			AnchorLeft = 0.5f,
			AnchorTop = 1.0f,
			AnchorRight = 0.5f,
			AnchorBottom = 1.0f,
			OffsetLeft = -76.0f,
			OffsetTop = -66.0f,
			OffsetRight = 76.0f,
			OffsetBottom = -22.0f
		};
		closeButton.Pressed += () => _skillDescriptionPopup.Visible = false;
		_skillDescriptionPopup.AddChild(closeButton);
	}

	private void ShowSkillDescription(SkillDefinition definition)
	{
		if (_skillDescriptionPopup == null || _skillDescriptionLabel == null)
			return;

		var currentRank = CharacterSheet.GetSkillRank(definition.Id);
		var maxRank = GetSkillMaxRank(definition);
		var hasSkill = currentRank > 0;
		var status = !definition.IsImplemented
			? hasSkill
				? "Adquirida em save anterior - efeito planejado"
				: "Planejada - compra indisponível"
			: currentRank >= maxRank
				? "Nivel maximo"
				: hasSkill
					? "Disponivel para aprimorar"
				: SkillCatalog.HasExclusiveConflict(definition)
					? "Bloqueada por escolha exclusiva"
					: SkillCatalog.HasPrerequisites(definition)
						? "Disponível"
						: "Bloqueada";

		_skillDescriptionLabel.Text =
			$"{definition.DisplayName}\n" +
			$"{definition.Tree} - Passiva - Tier {definition.Tier}\n\n" +
			$"{definition.Description}\n\n{definition.FlavorText}\n\n" +
			$"Nivel: {currentRank}/{maxRank}\n" +
			$"Custo do proximo nivel: {(currentRank >= maxRank ? 0 : GetSkillUpgradeCost(definition))} moedas\n" +
			$"Estado: {status}\n" +
			$"Pre-requisitos: {FormatPrerequisites(definition)}";
		_skillDescriptionPopup.Visible = true;
	}

	private string FormatPrerequisites(SkillDefinition definition)
	{
		if (definition.Prerequisites.Length == 0)
			return "nenhum";

		var text = "";
		for (var i = 0; i < definition.Prerequisites.Length; i++)
		{
			if (SkillCatalog.TryGet(definition.Prerequisites[i], out var prerequisite))
				text += prerequisite.DisplayName;
			else
				text += definition.Prerequisites[i];

			if (i < definition.Prerequisites.Length - 1)
				text += ", ";
		}

		return text;
	}

	private void CloseBloodFountain()
	{
		if (_bloodFountainOverlay != null)
			_bloodFountainOverlay.Visible = false;
	}
}
