using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public static class CompendiumTuning
{
    private const string SavePath = "user://dev_tuning.json";
    private const int FileVersion = 1;
    private const double SaveDelaySeconds = 0.5;

    private static readonly Dictionary<string, double> NumberOverrides = new();
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private static bool _loaded;
    private static bool _savePending;
    private static bool _exitHooked;

    /// <summary>Chave alterada (null = reset geral).</summary>
    public static event Action<string> Changed;

    public static IReadOnlyDictionary<string, double> Overrides
    {
        get
        {
            EnsureLoaded();
            return NumberOverrides;
        }
    }

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
        return NumberOverrides.TryGetValue(key, out var value) ? value : originalValue;
    }

    public static float GetFloat(string key, float originalValue)
    {
        return (float)GetNumber(key, originalValue);
    }

    public static int GetInt(string key, int originalValue)
    {
        return System.Math.Max(0, (int)System.Math.Round(GetNumber(key, originalValue)));
    }

    public static bool HasOverride(string key)
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

    public static void FlushPendingSave()
    {
        if (_savePending)
            Save();
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
