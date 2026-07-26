public static class WeaponLoadout
{
    public static event System.Action<string> EquippedWeaponChanged;

    public static string EquippedWeaponId { get; private set; } = "longsword";
    public static WeaponDefinition EquippedWeapon => WeaponCatalog.Get(EquippedWeaponId);

    public static void Equip(string weaponId)
    {
        var resolvedId = WeaponCatalog.Get(weaponId).Id;
        if (EquippedWeaponId == resolvedId)
            return;

        EquippedWeaponId = resolvedId;
        EquippedWeaponChanged?.Invoke(EquippedWeaponId);
        SaveGameManager.RequestAutosave();
    }
}
