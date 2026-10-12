using System;
using System.Collections.Generic;

public enum RunOutcome { None, Died, Abandoned, Victory }

public sealed record RunEndInfo(RunOutcome Outcome, int MapIndex, int Room, bool BossRoom)
{
    public string BossId => BossRoom ? BossCatalog.Get(MapIndex).Id : "";
}

/// <summary>
/// Progresso do dialogo (padrao CodexProgress): contadores, falas vistas e flags vao no save
/// (GameSaveData.Dialogue); LastRun e _abandonPending sao da sessao.
/// </summary>
public static class DialogueProgress
{
    private static readonly Dictionary<string, int> Seen = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> Plays = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Flags = new(StringComparer.Ordinal);
    private static readonly HashSet<string> BossesDefeated = new(StringComparer.Ordinal);
    private static bool _abandonPending;

    public static int RunsStarted { get; private set; }
    public static int RunsCompleted { get; private set; }
    public static int Deaths { get; private set; }
    /// <summary>null = nenhuma run terminou nesta sessao.</summary>
    public static RunEndInfo LastRun { get; private set; }

    public static bool IsSeen(string conversationId) => GetSeen(conversationId) > 0;

    public static int GetSeen(string conversationId)
        => conversationId != null && Seen.TryGetValue(conversationId, out var value) ? value : 0;

    public static int GetPlays(string pool) => pool != null && Plays.TryGetValue(pool, out var value) ? value : 0;

    /// <summary>Plays[pool] += 1; Seen[id] = Plays[pool] (4.3).</summary>
    public static void MarkSeen(DialogueDefinition definition)
    {
        if (definition == null) return;
        var plays = GetPlays(definition.Pool) + 1;
        Plays[definition.Pool] = plays;
        Seen[definition.Id] = plays;
        SaveGameManager.RequestAutosave();
    }

    /// <summary>Relogio por oportunidade: avanca Plays[pool] sem marcar conversa (destrava cooldowns, inclusive de saves antigos).</summary>
    public static void AdvancePlays(string pool)
    {
        if (string.IsNullOrEmpty(pool)) return;
        Plays[pool] = GetPlays(pool) + 1;
        SaveGameManager.RequestAutosave();
    }

    public static bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && Flags.Contains(flag);

    public static bool SetFlag(string flag)
    {
        if (string.IsNullOrWhiteSpace(flag) || !Flags.Add(flag)) return false;
        SaveGameManager.RequestAutosave();
        return true;
    }

    public static bool IsBossDefeated(string bossId) => !string.IsNullOrEmpty(bossId) && BossesDefeated.Contains(bossId);

    public static bool MarkBossDefeated(string bossId)
    {
        if (string.IsNullOrWhiteSpace(bossId) || !BossesDefeated.Add(bossId)) return false;
        SaveGameManager.RequestAutosave();
        return true;
    }

    public static void OnRunStarted()
    {
        RunsStarted++;
        LastRun = null;
        _abandonPending = false;
        SaveGameManager.RequestAutosave();
    }

    /// <summary>Chamar antes dos resets do RunState: le o mapa/sala finais.</summary>
    public static void OnRunCompleted()
    {
        RunsCompleted++;
        LastRun = new RunEndInfo(RunOutcome.Victory, RunState.CurrentMapIndex, RunState.CurrentRoom, true);
        SaveGameManager.RequestAutosave();
    }

    public static void OnRunEnded(bool runWasActive, int mapIndex, int room, bool bossRoom)
    {
        if (!runWasActive) return;
        if (_abandonPending)
        {
            _abandonPending = false;
            LastRun = new RunEndInfo(RunOutcome.Abandoned, mapIndex, room, bossRoom);
            return;
        }
        Deaths++;
        LastRun = new RunEndInfo(RunOutcome.Died, mapIndex, room, bossRoom);
        SaveGameManager.RequestAutosave();
    }

    /// <summary>Chamado pelo HUD ao detectar desistencia, antes do FailRun.</summary>
    public static void FlagAbandon() => _abandonPending = true;

    public static DialogueSaveData ToSaveData()
    {
        var bosses = new List<string>(BossesDefeated);
        bosses.Sort(StringComparer.Ordinal);
        var flags = new List<string>(Flags);
        flags.Sort(StringComparer.Ordinal);
        return new DialogueSaveData
        {
            RunsStarted = RunsStarted,
            RunsCompleted = RunsCompleted,
            Deaths = Deaths,
            BossesDefeated = bosses,
            Seen = new Dictionary<string, int>(Seen),
            Plays = new Dictionary<string, int>(Plays),
            Flags = flags
        };
    }

    /// <summary>Null seguro (save antigo). IDs desconhecidos sao preservados.</summary>
    public static void LoadSaveData(DialogueSaveData data)
    {
        Reset();
        if (data == null) return;
        RunsStarted = Math.Max(data.RunsStarted, 0);
        RunsCompleted = Math.Max(data.RunsCompleted, 0);
        Deaths = Math.Max(data.Deaths, 0);
        if (data.BossesDefeated != null)
            foreach (var id in data.BossesDefeated) if (!string.IsNullOrWhiteSpace(id)) BossesDefeated.Add(id);
        if (data.Flags != null)
            foreach (var flag in data.Flags) if (!string.IsNullOrWhiteSpace(flag)) Flags.Add(flag);
        if (data.Seen != null)
            foreach (var pair in data.Seen) if (!string.IsNullOrWhiteSpace(pair.Key)) Seen[pair.Key] = Math.Max(pair.Value, 1);
        if (data.Plays != null)
            foreach (var pair in data.Plays) if (!string.IsNullOrWhiteSpace(pair.Key)) Plays[pair.Key] = Math.Max(pair.Value, 0);
    }

    public static void Reset()
    {
        Seen.Clear();
        Plays.Clear();
        Flags.Clear();
        BossesDefeated.Clear();
        RunsStarted = 0;
        RunsCompleted = 0;
        Deaths = 0;
        LastRun = null;
        _abandonPending = false;
    }
}
