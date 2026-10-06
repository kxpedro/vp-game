using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public sealed class GameSaveData
{
    public int Version { get; set; } = 1;
    public DateTime LastPlayedUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, int> Attributes { get; set; } = new();
    public int BloodCoins { get; set; }
    public Dictionary<string, int> SkillRanks { get; set; } = new();
    public List<ArtifactRecord> Artifacts { get; set; } = new();
    public string[] EquippedArtifacts { get; set; } = Array.Empty<string>();
    public string EquippedWeaponId { get; set; } = "longsword";
    /// <summary>Diario de descobertas: categoria (CodexCategory) -> ids. Ausente em saves antigos (carrega vazio).</summary>
    public Dictionary<string, List<string>> Discoveries { get; set; } = new();
}

/// <summary>Exists = save lido; Corrupted = arquivo existe mas nao pode ser lido (o menu pede confirmacao antes de sobrescrever).</summary>
public sealed record SlotInfo(bool Exists, int BloodCoins, DateTime LastPlayedLocal, int SkillRanks, int ArtifactCount, string EquippedWeaponId, int DiscoveryCount = 0, bool Corrupted = false);

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
        if (data == null) return new SlotInfo(false, 0, default, 0, 0, null, 0, corrupted);
        var skillRanks = 0;
        if (data.SkillRanks != null) foreach (var rank in data.SkillRanks.Values) skillRanks += rank;
        var discoveries = 0;
        foreach (var ids in (data.Discoveries ?? new Dictionary<string, List<string>>()).Values) discoveries += ids?.Count ?? 0;
        return new SlotInfo(true, data.BloodCoins, data.LastPlayedUtc.ToLocalTime(), skillRanks, data.Artifacts?.Count ?? 0, data.EquippedWeaponId, discoveries);
    }

    public string GetSlotSummary(int slot)
    {
        var data = ReadSlot(slot);
        return data == null
            ? Localization.Text("menu.new_game")
            : Localization.TextFormat("menu.slot_summary", data.BloodCoins, data.LastPlayedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
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
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            corrupted = true;
            ReportUnreadableSlot(path, $"Nao foi possivel ler o slot {slot}: {FileAccess.GetOpenError()}");
            return null;
        }
        try
        {
            var data = JsonSerializer.Deserialize<GameSaveData>(file.GetAsText());
            if (data != null) return data;
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

    private static void ReportUnreadableSlot(string path, string message)
    {
        if (ReportedUnreadableSlots.Add($"{path}@{FileAccess.GetModifiedTime(path)}")) GD.PushError(message);
    }

    private static void BackupUnreadableSlot(int slot)
    {
        var path = ProjectSettings.GlobalizePath(GetSlotPath(slot));
        var backup = ProjectSettings.GlobalizePath($"user://save_slot_{Math.Clamp(slot, 1, 3)}.unreadable.json");
        var error = DirAccess.CopyAbsolute(path, backup);
        if (error != Error.Ok) GD.PushWarning($"Nao foi possivel copiar o save ilegivel do slot {slot}: {error}");
    }

    private static string GetSlotPath(int slot) => $"user://save_slot_{Math.Clamp(slot, 1, 3)}.json";
}
