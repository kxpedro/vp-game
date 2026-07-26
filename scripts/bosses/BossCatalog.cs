public sealed record BossMoveDefinition(
	string Id,
	string DisplayName,
	float Cooldown,
	float DamageMultiplier,
	float RangeMultiplier,
	int MinimumPhase = 1
);

public sealed record BossDefinition(string Id, string DisplayName, float TargetDurationSeconds, float ExpectedPlayerDps, BossMoveDefinition[] Moves)
{
	public float EstimatedMaxHealth => System.MathF.Round(TargetDurationSeconds * ExpectedPlayerDps);
}

public static class BossCatalog
{
	private static readonly BossDefinition[] Bosses =
	{
		new("silver_dragonkin", "Dragonkin Prateado", 300, 16.6667f, new[]
		{
			new BossMoveDefinition("claw_combo", "Trindade de Garras", 1.25f, 1.0f, 1.0f),
			new BossMoveDefinition("tail_sweep", "Varredura de Cauda", 1.6f, 1.2f, 1.45f),
			new BossMoveDefinition("dragon_volley", "Saliva Draconica", 1.8f, 1.1f, 1.2f),
			new BossMoveDefinition("blood_charge", "Investida de Sangue", 2.15f, 1.5f, 1.35f, 2),
			new BossMoveDefinition("meteor_requiem", "Requiem Draconico", 2.7f, 1.75f, 1.7f, 3)
		}),
		new("invisible_woman", "Guardia Invisivel", 280, 50, new[]
		{
			new BossMoveDefinition("psionic_volley", "Saraivada Psionica", 1.35f, 1.05f, 1.2f),
			new BossMoveDefinition("kinetic_shift", "Salto Cinetico", 1.8f, 0.9f, 1.15f),
			new BossMoveDefinition("force_outbreak", "Surto de Forca", 1.95f, 1.25f, 1.55f),
			new BossMoveDefinition("mirror_dash", "Investida Espelhada", 2.3f, 1.55f, 1.4f, 2),
			new BossMoveDefinition("singularity", "Singularidade Rubra", 2.8f, 1.8f, 1.8f, 3)
		}),
		new("labyrinth_keeper", "Guardiao do Labirinto", 300, 58, new[]
		{
			new BossMoveDefinition("psionic_volley", "Marcas do Labirinto", 1.45f, 1.15f, 1.25f),
			new BossMoveDefinition("tail_sweep", "Circulo das Correntes", 1.75f, 1.3f, 1.5f),
			new BossMoveDefinition("kinetic_shift", "Passo Entre Paredes", 1.9f, 1.0f, 1.2f),
			new BossMoveDefinition("mirror_dash", "Atalho Impossivel", 2.25f, 1.6f, 1.45f, 2),
			new BossMoveDefinition("singularity", "Centro do Labirinto", 2.9f, 1.9f, 1.85f, 3)
		}),
		new("armand", "Lorde Armand", 360, 70, new[]
		{
			new BossMoveDefinition("claw_combo", "Etiqueta do Rei", 1.2f, 1.25f, 1.1f),
			new BossMoveDefinition("psionic_volley", "Decreto de Sangue", 1.5f, 1.3f, 1.3f),
			new BossMoveDefinition("kinetic_shift", "Trono Intocavel", 1.85f, 1.0f, 1.2f),
			new BossMoveDefinition("meteor_requiem", "Chuva sobre os Subditos", 2.45f, 1.8f, 1.75f, 2),
			new BossMoveDefinition("singularity", "Sangue de Mil Anos", 3.0f, 2.2f, 2.0f, 3)
		})
	};

	public static BossDefinition Get(int mapIndex) => Bosses[System.Math.Clamp(mapIndex, 0, Bosses.Length - 1)];
}
