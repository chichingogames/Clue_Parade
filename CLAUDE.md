# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

**Clue Parade** (Player Settings Product Name: **Tilewrap**, which also names builds; the code and menus still say ShiftSort, from the game it is being redesigned from) is a Unity **6000.3.17f1** mobile game (URP 2D template, `Assets/Game Assets/Scenes/Game.unity`, Input System). It implements the **30-case Tilewrap content pack** (`Tilewrap_30_Level_Unity_Production`, with `Tilewrap_Ten_Case_Plan_and_Storyboards.pdf`): a new situation in every case (crew, pets, rooms, cakes, robots, bags, …) on the same swipe-and-wrap board, with the storyboards' HUD, guided first swipes on cases 1 and 3, Hint, Notebook, win / out-of-moves popups, the case map, menu, first-clear coins, feedback and a local playtest report. All content comes from the pack (`Assets/TilewrapVaried/`).

`skeleton.md` is the architecture blueprint this game is meant to follow (taken from a previous game, Color Farm). **Read it before adding any game code.** Its section 13 is the step-by-step bootstrap checklist. Where this file and `skeleton.md` disagree about the current repo state, this file is correct. `skeleton.md` describes the target, not what exists today.

Gaps between the blueprint and the repo right now:
- All animation goes through DOTween (no coroutines): the swap slide is a tween in `Manager_Board.AnimateHeldPair`. TextMesh Pro comes with `com.unity.ugui` 2.0; TMP Essentials are imported (`Assets/TextMesh Pro/`) and the UI uses dynamic TMP font assets of the pack's TilewrapUI fonts (`Fonts/TilewrapUI-* SDF`, plus a masked material for tile names, made by `HandoffBuilder`).
- `activeInputHandler: 1` (new Input System only) is already set, so legacy `Input.*` calls will throw.
- The `skeleton.md` folder layout exists under `Assets/Game Assets/`, `Assets/Resources/` and `Assets/Plugins/`. Empty folders hold a `.gitkeep` (Unity ignores dot-files) so they survive a clone; delete it once the folder has content. The only scene is `Assets/Game Assets/Scenes/Game.unity` (the template's SampleScene, renamed). 
- URP assets stay in `Assets/Settings/`, and the project-wide `Assets/InputSystem_Actions.inputactions` stays at the root, because project settings reference them.
- `.gitignore` excludes `Library/`, `Temp/`, `Logs/`, `UserSettings/` and the generated `.csproj`/`.sln` files. Commit `Assets/`, `Packages/` and `ProjectSettings/`, including every `.meta` file.

## Game design

A redesign of Shift & Sort with the same core mechanics. Rules, flow, layout and content come from the **Tilewrap
30-level content pack** and the **ten-case plan and storyboards** (both outside the repo, in the downloads they were
delivered in). The five-case Mia/Leo/Zoe build (Customer prefabs, lessons, coach line, step clues) was replaced on
2026-11-28 (in git history).

- **Content pack:** lives as delivered in `Assets/TilewrapVaried/`: `Data/Levels/level_001..030.json` (the cases),
  `Data/campaign.json` (chapters, map), `Data/ui_layout.json` (1080×1920 regions, sizes, timings), `Data/content.json`
  (manifest), `Data/hint_distances.bytes`, sprites, audio, fonts, its runtime scripts (namespace
  `Chichingo.TilewrapVaried`) and its importer **Tools > Chichingo > Tilewrap > Build 30-level content**, which makes
  `Generated/TilewrapContentLibrary.asset` and the 90 rig prefabs. Run it after replacing the pack, then **ShiftSort >
  Validate Levels**. Never hand-edit `Generated/`.
- **Board:** 3×3 item tiles (icon + name). **Columns are subjects** (per case: Mia/Leo/Zoe, three pets, three rooms…),
  **rows are categories** (per case). Each tile is an item with one correct cell (a *slot*) and one clue fact. A slot
  is matched while its item is in it: the tile shows a check and the column's subject rig shows the fitted part. Win:
  all 9 matched at the same time. Correct tiles stay movable; nothing is locked.
- **Swap mechanic (branch `swap-mechanic`):** instead of the pack's row / column wrap, a move drags one tile onto
  one of its **4 neighbours** and the two trade places (match-3 style swap, no matching or cascades; nothing wraps).
  The tutorial is off: the pack's `requiredFirstSwipe`, `tutorial.entryText` and wrap `solution` are ignored.
- **Clues:** the selected category's row is highlighted (row highlight + gold tab) and two of its facts are shown,
  unsatisfied first (`BoardState.PickClues`). The selected category is the player's tab choice (a tap costs no move)
  until a move completes that row, else the top incomplete row (`Manager_Board.SelectedRow`). Notebook shows all nine.
- **Subjects** are the pack's registered rigs (`TilewrapRigView`: one base plus layers bound to a goal cell and item
  id), shown in the HUD above the columns by `UI_SubjectCard` (new parts fade in, the card pops) and finished on the
  win popup.
- **Flow** (`Manager_Flow`, §3 of `ARCHITECTURE.md`): Hint = the selected category's first unsatisfied fact, then
  the next swap of a shortest solve from the current board (`SwapPuzzle.TryNextMove`), "Show the next swipe" draws
  the guide on the two cells (Hint pulses after 16 idle seconds); Notebook; win popup after 0.38 s with confetti, the finished subjects, the first-clear reward (+30,
  saved once) and "Up next"; out-of-moves popup (Retry / Undo / Choose a case); the case map (30 nodes, unlocked by
  clearing the case before); menu; feedback after case 30; every event in `PlaytestLog`.
- **Difficulty:** cases 1–3 unlimited, then a move limit; Hard cases (12, 16, 20, 24, 28, 30) show the Hard badge.
- **Fail:** running out of moves before the board is complete.

Rules as implemented:
- **Hold and drag:** a press on a tile picks it up once the pointer has moved `DragStartThreshold` (a fraction of the shorter screen side). The dominant direction (right, left, up, down) picks the neighbour, locked for the press; towards the board edge nothing happens. The pair then follows the pointer, trading places up to one cell.
- **Release:** **a swap counts as one move.** It commits if the tile was dragged at least `DragCommitThreshold` of a cell (default 0.5), otherwise it snaps back. A release within `FlickMaxDuration` commits anyway, so quick swipes still work. `ApplyMove(SwapMove)` from code makes the same swap; Undo makes it again.
- Levels are the pack's case files (`Assets/TilewrapVaried/Data/Levels/level_NNN.json`, schemaVersion 2), reached through `FileRef.Content` and converted by `CaseLevelSource` (format in `ARCHITECTURE.md` §7.1; tile id = the item's index in `goalCells`). **ShiftSort > Validate Levels** checks structure, art and rigs, the `nextLevelId` chain and that the swap minimum fits the move limit. It cannot check that the clues decide the answer.
- Progress (matched slots, fitted parts, win) is derived from the board in `BoardState`, never stored: a move that breaks a matched slot removes its check and part, and Undo restores them.
- **Move limits are recomputed for swaps** (`CaseLevelSource`): `MinimumMoves` = `SwapPuzzle.Distance` of the start board (a BFS over all 9! boards, built once on first use), and a limited case (file `moveLimit` > 0) gets that minimum plus the pack's own slack (`moveLimit - minimumMoves`, at least 2). 0 = unlimited. Case 30's `nextLevelId` is 0: its win leads to the feedback and the map.
- **Items, sprites and rigs** come from the content library: `FileRef.Sprite(key)`, `FileRef.Rig(id)`, `FileRef.Campaign`, `FileRef.HintDistances`. Item names and clue text are in the case files.
- **Board look** (world space, `ui_layout.json`'s 1080×1920 layout, `ReferenceLayout`: cells 290 px): teal background, the cream board panel (`board_frame`) with grid dividers, `ItemTile`s (icon + name, flash, fx_ring + fx_star on a new match), check badges top-right on matched cells, the selected row's highlight and the guide. The camera shows the whole 10.8×19.2 reference screen fitted to the safe area, so the HUD on the same layout lines up (subjects above the columns, category tabs beside the rows are uGUI).
- **HUD** (`UI_InGame`, built by **ShiftSort > Build UI** under `Canvas/Container/InGame/Reference`, rects from `ui_layout.json` regions): level pill (→ case map), menu, Hard badge, moves / Practice panel, title, goal line, "CLUES" + "All facts in Notebook", two clue cards (green + check while satisfied), subject cards with name tabs, category tabs (tap to select), control note, "x/9 matched", Undo / Notebook / Hint. The toast lives in `Container/Overlay` (over popups). **Popups** (`PopUp_X` under `Container/PopUps`, built by **ShiftSort > Build PopUps**) follow skeleton.md §9.2: `Popup` → `BasePopUp` → `PopUp_X.Setup(callbacks)`, registered by `Manager_PopUp`; input is gated while one is open. The loading screen (handoff style) holds `Manager_Game.Launch()` until its bar is full; the board and HUD intros play as it fades.
- **Audio:** `Manager_Audio.Instance?.PlaySound(SoundType.X)`; clips are mapped in `FileRef` by **ShiftSort > Build Assets**: TileMatch = the pack's match_1..3 and LevelComplete = level_win (at the manifest gains, `ClipVolumes`), the rest from `Audio/SFX/{snake_case SoundType}[_NN].wav` (variants), music = the first `Audio/Music/*.wav`. Adding a sound: append a `SoundType` value, drop the file, clear `FileRef`'s Sounds table, run Build Assets. One HUD switch mutes music and effects (saved in `OfflineData`). Volumes live on the scene's `Sound/Music` (background music level) and `Sound/SFX` (master effects level) AudioSources; `Manager_Audio` on `Managers` only references them and never writes their volume.
- **Tutorial:** disabled. No lesson popups, no guided first swipe, no entry-text toast. `OfflineData.Lessons` is retired (kept, append-only).
- **Haptics:** `Haptics.Play(HapticType.Light/Medium/Heavy)` (Essentials) over the CandyCoded HapticFeedback package (git, v1.0.3). Always on, no setting by design. One pulse per event, called next to the matching sound. It does nothing in the Editor; use the Tester's **Log Sounds & Haptics** to check it.
- **Tester:** the `Tester` GameObject (tag `EditorOnly`, stripped from builds) holds `TestManager`, an Inspector cheat panel: load/next/restart level, force win/fail, play a shortest solve, swap a tile, log state, time scale. Its body is `#if UNITY_EDITOR`; buttons use `[Sortify.Button]` (`Scripts/Sortify/`) on parameterless methods. Add new cheats there, never as game code paths.

## Commands

There is no CLI build or lint setup. Work happens in the Unity Editor. Headless runs go through the Editor executable (the path depends on the Unity Hub install, typically `C:\Program Files\Unity\Hub\Editor\6000.3.17f1\Editor\Unity.exe`):

```
# No tests exist yet (com.unity.test-framework is installed). EditMode run; use -testPlatform PlayMode for play-mode tests
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml
# Run a single test or fixture
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter "Namespace.Fixture.Method"
```

Batchmode fails if the project is already open in the Editor.

Android builds from the Build window are named `[GameName]_[version]_[date]_[additionalInformation].apk` (or `.aab`) by `Scripts/Editor/BuildNaming.cs`: GameName = Player Settings Product Name, version = Player Settings Version, date = build day, additional information from **ShiftSort > Build Name Settings** (shows a preview). Scripted builds must call `BuildNaming.Apply(ref options)`.

The Unity CLI (`unity`, documented in the `unity-cli` skill) can drive the open Editor live through the `com.unity.pipeline` package. Use it instead of hand-editing scene YAML. Always pass `--project-path C:/Project/Clue_Parade`.

```
unity status --project-path C:/Project/Clue_Parade                       # Editor connected and "ready"?
unity recompile --project-path C:/Project/Clue_Parade --format json      # compile and report errors
unity command script.eval --code 'SceneBuilder.Build(); return "ok";' --project-path C:/Project/Clue_Parade
unity command scene.get_hierarchy --project-path C:/Project/Clue_Parade --format json
unity command console.get --tail 20 --level warning --project-path C:/Project/Clue_Parade --format json
```

## Architecture as built

**Read `ARCHITECTURE.md` before changing game code.** It holds:
- the boot → level → drag → win call trace (§3);
- every module, with what it owns and who calls it (§4);
- the direction and coordinate conventions and the rules duplicated across files (§6). Check §6.4 before touching layout, directions or matching.
- the asset → loader table (§7), scene wiring (§8), CLI testing gotchas (§10) and known gaps (§11).

Keep it current: when you change a system, edit its section there, not here. Things to always do:
- Rule changes go in `BoardState` (the pure model). Views get their sprites and names from `Manager_Board` and never read `FileRef` or managers.
- **Unity skills:** drive the Editor through the `unity-cli` / `unity-pipeline` skills. Turn on `editor set_auto_tick` for headless work; put multi-line C# in `AgentScripts/*.cs` (outside `Assets/`) and run it with `unity command script run --file ... --entry ...`. `AgentScripts/PlayCheck.cs` reports the level state and makes real swipes, holds and undos in Play mode.
- **Changing content:** replace `Assets/TilewrapVaried` with a new delivery of the pack (keep the folder's `.meta` files so references survive), run **Tools > Chichingo > Tilewrap > Build 30-level content**, then **ShiftSort > Validate Levels**. Cases, items, rigs and the map are data; no game code names a subject or item.
- The builders (`AssetBuilder`, `SceneBuilder`) never overwrite an existing asset, prefab or filled reference. Change an existing prefab with `PrefabUtility.LoadPrefabContents`, or delete it and rebuild.
- Before testing in Play mode from the CLI, run `unity command editor.playmode.set_run_in_background --enabled true --project-path C:/Project/Clue_Parade`. Otherwise the unfocused Editor freezes at frame 1–2 and every press is rejected.

## Architecture (target, per skeleton.md)

- **One scene.** A `Managers` GameObject holds every `Manager_*` singleton plus `SingletonManager` and `GameTuning`. The other roots are `GamePlay` (world objects placed by hand) and `Canvas` (HUD and popups).
- **Managers decide, views display.** Managers own rules and state, and expose methods for commands and events for changes. Views (prefab MonoBehaviours, UI) never mutate state.
- **Singletons** derive from `Singleton<T>` and put their init in `Awake2()`, never `Awake()`. `SingletonManager.Awake` loads the save (`OfflineData.Load()`, once) and then initializes singletons in the order of its hand-wired Inspector list. A new manager has to be added to that list to get ordered init.
- **`Manager_Game`** owns the `GameState` machine and the per-frame tick order. Other managers expose `Tick(dt)` instead of running gameplay in their own `Update`. This game is turn-based and has no per-frame simulation yet. Pause is derived from popups, tutorials and intros, never stored as a flag.
- **Listeners subscribe, then read the current value right away**, because Unity doesn't order `Start` across scripts. Events default to `delegate { }`.
- **Gameplay numbers** live in `Tuning` (const defaults plus static facade) and `GameTuning` (scene overrides). The scene values are what ship. **Assets** are assigned only in `FileRef` (a `ScriptableSingleton` at `Resources/FileRef.asset`).
- **Save** is `OfflineData` (`Essentials/OfflineData.cs`): public fields only, JSON in one PlayerPrefs key, append-only, one writer per field. Today: `CurrentLevel` + `PassedLevels` (written on a win by `Manager_Game.SaveProgress`), `IsSoundOn` (`Manager_Audio.SetSoundOn`), and `CompletedLevels`, `Coins`, `Lessons`, `Feedback*` (`Manager_Flow`). Playtest events are a separate key (`PlaytestLog`). The game opens on the saved level; the Tester's **Clear Saved Progress** resets it. `LiveData` (unsaved session state) is not needed yet.
- **Serialized, saved or bit-indexed enums are append-only and explicitly numbered** (e.g. `PopupName`, `GameState`).
- Code lives in `Assembly-CSharp` in the global namespace. Naming follows `Manager_X`, `PopUp_X`, `UI_X`, with `[SerializeField] private _camelCase` fields exposed through expression-bodied properties.
- Scene object names are load-bearing, because builders find children by path. Scene structure that gets regenerated goes through re-runnable Editor `[MenuItem]` builders, not hand-edited scene YAML.
- `skeleton.md` flags two bugs in the original framework. The duplicate-singleton guard (section 3) is already fixed here. `OfflineData.Load()` is called only once, in `SingletonManager.Awake` (section 4); keep it that way.
