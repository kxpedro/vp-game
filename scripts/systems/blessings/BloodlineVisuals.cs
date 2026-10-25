using Godot;
using System;

// Golpes de Virgil que carregam uma herança de sangue. Dash = esquiva (sem golpe); Quick = o giro que sai dela.
public enum BloodlineSlot
{
    Basic,
    Quick,
    Special,
    Dash
}

// Cores das heranças de sangue no jogo: tudo vem do campo "color" (e "statuses") de cada família em
// data/blessings/blood_blessings.json, então família nova ganha cor sem mexer no código.
// - Golpe: a herança de um slot é a família da bênção equipada no grupo exclusivo dele (<slot>_attack_status);
//   o dash usa a do ataque rápido (as bênçãos de investida têm a tag "dash") e, sem ela, qualquer bênção com "dash".
// - Estado do inimigo: o id do estado (bleeding, pestilence, charm...) aponta para a família que o lista em "statuses".
public static class BloodlineVisuals
{
    public static BloodlineSlot ToSlot(PlayerAttackType attackType) => attackType switch
    {
        PlayerAttackType.Quick => BloodlineSlot.Quick,
        PlayerAttackType.Special => BloodlineSlot.Special,
        _ => BloodlineSlot.Basic
    };

    // Cor da herança do slot; false se o slot não tem herança (o golpe fica com a cor neutra de sempre).
    public static bool TryGetSlotColor(BloodlineSlot slot, out Color color)
    {
        color = Colors.White;
        var family = GetSlotFamily(slot);
        if (family == null)
            return false;
        color = family.Color;
        return true;
    }

    public static bool TryGetSlotColor(PlayerAttackType attackType, out Color color) => TryGetSlotColor(ToSlot(attackType), out color);

    public static BloodBlessingFamily GetSlotFamily(BloodlineSlot slot)
    {
        var record = slot switch
        {
            BloodlineSlot.Basic => RunState.GetBlessingInExclusiveGroup("basic_attack_status"),
            BloodlineSlot.Special => RunState.GetBlessingInExclusiveGroup("special_attack_status"),
            BloodlineSlot.Quick => RunState.GetBlessingInExclusiveGroup("quick_attack_status"),
            _ => RunState.GetBlessingInExclusiveGroup("quick_attack_status") ?? FindTagged("dash")
        };
        if (record == null || !BloodBlessingCatalog.TryGet(record.Id, out var definition))
            return null;
        return FindFamily(definition.Family);
    }

    public static BloodBlessingFamily FindFamily(string familyName)
    {
        if (string.IsNullOrWhiteSpace(familyName))
            return null;
        foreach (var family in BloodBlessingCatalog.AllFamilies)
            if (family.DisplayName.Equals(familyName, StringComparison.OrdinalIgnoreCase) || family.Id.Equals(familyName, StringComparison.OrdinalIgnoreCase))
                return family;
        return null;
    }

    // Cor do estado de inimigo pela família que o declara; fallback quando nenhuma declara.
    public static Color StatusColor(string statusId, Color fallback)
    {
        foreach (var family in BloodBlessingCatalog.AllFamilies)
            foreach (var status in family.Statuses)
                if (status.Equals(statusId, StringComparison.OrdinalIgnoreCase))
                    return family.Color;
        return fallback;
    }

    // Versão mais clara da cor (borda dos arcos, núcleo do brilho).
    public static Color Lighten(Color color, float amount) => color.Lerp(Colors.White, Mathf.Clamp(amount, 0.0f, 1.0f));

    private static BloodBlessingRecord FindTagged(string tag)
    {
        foreach (var blessing in RunState.RunBlessings)
        {
            if (!BloodBlessingCatalog.TryGet(blessing.Id, out var definition))
                continue;
            foreach (var entry in definition.Tags)
                if (entry.Equals(tag, StringComparison.OrdinalIgnoreCase))
                    return blessing;
        }
        return null;
    }
}
