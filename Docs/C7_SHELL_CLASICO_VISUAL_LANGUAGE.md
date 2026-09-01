# C7 — Shell Real + Clásico Gameplay Redesign v1 + Visual Language Prototype

**Status: FINAL / COMPLETE.** Every gate in "Manual validation" below has
been run by the user in the Unity Editor and in a standalone Windows
Development Build, and all passed — including a real bug found during that
pass (incorrect answers breaking the round) that was root-caused, fixed, and
covered by a dedicated regression test before this phase was closed. Per
this project's own rule, a gate only counts once a human actually runs it;
that has now happened for all of them.

## Objective

Give Hermit a first real product identity on three fronts: `01_Shell`
becomes the actual entry point (not a placeholder), Clásico gets its first
real gameplay-feel redesign (countdown, timer, streak, punch/shake feedback,
transitions) without touching its engine/content-pipeline contracts, and a
small reusable visual language (`HermitTheme`) makes Shell and Clásico speak
the same language instead of each screen hardcoding its own colors.

## Audit findings (before implementing)

- **Purely temporary**: every color/size in `ClasicoHud`/`GameSelectorHud`
  was a hardcoded literal repeated per file (the same dark blue-gray
  independently copy-pasted in both) — flat rectangles, legacy `Text`, no
  visual hierarchy. `01_Shell.unity` was byte-identical to `00_Bootstrap.unity`
  — a Main Camera and nothing else.
- **Reusable as-is**: `ClasicoGameEngine`'s phase model
  (`AwaitingAnswer`/`Revealing`) and the whole content pipeline (stable IDs,
  versioning, validation) needed zero changes to support a real rhythm
  redesign — the engine already exposed exactly the hooks a presenter needs.
  `RuntimeUIFactory` was already the right seam for a shared theme.
  `GameSessionInstaller`'s composition logic (registry → selector → hosts)
  was directly extractable into a shared `GameHub` rather than needing a
  parallel reimplementation for Shell.
- **What limited the game-feel**: no countdown (dropped straight into Q1),
  an invisible timer (silent timeout only), feedback that was one color word
  with no motion, no combo/streak, no screen transitions.
- **Confirmed before writing code**: none of the above required touching
  `IGameEngine`, `GameFlowController`, or `GameRegistry` — every gameplay
  addition fit inside `ClasicoGameEngine`/`ClasicoGameHost`/`ClasicoHud`,
  exactly as the brief's own constraint (section 10) requires.

## Shell

`01_Shell.unity` now holds one `ShellInstaller` GameObject (same
zero-scene-wiring, single-component pattern every installer in this project
uses). `ShellInstaller` builds:

- `ShellHomeHud` — temporary typographic title ("HERMIT"), a one-line
  tagline, one primary "Juegos" button, and a discrete session-status line
  (bottom-right, one short line: "Sesión activa" / "Invitado").
- The same `GameHub`/`GameSelectorHud`/`ClasicoHud`/`ClasicoGameHost` stack
  `GameSessionInstaller` uses in `02_GameplaySandbox` — Shell does not
  reimplement game orchestration, it wraps the existing one behind a Home
  screen and wires the selector's "Volver" button back to Home.

**Session status without a Networking dependency**: `HermitRuntimeInstaller`
(C4) now exposes `public static HermitAppContext Current { get; }`, set once
`Install()` runs. `ShellInstaller` reads it read-only to show "Sesión
activa"/"Invitado" — it never constructs a client or calls Supabase itself,
so the `Hermit.Games`/Shell/Networking boundary is unchanged; Shell is simply
reading a value Networking already made public. Session restore is
asynchronous and not awaited by `HermitRuntimeInstaller`, so this can
under-report "Invitado" for a brief window right after a cold start —
refreshed every time Home is (re)shown, so it is correct well before a
player has been through even one game. See "Known limitations".

Explicitly not built this phase (per the brief): a dashboard, a full
profile, settings, achievements, social, news, or wallet UI.

## Scene flow

```
00_Bootstrap (build index 0, camera-only, technical entry)
    ↓  BootstrapSceneFlow
01_Shell (ShellInstaller — product entry point)
    ↓  "Juegos"
  [selector, inside Shell — no scene change]
    ↓  pick a game
  [game, inside Shell — no scene change]
    ↓  Results → "Volver"
  [selector again]
    ↓  "Volver"
  Shell Home
```

`02_GameplaySandbox` (`GameSessionInstaller`) is kept exactly as the brief
requires — for development, PlayMode testing, and debugging — reachable by
opening it directly in the Editor. It shares 100% of the composition logic
with Shell via `GameHub`; the only difference is Shell wraps it behind a
Home screen and wires "Volver", while the dev/test installer shows the
selector immediately with no back button (there is nothing to go back to in
that scene).

No new scene was created for gameplay — Clásico still runs inside whichever
scene's installer launched it (Shell or Sandbox), matching "no crear una
escena por juego" and "gameplay puede ocurrir dentro de Shell".

## BootstrapSceneFlow

Updated `DestinationSceneName` (renamed from `GameplaySceneName`, which
would have been actively misleading pointed at Shell) from
`"02_GameplaySandbox"` to `"01_Shell"`. Everything else about the class is
unchanged: still one small hook, still separate from
`HermitRuntimeInstaller`, still no Networking/Supabase reference, still fires
exactly once (no loop risk — `RuntimeInitializeOnLoadMethod` fires once per
process lifetime, and `ShouldAdvance` only matches the literal string
`"00_Bootstrap"`, so `01_Shell` loading never re-triggers it).
`BootstrapSceneFlowTests` updated to match (3 tests, pure decision logic
only — same rationale as C6, driving a real scene load isn't a unit test's
job).

## Visual language

### Direction chosen

Dark "arcade premium" ground with one deliberate accent — not a generic
Bootstrap-web blue, not a neon-cyberpunk cyan/magenta cliché. Vivid,
high-contrast feedback colors (green/red) for instant legibility. Rounded
geometry throughout (buttons, panels, cards) instead of C5/C6's flat
rectangles, which read as "debug dashboard" rather than "game" — exactly
what the brief asks to move away from. See `HermitTheme.cs`'s own
doc-comment for the exact hex values and reasoning.

### Theme/tokens

`HermitTheme` (`Hermit.Runtime.GameFramework`, `ScriptableObject`) — one
asset (`Assets/Hermit/Data/Resources/HermitTheme.asset`), loaded once via
`RuntimeUIFactory.Theme` (Resources.Load, cached, with a hardcoded-default
fallback instance if the asset is missing so a missing theme degrades the
look rather than crashing). Centralizes:

- **Colors**: Background, Panel, PanelRaised, Accent, AccentDim,
  TextPrimary, TextSecondary, Correct, Incorrect, Warning.
- **Typography sizes**: Title, Subtitle, Heading, Body, Button, Caption.
- **Spacing/shape**: SpacingUnit, CornerRadius.
- **Animation timings**: Punch, Shake, Transition, CountdownBeat durations.

Not a "design system" — no variants, no per-widget style objects, just named
values `RuntimeUIFactory` and every Hud reads directly. Deliberately small
per the brief's explicit "no design system gigantesco".

### RuntimeUIFactory

Refactored to read every default from `Theme` instead of hardcoded literals.
Added:

- `CreateRoundedPanel` — a panel with theme corner-rounding, for cards/HUD
  chrome.
- `CreateFillBar` — a `Image.Type.Filled` bar (rounded track + rounded
  fill), reused for both Clásico's timer and generically available for any
  future progress readout.
- A procedural rounded-rect sprite generator (`GetRoundedSprite`), cached
  per radius. Chosen over an external asset (no asset hunt) or a custom
  shader (no Shader Graph pipeline exists in this project yet) — a small
  texture, generated once, sliced via `Image.Type.Sliced` so it stretches
  cleanly to any size.
- A real bug caught and fixed while writing this: `Selectable`'s
  `ColorTint` transition *replaces* a graphic's color with the state color
  (times a multiplier) — it does not multiply against whatever the graphic's
  own color was set to beforehand. `CreateButton`'s first draft set
  `image.color` once and relied on `colors.normalColor = white` to "pass it
  through" — that would have been silently overwritten the instant Unity ran
  its first state transition. Fixed by making every `ColorBlock` state an
  explicit, complete color (`AdjustBrightness` computes highlighted/pressed
  as offsets of the theme's base color instead).

Not turned into "a mini React" — still plain static factory methods, no
component/state abstraction layer.

## Clásico redesign v1

### Bug found during manual validation: incorrect answers broke the round

**Symptom reported by the user**: correct answers worked normally, but an
incorrect answer left the timer visibly still running while the question
and its options disappeared — no next question ever appeared, the session
looked stuck even though time kept passing.

**Root cause, confirmed by reading the code, not assumed**:
`ClasicoGameHost.RenderFrame()` called `_hud.RenderReveal(...)` **every
frame** for the entire ~0.9s reveal window — unlike `RenderQuestion`, which
was already guarded to fire once per question via `_lastRenderedQuestionId`.
`RenderReveal` triggers a feedback animation (`StartPunch` on correct,
`StartShake` on incorrect), and both `StartPunch`/`StartShake` restarted
their coroutine from scratch (`StopCoroutine` + `StartCoroutine`) on every
call. Being called every frame meant the animation was restarted every
frame for ~0.9s (~50+ times). `PunchRoutine` computes scale as an absolute
function of `elapsed` reset to near-0 on each restart, so this was
essentially harmless (a barely-visible flicker near scale 1.0) — which is
exactly why correct answers "worked normally". `ShakeRoutine` computes
`target.anchoredPosition` as an **additive offset from a `basePosition`
captured fresh on every restart** — and since the coroutine was always
cancelled before it could reach its own cleanup line
(`target.anchoredPosition = basePosition;`), each restart's `basePosition`
was the *already-shifted* position from the previous frame's partial
offset. The offset accumulated without bound, flinging the question card
roughly a thousand pixels off-screen within under a second. The corrupted
position was never reset anywhere, so it persisted into the next question
too — matching "no aparece la siguiente pregunta" exactly. The `TimerBar`
is a sibling of the question card (not a child of it), so it kept rendering
normally the whole time, off to the side of an invisible, off-screen
question — matching "el timer sigue corriendo" exactly.

**Fix** (minimal, no refactor):
1. `ClasicoGameHost` — added a `_revealRendered` bool guard, the same
   pattern `_lastRenderedQuestionId` already used for `RenderQuestion`.
   `RenderReveal` now fires exactly once per reveal; reset to `false`
   whenever a fresh question actually renders.
2. `ClasicoHud.StartPunch`/`StartShake` — changed from "stop and restart" to
   "no-op if already playing" (defense in depth: even if something ever
   calls these mid-animation again, the coroutine now always reaches its own
   cleanup line and restores a clean resting state, instead of being
   cancelled and abandoned).

Neither change touches `ClasicoGameEngine`, `GameFlowController`,
`GameRegistry`, or any content/scoring logic — the round-rhythm redesign's
actual design (Countdown/AwaitingAnswer/Revealing phases) was correct; only
the *host's rendering cadence* for the reveal phase was wrong.

### Round rhythm

Explicit phases, implemented as `ClasicoGameEngine.Phase`:
`Countdown → AwaitingAnswer (Reveal Prompt + Decision) → Revealing (Lock +
Feedback) → back to AwaitingAnswer for the next question (Transition is the
presenter's fade, not a separate engine phase)`. `GameFlowController` gained
**zero** new states — it still only knows Idle/Preparing/Playing/Ending/
Results; Countdown is entirely internal to `ClasicoGameEngine`, exposed only
as `IsCountingDown`/`CountdownSecondsRemaining`, exactly per the brief's
explicit constraint (section 10).

### Countdown

`ClasicoGameDefinition.CountdownDurationSeconds` (shipped value: 3s).
`ClasicoGameEngine.Begin()` enters a `Countdown` phase before drawing the
first question if configured > 0; `0` (the default for every pre-C7
`CreateInMemory` call site) skips it entirely — behavior identical to C5/C6
unless a definition opts in. `ClasicoHud.RenderCountdown` shows "3, 2, 1,
¡YA!" (whole seconds, `Math.Ceiling`) and dims the question card
(`CanvasGroup.alpha = 0`) behind it.

**Bug found and fixed while implementing this**: the very first draft of
`ClasicoGameHost` called `RenderCurrentQuestion()` unconditionally right
after `Start()`/`Restart()` — with a countdown configured, `CurrentView` is
still `null` at that point, which would have thrown a `NullReferenceException`
the instant a real countdown-enabled definition launched. Fixed by checking
`engine.IsCountingDown` first (`RenderCurrentState()`), confirmed by
`ClasicoGameEngineTests`' countdown tests and a dedicated PlayMode test.

**A second selection bug found and fixed**: during Countdown there are no
option buttons yet. Leaving whatever was selected before (e.g. the
now-hidden Results "Reintentar" button, right after clicking it) would have
repeated exactly the C5 "selection on a deactivated object" bug — just in a
new place. Fixed by selecting the Abort button (the one real Selectable
available during Countdown) once, on the frame the countdown overlay first
appears — not every frame, so it never fights a player who deliberately
navigates elsewhere.

### Timer

`ClasicoGameEngine.QuestionTimeFraction01` (1 → 0 linear decay across the
decision window; always 1 if untimed) drives `ClasicoHud`'s `TimerBar`
(`RuntimeUIFactory.CreateFillBar`), updated every frame during
`AwaitingAnswer` via `ClasicoGameHost.RenderFrame()` — a cheap fill-amount
write, not a rebuild, so it never touches keyboard selection. Color shifts
Accent → Warning → Incorrect as time runs low (thresholds at 50%/25%
remaining), communicating pressure without relying on the fill length alone
(see "Accessibility baseline").

### Combo / streak

Implemented — evaluated as adding real rhythm value for a low cost.
`ClasicoScoring.ComputeStreakBonus(streakAfterThisAnswer, threshold, points)`:
a **flat** bonus every Nth consecutive correct answer (shipped: every 3rd,
+30). A growing multiplier was considered and rejected — unbounded growth
across a long streak reads as an "exploit evidente", which the brief
explicitly asks to avoid; a flat, capped-per-hit bonus does not. Pure,
deterministic, exhaustively tested (`ClasicoScoringTests`). `GameSession`
gained `Streak`/`BestStreak` (same "generic enough, leave at 0 if unused"
reasoning already applied to Score/Round/Correct/Incorrect since C5).
`GameResult.BestStreak` surfaces it to Results. Kept entirely separate from
Hermit Coins — `Hermit.Games` still has zero reference to `Hermit.Networking`.

### Scoring v2

Unchanged formula for the base/speed component
(`ClasicoScoring.ComputeQuestionScore`) — still rewards precision (0 for any
wrong answer) and speed (linear decay bonus). C7 adds the streak bonus as a
**separate, additive** term computed after the base score, not folded into
the same opaque formula — `session.Score += gained + streakBonus;` — so the
two are independently readable/testable rather than one bigger black box.
No RNG anywhere in scoring, no exponential growth, no number that isn't
directly explainable as "base + speed + combo".

### Feedback

- **Correct**: green fill, "¡Correcto!" (+"`N` combo" when a streak bonus
  landed), a short "punch" scale on the whole question card
  (`Mathf.Sin`-eased, ~0.18s, coroutine-driven — no tweening package).
- **Incorrect**: red fill, "Incorrecto", a damped horizontal shake on the
  question card (~0.28s). Never blocks input for longer than the existing
  `FeedbackDisplaySeconds` reveal window — the animation is cosmetic on top
  of, not gating, the round rhythm.
- Both animations always leave the target in a clean resting state
  (`localScale = one` / `anchoredPosition = base`) even if interrupted by a
  new call — `StartPunch`/`StartShake` stop any in-flight routine first.

### Transitions

A single lightweight pattern reused everywhere: `CanvasGroup.alpha` faded
0→1 over `Theme.TransitionDuration` (~0.22s) via a coroutine +
`Mathf.Lerp` — used for the question card on every new question. No
animation framework, no DOTween, no per-screen bespoke transition code.

## Results (redesign)

`ClasicoHud.ShowResults` now shows: Puntaje, Correctas, Incorrectas,
Precisión, **Mejor racha** (new), Duración, inside a rounded card matching
the theme, with Reintentar/Volver ("Volver" replaces C6's "Salir" label —
same button, same wiring, just terminology matching "cierre de partida"
rather than an abrupt exit). No leaderboard, no reward coins — unchanged
from C6's explicit scope line.

## Selector (redesign)

`GameSelectorHud` now reads every color/size from `Theme`, uses rounded
cards, and shows each enabled game's `ShortDescription` as a second line on
its card (already-existing `GameDefinition` metadata from C6, simply
rendered now). Gained an optional **"Volver"** button
(`SetBackAction(Action)`) — hidden by default, shown and wired only when a
caller opts in. `ShellInstaller` opts in (returns to Shell Home);
`GameSessionInstaller` (dev/test) deliberately does not (nothing to return
to in that scene). Still exactly one real game listed (Clásico); a
future disabled/"próximamente" game would render correctly today without
any selector code change — proven generically by `GameSelectorPlayModeTests`
using two entirely fake games, zero Clásico involvement.

## Input

No new input concepts — reuses C5/C6's fix wholesale (`RuntimeUIFactory.Select`/
`ChainVertical`/`ChainHorizontal`), now also covering Shell's Home screen and
the selector's Back button. Every new screen/state this phase added
(Countdown, Shell Home, selector Back) was audited against the three C5
rules explicitly repeated in this brief:

- **Never nothing selected**: Shell Home selects "Juegos" on `Show()`;
  Countdown selects Abort (see "Countdown" above); the selector selects the
  first enabled game or Back as a fallback.
- **Never a deactivated object**: every screen transition (Home↔Selector,
  Selector↔Clásico, Countdown→Question) explicitly re-selects on the
  transition, verified by `AssertValidSelection` in every new PlayMode test.
- **Never re-selected every frame**: the Countdown fix specifically guards
  against this (`if (!_countdownText.gameObject.activeSelf)`), matching the
  question-render guard C5 already established.

Real keys, unchanged from C5/C6 (re-verified against the installed package,
not re-derived from memory): arrow keys/W-A-S-D navigate, **Enter** submits
(not Space, not Numpad Enter), **Escape** cancels, **Tab does nothing by
default**.

## Accessibility baseline

- Feedback is never color-only: correct/incorrect also change the option's
  label text ("¡Correcto!"/"Incorrecto") and the correct/incorrect states
  are additionally distinguished by which specific option is highlighted,
  not just a color swap.
- Timer pressure is communicated by both fill length *and* color (Accent →
  Warning → Incorrect), not color alone.
- All body text sizes come from `Theme.BodySize`/`CaptionSize` (22/18pt at
  the shipped reference resolution) — no sub-16pt text anywhere.
- No flashing/strobing — the punch/shake animations are smooth eased
  motion, not blinking, and both complete well under half a second.
- Focus is always visible (`selectedColor` tinted toward the theme accent,
  a real color change, not the near-invisible uGUI default — see C5's
  original keyboard-navigation fix).

## Performance

- The rounded-rect sprite is generated once per radius and cached
  (`RoundedSpriteCache`) — not regenerated per button/panel.
- `HermitTheme` is loaded once (`Resources.Load`, cached statically) — not
  reloaded per Hud.
- No `Canvas` is rebuilt or GameObjects created/destroyed during gameplay —
  every screen is built once at startup and toggled via `SetActive`/
  `CanvasGroup.alpha`, same pattern as C5/C6.
- The timer bar's per-frame update is a single `fillAmount`/`color` write,
  not a layout rebuild.
- No new `Update()`-loop allocations were introduced — the punch/shake/fade
  coroutines allocate once per trigger (a `Coroutine` handle), not per
  frame.

## Content

Sample content unchanged in substance — still C5's 10 questions. Wording
was not altered. No content migration, no new questions added, matching the
brief's explicit "no transformar C7 en curación académica".

## Tests

**EditMode** (`Hermit.Tests.EditMode`):
- `ClasicoScoringTests` — 4 new streak-bonus tests (lands exactly on
  threshold multiples, zero/negative streak, disabled by zero
  threshold/points, never compounds across repeated hits).
- `ClasicoGameEngineTests` — `BeginEngine` extended with
  `countdownSeconds`/`streakBonusThreshold`/`streakBonusPoints` (all
  default to C5/C6-identical "off" behavior, so every pre-C7 test in this
  file is unaffected). New: 5 countdown tests, 3 timer-fraction tests, 3
  streak tests (one specifically written to be robust against question
  *draw order*, which is RNG — the same care C5's original tests already
  took for option-shuffle order).
- `GameResultTests` — 2 new `BestStreak` tests.
- `BootstrapSceneFlowTests` — updated for the Shell destination (3 tests,
  unchanged shape).

**PlayMode** (`Hermit.Tests.PlayMode`):
- `ClasicoPlayModeTests` — `LaunchClasico()` now waits past the real 3s
  countdown; added a dedicated countdown test (state is Playing, no
  question yet, Abort is selected, mid-countdown) and fixed the Restart
  selection assertion (lands on Abort during the fresh countdown, not
  immediately on Option0). **Regression coverage for the incorrect-answer
  bug above**: `Timeout_StillShowsANewInteractableQuestion` (deterministic —
  the real 8s timeout always counts as incorrect) and
  `IncorrectAnswers_StillShowANewInteractableQuestion_AcrossMultipleOccurrences`
  (plays a full session, asserts the fix's invariants every time
  `CurrentSession.Incorrect` actually increments, and requires at least two
  such occurrences across the session). Both assert: a new question's text
  differs from the previous one, the question card's `anchoredPosition` is
  back within 5 units of its built resting position (the literal thing that
  broke), active options are interactable again, and the new question's
  timer fill is freshly reset. `AdvancingToTheNextQuestion_KeepsASelectionOnAnActiveOption`
  also gained the same card-position check for the correct-or-incorrect
  first answer of a session.
- `GameSelectorPlayModeTests` — 2 new Back-button tests; fixed the
  empty-registry button count (the always-present-but-hidden Back button
  is now accounted for).
- `ShellPlayModeTests` (new) — builds on Home, Juegos reveals the selector
  with Clásico listed and selected, selector Back returns to Home, launching
  Clásico from Shell starts a game, and a full loop test:
  Shell → Juegos → Selector → Clásico → Results → Selector → Shell.

**Deliberately not covered by automated tests**: an actual mouse/touch event
routed through the Input System's raycaster (same rationale as every prior
phase — a human clicking it in the Editor is the real gate), and the visual
correctness of the punch/shake/fade animations themselves (verified
manually, not asserted on pixel state).

## Manual validation

Run by the user in the Unity Editor and confirmed with a standalone Windows
Development Build. Every step below is PASS.

1. Open the project in Unity 6.3. Confirm it compiles with no console errors.
   **PASS.**
2. Test Runner → EditMode → Run All. Expect all tests green, including the
   new streak/countdown/timer tests. **PASS.**
3. Test Runner → PlayMode → Run All. Expect all tests green, including the
   new `ShellPlayModeTests`. This suite is noticeably longer than before —
   several tests now wait through a real 3s countdown on top of the
   existing full-playthrough tests — that's expected, not a hang. **PASS.**
4. Open `Assets/Hermit/Scenes/01_Shell.unity`, press Play. **PASS.**
5. Expect the Shell Home screen: "HERMIT" title, tagline, a "Juegos" button
   already visibly selected, a small session status line bottom-right.
   **PASS.**
6. Click (or press Enter on) **Juegos**. Expect the selector, themed
   (rounded card, accent colors), "Clásico" listed with its short
   description, visibly selected. **PASS.**
7. Launch Clásico. Expect a "3, 2, 1, ¡YA!" countdown before the first
   question appears. **PASS.**
8. Answer a question correctly: expect a green flash, a brief scale
   "punch" on the question card, the timer bar, and the score updating.
   **PASS.**
9. Answer one incorrectly: expect a red flash, a brief shake, the correct
   answer revealed. **PASS** — this is the step where the real bug below
   was originally found (question card flying off-screen); confirmed fixed
   after the `_revealRendered`/no-op-restart fix, including with multiple
   consecutive incorrect answers.
10. Answer 3 in a row correctly: expect "+30 combo" feedback text and the
    streak counter near the score to read "Racha: 3". **PASS.**
11. Let the timer run out on one question (or wait the full 8s): confirm
    the bar visibly reddens before it hits zero, and the timeout still
    auto-submits as incorrect. **PASS.**
12. Finish all 10 questions. Expect a redesigned Results screen: Puntaje,
    Correctas, Incorrectas, Precisión, **Mejor racha**, Duración,
    Reintentar/Volver. **PASS.**
13. Click **Volver** from Results. Expect to land back on the **selector**
    (not Shell Home directly), Clásico re-selectable. **PASS.**
14. Click **Volver** on the selector. Expect to land back on **Shell
    Home**. **PASS.**
15. **Keyboard, from a fresh Play (mouse untouched)**: confirm every step
    above (Home → Juegos → launch → answer → Results → Volver → Volver) is
    fully keyboard-navigable with arrows/W-S and Enter, with a visible
    selection at every step, never landing on nothing or on a hidden
    button. **PASS** (mouse and keyboard both verified independently).
16. **F1 / C4 regression**: press **F1**, confirm the C4 debug panel opens
    without visually breaking Shell's layout underneath, and one call
    (e.g. Login) still succeeds. Press F1 again to collapse it. **PASS.**
17. **C5/C6 regression**: confirm `02_GameplaySandbox` still works exactly
    as before (open it directly, press Play — selector appears immediately,
    no Shell wrapping, no Back button). **PASS.**
18. **Windows Development Build**: File → Build Settings → Build (all three
    scenes enabled, normal order). Run the produced `.exe` and repeat steps
    4–17 standalone. **PASS** — full flow (Bootstrap → Shell → selector →
    Clásico → countdown → timer → correct/incorrect/timeout → combo →
    Results → Volver → Volver), mouse, keyboard, F1/C4 panel, and C5/C6
    regression all reconfirmed standalone, outside the Editor.

Every gate above was actually run by the user, not inferred from the code or
from test coverage.

## Known limitations

- Shell's session-status line can under-report "Invitado" for a brief
  window right after a cold start, since `RestoreSessionAsync` is
  fire-and-forget and not awaited — refreshed every time Home is shown, so
  it self-corrects quickly; not a proper reactive subscription (would be
  over-engineering for one discreet status line this phase).
- The "Volver" chain (`BootstrapSceneFlow`'s direct `00_Bootstrap →
  01_Shell` hop, and `01_Shell` itself) still has no real Shell content
  beyond Home — a dashboard/profile/settings pass is explicitly deferred.
- No touch/gamepad testing was done (no such hardware in this session) —
  the Input System's default UI actions should already support both,
  unverified.
- No audio at all, still — deliberately, per the brief's stated preference.
- `IGamePresenterHost`'s dictionary-registration pattern remains proven
  only with fake games (`GameFrameworkExtensibilityTests`,
  `GameSelectorPlayModeTests`) — still no second *real* game to confirm it
  empirically (unchanged from C6).
- `HermitTheme`'s corner-rounding sprite is procedurally generated at
  runtime, not hand-authored art — functional and cheap, but a real UI pass
  should replace it with authored 9-sliced assets.

## Animation policy

Documented, applied consistently across every animation added this phase:

- Short duration (≤0.3s for punch/shake, ~0.22s for fades) — never blocks
  the round rhythm.
- Every animation communicates a state change (correct/incorrect/new
  question) — none are purely decorative.
- Input is never gated by an animation — a player can answer the next
  question as soon as the engine's own reveal timer allows, regardless of
  whether a punch/shake/fade is still visually finishing.
- Coroutines + `Mathf` only — no tweening package installed.

## Decisions discovered while implementing

- **Two selection bugs found by construction, not by testing**: the
  Countdown phase's "no question yet" state broke two assumptions the C5/C6
  code silently relied on (that `CurrentView` is always non-null while
  Playing, and that *something* interactive always exists to select). Both
  are documented above under "Clásico redesign v1 → Countdown" and fixed
  before any test was written against them — caught during design review,
  not discovered by a failing test.
- **`GameFlowController.CurrentEngine` unchanged, no new engine-facing
  surface needed**: Countdown/timer-fraction/streak were all addable purely
  as new members on `ClasicoGameEngine` itself, reachable through the
  existing `(ClasicoGameEngine)controller.CurrentEngine` cast
  `ClasicoGameHost` already performed — no change to `IGameEngine`,
  `GameFlowController`, or `GameRegistry` was needed anywhere in this phase.
- **`GameHub` extraction was the minimal generalization, not a
  premature one**: Shell needing the *exact* same registry/selector/host
  composition `GameSessionInstaller` already had (just wrapped behind a
  Home screen) was the concrete, present duplication that justified
  extracting it — not a speculative "might need this later" abstraction.
