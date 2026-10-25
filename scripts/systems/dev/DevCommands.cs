using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Comandos embutidos do console de dev. Cada área fica num partial (DevCommands.&lt;Area&gt;.cs) com um Register&lt;Area&gt;;
/// <see cref="RegisterAll"/> chama todos. O "list" é criado aqui e cada área pendura os seus subcomandos nele.
/// Textos literais em pt-BR (regra do projeto para debug).
/// </summary>
public static partial class DevCommands
{
    private const int MaxListRows = 60;

    /// <summary>Raiz de "list": as áreas fazem ListRoot.Add(new DevCommand(...)).</summary>
    private static DevCommand ListRoot;

    public static void RegisterAll()
    {
        RegisterGeneral();
        RegisterRewards();
        RegisterPlayer();
        RegisterWorld();
    }

    // ---------- Geral: help, clear, status, list ----------

    private static void RegisterGeneral()
    {
        DevConsole.Register(new DevCommand("help", "Lista os comandos ou mostra o uso de um deles.", Help)
        {
            Aliases = new[] { "ajuda", "?", "h" },
            Usage = "[comando [subcomando]]",
            Examples = new[] { "help", "help give", "help give boon" },
            Complete = c => c.Count == 0 ? DevConsole.All.Select(x => x.Name) : CommandAt(c.Args)?.Subcommands.Select(x => x.Name) ?? Enumerable.Empty<string>()
        });

        DevConsole.Register(new DevCommand("clear", "Limpa a saída do console.", _ => DevConsole.Clear())
        {
            Aliases = new[] { "cls", "limpar" }
        });

        DevConsole.Register(new DevCommand("status", "Resumo da run: mapa, sala, vida, recursos, bênçãos, artefatos e mods.", Status)
        {
            Aliases = new[] { "info", "estado" }
        });

        ListRoot = new DevCommand("list", "Lista conteúdo do jogo (ids para usar em give/spawn).")
        {
            Aliases = new[] { "ls", "listar", "lista" }
        };
        ListRoot.Add(new DevCommand("commands", "Lista os comandos (o mesmo que help).", Help) { Aliases = new[] { "comandos", "cmds" } });
        DevConsole.Register(ListRoot);
    }

    private static DevCommand CommandAt(IReadOnlyList<string> path)
    {
        if (path.Count == 0)
            return null;
        var command = DevConsole.Find(path[0]);
        for (var i = 1; command != null && i < path.Count; i++)
            command = command.FindSubcommand(path[i]);
        return command;
    }

    private static void Help(DevCommandContext c)
    {
        if (c.Count > 0)
        {
            var command = CommandAt(c.Args);
            if (command == null)
            {
                c.Error($"Comando desconhecido: \"{c.Rest(0)}\".");
                return;
            }
            c.Print($"{command.FullName}{(string.IsNullOrEmpty(command.Usage) ? "" : " " + command.Usage)}");
            c.Print("  " + command.Summary);
            if (command.Aliases.Length > 0)
                c.Print("  Sinônimos: " + string.Join(", ", command.Aliases));
            foreach (var sub in command.Subcommands)
                c.Print($"  {sub.Name}{(string.IsNullOrEmpty(sub.Usage) ? "" : " " + sub.Usage)}: {sub.Summary}");
            foreach (var example in command.Examples)
                c.Print("  ex.: " + example);
            return;
        }

        c.Print("Comandos (a \"/\" é opcional; Tab completa; ↑/↓ histórico; \";\" separa comandos):");
        foreach (var command in DevConsole.All)
        {
            var subs = command.Subcommands.Count > 0 ? " [" + string.Join("|", command.Subcommands.Select(s => s.Name)) + "]" : "";
            c.Print($"  {command.Name}{subs}: {command.Summary}");
        }
        c.Print("Use help <comando> para detalhes e exemplos.");
    }

    private static void Status(DevCommandContext c)
    {
        c.Print(RunState.IsRunActive
            ? $"Run ativa: mapa {RunState.CurrentMapIndex + 1}/{CampaignCatalog.Count} ({RunState.CurrentMap.Name}), sala {RunState.CurrentRoom}{(RunState.IsBossRoom ? " (boss)" : "")}."
            : "Nenhuma run ativa.");
        if (c.PlayerHealth is { } health)
            c.Print($"Vida: {Mathf.RoundToInt(health.CurrentHealth)}/{Mathf.RoundToInt(health.MaxHealth)}{(Health.DevGodMode ? " (modo deus)" : "")}.");
        c.Print($"Moedas de Sangue: {RunState.RunBloodCoins}. Ícor: {CharacterSheet.CursedBlood}. Frutas: {RunState.DevilsFruitCount}.");
        if (c.RunManager is { } runManager)
            c.Print($"Inimigos: {runManager.DefeatedEnemies}/{runManager.TotalEnemies}{(runManager.IsRoomCleared ? " (sala limpa)" : "")}.");
        c.Print($"Arma: {WeaponLoadout.EquippedWeapon.DisplayName} ({WeaponLoadout.EquippedWeaponId}).");
        c.Print("Bênçãos: " + (RunState.RunBlessings.Count == 0 ? "nenhuma" : string.Join(", ", RunState.RunBlessings.Select(b => $"{b.DisplayName} [{b.Rarity} nv{b.Level}{(b.Stack > 1 ? $" x{b.Stack}" : "")}]"))) + ".");
        c.Print("Artefatos: " + (RunState.RunArtifacts.Count == 0 ? "nenhum" : string.Join(", ", RunState.RunArtifacts.Select(a => ArtifactCatalog.DisplayNameFor(a)))) + ".");
        c.Print("Mods de arma: " + (RunState.RunWeaponMods.Count == 0 ? "nenhum" : string.Join(", ", RunState.RunWeaponMods)) + ".");
        if (!Mathf.IsEqualApprox((float)Engine.TimeScale, 1.0f))
            c.Warn($"Escala de tempo: {DevText.Number(Engine.TimeScale)}×.");
    }

    // ---------- Ajudantes compartilhados ----------

    /// <summary>Liga/desliga a partir de "on/off/1/0/sim/nao"; sem argumento, inverte. Null = argumento inválido.</summary>
    private static bool? ParseToggle(DevCommandContext c, int index, bool current)
    {
        if (index >= c.Count)
            return !current;
        switch (DevText.Normalize(c.Arg(index)))
        {
            case "on": case "1": case "true": case "sim": case "liga": case "ligar": case "ligado":
                return true;
            case "off": case "0": case "false": case "nao": case "desliga": case "desligar": case "desligado":
                return false;
            case "toggle": case "alternar":
                return !current;
            default:
                c.UsageError($"Esperado on/off, recebido \"{c.Arg(index)}\".");
                return null;
        }
    }

    private static readonly string[] ToggleWords = { "on", "off" };

    /// <summary>Resolve uma busca única; com 0 ou várias correspondências escreve o erro (com sugestões) e devolve default.</summary>
    private static T PickOne<T>(DevCommandContext c, List<T> matches, string query, string what, Func<T, string> describe) where T : class
    {
        if (matches.Count == 1)
            return matches[0];
        if (matches.Count == 0)
        {
            c.Error($"Nenhum(a) {what} encontrado(a) para \"{query}\". Use list para ver os ids.");
            return null;
        }
        c.Warn($"\"{query}\" é ambíguo ({matches.Count} resultados). Seja mais específico ou use o id:");
        foreach (var match in matches.Take(12))
            c.Print("  " + describe(match));
        if (matches.Count > 12)
            c.Print($"  ... e mais {matches.Count - 12}.");
        return null;
    }

    /// <summary>Escreve no máximo MaxListRows linhas e avisa o que ficou de fora.</summary>
    private static void PrintRows(DevCommandContext c, IReadOnlyList<string> rows, string emptyMessage)
    {
        if (rows.Count == 0)
        {
            c.Warn(emptyMessage);
            return;
        }
        foreach (var row in rows.Take(MaxListRows))
            c.Print("  " + row);
        if (rows.Count > MaxListRows)
            c.Print($"  ... e mais {rows.Count - MaxListRows} (filtre a busca).");
    }

    /// <summary>Atualiza HUD e jogador depois de mexer na run (bênção, artefato, moedas, vida máxima).</summary>
    private static void RefreshRun(DevCommandContext c)
    {
        c.Tree?.CallGroup("player", "ApplyRuntimeTuning");
        c.Hud?.DevConsoleRefreshRun();
    }

    /// <summary>Ponto no chão perto do jogador (anel de ~1,8 m, um "slot" a cada 55°), dentro da área caminhável da sala.</summary>
    private static Vector3 NearPlayer(DevCommandContext c, int slot, float radius = 1.8f)
    {
        var origin = c.PlayerBody?.GlobalPosition ?? Vector3.Zero;
        var angle = Mathf.DegToRad(90.0f + slot * 55.0f);
        var position = origin + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * radius;
        if (c.RoomGenerator is { } generator)
            position = generator.ClampToWalkableArea(position, 1);
        position.Y = origin.Y + 0.15f;
        return position;
    }
}
