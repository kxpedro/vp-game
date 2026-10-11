// Textos exibidos ficam em data/localization/<idioma>/world.json e sao resolvidos a cada leitura
// (troca de idioma em runtime). Ids nao mudam: CodexProgress e CampaignCatalog dependem deles.
public sealed record BossMoveDefinition(
	string BossId,
	string Id,
	float Cooldown,
	float DamageMultiplier,
	float RangeMultiplier,
	int MinimumPhase = 1
)
{
	// O mesmo Id de golpe se repete entre chefes com nomes diferentes, por isso a chave inclui o chefe.
	public string NameKey => $"boss.{BossId}.move.{Id}.name";
	public string DisplayName => Localization.Text(NameKey);
}

public sealed record BossDefinition(string Id, float TargetDurationSeconds, float ExpectedPlayerDps, BossMoveDefinition[] Moves)
{
	public float EstimatedMaxHealth => System.MathF.Round(TargetDurationSeconds * ExpectedPlayerDps);
	public string NameKey => $"boss.{Id}.name";
	public string DisplayName => Localization.Text(NameKey);
}

public static class BossCatalog
{
	private static readonly BossDefinition[] Bosses =
	{
		// Ordem narrativa = indice do mapa. A vida (duracao x DPS) segue o SLOT, nao o boss, para a dificuldade crescer com o mapa.
		new("invisible_woman", 300, 16.6667f, new[]
		{
			new BossMoveDefinition("invisible_woman", "psionic_volley", 1.35f, 1.05f, 1.2f),
			new BossMoveDefinition("invisible_woman", "kinetic_shift", 1.8f, 0.9f, 1.15f),
			new BossMoveDefinition("invisible_woman", "force_outbreak", 1.95f, 1.25f, 1.55f),
			new BossMoveDefinition("invisible_woman", "mirror_dash", 2.3f, 1.55f, 1.4f, 2),
			new BossMoveDefinition("invisible_woman", "singularity", 2.8f, 1.8f, 1.8f, 3)
		}),
		new("labyrinth_keeper", 280, 50, new[]
		{
			new BossMoveDefinition("labyrinth_keeper", "psionic_volley", 1.45f, 1.15f, 1.25f),
			new BossMoveDefinition("labyrinth_keeper", "tail_sweep", 1.75f, 1.3f, 1.5f),
			new BossMoveDefinition("labyrinth_keeper", "kinetic_shift", 1.9f, 1.0f, 1.2f),
			new BossMoveDefinition("labyrinth_keeper", "mirror_dash", 2.25f, 1.6f, 1.45f, 2),
			new BossMoveDefinition("labyrinth_keeper", "singularity", 2.9f, 1.9f, 1.85f, 3)
		}),
		new("silver_dragonkin", 300, 58, new[]
		{
			new BossMoveDefinition("silver_dragonkin", "claw_combo", 1.25f, 1.0f, 1.0f),
			new BossMoveDefinition("silver_dragonkin", "tail_sweep", 1.6f, 1.2f, 1.45f),
			new BossMoveDefinition("silver_dragonkin", "dragon_volley", 1.8f, 1.1f, 1.2f),
			new BossMoveDefinition("silver_dragonkin", "blood_charge", 2.15f, 1.5f, 1.35f, 2),
			new BossMoveDefinition("silver_dragonkin", "meteor_requiem", 2.7f, 1.75f, 1.7f, 3)
		}),
		new("armand", 360, 70, new[]
		{
			new BossMoveDefinition("armand", "claw_combo", 1.2f, 1.25f, 1.1f),
			new BossMoveDefinition("armand", "psionic_volley", 1.5f, 1.3f, 1.3f),
			new BossMoveDefinition("armand", "kinetic_shift", 1.85f, 1.0f, 1.2f),
			new BossMoveDefinition("armand", "meteor_requiem", 2.45f, 1.8f, 1.75f, 2),
			new BossMoveDefinition("armand", "singularity", 3.0f, 2.2f, 2.0f, 3)
		})
	};

	public static BossDefinition Get(int mapIndex) => Bosses[System.Math.Clamp(mapIndex, 0, Bosses.Length - 1)];
}
