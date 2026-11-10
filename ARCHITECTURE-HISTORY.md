# Architecture history

Append-only. Newest first. One entry per documentation run.

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
