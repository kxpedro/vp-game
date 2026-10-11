using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

// Textos por idioma: res://data/localization/<idioma>/*.json, formato plano { "chave": "texto" }.
// Cada idioma pode ter vários arquivos por domínio (ui.json, content.json, world.json...).
// Resolução: idioma atual -> pt-BR -> a própria chave (aviso em debug, uma vez por chave).
public partial class Localization : Node
{
    [Signal]
    public delegate void LanguageChangedEventHandler(string language);

    private const string LocalizationRoot = "res://data/localization";
    private const string FallbackLanguage = "pt-BR";
    private static readonly string[] Languages = { "pt-BR", "en", "es" };

    // idioma -> (chave -> texto)
    private readonly Dictionary<string, Dictionary<string, string>> _texts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _reportedMissing = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _reportedFormat = new(StringComparer.OrdinalIgnoreCase);

    public static Localization Instance { get; private set; }
    public static IReadOnlyList<string> SupportedLanguages => Languages;
    public string CurrentLanguage { get; private set; } = FallbackLanguage;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        LoadAllLanguages();
        ApplyCulture(CurrentLanguage);
    }

    public void SetLanguage(string language)
    {
        language = NormalizeLanguage(language);
        if (CurrentLanguage == language)
            return;

        CurrentLanguage = language;
        ApplyCulture(CurrentLanguage);
        EmitSignal(SignalName.LanguageChanged, CurrentLanguage);
    }

    public bool HasKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        return TryGetLocalized(CurrentLanguage, key, out _) || TryGetLocalized(FallbackLanguage, key, out _);
    }

    public string GetText(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key ?? "";

        if (TryGetLocalized(CurrentLanguage, key, out var localized))
            return localized;
        if (TryGetLocalized(FallbackLanguage, key, out var fallback))
            return fallback;

        if (OS.IsDebugBuild() && _reportedMissing.Add(key))
            GD.PushWarning($"Localization: missing key '{key}' (language {CurrentLanguage}).");
        return key;
    }

    public string Format(string key, params object[] arguments)
    {
        var text = GetText(key);
        var provider = FormatProviderFor(CurrentLanguage);
        if (TryFormat(text, arguments, provider, out var formatted))
            return formatted;

        // Tradução malformada: tenta o texto pt-BR e, se também falhar, devolve o texto cru.
        if (OS.IsDebugBuild() && _reportedFormat.Add(CurrentLanguage + ":" + key))
            GD.PushWarning($"Localization: invalid format for key '{key}' (language {CurrentLanguage}).");

        if (TryGetLocalized(FallbackLanguage, key, out var fallbackText)
            && fallbackText != text
            && TryFormat(fallbackText, arguments, FormatProviderFor(FallbackLanguage), out var fallbackFormatted))
            return fallbackFormatted;

        return text;
    }

    public static string Text(string key) => Instance?.GetText(key) ?? key;

    public static string TextFormat(string key, params object[] arguments)
    {
        if (Instance != null)
            return Instance.Format(key, arguments);
        return TryFormat(key, arguments, CultureInfo.InvariantCulture, out var formatted) ? formatted : key;
    }

    public static bool Has(string key) => Instance != null && Instance.HasKey(key);

    public static string NormalizeLanguage(string language) => language?.Trim().ToLowerInvariant() switch
    {
        "en" or "en-us" or "en_us" => "en",
        "es" or "es-es" or "es_es" => "es",
        _ => FallbackLanguage
    };

    // Números nas traduções ({0:0.#}) seguem o idioma do jogo, não a cultura do Windows. NumberFormatInfo próprio:
    // não depende de ICU/cultura instalada no build exportado.
    private static readonly NumberFormatInfo CommaDecimal = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    private static IFormatProvider FormatProviderFor(string language)
        => language == "en" ? CultureInfo.InvariantCulture : CommaDecimal;

    // A cultura da sessão acompanha o idioma do jogo: $"{x:0.00}" e ToString() fora das traduções também usam o separador
    // do idioma (e não o do Windows). Clone da cultura invariante com NumberFormat próprio: sem ICU nem cultura instalada.
    private static void ApplyCulture(string language)
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        if (language != "en")
            culture.NumberFormat = (NumberFormatInfo)CommaDecimal.Clone();
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
    }

    private static bool TryFormat(string text, object[] arguments, IFormatProvider provider, out string formatted)
    {
        try
        {
            formatted = string.Format(provider, text ?? "", arguments ?? Array.Empty<object>());
            return true;
        }
        catch (FormatException)
        {
            formatted = text;
            return false;
        }
    }

    private bool TryGetLocalized(string language, string key, out string text)
    {
        text = null;
        return _texts.TryGetValue(language, out var table)
            && table.TryGetValue(key, out text)
            && !string.IsNullOrWhiteSpace(text);
    }

    private void LoadAllLanguages()
    {
        _texts.Clear();
        _reportedMissing.Clear();
        _reportedFormat.Clear();
        foreach (var language in Languages)
            _texts[language] = LoadLanguage(language);

        if (_texts[FallbackLanguage].Count == 0)
            GD.PushError($"Localization: no texts loaded for fallback language {FallbackLanguage}.");
    }

    private static Dictionary<string, string> LoadLanguage(string language)
    {
        var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var origins = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var directory = $"{LocalizationRoot}/{language}";

        string[] files;
        try
        {
            files = DirAccess.GetFilesAt(directory);
        }
        catch (Exception error)
        {
            GD.PushError($"Localization: cannot list {directory}: {error.Message}");
            return table;
        }

        if (files == null || files.Length == 0)
        {
            GD.PushError($"Localization: no files found in {directory}.");
            return table;
        }

        Array.Sort(files, StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            var path = $"{directory}/{file}";
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(FileAccess.GetFileAsString(path));
                if (parsed == null)
                    continue;

                foreach (var entry in parsed)
                {
                    if (origins.TryGetValue(entry.Key, out var previous) && OS.IsDebugBuild())
                        GD.PushWarning($"Localization: duplicate key '{entry.Key}' in {language} ({previous} and {file}).");
                    table[entry.Key] = entry.Value;
                    origins[entry.Key] = file;
                }
            }
            catch (Exception error)
            {
                GD.PushError($"Localization: invalid resource {path}: {error.Message}");
            }
        }

        return table;
    }
}
