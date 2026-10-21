using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Console de desenvolvedor (só OS.IsDebugBuild()): registro de comandos, parser, execução, autocompletar e histórico.
/// A UI fica em HUD.DevConsole.cs; os comandos embutidos em DevCommands.*.cs. Para um comando novo, crie (ou estenda)
/// um DevCommands.&lt;Area&gt;.cs e chame <see cref="Register"/> no Register&lt;Area&gt; dele (ver docs/design/console-dev.md).
///
/// Sintaxe: "[/]comando [subcomando] arg1 arg2 ...". A barra inicial é opcional; aspas agrupam um argumento com espaços;
/// ";" separa vários comandos numa linha ("god on; add 500 coins"). Nomes e aliases são comparados sem acento e sem caixa.
/// Saída e histórico são estáticos: sobrevivem à troca de cena (o HUD é recriado a cada cena).
/// </summary>
public static class DevConsole
{
    private const int MaxLogLines = 400;
    private const int MaxHistory = 100;

    private static readonly List<DevCommand> Commands = new();
    private static readonly List<(DevConsoleLevel Level, string Text)> LogLines = new();
    private static readonly List<string> HistoryLines = new();
    private static bool _registered;

    /// <summary>Linha nova na saída (o HUD aberto se inscreve para desenhar).</summary>
    public static event Action<DevConsoleLevel, string> LineAdded;
    /// <summary>Saída limpa ("clear").</summary>
    public static event Action Cleared;

    public static IReadOnlyList<DevCommand> All
    {
        get
        {
            EnsureRegistered();
            return Commands;
        }
    }

    public static IReadOnlyList<(DevConsoleLevel Level, string Text)> Log => LogLines;
    public static IReadOnlyList<string> History => HistoryLines;

    public static void EnsureRegistered()
    {
        if (_registered)
            return;
        _registered = true;
        DevCommands.RegisterAll();
    }

    /// <summary>Registra um comando de topo. Nome/alias repetido é erro de programação: avisa e ignora o novo.</summary>
    public static void Register(DevCommand command)
    {
        if (command == null)
            return;
        foreach (var token in command.Aliases.Prepend(command.Name))
        {
            var clash = Commands.FirstOrDefault(c => c.Matches(token));
            if (clash != null)
            {
                GD.PushWarning($"DevConsole: \"{token}\" de \"{command.Name}\" já pertence a \"{clash.Name}\"; comando ignorado.");
                return;
            }
        }
        Commands.Add(command);
    }

    public static DevCommand Find(string token)
    {
        EnsureRegistered();
        return Commands.FirstOrDefault(c => c.Matches(token));
    }

    public static void Write(DevConsoleLevel level, string text)
    {
        foreach (var line in (text ?? "").Replace("\r", "").Split('\n'))
        {
            LogLines.Add((level, line));
            if (LogLines.Count > MaxLogLines)
                LogLines.RemoveAt(0);
            LineAdded?.Invoke(level, line);
        }
    }

    public static void Clear()
    {
        LogLines.Clear();
        Cleared?.Invoke();
    }

    // ---------- Execução ----------

    /// <summary>
    /// Executa uma linha (pode ter vários comandos separados por ";"). Devolve true se algum comando pediu para
    /// fechar o console (ex.: abriu uma oferta). Exceção dentro de um comando vira erro na saída, sem derrubar o jogo.
    /// </summary>
    public static bool Execute(string line, SceneTree tree, HUD hud)
    {
        EnsureRegistered();
        if (string.IsNullOrWhiteSpace(line))
            return false;

        line = line.Trim();
        AddHistory(line);
        Write(DevConsoleLevel.Input, "> " + line);

        var closeConsole = false;
        foreach (var statement in SplitStatements(line))
        {
            var tokens = Tokenize(statement);
            if (tokens.Count == 0)
                continue;
            closeConsole |= ExecuteTokens(tokens, tree, hud);
        }
        return closeConsole;
    }

    private static bool ExecuteTokens(List<string> tokens, SceneTree tree, HUD hud)
    {
        var command = Find(tokens[0]);
        if (command == null)
        {
            Write(DevConsoleLevel.Error, $"Comando desconhecido: \"{tokens[0]}\".{Suggest(tokens[0])} Digite help para a lista.");
            return false;
        }

        var argStart = 1;
        while (argStart < tokens.Count && command.FindSubcommand(tokens[argStart]) is { } child)
        {
            command = child;
            argStart++;
        }

        var context = new DevCommandContext(tree, hud, tokens.Skip(argStart).ToList(), Write) { Command = command };
        if (command.Run == null)
        {
            if (argStart < tokens.Count)
                context.Error($"\"{tokens[argStart]}\" não é um subcomando de {command.FullName}.");
            Write(DevConsoleLevel.Info, $"{command.FullName}: {command.Summary}");
            Write(DevConsoleLevel.Info, "Subcomandos: " + string.Join(", ", command.Subcommands.Select(c => c.Name)) + ".");
            return false;
        }
        if (command.NeedsArena && context.RunManager == null)
        {
            context.Error($"{command.FullName} só funciona na arena (cena com RunManager).");
            return false;
        }

        try
        {
            command.Run(context);
        }
        catch (Exception exception)
        {
            context.Error($"{command.FullName} falhou: {exception.Message}");
            GD.PushError($"DevConsole: {command.FullName}: {exception}");
        }
        return context.CloseConsole;
    }

    private static string Suggest(string token)
    {
        var normalized = DevText.Normalize(token);
        if (normalized.Length == 0)
            return "";
        var names = Commands.SelectMany(c => c.Aliases.Prepend(c.Name))
            .Where(n => DevText.Normalize(n).StartsWith(normalized[..1], StringComparison.Ordinal))
            .OrderBy(n => Distance(DevText.Normalize(n), normalized))
            .Take(1)
            .ToList();
        return names.Count > 0 && Distance(DevText.Normalize(names[0]), normalized) <= 2 ? $" Quis dizer \"{names[0]}\"?" : "";
    }

    private static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }

    // ---------- Parser ----------

    /// <summary>Separa por ";" fora de aspas.</summary>
    public static List<string> SplitStatements(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        foreach (var c in line)
        {
            if (c == '"')
                quoted = !quoted;
            if (c == ';' && !quoted)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }

    /// <summary>Quebra em tokens por espaço; aspas agrupam. A "/" inicial do primeiro token é descartada.</summary>
    public static List<string> Tokenize(string statement) => Tokenize(statement, out _);

    private static List<string> Tokenize(string statement, out bool endsWithSeparator)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        var hasToken = false;
        foreach (var c in statement ?? "")
        {
            if (c == '"')
            {
                quoted = !quoted;
                hasToken = true;
                continue;
            }
            if (char.IsWhiteSpace(c) && !quoted)
            {
                if (hasToken)
                    tokens.Add(current.ToString());
                current.Clear();
                hasToken = false;
                continue;
            }
            current.Append(c);
            hasToken = true;
        }
        if (hasToken)
            tokens.Add(current.ToString());
        endsWithSeparator = !quoted && statement != null && statement.Length > 0 && char.IsWhiteSpace(statement[^1]);

        if (tokens.Count > 0 && tokens[0].StartsWith("/"))
        {
            tokens[0] = tokens[0].TrimStart('/');
            if (tokens[0].Length == 0)
                tokens.RemoveAt(0);
        }
        return tokens;
    }

    // ---------- Autocompletar ----------

    /// <summary>
    /// Autocompletar do último comando da linha. Devolve os candidatos para o token atual e, em <paramref name="completed"/>,
    /// a linha com o token trocado pelo único candidato (+ espaço) ou pelo maior prefixo comum. Sem candidato, completed = line.
    /// </summary>
    public static List<string> Complete(string line, SceneTree tree, HUD hud, out string completed)
    {
        EnsureRegistered();
        completed = line ?? "";
        var statements = SplitStatements(completed);
        var statement = statements[^1];
        var head = completed[..(completed.Length - statement.Length)];
        var tokens = Tokenize(statement, out var endsWithSeparator);
        var slash = statement.TrimStart().StartsWith("/") ? "/" : "";

        var current = endsWithSeparator || tokens.Count == 0 ? "" : tokens[^1];
        var previous = endsWithSeparator ? tokens : tokens.Take(Math.Max(tokens.Count - 1, 0)).ToList();

        IEnumerable<string> candidates;
        if (previous.Count == 0)
        {
            candidates = Commands.Select(c => c.Name);
        }
        else
        {
            var command = Find(previous[0]);
            if (command == null)
                return new List<string>();
            var argStart = 1;
            while (argStart < previous.Count && command.FindSubcommand(previous[argStart]) is { } child)
            {
                command = child;
                argStart++;
            }
            var context = new DevCommandContext(tree, hud, previous.Skip(argStart).ToList(), null) { Command = command };
            var fromSubcommands = argStart == previous.Count ? command.Subcommands.Select(c => c.Name) : Enumerable.Empty<string>();
            IEnumerable<string> fromCompleter;
            try
            {
                fromCompleter = command.Complete?.Invoke(context) ?? Enumerable.Empty<string>();
            }
            catch (Exception exception)
            {
                GD.PushWarning($"DevConsole: autocompletar de {command.FullName} falhou: {exception.Message}");
                fromCompleter = Enumerable.Empty<string>();
            }
            candidates = fromSubcommands.Concat(fromCompleter);
        }

        var matches = DevText.FilterPrefix(candidates, current).OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
        if (matches.Count == 0)
            return matches;

        var replacement = matches.Count == 1 ? matches[0] : CommonPrefix(matches);
        if (replacement.Length < current.Length)
            replacement = current;
        var quotedReplacement = replacement.Contains(' ') ? "\"" + replacement + (matches.Count == 1 ? "\"" : "") : replacement;
        var rebuilt = previous.Select(t => t.Contains(' ') ? "\"" + t + "\"" : t).ToList();
        rebuilt.Add(quotedReplacement);
        var leading = statement.Length - statement.TrimStart().Length;
        var prefixSpace = head.Length > 0 && leading == 0 ? " " : statement[..leading];
        completed = head + prefixSpace + slash + string.Join(" ", rebuilt) + (matches.Count == 1 ? " " : "");
        return matches;
    }

    private static string CommonPrefix(List<string> values)
    {
        var prefix = values[0];
        foreach (var value in values.Skip(1))
        {
            var length = 0;
            while (length < prefix.Length && length < value.Length && char.ToLowerInvariant(prefix[length]) == char.ToLowerInvariant(value[length]))
                length++;
            prefix = prefix[..length];
        }
        return prefix;
    }

    // ---------- Histórico ----------

    private static void AddHistory(string line)
    {
        if (HistoryLines.Count > 0 && HistoryLines[^1] == line)
            return;
        HistoryLines.Add(line);
        if (HistoryLines.Count > MaxHistory)
            HistoryLines.RemoveAt(0);
    }
}
