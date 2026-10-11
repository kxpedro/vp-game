using Godot;
using System;
using System.Collections.Generic;

// Oferta de artefatos (docs/design/ui-guia.md §3.4, §8.16 e decisão D1).
// Obrigatória: entra na UiModalStack como modal não cancelável (Esc/B/Start só mostram o aviso), sem "Decidir depois".
// As opções sorteadas ficam guardadas por fonte (RoomArtifact) até a escolha: reabrir não re-sorteia nem chama
// ArtifactCatalog.RollChoices de novo (que também marca descobertas no diário). O reroll pago ("Espelho do Destino")
// continua, via ui_alt (dica do rodapé: oculta sem a skill; "(0)" desabilitada depois de usada no mapa, como na bênção).
public partial class HUD
{
	private const string ArtifactGenericIconPath = "res://images/generic.png";

	private UiWindow _artifactOfferWindow;
	private VBoxContainer _artifactOfferList;
	private UiHint _artifactRerollHint;
	private RoomArtifact _pendingArtifactSource;
	private ulong _artifactOfferSourceKey;
	// Opções sorteadas por fonte (GetInstanceId do RoomArtifact; 0 = sem fonte/preview), até a escolha.
	private readonly Dictionary<ulong, List<ArtifactDefinition>> _artifactOfferChoices = new();
	private readonly List<UiChoiceCard> _artifactOfferCards = new();
	// Preview (--ui-preview=artifact_offer|artifact_offer_4): opções fixas do catálogo, sem sorteio nem diário.
	private bool _artifactOfferPreviewMode;

	public void OpenArtifactOffer(RoomArtifact source)
	{
		if (_artifactOfferOverlay == null)
			return;

		var key = ArtifactOfferKey(source);
		// Já aberta para a mesma fonte (interact relido): mantém opções e foco.
		if (UiModalStack.IsOpen(_artifactOfferOverlay) && key == _artifactOfferSourceKey)
			return;

		_artifactOfferPreviewMode = false;
		ShowArtifactOffer(source, key, null);
	}

	private void BuildArtifactOfferOverlay(Control root)
	{
		_artifactOfferWindow = UiComponents.Window(root, "ArtifactOfferOverlay", WindowSize.Large,
			Localization.Text("offer.artifact.title"));
		_artifactOfferOverlay = _artifactOfferWindow.Root;
		_artifactOfferOverlay.Visible = false;
		_artifactOfferWindow.Mandatory = true;

		var scroll = UiComponents.ScrollList(0.0f, out _artifactOfferList);
		PadChoiceCardScroll(scroll, _artifactOfferList);
		scroll.Name = "ArtifactOfferScroll";
		_artifactOfferWindow.Body.AddChild(scroll);

		// Obrigatória: sem dica de "Voltar". Trocar (ui_alt) só aparece com a skill.
		var footer = _artifactOfferWindow.Footer;
		_artifactRerollHint = footer.Add(UiAction.Alt, "", RerollArtifactOffer);
		_artifactRerollHint.Visible = false;
		footer.Add(UiAction.Accept, Localization.Text("ui.common.choose"));
		// ui_alt (R / Y / △): HUD.RegisterModalShortcuts → HandleArtifactOfferShortcut (tecla/controle só chegam ao botão focado).
	}

	// Atalhos com a oferta no topo da pilha (HUD.RouteModalShortcut): ui_alt = Espelho do Destino.
	private bool HandleArtifactOfferShortcut(InputEvent input)
	{
		if (input.IsEcho() || !InputMap.HasAction("ui_alt") || !input.IsActionPressed("ui_alt"))
			return false;
		if (_artifactRerollHint != null && _artifactRerollHint.Visible && !_artifactRerollHint.Button.Disabled)
			RerollArtifactOffer();
		return true;
	}

	private void ShowArtifactOffer(RoomArtifact source, ulong key, List<ArtifactDefinition> fixedChoices)
	{
		PruneArtifactOfferChoices();
		if (!_artifactOfferChoices.TryGetValue(key, out var choices))
		{
			choices = fixedChoices ?? RollArtifactOfferChoices();
			if (choices.Count == 0)
			{
				// Catálogo esgotado: libera a saída como antes, sem abrir a tela.
				GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
				return;
			}
			_artifactOfferChoices[key] = choices;
		}

		_pendingArtifactSource = source;
		_artifactOfferSourceKey = key;
		_artifactOfferWindow.SetStatus(_artifactOfferPreviewMode
			? "Prévia: escolher e trocar só simulam; nada é gravado."
			: Localization.Text("offer.artifact.status"));
		RebuildArtifactOfferCards(choices);
		RefreshArtifactRerollHint();
		// Modal não cancelável (D1): ui_cancel só avisa; a pilha refoca dentro da oferta.
		UiModalStack.Push(_artifactOfferOverlay, pauses: true, initialFocus: FirstArtifactOfferTarget(),
			onCancel: WarnArtifactOfferMandatory, mandatory: true);
		_artifactOfferWindow.FadeIn();
	}

	// Margem interna na lista de cards: o anel de foco (expand 3 + brilho) do card não é cortado pelo ScrollContainer.
	// Usado também pela oferta de bênção e pela bancada.
	private static void PadChoiceCardScroll(ScrollContainer scroll, VBoxContainer list)
	{
		scroll.RemoveChild(list);
		var margin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
		foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
			margin.AddThemeConstantOverride(side, UiMetrics.FocusExpand + 6);
		margin.AddChild(list);
		scroll.AddChild(margin);
	}

	private List<ArtifactDefinition> RollArtifactOfferChoices()
	{
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		// "Ás das Relíquias" (humanitas.collector_eye): chance de uma quarta opção.
		var extraChoiceChance = GetArtifactOfferSkillController()?.ArtifactDropChanceBonus() ?? 0.0f;
		var choiceCount = rng.Randf() < extraChoiceChance ? 4 : 3;
		return ArtifactCatalog.RollChoices(rng, choiceCount, BuildExcludedArtifactOfferIds());
	}

	private void RebuildArtifactOfferCards(List<ArtifactDefinition> choices)
	{
		foreach (var child in _artifactOfferList.GetChildren())
		{
			_artifactOfferList.RemoveChild(child);
			child.QueueFree();
		}
		_artifactOfferCards.Clear();

		var targets = new List<Control>();
		foreach (var artifact in choices)
		{
			var captured = artifact;
			var card = UiComponents.ChoiceCard(new ChoiceCardData
			{
				Icon = LoadArtifactIcon(artifact),
				Title = artifact.DisplayName,
				RarityId = artifact.Rarity,
				Meta = $"{Localization.Text("offer.artifact.kind")} · {ArtifactSlotText(artifact.Slot)}",
				BodyBbcode = ArtifactBodyBbcode(artifact),
				ActionText = Localization.Text("ui.common.choose"),
				Width = 1080.0f
			}, () => SelectArtifactOffer(captured));
			_artifactOfferList.AddChild(card.Root);
			_artifactOfferCards.Add(card);
			targets.Add(card.FocusTarget);
		}
		// D-pad cima/baixo entre cards com volta (§3.4).
		ControllerUiSupport.SetVerticalCycle(targets);
	}

	private Control FirstArtifactOfferTarget()
	{
		return _artifactOfferCards.Count > 0 ? _artifactOfferCards[0].FocusTarget : null;
	}

	private void WarnArtifactOfferMandatory()
	{
		_artifactOfferWindow?.SetStatus(Localization.Text("offer.artifact.mandatory"));
	}

	private bool CanRerollArtifactOffer()
	{
		return _artifactOfferPreviewMode || (GetArtifactOfferSkillController()?.CanRerollReward() ?? false);
	}

	// Mesmo padrão da oferta de bênção: sem a skill a dica some; usada no mapa, fica "(0)" desabilitada.
	private void RefreshArtifactRerollHint()
	{
		if (_artifactRerollHint == null)
			return;
		var hasSkill = _artifactOfferPreviewMode || HasRewardRerollSkill();
		_artifactRerollHint.Visible = hasSkill;
		if (!hasSkill)
			return;
		// O reroll da skill vale uma vez por mapa.
		var remaining = CanRerollArtifactOffer() ? 1 : 0;
		_artifactRerollHint.SetLabel(Localization.TextFormat("offer.reroll_count", remaining));
		_artifactRerollHint.Button.Disabled = remaining == 0;
		_artifactRerollHint.Button.TooltipText = remaining > 0
			? Localization.Text("offer.reroll_tooltip")
			: Localization.Text("offer.reroll_used");
	}

	private void RerollArtifactOffer()
	{
		if (!UiModalStack.IsOpen(_artifactOfferOverlay) || UiModalStack.Top != _artifactOfferOverlay)
			return;

		List<ArtifactDefinition> choices;
		if (_artifactOfferPreviewMode)
		{
			if (!_artifactOfferChoices.TryGetValue(_artifactOfferSourceKey, out var current))
				return;
			choices = new List<ArtifactDefinition>(current);
			choices.Reverse();
		}
		else
		{
			// Sorteia antes de gastar o uso: sorteio vazio não consome o Espelho do Destino.
			if (!CanRerollArtifactOffer())
				return;
			choices = RollArtifactOfferChoices();
			// Sem nada novo para oferecer: mantém as opções atuais (a oferta continua obrigatória).
			if (choices.Count == 0)
			{
				_artifactOfferWindow.SetStatus(Localization.Text("offer.artifact.reroll_empty"));
				RefreshArtifactRerollHint();
				return;
			}
			if (GetArtifactOfferSkillController()?.TryConsumeRewardReroll() != true)
			{
				RefreshArtifactRerollHint();
				return;
			}
		}

		_artifactOfferChoices[_artifactOfferSourceKey] = choices;
		RebuildArtifactOfferCards(choices);
		RefreshArtifactRerollHint();
		_artifactOfferWindow.SetStatus(Localization.Text("offer.artifact.rerolled"));
		ControllerUiSupport.FocusInitial(FirstArtifactOfferTarget(), _artifactOfferOverlay);
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
		// Guarda contra clique duplo e escolha às cegas: só vale com a oferta aberta e no topo da pilha.
		if (UiModalStack.Top != _artifactOfferOverlay)
			return;

		_artifactOfferChoices.Remove(_artifactOfferSourceKey);
		var record = ArtifactCatalog.CreateRecord(artifact);

		CollectArtifact(record);
	}

	private void CollectArtifact(ArtifactRecord artifact)
	{
		RunState.AddTemporaryArtifact(artifact);
		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		NotifyArtifactCollected(artifact);
		_pendingArtifactSource?.Consume();
		_pendingArtifactSource = null;
		_artifactOfferPreviewMode = false;
		UiModalStack.Pop(_artifactOfferOverlay);
		GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
		RefreshCharacterSheet();
	}

	private static ulong ArtifactOfferKey(RoomArtifact source)
	{
		return source != null && IsInstanceValid(source) ? source.GetInstanceId() : 0;
	}

	// Esquece opções de fontes que já saíram do mundo (troca de cena sem escolha).
	private void PruneArtifactOfferChoices()
	{
		var stale = new List<ulong>();
		foreach (var key in _artifactOfferChoices.Keys)
		{
			if (key == 0)
				continue;
			var instance = InstanceFromId(key);
			if (instance == null || !IsInstanceValid(instance))
				stale.Add(key);
		}
		foreach (var key in stale)
			_artifactOfferChoices.Remove(key);
	}

	private PlayerSkillController GetArtifactOfferSkillController()
	{
		return _runtimePlayerController?.GetNodeOrNull<PlayerSkillController>("PlayerSkillController")
			?? GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<PlayerSkillController>("PlayerSkillController");
	}

	// Preview de debug (HUD.OpenPreviewScreen): primeiras opções por raridade, sem sorteio nem diário.
	private void OpenArtifactOfferPreview(int count)
	{
		if (_artifactOfferOverlay == null)
			return;

		var choices = new List<ArtifactDefinition>();
		foreach (var rarity in new[] { "Common", "Epic", "Mythic", "Rare", "Legendary", "Uncommon" })
		{
			if (choices.Count >= count)
				break;
			foreach (var artifact in ArtifactCatalog.All)
			{
				if (string.Equals(artifact.Rarity, rarity, StringComparison.OrdinalIgnoreCase))
				{
					choices.Add(artifact);
					break;
				}
			}
		}

		_artifactOfferPreviewMode = true;
		_artifactOfferChoices.Remove(0);
		if (UiModalStack.IsOpen(_artifactOfferOverlay))
			UiModalStack.Pop(_artifactOfferOverlay);
		ShowArtifactOffer(null, 0, choices);
	}

	// ---------- Texto e ícone de artefato (oferta e baú) ----------

	private static Texture2D LoadArtifactIcon(ArtifactDefinition artifact)
	{
		// generic.png é o placeholder do catálogo: o card usa monograma (§3.4).
		if (artifact == null || string.IsNullOrEmpty(artifact.IconPath) || artifact.IconPath == ArtifactGenericIconPath)
			return null;
		return ResourceLoader.Exists(artifact.IconPath) ? ResourceLoader.Load<Texture2D>(artifact.IconPath) : null;
	}

	private static string ArtifactSlotText(string slot)
	{
		return (slot ?? "").ToLowerInvariant() switch
		{
			"passive" => Localization.Text("ui.artifact_slot.passive"),
			"" => Localization.Text("ui.artifact_slot.passive"),
			"active" => Localization.Text("ui.artifact_slot.active"),
			_ => slot
		};
	}

	// Descrição + efeitos (valores com o mesmo cálculo do compêndio: ajuste salvo × poder de drop do chefe).
	private static string ArtifactBodyBbcode(ArtifactDefinition artifact)
	{
		var gold = UiPalette.Gold.ToHtml(false);
		var effects = FormatArtifactEffectList(artifact);
		return $"{EscapeArtifactBbcode(artifact.Description)}\n[color=#{gold}]{Localization.Text("ui.common.effects")}:[/color] {EscapeArtifactBbcode(effects)}";
	}

	// Formato único de efeito ("Nome: +20%") em oferta, baú e ficha: ArtifactEffectRegistry.FormatEffect.
	private static string FormatArtifactEffectList(ArtifactDefinition artifact)
	{
		if (artifact.Effects == null || artifact.Effects.Length == 0)
			return Localization.Text("ui.common.none");

		var parts = new List<string>();
		for (var i = 0; i < artifact.Effects.Length; i++)
		{
			var effect = artifact.Effects[i];
			var value = CompendiumTuning.GetNumber($"{artifact.Id}.effect.{i}.value", effect.Value) * ArtifactCatalog.BossDropPowerMultiplier;
			parts.Add(ArtifactEffectRegistry.FormatEffect(effect.Type, effect.Operation, value));
		}
		return string.Join(", ", parts);
	}

	// Registro antigo sem definição no catálogo: só tipo e valor (operação desconhecida, tratada como soma).
	private static string FormatArtifactRecordEffect(ArtifactRecord record)
	{
		return ArtifactEffectRegistry.FormatEffect(record.Effect, "AddFlat", record.Amount);
	}

	private static string EscapeArtifactBbcode(string text)
	{
		return (text ?? "").Replace("[", "[lb]");
	}
}
