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

Today the game runs the core loop only: authored JSON levels (generated past the last file), hold-and-drag/flick input,
win, move budget/fail, undo, the in-game HUD (UI_InGame), music and sound effects, and a save (OfflineData: level
progress + sound switch). There are no popups yet (§11).

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
          SafeArea.cs                 fits a RectTransform to Screen.safeArea
        Managers/                     one scene singleton per system: rules + state
          Manager_Game.cs             state machine, drag rules, win/fail
          Manager_Board.cs            board model owner, all board views, animation, camera fit
          Manager_Input.cs            pointer → press/drag/release events
          Manager_Level.cs            level number → LevelDefinition
        GameElements/                 prefab views: render what they are handed
          Cell.cs  Tile.cs  TargetMarker.cs
        BoardState.cs                 pure board model (no Unity objects)
        LevelDefinition.cs            LevelDefinition + ILevelSource
        JsonLevelSource.cs            Resources/Levels/level-NNN.json → LevelDefinition; LevelFile/LevelMove
        ProceduralLevelSource.cs      scramble-a-solved-board generator (fallback)
        Common.cs                     enums (GameState, TileShape, TileColor, SoundType), TileData, SortOrder
        Tuning.cs  GameTuning.cs      gameplay numbers (const defaults + scene overrides)
        FileRef.cs                    asset registry (ScriptableSingleton)
        TestManager.cs                Inspector cheat panel on the Tester object (body is UNITY_EDITOR only)
      Sortify/                        [Button] attribute + inspector drawer (copied from Chicken Spin)
      Editor/
        AssetBuilder.cs               menu ShiftSort/Build Assets
        SceneBuilder.cs               menu ShiftSort/Build Scene
        LevelValidator.cs             menu ShiftSort/Validate Levels
    Prefabs/Gameobjects/              Cell, Tile, TargetMarker (built by AssetBuilder)
    Prefabs/UI/                       empty (.gitkeep)
    Sprites/
      Sprites/                        delivered art: tiles/ targets/ fx/ board/ backgrounds/ branding/ ui/
      Placeholder/                    generated: BoardFrame.png (512), Panel.png (256), Solid.png
      Previews/                       reference mockups (not used by code)
    Fonts/                            Open Sans TTFs + dynamic TMP font assets (OpenSans-Bold/ExtraBold SDF, made by UIBuilder)
    Audio/Music/ Audio/SFX/           music loop + sound effects (files named after SoundType, see §7)
    Materials/ Shaders/               empty (.gitkeep)
  Resources/
    FileRef.asset                     the asset registry, loaded by name at runtime
    Levels/level-001…012.json         authored levels (format in §7.1)
  Plugins/Demigiant/DOTween/          all animation and timed delays (see §5)
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
 │   ├ OfflineData.Load()                    once: PlayerPrefs "OfflineData" JSON → OfflineData.Instance (fresh if missing)
 │   └ for each in _singletonsToInitialize (Inspector order):  ISingleton.Initialize() → Awake2()
 │       ├ Manager_Level.Awake2             FileRef.Instance (Resources.Load "FileRef")
 │       │                                   → new JsonLevelSource + new ProceduralLevelSource(PlayableShapes, PlayableColors)
 │       ├ Manager_Board                    (no Awake2)
 │       ├ Manager_Input                    (no Awake2)
 │       └ Manager_Game.Awake2              subscribes Manager_Input.OnPress/OnDrag/OnRelease/OnCancel
 │   ├ Application.targetFrameRate = 60
 │   └ DOTween.SetTweensCapacity(500, 100)
 ├ (each Singleton's own Awake → Initialize() → no-op, already initialized)
 ├ UI_Loading.Awake                   → Manager_Game.HoldLaunch() (skipped if Loading Screen is inactive / Skip In Editor)
 ├ Manager_Game.Start                 → Launch() now, unless held
 └ UI_Loading.Start                   → DOTween sequence: bar fills (LoadingDuration, InOutSine) → Manager_Game.Launch()
                                        → fade out (LoadingFadeDuration) under which the board + HUD intros play → SetActive(false)
Manager_Game.Launch (once)
     └ StartLevel(OfflineData.CurrentLevel)       (skipped if the Tester already loaded a level)
         ├ SetState(Loading)                 → Manager_Input.AcceptInput = false
         ├ Manager_Level.Load(1)             → JsonLevelSource.Load (null if no file / invalid) ?? ProceduralLevelSource.Load
         │    JSON: Resources.Load<TextAsset>("Levels/level-001") → TryParse → targets = first N of RowShapes/ColumnColors
         │    Procedural:
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
                                   steps = StepsForRelease(offset, secs)   0 or ±1: offset is clamped to ±MaxStepsPerMove (1)
                                                                           in DragOffset, so the line never follows past one cell
                                   ├ steps == 0 → board.CancelDrag()  → AnimateHeldLine(0, 0)
                                   └ else       → board.CommitDrag(steps)
                                                    ├ BoardState.ShiftRow/ShiftColumn(index, steps)   model now
                                                    └ AnimateHeldLine(steps, steps)                   views later
                                                  MovesUsed += |steps|; OnMoveMade()
                                                  EvaluateEndConditions()
                                                    ├ State.IsSolved              → SetState(Won)
                                                    └ limit && MovesUsed ≥ limit  → SetState(Failed)

AnimateHeldLine (DOTween.To on _offset, ShiftDuration × clamp(|distance|, 0.35, 1.5), Ease.OutCubic, SetLink)
 └ OnComplete → SettleHeldLine: reorder _tiles to match the model, snap positions, ReleaseHeldLine (destroy ghost),
              RefreshIndicators(), OnBoardSettled()

SetState(Won)    → AcceptInput = false; log. UI_InGame shows the win panel + Next level → AdvanceLevel() → StartLevel(n+1)
SetState(Failed) → AcceptInput = false; log. UI_InGame shows the fail panel; Undo → back to Playing, Restart → StartLevel(n)
Undo()           → pop _history, Manager_Board.Shift*(index, -steps), MovesUsed -= |steps|, SetState(Playing), OnMoveMade
```

**The model changes at commit; the views catch up at the end of the animation.** The win is decided at commit, before
the slide has finished. `IsAnimating` blocks new presses (dropped, not queued) until the slide ends.

---

## 4  Modules

### 4.1  Essentials (framework)

| File | Owns | Calls | Called by |
|---|---|---|---|
| `Singleton.cs` | `Singleton<T>.Instance`; init exactly once per instance (`_initialized`); a duplicate component logs `[Singleton]` and `Destroy(this)` (the component, not the shared GameObject); `OnDestroy` clears `Instance` | `Awake2()` | Unity `Awake`, `SingletonManager` |
| `Haptics.cs` | `Haptics.Play(HapticType)` → `CandyCoded.HapticFeedback.HapticFeedback.Light/Medium/HeavyFeedback()`. Always on. Behind `#if UNITY_ANDROID \|\| UNITY_IOS \|\| UNITY_EDITOR` because the package only compiles for those; a native exception disables it for the session with one warning. Editor-only `DebugLog` | package `xyz.candycoded.hapticfeedback` | `Manager_Board`, `Manager_Game`, `UI_InGame` |
| `OfflineData.cs` | The save: `CurrentLevel` (1-based, level to open), `PassedLevels` (highest won), `IsSoundOn`. Public fields, JSON in PlayerPrefs key `OfflineData`, append-only; `Load()` (repairs values < 1), `Save()`, `Clear()` (tester) | `PlayerPrefs`, `JsonUtility` | `SingletonManager.Awake` (Load, once); writers: `Manager_Game.SaveProgress` (levels), `Manager_Audio.SetSoundOn` (sound) |
| `SingletonManager.cs` | `OfflineData.Load()` once, then boot order (`_singletonsToInitialize`, Inspector-wired), `targetFrameRate = 60`, DOTween capacity | `ISingleton.Initialize()` | Unity (order −900) |
| `ScriptableSingleton.cs` | `ScriptableSingleton<T>.Instance` via `Resources.Load(<[AssetPath]>)`. **Editor only:** creates the asset if missing. | `Resources.Load`, `AssetDatabase.CreateAsset` | `FileRef.Instance` users |

`Singleton<T>` deliberately differs from the `skeleton.md` version: the guard there is a **static** flag, so a duplicate
skips its `Destroy`. Here it is per instance.

### 4.2  Managers (all on the `Managers` GameObject)

| Manager | Owns | Calls | Called by |
|---|---|---|---|
| `Manager_Game` | `GameState`, `CurrentLevel` (opens on `OfflineData.CurrentLevel` via `Launch()`, once: from `Start`, or from `UI_Loading` when it called `HoldLaunch()`; `HasLaunched`; on Won `SaveProgress()` writes CurrentLevel = max(saved, n+1) and PassedLevels = max(saved, n), so replaying an old level never moves progress back; `StartLevel` from the tester never saves), `Level`, `MovesUsed`; press/drag state (`_pressOnBoard`, `_lineHeld`, `_lineIsRow`); drag→steps rule; win/fail; undo history (`_history`, one entry per swipe; `CanUndo`, `Undo()` works from Playing, Won and Failed and refunds the moves). Advancing/restarting is the player's call (`AdvanceLevel`, `RestartLevel` from UI_InGame). `ApplyMove(isRow, index, steps)` is a move from code with the same counting and end check as a swipe (refused unless Playing and idle). Editor-only `DebugEndLevel(won)` | `Manager_Level.Load`, `Manager_Board.*`, `Manager_Input.AcceptInput` | `Manager_Input` events; `Start`; `UI_InGame` buttons; `TestManager` |
| `Manager_Board` | `BoardState State`; `Cell[,]`, `Tile[,]`, `TargetMarker[]`s; the held line (`_heldTiles`, `_offset`, `_ghost`, `_animation`); camera fit | `FileRef`, `Instantiate`, `BoardState.Shift*` | `Manager_Game` |
| `Manager_Input` | Press tracking; `AcceptInput` (setting false raises `OnCancel` if pressing) | `Pointer.current` (Input System) | `Update`; `Manager_Game` sets `AcceptInput` |
| `Manager_Level` | Two `ILevelSource`s: `JsonLevelSource` first, `ProceduralLevelSource` when it returns null | `FileRef.PlayableShapes/Colors` | `Manager_Game.StartLevel` |
| `Manager_Tutorial` | Levels 1-4 (`TutorialLessons.For(n)`: lesson title/body before and after the first swipe, `IdleHint`, `PulseMoves`). A level is **guided** when its file has `tutorial` `"row"`/`"column"`; the shown swipe is `solution[0]`. Guided: cues under `GamePlay/Board/Tutorial` (built per level: line + wrapping-tile outlines, dashed destination, arrow past the edge, pulsing outlines on the two targets the tile will match, a looping hand stopped on `OnLinePickedUp`), shown only while Playing with 0 moves (Undo brings them back); `AllowsLine` / `AllowsMove` (Manager_Game asks on pick-up and release: another line is not picked up, the wrong direction snaps back); on the win `IsConfirming` holds the "matched!" text for `TutorialConfirmation` after the settle, then the win card. `TryGetMessage` (UI_InGame), `OnChanged`, `OnPulseMoves` (first spent move, level 4). Idle hint (level 3): `Manager_Board.PulseTargets` every `TutorialIdleHint` s without a move | `Manager_Board` (`BoardRoot`, `CellPosition`, `GetRow/ColumnMarker`, `PulseTargets`), `FileRef.Tutorial*` sprites | `Manager_Game` (rules), `UI_InGame` (text, events) |
| `Manager_Audio` | Refs to the scene's `Sound/Music` (looping; its Volume = music level, never written by code) and `Sound/SFX` (`PlayOneShot(clip, SoundData.Volume)`; its Volume = master effects level) AudioSources; `SoundOn` (mutes both) + `OnSoundChanged`. `PlaySound(type, variant = -1)`: -1 = random clip. Editor-only `DebugLogSounds` (Tester: Log Sounds) | `FileRef.Music`, `FileRef.TryGetSound` | `Manager_Board.PlaySettleSounds`, `Manager_Game` (Restart, Undo, OutOfMoves), `UI_InGame` (UiTap, sound button) |

`Manager_Board` layout API: `GetWorldBounds(withTargets)` / `GetScreenRect(withTargets)` give the board's area (frame only, or
frame + row targets left + column targets top, which is what `FitCamera` fits; both share `GetLocalBounds`).
`OnLayoutChanged` fires at the end of `Build`. Scene-view gizmos: green = frame, yellow = with targets.
`UI_BoardArea` (`Canvas/Container/InGame/Board Area`) sizes its RectTransform to that screen rect (anchors as
unclamped fractions of the parent), so HUD placed around the board anchors to its edges.

**Haptics:** `Haptics.Play(HapticType)` (Essentials, static, always on) wraps the CandyCoded `HapticFeedback` package: one pulse
per event. Settle: Heavy if solved, Medium if a line completed, else Light. `Manager_Game`: OutOfMoves Heavy, Restart Light
(Undo gets its pulse from the slide's settle). `UI_InGame`: Next level and the sound button Light. No-op in the Editor;
the Tester's **Log Sounds & Haptics** logs each pulse.

**Sounds on settle** (`Manager_Board.PlaySettleFeedback`, committed moves only, undo included): `SwipeSettle`, then the best of
`LevelComplete` (board solved) > `LineComplete` (more complete rows + columns than at pick-up) > `TileMatch` (variant =
cells newly matched since pick-up, 1..4+ → clip 01..04). The pick-up snapshot is `SnapshotMatches()` in `BeginDrag`.
`OutOfMoves` plays from `Manager_Game.SetState(Failed)`, at commit.

**Loading screen** (`UI_Loading`, on `Canvas/Loading Screen`): full-screen `Tilewrap_Loading` art, a "LOADING" label and a
timed progress bar (the fill's `anchorMax.x`, starting as a dot). It holds `Manager_Game`'s launch, so nothing is built and
input stays off (`State` = Loading) until the bar is full; its art blocks HUD taps. The studio splash comes before it and is
not part of this script. The HUD's `_introItems` are hidden from `UI_InGame.Awake` and scale in on the first `OnLevelStarted`.

**Intro and spawn** (`Manager_Board.Build`, after `FitCamera` / `OnLayoutChanged` so both measure the board at full size):
`PlayIntro()` once per launch scales `_boardRoot` up (`_intro`; `Clear` completes it if a level changes mid-intro), then
`AnimateTileSpawn()` on every Build pops the level's tiles in a diagonal wave (the drag ghost is not animated), and returns the
wave's start so `RefreshIndicators(checkDelay)` can pop each check right after its own tile lands.
**Checks:** `Cell.SetMatched(matched, popDuration, delay)` pops a check in when it appears, hides it instantly, and ignores a
repeat of the current state, so a check already up never re-pops. `RefreshIndicators()` after a settle pops new checks at once.

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
| `LevelDefinition.cs` | `Number, Size, RowTargets, ColumnTargets, StartCells, MoveLimit` (0 = unlimited), `Name, Instruction, Tutorial` ("" for generated), `Solution` (empty for generated); `ILevelSource` (`Load` returns null when it has no such level) | Plain class. |
| `JsonLevelSource.cs` | Loads, validates and converts one level file (§7.1). `TryParse` is static so the validator shares it. Rejects levels needing shapes/colors without art. | Logs an error and returns null on a bad file, so the level is generated instead. |
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
| CameraMargin | 1.1 | multiplier around board + targets inside the band the camera fits it into |
| BoardIntroDelay / BoardIntroDuration | 0.22 / 0.45 | launch only: `_boardRoot` scales 0 → 1 (OutBack); `IsAnimating` is true meanwhile, so presses are refused |
| TileSpawnDuration / TileSpawnStagger | 0.28 / 0.045 | every `Build`: each tile scales 0 → its size (OutBack), delayed by (row + col) × stagger; on launch the wave starts when the board is half grown |
| CheckPopDuration | 0.22 | a matched check's scale-up (`Cell.SetMatched`, OutBack). On Build each check waits for its tile's spawn to end; in play it pops when the settle matches it |
| LoadingDuration / LoadingFadeDuration | 1.6 / 0.4 | loading bar fill time; loading screen fade-out, which starts as the level opens |
| UiIntroDelay / UiIntroDuration / UiIntroStagger | 0.1 / 0.35 / 0.06 | launch intro (after the loading screen): wait before the first HUD element, scale-up time per element, gap between elements |
| HudTopReserve / HudBottomReserve | 0.33 / 0.18 | fractions of the safe-area height kept free above / below the board for the HUD. `FitCamera` fits board + targets into the band between them. |
| FramePadding | 0.15 | frame border, cells per side |
| MarkerOffset | 0.65 | target centre distance from board edge, cells (+0.5 used in camera fit) |
| DragStartThreshold | 0.02 | fraction of the shorter screen side before a line follows the pointer |
| DragCommitThreshold | 0.5 | fraction of a cell the last partial step needs to count |
| FlickMaxDuration | 0.25 | release within this (s) moves ≥ 1 cell; 0 disables |
| ScrambleMovesPerSize | 2 | scramble swipes = size × this |
| LevelsPerSizeStep | 5 | levels 1–5 are 3×3, 6+ are 4×4 (capped by art) |
| Min/MaxBoardSize | 3 / 5 | max is further capped by playable shapes/colors (4) |

### 4.6  Editor builders

| Builder | Menu | Does | Never overwrites |
|---|---|---|---|
| `AssetBuilder` | ShiftSort/Build Assets | Writes `Placeholder/BoardFrame.png` (512 px, 128 px corner) and `Panel.png` (256 px, 64 px corner): SDF rounded boxes, ppu = width, 9-slice border = corner + 8, FullRect mesh, mipmaps; plus `Solid.png` (ppu 128). `ApplyPanel(renderer, size, cornerRadius)` shows Panel 9-sliced with a given corner (slots 0.94 / r 0.1, markers 0.8 / r 0.16); re-imports `Sprites/Sprites/{tiles,targets,fx}` as Single sprites with **ppu = texture width** (1 sprite = 1 world unit), no mips; builds Cell/Tile/TargetMarker prefabs; fills `FileRef` | an existing placeholder PNG, an existing prefab, a non-null prefab slot, a non-empty table |
| `SceneBuilder` | ShiftSort/Build Scene | Requires `Game.unity` active. Adds `GameTuning`, `SingletonManager`, the 4 managers; fills `_singletonsToInitialize` (Level, Board, Input, Game) if empty; creates `GamePlay/Board/{Frame, Mask, Cells, Tiles, Markers}` and wires `Manager_Board` refs if empty; camera ortho + cream background; `Canvas/Container/{InGame/Board Area (UI_BoardArea), PopUps}` (`SafeArea` on `Container`), `Dark Area` (inactive, black 70%), `Loading Screen`, `Transition Screen`; `EventSystem` with `InputSystemUIInputModule`; `Tester` (tag `EditorOnly`) with `TestManager` | existing objects (matched by name/path), filled refs, a non-empty init list |
| `UIBuilder` | ShiftSort/Build UI (also run by Build Scene) | Imports `ui/` and `branding/` as Single sprites, ppu 100, with 9-slice borders (buttons 64/40, pills 44/43, panels 52); creates dynamic TMP font assets `Fonts/OpenSans-{Bold,ExtraBold} SDF`; builds the HUD under `Canvas/Container/InGame` (Top Bar, Board Area/Pills, Bottom Stack) and wires every `UI_InGame` field | font assets that exist; the whole HUD once `InGame` has `UI_InGame` (delete Top Bar, Board Area/Pills, Bottom Stack and the component to rebuild) |
| `UIBuilder.BuildLoading` | ShiftSort/Build Loading Screen (also run by Build Scene) | Fills `Canvas/Loading Screen`: full-stretch, last sibling, `CanvasGroup` + `UI_Loading`, Art, Progress Bar (9-slice 12 px; fill 6 px), Fill, Label; wires `_progressFill` | the screen once it has `UI_Loading` (remove the component, `CanvasGroup` and `Art` to rebuild) |
| `BuildNaming` (+ `BuildNameSettings`) | ShiftSort/Build Name Settings | `[InitializeOnLoad]` registers `BuildPlayerWindow.RegisterBuildPlayerHandler`: an Android build from the Build window is saved as `[ProductName]_[bundleVersion]_[date]_[AdditionalInformation].apk/.aab` in the folder you picked (parts cleaned: spaces → '-', '_' and invalid characters dropped; empty info drops its '_'). Settings asset `Scripts/Editor/BuildNameSettings.asset` (info text, date format, live preview). Scripted builds: `BuildNaming.Apply(ref options)` | other platforms, Export Project |
| `LevelValidator` | ShiftSort/Validate Levels | For every `Resources/Levels/level-NNN.json`: parses it, checks it doesn't start solved, replays `solution` through `BoardState` and checks it solves within `moveBudget`; warns about numbering gaps. Run after adding or editing a level file. | nothing (read-only) |

Because builders skip existing assets, **changing a builder does not update an asset that already exists.** Edit the
prefab, or delete it and rebuild (§10).

---

## 5  Cross-cutting patterns

- **Managers decide, views display.** Views have no game logic and no access to managers or `FileRef`.
- **Ordered init.** Put setup in `Awake2()`. A manager that is not in `_singletonsToInitialize` still initializes from its
  own `Awake`, but in arbitrary order.
- **Events default to `delegate { }`.** Subscribed: `Manager_Input`'s four events (`Manager_Game`); `OnLevelStarted`, `OnStateChanged`, `OnMoveMade`, `OnBoardSettled` (`UI_InGame`); `OnLayoutChanged` (`UI_BoardArea`), `OnBoardSettled` (`TestManager`, during Play Solution).
- **Numbers in `Tuning`, assets in `FileRef`.** No literals in managers, except the generator seed constants and the
  animation easing (`Ease.OutCubic`). All animation and timed delays go through DOTween (no coroutines); every
  tween is `SetLink`ed to its owner and killed on `Clear` / `StartLevel`.
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
| `Audio/Music/*.wav` (first) | `Manager_Audio.Start` | `FileRef.Music`; imported Streaming / Vorbis by `AssetBuilder` |
| `Audio/SFX/{sound_type}[_NN].wav` | `Manager_Audio.PlaySound` | `FileRef._sounds` (`SoundData`: Type, Clips[], Volume), filled by `AssetBuilder` from the snake_case of each `SoundType` (`TileMatch` → `tile_match_01…04`); Decompress On Load, ADPCM, mono |
| `Sprites/Sprites/tiles/tile_*_*.png` (16) | `Manager_Board.SetupTile` | `FileRef.GetTileSprite(TileData)` |
| `Sprites/Sprites/targets/target_row_*.png` (4) | `Manager_Board.Build` | `FileRef.GetRowTargetSprite(shape)` |
| `Sprites/Sprites/targets/target_column_*.png` (4) | `Manager_Board.Build` | `FileRef.GetColumnTargetSprite(color)` |
| `Sprites/Sprites/fx/fx_matched_badge.png` | Cell prefab `Check` renderer | serialized in the prefab (scale 0.2, at (0.33, 0.33)) |
| `Sprites/Placeholder/BoardFrame.png` | scene `Board/Frame` | serialized; Sliced, sized by `Manager_Board.Build` (`_frame.size`, scale 1, corner 0.25) |
| `Sprites/Placeholder/Panel.png` | Cell `Background`, TargetMarker `Background` | serialized; Sliced via `AssetBuilder.ApplyPanel` |
| `Sprites/Placeholder/Solid.png` | scene `Board/Mask` (`SpriteMask`) | serialized |
| `Sprites/Sprites/{board, backgrounds, branding, ui}/*`, `fx/*` except the badge, `targets/target_complete_background.png` | **nothing** | delivered, not yet used |
| `Sprites/Previews/*.png` | nothing | reference mockups only |
| `Fonts/OpenSans-*.ttf` | nothing | for the coming UI |

Everything referenced by `FileRef` ships in the build, and so does everything under `Resources/`.

---

### 7.1  Level files

`Resources/Levels/level-NNN.json`, three-digit number = level number (level 1 = `level-001.json`). No index: level `n`
loads `level-{n:000}` directly, and a missing number falls back to a generated level, so play continues past the last file.

| Key | Meaning |
|---|---|
| `schemaVersion` | must be 1 |
| `id` | must equal the number in the file name |
| `size` | N (2..4). The targets are not stored: rows are the first N of `JsonLevelSource.RowShapes` (Circle, Square, Triangle, Star) and columns the first N of `ColumnColors` (Coral, Teal, Yellow, Blue). Those arrays are the file contract: append-only. |
| `board` | N² tile ids, row-major from the top-left. **A tile's id is the cell it belongs in when solved** (`id = row·N + col`), so the tile is `(RowShapes[id / N], ColumnColors[id % N])` and the solved board is `0, 1, 2, …`. Must be a permutation of `0..N²-1`. |
| `moveBudget` | → `MoveLimit`; 0 = unlimited |
| `name`, `instruction`, `tutorial` | → `LevelDefinition.Name / Instruction / Tutorial` (`Name`, `Instruction` are not shown; `Tutorial` drives `Manager_Tutorial`). `tutorial` is `""`, `"row"` or `"column"`. |
| `solution` | `{axis: "row"/"column", line, direction}`, direction +1 = right/down (same as `BoardState.Shift*`). Read by `LevelValidator` and the tester's Play Solution. |
| `verifiedMoves`, `minimumMoves` | ignored |

## 8  Wiring

**`Game.unity`**:

```
Main Camera            orthographic; position and size set at runtime by Manager_Board.FitCamera
Global Light 2D        template
Managers               GameTuning, SingletonManager, Manager_Audio, Manager_Level, Manager_Board, Manager_Input, Manager_Game, Manager_Tutorial
                       (init order: Audio, Level, Board, Input, Game, Tutorial)
Sound                  audio objects (SceneBuilder.BuildSound), wired into Manager_Audio._music / _sfx
  Music                AudioSource, loop, volume 0.45: set the background music level here
  SFX                  AudioSource: its volume is the master level for all effects
                         SingletonManager._singletonsToInitialize = [Level, Board, Input, Game]
                         Manager_Board → _boardRoot, _frame, _mask, _cellsRoot, _tilesRoot, _markersRoot
GamePlay
  Board                _boardRoot (runtime content parents below)
    Frame              SpriteRenderer (BoardFrame, purple, Sliced), size N·cell + 2·FramePadding (scale stays 1, so corners stay sharp)
    Mask               SpriteMask (Solid), scaled to N·cell
    Cells / Tiles / Markers   runtime-filled, emptied on each Build
Canvas                 Screen Space Overlay, CanvasScaler 1080×1920 match 0.5
  Container            SafeArea (anchors = Screen.safeArea, applied once in Awake)
    InGame             UI_InGame (HUD; every field wired by UIBuilder). On the first level start (once) its _introItems
                       scale 0 → 1 in order (DOScale, OutBack): Logo, Level, Sound, Matched, Moves, Message, Undo, Restart
      Top Bar          Logo, Level (TMP), Sound Button/Icon; anchored to the top of the safe area
      Board Area       UI_BoardArea: tracks the board's on-screen rect (OnLayoutChanged)
        Message Panel  Title, Body: 14 above the column targets, Board Area's width (goal, tutorial lesson, win, out of moves)
        Pills          Matched Pill, Moves Pill (child Warning: white pill mask, UI_InGame._movesWarning): above the message panel.
                       Moves left <= _lowMovesAt (2) while Playing: Warning breathes white <-> red, text red <-> white
                       (DOTween yoyo, GameTuning > HUD > MovesWarningBreath). Failed: solid red, white text. Otherwise hidden.
      Bottom Stack     pinned to the bottom of the safe area (anchor bottom, pivot bottom, y 40), grows upward;
                       UI_BoardArea (Horizontal Only) gives it the board's width. VerticalLayoutGroup + ContentSizeFitter:
                       Next Button (only when Won), Button Row (Undo Button, Restart Button)
    PopUps
 , Dark Area (inactive), Transition Screen
  Loading Screen       UI_Loading + CanvasGroup; last child of Canvas, full screen (outside the safe area). Built by UIBuilder.BuildLoading
    Art                Tilewrap_Loading, AspectRatioFitter Envelope Parent (9:16, crops overflow); raycast target (blocks the HUD)
      Progress Bar     506×25 sliced, anchored 1470 px from the art's top; Fill Area (inset 9 / 6.5) → Fill (UI_Loading._progressFill)
        Label          "LOADING", TMP OpenSans ExtraBold 30, spacing 30, #D4B9C5, just above the bar
EventSystem            EventSystem + InputSystemUIInputModule
Tester                 TestManager; tagged EditorOnly, so stripped from builds
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
| Packages | `xyz.candycoded.hapticfeedback` 1.0.3 from git (`CandyCoded/HapticFeedback.git#v1.0.3`; not on OpenUPM); its assemblies only include Android, iOS and Editor |
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

- **Unfocused Editor = frozen Play mode.** Frames stop at 1–2, tweens never finish, and `IsAnimating` sticks at true,
  so every press is rejected. Run `unity command editor.playmode.set_run_in_background --enabled true $P` first. This is
  a session override; it does not touch `ProjectSettings`.
- **The loading screen delays the level ~2 s** after Play starts (nothing is built and input is off until
  `Manager_Game.HasLaunched`). Wait for it, or tick `Skip In Editor` on `Canvas/Loading Screen` (UI_Loading). A domain
  reload on entering Play usually takes longer than the screen, so to inspect it, reactivate it and set the fill by hand.
- **Command names use dots** (`scene.get_hierarchy`, `editor.playmode.start`). The tag names (`scenes`, `editor/playmode`)
  are not commands.
- **Tester:** select `Tester` in Play mode for buttons: Load/Next/Previous/Restart Level, Force Win/Fail, Play Solution
  (restarts and plays the file's `solution` through `ApplyMove`, one move per `OnBoardSettled`), Shift Line, Log State,
  time scale, Validate Level Files. From the CLI, invoke its private button methods by reflection on
  `Object.FindFirstObjectByType<TestManager>()`. Buttons need parameterless methods; arguments are serialized fields.
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
| Popups, transition screen | never built | The HUD exists (`UI_InGame`) and the loading screen (`UI_Loading`); win/fail are shown in the HUD's message panel, not popups. No `Manager_PopUp`, `Popup`/`BasePopUp`; `PopUps`, `Dark Area`, `Transition Screen` are empty. The loading bar is timed, not tied to real loading. |
| Sound setting | one switch | `Manager_Audio.SoundOn` is saved (`OfflineData.IsSoundOn`). No separate music / SFX switches. Haptics have no setting (always on, by design). |
| Target markers vs mockups | differs | Markers still use the generated slot square with a tint; the mockups show small icons with no background and `target_complete_background` (green pill with check) when complete. |
| `LiveData` | not needed yet | `OfflineData` exists (§4.1); there is no session-only state worth a `LiveData` class. |
| Level text | data only | `Name`, `Instruction` reach `LevelDefinition` but nothing shows them (the tutorial copy is in `TutorialLessons`, keyed by level number); generated levels (13+) have no move limit and no text. |
| Tutorial pack extras | not built | `moves_left_first_swipe_ring` (a punch scale is used instead), reduced-motion static guide, dimming of the non-guided tiles, the "All N levels complete" final card. Lesson text shares the one message panel above the board (the mockups show a goal panel plus a lesson panel). |
| Delivered art not used | never built | board trays, background, `fx` except the badge, `target_complete_background`. The frame, slots and marker backgrounds use the generated `BoardFrame.png` / `Panel.png` with hard-coded tints in `AssetBuilder` / `SceneBuilder`. |
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
