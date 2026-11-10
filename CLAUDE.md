# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

**Shift & Sort** is a Unity mobile game in its earliest stage: a fresh Unity **6000.3.17f1** project from the URP 2D template (2D Renderer, `Assets/Game Assets/Scenes/Game.unity`, Input System package). The core gameplay loop works (board, swipes, JSON levels, win, fail, undo) with the in-game HUD. Music and sound effects play through `Manager_Audio`, and `OfflineData` saves level progress and the sound switch. A loading screen (`UI_Loading`) opens the app. There are no popups yet.

`skeleton.md` is the architecture blueprint this game is meant to follow (taken from a previous game, Color Farm). **Read it before adding any game code.** Its section 13 is the step-by-step bootstrap checklist. Where this file and `skeleton.md` disagree about the current repo state, this file is correct. `skeleton.md` describes the target, not what exists today.

Gaps between the blueprint and the repo right now:
- All animation goes through DOTween (no coroutines): the line slide is a tween in `Manager_Board.AnimateHeldLine`. TextMesh Pro comes with `com.unity.ugui` 2.0; TMP Essentials are imported (`Assets/TextMesh Pro/`) and the HUD uses dynamic Open Sans font assets in `Fonts/`.
- `activeInputHandler: 1` (new Input System only) is already set, so legacy `Input.*` calls will throw.
- The `skeleton.md` folder layout exists under `Assets/Game Assets/`, `Assets/Resources/Levels/` and `Assets/Plugins/`. Empty folders hold a `.gitkeep` (Unity ignores dot-files) so they survive a clone; delete it once the folder has content. The only scene is `Assets/Game Assets/Scenes/Game.unity` (the template's SampleScene, renamed). 
- URP assets stay in `Assets/Settings/`, and the project-wide `Assets/InputSystem_Actions.inputactions` stays at the root, because project settings reference them.
- `.gitignore` excludes `Library/`, `Temp/`, `Logs/`, `UserSettings/` and the generated `.csproj`/`.sln` files. Commit `Assets/`, `Packages/` and `ProjectSettings/`, including every `.meta` file.

## Game design

Puzzle game played on an N×N grid of tiles. Each tile has a **shape** and a **color**. The art in `Sprites/Sprites/` covers 4 shapes (circle, square, triangle, star) × 4 colors (coral, teal, yellow, blue), so the largest board is 4×4. Tile sprites are pre-rendered per shape × color, with shading and the color's symbol (dot, `||`, diamond, +) baked in, so the code never tints tiles. Reference mockups are in `Sprites/Previews/`.

- **Targets:** each row has a shape target, shown beside the board. Each column has a color target, shown above the board. A cell is *matched* when its shape equals its row's target and its color equals its column's target. HUD text: "Match shapes across and colors down".
- **Move:** swipe a whole row horizontally or a whole column vertically, which shifts every tile in that line.
- **Win:** every cell is matched ("N×N / N×N matched", "Board complete"). A row or column target turns green with a check once the whole line is satisfied. Each matched cell also shows a check.
- **HUD:** level number, a matched counter (`2 / 9 matched`), moves left (a per-level limit, or "Unlimited moves"), and a sound toggle.
- Implied fail condition: running out of moves before the board is complete.

Rules as implemented:
- **Hold and drag:** a press on the board picks up its row or column once the pointer has moved `DragStartThreshold` (a fraction of the shorter screen side). The axis is locked to the dominant direction. The line then follows the pointer, wrapping around the edge, but **never more than one cell** (`Manager_Game.MaxStepsPerMove`).
- **Release:** **a swipe moves exactly one cell and counts as one move.** It commits if the line was dragged at least `DragCommitThreshold` of a cell (default 0.5), otherwise it snaps back. A release within `FlickMaxDuration` commits anyway, so quick swipes still work. `ApplyMove` from code uses only the sign of `steps`.
- Levels are authored JSON files, `Assets/Resources/Levels/level-NNN.json` (levels 1–12 exist), loaded by `JsonLevelSource`. A tile's id is the cell it belongs in when solved; targets are fixed (format in `ARCHITECTURE.md` §7.1). After adding or editing one, run **ShiftSort > Validate Levels**, which replays each file's `solution`.
- Past the last file (or for a bad file), `ProceduralLevelSource` generates the level: it scrambles a solved board, seeded by level number. Board size grows from `MinBoardSize` by 1 every `LevelsPerSizeStep` levels, capped by the art (currently 4×4). Generated levels have no move limit.
- `moveBudget` from the file is the move limit (0 = unlimited).
- **HUD** (`UI_InGame`, built by **ShiftSort > Build UI**, matching the mockups in `Sprites/Previews/`): logo, level, sound toggle, matched / moves pills, a message panel (neutral, win, fail), Next level (after a win), Undo and Restart. When the app opens, the loading screen (`Canvas/Loading Screen`, built by **ShiftSort > Build Loading Screen**: `Tilewrap_Loading` art, "LOADING" and a timed bar, `GameTuning` > Loading screen) holds `Manager_Game.Launch()` until the bar is full; the studio splash before it is separate. As it fades, the board scales up (`Manager_Board.PlayIntro`, input blocked until done) and the HUD elements scale up one after another (`UI_InGame.PlayIntro`, DOTween, once per launch; order = `_introItems`, timing = `GameTuning` > UI intro). Every level's tiles pop in with a diagonal scale-up wave (`Manager_Board.AnimateTileSpawn`), and each matched check pops in after its tile has landed; in play, new checks pop in when the move settles (`Cell.SetMatched`). Nothing advances or restarts on its own. Undo takes back one swipe and also works after a win or fail. The camera fits the board between the `HudTopReserve` / `HudBottomReserve` bands; the pills hang off `Board Area`, so they follow the board; the bottom stack (message panel, Next level, Undo/Restart) is pinned to the bottom of the screen at the board's width (`UI_BoardArea` with Horizontal Only).
- **Audio:** `Manager_Audio.Instance?.PlaySound(SoundType.X)`; clips are mapped in `FileRef` by **ShiftSort > Build Assets** from `Audio/SFX/{snake_case SoundType}[_NN].wav` (variants) and the first `Audio/Music/*.wav`. Adding a sound: append a `SoundType` value, drop the file, clear `FileRef`'s Sounds table, run Build Assets. One HUD switch mutes music and effects (saved in `OfflineData`). Volumes live on the scene's `Sound/Music` (background music level) and `Sound/SFX` (master effects level) AudioSources; `Manager_Audio` on `Managers` only references them and never writes their volume.
- **Haptics:** `Haptics.Play(HapticType.Light/Medium/Heavy)` (Essentials) over the CandyCoded HapticFeedback package (git, v1.0.3). Always on, no setting by design. One pulse per event, called next to the matching sound. It does nothing in the Editor; use the Tester's **Log Sounds & Haptics** to check it.
- **Tester:** the `Tester` GameObject (tag `EditorOnly`, stripped from builds) holds `TestManager`, an Inspector cheat panel: load/next/restart level, force win/fail, play the level's solution, shift a line, log state, time scale. Its body is `#if UNITY_EDITOR`; buttons use `[Sortify.Button]` (`Scripts/Sortify/`) on parameterless methods. Add new cheats there, never as game code paths.

## Commands

There is no CLI build or lint setup. Work happens in the Unity Editor. Headless runs go through the Editor executable (the path depends on the Unity Hub install, typically `C:\Program Files\Unity\Hub\Editor\6000.3.17f1\Editor\Unity.exe`):

```
# Run EditMode tests (com.unity.test-framework is installed); use -testPlatform PlayMode for play-mode tests
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml
# Run a single test or fixture
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter "Namespace.Fixture.Method"
```

Batchmode fails if the project is already open in the Editor.

Android builds from the Build window are named `[GameName]_[version]_[date]_[additionalInformation].apk` (or `.aab`) by `Scripts/Editor/BuildNaming.cs`: GameName = Player Settings Product Name, version = Player Settings Version, date = build day, additional information from **ShiftSort > Build Name Settings** (shows a preview). Scripted builds must call `BuildNaming.Apply(ref options)`.

The Unity CLI (`unity`, documented in the `unity-cli` skill) can drive the open Editor live through the `com.unity.pipeline` package. Use it instead of hand-editing scene YAML. Always pass `--project-path C:/Project/Shift-Sort`.

```
unity status --project-path C:/Project/Shift-Sort                       # Editor connected and "ready"?
unity recompile --project-path C:/Project/Shift-Sort --format json      # compile and report errors
unity command script.eval --code 'SceneBuilder.Build(); return "ok";' --project-path C:/Project/Shift-Sort
unity command scene.get_hierarchy --project-path C:/Project/Shift-Sort --format json
unity command console.get --tail 20 --level warning --project-path C:/Project/Shift-Sort --format json
```

## Architecture as built

**Read `ARCHITECTURE.md` before changing game code.** It holds:
- the boot → level → drag → win call trace (§3);
- every module, with what it owns and who calls it (§4);
- the direction and coordinate conventions and the rules duplicated across files (§6). Check §6.4 before touching layout, directions or matching.
- the asset → loader table (§7), scene wiring (§8), CLI testing gotchas (§10) and known gaps (§11).

Keep it current: when you change a system, edit its section there, not here. Things to always do:
- Rule changes go in `BoardState` (the pure model). Views get their sprites from `Manager_Board` and never read `FileRef` or managers.
- **Adding a shape or color:** add the art (`tile_{shape}_{color}`, `target_row_{shape}`, `target_column_{color}`), then the enum value at the end (append-only), then run **ShiftSort > Build Assets**. Clear the `FileRef` table first if it's already filled.
- The builders (`AssetBuilder`, `SceneBuilder`) never overwrite an existing asset, prefab or filled reference. Change an existing prefab with `PrefabUtility.LoadPrefabContents`, or delete it and rebuild.
- Before testing in Play mode from the CLI, run `unity command editor.playmode.set_run_in_background --enabled true --project-path C:/Project/Shift-Sort`. Otherwise the unfocused Editor freezes at frame 1–2 and every press is rejected.

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
