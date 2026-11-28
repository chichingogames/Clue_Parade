# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

**Clue Parade** (Player Settings Product Name: **Tilewrap**, which also names builds; the code and menus still say ShiftSort, from the game it is being redesigned from) is a Unity **6000.3.17f1** mobile game (URP 2D template, `Assets/Game Assets/Scenes/Game.unity`, Input System). On branch `any-row` it implements **"Any row, two clues"** (`Tilewrap_Any_Row_5_Case_Playtest.html`): five cases where swapping neighbours lines up requested sets under their owners in any row, with deliveries, refills, two clues per set, a case list, Hint, Notebook, win / out-of-moves popups, first-clear coins, feedback and a local playtest report. Item art and subject rigs come from the 30-case content pack (`Assets/TilewrapVaried/`).

`skeleton.md` is the architecture blueprint this game is meant to follow (taken from a previous game, Color Farm). **Read it before adding any game code.** Its section 13 is the step-by-step bootstrap checklist. Where this file and `skeleton.md` disagree about the current repo state, this file is correct. `skeleton.md` describes the target, not what exists today.

Gaps between the blueprint and the repo right now:
- All animation goes through DOTween (no coroutines): the swap, delivery, fall and refill are tweens in `Manager_Board` (`PlayMove`). TextMesh Pro comes with `com.unity.ugui` 2.0; TMP Essentials are imported (`Assets/TextMesh Pro/`) and the UI uses dynamic TMP font assets of the pack's TilewrapUI fonts (`Fonts/TilewrapUI-* SDF`, plus a masked material for tile names, made by `HandoffBuilder`).
- `activeInputHandler: 1` (new Input System only) is already set, so legacy `Input.*` calls will throw.
- The `skeleton.md` folder layout exists under `Assets/Game Assets/`, `Assets/Resources/` and `Assets/Plugins/`. Empty folders hold a `.gitkeep` (Unity ignores dot-files) so they survive a clone; delete it once the folder has content. The only scene is `Assets/Game Assets/Scenes/Game.unity` (the template's SampleScene, renamed). 
- URP assets stay in `Assets/Settings/`, and the project-wide `Assets/InputSystem_Actions.inputactions` stays at the root, because project settings reference them.
- `.gitignore` excludes `Library/`, `Temp/`, `Logs/`, `UserSettings/` and the generated `.csproj`/`.sln` files. Commit `Assets/`, `Packages/` and `ProjectSettings/`, including every `.meta` file.

## Game design

**Branch `any-row`:** the game follows **`Tilewrap_Any_Row_5_Case_Playtest.html`** (a delivery outside the repo, in
Downloads): five cases, "Any row, two clues". It replaced the 30-case wrap / swap builds (branches `main`,
`swap-mechanic`, in git history).

- **Cases:** `Assets/Game Assets/Data/delivery_cases.json` (the playtest's `DELIVERY_CONTENT` without its embedded art,
  converted to a JsonUtility-friendly shape), parsed by `DeliveryContent` and reached through `FileRef.Cases`. Item
  sprites and subject rigs are the 30-case content pack's (`Assets/TilewrapVaried/`, `FileRef.Sprite` / `FileRef.Rig`);
  the pack's own 30 case files are no longer played. **ShiftSort > Validate Levels** checks every case.
- **Board:** 3×3, item tiles (icon + name); a cell may be **empty**. **Columns are subjects** (Mia/Leo/Zoe, three pets,
  three cakes, three stalls, three breakfasts). A case has 3–5 **sets** (Hats, Shirts, Bags…); a set names one item
  per column.
- **Move:** swap a tile with one of its **4 neighbours** (drag towards it, or tap it then the neighbour); swapping with
  an empty cell is allowed, two empty cells are not, edges do not wrap. One swap = one move.
- **Delivery (any row):** when **any row** holds an eligible set's three items under their owners, the set is
  delivered: the items fly to the subjects (their rig parts appear), the items above fall down their columns, and the
  next **refill** triple (if the case has one) drops into the top of each column. Deliveries chain. All sets
  delivered = win.
- **Order:** `orderMode` "staged" = sets in order; "choice" = up to two open sets (all items on the board), picked
  with the **order tabs**.
- **Clues:** two of the set's three facts on cards (tap = highlight the item and its owner); a **scene clue**
  (`referenceItem`) points at a prop already delivered. The Notebook lists every set's three facts.
- **Case 1:** a guided first swap (`tutorial` from/to: other swaps snap back, other tiles muted) and the hint's next
  swap shown after every move. Cases 4–5 have move limits (out of moves = fail, Undo allowed).
- **Flow** (`Manager_Flow`): coach line under the board, Hint (`DeliveryRules.Hint`: an exact 729-state planner for
  the three requested items into any row), clue focus, Notebook, the case list (`PopUp_Cases`, all open), menu, win
  (finished subjects with extras, +30 first clear, sets and swaps), out of moves (Retry / Undo / Choose), the closing
  feedback after case 5 and the playtest log.

Rules as implemented:
- **Model:** `DeliveryRules` (pure C#, a port of the playtest's `DeliveryCore`): `Eligible`, `CurrentSet`, `Ready`,
  `Transact` (swap → deliveries → fall → refill, returning the steps as `DeliveryEvent`s), `Hint`, `Solve`,
  `CheckInvariant`. Rule changes go there. `DeliveryState` is the whole state (board, delivered, completed sets,
  refill index, moves, status); Undo restores the previous one.
- **Drag:** a press on a cell picks up the pair once the pointer has moved `DragStartThreshold`; the dominant
  direction picks the neighbour (locked for the press). The pair follows the pointer; release commits at
  `DragCommitThreshold` of a cell or on a flick (`FlickMaxDuration`), else it snaps back. A press without moving is
  a tap (select / swap with the selected neighbour).
- **Board look** (`Manager_Board`, world space): the board is centred (x 105, cells 290 px), tiles tinted while under
  their owner for the request in focus (`TileLook.Aligned`), focus / hint / selected tints, three row zones (2 of 3
  aligned: gold; 3: green; hinted row: strong gold), ring + stars on a delivery, falling and dropping tiles.
- **HUD** (`UI_InGame`, **ShiftSort > Build UI**): case pill, menu, coins, moves / Practice; title, "x of N sets
  delivered" with pips; "CLUES · HATS" + "SET 1 / 3" or order tabs; two clue cards (SCENE CLUE tag); subject cards
  (`UI_SubjectCard`: rig parts by delivered item, plus the case's extra layers for refill items); board note; coach;
  Undo / Notebook / Hint; the "Hats delivered!" banner and the flying items.
- **Progress** saved in `OfflineData` (`CompletedLevels`, `Coins`, `CurrentLevel`); feedback answers reuse the
  `Feedback*` fields (any-row rule, clues, favourite scene).
- **Audio:** `Manager_Audio.Instance?.PlaySound(SoundType.X)`; clips are mapped in `FileRef` by **ShiftSort > Build Assets**: TileMatch = the pack's match_1..3 (a delivery) and LevelComplete = level_win (the winning delivery), the rest from `Audio/SFX/{snake_case SoundType}[_NN].wav` (variants), music = the first `Audio/Music/*.wav`. Adding a sound: append a `SoundType` value, drop the file, clear `FileRef`'s Sounds table, run Build Assets. One HUD switch mutes music and effects (saved in `OfflineData`). Volumes live on the scene's `Sound/Music` and `Sound/SFX` AudioSources; `Manager_Audio` only references them.
- **Haptics:** `Haptics.Play(HapticType.Light/Medium/Heavy)` (Essentials) over the CandyCoded HapticFeedback package (git, v1.0.3). Always on, no setting by design. One pulse per event, called next to the matching sound. It does nothing in the Editor; use the Tester's **Log Sounds & Haptics** to check it.
- **Tester:** the `Tester` GameObject (tag `EditorOnly`, stripped from builds) holds `TestManager`, an Inspector cheat panel: load/next/restart case, force win/fail, play the hint route to the win, swap a tile, log state, time scale. Its body is `#if UNITY_EDITOR`; buttons use `[Sortify.Button]` (`Scripts/Sortify/`) on parameterless methods. Add new cheats there, never as game code paths.

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
- Rule changes go in `DeliveryRules` (the pure model). Views get their sprites and names from `Manager_Board` and never read `FileRef` or managers.
- **Unity skills:** drive the Editor through the `unity-cli` / `unity-pipeline` skills. Turn on `editor set_auto_tick` for headless work; put multi-line C# in `AgentScripts/*.cs` (outside `Assets/`) and run it with `unity command script run --file ... --entry ...`. `AgentScripts/PlayCheck.cs` reports the case state and makes real drags, taps and undos in Play mode.
- **Changing content:** cases are data in `Assets/Game Assets/Data/delivery_cases.json` (edit or regenerate it from a new playtest delivery), art and rigs in `Assets/TilewrapVaried` (replace it with a new delivery of the pack, keeping its `.meta` files, and run **Tools > Chichingo > Tilewrap > Build 30-level content**). Then **ShiftSort > Validate Levels**. No game code names a subject or item.
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
