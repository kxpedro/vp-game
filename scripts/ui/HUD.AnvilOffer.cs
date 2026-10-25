using Godot;
using System.Collections.Generic;
using System.Linq;

// Oferta da Bigorna (docs/design/bigorna.md §1.4, §5.2): 1 a 3 modificações da arma equipada, escolha obrigatória
// (sem pular), pausa pela UiModalStack. Aberta pelo item no chão via GetTree().CallGroup("hud", "OpenAnvilOffer").
// Pool vazio: paga as Moedas de fallback e avisa. Preview (debug): --ui-preview=anvil_offer | anvil_offer_single,
// com modificações do catálogo e sem tocar no RunState.
public partial class HUD
{
	private UiWindow _anvilWindow;
	private Control _anvilOverlay;
	private VBoxContainer _anvilList;
	private readonly List<UiChoiceCard> _anvilCards = new();
	private readonly List<Button> _anvilButtons = new();
	private int _anvilFocused;
	private bool _anvilChoiceLocked;
	private bool _anvilPreview;

	private void BuildAnvilOfferOverlay(Control root)
	{
		_anvilWindow = UiComponents.Window(root, "AnvilOfferOverlay", WindowSize.Large, Localization.Text("anvil.offer.title"));
		_anvilWindow.Mandatory = true;
		_anvilOverlay = _anvilWindow.Root;
		_anvilOverlay.Visible = false;
		_anvilWindow.SetStatus(Localization.TextFormat("anvil.offer.status", WeaponLoadout.EquippedWeapon.DisplayName));

		var scroll = UiComponents.ScrollList(0, out _anvilList);
		PadChoiceCardScroll(scroll, _anvilList);
		_anvilWindow.Body.AddChild(scroll);

		// Sem "Voltar" (oferta obrigatória).
		_anvilWindow.Footer.Add(UiAction.Accept, Localization.Text("anvil.offer.choose"), ChooseFocusedAnvilMod);
	}

	// Chamado por string (CallGroup "hud", "OpenAnvilOffer") ao coletar o item da Bigorna.
	public void OpenAnvilOffer()
	{
		if (_anvilOverlay == null || _isGameOver || UiModalStack.IsOpen(_anvilOverlay)) return;
		_anvilPreview = false;
		var offers = RunState.RollWeaponModOffers();
		if (offers.Count == 0)
		{
			// Sem modificação para a arma: o ferro vira Moedas no saldo da run (mesmo fluxo da Bênção sem oferta).
			RunState.ResolveAnvilWithoutOffer();
			var fallbackCoins = CompendiumTuning.GetInt("economy.coins.blessing_fallback", EconomyDefaults.BlessingFallbackCoins);
			if (GetTree().GetFirstNodeInGroup("run_manager") is RunManager runManager)
				runManager.AddBloodCoins(fallbackCoins);
			else
			{
				RunState.AddRunBloodCoins(fallbackCoins);
				NotifyBloodCoinsRewarded(fallbackCoins, RunState.RunBloodCoins);
			}
			var message = Localization.TextFormat("anvil.offer.none", fallbackCoins);
			AppendGameLog(message);
			ShowNotificationToast(message, true);
			GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
			return;
		}
		PresentAnvilOffers(offers);
	}

	private void PresentAnvilOffers(IReadOnlyList<WeaponModDefinition> offers)
	{
		ClearAnvilCards();
		_anvilChoiceLocked = false;
		_anvilWindow.Status.RemoveThemeColorOverride("font_color");
		_anvilWindow.SetStatus(Localization.TextFormat("anvil.offer.status", WeaponLoadout.EquippedWeapon.DisplayName));

		foreach (var mod in offers)
		{
			var captured = mod;
			var card = UiComponents.ChoiceCard(BuildAnvilCardData(mod), () => SelectAnvilMod(captured));
			_anvilList.AddChild(card.Root);
			var index = _anvilButtons.Count;
			card.ActionButton.FocusEntered += () => _anvilFocused = index;
			_anvilCards.Add(card);
			_anvilButtons.Add(card.ActionButton);
		}
		var items = _anvilButtons.Cast<Control>().ToList();
		ControllerUiSupport.SetVerticalCycle(items);
		foreach (var item in items)
		{
			item.FocusNeighborLeft = item.FocusNeighborTop;
			item.FocusNeighborRight = item.FocusNeighborBottom;
		}

		_anvilFocused = 0;
		// Pausa a árvore, mas a música da sala continua tocando durante a escolha.
		UiModalStack.Push(_anvilOverlay, true, _anvilButtons[0], WarnAnvilOfferMandatory, mandatory: true, pausesAudio: false);
		_anvilWindow.FadeIn();
	}

	private static ChoiceCardData BuildAnvilCardData(WeaponModDefinition mod)
	{
		var secondary = UiPalette.TextSecondary.ToHtml(false);
		var body = EscapeSheetBbcode(mod.Description);
		var flavor = mod.Flavor;
		if (!string.IsNullOrEmpty(flavor))
			body += $"\n[i][color=#{secondary}]{EscapeSheetBbcode(flavor)}[/color][/i]";
		return new ChoiceCardData
		{
			Title = mod.DisplayName,
			Meta = WeaponLoadout.EquippedWeapon.DisplayName,
			BodyBbcode = body,
			ActionText = Localization.Text("anvil.offer.choose"),
			Width = BlessingCardWidth
		};
	}

	private void ClearAnvilCards()
	{
		if (_anvilList != null)
			foreach (var child in _anvilList.GetChildren())
			{
				_anvilList.RemoveChild(child);
				child.QueueFree();
			}
		_anvilCards.Clear();
		_anvilButtons.Clear();
	}

	// ui_cancel na oferta obrigatória: não fecha; avisa no status e mantém o foco no card.
	private void WarnAnvilOfferMandatory()
	{
		_anvilWindow.SetStatus(Localization.Text("anvil.offer.must_choose"));
		_anvilWindow.Status.AddThemeColorOverride("font_color", UiPalette.Warn);
		if (_anvilButtons.Count > 0)
			_anvilButtons[Mathf.Clamp(_anvilFocused, 0, _anvilButtons.Count - 1)].GrabFocus();
	}

	// "Forjar" do rodapé: só escolhe o card focado; sem card focado, foca o primeiro (não escolhe às cegas).
	private void ChooseFocusedAnvilMod()
	{
		if (_anvilCards.Count == 0 || UiModalStack.Top != _anvilOverlay) return;
		var focused = GetViewport().GuiGetFocusOwner();
		var index = focused is Button button ? _anvilButtons.IndexOf(button) : -1;
		if (index < 0)
		{
			_anvilButtons[0].GrabFocus();
			return;
		}
		_anvilCards[index].Choose();
	}

	private void SelectAnvilMod(WeaponModDefinition mod)
	{
		if (_anvilChoiceLocked || UiModalStack.Top != _anvilOverlay) return;
		if (!_anvilPreview && !RunState.TryApplyWeaponMod(mod.Id)) return;
		_anvilChoiceLocked = true;
		var message = Localization.TextFormat("anvil.offer.applied", mod.DisplayName);
		if (!_anvilPreview)
		{
			GetTree().CallGroup("player", "ApplyRuntimeTuning");
			GetTree().CallGroup("audio_manager", "PlayEffect", "blessing_select");
			AppendGameLog(message);
			ShowNotificationToast(message, true);
		}
		// Os cards ficam até a próxima oferta (removê-los aqui seria no meio do sinal do botão/card).
		UiModalStack.Pop(_anvilOverlay);
		if (_anvilPreview)
			return;
		RefreshCharacterSheet();
		GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
	}

	// Preview (debug): modificações do catálogo (todas as da arma equipada ou da Espada Grande), nada vai ao RunState.
	internal void PreviewAnvilOffer(int count)
	{
		_anvilPreview = true;
		var pool = WeaponModCatalog.ForWeapon(WeaponLoadout.EquippedWeaponId);
		if (pool.Count == 0)
			pool = WeaponModCatalog.ForWeapon("longsword");
		if (pool.Count == 0 || _anvilOverlay == null) return;
		// Preferência pelas de texto mais longo (estresse de layout): Contrapeso e Impacto Ampliado entram na oferta de 3.
		var picks = pool.OrderByDescending(mod => mod.Effects.Length).Take(count).ToList();
		PresentAnvilOffers(picks);
	}
}
