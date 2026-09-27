# C8.1d — Gold Slice Art Integration

**Status: GOLD VISUAL PASS ACCEPTED / MANUAL AUDIO/LIFECYCLE REGRESSION
FIXES IN PROGRESS.** Western's cinematic, gunshots, countershot, Outlaws,
DISPARA cue, timing, and scoring were confirmed against a real Windows
build (recorded at the C9 checkpoint — see
`Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md`). A later human manual
acceptance pass (C8.1f.2, see `Docs/C8_1F_BALANCE_MECHANIC_REDESIGN.md`)
found two real regressions since then; the first fix attempt there
(`ClasicoHud.ShowCommand` only playing its command clip for a non-empty
command) was confirmed by a second human manual pass to be root-cause-
incomplete on both counts, which is what C8.1d.10 (this pass) actually
fixed:

1. **The "TUM"/duplicate gunshot on a correct shot.** Root cause: the
   dust-accent "impact" cue (`WesternShootoutPresenter.FireSequenceRoutine`)
   used to fire unconditionally on every shot, ~0.12s after the gunshot
   crack — close enough, and itself a soft, heavily low-passed noise-burst
   onset, to read as a second discrete "thud" alongside the gunshot on a
   hit. C8.1d.10 first gated it to misses only; a third human manual pass
   (C8.1d.11) then reported the *incorrect*-answer sequence itself as
   overcrowded ("TUM -> PSS -> TUM"), so the accent was removed from the
   gameplay fire sequence entirely — its own 0.3s tail was still audibly
   ringing (~16% peak amplitude) when the countershot's own enemy gunshot
   fired ~0.18s later, and the countershot alone already fully punctuates a
   miss (a hit's own sprite/tint/punch/dust-puff feedback never needed it
   either). The clip itself is untouched and still used by the cinematic's
   own gunshot beat.
2. **Western activity continuing after SALIR.** Root cause: `Hide()` only
   deactivated the presenter's own root and stopped the music source — the
   entire `CinematicIntroRoutine` chain (~11 fire-and-forget coroutines
   started bare on the persistent `ClasicoHud` host, which outlives the
   presenter's own root being deactivated) kept running regardless,
   including further scheduled `PlayOneShot` calls on an SFX AudioSource
   `Hide()` never touched. Fixed via an explicit `_sequenceGeneration`
   guard checked at every coroutine resumption point plus a centralized
   `CancelActiveWesternSequence()` (stops both AudioSources, clears every
   leftover visual), both routed through `Hide()`.

Final incorrect-answer audio sequence, after C8.1d.11: player gunshot ->
(visual reveal, no audio) -> enemy-gunshot countershot -> silence. Final
correct-answer sequence, unchanged since C8.1d.10: player gunshot only.
Targeted Western PlayMode tests (including ones proving SALIR mid-cinematic
never lets the delayed pre-draw/gunshot fire, re-entry afterward starts a
clean new cinematic, and — new this pass — that no shared/duplicated audio
fires anywhere in an incorrect round's whole reveal/countershot window) were
run across C8.1d.10 and C8.1d.11.

A fourth human manual pass (C8.1d.12) confirmed all of the above (music,
delayed-FX, correct-shot, and incorrect-shot audio) were fixed, and reported
one remaining presentation issue: a ~1 second dead gap where only the
Western background was visible between the cinematic ending and the
outlaws appearing. Root cause: `ClasicoGameDefinition.EncounterIntroSeconds`
(the director's own Intro-phase timer, which alone used to gate the entire
outlaw reveal) included a ~1.0s safety buffer above
`CinematicIntroRoutine`'s own ~6.5s scripted runtime, so the cinematic
visually finished up to a full second before anything else appeared. Fixed
by splitting the old combined reveal into a visual half
(`RevealOutlawVisuals`) — now called directly from `CinematicIntroRoutine`
at the ~6.2s gunshot beat, overlapping the outlaws' entrance with the
flash/cut teardown — and an input-enable half (`EnableOutlawInput`, still
gated only by the real Intro->Decision transition), plus tightening
`EncounterIntroSeconds` from 7.5s to 6.9s (a still-safe ~0.4s buffer). Input
timing, answer logic, countershot behavior, and all Western audio were
untouched. Targeted tests updated/added to prove: outlaws become visible
before the Intro phase ends (no empty-background frame), input never
enables early, and SALIR/re-entry during the new overlap window still
cancels and resets cleanly.

Not yet re-declared regression-free until a human manual pass on this
latest rebuilt Windows executable confirms the C8.1d.12 transition fix with
real eyes, per this project's standing "real human input has final
authority over passing tests" rule — see this session's own report for the
exact test counts/results. Every phase-specific "manual validation
required" note below (C8.1d.1 through C8.1d.9) is preserved as historical
record of what was checked and found at the time — this line records only
the current, overall status.

## C8.1d.1 — Manual Validation Failure — Encounter Not Active in Production Runtime

**Do not erase the fact that the previous implementation passed tests but
failed manual validation. That is useful project history.**

### What manual validation showed

Playing the actual game in the Unity Editor (not the automated test suite)
showed: Western actor art (Sheriff, outlaws) displayed correctly, but the
game went directly into the accounting question with no Encounter intro, no
face-off, no tumbleweed, no sting, and Western did not stay active for 3
consecutive rounds — behavior that "resembles the old Clásico flow," despite
every EditMode/PlayMode test at the time being reported green.

### Investigation — what was actually verified, layer by layer

Every layer between the shipped configuration and the presentation code was
independently, empirically re-verified against the **real production
asset**, not just `CreateInMemory` test fixtures:

1. **Serialized asset** — `Assets/Hermit/Data/Resources/ClasicoGameDefinition.asset`
   is a stale file that has never been resaved through the Editor since
   before C8.1; its YAML has no `_westernEncounterRoundCount`,
   `_encounterIntroSeconds`, etc. keys at all. Loaded directly via
   `AssetDatabase.LoadAssetAtPath`, it correctly resolves every missing
   field to its C# declared default (`WesternEncounterRoundCount=3`,
   `EncounterIntroSeconds=2.2`, etc.) — this is standard, correct Unity
   deserialization behavior, not a bug, and is now a permanent regression
   test (`ProductionClasicoGameDefinition_RequestsAContiguousThreeRoundWesternEncounter`).
2. **Catalog wiring** — `GameCatalog.asset`'s single Clasico entry was
   GUID-verified to reference this exact asset file, and both
   `GameSessionInstaller` (test/sandbox composition root) and
   `ShellInstaller` (`01_Shell`, the real product entry point) resolve it
   through the identical `Resources.Load<GameCatalog>("GameCatalog")` path
   — no divergent test-vs-production wiring exists.
3. **Run-plan generation** — a new permanent EditMode test,
   `ProductionClasicoGameDefinition_GeneratedRunPlan_HasAContiguousThreeRoundWesternEncounter_WithCorrectStartMiddleEndMarkers`,
   loads the real asset, drives `ClasicoSessionDirector` through a full
   9-round session across 20 random seeds, and asserts the generated plan
   always contains exactly 3 contiguous `AimSelect` rounds with
   `CurrentRoundWithinEncounter` = 0 (start), 1 (continuation), 2 (end).
   **Passes.**
4. **Host/presenter runtime behavior** — the PlayMode test that most
   directly targets the user's reported symptom,
   `WesternEncounter_WorldStaysActive_AndIntroFlourishFiresOnlyOnRound1`,
   initially **failed** with exactly the reported symptom ("The tumbleweed
   must become active at some point during round 1's Encounter intro.
   Expected: True, But was: False"). This looked like direct confirmation
   of a real product bug.

### Root cause of the test failure: a test-observation artifact, not a product bug

The failing test used the suite's existing `CycleUntilArchetype` helper,
which skips ahead in fixed multi-second increments. If Western was not the
literal first microgame of that session's randomized draw, those coarse
skips could land the test's observation window *after* the tumbleweed's
entire ~1.6s crossing had already finished — the test would then correctly
report "never saw it active" despite the flourish having genuinely played.

The test was rewritten to drive frame-by-frame from the literal moment
`Game_clasico` is clicked, continuously watching `Tumbleweed.activeSelf`
with no skip-ahead, so no window can be missed. Its first execution (bounded
by a `20000`-frame loop) failed fast (~2.6s wall-clock) with "Never reached
round 1 of the Western Encounter" — revealing a second, unrelated test bug:
in this project's batch-mode/`-nographics` test environment, simulated
frames track real wall-clock time roughly 1:1, so a 20000-frame budget only
covers ~2.6 simulated seconds, not even enough to clear the real 3-second
pre-run countdown. Replacing the frame-count bound with a
`Time.realtimeSinceStartup`-based 60-real-second wall-clock deadline fixed
this, and the test now **passes** both in isolation and as part of the full
480s suite run: the tumbleweed fires once on round 1, never replays on
rounds 2/3, the world stays active (no hide/rebuild) across all 3 rounds,
and 3 distinct concepts are shown.

`git diff` on `ClasicoGameHost.cs` was also read in full and confirmed there
is no dual-path or fallback logic — `RenderFrame` routes exclusively through
`ClasicoSessionDirector`, and its "new Encounter vs. continuation round"
branch is exactly what the now-passing test verifies.

**Conclusion: every layer that can be exercised by an automated test —
serialized config, run-plan generation, host routing, presenter intro/
continuation behavior — is proven correct against the real production
configuration.** No code or config change was required or made to
`ClasicoGameHost.cs`, `WesternShootoutPresenter.cs`, `ClasicoSessionDirector.cs`,
or `ClasicoGameDefinition.asset` as a result of this investigation. The only
change was to the test itself (`ClasicoPlayModeTests.cs`), which had a false
negative.

### Reconciling this with what you actually saw

This is a real, acknowledged contradiction, not something to wave away: the
code is now proven correct, yet your manual session showed the old flow. The
most evidence-consistent explanation is **Unity Editor domain-reload
staleness**, not a logic bug:

- `git status` shows `Assets/Hermit/Games/Clasico/ClasicoGameEngine.cs` and
  `ClasicoQuestionView.cs` (the old C5-C7 flow) as **uncommitted deletions**,
  and `ClasicoSessionDirector.cs` plus the four Gold microgame presenter
  files as **uncommitted new/untracked files**.
- If the Unity Editor was already open when these changes landed on disk —
  or Auto Refresh was disabled, or Play was pressed before a triggered
  recompile finished — the Editor would keep running its **previously
  compiled assembly**, which still contains the old `ClasicoGameEngine`-
  driven flow, while newly-imported **art assets** (which don't require a
  script recompile to show up) would still render correctly.
- This matches your report precisely: correct new art, old game flow.

This cannot be confirmed after the fact from outside your Editor session,
so it is reported as the leading hypothesis backed by the available
evidence, not a certainty. See "Exact manual reproduction steps" below for
how to rule it out on the next attempt.

### Files changed this investigation

- `Assets/Hermit/Tests/EditMode/ClasicoSessionDirectorTests.cs` — added the
  two production-config regression tests described above (permanent, not
  removed after use).
- `Assets/Hermit/Tests/PlayMode/ClasicoPlayModeTests.cs` — rewrote
  `WesternEncounter_WorldStaysActive_AndIntroFlourishFiresOnlyOnRound1` to
  drive frame-by-frame from session start with a wall-clock deadline instead
  of `CycleUntilArchetype` skip-ahead, fixing a false negative in the test
  itself. No production code file was changed.

### Test results (this investigation)

- **EditMode: 110/110 passed**, including the two new production-config
  tests.
- **PlayMode: 43/43 passed** (479.8s), including
  `WesternEncounter_WorldStaysActive_AndIntroFlourishFiresOnlyOnRound1` and
  `WesternEncounter_EndsAfterThreeRounds_NextMicrogameIsADifferentArchetype`.

### Exact manual reproduction steps (redo required)

1. **Close and reopen the Unity project** (or explicitly force
   Assets → Refresh and confirm the Console shows a fresh, error-free
   compile) before pressing Play — this rules out the stale-assembly
   hypothesis above. Do not reuse an Editor session that was already open
   while these files were changing on disk.
2. Enter Play Mode from `01_Shell`, launch Clásico, and play until Western
   appears.
3. Confirm: a ~2-3s intro plays once (background already visible, sting,
   tumbleweed crossing, then gameplay) — not a jump straight to the
   accounting question.
4. Play all 3 rounds back to back: confirm the world never fades/hides
   between rounds, the intro does not replay on rounds 2/3, and each round
   shows a different accounting concept.
5. After round 3, confirm a brief settle plays, then the normal transition
   into the next world — Western must not reappear later in the same
   session.
6. Report back — this phase stays IMPLEMENTATION IN PROGRESS — MANUAL
   VISUAL VALIDATION REQUIRED until this pass comes back clean. If it fails
   *again* even with a guaranteed-fresh compile, that would point to a real
   runtime bug this investigation did not surface, and warrants reopening
   this investigation rather than repeating the same fix.

## C8.1d.2 — Western Intro Runtime Visibility Failure

### Manual validation update

3 consecutive Western rounds now play correctly in the real `01_Shell`
runtime — the C8.1d.1 Encounter grouping fix holds, and the stale-Editor
hypothesis is no longer the operative issue. But the face-off intro itself
is still not visible/audible: the first Western round appears directly in
normal gameplay state, concept already shown, no face-off, no tumbleweed,
no perceived sting.

### Root cause

Found by reading `WesternShootoutPresenter.ShowChallenge` line by line —
**this was a real product bug, not a test artifact.** `ShowChallenge`
unconditionally set the concept text, activated every target label, set
every target's `interactable = true`, and called
`RuntimeUIFactory.Select(first)` — all synchronously, regardless of
`isEncounterStart` — and only *afterward* kicked off
`PlayEncounterSting()`/`TumbleweedRoutine()` as fire-and-forget coroutines.
The round was already fully playable (concept visible, targets selectable)
on the exact same frame the sting/tumbleweed launched. `ClasicoGameHost`'s
`IsIntroPhase` branch does hold Decision back for `EncounterIntroSeconds`
(2.2s) as designed, but the only thing it adds on top of an already-live
Western world is a small floating `CommandText` label
(`_hud.ShowCommand(...)`) — not a visual/input gate. So for the full 2.2s
"intro," the player was looking at a normal, answerable Western round with
a word floating above it, while the sting/tumbleweed played somewhere
behind that already-resolved scene — exactly matching "the first Western
round appears directly in normal gameplay state."

This is the exact failure mode the C8.1d.2 brief predicted in section 2:
intro launched fire-and-forget, concept text enabled before intro
completes, presenter state not actually gated.

### Intro gating fix

`WesternShootoutPresenter.ShowChallenge` now branches on
`isEncounterStart`:

- **Round 1 (`isEncounterStart: true`)**: the world/actors are staged and
  reset (background, Sheriff → Idle, every outlaw → Neutral — unconditional
  either way) but the round is deliberately **not** revealed — concept text
  is cleared, every target label is cleared, every target's `interactable`
  is set `false`, and the reticle is deactivated. Only then does the sting
  play and `TumbleweedRoutine` start.
- **Continuation rounds (`isEncounterStart: false`)**: unchanged —
  `RevealRoundContent` (the extracted second half of the old `ShowChallenge`)
  runs immediately, exactly as before.

A new public method, `WesternShootoutPresenter.RevealAfterIntro()`, calls
that same `RevealRoundContent` — this is the actual "GO": concept text set,
every target's label set and `interactable = true`, reticle reactivated,
first target selected via `RuntimeUIFactory.Select`.

`ClasicoGameHost.RenderFrame`'s existing Intro→Decision transition (the
`if (_introVisualsActive) { _introVisualsActive = false; _hud.HideCommand();
}` block — already the single place per round where "intro just ended" is
known) now also calls `_western.RevealAfterIntro()` when
`_activeArchetype == MicrogameArchetype.AimSelect`. This is the smallest
local Clásico hook available: no new phase, no `GameFlowController` change,
no change to run-plan generation, `WesternEncounterRoundCount`, scoring, or
the 3-round continuation logic — those were all left untouched per the
brief's explicit constraints. For a continuation round this call is a
harmless idempotent re-application (content was already revealed
synchronously in `ShowChallenge`), not a second reveal or a replay.

### Face-off implementation

Satisfied by the gating fix itself, not a separate staging pass: because
the world/actor reset (background visible, Sheriff lower-left in Idle, all
four outlaws visible in Neutral) already ran unconditionally at the top of
`ShowChallenge`, staging round 1's face-off required no new visual
construction — only *withholding* the round-live elements (concept, labels,
reticle, interactable targets) until `RevealAfterIntro`. Buttons stay
technically clickable-if-you-could-see-them during this window is avoided
because `interactable = false` also suppresses the Selectable's own hover/
press visuals, and `SubmitSelection` already no-ops outside `Phase.Decision`
regardless (pre-existing, untouched). The optional settle-in motion
(section 4's "Sheriff eases in from left / outlaws from right") was **not**
added — out of scope per the brief's own "optional," and the fix above
already resolves the actual reported defect without it.

### Tumbleweed visibility fix

Inspected hierarchy/anchor/sibling-order/color per the brief's checklist:
the tumbleweed was correctly *un*-occluded (later sibling than
Background/TargetStage/SheriffActor, so always drawn in front of them) and
not behind a hidden transition — the defect was pure legibility. It was a
54×54-unit, 5-twig fan in a muted brown (0.42, 0.30, 0.14) sitting on a
1280-wide reference canvas — under 5% of screen width, in a hue close to
the ground/background browns around it. Fixed by enlarging the container to
100×100, using 7 uneven-length twigs (66-90 units) instead of 5 uniform
ones for a more tangled silhouette, adding a filled `Core` panel so it
reads as one mass rather than a thin spinning fan, adding a small
`GroundShadow` ellipse (the same pattern already used for every other
actor) so it reads as rolling on the ground rather than floating, and
lightening the twig color (0.60, 0.45, 0.22) for contrast against the dark
`TargetStage` band it crosses in front of. Raised its resting/crossing
height from y=100 to y=130 to sit more clearly at the characters' foot
level. Crossing path (off-screen left to off-screen right, ~1.6s, rotating)
is unchanged — it was already correctly timed, just too small to read.

### Audio sting verification

Verified by direct inspection, not just assumption: `BuildAudio` adds a
real `AudioSource` (`playOnAwake = false`, `spatialBlend = 0`, default
volume 1, never muted, never destroyed) to the shared host GameObject;
`ProceduralAudio.Tone/Sweep/Noise` produce non-null clips with real
sample data; `PlayEncounterSting`/`PlaySequencedStingRoutine` call
`PlayOneShot` on that source. `01_Shell.unity` (and every other scene) has
exactly one `AudioListener`, confirmed by direct scene-file inspection — so
production audio playback has a real listener to be heard by. (The "no
audio listener in the scene" warnings seen in this investigation's own
batch-mode test logs come from test/sandbox scene composition, not the
shipped `01_Shell` scene, and are unrelated to this fix.) No code change
was made here — no defect was found in the audio pipeline itself; the
"no perceived sting" report is attributed to the same root cause as the
rest of this section: the round was already fully resolved-looking by the
time the sting played, so it read as background noise rather than a
meaningful beat.

### Gameplay reveal behavior

Confirmed by the new regression test (below): the instant Decision begins
for round 1, the concept is visible and at least one target is
interactable — `RevealAfterIntro` and `IsDecisionPhase` fire in the same
host `RenderFrame` call, so there is no extra frame of "still hidden."

### Round 2/3 behavior confirmation

Unchanged and reconfirmed: `WesternEncounter_WorldStaysActive_AndIntroFlourishFiresOnlyOnRound1`
(from C8.1d.1, still passing) already asserts the tumbleweed never
reactivates on a continuation round and the world never hides between
rounds. This phase added no replay risk — continuation rounds still call
`ShowChallenge(isEncounterStart: false)`, which never touches the sting/
tumbleweed code path at all.

### Files changed

- `Assets/Hermit/Runtime/GameFramework/Microgames/WesternShootoutPresenter.cs`
  — `ShowChallenge` split into staged-intro vs. immediate-reveal branches;
  added `RevealAfterIntro()`/`RevealRoundContent(...)`; enlarged/improved
  `BuildTumbleweed`; `TumbleweedRoutine`'s `baseY` updated to match.
- `Assets/Hermit/Runtime/GameFramework/ClasicoGameHost.cs` — one new call
  (`_western.RevealAfterIntro()`) added inside the existing Intro→Decision
  transition, gated to `MicrogameArchetype.AimSelect`.
- `Assets/Hermit/Tests/PlayMode/ClasicoPlayModeTests.cs` — one new
  regression test (below).

### New/updated tests

- **New:** `WesternEncounter_Round1Intro_GatesConceptAndInputUntilIntroCompletes`
  — samples every single frame of round 1's Intro phase (no coarse waits)
  and asserts, continuously: the concept text stays empty and no
  `WesternTarget` is interactable for the entire intro. Then asserts the
  transition into Decision has a real, non-zero measured duration, and that
  the instant Decision begins, the concept is visible and at least one
  target is interactable. This is the state-ordering proof the brief asked
  for in section 9 — "the previous test only proving TumbleweedRoutine
  fired is insufficient" — this one proves gameplay state does not overrun
  the intro, in either direction.
- **Unchanged, reconfirmed:** `WesternEncounter_WorldStaysActive_AndIntroFlourishFiresOnlyOnRound1`
  and `WesternEncounter_EndsAfterThreeRounds_NextMicrogameIsADifferentArchetype`.

Debug-panel exposure (brief section 10, explicitly optional) was **not**
added — the new automated test already gives an unambiguous state-ordering
proof, and adding panel fields for a single already-solved investigation
would be scope creep the brief itself flagged as avoidable.

### Test results

- **EditMode: 110/110 passed** (full suite, clean recompile after clearing
  an unrelated stale Unity build-cache lock — see below).
- **PlayMode: 43/44 passed** in the full-suite run;
  `ClasicoPlayModeTests` itself was **26/26 passed**, including both
  `WesternEncounter_Round1Intro_GatesConceptAndInputUntilIntroCompletes`
  (12.0s) and `WesternEncounter_WorldStaysActive_AndIntroFlourishFiresOnlyOnRound1`
  (8.1s). The one failure,
  `ShellRealSceneCompositionTests.RealShellScene_DecisionTimeoutOnActivePresenter_RestoresLocalReactionTransforms`
  ("GameShow/Mouth did not return to its resting anchoredPosition... 3.52 vs
  expected <1.5"), is a **pre-existing, Game-Show-only, full-suite-timing
  flake unrelated to this fix** — Game Show was not touched this phase, the
  failure has nothing to do with Western/Encounter code, and it **passed
  cleanly in isolation** on immediate re-run, matching the exact
  full-suite-only timing pattern already documented earlier in this file
  ("A second, suite-wide timing fix..."). Not investigated further per the
  brief's explicit "do not touch Game Show" instruction.
- A separate, unrelated infrastructure snag during this investigation: the
  first EditMode retry hung indefinitely on a stale Unity incremental
  build-cache lock (`Library/Bee`) left behind by a race between the user's
  just-closed interactive Editor session and this run's own compile
  daemon — visible as a repeating `bee_backend: error: More than one copy
  of bee_backend running ... PID 18100 waiting` with no further progress
  for several minutes and no such process actually alive. Resolved by
  moving `Library/Bee` aside (a regenerable build cache, not user data) and
  re-running, which then compiled and passed cleanly. Not a code defect.

## C8.1d.3 — Sheriff Intro / Off-Screen Gameplay Model

### Manual validation update

3 consecutive Western rounds and the concept/input gating both now work
correctly (C8.1d.1/.2 confirmed). But the intro still did not read as a real
presentation: the tumbleweed appeared but nothing else moved, Sheriff and
the four outlaws sat static with no face-off, and Sheriff read as too small
next to the outlaws during gameplay. This was explicitly **not** a gating
bug this time — it was a presentation-design problem, and the brief changed
the Western presentation model itself rather than asking for another fix to
the same architecture.

### Why simultaneous Sheriff + outlaws staging was rejected

The C8.1d/.1d.1 model kept Sheriff as a small (140×200), permanently-visible
in-world actor standing beside the four much larger (240×320) outlaws for
the entire round — aiming and firing on-screen every round. Two problems
compounded: (1) at a size that reads clearly as "in the world" next to the
outlaws, Sheriff is necessarily smaller than them, so he can never read as
the protagonist; (2) making him large enough to read as the protagonist
would mean he permanently competes with the outlaws for the same limited
stage width during every round of gameplay, not just the intro. The fix
the brief specified is structural, not cosmetic: split Sheriff into two
different visual roles that never coexist — a large, unmistakable
protagonist during the one-time intro, and no Sheriff at all during
gameplay (the outlaws become gameplay's entire visual field, and the shot
is fired from a fixed off-screen origin instead of an on-screen actor).

### The Sheriff scale problem

Sheriff's old gameplay footprint (140×200 canvas units) was roughly 47% of
the outlaws' own height (320) at the reference resolution — deliberately
undersized to fit beside them without overlapping, but this is exactly what
read as "miniature." The new intro-only Sheriff is sized independently of
the outlaws entirely: 286×380 (`SheriffIntroWidth`/`SheriffIntroHeight`),
roughly 53% of the 720-unit reference viewport height, squarely inside the
brief's 45-60% "protagonist scale" range — because he is never on-screen
next to the outlaws, there is no competing constraint to shrink him for.

### The new intro-only Sheriff role

`WesternShootoutPresenter.IntroRoutine` (round 1 of the Encounter only)
sequences the brief's Phases A-D as one coroutine, timed to land at ~2.15s —
safely inside `ClasicoGameDefinition.EncounterIntroSeconds`'s existing 2.2s
default, so **no Encounter-timing or config change was needed**:

- **A/B (0.00-0.35s)** — Sheriff eases in from off-screen left
  (`SheriffEntranceOffsetX = -260`) with a small scale overshoot (ease-out
  cubic + a sine-based overshoot curve), Idle pose, at the new large
  center-left anchor (`0.18, 0`, up from the old edge-hugging `0.04, 0` —
  large enough now that the old anchor would clip too much of him
  off-screen).
- **C (0.35-1.95s)** — the existing sting (`PlayEncounterSting`) and
  tumbleweed (`TumbleweedRoutine`, unchanged 1.6s) play; partway through,
  Sheriff transitions Idle→Aim (`SetSheriffPose`) — "he settles/aims," per
  the brief, with no fire beat during the intro (the brief's own "do not
  fire yet if it makes the intro confusing").
- **D (1.95-2.15s)** — Sheriff slides back out and fades (a `CanvasGroup`
  added to `SheriffActor` for the alpha fade), then is hidden
  (`SetActive(false)`) for the rest of the Encounter.

Sheriff is now built hidden by default (`BuildSheriffActor` ends with
`SetActive(false)`) and is **only ever activated by `IntroRoutine`** — no
other code path shows him. The outlaws also now stay fully hidden
(`SetActive(false)`, not merely non-interactable as in C8.1d.2) throughout
the intro, per the brief's "outlaws may be hidden."

`RevealAfterIntro()` — the same host-triggered, authoritative gate C8.1d.2
established at the director's real Intro→Decision boundary — is the actual
correctness contract, independent of `IntroRoutine`'s own cosmetic timing:
it force-hides Sheriff (idempotent safety) and reveals the outlaws/concept/
labels/reticle/input, with a small staggered settle-in per outlaw
(`OutlawSettleInRoutine`, ~0.04s stagger × 4, ~0.32s total, well under the
brief's 0.35s budget) on round 1 only. This is the exact C8.1d.2 lesson
applied again: fire-and-forget animation timing must never be the thing
that decides correctness.

### The off-screen gameplay shooting model

Since Sheriff is never on-screen during Decision/Feedback, the shot can no
longer originate from his own transform. `BuildOffscreenShotRig` creates:

- **`OffscreenShotOrigin`** — an invisible anchor on the stage's left edge
  (`0.02, 0`, y=150), stable across all four targets (the brief's
  suggested name, used verbatim).
- **`MuzzleFlash`** — the same `Image` component that used to be a child of
  Sheriff's own `MuzzleAnchor`, now a child of `OffscreenShotOrigin`
  instead — same `LocalMotionFx.FlashColor` usage, just re-parented.
- **`Tracer`** — a new short (140×6), initially-inactive streak. At fire
  time (`FireOffscreenShot`), it's rotated toward the selected outlaw's
  world position and animated (world-space `.position` Lerp, ~0.12s, fading
  out) from the origin to the target, then deactivated — deliberately a
  fixed-length streak rather than one stretched to the exact origin-target
  distance, avoiding fragile anchored-space geometry (rect anchoring
  conventions don't compose simply across differently-anchored siblings)
  in favor of the same proven world-space `.position` technique the reticle
  already uses (`RenderDecision`'s `_reticle.position = selectedRect.position`).
  A small screen kick (`LocalMotionFx.Punch` on the whole `WesternShootout`
  root, 1.015× — deliberately subtle) replaces Sheriff's old recoil punch.

This is what "lets one fixed origin serve all four targets without a
per-target Sheriff aim pose" (brief section 4) — the old `AimSheriffAt`
per-target gun-rotation method is gone entirely (dead code once nothing
calls it), along with `SheriffPose.Fire`'s only caller (kept as an unused
enum value/sprite load in case a future closing pose wants it, per the
brief listing `Sheriff_Fire` among the sprites "to use").

### 3-round continuation behavior

Unchanged in spirit, updated in mechanism: `ShowChallenge(isEncounterStart:
false)` for rounds 2/3 never touches Sheriff at all (no `IntroRoutine` call)
and reveals the outlaws/concept/labels immediately with no settle-in
animation (`animateOutlawEntrance: false`) — exactly the brief's "no
repeated entrance on rounds 2 and 3." A new PlayMode test asserts Sheriff is
never seen active outside round 1 of the Encounter (see "Testing" below).

### Two pre-existing test bugs found and fixed along the way

Running the full suite (not just the new/changed tests in isolation)
surfaced two failures — both investigated properly rather than assumed to
be timing flakes, per this project's own established practice:

1. **A real, previously-latent bug in `ShellRealSceneCompositionTests.cs`**:
   `RealShellScene_LaunchingClasico_VisiblyShowsAGoldMicrogamePresenter_NotTheOldC7Screen`
   waited only `4.5f` seconds after launching Clásico — a value whose own
   comment said "3s countdown + first Intro beat (0.6s)," never updated
   since the C8.1d.1 Western Encounter intro grew to ~2.2s. This test could
   have failed in *any* prior full-suite run whenever RNG happened to draw
   Western as the session's very first microgame (3s + 2.2s = 5.2s needed,
   only 4.5s given) — this run was simply the first time that specific
   chance alignment was hit. Fixed by bumping to `5.5f`, matching
   `ClasicoPlayModeTests.LaunchClasico`'s identical, already-correct value
   for the identical reason. Confirmed fixed by isolated re-run (passed).
2. **The pre-existing Game Show timing flake**, `RealShellScene_DecisionTimeoutOnActivePresenter_RestoresLocalReactionTransforms`
   ("GameShow/Mouth did not return to its resting anchoredPosition...").
   This is unrelated to Western/Sheriff work (Game Show was not touched this
   phase) and was already identified and confirmed as a full-suite-only
   timing interaction during the C8.1d.1/.2 investigations — reproduced with
   byte-identical failure values across three separate full-suite runs now,
   and passes cleanly every time it's run in isolation (confirmed again this
   phase). Not investigated further per the explicit "do not touch Game
   Show" instruction.

### Testing

- **New:** `WesternShootout_SheriffActor_NotVisibleDuringActiveGameplay_OutlawsAre`
  replaces the obsolete `WesternShootout_SheriffPose_IdleDuringIntro_AimDuringDecision_RestoresAfterFire`
  (which asserted the now-deliberately-removed "Sheriff visible/posed during
  Decision" behavior) — proves Sheriff is hidden and the outlaws are visible
  across both Decision and Feedback of round 1, and again on round 2.
- **New:** `WesternShootout_FireSequence_RestoresScreenKickTransform`
  replaces `WesternShootout_FireSequence_RestoresSheriffTransform` (Sheriff
  no longer moves during the fire sequence at all) — proves the new screen
  kick restores the `WesternShootout` root's scale to rest.
- **New:** `WesternShootout_OffscreenShotRig_TracerActivatesOnFire` — polls
  frame-by-frame (not a coarse wait — the tracer's travel is only ~0.12s,
  and the C8.1d.1 tumbleweed investigation already proved a coarse wait can
  miss a window that short) and asserts the Tracer becomes active after
  firing.
- **Updated:** `WesternShootout_ShowsIllustratedBackground_AndInWorldSheriffActor`'s
  `MuzzleAnchor` assertion now checks `OffscreenShotOrigin` (where the
  muzzle flash actually lives now) instead of `SheriffActor`.
- **Extended:** `WesternEncounter_Round1Intro_GatesConceptAndInputUntilIntroCompletes`
  (C8.1d.2) now also asserts, in the same frame-accurate polling loop:
  Sheriff is seen active at intro (≥300 unit) scale at some point during
  the intro, the outlaws stay fully hidden (not just non-interactable)
  throughout it, and Sheriff is hidden again the instant Decision begins.
- **Extended:** `WesternEncounter_WorldStaysActive_AndIntroFlourishFiresOnlyOnRound1`
  (C8.1d.1/.2) now also asserts Sheriff is seen active during round 1,
  fails loudly if he's ever seen active on a continuation round, and
  asserts he's already hidden by the time any Western round reaches
  Decision.
- All other existing Western tests (production sprites, outlaw Hit/Neutral
  state, target chrome/size, Encounter-ends-after-3-rounds) are unchanged
  and still pass unmodified.

### Test results

- **EditMode: 110/110 passed** (unaffected — no EditMode test touches
  Western presentation internals).
- **PlayMode: 44/45 passed** on the definitive full-suite run, with the
  single failure being the pre-existing, unrelated Game Show flake described
  above (confirmed passing in isolation). Every Western/Sheriff/off-screen-
  shot test passed, including all five new/extended ones listed above.

### Remaining visual risks (new this phase)

- **Sheriff's entrance/exit timing (2.15s) was budgeted against
  `EncounterIntroSeconds` (2.2s) by arithmetic, not by feel** — worth
  judging in manual play whether the ~50ms buffer before he's force-hidden
  ever reads as an abrupt cut rather than a clean exit.
- **The tracer's fixed length (140 units) does not scale with the actual
  origin-to-target distance** — for the nearest vs. farthest outlaw the
  streak covers a different fraction of the true gap; worth a manual look
  at whether this reads as inconsistent.
- **The screen kick (1.015×) is deliberately very subtle** — worth judging
  whether it's perceptible at all, or whether the brief's "small screen
  kick" wants more.
- **Sheriff's exit direction reuses the entrance's off-screen-left offset**
  (he retreats the same way he came, per the brief's own Phase D wording
  "Sheriff exits / fades / slides out" without specifying a side) — worth a
  look at whether exiting off-screen right instead would read better
  dramatically (not implemented, to avoid guessing past the brief).

## Western Outlaw Candidate 02 Production Family

### Design decision: one character, four costume skins

The original A/B/C/D production outlaws were four independently-generated
illustrations with no shared identity — different body types, different
poses, even a different hat style on one of them (confirmed by a direct
same-scale comparison sheet during validation). This reads as four
unrelated characters that happen to share a folder, not "the same gang."
Candidate 02 replaces this with a single base character (tall/lean/wiry,
long duster coat, wide-brim hat, hip holster) recolored four ways (bandana/
shirt color only) — a genuine skin family, the way game asset variants are
normally built.

### Neutral source family

Extracted from `ArtBible/Candidates/Western/Outlaws/Skins/Outlaw_Skins_Lineup_Candidate_02.png`
(1456×816, magenta chroma). Before any destructive work, the full-height
column-density profile was checked for genuine gaps between all four
silhouettes — confirmed clean (minimum ~4-14 non-background rows out of
816, verified by direct zoomed-strip inspection, not just the numeric
profile). Cropped to `Outlaw_{A,B,C,D}_Neutral_Candidate_02.png`, all on an
identical 350×820 canvas, each character kept at its own natural width (no
forced normalization), identical vertical crop range/margin across all
four for consistent baseline.

### Hit source family

Extracted from `ArtBible/Candidates/Western/Outlaws/Skins/Outlaw_Skins_Hit_Lineup_Candidate_01.png`
(1456×816, same convention). Two of the three gaps (A↔B, C↔D) were found to
have the raised-fist reaction pose reaching to within **exactly 1 pixel**
of the neighboring character's hat brim — verified with a per-pixel
opaque/background map, not just column sums. Per this project's own
"stop and report before destructive cleanup" instruction, this was reported
before any extraction; the user chose "best-effort crop, flag it." Cropped
to `Outlaw_{A,B,C,D}_Hit_Candidate_02.png` on a **420×820** canvas —
deliberately wider than Neutral's 350, because the raised-fist/kicked-leg
reaction poses genuinely need more horizontal room, and forcing Neutral's
width would have clipped the reaction itself. Canvas **height**, vertical
crop range, and vertical margin are identical to Neutral, which is what
actually controls baseline/apparent-scale continuity (see "Unity import
and apparent-scale verification" below).

### Outlaw D foreign-fragment cleanup

The 1px A↔B/C↔D contact risk manifested as one real, small defect: Outlaw
D's Hit sprite carried a detached ~7×26px fragment of Outlaw C's fist
knuckle at canvas-local x=[14,20], y=[153,179]. Verified via connected-
component analysis (flood fill on the alpha channel) that this fragment
was a fully separate 69-pixel island from D's own 1632-pixel hat-brim
component — i.e., structurally provable as foreign, not merely "looks
separate." Cleared only that specific 69-pixel component in place (in
`ArtBible/.../Skins/ProductionPrep/Outlaw_D_Hit_Candidate_02.png` — this is
this project's own prepped-for-production staging copy, not the raw
lineup source, so updating it in place doesn't lose provenance; the raw
lineup PNGs are untouched). Re-verified afterward: zero components remain
in the search window besides D's own hat. No hole, no clipping, no halo
introduced by the cleanup — confirmed by re-running the same connected-
component scan and a full visual re-inspection.

### Final production integration

All 8 files copied into
`Assets/Hermit/Content/Resources/Art/Gold/Western/Actors/` **as file-
content replacements of the existing production filenames**
(`Outlaw_{A,B,C,D}_{Neutral,Hit}.png`) — the `.meta` files were never
touched, so every GUID is preserved exactly (verified: `.meta` file
timestamps are unchanged from before this integration). Sheriff's three
sprites (`Sheriff_{Idle,Aim,Fire}.png`) were not touched. No presenter
resource-path change was needed — `WesternShootoutPresenter` already loads
`Art/Gold/Western/Actors/Outlaw_{letter}_{Neutral,Hit}`, which now simply
resolves to the new art.

### Unity import settings

Inspected the existing `.meta` files (unchanged by this integration) and
confirmed they already specify exactly the required settings: Texture Type
Sprite (2D and UI), Sprite Mode Single, Alpha Is Transparency on, Filter
Bilinear, Mip Maps on, Wrap Clamp, Uncompressed, Max Size 2048 — no import
setting changes were needed since the files were replaced in place under
the same filenames.

### Apparent-scale verification (Neutral↔Hit canvas-width difference)

Because Hit's canvas (420 wide) is wider than Neutral's (350 wide), swapping
sprites could in principle change the character's apparent on-screen size.
Checked by computation against the real presentation math
(`WesternShootoutPresenter`'s outlaw `Sprite` Image uses `preserveAspect`
inside a fixed 240×320 box): Neutral's aspect (350/820 ≈ 0.427) and Hit's
aspect (420/820 ≈ 0.512) are both narrower than the box's own aspect
(240/320 = 0.75), so **both are height-constrained** — each renders at
exactly the box's full 320-unit height regardless of the canvas-width
difference, with only the width differing (as expected, since the reaction
pose is visually wider). A new permanent test,
`WesternOutlaws_NeutralAndHitSprites_RenderAtTheSameApparentHeight`, proves
this for all four letters directly from each Sprite asset's own pixel
rect. **No presenter or target-box change was needed or made** — the
existing sizing method already handles this correctly, confirmed rather
than assumed.

### Testing

`WesternOutlaws_NeutralAndHitSprites_RenderAtTheSameApparentHeight` (new,
`ClasicoPlayModeTests.cs`) is the only new test — everything else (all 8
sprites loading, correct per-letter Neutral→Hit mapping with no cross-
pairing, the 3-round Encounter, Round 1 Sheriff intro, the off-screen shot
triggering the Hit sprite swap) is already covered by existing, unmodified
tests, which continuing to pass after the art swap **is** the regression
proof — no code path changed, only image bytes.

- **EditMode: 110/110 passed.**
- **PlayMode: see full run result below.**

### Manual validation status

**Not yet performed.** This phase does not claim COMPLETE until you've
played Western in the real Editor and confirmed the new family visually.

## C8.1d.4 — Western Cinematic Duel Intro

### Why the previous simple Sheriff/tumbleweed intro was rejected aesthetically

C8.1d.3's large in-world Sheriff protagonist (entering, settling into an Aim
pose, exiting) proved the *mechanics* of an Encounter intro were sound —
gating, timing, no gameplay-state overrun — but manual validation judged the
result unconvincing as a *scene*: one procedural actor sliding across a
static background reads as an arcade transition, not "silence → tension →
eye contact → hand near revolver → silence peak → gunshot → gameplay." This
phase replaces that presentation entirely with a short cinematic 2D montage
built from three real illustrated close-ups, while leaving every piece of
underlying Encounter/gameplay logic this phase was told not to touch
(scoring, content, Encounter grouping, Round 2/3 behavior, GameFlowController,
GameRegistry, Shell architecture) completely alone.

### Cinematic montage concept

Classic western-duel editorial grammar, compressed into ~4.11s of choreographed
shots (the full Encounter-intro gate is 5.0s — see "Timing-margin fix"
below): a wide establishing shot, then hard cuts between the Sheriff's
face, the outlaw's face, and the Sheriff's hand hovering near his holster —
repeating the face cuts once more as tension rises — a near-silent pause,
then the gunshot. Cuts are genuinely hard (an instant `Image.sprite` swap),
never crossfades; the only "softening" anywhere is the gunshot flash's own
brief (~0.06s) alpha ease, which the brief explicitly allows. Each shot gets
a barely-perceptible scale-only "push-in" (1.0 → 1.02-1.05×) — no zooms,
rotations, or parallax, per the brief's "barely perceptible" instruction.

### Exact shot order / timeline

| Time | Shot | Audio |
|---|---|---|
| 0.00-0.75 | Wide establishing shot (real Western background, shortened 0.65s tumbleweed crossing) | wind fades in |
| 0.75-1.45 | Sheriff close-up, push-in | twang |
| 1.45-2.15 | Outlaw close-up, opposing push-in | tension note |
| 2.15-2.90 | Sheriff hand/holster close-up, subtle push-in | low pulse |
| 2.90-3.40 | Sheriff close-up again, tension rising | two closer-spaced low pulses (mid-shot) |
| 3.40-3.80 | Outlaw close-up, shorter shot | (music thinning) |
| 3.80-4.05 | Tension pause, near-silence | tiny pre-draw tick |
| 4.05 | Gunshot | gunshot cue + dust accent |
| 4.05-~4.11 | Flash / hard cut | flash fades (~0.06s) |
| ~4.11+ | Gameplay reveal | — |

### Close-up asset paths

Source candidates (`ArtBible/Candidates/Western/Cinematic/`, untouched):
`Western_Sheriff_Closeup_Candidate_01.png`, `Western_OutlawA_Closeup_Candidate_01.png`,
`Western_Sheriff_HandGun_Closeup_Candidate_01.png` — all 1456×816
(16:9), opaque, no alpha needed. Production copies at
`Assets/Hermit/Content/Resources/Art/Gold/Western/Cinematic/Western_Sheriff_Closeup.png`,
`Western_OutlawA_Closeup.png`, `Western_Sheriff_HandGun_Closeup.png`,
imported as Sprite (2D and UI), no mipmaps (matches the existing
`WesternBackground.png` convention — these are full-bleed shots displayed
near-native size, not minified small sprites), Bilinear, Clamp,
Uncompressed, max size 2048. Displayed via one shared `CinematicImage`
(`Image` + `AspectRatioFitter` in `EnvelopeParent` mode) that fills the
stage cleanly with no stretch, cropping only the minimum needed — no
letterbox bars (judged unnecessary complexity for this pass; the brief
explicitly allows skipping it).

### Intro gunshot vs. gameplay gunshot — a hard distinction

The cinematic's gunshot (`ProceduralAudio.Gunshot`, a composite crack+body+
tail clip) is **pure punctuation** — it never calls scoring, hit-state,
answer-selection, or any gameplay method. It fires once, from
`CinematicIntroRoutine`, purely for the flash + camera-punch + audio beat.
The real gameplay shot remains exactly what C8.1d.3 built —
`FireOffscreenShot`, fired later from `RevealOutcome` only after the player
answers — completely unmodified by this phase. A dedicated test
(`WesternCinematic_GunshotFlash_FiresOnce_ThenGameplayReveals`) proves the
intro flash fires exactly once and gameplay reveals immediately after.

### Audio placeholder architecture

Per the brief, kept as separate conceptual channels/clips rather than one
baked track, so any one can be swapped for a final Suno/Stable Audio asset
without touching the others: **A.** wind ambience (one long quiet
`ProceduralAudio.Noise` bed, non-looping since the intro plays once).
**B.** duel tension motif — a twang (`Sweep`), a tension note and low pulses
(`Tone`), each triggered individually at its own timeline offset, not one
score. **C.** a tiny metallic pre-draw tick (`Tone`). **D.** the gunshot
(`ProceduralAudio.Gunshot`, new — a single composite clip: a ~12ms bright
noise "crack", a ~92Hz exponentially-decaying tone "body", and a quiet
longer noise "tail" for a cheap sense of outdoor space; deliberately not a
cartoon pop, laser, or overlong boom). **E.** an optional dust/ricochet
noise accent right after the shot. All fully procedural/placeholder — no
Suno/Stable Audio finalization in this phase.

### Input/timer gating

Unchanged in mechanism from C8.1d.2/.3: `ShowChallenge`'s `isEncounterStart`
branch hides outlaws/concept/labels/reticle before `CinematicIntroRoutine`
even starts, and `RevealAfterIntro` (host-triggered, at the director's real
Intro→Decision boundary — see `ClasicoGameHost`) remains the sole
authoritative reveal gate, independent of the cinematic's own cosmetic
timing — the same C8.1d.2 lesson applied again. Because the cinematic is
genuinely longer (~4.11s of choreographed shots) than the old arcade intro,
`ClasicoGameDefinition.EncounterIntroSeconds`'s declared default was raised
from 2.2s to **5.0s** so the director's real gate duration matches — this
is a presentation-timing constant, not a change to Encounter grouping,
scoring, or round structure. Every hardcoded test wait that assumed the old
2.2s ceiling (`LaunchClasico`, `AnswerCurrentMicrogame`, and their
equivalents in `ShellPlayModeTests`/`ShellRealSceneCompositionTests`) was
bumped accordingly (5.5f→8.5f, 4.2f→7.0f) — the same class of suite-wide
timing fix this project has hit before whenever the Western intro's own
length changed. See "Timing-margin fix" immediately below for why 5.0s
(not the theoretical ~4.11s, and not the first-attempt 4.5s) was the value
actually shipped.

### Timing-margin fix

The first implementation raised `EncounterIntroSeconds` from 2.2s to 4.5s —
reasoned as "theoretical routine sum ~4.11s, so 4.5s leaves ~0.4s of
slack." A full PlayMode suite run at that value produced 6 failures, all
consistent with one root cause: `CinematicIntroRoutine` was occasionally
still touching `_cinematicRoot`/`_cinematicImage` when `RevealAfterIntro`
forcibly hid them, because several of the routine's per-shot
`PushInRoutine` while-loops can each overshoot their target duration by a
fraction of a frame, and five of them chained back-to-back can eat more
than 0.4s of slack under real per-frame timing.

Rather than assume that diagnosis, it was verified empirically per this
project's standing "verify, don't guess" discipline: the
`ClasicoPlayModeTests` fixture was re-run in isolation (no full-suite CPU
contention) at the same 4.5s value. 5 of the original 6 failures — plus
one more the full-suite run's own timing noise had been masking,
`EveryArchetype_RendersDistinctInteractableControls_WhenItAppears` — still
failed. Reproducing under isolation confirmed a genuine margin bug, not a
full-suite-load artifact, so the fix was to widen the margin rather than
chase individual test flakiness.

`EncounterIntroSeconds` was raised to **5.0s** — the top of this brief's
own stated 4.0-5.0s target range — leaving a real ~0.9s buffer over the
~4.11s theoretical sum. Every dependent hardcoded test wait was bumped
again to match (8.0f→8.5f, 6.5f→7.0f). A full EditMode re-run confirmed
110/110 still passing (this value doesn't affect any `CreateInMemory`-based
test, which defaults `encounterIntroSeconds` to 0f independently); the full
PlayMode suite was re-run at 5.0s to confirm all previously-failing tests
now pass — see "PlayMode result" below.

### Round 2/3 skip behavior

Unchanged in mechanism: `ShowChallenge(isEncounterStart: false)` never calls
`CinematicIntroRoutine` at all for continuation rounds — no establishing
shot, no close-ups, no gunshot, no cinematic audio. Proved by the existing
`WesternEncounter_WorldStaysActive_AndIntroFlourishFiresOnlyOnRound1` test
(now tracking `CinematicRoot` instead of the removed `SheriffActor`), which
fails loudly if the cinematic overlay is ever seen active on round 2 or 3.

### The removed C8.1d.3 SheriffActor

The large in-world Sheriff actor (build/pose machinery, entrance/exit
choreography) is fully removed, not left as dead code — it is completely
superseded by the cinematic close-ups. Tests that specifically validated
that mechanism were updated to validate the new one instead (documented
inline at each change site): `WesternShootout_ShowsIllustratedBackground_AndInWorldSheriffActor`
→ `..._AndCoreStructure`; `WesternShootout_SheriffActor_NotVisibleDuringActiveGameplay_OutlawsAre`
→ `WesternShootout_CinematicRoot_NotVisibleDuringActiveGameplay_OutlawsAre`;
the Sheriff-intro-scale assertions inside
`WesternEncounter_Round1Intro_GatesConceptAndInputUntilIntroCompletes` and
`WesternEncounter_WorldStaysActive_AndIntroFlourishFiresOnlyOnRound1` were
replaced with the equivalent `CinematicRoot` checks.

### Real-build visual validation requirement

**Manual visual validation of this phase must be performed in a real
Windows .exe build, not only the Unity Editor Game View** — the Editor's
Game View has been observed to render softer/distorted compared with the
actual executable. Editor play is still the right tool for verifying flow,
timing, state, and bugs (which is what the automated EditMode/PlayMode
suites below already cover); but sharpness, cropping, close-up quality, UI
clarity, and overall cinematic feel can only be judged correctly from the
built .exe.

### Testing

New: `WesternCinematic_AllThreeCloseUpAssets_Load`,
`WesternCinematic_AllThreeCloseUps_AppearDuringRound1Intro_LabelsAndReticleStayHidden`
(frame-accurate Resources.Load reference-equality per shot, plus label/
reticle hidden proof), `WesternCinematic_GunshotFlash_FiresOnce_ThenGameplayReveals`.
Updated (SheriffActor → CinematicRoot, see above):
`WesternShootout_ShowsIllustratedBackground_AndCoreStructure`,
`WesternShootout_CinematicRoot_NotVisibleDuringActiveGameplay_OutlawsAre`,
`WesternEncounter_Round1Intro_GatesConceptAndInputUntilIntroCompletes`,
`WesternEncounter_WorldStaysActive_AndIntroFlourishFiresOnlyOnRound1`. All
other existing Western tests (off-screen shot, Neutral↔Hit mapping, 3-round
Encounter, production sprite loading) are unmodified and continue to pass,
which is itself the regression proof that this phase touched only
presentation.

- **EditMode: 110/110 passed.**
- **PlayMode: see full run result below.**

### Manual validation status

**Not yet performed — and per section 20 above, cannot be fully judged in
the Editor alone.** This phase does not claim COMPLETE until you've played
Western in a real built .exe and confirmed the cinematic reads as intended.

## Status

Still **IMPLEMENTATION IN PROGRESS — MANUAL VISUAL VALIDATION REQUIRED**.

This phase replaces the remaining procedural/geometric prototype presentation
in the four Gold microgames with real 2D illustrated art, wherever the
Art Bible's `ArtBible/Candidates/` folder already has an asset for it —
while leaving `ClasicoSessionDirector`, `GameFlowController`, `GameRegistry`,
scoring, timing, and scene flow completely untouched. Presentation only, per
the C8.1d brief's hard scope. The visual direction is HERMIT HYBRID
(C8.1c, status CANDIDATE) — this phase does not re-evaluate it.

## Manual Validation Correction — Portrait Integration Rejected

The first pass of this phase (Western/Game Show/Detective, all using
`RuntimeUIFactory.CreatePortraitFrame`) was manually validated and
**rejected** for Western, on visual-direction grounds, not a defect:

- The portrait-card integration was **structurally valid** — sprites were
  correctly assigned, transforms correctly restored, tests correctly green.
- It was **manually rejected** because it felt like "images pasted onto a
  prototype" rather than "illustrated characters acting inside a videogame
  world": Sheriff Implacable sat as a framed photo in a corner while the
  actual gameplay (procedural outlaw targets) happened elsewhere on screen,
  fully disconnected from him.
- **New rule, effective immediately:** active gameplay characters must
  physically inhabit the scene as in-world actors. A portrait/card
  presentation is only acceptable where the mechanic itself specifically
  calls for a portrait/card (none of the current Gold microgames do).
  `RuntimeUIFactory.CreatePortraitFrame` is **not removed** — it stays
  available for a future mechanic that genuinely wants a framed portrait —
  but no Gold presenter's active gameplay composition may use it going
  forward.
- The opaque Sheriff/Auditor/Presentador Midjourney candidates
  (`ArtBible/Candidates/{Sheriff,Auditor,Presentador}/...`) are **reference
  material only** — they establish the character's design/costume/palette
  for briefing future art generation, but are never wired into gameplay
  presenters as in-world sprites (they have no transparency, and faking a
  cutout was already rejected in the first pass — see "Sprite import rules"
  below, which still applies).
- **Western Shootout is now the staging prototype** for how every future
  Gold-world character integration should work: an in-world actor, built
  from procedural placeholder shapes until real transparent sprites exist,
  structured so dropping in final art requires no presenter rewrite. Game
  Show and Detective still use the (structurally fine, visually rejected)
  portrait-card pattern as of this document and are explicitly **not**
  touched until Western's model is validated — see "Western actor
  hierarchy" below.

**Update — production sprites now exist.** The staged architecture above
proved out: real transparent Sheriff (Idle/Aim/Fire) and outlaw
(Neutral/Hit ×4) sprites have been prepared from the user's selected
Midjourney candidates and wired into `WesternShootoutPresenter` with no
further presenter rewrite required — see "Western Production Sprite
Integration" immediately below. Game Show, Detective, and Balance remain
untouched.

## Western Production Sprite Integration

The user selected a second batch of Western candidates specifically for
gameplay (not reference) use — Sheriff in three action poses, and four
outlaw archetypes each in Neutral/Hit — and asked for them to be turned into
real transparent PNGs and wired into the staged Western actor architecture.

### Source → production mapping

| Role | Source (`ArtBible/Candidates/...`) | Dimensions | Production file (`Assets/Hermit/Content/Resources/Art/Gold/Western/Actors/`) |
|---|---|---|---|
| Sheriff — Idle | `Sheriff/Gameplay/Sheriff_Idle_Candidate_01.png` | 928×1232 | `Sheriff_Idle.png` |
| Sheriff — Aim | `Sheriff/Gameplay/Sheriff_Aim_Candidate_01.png` | 928×1232 | `Sheriff_Aim.png` |
| Sheriff — Fire | `Sheriff/Gameplay/Sheriff_Fire_Candidate_01.png` | 928×1232 | `Sheriff_Fire.png` |
| Outlaw A — Neutral/Hit | `Western/Outlaws/Outlaw_A_{Neutral,Hit}_Candidate_01.png` | 928×1232 | `Outlaw_A_Neutral.png` / `Outlaw_A_Hit.png` |
| Outlaw B — Neutral/Hit | `Western/Outlaws/Outlaw_B_{Neutral,Hit}_Candidate_01.png` | 928×1232 | `Outlaw_B_Neutral.png` / `Outlaw_B_Hit.png` |
| Outlaw C — Neutral/Hit | `Western/Outlaws/Outlaw_C_{Neutral,Hit}_Candidate_01.png` | 928×1232 | `Outlaw_C_Neutral.png` / `Outlaw_C_Hit.png` |
| Outlaw D — Neutral/Hit | `Western/Outlaws/Outlaw_D_{Neutral,Hit}_Candidate_01.png` | 928×1232 | `Outlaw_D_Neutral.png` / `Outlaw_D_Hit.png` |

All 11 mappings were unambiguous (exact 1:1 filename correspondence, no
naming conflicts). One out-of-scope file was found alongside these and
**not used**: `ArtBible/Candidates/Exploration/Future Characters/FutureCharacter_WesternBruiser_Concept_01.png`
— a future-exploration concept, not part of the selected production set.
`ArtBible/Candidates/` is untouched; every production file above is a
**copy**, background-removed, never overwriting the source.

### Pre-flight inspection findings

Per the brief's "verify before changing anything" instruction, all 11
source files were inspected (dimensions, pixel format, corner-sampled
background color) before any processing:

- All 11 are `Format24bppRgb` (no alpha channel at all) on a flat,
  saturated magenta/pink chroma background, exactly as described.
- **10 of 11** sample to a true magenta-family background (e.g. Sheriff
  ≈(248,8,147), Outlaw A ≈(216,39,119)).
- **`Outlaw_B_Neutral` is the one exception**: it samples to a warm tan
  gradient background (≈(219,185,159)), inconsistent with its own `_Hit`
  counterpart and every other file. Flagged per the brief's "stop and
  report if a role doesn't conform" instruction rather than silently
  assumed-magenta. Given the risk (a tan background sits close in hue to
  the character's own skin tone), this file was processed with the same
  *per-image sampled* background-color pipeline as the other 10 (which
  samples each file's own corners rather than assuming a hardcoded magenta
  value) and the result was manually inspected at full resolution,
  cropped tight on the face/hands — clean, no skin damage, no holes (see
  "Manual cleanup notes" below). Accepted as production-ready; still worth
  a second look in manual play since it's the one file that didn't follow
  the expected pipeline exactly.
- No embedded borders, baked-in UI, or text on any of the 11.
- Sheriff's three poses and Outlaws A/C/D each have a **baked-in painted
  ground-contact shadow** near the feet — confirmed (by direct pixel
  sampling, not guesswork) to be genuine artist-painted color on Sheriff
  (a magenta+black blend, stripped along with the background — see below)
  and a distinct warm brown/maroon paint on the outlaws (not a magenta
  blend at all — left intact, see "Remaining visual risks").

### Background-removal method

A per-image color-distance chroma key, written as a small Add-Type C#
tool run through PowerShell (`System.Drawing`, `LockBits` for speed — no
Python available in this environment, no new dependency added to the
repo):

1. **Sample the background**, per file, from small patches in all four
   corners (not a hardcoded magenta constant) — this is what let the one
   non-magenta file (`Outlaw_B_Neutral`) still process correctly.
2. **Alpha from color distance**: Euclidean RGB distance from the sampled
   background, ramped from fully transparent (distance ≤ 20) to fully
   opaque (distance ≥ 60), linear in between.
3. **Edge decontamination**: for partial-alpha pixels, un-blend the
   background's color contribution so soft edges don't carry a
   background-colored fringe.
4. **Magenta-fingerprint shadow stripping**: a second, independent test —
   `G / min(R,B)` stays tiny for *any* brightness of a magenta/black blend
   but stays ≥1 for neutral/brown/red/gold character colors — catches a
   painted shadow that is itself a magenta+dark blend (this is what
   removed Sheriff's shadow) without touching genuinely dark clothing.
   **Guarded by a brightness floor** (only trusted when `min(R,B) > 40`):
   the first version of this test, applied unconditionally, produced a
   real defect — speckled transparent holes punched through Outlaw B's
   near-black shirt/pants/boots, because at single-digit RGB values a tiny
   compression-noise gap between channels swings the ratio wildly. Found
   via full-resolution checkerboard inspection (the downscaled contact
   sheet alone made it look like a resize artifact at first — it wasn't),
   fixed by the floor, re-verified clean.
5. **Zero the RGB of fully-transparent pixels** — otherwise they keep the
   source background color, which can bleed into visible edges as a faint
   fringe under Unity's bilinear filtering even though alpha is correctly
   0.

Both thresholds (20/60 distance, 0.35/0.65 ratio, 40 brightness floor) are
one shared set applied uniformly to all 11 files — no per-file tuning.

### Alpha validation results

Programmatic checks on all 11 final PNGs (none treated as sufficient by
themselves — see manual notes below):

- **Format**: all 11 confirmed `Format32bppArgb` (real RGBA).
- **Transparency present**: corner/border pixels alpha = 0 on every file;
  98.8–100% of a border sample ring is fully transparent per file (the
  remainder is legitimate silhouette — a hat brim or shoulder reaching
  near the canvas edge on the taller poses).
- **Opaque interior present**: 9.3–44.7% of each canvas is opaque
  character silhouette (varies by pose/archetype, as expected — e.g.
  Outlaw D's very tall/thin silhouette occupies far less area than
  Outlaw B's broad one).
- **No dominant background survives**: 0% of a fine sampling grid across
  all 11 files has both full opacity *and* a color within 15 units of that
  file's own sampled background — i.e. no "chroma key miss" left an opaque
  patch of the original backdrop.
- Dimensions uniform at 928×1232 across all 11; every file opens correctly
  (verified by re-loading each processed PNG back through `System.Drawing`
  and again through Unity's own importer — see "Import settings" below).

### Manual cleanup notes (contact-sheet inspection)

Per the brief, automated checks were **not** treated as sufficient alone.
Contact sheets were generated on light-gray, dark-charcoal, and
checkerboard backgrounds and visually inspected, plus targeted
full-resolution crops:

- First pass: clean silhouettes, no magenta halos, but every character's
  baked ground-contact shadow still carried a visible magenta-tinted
  smear — a real defect, not the intended art. Root-caused to the shadow
  being a magenta+black paint blend on Sheriff specifically (fixed by the
  ratio test above).
- Second pass (after the ratio-test fix): Outlaw_B_Hit showed obvious
  bright/white speckled blotches across the torso and arms in the
  contact-sheet grid. Cropped the actual full-resolution file (no
  downscaling) to confirm this was real, not a thumbnail-resize artifact:
  it was checkerboard-pattern **holes torn through the dark shirt/pants/
  boots** — exactly the "accidental transparent holes inside dark
  clothing" failure the brief warned against. Root-caused to the
  unguarded magenta-ratio test misfiring on near-black noise; fixed by the
  brightness floor.
- Final pass: re-inspected all 11 files at full resolution against a
  checkerboard. Clean silhouettes, hat tips/boots/fingers/revolver/coat
  edges/facial hair all preserved, no magenta halo, no holes, no white-
  fringe artifacts, safety margin preserved on every side (no cropping was
  performed — the full original canvas, background included, is kept, so
  a margin around the silhouette is automatic). `Outlaw_B_Neutral`'s
  face/hands specifically re-inspected via a tight crop — clean, no skin
  damage despite the non-magenta source background.
- **Intentionally left alone**: Outlaw A/C/D's baked warm-toned ground
  shadow. Confirmed by direct pixel sampling to be real character-adjacent
  paint (not a magenta blend), so removing it would mean guessing at
  silhouette/paint the brief said to preserve. Flagged as a possible
  double-shadow risk against the procedural `GroundShadow` ellipse — see
  "Remaining visual risks".

### Import settings

Identical convention to the first pass, applied to all 11 new textures:
Texture Type **Sprite (2D and UI)**, Sprite Mode **Single**, **Alpha
Source: Input Texture Alpha** (real alpha now exists, unlike the first
pass's opaque reference portraits), `alphaIsTransparency` enabled, no
mipmaps, filter **Bilinear**, wrap **Clamp**, **Uncompressed**, max size
2048 (no downscaling — source is 928×1232). Verified by running the
project through Unity 6000.3.23f1 in batch mode: every `.meta` re-
serialized cleanly to the editor's own importer schema with these settings
intact (same verification method as the first pass).

### Runtime role / sprite-state mapping

| Actor | Idle/Neutral state | Reaction state | Trigger |
|---|---|---|---|
| SheriffActor | `Sheriff_Idle` — shown in `ShowChallenge` (start of every AimSelect round) | `Sheriff_Aim` while a target is selected during Decision (every frame, sprite-swap only, no coroutine); `Sheriff_Fire` for the fire beat in `RevealOutcome`, then back to `Sheriff_Idle` ~0.15s later | `RenderDecision` (Aim) / `FireSequenceRoutine` (Fire → Idle) |
| Outlaw A–D | `Outlaw_{letter}_Neutral` — reset every `ShowChallenge` | `Outlaw_{letter}_Hit` for the *selected* outlaw only, at reveal, regardless of correctness (Sheriff fires at whoever was picked) | `FireSequenceRoutine` |

Sheriff's Idle→Aim→Fire→Idle flow and the outlaw Neutral→Hit flow are
both pure sprite-reference swaps (`Image.sprite = ...`) driven from
existing per-frame/coroutine code already in the presenter — no new
coroutines were added, so there is nothing to "restart every frame."
`WesternShootoutPresenter.cs` was extended, not rewritten from scratch.

**Update — manual validation found two more problems.** Playing the
production-sprite build surfaced (1) the outlaws still visually sitting
inside dark rectangular "answer cards," reported too small and pixelated,
and (2) a structural weakness: Western's full presentation (in practice,
its intro beat) was repeating every time an individual AimSelect round came
up, with no sense that these rounds belonged to one continuous scene. Both
are corrected below — C8.1d.1.

## C8.1d.1 — Western Target Visual Correction + Pixelation Investigation

### What was wrong, and why

The outlaw sprite (a `Sprite` child, `preserveAspect` on) was drawn inside a
`WesternTarget` **Button**, and that Button's own background `Image` was
still being tinted (`Theme.PanelRaised` at rest, `Theme.Correct`/
`Theme.Incorrect` on reveal) — exactly the "answer button" idiom the rest of
Clásico's UI legitimately uses everywhere else, but wrong here now that a
real illustrated character is meant to read as a physical target standing
in the world. Because the sprite's aspect (0.75, from the 928×1232 source)
is narrower than the button box, it was letterboxed — leaving a visible
strip of the tinted button color on either side, reading exactly as a
"card" behind the character.

### Pixelation investigation (measured, not guessed)

Per source file (all 11 actor sprites share these):

| Property | Value |
|---|---|
| Source texture dimensions | 928×1232 |
| Imported sprite dimensions | 928×1232 (max size 2048 — no import-time downscale) |
| Old RectTransform (button) size | 190×220 |
| Old effective rendered size (preserveAspect, height-limited) | ≈166×220 canvas units |
| Canvas scale factor | `CanvasScaler` is `ScaleWithScreenSize`, reference 1280×720, match 0.5 — since 1280:720 is exactly 16:9, both axes agree regardless of match, so scale factor = actual resolution ÷ 1280 (e.g. **1.5×** at a common 1920×1080 window) |
| Old effective on-screen pixel height (at 1920×1080) | ≈220 × 1.5 ≈ **330px** |
| Effective scaling ratio (old) | 330 ÷ 1232 ≈ **0.27×** — a **downscale**, not an upscale |
| Preserve Aspect | On (unchanged) |
| Filter Mode | Bilinear (unchanged — see below) |
| Max Texture Size | 2048 (unchanged) |
| Compression | Uncompressed (unchanged) |
| Mipmaps (old) | **Off** |

The arithmetic rules out "excessive upscaling" as the cause — the sprite
was being drawn *smaller* than its own source resolution, comfortably
inside it. Two real causes remained, both addressed:

1. **The character was rendered too small relative to the stage** — 330px
   tall in a 1080-tall window reads as visually undersized regardless of
   source quality, and boxed inside the button "card" (above) it read even
   smaller than its own already-small footprint.
2. **No mipmaps on a texture being significantly minified.** A texture
   drawn well below its native resolution needs mipmaps for the GPU to
   properly box-filter high-frequency detail (fine outlines, hat brims,
   hair) during minification; without them, minification aliasing shows up
   as a shimmery/noisy "pixelated" look — a genuine, well-known rendering
   artifact, not a naming coincidence with actual low resolution.

**Fix applied (both, no guessing which one alone would suffice):**
`enableMipMap` flipped to `1` in all 11 actor sprites' `.meta` files
(filter mode kept **Bilinear** — per the brief's explicit instruction, not
switched to Point; illustrated art stays smooth), and the on-screen size
substantially increased (below). Mipmap generation with Bilinear filtering
is the textbook-correct combination for illustrated 2D art that's
minified — Point filtering would have made it look worse (hard-edged/
blocky), not better, which is exactly why the brief pre-empted that guess.

### Target visual correction

- Every `WesternTarget` button's own background `Image.color` is now fixed
  at fully transparent (`(0,0,0,0)`) — set once at Build time, **never**
  touched again by `ShowChallenge` or the reveal sequence. No rectangular
  panel, no "answer card," at rest or on reveal.
- The Button component itself is unchanged — still the whole clickable/
  selectable/keyboard-navigable/accessible target (`RuntimeUIFactory`'s
  standard `Selectable` machinery), just visually invisible. Input
  architecture is untouched.
- Reveal feedback (which outlaw was correct/incorrect) moved from a
  button-panel color flash to a **subtle tint on the outlaw's own sprite**
  (`Color.Lerp(Color.white, Theme.Correct/Incorrect, 0.55f)` on the art
  path) or the existing face-pose change (procedural fallback path) — a
  "character-local highlight," never a rectangular color reveal.
- Target size grew from 190×220 to **240×320** (effective rendered height
  ≈320 canvas units, ≈45% taller than before) — Sheriff's own box shrank
  slightly (150×210 → 140×200, anchor 0.08 → 0.04) to keep clearance; both
  actors now share the same ground line (`anchoredPosition.y = 140`,
  bottom-pivoted) rather than the earlier mismatched 60/140 split, per the
  brief's "all grounded on one coherent world plane." The four outlaws'
  own relative proportions (A tall/thin, B broad, C compact, D tall/
  crooked) come straight from the source art and are preserved automatically
  by using one shared bounding box with `preserveAspect` — not normalized
  away.
- The reticle is now the *only* remaining selection-state chrome — it
  already existed for exactly this purpose and needed no changes.

## Western Encounter Presentation

### Why per-question thematic intros were rejected

Every AimSelect round previously played its own ~0.6s "command beat" Intro
independently, indistinguishable in kind from every other archetype's — a
model that's fine for a plain UI command word, but wrong once Western
carries real staging (a face-off, a sting, a tumbleweed): replaying that
full presentation on every random re-appearance of the archetype would
feel like the scene resetting itself for no diegetic reason, and repeating
it while the world never actually left the screen is redundant, not
dramatic. The fix is structural, not cosmetic: group Western's rounds into
one continuous scene with one intro, not N repeats of a smaller one.

### The Encounter concept

`ClasicoEncounterPlan` (`Assets/Hermit/Games/Clasico/ClasicoEncounterPlan.cs`)
is the minimum local concept the brief asked for — a small `internal`
struct, `{ Archetype, RoundCount }`, consumed only by
`ClasicoSessionDirector`. It is **not** a generic engine-wide framework:
`ClasicoSessionDirector` flattens a `List<ClasicoEncounterPlan>` into
exactly the same per-round `MicrogameArchetype[]` it always built (plus one
new parallel `int[] roundWithinEncounter` array), so every other piece of
the director — challenge drawing/cursors, scoring, `_index`/
`TotalMicrogames`, `GameFlowController`/`GameRegistry`/Shell — needed **no
change at all**. An Encounter is metadata about how many *consecutive*
rounds one archetype occupies, not a new execution model.

`ClasicoGameDefinition` gained four fields: `WesternEncounterRoundCount`
(declared default **3** — the shipped `.asset` picks this up automatically
as a brand-new field with no YAML edit needed), `EncounterIntroSeconds`
(2.2s), `RoundTransitionSeconds` (0.35s), `EncounterOutroSeconds` (0.7s).
`CreateInMemory`'s new parameters all default to values that fully restore
the original C8.1 per-round rhythm (`westernEncounterRoundCount: 1`) — every
pre-C8.1d.1 test using that factory without naming these parameters is
byte-for-byte unaffected; only tests that explicitly opt in observe the new
behavior (see "Testing" below).

### Sequence construction

`ClasicoSessionDirector.BuildEncounterPlans` builds exactly one Western
block of `WesternEncounterRoundCount` (3) consecutive AimSelect rounds,
inserted at a random position among single-round blocks of the other three
archetypes (round-robin, reshuffled until no two adjacent single-round
blocks share an archetype — reusing `ClasicoMicrogameLibrary`'s own
`Shuffle`/`HasAdjacentDuplicate` helpers, now `internal` instead of
`private` so the director can call them directly rather than re-deriving
the rule). The total flattened round count always equals the definition's
existing `MicrogameCount` (9 by default) — Western now always claims
exactly 3 of those 9, the other 6 split across ChooseSide/Balance/
DetectError exactly as the old distribution would have. Content reuse
(brief section 18 — no duplicate prompt within one 3-round Western block)
falls out of the *existing* pool-cursor mechanism for free: since Western
only ever appears as this one contiguous block, its classification cursor
only ever advances by 3 across the whole session, and the pool has 8
entries — no wraparound, no duplicate, no new dedup logic needed (verified
by a dedicated test, not just asserted).

### Intro/round-transition/outro timing

No new `GameFlowController` state, no new phase exposed outside
`ClasicoSessionDirector` — internally, `Phase.Intro`'s duration and
`Phase.Feedback`'s duration both became small per-round computations
instead of flat constants:

- `GetIntroDurationSeconds()` — `CommandBeatSeconds` (0.6s, unchanged) for
  any archetype not using Encounter presentation, or for `roundWithinEncounter`
  &gt; 0; `EncounterIntroSeconds` (2.2s) for round 0 of an Encounter-
  presenting archetype (Western only, this phase).
- `GetFeedbackDurationSeconds()` — `FeedbackDisplaySeconds` (0.8s,
  unchanged) normally; `FeedbackDisplaySeconds + EncounterOutroSeconds`
  (≈1.5s) only on the *final* round of an Encounter — this **is** the
  outro, deliberately implemented as an extended dwell on the existing
  Feedback phase rather than a new phase: Sheriff is already back to Idle
  by then (its own fire-sequence restore), the hit outlaw is already
  visibly holding its reveal pose, and stretching that beat a little longer
  reads as "the round settling" without inventing new animation. The
  existing `PlayTransitionCut()` (`CanvasGroup.alpha`, unchanged — no
  `Image.Type.Filled`/`Radial360` regression) still fires exactly once, at
  the *next* Encounter's start.
- Which archetypes get this treatment at all is centralized in two small
  methods, `GetEncounterRoundCount`/`UsesEncounterPresentation` — Western
  only, this phase, but a future world's Encounter support (brief section
  16/17: "avoid Western-specific branching... but do not overengineer") is
  a one-line addition there, not new branches through `Tick`/
  `AdvanceToNextMicrogame`/`ResolveCurrentMicrogame`.
- **Escalation hook, deliberately left a no-op** (brief section 14):
  `GetDecisionWindowSeconds` now takes a `roundWithinEncounter` parameter
  that nothing yet varies by. Wiring in "round 2/3 slightly shorter" is
  future work — the brief explicitly allows leaving every round at the
  same duration rather than guessing at untested numbers, and the
  parameter's presence is the documented hook itself.
- **Decision timer**: untouched. `_activeEngine.Tick(...)` (and
  `_decisionElapsed`) only ever run during `Phase.Decision`, exactly as
  before — Intro/Feedback/the extended outro dwell never touch it, so the
  shared top Timer bar is unaffected and the decision window never starts
  early or gets shorted by the intro (verified by a dedicated test).

### World persistence — the actual fix for "repeats every round"

`ClasicoGameHost.RenderFrame`'s microgame-index-change block now
distinguishes a genuine new Encounter (`director.CurrentArchetype !=`
whichever archetype was last shown, or the very first microgame of the
session) from a continuation round of the *same* Encounter:

- **New Encounter**: exactly the old behavior —
  `HideAllPresenters()` → show the new challenge with
  `isEncounterStart: true` → `PlayTransitionCut()`.
- **Continuation round**: none of that. The active presenter's own
  `ShowChallenge` is called again with `isEncounterStart: false` — no hide,
  no rebuild, no transition fade. For Western this means the background,
  Sheriff, and all four outlaw GameObjects are never deactivated between
  rounds 1→2→3 — "the scene persists" is a direct, verifiable structural
  property (see the new `WesternEncounter_WorldStaysActive...` PlayMode
  test), not just an intended feeling.

`WesternShootoutPresenter.ShowChallenge` gained an `isEncounterStart`
parameter (only Western's signature changed — `IMicrogamePresenter` only
requires `Build`/`Hide`, so Game Show/Detective/Balance's own
single-argument `ShowChallenge` methods are untouched). `true` triggers the
one-time face-off flourish (sting + tumbleweed) below; `false` is the exact
same quick reset `ShowChallenge` always did (Sheriff → Idle, every outlaw →
Neutral, fresh labels, fresh selection) — the "actors reset between rounds"
requirement was already this method's job before Encounters existed; the
only thing that changed is that it's now sometimes called without the
one-time flourish.

### Intro flourish — sting + tumbleweed

Both fire once, only when `isEncounterStart` is true, from
`WesternShootoutPresenter.ShowChallenge`:

- **Sting** (`BuildAudio`/`PlayEncounterSting`): three short
  `ProceduralAudio` cues on a dedicated `AudioSource` added to the shared
  host GameObject (alongside `ClasicoHud`'s own — multiple `AudioSource`s
  per GameObject is normal) — a 92Hz/0.12s thump, an 80ms later 523→311Hz/
  0.55s sweep (the "twang"), a 100ms-after-that short noise burst (dust/
  wind). All synthesized, no external/copyrighted audio, per the brief.
- **Tumbleweed** (`BuildTumbleweed`/`TumbleweedRoutine`): five thin rounded
  bars fanned around a center point (a cheap procedural "ball of twigs"),
  inactive except during its one 1.6s crossing — horizontal translate,
  continuous rotation, a small sine-wave vertical bounce, no physics.
  Positioned mid-ground (`anchoredPosition.y = 100`), clear of the concept
  text above and not overlapping the timer. Deactivated and reset to rest
  the moment the crossing finishes — never a persistent object.

### Scoring

Untouched. Each round scores exactly as before
(`ClasicoScoring.ComputeQuestionScore`, streak/streak-bonus unchanged); no
separate "Encounter score" was introduced, and Intro/the extended outro
dwell award nothing (verified by a dedicated test: three correct Western
rounds in a row score exactly 300, not more).

## A. Candidate assets discovered

`ArtBible/Candidates/` contains, as of this phase:

| Folder | Files | Notes |
|---|---|---|
| `Worlds/` | `Western_Hybrid_Candidate_01.png`, `GameShow_Hybrid_Candidate_01.png` | Full 16:9 establishing shots, 1456×816. No Detective or Balance world background exists. |
| `Sheriff/` | 12 files: `Hybrid_Base`, `Front`, `Profile`, `Neutral`, `Confident`, `Smug`, `Suspicious`, `Angry`, `Nervous`, `Shocked`, `Delighted`, `Defeated` (all `_Candidate_01.png`) | Full-body portrait, 928×1232, one shared pose/backdrop across the whole expression set. |
| `Auditor/` | `Auditor_Hybrid_Candidate_01 = opción #1.png`, `Auditor_Hybrid_Alternate_01 = opción #3.png` | Only two files, no expression set. Per the brief, candidate **#1** is the user-selected authority character; alternate #3 is not used. |
| `Presentador/` | `Presentador_Hybrid_Candidate_01.png` | One file, no expression set. |
| `Sheriff/Gameplay/` | `Sheriff_{Idle,Aim,Fire}_Candidate_01.png` | Gameplay-pose set, magenta-chroma background (not the reference portraits' opaque studio gradient) — see "Western Production Sprite Integration". |
| `Western/Outlaws/` | `Outlaw_{A,B,C,D}_{Neutral,Hit}_Candidate_01.png` | 8 files, magenta-chroma background (one exception — see below). |
| `Exploration/Future Characters/` | `FutureCharacter_WesternBruiser_Concept_01.png` | Out of scope — a future exploration concept, not part of the selected production set. |
| `Mascot/` | 8 files (Hermit directions/tests) | Out of scope — mascot work is paused this phase. |

All character portraits (Sheriff/Auditor/Presentador) are 928×1232, fully
opaque (alpha 255 everywhere, verified by direct pixel sampling), on their
own plain gradient studio backdrop — they are **not** alpha-cut cutouts.
See "Sprite import rules" below for how this was handled.

## B. Missing assets

- **Detective world background** — no lineup/interrogation-room illustration
  exists. The procedural back-wall/height-line/ambient-glow dressing is
  unchanged this phase.
- **Suspect art** (Detective) — no per-suspect illustration exists. Suspects
  remain `CharacterPrimitives` procedural faces (already an abstract,
  non-emoji placeholder per the fallback policy).
- **Outlaw target art** (Western) — no per-target illustration exists beyond
  Sheriff himself, who is not one of the four targets. Targets remain
  procedural, refined only by the new contrast band behind them.
- **Contestant art** (Game Show) — no contestant illustration exists.
  Remains `CharacterPrimitives` procedural.
- **Balance Machine world** — zero candidate assets of any kind (no
  background, no machine, no operator). Per the brief's own instruction
  ("better one excellent illustrated machine than a mediocre new operator"
  applied literally when *no* machine art exists either), this presenter is
  **unchanged** this phase.

## C. Files changed

First pass (portrait cards, Western/Game Show/Detective — Western since
superseded, Game Show/Detective still current):

- `Assets/Hermit/Runtime/GameFramework/RuntimeUIFactory.cs` — added
  `LoadArt`, `CreateBackgroundImage`, `CreatePortraitFrame`.
- `Assets/Hermit/Runtime/GameFramework/Microgames/GameShowPresenter.cs`
- `Assets/Hermit/Runtime/GameFramework/Microgames/DetectiveLineupPresenter.cs`
- New Resources art (copied, not moved, from `ArtBible/Candidates/`; see
  mapping table below) under
  `Assets/Hermit/Content/Resources/Art/Gold/{Western,GameShow,Detective}/`.
- `BalanceMachinePresenter.cs` — **not modified** (no candidate art exists).

Correction pass (Western staging — Western only, per the brief's hard scope):

- `Assets/Hermit/Runtime/GameFramework/Microgames/WesternShootoutPresenter.cs`
  — rewritten: removed the portrait-card Sheriff entirely, added the
  in-world `SheriffActor` (see "Western actor hierarchy" below).
- `Assets/Hermit/Tests/PlayMode/ClasicoPlayModeTests.cs` — the Western
  portrait-card test replaced with two actor-structure tests; the Game
  Show/Detective portrait-card tests are untouched.
- `RuntimeUIFactory.CreatePortraitFrame` — left in place, unused by Western
  now, still used by Game Show/Detective.

Production sprite pass (this update — Western only):

- `Assets/Hermit/Runtime/GameFramework/Microgames/WesternShootoutPresenter.cs`
  — extended (not rewritten): per-outlaw art loading/fallback, Sheriff
  resource paths updated to `Art/Gold/Western/Actors/...`, `SheriffPose`
  enum + `SetSheriffPose`, outlaw `Sprite` wiring, taller target buttons,
  re-anchored labels, Sheriff restore-to-Idle after firing.
- `Assets/Hermit/Tests/PlayMode/ClasicoPlayModeTests.cs` — four new tests
  (production sprites replace placeholders, Sheriff pose transitions,
  outlaw Hit/Neutral state, Neutral-restore-on-next-challenge).
- 11 new production sprites + `.meta`s under
  `Assets/Hermit/Content/Resources/Art/Gold/Western/Actors/` (new folder).
- No other presenter, test, or gameplay-logic file touched.

## D. Presenter changes

**Western Shootout** — the flat sky/sun/horizon/ground/rock primitives are
replaced by the illustrated background when present, with the original
procedural build kept intact as `BuildProceduralEnvironment` (the fallback
path, exercised automatically if the asset is ever removed). A new
semi-transparent `TargetStage` contrast band sits behind the target row
regardless of which background is active, guaranteeing label/target
readability against the now-busy illustrated scene. Each outlaw target now
has a small ground-shadow ellipse at its feet. Sheriff Implacable is no
longer a portrait card — see "Western actor hierarchy" and "Aim/fire/impact
sequence" below for the replacement in-world staging. The four targets,
reticle, muzzle flash (now anchored to Sheriff, not the reticle), and dust
puff are otherwise unchanged (no target art exists yet).

### Western actor hierarchy

```
WesternShootout (root)
  Background                  — illustrated Image (or procedural fallback)
  TargetStage                 — contrast band behind the target row
  Tumbleweed                  — 5-twig procedural ball, intro-only (C8.1d.1)
  SheriffActor                — anchored lower-left (0.04, 0), grounded, 140×200
    GroundShadow               — soft ellipse at Sheriff's feet
    Sprite                     — textured Image, Idle/Aim/Fire sprite-swap
    MuzzleAnchor               — anchor at (1, 0.62) of the sprite
  Concept                      — accounting concept label
  WesternTarget0..3            — outlaw buttons, invisible chrome, 240×320 (C8.1d.1)
    GroundShadow, Sprite (Neutral/Hit swap), DustPuff, Label
  Reticle                      — player-targeting crosshair (unchanged)
```

(The procedural fallback path — `Body`/`Head`/`Hat`/`Badge`/`Gun` under
`SheriffActor`, `Head`/`Bandana`/`Hat` under each outlaw — still exists in
code and is exercised automatically if a sprite file is ever missing, but
does not build at all while every production sprite loads successfully, as
is the case now.)

This matches the brief's suggested shape (`SheriffActor` with a `Sprite` +
`MuzzleAnchor`, `OutlawSlots`, `Labels`, `Reticle`) closely enough that no
generic actor framework was introduced — everything is local to
`WesternShootoutPresenter`, as instructed.

### Sheriff staging

`SheriffActor` stands at anchor `(0.04, 0)` (screen-left third, grounded at
the bottom of the stage, same `y = 140` ground line as the outlaws — see
the C8.1d.1 visual correction above), sized `140×200` — clear of the
concept text (top), the timer (HUD-reserved band above the stage), and the
target row (a ~18px gap to the nearest outlaw at these sizes, computed to
avoid overlap now that both actors are substantially larger).
`RuntimeUIFactory.LoadArt`
now successfully finds `Art/Gold/Western/Actors/Sheriff_{Idle,Aim,Fire}`, so
the actor is a single `Sprite` Image with a `MuzzleAnchor` child at its
approximate hand height/right edge (`(1, 0.62)` of the sprite rect — not
authored per-pixel muzzle metadata, since the source art carries none; see
"Remaining visual risks"). The procedural placeholder (body/head/hat/badge/
gun-rotation rig) is unchanged in code but no longer builds, since Sheriff's
own art always loads. A soft ground-shadow ellipse sits at Sheriff's feet
(and at each outlaw's feet) to kill the "floating sticker" read.

### Outlaw staging

Each `WesternTarget{0..3}` button now hosts a full-body `Sprite` Image
(Neutral/Hit swap) instead of the procedural head/face/bandana/hat — the
button itself grew from 190×100 (C8.1d) to 190×220 (first C8.1d production
pass) to **240×320** (C8.1d.1's size correction), pivoted at its own bottom
edge so it still stands on the same ground line, to fit a substantially
larger full-body character. Its own background `Image` is now fully
transparent (C8.1d.1 — see "Target visual correction" above); it remains
the whole clickable/selectable target, so keyboard and mouse selection are
unchanged. The accounting-option label was re-anchored to sit just below
each outlaw's feet rather than inside the old fixed-height button. The four
outlaws' actual relative proportions (A tall/thin, B broad, C compact, D
very tall/crooked) come straight from the source art — using one shared
bounding box with `preserveAspect` naturally preserves
each one's own silhouette rather than normalizing them to a uniform shape.

### Aim/fire/impact sequence

`RenderDecision` (called every frame during Decision) does three things
with the current EventSystem selection: moves the reticle (unchanged),
sets Sheriff's pose to Aim (`SetSheriffPose` — a sprite-swap, safe to call
every frame since it's a plain reference assignment, no coroutine), and
calls `AimSheriffAt(selectedRect)`. On the art path (now always active,
since Sheriff's sprites load) the fixed Aim sprite already points at the
target row per the brief's own note (section 11), so `AimSheriffAt` is a
no-op there; the clamped (`±30°`) placeholder gun-rotation logic is
unchanged in code for the fallback path.

`RevealOutcome` starts a `FireSequenceRoutine` coroutine:

1. Lock final aim toward the selected outlaw (Aim pose, no-op if timed out).
2. `WaitForSeconds(0.08f)` — anticipation beat (brief's 50-100ms range).
3. If a target was actually selected (not a timeout): swap Sheriff to the
   Fire sprite, flash the muzzle (`LocalMotionFx.FlashColor`, 0.14s)
   **from `MuzzleAnchor`** — a child of the sprite, so it always follows
   Sheriff regardless of pose, never a hardcoded screen coordinate — and a
   small recoil punch (`LocalMotionFx.Punch`, 0.18s, 1.05×) on the whole
   `SheriffActor`.
4. Reveal the outlaw states: the correct outlaw always turns
   `Theme.Correct`; if the player's selection *was* the correct one it also
   swaps to its Hit sprite, gets the existing hit punch, and the dust puff;
   the selected-but-wrong outlaw turns `Theme.Incorrect`, also swaps to its
   Hit sprite (it was still shot — it was just the wrong one), and shakes.
   Every non-selected outlaw stays on Neutral. **Sheriff fires at whoever
   was selected regardless of correctness** — the brief's "Sheriff shoots
   the selected outlaw," not "the game validates an answer first."
5. Timeout (`selectedIndex < 0`): no aim/fire/recoil at all, no outlaw
   swaps to Hit, every active outlaw gets the existing small shared flinch.
6. `WaitForSeconds(0.15f)`, then Sheriff settles back to Idle — he is never
   left stuck in the Fire pose (brief section 10). Outlaws stay on
   whichever sprite Reveal left them at (Hit for the selected one) through
   the rest of the feedback dwell — they only reset to Neutral the next
   time `ShowChallenge` runs, so the Hit pose reads clearly during
   feedback but never persists into a future microgame.

Total added latency before outcome is visible: ~0.08s anticipation + the
existing 0.14–0.24s per-element animations, comfortably inside the brief's
0.25–0.5s target window and unrelated to the director's own feedback-phase
timing (untouched).

**TV Game Show** — the curtain/floor/stage-light primitives are replaced by
the illustrated stage background when present (kept as the fallback
otherwise). The procedural presenter body/head/arm is replaced entirely by
a Presentador portrait card; the whole card takes the "arm raised" punch
reaction on a correct answer (there is no separate arm to animate once the
presenter is one illustration). The contestant, prize board, confetti, and
buttons are unchanged (no contestant art exists yet).

**Detective Lineup** — the procedural detective head/hat/magnifying-glass is
replaced by an Auditor Severo portrait card (candidate **#1**, per the
user's explicit preference) with a small, restrained punch on a correct
accusation — no sprite-swap reaction, since only one Auditor pose exists.
The interrogation-room background, height-wall, spotlight, badge, and all
four suspects are unchanged (no background or suspect art exists yet).

**Balance Machine** — unchanged. No candidate art exists for this world.

## E. Asset import settings

Every new texture was imported as: Texture Type **Sprite (2D and UI)**,
Sprite Mode **Single**, no mipmaps, filter **Bilinear**, wrap **Clamp**,
**Uncompressed** (`textureCompression: 0`) on the Default platform, max
texture size 2048 (comfortably above every source image's largest
dimension, 1456/1232px — nothing is downscaled), `alphaIsTransparency`
enabled. Uncompressed was chosen deliberately for this validation phase to
keep every character/background asset at full source fidelity while the
direction is still being judged visually; this should be revisited (likely
Compressed/High Quality) before a real ship build once VRAM budget matters.
Verified by running the project through Unity 6000.3.23f1 in batch mode —
every asset re-serialized cleanly to the editor's own importer schema with
the intended settings intact (see "Test results" below).

**Sprite import rule note (per the brief's section 11):** the Sheriff/
Auditor/Presentador source images are full-body character portraits on
their own opaque gradient studio backdrop — reporting this explicitly
rather than silently faking transparency. A pixel-level cutout was not
attempted: the backdrop is a smooth gradient with anti-aliased blending at
hair/hand edges, and any automated chroma-key there would produce visible
halos passed off as a clean cutout, which the brief explicitly forbids.
The first pass presented every character portrait through a new
`RuntimeUIFactory.CreatePortraitFrame` helper — a bordered rounded-rect
"character card" containing the portrait, aspect-preserved — which is
honest about what the source art actually is, but which manual validation
then rejected for *active gameplay* composition (see "Manual Validation
Correction" at the top of this document). It remains the right call for
Game Show's Presentador and Detective's Auditor as of this document (not
yet re-validated), and remains available for any future portrait/card-shaped
mechanic; Western's Sheriff no longer uses it (see "Western actor
hierarchy").

## F. Background vs. character layering

World backgrounds (Western, Game Show) are a single full-bleed
`Image` at the back of each presenter's root — no interactive element is
baked into them. Game Show's Presentador and Detective's Auditor remain
portrait-card characters (separate, later-sibling objects layered above the
background and below gameplay labels/targets/HUD) as of this document.
Western's Sheriff is now a true in-world actor at the same layering depth
as the outlaw targets, not a card above them — see "Western actor
hierarchy" for the corrected model.

## G. Asset mapping table

| World | Role | Source asset (`ArtBible/Candidates/...`) | Unity asset path | Presenter usage | Status |
|---|---|---|---|---|---|
| Western | Background | `Worlds/Western_Hybrid_Candidate_01.png` | `Assets/Hermit/Content/Resources/Art/Gold/Western/WesternBackground.png` | Full-bleed background `Image` | INTEGRATED |
| Western | Sheriff Idle | `Sheriff/Gameplay/Sheriff_Idle_Candidate_01.png` | `.../Western/Actors/Sheriff_Idle.png` | `SheriffActor`/`Sprite`, default pose | INTEGRATED |
| Western | Sheriff Aim | `Sheriff/Gameplay/Sheriff_Aim_Candidate_01.png` | `.../Western/Actors/Sheriff_Aim.png` | Sprite-swap while a target is selected | INTEGRATED |
| Western | Sheriff Fire | `Sheriff/Gameplay/Sheriff_Fire_Candidate_01.png` | `.../Western/Actors/Sheriff_Fire.png` | Sprite-swap during the fire beat | INTEGRATED |
| Western | Outlaw A Neutral/Hit | `Western/Outlaws/Outlaw_A_{Neutral,Hit}_Candidate_01.png` | `.../Western/Actors/Outlaw_A_{Neutral,Hit}.png` | `WesternTarget0`/`Sprite`, Neutral/Hit swap | INTEGRATED |
| Western | Outlaw B Neutral/Hit | `Western/Outlaws/Outlaw_B_{Neutral,Hit}_Candidate_01.png` | `.../Western/Actors/Outlaw_B_{Neutral,Hit}.png` | `WesternTarget1`/`Sprite`, Neutral/Hit swap | INTEGRATED (Neutral source had a non-standard background — see "Western Production Sprite Integration") |
| Western | Outlaw C Neutral/Hit | `Western/Outlaws/Outlaw_C_{Neutral,Hit}_Candidate_01.png` | `.../Western/Actors/Outlaw_C_{Neutral,Hit}.png` | `WesternTarget2`/`Sprite`, Neutral/Hit swap | INTEGRATED |
| Western | Outlaw D Neutral/Hit | `Western/Outlaws/Outlaw_D_{Neutral,Hit}_Candidate_01.png` | `.../Western/Actors/Outlaw_D_{Neutral,Hit}.png` | `WesternTarget3`/`Sprite`, Neutral/Hit swap | INTEGRATED |
| Western | *(superseded)* Sheriff opaque portraits | `Sheriff/Sheriff_{Neutral,Confident,Suspicious}_Candidate_01.png` | *(removed from Unity Resources this pass — reference copies remain only under `ArtBible/Candidates/Sheriff/`)* | No longer referenced by any presenter code | REFERENCE ONLY (not gameplay art) |
| Game Show | Background | `Worlds/GameShow_Hybrid_Candidate_01.png` | `Assets/Hermit/Content/Resources/Art/Gold/GameShow/GameShowBackground.png` | Full-bleed background `Image` | INTEGRATED |
| Game Show | Presentador Estelar | `Presentador/Presentador_Hybrid_Candidate_01.png` | `.../GameShow/Presentador.png` | Portrait card replacing procedural body/head/arm | INTEGRATED |
| Game Show | Contestant | — (none exists) | — | Procedural `CharacterPrimitives`, unchanged | MISSING — TODO |
| Detective | Auditor Severo | `Auditor/Auditor_Hybrid_Candidate_01 = opción #1.png` (user-selected #1, not alternate #3) | `Assets/Hermit/Content/Resources/Art/Gold/Detective/Auditor.png` | Portrait card replacing procedural head/hat/glass | INTEGRATED |
| Detective | Background | — (none exists) | — | Procedural wall/height-lines/glow, unchanged | MISSING — TODO |
| Detective | Suspects ×4 | — (none exists) | — | Procedural `CharacterPrimitives`, unchanged | MISSING — TODO |
| Balance | Machine / operator | — (none exists) | — | Procedural, entirely unchanged this phase | MISSING — TODO |

## H. Testing

All pre-existing automated tests are preserved unmodified in behavior.
`ClasicoPlayModeTests.cs` (shared helpers `FindImageUnder`/
`HasAncestorNamed`/`CycleUntilArchetype` from the first pass are reused):

- `WesternShootout_ShowsIllustratedBackground_AndInWorldSheriffActor` —
  replaces the old portrait-card test. Asserts: the background sprite is
  non-null; a `SheriffActor` RectTransform exists; **no** `SheriffCameo`/
  `PortraitFrame` object exists anywhere under `WesternShootout` (the
  rejected pattern is verifiably gone, not just unused); `SheriffActor` owns
  a `MuzzleAnchor`; all four `WesternTarget{0..3}` outlaw slots exist.
- `WesternShootout_FireSequence_RestoresSheriffTransform` — drives a real
  answer through `AnswerCurrentMicrogame` and asserts `SheriffActor`'s
  `localScale` returns to `Vector3.one` after the fire-sequence recoil
  punch, the same restore-contract every other `LocalMotionFx` reaction in
  this codebase is held to.
- `GameShow_ShowsIllustratedBackgroundAndPresentadorCard` and
  `DetectiveLineup_ShowsAuditorCard` — **unchanged**, since Game Show and
  Detective were not touched this pass (still using the visually-rejected
  but untouched-per-scope portrait-card pattern).

Production sprite pass adds four more tests to the same file:

- `WesternShootout_ProductionSpritesLoad_AndReplacePlaceholderVisuals` —
  Sheriff's and every outlaw's `Sprite` Image has a non-null sprite, and
  the procedural placeholder parts (`Body`/`Hat`/`Badge` on Sheriff,
  `Head`/`Hat`/`Bandana` on each outlaw) no longer exist anywhere in the
  hierarchy — i.e. real art actually *replaced* the placeholder, it isn't
  just layered on top of it.
- `WesternShootout_SheriffPose_IdleDuringIntro_AimDuringDecision_RestoresAfterFire`
  — Sheriff is already in the Aim pose once Decision is under way
  (`Resources.Load` reference-equality against the cached Aim sprite), and
  is back to Idle a short, precisely-bounded 0.5s after firing (see the
  "overshoot" note below — deliberately *not* the generic
  `AnswerCurrentMicrogame` wait here).
- `WesternShootout_SelectedOutlawSwitchesToHit_OthersStayNeutral` — answers
  a real Western round directly (click + a short 0.5s wait, same reasoning),
  then checks every outlaw's `Sprite.sprite` by reference equality against a
  fresh `Resources.Load` of its own Neutral/Hit art: the selected outlaw
  must be on Hit, every other outlaw must still be on Neutral.
- `WesternShootout_NextRoundWithinEncounter_RestoresEveryOutlawToNeutral` —
  renamed from the production-sprite pass's `..._NextChallenge_...`: under
  the C8.1d.1 Encounter model Western never reappears later in the same
  session (it's exactly one 3-round block), so "the next challenge" is now
  deterministically round 2 of the *same* Encounter rather than a
  probabilistic later re-draw — this test no longer needs an `Inconclusive`
  escape hatch.

**A real bug this phase's own tests caught in themselves ("overshoot"):**
the two tests above originally reused the generic `AnswerCurrentMicrogame`
helper (sized, suite-wide, for the *worst case* of landing on a fresh
Encounter's ~2.2s intro — see below). For a Western round that is *not* the
last of its Encounter, that wait is now far longer than the round's own
≈1.0-1.35s Lock+Feedback+next-round-Intro dwell, so by the time the
assertion ran, `ShowChallenge` for round 2 had *already* reset Sheriff to
Idle and every outlaw to Neutral — the tests were failing correctly, just
about the wrong round. Fixed by clicking the correct target directly and
waiting a short, precisely-bounded 0.5s (comfortably past the fire
sequence's own ≈0.23-0.28s settle, comfortably before round 2 begins).

C8.1d.1 adds two more Western PlayMode tests plus a new Encounter-focused
group, all in the same file:

- `WesternTarget_ButtonChrome_IsVisuallyTransparent` — every
  `WesternTarget{0..3}` button's own `Image.color.a` is exactly 0.
- `WesternOutlaw_Sprite_IsSubstantiallyLargerThanThePreviousCardPresentation`
  — the outlaw `Sprite` RectTransform's height exceeds 300 units (up from
  the rejected pass's 220).
- `WesternEncounter_WorldStaysActive_AcrossThreeConsecutiveRounds` — walks
  all 3 rounds of the Encounter, asserting `WesternShootout`'s root
  GameObject is `activeInHierarchy` before every round and immediately
  after every *continuation* round (never asserted after round 3, where
  transitioning away to a different world is exactly correct) — plus that
  all 3 rounds show a different concept label (the reset actually
  refreshed content, not just the sprite states).
- `WesternEncounter_EndsAfterThreeRounds_NextMicrogameIsADifferentArchetype`
  — after round 3, whatever comes next is never AimSelect again (only one
  Western block exists per session).

Five new EditMode tests in `ClasicoSessionDirectorTests.cs` (all pass
`westernEncounterRoundCount: 3` explicitly — every test above that point in
the file keeps the back-compatible default of 1, fully unaffected):
`WesternEncounter_GroupsExactlyThreeConsecutiveRounds_NoOtherArchetypeInterleaved`,
`WesternEncounter_NeverRepeatsTheSameChallenge_AcrossItsThreeRounds`,
`WesternEncounter_FirstRoundIntro_IsLonger_ThanContinuationRoundTransitions`,
`WesternEncounter_IntroDoesNotConsumeTheDecisionTimer`,
`WesternEncounter_EndingWithinASingleThreeRoundSession_ScoresAndFinishesNormally`.

These reuse the existing drive-the-real-UI approach (`LaunchClasico`/
`AnswerCurrentMicrogame`, or a direct click for the two time-sensitive cases
above) rather than any new scene or pixel comparison, per the brief's "no
brittle screenshot-pixel tests" instruction. The sprite-identity checks use
`Resources.Load` reference-equality (Unity caches loaded assets per path)
rather than polling animation timing.

### A second, suite-wide timing fix this phase's own full-suite run caught

Several *pre-existing* PlayMode tests (`ClasicoPlayModeTests.LaunchClasico`/
`AnswerCurrentMicrogame`, `ShellPlayModeTests`'s equivalents,
`ShellRealSceneCompositionTests`'s decision-timeout test) hardcoded waits
like "3.8s = 3s countdown + 0.6s Intro" or "2.2s = Lock+Feedback+next
Intro" — correct under the old rhythm, too short now that the *first*
microgame of a session (or the round right after any Western round) can be
round 1 of a fresh ~2.2s Western Encounter intro instead of an ordinary
~0.6s command beat. Running the full suite (not just the new tests in
isolation) surfaced this as real failures — an input click silently
ignored because the director was still in Intro, not Decision yet
(`SubmitSelection` no-ops outside `Phase.Decision`), which then read as
"AnswerCurrentMicrogame(false) must always register as incorrect" failing,
plus a full-scene decision-timeout test sampling its "resting" baseline
while the tumbleweed was still mid-crossing. All such waits were bumped to
cover the genuine worst case (`3.8f`→`5.5f`, `2.2f`→`4.2f`, `3.7f`→`5.5f`)
rather than narrowly patched — this is a real, if unglamorous, correctness
fix these existing tests earned by actually being run at full-suite scale
before this phase was reported done.

### Test results

Run via `unity test <project> --mode <EditMode|PlayMode>` (Unity
6000.3.23f1, the project's own editor version, in `-nographics` batch mode):

- **EditMode: 108/108 passed, 0 failed** (103 pre-existing + 5 new Western
  Encounter tests).
- **PlayMode: 39/39 passed, 0 failed** (35 pre-existing + 4 new Western
  Encounter/visual-correction tests) — on the *third* full-suite run this
  phase, at a 700s budget (487.9s actual). The first full run (2 genuine
  bugs, described above) and second (2 different, timing-overshoot bugs in
  this phase's own new tests, also above) both surfaced real problems that
  an isolated single-test run would not have — one seemingly-hung test
  passed cleanly in 32s in isolation, confirming that specific failure was
  a full-suite-only timing interaction (the suite's cumulative wall-clock
  duration grew once every Western-adjacent wait had to cover the new,
  longer worst case), not a logic bug in that test. The suite is
  meaningfully slower now (unavoidable — the worst-case wait genuinely grew
  from ~0.6s to ~2.2s for any round that might open a fresh Encounter) but
  passes cleanly and repeatably at a realistic timeout budget.

## I. Manual validation status

**Not yet performed.** Per the brief's stop condition, this phase does not
claim FINAL/COMPLETE until the user has manually played all four Gold
worlds. See the checklist and next steps below.

## J. Remaining visual risks

- **Western target-row contrast**: the new `TargetStage` band is a flat
  35%-alpha dark strip; worth a manual look at whether it reads as
  intentional "stage" dressing or as a visible seam against the desert
  ground art, now that real outlaw art sits in front of it.
- **Sheriff's Aim art is fixed, not rotated per target** — the brief
  explicitly allows this (section 11: "do not mirror... preserve the
  actual sprite orientation... small rotation if needed") and the single
  Aim pose already points at the target row, but it does not subtly re-aim
  toward whichever specific outlaw (leftmost vs. rightmost) is selected the
  way the placeholder gun-rotation rig did. Worth a manual look at whether
  this reads as "close enough" or whether a small added rotation is worth
  a follow-up.
- **MuzzleAnchor position is an estimate** (`(1, 0.62)` of the sprite rect),
  not authored per-pixel hand/gun coordinates from the art — first thing to
  check in manual play: does the muzzle flash appear to originate from the
  revolver, or noticeably offset from it?
- **Outlaw A/C/D's baked-in ground shadow** is real, intentional artist
  paint (confirmed by pixel sampling, not a chroma-key artifact) sitting
  alongside the engine's own procedural `GroundShadow` ellipse — these
  could visually double up under one outlaw's feet. Worth a manual look;
  if it reads as redundant, the cheapest fix is hiding the procedural
  ellipse only for outlaw slots using real art (Sheriff and Outlaw B have
  no such baked shadow, so they'd keep the procedural one).
- **`Outlaw_B_Neutral`'s source background was non-standard** (a tan
  gradient, not magenta) — the automated per-image pipeline handled it
  cleanly and a manual face/hands crop showed no damage, but this is the
  one file that didn't go through the "expected" path; worth a closer look
  in manual play specifically at Outlaw B.
- **Presentador/Auditor card proportions** (Game Show/Detective, untouched
  this pass, still portrait cards pending their own correction round): both
  cards crop a tall (928×1232, ~0.75 aspect) portrait into a card sized by
  eye with `preserveAspect` on, so some empty framing space inside the card
  is expected and by design.
- **No real-frame visual proof yet**: batch-mode testing runs `-nographics`
  (no GPU rendering), so the passing tests confirm structure (non-null
  sprites, correct sprite swaps, transform restoration, gameplay flow) but
  not actual on-screen composition/legibility/alpha-edge quality — that is
  exactly what the manual validation gate below is for.
- **Tumbleweed silhouette is very abstract** — five overlapping thin rounded
  bars fanned around a center point, not a "real" tangled-twig shape. It
  reads as *something* crossing the scene procedurally, which may or may not
  read specifically as "tumbleweed" without seeing it move; first thing to
  judge in manual play (brief section 10 explicitly allowed a simple
  procedural form, not final art).
- **The Western sting's exact tone/timing is a first pass**, never
  playtested by ear — three separate synthesized cues (thump/sweep/dust)
  fired in quick succession could read as one cohesive "western" motif or
  as three disconnected blips; volume levels (0.10-0.26) were picked to sit
  under the existing correct/incorrect/transition cues, not tuned against
  them side by side.
- **The outro is an extended Feedback dwell, not a distinct beat** — by
  design (brief section 15 allows "keep it simple"), so there is no visual
  cue marking "now the encounter is ending" beyond the round's own reveal
  holding a little longer than usual; worth judging in manual play whether
  this reads as "a satisfying settle" or "the round is just slow."
- **Encounter-intro timing was sized by formula, not by feel** — 2.2s
  intro / 0.35s round-transition / 0.7s outro extension are the brief's own
  suggested ranges, not yet validated against how the sting+tumbleweed
  actually look/sound together at real frame rate.

## K. Manual validation checklist

Launch the project in the Editor (or a build) and play Clásico until each
of the four Gold worlds has appeared at least once (a full session is 9
microgames, which the automated tests confirm always covers all four).

**WESTERN — visual**
- [ ] Instantly reads as Western (illustrated main-street background).
- [ ] Sheriff and all four outlaws look like illustrated characters, not
      polygonal/procedural shapes — the placeholder era is over.
- [ ] No magenta halo, no visible cutout fringe, no "sticker pasted on
      background" look on Sheriff or any outlaw.
- [ ] **No rectangular "answer card" behind any outlaw** — the character
      sprite itself is the target, no visible dark panel edges around it
      (the specific thing being re-validated after this correction).
- [ ] **Outlaws read as substantially bigger** than before, and not
      pixelated/noisy on their fine linework (hat brims, thin outlines) —
      the specific thing the pixelation investigation targeted.
- [ ] Sheriff and all four outlaws stand on one coherent, shared ground
      line — nobody looks like they're floating or standing at a different
      depth than the others.
- [ ] The four outlaws are visually distinct from each other (A tall/thin,
      B broad, C compact, D very tall) and don't overlap each other or
      Sheriff.
- [ ] Sheriff's Aim pose reads as aiming toward the target row (even though
      it doesn't rotate per specific outlaw — see "Remaining visual risks").
- [ ] Firing reads as one clear beat: aim → brief pause → muzzle flash
      (originating near Sheriff's gun, not a fixed screen point) → recoil →
      the selected outlaw switches to its Hit pose.
- [ ] Only the selected outlaw reacts (Hit); the other three stay Neutral.
      Correct/incorrect reads clearly from the sprite's own subtle tint
      (or, for a fallback outlaw, its face) — not from a colored panel.
- [ ] Target labels stay readable against the `TargetStage` band and
      against each outlaw's own art.
- [ ] Outlaw A/C/D's baked-in ground shadow doesn't look doubled/odd next
      to the procedural shadow ellipse (see "Remaining visual risks").
- [ ] Keyboard navigation and mouse clicks both still select/target outlaws
      correctly even though their buttons are now invisible.

**WESTERN — Encounter (intro once, 3 rounds, outro)**
- [ ] On first arriving at Western, a short (~2-3s) face-off intro plays:
      background, a short western-ish sting, a tumbleweed crossing the
      scene, then gameplay begins.
- [ ] The intro plays **once** — rounds 2 and 3 reset quickly (labels,
      Sheriff, outlaws) with **no** replayed sting/tumbleweed/face-off.
- [ ] The world never blacks out or fades between rounds 1→2→3 — the same
      background/Sheriff/outlaws stay on screen the whole time.
- [ ] All three rounds feel like one continuous Western sequence, not three
      separate unrelated microgames.
- [ ] After round 3, a brief settle/outro beat plays, then the normal
      transition cut into the next world — Western does not return later in
      the same session.
- [ ] The tumbleweed doesn't block any label, the timer, or a target.
- [ ] The sting doesn't feel jarring, too loud, or too quiet against the
      existing correct/incorrect/transition cues.

**GAME SHOW**
- [ ] Instantly reads as TV Game Show (illustrated stage background).
- [ ] Presentador's portrait card feels like a character, not a UI box.
- [ ] Contestant (still procedural) remains readable next to the new card.
- [ ] Answer zones (VERDADERO/FALSO) stay clear.
- [ ] Presentador card punches on a correct answer and restores cleanly.

**DETECTIVE**
- [ ] Instantly reads as lineup/investigation.
- [ ] Auditor's portrait card (candidate #1) is recognizable and doesn't
      crowd the suspect row.
- [ ] Suspects (still procedural) remain distinguishable from each other.
- [ ] Correct suspect readable in ~1 second; spotlight/badge feedback works.

**BALANCE**
- [ ] Unchanged from before this phase — confirm no regression only.

**GLOBAL**
- [ ] Timer never overlaps world content (`TopHudReservedHeight`).
- [ ] No black screen, no oversized overlays, transitions stay clean
      (`TransitionOverlay` `CanvasGroup.alpha` returns to 0).
- [ ] Keyboard and mouse both still work for every world.
- [ ] Results and Retry both still work; Shell flow still works.

## L. Production asset checklist — Western

**Status: FULFILLED.** All 11 files below were generated externally by the
user and delivered under `ArtBible/Candidates/Sheriff/Gameplay/` and
`ArtBible/Candidates/Western/Outlaws/`; this phase turned them into the
transparent production sprites listed in "Western Production Sprite
Integration" above. Kept verbatim below for the historical record of what
was requested vs. delivered. Every delivered production file meets:

- have a **transparent background** (real alpha, not a color-keyed opaque
  backdrop like the existing reference portraits);
- preserve the character's **full silhouette** (no cropping of hat, gun
  arm, boots, etc.);
- contain **no UI, no card/frame, no baked-in text** (unless intentionally
  part of a costume element, e.g. a badge engraving);
- be generated at a resolution comfortable for 1080p gameplay at roughly
  the on-screen size these actors render at (Sheriff 140×200 UI units,
  outlaws 240×320 UI units as of the C8.1d.1 size correction — see "Western
  Target Visual Correction" above) — a source image on the order of 1024px
  on the long edge is comfortably oversized for that and leaves headroom.

**Sheriff Implacable:** `Sheriff_Idle.png` (resting stance) / `Sheriff_Aim.png`
(one aim pose, arm/gun raised toward the target row) / `Sheriff_Fire.png`
(muzzle-flash-adjacent recoil pose) — all three delivered and integrated.
`Sheriff_Reaction.png` (the C8.1c brief's "one raised eyebrow"/"sharp nod"
beat) was optional and **not delivered** — not required for this phase to
read correctly; still open for a future pass.

**Outlaws — four visual archetypes:** `Outlaw_{A,B,C,D}_Neutral.png` /
`Outlaw_{A,B,C,D}_Hit.png` — all 8 delivered and integrated. No optional
per-archetype Correct/Wrong variants beyond Neutral/Hit were requested or
delivered this pass (the existing color-tint + shake/punch language covers
the correct/incorrect distinction, per "Aim/fire/impact sequence" above).

**Where the accounting label should live (future, not this phase):** the
brief asks that the architecture *allow* the label to visually belong to
the outlaw later (a vest patch, a wanted-poster tag, a belt plate, a ground
placard) without locking one in now. Nothing in the current `WesternTarget`
button structure prevents this — the label `Text` is already a distinct
child that can be re-anchored onto a specific costume region once a final
outlaw design exists; no architecture change is required to support it.
Still not done this pass, by design.

Resource paths the presenter checks (all 11 now present under
`Assets/Hermit/Content/Resources/Art/Gold/Western/Actors/`):
`Art/Gold/Western/Actors/Sheriff_Idle`, `..._Aim`, `..._Fire`, and
`Art/Gold/Western/Actors/Outlaw_{A,B,C,D}_{Neutral,Hit}`.

## M. Next manual test steps

1. Open the project in Unity 6000.3.23f1 (already the installed/matching
   editor version) — this re-imports the 11 actor textures with mipmaps
   now enabled; confirm each Inspector still shows Texture Type "Sprite
   (2D and UI)" with no import errors/warnings.
2. Enter Play Mode from the Shell scene, launch Clásico, and let it run
   until Western comes up. Watch the **intro** first: background, sting,
   tumbleweed crossing, then gameplay — confirm it plays once and reads as
   a coherent ~2-3s beat, not too long/annoying.
3. Play all **3 rounds** of the Western Encounter back to back. Confirm:
   no card behind any outlaw, outlaws read as clearly bigger/less noisy
   than before, the world never fades/blacks out between rounds, each
   round shows a different accounting concept, and the intro does **not**
   replay for rounds 2/3.
4. After round 3, confirm a brief settle happens before the normal
   transition into the next world, and that Western does not come back
   later in the same 9-microgame session.
5. Confirm Game Show, Detective, and Balance are visually and functionally
   unchanged from the previous validation pass (none were touched this
   round).
6. Report back against the checklist above — in particular the newly
   flagged risks: does the tumbleweed read as a tumbleweed, does the sting
   sit well against the other audio cues, does the extended final-round
   feedback feel like an intentional settle or just a slow round, and does
   Outlaw A/C/D's baked shadow look doubled up with the procedural one.
   This phase stays IN PROGRESS until that pass comes back clean.

## C8.1d.5 — Cinematic Suspense Timing + Outlaw Render Investigation

### Manual validation result (this phase's starting point)

The C8.1d.4 cinematic intro was validated as a real success: the Sheriff/
Outlaw/hand close-ups read correctly, the cinematic concept works, and it
stays in the game. Two new notes came back: (1) the cinematic feels
slightly too fast — more suspense is wanted before the gunshot; (2) the
Candidate 02 gameplay outlaws look "somewhat pixelated / rough-edged" in
the real Windows executable compared with expectations. This phase does not
redesign anything, regenerate art, alter the 3-round Encounter architecture,
or touch other worlds — it re-paces the existing cinematic and investigates
the render-quality report empirically.

### Part A — Outlaw render-quality investigation

**Method.** No Unity Editor session was used to eyeball this — every number
below was measured directly from the shipped files: pixel dimensions and
opaque-bbox extents via a `System.Drawing`/`LockBits` scan (same tool class
this project's own Candidate 02 background-removal pass used, per
"Background-removal method" above), import settings read directly from each
`.meta`'s YAML, and on-screen size computed from the actual
`WesternShootoutPresenter`/`RuntimeUIFactory` layout code and `CanvasScaler`
configuration — not assumed.

**Per-sprite measurements (all four current production Neutral sprites,
`Assets/Hermit/Content/Resources/Art/Gold/Western/Actors/Outlaw_{A,B,C,D}_Neutral.png`):**

| Property | A | B | C | D |
|---|---|---|---|---|
| Sprite rect (canvas) dimensions | 350×820 | 350×820 | 350×820 | 350×820 |
| Opaque character bounding box | 312×791 | 331×790 | 325×791 | 323×789 |
| Pixels Per Unit | 100 (all four, `.meta`) | | | |
| Filter Mode | Bilinear (all four) | | | |
| Compression | Uncompressed (all four; `textureCompression: 0` on both DefaultTexturePlatform and Standalone) | | | |
| Max Texture Size | 2048 (all four; no import-time downscale — native 350×820 is well under the cap) | | | |
| Mipmaps | On (`enableMipMap: 1`), `mipMapsPreserveCoverage: 0` (all four) | | | |

**Canvas / layout (shared by all four, from `RuntimeUIFactory.CreateCanvas` and `WesternShootoutPresenter.Build`):**

- Canvas render mode: `RenderMode.ScreenSpaceOverlay`.
- `CanvasScaler`: `ScaleWithScreenSize`, reference resolution 1280×720,
  match 0.5 — since 1280:720 is exactly 16:9, both axes agree regardless of
  the match slider, so scale factor = actual resolution ÷ 1280.
- **At 1920×1080**: Canvas scale factor = **1.5×**.
- Outlaw `Sprite` child `RectTransform` size: 240×320 canvas units,
  `preserveAspect = true` (unchanged since C8.1d.1's target-size fix).
- Sprite aspect (350/820 ≈ 0.427) is narrower than the box aspect
  (240/320 = 0.75) → **height-constrained**: rendered sprite-rect height =
  320 canvas units, rendered width = 320 × 0.427 ≈ 136.6 canvas units.
- **RectTransform rendered dimensions at 1920×1080**: ≈204.9×480 physical
  screen px for the full sprite rect; the character's own opaque bbox
  (using each letter's measured bbox above, scaled by the same 480/820 ≈
  0.5854 factor) renders at approximately **A 182.6×463.0px, B 193.8×462.5px,
  C 190.3×463.0px, D 189.1×461.9px**.
- **Source-pixel-to-screen-pixel ratio**: 0.5854:1 for all four (identical,
  since all four share the same 350×820 canvas) — the sprite is drawn at
  **~58.5% of its native pixel size**, i.e. **minified (downscaled), not
  upscaled**, at the reference 1920×1080 resolution.
- **Mipmap level likely sampled**: minification factor 820/480 ≈ 1.708 →
  required mip level = log2(1.708) ≈ **0.77** — a fractional level between
  mip 0 (350×820, full res) and mip 1 (175×410, half res).

### Part A2 — what the user is actually seeing

Ruled out first, per the brief's explicit "do not assume upscaling":
**confirmed by the arithmetic above that the sprite is minified to ~58.5%
of native resolution, not upscaled** — options F (Canvas scaling) and G
(sprite stretching) are also ruled out: the `CanvasScaler` math is internally
consistent (1280:720 reference exactly matches 1920×1080's 16:9), and
`preserveAspect` is on with no non-uniform scale anywhere in the chain.

Checked directly against the source PNGs (not guessed): every fully
transparent pixel (alpha = 0) across a full-image scan of Outlaw A and B's
Neutral sprites has RGB exactly **(0, 0, 0)** — i.e. the Candidate 02
background-removal pipeline's own "zero the RGB of fully-transparent
pixels" step (see "Background-removal method" above) already ran, so there
is no leftover chroma-key color sitting in the alpha=0 regions for mipmap
generation to blend into visible edges. Partial-alpha edge pixels (the
anti-aliased silhouette boundary) average a warm brownish RGB consistent
with real character-edge colors, not a magenta/foreign fringe. **This rules
out option E (chroma-key edge contamination)** — the art prep already
engineered this failure mode out.

Import configuration matches this project's own established, previously-
validated convention for minified illustrated 2D sprites (mipmaps on +
Bilinear filtering — see "C8.1d.1 — Target Visual Correction + Pixelation
Investigation" above, where the *old* Candidate 01 family was found with
mipmaps **off** on a much more aggressive 0.27× minification, and turning
them on was the fix). The current Candidate 02 family already has mipmaps
on with the same Bilinear filter mode — the config change that fixed the
prior pixelation report is already in place here, at a substantially milder
minification ratio (0.585× now vs. 0.27× then).

**No clearly incorrect runtime/import configuration was found** — per the
brief's own instruction, no import setting was changed. The two remaining,
non-mutually-exclusive candidates are:

- **Option D — painterly rim-light texture perceived as pixelation.** The
  Candidate 02 family's thin painterly rim-light strokes (hat brim, thin
  legs, boot edges, coat hem — the exact features the brief calls out) are
  fine, high-frequency detail. At a required mip level of ~0.77, the GPU is
  sampling a blend point between full-res and half-res — no single mip level
  cleanly represents a 1-2px painted highlight stroke at this exact scale,
  so it can read as slightly broken/noisy rather than a smooth line. The
  same rim-light texture was already flagged as intentional in an earlier
  validation pass; this measurement is consistent with that same texture
  now being perceived differently once seen in the sharper real-executable
  render (see next point) rather than the Editor's own Game View.
- **Option A (mild) — genuine source-resolution/style limit relative to
  render scale.** At ~58.5% minification the source has real headroom (it
  is not being stretched past its own resolution), but "headroom" does not
  mean "detail-frequency-matched" — fine painted linework this thin will
  always show some minification softening/moiré regardless of import
  settings once the character's own on-screen height (~462-463px) is well
  under the source's ~790px of vertical character detail.
- **A known, already-documented factor, not re-litigated here**: this same
  document already records (in the C8.1d.4 section above) that "the
  Editor's Game View has been observed to render softer/distorted compared
  with the actual executable" — i.e. real-executable sharpness genuinely
  differs from what was checked in-Editor during earlier passes. This is
  consistent with the pixelation only being reported now, against the real
  .exe, rather than at any earlier in-Editor check.

### Part A3 — no art or settings modified

Per the brief, nothing was regenerated, upscaled, sharpened, blurred, or
reimported with different mip/filter/PPU settings this phase — every number
above came from reading existing files, not from experiments run against
them.

### Part A4 — optional diagnostic build comparison

**Not performed.** Building a side-by-side Bilinear-vs-Trilinear (or
mip-bias) comparison would require an actual Windows `.exe` build and a
manual screenshot-level comparison, which this investigation — a static
file/config/math analysis — cannot itself judge ("Editor Game View renders
softer than the real executable" is exactly the gap a build comparison
would need to cross). Recorded as a candidate for a **future**, explicitly
authorized pass rather than attempted speculatively now: switching Filter
Mode from Bilinear to Trilinear on the four outlaw sprites would let the GPU
blend smoothly across the ~0.77 fractional mip level measured above instead
of snapping to a single nearest mip — a one-line, fully reversible import
setting, not a "clearly incorrect" one, which is why it was not applied
under this phase's "do not touch settings unless clearly wrong" instruction.

### Part A — conclusion

**Import/runtime configuration is correct** and already reflects this
project's own established, previously-validated fix for minified
illustrated sprites (mipmaps + Bilinear). The measured render is a genuine
downscale (~58.5%), not upscaling, not canvas mis-scaling, and not sprite
stretching. No chroma-key contamination survives in the shipped files. The
most evidence-consistent explanation for "pixelated / rough-edged" is the
painterly rim-light/fine-linework detail (hat brim, thin legs, boot edges,
coat hem) falling at a scale where no single mip level represents it
cleanly, compounded by the already-documented Editor-vs-built-executable
sharpness gap. **Higher-resolution source art is not recommended as an
immediate fix** — the sprite is not resolution-starved at this render size
(0.585× minification is comfortably a downscale); a future pass could
instead evaluate Filter Mode (Trilinear) or a slightly heavier/softer
rim-light stroke width as cheaper, art-preserving levers, but neither was
applied here per this phase's explicit scope.

### Part B — cinematic suspense timing

**Manual validation:** the C8.1d.4 cinematic is visually successful and
should remain; it just plays too fast, and more suspense is wanted before
the gunshot. This phase re-paces the existing three close-up stills to a
~6.5s target with no new art.

**Before (C8.1d.4, ~4.11s theoretical):**

| Time | Shot |
|---|---|
| 0.00-0.75 | Establishing shot |
| 0.75-1.45 | Sheriff close-up |
| 1.45-2.15 | Outlaw close-up |
| 2.15-2.90 | Hand/holster close-up |
| 2.90-3.40 | Sheriff close-up (tension) |
| 3.40-3.80 | Outlaw close-up (short) |
| 3.80-4.05 | Tension pause |
| 4.05 | Gunshot |
| 4.05-~4.11 | Flash/cut |

**After (C8.1d.5, ~6.50s target — implemented in `WesternShootoutPresenter.CinematicIntroRoutine`):**

| Time | Shot | Audio |
|---|---|---|
| 0.00-1.00 | Establishing shot (lengthened tumbleweed crossing, 0.65s→0.85s) | wind fades in |
| 1.00-2.00 | Sheriff close-up, slow push-in | twang |
| 2.00-3.00 | Outlaw close-up, opposing push-in | tension note |
| 3.00-4.10 | Hand/holster close-up — the longest detail shot | low pulse |
| 4.10-4.90 | Sheriff close-up, slightly tighter push-in | two closer-spaced low pulses |
| 4.90-5.60 | Outlaw close-up, short response shot | (none new) |
| 5.60-6.20 | **Tension silence** — duel motif drops out | leather-creak cue, then a late pre-draw tick |
| 6.20 | Gunshot | gunshot + dust accent |
| 6.20-6.50 | Flash / cut | flash fades (~0.16s), held blank to 0.30s total |
| 6.50+ | Gameplay reveal | — |

Every shot's `PushInRoutine` end-scale was kept the same or reduced from its
C8.1d.4 value (never increased) per the brief's B3 — a 1.00s or 1.10s shot
moves *more slowly* than the old 0.70-0.75s version of the same shot did,
not more dramatically; motion stays barely perceptible.

### Music sync map

For a future final soundtrack authored around this cadence (no Suno/Stable
Audio integration this phase — procedural placeholders only, per B4):

- **Tension build:** 0.00-5.60 — establishing shot through the second
  Outlaw response shot; the existing sparse twang/tension-note/low-pulse
  cues mark the beat structure a real score would hit.
- **Tension-drop region: 5.60-6.20** — the duel motif should thin to
  near-silence here; only ambient wind, a tiny leather-creak accent, and a
  last-moment metallic pre-draw tick remain (implemented via `_creakClip`
  and the existing `_predrawClip`).
- **Gunshot: 6.20** — strong transient, scored as the cadence's release.
- **Flash/cut: 6.20-6.50** — brief, no music re-entry until gameplay.
- **Gameplay reveal: 6.50+** — fast rounds 2/3 resume the ordinary
  ~0.6s command-beat cadence unchanged.

### EncounterIntroSeconds

Raised from **5.0s → 7.5s** (`ClasicoGameDefinition._encounterIntroSeconds`).
Reasoning, mirroring the C8.1d.4 margin methodology rather than guessing:
the cinematic's own theoretical sum is ~6.50s; C8.1d.4 previously found a
~0.4s buffer (4.5s gate over ~4.11s routine) insufficient under real
per-frame `while`-loop overshoot, and a ~0.9s buffer (5.0s gate) sufficient.
This phase's routine chains the same *number* of sequential push-in
`while`-loops (five) as C8.1d.4's did — each loop's overshoot is bounded by
a fraction of one frame regardless of how long that particular shot runs —
so the same absolute buffer class was applied here rather than scaling the
buffer proportionally with the now-longer total: 6.50s + ~1.0s ≈ **7.5s**.
This was then verified empirically against the real PlayMode suite, not
just assumed — see "Test results" below.

### Round 2/3 and 3-round Encounter architecture

Unchanged. `ShowChallenge(isEncounterStart: false)` still never calls
`CinematicIntroRoutine`; `ClasicoEncounterPlan`, `ClasicoSessionDirector`'s
Encounter grouping, and `WesternEncounterRoundCount` (3) were not touched.

### Files changed this phase

- `Assets/Hermit/Runtime/GameFramework/Microgames/WesternShootoutPresenter.cs`
  — `CinematicIntroRoutine` re-timed to the ~6.5s table above;
  `GunshotFlashRoutine` extended to a ~0.30s flash+hold window;
  `TumbleweedRoutine`'s establishing-shot call lengthened to 0.85s;
  `BuildCinematicAudio` extended the wind bed to 6.6s and added
  `_creakClip`; doc-comments updated. No architecture, gating, or scoring
  code touched.
- `Assets/Hermit/Games/Clasico/ClasicoGameDefinition.cs` —
  `_encounterIntroSeconds` default raised 5.0f → 7.5f, doc-comment updated
  with the margin reasoning above.
- `Assets/Hermit/Tests/PlayMode/ClasicoPlayModeTests.cs`,
  `Assets/Hermit/Tests/PlayMode/ShellPlayModeTests.cs`,
  `Assets/Hermit/Tests/PlayMode/ShellRealSceneCompositionTests.cs` — every
  hardcoded wait sized against the old 5.0s gate (8.5f, 7.0f) bumped by the
  same +2.5s delta (→11.0f, 9.5f) to match the new 7.5s gate. No test's
  *assertion* logic changed — the existing `WesternCinematic_*` and
  `WesternEncounter_*` tests already drive frame-by-frame off director/
  presenter state (`IsIntroPhase`/`IsDecisionPhase`/sprite reference
  equality) with a `Time.realtimeSinceStartup` wall-clock deadline, never a
  fixed-length coarse wait for the cinematic's own internal timing — so
  they needed no rewrite, only the *launch*-side waits that get a session
  into a state to observe needed bumping.

### Two real test-timing bugs found and fixed along the way

Running the full PlayMode suite against the initial 7.5s gate (with every
hardcoded `WaitForSeconds` simply bumped by the same +2.5s delta the 5.0s→
7.5s change implied) surfaced 5 failures + 1 inconclusive — investigated
properly rather than assumed to be flakes, per this project's own
established practice:

1. **A genuine test-margin ceiling, not a Western bug.** `AnswerCurrentMicrogame`'s
   flat wait (previously 7.0s, bumped to 9.5s) is sized to safely clear the
   *worst case* — landing on round 1 of a fresh Western Encounter. But a
   flat wait that long can no longer stay *under* an ordinary Balance
   round's own real decision-timeout window
   (`FeedbackDisplaySeconds`(0.8) + `CommandBeatSeconds`(0.6) +
   `BalanceDecisionWindowSeconds`(6) = 7.4s) once the Western gate grew past
   ~5.9s — so an *untouched* Balance round could silently time out and
   auto-advance an extra, uncounted round during the same wait, corrupting
   every test built on that helper (`EveryArchetype_RendersDistinctInteractableControls_WhenItAppears`,
   `AtLeastThreeArchetypeSwitches_OccurWithinOneSession`,
   `IncorrectAnswer_TransitionsCleanly_IntoADifferentMicrogameType`,
   `WesternShootout_SelectedOutlawSwitchesToHit_OthersStayNeutral` via
   `CycleUntilArchetype`, and `LaunchingClasicoFromShell_StartsAGame`'s own
   equivalent wait). This is exactly the brief's own B6 warning ("avoid
   continuing the pattern of simply adding arbitrary seconds to
   WaitForSeconds") — a single flat constant cannot simultaneously be long
   enough for the slowest case and short enough to never overrun the
   fastest one, once the two are far enough apart. **Fixed** by replacing
   every such flat wait in `ClasicoPlayModeTests.cs`, `ShellPlayModeTests.cs`,
   and `ShellRealSceneCompositionTests.cs` with frame-by-frame polling
   against real director/scene state (a new shared `WaitUntil(condition,
   timeoutSeconds, message)` helper, generously bounded — 20s — as a loud
   safety net, never the normal exit path) — this permanently removes the
   whole class of "constant needs bumping again" bugs regardless of any
   future Western pacing change.
2. **A second, subtler bug the fix above introduced.** Once "wait for
   Decision" became "return the instant Decision content is visible," a
   `ShellRealSceneCompositionTests` fixture that captures a "baseline"
   `anchoredPosition` immediately after that wait could sometimes capture a
   *transient, still-animating* position: `WesternShootoutPresenter`'s
   round-1-only `OutlawSettleInRoutine` (a ~0.04s-per-target-staggered,
   ~0.2s entrance ease) starts on the exact same frame the concept/labels
   become visible, so polling on "content visible" alone can return before
   that settle-in finishes. An isolated re-run caught this directly
   (`WesternTarget0` "restored" to 140 from a captured baseline of 123.17 —
   itself a mid-animation value, not a real regression). **Fixed** with a
   small, fixed 0.5s settle margin *after* the content-visible wait and
   *before* capturing baseline — deliberately not sized against any Western/
   Encounter timing (so it can never reintroduce bug #1's cascade risk),
   only against the settle-in's own short, constant duration.

Both fixes were verified empirically, not assumed: each failing test was
re-run in isolation after its fix (single-fixture `-testFilter` runs) before
re-running the full suite.

### Test results

- **EditMode: 110/110 passed** (both before and after the test-timing
  fixes above — no EditMode test touches Western presentation internals or
  the changed PlayMode helpers).
- **PlayMode, full suite: 48/49 passed.** The one failure,
  `ShellRealSceneCompositionTests.RealShellScene_DecisionTimeoutOnActivePresenter_RestoresLocalReactionTransforms`
  (this run: `DetectiveLineup/Spotlight did not return to its resting
  anchoredPosition... 378.04 vs expected <1.5`), is the same **pre-existing,
  full-suite-load-only timing flake already documented earlier in this file**
  under C8.1d.2/.3 (there reported against `GameShow/Mouth`, different
  numbers, same test method, same "restores local reaction transform after
  a decision timeout" mechanism) — not a Western/cinematic regression.
  Confirmed, not assumed: re-run in isolation (`-testFilter
  ShellRealSceneCompositionTests`) **passed 2/2** on two separate isolated
  runs, and the full-suite failure reproduced with **byte-identical values**
  (baseline `(0.00, 0.00)`, now `(-321.00, -199.69)`, distance
  `378.044952f`) across two independent full-suite runs — exactly the
  "byte-identical across full-suite runs, clean in isolation" signature this
  project's own history already established for this test. Neither
  Detective nor Game Show was touched this phase, and per this project's own
  standing instruction not to chase that specific pre-existing flake, it was
  not investigated further here.

## C8.1d.6 — Western Final Music Candidate Integration

**Status: IMPLEMENTATION IN PROGRESS — MANUAL AUDIO VALIDATION REQUIRED.**

### Candidate selection

Two manually-generated Western duel music candidates were found saved
locally, exactly matching the brief's expected naming:

- `ArtBible/Candidates/Audio/Western/Western_DuelMusic_Candidate_01_DustAndSilence.mp3`
  — **"Dust & Silence"**, the chosen **primary** candidate. MPEG Layer III,
  VBR, 48kHz stereo, ~6s (Windows Shell metadata; the file's own late-track
  natural quiet already aligns closely with the cinematic's 5.60-6.20s
  tension-silence window purely by the track's own length/pacing, before
  any programmatic ducking is applied).
- `ArtBible/Candidates/Audio/Western/Western_DuelMusic_Alternate_01_DustyDuel.mp3`
  — **"Dusty Duel"**, retained as the **alternate**, same format, ~7s. **Not
  integrated this phase**, per the brief's explicit "do not integrate the
  alternate yet." Both ArtBible originals are untouched (read-only source of
  truth, never modified).

### Production copy

The primary candidate was copied **byte-for-byte** (verified via `cmp`) to
`Assets/Hermit/Content/Resources/Audio/Gold/Western/Western_DuelMusic_01.mp3`
— no re-encoding, no destructive edit, matching the same "production copy,
never overwrite the source" convention this project already uses for every
Gold art asset (see "Western Production Sprite Integration" above). Loaded
at runtime via a new `RuntimeUIFactory.LoadAudio(resourcePath)` helper
(`Resources/Audio/Gold/Western/Western_DuelMusic_01`), which mirrors
`LoadArt`'s exact contract — cached for the process lifetime, warns once and
returns null on a missing asset rather than throwing, so a missing/removed
music file degrades to "no music" without breaking the cinematic's own
sound design or gunshot.

### Music vs. sound-design separation

The brief was explicit that this MP3 is music **only** and must not replace
wind/leather/pre-draw/gunshot/dust — implemented as two independent
`AudioSource` components added to the same shared host GameObject in
`BuildCinematicAudio` (multiple sources per GameObject is this project's own
established pattern — ClasicoHud already has its own):

- **`_sfxAudioSource`** (renamed from the old single `_audioSource`) — every
  sound-design one-shot: wind, leather creak, pre-draw tick, gunshot, dust
  accent. Never has `.clip` set, never `.Stop()`'d — only ever
  `PlayOneShot(...)`.
- **`_musicAudioSource`** — the real duel music clip only.
  `.clip`/`.volume`/`.Play()`/`.Stop()` are all driven explicitly; nothing
  else is ever routed through it.

This split matters mechanically, not just conceptually: Unity's
`AudioSource.Stop()` also cuts that same source's own in-flight
`PlayOneShot` voices. Had music and gunshot shared one source, stopping the
music at the gunshot beat could have silenced an in-flight gunshot/dust tail
too — the brief's own "gunshot unaffected by music fade" requirement is
satisfied structurally by the split, not by call ordering.

### Sync map (fitted to the existing C8.1d.5 cinematic — no pacing changed)

| Cinematic time | Event |
|---|---|
| 0.00 | `StartDuelMusic()` — the *first* statement inside `CinematicIntroRoutine`, the same authoritative coroutine that starts everything else about the cinematic (background reveal, wind, tumbleweed). Never a separately-delayed coroutine. Volume starts at `MusicBaseVolume` (0.50, "moderate-low"). |
| 0.00-4.90 | Normal level (0.50). The tension build across Sheriff/Outlaw/hand/Sheriff is now carried by the music itself — the C8.1d.4/.5 discrete twang/tension-note/low-pulse cues that used to land on these shots are gone (see "Procedural placeholder removal" below), not merely silent. |
| 4.90-5.60 | Gentle attenuation — a `MusicVolumeRoutine` lerps 0.50 → 0.20 across this exact 0.70s shot (the "Outlaw close-up, short response shot" window), launched as its own concurrent coroutine so it runs alongside that shot's own push-in rather than blocking it. |
| 5.60-6.20 | Near-silence — a second `MusicVolumeRoutine` lerps 0.20 → 0.05 across this exact 0.60s tension-silence window. Wind (already playing), the leather-creak cue, and the late pre-draw tick remain audible sound design throughout. |
| 6.20 | **`_musicAudioSource.Stop()`** — hard stop, not a fade to 0 — immediately before the gunshot/dust-accent one-shots fire on `_sfxAudioSource`, so the gunshot transient never shares the mix with any residual music tail. "Effectively gone," per the brief, achieved literally, not just approximately. |
| 6.20-6.50 | Flash/cut, no music. |
| 6.50+ | Gameplay reveal — music already stopped. |

The raw MP3 itself was never destructively edited and no new rendered file
was created — the "silence window" is entirely a runtime `AudioSource.volume`
envelope layered over the unmodified source clip, exactly as the brief
requested.

### Procedural placeholder removal

The three C8.1d.4/.5 procedural cues that were standing in for **music**
(not sound design) — `_twangClip`, `_tensionNoteClip`, `_lowPulseClip`, and
every `PlayOneShot` call site that triggered them — were **deleted from
`WesternShootoutPresenter.cs` entirely**, not left as disabled/dead fields,
per the brief's "remove or disable ONLY the procedural cues that were acting
as music." `ProceduralAudio.cs` itself (the generic `Tone`/`Sweep`/`Noise`/
`Gunshot` static factory methods) was **not touched** — those are shared,
general-purpose utilities, and `_predrawClip` (`Tone`) and `_creakClip`
(`Sweep`) still use them for genuine sound design, so nothing was removed
"globally." The removal was scoped to exactly the three call sites inside
the Western cinematic path, per the brief's explicit boundary.

### Files changed

- `Assets/Hermit/Runtime/GameFramework/Microgames/WesternShootoutPresenter.cs`
  — two-AudioSource split (`_sfxAudioSource`/`_musicAudioSource`); removed
  `_twangClip`/`_tensionNoteClip`/`_lowPulseClip` fields and their
  `PlayOneShot` call sites; added `_duelMusicClip`, `StartDuelMusic()`,
  `MusicVolumeRoutine(...)`, and the three volume-envelope/stop call sites
  wired into the existing (unmodified) `CinematicIntroRoutine` timeline;
  added idempotent `_musicAudioSource.Stop()` safety calls in
  `RevealAfterIntro()` and `Hide()` (no audio leak into rounds 2/3 or other
  worlds even on an unexpected early exit). Doc-comments updated throughout.
- `Assets/Hermit/Runtime/GameFramework/RuntimeUIFactory.cs` — new
  `LoadAudio(resourcePath)` helper (an `AudioCache` dictionary mirroring the
  existing `ArtCache` pattern), the audio equivalent of `LoadArt`.
- `Assets/Hermit/Content/Resources/Audio/Gold/Western/Western_DuelMusic_01.mp3`
  — new production audio asset (byte-identical copy of the ArtBible
  candidate) + its `.meta` (see "Import settings" below).
- `Assets/Hermit/Tests/PlayMode/ClasicoPlayModeTests.cs` — two new tests
  (below).
- `Docs/C8_1D_GOLD_ART_INTEGRATION.md` — this section.

No change to `ClasicoGameDefinition.cs`, `ClasicoSessionDirector.cs`,
`ClasicoEncounterPlan.cs`, any outlaw/Sheriff/cinematic-image art, the
cinematic's shot timing/push-ins, or any other world's presenter.
`EncounterIntroSeconds` stays at 7.5s — nothing in this integration required
touching it (the brief's own explicit instruction: "unless integration
evidence proves a timing issue," and none did).

### Tests added

Per the brief's "state and audio triggers, not waveform content" — both new
tests use `AudioSource.isPlaying`/`.volume`/`.clip` reference-equality
polling, the same frame-accurate, real-wall-clock-bounded pattern every
existing `WesternCinematic_*` test already uses (never the higher-level
`LaunchClasico`/`AnswerCurrentMicrogame` helpers, which are deliberately
state-driven to *wait past* the entire intro before returning control and so
can never observe transient mid-intro audio state):

- **`WesternCinematic_DuelMusicAsset_Loads`** — the primary music asset
  resolves via `Resources.Load<AudioClip>`.
- **`WesternCinematic_DuelMusic_PlaysOnlyDuringRound1Intro_SeparateFromSfx_DucksThenStops`**
  — drives a full 3-round Western Encounter frame-by-frame and asserts, in
  one pass: the music clip plays at some point during round 1's intro; it
  never plays outside that window (no restart on round 2/3, no leak into
  gameplay); it is stopped before round 1's Decision phase begins; a second,
  distinct `AudioSource` is also observed playing during the same window
  (proving sound-design cues fire independently); the music and that other
  source are never the same object; and the music's own volume is observed
  both near its base level and ducked toward near-silence at different
  points in the intro (proving the envelope actually ran, not just a static
  low value).

### Test results

- **EditMode: 110/110 passed** (both immediately after the code/test
  changes, and again after hand-editing the audio `.meta`'s import
  settings — a clean reimport, no import or compile errors either time).
- **PlayMode, full suite: 50/51 passed**, including both new audio tests.
  The one failure,
  `ShellRealSceneCompositionTests.RealShellScene_DecisionTimeoutOnActivePresenter_RestoresLocalReactionTransforms`,
  is the same pre-existing, full-suite-load-only timing flake already
  investigated and documented under C8.1d.5 immediately above (confirmed
  there to pass 2/2 in isolation across two separate runs) — unrelated to
  Western audio, Detective/Game Show were not touched this phase, and per
  this project's own standing practice it was not re-investigated here.

### Import settings

Inspected Unity's own auto-generated defaults first (never guessed), then
changed only the fields that mattered for a short, sync-critical cinematic
cue — left everything else at Unity's default:

| Setting | Unity's default | Final value | Why |
|---|---|---|---|
| Load Type | Decompress On Load | **unchanged** | Already correct for a ~6s clip — fully decoded to memory once, no per-play or streaming latency. |
| Compression Format | Vorbis | **PCM** | With Load Type already "Decompress On Load," Vorbis still costs one decode pass into memory before first use; PCM removes even that, at a completely negligible memory cost for a 6s stereo clip (~1.1MB decompressed either way) — the brief's own "do not over-optimize file size at the expense of sync." |
| Quality | 1.0 (irrelevant for PCM) | **unchanged** | Moot once Compression Format is PCM; not worth a synthetic value. |
| Preload Audio Data | **false** | **true** | Unity's default would load the clip's data lazily on first access/`Play()`, risking a load hitch on the exact frame the cinematic calls `StartDuelMusic()` at cinematic t=0 — the one setting here that was a genuine, not merely cosmetic, sync risk. |
| Load In Background | false | **unchanged** | Already correct — a synchronous, blocking load of this small a clip is negligible and *guarantees* readiness before first use, which background loading does not. |
| Force To Mono | false | **unchanged** | No reason to collapse a music track to mono. |
| Sample Rate Setting | Preserve Sample Rate | **unchanged** | Already correct — no resampling artifacts, no over-optimization. |
| 3D Sound | true | **false** | Cosmetic/clarity only, not functional — the runtime code already forces `spatialBlend = 0f` on `_musicAudioSource` regardless of this import flag; changing it just makes the Inspector-default match what the code actually does, for a future reader. |
| Normalize | true | **unchanged** | Standard per-clip peak normalization at import — unrelated to the brief's section 7 "avoid normalization tricks that crush dynamics" (which is about mastering-time dynamic-range compression, not this). Not asked about; no reason to change it. |

### Remaining risks

- **Base/duck/near-silent volume levels (0.50/0.20/0.05) are a first,
  reasoned guess, not ear-tuned** — the brief's own section 7 says "start
  conservatively," which this does, but only a real Windows-executable
  listen can confirm the mix balance (music vs. wind/creak vs. gunshot
  dominance) actually lands right.
- **The track's own natural ~6s length already trails off close to the
  5.60-6.20s window** — good news for "useful late-track emptiness," but
  also means the programmatic duck/near-silence envelope is working
  *with* an already-quiet region of the source audio, not fighting loud
  music down to silence; if the raw track is actually quite quiet there
  already, the audible effect of the envelope itself may be subtler than
  intended — worth a specific listen for exactly this in the build.
- **The pre-existing Detective/Game Show full-suite timing flake remains
  unresolved** (out of this phase's scope, unrelated to audio).
- **The Dusty Duel alternate is not wired in at all** — swapping candidates
  later would mean re-pointing one `Resources.Load` path and re-copying one
  file; no architecture change needed.

## C8.1d.7 — Western Gameplay Fire Feedback + Action Cue

**Status: IMPLEMENTATION IN PROGRESS — MANUAL GAMEPLAY FEEDBACK VALIDATION REQUIRED.**

### Manual validation context

The cinematic intro and the final music integration (C8.1d.6) both passed
manual validation. Two remaining Western polish items came back: (1) the
"¡DISPARA!" cue reads as generic instructional UI and competes with the
scene; (2) firing on an outlaw during gameplay has no audible firearm
feedback. This phase touches only those two things — no cinematic timing,
no music, no outlaw art, no Encounter architecture, no other world.

### DISPARA text refinement

`MicrogameVocabulary.CommandFor(MicrogameArchetype.AimSelect)` changed from
`"¡DISPARA!"` to `"DISPARA"` — opening/closing exclamation marks removed,
per the brief. Every other archetype's word (`¡DECIDE!`, `¡BALANCEA!`,
`¡ENCUÉNTRALO!`) is untouched.

### A new, presenter-owned cue — not the generic HUD banner

The real problem was never just the punctuation: `ClasicoGameHost` shows
every archetype's command word via `ClasicoHud.ShowCommand(...)`, active for
the archetype's *entire* Intro phase. For Western that Intro phase is now
the whole ~7.5s cinematic (C8.1d.5) — meaning the old "¡DISPARA!" banner
floated over the Sheriff/Outlaw/hand close-ups, the silence window, and the
gunshot flash for the full cinematic, which is exactly the "competes with
the cinematic" complaint. Text refinement alone could not fix this; the
*mechanism* had to change for AimSelect specifically.

`WesternShootoutPresenter` now owns its own small "DISPARA" cue instead:

- **`_disparaText`** — a `Text` built once in `Build()`, anchored top-center
  identically to `Concept` but higher up (`anchoredPosition (0, -24)`,
  `sizeDelta (300, 32)`, vs. Concept's `(0, -70)`/`(900, 80)`) — "slightly
  above the concept/question area," per the brief. `Theme.BodySize` (smaller
  than Concept's `Theme.HeadingSize`), `Theme.Accent` color, bold. Built
  hidden (`alpha 0`, inactive).
- **`ShowDisparaCue()`** — sets the text (via
  `MicrogameVocabulary.CommandFor(AimSelect)`, so the word itself is still
  owned centrally, only its *display mechanism* changed for Western),
  activates the GameObject, and plays a quick, non-repeating punch-in
  (`DisparaPunchInRoutine`): scale `0.92 → 1.04` and alpha `0 → 1` over the
  first 60% of a 0.16s duration, then scale eases `1.04 → 1.00` over the
  remaining 40% — no continuous pulsing. Called at the **end of
  `RevealRoundContent`**, so it fires identically whether that's reached via
  `RevealAfterIntro` (round 1, right after the cinematic's hard cut) or
  directly from `ShowChallenge` (rounds 2/3's immediate reveal, no
  cinematic) — "may reappear at gameplay start" for continuation rounds,
  per the brief, falls out of reusing the single existing reveal path rather
  than needing new round-specific logic.
- **`HideDisparaCueOnFire()`** — a quick alpha-only fade (`DisparaFadeOutRoutine`,
  ~0.15s, no scale change) called at the **very start of
  `FireSequenceRoutine`**, before the 0.08s anticipation wait — so it starts
  fading the instant Decision ends (hit or timeout alike), never lingering
  once it "no longer provides useful information," per the brief.
- **`HideDisparaCueImmediately()`** — a hard, non-fading reset used only when
  `ShowChallenge` stages round 1 hidden for the cinematic, mirroring how
  concept/labels/reticle are also hard-cleared there.

`ClasicoGameHost`'s `ShowCommand` call is still made for AimSelect (so its
*other* side effects — clearing stale feedback text, resetting the timer
fill — still happen for Western exactly as for every other archetype), but
the text argument is `string.Empty` for AimSelect specifically, so the
generic banner never becomes visible for Western. `HideCommand()` is
likewise still called at Decision start, now a harmless no-op on an
already-empty banner.

### Gameplay gunshot

A dedicated **`ProceduralAudio.GameplayGunshot(name, duration, volume)`**
generator was added — a genuinely different synthesis from the cinematic's
own `Gunshot`, not the same clip reused shorter:

| Parameter | Cinematic `Gunshot` | Gameplay `GameplayGunshot` |
|---|---|---|
| Crack duration | 12ms | 7ms (brighter/harder) |
| Body frequency | 92Hz | 185Hz (higher → reads as "crack," not "boom") |
| Body decay | 14 | 42 (much faster — dry, not sustained) |
| Tail decay | 5 | 26 (very short "air," no cinematic reverb tail) |
| Character | large, dramatic, scene punctuation | dry, short, crisp, repeatable |

Fired via `_sfxAudioSource.PlayOneShot(_gameplayGunshotClip)` inside
`FireSequenceRoutine`'s `hasTarget` branch, aligned exactly with the muzzle
flash/screen-kick (i.e. muzzle-flash/tracer-start), **never delayed behind
the outlaw's own hit reaction** — and fired **regardless of correctness**:
the shot is a physical action, outcome feedback (tint, Hit sprite, shake)
communicates correctness separately, exactly per the brief's "do not use
silence to indicate a wrong answer." A small optional impact accent
(reusing the existing `_dustAccentClip` rather than adding a new cue) plays
right after `FireOffscreenShot`'s tracer arrives — subtle, no gore, no
extra audio layers beyond what already existed for the cinematic.

### AudioSource routing

Both the gunshot and its impact accent play on **`_sfxAudioSource`**
(C8.1d.6's existing sound-design source) — never `_musicAudioSource`. Since
duel music only ever plays during round 1's own cinematic intro and is
already stopped well before Decision (let alone before a shot can be
fired), this is belt-and-suspenders in practice, but the routing itself is
unconditional and structural, not timing-dependent — no global audio
manager was introduced.

### Files changed

- `Assets/Hermit/Runtime/GameFramework/Microgames/WesternShootoutPresenter.cs`
  — `_disparaText`/`_disparaRoutine` fields, `ShowDisparaCue`/
  `DisparaPunchInRoutine`/`HideDisparaCueOnFire`/`DisparaFadeOutRoutine`/
  `HideDisparaCueImmediately`; `_gameplayGunshotClip` field + generation +
  playback in `FireSequenceRoutine` (plus the dust-accent impact); wiring
  calls in `ShowChallenge`/`RevealRoundContent`; doc-comments updated.
- `Assets/Hermit/Runtime/GameFramework/ProceduralAudio.cs` — new
  `GameplayGunshot` generator.
- `Assets/Hermit/Runtime/GameFramework/Microgames/MicrogameVocabulary.cs` —
  `"¡DISPARA!"` → `"DISPARA"`, doc-comment updated.
- `Assets/Hermit/Runtime/GameFramework/ClasicoGameHost.cs` — `ShowCommand`
  call for AimSelect passes an empty string instead of the command word.
- `Assets/Hermit/Tests/PlayMode/ClasicoPlayModeTests.cs` — two new tests
  (below).
- `Assets/Hermit/Tests/PlayMode/ShellRealSceneCompositionTests.cs` — one
  pre-existing test's TransitionOverlay check simplified (see "Test-timing
  bugs found this phase" below); unrelated to this phase's own production
  changes.
- `Docs/C8_1D_GOLD_ART_INTEGRATION.md` — this section.

No change to cinematic timing, music, outlaw/Sheriff art, Encounter
architecture, or any other world.

### Tests added

Both use state/trigger-level checks only (`GameObject.activeSelf`,
`Text.color.a`, `AudioSource.isPlaying`/`.clip` reference equality) — never
waveform assertions, per the brief:

- **`WesternShootout_DisparaCue_HiddenDuringCinematic_ShownAtReveal_FadesAfterFirstShot`**
  — drives round 1 frame-by-frame: the cue must never be visible during the
  cinematic Intro phase, must become visible at (or immediately after)
  Decision begins, and must fade again once the player's first shot fires.
- **`WesternShootout_GameplayGunshot_FiresOnSfxSource_ForBothCorrectAndIncorrectShots`**
  — drives all 3 rounds of the Encounter, alternating a correct answer
  (round 1), an incorrect answer (round 2), and a correct answer (round 3);
  after each shot, confirms some non-music `AudioSource` becomes
  `isPlaying` within a bounded window — proving the gunshot fires on the
  SFX path for every round regardless of correctness.

"No regression to tracer / Neutral→Hit / scoring / 3-round Encounter /
cinematic intro gunshot fires once / music behavior" are covered by the
existing, unmodified tests from prior phases continuing to pass — the
regression proof is that they are still green, not new assertions.

### Test-timing bugs found this phase

Three, all in test code, none in the production changes above — found and
fixed empirically, per this project's standing practice:

1. **DISPARA same-frame race**: the new DISPARA test originally checked
   cue visibility and fired the shot in the *same* frame Decision began —
   before `DisparaPunchInRoutine` had run even one frame, so alpha was
   always still 0. Fixed by giving the check a real 0.3s observation
   window before firing.
2. **Gunshot-test loop off-by-one**: the new gunshot test's outer loop was
   gated on `roundsPlayed < 3`, which let it exit one iteration too early —
   right after firing round 3's shot but before that round's own 0.5s
   post-shot confirmation window had run, silently dropping round 3's
   result. Fixed by gating the loop on the confirmed-result count instead
   of the round counter.
3. **TransitionOverlay double-check anomaly** (`ShellRealSceneCompositionTests.cs`,
   unrelated to this phase's own production changes — a latent artifact of
   C8.1d.5's own test rewrite that this phase's testing happened to
   surface): a `yield return WaitUntil(...)` call followed by a separate
   explicit `Assert.Less` on the same `CanvasGroup.alpha` intermittently
   showed a value at the explicit assert that appeared inconsistent with
   what `WaitUntil`'s own internal check must have just seen, despite zero
   `yield` boundaries between them. Investigated with temporary diagnostic
   logging; the exact mechanism was not conclusively identified, but the
   symptom was fully and reliably resolved by collapsing the two-step
   check into a single inline wait-loop followed by one assert (removing
   the redundant double-read entirely) — confirmed clean across repeated
   isolated and full-suite runs afterward.

### Test results

- **EditMode: 110/110 passed.**
- **PlayMode, full suite: 53/53 passed** on the definitive confirmation run.
  One earlier full-suite attempt showed a single unrelated timeout
  (`IncorrectAnswer_TransitionsCleanly_IntoADifferentMicrogameType`, a
  pre-existing test untouched by this phase) when that specific run's total
  suite duration was ~2.7x the usual (1311s vs. the ~480-500s this session
  otherwise consistently saw) — re-run in isolation immediately afterward,
  it passed in 25.7s, confirming a system-load anomaly for that one run
  rather than a code defect. No leftover Unity processes were found to
  explain the slowdown; not investigated further since it did not reproduce.

### Manual validation requirement

Final judgment on both the DISPARA cue and the gameplay gunshot must be
made in the real Windows executable:

A. Does DISPARA feel integrated rather than pasted on?
B. Does it avoid covering characters?
C. Does gameplay feel immediately responsive when firing?
D. Does the gameplay gunshot sound distinct from the cinematic gunshot?
E. Does the gameplay gunshot feel crisp rather than overly cinematic?
F. Does correct-answer feedback feel satisfying?
G. Does incorrect-answer feedback still feel physically coherent?
H. Does repeated firing across the 3-round Encounter avoid audio fatigue?

## C8.1d.8 — Western Audio Event Ordering

**Status: IMPLEMENTATION IN PROGRESS — MANUAL AUDIO ORDER VALIDATION REQUIRED.**

### Manual bug report

Two audio-order problems were reported: (1) immediately before the
cinematic gunshot, an extra sound feels wrong/too prominent; (2) during
gameplay fire, an outcome-specific sound (different for correct vs.
incorrect) is perceived *before* the gameplay gunshot. Both are "the player
hears two events in the wrong order," not a redesign, pacing, music, art,
or scoring problem — this phase touches only audio call order/synthesis.

### Investigation — exact cause, traced not guessed

**A. Cinematic.** `WesternShootoutPresenter.CinematicIntroRoutine`'s
tension-silence segment (5.60–6.20s) played, in order: `_creakClip` at
t=5.60, then (after a 0.45s wait) `_predrawClip` at **t=6.05**, then (after
a further 0.15s wait) `_gunshotClip` at **t=6.20**. `_predrawClip` was
`ProceduralAudio.Tone("WesternPredrawTick", 1800f, 0.04f, 0.10f)` — a pure
1800Hz sine burst with `Tone`'s standard fast (0.008s) attack. A sudden,
narrow-band, high-pitched tone with an near-instant onset, arriving against
an otherwise near-silent mix only 0.15s before the gunshot, is exactly what
reads as a click/pop/mini-shot — confirmed as the "extra sound" by direct
inspection of its synthesis parameters, not assumed.

**B/C.** `ClasicoGameHost.RenderFrame`'s Feedback-phase branch called, in
this exact order: `_hud.RenderFeedback(director.LastAnswerCorrect, ...)`
**then** `RevealActivePresenter(director)` (which calls
`_western.RevealOutcome(selectedIndex)` → starts
`FireSequenceRoutine`). `ClasicoHud.RenderFeedback` calls
`_audioSource.PlayOneShot(correct ? _correctClip : _incorrectClip)`
**synchronously, immediately** — on the very same frame Feedback begins.
`FireSequenceRoutine`, by contrast, `yield return new WaitForSeconds(0.08f)`
before playing the gameplay gunshot (plus, before the impact accent, a
further 0.12s of tracer travel). So on every single shot, the generic
correct/incorrect ding was audible **before** `RenderFeedback` even
returned, while the gameplay gunshot didn't fire until ~80ms+ later — the
outcome sound was always first, backwards from the intended "gunshot is the
first strong transient" order. This is exactly the pattern the brief's
section 4 predicted ("generic Clásico feedback audio triggered at Decision
end before Western's local FireSequenceRoutine") — confirmed by reading
both call sites side by side, not assumed.

### Previous vs. corrected event order

**Cinematic** (unchanged timing, only the pre-draw cue's own character/volume changed):

| Time | Before | After |
|---|---|---|
| 5.60 | creak | creak (unchanged) |
| 6.05 | sharp 1800Hz tick (vol 0.10, instant attack) | soft leather/metal texture (vol 0.05, real attack ramp) |
| 6.20 | gunshot | gunshot (unchanged) |

**Gameplay** (per shot, relative to the click):

| Before | After |
|---|---|
| t=0.00 — generic correct/incorrect ding (`ClasicoHud.RenderFeedback`, immediate) | t=0.08 — gameplay gunshot (`FireSequenceRoutine`, unchanged) |
| t=0.08 — gameplay gunshot | t=0.08–0.20 — tracer travel (unchanged) |
| t=0.08–0.20 — tracer travel | t=0.20 — impact accent (unchanged) |
| t=0.20 — impact accent | t=0.30 — outcome feedback (**deferred**, new) |

### Pre-draw cue change

New `ProceduralAudio.PreDrawTension(name, duration, volume = 0.05f)`
replaces the `Tone`-based tick entirely (call site now
`ProceduralAudio.PreDrawTension("WesternPreDrawTension", 0.12f, 0.05f)`,
half the old volume): a heavily low-pass-filtered noise burst (leather/
cloth texture, smoothing coefficient 0.12 — much darker/softer than
`Noise`'s own 0.35) with a genuine attack/release envelope (35%/45% of the
clip's duration — never an instant onset, the exact property that made the
old tone read as a click), plus only a faint (0.15 relative weight), fully
damped 2600Hz partial for a bare hint of "metal touch" rather than a
distinct audible tone. The tension-silence window's own timing (creak at
5.60, this cue at 6.05, gunshot at 6.20) is **completely unchanged** — only
the cue's own synthesis and volume changed, per the brief's explicit "do
not move the gunshot."

### Outcome-feedback timing change

New `ClasicoGameHost.DeferredWesternFeedbackRoutine(correct, score, streak,
streakBonus)`: for `MicrogameArchetype.AimSelect` only, the Feedback-phase
branch now starts this coroutine (via `_hud.StartCoroutine`, `ClasicoHud`
already being the shared MonoBehaviour every presenter borrows for its own
coroutines) instead of calling `_hud.RenderFeedback` immediately. The
coroutine waits 0.30s — landing inside the brief's suggested "impact +
0.05–0.15s" window (impact lands at ~0.20s into `FireSequenceRoutine`) —
then calls the exact same `_hud.RenderFeedback(...)`. `director.LastAnswerCorrect`/
`_controller.CurrentSession.Score`/`.Streak`/`director.LastStreakBonus` are
all captured as plain values *before* scheduling the delay — this is a
presentation-timing change only; nothing about when/how correctness or
score is computed changed, and `RevealActivePresenter(director)` (which
starts Western's own gunshot sequence) is still called on the exact same
original frame as before. Every other archetype (`ChooseSide`, `Balance`,
`DetectError`) is completely unaffected — `RenderFeedback` still fires
immediately for them, exactly as before, since none of them have a
competing "physical action" transient of their own.

### Test-observable instrumentation

New `Hermit.Runtime.GameFramework.Microgames.WesternAudioEvents` — a small
**public** static class (`Entry { Name, Time, Clip }`, `Record`/`Clear`/
`Events`) recording exactly five call sites: `CinematicPreDraw`,
`CinematicGunshot`, `GameplayGunshot`, `GameplayImpact`,
`WesternOutcomeFeedbackCorrect`/`WesternOutcomeFeedbackIncorrect`. Public
(not internal) specifically so PlayMode tests can read it without
`InternalsVisibleTo`, matching every other test in this project (which
interacts with production code only through public surface — scene graph,
`Resources.Load`, now also this log). No UI, no persistence beyond the
current process, no production code ever reads it — explicitly not a
general debug system, per the brief's own "do not create a new global
debug system."

### Files changed

- `Assets/Hermit/Runtime/GameFramework/Microgames/WesternAudioEvents.cs`
  — new.
- `Assets/Hermit/Runtime/GameFramework/ProceduralAudio.cs` — new
  `PreDrawTension` generator.
- `Assets/Hermit/Runtime/GameFramework/Microgames/WesternShootoutPresenter.cs`
  — `_predrawClip` generation call changed; `WesternAudioEvents.Record`
  calls added at the four presenter-owned audio events; doc-comments
  updated.
- `Assets/Hermit/Runtime/GameFramework/ClasicoGameHost.cs` — new `using
  System.Collections`/`UnityEngine`; `DeferredWesternFeedbackRoutine`;
  AimSelect-only branch in the Feedback-phase handler.
- `Assets/Hermit/Tests/PlayMode/ClasicoPlayModeTests.cs` — two new tests, a
  `PeakAmplitude` helper, `WesternAudioEvents.Clear()` added to `SetUp`.
- `Docs/C8_1D_GOLD_ART_INTEGRATION.md` — this section.

No change to cinematic pacing/timing, music, outlaw/Sheriff art, scoring/
correctness logic, Encounter architecture, or any other world.

### Tests added

Both use `WesternAudioEvents` for order/timestamp checks and
`AudioClip.GetData` for one coarse peak-amplitude comparison — never a
waveform/pitch/timbre assertion:

- **`WesternCinematic_PreDrawCue_FiresBeforeGunshot_QuieterThanGunshot_ExactlyOnce`**
  — drives round 1's intro; asserts exactly one pre-draw event and exactly
  one gunshot event (no second impact-like cue), the pre-draw fires before
  the gunshot, and its peak amplitude is lower than the gunshot's.
- **`WesternShootout_GameplayGunshot_FiresBeforeImpactAndOutcomeFeedback_ForCorrectAndIncorrect`**
  — drives all 3 rounds, alternating correct/incorrect; for every round,
  asserts gunshot-before-impact-before-outcome-feedback ordering, that the
  recorded outcome event matches the actual answer correctness (a
  structural check that correctness itself is untouched), and that the
  gameplay gunshot clip is the identical `AudioClip` instance across
  correct and incorrect rounds alike.

"No regression to music behavior / round 2-3 behavior / scoring / tracer /
Neutral→Hit / 3-round Encounter" is covered by every existing, unmodified
test from prior phases continuing to pass unchanged.

### Test results

- **EditMode: 110/110 passed.**
- **PlayMode, full suite: 55/55 passed**, including both new tests — a
  fully clean run (the pre-existing Detective/Game Show full-suite timing
  flake documented in C8.1d.5/.6/.7 did not reproduce this run either).

### Manual validation requirement

Final judgment on the corrected audio order must be made in the real
Windows executable:

A. Final intro beat now feels "silence → gunshot," not "click → gunshot"?
B. Correct shot feels "gunshot → impact → positive feedback"?
C. Incorrect shot feels "gunshot → impact → negative feedback"?
D. No result cue is perceived before the firearm transient?
E. Gunshot remains the first strong sound in both outcomes?

## C8.1d.9 — Western Countershot Failure Feedback

**Status: IMPLEMENTATION IN PROGRESS — MANUAL COUNTERSHOT VALIDATION REQUIRED.**

### Manual design decision

Manual validation concluded the generic correct/incorrect result audio is
unnecessary for Western — the outlaw's own red/green tint, Hit sprite swap,
and shake already communicate correctness. New design: **remove** Western's
result audio entirely, and for an incorrect answer, have the **correct**
outlaw fire back at the player as physical failure feedback.

### Removal of redundant Western result audio

`ClasicoHud.RenderFeedback` gained a `bool playAudio = true` parameter —
default `true` so every existing caller/archetype is completely unaffected;
an explicit **per-call opt-out**, never a global disable. The two
`_audioSource.PlayOneShot(...)` calls (correct/incorrect ding) are now
guarded by it; `StartPunch()`/`StartShake()` (the HUD's own global visual
punch/shake) and the score/streak/feedback text are unconditional, per the
brief's "keep all visual feedback." `ClasicoGameHost` now calls
`_hud.RenderFeedback(..., playAudio: _activeArchetype != MicrogameArchetype.AimSelect)`
**immediately**, exactly like every other archetype — C8.1d.8's
`DeferredWesternFeedbackRoutine` (which existed solely to delay that same
ding until after the gameplay gunshot) is deleted outright, since there is
no longer a ding to reorder.

### Retained visual feedback

Completely unchanged: the correct outlaw's green tint, the selected wrong
outlaw's red tint + Hit sprite + shake, the correct outlaw's own Hit
sprite + punch + dust puff on an actual hit, and every HUD element
(score/streak/`¡Correcto!`/`Incorrecto` text, the HUD's own global punch/
shake). Only the ding itself is gone for Western.

### Wrong-answer countershot

New `WesternShootoutPresenter.CountershotRoutine(correctIndex)`, started
from `FireSequenceRoutine` on a wrong-but-fired answer (`hasTarget && !hit`)
or a timeout (`!hasTarget`) — never on a correct answer:

1. `yield return new WaitForSeconds(0.18f)` — a short dramatic pause after
   the wrong-answer visual state (or timeout flinch) becomes clear, landing
   the countershot beat inside the brief's suggested 0.40–0.55s window from
   `FireSequenceRoutine`'s own start (0.08s anticipation + 0.12s tracer +
   this 0.18s pause ≈ 0.38s).
2. A punch on the **correct** outlaw's own target rect — reusing
   `_targetPunchHandles[correctIndex]` (safe: that handle is never touched
   on a wrong-answer/timeout path, only on an actual hit, so the two never
   collide).
3. `_sfxAudioSource.PlayOneShot(_enemyGunshotClip)` — the countershot's own
   cue (never `_musicAudioSource`).
4. `PlayerHitFlashRoutine()` — the red screen flash (below).

No new aiming, projectile, or bullet-simulation system — purely
presentation, per the brief's explicit constraint.

### Correct-outlaw-as-attacker resolution

`correctIndex` is the exact same value `FireSequenceRoutine` already
receives as a parameter (originally computed once in `RevealOutcome` via
`Array.IndexOf(_challenge.CategoryOptions, _challenge.CorrectCategory)`,
the same already-authoritative answer mapping every other reveal branch in
this method already uses) — never recomputed independently for the
countershot.

### Enemy gunshot implementation

New `ProceduralAudio.EnemyGunshot(name, duration, volume = 0.36f)` —
related to but distinct from the player's own `GameplayGunshot`, per the
brief's "same family, but a bit further away":

| Parameter | Player `GameplayGunshot` | Enemy `EnemyGunshot` |
|---|---|---|
| Crack duration | 7ms (brighter) | 10ms (duller, more smoothing) |
| Body frequency | 185Hz | 140Hz (lower) |
| Body decay | 42 (very fast) | 34 (slightly slower) |
| Tail decay | 26 (very short) | 20 (a touch longer/louder) |

Fired only via `CountershotRoutine`, on `_sfxAudioSource`, never
`_musicAudioSource`, never the cinematic's own `Gunshot`, and never a
generic incorrect-result ding.

### Red player-hit flash

New `_playerHitFlash` — a plain full-stretch `Image` built once in
`Build()` (`RuntimeUIFactory.CreatePanel` + `StretchFull`, `raycastTarget =
false`), animated via direct `Image.color` alpha changes only — **never**
`CanvasGroup`, **never** `Image.Type.Filled`/`Radial` (this codebase's own
documented C7-era black-screen bug class came from exactly that
technique). `PlayerHitFlashRoutine`: alpha rises to a peak of **0.32**
(within the brief's suggested 0.25–0.40) over 0.05s, then fades to 0 over
0.20s, plus a small punch on the whole `WesternShootout` root (reusing
`_screenKickHandle` — safe, since the player's own shot-kick from ~0.4s
earlier has long since finished). Defensively reset to alpha 0 at the top
of `ShowChallenge` every round, even though the routine's own ~0.25s total
duration always finishes well before a round transitions. "You got hit,"
not "you died" — no held red screen, no gore, no aggressive shake.

### Timeout behavior

Inspected before changing anything, per the brief's own instruction: the
existing `!hasTarget` branch in `FireSequenceRoutine` already does exactly
what the brief prefers — `hasTarget` being false means the entire
gunshot/tracer/impact block is skipped, so **no player gunshot was ever
invented for a timeout**, before or after this phase. That branch's own
logic is untouched; `CountershotRoutine(correctIndex)` is simply **added**
there too (alongside the existing shared flinch shake), so the correct
outlaw still counters as failure punctuation even though the player never
fired.

### Event ordering

- **Correct:** player gunshot → tracer → impact → (nothing further; no
  ding, no countershot).
- **Incorrect (fired):** player gunshot → tracer → impact → wrong-answer
  visual state → ~0.18s pause → enemy gunshot → red flash.
- **Timeout:** no player gunshot → shared flinch → ~0.18s pause → enemy
  gunshot → red flash.

There is no generic correct/incorrect ding in Western in any of the three
cases.

### Files changed

- `Assets/Hermit/Runtime/GameFramework/ClasicoHud.cs` — `RenderFeedback`'s
  new `playAudio` parameter.
- `Assets/Hermit/Runtime/GameFramework/ClasicoGameHost.cs` —
  `DeferredWesternFeedbackRoutine` removed; the Feedback-phase branch calls
  `RenderFeedback` immediately again, with `playAudio` conditioned on
  archetype; now-unused `using System.Collections`/`UnityEngine` removed.
- `Assets/Hermit/Runtime/GameFramework/ProceduralAudio.cs` — new
  `EnemyGunshot` generator.
- `Assets/Hermit/Runtime/GameFramework/Microgames/WesternShootoutPresenter.cs`
  — `_enemyGunshotClip`/`_playerHitFlash` fields + `Build()` construction;
  `CountershotRoutine`/`PlayerHitFlashRoutine`; wiring into
  `FireSequenceRoutine`'s wrong-answer and timeout branches; defensive
  reset in `ShowChallenge`; doc-comments updated throughout.
- `Assets/Hermit/Tests/PlayMode/ClasicoPlayModeTests.cs` — three new tests;
  one C8.1d.8 test's obsolete assertions removed and renamed (below).

No change to cinematic pacing/timing, music, outlaw/Sheriff art, scoring/
correctness logic, Encounter architecture, or any other world. No health,
damage values, lives, or gore added.

### Tests added / updated

- **`WesternShootout_NoGenericFeedbackAudio_ButOtherArchetypesKeepTheirs`**
  — identifies `ClasicoHud`'s own feedback `AudioSource` structurally (the
  source that plays specifically during a *non*-Western round's Feedback
  phase) and proves it never plays during any Western round's Feedback
  phase, while confirming it does play for at least one other archetype
  (positive control). Sampling is restricted to `IsFeedbackPhase`
  specifically — see "test bugs found" below for why.
- **`WesternShootout_Countershot_FiresOnlyOnWrongAnswer_WithEnemyGunshotAndRedFlash`**
  — drives all 3 rounds (correct/incorrect/correct); proves the red flash
  and exactly one `EnemyGunshot` event appear only for the incorrect round,
  after that round's own impact.
- **`WesternShootout_Timeout_NoFakePlayerGunshot_ButCountershotStillFires`**
  — lets round 1's Decision window run out unanswered; proves zero
  `GameplayGunshot` events (no invented player shot) and exactly one
  `EnemyGunshot` event (the countershot still fires).
- **`WesternShootout_GameplayGunshot_FiresBeforeImpact_ForCorrectAndIncorrect`**
  (renamed from C8.1d.8's `..._FiresBeforeImpactAndOutcomeFeedback_...`) —
  the old outcome-feedback assertions were removed (that feature no longer
  exists); the still-valid gunshot-before-impact ordering and gunshot-clip-
  identity-across-correctness checks remain.

"No regression to cinematic gunshot / music / 3-round Encounter /
Neutral↔Hit / other archetypes' own feedback audio" is covered by every
existing, unmodified test from prior phases continuing to pass.

### Two test bugs found and fixed this phase

1. **False-positive `AudioSource` fingerprint.** The first draft of
   `WesternShootout_NoGenericFeedbackAudio_...` sampled "is any source
   playing" across the *entire* time an archetype was active, not just its
   Feedback phase. `ClasicoHud`'s shared `AudioSource` also plays the
   unrelated world/encounter-transition-cut sting
   (`ClasicoHud.PlayTransitionCut`) right as Western's own block begins —
   a real, legitimate sound with nothing to do with the correct/incorrect
   ding. That transition-cut sting was being misattributed as "the ding
   playing during Western," a false failure. Fixed by restricting sampling
   to `director.IsFeedbackPhase` — the only moment `RenderFeedback`'s audio
   call could ever fire.
2. **Obsolete C8.1d.8 assertions.** `WesternShootout_GameplayGunshot_FiresBeforeImpactAndOutcomeFeedback_ForCorrectAndIncorrect`
   asserted on `WesternOutcomeFeedbackCorrect`/`Incorrect` events that this
   phase's own removal of `DeferredWesternFeedbackRoutine` stopped
   recording entirely — not a bug in new code, but a test whose premise
   (Western defers/plays an outcome ding) was intentionally invalidated by
   this phase's own design change. Stripped those assertions and renamed
   the test to `..._FiresBeforeImpact_ForCorrectAndIncorrect`, keeping only
   what remains true.

### Test results

- **EditMode: 110/110 passed.**
- **PlayMode, full suite: run twice this phase.** First run: 54/58 passed —
  3 of the 4 failures were the two test bugs above (now fixed) plus the
  known pre-existing Detective/Game Show flake; a 4th, `WesternCinematic_DuelMusic_PlaysOnlyDuringRound1Intro_SeparateFromSfx_DucksThenStops`
  (an unrelated C8.1d.6 test), failed with "0 of 3 rounds played" within
  its 60s budget — confirmed via isolated re-run (passed in 22s) to be
  system-load noise, not a regression. Second run (after both test fixes):
  57/58 passed — every C8.1d.9-relevant test green; the sole failure,
  `WesternShootout_DisparaCue_HiddenDuringCinematic_ShownAtReveal_FadesAfterFirstShot`
  (an unrelated, untouched C8.1d.7 test), hit a hard 180s NUnit timeout
  after running 2337s — the *entire* suite took 2909s (48.5 minutes) that
  run, versus a ~480-500s baseline earlier in this same session, a severe
  and escalating system-load pattern (this session's Unity batch runs have
  progressively slowed: ~500s → ~1311s → ~2909s across successive phases).
  Confirmed via isolated re-run (passed in 18s) to be the same class of
  environment noise, not a regression — every C8.1d.9-specific test has
  now passed cleanly in every run this phase.

### Manual validation requirement

Final judgment on the countershot must be made in the real Windows
executable:

A. Correct answer feels clean: shot → impact → green?
B. There is no unnecessary success ding?
C. Incorrect answer feels: shot → wrong/red → enemy returns fire → red screen?
D. The countershot clearly comes from the correct outlaw conceptually?
E. Enemy shot is readable but does not feel like a second player shot?
F. Red flash is noticeable but restrained?
G. Failure sequence does not make the rapid-fire Encounter feel slow?
H. Three rounds remain enjoyable when multiple mistakes happen consecutively?

## C8.1p — Western "DISPARA" visual prompt removed

Manual RC review: the presenter-owned "DISPARA" text cue (C8.1d.7; moved
beside the ConceptSign in C8.1n) did not match the Gold presentation. It is
removed outright — the `DisparaCue` Text, its show/punch-in, fade-on-fire and
hard-hide paths, and its tracked coroutine are gone from
`WesternShootoutPresenter`. Nothing replaces it: the round relies on the
cinematic reveal, the ConceptSign, the outlaw nameplates and the reticle. The
shared HUD command banner stays suppressed for Western (as since C8.1d.7), so
no "DISPARA" text appears anywhere. Input timing, the answer window, shooting,
scoring and timeout are unchanged — `EnableOutlawInput` is still the one input
gate, it just no longer shows a cue.

**Future Gold voice hook (documented only, not implemented):** a real recorded
"¡Dispara!" voice line belongs at the end of `EnableOutlawInput` — the exact
moment input goes live on every round — e.g. loaded via
`RuntimeUIFactory.LoadAudio("Audio/Gold/Western/Western_DisparaVoice")`, played
once on `_sfxAudioSource`, logged as `WesternAudioEvents.Record("DisparaVoice",
clip)`, and silent when the asset is absent. No synthesized/TTS/procedural
placeholder: silence until real Gold audio exists.

