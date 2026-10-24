using Godot;
using System.Threading.Tasks;

// Golpes da Guardia Invisivel (boss do mapa 1) com efeitos proprios, no lugar dos circulos vermelhos genericos:
// - Saraivada Psionica: orbes violeta lancados da Guardia caem sobre circulos de runas.
// - Salto Cinetico: escudo + runas em volta dela; some (fica invisivel) e reaparece ao lado do jogador.
// - Surto de Forca: ondas em ANEL que correm para fora (so a faixa do anel fere, nao o disco inteiro).
// - Investida Espelhada: deixa uma imagem espelhada que explode e atravessa a sala deixando rastro.
// - Singularidade Rubra: vortice sob o jogador que implode e depois se espalha em ondulacoes.
// Dano, alcance e tempos continuam nas chaves boss.* do painel de dev; as novas estao em DevParameters.Enemies.cs.
public partial class BossController
{
    private bool IsInvisibleWoman => Definition?.Id == "invisible_woman";

    // Forma usada pelo proximo CreateCircleTelegraph (StrikeAt nao sabe qual golpe o chamou).
    private BossRuneFx.Shape _nextCircleShape = BossRuneFx.Shape.Runes;

    private Node3D TrackTelegraph(Node3D telegraph)
    {
        _activeTelegraphs.Add(telegraph);
        return telegraph;
    }

    private float FloorY(Vector3 center) => Mathf.Max(center.Y, _enemy.GlobalPosition.Y);

    private Task ExecuteInvisibleWomanMove(BossMoveDefinition move) => move.Id switch
    {
        "psionic_volley" => ExecuteOrbVolley(move, CompendiumTuning.GetInt("boss.psionic_volley.shots_base", 4) + CurrentPhase, G("boss.psionic_volley.radius", 1.15f), G("boss.psionic_volley.cadence", 0.38f)),
        "kinetic_shift" => ExecuteVanishShift(move),
        "force_outbreak" => ExecuteShockwaves(move, G("boss.force_outbreak.radius", 5.0f), G("boss.force_outbreak.windup", 0.48f), CompendiumTuning.GetInt("boss.force_outbreak.pulses_base", 2) + CurrentPhase),
        "mirror_dash" => ExecuteMirrorImageDash(move),
        "singularity" => ExecuteRippleSingularity(move),
        _ => ExecuteDefaultMove(move)
    };

    private async Task ExecuteOrbVolley(BossMoveDefinition move, int count, float radius, float cadence)
    {
        for (var shot = 0; shot < count; shot++)
        {
            if (!CanContinue()) return;
            var target = _enemy.GetCombatTarget();
            if (target == null) return;
            _enemy.FaceCombatTarget();
            var lead = target is CharacterBody3D body ? body.Velocity * G("boss.volley.lead", 0.22f) : Vector3.Zero;
            var scatterRange = G("boss.volley.scatter", 0.65f);
            var scatter = new Vector3(_rng.RandfRange(-scatterRange, scatterRange), 0.0f, _rng.RandfRange(-scatterRange, scatterRange));
            var center = ClampBossDestination(target.GlobalPosition + lead + scatter);
            center.Y = target.GlobalPosition.Y;
            var floor = shot == 0 ? MinSequenceFirstWarning : MinSingleCircleWarning;
            BossOrbFx.Launch(GetTree().CurrentScene, OrbOrigin(), center, Mathf.Max(cadence * CastTimeMultiplier, floor));
            await StrikeAt(center, radius, _enemy.AttackDamage * G("boss.volley.damage", 0.58f), cadence, move.DisplayName, floor);
            await WaitSeconds(G("boss.volley.gap", 0.08f));
        }
    }

    private Vector3 OrbOrigin() => _enemy.GlobalPosition + Vector3.Up * G("boss.invisible_woman.orb_height", 1.7f);

    private async Task ExecuteVanishShift(BossMoveDefinition move)
    {
        var target = _enemy.GetCombatTarget();
        if (target == null) return;
        _health.ShieldHits += CompendiumTuning.GetInt("boss.kinetic_shift.shield_base", 2) + CurrentPhase;
        _enemy.PixelVisual?.Flash(BossRuneFx.Pale[2], 0.4f);
        await StrikeAt(_enemy.GlobalPosition, G("boss.kinetic_shift.radius", 2.4f), _enemy.AttackDamage * G("boss.kinetic_shift.damage", 0.45f), G("boss.kinetic_shift.windup", 0.42f), move.DisplayName);
        if (!CanContinue()) return;
        var angle = _rng.RandfRange(0.0f, Mathf.Tau);
        var offset = new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * G("boss.kinetic_shift.teleport_distance", 5.2f);
        var destination = ClampBossDestination(target.GlobalPosition + offset);
        await WarnTeleport(destination);
        if (!CanContinue()) return;
        _enemy.TeleportBoss(destination);
        _enemy.FaceCombatTarget();
        await AppearAfterTeleport();
        if (!CanContinue()) return;
        BossOrbFx.Launch(GetTree().CurrentScene, OrbOrigin(), target.GlobalPosition, Mathf.Max(G("boss.kinetic_shift.strike_windup", 0.35f) * CastTimeMultiplier, MinSingleCircleWarning));
        await StrikeAt(target.GlobalPosition, G("boss.kinetic_shift.strike_radius", 1.55f), _enemy.AttackDamage * G("boss.kinetic_shift.strike_damage", 0.65f), G("boss.kinetic_shift.strike_windup", 0.35f), move.DisplayName, MinSingleCircleWarning);
    }

    // Ondas em anel: cada pulso fere so a faixa entre (raio - largura) e raio; parado entre as ondas, o jogador escapa.
    private async Task ExecuteShockwaves(BossMoveDefinition move, float maximumRadius, float windup, int pulses)
    {
        var center = _enemy.GlobalPosition;
        var bandWidth = G("boss.force_outbreak.band_width", 1.3f);
        for (var pulse = 0; pulse < pulses; pulse++)
        {
            if (!CanContinue()) return;
            var outer = maximumRadius * (pulse + 1.0f) / pulses;
            var inner = Mathf.Max(outer - bandWidth, 0.0f);
            var floor = pulses == 1 ? MinSingleCircleWarning : pulse == 0 ? MinSequenceFirstWarning : 0.0f;
            await StrikeRing(center, inner, outer, _enemy.AttackDamage / Mathf.Max(pulses * G("boss.radial.damage_factor", 0.72f), 1.0f), windup, move.DisplayName, floor);
            await WaitSeconds(G("boss.radial.pulse_gap", 0.12f));
        }
    }

    private async Task ExecuteMirrorImageDash(BossMoveDefinition move)
    {
        var target = _enemy.GetCombatTarget();
        if (target == null) return;
        var origin = _enemy.GlobalPosition;
        var direction = FlatDirection(origin, target.GlobalPosition);
        var side = new Vector3(-direction.Z, 0.0f, direction.X) * (_rng.Randf() < 0.5f ? -1.0f : 1.0f);
        var destination = ClampBossDestination(target.GlobalPosition + side * G("boss.mirror_dash.teleport_distance", 4.8f));
        var strikeWindup = G("boss.mirror_dash.windup", 0.38f);
        // A imagem espelhada fica no lugar de origem ate explodir.
        _enemy.PixelVisual?.SpawnAfterimage(new Color(BossRuneFx.Pale[2], 0.75f), TeleportWarningSeconds + Mathf.Max(strikeWindup * CastTimeMultiplier, MinSingleCircleWarning) + 0.1f);
        await WarnTeleport(destination);
        if (!CanContinue()) return;
        _enemy.TeleportBoss(destination);
        await AppearAfterTeleport();
        if (!CanContinue()) return;
        await StrikeAt(origin, G("boss.mirror_dash.radius", 1.8f), _enemy.AttackDamage * G("boss.mirror_dash.damage", 0.55f), strikeWindup, move.DisplayName, MinSingleCircleWarning);
        if (!CanContinue()) return;
        var start = _enemy.GlobalPosition;
        await ExecuteCharge(move, G("boss.mirror_dash.charge_distance", 7.0f), G("boss.mirror_dash.charge_windup", 0.38f));
        SpawnDashTrail(start, _enemy.GlobalPosition);
    }

    // Implode num vortice sob o jogador e depois se espalha em ondulacoes (aneis), em vez de discos cada vez maiores.
    private async Task ExecuteRippleSingularity(BossMoveDefinition move)
    {
        var target = _enemy.GetCombatTarget();
        if (target == null) return;
        var center = target.GlobalPosition;
        var coreRadius = G("boss.singularity.radius", 1.4f);
        var step = G("boss.singularity.radius_step", 0.8f);
        var bandWidth = G("boss.singularity.band_width", 1.0f);
        var pulses = CompendiumTuning.GetInt("boss.singularity.pulses_base", 3) + CurrentPhase;
        for (var pulse = 0; pulse < pulses; pulse++)
        {
            if (!CanContinue()) return;
            var damage = _enemy.AttackDamage * G("boss.singularity.damage", 0.42f);
            var windup = G("boss.singularity.windup", 0.34f);
            var floor = pulse == 0 ? MinSequenceFirstWarning : MinSingleCircleWarning;
            if (pulse == 0)
            {
                _nextCircleShape = BossRuneFx.Shape.Vortex;
                await StrikeAt(center, coreRadius, damage, windup, move.DisplayName, floor);
                _nextCircleShape = BossRuneFx.Shape.Runes;
                continue;
            }
            var outer = coreRadius + pulse * step + bandWidth * 0.5f;
            await StrikeRing(center, Mathf.Max(outer - bandWidth, 0.0f), outer, damage, windup, move.DisplayName, floor);
        }
    }

    private async Task StrikeRing(Vector3 center, float inner, float outer, float damage, float windup, string source, float minCast)
    {
        var castTime = Mathf.Max(windup * CastTimeMultiplier, minCast);
        var telegraph = TrackTelegraph(BossRuneFx.Circle(GetTree().CurrentScene, center, outer, inner, BossRuneFx.Shape.Ring, BossRuneFx.Violet, FloorY(center)).Charge(castTime));
        if (_circlesInMove++ > 0) PlayCue("boss_tick");
        var threat = AddThreat(center, outer);
        await WaitSeconds(castTime);
        RemoveThreat(threat);
        var canContinue = CanContinue();
        ReleaseTelegraph(telegraph, canContinue);
        if (!canContinue) return;
        PlayCue("boss_impact");
        var target = _enemy.GetCombatTarget();
        if (target == null) return;
        var delta = target.GlobalPosition - center;
        delta.Y = 0.0f;
        var distance = delta.Length();
        if (distance <= outer && distance >= inner)
            target.GetNodeOrNull<Health>("Health")?.ApplyDamage(damage, false, source);
    }

    // Teleporte da Guardia: some no lugar (fica invisivel) enquanto runas claras marcam onde ela vai reaparecer.
    private async Task WarnFadeTeleport(Vector3 destination)
    {
        PlayCue("boss_teleport");
        _enemy.FlashWindup();
        var scene = GetTree().CurrentScene;
        BossRuneFx.Flare(scene, _enemy.GlobalPosition, 1.2f, BossRuneFx.Pale, _enemy.GlobalPosition.Y, TeleportWarningSeconds);
        var arrival = TrackTelegraph(BossRuneFx.Circle(scene, destination, 1.0f, 0.0f, BossRuneFx.Shape.Runes, BossRuneFx.Pale, _enemy.GlobalPosition.Y).Charge(TeleportWarningSeconds));
        if (_enemy.PixelVisual is { } sprite)
            await TweenAlpha(sprite, 0.0f, TeleportWarningSeconds);
        else
            await WaitSeconds(TeleportWarningSeconds);
        ReleaseTelegraph(arrival, CanContinue());
    }

    private async Task AppearAfterTeleport()
    {
        if (_enemy.PixelVisual is { } sprite)
            await TweenAlpha(sprite, 1.0f, G("boss.invisible_woman.appear_seconds", 0.15f));
    }

    // Golpe cancelado (troca de fase) no meio do teleporte: ela nunca fica presa invisivel.
    private void RestoreVisibility()
    {
        if (IsInvisibleWoman && IsInstanceValid(_enemy) && !_dormant)
            _enemy.PixelVisual?.SetAlpha(1.0f);
    }

    private void SpawnDashTrail(Vector3 from, Vector3 to)
    {
        if (_enemy.PixelVisual is not { } sprite) return;
        const int copies = 5;
        for (var i = 0; i < copies; i++)
        {
            var point = from.Lerp(to, i / (float)copies);
            sprite.SpawnAfterimage(new Color(BossRuneFx.Violet[2], 0.6f - i * 0.08f), 0.35f + i * 0.05f, point - _enemy.GlobalPosition);
        }
    }
}
