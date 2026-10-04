using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public partial class HUD
{
	public void OpenWeaponBench()
	{
		if (_weaponBenchOverlay == null)
			return;
		_weaponBenchStatusLabel.Text = $"Equipada: {WeaponLoadout.EquippedWeapon.DisplayName}";
		_weaponBenchOverlay.Visible = true;
		ControllerUiSupport.FocusFirst(_weaponBenchOverlay);
	}

	private void BuildWeaponBenchOverlay(Control root)
	{
		_weaponBenchOverlay = new ColorRect
		{
			Name = "WeaponBenchOverlay",
			Color = new Color(0.025f, 0.015f, 0.02f, 0.96f),
			AnchorLeft = 0.25f,
			AnchorTop = 0.18f,
			AnchorRight = 0.75f,
			AnchorBottom = 0.82f,
			Visible = false
		};
		root.AddChild(_weaponBenchOverlay);

		var list = new VBoxContainer
		{
			AnchorRight = 1,
			AnchorBottom = 1,
			OffsetLeft = 30,
			OffsetTop = 24,
			OffsetRight = -30,
			OffsetBottom = -24
		};
		_weaponBenchOverlay.AddChild(list);
		list.AddChild(new Label { Text = "Bancada de Armas", HorizontalAlignment = HorizontalAlignment.Center });
		_weaponBenchStatusLabel = new Label { Text = $"Equipada: {WeaponLoadout.EquippedWeapon.DisplayName}" };
		list.AddChild(_weaponBenchStatusLabel);

		foreach (var weapon in WeaponCatalog.All)
		{
			var captured = weapon;
			var button = new Button { Text = $"{weapon.DisplayName} — {weapon.Description}" };
			button.Pressed += () => EquipWeapon(captured.Id);
			list.AddChild(button);
		}

		var close = new Button { Text = Localization.Text("hud.close") };
		close.Pressed += () => _weaponBenchOverlay.Visible = false;
		list.AddChild(close);
	}

	private void EquipWeapon(string weaponId)
	{
		WeaponLoadout.Equip(weaponId);
		_weaponLabel.Text = $"Arma: {WeaponLoadout.EquippedWeapon.DisplayName}";
		_weaponBenchStatusLabel.Text = $"Equipada: {WeaponLoadout.EquippedWeapon.DisplayName}";
	}
}
