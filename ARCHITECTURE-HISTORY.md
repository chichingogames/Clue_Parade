# Architecture history

Append-only. Newest first. One entry per documentation run.

---

## 2026-11-29 — branch `any-row` on `4489eeb`

**The v5 content pack** (`Tilewrap_30_Level_Unity_Production_v5`, `Assets/TilewrapMystery`) replaces `TilewrapVaried`,
`Data/delivery_cases.json` and `Sprites/Evidence`. Level content is identical to the HTML conversion; new are the
UI skins, the 90 fitted rig prefabs, per-set `coaching[]`, `nextLevelId`, 13 new sounds and the mystery loop, and the
animated background. Added `Data/levels_01_30.json` (missing from the delivery; the pack's importer reads it).
`FileRef`, `DeliveryContent`, `UI_SubjectCard` (rig prefabs, three-icon found-objects tray), `AssetBuilder`
(pack sounds), `SceneBuilder` (background canvas), `Manager_Flow` (coaching lines, sound events), `Manager_Board`
(cascade-tier match sounds, refill, the winning delivery now plays level_win). `SoundType` +7 values. New saves start
with 60 coins. **Verified:** the pack importer (all 30 routes), compile clean, 0 missing scene references, Validate
Levels 30 OK; Play mode: case 1 (guide, invalid sound, win popup with prefab rigs), case 10 (badges, tray, order
tabs, coaching, level_win + coins).

---

## 2026-11-29 — branch `any-row` on `136a9e8`

**"Follow the Evidence": 30 deduction cases** (sources: `Tilewrap_Deduction_30_Level_Playtest.html`,
`Tilewrap_Game_Scope_v5.pdf`). `delivery_cases.json` regenerated (rigs per case, clue rules and pools, dossiers,
resolutions, deductions, economy, chapters); 12 evidence sprites in `Sprites/Evidence`. `DeliveryRules`: 3 or 4
rows, hybrid order, continues, per-height planners, `AnyPendingSetClears`. `Manager_Game`: save / resume of the
attempt, `Continue`. `Manager_Board`: variable rows, no partial tints, slot settle. `Manager_Flow`: per-case coach,
clue paging, explain-first Hint with coin cost, Notebook tabs, chapters map, deduction, +5 moves continue, win with
answer / evidence / lead. New `PopUp_Deduction` (`PopupName.Deduction = 9`); rebuilt HUD and every popup but the
menu. Earlier on this branch: tile names removed, the feedback screen removed, cases cycle. **Verified** in the
Editor: compile clean; Validate Levels 30 OK (case 17 parks its third set complete at the start, which the
any-pending rule clears on the first swap); Play mode: case 1 hint route to the win, case 9 clue paging / notebook /
paid hint, case 13 (4 rows), case 15 deduction (wrong then right, +90), case 4 counter spotlight / out of moves /
continue once / undo, resume after leaving Play mode, case 30 finale and the cycle to case 1, map scroll.

---

## 2026-11-28 — branch `any-row` on `7de3f53`

**"Any row, two clues" replaces the 30-case game** (source: `Tilewrap_Any_Row_5_Case_Playtest.html`). Five cases in
`Data/delivery_cases.json`; new `DeliveryContent`, `DeliveryRules` (port of the playtest's DeliveryCore), `PopUp_Cases`;
rewritten `Manager_Game`, `Manager_Board`, `Manager_Flow`, `UI_InGame`, `UI_SubjectCard`, `ItemTile`, `PopUp_Hint`,
`PopUp_Notebook`, the HUD and popup builders, the validator and the Tester. Removed: `BoardState`, `LevelDefinition`,
`CaseLevelSource`, `SwapPuzzle`, `PopUp_Map`, `UI_CategoryTab`, `TileData`. **Verified** in the Editor: compile clean,
Validate Levels 5 OK (hint route wins each case within its limit); Play mode: case 1 guided swap (wrong swap refused),
case 2 hint route to the win (refills, extras on the win popup), case 5 order tabs + tap-tap delivery in the middle row
with a refill, notebook, hint, case list, case 4 clue focus.

---

## 2026-11-28 — branch `swap-mechanic` on `047db43`

**Neighbour swaps replace row / column wraps; tutorial disabled.** New `SwapPuzzle` (BFS over 9! boards, `SwapMove`);
`BoardState.Swap`; `Manager_Board` holds a pair instead of a line (no ghost); `Manager_Game` picks the neighbour from
the drag direction; move limits recomputed from the swap minimum plus the pack's slack; Hint / guide / Tester / validator
/ HUD note moved to swaps; `requiredFirstSwipe`, the tutorial toast and the wrap solution are no longer used.
**Verified** in the Editor: compile clean, Validate Levels 30 OK, case 4 in Play mode (swap, edge drag ignored,
snap-back, undo, hint + guide, shortest solve to the win popup in 6 swaps).

---

## 2026-11-28 — `1ea9dea` + uncommitted

**The 30-case content pack replaces the five-case Mia/Leo/Zoe build** (source: `Tilewrap_30_Level_Unity_Production`
and `Tilewrap_Ten_Case_Plan_and_Storyboards.pdf`). `ARCHITECTURE.md` was rewritten to match.

- **Content:** `Assets/Tilewrap` was moved (GUIDs kept) to `Assets/TilewrapVaried` and overwritten by the pack: 30 case
  files, campaign map, `ui_layout.json`, hint table, ~640 sprites, 4 sounds, the pack's runtime scripts
  (`Chichingo.TilewrapVaried`) and importer. Its `Generated/` library and 90 rig prefabs are made by the importer.
- **Model:** `BoardState` lost steps (`StepRows`, `ActiveStep`, `ActiveRow`); gained `FirstIncompleteRow` and a
  row-based, unsatisfied-first `PickClues`. `LevelDefinition` holds subjects, categories, items (label, sprite, clue),
  difficulty, next id, coins, tutorial text. `CaseLevelSource` replaces `JsonLevelSource`; `Resources/Levels` is gone.
- **Board:** cells 290 px with names under the icons (masked TMP material), checks top-right, `SelectedRow` + category
  tab selection, fx_ring / fx_star match effects, the pack's match_1..3 / level_win. Customers, category tags and
  `CharacterRow` left the world; subjects (`UI_SubjectCard` over `TilewrapRigView`) and tabs (`UI_CategoryTab`) are HUD.
- **Flow:** no lessons, no coach line; Hint is synchronous over the hint table (`BoardSolver` removed); Notebook is a
  grid; win shows the finished rigs, reward and "Up next"; `PopUp_Map` (the 30-node case map) replaces the level list.
- **Removed as unused:** `Customer*`, `CategoryTag` prefabs and scripts, `UI_CrewPortrait`, `PopUp_Lesson`,
  `PopUp_LevelSelect`, `BoardSolver`, `JsonLevelSource`, the OpenSans and TilewrapNunito fonts, five replaced / unplayed
  SFX, `Canvas/Dark Area` and `Canvas/Transition Screen`, ten stale one-off AgentScripts.
- **Menu** trimmed to sound, restart, choose a case and back: "Save results (JSON)" and "Start a fresh test" (with
  `PopUp_Confirm`) removed. The report is still exported when the end-of-campaign feedback is saved; the Tester's
  Clear Saved Progress still wipes the save in the Editor.
- **Verified** in the Editor: Validate Levels (30 OK, incl. hint-table minimum), cases 1, 2, 9, 12, 30 in Play mode.

---

## 2026-11-10 17:31 +0600 — `8c8211f`

— initial entry —

Recorded against `8c8211f` ("gameplay done") **plus uncommitted work** on top of it: the loading screen
(`UI_Loading.cs`, `Canvas/Loading Screen`, `UIBuilder.BuildLoading`, `Manager_Game.Launch/HoldLaunch`,
`Tuning`/`GameTuning` Loading screen values), the new branding art and the app icon swap. `ARCHITECTURE.md`
already describes that uncommitted state.

**Scale** 35 C# scripts (4.3k lines; 7 Editor-only, 3 copied from Sortify), 1 game scene (`Game.unity`, plus
the URP template scene), 3 prefabs (Cell, Tile, TargetMarker), 12 level JSON files (`level-001`…`level-012`),
5 managers (Audio, Level, Board, Input, Game). Third-party: DOTween (in `Assets/Plugins/Demigiant`), CandyCoded
HapticFeedback (git, v1.0.3), TextMesh Pro via `com.unity.ugui` 2.0, Unity CLI pipeline (`com.unity.pipeline`
0.10.0-exp.1).

**Scope** §1–12 written: what this is, repo map, boot → level → drag → win flow (with the loading-screen
launch hold), per-module reference (Essentials, managers, model/levels, views, configuration, Editor builders),
cross-cutting patterns, data formats and conventions (§6.4 duplicated rules), asset → loader table and the
level file format, scene wiring, environment, CLI testing gotchas, known gaps, conventions.

**Baseline state**
- Rules duplicated (§6.4): cell ↔ position (2 sites), marker distance in `Build` and `FitCamera` (2 sites),
  wrap modulo (2 implementations), column direction sign (3 sites), per-line shape/color equality (3 methods),
  "which shapes/colors have art" (runtime vs builder).
- Never built: popups (`Manager_PopUp`, `BasePopUp`), the transition screen, pooling, tests (Test Framework
  installed, no test assembly; game code has no asmdef). `PopUps`, `Dark Area`, `Transition Screen` are
  empty placeholders.
- The loading bar is timed (1.6 s fill, 0.4 s fade), not tied to real loading. The studio splash is not part of
  the game code.
- Art delivered but unused: board trays, background, most `fx`, `target_complete_background`. Target markers
  still use a tinted generated square, unlike the mockups.
- Level `Name`/`Instruction`/`Tutorial` are loaded; only `Instruction` is shown (message panel body).
  Generated levels (13+) have no move limit and no text.
- Built but unreachable or unread: `Manager_Board.ShiftRow/ShiftColumn` (no game-code caller), `Cell._background`,
  `Cell.Row/Column`; `InputSystem_Actions.inputactions` is unused.
- Known quirks: a multiple of N steps adds moves without changing the board (unreachable while moves are
  capped at one cell); a flick back to offset 0 commits +1; presses during a slide are dropped.
- Unverified: touch on a real Android/iOS device (Editor-simulated input only), haptics on a device, refit on
  window resize mid-level.
- One single sound switch (music + SFX); haptics always on, by design.
- Doc inaccuracy found while recording: the §11 gap "Nearly everything is uncommitted … on top of `2241067`"
  is stale. Two commits (`d8d6ba0`, `8c8211f`) have landed since; only the loading screen, branding art and
  the icon swap are uncommitted now. The §11 "Level text" row says nothing shows `Instruction`, but
  `UI_InGame` uses it as the message panel body while playing.
