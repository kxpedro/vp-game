using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class HUD
{
	private void BuildArtifactChestOverlay(Control root)
	{
		_artifactChestOverlay = new ColorRect
		{
			Name = "ArtifactChestOverlay",
			Color = new Color(0.02f, 0.0f, 0.0f, 0.88f),
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -520.0f,
			OffsetTop = -300.0f,
			OffsetRight = 520.0f,
			OffsetBottom = 300.0f,
			Visible = false
		};
		root.AddChild(_artifactChestOverlay);

		var title = new Label
		{
			Text = "BAU DE ARTEFATOS",
			AnchorRight = 1.0f,
			OffsetTop = 18.0f,
			OffsetBottom = 56.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_artifactChestOverlay.AddChild(title);

		_artifactChestStatusLabel = new Label
		{
			Text = "Artefatos encontrados na run atual e na colecao permanente.",
			AnchorRight = 1.0f,
			OffsetTop = 58.0f,
			OffsetBottom = 92.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_artifactChestOverlay.AddChild(_artifactChestStatusLabel);

		var slotsTitle = new Label
		{
			Text = "Equipados para a proxima run",
			OffsetLeft = 40.0f,
			OffsetTop = 110.0f,
			OffsetRight = 420.0f,
			OffsetBottom = 140.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		slotsTitle.Visible = false;
		_artifactChestOverlay.AddChild(slotsTitle);

		var slotColumn = new VBoxContainer
		{
			OffsetLeft = 56.0f,
			OffsetTop = 150.0f,
			OffsetRight = 400.0f,
			OffsetBottom = 360.0f
		};
		slotColumn.Visible = false;
		_artifactChestOverlay.AddChild(slotColumn);

		for (var i = 0; i < _artifactSlotButtons.Length; i++)
		{
			var slotIndex = i;
			var button = new Button
			{
				Text = $"{slotIndex + 1}: vazio",
				CustomMinimumSize = new Vector2(320.0f, 42.0f)
			};
			button.Pressed += () => SelectArtifactSlot(slotIndex);
			_artifactSlotButtons[i] = button;
			slotColumn.AddChild(button);
		}

		var unequipButton = new Button
		{
			Text = "Remover do slot selecionado",
			CustomMinimumSize = new Vector2(320.0f, 40.0f)
		};
		unequipButton.Pressed += UnequipSelectedArtifactSlot;
		slotColumn.AddChild(unequipButton);

		var collectionTitle = new Label
		{
			Text = "Artefatos encontrados",
			OffsetLeft = 56.0f,
			OffsetTop = 110.0f,
			OffsetRight = 980.0f,
			OffsetBottom = 140.0f,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_artifactChestOverlay.AddChild(collectionTitle);

		var collectionScroll = new ScrollContainer
		{
			OffsetLeft = 56.0f,
			OffsetTop = 150.0f,
			OffsetRight = 984.0f,
			OffsetBottom = 470.0f
		};
		_artifactChestOverlay.AddChild(collectionScroll);

		_artifactCollectionList = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(900.0f, 0.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		collectionScroll.AddChild(_artifactCollectionList);

		var closeButton = new Button
		{
			Text = "Fechar",
			AnchorLeft = 0.5f,
			AnchorTop = 1.0f,
			AnchorRight = 0.5f,
			AnchorBottom = 1.0f,
			OffsetLeft = -90.0f,
			OffsetTop = -58.0f,
			OffsetRight = 90.0f,
			OffsetBottom = -18.0f
		};
		closeButton.Pressed += CloseArtifactChest;
		_artifactChestOverlay.AddChild(closeButton);
	}

	private void SelectArtifactSlot(int slotIndex)
	{
		_selectedArtifactSlot = slotIndex;
		RefreshArtifactChest();
	}

	private void EquipArtifactInSelectedSlot(string artifactName)
	{
		if (!CharacterSheet.EquipArtifact(_selectedArtifactSlot, artifactName))
		{
			SetArtifactChestStatus($"Nao foi possivel equipar {artifactName}.");
			return;
		}

		SetArtifactChestStatus($"{artifactName} equipado no slot {_selectedArtifactSlot + 1}.");
		RefreshArtifactChest();
		RefreshCharacterSheet();
	}

	private void UnequipSelectedArtifactSlot()
	{
		CharacterSheet.UnequipArtifact(_selectedArtifactSlot);
		SetArtifactChestStatus($"Slot {_selectedArtifactSlot + 1} vazio.");
		RefreshArtifactChest();
		RefreshCharacterSheet();
	}

	private void RefreshArtifactChest()
	{
		for (var i = 0; i < _artifactSlotButtons.Length; i++)
		{
			var button = _artifactSlotButtons[i];
			if (button == null)
				continue;

			var artifactName = CharacterSheet.GetEquippedArtifactSlot(i);
			button.Text = $"{i + 1}: {(string.IsNullOrWhiteSpace(artifactName) ? "vazio" : artifactName)}";
			button.Modulate = i == _selectedArtifactSlot ? new Color(1.0f, 0.35f, 0.32f) : Colors.White;
		}

		RefreshArtifactCollectionList();
	}

	private void RefreshArtifactCollectionList()
	{
		if (_artifactCollectionList == null)
			return;

		foreach (var child in _artifactCollectionList.GetChildren())
			child.QueueFree();

		if (RunState.RunArtifacts.Count == 0 && CharacterSheet.FoundArtifacts.Count == 0)
		{
			_artifactCollectionList.AddChild(new Label
			{
				Text = "Nenhum artefato encontrado nesta run ou na colecao permanente.",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(480.0f, 60.0f)
			});
			return;
		}

		var displayed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var artifact in RunState.RunArtifacts)
		{
			AddArtifactCollectionEntry(artifact, "RUN ATUAL");
			displayed.Add(ArtifactCollectionKey(artifact));
		}

		foreach (var artifact in CharacterSheet.FoundArtifacts)
			if (displayed.Add(ArtifactCollectionKey(artifact)))
				AddArtifactCollectionEntry(artifact, "COLECAO PERMANENTE", true);
	}

	private void AddArtifactCollectionEntry(ArtifactRecord artifact, string source, bool equippable = false)
	{
		var details = ArtifactCatalog.TryGet(artifact.Id, out var definition)
			? $"[{source}] {definition.DisplayName} | {definition.Rarity}\n{definition.Description}\nEfeitos: {FormatArtifactEffects(definition)}"
			: $"[{source}] {artifact.Name} | {artifact.Effect} {artifact.Amount:+0.##;-0.##;0}";

		// Artefatos da colecao permanente podem ser equipados no slot selecionado.
		if (!equippable)
		{
			_artifactCollectionList.AddChild(new Label
			{
				Text = details,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(880.0f, 82.0f)
			});
			return;
		}

		var button = new Button
		{
			Text = $"{details}\n> Equipar no slot {_selectedArtifactSlot + 1}",
			Alignment = HorizontalAlignment.Left,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(880.0f, 96.0f)
		};
		var artifactName = artifact.Name;
		button.Pressed += () => EquipArtifactInSelectedSlot(artifactName);
		_artifactCollectionList.AddChild(button);
	}

	private static string ArtifactCollectionKey(ArtifactRecord artifact)
	{
		return string.IsNullOrWhiteSpace(artifact.Id) ? artifact.Name : artifact.Id;
	}

	private void SetArtifactChestStatus(string text)
	{
		if (_artifactChestStatusLabel != null)
			_artifactChestStatusLabel.Text = text;
	}

	private void CloseArtifactChest()
	{
		if (_artifactChestOverlay != null)
			_artifactChestOverlay.Visible = false;
	}
}
