using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Poça de sangue que escorre do corpo do inimigo morto (pixel art, tools/pixel_art/blood_pool/). Fica deitada no chão,
/// girada para a câmera e esticada em profundidade, de modo que cada pixel da folha vire um pixel da arte na tela.
/// Ao limpar a sala, o HUD a suga (Drain) e um filete de sangue escorre dela até o Virgil (HUD.BlessingOffer, grupo "blood_pools").
///
/// É também a poça das Bênçãos de Sangue (BloodBlessingRuntime): a poça simples do inimigo tem raio pequeno e fica até a sala
/// acabar; uma bênção a ATIVA (Activate) por um tempo, e ela cresce (mais manchas da mesma arte lado a lado), podendo curar o
/// Virgil ou fazer sangrar inimigos. Passado o tempo volta a ser simples. Em chão limpo a bênção cria uma poça sem dono
/// (SpawnActive) que seca sozinha no fim. O raio de jogo fica no meta "radius" e o sangramento em "causes_bleeding".
/// </summary>
public partial class BloodPool : Node3D
{
    public const string Group = "blood_pools";
    private const string SheetPath = "res://assets/pixel_art/effects/blood_pool/blood_pool_sheet.png";
    private const int Columns = 12;
    private const int Variants = 3;
    private const int SpreadFrames = 8;
    private const int ShimmerFrames = 4;
    private const float SpreadFps = 13.0f;
    private const float ShimmerFps = 3.0f;
    // Rente ao chão, acima do quadrilátero da sala 2D e da sombra do Virgil (0,03 m fica por cima, transparente).
    private const float GroundHeight = 0.022f;
    // Poças que nascem perto demais são empurradas para longe até esta distância entre centros (m).
    private const float MinSeparation = 0.5f;
    // Cada poça nova fica um degrau acima da anterior: a mais nova sempre cobre a de baixo, sem as duas brigarem
    // pelo mesmo plano (z-fighting). O ciclo volta ao chão a cada LayerCount poças.
    private const float LayerStep = 0.0025f;
    private const int LayerCount = 24;
    // Degrau entre as manchas da mesma poça (todas dentro de um degrau de LayerStep).
    private const float BlobStep = 0.0001f;
    private static int _nextLayer;

    // Raio visível de uma mancha da folha (m) e espaçamento dos anéis de manchas numa poça ativada grande.
    private const float BlobRadius = 0.72f;
    private const float RingSpacing = 0.9f;
    private const float RingArc = 1.1f;
    private const float ShrinkDuration = 0.35f;
    private const int MaxBlobs = 48;

    private static Texture2D _sheet;

    // Uma mancha da folha. Age < 0 = esperando para surgir; Dying = secando (quadros de trás para frente).
    private sealed class Blob
    {
        public Sprite3D Sprite;
        public int Variant;
        public float Age;
        public float ShimmerOffset;
        public bool Extra;
        public bool Dying;
        public bool Dead;
        public float DyingT;
        public float DyingDuration;
        public int DyingFrom;
    }

    private readonly List<Blob> _blobs = new();
    private float _phase;
    private float _stretch = 1.0f;
    private bool _draining;
    private bool _freeWhenDrained;
    private double _activeUntil;

    public float BaseRadius { get; private set; } = 0.9f;
    public float Radius { get; private set; } = 0.9f;
    public bool Persistent { get; private set; } = true;
    public bool CausesBleeding { get; private set; }
    public bool IsActive => _activeUntil > 0.0;
    public bool IsDrained => _blobs.Count > 0 && _blobs.TrueForAll(b => b.Dead) && _draining;

    private static float SimpleRadius => Mathf.Max(CompendiumTuning.GetFloat("enemy.blood_pool.radius", 0.9f), 0.3f);

    private static double Now => Time.GetTicksMsec() / 1000.0;

    // delay: espera o corpo cair antes de o sangue começar a escorrer.
    public static BloodPool Spawn(Node parent, Vector3 position, float delay)
    {
        return Create(parent, position, delay, true);
    }

    // Poça criada por uma bênção em chão limpo: já nasce ativa e seca sozinha quando o tempo acaba.
    public static BloodPool SpawnActive(Node parent, Vector3 position, float radius, double expiresAt, bool causesBleeding)
    {
        var pool = Create(parent, position, 0.0f, false);
        pool?.Activate(radius, expiresAt, causesBleeding);
        return pool;
    }

    // Poça (simples ou ativa) que contém o ponto, para uma bênção ativá-la em vez de criar outra por cima.
    // Usa o raio atual; a mais larga vence. mergeRadius = raio que a bênção pediria (poças ativas só se fundem entre
    // semelhantes, como antes: a nova não pode ser bem maior que a existente).
    public static BloodPool FindForActivation(SceneTree tree, Vector3 position, float mergeRadius)
    {
        BloodPool best = null;
        if (tree == null)
            return null;
        foreach (var node in tree.GetNodesInGroup(Group))
        {
            if (node is not BloodPool pool || pool.IsQueuedForDeletion() || pool._draining)
                continue;
            var distance = pool.GlobalPosition.DistanceTo(position);
            var fits = pool.IsActive
                ? mergeRadius <= pool.Radius * 1.15f && distance <= Mathf.Min(mergeRadius, pool.Radius) * 0.72f
                : distance <= pool.BaseRadius;
            if (fits && (best == null || pool.Radius > best.Radius))
                best = pool;
        }
        return best;
    }

    private static BloodPool Create(Node parent, Vector3 position, float delay, bool persistent)
    {
        if (parent == null || LoadSheet() == null)
            return null;
        var height = GroundHeight + (_nextLayer++ % LayerCount) * LayerStep;
        var planar = SeparateFromOtherPools(parent.GetTree(), new Vector3(position.X, 0.0f, position.Z));
        var pool = new BloodPool
        {
            Name = "BloodPool",
            Position = new Vector3(planar.X, height, planar.Z),
            Persistent = persistent,
            BaseRadius = SimpleRadius,
        };
        pool.Radius = pool.BaseRadius;
        pool._startDelay = delay;
        parent.AddChild(pool);
        return pool;
    }

    private float _startDelay;

    public override void _Ready()
    {
        AddToGroup(Group);
        _phase = GD.Randf() * Mathf.Tau;
        var camera = GetViewport()?.GetCamera3D();
        var yaw = camera?.GlobalRotation.Y ?? Mathf.DegToRad(IsoCamera.DefaultYawDegrees);
        var pitch = camera != null ? -camera.GlobalRotation.X : Mathf.DegToRad(PixelRoom.CameraPitchDegrees);
        // A folha já está na projeção 2:1 da tela: estica no chão o que a inclinação da câmera achata.
        _stretch = 1.0f / Mathf.Max(Mathf.Sin(Mathf.Clamp(pitch, 0.2f, Mathf.Pi / 2.0f)), 0.2f);
        Rotation = new Vector3(0.0f, yaw, 0.0f);
        Scale = new Vector3(1.0f, 1.0f, _stretch);
        AddBlob(Vector2.Zero, _startDelay, false);
        SyncMeta();
    }

    // offset em metros no chão (x = direita da tela, y = profundidade).
    private void AddBlob(Vector2 offset, float delay, bool extra)
    {
        var variant = (int)(GD.Randi() % Variants);
        var sprite = new Sprite3D
        {
            Name = "Sprite",
            Texture = _sheet,
            Hframes = Columns,
            Vframes = Variants,
            Frame = variant * Columns,
            FlipH = GD.Randf() < 0.5f,
            PixelSize = PlayerSpriteVisual.PixelSize,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            // No 2D a luz já está na arte; no 3D a poça recebe a luz da sala.
            Shaded = !GameMode.IsPixelArt,
            DoubleSided = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
            Position = new Vector3(offset.X, _blobs.Count * BlobStep, offset.Y / _stretch),
            Visible = false,
        };
        AddChild(sprite);
        _blobs.Add(new Blob
        {
            Sprite = sprite,
            Variant = variant,
            Age = -delay,
            ShimmerOffset = (float)GD.RandRange(0.0, ShimmerFrames / ShimmerFps),
            Extra = extra,
        });
    }

    // Posições (no chão) das manchas para um raio: centro + anéis até caber no raio. Os anéis só se acrescentam
    // quando o raio cresce, então as manchas já existentes continuam no mesmo lugar.
    private List<Vector2> Layout(float radius)
    {
        var list = new List<Vector2> { Vector2.Zero };
        for (var ring = 1; ; ring++)
        {
            var distance = ring * RingSpacing;
            if (distance > radius - BlobRadius * 0.4f)
                break;
            var count = Mathf.Max(Mathf.RoundToInt(Mathf.Tau * distance / RingArc), 4);
            if (list.Count + count > MaxBlobs)
                break;
            for (var i = 0; i < count; i++)
            {
                var angle = _phase + ring * 0.9f + Mathf.Tau * i / count;
                list.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance);
            }
        }
        return list;
    }

    // Ativa a poça até expiresAt (segundos do relógio do jogo): cresce até radius (nunca encolhe enquanto ativa).
    public void Activate(float radius, double expiresAt, bool causesBleeding)
    {
        if (_draining)
            return;
        radius = Mathf.Max(radius, Radius);
        var layout = Layout(radius);
        for (var i = _blobs.Count; i < layout.Count; i++)
            AddBlob(layout[i], 0.04f * (i % 6) + (float)GD.RandRange(0.0, 0.06), true);
        Radius = radius;
        _activeUntil = Math.Max(_activeUntil, expiresAt);
        CausesBleeding |= causesBleeding;
        SyncMeta();
    }

    private void SyncMeta()
    {
        SetMeta("radius", Radius);
        SetMeta("causes_bleeding", CausesBleeding);
    }

    // Fim da ativação: a poça simples volta ao tamanho de inimigo; a criada pela bênção seca de vez.
    private void EndActivation()
    {
        _activeUntil = 0.0;
        CausesBleeding = false;
        if (!Persistent)
        {
            _freeWhenDrained = true;
            Drain(0.4f);
            return;
        }
        Radius = BaseRadius;
        foreach (var blob in _blobs)
            if (blob.Extra && !blob.Dying)
                StartDying(blob, ShrinkDuration);
        SyncMeta();
    }

    public override void _Process(double delta)
    {
        var step = (float)delta;
        var allDead = true;
        foreach (var blob in _blobs)
        {
            if (blob.Dead)
                continue;
            allDead = false;
            if (blob.Dying)
            {
                blob.DyingT += step;
                var progress = Mathf.Clamp(blob.DyingT / blob.DyingDuration, 0.0f, 1.0f);
                var frame = Mathf.RoundToInt(Mathf.Lerp(blob.DyingFrom, -1.0f, progress));
                if (frame < 0)
                {
                    blob.Dead = true;
                    blob.Sprite.Visible = false;
                    continue;
                }
                SetFrame(blob, frame);
                continue;
            }
            blob.Age += step;
            if (blob.Age < 0.0f)
                continue;
            blob.Sprite.Visible = true;
            if (blob.Age < SpreadFrames / SpreadFps)
                SetFrame(blob, (int)(blob.Age * SpreadFps));
            else
                SetFrame(blob, SpreadFrames + (int)((blob.Age + blob.ShimmerOffset) * ShimmerFps) % ShimmerFrames);
        }
        if (_draining)
        {
            if (allDead && _freeWhenDrained)
                QueueFree();
            return;
        }
        // Manchas extras que acabaram de secar saem da lista (e do nó).
        for (var i = _blobs.Count - 1; i > 0; i--)
            if (_blobs[i].Dead && _blobs[i].Extra)
            {
                _blobs[i].Sprite.QueueFree();
                _blobs.RemoveAt(i);
            }
        if (_activeUntil > 0.0 && Now >= _activeUntil)
            EndActivation();
    }

    // Empurra o centro para longe das poças já no chão, para duas não ocuparem o mesmo lugar.
    private static Vector3 SeparateFromOtherPools(SceneTree tree, Vector3 position)
    {
        if (tree == null)
            return position;
        for (var pass = 0; pass < 2; pass++)
        {
            foreach (var node in tree.GetNodesInGroup(Group))
            {
                if (node is not BloodPool other || other.IsQueuedForDeletion())
                    continue;
                var offset = position - new Vector3(other.GlobalPosition.X, 0.0f, other.GlobalPosition.Z);
                var distance = offset.Length();
                if (distance >= MinSeparation)
                    continue;
                var direction = distance > 0.01f
                    ? offset / distance
                    : Vector3.Right.Rotated(Vector3.Up, (float)GD.RandRange(0.0, Mathf.Tau));
                position += direction * (MinSeparation - distance);
            }
        }
        return position;
    }

    // Seca a poça de trás para frente (quadros do espalhar ao contrário). Roda mesmo com a árvore pausada.
    public void Drain(float duration)
    {
        if (_draining)
            return;
        _draining = true;
        _activeUntil = 0.0;
        ProcessMode = ProcessModeEnum.Always;
        foreach (var blob in _blobs)
            if (!blob.Dying && !blob.Dead)
                StartDying(blob, Mathf.Max(duration, 0.05f));
    }

    private void StartDying(Blob blob, float duration)
    {
        if (blob.Age < 0.0f)
        {
            blob.Dead = true;
            return;
        }
        blob.DyingFrom = blob.Age < SpreadFrames / SpreadFps
            ? Mathf.Min((int)(blob.Age * SpreadFps), SpreadFrames - 1)
            : SpreadFrames - 1;
        blob.DyingDuration = duration;
        blob.DyingT = 0.0f;
        blob.Dying = true;
    }

    private static void SetFrame(Blob blob, int column)
    {
        blob.Sprite.Frame = blob.Variant * Columns + Mathf.Clamp(column, 0, Columns - 1);
    }

    private static Texture2D LoadSheet()
    {
        _sheet ??= LoadTexture(SheetPath);
        return _sheet;
    }

    private static Texture2D LoadTexture(string path)
    {
        var image = PixelRoomComposer.LoadImage(path);
        return image == null ? null : ImageTexture.CreateFromImage(image);
    }
}
