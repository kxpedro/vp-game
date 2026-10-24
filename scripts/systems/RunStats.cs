using System.Collections.Generic;

/// <summary>
/// Estatisticas da run exibidas no game over (dano causado e recebido, abates, tempo, dano por bencao).
/// Volatil como o RunState: nao vai para o save e zera em StartRun, FailRun e no fim da campanha.
///
/// Dano por bencao: quem chama Health.ApplyDamage num inimigo marca antes a origem do golpe (Attribute para dano
/// direto de uma bencao, SetHitBonus para a parte bonus do golpe do jogador) e o Health consome a marca no mesmo
/// frame (TakeAttribution). A parte de cada bencao segue o quanto ela soma no tipo de efeito (RunState.GetBlessingShares).
/// Multiplicadores de critico, elite e chefe ficam fora da conta: o numero e uma estimativa do que a bencao acrescentou.
/// </summary>
public static class RunStats
{
    public static float DamageDealt { get; private set; }
    public static float DamageTaken { get; private set; }
    public static float HealingDone { get; private set; }
    public static float BiggestHit { get; private set; }
    public static int CriticalHits { get; private set; }
    public static int EnemiesKilled { get; private set; }
    public static int ElitesKilled { get; private set; }
    public static int RoomsCleared { get; private set; }
    public static float ElapsedSeconds { get; private set; }

    private static readonly Dictionary<string, float> BlessingDamage = new();
    // Marca pendente: (tipo de efeito, fracao do dano aplicado) valida so no frame em que foi feita.
    private static readonly List<(string EffectType, float Share)> Pending = new();
    private static readonly List<(string EffectType, float Share)> Active = new();
    private static ulong _pendingFrame = ulong.MaxValue;

    public static void Reset()
    {
        DamageDealt = DamageTaken = HealingDone = BiggestHit = ElapsedSeconds = 0.0f;
        CriticalHits = EnemiesKilled = ElitesKilled = RoomsCleared = 0;
        BlessingDamage.Clear();
        Pending.Clear();
        Active.Clear();
        _pendingFrame = ulong.MaxValue;
    }

    public static void Tick(float delta) => ElapsedSeconds += System.MathF.Max(delta, 0.0f);

    // Dano inteiro do proximo ApplyDamage num inimigo vem deste tipo de efeito (nuvem, explosao, sangramento...).
    public static void Attribute(string effectType)
    {
        Pending.Clear();
        if (!string.IsNullOrEmpty(effectType))
            Pending.Add((effectType, 1.0f));
        _pendingFrame = Godot.Engine.GetProcessFrames();
    }

    // Partes bonus do golpe (BloodBlessingRuntime.ModifyOutgoingDamage): cada parte vale part / multiplicador do dano final.
    public static void SetHitBonus(List<(string EffectType, float Part)> parts, float multiplier)
    {
        Pending.Clear();
        _pendingFrame = Godot.Engine.GetProcessFrames();
        if (multiplier <= 0.0f) return;
        foreach (var (effectType, part) in parts)
            Pending.Add((effectType, part / multiplier));
    }

    // Chamado no topo do Health.ApplyDamage de um inimigo: a marca so vale para o golpe do mesmo frame.
    public static void TakeAttribution()
    {
        Active.Clear();
        if (_pendingFrame == Godot.Engine.GetProcessFrames())
            Active.AddRange(Pending);
        Pending.Clear();
        _pendingFrame = ulong.MaxValue;
    }

    public static void RecordEnemyDamage(float applied, bool critical)
    {
        if (applied <= 0.0f) { Active.Clear(); return; }
        DamageDealt += applied;
        if (applied > BiggestHit) BiggestHit = applied;
        if (critical) CriticalHits++;
        foreach (var (effectType, share) in Active)
        {
            var shares = RunState.GetBlessingShares(effectType, out var total);
            if (shares == null || total <= 0.0f) continue;
            foreach (var (blessingId, amount) in shares)
            {
                BlessingDamage.TryGetValue(blessingId, out var current);
                BlessingDamage[blessingId] = current + applied * share * amount / total;
            }
        }
        Active.Clear();
    }

    public static void RecordDamageTaken(float amount)
    {
        if (amount > 0.0f) DamageTaken += amount;
    }

    public static void RecordHealing(float amount)
    {
        if (amount > 0.0f) HealingDone += amount;
    }

    public static void NotifyEnemyKilled(bool elite)
    {
        EnemiesKilled++;
        if (elite) ElitesKilled++;
    }

    public static void NotifyRoomCleared() => RoomsCleared++;

    // Bencaos com dano atribuido, do maior para o menor.
    public static List<(string BlessingId, float Damage)> GetBlessingDamageRanking()
    {
        var ranking = new List<(string BlessingId, float Damage)>();
        foreach (var (blessingId, damage) in BlessingDamage)
            if (damage >= 0.5f)
                ranking.Add((blessingId, damage));
        ranking.Sort((a, b) => b.Damage.CompareTo(a.Damage));
        return ranking;
    }

    // So para o preview do game over (UiPreview): numeros ficticios.
    public static void SeedPreview(IReadOnlyList<string> blessingIds)
    {
        Reset();
        DamageDealt = 18432.0f;
        DamageTaken = 1260.0f;
        HealingDone = 840.0f;
        BiggestHit = 412.0f;
        CriticalHits = 57;
        EnemiesKilled = 143;
        ElitesKilled = 9;
        RoomsCleared = 17;
        ElapsedSeconds = 1534.0f;
        var damage = 5200.0f;
        foreach (var blessingId in blessingIds)
        {
            BlessingDamage[blessingId] = damage;
            damage *= 0.55f;
        }
    }
}
