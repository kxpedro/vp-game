using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public sealed class GameSaveData
{
    /// <summary>Versao do formato gravado. v2: moedas de sangue viraram recurso so da run e surgiu o Sangue Amaldicoado (CursedBlood).</summary>
    public const int CurrentSaveVersion = 2;

    /// <summary>Padrao 0: um JSON sem o campo e lido como versao anterior a 2. Quem grava deve usar CurrentSaveVersion.</summary>
    public int Version { get; set; }
    public DateTime LastPlayedUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, int> Attributes { get; set; } = new();
    /// <summary>Sangue Amaldicoado: recurso permanente gasto na arvore de habilidades do hub.</summary>
    public int CursedBlood { get; set; }
    public Dictionary<string, int> SkillRanks { get; set; } = new();
    public string EquippedWeaponId { get; set; } = "longsword";
    /// <summary>Diario de descobertas: categoria (CodexCategory) -> ids. Ausente em saves antigos (carrega vazio).</summary>
    public Dictionary<string, List<string>> Discoveries { get; set; } = new();
    /// <summary>Progresso do dialogo. Ausente em saves anteriores (carrega com os valores padrao).</summary>
    public DialogueSaveData Dialogue { get; set; } = new();
}

public sealed class DialogueSaveData
{
    public int RunsStarted { get; set; }
    public int RunsCompleted { get; set; }
    public int Deaths { get; set; }
    /// <summary>Ids de boss derrotados (BossCatalog).</summary>
    public List<string> BossesDefeated { get; set; } = new();
    /// <summary>Conversa -> valor de Plays[pool] na ultima vez em que foi vista. Presenca = ja vista.</summary>
    public Dictionary<string, int> Seen { get; set; } = new();
    /// <summary>Pool ("npc", "bark") -> execucoes ate agora.</summary>
    public Dictionary<string, int> Plays { get; set; } = new();
    /// <summary>Flags gravadas por sets_flags.</summary>
    public List<string> Flags { get; set; } = new();
}

/// <summary>Exists = save lido; Corrupted = arquivo existe mas nao pode ser lido (o menu pede confirmacao antes de sobrescrever).</summary>
public sealed record SlotInfo(bool Exists, int CursedBlood, DateTime LastPlayedLocal, int SkillRanks, string EquippedWeaponId, int DiscoveryCount = 0, bool Corrupted = false);

public partial class SaveGameManager : Node
{
    public static SaveGameManager Instance { get; private set; }
    public int CurrentSlot { get; private set; }
    private const double AutosaveDelaySeconds = 1.5;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private bool _isLoading;
    private bool _autosavePending;
    private double _autosaveCountdown;
    // Slots ilegiveis ja registrados (caminho@data de modificacao): o PushError sai uma vez por arquivo, nao a cada GetSlotInfo.
    private static readonly HashSet<string> ReportedUnreadableSlots = new();

    public override void _Ready() { Instance = this; ProcessMode = ProcessModeEnum.Always; }

    public override void _Process(double delta)
    {
        if (!_autosavePending) return;
        _autosaveCountdown -= delta;
        if (_autosaveCountdown <= 0.0) SaveCurrentSlot();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest || what == NotificationPredelete) FlushPendingSave();
    }

    public override void _ExitTree() => FlushPendingSave();

    public void FlushPendingSave()
    {
        if (_autosavePending) SaveCurrentSlot();
    }

    public bool SlotExists(int slot) => FileAccess.FileExists(GetSlotPath(slot));

    public bool DeleteSlot(int slot)
    {
        slot = Math.Clamp(slot, 1, 3);
        var path = GetSlotPath(slot);
        if (!FileAccess.FileExists(path)) return false;
        var error = DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        if (error != Error.Ok) { GD.PushError($"Nao foi possivel excluir o slot {slot}: {error}"); return false; }
        if (CurrentSlot == slot) CurrentSlot = 0;
        return true;
    }

    /// <summary>Resumo estruturado de um slot para a tela de slots (Exists = false se vazio ou ilegível; Corrupted = ilegível).</summary>
    public SlotInfo GetSlotInfo(int slot)
    {
        var data = ReadSlot(slot, out var corrupted);
        if (data == null) return new SlotInfo(false, 0, default, 0, null, 0, corrupted);
        var skillRanks = 0;
        if (data.SkillRanks != null) foreach (var rank in data.SkillRanks.Values) skillRanks += rank;
        var discoveries = 0;
        foreach (var ids in (data.Discoveries ?? new Dictionary<string, List<string>>()).Values) discoveries += ids?.Count ?? 0;
        return new SlotInfo(true, data.CursedBlood, data.LastPlayedUtc.ToLocalTime(), skillRanks, data.EquippedWeaponId, discoveries);
    }

    public string GetSlotSummary(int slot)
    {
        var data = ReadSlot(slot);
        return data == null
            ? Localization.Text("menu.new_game")
            : Localization.TextFormat("menu.slot_summary", data.CursedBlood,data.LastPlayedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
    }

    public void StartSlot(int slot)
    {
        CurrentSlot = Math.Clamp(slot, 1, 3);
        _isLoading = true;
        var data = ReadSlot(CurrentSlot, out var corrupted);
        // Save ilegivel (o menu ja pediu confirmacao): guarda uma copia antes de sobrescrever com um jogo novo.
        if (corrupted) BackupUnreadableSlot(CurrentSlot);
        if (data == null) CharacterSheet.ResetPermanentProgress();
        else CharacterSheet.LoadSaveData(data);
        _isLoading = false;
        SaveCurrentSlot();
    }

    public void SaveCurrentSlot()
    {
        _autosavePending = false;
        if (_isLoading || CurrentSlot <= 0) return;
        var data = CharacterSheet.CreateSaveData();
        data.Version = GameSaveData.CurrentSaveVersion;
        data.LastPlayedUtc = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(data, SerializerOptions);

        // Grava em arquivo temporario e renomeia: um crash no meio da escrita nao corrompe o slot.
        var path = GetSlotPath(CurrentSlot);
        var tempPath = path + ".tmp";
        using (var file = FileAccess.Open(tempPath, FileAccess.ModeFlags.Write))
        {
            if (file == null)
            {
                GD.PushError($"Nao foi possivel gravar o slot {CurrentSlot}: {FileAccess.GetOpenError()}");
                return;
            }
            file.StoreString(json);
        }

        var error = DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(tempPath), ProjectSettings.GlobalizePath(path));
        if (error != Error.Ok) GD.PushError($"Nao foi possivel concluir a gravacao do slot {CurrentSlot}: {error}");
    }

    /// <summary>Agenda uma gravacao; varias chamadas seguidas viram uma unica escrita em disco.</summary>
    public static void RequestAutosave()
    {
        if (Instance == null) return;
        Instance._autosavePending = true;
        Instance._autosaveCountdown = AutosaveDelaySeconds;
    }

    private static GameSaveData ReadSlot(int slot) => ReadSlot(slot, out _);

    private static GameSaveData ReadSlot(int slot, out bool corrupted)
    {
        corrupted = false;
        var path = GetSlotPath(slot);
        if (!FileAccess.FileExists(path)) return null;
        string text;
        // Escopo proprio: o arquivo precisa estar fechado antes de uma eventual exclusao (save antigo).
        using (var file = FileAccess.Open(path, FileAccess.ModeFlags.Read))
        {
            if (file == null)
            {
                corrupted = true;
                ReportUnreadableSlot(path, $"Nao foi possivel ler o slot {slot}: {FileAccess.GetOpenError()}");
                return null;
            }
            text = file.GetAsText();
        }
        try
        {
            // Save v1 real (campo Version presente, >= 1 e < 2): formato descontinuado, preservado como .v1.json.
            // Version ausente/invalido NAO e v1: segue o fluxo normal (e, se falhar, o de slot corrompido).
            using (var document = JsonDocument.Parse(text))
            {
                if (document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.TryGetProperty("Version", out var versionElement)
                    && versionElement.ValueKind == JsonValueKind.Number
                    && versionElement.TryGetInt32(out var version)
                    && version >= 1 && version < GameSaveData.CurrentSaveVersion)
                {
                    ArchiveLegacySlot(slot, path, version);
                    return null;
                }
            }
            var data = JsonSerializer.Deserialize<GameSaveData>(text);
            if (data != null)
                return data;
            corrupted = true;
            ReportUnreadableSlot(path, $"Save corrompido no slot {slot}: conteudo vazio");
            return null;
        }
        catch (Exception error)
        {
            corrupted = true;
            ReportUnreadableSlot(path, $"Save corrompido no slot {slot}: {error.Message}");
            return null;
        }
    }

    private static void ArchiveLegacySlot(int slot, string path, int version)
    {
        var archive = ProjectSettings.GlobalizePath(SlotBase(slot) + ".v1.json");
        var error = DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(path), archive);
        if (error == Error.Ok)
            GD.PushWarning($"Save do slot {slot} (versao {version}) e anterior a {GameSaveData.CurrentSaveVersion}: preservado como save_slot_{Math.Clamp(slot, 1, 3)}.v1.json e o slot comeca vazio.");
        else
            GD.PushWarning($"Save do slot {slot} (versao {version}) e anterior a {GameSaveData.CurrentSaveVersion} mas nao pode ser arquivado: {error}. O slot sera tratado como vazio e sobrescrito ao iniciar.");
    }

    private static void ReportUnreadableSlot(string path, string message)
    {
        if (ReportedUnreadableSlots.Add($"{path}@{FileAccess.GetModifiedTime(path)}")) GD.PushError(message);
    }

    private static void BackupUnreadableSlot(int slot)
    {
        var path = ProjectSettings.GlobalizePath(GetSlotPath(slot));
        var backup = ProjectSettings.GlobalizePath(SlotBase(slot) + ".unreadable.json");
        var error = DirAccess.CopyAbsolute(path, backup);
        if (error != Error.Ok) GD.PushWarning($"Nao foi possivel copiar o save ilegivel do slot {slot}: {error}");
    }

    private static string GetSlotPath(int slot) => SlotBase(slot) + ".json";

    // Prefixo de teste (so debug, autoteste): isola os arquivos dos saves reais. null = comportamento normal.
    private static string _testSlotPrefix;

    private static string SlotBase(int slot) => $"{_testSlotPrefix ?? "user://save_slot_"}{Math.Clamp(slot, 1, 3)}";

    /// <summary>So autoteste (debug): redireciona os slots para user://{prefix}N.*. Nao toca nos saves reais.</summary>
    public void BeginTestSlots(string prefix)
    {
        if (!OS.IsDebugBuild() || string.IsNullOrWhiteSpace(prefix) || prefix == "save_slot_") return;
        _autosavePending = false;
        CurrentSlot = 0;
        _testSlotPrefix = "user://" + prefix;
    }

    public void EndTestSlots()
    {
        _autosavePending = false;
        CurrentSlot = 0;
        _testSlotPrefix = null;
    }
}
