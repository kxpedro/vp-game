using Godot;
using System;
using System.Text.Json;

public sealed class GlobalSettingsData
{
    public int Version { get; set; } = 2;
    public string Language { get; set; } = "pt-BR";
    public int ResolutionIndex { get; set; } = 1;
    public bool Fullscreen { get; set; }
    public float MusicVolume { get; set; } = 70.0f;
    public float EffectsVolume { get; set; } = 85.0f;
}

public partial class GlobalSettings : Node
{
    private const string SettingsPath = "user://global_settings.json";
    public static GlobalSettings Instance { get; private set; }
    public GlobalSettingsData Data { get; private set; } = new();

    public override void _Ready()
    {
        Instance = this; ProcessMode = ProcessModeEnum.Always; Load();
        Data.Language = Localization.NormalizeLanguage(Data.Language);
        Localization.Instance?.SetLanguage(Data.Language);
        ApplyDisplay();
    }

    public void SetResolution(int index) { Data.ResolutionIndex = Math.Clamp(index, 0, 2); Data.Fullscreen = false; ApplyDisplay(); Save(); }
    public void SetFullscreen(bool enabled) { Data.Fullscreen = enabled; ApplyDisplay(); Save(); }
    public void SetMusicVolume(float value) { Data.MusicVolume = Mathf.Clamp(value, 0, 100); ApplyAudio(); Save(); }
    public void SetEffectsVolume(float value) { Data.EffectsVolume = Mathf.Clamp(value, 0, 100); ApplyAudio(); Save(); }
    public void SetLanguage(string language) { Data.Language = Localization.NormalizeLanguage(language); Localization.Instance?.SetLanguage(Data.Language); Save(); }
    public void ApplyAudio() { GetTree().CallGroup("audio_manager", "SetMusicVolume", Data.MusicVolume); GetTree().CallGroup("audio_manager", "SetEffectsVolume", Data.EffectsVolume); }

    private void ApplyDisplay()
    {
        var sizes = new[] { new Vector2I(1280, 720), new Vector2I(1600, 900), new Vector2I(1920, 1080) };
        DisplayServer.WindowSetMode(Data.Fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        if (Data.Fullscreen) return;
        DisplayServer.WindowSetSize(sizes[Math.Clamp(Data.ResolutionIndex, 0, 2)]);
        DisplayServer.WindowSetPosition((DisplayServer.ScreenGetSize() - DisplayServer.WindowGetSize()) / 2);
    }

    private void Load()
    {
        if (!FileAccess.FileExists(SettingsPath)) return;
        try { Data = JsonSerializer.Deserialize<GlobalSettingsData>(FileAccess.GetFileAsString(SettingsPath)) ?? new(); }
        catch (Exception error) { GD.PushWarning($"Configuracoes globais invalidas: {error.Message}"); Data = new(); }
    }

    private void Save()
    {
        using var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true }));
    }
}
