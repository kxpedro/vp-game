using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Mundo e sala: kill, room, jump, doors, time e tune (parâmetros do DevParameterRegistry, os mesmos do painel F1).
public static partial class DevCommands
{
    private static readonly string[] DoorTypeWords = { "boon", "anvil", "fruit", "coins", "auto" };

    private static void RegisterWorld()
    {
        DevConsole.Register(new DevCommand("kill", "Mata inimigos (all/mobs = todos, boss = só o chefe) ou o próprio jogador (player).", Kill)
        {
            Aliases = new[] { "matar", "k" },
            Usage = "[all|mobs|boss|player]",
            Examples = new[] { "kill all mobs", "kill boss", "kill player" },
            Complete = _ => new[] { "all", "mobs", "boss", "player" }
        });

        var room = new DevCommand("room", "Ações da sala atual (arena).") { Aliases = new[] { "sala" } };
        room.Add(new DevCommand("complete", "Encerra a sala como limpa: mata tudo, cancela as waves e paga itens e portas.", RoomComplete)
        {
            Aliases = new[] { "clear", "completar", "limpar", "win" },
            NeedsArena = true
        });
        room.Add(new DevCommand("reroll", "Sorteia de novo as portas desta sala (respeita o override de doors).", RoomReroll)
        {
            Aliases = new[] { "rerrolar", "doors", "portas" },
            NeedsArena = true
        });
        DevConsole.Register(room);

        DevConsole.Register(new DevCommand("jump", "Pula para um mapa/sala (recarrega a arena; inicia a run se estiver no hub).", Jump)
        {
            Aliases = new[] { "pular", "goto", "tp" },
            Usage = "<mapa 1-4> [sala|boss]  ou  boss (chefe do mapa atual)",
            Examples = new[] { "jump 2", "jump 3 4", "jump 2 boss", "jump boss" },
            Complete = c => c.Count <= 1 ? new[] { "boss", "1", "2", "3", "4" } : Enumerable.Empty<string>()
        });

        DevConsole.Register(new DevCommand("doors", "Força o tipo das próximas portas de recompensa (como no painel F1). off = sorteio normal.", Doors)
        {
            Aliases = new[] { "portas" },
            Usage = "<tipo> [tipo]  |  <1|2>  |  off   (tipos: boon, anvil, fruit, coins, auto)",
            Examples = new[] { "doors anvil", "doors boon fruit", "doors 2", "doors off" },
            Complete = c => c.Count == 0 ? DoorTypeWords.Append("off").Append("1").Append("2") : DoorTypeWords
        });

        DevConsole.Register(new DevCommand("time", "Escala de tempo (0.1 a 4; 1 = normal). Sem argumento, mostra a atual.", TimeScale)
        {
            Aliases = new[] { "tempo", "timescale", "slowmo" },
            Usage = "[escala]",
            Examples = new[] { "time 0.25", "time 1" }
        });

        DevConsole.Register(new DevCommand("tune", "Lê, altera ou reseta um parâmetro do painel F1 (vai para dev_tuning.json).", Tune)
        {
            Aliases = new[] { "param", "tuning", "cvar" },
            Usage = "<chave> [valor|reset]",
            Examples = new[] { "tune economy.fruit.max_health", "tune room.waves.count 1", "tune room.waves.count reset" },
            Complete = c => c.Count == 0 ? DevParameterRegistry.All.Select(p => p.Key) : new[] { "reset" }
        });

        ListRoot.Add(new DevCommand("enemies", "Inimigos vivos na cena, arquétipos e raridades aceitos por spawn enemy.", ListEnemies)
        {
            Aliases = new[] { "enemy", "mobs", "inimigos" }
        });
        ListRoot.Add(new DevCommand("params", "Parâmetros do painel F1 (chave = valor); filtra por texto.", ListParams)
        {
            Aliases = new[] { "param", "tuning", "keys" },
            Usage = "[texto] [modified]",
            Examples = new[] { "list params fruit", "list params modified" }
        });
    }

    // ---------- kill ----------

    private static void Kill(DevCommandContext c)
    {
        var words = c.Args.Select(DevText.Normalize).ToList();
        if (words.Any(w => w is "player" or "me" or "jogador" or "eu" or "self"))
        {
            if (c.RequireLivingPlayer() is not { } playerHealth)
                return;
            if (Health.DevGodMode)
                c.Warn("Modo deus ligado: a morte forçada passa por cima dele.");
            c.CloseConsole = true;
            playerHealth.ForceDeath("dev");
            c.Ok("Jogador morto.");
            return;
        }

        var bossOnly = words.Any(w => w is "boss" or "chefe");
        var unknown = words.FirstOrDefault(w => w is not ("all" or "mobs" or "mob" or "enemies" or "inimigos" or "todos" or "tudo" or "boss" or "chefe"));
        if (unknown != null)
        {
            c.UsageError($"Não entendi \"{unknown}\".");
            return;
        }

        var killed = 0;
        foreach (var node in c.Tree.GetNodesInGroup("enemies"))
        {
            if (bossOnly && node.GetNodeOrNull<BossController>("BossController") == null)
                continue;
            var health = node.GetNodeOrNull<Health>("Health");
            if (health == null || health.IsDead)
                continue;
            health.ForceDeath("dev");
            killed++;
        }
        if (killed == 0)
            c.Warn(bossOnly ? "Nenhum chefe vivo nesta cena." : "Nenhum inimigo vivo nesta cena.");
        else
            c.Ok($"{killed} {(bossOnly ? "chefe(s)" : "inimigo(s)")} morto(s). Waves futuras continuam (room complete encerra a sala).");
    }

    // ---------- room ----------

    private static void RoomComplete(DevCommandContext c)
    {
        var runManager = c.RunManager;
        if (runManager.IsRoomCleared)
        {
            c.Warn("A sala já está limpa.");
            return;
        }
        if (runManager.DevCompleteRoom())
            c.Ok("Sala completada: recompensas e portas liberadas conforme o fluxo normal.");
        else
            c.Error("Uma wave está surgindo; tente de novo em instantes.");
    }

    private static void RoomReroll(DevCommandContext c)
    {
        if (RunState.IsBossRoom)
        {
            c.Error("A sala do boss não tem conjunto de portas.");
            return;
        }
        var set = c.RunManager.DevRerollRewardDoors();
        if (set == null)
        {
            c.Error("Esta saída não tem conjunto de portas (a sala N leva ao boss).");
            return;
        }
        var names = string.Join(" | ", set.Doors.Select(d => RoomRewardCatalog.DisplayName(d.Type)));
        c.Ok(c.RunManager.IsRoomCleared ? $"Portas recriadas: {names}." : $"Conjunto sorteado: {names}. As portas aparecem ao limpar a sala.");
    }

    // ---------- jump ----------

    private static void Jump(DevCommandContext c)
    {
        if (c.Count == 0)
        {
            c.UsageError();
            return;
        }
        int map;
        var argIndex = 0;
        if (DevText.Normalize(c.Arg(0)) is "boss" or "chefe")
            map = RunState.IsRunActive ? RunState.CurrentMapIndex + 1 : 1;
        else if (!DevText.TryInt(c.Arg(argIndex++), out map) || map < 1 || map > CampaignCatalog.Count)
        {
            c.UsageError($"Mapa deve ser de 1 a {CampaignCatalog.Count}.");
            return;
        }

        var room = 1;
        var roomToken = argIndex == 0 ? c.Arg(0) : c.Arg(argIndex);
        if (DevText.Normalize(roomToken) is "boss" or "chefe")
            room = int.MaxValue;
        else if (roomToken.Length > 0 && (!DevText.TryInt(roomToken, out room) || room < 1))
        {
            c.UsageError("Sala deve ser um número >= 1 ou boss.");
            return;
        }
        if (c.Hud == null)
        {
            c.Error("Sem HUD nesta cena.");
            return;
        }
        c.Ok(room == int.MaxValue ? $"Indo para o boss do mapa {map}..." : $"Indo para o mapa {map}, sala {room}...");
        c.CloseConsole = true;
        var hud = c.Hud;
        Callable.From(() => hud.DevConsoleJump(map - 1, room)).CallDeferred();
    }

    // ---------- doors ----------

    private static RoomRewardType? ParseDoorType(string token, out bool valid)
    {
        valid = true;
        if (IsWord(token, BoonWords)) return RoomRewardType.Blessing;
        if (IsWord(token, AnvilWords)) return RoomRewardType.Anvil;
        if (IsWord(token, FruitWords)) return RoomRewardType.DevilsFruit;
        if (IsWord(token, CoinWords)) return RoomRewardType.Coins;
        valid = DevText.Normalize(token) is "auto" or "any" or "qualquer";
        return null;
    }

    private static void Doors(DevCommandContext c)
    {
        if (c.Count == 0)
        {
            c.Print(RunState.DevForceDoors
                ? $"Override ligado: {(RunState.DevForcedDoorCount == 0 ? "quantidade auto" : $"{RunState.DevForcedDoorCount} porta(s)")}, tipos {DoorName(RunState.DevForcedType(0))} | {DoorName(RunState.DevForcedType(1))}."
                : "Override desligado (sorteio normal).");
            c.UsageError();
            return;
        }
        if (DevText.Normalize(c.Arg(0)) is "off" or "normal" or "desligar")
        {
            RunState.DevSetForceDoors(false);
            c.Ok("Forçar portas desligado: sorteio normal.");
            return;
        }
        if (c.Count > 2)
        {
            c.UsageError("No máximo 2 portas.");
            return;
        }

        int count;
        var types = new RoomRewardType?[2];
        if (c.Count == 1 && DevText.TryInt(c.Arg(0), out count))
        {
            if (count is < 1 or > 2)
            {
                c.UsageError("Quantidade de portas: 1 ou 2.");
                return;
            }
        }
        else
        {
            count = c.Count;
            for (var i = 0; i < c.Count; i++)
            {
                types[i] = ParseDoorType(c.Arg(i), out var valid);
                if (!valid)
                {
                    c.UsageError($"Tipo de porta desconhecido: \"{c.Arg(i)}\".");
                    return;
                }
            }
        }
        RunState.DevSetForceDoors(true);
        RunState.DevSetForcedDoorCount(count);
        RunState.DevSetForcedType(0, types[0]);
        RunState.DevSetForcedType(1, types[1]);
        c.Ok($"Forçar portas: {count} porta(s), {DoorName(types[0])}{(count > 1 ? " | " + DoorName(types[1]) : "")}. Vale nas próximas saídas ainda não sorteadas; room reroll aplica nesta sala.");
        if (types.Contains(RoomRewardType.Anvil) && !RunState.DevAnvilPoolAvailable())
            c.Warn("A arma equipada não tem mod livre: a porta de Bigorna não aparece.");
    }

    private static string DoorName(RoomRewardType? type) => type == null ? "auto" : RoomRewardCatalog.DisplayName(type.Value);

    // ---------- time ----------

    private static void TimeScale(DevCommandContext c)
    {
        if (c.Count == 0)
        {
            c.Print($"Escala de tempo: {DevText.Number(Engine.TimeScale)}×.");
            return;
        }
        if (!DevText.TryNumber(c.Arg(0), out var scale) || scale < 0.1 || scale > 4.0)
        {
            c.UsageError("Escala entre 0.1 e 4.");
            return;
        }
        CombatFeedback.DevSetBaseTimeScale((float)scale);
        c.Hud?.DevConsoleTimeChanged();
        c.Ok($"Escala de tempo: {DevText.Number(Engine.TimeScale)}×{(Mathf.IsEqualApprox((float)scale, 1.0f) ? " (normal)" : "; um aviso fica na tela até voltar a 1")}.");
    }

    // ---------- tune ----------

    private static string DescribeParam(DevParameter p)
    {
        var value = DevParameterRegistry.Get(p.Key);
        var modified = DevParameterRegistry.IsModified(p.Key) ? " *" : "";
        return $"{p.Key} = {DevText.Number(value)}{modified} (padrão {DevText.Number(p.Default)}, {DevText.Number(p.Min)}..{DevText.Number(p.Max)}) — {p.Label}";
    }

    private static void Tune(DevCommandContext c)
    {
        if (c.Count == 0 || c.Count > 2)
        {
            c.UsageError();
            return;
        }
        var parameter = DevParameterRegistry.Find(c.Arg(0));
        if (parameter == null)
        {
            var candidates = DevParameterRegistry.All.Where(p => p.Key.Contains(c.Arg(0), StringComparison.OrdinalIgnoreCase)).Take(10).ToList();
            c.Error($"Parâmetro não registrado: \"{c.Arg(0)}\".");
            foreach (var p in candidates)
                c.Print("  " + p.Key);
            return;
        }
        if (c.Count == 1)
        {
            c.Print(DescribeParam(parameter));
            if (!string.IsNullOrEmpty(parameter.Note))
                c.Print("  " + parameter.Note);
            return;
        }
        if (DevText.Normalize(c.Arg(1)) is "reset" or "default" or "padrao")
            DevParameterRegistry.Reset(parameter.Key);
        else if (DevText.TryNumber(c.Arg(1), out var value))
        {
            if (value < parameter.Min || value > parameter.Max)
                c.Warn($"Fora da faixa: limitado a {DevText.Number(parameter.Min)}..{DevText.Number(parameter.Max)}.");
            DevParameterRegistry.Set(parameter.Key, value);
        }
        else
        {
            c.UsageError("Valor deve ser um número ou reset.");
            return;
        }
        c.Hud?.DevConsoleApplyTuning();
        c.Ok(DescribeParam(parameter));
        var when = parameter.Apply switch
        {
            DevApplyMode.NewObjects => "Vale para inimigos/objetos criados a partir de agora.",
            DevApplyMode.NextRoom => "Vale a partir da próxima sala/sorteio.",
            _ => null
        };
        if (when != null)
            c.Print(when);
    }

    private static void ListParams(DevCommandContext c)
    {
        var words = c.Args.Select(DevText.Normalize).ToList();
        var onlyModified = words.RemoveAll(w => w is "modified" or "alterados" or "changed") > 0;
        var parameters = DevParameterRegistry.All.Where(p => !onlyModified || DevParameterRegistry.IsModified(p.Key));
        foreach (var word in words)
            parameters = parameters.Where(p => DevText.Normalize(p.Key + " " + p.Label + " " + p.Group).Contains(word, StringComparison.Ordinal));
        var rows = parameters.Select(DescribeParam).ToList();
        c.Print($"Parâmetros ({rows.Count}){(onlyModified ? ", só alterados" : "")}:");
        PrintRows(c, rows, "Nenhum.");
    }

    // ---------- list enemies ----------

    private static void ListEnemies(DevCommandContext c)
    {
        var alive = new List<string>();
        foreach (var node in c.Tree.GetNodesInGroup("enemies"))
        {
            if (node is not EnemyBasic enemy || enemy.GetNodeOrNull<Health>("Health") is not { IsDead: false } health)
                continue;
            var boss = enemy.GetNodeOrNull<BossController>("BossController") != null ? " (chefe)" : "";
            alive.Add($"{enemy.Name}: {enemy.Archetype.ToString().ToLowerInvariant()} {enemy.Rarity.ToString().ToLowerInvariant()}{boss}, vida {Mathf.RoundToInt(health.CurrentHealth)}/{Mathf.RoundToInt(health.MaxHealth)}");
        }
        c.Print($"Inimigos vivos ({alive.Count}):");
        PrintRows(c, alive, "Nenhum inimigo vivo nesta cena.");
        c.Print("Arquétipos: " + string.Join(", ", Enum.GetNames<EnemyArchetype>().Select(n => n.ToLowerInvariant())) + ".");
        c.Print("Raridades: " + string.Join(", ", Enum.GetNames<EnemyRarity>().Select(n => n.ToLowerInvariant())) + ".");
    }
}
