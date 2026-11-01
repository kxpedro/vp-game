using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Poça de sangue que escorre do corpo do inimigo morto (pixel art, tools/pixel_art/blood_pool/). Fica deitada no chão,
/// girada para a câmera e esticada em profundidade, de modo que cada pixel da folha vire um pixel da arte na tela.
/// Ao limpar a sala, o HUD a suga (Drain) e um filete de sangue escorre dela até o Virgil (HUD.BlessingOffer, grupo "blood_pools").
///
/// Uma poça é um conjunto de manchas da folha. Inimigos que morrem perto uns dos outros acrescentam manchas à MESMA poça,
/// que vai virando uma poça grande e contínua: as manchas são desenhadas em duas camadas (todos os contornos embaixo, todos
/// os preenchimentos em cima), então só a borda externa do conjunto tem contorno.
///
/// É também a poça das Bênçãos de Sangue (BloodBlessingRuntime): a bênção ATIVA (Activate) a poça que contém o ponto,
/// acrescentando manchas ao redor até o raio da bênção por um tempo (cura, sangramento etc. valem em qualquer mancha,
/// ver Contains); depois essas manchas secam e a poça volta ao que os inimigos deixaram. Em chão limpo a bênção cria uma
/// poça sem dono (SpawnActive) que seca inteira no fim.
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
    private const float GroundHeight = 0.022f;
    // Cada poça nova fica um degrau acima da anterior (sem z-fighting entre poças); o ciclo volta ao chão a cada LayerCount.
    private const float LayerStep = 0.0035f;
    private const int LayerCount = 8;
    // Dentro da poça: contornos no chão da poça, preenchimentos um pouco acima (cada mancha um degrau).
    private const float FillBase = 0.001f;
    private const float FillStep = 0.00003f;
    private const int MaxBlobs = 48;
    // Nova morte a até esta distância de uma mancha entra na mesma poça (m).
    private const float JoinDistance = 1.1f;
    // Duas manchas de inimigos não ficam mais perto que isso (empurra a nova).
    private const float MinSeparation = 0.45f;
    private const float RingSpacing = 0.9f;
    private const float RingArc = 1.1f;
    private const float ExtraReach = 0.65f;
    private const float ShrinkDuration = 0.35f;
    private static readonly Color Rim = new(0x3b / 255.0f, 0x17 / 255.0f, 0x25 / 255.0f);
    private static readonly Color Body = new(0x73 / 255.0f, 0x17 / 255.0f, 0x2d / 255.0f);

    // Bolhas: enquanto a poça está ativada por uma bênção ela borbulha (4 quadros de 8x8: surge, cresce, brilha, estoura).
    private const int BubbleFrames = 4;
    private const int BubbleSize = 8;
    private const float BubbleFps = 9.0f;
    private const float BubbleInterval = 0.09f;
    private const float BubbleHeight = 0.003f;
    private const int MaxBubbles = 40;
    private static readonly Color BubbleEdge = new(0xb4 / 255.0f, 0x20 / 255.0f, 0x2a / 255.0f);
    private static readonly Color BubbleSpec = new(0xe8 / 255.0f, 0x6a / 255.0f, 0x73 / 255.0f);
    private static readonly Color BubbleGlint = new(0xf5 / 255.0f, 0xa0 / 255.0f, 0x97 / 255.0f);
    private static Texture2D _bubbleSheet;

    private sealed class Bubble
    {
        public Sprite3D Sprite;
        public float Age;
    }

    private readonly List<Bubble> _bubbles = new();
    private float _bubbleTimer;

    private static Texture2D _fillSheet;
    private static Texture2D _outlineSheet;
    private static int _nextLayer;

    // Uma mancha: duas camadas da mesma célula da folha. Age < 0 = esperando; Dying = secando (quadros de trás para frente).
    private sealed class Blob
    {
        public Sprite3D Outline;
        public Sprite3D Fill;
        public int Variant;
        public float Age;
        public float ShimmerOffset;
        public float Reach;
        public bool Extra;
        public bool Dying;
        public bool Dead;
        public float DyingT;
        public float DyingDuration;
        public int DyingFrom;
    }

    private readonly List<Blob> _blobs = new();
    private float _stretch = 1.0f;
    private bool _draining;
    private bool _freeWhenDrained;
    private double _activeUntil;
    private float _firstDelay;
    private bool _firstExtra;

    public bool Persistent { get; private set; } = true;
    public bool CausesBleeding { get; private set; }
    public bool IsActive => _activeUntil > 0.0;
    public bool IsDraining => _draining;
    public bool IsDrained => _draining && _blobs.TrueForAll(b => b.Dead);

    private static float SimpleRadius => Mathf.Max(CompendiumTuning.GetFloat("enemy.blood_pool.radius", 0.9f), 0.3f);
    private static double Now => Time.GetTicksMsec() / 1000.0;

    // Poça do inimigo morto: entra na poça simples mais próxima (se houver uma a até JoinDistance) ou cria outra.
    // delay: espera o corpo cair antes de o sangue começar a escorrer.
    public static BloodPool Spawn(Node parent, Vector3 position, float delay)
    {
        if (parent == null || LoadSheets() == false)
            return null;
        var flat = new Vector3(position.X, 0.0f, position.Z);
        foreach (var node in parent.GetTree().GetNodesInGroup(Group))
        {
            if (node is not BloodPool pool || !pool.Persistent || pool._draining || pool.IsQueuedForDeletion())
                continue;
            var nearest = pool.NearestBlobDistance(flat, out var nearestPoint);
            if (nearest > JoinDistance)
                continue;
            if (nearest < MinSeparation)
            {
                var away = flat - nearestPoint;
                away = away.Length() > 0.01f ? away.Normalized() : Vector3.Right.Rotated(Vector3.Up, (float)GD.RandRange(0.0, Mathf.Tau));
                flat = nearestPoint + away * MinSeparation;
            }
            pool.AddBlob(flat, delay, false);
            return pool;
        }
        return Create(parent, flat, delay, true);
    }

    // Poça criada por uma bênção em chão limpo: já nasce ativa e seca inteira quando o tempo acaba.
    public static BloodPool SpawnActive(Node parent, Vector3 position, float radius, double expiresAt, bool causesBleeding)
    {
        if (parent == null || LoadSheets() == false)
            return null;
        var pool = Create(parent, new Vector3(position.X, 0.0f, position.Z), 0.0f, false);
        pool?.Activate(position, radius, expiresAt, causesBleeding);
        return pool;
    }

    // Poça (não secando) que contém o ponto, para a bênção ativá-la em vez de criar outra por cima.
    public static BloodPool FindContaining(SceneTree tree, Vector3 position)
    {
        if (tree == null)
            return null;
        foreach (var node in tree.GetNodesInGroup(Group))
            if (node is BloodPool pool && !pool._draining && !pool.IsQueuedForDeletion() && pool.Contains(position))
                return pool;
        return null;
    }

    // Alguma poça da sala está sob o ponto? (cura, "kill na poça" etc.)
    public static bool IsOnAnyPool(SceneTree tree, Vector3 position)
    {
        if (tree == null)
            return false;
        foreach (var node in tree.GetNodesInGroup(Group))
            if (node is BloodPool pool && !pool._draining && pool.Contains(position))
                return true;
        return false;
    }

    private static BloodPool Create(Node parent, Vector3 flat, float delay, bool persistent)
    {
        var height = GroundHeight + (_nextLayer++ % LayerCount) * LayerStep;
        var pool = new BloodPool
        {
            Name = "BloodPool",
            Position = new Vector3(flat.X, height, flat.Z),
            Persistent = persistent,
            _firstDelay = delay,
            _firstExtra = !persistent,
        };
        parent.AddChild(pool);
        return pool;
    }

    public override void _Ready()
    {
        AddToGroup(Group);
        var camera = GetViewport()?.GetCamera3D();
        var yaw = camera?.GlobalRotation.Y ?? Mathf.DegToRad(IsoCamera.DefaultYawDegrees);
        var pitch = camera != null ? -camera.GlobalRotation.X : Mathf.DegToRad(PixelRoom.CameraPitchDegrees);
        // A folha já está na projeção 2:1 da tela: estica no chão o que a inclinação da câmera achata.
        _stretch = 1.0f / Mathf.Max(Mathf.Sin(Mathf.Clamp(pitch, 0.2f, Mathf.Pi / 2.0f)), 0.2f);
        Rotation = new Vector3(0.0f, yaw, 0.0f);
        Scale = new Vector3(1.0f, 1.0f, _stretch);
        AddBlob(GlobalPosition, _firstDelay, _firstExtra);
        SyncMeta();
    }

    // O ponto (plano do chão, coordenadas do mundo) está sobre alguma mancha?
    public bool Contains(Vector3 point)
    {
        return NearestBlobDistance(point, out _, true) <= 0.0f;
    }

    // Distância do ponto à mancha mais próxima, descontando o alcance da mancha quando insideTest (então <= 0 = dentro).
    private float NearestBlobDistance(Vector3 point, out Vector3 nearestPoint, bool insideTest = false)
    {
        var best = float.MaxValue;
        nearestPoint = GlobalPosition;
        foreach (var blob in _blobs)
        {
            // Mancha ainda esperando o corpo cair (Age < 0) já conta para fundir poças (mortes quase simultâneas), só não para "estar em cima".
            if (blob.Dead || blob.Dying || (insideTest && blob.Age < 0.0f))
                continue;
            var center = blob.Fill.GlobalPosition;
            var distance = new Vector2(center.X - point.X, center.Z - point.Z).Length() - (insideTest ? blob.Reach : 0.0f);
            if (distance < best)
            {
                best = distance;
                nearestPoint = new Vector3(center.X, 0.0f, center.Z);
            }
        }
        return best;
    }

    private void AddBlob(Vector3 worldPoint, float delay, bool extra)
    {
        if (_blobs.Count >= MaxBlobs)
            return;
        // ToLocal já desfaz a rotação e o esticamento: o resultado é a posição da mancha na folha.
        var local = ToLocal(new Vector3(worldPoint.X, GlobalPosition.Y, worldPoint.Z));
        var variant = (int)(GD.Randi() % Variants);
        var flip = GD.Randf() < 0.5f;
        var index = _blobs.Count;
        var outline = MakeSprite(_outlineSheet, variant, flip, new Vector3(local.X, 0.0f, local.Z));
        var fill = MakeSprite(_fillSheet, variant, flip, new Vector3(local.X, FillBase + index * FillStep, local.Z));
        AddChild(outline);
        AddChild(fill);
        _blobs.Add(new Blob
        {
            Outline = outline,
            Fill = fill,
            Variant = variant,
            Age = -delay,
            ShimmerOffset = (float)GD.RandRange(0.0, ShimmerFrames / ShimmerFps),
            Reach = extra ? ExtraReach : SimpleRadius,
            Extra = extra,
        });
    }

    private static Sprite3D MakeSprite(Texture2D texture, int variant, bool flip, Vector3 position)
    {
        return new Sprite3D
        {
            Name = "Sprite",
            Texture = texture,
            Hframes = Columns,
            Vframes = Variants,
            Frame = variant * Columns,
            FlipH = flip,
            PixelSize = PlayerSpriteVisual.PixelSize,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            // No 2D a luz já está na arte; no 3D a poça recebe a luz da sala.
            Shaded = !GameMode.IsPixelArt,
            DoubleSided = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
            Position = position,
            Visible = false,
        };
    }

    // Ativa a poça que contém o ponto até expiresAt (segundos do relógio do jogo): acrescenta manchas ao redor do ponto
    // até o raio pedido (onde ainda não há mancha) e estende o tempo.
    public void Activate(Vector3 center, float radius, double expiresAt, bool causesBleeding)
    {
        if (_draining)
            return;
        var flat = new Vector3(center.X, 0.0f, center.Z);
        var spots = new List<Vector3> { flat };
        for (var ring = 1; ; ring++)
        {
            var distance = ring * RingSpacing;
            if (distance > radius - 0.3f)
                break;
            var count = Mathf.Max(Mathf.RoundToInt(Mathf.Tau * distance / RingArc), 4);
            var phase = ring * 0.9f + (float)(center.X * 1.7 + center.Z * 2.3);
            for (var i = 0; i < count; i++)
            {
                var angle = phase + Mathf.Tau * i / count;
                spots.Add(flat + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * distance);
            }
        }
        var n = 0;
        foreach (var spot in spots)
        {
            if (NearestBlobDistance(spot, out _) < 0.5f)
                continue;
            AddBlob(spot, 0.04f * (n++ % 6) + (float)GD.RandRange(0.0, 0.06), true);
        }
        _activeUntil = Math.Max(_activeUntil, expiresAt);
        CausesBleeding |= causesBleeding;
        SyncMeta();
    }

    private void SyncMeta()
    {
        SetMeta("causes_bleeding", CausesBleeding);
    }

    // Fim da ativação: as manchas extras secam e a poça volta ao que os inimigos deixaram; a poça sem dono seca de vez.
    private void EndActivation()
    {
        _activeUntil = 0.0;
        CausesBleeding = false;
        SyncMeta();
        if (!Persistent)
        {
            _freeWhenDrained = true;
            Drain(0.4f);
            return;
        }
        foreach (var blob in _blobs)
            if (blob.Extra && !blob.Dying)
                StartDying(blob, ShrinkDuration);
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
                    Kill(blob);
                    continue;
                }
                SetFrame(blob, frame);
                continue;
            }
            blob.Age += step;
            if (blob.Age < 0.0f)
                continue;
            blob.Outline.Visible = true;
            blob.Fill.Visible = true;
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
        for (var i = _blobs.Count - 1; i >= 0; i--)
            if (_blobs[i].Dead && _blobs[i].Extra && _blobs.Count > 1)
            {
                _blobs[i].Outline.QueueFree();
                _blobs[i].Fill.QueueFree();
                _blobs.RemoveAt(i);
            }
        if (_activeUntil > 0.0 && Now >= _activeUntil)
            EndActivation();
        UpdateBubbles(step);
    }

    private void UpdateBubbles(float step)
    {
        for (var i = _bubbles.Count - 1; i >= 0; i--)
        {
            var bubble = _bubbles[i];
            bubble.Age += step;
            var frame = (int)(bubble.Age * BubbleFps);
            if (frame >= BubbleFrames)
            {
                bubble.Sprite.QueueFree();
                _bubbles.RemoveAt(i);
                continue;
            }
            bubble.Sprite.Frame = frame;
        }
        if (_draining || _activeUntil <= 0.0 || _bubbles.Count >= MaxBubbles)
            return;
        _bubbleTimer -= step;
        if (_bubbleTimer > 0.0f)
            return;
        _bubbleTimer = BubbleInterval * (float)GD.RandRange(0.6, 1.4);
        // Borbulha sobre as manchas ativadas (as extras; se não houver, qualquer uma).
        var candidates = _blobs.FindAll(b => b.Extra && !b.Dying && !b.Dead && b.Age > 0.3f);
        if (candidates.Count == 0)
            candidates = _blobs.FindAll(b => !b.Dying && !b.Dead && b.Age > 0.3f);
        if (candidates.Count == 0 || !LoadBubbleSheet())
            return;
        var blob = candidates[(int)(GD.Randi() % (uint)candidates.Count)];
        var angle = (float)GD.RandRange(0.0, Mathf.Tau);
        var distance = (float)GD.RandRange(0.0, 0.5);
        var position = blob.Fill.Position + new Vector3(Mathf.Cos(angle) * distance, 0.0f, Mathf.Sin(angle) * distance / _stretch);
        position.Y = BubbleHeight;
        var sprite = new Sprite3D
        {
            Name = "Bubble",
            Texture = _bubbleSheet,
            Hframes = BubbleFrames,
            PixelSize = PlayerSpriteVisual.PixelSize,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlphaCut = SpriteBase3D.AlphaCutMode.Discard,
            Shaded = !GameMode.IsPixelArt,
            DoubleSided = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f),
            Position = position,
        };
        AddChild(sprite);
        _bubbles.Add(new Bubble { Sprite = sprite });
    }

    // Folha das bolhas desenhada em código só com cores da AAP-64 (as mesmas da folha da poça).
    private static bool LoadBubbleSheet()
    {
        if (_bubbleSheet != null)
            return true;
        var image = Image.CreateEmpty(BubbleSize * BubbleFrames, BubbleSize, false, Image.Format.Rgba8);
        void Px(int frame, int x, int y, Color color) => image.SetPixel(frame * BubbleSize + x, y, color);
        // Quadro 0: um ponto brilhante.
        Px(0, 3, 4, BubbleSpec); Px(0, 4, 4, BubbleSpec);
        // Quadro 1: bolha pequena (anel 4x4).
        for (var i = 2; i <= 5; i++) { Px(1, i, 2, BubbleEdge); Px(1, i, 5, BubbleEdge); Px(1, 2, i, BubbleEdge); Px(1, 5, i, BubbleEdge); }
        Px(1, 3, 3, BubbleSpec);
        // Quadro 2: bolha cheia (anel 6x6) com brilho.
        for (var i = 1; i <= 6; i++) { Px(2, i, 1, BubbleEdge); Px(2, i, 6, BubbleEdge); Px(2, 1, i, BubbleEdge); Px(2, 6, i, BubbleEdge); }
        Px(2, 1, 1, new Color(0, 0, 0, 0)); Px(2, 6, 1, new Color(0, 0, 0, 0)); Px(2, 1, 6, new Color(0, 0, 0, 0)); Px(2, 6, 6, new Color(0, 0, 0, 0));
        Px(2, 3, 3, BubbleGlint); Px(2, 4, 3, BubbleSpec); Px(2, 3, 4, BubbleSpec);
        // Quadro 3: estouro (respingos soltos).
        Px(3, 1, 3, BubbleSpec); Px(3, 6, 3, BubbleSpec); Px(3, 3, 1, BubbleSpec); Px(3, 4, 6, BubbleSpec); Px(3, 2, 6, BubbleEdge); Px(3, 5, 1, BubbleEdge);
        _bubbleSheet = ImageTexture.CreateFromImage(image);
        return true;
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
            Kill(blob);
            return;
        }
        blob.DyingFrom = blob.Age < SpreadFrames / SpreadFps
            ? Mathf.Min((int)(blob.Age * SpreadFps), SpreadFrames - 1)
            : SpreadFrames - 1;
        blob.DyingDuration = duration;
        blob.DyingT = 0.0f;
        blob.Dying = true;
    }

    private static void Kill(Blob blob)
    {
        blob.Dead = true;
        blob.Outline.Visible = false;
        blob.Fill.Visible = false;
    }

    private static void SetFrame(Blob blob, int column)
    {
        var frame = blob.Variant * Columns + Mathf.Clamp(column, 0, Columns - 1);
        blob.Outline.Frame = frame;
        blob.Fill.Frame = frame;
    }

    // Da folha original (contorno + corpo) saem duas folhas: o preenchimento (o contorno interno vira corpo) e só o
    // contorno (a silhueta engordada em 1 pixel). Só usa cores que já estão na folha (paleta AAP-64).
    private static bool LoadSheets()
    {
        if (_fillSheet != null && _outlineSheet != null)
            return true;
        var image = PixelRoomComposer.LoadImage(SheetPath);
        if (image == null)
            return false;
        var width = image.GetWidth();
        var height = image.GetHeight();
        var fill = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        var outline = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        bool Opaque(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && image.GetPixel(x, y).A > 0.5f;
        int Neighbors(int x, int y) => (Opaque(x - 1, y) ? 1 : 0) + (Opaque(x + 1, y) ? 1 : 0) + (Opaque(x, y - 1) ? 1 : 0) + (Opaque(x, y + 1) ? 1 : 0);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!Opaque(x, y))
                    continue;
                var color = image.GetPixel(x, y);
                var neighbors = Neighbors(x, y);
                var isRim = Mathf.Abs(color.R - Rim.R) < 0.02f && Mathf.Abs(color.G - Rim.G) < 0.02f && Mathf.Abs(color.B - Rim.B) < 0.02f;
                fill.SetPixel(x, y, isRim && neighbors >= 3 ? Body : color);
                // Respingos soltos (poucos vizinhos) ficam como estão, sem contorno extra.
                if (neighbors < 2)
                    continue;
                outline.SetPixel(x, y, Rim);
                foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                    if (!Opaque(x + dx, y + dy) && x + dx >= 0 && y + dy >= 0 && x + dx < width && y + dy < height)
                        outline.SetPixel(x + dx, y + dy, Rim);
            }
        }
        _fillSheet = ImageTexture.CreateFromImage(fill);
        _outlineSheet = ImageTexture.CreateFromImage(outline);
        return true;
    }
}
