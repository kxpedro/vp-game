using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

// Duas camadas sobre os padroes do codigo:
//  - "do jogo": res://data/tuning/tuning_overrides.json, versionado e lido em qualquer build (inclusive release);
//  - "locais": user://dev_tuning.json, so deste PC e so em debug (painel F1, console, compendio).
// Leitura: local -> do jogo -> padrao do codigo. "Promover" (DevParameterRegistry.PromoteAll) passa as locais para o arquivo do jogo.
public static class CompendiumTuning
{
    private const string SavePath = "user://dev_tuning.json";
    public const string ShippedPath = "res://data/tuning/tuning_overrides.json";
    private const int FileVersion = 1;
    private const double SaveDelaySeconds = 0.5;

    private static readonly Dictionary<string, double> NumberOverrides = new();
    private static readonly Dictionary<string, double> ShippedValues = new();
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private static bool _loaded;
    private static bool _savePending;
    private static bool _exitHooked;
    private static bool _shippedLoaded;

    /// <summary>Chave alterada (null = reset geral).</summary>
    public static event Action<string> Changed;

    // So a camada local (user://dev_tuning.json).
    public static IReadOnlyDictionary<string, double> Overrides
    {
        get
        {
            EnsureLoaded();
            return NumberOverrides;
        }
    }

    // Camada do jogo (res://data/tuning/tuning_overrides.json).
    public static IReadOnlyDictionary<string, double> ShippedOverrides
    {
        get
        {
            EnsureShippedLoaded();
            return ShippedValues;
        }
    }

    // O arquivo do jogo so pode ser regravado rodando a partir do projeto (executavel do editor), nunca num export.
    public static bool CanWriteShipped => OS.IsDebugBuild() && OS.HasFeature("editor");

    public static void SetNumber(string key, double value)
    {
        EnsureLoaded();
        NumberOverrides[key] = value;
        Changed?.Invoke(key);
        ScheduleSave();
    }

    public static double GetNumber(string key, double originalValue)
    {
        EnsureLoaded();
        if (NumberOverrides.TryGetValue(key, out var value))
            return value;
        return TryGetShipped(key, out var shipped) ? shipped : originalValue;
    }

    public static bool TryGetShipped(string key, out double value)
    {
        EnsureShippedLoaded();
        return ShippedValues.TryGetValue(key, out value);
    }

    public static float GetFloat(string key, float originalValue)
    {
        return (float)GetNumber(key, originalValue);
    }

    public static int GetInt(string key, int originalValue)
    {
        return System.Math.Max(0, (int)System.Math.Round(GetNumber(key, originalValue)));
    }

    // Local ou do jogo: o valor difere do padrao do codigo.
    public static bool HasOverride(string key)
    {
        EnsureLoaded();
        return NumberOverrides.ContainsKey(key) || ShippedOverrides.ContainsKey(key);
    }

    public static bool HasLocalOverride(string key)
    {
        EnsureLoaded();
        return NumberOverrides.ContainsKey(key);
    }

    public static bool Remove(string key)
    {
        EnsureLoaded();
        if (!NumberOverrides.Remove(key))
            return false;

        Changed?.Invoke(key);
        ScheduleSave();
        return true;
    }

    // Limpa so a camada local; os valores do jogo continuam.
    public static void Reset()
    {
        _loaded = true;
        NumberOverrides.Clear();
        _savePending = false;
        DeleteSaveFile();
        Changed?.Invoke(null);
    }

    // Persistência (só em build de debug; em release nada é lido nem gravado).
    public static void Load()
    {
        _loaded = true;
        if (!OS.IsDebugBuild())
            return;

        try
        {
            if (!FileAccess.FileExists(SavePath))
                return;

            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PushError($"CompendiumTuning: nao foi possivel abrir {SavePath} ({FileAccess.GetOpenError()}).");
                return;
            }

            using var document = JsonDocument.Parse(file.GetAsText());
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("values", out var values)
                || values.ValueKind != JsonValueKind.Object)
            {
                GD.PushError($"CompendiumTuning: formato invalido em {SavePath}.");
                return;
            }

            foreach (var property in values.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetDouble(out var number))
                    continue;

                // Chaves registradas respeitam faixa/inteiro; desconhecidas (familias dinamicas) passam como estao.
                if (DevParameterRegistry.Find(property.Name) is { } parameter)
                {
                    var clamped = System.Math.Clamp(number, parameter.Min, parameter.Max);
                    if (parameter.IsInt)
                        clamped = System.Math.Round(clamped);
                    if (System.Math.Abs(clamped - number) > 1e-9)
                        GD.PushWarning($"CompendiumTuning: {property.Name}={number} fora da faixa [{parameter.Min}, {parameter.Max}]; ajustado para {clamped}.");
                    number = clamped;
                }

                NumberOverrides[property.Name] = number;
            }
        }
        catch (Exception exception)
        {
            GD.PushError($"CompendiumTuning: falha ao ler {SavePath}: {exception.Message}");
        }
    }

    public static void Save()
    {
        _savePending = false;
        if (!OS.IsDebugBuild())
            return;

        try
        {
            if (NumberOverrides.Count == 0)
            {
                DeleteSaveFile();
                return;
            }

            var json = JsonSerializer.Serialize(new { version = FileVersion, values = NumberOverrides }, SerializerOptions);
            var tempPath = SavePath + ".tmp";
            using (var file = FileAccess.Open(tempPath, FileAccess.ModeFlags.Write))
            {
                if (file == null)
                {
                    GD.PushError($"CompendiumTuning: nao foi possivel gravar {tempPath} ({FileAccess.GetOpenError()}).");
                    return;
                }

                file.StoreString(json);
            }

            var error = DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(tempPath), ProjectSettings.GlobalizePath(SavePath));
            if (error != Error.Ok)
                GD.PushError($"CompendiumTuning: falha ao finalizar {SavePath} ({error}).");
        }
        catch (Exception exception)
        {
            GD.PushError($"CompendiumTuning: falha ao gravar {SavePath}: {exception.Message}");
        }
    }

    // Camada do jogo: lida em qualquer build. Arquivo ausente = nenhum valor do jogo.
    public static void LoadShipped()
    {
        _shippedLoaded = true;
        ShippedValues.Clear();
        try
        {
            if (!FileAccess.FileExists(ShippedPath))
                return;

            using var document = JsonDocument.Parse(FileAccess.GetFileAsString(ShippedPath));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("values", out var values)
                || values.ValueKind != JsonValueKind.Object)
            {
                GD.PushError($"CompendiumTuning: formato invalido em {ShippedPath}.");
                return;
            }

            foreach (var property in values.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var number))
                    ShippedValues[property.Name] = number;
            }
        }
        catch (Exception exception)
        {
            GD.PushError($"CompendiumTuning: falha ao ler {ShippedPath}: {exception.Message}");
        }
    }

    public static void EnsureShippedLoaded()
    {
        if (!_shippedLoaded)
            LoadShipped();
    }

    // Regrava o arquivo do jogo com estes valores (chaves em ordem alfabetica, para diffs limpos no git).
    public static bool WriteShipped(IReadOnlyDictionary<string, double> values, out string error)
    {
        error = null;
        if (!CanWriteShipped)
        {
            error = "só dá para promover rodando o jogo a partir do projeto (build de debug).";
            return false;
        }

        try
        {
            var sorted = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (var pair in values)
                sorted[pair.Key] = pair.Value;

            var json = JsonSerializer.Serialize(new { version = FileVersion, values = sorted }, SerializerOptions) + "\n";
            var absolutePath = ProjectSettings.GlobalizePath(ShippedPath);
            DirAccess.MakeDirRecursiveAbsolute(absolutePath.GetBaseDir());
            using (var file = FileAccess.Open(absolutePath, FileAccess.ModeFlags.Write))
            {
                if (file == null)
                {
                    error = $"não foi possível gravar {absolutePath} ({FileAccess.GetOpenError()}).";
                    return false;
                }

                file.StoreString(json);
            }

            ShippedValues.Clear();
            foreach (var pair in sorted)
                ShippedValues[pair.Key] = pair.Value;
            _shippedLoaded = true;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static void FlushPendingSave()
    {
        if (_savePending)
            Save();
    }

    // Autoteste: troca a camada do jogo em memoria, sem tocar no arquivo (LoadShipped devolve o real).
    internal static void SetShippedForSelfTest(IReadOnlyDictionary<string, double> values)
    {
        _shippedLoaded = true;
        ShippedValues.Clear();
        foreach (var pair in values)
            ShippedValues[pair.Key] = pair.Value;
    }

    // Simula nova sessao no autoteste: esquece a memoria sem tocar no arquivo.
    internal static void ClearMemoryForSelfTest()
    {
        NumberOverrides.Clear();
        _loaded = false;
    }

    // Idempotente: carrega o arquivo uma unica vez (nao sobrescreve alteracoes em memoria ainda nao gravadas).
    public static void EnsureLoaded()
    {
        if (!_loaded)
            Load();
    }

    private static void ScheduleSave()
    {
        if (!OS.IsDebugBuild() || _savePending)
            return;

        _savePending = true;
        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            Save();
            return;
        }

        if (!_exitHooked && tree.Root != null)
        {
            _exitHooked = true;
            tree.Root.TreeExiting += FlushPendingSave;
        }

        tree.CreateTimer(SaveDelaySeconds, true, false, true).Timeout += FlushPendingSave;
    }

    private static void DeleteSaveFile()
    {
        if (!OS.IsDebugBuild())
            return;

        try
        {
            if (FileAccess.FileExists(SavePath))
                DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SavePath));
        }
        catch (Exception exception)
        {
            GD.PushError($"CompendiumTuning: falha ao apagar {SavePath}: {exception.Message}");
        }
    }
}
