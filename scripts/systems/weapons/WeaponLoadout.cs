public static class WeaponLoadout
{
    public static string EquippedWeaponId { get; private set; } = "longsword";
    public static WeaponDefinition EquippedWeapon => WeaponCatalog.Get(EquippedWeaponId);

    public static void Equip(string weaponId)
    {
        EquippedWeaponId = WeaponCatalog.Get(weaponId).Id;
        SaveGameManager.RequestAutosave();
    }
}
