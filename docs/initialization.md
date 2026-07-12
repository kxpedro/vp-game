# Project Initialization

Snapshot of the current state of Vampire's Prey as of 2026-07-09.
Use this file as the first prompt anchor for future sessions.

## Game Pitch

Vampire's Prey is a 3D top-down 2.5D action roguelite.
The player is a vampire protagonist who runs dungeon rooms, collects blood coins,
finds artifacts, unlocks skills, and returns to the Crows Nest between runs.

The current tone is dark fantasy with a strong focus on combat, progression,
and room-by-room dungeon traversal.

## Core Loop

1. Start at the Crows Nest.
2. Leave through the portal into a run.
3. Clear 3, 25, and 35 procedurally generated rooms across three maps.
4. Collect blood coins and temporary artifacts during the run.
5. Defeat each map boss and continue directly to the next map.
6. Face the final boss in the fourth, boss-only map.
7. Return to the Crows Nest after victory or death; death preserves blood coins but clears temporary run progress.

## Current Run Structure

- Map definitions live in `scripts/systems/CampaignCatalog.cs`.
- The three procedural maps have 3, 25, and 35 common rooms.

## Saves and Main Menu

- The game starts in `scenes/ui/MainMenu.tscn`.
- Three save slots persist permanent progression only.
- Saved data includes attributes, blood coins, skills, permanent artifacts, loadouts, and weapon.
- Temporary run state is intentionally discarded when the application is closed.
- Every procedural map ends with its configured boss.
- The fourth map is reserved for Armand, the final boss.
- Room counts, map count, boss ids, special rules, and difficulty multipliers are data-driven.
- Rooms are procedurally generated as irregular map shapes, not simple rectangles.
- Each room currently spawns 10 enemy blocks with 5 enemies per block.
- Rooms can contain entrance, exit, treasure, dead ends, corridors, and large rooms.
- The generator has been iterated so disconnected pieces and orphan corridors were a concern and have been actively repaired.

## Key Scenes

- `scenes/game/CrowsNest.tscn`
- `scenes/game/TestArena.tscn`
- `scenes/player/Player.tscn`
- `scenes/enemies/EnemyBasic.tscn`
- `scenes/ui/HUD.tscn`

## Player Asset

- Player visual asset: `res://3d/character.glb`
- The model is already linked to the player scene.
- Imported animation names inside Godot differ from the raw GLB in a few cases.
- Some `_Loop` suffixes are removed by the import, for example:
  - `Rig|Walk_Loop` became `Rig|Walk`
  - `Rig|Sprint_Loop` became `Rig|Sprint`

Reference mapping lives in:

- `docs/character_asset.md`

## Player Systems

The player is a `CharacterBody3D` with these core scripts:

- `scripts/player/PlayerController.cs`
- `scripts/player/PlayerCombat.cs`
- `scripts/player/PlayerSkillController.cs`
- `scripts/player/PlayerAnimator.cs`

### Movement

- Movement uses isometric WASD direction.
- The player faces the movement direction, not the mouse.
- Sprint is allowed without stamina cost.
- Dodge is allowed without stamina cost.
- Dodge has a 2 second cooldown.
- The current movement tuning is adjustable in the pause menu.

### Combat

- Light and heavy attacks are implemented.
- Light attacks form a three-hit longsword combo; the third hit has stronger impact feedback.
- Damage is applied on the configured animation impact frame instead of immediately on input.
- Successful hits use short hit-stop, camera shake and archetype-sensitive enemy stagger.
- Left mouse performs the light combo and right mouse performs the heavy attack.
- Combat uses the current player damage multiplier and run damage multiplier.

### Room rewards

- Clearing a common room absorbs the defeated enemies' special blood and opens a choice of three Blood Blessings.
- Blessings are defined in `data/blessings/blood_blessings.json`, can stack up to `maxStacks`, and last until the run ends.
- Version 3 supports the Dracul, Bathory, Orlok and Karnstein families, boolean effects and effect parameters such as thresholds, durations, target limits and room cooldowns.
- Version 4 rolls every blessing in Common, Uncommon, Rare, Epic or Legendary quality. Numeric effect values use the editable `rarityScaling` table; boolean effects and timing parameters do not scale.
- Reactive effects are coordinated by `BloodBlessingRuntime`: overheal/barriers, blood pools, Pestilence and Charm all share the same data-driven effect totals.
- Boss rooms drop the artifact reward instead. Artifact effects use `bossDropPowerMultiplier` from `data/artifacts/artifacts.json`.
- A room-clear absorption sequence pulls each defeated enemy's blood essence into the player for at least five seconds before opening the offer.
- Each offer belongs to one random family and previously chosen blessings are excluded. After all 52 unique blessings are collected, excess special blood becomes Blood Coins.

### Global settings

- Resolution, fullscreen mode, music volume and effects volume are stored in `user://global_settings.json`.
- These settings are global and independent from the three progression save slots.
- Save slots can be deleted from the main menu.
- Attack timing was adjusted so the hit effect should line up better with the animation.
- The player has an attack area in front of the body.
- Combat reads its moveset from `WeaponCatalog`.
- The current combat is the Longsword moveset.
- Greatsword, Dual Revolvers, Whip, and Dual Blades are registered for future unique movesets.
- The equipped weapon is selected at the Crows Nest weapon bench and displayed in the HUD.

### Health and Resources

- Player health is managed by `scripts/combat/Health.cs`.
- There is no stamina cost for sprint or dodge anymore.
- Health, damage, movement speed, animation speed, and attack speed are now exposed to runtime tuning.

## Animation Notes

The player animation pipeline is currently:

- Idle
- Walk
- Run
- Dodge
- Light attack
- Heavy attack

Important implementation notes:

- The rig itself is correct and should be kept.
- The major animation issue was timing and synchronization, not rig setup.
- We observed that animation can appear to slide or flick when gameplay speed and animation playback are not matched.
- Dodge had been cutting before the clip ended.
- Attack effects had been appearing before the animation had finished.
- Current code uses animation length and playback speed tuning to better align those moments.

Current imported animation names used by the game:

- `Rig|Idle`
- `Rig|Walk`
- `Rig|Sprint`
- `Rig|Roll`
- `Rig|Sword_Attack`
- `Rig|Sword_Attack_RM`

## Camera

- Camera script: `scripts/camera/IsoCamera.cs`
- Camera is an isometric 3/4 top-down camera.
- It follows the player with smoothing.
- Camera distance is fixed; gameplay zoom was removed.

## HUD and Menus

The HUD is built entirely in code in `scripts/ui/HUD.cs`.

Current UI features:

- health, stamina, and fury bars
- blood coin counter
- room/enemy progress indicator
- enemy defeat toast
- room clear banner
- game over overlay with restart button
- pause overlay with restart button
- character sheet overlay
- blood fountain overlay
- artifact chest overlay
- skill tree interface
- fog-of-war minimap with a translucent full-screen TAB mode
- FPS counter, numeric health text, and red damage flash
- lower-right gameplay event log
- white normal damage, larger yellow critical damage, and red received-damage numbers

### Pause Tuning Panel

The pause menu now has a main page. Runtime tuning lives under the
`Parametros de gameplay` submenu instead of occupying the pause screen directly.

The `Opcoes` submenu controls window resolution, fullscreen mode, music volume,
and effects volume. Pausing suspends the music stream and resuming continues it
from the same playback position.

Current editable tuning values:

- walk speed
- run speed
- dash speed
- dash cooldown
- player damage
- max health
- animation speed
- attack speed

The Save button writes values to:

- `user://game_tuning.cfg`

Those values are loaded automatically at startup.

## Progression Systems

### Blood Coins

- Blood coins are accumulated during the run.
- They now persist across room transitions.
- Enemy kills grant 1 to 10 blood coins randomly.
- A cheat button in the blood fountain UI adds 500 coins for testing.

### Skills

- Skills are unlocked from the Blood Fountain in the Crows Nest.
- There are two skill trees:
  - Bestialis
  - Humanitas
- Skills are arranged as a hierarchy with prerequisites.
- Each tree uses icon-like square buttons.
- Right click on a skill button opens a popup with the description.
- The player can equip up to 4 active skills, mapped to keys `1`, `2`, `3`, `4`.
- `Q` and `R` are reserved for active artifacts later.

Current first skills:

- Bestialis: `Presas de Vampiro`
- Humanitas: `Instinto de Sobrevivencia`

### Artifacts

- Artifacts can spawn in rooms.
- Artifacts are first collected as temporary run artifacts.
- After defeating the final boss and returning successfully, temporary artifacts are committed permanently.
- The Crows Nest has an artifact chest to browse acquired artifacts.
- The player can equip artifacts for future runs.

## Run State

`scripts/systems/RunState.cs` currently tracks:

- current room
- damage multiplier
- max health bonus
- temporary run artifacts
- boss completion flow

`scripts/systems/CharacterSheet.cs` currently tracks:

- attributes
- blood coins
- acquired skills
- active skill slots
- found artifacts
- equipped artifact slots

## Enemy Behavior

Enemy behavior currently includes:

- patrol/routine behavior before detection
- line of sight detection
- chase behavior once the player is detected
- locked attack direction so attacks do not track the player mid-swing
- room clear notifications when enemies are defeated
- melee, ranged, and tank archetypes with distinct scale/color and behavior
- Normal, Magic, Rare, and Legendary rarity tiers driven by `EnemyTuningCatalog`
- aggressive rarity curve from 80/15/5/0 to 40/30/22/8 across each map
- collision recovery that turns enemies away from walls before resuming pursuit
- batched defeat rewards and capped potion drops to reduce simultaneous-death spikes
- regular enemy placeholders alternate between `zombie_commoner.glb` and `zombie_snapper.glb`
- map 1 boss placeholder uses `silver_dragonkin_mir4.glb`
- map 2 boss placeholder uses `invisible_women__marvel_rivals.glb`
- placeholder paths, scale, and idle animation live in `EnemyVisualCatalog`
- enemies telegraph their spawn positions for five seconds before appearing
- ranged enemies require an unobstructed shot and projectiles are removed on impact or room clear

Room templates now receive random rotation, mirroring, and two to five connected procedural
side rooms. This preserves connectivity while making repeated layouts less recognizable.

## Artifact Effects

- Artifact values remain data-driven in `data/artifacts/artifacts.json`.
- `ArtifactEffectRegistry` supplies player-facing names instead of internal identifiers.
- Passive aggregation supports flat and multiplier operations.
- Health, maximum health, damage, movement, attack speed, critical hits, life steal,
  execution, room costs, drop chance, and fatal protection are connected to runtime systems.
- Active effects include healing, area damage, slowing, fear, spectral damage,
  intangibility, and final-authority effects.

## Map Generation Notes

Room generation has been iterated heavily.

Current goals and constraints:

- rooms should feel like map layouts, not plain boxes
- large main chambers should dominate the map
- corridors should connect meaningful spaces
- dead ends and treasure branches should exist
- all pieces should stay connected
- isolated corridor fragments were a bug that got addressed repeatedly
- room labels were used while debugging the generation

Reference:

- `docs/room_generation.md`

## Known Design Decisions

- Isometric 2.5D presentation is the target.
- The gameplay should stay responsive and a bit arcade-like.
- Movement speed can be high, but animation timing must stay aligned.
- The current animation problem is timing and blend related, not rig related.
- The game should remain centered on runs, loot, and returning to the Crows Nest.

## Build and Verification

The project has been built and tested repeatedly with:

- `dotnet build`
- Godot debug runs on `TestArena.tscn`
- `jogar.bat` launches the project directly without opening the editor
- `exportar_windows.bat` uses `export_presets.cfg` to create a standalone Windows build

## Audio Context

- main menu: `audio/ambience/Main Menu Theme.mp3`
- Crows Nest: `audio/ambience/Hub.mp3`
- common rooms: `audio/ambience/Floor 1.mp3`
- boss rooms: `audio/boss/Floor 1 - Boss Theme.mp3`
- game over: `audio/ambience/Game Over.mp3`
- treasure collection, death, and boss victory use their matching effect tracks
- music is managed by the persistent `AudioManager` autoload and loops across scene changes

## Enemy Waves

- common rooms schedule three waves of 10 enemies, totaling 30 enemies
- each wave displays spawn telegraphs for five seconds
- the next wave begins only after the active wave is defeated
- room completion waits for every planned wave
- the HUD announces incoming waves and displays the current map/room

## Boss Framework

- bosses use `BossController` and `BossCatalog` instead of relying only on common-enemy tuning
- each boss defines a target duration, expected player DPS, health budget, and expandable moveset list
- the first boss currently has 5,000 HP, a five-minute target, and three phases
- a Dark Souls-style health bar appears at the top of the HUD
- boss music stops immediately when the boss is defeated

The current codebase is in a working state and compiles successfully.

## Useful Files To Open First

- `docs/initialization.md`
- `docs/room_generation.md`
- `docs/character_asset.md`
- `scripts/ui/HUD.cs`
- `scripts/player/PlayerController.cs`
- `scripts/player/PlayerCombat.cs`
- `scripts/player/PlayerAnimator.cs`
- `scripts/systems/GameTuning.cs`
- `scripts/systems/RunState.cs`
- `scripts/systems/CharacterSheet.cs`

## Prompt Seed

If you need to restart work in a future prompt, start from this:

```text
We are building Vampire's Prey, a 3D top-down 2.5D action roguelite in Godot.
The player is a vampire, runs rooms 1-5, fights a boss in room 6, and returns to the Crows Nest.
The project already has the player model linked, the combat loop, procedural rooms, skill trees, artifacts, and a code-built HUD.
Current focus is synchronization and tuning of movement, animation, dodge, and attack timing.
```
