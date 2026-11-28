# Clue Parade (Tilewrap) — Architecture

Written 2026-11-10 against commit `2241067`; rewritten for the Tilewrap handoff UI and onboarding flow on 2026-11-27;
rewritten for the **30-case content pack** (`Tilewrap_30_Level_Unity_Production`, the ten-case plan and storyboards)
on 2026-11-28 against `1ea9dea` plus uncommitted work.
**This file is maintained by hand.** When you change a system, edit the matching section.
See `ARCHITECTURE-HISTORY.md` for what changed and when.

> **Branch `any-row` (2026-11-28): "Any row, two clues"** (`Tilewrap_Any_Row_5_Case_Playtest.html`). This branch
> replaces the 30-case wrap / swap game; where the sections below describe `BoardState`, `LevelDefinition`,
> `CaseLevelSource`, `SwapPuzzle`, category tabs, the case map or line drags, read this instead (`CLAUDE.md`'s Game
> design section has the rules in full):
> - **Data:** `Assets/Game Assets/Data/delivery_cases.json` → `DeliveryContent` / `DeliveryCase` (`FileRef.Cases`,
>   assigned by Build Assets) → `Manager_Level.Load(n)`. Art and rigs still come from the pack's content library.
> - **Model:** `DeliveryRules` + `DeliveryState` (pure): swap neighbours, any-row deliveries of eligible sets, gravity,
>   refills, staged / choice order, the 729-state hint planner, `Solve` and `CheckInvariant` (the validator uses both).
>   `Transact` returns the move as steps (`DeliveryEvent`: Swap, Deliver, Fall).
> - **Game** (`Manager_Game`): `Model` (after the move) and `View` (the step on screen), the request in focus
>   (`FocusSet`, order tabs), tap selection, case 1's `RequiredSwap`, history of whole states for Undo. Events:
>   `OnViewChanged`, `OnSwapCommitted`, `OnSwapRejected`, `OnMoveResolved`, `OnUndone`.
> - **Board** (`Manager_Board`): tiles by item id, empty cells, `BeginDrag(a, b)` / `PlayMove(events, onDone)` (swap,
>   then `OnDelivering` → the HUD flies the items → `OnDelivered`, then fall / refill), marks (`TileLook`, row zones,
>   muted tiles), `ShowHint`, `ShowBoard` (Undo). The board is centred (`ReferenceLayout.BoardCenter` x 0).
> - **Flow / UI:** `Manager_Flow` owns the coach line (`OnCoach`), clue focus (`OnClueFocus`), Hint, Notebook (per set),
>   `PopUp_Cases` (LevelSelect), win / fail / feedback. `UI_InGame` and `UI_SubjectCard` (delivered parts + extra
>   layers) were rebuilt by Build UI; `PopUp_Hint`, `PopUp_Notebook`, `PopUp_Cases` by Build PopUps.

---

## 1  What this is

A portrait mobile puzzle game (**Clue Parade**; Player Settings name **Tilewrap**). The board is a 3×3 grid of item
tiles. Every **case** (level) is a new situation: **columns are subjects** (Mia, Leo, Zoe; three pets; three rooms;
three bags; three boats…) and **rows are categories** (Hats / Shirts / Bags; Wear / Beds / Extras; Walls / Floors /
Furniture…). The player swipes a whole row sideways or a whole column up or down, one cell per move; tiles wrap around
the edge. Every tile has one correct cell (a *slot*), stated by its **clue fact** ("Trail bag gets the raincoat.").
A slot is *matched* while its item is in it: the tile shows a check and the column's **subject rig** shows the fitted
part (one base plus registered layers). The case is won when all nine slots are matched **at the same time**. Correct
tiles stay movable; nothing is locked or banked.

30 cases in five chapters (campaign map). Levels 1–5 teach (only 1 and 3 have a guided first swipe; 1–3 have no move
limit), the rest are Practice (limit ≥ 2× minimum) or **Hard** (minimum + 2, Hard badge). Everything around the board
follows the storyboards: HUD, two clue cards for the selected category (a category tab tap changes them, no move),
Hint (explain the fact, then the next swipe from the current board), Notebook (all nine facts), win ("Case complete!",
the finished subjects, +30 coins on first clear, "Up next"), out of moves (Retry / Undo / Choose a case), the case map,
the menu, end-of-campaign feedback, the local playtest log.

The code follows the architecture blueprint in `skeleton.md` (ported from "Color Farm"). That file describes the
*target*; this file describes what exists.

---

## 2  Repository map

```
Assets/
  TilewrapVaried/                     the content pack, as delivered (do not edit; re-copy to update):
    Data/content.json                 manifest: sprite import settings, the 90 rigs' layers, levels, fonts, audio gains
    Data/Levels/level_001..030.json   the cases (format §7.1)
    Data/campaign.json                chapters, map node positions / sections, ending text
    Data/ui_layout.json               1080x1920 regions, text sizes, timings (HUD / board layout source)
    Data/hint_distances.bytes         shortest-solve distance of all 362,880 3x3 states (Hint, validator)
    Sprites/{UI, FX, Tutorial, Items, Rigs/<crew…>, Cases/<theme>/{Items,Rigs}}   Audio/ (match_1..3, level_win)
    Fonts/ (TilewrapUI-Regular/ExtraBold + OFL)
    Runtime/  TilewrapCaseData, TilewrapContentLibrary, TilewrapRigView, TilewrapWrapPuzzle   (namespace Chichingo.TilewrapVaried)
    Editor/TilewrapContentImporter.cs Tools > Chichingo > Tilewrap > Build 30-level content
    Generated/                        made by that importer: TilewrapContentLibrary.asset, Rigs/<id>.prefab (90 uGUI rigs)
  Game Assets/
    Scenes/Game.unity                 the only scene (in Build Settings, index 0)
    Scripts/
      Gameplay/
        Essentials/                   framework, game-agnostic: Singleton, SingletonManager, ScriptableSingleton, SafeArea,
                                      Haptics, OfflineData (the save), PlaytestLog (local event log + report export)
        Managers/
          Manager_Game.cs             state machine, drag rules, required first swipe, win/fail, undo, input gate
          Manager_Board.cs            board model owner, tiles, checks, selected-row highlight, guide, match FX, camera fit
          Manager_Input.cs            pointer → press/drag/release events
          Manager_Level.cs            level number → LevelDefinition (CaseLevelSource)
          Manager_Audio.cs            music, effects, the sound switch
          Manager_PopUp.cs            popup registry (by PopupName), show/close, IsPopUpActive
          Manager_Flow.cs             the flow: tutorial toast, hint, notebook, win/fail, map, menu, rewards, feedback, log
        GameElements/                 world views: Cell, ItemTile, BoardGuide
        BoardState.cs                 pure board model: shifts, matches, clue picking
        LevelDefinition.cs  CaseLevelSource.cs   levels (§7.1)
        ReferenceLayout.cs            the pack's layout in world units (board, cells, tile art / name / check)
        Common.cs                     enums (GameState, PopupName, HapticType, SoundType), TileData, SortOrder
        Tuning.cs  GameTuning.cs  FileRef.cs  TestManager.cs
      UI/                             UI_InGame (HUD), UI_SubjectCard (a rig in a card), UI_CategoryTab, UI_ReferenceFit,
                                      UI_Loading, UI_Toast, UI_Confetti (+Listener), UI_ChoiceRow
        Popups/                       Popup, BasePopUp, PopUp_{Win, Failed, Hint, Notebook, Map, Menu, Feedback}
      Sortify/                        [Button] attribute + inspector drawer
      Editor/                         AssetBuilder, HandoffBuilder, SceneBuilder, UIBuilder, PopUpBuilder, LevelValidator, BuildNaming
    Prefabs/Gameobjects/              Cell, ItemTile
    Sprites/Placeholder/Solid.png     generated white square (board mask, dividers)
    Sprites/Branding/                 app icon and splash art (referenced by Player Settings only)
    Fonts/                            TMP font assets: TilewrapUI-{ExtraBold,Regular} SDF, TilewrapUI-ExtraBold SDF Masked.mat
    Audio/Music/ Audio/SFX/           music loop + our effects (ui_tap, swipe_settle, out_of_moves, restart, undo)
  Resources/FileRef.asset             the asset registry, loaded by name at runtime
  Plugins/Demigiant/DOTween/          all animation and timed delays (see §5)
  Settings/                           URP 2D renderer assets (template)
AgentScripts/                         CLI scripts run with `unity command script run` (not compiled into the game):
                                      PlayCheck (status, real swipes), FlowCheck (flow status, tap popup/HUD buttons, toast),
                                      SaveBackup (back up / restore the Editor save around tests), UnusedSprites; Out/ = captures
skeleton.md  CLAUDE.md
```

---

## 3  The main flow

```
Scene load (Game.unity)
 ├ GameTuning.OnEnable                      [order -1000, ExecuteAlways]  GameTuning.Active = this
 ├ SingletonManager.Awake                   [order -900]
 │   ├ OfflineData.Load()                    once
 │   └ for each in _singletonsToInitialize (Inspector order):  ISingleton.Initialize() → Awake2()
 │       ├ Manager_Level.Awake2             new CaseLevelSource(FileRef.Instance): reads each case's id from
 │       │                                   FileRef.Content.levels; LevelCount = 1..N without a gap (30)
 │       ├ Manager_Game.Awake2              subscribes Manager_Input; popup events → ApplyInputGate
 │       └ Manager_Flow.Awake2              subscribes Manager_Game / Manager_Board events
 ├ UI_Loading.Awake → Manager_Game.HoldLaunch();  UI_Loading.Start → bar fills → Manager_Game.Launch() → fade
Manager_Game.Launch (once)
     └ StartLevel(OfflineData.CurrentLevel clamped to 1..30)
         ├ Manager_Level.Load(n)             CaseLevelSource: TilewrapCaseData.Parse → TryConvert (§7.1) → TryCheckArt
         │                                   (every item sprite and rig is in the library); cached per case
         ├ Manager_Board.Build(level)        tiles (FileRef.Sprite(item.Sprite), item.Label), cells, frame, highlight,
         │                                   FitCamera, intro / tile spawn wave, checks
         ├ SetState(Playing); OnLevelStarted(n)
         │   ├ UI_InGame.ShowLevel           each column's rig (FileRef.Rig → UI_SubjectCard.Show: Instantiate + FitInto),
         │   │                               category tabs, ApplyBoard(animate: false)
         │   └ Manager_Flow                  log case_start; guide for requiredFirstSwipe; 16 s hint attention;
         │                                   tutorial.entryText as a toast until the case is cleared once
One drag: unchanged from the five-case build (Manager_Input → Manager_Game.HandlePress / HandleDrag / HandleRelease
          → Manager_Board.BeginDrag / SetDragOffset / CommitDrag or CancelDrag); one cell per swipe, one move.
AnimateHeldLine → SettleHeldLine
 ├ reorder tiles; LastNewMatches; LastSettleCelebrated (false for Undo)
 ├ CelebrateNewMatches (played moves): tile flash, fx_ring, 2-4 fx_star per newly matched cell
 ├ a category the player chose stays selected until a move completes its row
 ├ RefreshIndicators: checks, highlight on SelectedRow
 ├ PlaySettleFeedback: swipe_settle + level_win (solved) or one of match_1..3 (new matches) + one haptic
 └ OnBoardSettled → UI_InGame: rigs ApplyBoard(animate = LastSettleCelebrated): new parts fade in 0.16 s, card pops
SetState(Won)    → Manager_Flow: RecordClear (completed + level.FirstClearCoins, once) → after 0.38 s PopUp_Win
                   (+ confetti): completion text, finished rigs, reward, "Up next <title>", Next case → nextLevelId;
                   case 30 (nextLevelId 0): "All 30 cases complete!", ending text → feedback → map
SetState(Failed) → after 0.38 s PopUp_Failed: Retry / Undo / Choose a case
```

Around the board (`Manager_Flow`):

```
swipe ≠ required    → cancelled, no move; toast "First, swipe the top row right."
category tab tap    → Manager_Board.SelectRow(row) → OnSelectionChanged → HUD clue cards + gold tab; highlight moves
Hint button         → PopUp_Hint: the selected row's first unsatisfied fact (item picture + clue), and the next swipe:
                      the required swipe, else TilewrapWrapPuzzle.TryNextMove over FileRef.HintDistances from the
                      CURRENT board ("Swipe the top row left." + which item wraps). "Show the next swipe" → guide.
Notebook / "All facts in Notebook" → PopUp_Notebook: names over three columns, each category's items (icon, name,
                      check while in place), rules line
Level pill          → PopUp_Map (PopupName.LevelSelect): 30 nodes, current / open / locked, checks
Popups open         → Manager_PopUp.OnPopupShown → Manager_Game.ApplyInputGate
```

**The model changes at commit; the views catch up at the end of the animation.** The win is decided at commit.

---

## 4  Modules

### 4.1  Essentials (framework)

| File | Owns | Calls | Called by |
|---|---|---|---|
| `Singleton.cs` | `Singleton<T>.Instance`; init exactly once per instance; a duplicate logs `[Singleton]` and destroys the component | `Awake2()` | Unity `Awake`, `SingletonManager` |
| `Haptics.cs` | `Haptics.Play(HapticType)` → CandyCoded HapticFeedback. Always on. Editor-only `DebugLog` | package | `Manager_Board`, `Manager_Game`, UI |
| `OfflineData.cs` | The save: `CurrentLevel`, `PassedLevels`, `IsSoundOn`, `CompletedLevels`, `Coins`, `Lessons` (retired, no writer), `Feedback*`. JSON in PlayerPrefs `OfflineData`, append-only | `PlayerPrefs` | `SingletonManager` (Load once); writers: `Manager_Game.SaveProgress`, `Manager_Audio.SetSoundOn`, `Manager_Flow` |
| `SingletonManager.cs` | `OfflineData.Load()` once, boot order, frame rate, DOTween capacity | | Unity (order −900) |
| `ScriptableSingleton.cs` | `Resources.Load` of `[AssetPath]`; Editor creates if missing | | `FileRef.Instance` |
| `PlaytestLog.cs` | Local event log (PlayerPrefs `PlaytestLog`), JSON report export | | `Manager_Flow` |

### 4.2  Managers (all on the `Managers` GameObject)

Init order: Audio, Level, Board, Input, PopUp, Game, Flow.

| Manager | Owns | Called by |
|---|---|---|
| `Manager_Game` | `GameState`, `CurrentLevel`, `Level`, `MovesUsed`; drag→steps rule; `RequiredMove`; win/fail; undo; input gate; `SaveProgress` | input events, `Manager_Flow`, HUD Undo, `TestManager` |
| `Manager_Board` | `BoardState`, `Level`; Cells, ItemTiles; held line + ghost; checks; **`SelectedRow`** (the player's tab choice until a move completes that row, else the top incomplete row; −1 solved) + `SelectRow` + `OnSelectionChanged`; highlight; `LastNewMatches`, `LastSettleCelebrated`; match FX (fx_ring / fx_star); settle sounds; guide; dividers; camera fit | `Manager_Game`, `Manager_Flow`, HUD |
| `Manager_Input` | Press tracking; `AcceptInput` | `Manager_Game` |
| `Manager_Level` | `CaseLevelSource`; `LevelCount`; `Load(n)` clamps to 1..LevelCount | `Manager_Game`, `Manager_Flow` |
| `Manager_Audio` | Music / SFX sources; `SoundOn`; `PlaySound(type, variant)` (Volume × per-clip `ClipVolumes`) | everything that makes noise |
| `Manager_PopUp` | Every `Popup` under `Canvas/Container/PopUps`, keyed by `PopupName` | `Manager_Flow`, `BasePopUp`, `Manager_Game` |
| `Manager_Flow` | Tutorial toast, toasts, hint attention, Hint (sync, table), Notebook, map (unlock: level 1, a cleared case, or the one after a cleared case), menu (sound, restart, choose a case, back), win / fail, feedback after the last case (saving it exports the report), active time, `PlaytestLog` events (`case_start`, `move`, `undo`, `case_complete`, `case_failed`, `clue_hint_open`, `hint_shown`, `notebook_open`, `map_open`, …) | HUD buttons, events |

**HUD** (`UI_InGame` on `Canvas/Container/InGame`, objects under `InGame/Reference`, fitted by `UI_ReferenceFit`;
every rect is a `ui_layout.json` region): level pill ("Level n" → map), menu icon, Hard badge (`level.IsHard`), moves
panel (`panel_moves` count or `panel_practice` "Practice / no move limit"; low colour at ≤ 3), title, goal line,
"CLUES" + "All facts in Notebook", two clue cards (`BoardState.PickClues(SelectedRow, 2)`: unsatisfied first;
`panel_clue_matched` + check while satisfied), three `UI_SubjectCard`s (white card, rig, name tab) above the board
columns, three `UI_CategoryTab`s beside the rows (gold on `SelectedRow`), control note, "x/9 matched", Undo / Notebook
/ Hint (pulses after 16 idle s). The toast (`UI_Toast`) lives in `Container/Overlay` (over popups).

**Subjects:** `TilewrapRigView` prefabs (the pack's importer: a RectTransform of `nativeSize` with one Image per layer
at its registered rect; `bindings[]` = layer, `goalIndex`, `expectedItem`; `ApplyBoard(board ids)` shows a part while
board[goalIndex] == expectedItem; `ShowCompleted()`; `FitInto(frame)`). `UI_SubjectCard` instantiates one per column
into its frame and fades newly shown parts. The same prefabs draw the win popup's finished scene.

**Popups** (`Canvas/Container/PopUps`, reference-fitted; built by `PopUpBuilder`): `Popup` (dim fade + holder scale),
`BasePopUp` (kicker / title / body, hidden when empty; `Bind`), `PopUp_X.Setup(..., callbacks)`. Popups display and
report taps; `Manager_Flow` decides. Win and Failed have no close; closing the map or notebook over a finished case
brings its popup back (`ReturnToLevel`). `PopUp_Map` is full screen and clones its path / heading / node templates from
`campaign.json` the first time it opens.

**Loading screen** (`UI_Loading`): teal background, "Tilewrap", a line, a gold bar, CHICHINGO; holds `Launch()`.

### 4.3  Model and levels (no Unity objects)

| File | Owns | Notes |
|---|---|---|
| `BoardState.cs` | `TileData[,]` `[row, col]`; `ShiftRow/ShiftColumn`; `IsHome`, `IsTileHome`, `IsRowComplete`, `MatchedCount`, `IsSolved`, `FirstIncompleteRow`; `PickClues(row, count)` (column indexes, unmatched first); `TileIds()` (= each cell's index into the goal board: the hint table's input) | Progress is derived, never stored. |
| `LevelDefinition.cs` | `Number`, `Size`, `StartCells`, `MoveLimit` (0 = unlimited), `MinimumMoves`, `Title`, `Goal`, `Setup`, `Completion`, `Difficulty` (`IsHard`), `ChapterId`, `NextLevelId`, `FirstClearCoins`, `Subjects[]` (Id, Name, RigId), `Categories[]`, `Items[]` by tile id (Id, Label, Sprite, Clue), `Solution`, `RequiredFirstMove`, `TutorialText`; `LevelMove` (`Describe`, `DescribeWrap`); `ILevelSource` | Plain classes. |
| `CaseLevelSource.cs` | Case file → `LevelDefinition` (`TryConvert`, `TryCheckArt`, shared with the validator) | Logs and returns null on a bad case. |

### 4.4  Views (GameElements)

| View | Shows | Serialized refs |
|---|---|---|
| `Cell` | A board position and its check badge (`check_badge`, 0.44 units, top-right) while matched | `_check` |
| `ItemTile` | The item icon (art rect 2.40×2.05, 0.275 above centre), its name under it (world TMP, masked material), a pale green `Flash`. `Size` 2.9 (290 px; **the board's cell size**) | `_item`, `_label`, `_flash`, `_size`, `_itemBox` |
| `BoardGuide` | `Board/Guide`: gold band, `tutorial_arrow`, `tutorial_hand` sliding on a 1.7 s loop | `_band`, `_arrow`, `_hand` |

Views never read `FileRef` or managers; `Manager_Board` hands them sprites and names. UI views (`UI_*`, `PopUp_*`) may
read managers; they change game state only through manager commands.

### 4.5  Configuration

| File | Owns |
|---|---|
| `Tuning.cs` / `GameTuning.cs` | const defaults + facade / scene overrides (the scene values ship) |
| `FileRef.cs` | Cell and ItemTile prefabs; **`Content`** (the pack's `TilewrapContentLibrary`) with lookups `Sprite(key)`, `Rig(id)`, `Campaign`, `HintDistances`; music and sounds (+ `ClipVolumes`); **Validate references** |
| `ReferenceLayout.cs` | The pack layout in world units: `BoardCenter` (0.65, −2.75), `CellSize` 2.9, `FramePadding` (0.15, 0.10), divider width, tile art / name / check offsets and sizes |
| `Common.cs` | `GameState`, `PopupName` (Lesson retired; LevelSelect = the map), `HapticType`, `SoundType` (LineComplete not played), `TileData`, `SortOrder` |

Current tuning: ShiftDuration 0.15, BoardIntro 0.22 / 0.45, TileSpawn 0.28 / 0.045, CheckPop 0.22, EquipPop 0.4 / 1.06
(subject card pop), SlotFade 0.16, WinPopupDelay 0.38, HintAttentionDelay 16, Loading 1.6 / 0.4, UiIntro 0.1 / 0.35 /
0.06, DragStart 0.02, DragCommit 0.5, FlickMax 0.25. Coins come from each case's `firstClearCoins` (30).

### 4.6  Editor builders

| Builder | Menu | Does | Never overwrites |
|---|---|---|---|
| `TilewrapContentImporter` (pack) | Tools/Chichingo/Tilewrap/Build 30-level content | Sprite import settings from `content.json` (512 px cases / 2048 px UI, ASTC 4×4 mobile), 90 rig prefabs, `Generated/TilewrapContentLibrary.asset` | — (rebuilds its own `Generated/`) |
| `AssetBuilder` | ShiftSort/Build Assets | `Solid.png`; audio import settings (ours + the pack's); Cell / ItemTile; fills `FileRef` (prefabs, content library, music, sounds: TileMatch = match_1..3, LevelComplete = level_win at manifest gains, others `Audio/SFX/{snake_case}.wav`) | an existing PNG, prefab, filled slot or non-empty table |
| `HandoffBuilder` | — | `LoadSprite` from the pack, the TilewrapUI TMP font assets, the masked tile-name material | an existing font asset / material |
| `SceneBuilder` | ShiftSort/Build Scene | Managers, `GamePlay/Background`, `Board/{Frame, Dividers, Active Row, Mask, Cells, Tiles, Guide}`, camera, sound, Canvas (`Container/{InGame, PopUps, Overlay}`, Confetti, Loading Screen), EventSystem, Tester; then UIBuilder, BuildLoading, PopUpBuilder | existing objects (by path), filled refs |
| `UIBuilder` | ShiftSort/Build UI | The HUD (§4.2) on `ui_layout.json`; `SubjectCard` helper (shared with the win popup); toast | the HUD once `InGame` has `UI_InGame` |
| `PopUpBuilder` | ShiftSort/Build PopUps | `PopUp_{Win, Failed, Hint, Notebook, LevelSelect (map), Menu, Feedback}` | a popup whose object exists (delete it to rebuild) |
| `LevelValidator` | ShiftSort/Validate Levels | Every case: converts, art and rigs exist, `nextLevelId` chain, not solved at start, solution solves within the limit, **hint table distance = `minimumMoves`** | read-only |
| `BuildNaming` | ShiftSort/Build Name Settings | Android build file naming | |

Because builders skip existing assets, **changing a builder does not update an asset that already exists.** Delete it
and rebuild (§10).

---

## 5  Cross-cutting patterns

- **Managers decide, views display.** World views have no game logic and no access to managers or `FileRef`.
- **Ordered init** in `Awake2()`; **events default to `delegate { }`**; listeners subscribe, then read.
- **Numbers in `Tuning`, assets in `FileRef`.** All animation and timed delays through DOTween (`SetLink`).
- **Re-runnable Editor builders** instead of hand-edited scene/prefab YAML.
- **The content pack is data.** Code outside `Assets/TilewrapVaried` reads it through `FileRef.Content`; the pack's
  four runtime scripts are used as delivered (`TilewrapCaseData` parsing, `TilewrapRigView`, `TilewrapWrapPuzzle`).
- **No pooling.** Cells and tiles are rebuilt every `Build`; rigs are re-instantiated when the prefab changes.

---

## 6  Data formats and conventions

### 6.1  Directions and coordinates

| Where | Convention |
|---|---|
| `BoardState` | cells `[row, col]`; row 0 = top; `ShiftRow` + = right; `ShiftColumn` + = **down** |
| Case files, `LevelMove`, `TilewrapWrapPuzzle` | `line` 0-based; `direction` +1 = right / down, −1 = left / up |
| `Manager_Board` | board-local, centred on `_boardRoot`, x right, y up; `CellPosition(r, c) = ((c − 1)·2.9, (1 − r)·2.9)` |
| `Manager_Game.DragOffset` | `isRow ? cells.x : −cells.y` (**screen up = toward row 0 = negative column offset**) |
| World ↔ reference layout | px (x, y from top-left) → ((x − 540)/100, (960 − y)/100) |
| HUD / map | `UIBuilder.Place(x, y, w, h)` in top-left px; the map content is 1080 × 6000, top-left pivot, nodes at (x, −y) |

### 6.2  Serialized by ordinal — append only

`SoundType` (in `FileRef.asset`), `PopupName` (on each popup), `GameState`. **Never renumber, reorder or reuse a value.**

### 6.3  Content ids

Item ids (`luggage_03`), sprite keys (= the PNG name, `FileRef.Sprite`), rig ids (`luggage_0`, `crew_mia`,
`FileRef.Rig`) all come from the pack. A tile's id is its item's index in the case's `goalCells`. A rig layer is bound
to a goal cell **and** an item id, so a case's rigs must use its own item ids.

### 6.4  Rules implemented in more than one place

| Rule | Places | Risk |
|---|---|---|
| Cell ↔ board-local position | `Manager_Board.CellPosition` and its inverse in `TryGetCell` | Two sites. |
| Board / HUD geometry | `ReferenceLayout` (world board) and `UIBuilder` (subjects over 290 px columns at x 170, tabs centred on rows at y 800) | Move the board in one only and subjects / tabs no longer line up. |
| Wrap modulo | `BoardState.Wrap`, `Manager_Board.Mod`, `TilewrapWrapPuzzle.ApplyMove` | Same maths, three implementations. |
| Column direction sign | `BoardState.ShiftColumn`, `Manager_Board` offset, `Manager_Game.DragOffset` | Three sites. |
| Cell size | `ItemTile._size` (prefab), `AssetBuilder.CellSize`, `ReferenceLayout.CellSize` | Rebuild the prefab after a change. |
| "Matched" for a subject part | `BoardState.IsHome` (tiles, checks) and `TilewrapRigView.ApplyBoard` (item id in goal cell) | Equivalent while ids are unique per case (the case format guarantees it). |
| Board → hint table input | `Manager_Flow.NextMove` and `LevelValidator.Distance` (tile ids as strings, goal "0".."8") | Two copies. |

---

## 7  Assets → loaders

| Asset | Loaded by | How |
|---|---|---|
| `Resources/FileRef.asset` | `FileRef.Instance` | `Resources.Load` |
| `TilewrapVaried/Generated/TilewrapContentLibrary.asset` | `FileRef.Content` | serialized |
| `TilewrapVaried/Data/Levels/level_NNN.json` | `CaseLevelSource` | `library.levels` TextAssets |
| `TilewrapVaried/Data/campaign.json` | `FileRef.Campaign` → `Manager_Flow.ShowMap`, win (ending text) | `library.campaign` |
| `TilewrapVaried/Data/hint_distances.bytes` | `FileRef.HintDistances` → Hint, validator | `library.hintDistances` |
| `TilewrapVaried/Generated/Rigs/*.prefab` | `UI_SubjectCard` (HUD, win popup) | `FileRef.Rig(id)` |
| `TilewrapVaried/Sprites/**` | tiles, clue cards, hint, notebook, FX (`FileRef.Sprite`); UI sprites serialized by the builders | library / serialized |
| `TilewrapVaried/Audio/*` | `Manager_Audio` (TileMatch, LevelComplete) | `FileRef` sounds |
| `TilewrapVaried/Data/ui_layout.json`, `content.json` | editor only (layout numbers copied into the builders; the importer) | — |
| `Prefabs/Gameobjects/{Cell, ItemTile}.prefab` | `Manager_Board.Build` | `FileRef` |
| `Fonts/TilewrapUI-* SDF`, `… Masked.mat` | HUD, popups, tile names | made by `HandoffBuilder` |
| `Audio/Music/*.wav`, `Audio/SFX/*.wav` | `Manager_Audio` | `FileRef` |
| PlayerPrefs `OfflineData`, `PlaytestLog` | `OfflineData`, `PlaytestLog` | JSON |

### 7.1  Case files

`Assets/TilewrapVaried/Data/Levels/level_NNN.json` (schemaVersion 2, the pack's format; `TilewrapCaseData.Validate`
checks 3×3, nine unique goal ids, a permutation start, three subjects / categories, nine items each bound to its goal
cell).

| Key | Meaning |
|---|---|
| `id`, `title`, `goalText`, `setupText`, `completionText`, `difficulty` (tutorial / practice / hard), `chapterId` | shown / logged |
| `startingCells` / `goalCells` | 9 item ids, row-major. Tile id = index in `goalCells`. |
| `subjects[]` | `{id, name, column, rigId}` |
| `categories[]` | `{id, label, row}` |
| `items[]` | `{id, label, sprite, targetCell, categoryId, clue}`; `categoryId`'s row must be `targetCell / 3` |
| `moveLimit` / `minimumMoves` | 0 = unlimited / verified shortest solve |
| `solution` | `{axis, line, direction}`; replayed by the validator and the tester |
| `requiredFirstSwipe` | optional; levels 1 and 3 |
| `tutorial.entryText` | optional; levels 1–5; shown as a toast until the case is cleared |
| `firstClearCoins` / `nextLevelId` | 30 / next id (0 on case 30) |

To change content, replace `Assets/TilewrapVaried` from a new pack (keep `Generated` out of hand edits), run the pack's
importer, then **ShiftSort > Validate Levels**.

---

## 8  Wiring

```
Main Camera            orthographic, teal; fitted by Manager_Board.FitCamera
Managers               GameTuning, SingletonManager, Manager_Audio, _Level, _Board, _Input, _PopUp, _Game, _Flow
Sound                  Music, SFX AudioSources (volumes live here)
GamePlay
  Background           background_teal, scaled to cover the view
  Board                _boardRoot at ReferenceLayout.BoardCenter
    Frame, Dividers (4 Solid lines), Active Row (row_highlight), Mask (SpriteMask), Cells, Tiles, Guide (BoardGuide)
Canvas                 Screen Space Overlay, 1080×1920 match 0.5
  Container            SafeArea
    InGame             UI_InGame → Reference (UI_ReferenceFit): the HUD, incl. Subject 1-3, Category 1-3
    PopUps             UI_ReferenceFit; PopUp_Win, _Failed, _Hint, _Notebook, _LevelSelect (PopUp_Map), _Menu,
                       _Feedback (each: Dim + Holder), inactive until shown
    Overlay            UI_ReferenceFit → Toast (UI_Toast)
  Confetti             UI_Confetti + UI_ConfettiListener
  Loading Screen       UI_Loading
EventSystem            InputSystemUIInputModule
Tester                 TestManager (EditorOnly)
```

**Names are load-bearing:** builders and `AgentScripts/FlowCheck` find objects by path (`PopUp_{Name}/Holder/{Button}`,
`InGame/Reference/{Hint Button | Category 2 | Level Button}`, `GamePlay/Board/Cells/Cell r,c`).

**Sorting orders** (`SortOrder`): Background −10 · Board 0 · Divider 1 · ActiveRow 2 · TileFlash 3 · TileItem 5 ·
TileLabel 6 · TileCheck 9 · Guide 20–22 · Particles 25. Subjects and category tabs are uGUI (over the world).

---

## 9  Environment

| | |
|---|---|
| Unity | 6000.3.17f1, URP 17.3 with the 2D Renderer |
| Input | Input System only (`activeInputHandler: 1`) |
| Packages of note | `com.unity.pipeline` (CLI), `com.unity.test-framework` (no tests yet), uGUI 2.0 (TMP) |
| Packages | `xyz.candycoded.hapticfeedback` 1.0.3 (git) |
| Plugins | DOTween in `Assets/Plugins` (`DOTWEEN` define) |
| Assemblies | everything in `Assembly-CSharp` / `-Editor`; the pack's scripts are in namespace `Chichingo.TilewrapVaried` |

---

## 10  Working with it

```bash
P="--project-path C:/Project/Clue_Parade"
unity status $P
unity recompile $P --format json
unity command script.eval --code 'return LevelValidator.Validate();' $P
unity command editor.playmode.set_run_in_background --enabled true $P   # before Play from the CLI
unity command editor.playmode.start $P
unity command script run --file AgentScripts/PlayCheck.cs --entry PlayCheck.Status $P
unity command script run --file AgentScripts/PlayCheck.cs --entry PlayCheck.Swipe --args '[0, 0, 1, 0]' $P
unity command script run --file AgentScripts/FlowCheck.cs --entry FlowCheck.Hud --args '["Category 3"]' $P
unity command capture.game_view --width 540 --height 960 --output-path <file.png> $P
```

Gotchas:

- **Unfocused Editor = frozen Play mode** unless run-in-background is on.
- **The loading screen delays the level ~2 s** after Play starts.
- **Winning writes the save.** Back up first: `SaveBackup.Backup` / `SaveBackup.Restore` with a folder argument.
- **The pack importer takes minutes** (it reimports ~640 sprites); `script.eval` times out at 30 s but the import
  finishes in the Editor. Wait for `Generated/TilewrapContentLibrary.asset`.
- **Rebuilding a prefab or popup:** builders skip existing ones. Delete it (prefab, `PopUp_X`, `InGame/Reference` +
  `UI_InGame`) and rerun Build Assets / Build Scene. `FileRef` slots keep their old reference until cleared.
- **Batch mode** refuses a project that is open in the Editor.

---

## 11  Known gaps

| Item | Kind | Detail |
|---|---|---|
| Hint choice | simple | The first shortest-path move found (row before column); it does not prefer the selected category's line |
| Audio gains | unverified | The pack's match / win gains (0.08–0.10) are honoured; our own effects play at 1.0, so the match tones may be quiet next to them. Listen on a device |
| Ads / wallet | not built | The pack's profile: hints free, ads off. Coins are only counted |
| Diagonal rejection | different | Hold-and-drag locks to the dominant axis |
| Responsive layout | partial | The 1080×1920 reference is fitted uniformly (letterboxed); not checked on devices or notches |
| Texture memory | unverified | The importer caps case art at 512 px, ASTC 4×4; check on the oldest test device |
| Reduced motion | not built | |
| Report sharing | partial | Saved to `persistentDataPath`; no share sheet |
| Tests | never built | `BoardState`, `CaseLevelSource` and `PickClues` would be the first candidates |
| Touch on a device | unverified | Verified in the Windows Editor with PlayCheck / FlowCheck |

---

## 12  Conventions

- **Naming:** `Manager_X`, `PopUp_X` / `UI_X`, `[SerializeField] private _camelCase` with expression-bodied properties,
  global namespace (the pack keeps its own namespace).
- **One MonoBehaviour per file**, file named after the class. **Comments:** short `//` lines explaining why.
- **Log tags:** `[Game]`, `[Flow]`, `[FileRef]`, `[CaseLevelSource]`, `[AssetBuilder]`, `[SceneBuilder]`, `[UIBuilder]`,
  `[PopUpBuilder]`, `[LevelValidator]`, `[Singleton]`.
