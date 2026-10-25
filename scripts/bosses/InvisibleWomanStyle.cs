using Godot;
using System.Collections.Generic;

// Apresentacao dos golpes da Guardia Invisivel (boss 1): efeitos, animacoes e a invisibilidade. Nao muda dano,
// alcance nem tempo de aviso: o BossController continua dono da mecanica e chama os ganchos On*.
// - Saraivada Psionica: um orbe sai da mao dela e cai no circulo no fim do aviso; estoura no impacto.
// - Salto Cinetico: escudo visivel enquanto houver cargas; ela some na saida e reaparece no destino.
// - Surto de Forca: ela canaliza (Cast) e cada pulso solta uma onda de choque do tamanho do circulo.
// - Investida Espelhada: deixa uma imagem espelhada no ponto de saida, que explode; a investida deixa rastro.
// - Singularidade Rubra: um vortice cresce no alvo a cada pulso e colapsa no fim.
// Da fase 2 em diante ela fica translucida entre golpes (volta a ficar nitida ao preparar o proximo).
public partial class InvisibleWomanStyle : Node
{
    private static readonly Color Violet = new(0.72f, 0.66f, 1.0f);
    private static readonly Color Lavender = new(0.85f, 0.8f, 1.0f, 0.6f);
    private static readonly Color Crimson = new(1.0f, 0.55f, 0.55f);

    private static float G(string key, float defaultValue) => CompendiumTuning.GetFloat(key, defaultValue);

    private EnemyBasic _enemy;
    private Health _health;
    private Sprite3D _shield;
    private Sprite3D _vortex;
    private Tween _vortexLoop;
    private Sprite3D _mirror;
    private readonly List<Node3D> _transient = new();
    private int _phase = 1;
    private bool _awake;

    public void Configure(EnemyBasic enemy)
    {
        _enemy = enemy;
        _health = enemy.GetNode<Health>("Health");
    }

    private Node Scene => GetTree()?.CurrentScene;
    private Vector3 HandPosition => _enemy.GlobalPosition + Vector3.Up * 1.35f;
    private float FloorY(Vector3 point) => Mathf.Max(point.Y, _enemy.GlobalPosition.Y) + 0.09f;

    public override void _Process(double delta)
    {
        if (_enemy == null || !IsInstanceValid(_enemy) || _health.IsDead)
        {
            ClearPersistent();
            return;
        }
        UpdateShield();
    }

    public override void _ExitTree() => ClearPersistent();

    // ---- Ganchos do BossController ----

    // Animacao do golpe. true = tratou (o controller nao toca a animacao generica de ataque).
    public bool OnMoveStart(string moveId)
    {
        // Prepara o golpe nitida: o aviso precisa ser lido.
        _enemy.SetBossVisibility(1.0f, 0.12f);
        var animation = moveId switch
        {
            "psionic_volley" => "Attack",
            "force_outbreak" or "singularity" or "kinetic_shift" => "Cast",
            _ => "Cast"
        };
        return _enemy.PlayBossAnimation(animation, 1.0f + (_phase - 1) * 0.12f);
    }

    public void OnMoveEnd(string moveId)
    {
        if (moveId == "singularity") CollapseVortex(true);
        FreeMirror(false);
        // Fase 2+: some entre um golpe e outro.
        if (_phase >= 2 && !_health.IsDead)
            _enemy.SetBossVisibility(G("boss.invisible_woman.fade_alpha", 0.28f), 0.45f);
    }

    // Circulo de aviso criado: center/radius do golpe, castTime ate o impacto.
    public void OnStrikeTelegraph(string moveId, Vector3 center, float radius, float castTime, int circleIndex)
    {
        switch (moveId)
        {
            case "psionic_volley":
                LaunchOrb(center, castTime);
                break;
            case "singularity":
                GrowVortex(center, radius, castTime, circleIndex == 0);
                break;
            case "force_outbreak":
                if (circleIndex == 0) _enemy.PlayBossAnimation("Cast", 1.0f);
                break;
        }
    }

    public void OnStrikeImpact(string moveId, Vector3 center, float radius)
    {
        var floor = new Vector3(center.X, FloorY(center), center.Z);
        switch (moveId)
        {
            case "psionic_volley":
                BossVfx.BurstAt(Scene, BossVfx.Burst, center + Vector3.Up * 0.5f, Mathf.Max(radius * 0.7f, 0.6f), 0.4f, false, Colors.White);
                PlayCue("iw_impact");
                break;
            case "force_outbreak":
            case "kinetic_shift":
                BossVfx.BurstAt(Scene, BossVfx.Ring, floor, radius, 0.35f, true, Violet);
                PlayCue("iw_impact");
                break;
            case "mirror_dash":
                // A imagem espelhada e que explode.
                FreeMirror(true);
                BossVfx.BurstAt(Scene, BossVfx.Burst, center + Vector3.Up * 0.7f, Mathf.Max(radius, 0.8f), 0.45f, false, Colors.White);
                BossVfx.BurstAt(Scene, BossVfx.Ring, floor, radius, 0.3f, true, Violet);
                PlayCue("iw_impact");
                break;
            case "singularity":
                BossVfx.BurstAt(Scene, BossVfx.Ring, floor, radius, 0.3f, true, Crimson);
                PlayCue("iw_impact");
                break;
        }
    }

    // Antes de reaparecer em outro ponto: some aqui (e, na Investida Espelhada, deixa a imagem espelhada).
    public void OnTeleportWarning(string moveId, Vector3 destination, float seconds)
    {
        if (moveId == "mirror_dash") SpawnMirror();
        _enemy.SetBossVisibility(0.0f, seconds * 0.8f);
        PlayCue("iw_vanish");
    }

    public void OnTeleported()
    {
        _enemy.SetBossVisibility(1.0f, 0.18f);
        BossVfx.BurstAt(Scene, BossVfx.Burst, _enemy.GlobalPosition + Vector3.Up * 0.8f, 0.9f, 0.35f, false, Lavender);
    }

    // Investida: rastro de imagens da Guardia ao longo do caminho.
    public void OnCharge(Vector3 start, Vector3 end)
    {
        var steps = 5;
        for (var i = 0; i < steps; i++)
        {
            var point = start.Lerp(end, (i + 0.5f) / steps);
            var ghost = SpawnAfterimage(new Vector3(point.X, _enemy.GlobalPosition.Y, point.Z), 0.35f + i * 0.05f);
            if (ghost != null) _transient.Add(ghost);
        }
    }

    public void OnPhaseChanged(int phase, float seconds)
    {
        _phase = phase;
        // Na troca de fase ela some por um instante e volta com uma onda.
        _enemy.SetBossVisibility(0.0f, seconds * 0.4f);
        var tween = CreateTween();
        tween.TweenInterval(seconds * 0.6f);
        tween.TweenCallback(Callable.From(() =>
        {
            if (!IsInstanceValid(_enemy) || _health.IsDead) return;
            _enemy.SetBossVisibility(1.0f, seconds * 0.4f);
            BossVfx.BurstAt(Scene, BossVfx.Ring, new Vector3(_enemy.GlobalPosition.X, FloorY(_enemy.GlobalPosition), _enemy.GlobalPosition.Z), 3.5f, 0.5f, true, Violet);
        }));
        PlayCue("iw_vanish");
    }

    // Golpe cancelado (troca de fase, morte): some com o que estava no ar.
    public void OnCancel()
    {
        CollapseVortex(false);
        FreeMirror(false);
        foreach (var node in _transient)
            if (IsInstanceValid(node)) node.QueueFree();
        _transient.Clear();
    }

    // Esperando na sala: quase invisivel, so um vulto tremeluzindo.
    public void OnDormant() => _enemy.SetBossVisibility(G("boss.invisible_woman.dormant_alpha", 0.2f), 0.01f);

    // O jogador chegou perto: ela se mostra antes de falar.
    public void OnReveal()
    {
        _enemy.FaceCombatTarget();
        _enemy.SetBossVisibility(1.0f, 0.6f);
        BossVfx.BurstAt(Scene, BossVfx.Burst, _enemy.GlobalPosition + Vector3.Up * 0.9f, 1.1f, 0.5f, false, Lavender);
        PlayCue("iw_vanish");
    }

    // Comeco da luta (fim da conversa).
    public void OnAwaken()
    {
        _awake = true;
        _enemy.SetBossVisibility(1.0f, 0.2f);
        _enemy.PlayBossAnimation("Cast", 1.0f);
        BossVfx.BurstAt(Scene, BossVfx.Ring, new Vector3(_enemy.GlobalPosition.X, FloorY(_enemy.GlobalPosition), _enemy.GlobalPosition.Z), 4.0f, 0.6f, true, Violet);
        PlayCue("iw_vanish");
    }

    // ---- Efeitos ----

    private void LaunchOrb(Vector3 center, float castTime)
    {
        var orb = BossVfx.Create(Scene, BossVfx.Orb, HandPosition, false, 1.6f);
        if (orb == null) return;
        _transient.Add(orb);
        BossVfx.Loop(orb, 0.4f);
        var start = HandPosition;
        var end = new Vector3(center.X, FloorY(center) + 0.3f, center.Z);
        // Sai da mao e desce em arco, chegando junto com o fim do aviso.
        var flight = Mathf.Max(castTime * 0.9f, 0.15f);
        var arc = G("boss.invisible_woman.orb_arc", 2.2f);
        var tween = orb.CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            if (!IsInstanceValid(orb)) return;
            orb.GlobalPosition = start.Lerp(end, t) + Vector3.Up * Mathf.Sin(t * Mathf.Pi) * arc;
        }), 0.0f, 1.0f, flight);
        tween.TweenCallback(Callable.From(() =>
        {
            _transient.Remove(orb);
            orb.QueueFree();
        }));
        PlayCue("iw_cast");
    }

    private void GrowVortex(Vector3 center, float radius, float castTime, bool first)
    {
        var floor = new Vector3(center.X, FloorY(center), center.Z);
        if (first || _vortex == null || !IsInstanceValid(_vortex))
        {
            CollapseVortex(false);
            _vortex = BossVfx.Create(Scene, BossVfx.Vortex, floor, true, 0.2f);
            if (_vortex == null) return;
            // Quadros 2-6 da folha = o vortice girando; os ultimos = o colapso.
            _vortexLoop = BossVfx.Loop(_vortex, 0.45f, 2, 6);
            PlayCue("iw_cast");
        }
        var scale = radius * 2.0f / (BossVfx.WorldSize(BossVfx.Vortex) * 0.9f);
        _vortex.CreateTween().TweenProperty(_vortex, "scale", Vector3.One * scale, Mathf.Max(castTime, 0.1f))
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    private void CollapseVortex(bool animate)
    {
        if (_vortex == null || !IsInstanceValid(_vortex))
        {
            _vortex = null;
            return;
        }
        var vortex = _vortex;
        _vortex = null;
        if (!animate)
        {
            vortex.QueueFree();
            return;
        }
        // Para o giro e toca o colapso (ultimos quadros), encolhendo.
        _vortexLoop?.Kill();
        _vortexLoop = null;
        var last = vortex.Hframes - 1;
        BossVfx.PlayOnce(vortex, 0.35f, Mathf.Max(last - 3, 0), last);
        vortex.CreateTween().TweenProperty(vortex, "scale", vortex.Scale * 0.4f, 0.35f);
    }

    private void UpdateShield()
    {
        // Esperando na sala ela esta invisivel: o escudo nao a denuncia.
        var active = _awake && _health.ShieldHits > 0;
        if (active && (_shield == null || !IsInstanceValid(_shield)))
        {
            _shield = BossVfx.Create(_enemy, BossVfx.Shield, _enemy.GlobalPosition + Vector3.Up * 0.75f, false, 1.15f);
            if (_shield != null)
            {
                _shield.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.85f);
                BossVfx.Loop(_shield, 0.6f);
            }
        }
        else if (!active && _shield != null)
        {
            if (IsInstanceValid(_shield))
            {
                BossVfx.BurstAt(Scene, BossVfx.Burst, _shield.GlobalPosition, 0.9f, 0.3f, false, Lavender);
                _shield.QueueFree();
            }
            _shield = null;
        }
    }

    private void SpawnMirror()
    {
        FreeMirror(false);
        _mirror = SpawnAfterimage(_enemy.GlobalPosition, -1.0f);
    }

    private void FreeMirror(bool shatter)
    {
        if (_mirror == null) return;
        if (IsInstanceValid(_mirror))
        {
            if (shatter) BossVfx.BurstAt(Scene, BossVfx.Burst, _mirror.GlobalPosition + Vector3.Up * 0.6f, 1.0f, 0.35f, false, Lavender);
            _mirror.QueueFree();
        }
        _mirror = null;
    }

    // Copia parada do quadro atual da Guardia, lavanda e translucida. lifetime < 0 = fica ate ser liberada.
    private Sprite3D SpawnAfterimage(Vector3 position, float lifetime)
    {
        var source = _enemy.FindChild("Sprite", true, false) as Sprite3D;
        if (source == null || Scene == null) return null;
        var ghost = new Sprite3D
        {
            Name = "GuardianAfterimage",
            Texture = source.Texture,
            Hframes = source.Hframes,
            Frame = source.Frame,
            FlipH = source.FlipH,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Shaded = false,
            DoubleSided = true,
            PixelSize = source.PixelSize,
            Offset = source.Offset,
            Modulate = new Color(0.75f, 0.7f, 1.0f, 0.6f),
            Scale = source.GlobalTransform.Basis.Scale,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        Scene.AddChild(ghost);
        ghost.GlobalPosition = position;
        if (lifetime > 0.0f)
        {
            var tween = ghost.CreateTween();
            tween.TweenProperty(ghost, "modulate:a", 0.0f, lifetime);
            tween.TweenCallback(Callable.From(() =>
            {
                _transient.Remove(ghost);
                ghost.QueueFree();
            }));
        }
        return ghost;
    }

    private void ClearPersistent()
    {
        OnCancel();
        if (_shield != null && IsInstanceValid(_shield)) _shield.QueueFree();
        _shield = null;
    }

    private void PlayCue(string effect) => GetTree()?.CallGroup("audio_manager", "PlayEffect", effect);
}
