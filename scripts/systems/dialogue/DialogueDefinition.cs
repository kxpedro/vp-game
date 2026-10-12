using System.Collections.Generic;

// Modelos do sistema de dialogo (docs/design/dialogos-arquitetura.md, 6.2). Textos ficam em
// data/localization/<idioma>/dialogue.json; aqui so IDs, numeros e condicoes.
public enum DialogueCategory { Hub, Bark, BossPre, BossPost, Death, Return }
public enum DialogueTrigger { None, RoomEnter, RoomCleared, LowHealth, BossPhase }
public enum DialoguePriorityClass { Evergreen = 1, Specific = 2, Essential = 3 }
public enum DialogueEvent { RoomEntered, RoomCleared, BossDefeated, BossPhaseChanged }

/// <summary>SpeakerId/Emotion/Portrait vazios = herdar (falante da conversa e retrato default).</summary>
public sealed record DialogueLine(string TextKey, string SpeakerId, string Emotion, string Portrait);
public sealed record DialogueBranch(string Id, DialogueLine[] Lines, string[] SetsFlags);
public sealed record DialogueChoice(string Id, string TextKey, string[] SetsFlags, string NextBranchId);

public sealed record DialogueDefinition(
    string Id, string SpeakerId, DialogueCategory Category, DialogueTrigger Trigger,
    DialoguePriorityClass PriorityClass, int Priority, bool Repeatable, int CooldownVisits, float Chance,
    bool AutoStart, DialogueCondition[] Requirements, string[] SetsFlags,
    DialogueLine[] Lines, DialogueChoice[] Choices, IReadOnlyDictionary<string, DialogueBranch> Branches)
{
    public string Pool => Category == DialogueCategory.Bark ? "bark" : "npc";
}
