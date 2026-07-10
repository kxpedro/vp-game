using Godot;
using System.Collections.Generic;

public partial class RoomGenerator : Node3D
{
    [Export] public PackedScene EnemyScene { get; set; }
    [Export] public int EnemyBlocksPerRoom { get; set; } = 10;
    [Export] public int EnemiesPerBlock { get; set; } = 5;
    [Export] public float EnemyBlockRadius { get; set; } = 1.05f;
    [Export] public float SpawnClearRadius { get; set; } = 3.0f;
    [Export] public int EnemySpawnWallPaddingCells { get; set; } = 2;
    [Export] public bool ShowLayoutDebugLabels { get; set; } = true;

    public Vector3 PlayerSpawnPosition { get; private set; }
    public Vector3 ExitPortalPosition { get; private set; }

    private const float CellSize = 2.05f;
    private const float FloorHeight = 0.22f;
    private const float WallHeight = 2.35f;
    private const int MainRoomScale = 2;
    private const int RegionSpacing = 2;

    private enum MapTemplate
    {
        BranchingKeep,
        LairWithVault,
        CrossDungeon,
        SplitHalls,
        WindingCrypt
    }

    private readonly RandomNumberGenerator _rng = new();
    private readonly HashSet<Vector2I> _walkableCells = new();
    private readonly HashSet<Vector2I> _roomCells = new();
    private readonly List<Vector2I> _enemySpawnCells = new();
    private readonly List<Vector2I> _usedEnemyBlockCells = new();
    private readonly List<Vector3> _reservedPositions = new();

    private Vector2I _entranceCell = new(0, 3);
    private Vector2I _exitCell = new(0, -4);
    private Vector2I _treasureCell = new(3, -2);
    private Vector2I _deadEndCell = new(-4, -2);

    private StandardMaterial3D _floorMaterial;
    private StandardMaterial3D _wallMaterial;
    private StandardMaterial3D _markerMaterial;

    public override void _Ready()
    {
        _rng.Seed = BuildRoomSeed();
        BuildMaterials();
        RemoveStaticArenaShell();
        BuildMap();
        ApplySceneAnchors();
        GenerateMapGeometry();
        GeneratePointsOfInterest();
        BuildEnemySpawnCells();
        GenerateEnemies();
    }

    public Vector3 GetArtifactSpawnPosition()
    {
        return CellToWorld(_treasureCell) + new Vector3(_rng.RandfRange(-0.55f, 0.55f), 0.15f, _rng.RandfRange(-0.55f, 0.55f));
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

        var template = (MapTemplate)_rng.RandiRange(0, 4);
        if (template == MapTemplate.BranchingKeep)
            BuildBranchingKeep();
        else if (template == MapTemplate.LairWithVault)
            BuildLairWithVault();
        else if (template == MapTemplate.CrossDungeon)
            BuildCrossDungeon();
        else if (template == MapTemplate.SplitHalls)
            BuildSplitHalls();
        else
            BuildWindingCrypt();

        RepairDisconnectedMapPieces();
    }

    private void BuildBranchingKeep()
    {
        _entranceCell = R(0, 4);
        _exitCell = R(0, -5);
        _treasureCell = R(5, -2);
        _deadEndCell = R(-5, -2);

        AddMainRoom(R(0, 2), 5, 3, "Comodo principal");
        AddMainRoom(R(0, -2), 5, 3, "Comodo principal");
        AddMainRoom(R(8, 2), 4, 3, "Ala leste");
        AddMainRoom(R(-8, 2), 4, 3, "Ala oeste");
        AddMainRoom(R(8, -5), 4, 3, "Ala inferior leste");
        AddMainRoom(R(-8, -5), 4, 3, "Ala inferior oeste");
        AddRoom(R(5, -2), 3, 3, "Tesouro");
        AddRoom(R(-5, -2), 3, 3, "Beco sem saida");
        AddRoom(R(0, -5), 3, 2, "Saida");
        ConnectCells(_entranceCell, R(0, 2), "Corredor entrada");
        ConnectCells(R(0, 1), R(0, -2), "Corredor central");
        ConnectCells(R(2, 2), R(8, 2), "Corredor ala");
        ConnectCells(R(-2, 2), R(-8, 2), "Corredor ala");
        ConnectCells(R(2, -2), R(8, -5), "Corredor diagonal");
        ConnectCells(R(-2, -2), R(-8, -5), "Corredor diagonal");
        ConnectCells(R(8, 1), R(8, -5), "Corredor vertical");
        ConnectCells(R(-8, 1), R(-8, -5), "Corredor vertical");
        ConnectCells(R(2, -2), _treasureCell, "Corredor tesouro");
        ConnectCells(R(-2, -2), _deadEndCell, "Corredor beco");
        ConnectCells(R(0, -3), _exitCell, "Corredor saida");
    }

    private void BuildLairWithVault()
    {
        _entranceCell = R(-4, 4);
        _exitCell = R(4, -5);
        _treasureCell = R(-5, -4);
        _deadEndCell = R(5, 3);

        AddMainRoom(R(-4, 3), 3, 3, "Comodo inicial");
        AddMainRoom(R(0, 1), 5, 4, "Sala central");
        AddMainRoom(R(4, -2), 4, 3, "Comodo principal");
        AddMainRoom(R(-9, 0), 4, 3, "Ala oeste");
        AddMainRoom(R(9, 2), 4, 3, "Ala leste");
        AddMainRoom(R(0, -7), 5, 3, "Ala inferior");
        AddRoom(R(-5, -4), 3, 3, "Tesouro");
        AddRoom(R(5, 3), 3, 2, "Beco sem saida");
        ConnectCells(_entranceCell, R(-1, 2), "Corredor entrada");
        ConnectCells(R(1, 0), R(4, -2), "Corredor principal");
        ConnectCells(R(-3, 1), R(-9, 0), "Corredor oeste");
        ConnectCells(R(4, 1), R(9, 2), "Corredor leste");
        ConnectCells(R(4, -3), R(0, -7), "Corredor inferior");
        ConnectCells(R(-9, -1), R(-5, -4), "Corredor tesouro");
        ConnectCells(R(4, -3), _exitCell, "Corredor saida");
        ConnectCells(R(-2, 0), _treasureCell, "Corredor tesouro");
        ConnectCells(R(2, 2), _deadEndCell, "Corredor beco");
    }

    private void BuildCrossDungeon()
    {
        _entranceCell = R(0, 5);
        _exitCell = R(0, -5);
        _treasureCell = R(6, 0);
        _deadEndCell = R(-6, 0);

        AddMainRoom(R(0, 0), 5, 5, "Sala central");
        AddMainRoom(R(0, 9), 5, 3, "Ala norte");
        AddMainRoom(R(0, -9), 5, 3, "Ala sul");
        AddMainRoom(R(9, 0), 4, 4, "Ala leste");
        AddMainRoom(R(-9, 0), 4, 4, "Ala oeste");
        AddRoom(R(0, 5), 3, 2, "Entrada");
        AddRoom(R(0, -5), 3, 2, "Saida");
        AddRoom(_treasureCell, 3, 3, "Tesouro");
        AddRoom(_deadEndCell, 3, 3, "Beco sem saida");
        ConnectCells(_entranceCell, R(0, 2), "Corredor entrada");
        ConnectCells(R(0, -2), _exitCell, "Corredor saida");
        ConnectCells(R(0, 3), R(0, 9), "Corredor norte");
        ConnectCells(R(0, -3), R(0, -9), "Corredor sul");
        ConnectCells(R(3, 0), R(9, 0), "Corredor leste");
        ConnectCells(R(-3, 0), R(-9, 0), "Corredor oeste");
        ConnectCells(R(9, -2), R(0, -9), "Corredor lateral");
        ConnectCells(R(-9, 2), R(0, 9), "Corredor lateral");
        ConnectCells(R(2, 0), _treasureCell, "Corredor tesouro");
        ConnectCells(R(-2, 0), _deadEndCell, "Corredor beco");
    }

    private void BuildSplitHalls()
    {
        _entranceCell = R(0, 5);
        _exitCell = R(0, -5);
        _treasureCell = R(-5, -3);
        _deadEndCell = R(5, -3);

        AddMainRoom(R(0, 4), 5, 2, "Galeria entrada");
        AddMainRoom(R(-4, 0), 3, 5, "Ala oeste");
        AddMainRoom(R(4, 0), 3, 5, "Ala leste");
        AddMainRoom(R(0, -4), 5, 3, "Sala inferior");
        AddMainRoom(R(-10, 0), 4, 4, "Comodo oeste");
        AddMainRoom(R(10, 0), 4, 4, "Comodo leste");
        AddMainRoom(R(-5, -8), 4, 3, "Ala inferior oeste");
        AddMainRoom(R(5, -8), 4, 3, "Ala inferior leste");
        AddRoom(_treasureCell, 3, 2, "Tesouro");
        AddRoom(_deadEndCell, 3, 2, "Beco sem saida");
        ConnectCells(_entranceCell, R(-4, 2), "Corredor bifurcado");
        ConnectCells(_entranceCell, R(4, 2), "Corredor bifurcado");
        ConnectCells(R(-4, -2), R(0, -4), "Corredor oeste");
        ConnectCells(R(4, -2), R(0, -4), "Corredor leste");
        ConnectCells(R(-6, 0), R(-10, 0), "Corredor oeste");
        ConnectCells(R(6, 0), R(10, 0), "Corredor leste");
        ConnectCells(R(-4, -4), R(-5, -8), "Corredor inferior");
        ConnectCells(R(4, -4), R(5, -8), "Corredor inferior");
        ConnectCells(R(-5, -8), R(5, -8), "Corredor baixo");
        ConnectCells(R(0, -4), _exitCell, "Corredor saida");
        ConnectCells(R(-4, -2), _treasureCell, "Corredor tesouro");
        ConnectCells(R(4, -2), _deadEndCell, "Corredor beco");
    }

    private void BuildWindingCrypt()
    {
        _entranceCell = R(-5, 5);
        _exitCell = R(5, -5);
        _treasureCell = R(-5, -3);
        _deadEndCell = R(5, 3);

        AddMainRoom(R(-5, 5), 3, 2, "Entrada cripta");
        AddMainRoom(R(-2, 2), 4, 3, "Comodo principal");
        AddMainRoom(R(3, 1), 4, 3, "Ala sinuosa");
        AddMainRoom(R(0, -3), 5, 3, "Sala inferior");
        AddMainRoom(R(-9, 1), 4, 3, "Ala oeste");
        AddMainRoom(R(9, -1), 4, 3, "Ala leste");
        AddMainRoom(R(-4, -8), 4, 3, "Cripta inferior");
        AddMainRoom(R(7, -7), 4, 3, "Galeria final");
        AddRoom(R(5, -5), 3, 2, "Saida");
        AddRoom(_treasureCell, 3, 3, "Tesouro");
        AddRoom(_deadEndCell, 3, 2, "Beco sem saida");
        ConnectCells(_entranceCell, R(-2, 2), "Corredor entrada");
        ConnectCells(R(0, 2), R(3, 1), "Corredor sinuoso");
        ConnectCells(R(3, 0), R(0, -3), "Corredor descida");
        ConnectCells(R(-4, 2), R(-9, 1), "Corredor oeste");
        ConnectCells(R(5, 1), R(9, -1), "Corredor leste");
        ConnectCells(R(0, -4), R(-4, -8), "Corredor cripta");
        ConnectCells(R(3, -4), R(7, -7), "Corredor final");
        ConnectCells(R(-4, -8), R(7, -7), "Corredor baixo");
        ConnectCells(R(2, -3), _exitCell, "Corredor saida");
        ConnectCells(R(-2, -3), _treasureCell, "Corredor tesouro");
        ConnectCells(R(3, 1), _deadEndCell, "Corredor beco");
    }

    private void BuildBossMap()
    {
        _entranceCell = new Vector2I(0, 5);
        _exitCell = new Vector2I(0, -5);
        _treasureCell = new Vector2I(0, -3);
        _deadEndCell = new Vector2I(0, 0);

        AddMainRoom(new Vector2I(0, 0), 9, 9, "Arena do chefe");
        AddRoom(_entranceCell, 3, 2, "Entrada");
        AddRoom(_exitCell, 3, 2, "Portao final");
        ConnectCells(_entranceCell, new Vector2I(0, 3), "Corredor entrada");
        ConnectCells(new Vector2I(0, -3), _exitCell, "Corredor saida");
        RepairDisconnectedMapPieces();
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
            CreatePointOfInterest("Tesouro", CellToWorld(_treasureCell), new Color(0.85f, 0.62f, 0.12f));
            CreatePointOfInterest("Beco sem saida", CellToWorld(_deadEndCell), new Color(0.28f, 0.28f, 0.32f));
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

        if (RunState.IsBossRoom)
        {
            var boss = EnemyScene.Instantiate<EnemyBasic>();
            boss.Name = "BloodMageBoss";
            boss.Position = Vector3.Zero;
            AddChild(boss);
            return;
        }

        for (var blockIndex = 0; blockIndex < EnemyBlocksPerRoom; blockIndex++)
        {
            var blockCenter = PickEnemyBlockCenter();

            for (var enemyIndex = 0; enemyIndex < EnemiesPerBlock; enemyIndex++)
            {
                var enemy = EnemyScene.Instantiate<EnemyBasic>();
                enemy.Name = $"EnemyBlock{blockIndex + 1}_{enemyIndex + 1}";
                enemy.Position = PickPositionNearBlock(blockCenter);
                AddChild(enemy);
            }
        }
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
        if (cell == _entranceCell || cell == _exitCell || cell == _treasureCell || cell == _deadEndCell)
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
        _treasureCell = NearestWalkableCell(_treasureCell);
        _deadEndCell = NearestWalkableCell(_deadEndCell);

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

    private void AddRoom(Vector2I center, int width, int height, string labelText)
    {
        AddRoom(center, width, height);
        CreateLayoutDebugLabel(labelText, CellToWorld(center));
    }

    private void AddMainRoom(Vector2I center, int width, int height)
    {
        AddRoom(center, width * MainRoomScale, height * MainRoomScale);
    }

    private void AddMainRoom(Vector2I center, int width, int height, string labelText)
    {
        AddMainRoom(center, width, height);
        CreateLayoutDebugLabel(labelText, CellToWorld(center));
    }

    private void ConnectCells(Vector2I from, Vector2I to)
    {
        var path = BuildCorridorPath(from, to);
        CarveCorridor(path);
    }

    private void ConnectCells(Vector2I from, Vector2I to, string labelText)
    {
        var path = BuildCorridorPath(from, to);
        CarveCorridor(path);

        if (TryFindValidCorridorLabelCell(path, out var midpoint))
            CreateLayoutDebugLabel(labelText, CellToWorld(midpoint), 0.58f);
    }

    private List<Vector2I> BuildCorridorPath(Vector2I from, Vector2I to)
    {
        var current = from;
        var path = new List<Vector2I> { current };

        while (current.X != to.X)
        {
            current.X += current.X < to.X ? 1 : -1;
            path.Add(current);
        }

        while (current.Y != to.Y)
        {
            current.Y += current.Y < to.Y ? 1 : -1;
            path.Add(current);
        }

        return path;
    }

    private void CarveCorridor(List<Vector2I> path)
    {
        for (var i = 0; i < path.Count; i++)
        {
            var cell = path[i];
            _walkableCells.Add(cell);

            var direction = GetPathDirection(path, i);
            if (direction.X != 0)
                _walkableCells.Add(new Vector2I(cell.X, cell.Y + 1));
            else if (direction.Y != 0)
                _walkableCells.Add(new Vector2I(cell.X + 1, cell.Y));
        }
    }

    private void RepairDisconnectedMapPieces()
    {
        if (_walkableCells.Count == 0)
            return;

        _entranceCell = NearestWalkableCell(_entranceCell);
        var reachableCells = FindReachableCells(_entranceCell);
        var repairs = 0;

        while (reachableCells.Count < _walkableCells.Count)
        {
            var disconnectedPiece = FindDisconnectedPiece(reachableCells);
            if (disconnectedPiece.Count == 0)
                break;

            var bridge = FindClosestBridge(reachableCells, disconnectedPiece);
            ConnectCells(bridge.FromReachable, bridge.ToDisconnected);
            repairs++;

            reachableCells = FindReachableCells(_entranceCell);
        }

        if (repairs > 0)
            GD.Print($"RoomGenerator repaired {repairs} disconnected map piece(s).");
    }

    private HashSet<Vector2I> FindReachableCells(Vector2I startCell)
    {
        var reachableCells = new HashSet<Vector2I>();
        if (!_walkableCells.Contains(startCell))
            return reachableCells;

        var queue = new Queue<Vector2I>();
        reachableCells.Add(startCell);
        queue.Enqueue(startCell);

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var neighbor in GetCardinalNeighbors(cell))
            {
                if (!_walkableCells.Contains(neighbor) || reachableCells.Contains(neighbor))
                    continue;

                reachableCells.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return reachableCells;
    }

    private List<Vector2I> FindDisconnectedPiece(HashSet<Vector2I> reachableCells)
    {
        var piece = new List<Vector2I>();
        var startCell = Vector2I.Zero;
        var foundStart = false;

        foreach (var cell in _walkableCells)
        {
            if (reachableCells.Contains(cell))
                continue;

            startCell = cell;
            foundStart = true;
            break;
        }

        if (!foundStart)
            return piece;

        var visited = new HashSet<Vector2I> { startCell };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(startCell);

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            piece.Add(cell);

            foreach (var neighbor in GetCardinalNeighbors(cell))
            {
                if (!_walkableCells.Contains(neighbor) || reachableCells.Contains(neighbor) || visited.Contains(neighbor))
                    continue;

                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return piece;
    }

    private (Vector2I FromReachable, Vector2I ToDisconnected) FindClosestBridge(HashSet<Vector2I> reachableCells, List<Vector2I> disconnectedPiece)
    {
        var bestReachableCell = _entranceCell;
        var bestDisconnectedCell = disconnectedPiece[0];
        var bestDistance = int.MaxValue;

        foreach (var reachableCell in reachableCells)
        {
            foreach (var disconnectedCell in disconnectedPiece)
            {
                var distance = Mathf.Abs(reachableCell.X - disconnectedCell.X) + Mathf.Abs(reachableCell.Y - disconnectedCell.Y);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestReachableCell = reachableCell;
                bestDisconnectedCell = disconnectedCell;
            }
        }

        return (bestReachableCell, bestDisconnectedCell);
    }

    private IEnumerable<Vector2I> GetCardinalNeighbors(Vector2I cell)
    {
        yield return cell + Vector2I.Up;
        yield return cell + Vector2I.Down;
        yield return cell + Vector2I.Left;
        yield return cell + Vector2I.Right;
    }

    private Vector2I GetPathDirection(List<Vector2I> path, int index)
    {
        if (path.Count <= 1)
            return Vector2I.Zero;

        if (index < path.Count - 1)
            return path[index + 1] - path[index];

        return path[index] - path[index - 1];
    }

    private bool TryFindValidCorridorLabelCell(List<Vector2I> path, out Vector2I labelCell)
    {
        labelCell = path[path.Count / 2];

        if (path.Count < 3)
            return false;

        if (!IsMapPieceCell(path[0]) || !IsMapPieceCell(path[^1]))
            return false;

        var corridorOnlyCells = new List<Vector2I>();
        foreach (var cell in path)
        {
            if (!_roomCells.Contains(cell))
                corridorOnlyCells.Add(cell);
        }

        if (corridorOnlyCells.Count < 2)
            return false;

        labelCell = corridorOnlyCells[corridorOnlyCells.Count / 2];
        return true;
    }

    private bool IsMapPieceCell(Vector2I cell)
    {
        return _roomCells.Contains(cell)
            || cell == _entranceCell
            || cell == _exitCell
            || cell == _treasureCell
            || cell == _deadEndCell;
    }

    private Vector2I R(int x, int y)
    {
        return new Vector2I(x * RegionSpacing, y * RegionSpacing);
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
