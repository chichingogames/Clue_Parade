# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

**Clue Parade** (Player Settings Product Name: **Tilewrap**, which also names builds; the code and menus still say ShiftSort, from the game it is being redesigned from) is a Unity mobile game in its earliest stage: a fresh Unity **6000.3.17f1** project from the URP 2D template (2D Renderer, `Assets/Game Assets/Scenes/Game.unity`, Input System package). The core gameplay loop works (item board, swipes, steps, JSON levels, win, fail, undo) with the in-game HUD; the HUD follows the art pack's layout. Music and sound effects play through `Manager_Audio`, and `OfflineData` saves level progress and the sound switch. A loading screen (`UI_Loading`) opens the app. There are no popups yet.

`skeleton.md` is the architecture blueprint this game is meant to follow (taken from a previous game, Color Farm). **Read it before adding any game code.** Its section 13 is the step-by-step bootstrap checklist. Where this file and `skeleton.md` disagree about the current repo state, this file is correct. `skeleton.md` describes the target, not what exists today.

Gaps between the blueprint and the repo right now:
- All animation goes through DOTween (no coroutines): the line slide is a tween in `Manager_Board.AnimateHeldLine`. TextMesh Pro comes with `com.unity.ugui` 2.0; TMP Essentials are imported (`Assets/TextMesh Pro/`) and the HUD uses dynamic Open Sans font assets in `Fonts/`.
- `activeInputHandler: 1` (new Input System only) is already set, so legacy `Input.*` calls will throw.
- The `skeleton.md` folder layout exists under `Assets/Game Assets/`, `Assets/Resources/Levels/` and `Assets/Plugins/`. Empty folders hold a `.gitkeep` (Unity ignores dot-files) so they survive a clone; delete it once the folder has content. The only scene is `Assets/Game Assets/Scenes/Game.unity` (the template's SampleScene, renamed). 
- URP assets stay in `Assets/Settings/`, and the project-wide `Assets/InputSystem_Actions.inputactions` stays at the root, because project settings reference them.
- `.gitignore` excludes `Library/`, `Temp/`, `Logs/`, `UserSettings/` and the generated `.csproj`/`.sln` files. Commit `Assets/`, `Packages/` and `ProjectSettings/`, including every `.meta` file.

## Game design

A complete visual redesign of Shift & Sort with the same core mechanics, in a fair-booth theme. Art, layout and reference
come from the **Tilewrap fair-booth art pack v1** (copied into `Sprites/Tilewrap/`; the pack's own Editor/Runtime scripts
were never compiled and are not used). Reference screen: `Approved_Reference_1080x1920.png` in the pack.

- **Board:** N×N item tiles (3×3 now). **Columns are characters** (Mia, Leo, Zoe), **rows are categories** (Hats, Gear, Bags). Each tile is an item with one correct cell; solved, every character has their item in every category.
- **Clues and steps:** the level is played one row per step (e.g. Gear, then Bags, then Hats). The active step's row is highlighted (honey tiles + outline) and its goal and clues are shown ("Mia carries the red bag."). A step is *confirmed* (checks on its row) once its row is all in place; then the next step starts. Per-tile correctness is never shown or sounded, so the clues matter. Win: every step confirmed.
- **Characters** change pose as steps are confirmed (neutral → gear → bag → complete); the poses in the pack fit level 1's solution only.
- **HUD (target):** level and step, goal, moves left, clue notes with item icons, characters on nameplates above the board, "Swipe rows or columns. Tiles wrap.", Undo / Notebook / Hint (Notebook and Hint are placeholders for now).
- **Fail:** running out of moves before the board is complete.

Rules as implemented:
- **Hold and drag:** a press on the board picks up its row or column once the pointer has moved `DragStartThreshold` (a fraction of the shorter screen side). The axis is locked to the dominant direction. The line then follows the pointer, wrapping around the edge, but **never more than one cell** (`Manager_Game.MaxStepsPerMove`).
- **Release:** **a swipe moves exactly one cell and counts as one move.** It commits if the line was dragged at least `DragCommitThreshold` of a cell (default 0.5), otherwise it snaps back. A release within `FlickMaxDuration` commits anyway, so quick swipes still work. `ApplyMove` from code uses only the sign of `steps`.
- Levels are authored JSON files (schemaVersion 2), `Assets/Resources/Levels/level-NNN.json` (only level 1 exists; play loops over the files, so any level number opens one), loaded by `JsonLevelSource`. A file lists the characters, categories, the solved board's item ids, the start board (tile ids = home cells), the steps with goals and clues, the move budget and a reference solution (format in `ARCHITECTURE.md` §7.1). After adding or editing one, run **ShiftSort > Validate Levels**, which checks the items/characters exist in `FileRef` and replays the `solution`. It cannot check that the clues decide the answer.
- Progress (confirmed steps, active row, win) is derived from the board in `BoardState`, never stored: a column move that breaks a confirmed row takes the level back to that step, and Undo repairs it.
- `moveBudget` from the file is the move limit (0 = unlimited).
- **Items and characters** are a catalog in `FileRef` (`_items`: id = art file name, display name, sprite; `_characters`: id, name, poses), filled by **ShiftSort > Build Assets** from `Sprites/Tilewrap/Items` and `Characters`. Fix a display name in `FileRef`, or in `AssetBuilder.ItemNames` before a refill.
- **Board look** (world space, matching the pack's 1080×1920 layout): `ItemTile` prefabs (surface, item art, name), wooden frame over the tiles, category tags on the left, `Active Row` outline, checks on confirmed rows. The camera shows the whole 10.8×19.2 reference screen fitted to the safe area (`ReferenceLayout`), so a HUD on the same layout lines up. Tile names are world TextMeshPro with a stencil material (`TilewrapNunito-ExtraBold SDF Masked.mat`) so the board's SpriteMask clips them while a line wraps.
- **HUD** (`UI_InGame`, built by **ShiftSort > Build UI** under `Canvas/Container/InGame/Reference`, the pack's layout): level/step, goal, moves panel, sound switch, two clue notes with item icons, characters on name plates above the board (pose = confirmed steps), "Swipe rows or columns. Tiles wrap.", Undo, Notebook (placeholder) and the gold button (Hint placeholder → Next after a win → Retry when out of moves). `UI_ReferenceFit` fits the 1080×1920 `Reference` rect into the safe area exactly as the camera fits the world, so HUD rects use the CSV's pixels and line up with the board. The booth background is a world sprite (`GamePlay/Background`) behind the board. When the app opens, the loading screen (`Canvas/Loading Screen`, built by **ShiftSort > Build Loading Screen**: `Tilewrap_Loading` art, "LOADING" and a timed bar, `GameTuning` > Loading screen) holds `Manager_Game.Launch()` until the bar is full; the studio splash before it is separate. As it fades, the board scales up (`Manager_Board.PlayIntro`, input blocked until done) and the HUD elements scale up one after another (`UI_InGame.PlayIntro`, DOTween, once per launch; order = `_introItems`, timing = `GameTuning` > UI intro). Every level's tiles pop in with a diagonal scale-up wave (`Manager_Board.AnimateTileSpawn`); a confirmed row's checks pop in when the move settles (`Cell.SetMatched`). Nothing advances or restarts on its own. Undo takes back one swipe and also works after a win or fail.
- **Audio:** `Manager_Audio.Instance?.PlaySound(SoundType.X)`; clips are mapped in `FileRef` by **ShiftSort > Build Assets** from `Audio/SFX/{snake_case SoundType}[_NN].wav` (variants) and the first `Audio/Music/*.wav`. Adding a sound: append a `SoundType` value, drop the file, clear `FileRef`'s Sounds table, run Build Assets. One HUD switch mutes music and effects (saved in `OfflineData`). Volumes live on the scene's `Sound/Music` (background music level) and `Sound/SFX` (master effects level) AudioSources; `Manager_Audio` on `Managers` only references them and never writes their volume.
- **Tutorial:** none. The Shift & Sort tutorial (`Manager_Tutorial`, levels 1–4) was retired with its levels.
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
- **Adding an item or character:** drop the art in `Sprites/Tilewrap/Items/{id}.png` or `Characters/character_{id}_{neutral|gear|bag|complete}.png` (add it to `art_manifest.json` for its import settings), clear `FileRef`'s Items / Characters table, run **ShiftSort > Build Assets**, then check the display names. Level files refer to items and characters by id.
- The builders (`AssetBuilder`, `SceneBuilder`) never overwrite an existing asset, prefab or filled reference. Change an existing prefab with `PrefabUtility.LoadPrefabContents`, or delete it and rebuild.
- Before testing in Play mode from the CLI, run `unity command editor.playmode.set_run_in_background --enabled true --project-path C:/Project/Clue_Parade`. Otherwise the unfocused Editor freezes at frame 1–2 and every press is rejected.

## Architecture (target, per skeleton.md)

- **One scene.** A `Managers` GameObject holds every `Manager_*` singleton plus `SingletonManager` and `GameTuning`. The other roots are `GamePlay` (world objects placed by hand) and `Canvas` (HUD and popups).
- **Managers decide, views display.** Managers own rules and state, and expose methods for commands and events for changes. Views (prefab MonoBehaviours, UI) never mutate state.
- **Singletons** derive from `Singleton<T>` and put their init in `Awake2()`, never `Awake()`. `SingletonManager.Awake` loads the save (`OfflineData.Load()`, once) and then initializes singletons in the order of its hand-wired Inspector list. A new manager has to be added to that list to get ordered init.
- **`Manager_Game`** owns the `GameState` machine and the per-frame tick order. Other managers expose `Tick(dt)` instead of running gameplay in their own `Update`. This game is turn-based and has no per-frame simulation yet. Pause is derived from popups, tutorials and intros, never stored as a flag.
- **Listeners subscribe, then read the current value right away**, because Unity doesn't order `Start` across scripts. Events default to `delegate { }`.
- **Gameplay numbers** live in `Tuning` (const defaults plus static facade) and `GameTuning` (scene overrides). The scene values are what ship. **Assets** are assigned only in `FileRef` (a `ScriptableSingleton` at `Resources/FileRef.asset`).
- **Save** is `OfflineData` (`Essentials/OfflineData.cs`): public fields only, JSON in one PlayerPrefs key, append-only, one writer per field. Today: `CurrentLevel` + `PassedLevels` (written on a win by `Manager_Game.SaveProgress`) and `IsSoundOn` (`Manager_Audio.SetSoundOn`). The game opens on the saved level; the Tester's **Clear Saved Progress** resets it. `LiveData` (unsaved session state) is not needed yet.
- **Serialized, saved or bit-indexed enums are append-only and explicitly numbered** (e.g. `PopupName`, `GameState`).
- Code lives in `Assembly-CSharp` in the global namespace. Naming follows `Manager_X`, `PopUp_X`, `UI_X`, with `[SerializeField] private _camelCase` fields exposed through expression-bodied properties.
- Scene object names are load-bearing, because builders find children by path. Scene structure that gets regenerated goes through re-runnable Editor `[MenuItem]` builders, not hand-edited scene YAML.
- `skeleton.md` flags two bugs in the original framework. The duplicate-singleton guard (section 3) is already fixed here. `OfflineData.Load()` is called only once, in `SingletonManager.Awake` (section 4); keep it that way.
