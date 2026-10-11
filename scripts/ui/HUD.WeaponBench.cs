using Godot;
using System;
using System.Collections.Generic;

// Bancada de armas (docs/design/ui-guia.md §8.18): Window(Large) com corpo de 880, um card por arma.
// Arma equipada: borda Gold + selo "EQUIPADA" (foco inicial). Armas sem moveset (WeaponDefinition.IsImplemented == false):
// selo "EM BREVE" e card desabilitado enquanto AllowEquipPlaceholderWeapons for false.
// Não pausa (§5.1 regra 2): entra na UiModalStack só para bloquear o input do jogo; Esc/B fecha pela pilha.
public partial class HUD
{
	private const float WeaponBenchBodyWidth = 880.0f;
	// Regra única de equipar armas placeholder (IsImplemented == false). false = card "Em breve" desabilitado.
	// Pendente de confirmação do usuário: trocar para true libera equipar (o card continua com a tag "Em breve").
	private const bool AllowEquipPlaceholderWeapons = false;

	private UiWindow _weaponBenchWindow;
	private VBoxContainer _weaponBenchList;
	private readonly Dictionary<string, Control> _weaponBenchTargets = new();

	public void OpenWeaponBench()
	{
		if (_weaponBenchOverlay == null)
			return;

		// Já aberta (interact relido pelo objeto 3D): não rouba o foco.
		if (UiModalStack.IsOpen(_weaponBenchOverlay))
			return;

		SetWeaponBenchStatus(null);
		RefreshWeaponBench();
		UiModalStack.Push(_weaponBenchOverlay, false, WeaponBenchTarget(WeaponLoadout.EquippedWeaponId), CloseWeaponBench);
		_weaponBenchWindow.FadeIn();
	}

	private void BuildWeaponBenchOverlay(Control root)
	{
		_weaponBenchWindow = UiComponents.Window(root, "WeaponBenchOverlay", WindowSize.Large,
			Localization.Text("bench.title"));
		_weaponBenchOverlay = _weaponBenchWindow.Root;
		_weaponBenchOverlay.Visible = false;

		var scroll = UiComponents.ScrollList(0.0f, out _weaponBenchList);
		PadChoiceCardScroll(scroll, _weaponBenchList);
		scroll.Name = "WeaponBenchScroll";
		_weaponBenchWindow.Body.AddChild(scroll);

		var footer = _weaponBenchWindow.Footer;
		footer.Add(UiAction.Cancel, Localization.Text("ui.common.close"), CloseWeaponBench, left: true);
		footer.Add(UiAction.Accept, Localization.Text("ui.common.equip"));
	}

	private void RefreshWeaponBench()
	{
		if (_weaponBenchList == null)
			return;

		foreach (var child in _weaponBenchList.GetChildren())
		{
			_weaponBenchList.RemoveChild(child);
			child.QueueFree();
		}
		_weaponBenchTargets.Clear();

		var equippedId = WeaponLoadout.EquippedWeaponId;
		var targets = new List<Control>();
		foreach (var weapon in WeaponCatalog.All)
		{
			var card = CreateWeaponBenchCard(weapon, weapon.Id == equippedId);
			_weaponBenchList.AddChild(card.Root);
			_weaponBenchTargets[weapon.Id] = card.FocusTarget;
			targets.Add(card.FocusTarget);
		}
		ControllerUiSupport.SetVerticalCycle(targets);
	}

	private UiChoiceCard CreateWeaponBenchCard(WeaponDefinition weapon, bool equipped)
	{
		var placeholder = IsPlaceholderWeapon(weapon);
		var description = weapon.Description ?? "";

		var data = new ChoiceCardData
		{
			IconText = UiComponents.MonogramText(weapon.DisplayName),
			Title = weapon.DisplayName,
			Meta = placeholder
				? Localization.Text("bench.meta_placeholder")
				: Localization.TextFormat("bench.meta", weapon.QuickAttack.Name, weapon.SpecialAttack.Name),
			BodyBbcode = description.Replace("[", "[lb]"),
			Width = WeaponBenchBodyWidth
		};

		if (equipped)
		{
			// Equipada: continua focável (foco inicial), a ação só confirma o estado.
			data.Accent = UiPalette.Gold;
			data.Tag = Localization.Text("ui.common.equipped");
			data.TagColor = UiPalette.Gold;
			data.ActionText = Localization.Text("ui.common.equipped");
			// Save antigo com placeholder equipado: mostra os dois selos ("Em breve" no lugar do selo de raridade).
			if (placeholder)
				data.RarityLabel = Localization.Text("ui.common.coming_soon");
			return UiComponents.ChoiceCard(data, () => SetWeaponBenchStatus(
				Localization.TextFormat("bench.already_equipped", weapon.DisplayName)));
		}

		data.ActionText = Localization.Text("ui.common.equip");
		if (placeholder)
		{
			// "Em breve" aparece uma vez só: no selo (com fundo visível), sem repetir como motivo sob o botão.
			data.Tag = Localization.Text("ui.common.coming_soon");
			data.TagColor = UiPalette.Border;
		}
		if (placeholder && !AllowEquipPlaceholderWeapons)
		{
			data.Disabled = true;
			// Handler vazio: o card mantém o botão (desabilitado); Choose ignora card desabilitado.
			return UiComponents.ChoiceCard(data, () => { });
		}

		var weaponId = weapon.Id;
		return UiComponents.ChoiceCard(data, () => EquipWeapon(weaponId));
	}

	private void EquipWeapon(string weaponId)
	{
		WeaponLoadout.Equip(weaponId);
		var weapon = WeaponLoadout.EquippedWeapon;
		if (_weaponLabel != null)
			_weaponLabel.Text = Localization.TextFormat("hud.weapon", weapon.DisplayName);
		SetWeaponBenchStatus(Localization.TextFormat("bench.equipped_now", weapon.DisplayName));
		RefreshWeaponBench();
		// Os cards foram recriados: refoca a arma recém-equipada.
		ControllerUiSupport.FocusInitial(WeaponBenchTarget(weapon.Id), _weaponBenchOverlay);
	}

	private Control WeaponBenchTarget(string weaponId)
	{
		return weaponId != null && _weaponBenchTargets.TryGetValue(weaponId, out var target) ? target : null;
	}

	private void SetWeaponBenchStatus(string text)
	{
		_weaponBenchWindow?.SetStatus(string.IsNullOrEmpty(text)
			? Localization.TextFormat("bench.current", WeaponLoadout.EquippedWeapon.DisplayName)
			: text);
	}

	private void CloseWeaponBench()
	{
		if (_weaponBenchOverlay != null)
			UiModalStack.Pop(_weaponBenchOverlay);
	}

	// Placeholder do catálogo (WeaponCatalog.Placeholder): WeaponDefinition.IsImplemented == false.
	private static bool IsPlaceholderWeapon(WeaponDefinition weapon) => weapon != null && !weapon.IsImplemented;
}
