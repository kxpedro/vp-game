using Godot;
using System;
using System.Collections.Generic;

// Baú de artefatos (docs/design/ui-guia.md §8.15): slots equipados à esquerda (estado "selecionado"), coleção à direita
// e painel de detalhes embaixo (segue foco e hover). Por controle: o foco começa no slot 1; ✕ num slot seleciona e leva
// o foco à coleção; ✕ num artefato da coleção permanente equipa no slot selecionado e devolve o foco ao slot.
// Não pausa (§5.1 regra 2): entra na UiModalStack só para bloquear o input do jogo; Esc/B fecha pela pilha.
public partial class HUD
{
	private const float ArtifactSlotColumnWidth = 260.0f;

	private sealed class ArtifactChestEntry
	{
		public ArtifactRecord Record;
		public ArtifactDefinition Definition;
		public bool FromRun;
	}

	private UiWindow _artifactChestWindow;
	private VBoxContainer _artifactCollectionList;
	private readonly Button[] _artifactSlotButtons = new Button[CharacterSheet.EquippedArtifactSlotCount];
	private Button _artifactUnequipButton;
	private UiDetailPanel _artifactChestDetail;
	private readonly List<Button> _artifactCollectionButtons = new();
	private readonly List<ArtifactChestEntry> _artifactCollectionEntries = new();
	private int _selectedArtifactSlot;
	// Preview (--ui-preview=artifact_chest): dados fictícios em memória; equipar/remover só simulam, nada é gravado.
	private bool _artifactChestPreviewMode;
	private readonly List<ArtifactRecord> _artifactChestPreviewRun = new();
	private readonly List<ArtifactRecord> _artifactChestPreviewFound = new();
	private readonly string[] _artifactChestPreviewSlots = new string[CharacterSheet.EquippedArtifactSlotCount];

	public void OpenArtifactChest()
	{
		if (_artifactChestOverlay == null)
			return;

		// Já aberto (interact relido pelo objeto 3D): não rouba o foco nem reinicia a seleção.
		if (UiModalStack.IsOpen(_artifactChestOverlay))
			return;

		_artifactChestPreviewMode = false;
		ShowArtifactChest();
	}

	private void ShowArtifactChest()
	{
		_selectedArtifactSlot = 0;
		SetArtifactChestStatus(null);
		RefreshArtifactChest();
		UiModalStack.Push(_artifactChestOverlay, false, _artifactSlotButtons[0], CloseArtifactChest);
		_artifactChestWindow.FadeIn();
		ShowArtifactSlotDetail(_selectedArtifactSlot);
	}

	private void BuildArtifactChestOverlay(Control root)
	{
		_artifactChestWindow = UiComponents.Window(root, "ArtifactChestOverlay", WindowSize.Large,
			Localization.Text("chest.title"));
		_artifactChestOverlay = _artifactChestWindow.Root;
		_artifactChestOverlay.Visible = false;

		var columns = new HBoxContainer { Name = "ArtifactChestColumns", SizeFlagsVertical = Control.SizeFlags.ExpandFill };
		columns.AddThemeConstantOverride("separation", UiMetrics.S5);
		_artifactChestWindow.Body.AddChild(columns);

		// Coluna esquerda: slots equipados (cards pequenos com estado selecionado) + remover.
		var slotColumn = new VBoxContainer { Name = "ArtifactSlots", CustomMinimumSize = new Vector2(ArtifactSlotColumnWidth, 0.0f) };
		slotColumn.AddThemeConstantOverride("separation", UiMetrics.S2);
		columns.AddChild(slotColumn);
		slotColumn.AddChild(UiComponents.MakeLabel(Localization.Text("chest.equipped"), UiVariation.SectionGoldLabel));

		for (var i = 0; i < _artifactSlotButtons.Length; i++)
		{
			var slotIndex = i;
			var button = new Button
			{
				Name = $"ArtifactSlot{slotIndex + 1}",
				ThemeTypeVariation = UiVariation.SelectButton,
				ToggleMode = true,
				CustomMinimumSize = new Vector2(ArtifactSlotColumnWidth, 72.0f),
				FocusMode = Control.FocusModeEnum.All,
				MouseDefaultCursorShape = Control.CursorShape.PointingHand
			};
			button.Pressed += () => OnArtifactSlotPressed(slotIndex);
			button.FocusEntered += () => ShowArtifactSlotDetail(slotIndex);
			button.MouseEntered += () => ShowArtifactSlotDetail(slotIndex);
			_artifactSlotButtons[i] = button;
			slotColumn.AddChild(button);
		}

		_artifactUnequipButton = UiComponents.MenuButton(Localization.Text("chest.unequip"),
			UnequipSelectedArtifactSlot, ButtonVariant.Secondary, ButtonSize.Small);
		_artifactUnequipButton.Name = "ArtifactUnequipButton";
		_artifactUnequipButton.CustomMinimumSize = new Vector2(ArtifactSlotColumnWidth, UiMetrics.ButtonS);
		_artifactUnequipButton.FocusEntered += () => ShowArtifactSlotDetail(_selectedArtifactSlot);
		slotColumn.AddChild(_artifactUnequipButton);

		// Coluna direita: coleção (run atual + coleção permanente), rolável.
		var collectionColumn = new VBoxContainer
		{
			Name = "ArtifactCollection",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		collectionColumn.AddThemeConstantOverride("separation", UiMetrics.S2);
		columns.AddChild(collectionColumn);
		collectionColumn.AddChild(UiComponents.MakeLabel(Localization.Text("chest.collection"), UiVariation.SectionGoldLabel));
		var scroll = UiComponents.ScrollList(0.0f, out _artifactCollectionList);
		scroll.Name = "ArtifactCollectionScroll";
		_artifactCollectionList.AddThemeConstantOverride("separation", UiMetrics.S2);
		collectionColumn.AddChild(scroll);

		_artifactChestDetail = UiComponents.DetailPanel(DetailPanelDock.Bottom);
		_artifactChestDetail.Root.Name = "ArtifactChestDetail";
		// Cabeçalho mais largo que os 360 padrão: nomes como "Prego do Caixão do Vampiro" cabem em 1 linha.
		_artifactChestDetail.Header.CustomMinimumSize = new Vector2(460.0f, 0.0f);
		_artifactChestWindow.Body.AddChild(_artifactChestDetail.Root);

		var footer = _artifactChestWindow.Footer;
		footer.Add(UiAction.Cancel, Localization.Text("ui.common.close"), CloseArtifactChest, left: true);
		footer.Add(UiAction.Accept, Localization.Text("chest.hint_accept"));
	}

	// ---------- Ações ----------

	private void OnArtifactSlotPressed(int slotIndex)
	{
		_selectedArtifactSlot = slotIndex;
		RefreshArtifactSlotButtons();
		LinkArtifactChestFocus();
		ShowArtifactSlotDetail(slotIndex);

		var target = PreferredArtifactCollectionButton();
		if (target == null)
		{
			SetArtifactChestStatus(Localization.Text("chest.nothing_to_equip"));
			return;
		}
		SetArtifactChestStatus(Localization.TextFormat("chest.pick_artifact", (slotIndex + 1).ToString()));
		target.GrabFocus();
	}

	private void OnArtifactCollectionPressed(ArtifactChestEntry entry)
	{
		if (entry.FromRun)
		{
			SetArtifactChestStatus(Localization.Text("chest.run_only"));
			return;
		}

		var artifactName = entry.Record.Name;
		var displayName = ArtifactCatalog.DisplayNameFor(entry.Record);
		if (!EquipChestArtifact(_selectedArtifactSlot, artifactName))
		{
			SetArtifactChestStatus(Localization.TextFormat("chest.equip_failed", displayName));
			return;
		}

		SetArtifactChestStatus(Localization.TextFormat("chest.equipped_message", displayName, _selectedArtifactSlot + 1));
		RefreshArtifactChest();
		if (!_artifactChestPreviewMode)
			RefreshCharacterSheet();
		// Volta ao slot (os itens da coleção foram recriados).
		_artifactSlotButtons[_selectedArtifactSlot]?.GrabFocus();
	}

	private void UnequipSelectedArtifactSlot()
	{
		if (_artifactChestPreviewMode)
			_artifactChestPreviewSlots[_selectedArtifactSlot] = "";
		else
			CharacterSheet.UnequipArtifact(_selectedArtifactSlot);

		SetArtifactChestStatus(Localization.TextFormat("chest.slot_emptied", _selectedArtifactSlot + 1));
		RefreshArtifactChest();
		if (!_artifactChestPreviewMode)
			RefreshCharacterSheet();
		ShowArtifactSlotDetail(_selectedArtifactSlot);
	}

	private void CloseArtifactChest()
	{
		if (_artifactChestOverlay != null)
			UiModalStack.Pop(_artifactChestOverlay);
	}

	private void SetArtifactChestStatus(string text)
	{
		_artifactChestWindow?.SetStatus(string.IsNullOrEmpty(text)
			? Localization.Text("chest.subtitle")
			: text);
	}

	// ---------- Montagem ----------

	private void RefreshArtifactChest()
	{
		if (_artifactCollectionList == null)
			return;

		RefreshArtifactSlotButtons();
		RebuildArtifactCollection();
		LinkArtifactChestFocus();
	}

	private void RefreshArtifactSlotButtons()
	{
		for (var i = 0; i < _artifactSlotButtons.Length; i++)
		{
			var button = _artifactSlotButtons[i];
			if (button == null)
				continue;

			button.SetPressedNoSignal(i == _selectedArtifactSlot);
			foreach (var child in button.GetChildren())
			{
				button.RemoveChild(child);
				child.QueueFree();
			}

			var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			row.AddThemeConstantOverride("separation", UiMetrics.S3);
			row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
			row.OffsetLeft = UiMetrics.S3;
			row.OffsetRight = -UiMetrics.S3;
			button.AddChild(row);

			var entry = FindArtifactChestEntry(GetChestEquippedName(i));
			var name = entry?.Definition?.DisplayName ?? (entry != null ? ArtifactCatalog.DisplayNameFor(entry.Record) : null);
			row.AddChild(entry != null
				? UiComponents.IconOrMonogram(LoadArtifactIcon(entry.Definition), name, UiMetrics.IconM)
				: EmptyArtifactSlotIcon());

			var text = new VBoxContainer
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			text.AddThemeConstantOverride("separation", UiMetrics.S1);
			row.AddChild(text);
			var badgeRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			badgeRow.AddChild(UiComponents.Badge(Localization.TextFormat("ui.artifact_slot.number", i + 1), i == _selectedArtifactSlot ? UiPalette.Gold : UiPalette.PanelInset));
			text.AddChild(badgeRow);
			var nameLabel = MakeArtifactChestLabel(name ?? Localization.Text("chest.empty_slot"), UiVariation.BodyLabel);
			nameLabel.AddThemeColorOverride("font_color", entry == null
				? UiPalette.TextDisabled
				: UiPalette.Rarity(entry.Definition?.Rarity));
			text.AddChild(nameLabel);
		}
	}

	private void RebuildArtifactCollection()
	{
		foreach (var child in _artifactCollectionList.GetChildren())
		{
			_artifactCollectionList.RemoveChild(child);
			child.QueueFree();
		}
		_artifactCollectionButtons.Clear();
		_artifactCollectionEntries.Clear();

		var entries = BuildArtifactChestEntries();
		if (entries.Count == 0)
		{
			_artifactCollectionList.AddChild(new Label
			{
				Text = Localization.Text("chest.empty"),
				ThemeTypeVariation = UiVariation.SecondaryLabel,
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(0.0f, 72.0f),
				VerticalAlignment = VerticalAlignment.Center
			});
			return;
		}

		foreach (var entry in entries)
		{
			var button = CreateArtifactCollectionRow(entry);
			_artifactCollectionList.AddChild(button);
			_artifactCollectionButtons.Add(button);
			_artifactCollectionEntries.Add(entry);
		}
	}

	private Button CreateArtifactCollectionRow(ArtifactChestEntry entry)
	{
		var button = new Button
		{
			CustomMinimumSize = new Vector2(0.0f, 68.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			FocusMode = Control.FocusModeEnum.All,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		button.Pressed += () => OnArtifactCollectionPressed(entry);
		button.FocusEntered += () => ShowArtifactEntryDetail(entry);
		button.MouseEntered += () => ShowArtifactEntryDetail(entry);

		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", UiMetrics.S3);
		row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		row.OffsetLeft = UiMetrics.S3;
		row.OffsetRight = -UiMetrics.S3;
		button.AddChild(row);

		var name = entry.Definition?.DisplayName ?? ArtifactCatalog.DisplayNameFor(entry.Record);
		row.AddChild(UiComponents.IconOrMonogram(LoadArtifactIcon(entry.Definition), name, UiMetrics.IconM));

		var text = new VBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		text.AddThemeConstantOverride("separation", UiMetrics.S1);
		row.AddChild(text);

		var titleRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		titleRow.AddThemeConstantOverride("separation", UiMetrics.S2);
		text.AddChild(titleRow);
		var nameLabel = MakeArtifactChestLabel(name, UiVariation.BodyLabel);
		nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		nameLabel.AddThemeColorOverride("font_color", UiPalette.Rarity(entry.Definition?.Rarity));
		titleRow.AddChild(nameLabel);
		if (entry.Definition != null)
			titleRow.AddChild(ShrinkBadge(UiComponents.Badge(UiComponents.RarityLabel(entry.Definition.Rarity), UiPalette.Rarity(entry.Definition.Rarity))));
		// Selos de origem com fundo visível, como os demais (PanelInset some sobre a linha).
		titleRow.AddChild(ShrinkBadge(entry.FromRun
			? UiComponents.Badge(Localization.Text("chest.origin_run"), UiPalette.DecorCrimson, UiPalette.TextPrimary)
			: UiComponents.Badge(Localization.Text("chest.origin_collection"), UiPalette.GoldDim, UiPalette.TextPrimary)));
		var equippedSlot = FindEquippedChestSlot(entry.Record.Name);
		if (!entry.FromRun && equippedSlot >= 0)
			titleRow.AddChild(ShrinkBadge(UiComponents.Badge(Localization.TextFormat("ui.artifact_slot.number", equippedSlot + 1), UiPalette.Gold)));

		text.AddChild(MakeArtifactChestLabel(ArtifactEntryEffects(entry), UiVariation.SecondaryLabel));
		return button;
	}

	// Vizinhança explícita: slots + remover em ciclo vertical; direita leva à coleção; coleção em ciclo; esquerda volta ao slot selecionado.
	private void LinkArtifactChestFocus()
	{
		var slotItems = new List<Control>();
		foreach (var button in _artifactSlotButtons)
			if (button != null)
				slotItems.Add(button);
		if (_artifactUnequipButton != null)
			slotItems.Add(_artifactUnequipButton);
		ControllerUiSupport.SetVerticalCycle(slotItems);

		var rowItems = new List<Control>(_artifactCollectionButtons);
		ControllerUiSupport.SetVerticalCycle(rowItems);

		var target = PreferredArtifactCollectionButton() ?? (_artifactCollectionButtons.Count > 0 ? _artifactCollectionButtons[0] : null);
		if (target != null)
			foreach (var item in slotItems)
				item.FocusNeighborRight = item.GetPathTo(target);

		var selectedSlot = _artifactSlotButtons[Mathf.Clamp(_selectedArtifactSlot, 0, _artifactSlotButtons.Length - 1)];
		if (selectedSlot != null)
			foreach (var row in rowItems)
				row.FocusNeighborLeft = row.GetPathTo(selectedSlot);
	}

	// Item da coleção que recebe o foco ao escolher um slot: o equipado nele, senão o primeiro equipável.
	private Button PreferredArtifactCollectionButton()
	{
		var equippedName = GetChestEquippedName(_selectedArtifactSlot);
		Button firstEquippable = null;
		for (var i = 0; i < _artifactCollectionEntries.Count; i++)
		{
			var entry = _artifactCollectionEntries[i];
			if (entry.FromRun)
				continue;
			if (!string.IsNullOrEmpty(equippedName) && entry.Record.Name == equippedName)
				return _artifactCollectionButtons[i];
			firstEquippable ??= _artifactCollectionButtons[i];
		}
		return firstEquippable;
	}

	// ---------- Detalhes ----------

	private void ShowArtifactSlotDetail(int slotIndex)
	{
		if (_artifactChestDetail == null)
			return;

		var entry = FindArtifactChestEntry(GetChestEquippedName(slotIndex));
		if (entry == null)
		{
			_artifactChestDetail.Show(new DetailData
			{
				IconText = (slotIndex + 1).ToString(),
				Title = Localization.TextFormat("ui.artifact_slot.number", slotIndex + 1),
				Meta = Localization.Text("chest.empty_slot"),
				BodyBbcode = Localization.Text("chest.empty_slot_body"),
				ActionText = Localization.Text("chest.choose_artifact"),
				ActionGlyph = UiAction.Accept
			});
			return;
		}

		var data = BuildArtifactChestDetail(entry);
		data.Meta = $"{Localization.TextFormat("ui.artifact_slot.number", slotIndex + 1)} · {data.Meta}";
		data.ActionText = Localization.Text("chest.swap_artifact");
		data.ActionGlyph = UiAction.Accept;
		_artifactChestDetail.Show(data);
	}

	private void ShowArtifactEntryDetail(ArtifactChestEntry entry)
	{
		if (_artifactChestDetail == null)
			return;

		var data = BuildArtifactChestDetail(entry);
		if (!entry.FromRun)
		{
			data.ActionText = Localization.TextFormat("chest.equip_in_slot", (_selectedArtifactSlot + 1).ToString());
			data.ActionGlyph = UiAction.Accept;
		}
		_artifactChestDetail.Show(data);
	}

	private DetailData BuildArtifactChestDetail(ArtifactChestEntry entry)
	{
		var origin = entry.FromRun
			? Localization.Text("chest.origin_run_long")
			: Localization.Text("chest.origin_collection_long");
		var definition = entry.Definition;
		if (definition == null)
		{
			return new DetailData
			{
				Title = ArtifactCatalog.DisplayNameFor(entry.Record),
				Meta = $"{ArtifactSlotText(entry.Record.Slot)} · {origin}",
				BodyBbcode = EscapeArtifactBbcode(ArtifactEntryEffects(entry))
			};
		}

		return new DetailData
		{
			Icon = LoadArtifactIcon(definition),
			Title = definition.DisplayName,
			RarityId = definition.Rarity,
			BadgeText = UiComponents.RarityLabel(definition.Rarity),
			Meta = $"{ArtifactSlotText(definition.Slot)} · {origin}",
			BodyBbcode = ArtifactBodyBbcode(definition),
			Flavor = definition.FlavorText
		};
	}

	private static string ArtifactEntryEffects(ArtifactChestEntry entry)
	{
		if (entry.Definition != null)
			return FormatArtifactEffectList(entry.Definition);
		return FormatArtifactRecordEffect(entry.Record);
	}

	// ---------- Dados (reais ou da prévia) ----------

	private List<ArtifactChestEntry> BuildArtifactChestEntries()
	{
		var entries = new List<ArtifactChestEntry>();
		var displayed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		IReadOnlyList<ArtifactRecord> run = _artifactChestPreviewMode ? _artifactChestPreviewRun : RunState.RunArtifacts;
		IReadOnlyList<ArtifactRecord> found = _artifactChestPreviewMode ? _artifactChestPreviewFound : CharacterSheet.FoundArtifacts;

		foreach (var artifact in run)
		{
			entries.Add(MakeArtifactChestEntry(artifact, true));
			displayed.Add(ArtifactCollectionKey(artifact));
		}

		// Artefatos da coleção permanente podem ser equipados no slot selecionado.
		foreach (var artifact in found)
			if (displayed.Add(ArtifactCollectionKey(artifact)))
				entries.Add(MakeArtifactChestEntry(artifact, false));

		return entries;
	}

	private static ArtifactChestEntry MakeArtifactChestEntry(ArtifactRecord record, bool fromRun)
	{
		ArtifactDefinition definition = null;
		if (!string.IsNullOrWhiteSpace(record.Id))
			ArtifactCatalog.TryGet(record.Id, out definition);
		return new ArtifactChestEntry { Record = record, Definition = definition, FromRun = fromRun };
	}

	// Equipados guardam o nome (CharacterSheet). Procura na coleção permanente (ou na prévia).
	private ArtifactChestEntry FindArtifactChestEntry(string artifactName)
	{
		if (string.IsNullOrWhiteSpace(artifactName))
			return null;

		IReadOnlyList<ArtifactRecord> found = _artifactChestPreviewMode ? _artifactChestPreviewFound : CharacterSheet.FoundArtifacts;
		foreach (var artifact in found)
			if (artifact.Name == artifactName)
				return MakeArtifactChestEntry(artifact, false);

		// Nome sem registro (save antigo): mostra só o nome.
		return new ArtifactChestEntry { Record = new ArtifactRecord("", artifactName, "", "", 0.0f), FromRun = false };
	}

	private string GetChestEquippedName(int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= _artifactSlotButtons.Length)
			return "";
		return _artifactChestPreviewMode
			? _artifactChestPreviewSlots[slotIndex] ?? ""
			: CharacterSheet.GetEquippedArtifactSlot(slotIndex);
	}

	private int FindEquippedChestSlot(string artifactName)
	{
		for (var i = 0; i < _artifactSlotButtons.Length; i++)
			if (!string.IsNullOrEmpty(artifactName) && GetChestEquippedName(i) == artifactName)
				return i;
		return -1;
	}

	private bool EquipChestArtifact(int slotIndex, string artifactName)
	{
		if (!_artifactChestPreviewMode)
			return CharacterSheet.EquipArtifact(slotIndex, artifactName);

		foreach (var artifact in _artifactChestPreviewFound)
		{
			if (artifact.Name != artifactName)
				continue;
			_artifactChestPreviewSlots[slotIndex] = artifactName;
			return true;
		}
		return false;
	}

	private static string ArtifactCollectionKey(ArtifactRecord artifact)
	{
		return string.IsNullOrWhiteSpace(artifact.Id) ? artifact.Name : artifact.Id;
	}

	// Preview de debug (HUD.OpenPreviewScreen): 1 artefato da run (Épico) e coleção com Comum/Mítico/Raro/Lendário/Incomum;
	// Mítico no slot 1.
	private void OpenArtifactChestPreview()
	{
		if (_artifactChestOverlay == null)
			return;

		_artifactChestPreviewRun.Clear();
		_artifactChestPreviewFound.Clear();
		Array.Fill(_artifactChestPreviewSlots, "");
		foreach (var rarity in new[] { "Epic", "Common", "Mythic", "Rare", "Legendary", "Uncommon" })
		{
			foreach (var artifact in ArtifactCatalog.All)
			{
				if (!string.Equals(artifact.Rarity, rarity, StringComparison.OrdinalIgnoreCase))
					continue;
				var record = ArtifactCatalog.CreateRecord(artifact);
				if (rarity == "Epic")
					_artifactChestPreviewRun.Add(record);
				else
					_artifactChestPreviewFound.Add(record);
				if (rarity == "Mythic")
					_artifactChestPreviewSlots[0] = record.Name;
				break;
			}
		}

		if (UiModalStack.IsOpen(_artifactChestOverlay))
			UiModalStack.Pop(_artifactChestOverlay);
		_artifactChestPreviewMode = true;
		ShowArtifactChest();
		SetArtifactChestStatus("Prévia: equipar e remover só simulam; nada é gravado.");
	}

	// ---------- Peças visuais ----------

	private static Label MakeArtifactChestLabel(string text, string variation)
	{
		return new Label
		{
			Text = text ?? "",
			ThemeTypeVariation = variation,
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
	}

	private static Control ShrinkBadge(Control badge)
	{
		badge.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		return badge;
	}

	// Slot vazio: quadro PanelInset do tamanho do ícone, borda 1 Border.
	private static Control EmptyArtifactSlotIcon()
	{
		var frame = new Panel
		{
			CustomMinimumSize = new Vector2(UiMetrics.IconM, UiMetrics.IconM),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		frame.AddThemeStyleboxOverride("panel", UIThemeFactory.ControlBox(UiPalette.PanelInset, UiPalette.Border, UiMetrics.BorderThin, 0, 0));
		return frame;
	}
}
