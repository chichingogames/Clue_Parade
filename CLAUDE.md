# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

**Clue Parade** (Player Settings Product Name: **Tilewrap**, which also names builds; the code and menus still say ShiftSort, from the game it is being redesigned from) is a Unity **6000.3.17f1** mobile game (URP 2D template, `Assets/Game Assets/Scenes/Game.unity`, Input System). It implements the **Tilewrap Unity handoff**: the five onboarding levels with the full flow of the approved prototype (`Tilewrap_5_Level_Clues_Prototype_v2.html`): loading screen, HUD, lessons and guided first swipes, coach line, Hint, Notebook, win / out-of-moves popups, level selector, menu, first-clear coins, feedback and a local playtest report. All art comes from the handoff (`Assets/Tilewrap/`).

`skeleton.md` is the architecture blueprint this game is meant to follow (taken from a previous game, Color Farm). **Read it before adding any game code.** Its section 13 is the step-by-step bootstrap checklist. Where this file and `skeleton.md` disagree about the current repo state, this file is correct. `skeleton.md` describes the target, not what exists today.

Gaps between the blueprint and the repo right now:
- All animation goes through DOTween (no coroutines): the line slide is a tween in `Manager_Board.AnimateHeldLine`. TextMesh Pro comes with `com.unity.ugui` 2.0; TMP Essentials are imported (`Assets/TextMesh Pro/`) and the UI uses dynamic TMP font assets of the handoff's TilewrapUI fonts (`Fonts/TilewrapUI-* SDF`, made by `HandoffBuilder`).
- `activeInputHandler: 1` (new Input System only) is already set, so legacy `Input.*` calls will throw.
- The `skeleton.md` folder layout exists under `Assets/Game Assets/`, `Assets/Resources/Levels/` and `Assets/Plugins/`. Empty folders hold a `.gitkeep` (Unity ignores dot-files) so they survive a clone; delete it once the folder has content. The only scene is `Assets/Game Assets/Scenes/Game.unity` (the template's SampleScene, renamed). 
- URP assets stay in `Assets/Settings/`, and the project-wide `Assets/InputSystem_Actions.inputactions` stays at the root, because project settings reference them.
- `.gitignore` excludes `Library/`, `Temp/`, `Logs/`, `UserSettings/` and the generated `.csproj`/`.sln` files. Commit `Assets/`, `Packages/` and `ProjectSettings/`, including every `.meta` file.

## Game design

A redesign of Shift & Sort with the same core mechanics. Rules, flow, layout and art come from the **Tilewrap Unity
handoff** (scope document `Tilewrap_Unity_Implementation_Scope.docx` and the prototype, both outside the repo). The older
fair-booth pack and the Shift & Sort art were deleted (2026-11-27, in git history).

- **Handoff:** lives as delivered in `Assets/Tilewrap/`: sprites, `Data/game_config.json` (catalog, layout rects, timings), the 5 source levels, audio (not wired yet), fonts (TilewrapUI, used through TMP assets in `Fonts/`), and its import helper **Tools > Chichingo > Tilewrap > Apply sprite import settings** (run it after adding a sprite there).
- **Board:** N×N item tiles (3×3). **Columns are characters** (Mia, Leo, Zoe), **rows are categories** (Hats, Shirts, Bags). Each tile is an item with one correct cell (a *slot*). A slot is matched while its item is in it: the tile shows a check and the column's customer wears the item. Win: all 9 matched at the same time.
- **Clues and steps:** steps are the level's clue row order (default Hats → Shirts → Bags). The active step is the first category whose row is not complete: its row is highlighted (row highlight + gold category tab) and two of its clues are shown, tinted while matched (`BoardState.PickClues`: the first two in the file's order, unless both are matched while the third is not).
- **Customers** (the characters above the board) are modular: `Customer.prefab` holds five registered 600×900 layers (Bag Rear, Base, Shirt, Bag Straps, Hat) grouped into accessories per category; `Customer_{Name}.prefab` is a **prefab variant per person** that only assigns the sprites. `Manager_Board` instantiates each column's variant and switches an accessory's layers on with `SetActive` while that slot is matched (a pop when a move equips one; none on Undo).
- **Flow** (`Manager_Flow`, §3 of `ARCHITECTURE.md`): lessons on levels 1, 3, 4 (once per lesson id), a level's `requiredFirstSwipe` guided by `Board/Guide` (other swipes snap back, cost nothing, toast), coach line after every move, Hint = explain the first unmatched clue, then solve the current board on a worker thread (`BoardSolver`) and guide the next swipe (Hint button pulses after 16 idle seconds), Notebook (goal crew, all clues, rules), win popup after 0.38 s with confetti and the first-clear reward (+30, saved once), out-of-moves popup (Undo / Try again / Choose a level), level selector, menu (sound, restart, levels, save results, fresh test), feedback after level 5, every event in `PlaytestLog`.
- **Fail:** running out of moves before the board is complete.

Rules as implemented:
- **Hold and drag:** a press on the board picks up its row or column once the pointer has moved `DragStartThreshold` (a fraction of the shorter screen side). The axis is locked to the dominant direction. The line then follows the pointer, wrapping around the edge, but **never more than one cell** (`Manager_Game.MaxStepsPerMove`).
- **Release:** **a swipe moves exactly one cell and counts as one move.** It commits if the line was dragged at least `DragCommitThreshold` of a cell (default 0.5), otherwise it snaps back. A release within `FlickMaxDuration` commits anyway, so quick swipes still work. `ApplyMove` from code uses only the sign of `steps`.
- Levels are authored JSON files (schemaVersion 2), `Assets/Resources/Levels/level-NNN.json` (levels 1–5, converted from the handoff's `Data/Levels`; play loops over the files, so any level number opens one), loaded by `JsonLevelSource`. A file lists the characters, categories, the solved board's item ids, the start board (tile ids = home cells), the steps with goals and clues, the move budget and a reference solution (format in `ARCHITECTURE.md` §7.1). After adding or editing one, run **ShiftSort > Validate Levels**, which checks the items/characters exist in `FileRef` and replays the `solution`. It cannot check that the clues decide the answer.
- Progress (matched slots, active step, win) is derived from the board in `BoardState`, never stored: a move that breaks a matched slot removes its check and accessory, and Undo restores them.
- `moveBudget` from the file is the move limit (0 = unlimited).
- **Items and characters** are a catalog in `FileRef` (`_items`: id, display name, icon; `_characters`: id, name, Customer variant), filled by **ShiftSort > Build Assets** (`HandoffBuilder`) from `Assets/Tilewrap/Data/game_config.json`. Fix a display name in `FileRef` or in the config before a refill.
- **Board look** (world space, the handoff's 1080×1920 layout, `ReferenceLayout`): teal background, the cream board panel (`board_frame`) with grid dividers, `ItemTile`s (item icon only, flash on a new match, sparks), check badges on matched cells, the active row's highlight and gold category tab, tabs with category icons on the left, the customers above it (`Board/Characters`: a `Customer Stand` per column that the person's Customer variant is instantiated into, name tabs with symbols) and the guide. The camera shows the whole 10.8×19.2 reference screen fitted to the safe area, so the HUD on the same layout lines up.
- **HUD** (`UI_InGame`, built by **ShiftSort > Build UI** under `Canvas/Container/InGame/Reference`, rects from `game_config.json` `layoutTopLeft`): level pill (→ level selector), moves / Practice panel, menu, title, goal line, "CLUES · HATS" + "n/3 matched" + "All 9" (→ Notebook), two clue cards (green + check while matched), coach line, match meter, control note, Undo / Notebook / Hint (no brand footer). The toast lives in `Container/Overlay` (over popups). **Popups** (`PopUp_X` under `Container/PopUps`, built by **ShiftSort > Build PopUps**) follow skeleton.md §9.2: `Popup` → `BasePopUp` → `PopUp_X.Setup(callbacks)`, registered by `Manager_PopUp`; input is gated while one is open. The loading screen (handoff style) holds `Manager_Game.Launch()` until its bar is full; the board and HUD intros play as it fades.
- **Audio:** `Manager_Audio.Instance?.PlaySound(SoundType.X)`; clips are mapped in `FileRef` by **ShiftSort > Build Assets** from `Audio/SFX/{snake_case SoundType}[_NN].wav` (variants) and the first `Audio/Music/*.wav`. Adding a sound: append a `SoundType` value, drop the file, clear `FileRef`'s Sounds table, run Build Assets. One HUD switch mutes music and effects (saved in `OfflineData`). Volumes live on the scene's `Sound/Music` (background music level) and `Sound/SFX` (master effects level) AudioSources; `Manager_Audio` on `Managers` only references them and never writes their volume.
- **Tutorial:** the handoff lessons (`lesson` + `requiredFirstSwipe` in the level files), shown by `Manager_Flow`; acknowledgements are saved in `OfflineData.Lessons`. Menu > Start a fresh test (or the Tester's Clear Saved Progress) resets them.
- **Haptics:** `Haptics.Play(HapticType.Light/Medium/Heavy)` (Essentials) over the CandyCoded HapticFeedback package (git, v1.0.3). Always on, no setting by design. One pulse per event, called next to the matching sound. It does nothing in the Editor; use the Tester's **Log Sounds & Haptics** to check it.
- **Tester:** the `Tester` GameObject (tag `EditorOnly`, stripped from builds) holds `TestManager`, an Inspector cheat panel: load/next/restart level, force win/fail, play the level's solution, shift a line, log state, time scale. Its body is `#if UNITY_EDITOR`; buttons use `[Sortify.Button]` (`Scripts/Sortify/`) on parameterless methods. Add new cheats there, never as game code paths.

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
- **Adding an item or character:** drop the art in `Assets/Tilewrap/Sprites/Items/{id}.png` or `Characters/{id}/{id}_{base|shirt|hat|bag_rear|bag_straps}.png` (registered 600×900 layers), add it to `Data/game_config.json` (`items` / `recipients`, and `sprites` for its import settings; run the pack's import helper), clear `FileRef`'s Items / Characters table, run **ShiftSort > Build Assets** (it builds a missing `Customer_{Name}` variant), then check the display names. Level files refer to items and characters by id. An accessory's category must equal the level's row label (`Customer._accessories[].Category`, "Hats").
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
