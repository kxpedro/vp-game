using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public partial class Localization : Node
{
    [Signal]
    public delegate void LanguageChangedEventHandler(string language);

    private const string TextResourcePath = "res://data/localization/game_texts.json";
    private const string FallbackLanguage = "pt-BR";
    private readonly Dictionary<string, Dictionary<string, string>> _texts = new(StringComparer.OrdinalIgnoreCase);

    public static Localization Instance { get; private set; }
    public string CurrentLanguage { get; private set; } = FallbackLanguage;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        LoadTextResource();
    }

    public void SetLanguage(string language)
    {
        language = NormalizeLanguage(language);
        if (CurrentLanguage == language)
            return;

        CurrentLanguage = language;
        EmitSignal(SignalName.LanguageChanged, CurrentLanguage);
    }

    public string GetText(string key)
    {
        if (!_texts.TryGetValue(key, out var translations))
            return key;

        if (translations.TryGetValue(CurrentLanguage, out var localized) && !string.IsNullOrWhiteSpace(localized))
            return localized;

        return translations.TryGetValue(FallbackLanguage, out var fallback) && !string.IsNullOrWhiteSpace(fallback)
            ? fallback
            : key;
    }

    public string Format(string key, params object[] arguments)
        => string.Format(GetText(key), arguments);

    public static string Text(string key) => Instance?.GetText(key) ?? key;
    public static string TextFormat(string key, params object[] arguments)
        => Instance?.Format(key, arguments) ?? string.Format(key, arguments);

    public static string NormalizeLanguage(string language) => language?.Trim().ToLowerInvariant() switch
    {
        "en" or "en-us" or "en_us" => "en",
        "es" or "es-es" or "es_es" => "es",
        _ => FallbackLanguage
    };

    private void LoadTextResource()
    {
        if (!FileAccess.FileExists(TextResourcePath))
        {
            GD.PushError($"Localization resource not found: {TextResourcePath}");
            return;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                FileAccess.GetFileAsString(TextResourcePath));
            if (parsed == null)
                return;

            _texts.Clear();
            foreach (var entry in parsed)
                _texts[entry.Key] = entry.Value;
        }
        catch (Exception error)
        {
            GD.PushError($"Invalid localization resource: {error.Message}");
        }
    }
}
