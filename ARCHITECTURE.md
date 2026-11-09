# Shift & Sort — Architecture

Written 2026-11-10 against commit `2241067`, plus the uncommitted working tree. **All game code, art, prefabs and the
`Game.unity` scene are uncommitted at the time of writing.** `2241067` holds only the template project, `skeleton.md` and
the skills.
**This file is maintained by hand.** When you change a system, edit the matching section.
See `ARCHITECTURE-HISTORY.md` for what changed and when.

---

## 1  What this is

A portrait mobile puzzle game. The board is an N×N grid of tiles, and each tile has a **shape** and a **color**. Each
row has a shape target and each column has a color target. The player drags a whole row sideways or a whole column up
or down; tiles wrap around the edge. A cell is *matched* when its shape equals its row's target and its color equals
its column's target. The level is won when every cell matches.

Today the game runs the core loop only: procedural levels, hold-and-drag/flick input, win, auto-advance. There is no
UI, save, audio or JSON levels yet (§11).

The code follows the architecture blueprint in `skeleton.md` (ported from an earlier game, "Color Farm"). That file
describes the *target*; this file describes what exists.

---

## 2  Repository map

```
Assets/
  Game Assets/
    Scenes/Game.unity                 the only scene (in Build Settings, index 0)
    Scripts/
      Gameplay/
        Essentials/                   framework, game-agnostic
          Singleton.cs                Singleton<T>, ISingleton
          SingletonManager.cs         ordered boot
          ScriptableSingleton.cs      ScriptableSingleton<T>, [AssetPath]
        Managers/                     one scene singleton per system: rules + state
          Manager_Game.cs             state machine, drag rules, win/fail
          Manager_Board.cs            board model owner, all board views, animation, camera fit
          Manager_Input.cs            pointer → press/drag/release events
          Manager_Level.cs            level number → LevelDefinition
        GameElements/                 prefab views: render what they are handed
          Cell.cs  Tile.cs  TargetMarker.cs
        BoardState.cs                 pure board model (no Unity objects)
        LevelDefinition.cs            LevelDefinition + ILevelSource
        ProceduralLevelSource.cs      scramble-a-solved-board generator
        Common.cs                     enums, TileData, SortOrder
        Tuning.cs  GameTuning.cs      gameplay numbers (const defaults + scene overrides)
        FileRef.cs                    asset registry (ScriptableSingleton)
      Editor/
        AssetBuilder.cs               menu ShiftSort/Build Assets
        SceneBuilder.cs               menu ShiftSort/Build Scene
    Prefabs/Gameobjects/              Cell, Tile, TargetMarker (built by AssetBuilder)
    Prefabs/UI/                       empty (.gitkeep)
    Sprites/
      Sprites/                        delivered art: tiles/ targets/ fx/ board/ backgrounds/ branding/ ui/
      Placeholder/                    generated: Slot.png, Solid.png
      Previews/                       reference mockups (not used by code)
    Fonts/                            Open Sans (unused so far)
    Audio/ Materials/ Shaders/        empty (.gitkeep)
  Resources/
    FileRef.asset                     the asset registry, loaded by name at runtime
    Levels/                           empty (.gitkeep) — reserved for JSON levels
  Plugins/Demigiant/DOTween/          installed, not referenced by game code
  Settings/                           URP 2D renderer assets (template)
  InputSystem_Actions.inputactions    template, project-wide actions (unused by game code)
.agents/skills/, .claude/skills/      Unity agent skills (symlinks in .claude → .agents)
skeleton.md                           architecture blueprint (target, not current state)
CLAUDE.md                             agent instructions; points here for architecture
```

Scale: 19 C# files, ~1.7k lines (Editor ~480, Gameplay ~1.2k). One scene, 3 prefabs, 1 ScriptableObject, 16 tile + 8
target sprites wired.

---

## 3  The main flow

Boot → level → one drag → win → next level. Real names, in order.

```
Scene load (Game.unity)
 ├ GameTuning.OnEnable                      [order -1000, ExecuteAlways]  GameTuning.Active = this
 ├ SingletonManager.Awake                   [order -900]
 │   └ for each in _singletonsToInitialize (Inspector order):  ISingleton.Initialize() → Awake2()
 │       ├ Manager_Level.Awake2             FileRef.Instance (Resources.Load "FileRef")
 │       │                                   → new ProceduralLevelSource(PlayableShapes, PlayableColors)
 │       ├ Manager_Board                    (no Awake2)
 │       ├ Manager_Input                    (no Awake2)
 │       └ Manager_Game.Awake2              subscribes Manager_Input.OnPress/OnDrag/OnRelease/OnCancel
 │   └ Application.targetFrameRate = 60
 ├ (each Singleton's own Awake → Initialize() → no-op, already initialized)
 └ Manager_Game.Start
     └ StartLevel(1)
         ├ SetState(Loading)                 → Manager_Input.AcceptInput = false
         ├ Manager_Level.Load(1)             → ProceduralLevelSource.Load
         │    ├ size = clamp(MinBoardSize + (n-1)/LevelsPerSizeStep, …, min(MaxBoardSize, #shapes, #colors))
         │    ├ PickDistinct shapes/colors   (System.Random seeded n*7919+17)
         │    ├ BoardState.Solved → Scramble(size * ScrambleMovesPerSize swipes, never undo, never end solved)
         │    └ new LevelDefinition(..., moveLimit: 0)
         ├ Manager_Board.Build(level)
         │    ├ Clear()                       stop animation, destroy Cells/Tiles/Markers children
         │    ├ State = new BoardState(level)
         │    ├ scale Frame and Mask to the board
         │    ├ Instantiate FileRef.CellPrefab ×N², Tile ×N² (SpawnTile → Tile.Setup(data, GetTileSprite))
         │    ├ Instantiate TargetMarker ×N rows (GetRowTargetSprite) + ×N columns (GetColumnTargetSprite)
         │    ├ RefreshIndicators()           Cell.SetMatched, TargetMarker.SetComplete
         │    └ FitCamera()                   moves Camera.main + sets orthographicSize
         ├ MovesUsed = 0
         ├ SetState(Playing)                 → AcceptInput = true
         └ OnLevelStarted(1); log "[Game] Level 1 started (…)"

One drag (Manager_Input.Update reads Pointer.current each frame)
 ├ press   → OnPress(pos)       → Manager_Game.HandlePress
 │                                 _pressOnBoard = Playing && !board.IsAnimating && board.TryGetCell(pos)
 ├ move    → OnDrag(start, now) → Manager_Game.HandleDrag
 │                                 first time |delta| ≥ DragStartThreshold·min(screen):
 │                                   lock axis (|dx| ≥ |dy| → row) → board.BeginDrag(isRow, index)
 │                                 board.SetDragOffset(DragOffset())  → RenderHeldLine()
 └ release → OnRelease(start, end, secs) → Manager_Game.HandleRelease
                                   steps = StepsForRelease(offset, secs)
                                   ├ steps == 0 → board.CancelDrag()  → AnimateHeldLine(0, 0)
                                   └ else       → board.CommitDrag(steps)
                                                    ├ BoardState.ShiftRow/ShiftColumn(index, steps)   model now
                                                    └ AnimateHeldLine(steps, steps)                   views later
                                                  MovesUsed += |steps|; OnMoveMade()
                                                  EvaluateEndConditions()
                                                    ├ State.IsSolved              → SetState(Won)
                                                    └ limit && MovesUsed ≥ limit  → SetState(Failed)

AnimateHeldLine (coroutine, ShiftDuration × clamp(|distance|, 0.35, 1.5), ease-out cubic)
 └ on finish: reorder _tiles to match the model, snap positions, ReleaseHeldLine (destroy ghost),
              RefreshIndicators(), OnBoardSettled()

SetState(Won)    → AcceptInput = false; log; after WinAdvanceDelay → AdvanceLevel() → StartLevel(n+1)
SetState(Failed) → AcceptInput = false; log; after FailRestartDelay → RestartLevel() → StartLevel(n)
```

**The model changes at commit; the views catch up at the end of the animation.** The win is decided at commit, before
the slide has finished. `IsAnimating` blocks new presses (dropped, not queued) until the slide ends.

---

## 4  Modules

### 4.1  Essentials (framework)

| File | Owns | Calls | Called by |
|---|---|---|---|
| `Singleton.cs` | `Singleton<T>.Instance`; init exactly once per instance (`_initialized`); a duplicate component logs `[Singleton]` and `Destroy(this)` (the component, not the shared GameObject); `OnDestroy` clears `Instance` | `Awake2()` | Unity `Awake`, `SingletonManager` |
| `SingletonManager.cs` | Boot order (`_singletonsToInitialize`, Inspector-wired), `targetFrameRate = 60` | `ISingleton.Initialize()` | Unity (order −900) |
| `ScriptableSingleton.cs` | `ScriptableSingleton<T>.Instance` via `Resources.Load(<[AssetPath]>)`. **Editor only:** creates the asset if missing. | `Resources.Load`, `AssetDatabase.CreateAsset` | `FileRef.Instance` users |

`Singleton<T>` deliberately differs from the `skeleton.md` version: the guard there is a **static** flag, so a duplicate
skips its `Destroy`. Here it is per instance.

### 4.2  Managers (all on the `Managers` GameObject)

| Manager | Owns | Calls | Called by |
|---|---|---|---|
| `Manager_Game` | `GameState`, `CurrentLevel` (starts at 1, not saved), `Level`, `MovesUsed`; press/drag state (`_pressOnBoard`, `_lineHeld`, `_lineIsRow`); drag→steps rule; win/fail; auto advance/restart | `Manager_Level.Load`, `Manager_Board.*`, `Manager_Input.AcceptInput` | `Manager_Input` events; `Start` |
| `Manager_Board` | `BoardState State`; `Cell[,]`, `Tile[,]`, `TargetMarker[]`s; the held line (`_heldTiles`, `_offset`, `_ghost`, `_animation`); camera fit | `FileRef`, `Instantiate`, `BoardState.Shift*` | `Manager_Game` |
| `Manager_Input` | Press tracking; `AcceptInput` (setting false raises `OnCancel` if pressing) | `Pointer.current` (Input System) | `Update`; `Manager_Game` sets `AcceptInput` |
| `Manager_Level` | The `ILevelSource` (currently always `ProceduralLevelSource`) | `FileRef.PlayableShapes/Colors` | `Manager_Game.StartLevel` |

`Manager_Board`'s held-line API: `BeginDrag(isRow, index)` → `SetDragOffset(cells)` → `CommitDrag(steps)` | `CancelDrag()`.
`ShiftRow` / `ShiftColumn` wrap Begin+Commit for one-shot shifts (no in-game caller, §11).

**Line rendering** (`RenderHeldLine`): tile *i* is drawn at `p = mod(i + offset + 0.5, N) − 0.5`, i.e. wrapped into
`[−0.5, N−0.5)`. The one tile with `p > N−1` or `p < 0` is half off the board; a ghost `Tile` (a temporary prefab instance,
re-`Setup` when the crossing tile changes) is drawn at `p ∓ N` to show the half entering on the other edge.
`GamePlay/Board/Mask` (a `SpriteMask`) hides everything outside the grid; only tile renderers use
`VisibleInsideMask`.

### 4.3  Model and levels (no Unity objects except `Mathf`)

| File | Owns | Notes |
|---|---|---|
| `BoardState.cs` | `TileData[,]` cells `[row, col]`, `RowTargets`, `ColumnTargets`; `ShiftRow/ShiftColumn(index, dir)` (any integer dir); `IsCellMatched`, `IsRowComplete`, `IsColumnComplete`, `MatchedCount`, `IsSolved`; `Wrap` | Row 0 is the top. Constructors clone their inputs. |
| `LevelDefinition.cs` | `Number, Size, RowTargets, ColumnTargets, StartCells, MoveLimit` (0 = unlimited); `ILevelSource` | Plain class, no JSON yet. |
| `ProceduralLevelSource.cs` | Seeded generator (§3) | Every level is solvable (reachable from solved by inverse swipes). Same level number → same board. |

### 4.4  Views (GameElements)

| View | Shows | Serialized refs |
|---|---|---|
| `Cell` | Static slot + the "matched" badge (`SetMatched`). Matching is positional, so the badge is on the cell, not the tile. | `_background` (unused, §11), `_check` |
| `Tile` | One pre-rendered sprite (`Setup(data, sprite)`), keeps its `TileData` | `_body` |
| `TargetMarker` | Target icon (`Setup(sprite)`) + background tint idle/complete (`SetComplete`) | `_background`, `_icon`, `_idleColor`, `_completeColor` |

Views never read `FileRef` or any manager; `Manager_Board` hands them sprites.

### 4.5  Configuration

| File | Owns |
|---|---|
| `Tuning.cs` | `const` defaults + static read facade `Tuning.X` → `GameTuning.Active` or the default |
| `GameTuning.cs` | Inspector overrides on `Managers`; `[ExecuteAlways]`, order −1000. **The scene values ship**, not the consts. |
| `FileRef.cs` | Prefabs; `_tileSprites` (shape, color → sprite); `_rowTargetSprites` (shape → sprite); `_columnTargetSprites` (color → sprite); `PlayableShapes` / `PlayableColors`; context menu **Validate references** |
| `Common.cs` | `GameState`, `TileShape`, `TileColor`, `TileData`, `SortOrder` |

`PlayableShapes`: shapes with a row-target sprite **and** a tile sprite for every color that appears in the tile table and
has a column target. `PlayableColors`: colors with a column-target sprite and a tile for every playable shape. Today: 4
shapes (Circle, Square, Triangle, Star) × 4 colors (Coral, Teal, Yellow, Blue) → **max board 4×4**. `Diamond`, `Hexagon`,
`Purple` exist in the enums with no art and are never generated.

Current tuning (scene values):

| Field | Value | Meaning |
|---|---|---|
| CellSize | 1 | world units per cell |
| TileScale | 0.95 | tile sprite size in cells (art has transparent padding) |
| ShiftDuration | 0.15 | seconds per cell of animated travel (clamped 0.35–1.5 cells) |
| CameraMargin | 1.1 | ortho size multiplier around board + targets |
| FramePadding | 0.15 | frame border, cells per side |
| MarkerOffset | 0.65 | target centre distance from board edge, cells (+0.5 used in camera fit) |
| DragStartThreshold | 0.02 | fraction of the shorter screen side before a line follows the pointer |
| DragCommitThreshold | 0.5 | fraction of a cell the last partial step needs to count |
| FlickMaxDuration | 0.25 | release within this (s) moves ≥ 1 cell; 0 disables |
| ScrambleMovesPerSize | 2 | scramble swipes = size × this |
| LevelsPerSizeStep | 5 | levels 1–5 are 3×3, 6+ are 4×4 (capped by art) |
| Min/MaxBoardSize | 3 / 5 | max is further capped by playable shapes/colors (4) |
| WinAdvanceDelay / FailRestartDelay | 1 / 1 | seconds before auto advance / restart |

### 4.6  Editor builders

| Builder | Menu | Does | Never overwrites |
|---|---|---|---|
| `AssetBuilder` | ShiftSort/Build Assets | Writes `Placeholder/Slot.png` and `Solid.png` (SDF-generated, ppu 128); re-imports `Sprites/Sprites/{tiles,targets,fx}` as Single sprites with **ppu = texture width** (1 sprite = 1 world unit), no mips; builds Cell/Tile/TargetMarker prefabs; fills `FileRef` | an existing placeholder PNG, an existing prefab, a non-null prefab slot, a non-empty table |
| `SceneBuilder` | ShiftSort/Build Scene | Requires `Game.unity` active. Adds `GameTuning`, `SingletonManager`, the 4 managers; fills `_singletonsToInitialize` (Level, Board, Input, Game) if empty; creates `GamePlay/Board/{Frame, Mask, Cells, Tiles, Markers}` and wires `Manager_Board` refs if empty; camera ortho + cream background; `Canvas/Container/{InGame, PopUps}`, `Dark Area` (inactive, black 70%), `Loading Screen`, `Transition Screen`; `EventSystem` with `InputSystemUIInputModule` | existing objects (matched by name/path), filled refs, a non-empty init list |

Because builders skip existing assets, **changing a builder does not update an asset that already exists.** Edit the
prefab, or delete it and rebuild (§10).

---

## 5  Cross-cutting patterns

- **Managers decide, views display.** Views have no game logic and no access to managers or `FileRef`.
- **Ordered init.** Put setup in `Awake2()`. A manager that is not in `_singletonsToInitialize` still initializes from its
  own `Awake`, but in arbitrary order.
- **Events default to `delegate { }`.** Today nothing subscribes to `OnStateChanged`, `OnLevelStarted`, `OnMoveMade` or
  `OnBoardSettled` (they exist for the UI). Only `Manager_Input`'s four events have a subscriber (`Manager_Game`).
- **Numbers in `Tuning`, assets in `FileRef`.** No literals in managers, except the generator seed constants and the
  animation easing.
- **Re-runnable Editor builders** instead of hand-edited scene/prefab YAML.
- **No pooling.** Cells, tiles and markers are destroyed and re-instantiated on every `Build`; one ghost tile is
  created per drag.

---

## 6  Data formats and conventions

### 6.1  Directions and coordinates (the convention most likely to be broken)

| Where | Convention |
|---|---|
| `BoardState` | cells `[row, col]`; row 0 = top; `ShiftRow` dir + = right; `ShiftColumn` dir + = **down** |
| `Manager_Board` | board-local: centred on `_boardRoot`, x right, y up; `CellPosition(r, c) = ((c − (N−1)/2)·cell, ((N−1)/2 − r)·cell)`; held-line offset row + = right, column + = down |
| `Manager_Game.DragOffset` | `isRow ? cells.x : −cells.y` (**screen up = toward row 0 = negative column offset**) |
| `TryGetCell` | inverse of `CellPosition`: `col = floor((x + N·cell/2)/cell)`, `row = floor((N·cell/2 − y)/cell)` |

### 6.2  Serialized by ordinal — append only

`TileShape` and `TileColor` are written into `FileRef.asset` as integers (`Shape: 0`, `Color: 1`). `GameState` is not
serialized yet but is declared append-only. **Never renumber, reorder or reuse a value.** `TileColor.Red` was renamed
to `Coral` keeping ordinal 0 (safe: the name is not serialized).

### 6.3  Art file names → enums

`AssetBuilder` maps by **lower-cased enum name**: `tiles/tile_{shape}_{color}.png`, `targets/target_row_{shape}.png`,
`targets/target_column_{color}.png`. The enum name *is* the file-name contract: renaming `Coral` breaks the next
`FileRef` fill.

### 6.4  Rules implemented in more than one place

| Rule | Places | Risk |
|---|---|---|
| Cell ↔ board-local position | `Manager_Board.CellPosition` and its hand-written inverse in `TryGetCell` | Change the layout (gaps, offset, non-square) in one and taps hit the wrong cell. **Two sites.** |
| "Marker distance" | `Build` (`boardSize/2 + MarkerOffset·cell`) and `FitCamera` (`half + (MarkerOffset + 0.5)·cell`) | Move markers without updating the camera and they get clipped. **Two sites.** |
| Wrap modulo | `BoardState.Wrap` (int) and `Manager_Board.Mod` (float, used in `RenderHeldLine`) | Same maths, two implementations. |
| Column direction sign | `BoardState.ShiftColumn` (+ = down), `Manager_Board` offset (+ = down), `Manager_Game.DragOffset` (`−cells.y`) | Flip one and columns move against the finger. **Three sites.** |
| Shape/color equality per line | `IsCellMatched` vs `IsRowComplete` / `IsColumnComplete` | A rule change (e.g. wildcard tiles) must touch all three. |
| "Which shapes/colors have art" | `FileRef.PlayableShapes/Colors` (at runtime) and `AssetBuilder.FillFileRef` (file-name probe) | The builder only fills; playability is decided at runtime. Consistent, but two notions of "available". |

---

## 7  Assets → loaders

| Asset | Loaded by | How |
|---|---|---|
| `Resources/FileRef.asset` | `FileRef.Instance` (first use: `Manager_Level.Awake2`) | `Resources.Load<FileRef>("FileRef")` via `[AssetPath("FileRef")]`; Editor auto-creates if missing |
| `Prefabs/Gameobjects/Cell.prefab` | `Manager_Board.Build` | `Instantiate(FileRef.CellPrefab, _cellsRoot)` ×N² |
| `Prefabs/Gameobjects/Tile.prefab` | `Manager_Board.SpawnTile` | `Instantiate(FileRef.TilePrefab, _tilesRoot)` ×N² + 1 ghost per drag |
| `Prefabs/Gameobjects/TargetMarker.prefab` | `Manager_Board.SpawnMarker` | `Instantiate(FileRef.TargetMarkerPrefab, _markersRoot)` ×2N |
| `Sprites/Sprites/tiles/tile_*_*.png` (16) | `Manager_Board.SetupTile` | `FileRef.GetTileSprite(TileData)` |
| `Sprites/Sprites/targets/target_row_*.png` (4) | `Manager_Board.Build` | `FileRef.GetRowTargetSprite(shape)` |
| `Sprites/Sprites/targets/target_column_*.png` (4) | `Manager_Board.Build` | `FileRef.GetColumnTargetSprite(color)` |
| `Sprites/Sprites/fx/fx_matched_badge.png` | Cell prefab `Check` renderer | serialized in the prefab (scale 0.2, at (0.33, 0.33)) |
| `Sprites/Placeholder/Slot.png` | Cell `Background`, TargetMarker `Background`, scene `Board/Frame` | serialized |
| `Sprites/Placeholder/Solid.png` | scene `Board/Mask` (`SpriteMask`) | serialized |
| `Sprites/Sprites/{board, backgrounds, branding, ui}/*`, `fx/*` except the badge, `targets/target_complete_background.png` | **nothing** | delivered, not yet used |
| `Sprites/Previews/*.png` | nothing | reference mockups only |
| `Fonts/OpenSans-*.ttf` | nothing | for the coming UI |

Everything referenced by `FileRef` ships in the build, and so does everything under `Resources/`.

---

## 8  Wiring

**`Game.unity`**:

```
Main Camera            orthographic; position and size set at runtime by Manager_Board.FitCamera
Global Light 2D        template
Managers               GameTuning, SingletonManager, Manager_Level, Manager_Board, Manager_Input, Manager_Game
                         SingletonManager._singletonsToInitialize = [Level, Board, Input, Game]
                         Manager_Board → _boardRoot, _frame, _mask, _cellsRoot, _tilesRoot, _markersRoot
GamePlay
  Board                _boardRoot (runtime content parents below)
    Frame              SpriteRenderer (Slot, purple), scaled to N·cell + 2·FramePadding
    Mask               SpriteMask (Solid), scaled to N·cell
    Cells / Tiles / Markers   runtime-filled, emptied on each Build
Canvas                 Screen Space Overlay, CanvasScaler 1080×1920 match 0.5 — empty shell, no scripts
  Container/{InGame, PopUps}, Dark Area (inactive), Loading Screen, Transition Screen
EventSystem            EventSystem + InputSystemUIInputModule
```

**Names are load-bearing:** `SceneBuilder` finds objects by path, and CLI test snippets find `GamePlay/Board/Cells/Cell r,c`
(the name is set by `Cell.Setup`).

**Sorting orders** (`SortOrder`, all on the Default layer): Board 0 · Slot 1 · Marker 1 · MarkerIcon 2 · TileBody 4 ·
TileCheck 5. The badge (5) draws over tiles (4), and tiles over slots (1).

---

## 9  Environment

| | |
|---|---|
| Unity | 6000.3.17f1, URP 17.3 with the 2D Renderer (template `Universal 2D`) |
| Input | Input System 1.19 only (`activeInputHandler: 1`): legacy `UnityEngine.Input` throws |
| Packages of note | `com.unity.pipeline` 0.10.0-exp.1 (drives the Editor from the `unity` CLI), `com.unity.test-framework` 1.6 (no tests yet), uGUI 2.0 (includes TMP) |
| Plugins | DOTween (Demigiant) in `Assets/Plugins`; added the `DOTWEEN` define to `ProjectSettings` for all build targets |
| Target | portrait mobile (Canvas reference 1080×1920). No platform module configuration checked. |
| Assemblies | everything in `Assembly-CSharp` / `Assembly-CSharp-Editor`, global namespace, no asmdefs |

---

## 10  Working with it

The `unity` CLI drives the open Editor. Always pin the project:

```bash
P="--project-path C:/Project/Shift-Sort"
unity status $P                                        # Editor "ready"?
unity recompile $P --format json                       # errors in data.errors; a second call says "up_to_date"
unity command script.eval --code 'AssetBuilder.Build(); SceneBuilder.Build(); return "ok";' $P
unity command scene.get_hierarchy $P --format json
unity command console.get --tail 20 --level warning $P --format json
unity command editor.playmode.start $P                 # .stop to leave
unity command capture.game_view --width 540 --height 960 --output-path <file.png> $P
```

Gotchas:

- **Unfocused Editor = frozen Play mode.** Frames stop at 1–2, coroutines never finish, and `IsAnimating` sticks at true,
  so every press is rejected. Run `unity command editor.playmode.set_run_in_background --enabled true $P` first. This is
  a session override; it does not touch `ProjectSettings`.
- **Command names use dots** (`scene.get_hierarchy`, `editor.playmode.start`). The tag names (`scenes`, `editor/playmode`)
  are not commands.
- **Simulating input:** call the private `Manager_Game.HandlePress / HandleDrag / HandleRelease` by reflection, with
  screen positions from `Camera.main.WorldToScreenPoint(GameObject.Find("/GamePlay/Board/Cells/Cell r,c").position)`. One
  cell in pixels is the x distance between `Cell 0,1` and `Cell 0,0`. To test the win, call
  `Manager_Board.Build(new LevelDefinition(…))` with a board one move from solved.
- **Rebuilding a prefab:** the builders skip existing assets. Either edit the prefab
  (`PrefabUtility.LoadPrefabContents` → change → `SaveAsPrefabAsset` → `UnloadPrefabContents`), or delete it and run
  Build Assets. `FileRef` slots keep their old reference until cleared.
- **Refilling a `FileRef` table:** clear the array in the Inspector, then Build Assets.
- **`script.eval` after `UnloadPrefabContents`:** objects from the unloaded contents are destroyed. Read what you need
  before unloading, or you get `MissingReferenceException` on the return line, *after* the save has already happened.
- **Batch mode** (`Unity.exe -batchmode -runTests …`) refuses a project that is open in the Editor.

---

## 11  Known gaps

| Item | Kind | Detail |
|---|---|---|
| Nearly everything is uncommitted | state | Game code, scene, prefabs, art, `FileRef`, DOTween and this file are untracked or modified on top of `2241067`. |
| UI (HUD, popups, loading/transition) | never built | `Canvas` hierarchy exists with no scripts. No `Manager_PopUp`, `UI_*`, `Popup`/`BasePopUp`. The events for it exist (`OnStateChanged`, `OnLevelStarted`, `OnMoveMade`, `OnBoardSettled`) with no subscribers. |
| Save (`OfflineData` / `LiveData`) | never built | `CurrentLevel` always starts at 1. |
| JSON levels (`JsonLevelSource`, `Resources/Levels/`) | never built | `ILevelSource` exists; only `ProceduralLevelSource` is implemented. |
| Move limit | built, unused | `LevelDefinition.MoveLimit`, `HasMoveLimit`, `MovesLeft`, the `Failed` state and auto-restart all work, but every generated level passes `moveLimit: 0`. The `Failed` path has never been run. |
| Audio / haptics | never built | `Audio/` empty. |
| Delivered art not used | never built | board trays, background, branding, `fx` except the badge, `target_complete_background`, all `ui/`. The frame, slots and marker backgrounds still use the generated `Slot.png` with hard-coded tints in `AssetBuilder` / `SceneBuilder`. |
| DOTween | installed, unused | No `DG.Tweening` reference; slides are a coroutine. |
| Pooling | never built | Per-level destroy/instantiate; a ghost tile is instantiated per drag. |
| `Manager_Board.ShiftRow/ShiftColumn` | built, unreachable | Public one-shot wrappers; no caller in game code (CLI tests only). |
| `Cell._background`, `Cell.Row`, `Cell.Column` | built, unread | Assigned, never read. |
| `InputSystem_Actions.inputactions` | unused | Template asset; input reads `Pointer.current` directly. |
| Enum values `Diamond`, `Hexagon`, `Purple` | unused by design | No art; filtered out by `PlayableShapes/Colors`. |
| Steps that are a multiple of N | quirk | Dragging exactly N cells (or 2N…) leaves the board unchanged but still adds N to `MovesUsed`. |
| Flick back to the start | quirk | A quick release after the line was picked up but returned to offset 0 commits **+1** (sign of 0 is treated as +). |
| Presses during a slide | quirk | Dropped, not queued (0.05–0.2 s windows). |
| Window resize / aspect change | unverified | `FitCamera` runs only on `Build`; rotating or resizing the Game view mid-level does not refit. |
| Touch on a device | unverified | Drag, flick and win were verified in the Windows Editor (Play mode, simulated through `HandlePress/Drag/Release` with real screen coordinates). Not run with a mouse by hand or on Android/iOS touch. |
| Tests | never built | Test Framework installed, no test assembly. `BoardState` and `StepsForRelease` would be the first candidates. Game code has no asmdef, so a test asmdef cannot reference it yet. |
| `ProjectSettings.templateDefaultScene` | stale | Still `Assets/Scenes/SampleScene.unity` (moved). Harmless: it is template metadata only. |

---

## 12  Conventions

- **Naming:** `Manager_X` (scene singletons), `PopUp_X` / `UI_X` (reserved for UI), `[SerializeField] private _camelCase`
  fields exposed through expression-bodied properties, global namespace.
- **One MonoBehaviour per file**, file named after the class.
- **Comments:** short `//` lines explaining why, above the member. No `TODO`/`HACK` markers are in use yet.
- **Log tags** (grep these): `[Game]` (level start, complete, failed), `[FileRef]`, `[AssetBuilder]`, `[SceneBuilder]`,
  `[Singleton]`, `[SingletonManager]`, `[ScriptableSingleton]`.
- **Menus:** `ShiftSort/Build Assets`, `ShiftSort/Build Scene`.
