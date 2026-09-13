# C8.1 — Gold Microgame Runtime + Four Playable Worlds

**Status: implementation complete and automatically verified (compile +
full EditMode/PlayMode suites + a standalone Windows build all run via
Unity 6000.3.23f1's CLI batch mode from this session, all green — see
"Automated verification" below) — pending the user's own manual pass in
the Editor before this phase can be marked COMPLETE.** Per this project's
own rule, no gate is PASS until a human actually runs it — the automated
runs below are strong, real evidence (not "compiles by inspection"), but
they cannot replace a human confirming the four worlds actually look and
feel right, or clicking through the real mouse/keyboard/F1 experience.

## Objective

Turn Clásico from C5-C7's single fixed Q&A rhythm into what
`Docs/C8_CLASICO_DESIGN_LOCK.md` defines it as: one session hosting a
sequence of heterogeneous microgames. Prove exactly one thing —
*"can we switch mechanic, world, and input every few seconds inside one
Clásico session and have it still feel coherent?"* — by building the Gold
Slice the design lock names: Western Shootout (Aim&Select/Classification),
TV Game Show (Choose Side/TrueFalse), Balance Machine (Balance/Equation),
Detective Lineup (Detect Error/ErrorDetection). Boxing is explicitly not
built this phase.

## 1. Audit (before writing any code)

Read `Docs/C5_GAME_FRAMEWORK.md`, `Docs/C6_GAME_REGISTRY_CONTENT_PIPELINE.md`,
`Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md`, `Docs/C8_CLASICO_DESIGN_LOCK.md`,
plus `git status` (clean except an unrelated `ProjectSettings.asset` diff
that predates this phase and was not touched — see "Security/scope" below)
and every file the brief named. Findings:

**A. Survives untouched**: `IGameEngine` (Begin/Tick/IsFinished/
BuildResult/Cleanup — the director implements this exactly like the old
engine did); `GameFlowController` (Idle/Preparing/Playing/Ending/Results —
zero new states, zero Clasico-specific branches, confirmed by grep); `Game
Session`/`GameResult` (Score/Round/Correct/Incorrect/Streak/BestStreak —
every new Results metric is additive, none replaced); `GameRegistry`/
`GameCatalog`/`GameHub`/`IGamePresenterHost` (Clasico is still exactly one
registry entry); `HermitTheme`/`RuntimeUIFactory` (one small justified
addition, see below — everything else read as-is); `ShellInstaller`/
`GameSessionInstaller` (zero changes needed — `ClasicoGameHost`'s
constructor signature never changed); `Hermit.Networking` (zero references
anywhere in `Hermit.Games`/`Hermit.Runtime.GameFramework`, confirmed by
grep, before and after); `Hermit.Games.Content` (`QuestionSet`/
`QuestionDefinition`/`ContentValidator`/`IContentProvider` — kept, unused
by Clasico now, not deleted; see "Known limitations").

**B. Refactored**: `ClasicoGameEngine` — replaced outright by
`ClasicoSessionDirector` (see "Migration strategy"); `ClasicoGameHost`/
`ClasicoHud` — split into a thin outer "Hermit chrome" host (score, streak,
timer, command text, feedback, results, transitions) plus four new,
separately-built presenters, exactly the split C8.0's Design Lock's
"Framework impact" section (§AD) predicted.

**C. Not touched**: `Hermit.Networking` (confirmed — no diff, no new
reference); `GameRegistry`'s enumeration contract; `GameDefinition`'s outer
shape; `HermitTheme`'s token set (only a new *overload* was added to
`RuntimeUIFactory`, no existing signature changed); the Shell/Bootstrap
scene flow; `02_GameplaySandbox`.

## 2. Architectural rule (confirmed, not just asserted)

`grep -rn "Clasico" Assets/Hermit/Games/GameFlowController.cs` returns
nothing — the outer framework has zero knowledge of microgames, exactly as
required. From outside, Clásico is still one `IGameEngine`. Inside:

```
Clasico (one registry entry)
  -> ClasicoSessionDirector (one IGameEngine instance)
       -> hosts one microgame archetype engine at a time
       -> Western | Game Show | Balance | Detective (presentation only, Hermit.Runtime)
  -> Results
```

## 3. Migration strategy

Evaluated the brief's three options:

- **(A) Replace in place** — delete `ClasicoGameEngine`, put
  `ClasicoSessionDirector` in its slot.
- **(B) Temporary adapter** — keep both, `ClasicoGameDefinition.CreateEngine()`
  picks one.
- **(C) Gradual rename/refactor** — evolve `ClasicoGameEngine` in place,
  incrementally.

**Chosen: (A), replace in place.** `ClasicoGameDefinition` can only ever
return one engine from `CreateEngine()` — nothing in this codebase can ever
call the old Q&A engine once the director is wired in, so keeping it around
(B) would be dead code with a maintenance cost and zero real consumer, and
(C) would mean evolving a class whose *entire phase model* (Countdown →
AwaitingAnswer → Revealing, one question shape) doesn't fit a
heterogeneous-microgame session at all — there's no meaningful "gradual"
path from one fixed rhythm to hosting four different ones.
`ClasicoGameEngine.cs`, `ClasicoQuestionView.cs`, and the old
`ClasicoGameEngineTests.cs` were deleted (with their `.meta` files);
`ClasicoScoring.cs` and its tests were kept as-is — the base+speed+streak
formulas are archetype-agnostic and are reused unchanged by the director.

**Risk this posed, and how it was covered**: deleting the old engine meant
losing its entire EditMode test suite in the same commit. `ClasicoSessionDirectorTests.cs`
(14 tests) and `ClasicoMicrogameLibraryTests.cs` (3 tests, one sweeping 1,200
cases across 6 counts × 200 seeds) were written to cover the same
lifecycle guarantees the old suite had (begin/tick/submit/finish/result)
plus the new sequencing guarantees, before the old file was removed for good.

## 4. SessionDirector

`ClasicoSessionDirector` (`Hermit.Games.Clasico`, public — same visibility
the old `ClasicoGameEngine` had, so PlayMode tests can read it the same way
they used to) implements `IGameEngine` directly. It owns: the microgame
sequence (drawn once, in `Begin`), the current microgame index, the overall
`GameSession` (score/correct/incorrect/streak — unchanged fields), which
archetype is active, the current typed challenge, and the phase rhythm:

```
Countdown (session-level, optional, reuses the C7 field/UX unchanged)
  -> Intro (command beat, non-interactive)
  -> Decision (the archetype engine is live)
  -> Lock (tiny freeze, no new visuals)
  -> Feedback (reveal)
  -> next microgame's Intro, or finished
```

C8.1 ships: 9 microgames (`ClasicoGameDefinition.MicrogameCount`, inside the
brief's 8-10 range), a single tier (no speed escalation), no Integrity
Meter, no Heat, no full Session Arc — exactly the brief's explicit scope cut.

## 5. Microgame contracts

Only what four concrete microgames actually needed, per the brief's own
rule ("no framework para 50 juegos imaginarios"):

- `IMicrogameEngine` (internal) — `Tick`, `IsResolved`, `IsCorrect`,
  `DecisionFraction01`. Two concrete implementations only:
  `SelectionMicrogameEngine` (one correct index among N — backs
  AimSelect/ChooseSide/DetectError, see §7) and `BalanceMicrogameEngine`
  (nudge-to-target). No `IMicrogameChallenge`/`MicrogameContext` types were
  created — every challenge type is plain, typed data
  (`ClassificationChallenge` etc.), and the only context an archetype
  engine needs (a decision-window length) is passed straight into its
  constructor. No `MicrogameDefinition` type either — for exactly four
  fixed Gold microgames, `MicrogameArchetype` (the enum) plus a switch in
  `ClasicoSessionDirector.AdvanceToNextMicrogame` *is* the definition; a
  registry-of-definitions abstraction would have had exactly one real
  caller and zero variability to justify it this phase.
- `MicrogameArchetype` (public enum) — `AimSelect`, `ChooseSide`,
  `Balance`, `DetectError`.

**A real finding worth reporting** (per the brief's "if a Gold microgame
proves the architecture wrong, report the evidence"): building all four
showed that **three of the four archetypes — AimSelect, ChooseSide,
DetectError — reduce to the exact same resolution shape at the data level**
("pick 1 of N, compare to a correct index"). Only Balance needed a
genuinely different engine. This isn't a shortcut around the grammar — the
Design Lock's own archetypes are a *mechanical/presentational* taxonomy
(how it feels to play), and three very different-feeling presenters
(aiming at outlaws, a two-buzzer game show, pointing in a lineup)
legitimately share one small resolver underneath. `SelectionMicrogameEngine`
being reused three ways is the cheap, honest read of the grammar, not a
workaround.

## 6. Challenge model

Four plain public data classes in `Hermit.Games.Clasico.Microgames`:
`ClassificationChallenge`, `TrueFalseChallenge`, `EquationChallenge`,
`ErrorDetectionChallenge` — each carries a stable `Id`, a `ContentVersion`,
a minimal `Difficulty` string, and its own type-specific fields (see each
class's doc-comment). None of the ten Challenge Types beyond these four
were implemented.

## 7. Compatibility model

Fixed, explicit, no reflection, no string maps: the compatibility is simply
which `IMicrogameEngine`/presenter `ClasicoSessionDirector.AdvanceToNextMicrogame`
and `ClasicoGameHost.SwitchPresenter` construct for each `MicrogameArchetype`
case —

```
Classification -> AimSelect     (Western Shootout)
TrueFalse      -> ChooseSide    (TV Game Show)
Equation       -> Balance       (Balance Machine)
ErrorDetection -> DetectError   (Detective Lineup)
```

Extending this later (per the design lock's "compatibility, not full free
mixing") means adding one more `case` in each of those two switches — both
already exist and are small; no new indirection layer was built ahead of a
second real need.

## 8. Separation model (verified, not just designed)

`ClasicoMicrogameLibrary`'s challenge data (`Hermit.Games.Clasico.Microgames`)
has zero references to `UnityEngine.UI`, `RuntimeUIFactory`, or any
presenter type — confirmed by inspection, and structurally impossible to
violate since that assembly boundary (`Hermit.Games`) has never referenced
`Hermit.Runtime`. Each presenter (`Hermit.Runtime.GameFramework.Microgames`)
only ever reads the plain strings/floats/bools on its matching challenge
type — none contains an `if` on an account name or category string; a
`WesternShootoutPresenter` given a `ClassificationChallenge` about
"Depreciación acumulada" instead of "Cuentas por cobrar" needs no changes
to render it correctly.

## 9. Western Shootout

Aim-and-shoot classification. Background: a dusty two-tone Wild West panel
with a horizon band. Up to 4 outlaw "targets" (a rounded rect with a dark
circular hat above it, distinguishing them from a plain button) each
labeled with a category from `ClassificationChallenge.CategoryOptions`.
The concept (e.g. "Cuentas por cobrar") is shown large above them. A
reticle graphic snaps to whichever target is currently selected —
identical behavior for mouse (clicking a target selects and fires it) and
keyboard (arrow-navigate, Enter fires), so both input paths look and feel
the same, not two different implementations. Correct: the correct target
tints `Theme.Correct`. Incorrect: the wrong target tints `Theme.Incorrect`
and the correct one is revealed. Command: **¡DISPARA!**.

## 10. TV Game Show

Choose-side true/false. A studio backdrop, a contestant "head" (a circle
with a simple two-state ASCII face, `° °` / `^ ^` / `T_T`), a prize board,
two big buzzers (VERDADERO/FALSO). Understandable at a glance — two giant
labeled buttons and a statement, nothing else competing for attention.
Correct: the contestant's face turns happy, the true answer's button glows
green. Incorrect: the wrong button (if one was pressed) glows red, the face
turns sad, the correct button still glows green. Command: **¡DECIDE!**.
The cheapest of the four to build (see §14), deliberately — it's the
scalability floor the content pipeline can lean on.

## 11. Balance Machine

The technically riskiest Gold pick, exactly as the design lock flagged.
A tilting beam on a post, two pans (ACTIVO on the left, PASIVO + PATRIMONIO
on the right), a live numeric readout of the adjustable Patrimonio value,
Up/Down/Confirm controls. The beam's tilt is a **normalized-angle rotation
driven by the numeric imbalance**, not a physics simulation — "claridad >
simulación física," per the brief. `EquationChallenge.StartValue` is
constructed to always differ from `CorrectValue` by a nonzero multiple of
`StepSize`, so confirming without nudging is *always* wrong, by
construction, never by accidental tuning (and is exercised by a dedicated
test, see §21). Correct: beam levels, values turn green. Incorrect: the
final tilt is shown, the correct value is appended to the equation text.
Command: **¡BALANCEA!**.

## 12. Detective Lineup

Point out the one item that doesn't belong. A row of up to 4 "suspects"
(a rounded card with a circular "face"), a group label (e.g. "Cuentas de
Activo"), and — specifically because the C8.0 Design Lock flagged this
archetype's ≤1s clarity as *unverified*, unlike the other three — an
explicit **"¿Cuál no pertenece?"** cue in warning-colored text above the
group label, plus a spotlight overlay that follows the current selection
(same cheap transform-follow trick as Western's reticle). Correct: the true
anomaly (`ErrorDetectionChallenge.AnomalyIndex`) tints green. Incorrect: the
wrongly-accused suspect tints red, the real anomaly is revealed. Command:
**¡ENCUÉNTRALO!**. Whether the added cue is *enough* to hit the ≤1s rule is
explicitly not something this phase can confirm without a human watching a
first-time player — flagged again in "Risks" below, unchanged from the
design lock's own flag.

## 13. Visual fidelity

Every world uses procedural shapes only (no external asset hunt, per the
brief) but each is visually distinct in silhouette, palette, and layout —
not four relabeled button grids. Concretely: Western's outlaws wear a dark
"hat" shape none of the other three presenters use; Game Show's contestant
head + prize board + two giant buzzers reads immediately as a studio, not a
quiz; Balance's tilting beam is the only presenter with a physically
animated prop; Detective's spotlight + warning-colored cue is the only
presenter with an explicit textual hint layered over its visuals. All four
reuse `RuntimeUIFactory.CreateButton`/`CreateText`/`CreateRoundedPanel` for
their interactive hit-targets (one new overload — `CreateRoundedPanel`
taking an explicit corner radius — was added so all four could build
circular "character" dressing without four copies of sprite-generation
code) but dress those primitives into genuinely different scenes.

## 14. Character prototypes

One per microgame, no rigs, per the brief: Western's outlaws (hat + vest
colored per state), Game Show's contestant (a circle head with a
two-glyph face that swaps on outcome) and presenter framing (prize board),
Detective's suspects (rounded "face" card), and Balance intentionally has
no character (the brief allowed this — "operator/engineer optional" — the
scale itself is the star of that scene).

## 15. Shared Hermit chrome

`ClasicoHud` is now purely the outer chrome: progress ("Microjuego X/Y"),
score, streak, the decision-window timer bar, the big command word (bound
to archetype via `MicrogameVocabulary`, never to a specific world), the
correct/incorrect feedback line and its color law
(`Theme.Correct`/`Theme.Incorrect`, read once, applied identically
regardless of which presenter is active), the pre-run countdown, the
results panel, and the transition overlay. It has never heard of Western
Shootout or any other world — `ClasicoGameHost` is the only thing that
knows both `ClasicoHud` and the four presenters exist.

## 16. Transitions

One shared radial wipe (`ClasicoHud.PlayTransitionCut`) — a full-screen
`Image` with `Image.FillMethod.Radial360`, animated 0→1→0 over
`Theme.TransitionDuration` (~0.22s), tinted `Theme.Background`. Triggered
once per microgame-index change, connecting every world cut with the same
motion regardless of how different the two worlds look. No transition
framework — one coroutine, no-op if already playing (same defensive
pattern as punch/shake, see §20).

## 17. Scoring

Base (`ClasicoScoring.ComputeQuestionScore`, unchanged) + Speed bonus only,
exactly per the brief's C8.1 scope. Streak is kept technically (the field,
the bonus formula, `BestStreak`) for compatibility with `GameResult`/
`GameSession`, but is not the focus and has no new UI beyond the existing
streak line. No Integrity, no Heat, no Perfect Clear, no final grade.

## 18. Results

Deliberately barebones: Score, Accuracy%, Microjuegos superados
(Correct+Incorrect — the run always resolves every microgame it starts,
never skips one), Retry, Volver. No Best Streak/domain-strength text this
phase (the field still exists on `GameResult` for later use, just not
surfaced yet) — matches "no construir todavía full C8.0 Results."

## 19. Content

In-code sample pools (`ClasicoMicrogameLibrary`), not a ScriptableObject
asset — a deliberate simplification: hand-authoring a new Unity-serialized
asset type's YAML by hand for four content pools this small would have
added real risk for zero benefit at prototype fidelity, and the brief
explicitly said not to build content-migration infrastructure this phase.
8 Classification, 8 TrueFalse, 6 Equation, 6 ErrorDetection challenges —
inside the brief's 5-10 per pool target, enough for each Gold microgame to
appear 2-3 times in a 9-microgame run without repeating.

## 20. Content validation

`MicrogameContentValidator` (mirrors `Hermit.Games.Content.ContentValidator`'s
plain-static-function shape) checks exactly what the brief asked: stable,
non-empty ids; `ContentVersion >= 1`; unique options with no duplicates;
`ClassificationChallenge.CorrectCategory` appears in `CategoryOptions`
exactly once; `EquationChallenge.StartValue` is reachable from
`CorrectValue` in whole `StepSize` increments and never already equal to
it; `ErrorDetectionChallenge.AnomalyIndex` is in range. 22 tests cover both
malformed-content rejection and a standing regression that every shipped
pool entry validates clean (`MicrogameContentValidatorTests`).

## 21. Input

Every archetype has a real, tested non-mouse-exclusive path, per the
brief's hard rule: Western/Detective use the existing keyboard-navigation
chain (`RuntimeUIFactory.ChainHorizontal`) plus Enter-to-confirm — no
mouse-only aiming is required, a target can be reached and fired purely by
arrow keys; Game Show is a two-button horizontal chain; Balance's Up/Down/
Confirm are three chained buttons (arrow-navigate between them, Enter
activates whichever is selected) rather than raw keycode polling — this
project has never polled `Keyboard.current` directly anywhere, and this
phase does not start now; every archetype's mouse path is a plain
`Button.onClick`, identical in mechanism to every other button in this
project.

## 22. Presenter / engine architecture

No 1000-line `ClasicoHud` with per-microgame branches: `ClasicoHud` (241
lines) only knows chrome; each of the four presenters
(`WesternShootoutPresenter`, `GameShowPresenter`, `BalanceMachinePresenter`,
`DetectiveLineupPresenter`) is its own file, builds its own GameObjects
once, and exposes a small typed surface (`ShowChallenge`, `RevealOutcome`,
optionally `RenderDecision`/`RenderLiveValue`). `ClasicoGameHost` is the
one place a `switch (archetype)` exists, and it is **presentation routing**
("which presenter is this"), never game logic — the actual correctness
rules live in `SelectionMicrogameEngine`/`BalanceMicrogameEngine`, which
have no `switch` at all. `ClasicoSessionDirector` "solo orquesta" — it owns
phase timing and score bookkeeping, and delegates every archetype-specific
decision to whichever `IMicrogameEngine` is currently active.

## 23. Frame-reset bugs — lessons from C5-C7, applied

- No per-frame re-render: `ClasicoGameHost.RenderFrame` guards Intro/
  Feedback rendering with `_introVisualsActive`/`_feedbackRendered` flags
  reset exactly once per microgame-index change — the direct architectural
  descendant of the C7 `_revealRendered` fix.
- No coroutine restarted mid-flight: `ClasicoHud.StartPunch`/`StartShake`/
  `PlayTransitionCut` are all still "no-op if already playing," the exact
  C7 fix, now applied to `StageRoot` instead of a single question card.
- No stale selection: every presenter's `ShowChallenge` re-selects a fresh
  first control (`RuntimeUIFactory.Select`), and hidden presenters'
  buttons are deactivated (`SetActive(false)`), so a keyboard selection can
  never land on an invisible control from a different world.
- Transitions always clean up: `PlayTransitionCut`'s coroutine sets
  `_transitionRoutine = null` on completion regardless of how the run
  proceeds around it.

## 24. Feedback

Global: `Theme.Correct`/`Theme.Incorrect` applied identically by every
presenter's `RevealOutcome`, plus the shared punch (correct) / shake
(incorrect) on `StageRoot`. World-specific: Western's target flash,
Game Show's contestant face swap, Balance's beam settling/tilting further,
Detective's spotlight removal + suspect reveal.

## 25. Audio

Not implemented this phase, as explicitly allowed ("si audio complica el
slice: defer and document"). No SFX, no tones — C8.1 is silent. Deferred to
C8.2+ alongside the Design Lock's full audio direction (§U/§V).

## 26. Shell integration

Zero changes to `ShellInstaller.cs` or `GameSessionInstaller.cs` — Clásico
is still exactly one `GameRegistry` entry, still wired via one
`RegisterPresenter("clasico", new ClasicoGameHost(clasicoHud))` call.
`02_GameplaySandbox` is unaffected and still reachable directly for
dev/test, no per-microgame scene was created.

## 27. Tests

**EditMode** (`Hermit.Tests.EditMode`, 103 tests, all green — see
"Automated verification"):
- `ClasicoSessionDirectorTests` (14) — begin/countdown, sequencing contains
  all four archetypes, no adjacent archetype repeats, correct/incorrect for
  a selection archetype, a resolved microgame can't be double-resolved,
  Equation confirm-without-nudging is always wrong, timeout on a selection
  archetype auto-resolves incorrect, finishing, `BuildResult`, streak bonus
  still applies across different archetypes, an empty run finishes
  immediately.
- `ClasicoMicrogameLibraryTests` (3) — `BuildSequence` never produces an
  adjacent duplicate across 6 counts × 200 seeds (1,200 cases), always
  contains every archetype at least twice for count ≥ 8, always returns
  exactly the requested count.
- `MicrogameContentValidatorTests` (22) — well-formed content passes for
  all four types; each type's specific malformed cases are rejected
  (missing id, duplicate options, correct-answer-not-in-options,
  unreachable/already-equal StartValue, duplicate items, out-of-range
  anomaly index); every shipped pool entry validates clean.
- `ClasicoScoringTests`/`GameResultTests` — unchanged, still green (neither
  file needed a single edit).

**PlayMode** (`Hermit.Tests.PlayMode`, 29 tests, all green): the full
`ClasicoPlayModeTests` was rewritten around `ClasicoSessionDirector` (public,
same as the deleted engine) instead of a fixed "Question"/"Option0" screen —
countdown then a microgame, every archetype renders distinct interactable
controls when it appears (looped across a real 9-microgame session), at
least 3 archetype switches occur within one session, **the exact named
regression this phase called for**: an incorrect answer transitions cleanly
into a *different* microgame type (asserted via the same
`StageRoot`-on-screen check C7's shake-bug regression used, generalized
from the old single question card), completing a session reaches Results,
Restart lands back in a fresh countdown, Abort reaches Results incomplete,
Exit returns to the selector and Clásico relaunches. `ShellPlayModeTests`'
two Clasico-touching tests were updated to drive the new generic
"answer whatever's showing" helper instead of assuming a fixed screen;
every other Shell/selector/Bootstrap test needed no changes.

## 28. Performance

Every presenter's GameObjects are built exactly once, in
`ClasicoGameHost`'s constructor (called once, at Shell/Sandbox composition
time) — never per microgame, per the brief. Switching microgames is
`SetActive` toggling, not `Instantiate`/`Destroy`. No `Resources.Load` per
microgame — content is plain in-memory data. The only new `Update`-adjacent
work is the reticle/spotlight transform-follow (one `RectTransform.position`
write per frame while active) and Balance's live-value text/rotation write —
both cheap, neither allocates.

## 29. Automated verification (this session)

Unity 6000.3.23f1 (matching the project's own version) was run from this
session via its CLI batch mode — real Editor/Player executions, not static
analysis:

| Check | Command | Result |
|---|---|---|
| Compile | `-batchmode -nographics -quit` | Clean, 0 errors, 0 warnings |
| EditMode tests | `-runTests -testPlatform EditMode` | **103/103 passed** (run twice, stable) |
| PlayMode tests | `-runTests -testPlatform PlayMode` | **29/29 passed** |
| Windows standalone build | `BuildPipeline.BuildPlayer` (00/01/02 scenes, Win64) | **Succeeded** — produced a working `Hermit.exe` |
| Standalone smoke test | Launched the built exe headlessly for ~10s | No exceptions/errors in the player log; ran without crashing until force-stopped |

A real bug was caught by this process, not shipped and only found later:
`ClasicoMicrogameLibrary`'s first archetype-sequencing algorithm
(patch-a-bad-shuffle-in-place) could provably cycle back to its own
starting state when three copies of one archetype clustered together — a
test (`Sequence_NeverRepeatsTheSameArchetypeAdjacently`, then isolated
further by `ClasicoMicrogameLibraryTests`) caught the exact failing case
(`Balance,DetectError,Balance,DetectError,ChooseSide,AimSelect,AimSelect,
AimSelect,ChooseSide`). Rather than special-casing that one shape, the
whole approach was replaced with rejection sampling (reshuffle-and-check,
bounded at 500 attempts with a guaranteed-safe round-robin fallback) —
the smallest general fix, not a patch for one seed. See
`ClasicoMicrogameLibrary.BuildSequence`'s doc-comment.

**What automated runs cannot substitute for** — still the user's manual
gate, per this project's standing rule: whether the four worlds actually
*look* distinct and readable, whether the ≤1s clarity rule genuinely holds
for Detective Lineup (§12's open question), real mouse/keyboard *feel*
(not just "a valid selection exists"), F1/C4 debug panel interaction, and
watching the standalone build with actual graphics (this session's smoke
test forced a null graphics device via `-nographics`, so it proves the
scene loads and runs without exceptions, not that it renders correctly).

## 30. Manual validation checklist

Not yet run by a human — do not mark any of these PASS until you do:

1. Open the project in Unity 6.3, confirm no console errors on open/enter Play.
2. Test Runner → EditMode → Run All (expect 103 green — already confirmed via CLI, worth re-confirming inside the Editor).
3. Test Runner → PlayMode → Run All (expect 29 green, same caveat).
4. Open `01_Shell.unity`, press Play — Shell Home appears.
5. Juegos → Clásico — countdown, then the first microgame.
6. Western Shootout with mouse — click a target directly.
7. Western Shootout with keyboard only — arrows move the reticle/selection, Enter fires.
8. Game Show appears at some point in the run — VERDADERO/FALSO both work, mouse and keyboard.
9. Balance Machine appears — Up/Down visibly tilt the beam and move the number, Confirm locks it in.
10. Detective Lineup appears — judge honestly whether "¿Cuál no pertenece?" is enough to understand it in ≤1s on a first look.
11. Answer at least one correctly — global correct feedback (punch, green) plus that world's own correct reaction.
12. Answer at least one incorrectly — global incorrect feedback (shake, red) plus that world's own incorrect reaction, correct answer briefly revealed.
13. Let a decision window expire with no input — same incorrect path as #12.
14. Confirm the transition wipe plays between every microgame, including across very different worlds.
15. Confirm the score updates after every microgame.
16. Finish all 9 microgames — Results shows Puntaje/Precisión/Microjuegos superados.
17. Reintentar — a fresh countdown, score reset to 0.
18. Volver from Results — lands on the selector, Clásico still launchable.
19. F1 — C4 debug panel opens without visually breaking Shell/Clásico underneath; one call (e.g. Login) still succeeds.
20. C4/C7 regression — `02_GameplaySandbox` still works exactly as before; Shell's visual theme is unchanged outside Clásico.
21. Windows Development Build — File → Build Settings → Build (00/01/02 scenes), run the real `.exe` with a real graphics device and repeat steps 4-19 standalone.
22. A full standalone short run start-to-finish, judged purely as "does this feel coherent," per the brief's own success question.

## Known limitations

- `Hermit.Games.Content` (`QuestionSet`/`QuestionDefinition`/
  `ContentValidator`/`IContentProvider`) is now unreferenced by Clásico —
  kept, not deleted, as generic content-pipeline infra a future
  question-shaped game could still use; revisit only if it stays unused
  once a second real game exists (unchanged concern from C6).
- Content lives in code (`ClasicoMicrogameLibrary`), not an
  Inspector-editable asset — fine for four small prototype pools, a real
  authoring pipeline is future work once a non-engineer needs to add
  content.
- Detective Lineup's ≤1s clarity is genuinely unverified — flagged by the
  Design Lock, not resolved by this phase, needs a human's honest first
  look (see checklist #10).
- No touch/gamepad testing (no such hardware in this session) — the
  archetypes were deliberately built on a "select/confirm/navigate" model
  with this in mind (per §AB), unverified empirically.
- Balance's tilt is a simple linear-clamped rotation, not a physically
  simulated scale — a deliberate choice ("claridad > simulación física"),
  not an oversight.
- No audio at all this phase, deliberately deferred.

## Decisions discovered while implementing

- **Three archetypes, one engine**: see §5 — `SelectionMicrogameEngine`
  backs AimSelect/ChooseSide/DetectError; only Balance needed a distinct
  shape. A real, reportable finding about how the grammar actually
  decomposes at the code level, not a shortcut.
- **Sequencing bug and fix**: see §29 — an in-place duplicate-patching
  algorithm was provably capable of cycling back to its own broken state;
  replaced with rejection sampling, the smaller and more general fix.
- **No pre-session Countdown redesign**: C7's existing pre-run countdown
  mechanic (and its UI) was reused unchanged for the whole session's
  intro, rather than inventing a second "session countdown" concept — one
  less thing to build, and the existing countdown tests/UX already proved
  it works.
- **No `MicrogameDefinition`/`MicrogameContext` types**: the brief
  explicitly asked not to build every named contract if the four Gold
  microgames don't demonstrate a need for it — they didn't; `MicrogameArchetype`
  plus two small `switch` statements (presentation routing, not logic)
  fully cover C8.1's needs.
