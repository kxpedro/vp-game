using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;

/// <summary>
/// Carrega res://data/dialogue/*.json (JSONC): speakers.json = falantes; os demais = conversas.
/// Erro numa conversa desativa so aquela conversa (GD.PushError com o id).
/// </summary>
public static class DialogueCatalog
{
    private const string Directory = "res://data/dialogue";
    private static readonly Regex IdPattern = new("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);
    private static readonly string[] Emotions = { "neutral", "grim", "amused", "angry", "sad", "afraid", "awe" };
    private static readonly List<DialogueDefinition> Definitions = new();
    private static readonly Dictionary<string, DialogueDefinition> ById = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, SpeakerDefinition> Speakers = new(StringComparer.Ordinal);
    private static bool _loaded;

    public static IReadOnlyList<DialogueDefinition> All { get { EnsureLoaded(); return Definitions; } }

    public static bool TryGet(string conversationId, out DialogueDefinition definition)
    {
        EnsureLoaded();
        if (conversationId != null && ById.TryGetValue(conversationId, out definition)) return true;
        definition = null;
        return false;
    }

    public static bool TryGetSpeaker(string speakerId, out SpeakerDefinition speaker)
    {
        EnsureLoaded();
        if (speakerId != null && Speakers.TryGetValue(speakerId, out speaker)) return true;
        speaker = null;
        return false;
    }

    public static IEnumerable<DialogueDefinition> Where(DialogueCategory category, string speakerId = null, DialogueTrigger trigger = DialogueTrigger.None)
    {
        EnsureLoaded();
        foreach (var definition in Definitions)
        {
            if (definition.Category != category) continue;
            if (speakerId != null && definition.SpeakerId != speakerId) continue;
            if (trigger != DialogueTrigger.None && definition.Trigger != trigger) continue;
            yield return definition;
        }
    }

    /// <summary>So debug/selftest.</summary>
    public static void Reload()
    {
        _loaded = false;
        EnsureLoaded();
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        Definitions.Clear();
        ById.Clear();
        Speakers.Clear();

        string[] files;
        try { files = DirAccess.GetFilesAt(Directory); }
        catch (Exception exception) { GD.PushError($"Dialogue catalog: cannot list {Directory}: {exception.Message}"); return; }
        if (files == null || files.Length == 0) { GD.PushError($"Dialogue catalog: no files in {Directory}."); return; }
        Array.Sort(files, StringComparer.Ordinal);

        var documents = new List<(string Path, JsonDocument Document)>();
        try
        {
            // Falantes primeiro (as conversas validam o falante contra eles).
            foreach (var file in files)
            {
                if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                var path = $"{Directory}/{file}";
                try
                {
                    var document = JsonDocument.Parse(FileAccess.GetFileAsString(path),
                        new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                    documents.Add((path, document));
                    if (file.Equals("speakers.json", StringComparison.OrdinalIgnoreCase))
                        LoadSpeakers(path, document.RootElement);
                }
                catch (Exception exception) { GD.PushError($"Dialogue catalog: failed to load {path}: {exception.Message}"); }
            }
            foreach (var (path, document) in documents)
            {
                if (path.EndsWith("/speakers.json", StringComparison.OrdinalIgnoreCase)) continue;
                try { LoadConversations(path, document.RootElement); }
                catch (Exception exception) { GD.PushError($"Dialogue catalog: failed to read {path}: {exception.Message}"); }
            }
        }
        finally
        {
            foreach (var (_, document) in documents) document.Dispose();
        }
    }

    // ---------- Falantes ----------

    private static void LoadSpeakers(string path, JsonElement root)
    {
        if (!root.TryGetProperty("speakers", out var list) || list.ValueKind != JsonValueKind.Array) { GD.PushError($"Dialogue catalog: {path} sem 'speakers'."); return; }
        foreach (var element in list.EnumerateArray())
        {
            try
            {
                var id = Str(element, "id");
                if (!IdPattern.IsMatch(id) || Speakers.ContainsKey(id)) { GD.PushError($"Dialogue speaker '{id}' invalido ou duplicado em {path}."); continue; }
                var nameKey = Str(element, "name_key");
                if (string.IsNullOrEmpty(nameKey)) nameKey = $"dlg.speaker.{id}.name";
                var color = ParseColor(Str(element, "name_color"), UiPalette.Gold);
                var portraits = new Dictionary<string, PortraitSpec>(StringComparer.Ordinal);
                if (element.TryGetProperty("portraits", out var portraitsElement) && portraitsElement.ValueKind == JsonValueKind.Object)
                {
                    var fallback = new PortraitSpec("", id.Substring(0, 1).ToUpperInvariant(), new Color("#3A3040"), UiPalette.Gold);
                    if (portraitsElement.TryGetProperty("default", out var defaultElement))
                        fallback = ReadPortrait(defaultElement, fallback);
                    portraits["default"] = fallback;
                    foreach (var property in portraitsElement.EnumerateObject())
                        if (property.Name != "default") portraits[property.Name] = ReadPortrait(property.Value, fallback);
                }
                else
                {
                    portraits["default"] = new PortraitSpec("", id.Substring(0, 1).ToUpperInvariant(), new Color("#3A3040"), UiPalette.Gold);
                }
                var kind = Str(element, "kind");
                Speakers[id] = new SpeakerDefinition(id, nameKey, kind == "player", color, portraits);
            }
            catch (Exception exception) { GD.PushError($"Dialogue speaker em {path}: {exception.Message}"); }
        }
    }

    private static PortraitSpec ReadPortrait(JsonElement element, PortraitSpec baseSpec)
    {
        var texture = element.TryGetProperty("texture", out _) ? Str(element, "texture") : baseSpec.TexturePath;
        var initial = Str(element, "initial");
        return new PortraitSpec(
            texture,
            string.IsNullOrEmpty(initial) ? baseSpec.Initial : initial,
            ParseColor(Str(element, "color"), baseSpec.Color),
            ParseColor(Str(element, "accent"), baseSpec.Accent));
    }

    private static Color ParseColor(string html, Color fallback)
        => !string.IsNullOrEmpty(html) && Color.HtmlIsValid(html) ? Color.FromHtml(html) : fallback;

    // ---------- Conversas ----------

    private static void LoadConversations(string path, JsonElement root)
    {
        if (!root.TryGetProperty("conversations", out var list) || list.ValueKind != JsonValueKind.Array) { GD.PushError($"Dialogue catalog: {path} sem 'conversations'."); return; }
        foreach (var element in list.EnumerateArray())
        {
            var id = Str(element, "id");
            try
            {
                var definition = ParseConversation(element, id, out var error);
                if (definition == null) { GD.PushError($"Dialogue '{id}' desativada ({path}): {error}"); continue; }
                if (ById.ContainsKey(id)) { GD.PushError($"Dialogue '{id}' duplicada ({path}): ignorada."); continue; }
                Definitions.Add(definition);
                ById[id] = definition;
            }
            catch (Exception exception) { GD.PushError($"Dialogue '{id}' desativada ({path}): {exception.Message}"); }
        }
    }

    private static DialogueDefinition ParseConversation(JsonElement e, string id, out string error)
    {
        error = null;
        if (!IdPattern.IsMatch(id) || id == "speaker" || id == "choice") { error = "id invalido"; return null; }
        var speaker = Str(e, "speaker");
        if (!Speakers.ContainsKey(speaker)) { error = $"speaker '{speaker}' inexistente"; return null; }
        if (!Enum.TryParse(Pascal(Str(e, "category")), out DialogueCategory category)) { error = "category invalida"; return null; }
        var trigger = DialogueTrigger.None;
        if (category == DialogueCategory.Bark)
        {
            if (!Enum.TryParse(Pascal(Str(e, "trigger")), out trigger) || trigger == DialogueTrigger.None) { error = "bark exige trigger valido"; return null; }
        }
        else if (!string.IsNullOrEmpty(Str(e, "trigger"))) { error = "trigger so vale em bark"; return null; }
        if (!Enum.TryParse(Pascal(Str(e, "priority_class")), out DialoguePriorityClass priorityClass)) { error = "priority_class invalida"; return null; }

        var requirements = new List<DialogueCondition>();
        if (e.TryGetProperty("requirements", out var reqs) && reqs.ValueKind == JsonValueKind.Array)
            foreach (var item in reqs.EnumerateArray())
            {
                var condition = DialogueConditions.Parse(item, out var conditionError);
                if (condition == null) { error = conditionError; return null; }
                requirements.Add(condition);
            }

        var lines = ParseLines(e, "lines", out error);
        if (lines == null) return null;
        if (lines.Length == 0) { error = "sem linhas"; return null; }
        if (category == DialogueCategory.Bark && lines.Length > 2) { error = "bark com mais de 2 linhas"; return null; }

        var branches = new Dictionary<string, DialogueBranch>(StringComparer.Ordinal);
        if (e.TryGetProperty("branches", out var branchesElement) && branchesElement.ValueKind == JsonValueKind.Object)
            foreach (var property in branchesElement.EnumerateObject())
            {
                var branchLines = ParseLines(property.Value, "lines", out error);
                if (branchLines == null) return null;
                if (!IdPattern.IsMatch(property.Name) || branchLines.Length == 0) { error = $"ramo '{property.Name}' invalido"; return null; }
                branches[property.Name] = new DialogueBranch(property.Name, branchLines, Strings(property.Value, "sets_flags"));
            }

        var choices = new List<DialogueChoice>();
        if (e.TryGetProperty("choices", out var choicesElement) && choicesElement.ValueKind == JsonValueKind.Array)
        {
            if (choicesElement.GetArrayLength() > 3) { error = "mais de 3 escolhas"; return null; }
            var seenIds = new HashSet<string>();
            foreach (var item in choicesElement.EnumerateArray())
            {
                var choiceId = Str(item, "id");
                var textKey = Str(item, "text_key");
                var next = Str(item, "next");
                if (!IdPattern.IsMatch(choiceId) || !seenIds.Add(choiceId) || string.IsNullOrEmpty(textKey)) { error = $"escolha '{choiceId}' invalida"; return null; }
                if (!string.IsNullOrEmpty(next) && !branches.ContainsKey(next)) { error = $"escolha '{choiceId}': ramo '{next}' inexistente"; return null; }
                choices.Add(new DialogueChoice(choiceId, textKey, Strings(item, "sets_flags"), next));
            }
        }

        var repeatable = Bool(e, "repeatable", false);
        var cooldown = Math.Max(Int(e, "cooldown_visits", category == DialogueCategory.Bark ? 6 : 4), 0);
        var chance = Mathf.Clamp(e.TryGetProperty("chance", out var ch) && ch.ValueKind == JsonValueKind.Number ? (float)ch.GetDouble() : 1.0f, 0.0f, 1.0f);
        var autoStart = Bool(e, "auto_start", false) && priorityClass == DialoguePriorityClass.Essential
            && (category == DialogueCategory.Hub || category == DialogueCategory.Death || category == DialogueCategory.Return);

        return new DialogueDefinition(id, speaker, category, trigger, priorityClass, Math.Clamp(Int(e, "priority", 0), 0, 99),
            repeatable, cooldown, chance, autoStart, requirements.ToArray(), Strings(e, "sets_flags"),
            lines, choices.ToArray(), branches);
    }

    private static DialogueLine[] ParseLines(JsonElement owner, string name, out string error)
    {
        error = null;
        if (!owner.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array) { error = $"'{name}' ausente"; return null; }
        var lines = new List<DialogueLine>();
        foreach (var item in array.EnumerateArray())
        {
            var textKey = Str(item, "text_key");
            if (string.IsNullOrEmpty(textKey)) { error = "linha sem text_key"; return null; }
            var lineSpeaker = Str(item, "speaker");
            if (!string.IsNullOrEmpty(lineSpeaker) && !Speakers.ContainsKey(lineSpeaker)) { error = $"speaker de linha '{lineSpeaker}' inexistente"; return null; }
            var emotion = Str(item, "emotion");
            if (!string.IsNullOrEmpty(emotion) && Array.IndexOf(Emotions, emotion) < 0) { error = $"emotion '{emotion}' invalida"; return null; }
            lines.Add(new DialogueLine(textKey, lineSpeaker, emotion, Str(item, "portrait")));
        }
        return lines.ToArray();
    }

    // "boss_pre" -> "BossPre"
    private static string Pascal(string snake)
    {
        if (string.IsNullOrEmpty(snake)) return "";
        var parts = snake.Split('_');
        for (var i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
        return string.Concat(parts);
    }

    private static string Str(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static int Int(JsonElement element, string name, int fallback)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : fallback;

    private static bool Bool(JsonElement element, string name, bool fallback)
        => element.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) ? v.GetBoolean() : fallback;

    private static string[] Strings(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        var list = new List<string>();
        foreach (var item in array.EnumerateArray())
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString())) list.Add(item.GetString());
        return list.ToArray();
    }
}
