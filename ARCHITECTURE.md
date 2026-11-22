# Clue Parade (Tilewrap) — Architecture

Written 2026-11-10 against commit `2241067`, plus the uncommitted working tree. **All game code, art, prefabs and the
`Game.unity` scene are uncommitted at the time of writing.** `2241067` holds only the template project, `skeleton.md` and
the skills.
**This file is maintained by hand.** When you change a system, edit the matching section.
See `ARCHITECTURE-HISTORY.md` for what changed and when.

---

## 1  What this is

A portrait mobile puzzle game (**Clue Parade**, being redesigned from the earlier Shift & Sort; the core mechanics are
unchanged). The board is an N×N grid of item tiles: **columns are characters** (Mia, Leo, Zoe) and **rows are item
categories** (Hats, Gear, Bags). The player drags a whole row sideways or a whole column up or down, one cell per move;
tiles wrap around the edge. Every tile has one correct cell (its character and category), which the player works out
from **clues**. A level is played in **steps**, one row each: the active step's row is highlighted, its goal and clues
are shown, and it is *confirmed* (checks on its cells) once all its tiles are in place. The level is won when every step
is confirmed. Per-tile correctness is never shown or sounded, so the clues matter.

Today: authored JSON levels (one so far, played in a loop), hold-and-drag/flick input, steps, win, move budget/fail,
undo, an interim HUD (UI_InGame, the old Shift & Sort layout showing step, moves, goal and clues), music and sound
effects, and a save (OfflineData). The HUD still has to be rebuilt from the art pack's 1080×1920 layout (§11).

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
          Manager_Board.cs            board model owner, all board views (tiles, tags, checks, active row), animation, camera fit
          Manager_Input.cs            pointer → press/drag/release events
          Manager_Level.cs            level number → LevelDefinition (loops over the level files)
        GameElements/                 prefab views: render what they are handed
          Cell.cs  ItemTile.cs  CategoryTag.cs
        BoardState.cs                 pure board model (no Unity objects): shifts, home cells, steps
        LevelDefinition.cs            LevelDefinition + ILevelSource
        JsonLevelSource.cs            Resources/Levels/level-NNN.json → LevelDefinition; LevelFile/LevelStep/LevelClue/LevelMove
        ReferenceLayout.cs            the art pack's 1080×1920 positions in world units (board centre, frame, tags, checks)
        Common.cs                     enums (GameState, HapticType, SoundType), TileData, SortOrder
        Tuning.cs  GameTuning.cs      gameplay numbers (const defaults + scene overrides)
        FileRef.cs                    asset registry (ScriptableSingleton)
        TestManager.cs                Inspector cheat panel on the Tester object (body is UNITY_EDITOR only)
      Sortify/                        [Button] attribute + inspector drawer (copied from Chicken Spin)
      Editor/
        AssetBuilder.cs               menu ShiftSort/Build Assets
        SceneBuilder.cs               menu ShiftSort/Build Scene
        LevelValidator.cs             menu ShiftSort/Validate Levels
    Prefabs/Gameobjects/              Cell, ItemTile, CategoryTag (built by AssetBuilder)
    Prefabs/UI/                       empty (.gitkeep)
    Sprites/
      Tilewrap/                       Clue Parade art pack v1: Backgrounds/ Characters/ Items/ UI/ + art_manifest.json (import settings)
      Sprites/                        delivered art: tiles/ targets/ fx/ board/ backgrounds/ branding/ ui/
      Placeholder/                    generated: BoardFrame.png (512), Panel.png (256), Solid.png
      Previews/                       reference mockups (not used by code)
    Fonts/                            Open Sans TTFs + dynamic TMP font assets (OpenSans-Bold/ExtraBold SDF, made by UIBuilder)
    Audio/Music/ Audio/SFX/           music loop + sound effects (files named after SoundType, see §7)
    Materials/ Shaders/               empty (.gitkeep)
  Resources/
    FileRef.asset                     the asset registry, loaded by name at runtime
    Levels/level-001.json             authored levels (schemaVersion 2, format in §7.1)
  Plugins/Demigiant/DOTween/          all animation and timed delays (see §5)
  Settings/                           URP 2D renderer assets (template)
  InputSystem_Actions.inputactions    template, project-wide actions (unused by game code)
.agents/skills/, .claude/skills/      Unity agent skills (symlinks in .claude → .agents)
AgentScripts/                         CLI builder/check scripts run with `unity command script run` (outside Assets/, not compiled
                                      into the game): ItemTilePreview, PlayCheck (status, real swipes, hold/release, undo in
                                      Play mode), one-off migrations (RetireShiftSort, MigrateBoardScene); Out/ = captures
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
 │       ├ Manager_Level.Awake2             FileRef.Instance (Resources.Load "FileRef") → new JsonLevelSource(fileRef);
 │       │                                   LevelCount = files level-001… up to the first missing number
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
         ├ Manager_Level.Load(n)             → file ((n-1) % LevelCount) + 1 → JsonLevelSource.Load (null if invalid:
         │                                     StartLevel logs and stops, input stays off)
         │    Resources.Load<TextAsset>("Levels/level-001") → TryParse (§7.1) → TryCheckArt (items, characters in FileRef)
         ├ Manager_Board.Build(level)
         │    ├ Clear()                       stop animation, destroy Cells/Tiles/Tags children
         │    ├ State = new BoardState(level); _boardRoot at ReferenceLayout.BoardCenter
         │    ├ size Frame (grid + FramePadding), Mask (grid) and Active Row (one row)
         │    ├ Instantiate FileRef.CellPrefab ×N², ItemTile ×N² (SpawnTile → ItemTile.Setup(data, item sprite, name))
         │    ├ Instantiate CategoryTag ×N rows (level.Categories), left of the grid
         │    ├ FitCamera()                   the 10.8×19.2 reference screen fitted into the safe area
         │    └ RefreshIndicators()           checks on confirmed rows, honey surface + outline on the active row
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
                                                    ├ State.IsSolved (all steps)  → SetState(Won)
                                                    └ limit && MovesUsed ≥ limit  → SetState(Failed)

AnimateHeldLine (DOTween.To on _offset, ShiftDuration × clamp(|distance|, 0.35, 1.5), Ease.OutCubic, SetLink)
 └ OnComplete → SettleHeldLine: reorder _tiles to match the model, snap positions, ReleaseHeldLine (destroy ghost),
              RefreshIndicators(), PlaySettleFeedback(), OnBoardSettled()

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
| `Manager_Board` | `BoardState State`, `Level`; `Cell[,]`, `ItemTile[,]`, `CategoryTag[]`; the held line (`_heldTiles`, `_offset`, `_ghost`, `_animation`); step indicators (checks, surfaces, `_activeRow`); camera fit. `CellSize` = the ItemTile prefab's `Size` (cells are not square) | `FileRef`, `Instantiate`, `BoardState.Shift*` | `Manager_Game` |
| `Manager_Input` | Press tracking; `AcceptInput` (setting false raises `OnCancel` if pressing) | `Pointer.current` (Input System) | `Update`; `Manager_Game` sets `AcceptInput` |
| `Manager_Level` | `JsonLevelSource`; `LevelCount` (files from level-001 to the first gap); `FileNumber(n)` wraps any level number into 1..LevelCount, so play loops and old saves still open a level | `FileRef` | `Manager_Game.StartLevel` |
| `Manager_Audio` | Refs to the scene's `Sound/Music` (looping; its Volume = music level, never written by code) and `Sound/SFX` (`PlayOneShot(clip, SoundData.Volume)`; its Volume = master effects level) AudioSources; `SoundOn` (mutes both) + `OnSoundChanged`. `PlaySound(type, variant = -1)`: -1 = random clip. Editor-only `DebugLogSounds` (Tester: Log Sounds) | `FileRef.Music`, `FileRef.TryGetSound` | `Manager_Board.PlaySettleSounds`, `Manager_Game` (Restart, Undo, OutOfMoves), `UI_InGame` (UiTap, sound button) |

`Manager_Board` layout API: `GetWorldBounds(withTags)` / `GetScreenRect(withTags)` give the board's area (frame only, or
frame + category tags on the left; both share `GetLocalBounds`). `FitCamera` does not fit the board: it fits the art
pack's whole 10.8×19.2 reference screen (`ReferenceLayout`) uniformly into the safe area, world origin at its centre, so
the board sits where the pack's layout puts it and a HUD built on the same 1080×1920 layout lines up with it.
`OnLayoutChanged` fires at the end of `Build`. Scene-view gizmos: green = frame, yellow = with tags.
`UI_BoardArea` (`Canvas/Container/InGame/Board Area`) sizes its RectTransform to that screen rect (anchors as
unclamped fractions of the parent), so HUD placed around the board anchors to its edges.

**Haptics:** `Haptics.Play(HapticType)` (Essentials, static, always on) wraps the CandyCoded `HapticFeedback` package: one pulse
per event. Settle: Heavy if solved, Medium if a step was confirmed, else Light. `Manager_Game`: OutOfMoves Heavy, Restart Light
(Undo gets its pulse from the slide's settle). `UI_InGame`: Next level and the sound button Light. No-op in the Editor;
the Tester's **Log Sounds & Haptics** logs each pulse.

**Sounds on settle** (`Manager_Board.PlaySettleFeedback`, committed moves only, undo included): `SwipeSettle`, then
`LevelComplete` (solved) or `LineComplete` (more steps confirmed than at pick-up, `_stepsAtPickUp` from `BeginDrag`).
`TileMatch` is no longer played: a sound per tile reaching its cell would give the answer away.
`OutOfMoves` plays from `Manager_Game.SetState(Failed)`, at commit.

**Loading screen** (`UI_Loading`, on `Canvas/Loading Screen`): full-screen `Tilewrap_Loading` art, a "LOADING" label and a
timed progress bar (the fill's `anchorMax.x`, starting as a dot). It holds `Manager_Game`'s launch, so nothing is built and
input stays off (`State` = Loading) until the bar is full; its art blocks HUD taps. The studio splash comes before it and is
not part of this script. The HUD's `_introItems` are hidden from `UI_InGame.Awake` and scale in on the first `OnLevelStarted`.

**Intro and spawn** (`Manager_Board.Build`, after `FitCamera` / `OnLayoutChanged` so both measure the board at full size):
`PlayIntro()` once per launch scales `_boardRoot` up (`_intro`; `Clear` completes it if a level changes mid-intro), then
`AnimateTileSpawn()` on every Build pops the level's tiles in a diagonal wave (the drag ghost is not animated), and returns the
wave's start so `RefreshIndicators(checkDelay)` can pop each check right after its own tile lands.
**Steps on the board** (`RefreshIndicators`, after Build and every settle, all derived from `BoardState`): cells of a
confirmed row show checks; the tiles in the active step's row get the honey surface (`FileRef.TileSurfaceActive`, others
cream) and `Board/Active Row` outlines that row (hidden when solved). Picking a line up hides its checks until it settles.
**Checks:** `Cell.SetMatched(matched, popDuration, delay)` pops a check in when it appears, hides it instantly, and ignores a
repeat of the current state, so a check already up never re-pops. `RefreshIndicators()` after a settle pops new checks at once.

`Manager_Board`'s held-line API: `BeginDrag(isRow, index)` → `SetDragOffset(cells)` → `CommitDrag(steps)` | `CancelDrag()`.
`ShiftRow` / `ShiftColumn` wrap Begin+Commit for one-shot shifts (no in-game caller, §11).

**Line rendering** (`RenderHeldLine`): tile *i* is drawn at `p = mod(i + offset + 0.5, N) − 0.5`, i.e. wrapped into
`[−0.5, N−0.5)`. The one tile with `p > N−1` or `p < 0` is half off the board; a ghost `ItemTile` (a temporary prefab
instance, re-`Setup` when the crossing tile changes, wearing the crossing tile's surface) is drawn at `p ∓ N` to show the
half entering on the other edge. `GamePlay/Board/Mask` (a `SpriteMask`) hides everything outside the grid: the tile
sprites use `VisibleInsideMask`, and the tile names (world TextMeshPro, which ignores SpriteMask) use
`TilewrapNunito-ExtraBold SDF Masked.mat`, whose stencil test (ref 1, LessEqual) draws only where the mask wrote.

### 4.3  Model and levels (no Unity objects except `Mathf`)

| File | Owns | Notes |
|---|---|---|
| `BoardState.cs` | `TileData[,]` cells `[row, col]`, `StepRows`; `ShiftRow/ShiftColumn(index, dir)` (any integer dir); `IsHome(r, c)` (the cell holds the tile whose id is `r·N + c`), `IsRowComplete`; steps: `ConfirmedSteps` (leading steps whose rows are complete), `ActiveStep`, `ActiveRow` (−1 when solved), `IsRowConfirmed`, `IsSolved` (all steps confirmed); `Wrap` | Row 0 is the top. Constructors clone their inputs. **Progress is derived, never stored:** a move that breaks a confirmed row takes the level back to that step, and Undo repairs it. |
| `LevelDefinition.cs` | `Number` (the file's), `Size`, `StartCells`, `MoveLimit` (0 = unlimited), `Name`, `Characters` (column ids), `Categories` (row labels), `Items` (item id per home cell), `Steps` (`LevelStep`: row, goal, clues), `StepRows`, `ItemFor(tile)`, `Solution`; `ILevelSource` | Plain class. |
| `JsonLevelSource.cs` | Loads, validates and converts one level file (§7.1). `TryParse` (structure) and `TryCheckArt` (every item and character is in FileRef) are static so the validator shares them. `Exists(n)`. | Logs an error and returns null on a bad file. |

### 4.4  Views (GameElements)

| View | Shows | Serialized refs |
|---|---|---|
| `Cell` | A board position and the check of a confirmed row (`SetMatched`): positional, so on the cell, not the tile. `check_complete`, 0.58 units, top-right of the tile (`ReferenceLayout.CheckOffset`) | `_check` |
| `ItemTile` | Tile surface (sliced; `SetSurface` cream / honey), item art scaled to fit `_itemBox` (2.52×1.84, keeps aspect), name label (world `TextMeshPro`, Nunito ExtraBold, auto-size 2.4–3.8, masked material, 0.88 below the centre). `Setup(data, item, itemName)`; `Data`, `Surface`, `Size` (2.96×2.56 units = the pack's 296×256 px; **this is the board's cell size**) | `_surface`, `_item`, `_label`, `_size`, `_itemBox` |
| `CategoryTag` | The row label beside the board (`category_tag` sliced 1.22×0.88 + TMP label). `Setup(category)` | `_label` |

Views never read `FileRef` or any manager; `Manager_Board` hands them sprites and names.

### 4.5  Configuration

| File | Owns |
|---|---|
| `Tuning.cs` | `const` defaults + static read facade `Tuning.X` → `GameTuning.Active` or the default |
| `GameTuning.cs` | Inspector overrides on `Managers`; `[ExecuteAlways]`, order −1000. **The scene values ship**, not the consts. |
| `FileRef.cs` | Prefabs (Cell, ItemTile, CategoryTag); tile surfaces (`TileSurface` cream, `TileSurfaceActive` honey); the catalog: `_items` (`ItemData`: Id = art file name, Name shown on the tile, Sprite; `TryGetItem` / `GetItem`) and `_characters` (`CharacterData`: Id, Name, `Poses[k]` = pose after k confirmed steps; `TryGetCharacter`); music and sounds; context menu **Validate references** |
| `ReferenceLayout.cs` | The art pack's layout in world units: reference screen 10.8×19.2, `BoardCenter` (0.40, −2.44), `FramePadding` 0.26, tag offset/size, check offset/size. Art positions, not tuning. |
| `Common.cs` | `GameState`, `HapticType`, `SoundType`, `TileData` (`Id` = home cell, `HomeRow/HomeColumn`), `SortOrder` |

Item names are filled by **Build Assets** from a small table in `AssetBuilder.ItemNames` (names the file name can't give,
e.g. `hat_sunhat_purple` → "Sunhat"), else "{color} {thing}" (`bag_blue` → "Blue bag"); edit them in FileRef afterwards.

Current tuning (scene values):

| Field | Value | Meaning |
|---|---|---|
| ShiftDuration | 0.15 | seconds per cell of animated travel (clamped 0.35–1.5 cells) |
| BoardIntroDelay / BoardIntroDuration | 0.22 / 0.45 | launch only: `_boardRoot` scales 0 → 1 (OutBack); `IsAnimating` is true meanwhile, so presses are refused |
| TileSpawnDuration / TileSpawnStagger | 0.28 / 0.045 | every `Build`: each tile scales 0 → its size (OutBack), delayed by (row + col) × stagger; on launch the wave starts when the board is half grown |
| CheckPopDuration | 0.22 | a matched check's scale-up (`Cell.SetMatched`, OutBack). On Build each check waits for its tile's spawn to end; in play it pops when the settle matches it |
| LoadingDuration / LoadingFadeDuration | 1.6 / 0.4 | loading bar fill time; loading screen fade-out, which starts as the level opens |
| UiIntroDelay / UiIntroDuration / UiIntroStagger | 0.1 / 0.35 / 0.06 | launch intro (after the loading screen): wait before the first HUD element, scale-up time per element, gap between elements |
| DragStartThreshold | 0.02 | fraction of the shorter screen side before a line follows the pointer |
| DragCommitThreshold | 0.5 | fraction of a cell the last partial step needs to count |
| FlickMaxDuration | 0.25 | release within this (s) moves ≥ 1 cell; 0 disables |

### 4.6  Editor builders

| Builder | Menu | Does | Never overwrites |
|---|---|---|---|
| `AssetBuilder` | ShiftSort/Build Assets | Writes the placeholder `Solid.png` (board mask; also the unused Shift & Sort `BoardFrame.png` / `Panel.png`); imports `Sprites/Tilewrap/*` with each asset's PPU, pivot and 9-slice border from `art_manifest.json` (FullRect, no mips, uncompressed) and the old `Sprites/Sprites/{tiles,targets,fx}`; builds Cell / ItemTile / CategoryTag prefabs (+ `TilewrapNunito-ExtraBold SDF Masked.mat`); fills `FileRef` (prefabs, tile surfaces, `_items` with names from `ItemNames`, `_characters` with poses, music, sounds) | an existing placeholder PNG, an existing prefab, a non-null slot, a non-empty table |
| `SceneBuilder` | ShiftSort/Build Scene | Requires `Game.unity` active. Adds `GameTuning`, `SingletonManager`, the managers; fills `_singletonsToInitialize` (Audio, Level, Board, Input, Game) if empty; creates `GamePlay/Board/{Frame (board_frame), Mask, Active Row, Cells, Tiles, Tags}` and wires `Manager_Board` refs if empty; camera ortho + booth-wall background; `Canvas/Container/{InGame/Board Area (UI_BoardArea), PopUps}` (`SafeArea` on `Container`), `Dark Area` (inactive, black 70%), `Loading Screen`, `Transition Screen`; `EventSystem` with `InputSystemUIInputModule`; `Tester` (tag `EditorOnly`) with `TestManager` | existing objects (matched by name/path), filled refs, a non-empty init list |
| `UIBuilder` | ShiftSort/Build UI (also run by Build Scene) | Imports `ui/` and `branding/` as Single sprites, ppu 100, with 9-slice borders (buttons 64/40, pills 44/43, panels 52); creates dynamic TMP font assets `Fonts/OpenSans-{Bold,ExtraBold} SDF`; builds the HUD under `Canvas/Container/InGame` (Top Bar, Board Area/Pills, Bottom Stack) and wires every `UI_InGame` field | font assets that exist; the whole HUD once `InGame` has `UI_InGame` (delete Top Bar, Board Area/Pills, Bottom Stack and the component to rebuild) |
| `UIBuilder.BuildLoading` | ShiftSort/Build Loading Screen (also run by Build Scene) | Fills `Canvas/Loading Screen`: full-stretch, last sibling, `CanvasGroup` + `UI_Loading`, Art, Progress Bar (9-slice 12 px; fill 6 px), Fill, Label; wires `_progressFill` | the screen once it has `UI_Loading` (remove the component, `CanvasGroup` and `Art` to rebuild) |
| `BuildNaming` (+ `BuildNameSettings`) | ShiftSort/Build Name Settings | `[InitializeOnLoad]` registers `BuildPlayerWindow.RegisterBuildPlayerHandler`: an Android build from the Build window is saved as `[ProductName]_[bundleVersion]_[date]_[AdditionalInformation].apk/.aab` in the folder you picked (parts cleaned: spaces → '-', '_' and invalid characters dropped; empty info drops its '_'). Settings asset `Scripts/Editor/BuildNameSettings.asset` (info text, date format, live preview). Scripted builds: `BuildNaming.Apply(ref options)` | other platforms, Export Project |
| `LevelValidator` | ShiftSort/Validate Levels | For every `Resources/Levels/level-NNN.json`: parses it, checks every item and character is in `FileRef`, checks it doesn't start solved, replays `solution` through `BoardState` and checks it solves within `moveBudget`; warns about numbering gaps. Run after adding or editing a level file. | nothing (read-only) |

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
- **No pooling.** Cells, tiles and tags are destroyed and re-instantiated on every `Build`; one ghost tile is
  created per drag.

---

## 6  Data formats and conventions

### 6.1  Directions and coordinates (the convention most likely to be broken)

| Where | Convention |
|---|---|
| `BoardState` | cells `[row, col]`; row 0 = top; `ShiftRow` dir + = right; `ShiftColumn` dir + = **down** |
| `Manager_Board` | board-local: centred on `_boardRoot`, x right, y up; cells are `CellSize` = (w, h) = (2.96, 2.56), not square; `CellPosition(r, c) = ((c − (N−1)/2)·w, ((N−1)/2 − r)·h)`; held-line offset row + = right, column + = down |
| `Manager_Game.DragOffset` | `isRow ? cells.x : −cells.y`, cells measured per axis (`ScreenDeltaToCells`: x / w, y / h) (**screen up = toward row 0 = negative column offset**) |
| `TryGetCell` | inverse of `CellPosition`: `col = floor((x + N·w/2)/w)`, `row = floor((N·h/2 − y)/h)` |
| World ↔ reference layout | world origin = centre of the pack's 1080×1920 screen, 1 unit = 100 px, y up: px (x, y from top-left) → ((x − 540)/100, (960 − y)/100) |

### 6.2  Serialized by ordinal — append only

`SoundType` is written into `FileRef.asset` as integers. `GameState` is not serialized yet but is declared append-only.
**Never renumber, reorder or reuse a value.** `SoundType.TileMatch` is no longer played but keeps its ordinal.
(`TileShape` / `TileColor` were removed with the Shift & Sort mode; stale `FileRef` fields from them are dropped on save.)

### 6.3  Art file names → catalog ids

Item ids **are** the art file names: `Sprites/Tilewrap/Items/{id}.png` → `FileRef._items[].Id`, which level files use
in `items` and clue `item`. Characters: `Characters/character_{id}_{neutral|gear|bag|complete}.png` → `_characters[].Id`
with `Poses` in that order. Renaming a file breaks every level that names it (Validate Levels reports it).

### 6.4  Rules implemented in more than one place

| Rule | Places | Risk |
|---|---|---|
| Cell ↔ board-local position | `Manager_Board.CellPosition` and its hand-written inverse in `TryGetCell` | Change the layout (gaps, offset, non-square) in one and taps hit the wrong cell. **Two sites.** |
| Tag position | `Build` (`−grid/2 − TagOffset`) and `GetLocalBounds` (`… − TagSize.x/2`) | Both read `ReferenceLayout`, so they stay in step; a third place would not. |
| Wrap modulo | `BoardState.Wrap` (int) and `Manager_Board.Mod` (float, used in `RenderHeldLine`) | Same maths, two implementations. |
| Column direction sign | `BoardState.ShiftColumn` (+ = down), `Manager_Board` offset (+ = down), `Manager_Game.DragOffset` (`−cells.y`) | Flip one and columns move against the finger. **Three sites.** |
| Cell size | `ItemTile._size` (prefab, = `Manager_Board.CellSize`) and `AssetBuilder.ItemTileSize` (builds the prefab) | Change one only and tiles overlap or leave gaps. |
| Layout px → units | `ReferenceLayout` (runtime) and `AssetBuilder` (prefab internals: item box, label, tag label) | Both copy numbers from `Layout_1080x1920.csv` by hand. |

---

## 7  Assets → loaders

| Asset | Loaded by | How |
|---|---|---|
| `Resources/FileRef.asset` | `FileRef.Instance` (first use: `Manager_Level.Awake2`) | `Resources.Load<FileRef>("FileRef")` via `[AssetPath("FileRef")]`; Editor auto-creates if missing |
| `Prefabs/Gameobjects/Cell.prefab` | `Manager_Board.Build` | `Instantiate(FileRef.CellPrefab, _cellsRoot)` ×N² |
| `Prefabs/Gameobjects/ItemTile.prefab` | `Manager_Board.SpawnTile` | `Instantiate(FileRef.ItemTilePrefab, _tilesRoot)` ×N² + 1 ghost per drag |
| `Prefabs/Gameobjects/CategoryTag.prefab` | `Manager_Board.Build` | `Instantiate(FileRef.CategoryTagPrefab, _tagsRoot)` ×N |
| `Audio/Music/*.wav` (first) | `Manager_Audio.Start` | `FileRef.Music`; imported Streaming / Vorbis by `AssetBuilder` |
| `Audio/SFX/{sound_type}[_NN].wav` | `Manager_Audio.PlaySound` | `FileRef._sounds` (`SoundData`: Type, Clips[], Volume), filled by `AssetBuilder` from the snake_case of each `SoundType`; Decompress On Load, ADPCM, mono |
| `Sprites/Tilewrap/**` + `art_manifest.json` | `AssetBuilder.ImportTilewrapArt` | import settings per asset (PPU 200 for most UI, 100 for the rest; pivot; 9-slice border LBRT) |
| `Sprites/Tilewrap/Items/*.png` (9) | `Manager_Board.SetupTile` | `FileRef.GetItem(level.ItemFor(tile)).Sprite` |
| `Sprites/Tilewrap/Characters/*.png` (12) | nothing yet | `FileRef._characters[].Poses`; for the HUD's character row |
| `Sprites/Tilewrap/UI/tile_cream`, `tile_honey` | `Manager_Board.RefreshIndicators` | `FileRef.TileSurface` / `TileSurfaceActive` → `ItemTile.SetSurface` |
| `Sprites/Tilewrap/UI/board_frame`, `active_row_outline` | scene `Board/Frame`, `Board/Active Row` | serialized; Sliced, sized by `Manager_Board.Build` |
| `Sprites/Tilewrap/UI/check_complete`, `category_tag` | Cell, CategoryTag prefabs | serialized in the prefabs |
| `Sprites/Tilewrap/UI/*` (rest), `Backgrounds/booth_background` | **nothing yet** | for the HUD rebuild |
| `Fonts/TilewrapNunito-ExtraBold SDF` (+ `Masked.mat`) | ItemTile, CategoryTag prefabs | dynamic TMP font asset made by `UIBuilder.GetOrCreateFont`; the masked material by `AssetBuilder` |
| `Sprites/Placeholder/Solid.png` | scene `Board/Mask` (`SpriteMask`) | serialized |
| `Sprites/Placeholder/BoardFrame.png`, `Panel.png`, `Sprites/Sprites/{tiles, targets, fx, board, backgrounds}` | **nothing** | Shift & Sort art, still imported; `ui/` and `branding/` are used by the interim HUD and loading screen |
| `Sprites/Previews/*.png` | nothing | reference mockups only |
| `Fonts/OpenSans-*` SDF | interim HUD, loading screen | made by `UIBuilder` |

Everything referenced by `FileRef` ships in the build, and so does everything under `Resources/`.

---

### 7.1  Level files

`Resources/Levels/level-NNN.json`, three-digit number = level number (level 1 = `level-001.json`). No index: level `n`
plays file `((n − 1) mod LevelCount) + 1`, where `LevelCount` counts files from 001 to the first gap, so play loops.

| Key | Meaning |
|---|---|
| `schemaVersion` | must be 2 (Shift & Sort's version 1 files were removed) |
| `id` | must equal the number in the file name |
| `name` | → `LevelDefinition.Name` (logged, not shown yet) |
| `characters` | N character ids (2..4), one per column, left to right; each must be in `FileRef._characters` |
| `categories` | N row labels, top to bottom ("Hats"), shown on the category tags |
| `items` | N² different item ids, row-major: **the solved board**. Each must be in `FileRef._items` |
| `board` | N² tile ids, row-major from the top-left: the start board. **A tile's id is the cell it belongs in when solved** (`id = row·N + col`, so it shows `items[id]`), and the solved board is `0, 1, 2, …`. Must be a permutation of `0..N²-1`. |
| `steps` | exactly N, in play order, each naming a different row: `{row, goal, clues: [{item, text}]}`. `goal` is the step's title ("Pack their bags"); clue `text` may use TMP rich text (`<color=#ED3937>red</color>`); clue `item` (optional) must be on the board and is for the clue's icon. |
| `moveBudget` | → `MoveLimit`; 0 = unlimited |
| `solution` | `{axis: "row"/"column", line, direction}`, direction +1 = right/down (same as `BoardState.Shift*`). Read by `LevelValidator` and the tester's Play Solution. |

The validator cannot check that the clues decide the answer; that is the level author's job. With only row swipes, a
row has N arrangements (its rotations), so one clue per row is enough when every item starts in its own row.

## 8  Wiring

**`Game.unity`**:

```
Main Camera            orthographic, background #6E4155 (booth wall); position and size set at runtime by Manager_Board.FitCamera
Global Light 2D        template
Managers               GameTuning, SingletonManager, Manager_Audio, Manager_Level, Manager_Board, Manager_Input, Manager_Game
                       (init order: Audio, Level, Board, Input, Game)
Sound                  audio objects (SceneBuilder.BuildSound), wired into Manager_Audio._music / _sfx
  Music                AudioSource, loop, volume 0.45: set the background music level here
  SFX                  AudioSource: its volume is the master level for all effects
                         SingletonManager._singletonsToInitialize = [Level, Board, Input, Game]
                         Manager_Board → _boardRoot, _frame, _mask, _activeRow, _cellsRoot, _tilesRoot, _tagsRoot
GamePlay
  Board                _boardRoot, placed at ReferenceLayout.BoardCenter by Build (runtime content parents below)
    Frame              SpriteRenderer (board_frame, Sliced, order Frame = over the tiles), size grid + 2·FramePadding
    Mask               SpriteMask (Solid), scaled to the grid
    Active Row         SpriteRenderer (active_row_outline, Sliced, order ActiveRow), one row; hidden when solved
    Cells / Tiles / Tags   runtime-filled, emptied on each Build
Canvas                 Screen Space Overlay, CanvasScaler 1080×1920 match 0.5
  Container            SafeArea (anchors = Screen.safeArea, applied once in Awake)
    InGame             UI_InGame (HUD; every field wired by UIBuilder). On the first level start (once) its _introItems
                       scale 0 → 1 in order (DOScale, OutBack): Logo, Level, Sound, Matched, Moves, Message, Undo, Restart
      Top Bar          Logo, Level (TMP), Sound Button/Icon; anchored to the top of the safe area
      Board Area       UI_BoardArea: tracks the board's on-screen rect (OnLayoutChanged)
        Message Panel  Title, Body: 14 above the board, Board Area's width (step goal + clues, win, out of moves)
        Pills          Matched Pill (shows "Step k of n"; field still `_matchedText`), Moves Pill (child Warning: white pill mask, UI_InGame._movesWarning): above the message panel.
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

**Sorting orders** (`SortOrder`, all on the Default layer): TileBody 4 · TileItem 5 · TileLabel 6 · ActiveRow 7 ·
Frame 8 · TileCheck 9 · Tag 10 · TagLabel 11. The frame is a foreground layer over the tiles; tags overlap its left edge.

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
P="--project-path C:/Project/Clue_Parade"
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
| HUD vs the art pack | interim | `UI_InGame` is the Shift & Sort HUD showing step, moves, goal and clues (text only). The pack's layout (level/step title, moves panel, clue notes with item icons, characters on nameplates whose pose follows `ConfirmedSteps`, Undo / Notebook / Hint, booth background) is not built yet. `UI_BoardArea` still sizes `Board Area` around the board. |
| One level | content | Only `level-001.json`; play loops over it. Later levels need more art (items, characters, poses per solution). |
| Notebook, Hint | never built | Placeholders in the plan; no code. |
| Shift & Sort leftovers | unused | `Sprites/Sprites/{tiles, targets, fx, board}`, `Sprites/Placeholder/BoardFrame.png` / `Panel.png` and `AssetBuilder`'s generator for them; the OpenSans HUD art. Remove with the HUD rebuild. |
| `LiveData` | not needed yet | `OfflineData` exists (§4.1); there is no session-only state worth a `LiveData` class. |
| Tutorial | removed | The Shift & Sort tutorial (`Manager_Tutorial`, levels 1–4) was retired with its levels; Clue Parade has none yet. |
| Pooling | never built | Per-level destroy/instantiate; a ghost tile is instantiated per drag. |
| `Manager_Board.ShiftRow/ShiftColumn` | built, unreachable | Public one-shot wrappers; no caller in game code (CLI tests only). |
| `Cell.Row`, `Cell.Column` | built, unread | Assigned, never read. |
| `InputSystem_Actions.inputactions` | unused | Template asset; input reads `Pointer.current` directly. |
| Steps that are a multiple of N | quirk | Dragging exactly N cells (or 2N…) leaves the board unchanged but still adds N to `MovesUsed`. |
| Flick back to the start | quirk | A quick release after the line was picked up but returned to offset 0 commits **+1** (sign of 0 is treated as +). |
| Presses during a slide | quirk | Dropped, not queued (0.05–0.2 s windows). |
| Window resize / aspect change | unverified | `FitCamera` runs only on `Build`; rotating or resizing the Game view mid-level does not refit. |
| Touch on a device | unverified | Drag, release snap-back, steps, a breaking column move, undo and the win were verified in the Windows Editor (Play mode, `AgentScripts/PlayCheck.cs` driving `HandlePress/Drag/Release` with real screen coordinates). Not run with a mouse by hand or on Android/iOS touch. |
| Tests | never built | Test Framework installed, no test assembly. `BoardState` (steps) and `StepsForRelease` would be the first candidates. Game code has no asmdef, so a test asmdef cannot reference it yet. |
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
