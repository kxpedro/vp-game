using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Recompensas da run: give (direto no inventário), spawn (item no chão, perto do jogador), offer (abre a oferta agora)
// e as listas de conteúdo (bênçãos, linhagens, raridades, artefatos, mods de arma, armas).
public static partial class DevCommands
{
    // Nomes aceitos para cada tipo de recompensa (spawn/give/doors). Comparados sem acento/caixa.
    private static readonly string[] BoonWords = { "boon", "boons", "blessing", "blessings", "bencao", "bencaos", "b" };
    private static readonly string[] AnvilWords = { "anvil", "bigorna", "forge", "mod" };
    private static readonly string[] FruitWords = { "fruit", "fruta", "fruta do diabo", "devils fruit" };
    private static readonly string[] CoinWords = { "coins", "coin", "moedas", "moeda", "gold", "ouro" };
    private static readonly string[] EnemyWords = { "enemy", "enemies", "mob", "mobs", "inimigo", "inimigos" };

    // Rótulos em pt-BR aceitos além do id/DisplayName da raridade de bênção.
    private static readonly Dictionary<string, string> BlessingRarityAliases = new()
    {
        ["comum"] = "common", ["c"] = "common", ["incomum"] = "uncommon", ["u"] = "uncommon",
        ["raro"] = "rare", ["rara"] = "rare", ["r"] = "rare", ["epico"] = "epic", ["epica"] = "epic", ["e"] = "epic",
        ["lendario"] = "legendary", ["lendaria"] = "legendary", ["l"] = "legendary"
    };

    private static bool IsWord(string token, string[] words)
    {
        var normalized = DevText.Normalize(token);
        return words.Any(w => DevText.Normalize(w) == normalized);
    }

    private static void RegisterRewards()
    {
        var give = new DevCommand("give", "Dá uma recompensa direto na run. Sem tipo, procura o nome em bênçãos, artefatos e mods.", GiveAny)
        {
            Aliases = new[] { "dar", "g" },
            Usage = "<boon|artifact|mod|fruit> <nome ou id> [raridade]",
            Examples = new[] { "give boon predatory_thirst", "give boon \"sede predatória\" epic", "give artifact ledger", "give fruit 3", "give orlok_rotten_bite" },
            Complete = _ => AllRewardIds()
        };
        give.Add(new DevCommand("boon", "Dá uma bênção (raridade padrão: Comum; Stirps tem poder fixo).", GiveBoon)
        {
            Aliases = BoonWords.Where(w => w != "boon").ToArray(),
            Usage = "<nome ou id> [raridade]",
            Complete = c => BloodBlessingCatalog.All.Select(b => b.Id).Concat(c.Count > 0 ? BlessingRarityNames() : Enumerable.Empty<string>())
        });
        give.Add(new DevCommand("artifact", "Dá um artefato da run.", GiveArtifact)
        {
            Aliases = new[] { "artifacts", "artefato", "artefatos", "art" },
            Usage = "<nome ou id>",
            Complete = _ => ArtifactCatalog.All.Select(a => a.Id)
        });
        give.Add(new DevCommand("mod", "Forja um mod de arma (Bigorna) sem passar pela oferta.", GiveMod)
        {
            Aliases = new[] { "mods", "weaponmod", "bigorna", "anvil" },
            Usage = "<nome ou id>",
            Complete = _ => WeaponModCatalog.All.Select(m => m.Id)
        });
        give.Add(new DevCommand("fruit", "Come Frutas do Diabo na hora (+vida máxima da run).", GiveFruit)
        {
            Aliases = FruitWords.Where(w => w != "fruit").ToArray(),
            Usage = "[quantidade]"
        });
        DevConsole.Register(give);

        DevConsole.Register(new DevCommand("spawn", "Põe itens no chão perto do jogador ou cria inimigos. Aceita vários de uma vez.", Spawn)
        {
            Aliases = new[] { "criar", "sp" },
            Usage = "<boon [linhagem]|anvil|fruit|coins [qtd]|enemy [arquetipo] [raridade]> ... (número antes = quantidade)",
            Examples = new[] { "spawn boon anvil", "spawn boon orlok", "spawn 3 fruit", "spawn coins 500", "spawn enemy tank rare", "spawn 5 enemy" },
            Complete = _ => new[] { "boon", "anvil", "fruit", "coins", "enemy" }
                .Concat(BloodBlessingCatalog.AllFamilies.Select(f => f.Id))
                .Concat(Enum.GetNames<EnemyArchetype>().Select(n => n.ToLowerInvariant()))
                .Concat(Enum.GetNames<EnemyRarity>().Select(n => n.ToLowerInvariant()))
        });

        var offer = new DevCommand("offer", "Abre agora a tela de oferta (sem item no chão nem porta).")
        {
            Aliases = new[] { "oferta", "open" }
        };
        offer.Add(new DevCommand("boon", "Oferta de Bênção (sorteio de porta, ou só da linhagem pedida).", OfferBoon)
        {
            Aliases = BoonWords.Where(w => w != "boon").ToArray(),
            Usage = "[linhagem]",
            Complete = _ => BloodBlessingCatalog.AllFamilies.Select(f => f.Id)
        });
        offer.Add(new DevCommand("anvil", "Oferta da Bigorna para a arma equipada.", OfferAnvil)
        {
            Aliases = AnvilWords.Where(w => w != "anvil").ToArray()
        });
        DevConsole.Register(offer);

        ListRoot.Add(new DevCommand("boons", "Bênçãos; filtra por linhagem (orlok, dracul...), por texto ou \"owned\" (as da run).", ListBoons)
        {
            Aliases = BoonWords.Where(w => w != "boons").ToArray(),
            Usage = "[linhagem|owned|texto]",
            Examples = new[] { "list boons", "list boons orlok", "list boons owned", "list boons dash" },
            Complete = _ => BloodBlessingCatalog.AllFamilies.Select(f => f.Id).Append("owned")
        });
        ListRoot.Add(new DevCommand("families", "Linhagens de bênção e quantas há de cada.", ListFamilies) { Aliases = new[] { "linhagens", "familias", "family" } });
        ListRoot.Add(new DevCommand("rarities", "Raridades de bênção (multiplicador de poder).", ListRarities) { Aliases = new[] { "raridades", "rarity" } });
        ListRoot.Add(new DevCommand("artifacts", "Artefatos; filtra por texto.", ListArtifacts)
        {
            Aliases = new[] { "artifact", "artefatos", "artefato", "art" },
            Usage = "[texto]"
        });
        ListRoot.Add(new DevCommand("mods", "Mods de arma (Bigorna); filtra pela arma (padrão: todas).", ListMods)
        {
            Aliases = new[] { "mod", "weaponmods", "bigorna", "anvil" },
            Usage = "[arma]",
            Complete = _ => WeaponCatalog.All.Select(w => w.Id)
        });
        ListRoot.Add(new DevCommand("weapons", "Armas do catálogo.", ListWeapons) { Aliases = new[] { "weapon", "armas", "arma" } });
    }

    private static IEnumerable<string> BlessingRarityNames() => BloodBlessingCatalog.AllRarities.Select(r => r.Id);

    private static IEnumerable<string> AllRewardIds() =>
        BloodBlessingCatalog.All.Select(b => b.Id)
            .Concat(ArtifactCatalog.All.Select(a => a.Id))
            .Concat(WeaponModCatalog.All.Select(m => m.Id));

    private static BloodBlessingFamily FindFamily(string token)
    {
        var normalized = DevText.Normalize(token);
        return BloodBlessingCatalog.AllFamilies.FirstOrDefault(f => DevText.Normalize(f.Id) == normalized || DevText.Normalize(f.DisplayName) == normalized);
    }

    private static BloodBlessingRarity FindBlessingRarity(string token)
    {
        var normalized = DevText.Normalize(token);
        if (BlessingRarityAliases.TryGetValue(normalized, out var aliased))
            normalized = aliased;
        return BloodBlessingCatalog.AllRarities.FirstOrDefault(r =>
            DevText.Normalize(r.Id) == normalized || DevText.Normalize(r.DisplayName) == normalized || DevText.Normalize(UiComponents.RarityLabel(r.DisplayName)) == normalized);
    }

    private static string DescribeBoon(BloodBlessingDefinition b)
    {
        var stack = RunState.GetBlessingStack(b.Id);
        var owned = stack > 0 ? $" — na run{(b.MaxStacks > 1 ? $" ({stack}/{b.MaxStacks})" : "")}" : "";
        return $"{b.Id} — {b.DisplayName} [{b.Family}]{owned}";
    }

    // ---------- give ----------

    private static void GiveAny(DevCommandContext c)
    {
        if (c.Count == 0)
        {
            c.UsageError();
            return;
        }
        var query = c.Rest(0);
        var boons = DevText.Find(BloodBlessingCatalog.All, query, b => b.Id, b => b.DisplayName);
        var artifacts = DevText.Find(ArtifactCatalog.All, query, a => a.Id, a => a.DisplayName);
        var mods = DevText.Find(WeaponModCatalog.All, query, m => m.Id, m => m.DisplayName);
        var total = boons.Count + artifacts.Count + mods.Count;
        if (total == 1)
        {
            if (boons.Count == 1) GrantBoon(c, boons[0], null);
            else if (artifacts.Count == 1) GrantArtifact(c, artifacts[0]);
            else GrantMod(c, mods[0]);
            return;
        }
        if (total == 0)
        {
            c.Error($"Nada com \"{query}\" em bênçãos, artefatos ou mods. Use list boons / list artifacts / list mods.");
            return;
        }
        c.Warn($"\"{query}\" é ambíguo ({total} resultados). Use give boon|artifact|mod <id>:");
        foreach (var b in boons.Take(8)) c.Print("  boon " + DescribeBoon(b));
        foreach (var a in artifacts.Take(8)) c.Print($"  artifact {a.Id} — {a.DisplayName}");
        foreach (var m in mods.Take(8)) c.Print($"  mod {m.Id} — {m.DisplayName}");
    }

    private static void GiveBoon(DevCommandContext c)
    {
        if (c.Count == 0)
        {
            c.UsageError();
            return;
        }
        // Último argumento pode ser a raridade ("give boon sede predatoria epic").
        BloodBlessingRarity rarity = null;
        var nameEnd = c.Count;
        if (c.Count > 1 && FindBlessingRarity(c.Arg(c.Count - 1)) is { } parsedRarity)
        {
            rarity = parsedRarity;
            nameEnd--;
        }
        var query = string.Join(" ", c.Args.Take(nameEnd));
        var matches = DevText.Find(BloodBlessingCatalog.All, query, b => b.Id, b => b.DisplayName);
        var definition = PickOne(c, matches, query, "bênção", DescribeBoon);
        if (definition != null)
            GrantBoon(c, definition, rarity);
    }

    private static void GrantBoon(DevCommandContext c, BloodBlessingDefinition definition, BloodBlessingRarity rarity)
    {
        var isStirps = definition.Rarity.Equals("Stirps", StringComparison.OrdinalIgnoreCase);
        rarity ??= BloodBlessingCatalog.AllRarities.FirstOrDefault();
        var rarityName = isStirps ? "Stirps" : rarity?.DisplayName ?? "Common";
        var power = isStirps ? 1.0f : rarity?.PowerMultiplier ?? 1.0f;
        var missingRequirements = RunState.GetBlessingStack(definition.Id) == 0 && !BloodBlessingCatalog.IsOfferable(definition);
        if (!RunState.AddBloodBlessing(new BloodBlessingOffer(definition, rarityName, power)))
        {
            c.Error($"{definition.DisplayName}: já está no máximo de acúmulos ({definition.MaxStacks}).");
            return;
        }
        CodexProgress.MarkDiscovered(CodexCategory.Blessing, definition.Id);
        RefreshRun(c);
        c.Ok($"Bênção: {definition.DisplayName} [{UiComponents.RarityLabel(rarityName)}] ({definition.Id}).");
        if (missingRequirements)
            c.Warn("Os pré-requisitos dela não estavam atendidos; o efeito pode não funcionar sozinho.");
        c.WarnIfNoRun();
    }

    private static void GiveArtifact(DevCommandContext c)
    {
        if (c.Count == 0)
        {
            c.UsageError();
            return;
        }
        var query = c.Rest(0);
        var definition = PickOne(c, DevText.Find(ArtifactCatalog.All, query, a => a.Id, a => a.DisplayName), query, "artefato",
            a => $"{a.Id} — {a.DisplayName} [{a.Rarity}]");
        if (definition != null)
            GrantArtifact(c, definition);
    }

    private static void GrantArtifact(DevCommandContext c, ArtifactDefinition definition)
    {
        RunState.AddTemporaryArtifact(ArtifactCatalog.CreateRecord(definition));
        RefreshRun(c);
        c.Ok($"Artefato: {definition.DisplayName} ({definition.Id}).");
        c.WarnIfNoRun();
    }

    private static void GiveMod(DevCommandContext c)
    {
        if (c.Count == 0)
        {
            c.UsageError();
            return;
        }
        var query = c.Rest(0);
        var definition = PickOne(c, DevText.Find(WeaponModCatalog.All, query, m => m.Id, m => m.DisplayName), query, "mod de arma",
            m => $"{m.Id} — {m.DisplayName} ({m.WeaponId})");
        if (definition != null)
            GrantMod(c, definition);
    }

    private static void GrantMod(DevCommandContext c, WeaponModDefinition definition)
    {
        if (!RunState.TryApplyWeaponMod(definition.Id))
        {
            c.Error($"{definition.DisplayName}: já forjado nesta run.");
            return;
        }
        RefreshRun(c);
        c.Ok($"Mod de arma: {definition.DisplayName} ({definition.Id}).");
        if (definition.WeaponId != WeaponLoadout.EquippedWeaponId)
            c.Warn($"É da arma {definition.WeaponId}; só vale com ela equipada (agora: {WeaponLoadout.EquippedWeaponId}).");
        c.WarnIfNoRun();
    }

    private static void GiveFruit(DevCommandContext c)
    {
        var count = 1;
        if (c.Count > 0 && (!DevText.TryInt(c.Arg(0), out count) || count <= 0))
        {
            c.UsageError("Quantidade inválida.");
            return;
        }
        EatFruits(c, count);
    }

    private static void EatFruits(DevCommandContext c, int count)
    {
        var gained = 0.0f;
        for (var i = 0; i < count; i++)
            gained += RunState.AddDevilsFruit();
        c.PlayerController?.ApplyRuntimeTuning();
        var extraHeal = (RunState.DevilsFruitHealAmount * count) - gained;
        if (extraHeal > 0.0f)
            c.PlayerHealth?.Heal(extraHeal);
        c.Hud?.DevConsoleRefreshRun();
        c.Ok($"{count} Fruta(s) do Diabo: +{Mathf.RoundToInt(gained)} de vida máxima (frutas na run: {RunState.DevilsFruitCount}).");
        c.WarnIfNoRun();
    }

    // ---------- spawn ----------

    private enum SpawnKind { Boon, Anvil, Fruit, Coins, Enemy }

    private sealed class SpawnRequest
    {
        public SpawnKind Kind;
        public int Count = 1;
        public int? Amount;
        public BloodBlessingFamily Family;
        public EnemyArchetype? Archetype;
        public EnemyRarity? Rarity;
    }

    private static SpawnKind? ParseSpawnKind(string token)
    {
        if (IsWord(token, BoonWords)) return SpawnKind.Boon;
        if (IsWord(token, AnvilWords)) return SpawnKind.Anvil;
        if (IsWord(token, FruitWords)) return SpawnKind.Fruit;
        if (IsWord(token, CoinWords)) return SpawnKind.Coins;
        if (IsWord(token, EnemyWords)) return SpawnKind.Enemy;
        return null;
    }

    private static EnemyArchetype? ParseArchetype(string token) => DevText.Normalize(token) switch
    {
        "melee" or "corpo a corpo" or "espadachim" => EnemyArchetype.Melee,
        "ranged" or "distancia" or "atirador" or "archer" => EnemyArchetype.Ranged,
        "tank" or "tanque" or "brute" => EnemyArchetype.Tank,
        _ => null
    };

    private static EnemyRarity? ParseEnemyRarity(string token) => DevText.Normalize(token) switch
    {
        "normal" or "common" or "comum" => EnemyRarity.Normal,
        "magic" or "magico" => EnemyRarity.Magic,
        "rare" or "raro" => EnemyRarity.Rare,
        "legendary" or "lendario" or "elite" => EnemyRarity.Legendary,
        _ => null
    };

    // Lê "spawn 3 boon orlok anvil coins 500 enemy tank rare": número antes de um tipo = quantidade; depois de "coins" = valor,
    // depois dos outros = quantidade. Linhagem fica com a bênção anterior; arquétipo/raridade, com o inimigo anterior
    // (arquétipo sozinho, "spawn tank", já cria um inimigo).
    private static List<SpawnRequest> ParseSpawn(DevCommandContext c)
    {
        var requests = new List<SpawnRequest>();
        int? pendingCount = null;
        SpawnRequest last = null;
        for (var i = 0; i < c.Count; i++)
        {
            var token = c.Arg(i);
            if (DevText.TryInt(token, out var number))
            {
                if (number <= 0)
                {
                    c.UsageError($"Número inválido: {token}.");
                    return null;
                }
                if (last != null && last.Kind == SpawnKind.Coins && last.Amount == null && pendingCount == null)
                    last.Amount = number;
                else if (last != null && last.Count == 1 && pendingCount == null && i > 0 && !DevText.TryInt(c.Arg(i - 1), out _) && last.Kind != SpawnKind.Coins)
                    last.Count = number;
                else
                    pendingCount = number;
                continue;
            }

            var kind = ParseSpawnKind(token);
            var archetype = ParseArchetype(token);
            if (kind == null && archetype != null && (last == null || last.Kind != SpawnKind.Enemy || last.Archetype != null))
                kind = SpawnKind.Enemy;
            if (kind != null)
            {
                last = new SpawnRequest { Kind = kind.Value, Count = pendingCount ?? 1, Archetype = archetype };
                pendingCount = null;
                requests.Add(last);
                continue;
            }

            if (last?.Kind == SpawnKind.Boon && last.Family == null && FindFamily(token) is { } family)
            {
                last.Family = family;
                continue;
            }
            if (last?.Kind == SpawnKind.Enemy)
            {
                if (archetype != null && last.Archetype == null) { last.Archetype = archetype; continue; }
                if (ParseEnemyRarity(token) is { } rarity && last.Rarity == null) { last.Rarity = rarity; continue; }
            }
            c.UsageError($"Não entendi \"{token}\" (tipos: boon, anvil, fruit, coins, enemy).");
            return null;
        }
        if (pendingCount != null)
        {
            c.UsageError($"Quantidade {pendingCount} sem tipo depois dela.");
            return null;
        }
        return requests;
    }

    private static void Spawn(DevCommandContext c)
    {
        if (c.Count == 0)
        {
            c.UsageError();
            return;
        }
        var requests = ParseSpawn(c);
        if (requests == null)
            return;
        var parent = c.Tree?.CurrentScene;
        if (parent == null || c.PlayerBody == null)
        {
            c.Error("Sem cena de jogo com jogador.");
            return;
        }

        var slot = 0;
        var mapIndex = Mathf.Clamp(RunState.CurrentMapIndex, 0, 3);
        foreach (var request in requests)
        {
            switch (request.Kind)
            {
                case SpawnKind.Enemy:
                    SpawnEnemies(c, request);
                    break;
                case SpawnKind.Fruit:
                    for (var i = 0; i < request.Count; i++)
                        DevilsFruit.Spawn(parent, NearPlayer(c, slot++));
                    c.Ok($"{request.Count} Fruta(s) do Diabo no chão.");
                    break;
                case SpawnKind.Coins:
                    var amount = request.Amount ?? RoomRewardCatalog.CoinPackAmount(mapIndex);
                    for (var i = 0; i < request.Count; i++)
                        SpawnDevPickup(c, parent, new RoomRewardDoor(RoomRewardType.Coins, mapIndex, amount), slot++);
                    c.Ok($"{request.Count} pilha(s) de {amount} Moedas no chão.");
                    break;
                case SpawnKind.Anvil:
                    if (!RunState.DevAnvilPoolAvailable())
                        c.Warn($"A arma equipada não tem mod livre: ao pegar, a Bigorna vira Moedas de fallback.");
                    for (var i = 0; i < request.Count; i++)
                        SpawnDevPickup(c, parent, new RoomRewardDoor(RoomRewardType.Anvil, mapIndex, 0), slot++);
                    c.Ok($"{request.Count} Bigorna(s) no chão.");
                    break;
                case SpawnKind.Boon:
                    for (var i = 0; i < request.Count; i++)
                    {
                        var plan = RollDevBlessingPlan(request.Family);
                        if (plan == null)
                        {
                            c.Warn(request.Family == null ? "Não há bênção elegível para oferecer." : $"Nenhuma bênção elegível da linhagem {request.Family.DisplayName}.");
                            break;
                        }
                        SpawnDevPickup(c, parent, new RoomRewardDoor(RoomRewardType.Blessing, mapIndex, 0, plan), slot++);
                        if (i == request.Count - 1)
                            c.Ok($"{request.Count} Bênção(ões) no chão{(request.Family != null ? $" ({request.Family.DisplayName})" : "")}.");
                    }
                    break;
            }
        }
        if (requests.Any(r => r.Kind != SpawnKind.Enemy))
            c.Print("Feche o console e interaja com o item para pegar.");
    }

    private static RoomBlessingPlan RollDevBlessingPlan(BloodBlessingFamily family)
    {
        if (family == null)
            return RunState.DevRollBlessingPlan();
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var offers = BloodBlessingCatalog.DevRollFamilyOffers(rng, family.DisplayName, Math.Max(1, CompendiumTuning.GetInt("reward.blessing.choices", 3)));
        return offers.Count == 0 ? null : new RoomBlessingPlan(family.DisplayName, offers);
    }

    // Item avulso do console: não conta nos itens da sala (não trava nem destrava portas) e paga a si mesmo ao ser pego.
    private static void SpawnDevPickup(DevCommandContext c, Node parent, RoomRewardDoor door, int slot)
    {
        var pickup = RewardPickup.Spawn(parent, NearPlayer(c, slot), door);
        if (pickup == null)
            return;
        var tree = c.Tree;
        pickup.Claimed += () => ClaimDevPickup(tree, door);
    }

    private static void ClaimDevPickup(SceneTree tree, RoomRewardDoor door)
    {
        if (tree == null)
            return;
        switch (door.Type)
        {
            case RoomRewardType.Blessing:
                RunState.DevSetDeliveredBlessingPlan(door.BlessingPlan);
                tree.CallGroup("hud", "OpenBloodBlessingOffer");
                break;
            case RoomRewardType.Anvil:
                tree.CallGroup("hud", "OpenAnvilOffer");
                break;
            case RoomRewardType.Coins:
                if (tree.GetFirstNodeInGroup("run_manager") is RunManager runManager)
                    runManager.AddBloodCoins(door.CoinAmount);
                else
                {
                    RunState.AddRunBloodCoins(door.CoinAmount);
                    tree.CallGroup("hud", "NotifyBloodCoinsRewarded", door.CoinAmount, RunState.RunBloodCoins);
                }
                break;
        }
    }

    private static void SpawnEnemies(DevCommandContext c, SpawnRequest request)
    {
        var runManager = c.RunManager;
        if (runManager == null || c.RoomGenerator == null)
        {
            c.Error("Inimigos só nascem na arena (cena com RunManager e gerador de sala).");
            return;
        }
        var created = runManager.DevSpawnEnemies(request.Count, request.Rarity, request.Archetype, c.PlayerBody.GlobalPosition);
        var archetype = request.Archetype?.ToString().ToLowerInvariant() ?? "arquétipo sorteado";
        var rarity = (request.Rarity ?? EnemyRarity.Normal).ToString().ToLowerInvariant();
        if (created > 0)
            c.Ok($"{created} inimigo(s) ({archetype}, {rarity}) perto do jogador{(runManager.IsRoomCleared ? "" : "; entram no total da sala")}.");
        else
            c.Error("Nenhum inimigo criado (a cena não tem a cena de inimigo configurada).");
    }

    // ---------- offer ----------

    private static void OfferBoon(DevCommandContext c)
    {
        BloodBlessingFamily family = null;
        if (c.Count > 0)
        {
            family = FindFamily(c.Arg(0));
            if (family == null)
            {
                c.UsageError($"Linhagem desconhecida: \"{c.Arg(0)}\" ({string.Join(", ", BloodBlessingCatalog.AllFamilies.Select(f => f.Id))}).");
                return;
            }
        }
        var plan = RollDevBlessingPlan(family);
        if (plan == null)
        {
            c.Error(family == null ? "Não há bênção para oferecer." : $"Nenhuma bênção elegível da linhagem {family.DisplayName}.");
            return;
        }
        RunState.DevSetDeliveredBlessingPlan(plan);
        c.CloseConsole = true;
        if (c.Hud != null)
            Callable.From(c.Hud.OpenBloodBlessingOffer).CallDeferred();
        c.Ok($"Oferta de Bênção ({plan.Family}): {string.Join(", ", plan.Offers.Select(o => o.Definition.DisplayName))}.");
    }

    private static void OfferAnvil(DevCommandContext c)
    {
        if (!RunState.DevAnvilPoolAvailable())
        {
            c.Error("A arma equipada não tem mod livre (a oferta só pagaria Moedas de fallback).");
            return;
        }
        c.CloseConsole = true;
        if (c.Hud != null)
            Callable.From(c.Hud.OpenAnvilOffer).CallDeferred();
        c.Ok("Oferta da Bigorna aberta.");
    }

    // ---------- list ----------

    private static void ListBoons(DevCommandContext c)
    {
        IEnumerable<BloodBlessingDefinition> boons = BloodBlessingCatalog.All;
        var title = "Bênçãos";
        if (c.Count > 0)
        {
            var filter = c.Rest(0);
            if (FindFamily(filter) is { } family)
            {
                boons = boons.Where(b => b.Family.Equals(family.DisplayName, StringComparison.OrdinalIgnoreCase));
                title = $"Bênçãos de {family.DisplayName}";
            }
            else if (DevText.Normalize(filter) is "owned" or "run" or "minhas" or "possuidas")
            {
                boons = boons.Where(b => RunState.GetBlessingStack(b.Id) > 0);
                title = "Bênçãos da run";
            }
            else
            {
                boons = DevText.Find(boons, filter, b => b.Id, b => b.DisplayName);
                title = $"Bênçãos com \"{filter}\"";
            }
        }
        var rows = boons.Select(DescribeBoon).ToList();
        c.Print($"{title} ({rows.Count}):");
        PrintRows(c, rows, "Nenhuma.");
    }

    private static void ListFamilies(DevCommandContext c)
    {
        c.Print("Linhagens:");
        foreach (var family in BloodBlessingCatalog.AllFamilies)
        {
            var count = BloodBlessingCatalog.All.Count(b => b.Family.Equals(family.DisplayName, StringComparison.OrdinalIgnoreCase));
            c.Print($"  {family.Id} — {family.DisplayName} ({count} bênçãos, efeito {family.StatusEffect})");
        }
    }

    private static void ListRarities(DevCommandContext c)
    {
        c.Print("Raridades de bênção:");
        foreach (var rarity in BloodBlessingCatalog.AllRarities)
            c.Print($"  {rarity.Id} — {UiComponents.RarityLabel(rarity.DisplayName)} (poder ×{DevText.Number(rarity.PowerMultiplier)}, peso {DevText.Number(rarity.Weight)})");
    }

    private static void ListArtifacts(DevCommandContext c)
    {
        IEnumerable<ArtifactDefinition> artifacts = ArtifactCatalog.All;
        if (c.Count > 0)
            artifacts = DevText.Find(artifacts, c.Rest(0), a => a.Id, a => a.DisplayName);
        var rows = artifacts.Select(a => $"{a.Id} — {a.DisplayName} [{a.Rarity}]{(RunState.HasRunArtifact(a.Id) ? " — na run" : "")}").ToList();
        c.Print($"Artefatos ({rows.Count}):");
        PrintRows(c, rows, "Nenhum.");
    }

    private static void ListMods(DevCommandContext c)
    {
        IEnumerable<WeaponModDefinition> mods = WeaponModCatalog.All;
        if (c.Count > 0)
        {
            var weapon = DevText.Normalize(c.Arg(0));
            mods = mods.Where(m => DevText.Normalize(m.WeaponId) == weapon);
        }
        var rows = mods.Select(m => $"{m.Id} — {m.DisplayName} ({m.WeaponId}){(RunState.HasWeaponMod(m.Id) ? " — forjado" : "")}").ToList();
        c.Print($"Mods de arma ({rows.Count}); arma equipada: {WeaponLoadout.EquippedWeaponId}:");
        PrintRows(c, rows, "Nenhum.");
    }

    private static void ListWeapons(DevCommandContext c)
    {
        c.Print("Armas:");
        foreach (var weapon in WeaponCatalog.All)
            c.Print($"  {weapon.Id} — {weapon.DisplayName}{(weapon.Id == WeaponLoadout.EquippedWeaponId ? " (equipada)" : "")}{(weapon.IsImplemented ? "" : " (não implementada)")}");
    }
}
