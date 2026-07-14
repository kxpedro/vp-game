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
    public string[] ActiveSkills { get; set; } = Array.Empty<string>();
    public List<ArtifactRecord> Artifacts { get; set; } = new();
    public string[] EquippedArtifacts { get; set; } = Array.Empty<string>();
    public string EquippedWeaponId { get; set; } = "longsword";
}

public partial class SaveGameManager : Node
{
    public static SaveGameManager Instance { get; private set; }
    public int CurrentSlot { get; private set; }
    private bool _isLoading;

    public override void _Ready() { Instance = this; ProcessMode = ProcessModeEnum.Always; }

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
        if (_isLoading || CurrentSlot <= 0) return;
        var data = CharacterSheet.CreateSaveData();
        data.LastPlayedUtc = DateTime.UtcNow;
        using var file = FileAccess.Open(GetSlotPath(CurrentSlot), FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void RequestAutosave() => Instance?.SaveCurrentSlot();

    private static GameSaveData ReadSlot(int slot)
    {
        var path = GetSlotPath(slot);
        if (!FileAccess.FileExists(path)) return null;
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        try { return JsonSerializer.Deserialize<GameSaveData>(file.GetAsText()); }
        catch (Exception error) { GD.PushError($"Save corrompido no slot {slot}: {error.Message}"); return null; }
    }

    private static string GetSlotPath(int slot) => $"user://save_slot_{Math.Clamp(slot, 1, 3)}.json";
}
