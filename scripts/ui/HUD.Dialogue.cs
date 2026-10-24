using Godot;
using System;
using System.Collections.Generic;

// Dialogo (docs/design/dialogos-arquitetura.md, secao 7): conversa modal de NPC/boss e legenda de bark nao modal.
// O nucleo (DialogueManager) chama por CallGroup("hud", ...): OpenDialogue, ShowDialogueBark, HideDialogueBark. Nao renomear.
// Conversa = ColorRect de tela cheia (scrim leve, 0,35) + caixa no terco inferior + retrato que sobe 40 px acima da caixa.
// Pausa a arvore e mantem a musica (UiModalStack.Push pausesAudio: false). Esc/B pula ate a proxima escolha ou o fim.
// O texto aparece em ~45 caracteres/s (HUD._Process -> UpdateDialogue); confirmar completa a linha e, completa, avanca.
// Bark = painel dentro do _combatHudLayer (some junto com pausa/game over), sem foco, uma linha por vez.
// Previa (--ui-preview=dialogue|dialogue_choice|dialogue_bark|hub_npc): definicao em memoria, nada e gravado.
public partial class HUD
{
	private const float DialogueCharsPerSecond = 45.0f;
	private const float DialogueSentencePause = 0.15f;
	private const float DialogueScrimAlpha = 0.35f;
	private const float DialogueBoxWidth = 1100.0f;
	private const float DialogueBoxMinHeight = 220.0f;
	private const int DialoguePortraitSize = 160;
	private const float DialoguePortraitRise = 40.0f;
	private const int DialogueBarkPortraitSize = 40;
	private const float DialogueBarkWidth = 640.0f;
	private const float BarkFadeInSeconds = 0.15f;
	private const float BarkFadeOutSeconds = 0.30f;

	private ColorRect _dialogueOverlay;
	private PanelContainer _dialogueBox;
	private PanelContainer _dialoguePortrait;
	private Label _dialogueNameLabel;
	private Label _dialogueTextLabel;
	private Label _dialogueChoosePrompt;
	private Button _dialogueAdvanceButton;
	private Control _dialogueArrow;
	private VBoxContainer _dialogueChoiceList;
	private UiHintBar _dialogueHints;
	private UiHint _dialogueAcceptHint;
	private UiHint _dialogueSkipHint;
	private readonly List<Control> _dialogueChoiceButtons = new();
	private DialoguePlayback _dialoguePlayback;
	private bool _dialoguePreviewMode;
	private bool _dialogueInChoice;
	private bool _dialogueLineDone;
	private float _dialogueReveal;
	private float _dialoguePause;
	private ulong _dialogueLastAdvanceFrame;

	private PanelContainer _barkPanel;
	private PanelContainer _barkPortrait;
	private Label _barkNameLabel;
	private Label _barkTextLabel;
	private bool _barkActive;
	private float _barkAge;
	private float _barkTimeLeft;

	// ---------- API chamada pelo nucleo (CallGroup) ----------

	// Chamado via CallGroup("hud", "OpenDialogue", conversationId) por DialogueManager.StartConversation.
	public void OpenDialogue(string conversationId)
	{
		var playback = DialogueManager.Instance?.CurrentPlayback;
		if (playback == null || playback.Definition.Id != conversationId)
		{
			GD.PushWarning($"HUD.OpenDialogue: conversa '{conversationId}' nao e a corrente; ignorada.");
			return;
		}
		if (_dialogueOverlay == null || _dialoguePlayback != null)
			return;
		BeginDialogue(playback);
	}

	// Chamado via CallGroup("hud", "ShowDialogueBark", speakerId, speakerName, text, emotion, seconds).
	public void ShowDialogueBark(string speakerId, string speakerName, string text, string emotion, float seconds)
	{
		if (_barkPanel == null || _isGameOver || _dialoguePlayback != null || UiModalStack.AnyOpen)
			return;
		_barkNameLabel.Text = speakerName ?? "";
		_barkTextLabel.Text = text ?? "";
		FillDialoguePortrait(_barkPortrait, speakerId, speakerName, emotion, DialogueBarkPortraitSize);
		_barkTimeLeft = Mathf.Max(seconds, 0.5f);
		_barkAge = 0.0f;
		_barkActive = true;
		_barkPanel.Modulate = new Color(1, 1, 1, 0);
		_barkPanel.Visible = true;
	}

	// Chamado via CallGroup("hud", "HideDialogueBark") ao abrir uma conversa modal e na troca de sala.
	public void HideDialogueBark()
	{
		_barkActive = false;
		if (_barkPanel != null)
			_barkPanel.Visible = false;
	}

	// ---------- Montagem ----------

	// Dentro do _combatHudLayer: chamado em BuildLayout antes de BuildPauseOverlay (GroupCombatHud recolhe o que vem antes).
	private void BuildDialogueBark(Control root)
	{
		_barkPanel = new PanelContainer
		{
			Name = "DialogueBark",
			ThemeTypeVariation = UiVariation.HudPanel,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1.0f, AnchorBottom = 1.0f,
			OffsetLeft = -DialogueBarkWidth * 0.5f, OffsetRight = DialogueBarkWidth * 0.5f,
			OffsetTop = -(72.0f + UiMetrics.SafeMargin), OffsetBottom = -(72.0f + UiMetrics.SafeMargin),
			GrowVertical = Control.GrowDirection.Begin,
			Visible = false
		};
		root.AddChild(_barkPanel);

		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", UiMetrics.S3);
		_barkPanel.AddChild(row);

		_barkPortrait = new PanelContainer { Name = "BarkPortrait", MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
		row.AddChild(_barkPortrait);

		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		column.AddThemeConstantOverride("separation", 0);
		row.AddChild(column);

		_barkNameLabel = new Label
		{
			Name = "BarkName",
			ThemeTypeVariation = UiVariation.BodyLabel,
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_barkNameLabel.AddThemeColorOverride("font_color", UiPalette.Gold);
		column.AddChild(_barkNameLabel);

		_barkTextLabel = new Label
		{
			Name = "BarkText",
			ThemeTypeVariation = UiVariation.BodyLabel,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(DialogueBarkWidth - DialogueBarkPortraitSize - 64.0f, 0.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		column.AddChild(_barkTextLabel);
	}

	private void BuildDialogueOverlay(Control root)
	{
		_dialogueOverlay = new ColorRect
		{
			Name = "DialogueOverlay",
			Color = new Color(UiPalette.Scrim, DialogueScrimAlpha),
			MouseFilter = Control.MouseFilterEnum.Stop,
			ProcessMode = ProcessModeEnum.Always,
			Visible = false
		};
		_dialogueOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		root.AddChild(_dialogueOverlay);

		_dialogueBox = new PanelContainer
		{
			Name = "DialogueBox",
			ThemeTypeVariation = UiVariation.WindowPanel,
			CustomMinimumSize = new Vector2(DialogueBoxWidth, DialogueBoxMinHeight),
			AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1.0f, AnchorBottom = 1.0f,
			OffsetLeft = -DialogueBoxWidth * 0.5f, OffsetRight = DialogueBoxWidth * 0.5f,
			OffsetTop = -UiMetrics.SafeMargin, OffsetBottom = -UiMetrics.SafeMargin,
			GrowVertical = Control.GrowDirection.Begin
		};
		_dialogueBox.AddThemeStyleboxOverride("panel", UiPixelKit.WindowFrame() ?? UIThemeFactory.WindowBox());
		if (UiPixelKit.Enabled)
			_dialogueBox.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
		_dialogueOverlay.AddChild(_dialogueBox);

		// Botao plano de caixa inteira: recebe o foco (ui_accept) e o clique; fica por baixo do conteudo.
		_dialogueAdvanceButton = new Button
		{
			Name = "DialogueAdvanceButton",
			Flat = true,
			FocusMode = Control.FocusModeEnum.All,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		var empty = new StyleBoxEmpty();
		foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
			_dialogueAdvanceButton.AddThemeStyleboxOverride(state, empty);
		_dialogueAdvanceButton.Pressed += OnDialogueAdvance;
		_dialogueBox.AddChild(_dialogueAdvanceButton);

		var content = new HBoxContainer { Name = "DialogueContent", MouseFilter = Control.MouseFilterEnum.Ignore };
		content.AddThemeConstantOverride("separation", 0);
		_dialogueBox.AddChild(content);

		// Reserva o espaco do retrato (160 + folga), que e um irmao desenhado por cima da caixa.
		content.AddChild(new Control { CustomMinimumSize = new Vector2(DialoguePortraitSize + UiMetrics.S4, 0), MouseFilter = Control.MouseFilterEnum.Ignore });

		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		column.AddThemeConstantOverride("separation", UiMetrics.S2);
		content.AddChild(column);

		_dialogueNameLabel = new Label
		{
			Name = "DialogueName",
			ThemeTypeVariation = UiVariation.SectionGoldLabel,
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		column.AddChild(_dialogueNameLabel);

		_dialogueTextLabel = new Label
		{
			Name = "DialogueText",
			ThemeTypeVariation = UiVariation.BodyLabel,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			CustomMinimumSize = new Vector2(0, 100),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_dialogueTextLabel.AddThemeFontSizeOverride("font_size", UiFont.Section);
		column.AddChild(_dialogueTextLabel);

		_dialogueChoosePrompt = new Label
		{
			Name = "DialogueChoosePrompt",
			Text = Localization.Text("ui.dialogue.choose"),
			ThemeTypeVariation = UiVariation.SecondaryLabel,
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		column.AddChild(_dialogueChoosePrompt);

		_dialogueChoiceList = new VBoxContainer { Name = "DialogueChoices", Visible = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_dialogueChoiceList.AddThemeConstantOverride("separation", UiMetrics.S2);
		column.AddChild(_dialogueChoiceList);

		_dialogueArrow = new Control
		{
			Name = "DialogueContinueArrow",
			CustomMinimumSize = new Vector2(18, 12),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1, 1, 1, 0)
		};
		_dialogueArrow.Draw += () => _dialogueArrow.DrawColoredPolygon(
			new[] { new Vector2(2, 1), new Vector2(16, 1), new Vector2(9, 11) }, UiPalette.Gold);
		column.AddChild(_dialogueArrow);

		_dialogueHints = UiComponents.HintBar();
		_dialogueSkipHint = _dialogueHints.Add(UiAction.Cancel, Localization.Text("ui.dialogue.skip"), SkipDialogue, left: true);
		_dialogueAcceptHint = _dialogueHints.Add(UiAction.Accept, Localization.Text("ui.dialogue.continue"), OnDialogueAccept);
		column.AddChild(_dialogueHints.Root);

		// Retrato por cima da caixa, subindo ~40 px; posicionado a cada quadro (UpdateDialogue) porque a caixa cresce para cima.
		_dialoguePortrait = new PanelContainer { Name = "DialoguePortrait", MouseFilter = Control.MouseFilterEnum.Ignore };
		_dialogueOverlay.AddChild(_dialoguePortrait);

		_dialogueOverlay.VisibilityChanged += OnDialogueOverlayVisibilityChanged;
		_dialogueOverlay.TreeExiting += EndDialoguePlaybackSilently;
		RegisterModalShortcuts(_dialogueOverlay, HandleDialogueShortcut);
	}

	// ---------- Fluxo ----------

	private void BeginDialogue(DialoguePlayback playback)
	{
		_dialoguePlayback = playback;
		_dialoguePreviewMode = playback.IsPreview;
		HideDialogueBark();
		var focus = ShowDialogueFrame(false);
		UiModalStack.Push(_dialogueOverlay, pauses: true, initialFocus: focus, onCancel: SkipDialogue, mandatory: false, pausesAudio: false);
		_dialogueOverlay.Modulate = new Color(1, 1, 1, 0);
		var fade = _dialogueOverlay.CreateTween();
		fade.TweenProperty(_dialogueOverlay, "modulate:a", 1.0f, UiMotion.OverlayIn);
	}

	// Le playback.Frame e monta a caixa. Devolve o controle que deve ficar com o foco.
	private Control ShowDialogueFrame(bool revealAll)
	{
		var frame = _dialoguePlayback.Frame;
		_dialogueInChoice = frame.IsChoice;
		_dialogueNameLabel.Text = frame.SpeakerName ?? "";
		_dialogueNameLabel.AddThemeColorOverride("font_color", DialogueCatalog.TryGetSpeaker(frame.SpeakerId, out var speaker) ? speaker.NameColor : UiPalette.Gold);
		FillDialoguePortrait(_dialoguePortrait, frame.SpeakerId, frame.SpeakerName, frame.PortraitVariant, DialoguePortraitSize);

		_dialogueSkipHint.SetLabel(Localization.Text("ui.dialogue.skip"));
		_dialogueChoosePrompt.Text = Localization.Text("ui.dialogue.choose");
		ClearDialogueChoices();

		Control focus;
		if (frame.IsChoice)
		{
			_dialogueTextLabel.Visible = false;
			_dialogueChoosePrompt.Visible = true;
			_dialogueChoiceList.Visible = true;
			_dialogueAdvanceButton.Visible = false;
			_dialogueArrow.Modulate = new Color(1, 1, 1, 0);
			_dialogueArrow.Visible = false;
			_dialogueSkipHint.Root.Visible = false;
			_dialogueAcceptHint.SetLabel(Localization.Text("ui.common.choose"));
			for (var i = 0; i < frame.ChoiceTexts.Count; i++)
			{
				var index = i;
				var button = UiComponents.MenuButton(frame.ChoiceTexts[i], () => ChooseDialogueOption(index), ButtonVariant.Secondary, ButtonSize.Medium);
				button.Name = "DialogueChoice" + (i + 1);
				button.Alignment = HorizontalAlignment.Left;
				button.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				button.CustomMinimumSize = new Vector2(0, UiMetrics.ButtonM);
				button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				_dialogueChoiceList.AddChild(button);
				_dialogueChoiceButtons.Add(button);
			}
			ControllerUiSupport.SetVerticalCycle(_dialogueChoiceButtons);
			focus = _dialogueChoiceButtons.Count > 0 ? _dialogueChoiceButtons[0] : _dialogueAdvanceButton;
		}
		else
		{
			_dialogueTextLabel.Visible = true;
			_dialogueChoosePrompt.Visible = false;
			_dialogueChoiceList.Visible = false;
			_dialogueAdvanceButton.Visible = true;
			_dialogueArrow.Visible = true;
			_dialogueSkipHint.Root.Visible = true;
			_dialogueAcceptHint.SetLabel(Localization.Text("ui.dialogue.continue"));
			_dialogueTextLabel.Text = frame.Text ?? "";
			var instant = revealAll || _dialoguePreviewMode || string.IsNullOrEmpty(frame.Text);
			_dialogueTextLabel.VisibleCharacters = instant ? -1 : 0;
			_dialogueReveal = 0.0f;
			_dialoguePause = 0.0f;
			_dialogueLineDone = instant;
			focus = _dialogueAdvanceButton;
		}

		if (_dialogueOverlay.IsVisibleInTree())
			ControllerUiSupport.FocusInitial(focus, _dialogueOverlay);
		return focus;
	}

	private void ClearDialogueChoices()
	{
		foreach (var child in _dialogueChoiceList.GetChildren())
		{
			_dialogueChoiceList.RemoveChild(child);
			child.QueueFree();
		}
		_dialogueChoiceButtons.Clear();
	}

	// Chamado todo quadro por HUD._Process (a arvore pode estar pausada pela conversa: o HUD e Always).
	private void UpdateDialogue(float delta)
	{
		UpdateDialogueBark(delta);
		if (_dialoguePlayback == null || _dialogueOverlay == null || !_dialogueOverlay.Visible)
			return;

		// Retrato: canto superior esquerdo da caixa (dentro do preenchimento), subindo DialoguePortraitRise acima do topo.
		_dialoguePortrait.Position = new Vector2(
			_dialogueBox.Position.X + UiMetrics.PadWindowX,
			_dialogueBox.Position.Y - DialoguePortraitRise);

		if (_dialogueInChoice)
			return;

		if (_dialogueLineDone)
		{
			var pulse = 0.5f + 0.5f * Mathf.Sin(_hudClock * 6.0f);
			_dialogueArrow.Modulate = new Color(1, 1, 1, 0.4f + 0.6f * pulse);
			return;
		}
		_dialogueArrow.Modulate = new Color(1, 1, 1, 0);

		if (_dialoguePause > 0.0f)
		{
			_dialoguePause -= delta;
			return;
		}
		var text = _dialogueTextLabel.Text;
		var total = text.Length;
		var before = (int)_dialogueReveal;
		_dialogueReveal += delta * DialogueCharsPerSecond;
		var after = Math.Min((int)_dialogueReveal, total);
		for (var i = before; i < after; i++)
		{
			if (!IsDialogueSentenceEnd(text, i))
				continue;
			after = i + 1;
			_dialogueReveal = after;
			_dialoguePause = DialogueSentencePause;
			break;
		}
		_dialogueTextLabel.VisibleCharacters = after;
		if (after >= total)
			FinishDialogueLine();
	}

	private static bool IsDialogueSentenceEnd(string text, int index)
	{
		var c = text[index];
		if (c != '.' && c != '!' && c != '?' && c != '…')
			return false;
		return index + 1 >= text.Length || char.IsWhiteSpace(text[index + 1]);
	}

	private void FinishDialogueLine()
	{
		_dialogueTextLabel.VisibleCharacters = -1;
		_dialogueLineDone = true;
	}

	// Confirmar: completa a linha em andamento; com a linha completa, avanca.
	private void OnDialogueAdvance()
	{
		if (_dialoguePlayback == null || _dialogueInChoice || UiModalStack.Top != _dialogueOverlay)
			return;
		// No maximo um avanco por quadro (evita ui_accept + interact do mesmo aperto).
		var frameNow = Engine.GetProcessFrames();
		if (frameNow == _dialogueLastAdvanceFrame)
			return;
		_dialogueLastAdvanceFrame = frameNow;
		if (!_dialogueLineDone)
		{
			FinishDialogueLine();
			return;
		}
		_dialoguePlayback.Advance();
		AfterDialogueStep();
	}

	// Dica "Continuar/Escolher": avanca a linha ou aciona a opcao com foco (a primeira, se nenhuma tiver).
	private void OnDialogueAccept()
	{
		if (!_dialogueInChoice)
		{
			OnDialogueAdvance();
			return;
		}
		var index = 0;
		for (var i = 0; i < _dialogueChoiceButtons.Count; i++)
			if (_dialogueChoiceButtons[i].HasFocus())
				index = i;
		ChooseDialogueOption(index);
	}

	private void ChooseDialogueOption(int index)
	{
		if (_dialoguePlayback == null || !_dialogueInChoice || UiModalStack.Top != _dialogueOverlay)
			return;
		_dialoguePlayback.Choose(index);
		AfterDialogueStep();
	}

	// Esc/B/Circulo: pula ate a proxima escolha ou o fim. Numa escolha nao faz nada (a resposta e obrigatoria).
	private void SkipDialogue()
	{
		if (_dialoguePlayback == null || _dialogueInChoice)
			return;
		_dialoguePlayback.Skip();
		AfterDialogueStep();
	}

	private void AfterDialogueStep()
	{
		if (_dialoguePlayback.IsFinished)
		{
			CloseDialogue();
			return;
		}
		ShowDialogueFrame(false);
	}

	private void CloseDialogue()
	{
		var playback = _dialoguePlayback;
		_dialoguePlayback = null;
		_dialogueInChoice = false;
		if (_dialogueOverlay != null && IsInstanceValid(_dialogueOverlay))
			UiModalStack.Pop(_dialogueOverlay);
		DialogueManager.Instance?.EndPlayback(playback);
	}

	// Fechada por outro caminho (UiModalStack.Clear no game over, esconder): encerra o roteiro uma unica vez.
	private void OnDialogueOverlayVisibilityChanged()
	{
		if (_dialogueOverlay != null && !_dialogueOverlay.Visible && _dialoguePlayback != null)
			EndDialoguePlaybackSilently();
	}

	private void EndDialoguePlaybackSilently()
	{
		var playback = _dialoguePlayback;
		_dialoguePlayback = null;
		_dialogueInChoice = false;
		if (playback != null)
			DialogueManager.Instance?.EndPlayback(playback);
	}

	// interact (E / A) avanca como o botao focado. ui_accept ja e consumido pelo proprio Button, entao nao ha avanco duplo.
	private bool HandleDialogueShortcut(InputEvent @event)
	{
		if (_dialoguePlayback == null || _dialogueInChoice || !@event.IsActionPressed("interact"))
			return false;
		// A do controle mapeia ui_accept e interact: com o botao de avanco focado ele ja tratou o ui_accept.
		if (@event.IsActionPressed("ui_accept") && _dialogueAdvanceButton.HasFocus())
			return false;
		OnDialogueAdvance();
		return true;
	}

	// Troca de idioma: recalcula o quadro atual sem perder a posicao (texto completo, sem redigitar). Fechada, so reconstroi.
	private void RefreshDialogueLanguage()
	{
		if (_dialogueOverlay == null || _hudRoot == null)
			return;
		if (_dialoguePlayback != null && _dialogueOverlay.Visible)
		{
			var focusedIndex = -1;
			for (var i = 0; i < _dialogueChoiceButtons.Count; i++)
				if (_dialogueChoiceButtons[i].HasFocus())
					focusedIndex = i;
			ShowDialogueFrame(true);
			if (focusedIndex >= 0 && focusedIndex < _dialogueChoiceButtons.Count)
				ControllerUiSupport.FocusInitial(_dialogueChoiceButtons[focusedIndex], _dialogueOverlay);
			return;
		}
		if (_dialogueOverlay.Visible)
			return;
		_modalShortcutHandlers.Remove(_dialogueOverlay);
		RebuildOverlay(_dialogueOverlay, () => BuildDialogueOverlay(_hudRoot), () => _dialogueOverlay);
	}

	// ---------- Bark ----------

	private void UpdateDialogueBark(float delta)
	{
		if (!_barkActive || _barkPanel == null)
			return;
		if (_dialoguePlayback != null || UiModalStack.AnyOpen || _isGameOver)
		{
			HideDialogueBark();
			return;
		}
		if (GetTree().Paused)
			return;
		_barkAge += delta;
		_barkTimeLeft -= delta;
		if (_barkTimeLeft <= 0.0f)
		{
			HideDialogueBark();
			return;
		}
		var alpha = Mathf.Min(Mathf.Clamp(_barkAge / BarkFadeInSeconds, 0.0f, 1.0f), Mathf.Clamp(_barkTimeLeft / BarkFadeOutSeconds, 0.0f, 1.0f));
		_barkPanel.Modulate = new Color(1, 1, 1, alpha);
	}

	// ---------- Retrato ----------

	// Textura do falante quando existir; senao quadrado colorido com a inicial e moldura (acento).
	private static void FillDialoguePortrait(PanelContainer holder, string speakerId, string speakerName, string variant, int size)
	{
		if (holder == null)
			return;
		foreach (var child in holder.GetChildren())
		{
			holder.RemoveChild(child);
			child.QueueFree();
		}
		var spec = DialogueManager.ResolvePortrait(speakerId, variant);
		var fill = spec?.Color ?? UiPalette.PanelInset;
		var accent = spec?.Accent ?? UiPalette.Gold;
		holder.CustomMinimumSize = new Vector2(size, size);
		holder.ClipContents = true;
		holder.AddThemeStyleboxOverride("panel", UIThemeFactory.ControlBox(fill, accent, size >= 100 ? 3 : 2, 0, 0, UiMetrics.RadiusControl));

		Texture2D texture = null;
		if (spec != null && !string.IsNullOrEmpty(spec.TexturePath) && ResourceLoader.Exists(spec.TexturePath))
			texture = ResourceLoader.Load<Texture2D>(spec.TexturePath);
		if (texture != null)
		{
			holder.AddChild(new TextureRect
			{
				Texture = texture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				MouseFilter = Control.MouseFilterEnum.Ignore
			});
			return;
		}

		var initial = !string.IsNullOrEmpty(spec?.Initial)
			? spec.Initial
			: (string.IsNullOrEmpty(speakerName) ? "?" : speakerName.Substring(0, 1).ToUpperInvariant());
		var label = new Label
		{
			Text = initial,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeFontSizeOverride("font_size", Mathf.RoundToInt(size * 0.5f));
		label.AddThemeColorOverride("font_color", UiPalette.TextPrimary);
		label.AddThemeConstantOverride("outline_size", size >= 100 ? 6 : 3);
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.7f));
		holder.AddChild(label);
	}

	// ---------- Previa (debug, HUD.OpenPreviewScreen) ----------

	// dialogue / dialogue_choice: conversa em memoria (texto da interface, nao do dialogue.json) com playback de previa.
	// Tudo ja revelado, para a captura do lote ser estavel; nada e marcado como visto nem gravado.
	internal void OpenDialoguePreview(bool choiceFrame)
	{
		if (_dialogueOverlay == null)
			return;
		if (_dialoguePlayback != null)
			CloseDialogue();
		var definition = new DialogueDefinition(
			"ui_preview", "odalric", DialogueCategory.Hub, DialogueTrigger.None, DialoguePriorityClass.Evergreen,
			0, true, 0, 1.0f, false, Array.Empty<DialogueCondition>(), Array.Empty<string>(),
			new[]
			{
				new DialogueLine("ui.dialogue.preview.line.1", "", "", ""),
				new DialogueLine("ui.dialogue.preview.line.2", "virgil", "grim", ""),
				new DialogueLine("ui.dialogue.preview.line.3", "", "", "")
			},
			new[]
			{
				new DialogueChoice("who", "ui.dialogue.preview.choice.1", Array.Empty<string>(), ""),
				new DialogueChoice("curt", "ui.dialogue.preview.choice.2", Array.Empty<string>(), "")
			},
			new Dictionary<string, DialogueBranch>());
		var playback = DialogueManager.Instance != null
			? DialogueManager.Instance.CreatePlayback(definition, true)
			: new DialoguePlayback(definition, true, null);
		if (choiceFrame)
			playback.Skip();
		BeginDialogue(playback);
	}

	// Estresse (dialogue_long / dialogue_choice_long / dialogue_bark_long): texto sintetico no limite do validador
	// (linha 180, bark 90) com palavras longas, no idioma corrente (pior caso en/es). So debug; nada e gravado.
	private static string DialogueStressText(int length)
	{
		var words = Localization.Instance?.CurrentLanguage switch
		{
			"en" => new[] { "Unquestionably", "the", "extraordinarily", "overwhelming", "bloodstained", "inheritance", "of", "Armand's", "forsaken", "cathedral", "whispers" },
			"es" => new[] { "Indudablemente", "la", "extraordinariamente", "abrumadora", "herencia", "ensangrentada", "de", "la", "catedral", "abandonada", "susurra" },
			_ => new[] { "Indubitavelmente", "a", "extraordinariamente", "avassaladora", "herança", "ensanguentada", "da", "catedral", "abandonada", "de", "Armand", "sussurra" }
		};
		var builder = new System.Text.StringBuilder();
		for (var i = 0; builder.Length < length; i++)
			builder.Append(i == 0 ? "" : " ").Append(words[i % words.Length]);
		return builder.ToString(0, length);
	}

	internal void OpenDialogueStressPreview(bool choiceFrame)
	{
		OpenDialoguePreview(choiceFrame);
		if (_dialoguePlayback == null)
			return;
		const string longName = "Odalric, Cronista da Corte de Armand";
		_dialogueNameLabel.Text = longName;
		if (choiceFrame)
		{
			var texts = new[] { DialogueStressText(120), DialogueStressText(100) };
			for (var i = 0; i < _dialogueChoiceButtons.Count && i < texts.Length; i++)
				if (_dialogueChoiceButtons[i] is Button button)
					button.Text = texts[i];
		}
		else
			_dialogueTextLabel.Text = DialogueStressText(180);
	}

	internal void PreviewDialogueBarkStress()
	{
		ShowDialogueBark("virgil", "Virgil, o Caçador Amaldiçoado", DialogueStressText(90), "grim", 2.8f);
	}

	// dialogue_bark: legenda de Virgil; o UiPreview repete a cada 3 s.
	internal void PreviewDialogueBark()
	{
		var name = DialogueCatalog.TryGetSpeaker("virgil", out var speaker) ? speaker.DisplayName : "Virgil";
		ShowDialogueBark("virgil", name, Localization.Text("ui.dialogue.preview.bark"), "grim", 2.8f);
	}

	// hub_npc: so o cenario, com o jogador diante de Odalric (CrowsNestCrypt.CreateDialogueNpc).
	internal void PreviewHubNpc()
	{
		if (GetTree().GetFirstNodeInGroup("player") is Node3D player)
		{
			player.GlobalPosition = new Vector3(-11.4f, player.GlobalPosition.Y, 2.6f);
			player.LookAt(new Vector3(-12.6f, player.GlobalPosition.Y, 3.2f), Vector3.Up);
		}
	}
}
