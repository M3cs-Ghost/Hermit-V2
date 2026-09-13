# Hermit V2 — Architecture (repo-local reference)

This is a short on-repo reference, not a replacement for the Phase C / C2 / C2.5
documents (audit, Blueprint, ADR ratification). Read those for the *why*; this
file only tracks what actually exists in this repo *today*.

## Client / backend split

- **Unity 6.3 LTS (URP 2D)** is the client. It owns UI, scenes, gameplay, input,
  audio, local cache, and the Game Framework (`Hermit.Games`, added in C5).
- **Supabase** (Postgres + Auth + RPC + RLS) is the backend, unchanged from V1.
  Schema, migrations, RPCs and the 1,300+ line test suite live in the V1 repo
  and are never duplicated here — this repo only ever gets a *client* to them.

## Boundaries (non-negotiable, ADR-002 / ADR-006 / ADR-018)

- This client never talks to Postgres directly — only through RPCs the backend
  already exposes (or new ones added additively, per ADR-016).
- A `service_role` key must never exist in this repository. The `anon` key is
  not a secret (see `EnvironmentConfig` doc-comment) and is fine to version.
- `Hermit.Games` (the Game Framework + all game implementations, added in C5)
  never references `Hermit.Networking` or `Hermit.UI` directly. A game's only
  external dependencies are what `GameContext` exposes — analytics + RNG,
  never a Supabase/HTTP client. See `Docs/C5_GAME_FRAMEWORK.md`.
- V1 (`contabilidad_app_backup_2025-08-31 - Copy (2) - Copy`) is a separate
  repo and is never touched from here.

## Assemblies

| Assembly | Folder | References | Purpose |
|---|---|---|---|
| `Hermit.Core` | `Assets/Hermit/Core/` | — | Pure data/utility: logging, environment config, `HermitError`/`HermitResult<T>`. Zero dependencies, on purpose. (C2 once expected the Game Framework to live here; it ended up in `Hermit.Games` instead — see that row.) |
| `Hermit.Networking` | `Assets/Hermit/Networking/` | Core | Auth/backend/session/connectivity contracts **and** their concrete REST implementations (`Supabase*Service`), added in C4. |
| `Hermit.Runtime` | `Assets/Hermit/Runtime/` | Core, Networking, Games | **Composition root.** The one assembly allowed to construct concrete Networking services and games, and wire them together. Holds `HermitBootstrap`, `HermitAppContext`, `BootstrapSceneFlow`, the C4 debug panel, and (`GameFramework/`) the shared game-orchestration stack: `GameHub` (registry+selector+hosts, extracted in C7), `GameSelectorHud`, `ClasicoGameHost`/`ClasicoHud` (C8.1: outer "Hermit chrome" only — score/streak/timer/command/feedback/results/transitions, no game-specific visuals), `HermitTheme`/`RuntimeUIFactory`, `GameFramework/Microgames/` (C8.1: the four Gold microgame presenters — `WesternShootoutPresenter`/`GameShowPresenter`/`BalanceMachinePresenter`/`DetectiveLineupPresenter` — plus `IMicrogamePresenter`/`MicrogameVocabulary`; C8.1b added three presentation/runtime helpers the four presenters share, none of them generic engine systems: `CharacterPrimitives` (procedural two-eye/one-mouth face kit, built once per character, posed via `Idle`/`Correct`/`Incorrect` property writes on cached references), `LocalMotionFx` (the reusable local reaction-motion toolkit — `Punch`/`Shake`/`FlashColor`/`FadeAlpha` — every call scoped to one element inside one presenter, following a strict "no-op if already playing, always restore on completion" contract), and `GameFramework/ProceduralAudio.cs` (runtime-generated `AudioClip`s — tone/sweep/noise — that `ClasicoHud` builds once and plays through its own `AudioSource`, not owned by any single presenter). See `Docs/C8_1B_GOLD_VISUAL_POLISH.md`), and two composition roots that both build a `GameHub` — `ShellInstaller` (`01_Shell`, the real product entry point) and `GameSessionInstaller` (`02_GameplaySandbox`, dev/test only, unchanged by C8.1). See `Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md` and `Docs/C8_1_GOLD_MICROGAME_SLICE.md`. |
| `Hermit.UI` | `Assets/Hermit/UI/` | Core | Reserved. Still empty — the C4 debug panel, Shell, the game selector, and Clásico's screens are deliberately **not** here (see below and `Docs/C5_GAME_FRAMEWORK.md`/`Docs/C6_GAME_REGISTRY_CONTENT_PIPELINE.md`/`Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md`/`Docs/C8_1_GOLD_MICROGAME_SLICE.md`). |
| `Hermit.Games` | `Assets/Hermit/Games/` | Core | **The Game Framework + all game implementations**, added in C5, extended in C6 (`GameDefinition`/`GameSession`/`GameContext`/`GameResult`/`IGameEngine`/`GameFlowController`/`GameRegistry`/`GameCatalog`, `Content/` incl. `ContentValidator`, `Analytics/`, `Clasico/`). Never references Networking or UI. C8.1: `Clasico/ClasicoGameEngine` (one fixed Q&A rhythm) was deleted and replaced by `Clasico/ClasicoSessionDirector` (still one `IGameEngine`, now hosting a sequence of heterogeneous microgames) plus `Clasico/Microgames/` — `MicrogameArchetype`, four Challenge Type data classes (`ClassificationChallenge`/`TrueFalseChallenge`/`EquationChallenge`/`ErrorDetectionChallenge`), `MicrogameContentValidator`, `ClasicoMicrogameLibrary` (in-code sample content + sequencing), and two archetype engines (`SelectionMicrogameEngine`, `BalanceMicrogameEngine`). `Content/`'s `QuestionSet`-family types are kept but no longer referenced by Clásico — see `Docs/C6_GAME_REGISTRY_CONTENT_PIPELINE.md` and `Docs/C8_1_GOLD_MICROGAME_SLICE.md`. |
| `Hermit.Editor` | `Assets/Hermit/Editor/` | Core | Editor-only tooling. Empty today. |
| `Hermit.Tests.EditMode` | `Assets/Hermit/Tests/EditMode/` | Core, Networking, Games | Pure-logic tests, Editor platform only. |
| `Hermit.Tests.PlayMode` | `Assets/Hermit/Tests/PlayMode/` | Runtime, Games | Tests that need the runtime loop (Bootstrap lives in Runtime now, not Core). |

No circular references. `Hermit.Core` still has zero dependencies. Dependency
direction: `Core ← Networking ← Runtime` and `Core ← Games ← Runtime`, as two
parallel branches that never touch each other except inside `Runtime`;
`UI`/`Editor` depend only on `Core` and currently contain nothing.

**Why the debug panel lives in `Hermit.Runtime`, not `Hermit.UI`:** it needs to
call `HermitAppContext` directly (Login/Refresh/Logout/etc.), and `Hermit.UI`
must stay decoupled from Networking per the client/backend boundary above. The
panel is explicitly spike-only tooling, not product UI — putting it in the
composition-root assembly keeps that boundary honest instead of quietly
bending it "just for the debug panel."

**Asmdef gotcha found in C5 (applies to any assembly using uGUI + the New
Input System's UI module):** `com.unity.ugui`'s `UnityEngine.UI` assembly is
genuinely auto-referenced, but `com.unity.inputsystem`'s `Unity.InputSystem`
assembly was **not** picked up automatically for a custom, non-predefined
asmdef in this project despite also being marked `autoReferenced: true` in
its own package asmdef — any assembly whose code touches
`UnityEngine.InputSystem.UI.InputSystemUIInputModule` (or other Input System
types) needs an explicit reference. Both `Hermit.Runtime.asmdef` and
`Hermit.Tests.PlayMode.asmdef` now carry
`"GUID:75469ad4d38634e559750d17036d5f7c"` for exactly this reason (verified
against `Unity.InputSystem.asmdef.meta` in the installed package, not
guessed).

## Namespaces

`Hermit.Core`, `Hermit.Networking`, `Hermit.Runtime`, `Hermit.UI`,
`Hermit.Games`, `Hermit.Editor`, `Hermit.Tests.EditMode`,
`Hermit.Tests.PlayMode` — one namespace per assembly, matching the folder 1:1.

## Environments

Three environments are modeled (`HermitEnvironment` enum) and one data type
(`EnvironmentConfig`, a `ScriptableObject`, now also exposing `IsConfigured`).
A **Development** instance exists at
`Assets/Hermit/Data/Resources/EnvironmentConfig_Development.asset`, pointed at
the same Supabase project V1 already uses
(`https://pgmwxbtnwpggyxipobif.supabase.co`). Its `_supabaseAnonKey` field is
**intentionally blank** — an automated safety check refused to let this
session write a JWT-shaped string to a file, so filling it in is a manual
step: open the asset in the Inspector and paste the anon key from V1's
`supabase-client.js` (`SUPABASE_ANON_KEY` constant). No Staging/Production
instances exist — not needed yet.

The asset lives under a `Resources/` folder specifically so
`HermitRuntimeInstaller` can `Resources.Load<EnvironmentConfig>(...)` it at
startup with zero scene wiring — nothing needs to be manually dragged onto any
GameObject.

## Boot flow (implemented in C4)

`HermitRuntimeInstaller.Install()` runs automatically via
`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` — no component needs to be
placed in `00_Bootstrap` (or any scene) by hand. It spawns one
`DontDestroyOnLoad` GameObject with `HermitBootstrap`, loads
`EnvironmentConfig_Development` from `Resources/`, constructs one
`HermitAppContext`, calls `RestoreSessionAsync()`, and — only when the loaded
config's environment is `Development` — attaches `C4DebugPanel`. A
Staging/Production build (once those configs exist) never shows the panel.

**This hook is scene-agnostic, confirmed in practice, not just in theory:**
`RuntimeInitializeOnLoadMethod(AfterSceneLoad)` fires once after whichever
scene loads first, regardless of which one that is — `00_Bootstrap` is not
special-cased anywhere in *this* path. C5's manual validation built with only
`02_GameplaySandbox` enabled and the C4 panel still started correctly. See
`Docs/C5_GAME_FRAMEWORK.md`, "Runtime bootstrap" for the full writeup.

**Update from C6 — `00_Bootstrap` is now load-bearing, just not for
Networking:** a standalone Player always starts at build index 0
(`00_Bootstrap`), and nothing advanced past it — Editor manual testing never
caught this because opening a gameplay scene directly and pressing Play
loads that scene directly, bypassing build order.
`Hermit.Runtime.BootstrapSceneFlow` (a second, separate
`RuntimeInitializeOnLoadMethod` hook, deliberately not merged into
`HermitRuntimeInstaller`) checks whether the active scene is `00_Bootstrap`
and, if so, loads its destination scene directly. `00_Bootstrap.unity`
itself still holds no content beyond a Main Camera; its role is purely "be
scene 0", not to display anything.

**Update from C7 — the destination is `01_Shell`, not
`02_GameplaySandbox`:** now that `01_Shell`/`ShellInstaller` is the real
product entry point, `BootstrapSceneFlow.DestinationSceneName` (renamed
from `GameplaySceneName`, which would have been actively misleading pointed
at Shell) was repointed there. `02_GameplaySandbox` is unaffected — it
remains reachable by opening it directly in the Editor, per its own
dev/test role (see "C7 objective" below). Full root-cause writeup and the
original fix: `Docs/C6_GAME_REGISTRY_CONTENT_PIPELINE.md`, "Bootstrap scene
flow"; the C7 destination change: `Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md`,
"BootstrapSceneFlow".

## Folders not yet in use

`Text/`, `Art/` exist but are still empty/unused. `Audio/` remains unused —
C7 deliberately shipped without audio. `Runtime/` is resolved (see the
Assemblies table). `Content/` and `Data/` are both in use — see Environments
above and `Docs/C5_GAME_FRAMEWORK.md` ("Content separation") for
`Content/Resources/C5SampleQuestions.asset`; `Data/Resources/` also now
holds `HermitTheme.asset` (C7).

## C8.1 objective

Gold microgame runtime + four playable worlds — implementation complete,
verified by an automated compile + full EditMode/PlayMode suite run +
standalone Windows build (all green, via Unity CLI batch mode), pending the
user's own manual pass. Clásico's C5-C7 fixed Q&A rhythm
(`ClasicoGameEngine`, deleted this phase) is replaced by
`ClasicoSessionDirector` — still exactly one `IGameEngine`, now hosting a
sequence of heterogeneous microgames instead of one question shape.
Ships the C8.0 Design Lock's Gold Slice: Western Shootout (Aim&Select /
Classification), TV Game Show (Choose Side / TrueFalse), Balance Machine
(Balance / Equation), Detective Lineup (Detect Error / ErrorDetection) —
Boxing explicitly deferred. `GameFlowController`/`GameRegistry` gained zero
new states or knowledge of microgames; Clásico is still one registry entry.
`ClasicoHud` split into outer "Hermit chrome" (score/streak/timer/command/
feedback/transitions/results) plus four separately-built presenters in the
new `GameFramework/Microgames/` folder, avoiding both a 1000-line Hud and a
`switch`-driven engine — the only `switch` in the new code is presentation
routing (which presenter to show), never game logic. A real sequencing bug
(an in-place duplicate-patch algorithm that could cycle back to its own
broken state) was caught by an automated test before ever reaching the
user, and fixed with a smaller, more general rejection-sampling approach
rather than a special case. Full design source, migration decision,
per-microgame writeups, and the manual validation checklist: see
`Docs/C8_CLASICO_DESIGN_LOCK.md` and `Docs/C8_1_GOLD_MICROGAME_SLICE.md`.

## C7 objective

Shell real + Clásico gameplay redesign v1 + visual language prototype —
**COMPLETE**, every manual gate PASS including a standalone Windows
Development Build.
`01_Shell` (`ShellInstaller`) becomes the actual product entry point,
wrapping the same `GameHub`/`GameSelectorHud`/`ClasicoGameHost` stack
`02_GameplaySandbox` uses (extracted into `GameHub` this phase specifically
to avoid duplicating that composition logic) behind a Home screen. Clásico
gained a countdown, a visible timer, a streak/combo bonus, and
punch/shake/fade feedback — all inside `ClasicoGameEngine` (superseded in
C8.1 by `ClasicoSessionDirector` — see "C8.1 objective" above)/`ClasicoGameHost`,
with zero new `GameFlowController` states. A first reusable visual language
(`HermitTheme` + a `RuntimeUIFactory` refactor) replaced C5/C6's
per-file-hardcoded colors. A real bug found during manual validation
(incorrect answers flinging the question card off-screen via an
accumulating `ShakeRoutine` offset, caused by `RenderReveal` re-triggering
the animation every frame) was root-caused, fixed, and covered by a
dedicated PlayMode regression test. Full design, decision log, bug writeup,
and manual validation log: see `Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md`.

## C6 objective

Game registry + selector + content pipeline foundation — **COMPLETE**,
16/16 gates PASS, including a standalone Windows Development Build.
`GameRegistry` is enumerable/ordered/enabled-aware; a generic
`GameSelectorHud` replaces Clásico's C5 "Idle" screen as the actual
game-choice entry point; the C5 per-game fixed `Resources.Load` path is
gone, replaced by one `GameCatalog` ScriptableObject loaded once; a
standalone-build scene-routing gap (see "Boot flow" above) was found and
fixed via `BootstrapSceneFlow`; content gained stable-ID-adjacent
versioning (`ContentVersion`/`SchemaVersion`), source/traceability fields,
and a minimal validator. Full design, decision log, and manual validation
log: see `Docs/C6_GAME_REGISTRY_CONTENT_PIPELINE.md`.

## C5 objective

Game Framework + vertical slice (Clásico) — **COMPLETE**, 12/12 gates PASS.
Contracts, lifecycle, content model, scoring, extensibility proof, the
keyboard-navigation fix, and the full manual validation log: see
`Docs/C5_GAME_FRAMEWORK.md`.

## Known housekeeping item

Resolved at C4 closeout: the old C3 `Assets/Hermit/Core/HermitBootstrap.cs`
(superseded by `Hermit.Runtime.HermitBootstrap`) was confirmed to have zero
remaining references — its `GameObject` was already removed from
`00_Bootstrap.unity`, and no scene, prefab, or code pointed at its GUID or
type — and has been deleted along with its `.meta` file.

## C4 objective

Unity ↔ Supabase Technical Spike. Exit criteria, decision, and current
PASS/FAIL state: see `Docs/C4_SUPABASE_SPIKE.md` and `Docs/C4_TEST_RESULTS.md`.
