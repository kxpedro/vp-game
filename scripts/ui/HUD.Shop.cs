using Godot;
using System;
using System.Collections.Generic;

// Loja de Artefatos da Câmara de Hemomancia (docs/design/economia-v2.md §6, guia de UI §3.4 e §5.1).
// Aberta pelo ponto interativo da Câmara (IntermissionChamber → CallGroup("hud", "OpenShop")). Pausa pela UiModalStack,
// fecha com ui_cancel (Esc/B) e usa Window(Large) + ChoiceCard, como a oferta de artefato.
// O estoque é sorteado UMA vez por visita (campo do HUD, que nasce com a cena da Câmara): fechar e reabrir mostra as mesmas
// opções, então não dá para rerrolar. O preço exibido vem de ArtifactCatalog.GetShopPrice (já com o desconto da skill).
// Prévia (--ui-preview=shop): estoque e saldo fictícios; comprar só simula e nada é gravado nem toca o RunState.
public partial class HUD
{
	private UiWindow _shopWindow;
	private Control _shopOverlay;
	private VBoxContainer _shopList;
	private Label _shopBalanceLabel;
	private Button _shopLeaveButton;
	private readonly List<UiChoiceCard> _shopCards = new();
	// Estoque da visita (nulo até a primeira abertura) e o que já foi comprado dele.
	private List<ShopOffer> _shopStock;
	private readonly HashSet<string> _shopPurchased = new();
	// Prévia: estoque, compras e saldo próprios (a loja real nunca é tocada).
	private bool _shopPreviewMode;
	private List<ShopOffer> _shopPreviewStock;
	private readonly HashSet<string> _shopPreviewPurchased = new();
	private int _shopPreviewCoins;

	private const int ShopPreviewStartCoins = 260; // ~ renda do mapa 1 (economia de out/2026): 2 compras baratas, nem todas

	private List<ShopOffer> ActiveShopStock => _shopPreviewMode ? _shopPreviewStock : _shopStock;

	private HashSet<string> ActiveShopPurchased => _shopPreviewMode ? _shopPreviewPurchased : _shopPurchased;

	private int ShopBalance => _shopPreviewMode ? _shopPreviewCoins : RunState.RunBloodCoins;

	// Chamado via CallGroup("hud", "OpenShop") pela IntermissionChamber. Não renomear sem procurar a string em scripts/.
	public void OpenShop()
	{
		if (_shopOverlay == null || UiModalStack.IsOpen(_shopOverlay))
			return;

		_shopPreviewMode = false;
		if (_shopStock == null)
		{
			var rng = new RandomNumberGenerator();
			rng.Randomize();
			_shopStock = ArtifactCatalog.RollShopStock(rng);
		}
		ShowShop();
	}

	private void BuildShopOverlay(Control root)
	{
		_shopCards.Clear();
		_shopWindow = UiComponents.Window(root, "ShopOverlay", WindowSize.Large, Localization.Text("shop.title"));
		_shopOverlay = _shopWindow.Root;
		_shopOverlay.Visible = false;
		// Janela mais alta que o Large padrão (720): cabem os 4 cartões do estoque sem rolagem em 1600x900
		// (margem vertical de 40, UiMetrics.OverlayMarginY).
		_shopWindow.Panel.OffsetTop = -410.0f;
		_shopWindow.Panel.OffsetBottom = 410.0f;

		// Saldo da run no cabeçalho, com a gota das Moedas de Sangue.
		_shopWindow.HeaderRight.AddChild(UiComponents.CoinGlyph(UiMetrics.IconGlyph + 2));
		_shopBalanceLabel = new Label
		{
			Name = "ShopBalanceLabel",
			ThemeTypeVariation = UiVariation.SectionGoldLabel,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_shopWindow.HeaderRight.AddChild(_shopBalanceLabel);

		var scroll = UiComponents.ScrollList(0.0f, out _shopList);
		PadChoiceCardScroll(scroll, _shopList);
		scroll.Name = "ShopScroll";
		_shopWindow.Body.AddChild(scroll);

		// Só aparece com a banca vazia (nenhum cartão): garante um alvo para o d-pad.
		_shopLeaveButton = UiComponents.MenuButton(Localization.Text("shop.leave"), CloseShop, ButtonVariant.Secondary, ButtonSize.Medium);
		_shopLeaveButton.Name = "ShopLeaveButton";
		_shopWindow.Actions.Alignment = BoxContainer.AlignmentMode.Center;
		_shopWindow.Actions.AddChild(_shopLeaveButton);

		_shopWindow.Footer.Add(UiAction.Cancel, Localization.Text("ui.common.back"), CloseShop, left: true);
		_shopWindow.Footer.Add(UiAction.Accept, Localization.Text("shop.buy"), BuyFocusedShopCard);
	}

	private void ShowShop()
	{
		_shopWindow.Status.RemoveThemeColorOverride("font_color");
		_shopWindow.SetStatus(_shopPreviewMode ? Localization.Text("shop.preview_status") : Localization.Text("shop.status"));
		RebuildShopCards();
		// Foco inicial: primeiro cartão comprável (ou o primeiro cartão, se nenhum for).
		UiModalStack.Push(_shopOverlay, pauses: true, initialFocus: PickShopFocus(0), onCancel: CloseShop, pausesAudio: false);
		_shopWindow.FadeIn();
	}

	private void CloseShop()
	{
		if (_shopOverlay != null)
			UiModalStack.Pop(_shopOverlay);
	}

	private bool IsShopArtifactBought(ArtifactDefinition definition)
	{
		if (ActiveShopPurchased.Contains(definition.Id))
			return true;
		return !_shopPreviewMode && ArtifactCatalog.ShopExcludesOwned && RunState.HasRunArtifact(definition.Id);
	}

	// Refaz os cartões (RemoveChild + QueueFree, guia §5.1 regra 10) e o ciclo de foco só entre alvos habilitados.
	private void RebuildShopCards()
	{
		foreach (var child in _shopList.GetChildren())
		{
			_shopList.RemoveChild(child);
			child.QueueFree();
		}
		_shopCards.Clear();

		var balance = ShopBalance;
		_shopBalanceLabel.Text = Localization.TextFormat("shop.balance", balance);

		var stock = ActiveShopStock;
		var targets = new List<Control>();
		if (stock == null || stock.Count == 0)
		{
			_shopList.AddChild(new Label
			{
				Text = Localization.Text("shop.empty"),
				ThemeTypeVariation = UiVariation.SecondaryLabel,
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				MouseFilter = Control.MouseFilterEnum.Ignore
			});
		}
		else
		{
			foreach (var offer in stock)
			{
				var definition = offer.Definition;
				var bought = IsShopArtifactBought(definition);
				var basePrice = ArtifactCatalog.GetShopBasePrice(definition);
				var price = ArtifactCatalog.GetShopPrice(definition);
				var affordable = balance >= price;
				var captured = definition;
				// Cartão sem meta (Artefato · Passivo): 4 cartões precisam caber sem rolagem. "Comprado" escurece o cartão
				// (Disabled só no visual); sem saldo o cartão fica normal e só o botão e o preço mudam de aparência.
				var costText = bought ? null : price < basePrice
					? Localization.TextFormat("shop.price_discount", price, basePrice)
					: Localization.TextFormat("shop.price", price);
				if (!bought && !affordable)
					costText += "\n" + Localization.TextFormat("shop.missing", price - balance);
				var card = UiComponents.ChoiceCard(new ChoiceCardData
				{
					Icon = LoadArtifactIcon(definition),
					Title = definition.DisplayName,
					RarityId = definition.Rarity,
					BodyBbcode = ArtifactBodyBbcode(definition),
					Tag = bought ? Localization.Text("shop.bought") : null,
					ActionText = bought ? Localization.Text("shop.bought") : Localization.Text("shop.buy"),
					CostText = costText,
					CostColor = affordable ? UiPalette.Gold : UiPalette.Warn,
					Disabled = bought,
					Width = 1080.0f
				}, () => BuyShopArtifact(captured));
				if (card.ActionButton != null)
				{
					card.ActionButton.CustomMinimumSize = new Vector2(148.0f, UiMetrics.ButtonL);
					if (bought || !affordable)
					{
						// Indisponível mas focável: ao acionar, só mostra a mensagem (BuyShopArtifact não compra).
						card.ActionButton.Disabled = false;
						StyleShopUnavailableButton(card.ActionButton);
					}
					if (bought)
						card.ActionButton.Pressed += () => BuyShopArtifact(captured);
				}
				_shopList.AddChild(card.Root);
				_shopCards.Add(card);
				targets.Add(card.FocusTarget);
			}
		}

		var empty = targets.Count == 0;
		_shopWindow.Actions.Visible = empty;
		if (empty)
			targets.Add(_shopLeaveButton);
		ControllerUiSupport.SetVerticalCycle(targets);
	}

	// Aparência de indisponível sem desabilitar: fundo PanelInset, borda fina e texto apagado. O anel de foco continua o do tema.
	private static void StyleShopUnavailableButton(Button button)
	{
		var box = UIThemeFactory.ControlBox(UiPalette.PanelInset, UiPalette.Border, UiMetrics.BorderThin);
		foreach (var state in new[] { "normal", "hover", "pressed" })
			button.AddThemeStyleboxOverride(state, box);
		foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
			button.AddThemeColorOverride(color, UiPalette.TextSecondary);
	}

	private bool IsShopOfferBuyable(ShopOffer offer)
	{
		return !IsShopArtifactBought(offer.Definition) && ShopBalance >= ArtifactCatalog.GetShopPrice(offer.Definition);
	}

	// Alvo de foco a partir do índice do cartão: o primeiro cartão comprável dali em diante (com volta); se nenhum for,
	// o cartão do próprio índice (todos são focáveis). Sem cartões: "Sair da loja".
	private Control PickShopFocus(int fromIndex)
	{
		var stock = ActiveShopStock;
		var count = _shopCards.Count;
		if (stock == null || count == 0)
			return _shopLeaveButton;
		for (var step = 0; step < count; step++)
		{
			var index = (fromIndex + step) % count;
			if (index < stock.Count && IsShopOfferBuyable(stock[index]))
				return _shopCards[index].FocusTarget;
		}
		return _shopCards[Mathf.Clamp(fromIndex, 0, count - 1)].FocusTarget;
	}

	// Rodapé "[✕] Comprar": compra o cartão com foco; sem cartão focado (clique no rodapé), só foca o primeiro alvo.
	private void BuyFocusedShopCard()
	{
		var stock = ActiveShopStock;
		for (var i = 0; i < _shopCards.Count; i++)
		{
			var button = _shopCards[i].ActionButton;
			if (button != null && button.HasFocus() && stock != null && i < stock.Count)
			{
				// Direto em BuyShopArtifact: também cobre "Comprado" e sem saldo (só mensagem, nada é cobrado).
				BuyShopArtifact(stock[i].Definition);
				return;
			}
		}
		ControllerUiSupport.FocusInitial(PickShopFocus(0), _shopOverlay);
	}

	private void BuyShopArtifact(ArtifactDefinition definition)
	{
		// Só vale com a loja aberta e no topo da pilha (clique duplo / input atrasado).
		if (definition == null || UiModalStack.Top != _shopOverlay)
			return;

		var index = Math.Max(ActiveShopStock?.FindIndex(offer => offer.Definition.Id == definition.Id) ?? 0, 0);

		// Recusas (já comprado, sem saldo): só mensagem em Warn, nada é cobrado, o foco fica no mesmo cartão.
		if (IsShopArtifactBought(definition))
		{
			SetShopStatus(Localization.TextFormat("shop.status_owned", definition.DisplayName), index, true);
			return;
		}

		var price = ArtifactCatalog.GetShopPrice(definition);
		if (ShopBalance < price || !TryPayShopArtifact(definition, price))
		{
			SetShopStatus(Localization.TextFormat("shop.not_enough", definition.DisplayName), index, true);
			return;
		}

		ActiveShopPurchased.Add(definition.Id);
		SetShopStatus(Localization.TextFormat("shop.status_bought", definition.DisplayName), index, false);
	}

	// Cobra e entrega. Prévia: só desconta o saldo fictício. Loja real: RunState gasta as Moedas da run e adiciona o
	// artefato; depois reaplica o ajuste do jogador (vida máxima etc.), sincroniza o HUD e avisa.
	private bool TryPayShopArtifact(ArtifactDefinition definition, int price)
	{
		if (_shopPreviewMode)
		{
			_shopPreviewCoins -= price;
			return true;
		}

		if (!RunState.TryBuyShopArtifact(definition, price))
			return false;

		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		SyncBloodCoins(RunState.RunBloodCoins);
		GetTree().CallGroup("audio_manager", "PlayEffect", "treasure");
		AppendGameLog(Localization.TextFormat("hud.toast.artifact", definition.DisplayName));
		ShowNotificationToast(Localization.TextFormat("hud.toast.artifact", definition.DisplayName), true);
		RefreshCharacterSheet();
		return true;
	}

	// Mostra a mensagem curta (Warn se for recusa), atualiza saldo e cartões e devolve o foco: recusa = mesmo cartão;
	// compra = próximo cartão comprável (ou o mesmo, se não restar nenhum).
	private void SetShopStatus(string message, int focusIndex, bool refused)
	{
		if (refused)
			_shopWindow.Status.AddThemeColorOverride("font_color", UiPalette.Warn);
		else
			_shopWindow.Status.RemoveThemeColorOverride("font_color");
		_shopWindow.SetStatus(message);
		RebuildShopCards();
		var target = refused && focusIndex < _shopCards.Count ? _shopCards[focusIndex].FocusTarget : PickShopFocus(focusIndex);
		ControllerUiSupport.FocusInitial(target, _shopOverlay);
	}

	// ---------- Prévia (debug, --ui-preview=shop; HUD.OpenPreviewScreen) ----------

	// Estoque fictício com raridades variadas: Incomum já comprado, Raro ao alcance (saldo 1000), Épico e Lendário caros demais.
	internal void OpenShopPreview()
	{
		if (_shopOverlay == null)
			return;

		_shopPreviewMode = true;
		_shopPreviewCoins = ShopPreviewStartCoins;
		_shopPreviewPurchased.Clear();
		_shopPreviewStock = new List<ShopOffer>();
		foreach (var rarity in new[] { "Uncommon", "Rare", "Epic", "Legendary" })
		{
			foreach (var artifact in ArtifactCatalog.All)
			{
				if (!string.Equals(artifact.Rarity, rarity, StringComparison.OrdinalIgnoreCase))
					continue;
				_shopPreviewStock.Add(new ShopOffer(artifact, ArtifactCatalog.GetShopBasePrice(artifact), ArtifactCatalog.GetShopPrice(artifact)));
				break;
			}
		}
		if (_shopPreviewStock.Count > 0)
			_shopPreviewPurchased.Add(_shopPreviewStock[0].Definition.Id);

		if (UiModalStack.IsOpen(_shopOverlay))
			UiModalStack.Pop(_shopOverlay);
		ShowShop();
	}
}
