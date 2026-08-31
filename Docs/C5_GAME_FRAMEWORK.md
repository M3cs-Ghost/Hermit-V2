# C5 — Game Framework + Vertical Slice (Clásico)

**Status: COMPLETE.** All gates below are PASS, manually verified by the user
in the Unity Editor and in a standalone Windows Development Build. See
"Manual validation" for the full evidence log.

## Objective

Prove a reusable game loop exists, that mechanic and academic content are
separated, that a session starts/runs/ends cleanly, that scoring/results
don't couple to UI, and that a second game can be added without editing the
framework. Clásico is the first real consumer, not the final product.

## Architecture

```
Hermit.Core        — shared primitives (HermitResult, HermitError, HermitLog).
                      Knows nothing about games.
Hermit.Games       — the Game Framework + all game implementations.
                      References Core only. No Networking, no Supabase, no UI.
  ├─ GameDefinition / GameContext / GameSession / GameResult / IGameEngine
  ├─ GameFlowController, GameRegistry
  ├─ Content/   — QuestionDefinition, AnswerOption, QuestionSet, IContentProvider
  ├─ Analytics/ — IGameAnalyticsSink + two implementations
  └─ Clasico/   — ClasicoGameDefinition, ClasicoGameEngine, ClasicoScoring
Hermit.Runtime     — composition root. References Core, Networking, Games.
  └─ GameFramework/ — RuntimeUIFactory, ClasicoHud, ClasicoVerticalSliceInstaller
                       (the presentation layer — see "Where the UI lives")
```

This mirrors C3/C4's existing boundary rule (`Core ← Networking ← Runtime`)
with `Games` added as a second, parallel branch off `Core`
(`Core ← Games ← Runtime`), never touching `Networking`.

### Where the UI lives

The C5 brief asks for two things that pull in opposite directions: "Games
must not know concrete UI" and "Clásico needs a playable uGUI screen."
Resolution: `Hermit.Games` stays pure C# (no `UnityEngine.UI`, no Input
System) and only exposes read-only view state
(`ClasicoGameEngine.CurrentView`, `.IsRevealing`, `.SelectedOptionIndex`,
`.CorrectOptionIndex`) plus one input method (`SubmitAnswer`). The actual
uGUI screen (`ClasicoHud`) and its wiring (`ClasicoVerticalSliceInstaller`)
live in `Hermit.Runtime` — the one assembly this project's own architecture
already designates as allowed to see everything. `Hermit.UI` (reserved for
genuinely reusable, game-agnostic widgets) is intentionally left untouched
this phase — Clásico's screen is not reusable by another game, so putting it
there would have been a boundary violation in the other direction.

## Contracts

Evaluated against C2's naming rather than copied blindly:

| C2 concept | C5 shape | Notes |
|---|---|---|
| `GameDefinition` | `abstract class GameDefinition : ScriptableObject` | Kept as-is — Unity-idiomatic (Inspector-tunable config). `ClasicoGameDefinition` subclasses it. |
| `GameSession` | `sealed class GameSession` (plain C#) | Kept. Public settable properties, not `internal` — any `IGameEngine`, including one outside this assembly (see Extensibility), must be able to write to it. |
| `GameContext` | `sealed class GameContext` | Narrowed from "whatever a game needs" to exactly `IGameAnalyticsSink` + `System.Random`. No Supabase, no HTTP client, no raw token — a game cannot reach the network through this type by construction. |
| `GameResult` | `sealed class GameResult` (immutable) | Kept. `AccuracyPercent` computed in the constructor, not stored redundantly. |
| `GameManager` + `GameFlowController` | merged into one `GameFlowController` | See "Deviations from C2" below. |

## Lifecycle

`Idle → Preparing → Playing → Ending → Results → Idle`, owned exclusively by
`GameFlowController.State` — no other type tracks lifecycle state, so there
is no second source of truth to desync from (see "Deviations from C2").

- **Start** — `Idle`/`Results` → `Preparing` → engine `Begin()`s a fresh
  `GameSession` → `Playing`.
- **Finish (normal)** — engine reports `IsFinished` after a `Tick` →
  `Ending` → `BuildResult(completed: true)` → `Cleanup()` → `Results`.
- **Abort** — caller calls `Abort()` from `Playing` → same `Ending` path with
  `completed: false`.
- **Restart** — from `Results`, calls `Start()` again with the same stored
  registration/context, producing a brand-new engine and session (proven by
  `GameFlowControllerTests.Restart_FromResults_CreatesABrandNewEngineAndFreshSession`).
- **Cleanup** — `AcknowledgeResults()` drops the engine/session/context
  references and returns to `Idle`.

No scene load is involved in any transition — everything happens inside one
loaded scene (`02_GameplaySandbox`), per the C5 brief.

## Deviations from C2

- **`GameManager` and `GameFlowController` merged.** In C2 these were two
  separate concepts. Writing the actual code, they turned out to be the same
  responsibility under two names — a "manager" that owns the active game and
  a "flow controller" that owns its lifecycle state would have needed to
  agree with each other on every transition, which only creates a
  synchronization bug waiting to happen. One class, `GameFlowController`,
  does both.
- **`GameSession.State` dropped.** An early draft kept a lifecycle state on
  the session too (per C2's own field list). Removed once it became clear
  two objects (session and controller) tracking the same enum is exactly the
  desync risk above, just moved one level down. `GameFlowController.State`
  is the only lifecycle authority.
- **`GameContext` narrowed, not left generic.** C2 described Context as
  "whatever a game needs." Left that open-ended, it would have grown into a
  service-locator (explicitly forbidden in this phase). Instead it carries
  exactly `IGameAnalyticsSink` + `Random`; a game needing something else
  takes it through its own engine's constructor (see `ClasicoGameEngine`,
  built via a factory closure that can capture anything it needs), not
  through the shared framework type.

## Registration

`GameRegistry` — a plain instance, owned by `ClasicoVerticalSliceInstaller`
(the composition root), holding `GameId → (GameDefinition, Func<IGameEngine>)`
pairs. Chosen over a ScriptableObject-based auto-discovery registry or a
reflection scan because C5 has exactly one real caller that needs the game
list, and that caller already knows which definitions exist — scanning for
them would solve a problem this phase doesn't have. No static/global
instance anywhere: nothing can reach "the games" as a hidden singleton.

`GameFlowController` never branches on `GameId` or on a concrete engine
type — it only ever calls through `IGameEngine`/`GameRegistration`. See
"Extensibility test".

**C6 update:** `GameRegistration` was dropped — `GameDefinition.CreateEngine()`
replaced the externally-supplied factory closure, so wrapping a definition a
second time stopped adding a real guarantee. `GameRegistry`/`GameFlowController`
now hold/take `GameDefinition` directly. See
`Docs/C6_GAME_REGISTRY_CONTENT_PIPELINE.md`, "Registration model".

## Content separation

`QuestionDefinition`/`AnswerOption` are plain `[Serializable]` classes (not
individual ScriptableObjects) held in an array inside one `QuestionSet`
ScriptableObject asset. Considered one-ScriptableObject-per-question (closer
to a future Knowledge Engine's likely shape) and rejected for C5: it would
have meant ~20 extra asset+meta files for content explicitly marked
temporary, with no present benefit (no per-question Inspector workflow is
needed yet). `Domain`/`Topic`/`Difficulty`/`Tags` exist on `QuestionDefinition`
now, ahead of present need, specifically because they cost nothing today and
are the exact shape the Knowledge Engine is expected to want later — no
misconception/confusable/source fields yet, since a 10-question sample has
no real use for them.

`ClasicoGameEngine` never reads a `QuestionSet` directly — it goes through
`IContentProvider` (`QuestionSetContentProvider`), so swapping the content
source later (e.g. a Supabase-backed provider in a future phase) means
implementing one interface, not touching the engine.

**C5 sample content:** `Assets/Hermit/Content/Resources/C5SampleQuestions.asset`
— 10 basic accounting questions (activo, pasivo, patrimonio, ecuación
contable, ingreso, gasto, débito/crédito, dos ejemplos de cuentas). Explicitly
labelled `"C5 sample content"` in its own `Description` field. Not the
academic library — no Supabase content pipeline was built or touched.

## Scoring

`ClasicoScoring.ComputeQuestionScore` — pure, static, deterministic:
correct = `pointsPerCorrectAnswer` + a linearly-decaying speed bonus (full at
instant answer, zero at the time limit); incorrect = always 0. Untimed play
or a zero speed-bonus config skips the bonus term entirely. No coins, no
rewards, no backend score, no leaderboard — "game score" (`GameSession.Score`,
an `int`) is entirely separate from Hermit Coins, which C5 does not touch at
all (no `Hermit.Networking` reference exists anywhere in `Hermit.Games`).

## Clásico vertical slice

`ClasicoGameEngine` (`IGameEngine`): draws `QuestionCount` questions without
repetition (`QuestionSetContentProvider`, sampling without replacement),
shows one at a time with its options shuffled, accepts one `SubmitAnswer`,
reveals for `FeedbackDisplaySeconds`, advances; finishes once every drawn
question has been answered (or the pool was empty). A per-question timeout
(`TimePerQuestionSeconds`, 8s in the shipped definition) auto-submits as
incorrect if the player doesn't answer in time.

Config lives in `Assets/Hermit/Data/Resources/ClasicoGameDefinition.asset`
(10 questions, 8s/question, 100 base points, up to 50 speed bonus, 0.9s
reveal) — tunable without touching code.

## UI, input, feedback

Built entirely from code (`RuntimeUIFactory` + `ClasicoHud`), the same
zero-scene-wiring approach C4's debug panel used — except this is real uGUI
(`UnityEngine.UI.Text`/`Button`/`Image`), not IMGUI, per this phase's
requirement that gameplay UI must be uGUI. Three screens (Idle/Playing/
Results) toggle via `SetActive`; nothing is instantiated or destroyed
per-frame. Uses Unity's built-in `LegacyRuntime.ttf` font rather than
TextMeshPro — this project has never imported TMP resources, and a runtime
font-asset dependency the coding agent cannot verify inside the Editor was
judged a worse risk than plain `Text`. Revisit when a real UI pass replaces
this screen with authored TMP prefabs.

Input: New Input System, via a runtime-created `EventSystem` +
`InputSystemUIInputModule` (`RuntimeUIFactory.EnsureEventSystem`, guarded
against duplicates). No custom `InputActionAsset` was authored — verified
directly against `com.unity.inputsystem@1.20.0`'s `DefaultInputActions.inputactions`
and `Keyboard.cs` (not assumed), the module auto-assigns its built-in default
actions (`InputSystemUIInputModule.OnEnable` calls `AssignDefaultActions()`
whenever no actions are configured), which bind:

- **Navigate:** arrow keys and WASD (keyboard), left/right stick + d-pad
  (gamepad), stick (joystick).
- **Submit:** the `enter` key specifically (its usage tag is literally
  `"Submit"` in `Keyboard.cs`) — **not Space, not Numpad Enter**, neither of
  which carries that usage tag. Also bound on gamepad/touch/joystick/XR.
- **Cancel:** `escape` (usage tag `"Cancel"`).
- **Tab is not bound to anything UI-related by default** — do not expect it
  to move focus; a real keyboard-nav pass would need to add it explicitly if
  desired.
- Point/Click: mouse, pen, touch — unchanged, this is what already worked.

This covers "ready for touch/gamepad without implementing them this phase."
See "Keyboard navigation fix" below for why default actions alone were not
enough to make Navigate/Submit actually do anything.

### Keyboard navigation fix

Manual validation found mouse worked but keyboard did not. Root cause was
**not** the input actions above (which were already correct and auto-wired)
— it was that nothing ever told Unity's `EventSystem` what should be
selected:

- `EventSystem.firstSelectedGameObject` was never set, so a fresh Idle screen
  had nothing selected and arrow keys had no starting point.
- No code called `EventSystem.SetSelectedGameObject(...)` when a screen
  changed (Idle→Playing, Playing→Results, Results→Playing on Restart) or a
  new question appeared, so even a selection made by clicking with the mouse
  was left pointing at a GameObject that the very next screen change would
  deactivate — silently breaking keyboard input on every screen after the
  first.
- Separately, `RuntimeUIFactory.CreateButton` never set `ColorBlock.selectedColor`,
  which defaults to a near-white tint (`~0.96,0.96,0.96`) — multiplied
  against this UI's dark button color, a keyboard-selected button looked
  visually identical to an unselected one even on the rare frame selection
  was correct.

Fix (`ClasicoHud.cs`, `ClasicoVerticalSliceInstaller.cs`, `RuntimeUIFactory.cs`):

- `ClasicoHud` now explicitly calls `EventSystem.SetSelectedGameObject(...)`
  in `ShowIdle()` (selects Play), `ShowResults()` (selects Restart), and
  `RenderQuestion()` (selects the first active option) — every screen/question
  change re-establishes a valid selection instead of hoping one survives.
- `EventSystem.firstSelectedGameObject` is also set once, as a fallback for
  the very first frame before any explicit `Select()` call has run.
- The option buttons get an explicit vertical `Navigation` chain
  (`Option0 ↔ Option1 ↔ Option2 ↔ Option3`) instead of relying on
  `Selectable`'s spatial "Automatic" mode, which could otherwise resolve a
  neighbor unpredictably (e.g. toward the separately-positioned Abort
  button). Results' Restart/Exit get an explicit horizontal chain.
- `RuntimeUIFactory.CreateButton` now sets `colors.selectedColor` to a
  distinct, clearly visible blue.
- `ClasicoVerticalSliceInstaller.Update()` was also calling
  `ClasicoHud.RenderQuestion` (which resets option state) every single frame
  while awaiting an answer, not just once per question — that would have
  fought any manual selection change by resetting it back to option 0 every
  frame. It now renders (and re-selects) only when the question actually
  changes, tracked via a `_lastRenderedQuestionId` guard reset on every
  Start/Restart.

Feedback: on submit, the chosen option's background flashes red (incorrect)
or the correct option flashes green, a "¡Correcto!"/"Incorrecto" line appears
in-color, and all options disable for the reveal window before the next
question — no external packages, no audio (a placeholder was judged
unnecessary noise for a slice this small).

## Analytics hooks

`IGameAnalyticsSink`: `GameStarted`, `QuestionPresented`, `AnswerSubmitted`,
`GameCompleted`, `GameAborted`. Two implementations ship: `NullGameAnalyticsSink`
(default, used by every test) and `HermitLogAnalyticsSink` (used by the
vertical slice, writes to the Console via `HermitLog` so the events are
visible during manual testing). Nothing sends anything to Supabase or any
backend — swapping in a real sink later touches this one interface's
implementation, not `GameFlowController` or any game.

## Extensibility test

`Assets/Hermit/Tests/EditMode/Fakes/FakeGame.cs` defines `FakeGameDefinition`
and `FakeGameEngine` **outside `Hermit.Games` entirely** (a separate test
assembly), implementing only the public `GameDefinition`/`IGameEngine`
contract. `GameFrameworkExtensibilityTests` registers it into a
`GameRegistry` and runs it through `GameFlowController` end to end. Nothing
in `Hermit.Games` — not `GameRegistry`, not `GameFlowController`, not any
Clásico type — was edited to make this work; if it had needed to be, this
test would not compile. This is a stronger proof than adding a second game
*inside* `Hermit.Games` would have been.

## Runtime integration

`ClasicoVerticalSliceInstaller` is a separate, scene-scoped `MonoBehaviour`
placed directly on one GameObject in `02_GameplaySandbox` (the same
single-component, zero-serialized-field pattern `HermitBootstrap` already
used in `00_Bootstrap`). It does not touch `HermitRuntimeInstaller` and
`HermitRuntimeInstaller` does not know it exists — the C4 networking/auth
boot path is completely unmodified conceptually and in code (the only
touched file from C4 is `EnvironmentConfig.cs`'s already-existing
`IsConfigured` — untouched this phase). C5 needs no Supabase call anywhere;
`GameContext` cannot reach one even if a game tried.

## No persistence

Score/results live only in `GameSession`/`GameResult` in memory for the
duration of one play session. Nothing is written to disk, `PlayerPrefs`, or
any backend. Restarting Play mode loses all C5 game state — expected and
correct for this phase.

## Tests

**EditMode** (pure logic, no Editor/Play loop needed):
`GameFlowControllerTests` (start/tick/finish/abort/restart/acknowledge/guard
rails), `GameFrameworkExtensibilityTests` (see above),
`ClasicoScoringTests` (exhaustive scoring cases), `QuestionSetContentProviderTests`
(draw count, no-repeat, pool-cap, seed variety), `ClasicoGameEngineTests`
(question flow, submit-once, reveal timing, timeout auto-submit, result
totals, empty pool), `GameResultTests` (accuracy math incl. zero-division).

**PlayMode** (`ClasicoPlayModeTests`, in `Hermit.Tests.PlayMode`, 11 tests):
builds the real `ClasicoVerticalSliceInstaller` and drives it by invoking the
actual `Button.onClick` events a player's click would fire — not by calling
`GameFlowController` directly — so it also proves `ClasicoHud`'s wiring.
Covers: UI builds on Idle, Play starts a session and shows a question,
answering updates correct/incorrect, advancing to the next question, playing
through all questions reaches Results, Restart gets a fresh session, Abort
reaches Results as incomplete, Exit-from-Results returns to Idle and Play
works again, a second installer never creates a duplicate `EventSystem`, and
— added for the keyboard navigation fix — an `EventSystem` with
`InputSystemUIInputModule` exists, and every screen/question transition
(Idle, first question, next question, Results, post-Restart, post-Exit)
leaves the `EventSystem`'s selection on a real, active object, never a
disabled leftover from the previous screen.

**Deliberately not covered by automated tests:** an actual mouse/touch event
routed through the Input System's raycaster hitting a button. Simulating
that reliably in a headless PlayMode run is its own source of flakiness this
slice doesn't take on — a human clicking it in the Editor (see "Manual
validation") is the real gate for that.

## Manual validation

**Status: COMPLETE. All gates PASS**, run by the user in both the Unity
Editor and a standalone Windows Development Build (`Builds/Windows/Dev`, via
a throwaway `Windows C5 Sandbox` Build Profile built with only
`02_GameplaySandbox` enabled — see "Runtime bootstrap" below for why that
still exercised the real C4 Networking path correctly; the profile and the
scene-list change were reverted after validation).

| # | Gate | Status | Notes |
|---|---|---|---|
| 1 | Clásico playable start-to-finish | **PASS** | `02_GameplaySandbox`, Editor and standalone build. |
| 2 | Scoring | **PASS** | Correct/incorrect accounting and speed bonus confirmed live. |
| 3 | Results | **PASS** | Score/Correct/Incorrect/Accuracy/Duration shown correctly. |
| 4 | Restart | **PASS** | Fresh session, score reset, no Idle flicker. |
| 5 | Abort / Salir | **PASS** | Mid-game exit reaches Results as "Partida abandonada". |
| 6 | Timeout | **PASS** | 8s per-question timeout auto-submits incorrect and advances. |
| 7 | EditMode tests | **PASS** | All green, including the 6 C5 suites + pre-existing C4 tests. |
| 8 | PlayMode tests | **PASS** | All 11 `ClasicoPlayModeTests` green, plus `HermitBootstrapPlayModeTests`. |
| 9 | Mouse input | **PASS** | All buttons, all screens. |
| 10 | Keyboard input | **PASS** | Arrows/W-S navigate, Enter activates, selection visible throughout, Results/Restart/Salir all keyboard-reachable — see "Keyboard navigation fix" above for what was fixed and exactly which keys are real. |
| 11 | Windows Development Build | **PASS** | Built and run outside the Editor; gameplay, mouse, keyboard, results, and timeout all re-verified against the standalone `.exe`. |
| 12 | C4 regression | **PASS** | Debug panel (login/profile/wallet/RPC) still works; C5 introduced no change to `Hermit.Networking` or `HermitRuntimeInstaller`. |

**Gate state: 12/12 PASS. C5 objective (reusable game loop, mechanic/content
separation, clean session lifecycle, decoupled scoring, multi-game-ready
framework, Clásico as first real consumer, extensibility without core edits)
is met.**

### Manual evidence log (user-confirmed)

- Clásico runs correctly in `02_GameplaySandbox`; a session starts, correct
  and incorrect answers both work, score calculates and displays.
- The 8s timeout fires and advances correctly.
- Results appear on finish; Reintentar and Salir/abort both work.
- Mouse works on every screen.
- Keyboard: arrows/W-S navigate, Enter selects, Results screen is
  keyboard-navigable, Reintentar/Salir both reachable and activatable by
  keyboard.
- Windows Development Build run outside the Editor: gameplay, mouse,
  keyboard, results, and timeout all re-verified against the standalone
  executable.
- The C4 debug panel still opens and Profile/Wallet still respond — no
  regression from C5's changes.

### Procedure as executed

1. Open the project in Unity 6.3, open scene `Assets/Hermit/Scenes/02_GameplaySandbox.unity`.
2. Window → General → Test Runner → EditMode → Run All. Expect all tests
   green, including the new `GameFlowControllerTests`,
   `GameFrameworkExtensibilityTests`, `ClasicoScoringTests`,
   `QuestionSetContentProviderTests`, `ClasicoGameEngineTests`,
   `GameResultTests` (plus the pre-existing C4 EditMode tests, unaffected).
3. Test Runner → PlayMode → Run All. Expect `ClasicoPlayModeTests` (11 tests,
   including the keyboard-selection tests added for this fix) green alongside
   the pre-existing `HermitBootstrapPlayModeTests`. This suite takes
   noticeably longer than EditMode (~30–60s) because a few tests play through
   all 10 real questions in real time — that's expected, not a hang.
4. Press Play. A dark screen titled "Clásico" with a "Jugar" button should
   appear immediately — no console errors.
5. Click **Jugar**. A question and up to 4 options should appear, with
   "Pregunta 1/10" top-left and "Puntaje: 0" top-right.
6. Click any option. Expect: the correct option flashes green, a wrong pick
   also flashes red, "¡Correcto!"/"Incorrecto" appears, buttons disable
   briefly, then the next question appears automatically with the score
   updated.
7. Answer all 10 questions (or wait 8s on one to confirm the timeout
   auto-submits as incorrect and still advances). Expect a Results screen:
   Puntaje/Correctas/Incorrectas/Precisión/Duración, plus **Reintentar** and
   **Salir** buttons.
8. Click **Reintentar**. Expect an immediate fresh game (Pregunta 1/10,
   Puntaje: 0) — no flash of the Idle screen in between.
9. Mid-game, click **Salir** (top-left, during Playing). Expect the Results
   screen to appear titled "Partida abandonada" instead of "Resultados".
10. From Results, click **Salir**. Expect the Idle "Jugar" screen again;
    clicking **Jugar** again must start a normal new game.
11. **Keyboard, from a fresh Play (mouse untouched):**
    - The "Jugar" button should already look visibly selected (blue) with no
      click at all. Press **Enter** — the game should start.
    - On the question screen, one option should already be visibly selected.
      Press **↑/↓** (or **W/S**) — the highlighted option should move between
      the 4 options. **Tab does nothing by default — do not expect it to.**
    - Press **Enter** on a highlighted option — it should submit exactly like
      a click (feedback shows, score updates).
    - After advancing to the next question, an option should again be
      visibly selected without touching the mouse.
    - Reach Results using only the keyboard (Enter to submit each question).
      **Reintentar** should be visibly selected; press **←/→** to move to
      **Salir**, then **Enter** to activate whichever is selected.
    - Confirm the mouse still works throughout (click any button at any
      point and it should behave exactly as before this fix).
12. **C4 regression:** press Play on `00_Bootstrap` (or whichever scene the
    Development build launches from) and confirm the existing C4 debug panel
    (login/profile/wallet/RPC) still works exactly as before — C5 must not
    have changed that behavior. This does not need re-running the full C4
    manual checklist, just confirming the panel still opens and one call
    (e.g. Login) still succeeds.
13. **Windows Development Build:** File → Build Settings → Build. Run the
    produced `.exe`, load `02_GameplaySandbox` (or confirm it's reachable
    from the build's actual entry flow), and repeat steps 4–10.

All 13 steps above were executed by the user and passed — see the gate table
and evidence log at the top of this section.

## Known limitations

- Legacy `UnityEngine.UI.Text`/built-in font, not TextMeshPro — a real UI
  pass should replace this with authored TMP prefabs.
- No touch/gamepad testing was done (no such hardware in this session) —
  the Input System's default UI actions should already support both, but
  this is unverified.
- No audio at all, not even a placeholder.
- `GameSession`'s Score/Round/Correct/Incorrect fields are quiz-shaped; a
  future game with a fundamentally different shape (e.g. a pure rhythm/timing
  game with no right/wrong) may find them insufficient — leave them unused
  rather than force-fit, and revisit the session shape only once a second
  *real* game actually needs something they can't express.
- ~~The vertical slice's `ClasicoGameDefinition`/`C5SampleQuestions` are loaded
  by fixed `Resources.Load` path~~ — **resolved in C6**: a single
  `GameCatalog` ScriptableObject (one `Resources.Load`, not one per game)
  now holds every registered `GameDefinition`, and a real `GameSelectorHud`
  enumerates `GameRegistry` instead of hardcoding a path. See
  `Docs/C6_GAME_REGISTRY_CONTENT_PIPELINE.md`, "Resources strategy".
- During manual validation the user temporarily built with only
  `02_GameplaySandbox` enabled in `ProjectSettings/EditorBuildSettings.asset`
  (via a throwaway `Windows C5 Sandbox` Build Profile), to test the vertical
  slice standalone. Both have since been reverted/removed — see "Runtime
  bootstrap: what actually starts it, and what 00_Bootstrap is for" below for
  why that build still showed the C4 panel correctly, and why an earlier
  draft of this document was wrong about the reason.

## Runtime bootstrap: what actually starts it, and what 00_Bootstrap is for

An earlier draft of this document claimed that disabling `00_Bootstrap` in
the build scene list would stop `HermitRuntimeInstaller` from running,
breaking Networking. **That claim was wrong**, and manual validation proved
it wrong before this correction was written: the `Windows C5 Sandbox` build
had only `02_GameplaySandbox` enabled (build index 0), yet the C4 debug
panel still appeared and Profile/Wallet still worked.

**Why:** `HermitRuntimeInstaller.Install()` is attributed
`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]`.
This attribute is a global engine hook, not a per-scene one — Unity invokes
it once, automatically, after **whichever scene loads first** at player
startup, regardless of that scene's name, content, or number of
GameObjects in it. It does not require `00_Bootstrap` specifically, does not
require any GameObject or component to exist in that first scene, and would
fire identically if `02_GameplaySandbox` (or any other scene) were build
index 0. This has been true since C4 — C5 did not change it, and the manual
test above is direct evidence of it, not just a reading of the attribute's
documentation.

**So what does `00_Bootstrap.unity` actually do today?** As of C4's closeout,
nothing functional. It once held a `Hermit.Core.HermitBootstrap` component
(a minimal "confirm Core loaded" marker, C3-era); that GameObject was
removed from the scene and the script deleted once
`Hermit.Runtime.HermitRuntimeInstaller`/`HermitBootstrap` took over
(see `Docs/C4_SUPABASE_SPIKE.md` and the "Known housekeeping item" history in
`Docs/ARCHITECTURE.md`). Today the scene contains only a default Main
Camera — it is an empty placeholder that exists to preserve the
`00_Bootstrap → 01_Shell → 02_GameplaySandbox` naming/ordering convention
for a scene flow that has not been implemented yet (no additive loading, no
Shell/menu logic exists in any of these scenes). Its only real effect right
now is which scene happens to be build index 0, and — as shown above — that
does not gate Networking startup at all.

The correct, evidence-based caution is therefore not about Networking: it is
that `01_Shell`/`00_Bootstrap` will need real content and scene-flow logic
before a build is representative of the eventual product, and that work has
not started. `ProjectSettings/EditorBuildSettings.asset` has been restored to
all three scenes enabled, in their original order, so the project's default
build configuration is not left permanently scoped to the C5 test scene.

## Closeout

C5 is **COMPLETE**. 12/12 gates PASS, verified manually by the user in the
Unity Editor and in a standalone Windows Development Build. The keyboard
navigation bug found during manual validation (see "Keyboard navigation
fix" above) is resolved and covered by 5 new/extended PlayMode tests.
`Hermit.Runtime.asmdef` and `Hermit.Tests.PlayMode.asmdef` both correctly
reference `Unity.InputSystem` via `GUID:75469ad4d38634e559750d17036d5f7c`
(verified against the installed package's own `.asmdef.meta` — not a
guessed GUID). No V1, Networking, Supabase, or Hermit Coins changes were
made at any point in this phase.

A documentation error was caught and corrected before commit: an earlier
draft of this closeout wrongly attributed Networking startup to
`00_Bootstrap` being enabled in the build scene list. It is not —
`HermitRuntimeInstaller` runs via a global `RuntimeInitializeOnLoadMethod`
hook independent of which scene loads first, confirmed by the user's own
manual evidence (C4 panel worked in a build where `02_GameplaySandbox` was
the only enabled scene). See "Runtime bootstrap" above for the full
correction. `ProjectSettings/EditorBuildSettings.asset` has been restored to
its normal state (`00_Bootstrap`, `01_Shell`, `02_GameplaySandbox`, all
enabled, original order) and the throwaway `Windows C5 Sandbox` Build
Profile has been deleted — it was a phase-specific test artifact with no
architectural role, not something the project needs going forward.
