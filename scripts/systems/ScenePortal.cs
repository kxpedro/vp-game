using Godot;
using System;
using System.Collections.Generic;

public partial class ScenePortal : Area3D
{
    public enum RunPortalAction
    {
        None,
        StartRun,
        AdvanceRoom,
        CompleteBoss
    }

    // Mesmo valor de RoomGenerator.CellSize: as portas de recompensa ficam a +-1 celula do centro da saida.
    private const float DoorCellSize = 1.45f;
    // Folga entre a face interna da parede e a face da moldura que olha para a sala (docs/design/portas-recompensas.md, 6.3).
    public const float WallClearance = 0.05f;
    // Queda das barras ao fechar (barra com 2,25 m de altura, docs/design/portas-recompensas.md, 7.2).
    private const float DoorBarDrop = 2.3f;

    [Export(PropertyHint.File, "*.tscn")] public string TargetScenePath { get; set; } = "";
    // Texto do prompt ou chave de data/localization/<idioma>/*.json (resolvida na exibição e na troca de idioma).
    // Vazio = "portal.default".
    [Export] public string PromptText { get; set; } = "";
    [Export] public bool StartsEnabled { get; set; } = true;
    [Export] public RunPortalAction PortalAction { get; set; } = RunPortalAction.None;

    /// <summary>Porta de recompensa (docs/design/portas-recompensas.md). Null nos portais comuns (hub, boss, saída sem conjunto).</summary>
    public RoomRewardDoor RewardDoor { get; private set; }
    /// <summary>Demais portas do mesmo conjunto: ao escolher uma, as outras fecham antes da troca de cena.</summary>
    public List<ScenePortal> RewardDoorSiblings { get; private set; } = new();

    private bool _isPlayerInside;
    private bool _isEnabled;
    private bool _isChosen;
    // Aviso ao aproximar de uma porta travada pela fruta da sala (reward.door.fruit.locked).
    private bool _fruitLockHint;
    private Label3D _promptLabel;

    private static float CloseSeconds => CompendiumTuning.GetFloat("reward.door.close_seconds", 0.3f);

    public override void _Ready()
    {
        _isEnabled = StartsEnabled;
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
        CreatePromptLabel();
        UpdatePromptVisibility();
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged += OnLanguageChanged;
    }

    public override void _ExitTree()
    {
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(string language)
    {
        if (_promptLabel != null)
            UpdatePromptVisibility();
    }

    private string ResolvePromptText()
    {
        if (RewardDoor != null)
            return RewardDoorPromptText(RewardDoor);
        if (string.IsNullOrEmpty(PromptText))
            return Localization.Text("portal.default");
        return Localization.Has(PromptText) ? Localization.Text(PromptText) : PromptText;
    }

    public override void _Process(double delta)
    {
        if (!_isEnabled || !_isPlayerInside)
            return;

        if (!InteractPrompt.ConsumeInteract(this) || string.IsNullOrWhiteSpace(TargetScenePath))
            return;

        if (RewardDoor != null)
        {
            ChooseRewardDoor();
            return;
        }

        ApplyPortalAction();
        GetTree().ChangeSceneToFile(TargetScenePath);
    }

    public void ConfigureDestination(string targetScenePath, string promptText, RunPortalAction portalAction)
    {
        TargetScenePath = targetScenePath;
        PromptText = promptText;
        PortalAction = portalAction;

        if (_promptLabel != null)
            UpdatePromptVisibility();
    }

    public void SetPortalEnabled(bool isEnabled)
    {
        _isEnabled = isEnabled;
        UpdatePromptVisibility();
    }

    /// <summary>Ao aproximar de uma porta travada, mostra "pegue a fruta para destravar as portas" (sala com Fruta pendente).</summary>
    public void SetFruitLockHint(bool show)
    {
        _fruitLockHint = show;
        UpdatePromptVisibility();
    }

    /// <summary>Troca o material das barras da porta (aberta ou travada). Só age sobre as barras da própria porta.</summary>
    public void SetBarsMaterial(Material material)
    {
        foreach (var bar in BarsOf(GetParent() as Node3D))
            bar.MaterialOverride = material;
    }

    /// <summary>Pulso de abertura da moldura da porta (mesmo ritmo da saída comum).</summary>
    public void PlayOpenPulse()
    {
        if (GetParent() is not Node3D root)
            return;
        root.Scale = Vector3.One * 0.78f;
        var tween = root.CreateTween();
        tween.TweenProperty(root, "scale", Vector3.One * 1.12f, 0.18f);
        tween.TweenProperty(root, "scale", Vector3.One, 0.12f);
    }

    /// <summary>Fecha a porta que não foi escolhida: as barras descem e a entrada fica desligada.</summary>
    public void CloseDoor(float seconds)
    {
        SetPortalEnabled(false);
        var bars = BarsOf(GetParent() as Node3D);
        if (bars.Count == 0)
            return;

        var tween = CreateTween().SetParallel(true);
        foreach (var bar in bars)
            tween.TweenProperty(bar, "position:y", bar.Position.Y - DoorBarDrop, seconds);
    }

    private void CreatePromptLabel()
    {
        // Sem título no portal (salas da run): prompt acima do lintel (~2,9 m). No hub, ancora no título irmão.
        _promptLabel = InteractPrompt.CreatePrompt(this, ResolvePromptText(), 3.2f);
        // Porta de recompensa: o prompt (uma ou duas linhas) fica acima do medalhão (RewardIcon, centro a 3,5 m; topo do brilho ~4,1 m).
        if (RewardDoor != null)
            _promptLabel.Position = Vector3.Up * 4.45f;
        InteractPrompt.StyleSiblingTitlesAndAnchor(this, _promptLabel);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!body.IsInGroup("player"))
            return;

        _isPlayerInside = true;
        UpdatePromptVisibility();
    }

    private void OnBodyExited(Node3D body)
    {
        if (!body.IsInGroup("player"))
            return;

        _isPlayerInside = false;
        UpdatePromptVisibility();
    }

    private void UpdatePromptVisibility()
    {
        if (_promptLabel == null)
            return;

        var showLocked = !_isEnabled && _fruitLockHint;
        InteractPrompt.SetText(_promptLabel, showLocked ? Localization.Text("reward.door.fruit.locked") : ResolvePromptText());
        _promptLabel.Visible = (_isEnabled || showLocked) && _isPlayerInside;
    }

    // Entrar numa porta de recompensa: grava a escolha (paga ao limpar a sala seguinte), fecha as outras
    // e só então troca de cena (docs/design/portas-recompensas.md, 7.1 e 7.2).
    private void ChooseRewardDoor()
    {
        if (_isChosen)
            return;
        if (!RunState.ChooseRoomRewardDoor(RewardDoor))
        {
            GD.PushWarning("Porta de recompensa recusada: ja existe uma recompensa pendente.");
            return;
        }

        _isChosen = true;
        var closeSeconds = CloseSeconds;
        SetPortalEnabled(false);
        foreach (var sibling in RewardDoorSiblings)
        {
            if (sibling != this && IsInstanceValid(sibling))
                sibling.CloseDoor(closeSeconds);
        }
        ApplyPortalAction();

        var timer = GetTree().CreateTimer(closeSeconds, false);
        timer.Timeout += () =>
        {
            if (IsInstanceValid(this) && !string.IsNullOrWhiteSpace(TargetScenePath))
                GetTree().ChangeSceneToFile(TargetScenePath);
        };
    }

    private void ApplyPortalAction()
    {
        if (PortalAction == RunPortalAction.StartRun)
            RunState.StartRun();

        if (PortalAction == RunPortalAction.AdvanceRoom)
            RunState.AdvanceRoom();

        if (PortalAction == RunPortalAction.CompleteBoss)
            RunState.CompleteBoss();
    }

    /// <summary>
    /// Base de uma porta encostada numa parede: +Z local aponta para dentro da sala (<paramref name="inward"/>), +Y para cima
    /// e +X ao longo da parede. Assim a frente do arco e do ícone olha para a sala, e as duas portas de um conjunto ficam
    /// lado a lado ao longo da parede (±X local).
    /// </summary>
    public static Basis WallBasis(Vector3 inward)
    {
        var forward = new Vector3(inward.X, 0.0f, inward.Z).Normalized();
        var right = Vector3.Up.Cross(forward);
        return new Basis(right, Vector3.Up, forward);
    }

    /// <summary>
    /// Põe o modelo de saída (template) encostado na face interna da parede, com a frente voltada para a sala.
    /// <paramref name="wallCenterline"/> é o centro da espessura da parede; a face da moldura que olha para a sala
    /// fica <see cref="WallClearance"/> m à frente da face interna (sem atravessar a parede). A origem do modelo é o
    /// centro da profundidade da moldura, então ela recua metade da profundidade a partir dessa face.
    /// </summary>
    public static void PlaceOnWall(Node3D template, Vector3 wallCenterline, Vector3 inward, float wallThickness)
    {
        var basis = WallBasis(inward);
        // basis.Z é a direção horizontal para dentro da sala (ver WallBasis).
        var towardRoom = basis.Z;
        var frontFromCenterline = wallThickness * 0.5f + WallClearance;
        var originFromCenterline = frontFromCenterline - FrameDepth(template) * 0.5f;
        template.Transform = new Transform3D(basis, wallCenterline + towardRoom * originFromCenterline);
    }

    // Profundidade da moldura do template (Z do pilar), em metros. Fallback = profundidade de TestArena.tscn.
    private static float FrameDepth(Node3D template)
    {
        foreach (var child in template.GetChildren())
        {
            if (child is MeshInstance3D piece
                && piece.Name.ToString().StartsWith("Pillar", StringComparison.Ordinal)
                && piece.Mesh is BoxMesh box)
                return box.Size.Z;
        }
        return 0.42f;
    }

    /// <summary>
    /// Monta as portas de recompensa de uma saída a partir do portal modelo (docs/design/portas-recompensas.md, 6.3).
    /// Uma porta fica no centro do modelo; duas ficam a ±1 célula ao longo da base do modelo (+X = direita de quem olha
    /// a porta de frente, ver WallBasis), com a moldura em 60% da largura. Quem chama põe o modelo na parede com PlaceOnWall.
    /// Cada porta vira a raiz "RewardDoor_i" (com a moldura copiada, um ícone provisório e o ScenePortal "PortalArea").
    /// O modelo fica escondido. Devolve as portas na ordem de <see cref="RoomRewardSet.Doors"/> (0 = esquerda).
    /// <paramref name="startEnabled"/> = false deixa as portas travadas até a recompensa ser resolvida.
    /// </summary>
    public static List<ScenePortal> BuildRewardDoors(Node3D template, RoomRewardSet set, string targetScenePath, RunPortalAction action, bool startEnabled)
    {
        var doors = new List<ScenePortal>();
        var parent = template?.GetParent();
        if (set == null || set.Doors.Count == 0 || parent == null)
            return doors;

        var templatePortal = template.GetNodeOrNull<ScenePortal>("PortalArea");
        var basis = template.Transform.Basis.Orthonormalized();
        var twoDoors = set.TwoDoors;
        var offset = CompendiumTuning.GetFloat("reward.door.two_door_offset_cells", 1.0f) * DoorCellSize;
        var frameScale = twoDoors ? CompendiumTuning.GetFloat("reward.door.two_door_frame_scale", 0.6f) : 1.0f;

        for (var i = 0; i < set.Doors.Count; i++)
        {
            var lateral = twoDoors ? (i == 0 ? -offset : offset) : 0.0f;
            var root = new Node3D
            {
                Name = $"RewardDoor_{i}",
                Transform = new Transform3D(basis, template.Position + basis.X * lateral)
            };
            parent.AddChild(root);
            CopyDoorFrame(template, root, frameScale);
            // Rótulo do medalhão para fora do conjunto: a porta da esquerda o põe à esquerda, a da direita à direita.
            AddRewardIcon(root, set.Doors[i], lateral < 0.0f ? -1 : 1);
            doors.Add(CreateDoorArea(templatePortal, root, set.Doors[i], targetScenePath, action, startEnabled));
        }

        foreach (var door in doors)
            door.RewardDoorSiblings = doors;

        template.Visible = false;
        templatePortal?.SetPortalEnabled(false);
        return doors;
    }

    // Copia as peças de moldura (colunas, lintel, disco e barras). Com duas portas, as colunas e as barras
    // ficam mais perto do centro e o lintel e o disco encolhem na largura.
    private static void CopyDoorFrame(Node3D template, Node3D root, float frameScale)
    {
        foreach (var child in template.GetChildren())
        {
            if (child is not MeshInstance3D piece)
                continue;

            var copy = (MeshInstance3D)piece.Duplicate();
            var name = piece.Name.ToString();
            if (name.StartsWith("Pillar", StringComparison.Ordinal) || name.StartsWith("Bar", StringComparison.Ordinal))
                copy.Position = piece.Position * new Vector3(frameScale, 1.0f, 1.0f);
            else if (name == "Lintel")
                copy.Scale = piece.Scale * new Vector3(frameScale, 1.0f, 1.0f);
            else if (name == "Disc")
                copy.Scale = piece.Scale * frameScale;
            root.AddChild(copy);
        }
    }

    private static ScenePortal CreateDoorArea(ScenePortal templatePortal, Node3D root, RoomRewardDoor door, string targetScenePath, RunPortalAction action, bool startEnabled)
    {
        var area = new ScenePortal
        {
            Name = "PortalArea",
            TargetScenePath = targetScenePath,
            PortalAction = action,
            StartsEnabled = startEnabled
        };
        area.RewardDoor = door;
        if (templatePortal != null)
        {
            area.Position = templatePortal.Position;
            area.CollisionLayer = templatePortal.CollisionLayer;
            area.CollisionMask = templatePortal.CollisionMask;
            if (FindCollisionShape(templatePortal) is { } shapeSource)
                area.AddChild(new CollisionShape3D { Name = "Collision", Position = shapeSource.Position, Shape = shapeSource.Shape });
        }

        // Entra na árvore depois de RewardDoor e StartsEnabled: o prompt já nasce com o texto e o estado certos.
        root.AddChild(area);
        return area;
    }

    // Medalhão da porta: por tipo em DoorRewardIcon (na Bênção, o ícone da família sorteada), docs/design/portas-recompensas.md, secao 8.
    // Centro a 3,5 m: o lintel do modelo vai até ~2,87 m; o disco visível tem raio ~0,54 m, então a borda inferior fica ~3,0 m (acima do lintel).
    private static void AddRewardIcon(Node3D root, RoomRewardDoor door, int labelSide)
    {
        var icon = new Node3D { Name = "RewardIcon", Position = new Vector3(0.0f, 3.5f, 0.0f) };
        root.AddChild(icon);
        DoorRewardIcon.PopulateDoor(icon, door, labelSide);
    }

    private static string RewardDoorPromptText(RoomRewardDoor door)
    {
        string label;
        string hint;
        switch (door.Type)
        {
            case RoomRewardType.Blessing:
                // "Atravessar: Bênção <Família>": com plano, a família já diz o que vem (sem lista de nomes); sem plano, o texto genérico.
                label = DoorRewardIcon.ShortLabel(door);
                hint = string.IsNullOrEmpty(door.BlessingPlan?.Family) ? Localization.Text("reward.door.blessing.hint") : null;
                break;
            case RoomRewardType.DevilsFruit:
                label = Localization.Text("reward.door.fruit.short");
                hint = Localization.TextFormat("reward.door.fruit.hint", Mathf.RoundToInt(RunState.DevilsFruitMaxHealthAmount));
                break;
            default:
                label = Localization.TextFormat("reward.door.coins.short", door.CoinAmount);
                hint = Localization.TextFormat("reward.door.coins.hint", door.CoinAmount);
                break;
        }
        var prompt = Localization.TextFormat("reward.door.prompt", label);
        return hint == null ? prompt : prompt + "\n" + hint;
    }

    private static List<MeshInstance3D> BarsOf(Node3D root)
    {
        var bars = new List<MeshInstance3D>();
        if (root == null)
            return bars;
        foreach (var child in root.GetChildren())
        {
            if (child is MeshInstance3D mesh && mesh.Name.ToString().StartsWith("Bar", StringComparison.Ordinal))
                bars.Add(mesh);
        }
        return bars;
    }

    private static CollisionShape3D FindCollisionShape(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is CollisionShape3D shape)
                return shape;
        }
        return null;
    }
}
