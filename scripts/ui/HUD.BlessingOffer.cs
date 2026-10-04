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

	private async void OpenBloodBlessingOfferInternal(bool mandatory)
	{
		if (_bloodBlessingOverlay == null) return;
		if (mandatory)
		{
			AppendGameLog("A fonte primordial revelou a primeira Bencao de Sangue desta run.");
			BuildBloodBlessingChoices();
			return;
		}
		GetTree().Paused = false;
		_bloodAbsorptionOverlay.Visible = true;
		_bloodAbsorptionOverlay.Modulate = Colors.White;
		_bloodAbsorptionOverlay.Color = new Color(0.16f, 0.0f, 0.018f, 0.0f);
		_bloodAbsorptionLabel.Text = "SANGUIS PURGATUS";
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
		}
		if (player != null && IsInstanceValid(player))
			SpawnSanguisArrivalPulse(player.GlobalPosition + Vector3.Up * 0.92f);
		await ToSignal(GetTree().CreateTimer(0.42f, true), SceneTreeTimer.SignalName.Timeout);
		foreach (var essence in essences)
			if (IsInstanceValid(essence))
				essence.QueueFree();
		_bloodAbsorptionOverlay.Visible = false;
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

	private void BuildBloodBlessingOverlay(Control root)
	{
		_bloodBlessingOverlay = new ColorRect
		{
			Name = "BloodBlessingOverlay", Color = new Color(0.025f, 0.0f, 0.008f, 0.95f),
			AnchorLeft = 0.5f, AnchorTop = 0.5f, AnchorRight = 0.5f, AnchorBottom = 0.5f,
			OffsetLeft = -620, OffsetTop = -340, OffsetRight = 620, OffsetBottom = 340,
			Visible = false, ProcessMode = ProcessModeEnum.Always
		};
		root.AddChild(_bloodBlessingOverlay);
		UIThemeFactory.AddSimpleFrame(
			_bloodBlessingOverlay,
			"BlessingChoiceFrame",
			new Vector4(0.0f, 0.0f, 1.0f, 1.0f),
			Vector4.Zero,
			new Color(0.12f, 0.055f, 0.07f, 0.96f),
			12);
		_bloodAbsorptionOverlay = new ColorRect
		{
			Name = "BloodAbsorptionOverlay", Color = Colors.Transparent,
			AnchorRight = 1, AnchorBottom = 1, Visible = false, ProcessMode = ProcessModeEnum.Always
		};
		root.AddChild(_bloodAbsorptionOverlay);
		_bloodAbsorptionLabel = new Label
		{
			AnchorLeft = 0.2f, AnchorTop = 0.43f, AnchorRight = 0.8f, AnchorBottom = 0.57f,
			Text = "SANGUIS PURGATUS", HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};
		_bloodAbsorptionLabel.AddThemeFontSizeOverride("font_size", 32);
		_bloodAbsorptionLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.02f, 0.05f));
		_bloodAbsorptionOverlay.AddChild(_bloodAbsorptionLabel);

		var title = new Label
		{
			Text = "Hereditas Sanguinis", AnchorRight = 1, OffsetTop = 18, OffsetBottom = 58,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		title.AddThemeFontSizeOverride("font_size", 27);
		_bloodBlessingOverlay.AddChild(title);

		_bloodBlessingStatusLabel = new Label
		{
			Text = Localization.Text("hud.blessing_choice"), AnchorRight = 1, OffsetTop = 62, OffsetBottom = 100,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		_bloodBlessingOverlay.AddChild(_bloodBlessingStatusLabel);

		_blessingRerollButton = new Button
		{
			Text = "Trocar oferta",
			OffsetLeft = 980.0f, OffsetTop = 68.0f, OffsetRight = 1180.0f, OffsetBottom = 106.0f
		};
		_blessingRerollButton.Pressed += RerollBlessingOffer;
		_bloodBlessingOverlay.AddChild(_blessingRerollButton);

		_bloodBlessingList = new VBoxContainer
		{
			OffsetLeft = 56, OffsetTop = 116, OffsetRight = 1184, OffsetBottom = 640
		};
		_bloodBlessingOverlay.AddChild(_bloodBlessingList);
	}

	private void BuildBloodBlessingChoices()
	{
		foreach (var child in _bloodBlessingList.GetChildren()) child.QueueFree();
		_blessingChoiceButtons.Clear();
		_focusedBlessingChoice = 0;
		var rng = new RandomNumberGenerator(); rng.Randomize();
		var choices = BloodBlessingCatalog.RollOffers(rng, 3, out var family, RunState.RunBlessings.Count == 0);
		if (_blessingRerollButton != null)
			_blessingRerollButton.Disabled = !(GetPlayerSkillController()?.CanRerollReward() ?? false);
		if (choices.Count == 0)
		{
			GetTree().CallGroup("run_manager", "AddBloodCoins", 25);
			AppendGameLog("Todas as bencaos unicas foram encontradas. Sangue convertido em 25 moedas.");
			_bloodBlessingOverlay.Visible = false;
			GetTree().Paused = false;
			GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
			return;
		}

		_bloodBlessingOverlay.Visible = true;
		GetTree().Paused = true;
		_bloodBlessingStatusLabel.Text = $"O sangue revelou a familia {family}. Escolha uma bencao unica:";
		foreach (var offer in choices)
		{
			var blessing = offer.Definition;
			var replaced = RunState.GetBlessingInExclusiveGroup(blessing.ExclusiveGroup);
			var replacementWarning = replaced != null ? $"\n[color=#ffb347]SUBSTITUI: {replaced.Name}[/color]" : "";
			var card = new PanelContainer
			{
				CustomMinimumSize = new Vector2(1080, 125),
				MouseDefaultCursorShape = Control.CursorShape.PointingHand
			};
			card.AddThemeStyleboxOverride("panel", UIThemeFactory.ChoiceCard(BlessingRarityColor(offer.Rarity)));
			var content = new RichTextLabel
			{
				BbcodeEnabled = true, FitContent = true, ScrollActive = false,
				Text = $"[font_size=22][b]{blessing.DisplayName}[/b] | {FormatBlessingLineage(blessing)} | {offer.Rarity}[/font_size]\n{FormatEffectKeywords(FormatBloodBlessingDescription(blessing, offer.PowerMultiplier))}{replacementWarning}",
				CustomMinimumSize = new Vector2(1040, 115),
				MouseDefaultCursorShape = Control.CursorShape.PointingHand,
				TooltipText = replaced != null ? $"Ao escolher, substitui {replaced.Name}." : FullBlessingTooltip(blessing, offer.Rarity, offer.PowerMultiplier)
			};
			content.GuiInput += input =>
			{
				if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) SelectBloodBlessing(offer);
			};
			content.MouseEntered += () =>
			{
				if (replaced != null) _bloodBlessingStatusLabel.Text = $"ATENÇÃO: esta escolha substituirá {replaced.Name}.";
			};
			content.MouseExited += () => _bloodBlessingStatusLabel.Text = $"O sangue revelou a familia {family}. Escolha uma bencao unica:";
			var row = new HBoxContainer();
			row.AddChild(new TextureRect
			{
				Texture = ResourceLoader.Load<Texture2D>(blessing.IconPath),
				CustomMinimumSize = new Vector2(92, 92),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				MouseFilter = Control.MouseFilterEnum.Ignore
			});
			content.CustomMinimumSize = new Vector2(790, 115);
			row.AddChild(content);
			var chooseButton = new Button
			{
				Text = "Escolher",
				CustomMinimumSize = new Vector2(135.0f, 82.0f),
				SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
			};
			chooseButton.Pressed += () => SelectBloodBlessing(offer);
			var choiceIndex = _blessingChoiceButtons.Count;
			chooseButton.FocusEntered += () => _focusedBlessingChoice = choiceIndex;
			_blessingChoiceButtons.Add(chooseButton);
			row.AddChild(chooseButton);
			card.AddChild(row);
			_bloodBlessingList.AddChild(card);
		}
		FocusBlessingChoice(0);
	}

	private void FocusBlessingChoice(int index)
	{
		if (_blessingChoiceButtons.Count == 0) return;
		_focusedBlessingChoice = Mathf.Clamp(index, 0, _blessingChoiceButtons.Count - 1);
		_blessingChoiceButtons[_focusedBlessingChoice].GrabFocus();
	}

	private void RerollBlessingOffer()
	{
		if (GetPlayerSkillController()?.TryConsumeRewardReroll() != true) return;
		BuildBloodBlessingChoices();
		_bloodBlessingStatusLabel.Text = "O Espelho do Destino revelou uma nova linhagem.";
	}

	private PlayerSkillController GetPlayerSkillController()
	{
		return _runtimePlayerController?.GetNodeOrNull<PlayerSkillController>("PlayerSkillController")
			?? GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<PlayerSkillController>("PlayerSkillController");
	}

	private static Color BlessingRarityColor(string rarity) => rarity.ToLowerInvariant() switch
	{
		"stirps" => new Color(1.0f, 0.08f, 0.22f),
		"uncommon" => new Color(0.2f, 0.9f, 0.3f),
		"rare" => new Color(0.2f, 0.48f, 1.0f),
		"epic" => new Color(0.68f, 0.25f, 0.95f),
		"legendary" => new Color(1.0f, 0.48f, 0.08f),
		_ => Colors.White
	};

	private static string FormatBlessingLineage(BloodBlessingDefinition blessing)
	{
		return blessing.ParentFamilies.Length > 0
			? $"{blessing.Family}: {string.Join(" + ", blessing.ParentFamilies)}"
			: blessing.Family;
	}

	private void SelectBloodBlessing(BloodBlessingOffer offer)
	{
		var blessing = offer.Definition;
		if (!RunState.AddBloodBlessing(offer)) return;
		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		GetTree().CallGroup("audio_manager", "PlayEffect", "blessing_select");
		AppendGameLog($"Bencao adquirida: {blessing.DisplayName} [{offer.Rarity}].");
		ShowEnemyDefeatedToast($"{offer.Rarity}: {blessing.DisplayName}");
		_bloodBlessingOverlay.Visible = false;
		GetTree().Paused = false;
		RefreshCharacterSheet();
		RefreshBlessingHud();
		GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
	}

	private string FormatBloodBlessingEffects(BloodBlessingDefinition blessing, float powerMultiplier = 1.0f)
	{
		var text = "Efeitos: ";
		for (var i = 0; i < blessing.Effects.Length; i++)
		{
			var effect = blessing.Effects[i];
			var value = effect.BooleanValue ? 1.0f : GetDisplayedBlessingValue(effect, effect.Value * powerMultiplier);
			text += $"{ArtifactEffectRegistry.FriendlyName(effect.Type)} {FormatArtifactOperation(effect.Operation, value)}";
			if (i < blessing.Effects.Length - 1) text += " | ";
		}
		return text;
	}

	private static string FormatBloodBlessingDescription(BloodBlessingDefinition blessing, float effectivePowerMultiplier)
	{
		var description = blessing.Description;
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
			description = ReplaceFirst(description, sourcePattern, replacement);
		}
		return description;
	}

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

	private static string FormatEffectKeywords(string text)
	{
		text = Regex.Replace(text, @"\b(Ataques? básicos?|Ataques? rápidos?|Ataques? especiais?|Fúria Bestial)\b", match =>
		{
			var lower = match.Value.ToLowerInvariant();
			if (lower.Contains("básic"))
				return $"[hint=\"Ataque principal no botão esquerdo; encadeia o combo da arma equipada.\"][color=#ffd166][b]{match.Value}[/b][/color][/hint]";
			if (lower.Contains("rápid"))
				return $"[hint=\"Golpe executado ao atacar durante a janela da esquiva.\"][color=#72e6ff][b]{match.Value}[/b][/color][/hint]";
			if (lower.Contains("especial"))
				return $"[hint=\"Ataque da arma no Q; interrompe o combo básico e usa a forma especial da arma.\"][color=#d58cff][b]{match.Value}[/b][/color][/hint]";
			return $"[hint=\"Estado ativado no F quando desbloqueado; consome a barra de Fúria para ampliar o poder ofensivo.\"][color=#ff4d5f][b]{match.Value}[/b][/color][/hint]";
		}, RegexOptions.IgnoreCase);

		return Regex.Replace(text, @"\b(Sangramento|Sangrando|Pestilência|Pestilento|Pestilenta|Encanto|Encantar|Encantado|Encantada|Encantados|Encantadas|Barreira|Fascínio|Hospedeiro|Hospedeiros|Interrompido|Interrompida|Ferida recuperável)\b", match =>
		{
			var lower = match.Value.ToLowerInvariant();
			if (lower.StartsWith("sangr"))
				return $"[hint=\"Causa dano periódico cumulativo em um único inimigo.\"][color=#ff4055]{match.Value}[/color][/hint]";
			if (lower.StartsWith("pest"))
				return $"[hint=\"Cada acúmulo aumenta o dano recebido; ao morrer, a infecção pode se espalhar.\"][color=#78d957]{match.Value}[/color][/hint]";
			if (lower.StartsWith("barreira"))
				return $"[hint=\"Vida temporária consumida antes da vida normal.\"][color=#63c7ff]{match.Value}[/color][/hint]";
			if (lower.StartsWith("fasc"))
				return $"[hint=\"Chefes e inimigos imunes a Encanto sofrem 25% mais dano.\"][color=#ff6fe8]{match.Value}[/color][/hint]";
			if (lower.StartsWith("hosp"))
				return $"[hint=\"Recebem mais dano e espalham um acúmulo de Pestilência em área ao serem atingidos.\"][color=#b4ff6a]{match.Value}[/color][/hint]";
			if (lower.StartsWith("interromp"))
				return $"[hint=\"Interrupção impede temporariamente a reação ou o ataque.\"][color=#ffd166]{match.Value}[/color][/hint]";
			if (lower.StartsWith("ferida"))
				return $"[hint=\"Parte do dano é restaurada gradualmente se você não sofrer outro golpe.\"][color=#ff9f9f]{match.Value}[/color][/hint]";
			return $"[hint=\"O inimigo passa a atacar outros inimigos; sem alvo, vagueia pela sala.\"][color=#dd77ff]{match.Value}[/color][/hint]";
		}, RegexOptions.IgnoreCase);
	}

	private string FullBlessingTooltip(BloodBlessingDefinition blessing, string rarity, float powerMultiplier, int level = 1)
	{
		return $"{blessing.DisplayName} [{rarity}] — N{level}\n{FormatBloodBlessingDescription(blessing, powerMultiplier)}\n\n{blessing.FlavorText}";
	}
}
