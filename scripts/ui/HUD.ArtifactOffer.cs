using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class HUD
{
	public void OpenArtifactOffer(RoomArtifact source)
	{
		if (_artifactOfferOverlay == null)
			return;

		_pendingArtifactSource = source;
		BuildArtifactOfferChoices();
		_artifactOfferOverlay.Visible = true;
		GetTree().Paused = true;
		ControllerUiSupport.FocusFirst(_artifactOfferOverlay);
	}

	private void BuildArtifactOfferOverlay(Control root)
	{
		_artifactOfferOverlay = new ColorRect
		{
			Name = "ArtifactOfferOverlay",
			Color = new Color(0.015f, 0.0f, 0.0f, 0.93f),
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -620.0f,
			OffsetTop = -330.0f,
			OffsetRight = 620.0f,
			OffsetBottom = 330.0f,
			Visible = false,
			ProcessMode = ProcessModeEnum.Always
		};
		root.AddChild(_artifactOfferOverlay);

		var title = new Label
		{
			Text = "TESOURO DE ARTEFATOS",
			AnchorRight = 1.0f,
			OffsetTop = 18.0f,
			OffsetBottom = 56.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_artifactOfferOverlay.AddChild(title);

		_artifactOfferStatusLabel = new Label
		{
			Text = "Escolha um dos tres artefatos.",
			AnchorRight = 1.0f,
			OffsetTop = 58.0f,
			OffsetBottom = 92.0f,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_artifactOfferOverlay.AddChild(_artifactOfferStatusLabel);

		_artifactRerollButton = new Button
		{
			Text = "Trocar oferta",
			OffsetLeft = 980.0f, OffsetTop = 68.0f, OffsetRight = 1180.0f, OffsetBottom = 106.0f
		};
		_artifactRerollButton.Pressed += RerollArtifactOffer;
		_artifactOfferOverlay.AddChild(_artifactRerollButton);

		_artifactOfferList = new VBoxContainer
		{
			OffsetLeft = 56.0f,
			OffsetTop = 112.0f,
			OffsetRight = 1184.0f,
			OffsetBottom = 570.0f
		};
		_artifactOfferOverlay.AddChild(_artifactOfferList);
	}

	private void BuildArtifactOfferChoices()
	{
		ClearArtifactOfferList();

		var rng = new RandomNumberGenerator();
		rng.Randomize();
		// "As das Reliquias" (humanitas.collector_eye): chance de uma quarta opcao no altar.
		var extraChoiceChance = GetPlayerSkillController()?.ArtifactDropChanceBonus() ?? 0.0f;
		var choiceCount = rng.Randf() < extraChoiceChance ? 4 : 3;
		var choices = ArtifactCatalog.RollChoices(rng, choiceCount, BuildExcludedArtifactOfferIds());
		if (_artifactRerollButton != null)
			_artifactRerollButton.Disabled = !(GetPlayerSkillController()?.CanRerollReward() ?? false);

		if (choices.Count == 0)
		{
			_artifactOfferStatusLabel.Text = "Nenhum artefato disponivel no catalogo.";
			_artifactOfferOverlay.Visible = false;
			GetTree().Paused = false;
			GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
			return;
		}

		_artifactOfferStatusLabel.Text = "Escolha um artefato. Raridade e peso influenciam as opcoes que aparecem.";
		foreach (var artifact in choices)
			AddArtifactOfferButton(artifact);
	}

	private void RerollArtifactOffer()
	{
		if (GetPlayerSkillController()?.TryConsumeRewardReroll() != true) return;
		BuildArtifactOfferChoices();
		_artifactOfferStatusLabel.Text = "O Espelho do Destino reescreveu a oferta deste mapa.";
	}

	private void AddArtifactOfferButton(ArtifactDefinition artifact)
	{
		var button = new Button
		{
			Text = $"{artifact.DisplayName} | {artifact.Rarity} | {artifact.Slot}\n{artifact.Description}\nEfeitos: {FormatArtifactEffects(artifact)}",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(1080.0f, 112.0f)
		};
		button.Pressed += () => SelectArtifactOffer(artifact);
		_artifactOfferList.AddChild(button);
	}

	private HashSet<string> BuildExcludedArtifactOfferIds()
	{
		var excluded = new HashSet<string>();

		foreach (var artifact in RunState.RunArtifacts)
		{
			if (!string.IsNullOrWhiteSpace(artifact.Id))
				excluded.Add(artifact.Id);
		}

		foreach (var artifact in CharacterSheet.GetEquippedArtifacts())
		{
			if (!string.IsNullOrWhiteSpace(artifact.Id))
				excluded.Add(artifact.Id);
		}

		return excluded;
	}

	private void SelectArtifactOffer(ArtifactDefinition artifact)
	{
		var record = ArtifactCatalog.CreateRecord(artifact);

		CollectArtifact(record);
	}

	private void CollectArtifact(ArtifactRecord artifact)
	{
		RunState.AddTemporaryArtifact(artifact);
		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		NotifyArtifactCollected(artifact.Name);
		_pendingArtifactSource?.Consume();
		_pendingArtifactSource = null;
		_artifactOfferOverlay.Visible = false;
		GetTree().Paused = false;
		GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
		RefreshCharacterSheet();
	}

	private void ClearArtifactOfferList()
	{
		if (_artifactOfferList == null)
			return;

		foreach (var child in _artifactOfferList.GetChildren())
			child.QueueFree();
	}
}
