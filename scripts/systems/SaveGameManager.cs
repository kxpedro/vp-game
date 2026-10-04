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
}

public partial class SaveGameManager : Node
{
    public static SaveGameManager Instance { get; private set; }
    public int CurrentSlot { get; private set; }
    private const double AutosaveDelaySeconds = 1.5;
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private bool _isLoading;
    private bool _autosavePending;
    private double _autosaveCountdown;

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
        var data = ReadSlot(CurrentSlot);
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

    private static GameSaveData ReadSlot(int slot)
    {
        var path = GetSlotPath(slot);
        if (!FileAccess.FileExists(path)) return null;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) { GD.PushError($"Nao foi possivel ler o slot {slot}: {FileAccess.GetOpenError()}"); return null; }
        try { return JsonSerializer.Deserialize<GameSaveData>(file.GetAsText()); }
        catch (Exception error) { GD.PushError($"Save corrompido no slot {slot}: {error.Message}"); return null; }
    }

    private static string GetSlotPath(int slot) => $"user://save_slot_{Math.Clamp(slot, 1, 3)}.json";
}
