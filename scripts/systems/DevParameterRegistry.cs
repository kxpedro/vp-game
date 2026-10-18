using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Quando um parametro de dev passa a valer depois de alterado.
public enum DevApplyMode
{
    Live,       // lido a cada uso
    NewObjects, // so inimigos/objetos criados depois
    NextRoom,   // proxima sala/sorteio
    Reapply     // precisa de ApplyCompendiumTuning/ApplyRuntimeTuning
}

// Getter/Setter opcionais: armazenamento externo (ex.: GameTuning). Quando presentes, o valor NAO vai para dev_tuning.json.
public sealed record DevParameter(
    string Key,
    string Label,
    string Group,
    double Default,
    double Min,
    double Max,
    double Step,
    bool IsInt,
    DevApplyMode Apply = DevApplyMode.Live,
    string Note = "",
    Func<double> Getter = null,
    Action<double> Setter = null);

// Registro central dos parametros de gameplay ajustaveis (base do painel F1, so debug).
// As chaves normais ficam em CompendiumTuning (persistidas em user://dev_tuning.json em debug).
public static partial class DevParameterRegistry
{
    private const double Epsilon = 1e-9;

    private static readonly List<DevParameter> Parameters = new();
    private static readonly Dictionary<string, DevParameter> ByKey = new();
    private static readonly List<string> GroupList = new();
    private static bool _registered;

    public static IReadOnlyList<DevParameter> All
    {
        get
        {
            EnsureRegistered();
            return Parameters;
        }
    }

    public static IReadOnlyList<string> Groups
    {
        get
        {
            EnsureRegistered();
            return GroupList;
        }
    }

    public static event Action<string> Changed;

    static partial void RegisterPlayer();
    static partial void RegisterEnemies();
    static partial void RegisterEconomy();

    public static void EnsureRegistered()
    {
        if (_registered)
            return;

        _registered = true;
        CompendiumTuning.Changed += OnCompendiumChanged;
        RegisterLegacy();
        RegisterPlayer();
        RegisterEnemies();
        RegisterEconomy();
        CompendiumTuning.EnsureLoaded();
    }

    public static void Register(DevParameter p)
    {
        if (p == null)
            return;

        if (ByKey.ContainsKey(p.Key))
        {
            if (OS.IsDebugBuild())
                GD.PushWarning($"DevParameterRegistry: chave duplicada ignorada: {p.Key}");
            return;
        }

        ByKey[p.Key] = p;
        Parameters.Add(p);
        if (!GroupList.Contains(p.Group))
            GroupList.Add(p.Group);
    }

    // keyPattern contem "{m}"; expande para m = 0..N-1 (N = defaultsPerMap.Length).
    public static void RegisterPerMap(string keyPattern, string label, string group, double[] defaultsPerMap, double min, double max, double step, bool isInt, DevApplyMode apply = DevApplyMode.Live, string note = "")
    {
        for (var m = 0; m < defaultsPerMap.Length; m++)
        {
            Register(new DevParameter(
                keyPattern.Replace("{m}", m.ToString()),
                $"{label} (Mapa {m + 1})",
                group,
                defaultsPerMap[m],
                min,
                max,
                step,
                isInt,
                apply,
                note));
        }
    }

    public static DevParameter Find(string key)
    {
        EnsureRegistered();
        return key != null && ByKey.TryGetValue(key, out var p) ? p : null;
    }

    public static IEnumerable<DevParameter> InGroup(string group)
    {
        EnsureRegistered();
        return Parameters.Where(p => p.Group == group);
    }

    public static double Get(string key)
    {
        var p = Find(key);
        if (p == null)
            return CompendiumTuning.GetNumber(key, 0.0);

        return p.Getter != null ? p.Getter() : CompendiumTuning.GetNumber(key, p.Default);
    }

    public static void Set(string key, double value)
    {
        var p = Find(key);
        if (p == null)
        {
            GD.PushWarning($"DevParameterRegistry: chave nao registrada: {key}");
            return;
        }

        value = Math.Clamp(value, p.Min, p.Max);
        if (p.IsInt)
            value = Math.Round(value);

        if (p.Setter != null)
        {
            p.Setter(value);
            Changed?.Invoke(key);
            return;
        }

        // Valor igual ao padrao nao e override: mantem o arquivo limpo e IsModified falso.
        if (Math.Abs(value - p.Default) < Epsilon)
            CompendiumTuning.Remove(key);
        else
            CompendiumTuning.SetNumber(key, value); // dispara CompendiumTuning.Changed -> Changed e agenda a gravacao
    }

    public static void Reset(string key)
    {
        var p = Find(key);
        if (p == null)
            return;

        if (p.Setter != null)
        {
            p.Setter(p.Default);
            Changed?.Invoke(key);
        }
        else
        {
            CompendiumTuning.Remove(key);
        }
    }

    public static void ResetGroup(string group)
    {
        foreach (var p in InGroup(group).ToList())
            Reset(p.Key);
    }

    public static void ResetAll()
    {
        EnsureRegistered();
        foreach (var p in Parameters.Where(x => x.Setter != null))
            p.Setter(p.Default);

        CompendiumTuning.Reset(); // limpa tudo, apaga o arquivo e dispara Changed(null)
    }

    public static bool IsModified(string key)
    {
        var p = Find(key);
        if (p == null)
            return CompendiumTuning.HasOverride(key);

        return p.Getter != null
            ? Math.Abs(p.Getter() - p.Default) > Epsilon
            : CompendiumTuning.HasOverride(key);
    }

    private static void OnCompendiumChanged(string key) => Changed?.Invoke(key);

    // Autoteste de persistencia: roda sozinho com o argumento de usuario --dev-tuning-selftest (so debug).
#pragma warning disable CA2255
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void HookSelfTest()
#pragma warning restore CA2255
    {
        if (!OS.IsDebugBuild() || !OS.GetCmdlineUserArgs().Contains("--dev-tuning-selftest"))
            return;

        Callable.From(() => RunSelfTest()).CallDeferred();
    }

    // Autoteste de persistencia (debug); imprime OK/FALHA.
    public static bool RunSelfTest()
    {
        var ok = true;
        // O teste grava e apaga user://dev_tuning.json: guarda os valores reais do usuario e os devolve no fim.
        var backup = FileAccess.FileExists("user://dev_tuning.json") ? FileAccess.GetFileAsString("user://dev_tuning.json") : null;
        try
        {
            EnsureRegistered();
            ok &= Check(Parameters.Select(p => p.Key).Distinct().Count() == Parameters.Count, "chaves unicas");
            GD.Print("DEV-TUNING-SELFTEST: " + Parameters.Count + " chaves; " + string.Join("; ", Parameters.GroupBy(p => p.Group).Select(g => g.Key + "=" + g.Count())));

            const string key = "economy.coins.enemy.base_max";
            Set(key, 9);
            ok &= Check(Math.Abs(Get(key) - 9) < Epsilon && IsModified(key), "Set/Get/IsModified");
            Set(key, 9999);
            ok &= Check(Math.Abs(Get(key) - Find(key).Max) < Epsilon, "clamp no Max");
            Set(key, 9);

            CompendiumTuning.Save();
            ok &= Check(FileAccess.FileExists("user://dev_tuning.json"), "arquivo gravado");

            // Simula nova sessao: limpa a memoria sem apagar o arquivo e recarrega.
            var raw = FileAccess.GetFileAsString("user://dev_tuning.json");
            ok &= Check(raw.Contains("\"version\"") && raw.Contains(key), "conteudo do arquivo");
            CompendiumTuning.ClearMemoryForSelfTest();
            ok &= Check(CompendiumTuning.HasOverride(key) && Math.Abs(Get(key) - 9) < Epsilon, "Load apos reinicio");

            ResetAll();
            ok &= Check(!IsModified(key) && !FileAccess.FileExists("user://dev_tuning.json"), "ResetAll apaga arquivo");
        }
        catch (Exception exception)
        {
            GD.PushError($"DevParameterRegistry self-test: {exception.Message}");
            ok = false;
        }

        if (backup != null)
        {
            using var restored = FileAccess.Open("user://dev_tuning.json", FileAccess.ModeFlags.Write);
            restored?.StoreString(backup);
        }

        // A memoria deve refletir o arquivo do usuario restaurado.
        CompendiumTuning.ClearMemoryForSelfTest();
        CompendiumTuning.EnsureLoaded();

        GD.Print(ok ? "DEV-TUNING-SELFTEST: OK" : "DEV-TUNING-SELFTEST: FALHA");
        return ok;
    }

    private static bool Check(bool condition, string what)
    {
        if (!condition)
            GD.Print($"DEV-TUNING-SELFTEST: falhou: {what}");
        return condition;
    }
}
