# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

**Shift & Sort** is a Unity mobile game in its earliest stage: a fresh Unity **6000.3.17f1** project from the URP 2D template (2D Renderer, `Assets/Game Assets/Scenes/Game.unity`, Input System package). The core gameplay loop works (board, swipes, win, auto-advance). There is no UI, save or JSON levels yet.

`skeleton.md` is the architecture blueprint this game is meant to follow (taken from a previous game, Color Farm). **Read it before adding any game code.** Its section 13 is the step-by-step bootstrap checklist. Where this file and `skeleton.md` disagree about the current repo state, this file is correct. `skeleton.md` describes the target, not what exists today.

Gaps between the blueprint and the repo right now:
- DOTween is installed but unused. Line slides are a coroutine in `Manager_Board.AnimateHeldLine`. TextMesh Pro comes with `com.unity.ugui` 2.0.
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
- **Hold and drag:** a press on the board picks up its row or column once the pointer has moved `DragStartThreshold` (a fraction of the shorter screen side). The axis is locked to the dominant direction. The line then follows the pointer continuously, wrapping around the edge.
- **Release:** every whole cell dragged is one step, plus one more if the leftover reaches `DragCommitThreshold` of a cell (default 0.5). Zero steps snaps back. A release within `FlickMaxDuration` moves at least one cell, so quick swipes still work. Each step counts as one move.
- Levels come from `ProceduralLevelSource`: it scrambles a solved board with random swipes, so every level is solvable. It's seeded by level number. Board size grows from `MinBoardSize` by 1 every `LevelsPerSizeStep` levels, and is capped by the shapes and colors that have art (currently 4×4).
- Move limit is 0 (unlimited) for now.
- With no UI, a win advances and a fail restarts automatically after a `Tuning` delay.

## Commands

There is no CLI build or lint setup. Work happens in the Unity Editor. Headless runs go through the Editor executable (the path depends on the Unity Hub install, typically `C:\Program Files\Unity\Hub\Editor\6000.3.17f1\Editor\Unity.exe`):

```
# Run EditMode tests (com.unity.test-framework is installed); use -testPlatform PlayMode for play-mode tests
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml
# Run a single test or fixture
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter "Namespace.Fixture.Method"
```

Batchmode fails if the project is already open in the Editor.

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
- **Singletons** derive from `Singleton<T>` and put their init in `Awake2()`, never `Awake()`. `SingletonManager.Awake` (will also load the save once `OfflineData` exists) initializes singletons in the order of its hand-wired Inspector list. A new manager has to be added to that list to get ordered init.
- **`Manager_Game`** owns the `GameState` machine and the per-frame tick order. Other managers expose `Tick(dt)` instead of running gameplay in their own `Update`. This game is turn-based and has no per-frame simulation yet. Pause is derived from popups, tutorials and intros, never stored as a flag.
- **Listeners subscribe, then read the current value right away**, because Unity doesn't order `Start` across scripts. Events default to `delegate { }`.
- **Gameplay numbers** live in `Tuning` (const defaults plus static facade) and `GameTuning` (scene overrides). The scene values are what ship. **Assets** are assigned only in `FileRef` (a `ScriptableSingleton` at `Resources/FileRef.asset`).
- **Save** is `OfflineData`: public fields only, JSON in one PlayerPrefs key, append-only, one writer per field. `LiveData` holds unsaved session state.
- **Serialized, saved or bit-indexed enums are append-only and explicitly numbered** (e.g. `PopupName`, `GameState`).
- Code lives in `Assembly-CSharp` in the global namespace. Naming follows `Manager_X`, `PopUp_X`, `UI_X`, with `[SerializeField] private _camelCase` fields exposed through expression-bodied properties.
- Scene object names are load-bearing, because builders find children by path. Scene structure that gets regenerated goes through re-runnable Editor `[MenuItem]` builders, not hand-edited scene YAML.
- `skeleton.md` flags two bugs in the original framework. The duplicate-singleton guard (section 3) is already fixed here. Call `OfflineData.Load()` only once when you add it (section 4).
