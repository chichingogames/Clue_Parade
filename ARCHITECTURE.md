# Clue Parade (Tilewrap) — Architecture

Written 2026-11-10 against commit `2241067`; rewritten for the Tilewrap handoff UI and onboarding flow on 2026-11-27 (uncommitted working tree). **All game code, art, prefabs and the
`Game.unity` scene are uncommitted at the time of writing.** `2241067` holds only the template project, `skeleton.md` and
the skills.
**This file is maintained by hand.** When you change a system, edit the matching section.
See `ARCHITECTURE-HISTORY.md` for what changed and when.

---

## 1  What this is

A portrait mobile puzzle game (**Clue Parade**; Player Settings name **Tilewrap**), built to the **Tilewrap Unity handoff**
(five onboarding levels, approved clue prototype v2). The board is a 3×3 grid of item tiles: **columns are people** (Mia,
Leo, Zoe) and **rows are item categories** (Hats, Shirts, Bags). The player swipes a whole row sideways or a whole column
up or down, one cell per move; tiles wrap around the edge. Every tile has one correct cell (a *slot*), which the player
works out from **clues**. A slot is *matched* while its item is in it: the tile shows a check and the column's
**customer** wears the item (a modular prefab whose accessory layers are switched on). The level is won when all nine
slots are matched at the same time. **Steps** are the level's clue order of rows: the active step is the first row not
complete; its row is highlighted and two of its clues are shown.

Everything the handoff specifies is built: the loading screen, the HUD on the handoff's 1080×1920 layout and sprites,
lessons (levels 1, 3, 4) with required first swipes, the coach line, Hint (explain, then solve the current board and
guide the next swipe), Notebook, win / out-of-moves popups, the level selector, the menu (sound, restart, levels, save
results, fresh test), first-clear coins, end-of-playtest feedback, the local playtest log and report, match sparks and
win confetti. The onboarding flow lives in `Manager_Flow`; popups are `PopUp_X` views over `Manager_PopUp`.

The code follows the architecture blueprint in `skeleton.md` (ported from an earlier game, "Color Farm"). That file
describes the *target*; this file describes what exists.

---

## 2  Repository map

```
Assets/
  Tilewrap/                           the handoff, as delivered: Data/game_config.json (catalog, layout, timings) + Data/Levels
                                      (5 source levels), Sprites/{Characters/{id}/, Items/, UI/, Tutorial/}, Audio/, Fonts/
                                      (TilewrapUI-Regular/ExtraBold + OFL), Editor/TilewrapAssetImporter.cs (sprite import settings)
  Game Assets/
    Scenes/Game.unity                 the only scene (in Build Settings, index 0)
    Scripts/
      Gameplay/
        Essentials/                   framework, game-agnostic: Singleton, SingletonManager, ScriptableSingleton, SafeArea,
                                      Haptics, OfflineData (the save), PlaytestLog (local event log + report export)
        Managers/
          Manager_Game.cs             state machine, drag rules, required first swipe, win/fail, undo, input gate
          Manager_Board.cs            board model owner, all board views, guide, sparks, customers, camera fit
          Manager_Input.cs            pointer → press/drag/release events
          Manager_Level.cs            level number → LevelDefinition
          Manager_Audio.cs            music, effects, the sound switch
          Manager_PopUp.cs            popup registry (by PopupName), show/close, IsPopUpActive
          Manager_Flow.cs             the onboarding flow: lessons, coach, toasts, hint, notebook, win/fail, levels, menu,
                                      rewards, feedback, playtest log
        GameElements/                 world views: Cell, ItemTile, CategoryTag, CharacterRow, Customer, BoardGuide
        BoardState.cs                 pure board model: shifts, matches, active step, clue picking
        BoardSolver.cs                exact shortest-route solver (bidirectional BFS), thread-safe; the Hint's
        LevelDefinition.cs  JsonLevelSource.cs   levels (§7.1)
        ReferenceLayout.cs            the handoff layout in world units (board, tabs, checks, crew, name tabs)
        Common.cs                     enums (GameState, PopupName, HapticType, SoundType), TileData, SortOrder
        Tuning.cs  GameTuning.cs  FileRef.cs  TestManager.cs
      UI/                             UI_InGame (HUD), UI_ReferenceFit, UI_Loading, UI_Toast, UI_Confetti (+Listener),
                                      UI_CrewPortrait (a Customer drawn in uGUI), UI_ChoiceRow
        Popups/                       Popup, BasePopUp, PopUp_{Lesson, Win, Failed, Hint, Notebook, LevelSelect, Menu, Confirm, Feedback}
      Sortify/                        [Button] attribute + inspector drawer
      Editor/                         AssetBuilder, HandoffBuilder, SceneBuilder, UIBuilder, PopUpBuilder, LevelValidator, BuildNaming
    Prefabs/Gameobjects/              Cell, ItemTile, CategoryTag, Customer + Customer_{Mia,Leo,Zoe} (variants)
    Sprites/Placeholder/Solid.png     generated white square (board mask, dividers, sparks)
    Sprites/Branding/                 app icon and splash art (referenced by Player Settings only)
    Fonts/                            TMP font assets: TilewrapUI-{ExtraBold,Regular} SDF (used); older Nunito / Open Sans (unused)
    Audio/Music/ Audio/SFX/           music loop + sound effects (files named after SoundType, see §7)
  Resources/
    FileRef.asset                     the asset registry, loaded by name at runtime
    Levels/level-001..005.json        the five levels (schemaVersion 2, format in §7.1), converted from Tilewrap/Data/Levels
  Plugins/Demigiant/DOTween/          all animation and timed delays (see §5)
  Settings/                           URP 2D renderer assets (template)
AgentScripts/                         CLI scripts run with `unity command script run` (not compiled into the game):
                                      PlayCheck (status, real swipes), FlowCheck (flow status, tap popup/HUD buttons, toast),
                                      UnusedSprites, one-off migrations (MigrateToCustomers, MigrateToHandoffUI, FixNameTabs,
                                      FixFlowLayout, older ones that no longer compile); Out/ = captures
skeleton.md  CLAUDE.md
```

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
         │    ├ PlaceCharacters()             CharacterRow at the world origin; each column's character shown over it
         │    ├ FitCamera()                   the 10.8×19.2 reference screen fitted into the safe area
         │    └ RefreshIndicators()           checks on matched cells, highlight + gold tab on the active row,
         │                                    customer accessories (RefreshCustomers)
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

Around the board (`Manager_Flow`, listening to the events above):

```
OnLevelStarted   → coach = level.coach; log level_start; guide (required first swipe); 16 s hint-attention timer;
                   lesson popup if the level has one not yet acknowledged; on the first open with every level cleared,
                   the level selector
swipe ≠ required → Manager_Game.HandleRelease cancels the drag (no move), OnMoveRejected → toast "First, swipe the …"
OnMoveCommitted  → log first_move; clear hint + guide;  OnLinePickedUp → snapshot step / matched count
OnBoardSettled   → log move; coach line (won / step done / new matches / neutral); guide again
OnStateChanged   → Won: RecordClear (completed + coins, saved once) → after WinPopupDelay PopUp_Win + confetti
                   Failed: after WinPopupDelay PopUp_Failed (Undo last move / Try again / Choose a level)
Hint button      → OpenHint: PopUp_Hint for the first unmatched clue of the active row → "Show the next swipe":
                   BoardSolver.Solve on a worker thread → if the level, board and state are unchanged: guide + coach
Popups open      → Manager_PopUp.OnPopupShown → Manager_Game.ApplyInputGate (AcceptInput false, a held line cancels)
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

Init order (`SingletonManager._singletonsToInitialize`): Audio, Level, Board, Input, PopUp, Game, Flow.

| Manager | Owns | Calls | Called by |
|---|---|---|---|
| `Manager_Game` | `GameState`, `CurrentLevel` (launch opens `OfflineData.CurrentLevel` clamped to the level count), `Level`, `MovesUsed`; press/drag state; drag→steps rule; **`RequiredMove`** (the level's `requiredFirstSwipe` while no move is made and the board is the start board; any other swipe is cancelled, costs nothing, raises `OnMoveRejected`); win/fail; undo history (`Undo()` works from Playing, Won and Failed, raises `OnUndone`); **input gate** `ApplyInputGate`: `AcceptInput = Playing && !IsPaused`, `IsPaused` = `Manager_PopUp.IsPopUpActive` (re-run on state change, popup shown, popup close finished). `SaveProgress` on a win (CurrentLevel = min(max(saved, n+1), count)). Events: `OnStateChanged`, `OnLevelStarted`, `OnMoveMade`, `OnMoveCommitted(move)`, `OnMoveRejected(expected)`, `OnUndone` | `Manager_Level`, `Manager_Board`, `Manager_Input` | input events; `Manager_Flow`; HUD Undo; `TestManager` |
| `Manager_Board` | `BoardState`, `Level`; Cells, ItemTiles, CategoryTags, **Customers** (one instance of each column's `Customer_{Name}` variant on its stand); held line + ghost; indicators (checks on matched cells, active-row highlight, gold tab, customers' accessories); `LastNewMatches`; match celebration (`CelebrateNewMatches`: tile flash + 6 sparks per new match, played moves only); the guide (`ShowGuide(move)` / `HideGuide`); dividers; camera fit | `FileRef`, `BoardState` | `Manager_Game`, `Manager_Flow` |
| `Manager_Input` | Press tracking; `AcceptInput` (false cancels a press) | `Pointer.current` | `Manager_Game` |
| `Manager_Level` | `JsonLevelSource`; `LevelCount`; `FileNumber(n)` wraps | `FileRef` | `Manager_Game`, `Manager_Flow` |
| `Manager_Audio` | Music / SFX sources; `SoundOn` (saved) | `FileRef` | board, game, HUD, popups, menu |
| `Manager_PopUp` | Every `Popup` under `Canvas/Container/PopUps`, keyed by `PopupName`; `ShowPopup` (closes others), `ClosePopup`, `CloseAll`, `GetPopup<T>`, `IsOpen`, `IsPopUpActive` (open or still closing); events `OnPopupShown`, `OnPopupClosed`, `OnPopupCloseFinished` | `Popup.Show/Close` | `Manager_Flow`, `BasePopUp.Dismiss`, `Manager_Game` (gate) |
| `Manager_Flow` | The onboarding flow (§3): `Coach` + `OnCoachChanged`, `OnToast`, `OnHintAttention`, `HintPending` + `OnHintStateChanged`, `OnWinCelebration`; lessons (acknowledgement saved per lesson id), Hint (explain → async solve → guide; result dropped if the session / board / state changed), Notebook, level selector, menu, fresh-test confirm, report export, win (first-clear reward recorded before the popup), fail, feedback after the last level; active play time; every event into `PlaytestLog`. Writer of `OfflineData.CompletedLevels / Coins / Lessons / Feedback*` | managers, popups' `Setup`, `PlaytestLog`, `BoardSolver` | HUD buttons, Manager_Game / Manager_Board events |

**HUD** (`UI_InGame` on `Canvas/Container/InGame`, objects under `InGame/Reference`, fitted by `UI_ReferenceFit` exactly as
`FitCamera` fits the world; every rect is a `game_config.json` `layoutTopLeft` group or inside one): level pill
("LEVEL n of 5" → level selector), moves panel (`panel_moves` with the count, or `panel_practice` "Practice / no move
limit"; low colour at ≤ 3), menu button; title (level name) and subtitle (goal line); clue heading "CLUES · HATS", "n/3
matched", "All 9" (→ Notebook); two clue cards (`panel_clue`, pin, item icon, rich text; `panel_clue_matched` + check
badge while matched; `BoardState.PickClues` chooses them); coach line (capsule, green when a success); match meter
("6/9 matched" + bar); control note; Undo / Notebook (badge 9) / Hint (gold; pulses twice on `OnHintAttention`). The handoff's brand footer is left out.
The toast (`UI_Toast`) lives in `Canvas/Container/Overlay` so it shows over popups.

**Popups** (`Canvas/Container/PopUps`, reference-fitted; built by `PopUpBuilder`): `Popup` (dim fade + holder scale,
`SetUpdate(true)`), `BasePopUp` (kicker / title / body, `Bind` plays the tap, `Dismiss`), `PopUp_X.Setup(..., callbacks)`.
Popups only display and report taps; `Manager_Flow` decides. Win and Failed have no close: the player leaves through an
action; closing the level selector or notebook over a finished level brings its popup back (`ReturnToLevel`).

**Customers and crew portraits:** the board's customers are world sprites (`Customer`, SortingGroup); the lesson, win and
notebook crews are `UI_CrewPortrait`s that read the same prefab's layers (`Customer.GetLayers`) into uGUI Images, so
the art and the accessory grouping exist once.

**Haptics / sounds on settle** (`Manager_Board.PlaySettleFeedback`, played moves only): `SwipeSettle`, then
`LevelComplete` (solved, Heavy) or `TileMatch` variant `min(new matches, 3) − 1` (Medium), else Light. Undo shifts with
`celebrate: false`. `OutOfMoves` on Failed. UI taps: `UiTap` + Light.

**Loading screen** (`UI_Loading`): the teal background, "Tilewrap", "Getting the crew ready…", a gold bar, CHICHINGO; it
holds `Manager_Game.Launch()` until the bar is full, then fades while the board and HUD intros play.

**Intro, spawn, checks, line rendering:** unchanged: `PlayIntro` once per launch, `AnimateTileSpawn` diagonal wave,
`Cell.SetMatched` pops a check when it appears, a held line wraps with a ghost tile under `Board/Mask`.

### 4.3  Model and levels (no Unity objects except `Mathf`)

| File | Owns | Notes |
|---|---|---|
| `BoardState.cs` | `TileData[,]` cells `[row, col]`, `StepRows` (the clue row order); `ShiftRow/ShiftColumn(index, dir)` (any integer dir); `IsHome(r, c)` (the cell holds the tile whose id is `r·N + c`), `IsTileHome(id)`, `IsRowComplete`, `MatchedCount` / `CellCount`; steps: `ActiveStep` (first step whose row is not complete), `ActiveRow` (−1 when solved); `IsSolved` (all cells home); `PickClues(clueTiles, count)` (which of a step's clues the HUD shows: the first `count`, but the first is swapped for an unmatched one when all shown are matched); `Wrap` | Row 0 is the top. Constructors clone their inputs. **Progress is derived, never stored:** a move that breaks a matched slot removes its check and accessory, and Undo restores them. |
| `LevelDefinition.cs` | `Number` (the file's), `Size`, `StartCells`, `MoveLimit` (0 = unlimited), `Name`, `Characters` (column ids), `Categories` (row labels), `Items` (item id per home cell), `Steps` (`LevelStep`: row, goal, clues), `StepRows`, `ItemFor(tile)`, `Solution`; `ILevelSource` | Plain class. |
| `JsonLevelSource.cs` | Loads, validates and converts one level file (§7.1). `TryParse` (structure) and `TryCheckArt` (every item and character is in FileRef) are static so the validator shares them. `Exists(n)`. | Logs an error and returns null on a bad file. |

### 4.4  Views (GameElements)

| View | Shows | Serialized refs |
|---|---|---|
| `Cell` | A board position and its check badge (`check_badge`, 0.5 units, bottom-right) while matched (`SetMatched`) | `_check` |
| `ItemTile` | The item icon (88% of the cell, no surface: the board panel shows through) and a pale green `Flash` behind it. `Size` (2.2667 square = the handoff's 680/3 px; **the board's cell size**) | `_item`, `_flash`, `_size`, `_itemBox` |
| `CategoryTag` | The row's tab beside the board (`tab_category`, gold `tab_category_active` on the active step's row) with the category icon and label. `Setup(label, icon)`, `SetActive(bool)` | `_tab`, `_icon`, `_label`, `_normal`, `_active` |
| `CharacterRow` | Scene object `Board/Characters`: per slot a `Customer Stand` (feet at y 2.08, scale 2.3/9) and a name tab (`tab_name`, symbol + name). `Show(i, x, name, symbol)`, `Hide(i)`, `StandOf(i)` | `_slots[]` (`Stand`, `Plate`, `Symbol`, `Name`) |
| `Customer` | One person: prefab `Customer` (base) → `Customer_Mia/Leo/Zoe` (**prefab variants**: only `_id` and the five sprites differ). Root = feet (`SortingGroup`, `SortOrder.Character`) → `Body` (pops) → `Bag Rear`, `Base`, `Shirt`, `Bag Straps`, `Hat` (orders 0–4, all at (0, 4.5): the registered 600×900 layers share one rect). `_accessories[]` = {Category, Layers}: Hats → Hat, Shirts → Shirt, Bags → Bag Rear + Bag Straps. `SetEquipped` (SetActive), `SetAllEquipped`, `IsEquipped`, `Pop`, `GetLayers` | `_id`, `_body`, `_accessories` |
| `BoardGuide` | `Board/Guide`: a gold band over a row / column, `tutorial_arrow` rotated to the swipe, `tutorial_hand` (pivot at the fingertip) sliding along it on a 1.7 s loop. `Show(center, size, dir, travel)`, `Hide` | `_band`, `_arrow`, `_hand` |

Views never read `FileRef` or any manager; `Manager_Board` hands them sprites and names. UI views (`UI_*`, `PopUp_*`) may
read managers; they never change game state except through manager commands.

### 4.5  Configuration

| File | Owns |
|---|---|
| `Tuning.cs` | `const` defaults + static read facade `Tuning.X` → `GameTuning.Active` or the default |
| `GameTuning.cs` | Inspector overrides on `Managers`; `[ExecuteAlways]`, order −1000. **The scene values ship**, not the consts. |
| `FileRef.cs` | Prefabs (Cell, ItemTile, CategoryTag); `Spark` (Solid); the catalog from `game_config.json`: `_items` (Id, Name, Sprite), `_characters` (Id, Name, `Customer` variant, `Symbol`), `_categories` (Label, Icon; `CategoryIcon(label)`); music and sounds; **Validate references** |
| `ReferenceLayout.cs` | The handoff layout in world units: reference screen 10.8×19.2, `BoardCenter` (0.30, −2.16), `FramePadding` 0.20, divider width, tab size/offset, check offset/size, crew (feet y, customer height), name tabs. Art positions, not tuning. |
| `Common.cs` | `GameState`, `PopupName`, `HapticType`, `SoundType`, `TileData`, `SortOrder` |

Current tuning (scene values; new fields start at their defaults):

| Field | Value | Meaning |
|---|---|---|
| ShiftDuration | 0.15 | seconds per cell of animated travel (clamped 0.35–1.5 cells) |
| BoardIntroDelay / BoardIntroDuration | 0.22 / 0.45 | launch only: the board scales 0 → 1 |
| TileSpawnDuration / TileSpawnStagger | 0.28 / 0.045 | every level: diagonal tile pop-in |
| CheckPopDuration | 0.22 | a matched check's scale-up (the tile flash lasts twice this) |
| EquipPopDuration / EquipPopScale | 0.4 / 1.06 | a customer's pop when a move puts an item on them |
| WinPopupDelay | 0.38 | winning (or failing) move → its popup |
| HintAttentionDelay | 16 | idle seconds (no move yet) before the Hint button pulses twice |
| FirstClearCoins | 30 | reward for a level's first clear |
| LoadingDuration / LoadingFadeDuration | 1.6 / 0.4 | loading bar fill; fade-out as the level opens |
| UiIntroDelay / UiIntroDuration / UiIntroStagger | 0.1 / 0.35 / 0.06 | HUD pop-in on launch |
| DragStartThreshold / DragCommitThreshold / FlickMaxDuration | 0.02 / 0.5 / 0.25 | input |

### 4.6  Editor builders

| Builder | Menu | Does | Never overwrites |
|---|---|---|---|
| `TilewrapAssetImporter` (handoff) | Tools/Chichingo/Tilewrap/Apply sprite import settings | Applies `game_config.json` `sprites` (pivot, border, FullRect, 100 PPU, uncompressed) to `Assets/Tilewrap/Sprites/**` | — |
| `AssetBuilder` | ShiftSort/Build Assets | Writes `Solid.png`; imports audio; builds Cell / ItemTile / CategoryTag on the handoff art; runs `HandoffBuilder.BuildCustomers`; fills `FileRef` (prefabs, spark, music, sounds; catalog through `HandoffBuilder.FillCatalog`) | an existing PNG, prefab, filled slot or non-empty table |
| `HandoffBuilder` | ShiftSort/Build Customers | `Customer.prefab` + `Customer_{name}` variants; `FillCatalog` (items, characters + symbols, categories + icons); `LoadSprite(relative)`; the TilewrapUI TMP font assets (`BoldFont`, `RegularFont`) | an existing prefab, variant, font asset, non-empty table |
| `SceneBuilder` | ShiftSort/Build Scene | Managers (+ init order; missing managers are inserted at their place), `GamePlay/Background` (background_teal), `Board/{Frame (board_frame), Dividers, Active Row (row_highlight), Mask, Cells, Tiles, Tags, Guide, Characters}`, camera, sound, `Canvas/Container/{InGame, PopUps}`, `Canvas/Confetti`, loading / transition placeholders, EventSystem, Tester; then UIBuilder, BuildLoading, PopUpBuilder | existing objects (by path), filled refs |
| `UIBuilder` | ShiftSort/Build UI | The HUD (§4.2) on the handoff layout, every `UI_InGame` field, the intro order; `Container/Overlay/Toast`. Shared helpers (`Place`, `Label`, `AddImage`, `MakeButton`…) | the HUD once `InGame` has `UI_InGame` |
| `UIBuilder.BuildLoading` | ShiftSort/Build Loading Screen | The handoff loading screen (§4.2) | the screen once it has `UI_Loading` |
| `PopUpBuilder` | ShiftSort/Build PopUps | Every `PopUp_{Name}` under `Container/PopUps` (+ `UI_ReferenceFit` on PopUps): Dim, Holder (`panel_modal`, vertical layout + content size fitter), kicker / title / body, crew rows (`UI_CrewPortrait`), scroll areas, choice rows (`UI_ChoiceRow`), primary (`button_gold`) / secondary (`button_cream`) / text buttons; wires `Manager_PopUp._popUpParent` | a popup whose object exists (delete it to rebuild) |
| `BuildNaming` | ShiftSort/Build Name Settings | Android build file naming | other platforms |
| `LevelValidator` | ShiftSort/Validate Levels | Parses every level, checks art, replays `solution` within `moveBudget` | read-only |

Because builders skip existing assets, **changing a builder does not update an asset that already exists.** Edit the
prefab / scene object, or delete it and rebuild (§10).

---

## 5  Cross-cutting patterns

- **Managers decide, views display.** Views have no game logic and no access to managers or `FileRef`.
- **Ordered init.** Put setup in `Awake2()`. A manager that is not in `_singletonsToInitialize` still initializes from its
  own `Awake`, but in arbitrary order.
- **Events default to `delegate { }`.** Subscribed: `Manager_Input`'s four events (`Manager_Game`); `OnLevelStarted`, `OnStateChanged`, `OnMoveMade`, `OnBoardSettled` (`UI_InGame`); `OnBoardSettled` (`TestManager`, during Play Solution). `OnLayoutChanged` has no subscriber.
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

`SoundType` is written into `FileRef.asset` as integers, `PopupName` into each popup. `GameState` is not serialized yet but is declared append-only.
**Never renumber, reorder or reuse a value.** `SoundType.TileMatch` is no longer played but keeps its ordinal.
(`TileShape` / `TileColor` were removed with the Shift & Sort mode; stale `FileRef` fields from them are dropped on save.)

### 6.3  Art file names → catalog ids

Item and character ids come from `Assets/Tilewrap/Data/game_config.json` (`items[].id` → `FileRef._items[].Id`, icon
`Sprites/Items/{sprite}.png`; `recipients[].id` → `_characters[].Id`, layers `Sprites/Characters/{id}/{id}_{base|shirt|hat|bag_rear|bag_straps}.png`).
Level files use them in `items`, `characters` and clue `item`. An accessory's `Category` must equal the level's row label
("Hats"), compared ignoring case. Renaming an id breaks every level that names it (Validate Levels reports it).

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
| `Resources/FileRef.asset` | `FileRef.Instance` | `Resources.Load<FileRef>("FileRef")` |
| `Resources/Levels/level-NNN.json` | `JsonLevelSource` | TextAsset (§7.1) |
| `Prefabs/Gameobjects/{Cell, ItemTile, CategoryTag}.prefab` | `Manager_Board.Build` | `FileRef` prefab slots |
| `Prefabs/Gameobjects/Customer_{Name}.prefab` | `Manager_Board.PlaceCharacters`, `UI_CrewPortrait` | `FileRef._characters[].Customer` |
| `Assets/Tilewrap/Sprites/Items/*` | tiles, clue cards, hint | `FileRef._items[].Sprite` |
| `Assets/Tilewrap/Sprites/Characters/{id}/*` | `Customer_{Name}` variants | serialized |
| `Assets/Tilewrap/Sprites/UI/*` | board objects, prefabs, HUD, popups, loading screen | serialized by the builders; symbols / category icons through `FileRef` |
| `Assets/Tilewrap/Sprites/Tutorial/*` | `Board/Guide` | serialized |
| `Assets/Tilewrap/Data/game_config.json`, `Data/Levels/*` | editor only: `HandoffBuilder`, the import helper | source of the catalog and of `Resources/Levels` (converted once) |
| `Assets/Tilewrap/Audio/*` | **nothing yet** | match_1..3 / level_win; the game plays `Audio/SFX` (TileMatch variants, LevelComplete) |
| `Fonts/TilewrapUI-{ExtraBold,Regular} SDF` | HUD, popups, world labels | made by `HandoffBuilder` from `Assets/Tilewrap/Fonts` |
| `Sprites/Placeholder/Solid.png` | `Board/Mask`, `Board/Dividers`, sparks | serialized / `FileRef.Spark` |
| `Sprites/Branding/*` | Player Settings only | app icon, splash background, splash logo |
| `Audio/Music/*.wav`, `Audio/SFX/*.wav` | `Manager_Audio` | `FileRef` (§ sounds) |
| PlayerPrefs `OfflineData`, `PlaytestLog` | `OfflineData`, `PlaytestLog` | JSON; the report is written to `persistentDataPath/Tilewrap_Clues_Onboarding_Playtest_{date}.json` |

The old fair-booth pack (`Sprites/Tilewrap`), the Shift & Sort art (`Sprites/Sprites`) and the generated `BoardFrame` /
`Panel` sprites were deleted once nothing referenced them (2026-11-27; they are in git history).

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
| `subtitle` | optional: the goal line under the title |
| `coach` / `completion` | optional: the coach line when the level opens / the win popup's payoff line |
| `requiredFirstSwipe` | optional move: the only move accepted while no move is made and the board is the start board (guided, others cost nothing) |
| `lesson` | optional `{id, title, paragraphs[], illustration: neutralCrew / categoryOrder / moveCounter, button}`: a popup shown once per id |

The five files are converted from the handoff's `Data/Levels/level_00N.json` (schema 1) and `game_config.json`: board =
each start item's index in `goalCells`; steps = `clueRowOrder`, each with all three clues in the preferred card order
(default Mia, Zoe, Leo; `clueOverrides` put first); `moveLimit` → `moveBudget`. Level 4's button text arrived with broken
characters and was repaired to "Got it — let’s practice".

The validator cannot check that the clues decide the answer; that is the level author's job. With only row swipes, a
row has N arrangements (its rotations), so one clue per row is enough when every item starts in its own row.

## 8  Wiring

**`Game.unity`**:

```
Main Camera            orthographic, teal; fitted by Manager_Board.FitCamera
Managers               GameTuning, SingletonManager, Manager_Audio, _Level, _Board, _Input, _PopUp, _Game, _Flow
Sound                  Music, SFX AudioSources (volumes live here)
GamePlay
  Background           background_teal, scaled to cover the view
  Board                _boardRoot at ReferenceLayout.BoardCenter
    Frame              board_frame, sliced, order Board (the panel the tiles sit on)
    Dividers           4 Solid lines (#C5A581), placed by Manager_Board
    Active Row         row_highlight, sliced, behind the active step's row
    Mask               SpriteMask (Solid) over the grid
    Cells / Tiles / Tags   runtime-filled (sparks also go under Cells)
    Guide              BoardGuide (Band, Arrow, Hand), inactive until shown
    Characters         CharacterRow: Customer Stand 1–3 (a Customer variant instance each, per level), Name Tab 1–3 → Symbol, Name
Canvas                 Screen Space Overlay, 1080×1920 match 0.5
  Container            SafeArea
    InGame             UI_InGame → Reference (UI_ReferenceFit): the HUD
    PopUps             UI_ReferenceFit; PopUp_Lesson, _Win, _Failed, _Hint, _Notebook, _LevelSelect, _Menu, _Confirm,
                       _Feedback (each: Dim + Holder), inactive until shown
    Overlay            UI_ReferenceFit → Toast (UI_Toast)
  Dark Area            unused (each popup has its own Dim)
  Confetti             UI_Confetti + UI_ConfettiListener
  Loading Screen       UI_Loading (Art, Content → Title, Line, Progress Bar → Fill, Brand)
  Transition Screen    empty
EventSystem            InputSystemUIInputModule
Tester                 TestManager (EditorOnly)
```

**Names are load-bearing:** builders and `AgentScripts/FlowCheck` find objects by path (`PopUp_{Name}/Holder/{Button}`,
`InGame/Reference/{Hint Button}`, `GamePlay/Board/Cells/Cell r,c`).

**Sorting orders** (`SortOrder`): Background −10 · Board 0 · Divider 1 · ActiveRow 2 · TileFlash 3 · TileItem 5 ·
TileCheck 9 · Tag 10 · TagIcon 11 · Character 12 (SortingGroup) · NamePlate 14 · NameLabel 15 · Guide 20–22 · Particles 25.

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
| Handoff audio | not wired | `Assets/Tilewrap/Audio` (match_1..3, level_win, with config volumes) is unused; the game plays its own `Audio/SFX` |
| Diagonal rejection | different | The prototype rejects diagonal swipes with a toast; here hold-and-drag locks to the dominant axis |
| Responsive layout | partial | The 1080×1920 reference is fitted uniformly (letterboxed on other aspects); the handoff's "shrink gaps / grow the board" rules are not implemented. Not checked on devices or notches |
| Reduced motion | not built | Effects always play |
| Report sharing | partial | The report is saved to `persistentDataPath` (path logged); no share sheet on mobile |
| Active time | approximate | Counted in `Manager_Flow.Update` while playing, unpaused and focused |
| Hint solver | thread | `Task.Run`: not available on WebGL (not a target) |
| Old AgentScripts | stale | ItemTilePreview, MigrateBoardScene, RetireShiftSort, MoveCharactersToWorld, MigrateToCustomers reference removed code; kept as history, they no longer compile |
| Unused fonts | leftover | `Fonts/TilewrapNunito-*`, `OpenSans-*` and the masked material are no longer used |
| Tests | never built | `BoardState`, `BoardSolver` and `PickClues` would be the first candidates |
| Touch on a device | unverified | Flow verified in the Windows Editor with `AgentScripts/PlayCheck` + `FlowCheck` (real swipes through the input handlers, popup buttons invoked) |

---

## 12  Conventions

- **Naming:** `Manager_X` (scene singletons), `PopUp_X` / `UI_X` (reserved for UI), `[SerializeField] private _camelCase`
  fields exposed through expression-bodied properties, global namespace.
- **One MonoBehaviour per file**, file named after the class.
- **Comments:** short `//` lines explaining why, above the member. No `TODO`/`HACK` markers are in use yet.
- **Log tags** (grep these): `[Game]` (level start, complete, failed), `[FileRef]`, `[AssetBuilder]`, `[SceneBuilder]`,
  `[Singleton]`, `[SingletonManager]`, `[ScriptableSingleton]`.
- **Menus:** `ShiftSort/Build Assets`, `ShiftSort/Build Scene`.
