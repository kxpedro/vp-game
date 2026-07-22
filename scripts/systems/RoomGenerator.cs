using Godot;
using System.Collections.Generic;

public partial class RoomGenerator : Node3D
{
    [Export] public PackedScene EnemyScene { get; set; }
    [Export] public int EnemyBlocksPerRoom { get; set; } = 2;
    [Export] public int EnemiesPerBlock { get; set; } = 5;
    [Export] public float EnemyBlockRadius { get; set; } = 1.05f;
    [Export] public float SpawnClearRadius { get; set; } = 3.0f;
    [Export] public int EnemySpawnWallPaddingCells { get; set; } = 2;
    [Export] public bool ShowLayoutDebugLabels { get; set; } = false;
    [Export] public float EnemySpawnDelaySeconds { get; set; } = 5.0f;
    [Export] public float WaveIntervalSeconds { get; set; } = 15.0f;
    [Export] public int EnemiesPerWave { get; set; } = 5;
    [Export] public int WaveCount { get; set; } = 3;

    public Vector3 PlayerSpawnPosition { get; private set; }
    public Vector3 ExitPortalPosition { get; private set; }
    public IReadOnlyCollection<Vector2I> WalkableCells => _walkableCells;
    public float MapCellSize => CellSize;

    private const float CellSize = 1.45f;
    private const float FloorHeight = 0.22f;
    private const float WallHeight = 2.35f;
    private const int RegionSpacing = 1;

    private readonly RandomNumberGenerator _rng = new();
    private readonly HashSet<Vector2I> _walkableCells = new();
    private readonly HashSet<Vector2I> _roomCells = new();
    private readonly List<Vector2I> _enemySpawnCells = new();
    private readonly List<Vector2I> _usedEnemyBlockCells = new();
    private readonly List<Vector3> _reservedPositions = new();

    private Vector2I _entranceCell = new(0, 3);
    private Vector2I _exitCell = new(0, -4);
    private Vector2I _rewardCell = new(0, -12);
    private Vector2I _focusCell = Vector2I.Zero;

    private StandardMaterial3D _floorMaterial;
    private StandardMaterial3D _wallMaterial;
    private StandardMaterial3D _markerMaterial;
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

    private void BuildMap()
    {
        _walkableCells.Clear();
        _roomCells.Clear();

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
    }

    private void GeneratePointsOfInterest()
    {
        CreatePointOfInterest("Entrada", CellToWorld(_entranceCell), new Color(0.35f, 0.35f, 0.38f));
        CreatePointOfInterest(RunState.IsBossRoom ? "Portao Final" : "Saida", CellToWorld(_exitCell), new Color(0.85f, 0.02f, 0.04f));

        if (!RunState.IsBossRoom)
        {
            CreatePointOfInterest("Camara central", CellToWorld(_focusCell), new Color(0.28f, 0.28f, 0.32f));
        }
        else
        {
            CreatePointOfInterest("Arena do Chefe", Vector3.Zero, new Color(0.85f, 0.02f, 0.04f));
        }
    }

    private void GenerateEnemies()
    {
        if (EnemyScene == null)
            return;

        _pendingEnemyWaves.Clear();
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
        SpawnNextWave();
    }

    private EnemyArchetype RollEnemyArchetype()
    {
        // Ranged used to be one third of the pool. Twenty percent fewer yields 26.67%,
        // with the freed weight shared evenly between melee and tank enemies.
        var roll = _rng.Randf();
        if (roll < 0.266667f)
            return EnemyArchetype.Ranged;
        return roll < 0.633333f ? EnemyArchetype.Melee : EnemyArchetype.Tank;
    }

    private Vector3 PickRandomWavePosition()
    {
        if (_enemySpawnCells.Count == 0)
            return Vector3.Zero;

        var best = Vector3.Zero;
        var bestClearance = -1.0f;
        for (var attempt = 0; attempt < 18; attempt++)
        {
            var cell = _enemySpawnCells[_rng.RandiRange(0, _enemySpawnCells.Count - 1)];
            var candidate = CellToWorld(cell) + new Vector3(_rng.RandfRange(-0.45f, 0.45f), 0, _rng.RandfRange(-0.45f, 0.45f));
            var clearance = float.MaxValue;
            foreach (var existing in _pendingEnemyWaves)
                clearance = Mathf.Min(clearance, candidate.DistanceTo(existing.Position));
            if (_pendingEnemyWaves.Count == 0 || clearance >= 2.35f)
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
        var wave = _pendingEnemyWaves.GetRange(_nextWaveEnemyIndex, Mathf.Min(EnemiesPerWave, _pendingEnemyWaves.Count - _nextWaveEnemyIndex));
        _nextWaveEnemyIndex += wave.Count;
        var markers = new List<Node3D>();
        foreach (var spawn in wave) markers.Add(CreateSpawnMarker(spawn.Position));
        GetTree().CallGroup("hud", "AppendGameLog", $"Wave com {wave.Count} inimigos chega em {EnemySpawnDelaySeconds:0} segundos.");
        GetTree().CallGroup("hud", "NotifyWaveIncoming", wave.Count, EnemySpawnDelaySeconds);
        await ToSignal(GetTree().CreateTimer(EnemySpawnDelaySeconds), SceneTreeTimer.SignalName.Timeout);

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
            enemy.SetSpawnActive(false);
            spawnedEnemies.Add(enemy);
            // Model instantiation is deliberately spread over frames to avoid a spawn hitch.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        foreach (var enemy in spawnedEnemies) if (IsInstanceValid(enemy)) enemy.SetSpawnActive(true);
        RemoveFromGroup("enemy_spawn_pending");
        if (_nextWaveEnemyIndex >= _pendingEnemyWaves.Count) RemoveFromGroup("enemy_waves_remaining");
        _isSpawningWave = false;
        _waveCountdown = Mathf.Max(WaveIntervalSeconds - EnemySpawnDelaySeconds, 0.1f);
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

    private Vector3 PickEnemyBlockCenter()
    {
        if (_enemySpawnCells.Count == 0)
            return Vector3.Zero;

        var cell = PickSpreadOutEnemyCell();
        _enemySpawnCells.Remove(cell);
        _usedEnemyBlockCells.Add(cell);
        return CellToWorld(cell);
    }

    private Vector2I PickSpreadOutEnemyCell()
    {
        if (_usedEnemyBlockCells.Count == 0)
            return PickBestFirstEnemyCell();

        var bestCell = _enemySpawnCells[0];
        var bestScore = -1.0f;

        foreach (var candidate in _enemySpawnCells)
        {
            var score = DistanceToClosestUsedBlock(candidate);
            score += GetSpawnClearanceScore(candidate) * 1.5f;
            score += _rng.RandfRange(0.0f, 0.35f);

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = candidate;
            }
        }

        return bestCell;
    }

    private Vector2I PickBestFirstEnemyCell()
    {
        var bestCell = _enemySpawnCells[0];
        var bestScore = -1.0f;

        foreach (var candidate in _enemySpawnCells)
        {
            var score = GetSpawnClearanceScore(candidate);
            score += candidate.DistanceSquaredTo(_entranceCell) * 0.01f;
            score += _rng.RandfRange(0.0f, 0.35f);

            if (score > bestScore)
            {
                bestScore = score;
                bestCell = candidate;
            }
        }

        return bestCell;
    }

    private float DistanceToClosestUsedBlock(Vector2I candidate)
    {
        var bestDistance = float.MaxValue;
        var candidatePosition = CellToWorld(candidate);

        foreach (var usedCell in _usedEnemyBlockCells)
        {
            var distance = candidatePosition.DistanceTo(CellToWorld(usedCell));
            if (distance < bestDistance)
                bestDistance = distance;
        }

        return bestDistance;
    }

    private Vector3 PickPositionNearBlock(Vector3 blockCenter)
    {
        for (var i = 0; i < 8; i++)
        {
            var angle = _rng.RandfRange(0.0f, Mathf.Tau);
            var radius = _rng.RandfRange(0.15f, EnemyBlockRadius);
            var candidate = blockCenter + new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);
            var cell = WorldToCell(candidate);

            if (_walkableCells.Contains(cell))
                return candidate;
        }

        return blockCenter;
    }

    private void BuildEnemySpawnCells()
    {
        _enemySpawnCells.Clear();

        AddEnemySpawnCellsWithPadding(EnemySpawnWallPaddingCells, true);

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
        var keep = Mathf.Max(Mathf.CeilToInt(_enemySpawnCells.Count * 0.62f), 8);
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

    private float GetSpawnClearanceScore(Vector2I cell)
    {
        var score = 0.0f;

        for (var radius = 1; radius <= 4; radius++)
        {
            if (!HasWalkablePadding(cell, radius))
                break;

            score += 1.0f;
        }

        if (_roomCells.Contains(cell))
            score += 2.0f;

        return score;
    }

    private void ApplySceneAnchors()
    {
        var playerSpawn = CellToWorld(_entranceCell);
        PlayerSpawnPosition = new Vector3(playerSpawn.X, 0.2f, playerSpawn.Z);
        var exitPortal = CellToWorld(_exitCell);
        ExitPortalPosition = new Vector3(exitPortal.X, 0.18f, exitPortal.Z);
        _rewardCell = NearestWalkableCell(_rewardCell);
        _focusCell = NearestWalkableCell(_focusCell);

        if (GetParent().GetNodeOrNull<Node3D>("Player") is { } player)
            player.Position = PlayerSpawnPosition;

        if (GetParent().GetNodeOrNull<Node3D>("ExitPortal") is { } portal)
            portal.Position = ExitPortalPosition;

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

        var collision = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(CellSize, FloorHeight, CellSize) }
        };
        body.AddChild(collision);

        var visual = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(CellSize, FloorHeight, CellSize) },
            MaterialOverride = _floorMaterial
        };
        body.AddChild(visual);
    }

    private void AddBoundaryWalls(Vector2I cell)
    {
        AddWallIfEmpty(cell, Vector2I.Up, new Vector3(0.0f, WallHeight * 0.5f, -CellSize * 0.5f), new Vector3(CellSize, WallHeight, 0.28f));
        AddWallIfEmpty(cell, Vector2I.Down, new Vector3(0.0f, WallHeight * 0.5f, CellSize * 0.5f), new Vector3(CellSize, WallHeight, 0.28f));
        AddWallIfEmpty(cell, Vector2I.Left, new Vector3(-CellSize * 0.5f, WallHeight * 0.5f, 0.0f), new Vector3(0.28f, WallHeight, CellSize));
        AddWallIfEmpty(cell, Vector2I.Right, new Vector3(CellSize * 0.5f, WallHeight * 0.5f, 0.0f), new Vector3(0.28f, WallHeight, CellSize));
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

    private void CreatePointOfInterest(string labelText, Vector3 position, Color color)
    {
        var marker = new MeshInstance3D
        {
            Name = $"{labelText.Replace(" ", "")}Marker",
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

        var label = new Label3D
        {
            Name = $"{labelText.Replace(" ", "")}Label",
            Text = labelText,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 30,
            Modulate = color,
            OutlineSize = 8,
            OutlineModulate = Colors.Black,
            Position = position + Vector3.Up * 0.75f
        };
        AddChild(label);
    }

    private void CreateLayoutDebugLabel(string labelText, Vector3 position, float height = 0.9f)
    {
        if (!ShowLayoutDebugLabels)
            return;

        var label = new Label3D
        {
            Name = $"{labelText.Replace(" ", "")}DebugLabel",
            Text = labelText,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 26,
            Modulate = Colors.White,
            OutlineSize = 8,
            OutlineModulate = Colors.Black,
            Position = position + Vector3.Up * height
        };
        AddChild(label);
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
        _floorMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.09f, 0.085f, 0.095f),
            Roughness = 0.9f
        };
        _wallMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.18f, 0.16f, 0.18f),
            Roughness = 0.92f
        };
        _markerMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.7f, 0.05f, 0.05f, 0.65f),
            EmissionEnabled = true,
            Emission = new Color(0.8f, 0.0f, 0.0f),
            EmissionEnergyMultiplier = 0.8f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
    }
}
