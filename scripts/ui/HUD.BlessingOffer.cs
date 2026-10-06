using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class HUD
{
	public void OpenBloodBlessingOffer()
	{
		OpenBloodBlessingOfferInternal(false);
	}

	public void OpenMandatoryBloodBlessingOffer()
	{
		OpenBloodBlessingOfferInternal(true);
	}

	// "mandatory" = primeira bênção da run (sem absorção). Toda oferta é obrigatória: não fecha sem escolher.
	private async void OpenBloodBlessingOfferInternal(bool mandatory)
	{
		if (_bloodBlessingOverlay == null) return;
		// Reentrada: absorção em curso ou oferta já aberta (reabrir re-sortearia as opções).
		if (_blessingAbsorptionRunning || UiModalStack.IsOpen(_bloodBlessingOverlay)) return;
		if (mandatory)
		{
			AppendGameLog("A fonte primordial revelou a primeira Bencao de Sangue desta run.");
			BuildBloodBlessingChoices();
			return;
		}
		_blessingAbsorptionRunning = true;
		UiModalStack.RefreshPause();
		_bloodAbsorptionOverlay.Visible = true;
		_bloodAbsorptionOverlay.Modulate = Colors.White;
		_bloodAbsorptionOverlay.Color = new Color(0.16f, 0.0f, 0.018f, 0.0f);
		_bloodAbsorptionLabel.Text = "Sanguis Purgatus";
		_bloodAbsorptionLabel.Modulate = Colors.Transparent;
		var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		var essences = new List<Node3D>();
		var startingPositions = new List<Vector3>();
		var spiralPhases = new List<float>();
		foreach (var node in GetTree().GetNodesInGroup("blood_essences"))
		{
			if (node is not Node3D essence)
				continue;
			essences.Add(essence);
			startingPositions.Add(essence.GlobalPosition);
			spiralPhases.Add((float)GD.RandRange(0.0, Mathf.Tau));
			AttachSanguisTrail(essence);
		}
		var screenTween = _bloodAbsorptionLabel.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
		screenTween.TweenProperty(_bloodAbsorptionLabel, "modulate", Colors.White, 0.4f);
		screenTween.TweenInterval(2.55f);
		screenTween.TweenProperty(_bloodAbsorptionLabel, "modulate", Colors.Transparent, 0.5f);
		var vignetteTween = _bloodAbsorptionOverlay.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
		vignetteTween.TweenProperty(_bloodAbsorptionOverlay, "color:a", 0.11f, 0.5f);
		vignetteTween.TweenProperty(_bloodAbsorptionOverlay, "color:a", 0.035f, 2.35f);
		vignetteTween.TweenProperty(_bloodAbsorptionOverlay, "color:a", 0.0f, 0.55f);

		const float absorptionDuration = 3.45f;
		var elapsed = 0.0f;
		while (elapsed < absorptionDuration && player != null && IsInstanceValid(player))
		{
			var delta = Mathf.Max((float)GetProcessDeltaTime(), 0.001f);
			var destination = player.GlobalPosition + Vector3.Up * 0.95f;
			for (var i = 0; i < essences.Count; i++)
			{
				var essence = essences[i];
				if (!IsInstanceValid(essence))
					continue;

				var delay = Mathf.Min(i * 0.024f, 0.42f);
				var progress = Mathf.Clamp((elapsed - delay) / Mathf.Max(absorptionDuration - delay, 0.1f), 0.0f, 1.0f);
				var eased = progress * progress * (3.0f - 2.0f * progress);
				var start = startingPositions[i];
				var planar = destination - start;
				planar.Y = 0.0f;
				var direction = planar.LengthSquared() > 0.001f ? planar.Normalized() : Vector3.Forward;
				var tangent = new Vector3(-direction.Z, 0.0f, direction.X);
				var distance = start.DistanceTo(destination);
				var spiralRadius = Mathf.Sin(progress * Mathf.Pi)
					* Mathf.Min(distance * 0.22f, 1.35f)
					* (1.0f - progress * 0.28f);
				var angle = spiralPhases[i] + progress * Mathf.Tau * 2.25f;
				var spiralOffset = tangent * Mathf.Cos(angle) * spiralRadius
					+ Vector3.Up * Mathf.Sin(angle) * spiralRadius * 0.48f;
				essence.GlobalPosition = start.Lerp(destination, eased) + spiralOffset;
				essence.Scale = Vector3.One * Mathf.Lerp(1.0f, 0.055f, Mathf.Pow(progress, 1.65f));
			}
			elapsed += delta;
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (!IsInstanceValid(this) || !IsInsideTree()) return;
		}
		if (player != null && IsInstanceValid(player))
			SpawnSanguisArrivalPulse(player.GlobalPosition + Vector3.Up * 0.92f);
		await ToSignal(GetTree().CreateTimer(0.42f, true), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || !IsInsideTree()) return;
		foreach (var essence in essences)
			if (IsInstanceValid(essence))
				essence.QueueFree();
		_bloodAbsorptionOverlay.Visible = false;
		_blessingAbsorptionRunning = false;
		// Morreu durante a absorção: a oferta não abre por cima do game over.
		if (_isGameOver) return;
		AppendGameLog("O sangue dos inimigos foi absorvido e condensado em uma Bencao de Sangue.");
		BuildBloodBlessingChoices();
	}

	private static void AttachSanguisTrail(Node3D essence)
	{
		var moteMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.34f, 0.008f, 0.018f, 0.72f),
			EmissionEnabled = true,
			Emission = new Color(0.28f, 0.004f, 0.012f),
			EmissionEnergyMultiplier = 0.85f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		var particleMaterial = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 0.045f,
			Gravity = new Vector3(0.0f, -0.08f, 0.0f),
			InitialVelocityMin = 0.01f,
			InitialVelocityMax = 0.09f,
			ScaleMin = 0.35f,
			ScaleMax = 1.0f,
			Color = new Color(0.72f, 0.035f, 0.05f, 0.78f)
		};
		var trail = new GpuParticles3D
		{
			Name = "SanguisTrail",
			Amount = 14,
			Lifetime = 0.52f,
			Randomness = 0.62f,
			LocalCoords = false,
			ProcessMaterial = particleMaterial,
			DrawPass1 = new SphereMesh
			{
				Radius = 0.026f,
				Height = 0.052f,
				RadialSegments = 6,
				Rings = 3,
				Material = moteMaterial
			},
			VisibilityAabb = new Aabb(new Vector3(-2.0f, -2.0f, -2.0f), Vector3.One * 4.0f)
		};
		essence.AddChild(trail);
		trail.Emitting = true;
	}

	private async void SpawnSanguisArrivalPulse(Vector3 position)
	{
		var scene = GetTree().CurrentScene;
		if (scene == null)
			return;

		var root = new Node3D { Name = "SanguisArrivalPulse" };
		scene.AddChild(root);
		root.GlobalPosition = position;
		var pulseMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.32f, 0.006f, 0.018f, 0.26f),
			EmissionEnabled = true,
			Emission = new Color(0.22f, 0.003f, 0.01f),
			EmissionEnergyMultiplier = 0.8f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		var pulse = new MeshInstance3D
		{
			Name = "AbsorptionPulse",
			Mesh = new SphereMesh
			{
				Radius = 0.42f,
				Height = 0.84f,
				RadialSegments = 16,
				Rings = 8,
				Material = pulseMaterial
			},
			Scale = Vector3.One * 0.08f
		};
		root.AddChild(pulse);
		var pulseTween = root.CreateTween().SetParallel();
		pulseTween.TweenProperty(pulse, "scale", Vector3.One * 1.55f, 0.38f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		pulseTween.TweenProperty(pulse, "transparency", 1.0f, 0.38f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

		var random = new RandomNumberGenerator();
		random.Randomize();
		for (var i = 0; i < 11; i++)
		{
			var angle = random.RandfRange(0.0f, Mathf.Tau);
			var radius = random.RandfRange(0.22f, 0.68f);
			var start = new Vector3(Mathf.Cos(angle) * radius, random.RandfRange(-0.3f, 0.34f), Mathf.Sin(angle) * radius);
			var moteMaterial = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.5f, 0.01f, 0.025f, 0.82f),
				EmissionEnabled = true,
				Emission = new Color(0.38f, 0.006f, 0.018f),
				EmissionEnergyMultiplier = 0.9f,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
			};
			var mote = new MeshInstance3D
			{
				Mesh = new SphereMesh
				{
					Radius = random.RandfRange(0.018f, 0.036f),
					Height = random.RandfRange(0.04f, 0.085f),
					RadialSegments = 6,
					Rings = 3,
					Material = moteMaterial
				},
				Position = start
			};
			root.AddChild(mote);
			var moteTween = root.CreateTween().SetParallel();
			moteTween.TweenProperty(mote, "position", Vector3.Zero, random.RandfRange(0.18f, 0.3f))
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
			moteTween.TweenProperty(mote, "scale", Vector3.One * 0.08f, 0.3f);
			moteTween.TweenProperty(mote, "transparency", 1.0f, 0.32f);
		}

		await ToSignal(GetTree().CreateTimer(0.48f), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(root))
			root.QueueFree();
	}

	// ---------- Oferta (guia §8.17): Window(Large, obrigatória) + ChoiceCard + HintBar ----------

	// Mesma largura máxima dos cards de artefato (guia §3.4).
	private const float BlessingCardWidth = 1080.0f;

	private UiWindow _blessingWindow;
	private RichTextLabel _blessingGlossaryLabel;
	private UiHint _blessingRerollHint;
	private readonly List<UiChoiceCard> _blessingCards = new();
	// Termos de cada card (o glossário mostra só os do card focado/sob o mouse, em 1–2 linhas).
	private readonly List<List<BlessingKeyword>> _blessingCardGlossaries = new();
	private string _blessingOfferFamily = "";
	private bool _blessingAbsorptionRunning;
	private bool _blessingChoiceLocked;
	// Preview (debug): o reroll aparece e só simula (não consome o uso do mapa).
	private bool _blessingOfferPreview;

	private void BuildBloodBlessingOverlay(Control root)
	{
		_blessingWindow = UiComponents.Window(root, "BloodBlessingOverlay", WindowSize.Large, "Hereditas Sanguinis");
		_blessingWindow.Mandatory = true;
		_bloodBlessingOverlay = _blessingWindow.Root;
		_bloodBlessingOverlay.Visible = false;
		_bloodBlessingStatusLabel = _blessingWindow.Status;
		_blessingWindow.SetStatus(Localization.Text("hud.blessing_choice"));

		var scroll = UiComponents.ScrollList(0, out _bloodBlessingList);
		PadChoiceCardScroll(scroll, _bloodBlessingList);
		_blessingWindow.Body.AddChild(scroll);

		// Glossário dos termos destacados no card focado (substitui o [hint], que só funcionava com mouse).
		// Altura fixa de 2 linhas: trocar de card não empurra a lista.
		_blessingGlossaryLabel = new RichTextLabel
		{
			Name = "BlessingGlossary",
			BbcodeEnabled = true, FitContent = false, ScrollActive = false,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(BlessingCardWidth, UiFont.Secondary * 3.0f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Visible = false
		};
		_blessingGlossaryLabel.AddThemeFontSizeOverride("normal_font_size", UiFont.Secondary);
		_blessingGlossaryLabel.AddThemeFontSizeOverride("bold_font_size", UiFont.Secondary);
		_blessingGlossaryLabel.AddThemeColorOverride("default_color", UiPalette.TextSecondary);
		_blessingWindow.Content.AddChild(_blessingGlossaryLabel);
		_blessingWindow.Content.MoveChild(_blessingGlossaryLabel, _blessingWindow.FooterLine.GetIndex());

		// Rodapé sem "Voltar" (oferta obrigatória). Direita: ui_alt (trocar), ui_accept (escolher).
		_blessingRerollHint = _blessingWindow.Footer.Add(UiAction.Alt, "", RerollBlessingOffer);
		_blessingRerollHint.Visible = false;
		_blessingWindow.Footer.Add(UiAction.Accept, UiComponents.TextOr("ui.common.choose", "Escolher"), ChooseFocusedBlessing);
		// ui_alt (R / Y / △): HUD.RegisterModalShortcuts → HandleBlessingOfferShortcut (tecla/controle só chegam ao botão focado).

		_bloodAbsorptionOverlay = new ColorRect
		{
			Name = "BloodAbsorptionOverlay", Color = Colors.Transparent,
			AnchorRight = 1, AnchorBottom = 1, Visible = false, ProcessMode = ProcessModeEnum.Always
		};
		root.AddChild(_bloodAbsorptionOverlay);
		// Banner 34 Gold em caixa alta (antes vermelho de perigo, §2.2).
		_bloodAbsorptionLabel = new Label
		{
			AnchorLeft = 0.2f, AnchorTop = 0.43f, AnchorRight = 0.8f, AnchorBottom = 0.57f,
			Text = "Sanguis Purgatus", ThemeTypeVariation = UiVariation.BannerLabel, Uppercase = true,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		_bloodAbsorptionOverlay.AddChild(_bloodAbsorptionLabel);
	}

	private void BuildBloodBlessingChoices()
	{
		var rng = new RandomNumberGenerator(); rng.Randomize();
		var choices = BloodBlessingCatalog.RollOffers(rng, 3, out var family, RunState.RunBlessings.Count == 0);
		if (choices.Count == 0)
		{
			GetTree().CallGroup("run_manager", "AddBloodCoins", 25);
			AppendGameLog("Todas as bencaos unicas foram encontradas. Sangue convertido em 25 moedas.");
			ClearBlessingCards();
			UiModalStack.Pop(_bloodBlessingOverlay);
			GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
			return;
		}
		PresentBlessingOffers(choices, family);
	}

	// Monta os cards; na primeira vez abre a janela (pausa, obrigatória), no reroll só troca os cards e refoca o mesmo índice.
	private void PresentBlessingOffers(IReadOnlyList<BloodBlessingOffer> choices, string family)
	{
		var wasOpen = UiModalStack.IsOpen(_bloodBlessingOverlay);
		var previousIndex = wasOpen ? _focusedBlessingChoice : 0;
		ClearBlessingCards();
		_blessingChoiceLocked = false;
		_blessingOfferFamily = family ?? "";
		ResetBlessingOfferStatus();

		foreach (var offer in choices)
		{
			var glossary = new List<BlessingKeyword>();
			var card = UiComponents.ChoiceCard(BuildBlessingCardData(offer, glossary), () => SelectBloodBlessing(offer));
			card.Root.TooltipText = FullBlessingTooltip(offer.Definition, UiComponents.RarityLabel(offer.Rarity), offer.PowerMultiplier);
			_bloodBlessingList.AddChild(card.Root);
			var choiceIndex = _blessingChoiceButtons.Count;
			card.ActionButton.FocusEntered += () =>
			{
				_focusedBlessingChoice = choiceIndex;
				UpdateBlessingGlossary(choiceIndex);
			};
			card.Root.MouseEntered += () => UpdateBlessingGlossary(choiceIndex);
			card.Root.MouseExited += () => UpdateBlessingGlossary(_focusedBlessingChoice);
			_blessingCards.Add(card);
			_blessingCardGlossaries.Add(glossary);
			_blessingChoiceButtons.Add(card.ActionButton);
		}
		SetBlessingChoiceNeighbors();
		UpdateBlessingRerollHint();

		_focusedBlessingChoice = Mathf.Clamp(previousIndex, 0, _blessingChoiceButtons.Count - 1);
		UpdateBlessingGlossary(_focusedBlessingChoice);
		if (wasOpen)
		{
			FocusBlessingChoice(_focusedBlessingChoice);
			return;
		}
		UiModalStack.Push(_bloodBlessingOverlay, true, _blessingChoiceButtons[_focusedBlessingChoice], WarnBlessingOfferMandatory, mandatory: true);
		_blessingWindow.FadeIn();
	}

	private ChoiceCardData BuildBlessingCardData(BloodBlessingOffer offer, List<BlessingKeyword> glossary)
	{
		var blessing = offer.Definition;
		var replaced = RunState.GetBlessingInExclusiveGroup(blessing.ExclusiveGroup);
		var description = FormatBloodBlessingDescription(blessing, offer.PowerMultiplier, true);
		CollectBlessingKeywords(description, glossary);
		return new ChoiceCardData
		{
			Icon = LoadBlessingIcon(blessing),
			Title = blessing.DisplayName,
			RarityId = offer.Rarity,
			Meta = FormatBlessingOfferMeta(blessing),
			BodyBbcode = FormatEffectKeywords(description),
			// Aviso de substituição sempre visível (não depende de hover).
			Warning = replaced != null
				? string.Format(UiComponents.TextOr("offer.blessing.replaces", "Substitui: {0} (N{1})"), replaced.Name, replaced.Level)
				: null,
			ActionText = UiComponents.TextOr("ui.common.choose", "Escolher"),
			Width = BlessingCardWidth
		};
	}

	// Meta do card: linhagem · ataque afetado · nível (nova N1, ou evolução N→N+1 se já estiver na run).
	private static string FormatBlessingOfferMeta(BloodBlessingDefinition blessing)
	{
		var parts = new List<string>
		{
			string.Format(UiComponents.TextOr("offer.blessing.lineage", "Linhagem {0}"), FormatBlessingLineage(blessing))
		};
		var attacks = AffectedAttackLabels(blessing);
		if (attacks.Count > 0)
			parts.Add(string.Join(", ", attacks));
		var owned = RunState.RunBlessings.FirstOrDefault(record => record.Id.Equals(blessing.Id, StringComparison.OrdinalIgnoreCase));
		parts.Add(owned == null
			? UiComponents.TextOr("offer.blessing.level_new", "Nova (N1)")
			: string.Format(UiComponents.TextOr("offer.blessing.level_up", "N{0} → N{1}"), owned.Level, owned.Level + 1));
		return string.Join("  ·  ", parts);
	}

	private static List<string> AffectedAttackLabels(BloodBlessingDefinition blessing)
	{
		var labels = new List<string>();
		foreach (var tag in blessing.Tags ?? Array.Empty<string>())
		{
			var label = tag?.ToLowerInvariant() switch
			{
				"basic_attack" => UiComponents.TextOr("ui.attack.basic", "Ataque básico"),
				"quick_attack" => UiComponents.TextOr("ui.attack.quick", "Ataque rápido"),
				"special_attack" => UiComponents.TextOr("ui.attack.special", "Ataque especial"),
				"dash" => UiComponents.TextOr("ui.attack.dash", "Esquiva"),
				_ => null
			};
			if (label != null && !labels.Contains(label))
				labels.Add(label);
		}
		return labels;
	}

	private static Texture2D LoadBlessingIcon(BloodBlessingDefinition blessing)
	{
		if (string.IsNullOrEmpty(blessing.IconPath) || !ResourceLoader.Exists(blessing.IconPath))
			return null;
		return ResourceLoader.Load<Texture2D>(blessing.IconPath);
	}

	// Refresh sem foco preso em nó morto (§5.1 regra 10): RemoveChild + QueueFree.
	private void ClearBlessingCards()
	{
		if (_bloodBlessingList != null)
			foreach (var child in _bloodBlessingList.GetChildren())
			{
				_bloodBlessingList.RemoveChild(child);
				child.QueueFree();
			}
		_blessingCards.Clear();
		_blessingCardGlossaries.Clear();
		_blessingChoiceButtons.Clear();
	}

	// Cima/baixo com wrap pelos vizinhos de foco; esquerda/direita andam para o card anterior/seguinte.
	private void SetBlessingChoiceNeighbors()
	{
		var items = _blessingChoiceButtons.Cast<Control>().ToList();
		ControllerUiSupport.SetVerticalCycle(items);
		foreach (var item in items)
		{
			item.FocusNeighborLeft = item.FocusNeighborTop;
			item.FocusNeighborRight = item.FocusNeighborBottom;
		}
	}

	// Só os termos do card indicado; a área continua reservada (vazia) para não deslocar os cards.
	private void UpdateBlessingGlossary(int index)
	{
		if (_blessingGlossaryLabel == null) return;
		var glossary = index >= 0 && index < _blessingCardGlossaries.Count ? _blessingCardGlossaries[index] : null;
		_blessingGlossaryLabel.Visible = _blessingCardGlossaries.Any(entries => entries.Count > 0);
		_blessingGlossaryLabel.Text = glossary == null ? "" : string.Join("   ·   ", glossary.Select(keyword =>
			$"[color={keyword.Color}][b]{keyword.Term}[/b][/color]: {keyword.Hint}"));
	}

	private void ResetBlessingOfferStatus()
	{
		_blessingWindow.Status.RemoveThemeColorOverride("font_color");
		_blessingWindow.SetStatus(string.IsNullOrEmpty(_blessingOfferFamily)
			? Localization.Text("hud.blessing_choice")
			: string.Format(UiComponents.TextOr("offer.blessing.status", "O sangue revelou a linhagem {0}. Escolha uma Bênção de Sangue."), _blessingOfferFamily));
	}

	// ui_cancel na oferta obrigatória: não fecha; avisa no status (Warn) e mantém o foco no card.
	private void WarnBlessingOfferMandatory()
	{
		_blessingWindow.SetStatus(UiComponents.TextOr("offer.blessing.must_choose", "Escolha uma Bênção de Sangue para liberar a saída."));
		_blessingWindow.Status.AddThemeColorOverride("font_color", UiPalette.Warn);
		FocusBlessingChoice(_focusedBlessingChoice);
	}

	// Atalhos com a oferta no topo da pilha (HUD.RouteModalShortcut): ui_alt = Espelho do Destino.
	private bool HandleBlessingOfferShortcut(InputEvent input)
	{
		if (input.IsEcho() || !InputMap.HasAction("ui_alt") || !input.IsActionPressed("ui_alt")) return false;
		if (_blessingRerollHint != null && _blessingRerollHint.Visible && !_blessingRerollHint.Button.Disabled)
			RerollBlessingOffer();
		return true;
	}

	// "Escolher" do rodapé: só escolhe o card que tem o foco; sem card focado, foca o primeiro (não escolhe às cegas).
	private void ChooseFocusedBlessing()
	{
		if (_blessingCards.Count == 0 || UiModalStack.Top != _bloodBlessingOverlay) return;
		var focused = GetViewport().GuiGetFocusOwner();
		var index = focused is Button button ? _blessingChoiceButtons.IndexOf(button) : -1;
		if (index < 0)
		{
			FocusBlessingChoice(0);
			return;
		}
		_blessingCards[index].Choose();
	}

	private void FocusBlessingChoice(int index)
	{
		if (_blessingChoiceButtons.Count == 0) return;
		_focusedBlessingChoice = Mathf.Clamp(index, 0, _blessingChoiceButtons.Count - 1);
		_blessingChoiceButtons[_focusedBlessingChoice].GrabFocus();
	}

	// Reroll = skill "Espelho do Destino" (RewardOfferReroll): sem custo em moedas, 1 uso por mapa (RunState).
	// Sem a skill a dica some; usada no mapa, fica visível e desabilitada com "(0)".
	private void UpdateBlessingRerollHint()
	{
		if (_blessingRerollHint == null) return;
		var hasSkill = _blessingOfferPreview || HasRewardRerollSkill();
		_blessingRerollHint.Visible = hasSkill;
		if (!hasSkill) return;
		var remaining = _blessingOfferPreview || (GetPlayerSkillController()?.CanRerollReward() ?? false) ? 1 : 0;
		_blessingRerollHint.SetLabel(string.Format(UiComponents.TextOr("offer.reroll_count", "Trocar ({0})"), remaining));
		_blessingRerollHint.Button.Disabled = remaining == 0;
		_blessingRerollHint.Button.TooltipText = remaining > 0
			? UiComponents.TextOr("offer.reroll_tooltip", "Espelho do Destino: troca a oferta sem custo, uma vez por mapa.")
			: UiComponents.TextOr("offer.reroll_used", "Espelho do Destino já usado neste mapa.");
	}

	// Só leitura do catálogo/ficha: distingue "sem a skill" (oculta) de "usada neste mapa" (desabilitada).
	private static bool HasRewardRerollSkill()
	{
		foreach (var skillId in CharacterSheet.AcquiredSkills)
		{
			if (!SkillCatalog.TryGet(skillId, out var skill) || !skill.IsImplemented) continue;
			foreach (var effect in skill.Effects)
				if (effect.Type.Equals("RewardOfferReroll", StringComparison.OrdinalIgnoreCase))
					return true;
		}
		return false;
	}

	private void RerollBlessingOffer()
	{
		if (_blessingChoiceLocked || UiModalStack.Top != _bloodBlessingOverlay) return;
		if (!_blessingOfferPreview && GetPlayerSkillController()?.TryConsumeRewardReroll() != true) return;
		BuildBloodBlessingChoices();
		if (UiModalStack.IsOpen(_bloodBlessingOverlay))
			_blessingWindow.SetStatus(UiComponents.TextOr("offer.blessing.rerolled", "O Espelho do Destino revelou uma nova linhagem."));
	}

	private PlayerSkillController GetPlayerSkillController()
	{
		return _runtimePlayerController?.GetNodeOrNull<PlayerSkillController>("PlayerSkillController")
			?? GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<PlayerSkillController>("PlayerSkillController");
	}

	private static string FormatBlessingLineage(BloodBlessingDefinition blessing)
	{
		return blessing.ParentFamilies.Length > 0
			? $"{blessing.Family}: {string.Join(" + ", blessing.ParentFamilies)}"
			: blessing.Family;
	}

	private void SelectBloodBlessing(BloodBlessingOffer offer)
	{
		// Só escolhe com a oferta no topo (nada de escolha às cegas com outra tela por cima).
		if (_blessingChoiceLocked || UiModalStack.Top != _bloodBlessingOverlay) return;
		var blessing = offer.Definition;
		if (!RunState.AddBloodBlessing(offer)) return;
		_blessingChoiceLocked = true;
		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		GetTree().CallGroup("audio_manager", "PlayEffect", "blessing_select");
		AppendGameLog($"Bencao adquirida: {blessing.DisplayName} [{offer.Rarity}].");
		ShowNotificationToast($"{UiComponents.RarityLabel(offer.Rarity)}: {blessing.DisplayName}", true, LoadBlessingIcon(blessing));
		// Os cards ficam até a próxima oferta (removê-los aqui seria no meio do sinal do próprio botão/card).
		UiModalStack.Pop(_bloodBlessingOverlay);
		RefreshCharacterSheet();
		RefreshBlessingHud();
		GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
	}

	// Preview (debug, --ui-preview=blessing_offer | blessing_offer_replace). Nada é salvo; a run é volátil.
	internal void PreviewBlessingOffer(bool withReplacement)
	{
		_blessingOfferPreview = true;
		if (!withReplacement)
		{
			OpenMandatoryBloodBlessingOffer();
			return;
		}
		// Uma bênção do grupo exclusivo já na run e três do mesmo grupo na oferta: todos os cards mostram "Substitui".
		var group = BloodBlessingCatalog.All
			.Where(definition => !string.IsNullOrWhiteSpace(definition.ExclusiveGroup))
			.GroupBy(definition => definition.ExclusiveGroup)
			.OrderByDescending(entries => entries.Count())
			.FirstOrDefault()?.ToList();
		if (group == null || group.Count < 2)
		{
			OpenMandatoryBloodBlessingOffer();
			return;
		}
		if (RunState.GetBlessingInExclusiveGroup(group[0].ExclusiveGroup) == null)
			RunState.AddBloodBlessing(group[0]);
		var rarities = BloodBlessingCatalog.AllRarities;
		var offers = new List<BloodBlessingOffer>();
		for (var i = 1; i < group.Count && offers.Count < 3; i++)
		{
			var rarity = rarities.Count > 0 ? rarities[(i * 2) % rarities.Count] : new BloodBlessingRarity("common", "Common", 1.0f, 1.0f);
			offers.Add(new BloodBlessingOffer(group[i], rarity.DisplayName, rarity.PowerMultiplier));
		}
		PresentBlessingOffers(offers, group[1].Family);
	}

	// highlightValues: números calculados em negrito Gold (só na oferta, BBCode). Sem destaque, o texto sai puro (tooltips).
	private static string FormatBloodBlessingDescription(BloodBlessingDefinition blessing, float effectivePowerMultiplier, bool highlightValues = false)
	{
		var description = blessing.Description;
		// Com destaque, o valor entra como marcador sem dígitos e só vira BBCode no fim (o hex da cor não casa com o próximo número).
		var highlighted = highlightValues ? new List<string>() : null;
		for (var i = 0; i < blessing.Effects.Length; i++)
		{
			var effect = blessing.Effects[i];
			if (effect.BooleanValue || effect.Operation is "Set" or "SetBool")
				continue;

			var baseValue = CompendiumTuning.GetFloat($"blessing.{blessing.Id}.effect.{i}.value", effect.Value);
			var percentValue = effect.Value * 100.0f;
			var percentPattern = NumberPattern(percentValue) + @"\s*%";
			var usesPercent = effect.Operation.Contains("Multiplier", StringComparison.OrdinalIgnoreCase)
				|| effect.Type.Contains("Chance", StringComparison.OrdinalIgnoreCase)
				|| Regex.IsMatch(description, percentPattern, RegexOptions.IgnoreCase);
			var sourcePattern = usesPercent ? percentPattern : NumberPattern(effect.Value);
			var effectiveValue = GetDisplayedBlessingValue(effect, baseValue * effectivePowerMultiplier);
			if (effect.Type == "MaxHealth" && effect.Operation == "AddFlat")
				effectiveValue *= RunState.MaxHealthMultiplier * RunState.SacrificedMaxHealthMultiplier;
			var replacement = usesPercent
				? $"{FormatLocalizedNumber(effectiveValue * 100.0f)}%"
				: FormatLocalizedNumber(effectiveValue);
			if (highlighted != null)
			{
				highlighted.Add(replacement);
				replacement = BlessingValueMarker(highlighted.Count - 1);
			}
			description = ReplaceFirst(description, sourcePattern, replacement);
		}
		if (highlighted == null)
			return description;
		var gold = UiPalette.Gold.ToHtml(false);
		for (var i = 0; i < highlighted.Count; i++)
			description = description.Replace(BlessingValueMarker(i), $"[b][color=#{gold}]{highlighted[i]}[/color][/b]");
		return description;
	}

	// Marcador sem dígitos nem letras comuns em texto: não casa com NumberPattern nem com as palavras-chave.
	private static string BlessingValueMarker(int index) => $"\u0001{(char)('a' + index)}\u0002";

	private static float GetDisplayedBlessingValue(BloodBlessingEffectDefinition effect, float value)
	{
		if (effect.Type.Contains("Chance", StringComparison.OrdinalIgnoreCase))
			return Mathf.Clamp(value, 0.0f, 1.0f);
		if (effect.Type == "PestilenceSpreadTargets")
			return Mathf.Round(value);
		return value;
	}

	private static string NumberPattern(float value)
	{
		var invariant = value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
		return $@"(?<![\d.,]){Regex.Escape(invariant).Replace(@"\.", @"[.,]")}(?![\d.,])";
	}

	private static string FormatLocalizedNumber(float value)
	{
		return value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
	}

	private static string ReplaceFirst(string text, string pattern, string replacement)
	{
		var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
		if (!match.Success)
			return text;
		return text[..match.Index] + replacement + text[(match.Index + match.Length)..];
	}

	// Termo destacado nas descrições (cores mantidas, guia §9) e explicação usada no [hint] e no glossário da oferta.
	private sealed record BlessingKeyword(string Term, string Color, bool Bold, string Hint);

	private static BlessingKeyword ResolveAttackKeyword(string match)
	{
		var lower = match.ToLowerInvariant();
		if (lower.Contains("básic"))
			return new BlessingKeyword(UiComponents.TextOr("ui.attack.basic", "Ataque básico"), "#ffd166", true,
				UiComponents.TextOr("hud.glossary.basic_attack", "Ataque principal; encadeia o combo da arma equipada."));
		if (lower.Contains("rápid"))
			return new BlessingKeyword(UiComponents.TextOr("ui.attack.quick", "Ataque rápido"), "#72e6ff", true,
				UiComponents.TextOr("hud.glossary.quick_attack", "Golpe executado ao atacar durante a janela da esquiva."));
		if (lower.Contains("especial"))
			return new BlessingKeyword(UiComponents.TextOr("ui.attack.special", "Ataque especial"), "#d58cff", true,
				UiComponents.TextOr("hud.glossary.special_attack", "Forma especial do ataque da arma; interrompe o combo básico."));
		return new BlessingKeyword(UiComponents.TextOr("hud.glossary.fury.term", "Fúria Bestial"), "#ff4d5f", true,
			UiComponents.TextOr("hud.glossary.fury", "Estado que consome a barra de Fúria para ampliar o poder ofensivo."));
	}

	private static BlessingKeyword ResolveStatusKeyword(string match)
	{
		var lower = match.ToLowerInvariant();
		if (lower.StartsWith("sangr"))
			return new BlessingKeyword(UiComponents.TextOr("hud.glossary.bleed.term", "Sangramento"), "#ff4055", false,
				UiComponents.TextOr("hud.glossary.bleed", "Causa dano periódico cumulativo em um único inimigo."));
		if (lower.StartsWith("pest"))
			return new BlessingKeyword(UiComponents.TextOr("hud.glossary.pestilence.term", "Pestilência"), "#78d957", false,
				UiComponents.TextOr("hud.glossary.pestilence", "Cada acúmulo aumenta o dano recebido; ao morrer, a infecção pode se espalhar."));
		if (lower.StartsWith("barreira"))
			return new BlessingKeyword(UiComponents.TextOr("hud.glossary.barrier.term", "Barreira"), "#63c7ff", false,
				UiComponents.TextOr("hud.glossary.barrier", "Vida temporária consumida antes da vida normal."));
		if (lower.StartsWith("fasc"))
			return new BlessingKeyword(UiComponents.TextOr("hud.glossary.fascination.term", "Fascínio"), "#ff6fe8", false,
				UiComponents.TextOr("hud.glossary.fascination", "Chefes e inimigos imunes a Encanto sofrem 25% mais dano."));
		if (lower.StartsWith("hosp"))
			return new BlessingKeyword(UiComponents.TextOr("hud.glossary.host.term", "Hospedeiro"), "#b4ff6a", false,
				UiComponents.TextOr("hud.glossary.host", "Recebem mais dano e espalham um acúmulo de Pestilência em área ao serem atingidos."));
		if (lower.StartsWith("interromp"))
			return new BlessingKeyword(UiComponents.TextOr("hud.glossary.interrupt.term", "Interrupção"), "#ffd166", false,
				UiComponents.TextOr("hud.glossary.interrupt", "Interrupção impede temporariamente a reação ou o ataque."));
		if (lower.StartsWith("ferida"))
			return new BlessingKeyword(UiComponents.TextOr("hud.glossary.wound.term", "Ferida recuperável"), "#ff9f9f", false,
				UiComponents.TextOr("hud.glossary.wound", "Parte do dano é restaurada gradualmente se você não sofrer outro golpe."));
		return new BlessingKeyword(UiComponents.TextOr("hud.glossary.charm.term", "Encanto"), "#dd77ff", false,
			UiComponents.TextOr("hud.glossary.charm", "O inimigo passa a atacar outros inimigos; sem alvo, vagueia pela sala."));
	}

	private static readonly Regex AttackKeywordRegex = new(@"\b(Ataques? básicos?|Ataques? rápidos?|Ataques? especiais?|Fúria Bestial)\b", RegexOptions.IgnoreCase);
	private static readonly Regex StatusKeywordRegex = new(@"\b(Sangramento|Sangrando|Pestilência|Pestilento|Pestilenta|Encanto|Encantar|Encantado|Encantada|Encantados|Encantadas|Barreira|Fascínio|Hospedeiro|Hospedeiros|Interrompido|Interrompida|Ferida recuperável)\b", RegexOptions.IgnoreCase);

	private static string FormatEffectKeywords(string text)
	{
		text = AttackKeywordRegex.Replace(text, match => FormatKeyword(match.Value, ResolveAttackKeyword(match.Value)));
		return StatusKeywordRegex.Replace(text, match => FormatKeyword(match.Value, ResolveStatusKeyword(match.Value)));
	}

	// Sem [hint]: o texto do card não recebe mouse e o glossário da oferta já explica o termo (§8.17).
	private static string FormatKeyword(string word, BlessingKeyword keyword)
	{
		var body = keyword.Bold ? $"[b]{word}[/b]" : word;
		return $"[color={keyword.Color}]{body}[/color]";
	}

	// Termos presentes no texto, em ordem de aparição e sem repetir (glossário da oferta).
	private static void CollectBlessingKeywords(string text, List<BlessingKeyword> glossary)
	{
		var found = new List<(int Index, BlessingKeyword Keyword)>();
		foreach (Match match in AttackKeywordRegex.Matches(text))
			found.Add((match.Index, ResolveAttackKeyword(match.Value)));
		foreach (Match match in StatusKeywordRegex.Matches(text))
			found.Add((match.Index, ResolveStatusKeyword(match.Value)));
		foreach (var entry in found.OrderBy(item => item.Index))
			if (!glossary.Any(existing => existing.Term == entry.Keyword.Term))
				glossary.Add(entry.Keyword);
	}

	private string FullBlessingTooltip(BloodBlessingDefinition blessing, string rarity, float powerMultiplier, int level = 1)
	{
		return $"{blessing.DisplayName} [{rarity}] — N{level}\n{FormatBloodBlessingDescription(blessing, powerMultiplier)}\n\n{blessing.FlavorText}";
	}
}
