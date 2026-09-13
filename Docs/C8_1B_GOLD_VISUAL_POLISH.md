# C8.1b — Gold Slice Visual Polish Pass

**Status: IMPLEMENTED — MANUAL VISUAL VALIDATION PENDING**

This phase is code-complete and fully covered by automated tests (see
section U). It is explicitly **not** FINAL/COMPLETE — see section X for the
exact criteria still outstanding. Do not treat this document as a closeout
report; treat it as the record of what was built, pending your own eyes on
it in the Editor.

## A. Phase objective

C8.1 (`Docs/C8_1_GOLD_MICROGAME_SLICE.md`) proved the microgame architecture
end-to-end but left all four Gold presenters as geometric prototypes —
flat-colored rectangles, ASCII-art faces (`° °` / `^ ^` / `T_T`), no
character read, no environment dressing, no local reaction motion beyond
the shared global punch/shake ClasicoHud already had. C8.1b's objective was
to take those four working-but-ugly presenters and turn them into a
coherent, expressive, premium-feeling playable slice — **without** touching
gameplay, scoring, `ClasicoSessionDirector`, the microgame count, or any
C8.2 system (Integrity/Heat/networking/new content). Every change in this
phase is presentation-layer only.

## B. Visual philosophy

- **Procedural, not asset-based.** Every shape is built at runtime from
  `RuntimeUIFactory`'s rounded-panel/text/button primitives — no imported
  sprites, no ParticleSystem, no per-round `Instantiate`/`Destroy`. This was
  already C8.1's constraint (no asset hunt, no rebuild-per-round); C8.1b
  extends it to character faces, environment dressing, and reaction motion
  rather than relaxing it.
- **Character over chrome.** The single biggest gap C8.1 left was that
  nothing in any world looked like *someone* — a target, a contestant, a
  suspect, a detective. C8.1b's first move in every world was adding a face
  (via `CharacterPrimitives`, section G) to the thing the player is meant to
  read as a character.
- **Restrained motion.** Every reaction is a short (0.2–0.5s), local,
  self-resetting animation — a punch, a shake, a color flash, an alpha
  fade — never a new persistent visual state left behind by accident. This
  is the same discipline the C7 shake bug and the C8.1 transition
  black-screen bug both forced onto the *shared* chrome; C8.1b's
  `LocalMotionFx` (section H) is that discipline made reusable for
  per-presenter motion too.
- **Read the archetype at a glance.** Each world's environment dressing
  exists to make the underlying challenge type identifiable before any text
  is parsed — a saloon sky and rock silhouettes for aim-and-shoot, stage
  curtains and lights for a TV contest, an industrial base plate for a
  physical machine, a marked back wall for a lineup. None of this changes
  what the archetype *is* (still `AimSelect`/`ChooseSide`/`Balance`/
  `DetectError` underneath) — only how it presents.

## C. Western Shootout polish

`Assets/Hermit/Runtime/GameFramework/Microgames/WesternShootoutPresenter.cs`

- **Environment:** a two-band sky/ground split, a warm semi-transparent sun
  glow, a horizon line, and two rock silhouettes for depth.
- **Characters:** each target now has a rounded head with a
  `CharacterPrimitives.Face`, a colored bandana (one of four palette
  colors, cycled per target), and the existing hat repositioned onto the
  new head — reading as an outlaw, not a labeled button.
- **Reticle:** rebuilt as an actual crosshair — an outer ring with a
  punched-hole center (a smaller panel matching the background color) plus
  two thin bars, instead of the C8.1 flat circle. A `MuzzleFlash` child
  Image sits on it for the hit flash.
- **Reactions:** correct hit → target's face switches to
  `FacePose.Correct`, a `LocalMotionFx.Punch` on the target, a dust-puff
  coroutine (scale 0.6→1.6, fade 0.7→0, explicit reset), and a
  `LocalMotionFx.FlashColor` on the muzzle flash. Wrong selection → the
  wrongly-picked target's face goes `FacePose.Incorrect` plus a
  `LocalMotionFx.Shake`. Timeout (`selectedIndex < 0`) → every active
  target gets a small shared shake ("all outlaws flinch").

## D. TV Game Show polish

`Assets/Hermit/Runtime/GameFramework/Microgames/GameShowPresenter.cs`

- **Environment:** curtain bars down each side, a floor line, three
  translucent stage lights along the top, and a purple backdrop panel —
  reads as a TV studio before any text is read.
- **Characters:** a presenter silhouette (with a `CharacterPrimitives`
  face) stands opposite the contestant, arm resting at an angle; the
  contestant is a larger `CharacterPrimitives` face, upgraded from C8.1's
  plain rounded head.
- **Prize board:** a small accent-colored panel labeled "PREMIO" that
  reacts to the outcome instead of being purely decorative.
- **Reactions:** correct → contestant face → `FacePose.Correct`, prize
  board flashes gold then settles to the correct color
  (`LocalMotionFx.FlashColor`), the presenter's arm punches up
  (`LocalMotionFx.Punch`), and a reusable 8-piece confetti burst plays
  (pre-built `Confetti` panels animated outward + faded via a dedicated
  coroutine, never `Instantiate`d). Incorrect → contestant face →
  `FacePose.Incorrect`, prize board flashes red then dims and shrinks to
  90% scale (an intentional persistent "the prize is off" state, not a
  stuck animation — see section H's note on this).

## E. Balance Machine polish

`Assets/Hermit/Runtime/GameFramework/Microgames/BalanceMachinePresenter.cs`

- **Environment:** an industrial base plate with two rivets under the
  post, and a pivot cap where the beam mounts — the C8.1 version had the
  beam floating on a bare post with nothing reading as "machine."
- **Level indicator:** a short bar on the pivot cap that rotates at half
  the beam's tilt angle, giving a second, smaller readout of imbalance
  independent of the beam's own scale.
- **Reactions:** correct → beam snaps level (rotation reset to identity,
  matching the correct value), a firm `LocalMotionFx.Punch` on the beam
  ("lock/clunk"), both pans flash the correct color. Incorrect → beam
  stays at its final (wrong) tilt, `LocalMotionFx.Shake` on the beam
  ("strain"), and whichever pan is heavier flashes the incorrect color.
  Per the C8.0 brief's own priority ("claridad > simulación física"), the
  tilt stays a normalized-angle rotation, not physics — clarity that the
  scale is unbalanced matters more than a physically accurate settle.

## F. Detective Lineup polish

`Assets/Hermit/Runtime/GameFramework/Microgames/DetectiveLineupPresenter.cs`

- **Environment:** a back wall with five lineup height-marker lines and an
  ambient overhead glow — the C8.0 Design Lock flagged this archetype's
  clarity as the one *unverified* Gold pick (unlike the other three,
  self-evident); the height-line wall exists specifically to make
  "interrogation room" legible without depending on the cue text.
- **Detective character:** a `CharacterPrimitives` face with a hat
  silhouette and a magnifying glass prop (a ring + punched hole + handle),
  watching from the side — pure dressing, never interactive.
- **Suspects:** each of the four suspect cards now carries a
  `CharacterPrimitives` face (from a four-color palette) instead of a flat
  gray panel.
- **Reactions:** correct accusation → a badge (`*` on a colored roundel)
  pops above the accused suspect via `LocalMotionFx.Punch`, plus the
  correct suspect's face → `FacePose.Correct`. Wrong accusation → accused
  suspect's face → `FacePose.Incorrect` plus a `LocalMotionFx.Shake`
  ("protest"), while the actual anomaly's face still turns `Correct`.
  Timeout (`accusedIndex < 0`) → a full-panel `LightsOutOverlay`
  (`CanvasGroup`, black, alpha 0→0.6→0) briefly dims the whole world
  ("lights out") since nobody was actually pointed at.

## G. CharacterPrimitives

`Assets/Hermit/Runtime/GameFramework/Microgames/CharacterPrimitives.cs`
(`internal static class`)

The shared face kit every character in all four worlds is built from — two
eye panels and one mouth panel, sized relative to a `headSize` parameter so
the same call works for a 46px presenter head or a 64px suspect card head.
`Build(Transform head, float headSize)` constructs the three child panels
once and returns a `Face` struct of cached `RectTransform`/`Image`
references; `ApplyPose(Face, FacePose, float headSize)` is a pure
size/color/position write on those cached references — `Idle` (small round
eyes, flat mouth), `Correct` (wide eyes, wide warm-colored mouth),
`Incorrect` (narrow eyes, short downturned mouth). No rebuild, no
allocation, safe to call every reveal. This is deliberately the *entire*
character system for C8.1b — no rigs, no bones, no per-character bespoke
drawing code; every world's characters are this same primitive reused with
different head sizes, colors, and props layered around it.

## H. LocalMotionFx

`Assets/Hermit/Runtime/GameFramework/Microgames/LocalMotionFx.cs`
(`internal static class`, plus `internal sealed class MotionHandle`)

The shared reaction-motion toolkit: `Punch`, `Shake`, `FlashColor`,
`FadeAlpha`. Every routine follows one contract, chosen deliberately
because it is the exact contract that would have prevented both the C7
shake bug and the C8.1 transition black-screen bug had it existed then:

1. A `MotionHandle` is allocated once per animatable element at `Build()`
   time, never per-trigger.
2. Calling the effect when `handle.Current != null` is a silent no-op — a
   trigger arriving mid-animation cannot stack or restart the coroutine.
3. The routine unconditionally restores the element to a defined rest
   state on completion (`localScale = Vector3.one` for `Punch`,
   `anchoredPosition = basePosition` for `Shake`, the given `restColor`
   for `FlashColor`, the given `to` alpha for `FadeAlpha`) and always sets
   `handle.Current = null` last.

Every presenter's correct/incorrect/timeout reaction in sections C–F is
built from these four calls — no presenter hand-rolls its own
restart-on-call coroutine, which is exactly the bug class this class exists
to close off. One deliberate exception worth naming so it isn't mistaken
for a bug: `GameShowPresenter`'s prize board settles to `localScale = Vector3.one
* 0.9f` on an incorrect answer — that is a **persistent design state**
("the prize is off"), not a `Punch` call, and is never expected to return
to 1.0 on its own; the regression test in section U's new test scopes
itself to `anchoredPosition` for exactly this reason.

## I. ProceduralAudio

`Assets/Hermit/Runtime/GameFramework/ProceduralAudio.cs` (`static class`)

Generates `AudioClip`s at runtime via `AudioClip.Create` + manual PCM
sample buffers — `Tone(name, frequency, duration, volume)` (sine),
`Sweep(name, fromFreq, toFreq, duration, volume)` (linear frequency sweep,
for the transition cut), and `Noise(name, duration, volume)` (filtered
noise, for incorrect feedback), each with attack/release envelopes to avoid
clicks. No external asset files. `ClasicoHud.Build()` builds one clip of
each kind once (`_commandClip`, `_correctClip`, `_incorrectClip`,
`_transitionClip`) and plays them via a single `AudioSource.PlayOneShot`
call from `ShowCommand()`, `RenderFeedback()`, and `PlayTransitionCut()`.
This lives in `ClasicoHud`, not per-presenter — see section J.

## J. Shared Hermit chrome

C8.1b changed **zero** of `ClasicoHud`'s own responsibilities as
established in C8.1: score/streak/timer/command/feedback/results/
transitions stay entirely in the shared outer chrome, and no presenter
reaches into or duplicates any of it. The two additions inside
`ClasicoHud.cs` this phase made are both chrome-level, not per-world:

- The audio clips and `PlayOneShot` calls described in section I.
- Presenter construction moved from field initializers to the constructor
  body in `ClasicoGameHost.cs`, so `_hud` (a `MonoBehaviour`) could be
  passed to all four presenter constructors — this is what lets
  `LocalMotionFx` run its coroutines on `ClasicoHud`'s own
  `MonoBehaviour`, the same pattern `ClasicoHud`'s own global punch/shake
  already used before C8.1b existed.

`GameFlowController`/`GameRegistry` still know nothing about any of this —
the architectural boundary from C8.1 is unchanged.

## K. Motion language

Two tiers, kept deliberately distinct:

- **Global chrome motion** (unchanged from C7/C8.1): `ClasicoHud`'s own
  punch/shake on the whole stage, the `CanvasGroup.alpha` transition fade
  between microgames.
- **Local character motion** (new in C8.1b): `LocalMotionFx` calls scoped
  to one element inside one presenter — a target, a beam, a suspect card,
  a prize board — never the presenter root and never another presenter's
  elements. A world's local motion never fires while a different world is
  active, since all four presenters are hidden/shown via `Hide()`/
  `ShowChallenge()` and their `MotionHandle`s only ever get triggered from
  that same presenter's own `RevealOutcome`/`ShowChallenge`.

## L. Feedback language

Correct and incorrect now read through three simultaneous, consistent
channels in every world: **face pose** (`CharacterPrimitives.FacePose`),
**color** (`Theme.Correct`/`Theme.Incorrect`, via `LocalMotionFx.FlashColor`
or a direct `Image.color` write), and **motion** (`Punch` on correct,
`Shake` on incorrect) — plus the pre-existing global chrome feedback
(`ClasicoHud.RenderFeedback`'s own banner/sound). Timeout is its own,
world-specific fourth state (flinch / dim prize / strain / lights-out)
rather than being silently folded into "incorrect," since C8.1 already
established `selectedIndex < 0` / `accusedIndex < 0` as the timeout
convention across all four presenters.

## M. Audio implementation/status

Implemented and wired (section I): a command cue, a correct tone, an
incorrect noise burst, and a transition sweep, all procedural, all played
through one shared `AudioSource` on `ClasicoHud`. **Status: implemented,
not yet manually verified** — automated tests do not and cannot assert on
actual audio output; whether the four sounds are distinguishable, at a
sane relative volume, and not annoying on repetition is part of the pending
manual validation in section V.

## N. Readability safeguards

- Every new decorative element (environment dressing, character props) is
  built at a fixed z-order relative to the interactive elements it
  surrounds — dressing is added to the presenter root or to a challenge
  card *before* the interactive buttons/text in build order, so buttons
  and labels are never occluded by a later, higher-sibling-index
  decoration.
- Text elements (`Statement`, `Equation`, `GroupLabel`, `Cue`, item labels)
  were not resized, recolored, or repositioned by C8.1b — only the
  environment around them changed.
- The confetti burst (section D) and the dust puff (section C) are both
  positioned and scaled to stay within their own presenter's stage bounds,
  never anchored to grow toward the shared HUD's timer/command text.

## O. Accessibility safeguards

- No new information is conveyed by color alone: every correct/incorrect
  state pairs a color change with a face-pose change and a motion cue
  (section L), so the same read is available to a player who cannot
  distinguish the accent colors.
- No new flashing exceeds roughly 2–3 Hz — `LocalMotionFx.FlashColor`
  durations are all ≥0.35s single transitions (one flash, not a strobe),
  and the Detective "lights out" dim is a single 0.15s-in/0.35s-out fade,
  not a blink.
- All existing keyboard navigation (`RuntimeUIFactory.ChainHorizontal`/
  `Select`) is untouched by C8.1b — no new interactive element was added
  outside the existing chained button sets, so the keyboard path through
  each world is identical to C8.1.

## P. Performance considerations

- All new visual elements (environment dressing, faces, badges, confetti
  pieces, the lights-out overlay) are built exactly once per presenter at
  `Build()` time, same as every C8.1 element — toggled active/inactive or
  animated in place, never `Instantiate`d/`Destroy`ed per round.
- The confetti burst reuses its 8 pre-built panels every time it fires;
  the dust puff reuses one coroutine pattern per target, not one object
  per particle.
- `ProceduralAudio` clips are generated once in `ClasicoHud.Build()` and
  cached for the process lifetime, not regenerated per round.
- `CharacterPrimitives.ApplyPose` and `RenderLiveValue`/`RenderDecision`
  remain the only per-frame work, and both are simple property writes on
  cached references, not layout rebuilds.

## Q. Regression guardrails

These are load-bearing lessons from earlier phases; C8.1b was required to
respect all of them, and did:

- **Never `Image.Type.Filled`/`Radial360` for a full-screen overlay
  without a valid rendering basis.** This was the C8.1 transition
  black-screen bug's root cause (see section S) — C8.1b did not touch
  `TransitionOverlay` and introduced no new `Filled`/`Radial360` usage
  anywhere.
- **Point-anchor-before-`sizeDelta` on every `CreateRoundedPanel`/
  `CreatePanel` child that needs a fixed size.** See section T.
- **Presentation coroutines must not restart every frame** and **must
  restore their original state on completion.** This is `LocalMotionFx`'s
  entire contract (section H) — every one of the ~15 new local-reaction
  call sites across all four presenters goes through it, none hand-rolls
  its own coroutine.
- **`StageRoot` respects `TopHudReservedHeight`.** See section R.

## R. Timer/top-HUD reserved zone

Established in the C8.1 layout-polish pass (not C8.1b): `ClasicoHud`
insets `_stageRoot` from the top of the canvas by
`TopHudReservedHeight = 112f`, so every presenter's top-anchored content
(concept text, group labels, cue text) is automatically pushed clear of
the shared `TimerBar` regardless of which world is active. C8.1b's new
environment dressing in every world (sky bands, stage curtains, back
walls, ambient glows) was built *inside* each presenter's own root, which
is already inset by this reserved zone — none of it needed, or was given,
a presenter-specific carve-out. `ShellRealSceneCompositionTests`'s
Timer-vs-concept overlap check (section U) covers this for the current
concept-element set, and continues to pass with C8.1b's dressing added
around those elements.

## S. Previous black-screen transition lesson

Recorded in full in `Docs/C8_1_GOLD_MICROGAME_SLICE.md`; summarized here
because C8.1b's guardrail in section Q depends on it. The root cause:
`TransitionOverlay` used `Image.Type.Filled` with `FillMethod.Radial360`
and no sprite assigned. The `CanvasRenderer`/`Graphic` API reported
everything correct (not culled, alpha 1, `fillAmount` update visible in
the Editor hierarchy inspector) while the actual GPU-rendered output
stayed a solid, un-clearing block — hierarchy/logical state was
insufficient to catch this; it was only caught by comparing a real
OS-level screenshot against Unity's own `ScreenCapture.CaptureScreenshot()`
(which was itself found unreliable for this specific bug) and by A/B
disabling the transition call. Fixed by replacing the fill-geometry
mechanism entirely with a `CanvasGroup.alpha` fade, which has no sprite/
fill-geometry dependency to go wrong. C8.1b's `LightsOutOverlay` (Detective
timeout, section F) and the confetti/dust-puff fades (sections C, D) all
use plain `Image.color`/`CanvasGroup.alpha` writes for exactly this reason
— never `Filled`/`Radial360`.

## T. Point-anchor-before-sizeDelta rule

`RuntimeUIFactory.CreatePanel`/`CreateRoundedPanel` stretch to fill their
parent by default (`StretchFull`: `anchorMin = 0, anchorMax = 1, sizeDelta
= 0`). Setting `sizeDelta` on a still-stretched `RectTransform` is an
*offset added to* the full stretched size, not an absolute size — so any
new fixed-size child must first reset `anchorMin`/`anchorMax` to a single
point anchor, *then* set `sizeDelta`, or it balloons to roughly the whole
parent's size. This was the root cause of the original oversized
Reticle/Spotlight bug in C8.1. C8.1b added roughly 30 new fixed-size
elements across the four presenters (sun, rocks, muzzle flash, stage
lights, presenter arm, prize board, confetti pieces, base plate, rivets,
pivot cap, level indicator, height lines' parent wall, ambient glow,
detective head/hat/glass parts, badge, lights-out overlay, and more) —
every one was built point-anchor-first, `sizeDelta`-second, and this was
re-checked by hand for each during implementation. `ShellRealSceneCompositionTests`'s
existing oversized-descendant check (section U) covers this generically
across all four presenter roots regardless of which one is active in a
given test run.

## U. Tests

No test infrastructure changed shape; one new `[UnityTest]` was added to
the existing real-scene fixture, matching the pattern the C8.1 layout-polish
pass already established (extend the shared fixture rather than add a
parallel one):

- `ShellRealSceneCompositionTests.RealShellScene_LaunchingClasico_VisiblyShowsAGoldMicrogamePresenter_NotTheOldC7Screen`
  — unchanged, still covers: exactly one presenter active, real on-screen
  size, under a Canvas, non-zero own/ancestor alpha, `TransitionOverlay`
  cleared, non-empty visible text, no oversized descendant across all four
  presenter roots, old C7 objects absent, Timer-vs-concept no-overlap.
- `ShellRealSceneCompositionTests.RealShellScene_DecisionTimeoutOnActivePresenter_RestoresLocalReactionTransforms`
  (**new in C8.1b**) — lets a microgame's decision window run out with no
  input (a presenter-agnostic way to trigger the "wrong/timeout" reaction
  path on whichever archetype the sequence happened to activate), captures
  every descendant `RectTransform`'s `anchoredPosition` just before the
  timeout, waits past the longest possible decision window plus lock plus
  feedback plus a settle margin, then asserts every one of those positions
  matches its pre-timeout baseline within 1.5px — the structural regression
  guard for `LocalMotionFx`'s "always restore on completion" contract
  (section H) as applied through real per-presenter reactions, not just
  the shared chrome's own punch/shake. Scoped to `anchoredPosition` only
  (not `localScale`/rotation) specifically because at least one presenter
  (`GameShowPresenter`'s prize board) has a legitimate persistent
  non-1.0-scale design state on incorrect — see section H's note.

**Current results (this session, Unity 6000.3.23f1, batch mode):**
compile clean, EditMode 103/103 passed, PlayMode 31/31 passed (was 30/30
before C8.1b's new test), `git diff --check` clean.

## V. Manual visual validation checklist

**None of the items below are marked PASS.** They are the checklist to run
through in the Editor; check them off only after you've actually looked.

### Western Shootout
- [ ] Immediately recognizable as a western scene
- [ ] Environment (sky/ground/rocks/sun) reads clearly, not as noise
- [ ] Targets look like characters (face + bandana + hat), not answer
      buttons with labels
- [ ] Reticle is small, readable, and doesn't obscure targets
- [ ] Mouse aiming feels like aiming (cursor/selection tracks naturally)
- [ ] Keyboard-only path works (navigate + confirm without a mouse)
- [ ] Correct-hit reaction (face, punch, dust puff, muzzle flash) reads
      clearly and isn't jarring
- [ ] Incorrect reaction (face, shake) reads clearly
- [ ] Timeout reaction (all-targets flinch) reads clearly
- [ ] All labels/text stay readable through every reaction

### TV Game Show
- [ ] Immediately recognizable as a television contest
- [ ] Presenter character is readable as a presenter, not a stray shape
- [ ] Contestant visibly reads as "nervous"/reactive, not static
- [ ] VERDADERO/FALSO choice is obvious at a glance
- [ ] Correct celebration (prize flash, arm punch, confetti) feels
      rewarding and isn't too long/short
- [ ] Incorrect reaction (prize dim/shrink) reads clearly
- [ ] Timeout state is distinguishable from a wrong answer
- [ ] Confetti never obscures the statement text mid-burst

### Balance Machine
- [ ] Machine reads clearly as a balancing mechanism (base, post, pivot,
      beam, pans)
- [ ] Beam tilt communicates "which side is heavier" at a glance
- [ ] Numbers (pan values, current adjustable value) stay readable while
      the beam is tilted
- [ ] Up/Down/Confirm controls are understandable without instruction
- [ ] Correct lock/clunk (beam levels, punch, pan flash) feels like a
      satisfying "settle"
- [ ] Incorrect strain (shake, tipped-pan flash) reads clearly as
      "wrong," not as a glitch
- [ ] No content overlap between equation text, value readout, and beam/
      pans at any tilt angle

### Detective Lineup
- [ ] Lineup/interrogation-room setting recognizable immediately
- [ ] "¿Cuál no pertenece?" is understood quickly (this is the archetype
      C8.0 flagged as unverified — pay particular attention here)
- [ ] Suspects are visually differentiated from each other (face + card
      color), not identical cutouts
- [ ] Detective character helps the framing rather than being a
      distraction
- [ ] Correct reaction (badge pop + culprit's face) reads clearly
- [ ] Incorrect reaction (protest shake + wrong suspect's face) reads
      clearly
- [ ] Timeout lights-out dim is noticeable but brief, not disorienting
- [ ] Spotlight and other FX never obscure suspect labels/faces

### Global
- [ ] Timer bar never visually overlaps any presenter's concept/command
      text, in any of the four worlds
- [ ] Command text ("¡Dispara!" etc.) is readable against every world's
      background
- [ ] Transitions between microgames feel fast and clean, no stutter
- [ ] No black screen, at any point, in any world, on any transition
- [ ] No oversized/overflowing decorative element in any world
- [ ] No element ends up visibly off-position after repeated
      correct/incorrect/timeout cycles on the same world (motion actually
      restores, not just in the automated test)
- [ ] Only one world is ever visible at a time, including mid-transition
- [ ] Full mouse-only playthrough (all four worlds, at least once each)
- [ ] Full keyboard-only playthrough (all four worlds, at least once
      each)
- [ ] At least one deliberate correct answer per world
- [ ] At least one deliberate incorrect answer per world
- [ ] At least one deliberate timeout per world
- [ ] Results screen displays correctly after a full run
- [ ] Retry correctly resets to a fresh run
- [ ] Return-to-selector correctly exits Clásico
- [ ] No regression in other games/flows (F1/C4 debug panel, the C4
      spike surfaces) after this change
- [ ] Verified on Windows standalone (not just the Editor Game view)

## W. Known limitations

- Audio is implemented but unverified by ear (section M) — volume
  balance, whether the four cues are distinguishable, and whether any
  becomes grating on repetition are all open questions.
- The Detective Lineup archetype's ≤1s clarity was flagged as unverified
  since C8.0 and remains the single highest-risk item in this checklist —
  the height-wall/cue-text framing added in C8.1b is an attempt to address
  it, not proof that it does.
- No player-facing settings exist yet for audio volume or reduced motion —
  section O's accessibility safeguards are built into the default
  presentation, not configurable.
- Windows-standalone-build validation (as opposed to Editor Play mode) has
  not been performed in this session.
- As with every prior phase, automated tests can prove structure
  (something exists, is sized correctly, is positioned correctly, returns
  to rest) but cannot prove that the result actually looks and feels good —
  that gap is exactly what section V exists to close.

## X. Criteria required before C8.1b can be marked FINAL/COMPLETE

All of the following must be true — none are true yet:

1. You have personally run through every unchecked item in section V in
   the Unity Editor (or a Windows standalone build, for the last item) and
   changed each checkbox to reflect what you actually observed.
2. Any visual or interaction problem found during that pass has either
   been fixed and re-verified, or explicitly deferred with your sign-off
   (recorded back into this document's Known Limitations, section W).
3. You have explicitly told me C8.1b is approved to be marked
   FINAL/COMPLETE.

Until all three are true, this document's status header stays
**IMPLEMENTED — MANUAL VISUAL VALIDATION PENDING**, and no other document
in this repository should describe C8.1b as complete or final.
