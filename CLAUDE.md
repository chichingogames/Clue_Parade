# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

**Clue Parade** (Player Settings Product Name: **Tilewrap**, which also names builds; the code and menus still say ShiftSort, from the game it is being redesigned from) is a Unity **6000.3.17f1** mobile game (URP 2D template, `Assets/Game Assets/Scenes/Game.unity`, Input System). On branch `any-row` it implements **"Follow the Evidence"** (`Tilewrap_Deduction_30_Level_Playtest.html` + `Tilewrap_Game_Scope_v5.pdf`): 30 deduction cases where swapping neighbours lines up requested sets under fixed profiles in any row of a 3- or 4-row board, with clue rules and shared clue pools, refills, a case question per case, milestone deductions, Hint, Notebook, a chapter map, win / out-of-moves (+5 moves) popups, coins and save / resume. The cases cycle: after case 30 comes case 1. Item and rig-part art comes from the 30-case content pack (`Assets/TilewrapVaried/`) plus `Assets/Game Assets/Sprites/Evidence`.

`skeleton.md` is the architecture blueprint this game is meant to follow (taken from a previous game, Color Farm). **Read it before adding any game code.** Its section 13 is the step-by-step bootstrap checklist. Where this file and `skeleton.md` disagree about the current repo state, this file is correct. `skeleton.md` describes the target, not what exists today.

Gaps between the blueprint and the repo right now:
- All animation goes through DOTween (no coroutines): the swap, delivery, fall and refill are tweens in `Manager_Board` (`PlayMove`). TextMesh Pro comes with `com.unity.ugui` 2.0; TMP Essentials are imported (`Assets/TextMesh Pro/`) and the UI uses dynamic TMP font assets of the pack's TilewrapUI fonts (`Fonts/TilewrapUI-* SDF`, plus a masked material, made by `HandoffBuilder`).
- `activeInputHandler: 1` (new Input System only) is already set, so legacy `Input.*` calls will throw.
- The `skeleton.md` folder layout exists under `Assets/Game Assets/`, `Assets/Resources/` and `Assets/Plugins/`. Empty folders hold a `.gitkeep` (Unity ignores dot-files) so they survive a clone; delete it once the folder has content. The only scene is `Assets/Game Assets/Scenes/Game.unity` (the template's SampleScene, renamed). 
- URP assets stay in `Assets/Settings/`, and the project-wide `Assets/InputSystem_Actions.inputactions` stays at the root, because project settings reference them.
- `.gitignore` excludes `Library/`, `Temp/`, `Logs/`, `UserSettings/` and the generated `.csproj`/`.sln` files. Commit `Assets/`, `Packages/` and `ProjectSettings/`, including every `.meta` file.

## Game design

**Branch `any-row`:** the game follows **`Tilewrap_Deduction_30_Level_Playtest.html`** and
**`Tilewrap_Game_Scope_v5.pdf`** (deliveries outside the repo, in Downloads): 30 cases, "Follow the Evidence" (the
missing festival star). It replaced the five-case "Any row, two clues" build and the 30-case wrap / swap builds
(branches `main`, `swap-mechanic`, in git history).

- **Cases:** `Assets/Game Assets/Data/delivery_cases.json` (the playtest's `DELIVERY_CONTENT` without its embedded art,
  converted to a JsonUtility-friendly shape: no nested arrays, `""` for empty cells and missing ids, `-1` for a missing
  column), parsed by `DeliveryContent` (economy, chapters, cases) and reached through `FileRef.Cases`. Each case
  carries its subjects' **rigs** (layers drawn by `UI_SubjectCard`). Sprites come from the 30-case content pack
  (`Assets/TilewrapVaried/`, `FileRef.Sprite`) plus `Sprites/Evidence` (12 props the pack lacks, extracted from the
  HTML). **ShiftSort > Validate Levels** checks every case.
- **Board:** 3 columns × 3 or 4 rows (`rows`), item tiles (icon only, no name); a cell may be **empty**. The board keeps
  its 870 px square: 4-row cells are shorter. **Columns are fixed profiles** (Mia/Leo/Zoe, Vehicle A/B/C…). A case
  has 3–10 **sets**; a set names one item per column. No row numbers or row labels are shown.
- **Move:** swap a tile with one of its **4 neighbours** (drag towards it, or tap it then the neighbour); swapping with
  an empty cell is allowed, two empty cells are not, edges do not wrap. One swap = one move.
- **Delivery (any row):** when **any row** holds a pending set's three items under their owners, the set is
  delivered, even when it is not the request on screen (`DeliveryRules.AnyPendingSetClears`, the user's rule; the
  HTML clears only open requests, and its case 17 parks its third set complete in a row, which this rule delivers on
  the first swap): the items fly to the subjects (their fitted parts appear; found objects go in a badge), the items
  above fall, and the next **refill** triple (if any) drops in. Deliveries chain. All sets delivered = win (a win on
  the last move beats running out).
- **Order:** `orderMode` "staged" = sets in order; "choice" = up to two open sets (all items on the board), picked
  with the **order tabs**; "hybrid" = the first three staged, then choice.
- **Clues:** two cards at a time. The first three sets of a case with a `cluePool` share it ("More clues · 1/3" pages
  it); later sets show their own two. A clue states a rule (at, notAt, same, different, left, next, notNext, lowest,
  highest, less) over items (`refs`); tapping it highlights only those items (and the profile, for "at"); it is
  **CONFIRMED** once they are delivered. Nothing tints an item for being under its right owner. Cases with a
  `dossier` show a badge per profile (age, price, time). The Notebook has every clue (Clues tab) and the case
  question, evidence, witness statements and earlier cases' evidence (Case file tab).
- **Opening:** case 1 guides its first swap only (`tutorial` from/to: other swaps snap back, other tiles muted); cases
  2–5 have a teaching coach line each (case 4 spotlights the move counter); 6–14 show their `reasoningTip`. The first
  time a case opens its tiles settle in like slot reels (cosmetic, `OfflineData.SeenCases`).
- **Flow** (`Manager_Flow`): coach line, Hint (an explanation of the clues first; then the next swap,
  `DeliveryRules.Hint`, exact planners for 3 and 4 rows: free through case 8, then 30 coins), the case map
  (`PopUp_Cases`: chapters, EXPERT markers, all open), menu, the milestone **deduction** (cases 15, 20, 25, 30:
  `PopUp_Deduction` before the win, wrong answers cost nothing), win (the case question answered, the finished scene
  or the festival star after case 30, evidence, +30 / +90 Expert first clear, the next case and lead), out of moves
  (+5 moves once per attempt: free through case 8, then 80 coins; Retry / Undo free) and the playtest log. No ads
  (the HTML only simulates them). After case 30, Next case (and the saved case) is case 1 again.

Rules as implemented:
- **Model:** `DeliveryRules` (pure C#, a port of the playtest's `DeliveryCore`): `Eligible`, `CurrentSet`, `Ready`,
  `Transact` (swap → deliveries → fall → refill, returning the steps as `DeliveryEvent`s), `Continue`, `Hint`,
  `Solve`, `CheckInvariant`. Rule changes go there. `DeliveryState` is the whole state (board, delivered, completed
  sets, refill index, moves, extra moves, continues, status); Undo restores the previous one (a used continue stays
  used).
- **Drag:** a press on a cell picks up the pair once the pointer has moved `DragStartThreshold`; the dominant
  direction picks the neighbour (locked for the press). The pair follows the pointer; release commits at
  `DragCommitThreshold` of a cell or on a flick (`FlickMaxDuration`), else it snaps back. A press without moving is
  a tap (select / swap with the selected neighbour).
- **Board look** (`Manager_Board`, world space): the board is centred (x 105, cells 290 px wide), focus / hint /
  selected tints, the hinted row's zone, ring + stars on a delivery, falling and dropping tiles.
- **HUD** (`UI_InGame`, **ShiftSort > Build UI**): case pill, menu, coins, moves / Practice (case 4 spotlight); title,
  the case question, "x of N sets delivered" with up to 10 pips; "BUILD · HATS" + "SET 1 / 3" / "More clues" or order
  tabs; two clue cards (CONFIRMED tag); subject cards (`UI_SubjectCard`: the case's rig layers, profile fact,
  found-objects badge); board note; coach; Undo / Notebook / Hint; the "Hats delivered!" banner and flying items. On a phone taller than 9:16
  (iPhones) the reference is centred in the safe area with spare room above and below; `UI_SafeDock` moves the
  buttons down to the safe area's bottom edge and the coach / board note part of the way (runtime only).
- **Progress** saved in `OfflineData`: `CompletedLevels`, `Coins` (Manager_Flow: rewards, hint and continue costs),
  `CurrentLevel` (the case open; the next one after a win, wrapping to 1), `Resume` (the unfinished attempt with its
  undo history, resumed on launch; dropped if the content version changes), `SeenCases`. The `Feedback*` and
  `Lessons` fields are retired (append-only).
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
- **Unity skills:** drive the Editor through the `unity-cli` / `unity-pipeline` skills. Turn on `editor set_auto_tick` for headless work; put multi-line C# in `AgentScripts/*.cs` (outside `Assets/`) and run it with `unity command script run --file ... --entry ...`. `AgentScripts/PlayCheck.cs` reports the case state and makes real drags, taps and undos in Play mode (`Load(n)`, `Hint(k)` follows k hint swaps, `Waste(n, a, b)` spends moves); `FlowCheck.cs` taps HUD and popup buttons.
- **Changing content:** cases (with their rigs) are data in `Assets/Game Assets/Data/delivery_cases.json` (regenerate it from a new playtest delivery: strip the embedded art, convert to the JsonUtility shape, extract art the pack lacks into `Sprites/Evidence` and clear `FileRef`'s evidence list before Build Assets), sprites in `Assets/TilewrapVaried` (replace it with a new delivery of the pack, keeping its `.meta` files, and run **Tools > Chichingo > Tilewrap > Build 30-level content**). Then **ShiftSort > Validate Levels**. No game code names a subject or item, except the playtest's teaching lines for cases 2–5 in `Manager_Flow.DefaultCoach` / `Explain` (ported as written; they mention the teal scarf and the sugar star).
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
