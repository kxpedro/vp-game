using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

/// <summary>Dados nao estaticos usados pelas condicoes. PlayerHealthRatio = -1 quando nao ha jogador.</summary>
public sealed record DialogueContext(float PlayerHealthRatio);

/// <summary>Condicao ja parseada (lista fechada: docs/design/dialogos-arquitetura.md, secao 3).</summary>
public sealed class DialogueCondition
{
    public string Type = "";
    public string Id = "";
    public string Category = "";
    public int? Min;
    public int? Max;
    public bool Value;
    public string StringValue = "";
    public float Ratio;
    public int MinRank = 1;
    public DialogueCondition Child;
    public DialogueCondition[] Children = Array.Empty<DialogueCondition>();
}

public static class DialogueConditions
{
    private static readonly HashSet<string> RangeTypes = new() { "runs_started", "runs_completed", "deaths", "room", "cursed_blood" };
    private static readonly HashSet<string> IdTypes = new()
    {
        "boss_defeated", "boss_met", "boss_in_room", "map", "seen", "not_seen", "flag", "no_flag",
        "weapon", "blessing_owned", "died_at_boss", "last_run_map"
    };
    private static readonly HashSet<string> BoolTypes = new() { "boss_room", "in_run" };
    private static readonly HashSet<string> Outcomes = new() { "died", "victory", "abandoned" };

    /// <summary>Converte um item de "requirements". error != null = condicao invalida (a conversa e desativada).</summary>
    public static DialogueCondition Parse(JsonElement element, out string error)
    {
        error = null;
        if (element.ValueKind != JsonValueKind.Object) { error = "condicao nao e um objeto"; return null; }
        var type = Str(element, "type");
        if (string.IsNullOrEmpty(type)) { error = "condicao sem 'type'"; return null; }
        var condition = new DialogueCondition { Type = type };

        if (RangeTypes.Contains(type))
        {
            condition.Min = Int(element, "min");
            condition.Max = Int(element, "max");
            if (condition.Min == null && condition.Max == null) error = $"'{type}' exige min e/ou max";
        }
        else if (IdTypes.Contains(type))
        {
            condition.Id = Str(element, "id");
            if (string.IsNullOrEmpty(condition.Id)) error = $"'{type}' exige id";
        }
        else if (BoolTypes.Contains(type))
        {
            if (!element.TryGetProperty("value", out var v) || (v.ValueKind != JsonValueKind.True && v.ValueKind != JsonValueKind.False))
                error = $"'{type}' exige value booleano";
            else condition.Value = v.GetBoolean();
        }
        else
        {
            switch (type)
            {
                case "health_below":
                    if (!element.TryGetProperty("ratio", out var r) || r.ValueKind != JsonValueKind.Number) error = "'health_below' exige ratio";
                    else condition.Ratio = (float)r.GetDouble();
                    break;
                case "last_run_outcome":
                    condition.StringValue = Str(element, "value");
                    if (!Outcomes.Contains(condition.StringValue)) error = "'last_run_outcome' exige value died|victory|abandoned";
                    break;
                case "skill":
                    condition.Id = Str(element, "id");
                    condition.MinRank = Int(element, "min_rank") ?? 1;
                    if (string.IsNullOrEmpty(condition.Id)) error = "'skill' exige id";
                    break;
                case "discovered":
                    condition.Category = Str(element, "category");
                    condition.Id = Str(element, "id");
                    if (string.IsNullOrEmpty(condition.Category) || string.IsNullOrEmpty(condition.Id)) error = "'discovered' exige category e id";
                    break;
                case "not":
                    if (!element.TryGetProperty("condition", out var child)) { error = "'not' exige condition"; break; }
                    condition.Child = Parse(child, out error);
                    break;
                case "any":
                    if (!element.TryGetProperty("conditions", out var list) || list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
                    {
                        error = "'any' exige conditions (lista nao vazia)";
                        break;
                    }
                    var children = new List<DialogueCondition>();
                    foreach (var item in list.EnumerateArray())
                    {
                        var parsed = Parse(item, out error);
                        if (error != null) break;
                        children.Add(parsed);
                    }
                    condition.Children = children.ToArray();
                    break;
                default:
                    error = $"tipo de condicao desconhecido '{type}'";
                    break;
            }
        }
        return error == null ? condition : null;
    }

    public static bool EvaluateAll(IReadOnlyList<DialogueCondition> conditions, DialogueContext context)
    {
        if (conditions == null) return true;
        for (var i = 0; i < conditions.Count; i++)
            if (!Evaluate(conditions[i], context)) return false;
        return true;
    }

    public static bool Evaluate(DialogueCondition c, DialogueContext context)
    {
        if (c == null) return false;
        switch (c.Type)
        {
            case "runs_started": return InRange(DialogueProgress.RunsStarted, c);
            case "runs_completed": return InRange(DialogueProgress.RunsCompleted, c);
            case "deaths": return InRange(DialogueProgress.Deaths, c);
            case "room": return InRange(RunState.CurrentRoom, c);
            case "cursed_blood": return InRange(CharacterSheet.CursedBlood, c);
            case "boss_defeated": return DialogueProgress.IsBossDefeated(c.Id);
            case "boss_met": return CodexProgress.IsDiscoveredRaw(CodexCategory.Boss, c.Id);
            case "boss_in_room":
                return RunState.IsRunActive && RunState.IsBossRoom && BossCatalog.Get(RunState.CurrentMapIndex).Id == c.Id;
            case "map": return CampaignCatalog.Get(RunState.CurrentMapIndex).Id == c.Id;
            case "boss_room": return (RunState.IsRunActive && RunState.IsBossRoom) == c.Value;
            case "in_run": return RunState.IsRunActive == c.Value;
            case "seen": return DialogueProgress.IsSeen(c.Id);
            case "not_seen": return !DialogueProgress.IsSeen(c.Id);
            case "flag": return DialogueProgress.HasFlag(c.Id);
            case "no_flag": return !DialogueProgress.HasFlag(c.Id);
            case "weapon": return WeaponLoadout.EquippedWeaponId == c.Id;
            case "health_below": return context != null && context.PlayerHealthRatio >= 0.0f && context.PlayerHealthRatio < c.Ratio;
            case "last_run_outcome":
                return DialogueProgress.LastRun != null && DialogueProgress.LastRun.Outcome == ParseOutcome(c.StringValue);
            case "died_at_boss":
                return DialogueProgress.LastRun is { Outcome: RunOutcome.Died, BossRoom: true } run && run.BossId == c.Id;
            case "last_run_map":
                return DialogueProgress.LastRun != null && CampaignCatalog.Get(DialogueProgress.LastRun.MapIndex).Id == c.Id;
            case "skill": return CharacterSheet.GetSkillRank(c.Id) >= c.MinRank;
            case "blessing_owned": return RunState.GetBlessingStack(c.Id) > 0;
            case "discovered": return CodexProgress.IsDiscoveredRaw(c.Category, c.Id);
            case "not": return !Evaluate(c.Child, context);
            case "any":
                foreach (var child in c.Children)
                    if (Evaluate(child, context)) return true;
                return false;
            default: return false;
        }
    }

    private static RunOutcome ParseOutcome(string value) => value switch
    {
        "died" => RunOutcome.Died,
        "victory" => RunOutcome.Victory,
        "abandoned" => RunOutcome.Abandoned,
        _ => RunOutcome.None
    };

    private static bool InRange(int value, DialogueCondition c)
        => (c.Min == null || value >= c.Min.Value) && (c.Max == null || value <= c.Max.Value);

    private static string Str(JsonElement element, string name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static int? Int(JsonElement element, string name)
        => element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
}
