using System.Collections.Generic;
using Godot;

/// <summary>Algoritmo de selecao de conversas (docs/design/dialogos-arquitetura.md, 4.2 e 4.3). Sem efeitos colaterais.</summary>
public static class DialogueSelector
{
    public static DialogueDefinition Select(IEnumerable<DialogueDefinition> candidates, DialogueContext context, RandomNumberGenerator rng, bool allowFallback)
    {
        var eligible = new List<DialogueDefinition>();
        DialogueDefinition fallback = null;
        foreach (var c in candidates)
        {
            if (!DialogueConditions.EvaluateAll(c.Requirements, context)) continue;
            var seen = DialogueProgress.GetSeen(c.Id);
            if (seen > 0)
            {
                if (allowFallback && c.Repeatable && (fallback == null || seen < DialogueProgress.GetSeen(fallback.Id)))
                    fallback = c;
                if (!(c.Repeatable && DialogueProgress.GetPlays(c.Pool) - seen >= c.CooldownVisits)) continue;
            }
            // Uma chamada de sorteio por elegivel com chance < 1.
            if (c.Chance < 1.0f && rng.Randf() > c.Chance) continue;
            eligible.Add(c);
        }

        if (eligible.Count > 0)
        {
            var best = eligible[0];
            var bestTie = rng.Randi();
            for (var i = 1; i < eligible.Count; i++)
            {
                var tie = rng.Randi();
                if (Compare(eligible[i], best, tie, bestTie) < 0)
                {
                    best = eligible[i];
                    bestTie = tie;
                }
            }
            return best;
        }
        return allowFallback ? fallback : null;
    }

    // Menor = melhor: classe, nao vista, prioridade, mais antiga, sorteio.
    private static int Compare(DialogueDefinition a, DialogueDefinition b, uint tieA, uint tieB)
    {
        var byClass = ((int)b.PriorityClass).CompareTo((int)a.PriorityClass);
        if (byClass != 0) return byClass;
        var aSeen = DialogueProgress.GetSeen(a.Id);
        var bSeen = DialogueProgress.GetSeen(b.Id);
        var byUnseen = (aSeen > 0 ? 1 : 0).CompareTo(bSeen > 0 ? 1 : 0);
        if (byUnseen != 0) return byUnseen;
        var byPriority = b.Priority.CompareTo(a.Priority);
        if (byPriority != 0) return byPriority;
        var byAge = aSeen.CompareTo(bSeen);
        if (byAge != 0) return byAge;
        return tieA.CompareTo(tieB);
    }
}
