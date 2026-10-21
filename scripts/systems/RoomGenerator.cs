using Godot;
using System.Collections.Generic;

public partial class RoomGenerator : Node3D
{
    [Export] public PackedScene EnemyScene { get; set; }
    [Export] public int EnemyBlocksPerRoom { get; set; } = 2;
    [Export] public int EnemySpawnWallPaddingCells { get; set; } = 2;
    [Export] public float EnemySpawnDelaySeconds { get; set; } = 3.0f;
    [Export] public float WaveIntervalSeconds { get; set; } = 9.0f;
    [Export] public int EnemiesPerWave { get; set; } = 5;
    [Export] public int WaveCount { get; set; } = 4;

    public Vector3 PlayerSpawnPosition { get; private set; }
    public Vector3 ExitPortalPosition { get; private set; }
    public IReadOnlyCollection<Vector2I> WalkableCells => _walkableCells;
    public float MapCellSize => CellSize;

    private const float CellSize = 1.45f;
    // Espessura das paredes de borda geradas (AddBoundaryWalls). A moldura da saída usa este valor para encostar na face interna.
    private const float WallThickness = 0.28f;
    private const float FloorHeight = 0.22f;
    private const float WallHeight = 2.35f;
    private const int RegionSpacing = 1;
    private const string DungeonFloorDetailPath = "res://assets/environment/kenney_modular_dungeon/floor_detail.glb";
    private const string DungeonFloorDetailAltPath = "res://assets/environment/kenney_modular_dungeon/floor_detail_a.glb";
    private const string DungeonGatePath = "res://assets/environment/kenney_modular_dungeon/gate.glb";
    private const string WallTorchPath = "res://assets/environment/quaternius_medieval_dungeon/Torch_Wall.fbx";

    private readonly RandomNumberGenerator _rng = new();
    private readonly HashSet<Vector2I> _walkableCells = new();
    private readonly HashSet<Vector2I> _roomCells = new();
    private readonly List<Vector2I> _enemySpawnCells = new();
    private readonly List<Vector3> _reservedPositions = new();

    private Vector2I _entranceCell = new(0, 3);
    private Vector2I _exitCell = new(0, -4);
    private Vector2I _rewardCell = new(0, -12);
    private Vector2I _focusCell = Vector2I.Zero;
    // Direção (célula) para fora da parede onde fica a saída, quando a sala tem conjunto de portas (docs/design/portas-recompensas.md, 6.3).
    // Zero = saída no centro da célula (boss, mapa sem conjunto, sala N).
    private Vector2I _exitWallDirection = Vector2I.Zero;
    // true = a saída tem conjunto de portas (esconde o visual antigo "Saída"). A sala N tem saída na parede, mas sem portas.
    private bool _exitHasDoors;

    private StandardMaterial3D _floorMaterial;
    private StandardMaterial3D _wallMaterial;
    private StandardMaterial3D _markerMaterial;
    private readonly Dictionary<Vector3, BoxMesh> _boxMeshCache = new();
    private readonly Dictionary<Vector3, BoxShape3D> _boxShapeCache = new();
    private int _layoutQuarterTurns;
    private bool _layoutMirror;
    private readonly List<(Vector3 Position, EnemyRarity Rarity, EnemyArchetype Archetype, string Name)> _pendingEnemyWaves = new();
    private int _nextWaveEnemyIndex;
    private bool _isSpawningWave;
    private float _waveCountdown;

    public override void _Ready()
    {
        AddToGroup("room_generator");
        _rng.Seed = BuildRoomSeed();
        _layoutQuarterTurns = _rng.RandiRange(0, 3);
        _layoutMirror = _rng.Randf() < 0.5f;
        BuildMaterials();
        RemoveStaticArenaShell();
        BuildMap();
        ApplySceneAnchors();
        GenerateMapGeometry();
        GeneratePointsOfInterest();
        BuildEnemySpawnCells();
        GenerateEnemies();
    }

    public override void _Process(double delta)
    {
        _waveCountdown -= (float)delta;
        var canSpawnNextWave = !_isSpawningWave && _nextWaveEnemyIndex < _pendingEnemyWaves.Count;
        if (canSpawnNextWave && (_waveCountdown <= 0.0f || !HasLivingEnemies()))
            SpawnNextWave();
    }

    private bool HasLivingEnemies()
    {
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node.IsQueuedForDeletion()) continue;
            var health = node.GetNodeOrNull<Health>("Health");
            if (health != null && !health.IsDead) return true;
        }
        return false;
    }

    public Vector3 GetArtifactSpawnPosition()
    {
        return CellToWorld(_rewardCell) + new Vector3(_rng.RandfRange(-0.55f, 0.55f), 0.15f, _rng.RandfRange(-0.55f, 0.55f));
    }

    public Vector3 ClampToWalkableArea(Vector3 position, int paddingCells = 1)
    {
        if (_walkableCells.Count == 0)
            return position;

        var requestedCell = WorldToCell(position);
        var safeCell = requestedCell;
        if (!_walkableCells.Contains(safeCell) || !HasWalkablePadding(safeCell, paddingCells))
        {
            var bestDistance = float.PositiveInfinity;
            foreach (var candidate in _walkableCells)
            {
                if (!HasWalkablePadding(candidate, paddingCells))
                    continue;
                var distance = CellToWorld(candidate).DistanceSquaredTo(position);
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                safeCell = candidate;
            }

            if (float.IsPositiveInfinity(bestDistance))
                safeCell = NearestWalkableCell(requestedCell);
        }

        var cellCenter = CellToWorld(safeCell);
        var localLimit = CellSize * 0.32f;
        return new Vector3(
            cellCenter.X + Mathf.Clamp(position.X - cellCenter.X, -localLimit, localLimit),
            position.Y,
            cellCenter.Z + Mathf.Clamp(position.Z - cellCenter.Z, -localLimit, localLimit));
    }

    private void BuildMap()
    {
        _walkableCells.Clear();
        _roomCells.Clear();
        _exitWallDirection = Vector2I.Zero;
        _exitHasDoors = false;

        if (RunState.IsBossRoom)
        {
            BuildBossMap();
            return;
        }

        BuildCompactLinearMap();
    }

    private void BuildCompactLinearMap()
    {
        var firstOffset = _rng.RandiRange(-1, 1) * 3;
        var secondOffset = _rng.RandiRange(-1, 1) * 3;
        var rooms = new[] { R(0, 12), R(firstOffset, 4), R(secondOffset, -4), R(0, -12) };

        _entranceCell = rooms[0];
        _focusCell = rooms[1];
        _rewardCell = rooms[3];
        _exitCell = rooms[3];

        for (var i = 0; i < rooms.Length; i++)
            AddRoom(rooms[i], 9, 9);

        // Toda saída de sala comum vai para a parede de borda. Com conjunto de portas esconde o visual antigo;
        // na sala N (sem conjunto, leva ao boss) é só a saída única na parede.
        _exitHasDoors = RoomRewardCatalog.HasRoomRewardSet(RunState.CurrentMapIndex, RunState.CurrentRoom);
        PlaceExitOnFarWall(rooms[3], _entranceCell);
    }

    // Escolhe a parede de borda mais distante da entrada (a oposta à ligação com a sala anterior) e põe a saída no meio dela.
    // Só vale parede com 3 células caminháveis e parede sólida atrás (sem corredor de ligação), para caber duas portas (±1 célula).
    private void PlaceExitOnFarWall(Vector2I room, Vector2I entrance)
    {
        const int halfCells = 4; // sala de 9x9 (AddRoom(..., 9, 9))
        var bestDistance = -1;
        // strictness 0 = regra completa (3 celulas + parede atras); 1 = so a celula central com parede atras; 2 = qualquer borda da sala.
        for (var strictness = 0; strictness <= 2 && !HasExitOnWall; strictness++)
        foreach (var outward in new[] { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right })
        {
            var middle = room + outward * halfCells;
            var along = new Vector2I(Mathf.Abs(outward.Y), Mathf.Abs(outward.X));
            var fits = true;
            if (strictness == 0)
            {
                for (var step = -1; step <= 1 && fits; step++)
                {
                    var cell = middle + along * step;
                    fits = _walkableCells.Contains(cell) && !_walkableCells.Contains(cell + outward);
                }
            }
            else if (strictness == 1)
                fits = _walkableCells.Contains(middle) && !_walkableCells.Contains(middle + outward);
            if (!fits)
                continue;

            var delta = middle - entrance;
            var distance = delta.X * delta.X + delta.Y * delta.Y;
            if (distance <= bestDistance)
                continue;
            bestDistance = distance;
            _exitCell = middle;
            _exitWallDirection = outward;
        }

        if (!HasExitOnWall)
            GD.PushWarning("RoomGenerator: nenhuma parede livre para a saida com portas; a saida fica no centro da sala.");
    }

    private bool HasExitOnWall => _exitWallDirection != Vector2I.Zero;

    // Direção para fora da parede da saída, no mundo (célula (x, y) → mundo (x, 0, y), como CellToWorld).
    private Vector3 OutwardWorld() => new(_exitWallDirection.X, 0.0f, _exitWallDirection.Y);

    // Linha da saída no mundo: a própria célula, ou a linha da parede (meia célula para fora) quando a saída está na parede.
    private Vector3 ExitLineWorld()
    {
        var cell = CellToWorld(_exitCell);
        return HasExitOnWall ? cell + OutwardWorld() * (CellSize * 0.5f) : cell;
    }

    private void BuildBossMap()
    {
        _entranceCell = new Vector2I(0, 8);
        _exitCell = new Vector2I(0, -8);
        _rewardCell = new Vector2I(0, -5);
        _focusCell = Vector2I.Zero;

        AddMainRoom(new Vector2I(0, 0), 21, 21);
    }

    private void GenerateMapGeometry()
    {
        foreach (var cell in _walkableCells)
            AddFloorCell(cell);

        foreach (var cell in _walkableCells)
            AddBoundaryWalls(cell);

        if (RunState.IsBossRoom)
            AddBossPillars();

        AddImportedDungeonDetails();
        AddTorchLights();
    }

    private void AddImportedDungeonDetails()
    {
        var floorDetail = ResourceLoader.Load<PackedScene>(DungeonFloorDetailPath);
        var floorDetailAlt = ResourceLoader.Load<PackedScene>(DungeonFloorDetailAltPath);
        if (floorDetail != null && floorDetailAlt != null)
        {
            foreach (var cell in _walkableCells)
            {
                var hash = unchecked(cell.X * 73856093 ^ cell.Y * 19349663);
                if ((hash & int.MaxValue) % 7 != 0)
                    continue;

                var source = ((hash & int.MaxValue) / 7) % 2 == 0 ? floorDetail : floorDetailAlt;
                var detail = source.Instantiate<Node3D>();
                detail.Name = $"DungeonFloorDetail_{cell.X}_{cell.Y}";
                detail.Position = CellToWorld(cell) + Vector3.Up * 0.015f;
                detail.Scale = Vector3.One * (CellSize * 0.49f);
                detail.Rotation = new Vector3(0.0f, Mathf.Pi * ((hash & 3) * 0.5f), 0.0f);
                AddChild(detail);
                ApplyImportedMaterial(detail, _floorMaterial);
            }
        }

        // Com conjunto de portas, o visual antigo de saída (gate) não aparece: a saída são as portas na parede.
        var gateScene = ResourceLoader.Load<PackedScene>(DungeonGatePath);
        if (gateScene == null || _exitHasDoors)
            return;

        var gate = gateScene.Instantiate<Node3D>();
        gate.Name = "DungeonExitGateVisual";
        gate.Position = ExitLineWorld();
        gate.Scale = Vector3.One * 0.9f;
        AddChild(gate);

        // Saída na parede: o gate olha para dentro da sala. Sem parede (boss), olha para o centro focal, como antes.
        var towardRoom = HasExitOnWall ? -OutwardWorld() : CellToWorld(_focusCell) - gate.Position;
        towardRoom.Y = 0.0f;
        if (towardRoom.LengthSquared() > 0.001f)
            gate.LookAt(gate.GlobalPosition + towardRoom.Normalized(), Vector3.Up);
    }

    private static void ApplyImportedMaterial(Node3D root, Material material)
    {
        if (root is MeshInstance3D rootMesh)
            rootMesh.MaterialOverride = material;
        foreach (var child in root.FindChildren("*", "MeshInstance3D", true, false))
            if (child is MeshInstance3D mesh)
                mesh.MaterialOverride = material;
    }

    private void GeneratePointsOfInterest()
    {
        CreatePointOfInterest("Entrance", CellToWorld(_entranceCell), new Color(0.35f, 0.35f, 0.38f));
        if (RunState.IsBossRoom)
            CreatePointOfInterest("FinalGate", CellToWorld(_exitCell), new Color(0.85f, 0.02f, 0.04f));
        else if (!_exitHasDoors)
            // Com conjunto de portas não há marcador "Saída" no piso: as portas da parede são a saída.
            CreatePointOfInterest("Exit", CellToWorld(_exitCell), new Color(0.85f, 0.02f, 0.04f));

        if (!RunState.IsBossRoom)
        {
            CreatePointOfInterest("CentralChamber", CellToWorld(_focusCell), new Color(0.28f, 0.28f, 0.32f));
        }
        else
        {
            CreatePointOfInterest("BossArena", Vector3.Zero, new Color(0.85f, 0.02f, 0.04f));
        }
    }

    private void GenerateEnemies()
    {
        if (EnemyScene == null)
            return;

        _pendingEnemyWaves.Clear();
        // Parametros de geracao (painel de dev): valem para a proxima sala gerada.
        WaveCount = CompendiumTuning.GetInt("room.waves.count", WaveCount);
        EnemiesPerWave = Mathf.Max(CompendiumTuning.GetInt("room.waves.size", EnemiesPerWave), 1);
        EnemyVisualCatalog.BeginWarmup(RunState.IsBossRoom, RunState.CurrentMapIndex);

        if (RunState.IsBossRoom)
        {
            _pendingEnemyWaves.Add((Vector3.Zero, EnemyRarity.Legendary, EnemyArchetype.Ranged, "BloodMageBoss"));
        }
        else
        {
            var progress = RunState.CurrentMap.CommonRoomCount > 1
                ? (RunState.CurrentRoom - 1.0f) / (RunState.CurrentMap.CommonRoomCount - 1.0f)
                : 1.0f;
            for (var waveIndex = 0; waveIndex < WaveCount; waveIndex++)
            {
                for (var enemyIndex = 0; enemyIndex < EnemiesPerWave; enemyIndex++)
                    _pendingEnemyWaves.Add((
                        PickRandomWavePosition(),
                        EnemyTuningCatalog.Roll(_rng, RunState.CurrentMapIndex, progress),
                        RollEnemyArchetype(),
                        $"Wave{waveIndex + 1}_Enemy{enemyIndex + 1}"));
            }
        }

        _nextWaveEnemyIndex = 0;
        _waveCountdown = 0.0f;
        AddToGroup("enemy_waves_remaining");
        // Marca o spawn como pendente já aqui para o RunManager._Ready não configurar a sala vazia,
        // mas adia o disparo da 1ª wave para depois dos _Ready da cena: o HUD (irmão posterior)
        // ainda não está no grupo "hud" e perderia o aviso "Próxima onda".
        AddToGroup("enemy_spawn_pending");
        CallDeferred(MethodName.SpawnNextWave);
    }

    private EnemyArchetype RollEnemyArchetype()
    {
        // Ranged used to be one third of the pool. Twenty percent fewer yields 26.67%,
        // with the freed weight shared evenly between melee and tank enemies.
        var roll = _rng.Randf();
        var rangedChance = CompendiumTuning.GetFloat("room.archetype.ranged_chance", 0.266667f);
        var meleeChance = CompendiumTuning.GetFloat("room.archetype.melee_chance", 0.366666f);
        if (roll < rangedChance)
            return EnemyArchetype.Ranged;
        return roll < rangedChance + meleeChance ? EnemyArchetype.Melee : EnemyArchetype.Tank;
    }

    private Vector3 PickRandomWavePosition()
    {
        if (_enemySpawnCells.Count == 0)
            return Vector3.Zero;

        var best = Vector3.Zero;
        var bestClearance = -1.0f;
        var spacing = CompendiumTuning.GetFloat("room.spawn.min_spacing", 2.35f);
        var jitter = CompendiumTuning.GetFloat("room.spawn.jitter", 0.45f);
        var attempts = CompendiumTuning.GetInt("room.spawn.attempts", 18);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var cell = _enemySpawnCells[_rng.RandiRange(0, _enemySpawnCells.Count - 1)];
            var candidate = CellToWorld(cell) + new Vector3(_rng.RandfRange(-jitter, jitter), 0, _rng.RandfRange(-jitter, jitter));
            var clearance = float.MaxValue;
            foreach (var existing in _pendingEnemyWaves)
                clearance = Mathf.Min(clearance, candidate.DistanceTo(existing.Position));
            if (_pendingEnemyWaves.Count == 0 || clearance >= spacing)
                return candidate;
            if (clearance > bestClearance) { best = candidate; bestClearance = clearance; }
        }
        return best;
    }

    private async void SpawnNextWave()
    {
        if (_isSpawningWave || _nextWaveEnemyIndex >= _pendingEnemyWaves.Count) return;
        _isSpawningWave = true;
        AddToGroup("enemy_spawn_pending");
        var spawnDelay = CompendiumTuning.GetFloat("room.spawn.delay", EnemySpawnDelaySeconds);
        var wave = _pendingEnemyWaves.GetRange(_nextWaveEnemyIndex, Mathf.Min(EnemiesPerWave, _pendingEnemyWaves.Count - _nextWaveEnemyIndex));
        _nextWaveEnemyIndex += wave.Count;
        var markers = new List<Node3D>();
        foreach (var spawn in wave) markers.Add(CreateSpawnMarker(spawn.Position));
        GetTree().CallGroup("hud", "AppendGameLog", $"Wave com {wave.Count} inimigos chega em {spawnDelay:0} segundos.");
        GetTree().CallGroup("hud", "NotifyWaveIncoming", wave.Count, spawnDelay);
        await ToSignal(GetTree().CreateTimer(spawnDelay), SceneTreeTimer.SignalName.Timeout);

        if (RunState.IsBossRoom)
        {
            var bossVisual = EnemyVisualCatalog.GetBoss(RunState.CurrentMapIndex);
            var framesWaited = 0;
            while (ResourceLoader.LoadThreadedGetStatus(bossVisual.ScenePath) == ResourceLoader.ThreadLoadStatus.InProgress && framesWaited < 600)
            {
                framesWaited++;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }

        foreach (var marker in markers) if (IsInstanceValid(marker)) marker.QueueFree();
        var spawnedEnemies = new List<EnemyBasic>();
        foreach (var spawn in wave)
        {
            var enemy = EnemyScene.Instantiate<EnemyBasic>();
            enemy.Name = spawn.Name;
            enemy.Position = spawn.Position;
            AddChild(enemy);
            enemy.ConfigureVariant(spawn.Rarity, spawn.Archetype);
            if (RunState.IsBossRoom)
                enemy.ApplyPlaceholderVisual(EnemyVisualCatalog.GetBoss(RunState.CurrentMapIndex));
            else
            {
                enemy.ApplyPlaceholderVisual(EnemyVisualCatalog.GetEnemy(spawn.Archetype));
                // Diario: so inimigos comuns (inclui Legendary comum do mapa 2+). O boss e marcado via MarkBoss em RunManager.ConfigureBossRoom.
                CodexProgress.MarkEnemy(spawn.Archetype, spawn.Rarity);
            }
            enemy.SetSpawnActive(false);
            spawnedEnemies.Add(enemy);
            // Model instantiation is deliberately spread over frames to avoid a spawn hitch.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        foreach (var enemy in spawnedEnemies) if (IsInstanceValid(enemy)) enemy.SetSpawnActive(true);
        RemoveFromGroup("enemy_spawn_pending");
        if (_nextWaveEnemyIndex >= _pendingEnemyWaves.Count) RemoveFromGroup("enemy_waves_remaining");
        _isSpawningWave = false;
        _waveCountdown = Mathf.Max(CompendiumTuning.GetFloat("room.waves.interval", WaveIntervalSeconds) - spawnDelay, 0.1f);
        GetTree().CallGroup("run_manager", "NotifyWaveSpawned", wave.Count, _pendingEnemyWaves.Count);
    }

    private Node3D CreateSpawnMarker(Vector3 position)
    {
        var marker = new MeshInstance3D
        {
            Position = position + Vector3.Up * 0.04f,
            Mesh = new CylinderMesh { TopRadius = 0.48f, BottomRadius = 0.48f, Height = 0.05f },
            MaterialOverride = _markerMaterial,
            Scale = Vector3.One * 0.35f
        };
        AddChild(marker);
        var tween = marker.CreateTween().SetLoops();
        tween.TweenProperty(marker, "scale", Vector3.One * 1.15f, 0.55f);
        tween.TweenProperty(marker, "scale", Vector3.One * 0.35f, 0.55f);
        return marker;
    }

    private void BuildEnemySpawnCells()
    {
        _enemySpawnCells.Clear();

        AddEnemySpawnCellsWithPadding(CompendiumTuning.GetInt("room.spawn.wall_padding", EnemySpawnWallPaddingCells), true);

        if (_enemySpawnCells.Count < EnemyBlocksPerRoom)
            AddEnemySpawnCellsWithPadding(1, true);

        if (_enemySpawnCells.Count < EnemyBlocksPerRoom)
            AddEnemySpawnCellsWithPadding(1, false);

        if (_enemySpawnCells.Count < EnemyBlocksPerRoom)
            AddEnemySpawnCellsWithPadding(0, false);

        Shuffle(_enemySpawnCells);
        KeepCentralSpawnCells();
    }

    private void KeepCentralSpawnCells()
    {
        if (_enemySpawnCells.Count < 8 || _roomCells.Count == 0) return;
        var center = Vector2.Zero;
        foreach (var cell in _roomCells) center += new Vector2(cell.X, cell.Y);
        center /= _roomCells.Count;
        _enemySpawnCells.Sort((a, b) =>
            new Vector2(a.X, a.Y).DistanceSquaredTo(center).CompareTo(new Vector2(b.X, b.Y).DistanceSquaredTo(center)));
        var keep = Mathf.Max(Mathf.CeilToInt(_enemySpawnCells.Count * CompendiumTuning.GetFloat("room.spawn.central_fraction", 0.62f)), 8);
        if (_enemySpawnCells.Count > keep) _enemySpawnCells.RemoveRange(keep, _enemySpawnCells.Count - keep);
        Shuffle(_enemySpawnCells);
    }

    private void AddEnemySpawnCellsWithPadding(int wallPaddingCells, bool roomCellsOnly)
    {
        foreach (var cell in _walkableCells)
        {
            if (_enemySpawnCells.Contains(cell))
                continue;

            if (!IsValidEnemySpawnCell(cell, wallPaddingCells, roomCellsOnly))
                continue;

            _enemySpawnCells.Add(cell);
        }
    }

    private bool IsValidEnemySpawnCell(Vector2I cell, int wallPaddingCells, bool roomCellsOnly)
    {
        if (cell == _entranceCell || cell == _exitCell || cell == _focusCell)
            return false;

        if (roomCellsOnly && !_roomCells.Contains(cell))
            return false;

        var position = CellToWorld(cell);
        if (position.DistanceTo(PlayerSpawnPosition) < 7.0f || position.DistanceTo(ExitPortalPosition) < 4.0f)
            return false;

        return HasWalkablePadding(cell, wallPaddingCells);
    }

    private bool HasWalkablePadding(Vector2I center, int paddingCells)
    {
        if (paddingCells <= 0)
            return true;

        for (var x = center.X - paddingCells; x <= center.X + paddingCells; x++)
        {
            for (var y = center.Y - paddingCells; y <= center.Y + paddingCells; y++)
            {
                if (!_walkableCells.Contains(new Vector2I(x, y)))
                    return false;
            }
        }

        return true;
    }

    private void ApplySceneAnchors()
    {
        var playerSpawn = CellToWorld(_entranceCell);
        PlayerSpawnPosition = new Vector3(playerSpawn.X, 0.2f, playerSpawn.Z);
        var exitLine = ExitLineWorld();
        ExitPortalPosition = new Vector3(exitLine.X, 0.18f, exitLine.Z);
        _rewardCell = NearestWalkableCell(_rewardCell);
        _focusCell = NearestWalkableCell(_focusCell);

        if (GetParent().GetNodeOrNull<Node3D>("Player") is { } player)
            player.Position = PlayerSpawnPosition;

        // Saída na parede: o modelo (moldura e, sem conjunto de portas, a saída única) fica virado para a sala.
        if (GetParent().GetNodeOrNull<Node3D>("ExitPortal") is { } portal)
        {
            if (HasExitOnWall)
                ScenePortal.PlaceOnWall(portal, ExitPortalPosition, -OutwardWorld(), WallThickness);
            else
                portal.Position = ExitPortalPosition;
        }

        _reservedPositions.Add(PlayerSpawnPosition);
        _reservedPositions.Add(ExitPortalPosition);
    }

    private void AddFloorCell(Vector2I cell)
    {
        var body = new StaticBody3D
        {
            Name = $"Floor_{cell.X}_{cell.Y}",
            Position = CellToWorld(cell) + Vector3.Down * (FloorHeight * 0.5f)
        };
        AddChild(body);

        var size = new Vector3(CellSize, FloorHeight, CellSize);
        var collision = new CollisionShape3D
        {
            Shape = GetSharedBoxShape(size)
        };
        body.AddChild(collision);

        var visual = new MeshInstance3D
        {
            Mesh = GetSharedBoxMesh(size),
            MaterialOverride = _floorMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        body.AddChild(visual);
    }

    private void AddBoundaryWalls(Vector2I cell)
    {
        AddWallIfEmpty(cell, Vector2I.Up, new Vector3(0.0f, WallHeight * 0.5f, -CellSize * 0.5f), new Vector3(CellSize, WallHeight, WallThickness));
        AddWallIfEmpty(cell, Vector2I.Down, new Vector3(0.0f, WallHeight * 0.5f, CellSize * 0.5f), new Vector3(CellSize, WallHeight, WallThickness));
        AddWallIfEmpty(cell, Vector2I.Left, new Vector3(-CellSize * 0.5f, WallHeight * 0.5f, 0.0f), new Vector3(WallThickness, WallHeight, CellSize));
        AddWallIfEmpty(cell, Vector2I.Right, new Vector3(CellSize * 0.5f, WallHeight * 0.5f, 0.0f), new Vector3(WallThickness, WallHeight, CellSize));
    }

    private void AddWallIfEmpty(Vector2I cell, Vector2I direction, Vector3 localOffset, Vector3 size)
    {
        if (_walkableCells.Contains(cell + direction))
            return;

        var body = new StaticBody3D
        {
            Name = $"Wall_{cell.X}_{cell.Y}_{direction.X}_{direction.Y}",
            Position = CellToWorld(cell) + localOffset
        };
        AddChild(body);

        var collision = new CollisionShape3D
        {
            Shape = GetSharedBoxShape(size)
        };
        body.AddChild(collision);

        var visual = new MeshInstance3D
        {
            Mesh = GetSharedBoxMesh(size),
            MaterialOverride = _wallMaterial
        };
        body.AddChild(visual);
    }

    private BoxMesh GetSharedBoxMesh(Vector3 size)
    {
        if (!_boxMeshCache.TryGetValue(size, out var mesh))
        {
            mesh = new BoxMesh { Size = size };
            _boxMeshCache[size] = mesh;
        }
        return mesh;
    }

    private BoxShape3D GetSharedBoxShape(Vector3 size)
    {
        if (!_boxShapeCache.TryGetValue(size, out var shape))
        {
            shape = new BoxShape3D { Size = size };
            _boxShapeCache[size] = shape;
        }
        return shape;
    }

    private void AddBossPillars()
    {
        AddObstacle("BossPillarNW", new Vector3(-4.8f, 1.25f, -4.8f), new Vector3(1.4f, 2.5f, 1.4f));
        AddObstacle("BossPillarNE", new Vector3(4.8f, 1.25f, -4.8f), new Vector3(1.4f, 2.5f, 1.4f));
        AddObstacle("BossPillarSW", new Vector3(-4.8f, 1.25f, 3.2f), new Vector3(1.4f, 2.5f, 1.4f));
        AddObstacle("BossPillarSE", new Vector3(4.8f, 1.25f, 3.2f), new Vector3(1.4f, 2.5f, 1.4f));
    }

    private void AddRoom(Vector2I center, int width, int height)
    {
        var halfWidth = width / 2;
        var halfHeight = height / 2;

        for (var x = center.X - halfWidth; x <= center.X + halfWidth; x++)
        {
            for (var y = center.Y - halfHeight; y <= center.Y + halfHeight; y++)
            {
                var cell = new Vector2I(x, y);
                _walkableCells.Add(cell);
                _roomCells.Add(cell);
            }
        }
    }

    private void AddMainRoom(Vector2I center, int width, int height)
    {
        AddRoom(center, width, height);
    }

    private Vector2I R(int x, int y)
    {
        var point = new Vector2I((_layoutMirror ? -x : x) * RegionSpacing, y * RegionSpacing);
        for (var i = 0; i < _layoutQuarterTurns; i++) point = new Vector2I(-point.Y, point.X);
        return point;
    }

    private Vector3 CellToWorld(Vector2I cell)
    {
        return new Vector3(cell.X * CellSize, 0.2f, cell.Y * CellSize);
    }

    private Vector2I WorldToCell(Vector3 position)
    {
        return new Vector2I(Mathf.RoundToInt(position.X / CellSize), Mathf.RoundToInt(position.Z / CellSize));
    }

    private Vector2I NearestWalkableCell(Vector2I preferred)
    {
        if (_walkableCells.Contains(preferred))
            return preferred;

        var bestCell = preferred;
        var bestDistance = int.MaxValue;

        foreach (var cell in _walkableCells)
        {
            var distance = Mathf.Abs(cell.X - preferred.X) + Mathf.Abs(cell.Y - preferred.Y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestCell = cell;
            }
        }

        return bestCell;
    }

    private void AddObstacle(string obstacleName, Vector3 position, Vector3 size)
    {
        var body = new StaticBody3D
        {
            Name = obstacleName,
            Position = position
        };
        AddChild(body);

        var collision = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = size }
        };
        body.AddChild(collision);

        var visual = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = _wallMaterial
        };
        body.AddChild(visual);
    }

    private void CreatePointOfInterest(string nodeId, Vector3 position, Color color)
    {
        var marker = new MeshInstance3D
        {
            Name = $"{nodeId}Marker",
            Position = position + Vector3.Up * 0.05f,
            Mesh = new CylinderMesh
            {
                TopRadius = 0.55f,
                BottomRadius = 0.55f,
                Height = 0.08f,
                RadialSegments = 28
            },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(color.R, color.G, color.B, 0.65f),
                EmissionEnabled = true,
                Emission = color,
                EmissionEnergyMultiplier = 0.65f,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            }
        };
        AddChild(marker);
    }

    private void RemoveStaticArenaShell()
    {
        var parent = GetParent();
        foreach (var nodeName in new[] { "Floor", "NorthWall", "SouthWall", "EastWall", "WestWall" })
            parent.GetNodeOrNull<Node>(nodeName)?.QueueFree();
    }

    private void Shuffle(List<Vector2I> cells)
    {
        for (var i = cells.Count - 1; i > 0; i--)
        {
            var swapIndex = _rng.RandiRange(0, i);
            (cells[i], cells[swapIndex]) = (cells[swapIndex], cells[i]);
        }
    }

    private ulong BuildRoomSeed()
    {
        var seed = 1469598103934665603UL;
        seed ^= (ulong)RunState.CurrentRoom;
        seed *= 1099511628211UL;
        seed ^= (ulong)RunState.StoryFragmentsUnlocked + 97UL;
        seed *= 1099511628211UL;
        seed ^= (ulong)Time.GetTicksMsec();
        return seed;
    }

    private void BuildMaterials()
    {
        _floorMaterial = BuildStoneMaterial(new Color(0.055f, 0.045f, 0.055f), new Color(0.13f, 0.1f, 0.105f), 0.45f, 1201);
        _wallMaterial = BuildStoneMaterial(new Color(0.1f, 0.08f, 0.095f), new Color(0.2f, 0.16f, 0.17f), 0.6f, 2203);
        _markerMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.7f, 0.05f, 0.05f, 0.65f),
            EmissionEnabled = true,
            Emission = new Color(0.8f, 0.0f, 0.0f),
            EmissionEnergyMultiplier = 0.8f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
    }

    private static readonly Dictionary<int, (ImageTexture Albedo, ImageTexture Normal)> StoneTextureCache = new();

    private static StandardMaterial3D BuildStoneMaterial(Color dark, Color light, float uvScale, int seed)
    {
        var (albedo, normal) = GetStoneTextures(dark, light, seed);
        return new StandardMaterial3D
        {
            AlbedoTexture = albedo,
            NormalEnabled = true,
            NormalScale = 0.6f,
            NormalTexture = normal,
            Roughness = 0.88f,
            Uv1Triplanar = true,
            Uv1Scale = Vector3.One * uvScale
        };
    }

    private static (ImageTexture Albedo, ImageTexture Normal) GetStoneTextures(Color dark, Color light, int seed)
    {
        if (StoneTextureCache.TryGetValue(seed, out var cached))
            return cached;

        const int size = 128;
        var noise = new FastNoiseLite { NoiseType = FastNoiseLite.NoiseTypeEnum.Cellular, Seed = seed, Frequency = 0.06f };
        var height = noise.GetSeamlessImage(size, size);
        height.Convert(Image.Format.Rgba8);

        var albedo = Image.CreateEmpty(size, size, true, Image.Format.Rgba8);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
                albedo.SetPixel(x, y, dark.Lerp(light, height.GetPixel(x, y).R));
        }
        albedo.GenerateMipmaps();

        var normal = (Image)height.Duplicate();
        normal.BumpMapToNormalMap(6.0f);
        normal.GenerateMipmaps();

        cached = (ImageTexture.CreateFromImage(albedo), ImageTexture.CreateFromImage(normal));
        StoneTextureCache[seed] = cached;
        return cached;
    }

    private void AddWallTorchModel(PackedScene torchScene, Vector3 wallPosition, Vector2I wallSide, int index)
    {
        var torch = torchScene.Instantiate<Node3D>();
        torch.Name = $"WallTorch_{index}";
        torch.Position = wallPosition + Vector3.Up * 1.2f;
        torch.Rotation = new Vector3(0.0f, Mathf.Atan2(-wallSide.X, -wallSide.Y), 0.0f);
        AddChild(torch);

        if (!CrowsNestCrypt.TryGetVisualBounds(torch, out var minimum, out var maximum))
            return;
        var size = maximum - minimum;
        var largest = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
        if (largest > 0.001f)
            torch.Scale *= 1.1f / largest;
    }

    private void AddTorchLights()
    {
        const int maxLights = 8;
        const float minSpacing = 4.5f;
        var placed = new List<Vector3>();
        var torchScene = ResourceLoader.Load<PackedScene>(WallTorchPath);
        var candidates = new List<Vector2I>(_walkableCells);
        // Ordem pseudoaleatoria estavel para espalhar as tochas pela sala inteira.
        candidates.Sort((a, b) => (unchecked(a.X * 73856093 ^ a.Y * 19349663) & int.MaxValue).CompareTo(unchecked(b.X * 73856093 ^ b.Y * 19349663) & int.MaxValue));

        var exitLine = ExitLineWorld();
        foreach (var cell in candidates)
        {
            if (placed.Count >= maxLights)
                break;

            // Sem tocha perto das portas da parede de saída (a moldura e o ícone precisam da parede livre).
            if (HasExitOnWall)
            {
                var toExit = CellToWorld(cell) - exitLine;
                toExit.Y = 0.0f;
                if (toExit.Length() < 2.6f)
                    continue;
            }

            var wallSide = Vector2I.Zero;
            foreach (var dir in new[] { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right })
            {
                if (!_walkableCells.Contains(cell + dir))
                {
                    wallSide = dir;
                    break;
                }
            }
            if (wallSide == Vector2I.Zero)
                continue;

            var position = CellToWorld(cell) + new Vector3(wallSide.X, 0.0f, wallSide.Y) * (CellSize * 0.32f) + Vector3.Up * 1.7f;
            var tooClose = false;
            foreach (var other in placed)
            {
                if (other.DistanceTo(position) < minSpacing)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose)
                continue;

            placed.Add(position);
            if (torchScene != null)
                AddWallTorchModel(torchScene, CellToWorld(cell) + new Vector3(wallSide.X, 0.0f, wallSide.Y) * (CellSize * 0.5f - 0.16f), wallSide, placed.Count);
            AddChild(new OmniLight3D
            {
                Name = $"TorchLight_{placed.Count}",
                Position = position,
                LightColor = new Color(1.0f, 0.55f, 0.28f),
                LightEnergy = 2.6f,
                OmniRange = 6.5f,
                OmniAttenuation = 1.4f,
                ShadowEnabled = false
            });
        }

        AddChild(new OmniLight3D
        {
            Name = "ExitGlowLight",
            Position = CellToWorld(_exitCell) + Vector3.Up * 1.4f,
            LightColor = new Color(0.95f, 0.12f, 0.1f),
            LightEnergy = 1.4f,
            OmniRange = 4.0f,
            ShadowEnabled = false
        });
    }
}
