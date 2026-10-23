using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Jogador e recursos: add/set (vida, moedas, sangue amaldiçoado, fúria, frutas), heal, god, stamina, sprite.
public static partial class DevCommands
{
    private enum DevResource { Health, Coins, CursedBlood, Fury, Fruit }

    private static readonly string[] ResourceNames = { "health", "coins", "cursed", "fury", "fruit" };

    private static DevResource? ParseResource(string token) => DevText.Normalize(token) switch
    {
        "health" or "hp" or "vida" or "life" or "heal" => DevResource.Health,
        "coins" or "coin" or "moedas" or "moeda" or "gold" or "ouro" or "blood coins" => DevResource.Coins,
        "cursed" or "cursed blood" or "sangue" or "sangue amaldicoado" or "cb" or "blood" => DevResource.CursedBlood,
        "fury" or "furia" or "rage" => DevResource.Fury,
        "fruit" or "fruits" or "fruta" or "frutas" => DevResource.Fruit,
        _ => null
    };

    private static void RegisterPlayer()
    {
        DevConsole.Register(new DevCommand("add", "Soma (ou subtrai, com número negativo) a um recurso. A ordem dos argumentos tanto faz.", Add)
        {
            Aliases = new[] { "adicionar", "+" },
            Usage = "<quantidade> <health|coins|cursed|fury|fruit>",
            Examples = new[] { "add 50 health", "add coins 1000", "add -20 vida", "add 200 cursed", "add 2 fruit" },
            Complete = c => ResourceNames
        });
        DevConsole.Register(new DevCommand("set", "Define o valor de um recurso.", Set)
        {
            Aliases = new[] { "definir" },
            Usage = "<health|coins|cursed|fury> <valor>",
            Examples = new[] { "set health 1", "set coins 0", "set cursed 9999" },
            Complete = c => c.Count == 0 ? ResourceNames.Where(n => n != "fruit") : Enumerable.Empty<string>()
        });
        DevConsole.Register(new DevCommand("heal", "Cura a vida toda.", Heal) { Aliases = new[] { "curar", "cura" } });
        DevConsole.Register(new DevCommand("god", "Modo deus (não recebe dano). Sem argumento, alterna.", God)
        {
            Aliases = new[] { "deus", "godmode" },
            Usage = "[on|off]",
            Complete = _ => ToggleWords
        });
        DevConsole.Register(new DevCommand("stamina", "Stamina infinita. Sem argumento, alterna.", Stamina)
        {
            Aliases = new[] { "estamina", "infstamina" },
            Usage = "[on|off]",
            Complete = _ => ToggleWords
        });
        DevConsole.Register(new DevCommand("sprite", "Teste: Virgil como sprite 2D em vez do modelo 3D (também F3). Sem argumento, alterna.", Sprite)
        {
            Aliases = new[] { "sprite2d", "pixel" },
            Usage = "[on|off]",
            Complete = _ => ToggleWords
        });
    }

    // Aceita "<n> <recurso>" e "<recurso> <n>".
    private static bool TryAmountAndResource(DevCommandContext c, out double amount, out DevResource resource)
    {
        amount = 0;
        resource = DevResource.Health;
        if (c.Count != 2)
        {
            c.UsageError();
            return false;
        }
        var numberFirst = DevText.TryNumber(c.Arg(0), out amount);
        if (!numberFirst && !DevText.TryNumber(c.Arg(1), out amount))
        {
            c.UsageError("Falta a quantidade (número).");
            return false;
        }
        var parsed = ParseResource(c.Arg(numberFirst ? 1 : 0));
        if (parsed == null)
        {
            c.UsageError($"Recurso desconhecido: \"{c.Arg(numberFirst ? 1 : 0)}\" ({string.Join(", ", ResourceNames)}).");
            return false;
        }
        resource = parsed.Value;
        return true;
    }

    private static void Add(DevCommandContext c)
    {
        if (!TryAmountAndResource(c, out var amount, out var resource))
            return;
        switch (resource)
        {
            case DevResource.Health:
                if (c.RequireLivingPlayer() is { } health)
                {
                    health.SetCurrentHealth(Mathf.Clamp(health.CurrentHealth + (float)amount, 1.0f, health.MaxHealth));
                    c.Ok($"Vida: {Mathf.RoundToInt(health.CurrentHealth)}/{Mathf.RoundToInt(health.MaxHealth)}.");
                }
                break;
            case DevResource.Coins:
                ChangeCoins(c, (int)Math.Round(amount));
                break;
            case DevResource.CursedBlood:
                CharacterSheet.AddCursedBlood(Math.Max((int)Math.Round(amount), -CharacterSheet.CursedBlood));
                c.Hud?.DevConsoleRefreshRun();
                c.Ok($"Sangue Amaldiçoado: {CharacterSheet.CursedBlood} (permanente, vai ao save).");
                break;
            case DevResource.Fury:
                AddFury(c, (float)amount, false);
                break;
            case DevResource.Fruit:
                if (amount < 1)
                {
                    c.UsageError("Frutas só somam (quantidade >= 1).");
                    return;
                }
                EatFruits(c, (int)Math.Round(amount));
                break;
        }
    }

    private static void Set(DevCommandContext c)
    {
        if (!TryAmountAndResource(c, out var value, out var resource))
            return;
        switch (resource)
        {
            case DevResource.Health:
                if (c.RequireLivingPlayer() is { } health)
                {
                    health.SetCurrentHealth(Mathf.Clamp((float)value, 1.0f, health.MaxHealth));
                    c.Ok($"Vida: {Mathf.RoundToInt(health.CurrentHealth)}/{Mathf.RoundToInt(health.MaxHealth)}.");
                }
                break;
            case DevResource.Coins:
                ChangeCoins(c, Math.Max((int)Math.Round(value), 0) - RunState.RunBloodCoins);
                break;
            case DevResource.CursedBlood:
                CharacterSheet.AddCursedBlood(Math.Max((int)Math.Round(value), 0) - CharacterSheet.CursedBlood);
                c.Hud?.DevConsoleRefreshRun();
                c.Ok($"Sangue Amaldiçoado: {CharacterSheet.CursedBlood} (permanente, vai ao save).");
                break;
            case DevResource.Fury:
                AddFury(c, (float)value, true);
                break;
            case DevResource.Fruit:
                c.UsageError("Frutas não têm \"set\": use add <n> fruit.");
                break;
        }
    }

    private static void ChangeCoins(DevCommandContext c, int delta)
    {
        // Ganho passa pelo RunManager (toast + sinais), como uma recompensa; perda/ajuste só sincroniza o HUD.
        if (delta > 0 && c.RunManager is { } runManager)
            runManager.AddBloodCoins(delta);
        else
            RunState.AddRunBloodCoins(delta);
        c.Hud?.DevConsoleRefreshRun();
        c.Ok($"Moedas de Sangue: {RunState.RunBloodCoins}.");
        c.WarnIfNoRun();
    }

    private static void AddFury(DevCommandContext c, float amount, bool absolute)
    {
        var combat = c.PlayerCombat;
        if (combat == null)
        {
            c.Error("Sem jogador nesta cena.");
            return;
        }
        if (!combat.IsFuryUnlocked)
        {
            c.Error("Fúria bloqueada nesta run (falta a habilidade que a libera).");
            return;
        }
        if (absolute && amount < combat.MaxFury)
        {
            c.Error("Só dá para encher a Fúria (set fury <máximo>) ou somar; ela não diminui por comando.");
            return;
        }
        // AddFury aplica o multiplicador de Bloodcraft e limita no máximo.
        combat.AddFury(absolute ? combat.MaxFury * 2.0f : amount);
        c.Ok($"Fúria +{DevText.Number(absolute ? combat.MaxFury : amount)}.");
    }

    private static void Heal(DevCommandContext c)
    {
        if (c.RequireLivingPlayer() is not { } health)
            return;
        health.SetCurrentHealth(health.MaxHealth);
        c.Ok($"Vida cheia ({Mathf.RoundToInt(health.MaxHealth)}).");
    }

    private static void God(DevCommandContext c)
    {
        if (ParseToggle(c, 0, Health.DevGodMode) is not { } on)
            return;
        Health.DevGodMode = on;
        c.Ok(on ? "Modo deus ligado." : "Modo deus desligado.");
    }

    private static void Stamina(DevCommandContext c)
    {
        if (ParseToggle(c, 0, PlayerController.DevInfiniteStamina) is not { } on)
            return;
        PlayerController.DevInfiniteStamina = on;
        c.Ok(on ? "Stamina infinita ligada." : "Stamina infinita desligada.");
    }

    private static void Sprite(DevCommandContext c)
    {
        if (ParseToggle(c, 0, PlayerSpriteVisual.Enabled) is not { } on)
            return;
        PlayerSpriteVisual.Enabled = on;
        c.Ok(on ? "Virgil em sprite 2D." : "Virgil em modelo 3D.");
    }
}
