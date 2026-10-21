using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// Console de desenvolvedor (só OS.IsDebugBuild()): tipos do registro de comandos. Ver DevConsole.cs e docs/design/console-dev.md.

public enum DevConsoleLevel
{
    Input,   // a linha digitada (eco)
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Um comando (ou subcomando) do console. Comandos com <see cref="Subcommands"/> descem pela árvore quando o próximo
/// argumento casa com o nome/alias de um filho ("give boon ...", "list artifacts ..."); sem casar, roda o próprio
/// <see cref="Run"/> (se houver) ou avisa quais subcomandos existem.
/// </summary>
public sealed class DevCommand
{
    public DevCommand(string name, string summary, Action<DevCommandContext> run = null)
    {
        Name = name;
        Summary = summary;
        Run = run;
    }

    public string Name { get; }
    public string Summary { get; }
    public Action<DevCommandContext> Run { get; }
    /// <summary>Sinônimos (inglês/português, abreviações). Comparados sem acento e sem caixa.</summary>
    public string[] Aliases { get; init; } = Array.Empty<string>();
    /// <summary>Uso mostrado no help, sem o nome do comando (ex.: "&lt;quantidade&gt; &lt;recurso&gt;").</summary>
    public string Usage { get; init; } = "";
    /// <summary>Exemplos completos mostrados em "help &lt;comando&gt;".</summary>
    public string[] Examples { get; init; } = Array.Empty<string>();
    /// <summary>Candidatos para o Tab no argumento atual. Recebe o contexto (Args = argumentos já digitados antes do atual).</summary>
    public Func<DevCommandContext, IEnumerable<string>> Complete { get; init; }
    /// <summary>Exige cena com RunManager (arena)? O console avisa em vez de chamar Run.</summary>
    public bool NeedsArena { get; init; }

    public List<DevCommand> Subcommands { get; } = new();
    public DevCommand Parent { get; private set; }

    public string FullName => Parent == null ? Name : Parent.FullName + " " + Name;

    public DevCommand Add(DevCommand child)
    {
        child.Parent = this;
        Subcommands.Add(child);
        return this;
    }

    public bool Matches(string token)
    {
        var normalized = DevText.Normalize(token);
        if (normalized.Length == 0)
            return false;
        if (DevText.Normalize(Name) == normalized)
            return true;
        foreach (var alias in Aliases)
            if (DevText.Normalize(alias) == normalized)
                return true;
        return false;
    }

    public DevCommand FindSubcommand(string token) => Subcommands.FirstOrDefault(c => c.Matches(token));
}

/// <summary>
/// O que um comando recebe: argumentos (já sem o nome do comando/subcomando), saída e atalhos para os nós da cena.
/// Os atalhos devolvem null quando a cena não tem o nó (hub, menu); o comando decide o que fazer.
/// </summary>
public sealed class DevCommandContext
{
    private readonly Action<DevConsoleLevel, string> _output;

    public DevCommandContext(SceneTree tree, HUD hud, IReadOnlyList<string> args, Action<DevConsoleLevel, string> output)
    {
        Tree = tree;
        Hud = hud;
        Args = args ?? Array.Empty<string>();
        _output = output;
    }

    public SceneTree Tree { get; }
    public HUD Hud { get; }
    public IReadOnlyList<string> Args { get; }
    public DevCommand Command { get; set; }
    /// <summary>O comando pede para fechar o console depois de rodar (ex.: abriu uma oferta por cima).</summary>
    public bool CloseConsole { get; set; }

    public int Count => Args.Count;
    public string Arg(int index) => index >= 0 && index < Args.Count ? Args[index] : "";
    public string Rest(int from) => from < Args.Count ? string.Join(" ", Args.Skip(from)) : "";

    public void Print(string message) => _output?.Invoke(DevConsoleLevel.Info, message);
    public void Ok(string message) => _output?.Invoke(DevConsoleLevel.Success, message);
    public void Warn(string message) => _output?.Invoke(DevConsoleLevel.Warning, message);
    public void Error(string message) => _output?.Invoke(DevConsoleLevel.Error, message);

    /// <summary>Erro padrão de uso: mostra o uso do comando atual.</summary>
    public void UsageError(string message = null)
    {
        if (!string.IsNullOrEmpty(message))
            Error(message);
        if (Command != null)
            Print("Uso: " + Command.FullName + (string.IsNullOrEmpty(Command.Usage) ? "" : " " + Command.Usage));
    }

    public Node Player => Tree?.GetFirstNodeInGroup("player");
    public PlayerController PlayerController => Player as PlayerController;
    public Node3D PlayerBody => Player as Node3D;
    public Health PlayerHealth => Player?.GetNodeOrNull<Health>("Health");
    public PlayerCombat PlayerCombat => Player?.GetNodeOrNull<PlayerCombat>("PlayerCombat");
    public RunManager RunManager => Tree?.GetFirstNodeInGroup("run_manager") as RunManager;
    public RoomGenerator RoomGenerator => Tree?.GetFirstNodeInGroup("room_generator") as RoomGenerator;

    /// <summary>Jogador vivo nesta cena; senão escreve o erro e devolve null.</summary>
    public Health RequireLivingPlayer()
    {
        var health = PlayerHealth;
        if (health == null || health.IsDead)
        {
            Error("Sem jogador vivo nesta cena.");
            return null;
        }
        return health;
    }

    /// <summary>Recompensa de run fora de uma run ativa: funciona, mas some ao iniciar a próxima run. Só avisa.</summary>
    public void WarnIfNoRun()
    {
        if (!RunState.IsRunActive)
            Warn("Nenhuma run ativa: o efeito vale até a próxima run começar (StartRun zera a run).");
    }
}

/// <summary>Normalização e busca por nome usadas pelos comandos (sem acento, sem caixa, "_"/"-" = espaço).</summary>
public static class DevText
{
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        var decomposed = text.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || c == '\'' || c == '\u2019')
                continue;
            builder.Append(c == '_' || c == '-' ? ' ' : c);
        }
        return string.Join(" ", builder.ToString().Normalize(System.Text.NormalizationForm.FormC).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Número com ponto ou vírgula decimal ("1.5", "1,5", "-20").</summary>
    public static bool TryNumber(string text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var normalized = text.Trim().Replace(',', '.');
        if (normalized.EndsWith("x", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith("×"))
            normalized = normalized[..^1];
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public static bool TryInt(string text, out int value)
    {
        value = 0;
        if (!TryNumber(text, out var number) || Math.Abs(number) > int.MaxValue)
            return false;
        value = (int)Math.Round(number);
        return true;
    }

    public static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Busca por id ou nome exibido. Ordem: id exato, nome exato, id começando pela busca, todas as palavras da busca
    /// contidas no id ou no nome. Devolve as melhores correspondências do primeiro critério que achar algo.
    /// </summary>
    public static List<T> Find<T>(IEnumerable<T> items, string query, Func<T, string> id, Func<T, string> name)
    {
        var list = items.ToList();
        var q = Normalize(query);
        if (q.Length == 0)
            return new List<T>();

        var exactId = list.Where(i => Normalize(id(i)) == q).ToList();
        if (exactId.Count > 0)
            return exactId;
        var exactName = list.Where(i => Normalize(name(i)) == q).ToList();
        if (exactName.Count > 0)
            return exactName;
        var prefix = list.Where(i => Normalize(id(i)).StartsWith(q, StringComparison.Ordinal)).ToList();
        if (prefix.Count > 0)
            return prefix;
        var words = q.Split(' ');
        return list.Where(i =>
        {
            var haystack = Normalize(id(i)) + " | " + Normalize(name(i));
            return words.All(w => haystack.Contains(w, StringComparison.Ordinal));
        }).ToList();
    }

    /// <summary>Filtra candidatos de autocompletar pelo prefixo digitado (sem acento/caixa).</summary>
    public static IEnumerable<string> FilterPrefix(IEnumerable<string> candidates, string prefix)
    {
        var p = Normalize(prefix);
        return candidates.Where(c => !string.IsNullOrEmpty(c) && Normalize(c).StartsWith(p, StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Texto seguro para o RichTextLabel com BBCode ("[" vira "[lb]").</summary>
    public static string EscapeBbcode(string text) => (text ?? "").Replace("[", "[lb]");
}
