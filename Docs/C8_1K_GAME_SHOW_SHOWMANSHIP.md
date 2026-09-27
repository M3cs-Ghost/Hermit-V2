# C8.1k — Game Show Showmanship / High-Stakes Experience Pass

Goal: make the Game Show (ChooseSide / True-False) round read as a televised
quiz show — host, prize at stake, suspense, reveal — without redesigning the
accepted C8.1e/C8.1j stage, the presenter art, the True/False mechanic, the
content, or scoring. Western, Balance and Detective are untouched.

## Round flow

| Phase (director) | Duration | What the player sees |
|---|---|---|
| Intro | **1.5s** (`GameShowIntroSeconds`, was the shared 0.6s command beat) | Broadcast preamble (below). Input gated. |
| Decision | **4.2s** (`GameShowDecisionWindowSeconds`, C8.1l user-test pacing; was the shared 3.2s) | VERDADERO / FALSO live. Host idles. |
| Lock | 0.2s (unchanged, shared) | Chosen zone locked; no correctness shown. |
| Feedback | **4.4s** (`GameShowFeedbackDisplaySeconds`; C8.1k 3.4s, C8.1l +1.0s reading time) | +0.2s more suspense → reveal → explanation card (~3.8s readable hold). |

Both new durations follow the existing Balance/Detective per-archetype
override pattern (`ClasicoGameDefinition` + `ClasicoSessionDirector`), with a
`-1` sentinel in `CreateInMemory` so every existing in-memory test keeps the
shared timings. No other archetype's timing changes.

### Preamble (inside Intro)

- **0.00–0.40 Opening** — spotlight rises; host fades in with a small entrance
  punch (0.94 → 1.03 → 1.00). `GameShow_Opening`. Ambience bed starts.
- **0.30–0.75 Prize** — the `PrizePlaque` (PREMIO / $1,000,000; the
  `ProgressBadge` "PREGUNTA n" fades in with it)
  pops in (0.85 → 1.04 → 1.00) and a light sweep crosses it.
  `GameShow_PrizeReveal`.
- **0.75–1.10 Question** — host "presenting" nod; the statement panel
  fades/slides (18px) into its accepted position. `GameShow_QuestionReveal`.
- **1.00–1.30** — the choice zones light up to rest.
- **1.10–1.35** — the plaque settles to 0.92 scale / 0.82 alpha so the
  statement stays primary.

`RevealAfterIntro` (director Intro→Decision) force-finishes the preamble and
is still the only thing that enables input. The shared HUD "¡DECIDE!" banner
is suppressed for Game Show (same opt-out Western uses) — it would float over
the preamble.

### Answer lock → reveal

- On an **accepted** submission (`ClasicoGameHost.OnSelectionInput` calls
  `GameShowPresenter.LockAnswer` only after the director leaves Decision):
  both zones go non-interactable, the chosen zone gets an ivory focus glow and
  a held 1.04 scale, the other dims, the host leans in (1.015).
  `GameShow_AnswerLock`. No correctness is shown.
- Director Lock (0.2s) + presenter suspense (0.2s) ≈ **0.4s** click → reveal.
  A timeout (no lock) gets a 0.15s breath.
- **Correct**: correct zone green/gold + "RESPUESTA CORRECTA" tag + pop; small
  gold/ivory confetti rising from that zone; restrained green flash; host
  celebratory lift; prize plaque pulse + sweep. `GameShow_CorrectReveal`.
- **Incorrect / timeout**: chosen wrong zone muted red and slightly smaller;
  the actual correct zone gets the same green/gold confirmation + tag; host
  restrained deflate (dip/sink, hold, recover — never a shake); plaque dims.
  `GameShow_IncorrectReveal`.
- The shared HUD update (score/streak + global punch/shake) is handed to the
  presenter as a callback and fires at the reveal moment, not before it. The
  shared correct/incorrect ding and banner are opted out for Game Show.
- **+0.35s**: the `ExplanationCard` fades in with Game Show's own verdict
  (¡RESPUESTA CORRECTA! / RESPUESTA INCORRECTA / SE ACABÓ EL TIEMPO) and the
  challenge's `FeedbackExplanation` (content unchanged).

## Layout (1280×720 reference, stage coordinates from the stage bottom-center)

- `PrizePlaque` — **C8.1k.1: directly above the host** (he "owns" the
  prize), center (0, 481), 260×58 (PREMIO 13pt / $1,000,000 28pt), so y
  452..510 at rest. The host sprite's opaque head top is at y ≈445 (the art
  has a 46/1232 transparent top margin in the 420px actor rect). The plaque is
  built before the actor, so it draws *behind* him — his entrance/celebration
  lift can pass in front of it, never the reverse. (C8.1k had it stage-left at
  (-405, 345), 320×150.)
- `StatementArea` — **C8.1k.1: moved up 22px (-78 → -56)** to open that gap.
  -78 existed only to clear the shared "¡DECIDE!" banner, which Game Show no
  longer shows; the panel still clears the HUD's Salir button by ~8px. The
  C8.1j statement box/typography itself is unchanged.
- `ProgressBadge` ("PREGUNTA n" + gold underline) — split out of the plaque in
  C8.1k.1; upper stage-left, center (-440, 455), 220×34.
- `ExplanationCard` — stage-right, center (405, 335), 400×176; text box
  368×106, TMP best-fit 14–18pt.
- `CorrectTag` — 26px above each zone.
- Zones, host, and presenter art positions are unchanged.

`GameShow_ShowmanshipElements_DoNotOverlapStatementZonesPresenterOrHud`
verifies — at 1280×720 and 1920×1080, using the real CanvasScaler math — that
none of the new elements overlap the statement, zones, host, or the shared
HUD, and that the plaque is centred over the host and above his head; `GameShow_AllFeedbackExplanations_FitTheExplanationCard` and
`GameShow_VerdictLines_FitOnOneLine` verify text fit for all 30 challenges.
The C8.1j statement box/typography is unchanged.

## Audio hooks

Seven hooks, each loaded from `Resources/Audio/Gold/GameShow/<Hook>`:
`GameShow_Opening`, `GameShow_PrizeReveal`, `GameShow_QuestionReveal`,
`GameShow_AnswerLock`, `GameShow_CorrectReveal`, `GameShow_IncorrectReveal`,
`GameShow_AmbienceLoop`.

**C8.1k.1: a missing asset means silence.** The C8.1k noise-built temporary
fallbacks (and their `ProceduralAudio` generators) were removed after manual
review — the opening read as ocean/wave noise and the ambience as a sustained
"shhhh". No noise, ambience, tones or placeholder music is synthesized; the
ambience source only plays when a real `GameShow_AmbienceLoop` clip ships.
None ship today, so Game Show is currently silent apart from the shared HUD
transition cue.

Both AudioSources live under the Game Show root (`GameShowAudio`). Every fired
hook is logged to `GameShowAudioEvents` for tests. Final music is deferred.

## Lifecycle

All presenter motion runs on tracked coroutines plus a generation counter.
`CancelSequence` (called by `Hide()` — i.e. abort, restart, exit, next
archetype — and by every `ShowChallenge`) stops them all and stops both audio
sources; `ResetVisuals` then clears the preamble/prize/statement transition
state, lock state, result colors and tags, confetti, result flash, host
reaction scale/position, and the explanation card. The on-air "PREGUNTA n"
count resets on every session start (`ResetSession`).

## Manual acceptance (real Windows executable is the authority for pacing)

A. TV game show rather than a quiz card? B. $1,000,000 clear but not
casino-like? C. Host feels like a host? D. Enough suspense before reveal?
E. Correct feels rewarding? F. Incorrect clearly shows the right answer?
G. Still fast enough for Clásico? H. Statement and choices easy to read?
I. Clean reset between rounds?

## Future enhancement — presenter + contestant staging (not in C8.1k / C8.1k.1)

Recorded from the C8.1k.1 manual review as a future Game Show Gold
presentation expansion. Nothing is implemented or scaffolded for it yet.

Intended fantasy:

- a participant/contestant enters or is revealed;
- the presenter introduces the stakes;
- the $1,000,000 prize becomes part of the presenter + participant
  composition;
- the question sequence follows;
- the contestant visibly reacts to the result.
