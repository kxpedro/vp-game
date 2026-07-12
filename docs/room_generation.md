# Room Generation Spec

This document describes the current room generation structure for Vampire's Prey.

## Current Run Structure

- Map 1 has 3 common rooms and a boss.
- Map 2 has 25 common rooms and a boss.
- Map 3 has 35 common rooms and a boss.
- Map 4 contains only the final boss.
- Boss victories continue directly into the next map.
- `CampaignCatalog` owns map count, room count, boss id, special rule, and difficulty multiplier.
- Common rooms generate 10 enemy blocks.
- Each enemy block currently spawns 5 enemies.
- Artifacts spawn near the treasure point when a room rolls an artifact.

## Generation Model

Rooms are generated as grid-based maps, not rectangular arenas.

The generator stores walkable space as `HashSet<Vector2I> _walkableCells`.
Each cell becomes:

- one floor tile
- boundary walls only where neighboring cells are not walkable

This means map silhouettes are irregular and can form branches, corridors, vaults, dead ends, and connected chambers.

## Main Types

`MapTemplate`

Defines the high-level room layout family:

- `BranchingKeep`
- `LairWithVault`
- `CrossDungeon`
- `SplitHalls`
- `WindingCrypt`

`RunState`

Defines run progression:

- current map and room
- dynamic boss-room calculation from the current `MapDefinition`
- continuous transitions between maps
- `CurrentRoom`
- `IsBossRoom`
- temporary run stats such as damage and max health bonuses

`RoomGenerator`

Owns map construction:

- `BuildMap()`
- `AddRoom(center, width, height)`
- `AddMainRoom(center, width, height)`
- `ConnectCells(from, to)`
- `GenerateMapGeometry()`
- `GenerateEnemies()`
- `GetArtifactSpawnPosition()`

## Room Properties

Each template currently defines:

- entrance cell
- exit cell
- treasure cell
- dead-end cell
- main rooms
- side rooms
- corridor connections

Example:

```csharp
_entranceCell = new Vector2I(0, 4);
_exitCell = new Vector2I(0, -5);
_treasureCell = new Vector2I(5, -2);
_deadEndCell = new Vector2I(-5, -2);

AddMainRoom(new Vector2I(0, 2), 5, 3);
AddMainRoom(new Vector2I(0, -2), 5, 3);
AddRoom(new Vector2I(5, -2), 3, 3);
AddRoom(new Vector2I(-5, -2), 3, 3);
ConnectCells(new Vector2I(2, -2), _treasureCell);
```

## Scale Rules

`MainRoomScale = 3`

Main rooms use `AddMainRoom`, which multiplies width and height by 3.

Side rooms, treasure alcoves, exits, and dead ends still use `AddRoom` at normal scale.

## How To Specify A New Map Template

Use this format:

```text
Template name:
Entrance:
Exit:
Main rooms:
Side rooms:
Treasure:
Dead ends:
Connections:
Enemy block placement:
Special rules:
```

Example:

```text
Template name: Chapel Fork
Entrance: south center
Exit: north altar room
Main rooms: one large chapel nave, one side transept
Side rooms: two small confession rooms
Treasure: east side room
Dead ends: west confession room
Connections: entrance -> nave -> altar, nave -> east treasure, nave -> west dead end
Enemy block placement: 4 in nave, 3 near altar, 2 in transept, 1 guarding treasure
Special rules: center aisle must stay mostly open
```
