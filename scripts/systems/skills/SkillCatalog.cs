using System.Collections.Generic;

public static class SkillCatalog
{
    public const string VampireFangsId = "bestialis.vampire_fangs";
    public const string BestialTransformationId = "bestialis.bestial_transformation";
    public const string BloodHuntId = "bestialis.blood_hunt";
    public const string SurvivalInstinctId = "humanitas.survival_instinct";
    public const string PocketWatchId = "humanitas.pocket_watch";
    public const string IronWillId = "humanitas.iron_will";

    private static readonly Dictionary<string, SkillDefinition> Definitions = new()
    {
        {
            VampireFangsId,
            new SkillDefinition(
                VampireFangsId,
                "Presas de Vampiro",
                SkillTree.Bestialis,
                SkillActivationMode.Passive,
                1000,
                1,
                0.0f,
                "Cura 5% da vida maxima ao abater inimigos.",
                "PV",
                System.Array.Empty<string>()
            )
        },
        {
            SurvivalInstinctId,
            new SkillDefinition(
                SurvivalInstinctId,
                "Instinto de Sobrevivencia",
                SkillTree.Humanitas,
                SkillActivationMode.Passive,
                500,
                1,
                0.0f,
                "Com vida abaixo de 10%, aumenta a velocidade em 5%.",
                "IS",
                System.Array.Empty<string>()
            )
        },
        {
            BestialTransformationId,
            new SkillDefinition(
                BestialTransformationId,
                "Transformacao Bestial",
                SkillTree.Bestialis,
                SkillActivationMode.Active,
                1500,
                1,
                10.0f,
                "Ativa o modo Furia quando a barra estiver cheia.",
                "TB",
                new[] { VampireFangsId }
            )
        },
        {
            BloodHuntId,
            new SkillDefinition(
                BloodHuntId,
                "Cacada Rubra",
                SkillTree.Bestialis,
                SkillActivationMode.Passive,
                2200,
                1,
                0.0f,
                "Aumenta recompensas de Moedas de Sangue durante a Furia. Efeito de gameplay sera refinado depois.",
                "CR",
                new[] { BestialTransformationId }
            )
        },
        {
            PocketWatchId,
            new SkillDefinition(
                PocketWatchId,
                "Relogio de Bolso",
                SkillTree.Humanitas,
                SkillActivationMode.Passive,
                900,
                1,
                0.0f,
                "Reduz recarga de habilidades especiais. Efeito de gameplay sera refinado depois.",
                "RB",
                new[] { SurvivalInstinctId }
            )
        },
        {
            IronWillId,
            new SkillDefinition(
                IronWillId,
                "Vontade de Ferro",
                SkillTree.Humanitas,
                SkillActivationMode.Passive,
                1600,
                1,
                0.0f,
                "Aumenta sobrevivencia quando Virgil esta perto da morte. Efeito de gameplay sera refinado depois.",
                "VF",
                new[] { PocketWatchId }
            )
        }
    };

    public static IEnumerable<SkillDefinition> All => Definitions.Values;

    public static bool TryGet(string skillId, out SkillDefinition definition)
    {
        return Definitions.TryGetValue(skillId, out definition);
    }

    public static SkillDefinition Get(string skillId)
    {
        return Definitions[skillId];
    }

    public static IEnumerable<SkillDefinition> GetTree(SkillTree tree)
    {
        foreach (var definition in Definitions.Values)
        {
            if (definition.Tree == tree)
                yield return definition;
        }
    }

    public static bool HasPrerequisites(SkillDefinition definition)
    {
        foreach (var prerequisite in definition.Prerequisites)
        {
            if (!CharacterSheet.HasSkill(prerequisite))
                return false;
        }

        return true;
    }
}
