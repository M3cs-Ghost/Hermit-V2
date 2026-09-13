# C8.0 — Clásico Design Lock

**Status: LOCKED.** This is the product-design source of truth C8.1 (and
every future Clásico phase) implements against — see
`Docs/C8_1_GOLD_MICROGAME_SLICE.md` for the C8.1 Gold Slice built from it.
This document is design, not architecture — it does not describe C# types;
see the C8.1 doc's "Naming" and "Framework impact" sections for how these
decisions became code. Ported here verbatim in substance from the C8.0
design-lock artifact produced during that phase; no design decision below
was altered while porting it into the repo.

## A. North Star

**Clásico is a 2-minute reflex gauntlet where accounting judgment is the
only tool that keeps the run alive.** Every few seconds the world changes
completely — the outfit, the rules of engagement, the character in front of
you — but the underlying ask is always the same shape: look, know, act,
before the scene cuts away. It should feel less like being quizzed and more
like being *ambushed*, repeatedly, by situations that happen to require
knowing whether Cuentas por Cobrar is an asset.

| Property | Definition |
|---|---|
| Ideal run length | ~2 minutes (100-130s core, one short intermission) |
| Microgames per run | ~16-18 at baseline pacing (design-lock target; C8.1 ships 8-10) |
| Ritmo | Escalating within the run; resets each run — no cross-run difficulty ramp |
| Progression | Entirely intra-run (Warm-up → Chaos); no meta-progression |
| Rejugabilidad | From theme-shuffling and content variance, not unlocks |
| Ends when | A fixed microgame count is cleared, or the Integrity Meter empties early |
| Doing well | Keeping Streak alive and Integrity high under rising Heat — consistency, not perfection |
| Failing | A broken run, not a broken player — the correct answer is always shown, the next scene always comes |

**Load-bearing line:** Clásico is a videogame whose game-material happens to
be accounting — not a quiz wearing a videogame's clothes.

## B. Core Loop

Warm-Up (2-3 microgames, generous timing) → Momentum (5-7, the real ramp) →
one Intermission (~1s, a score/streak callout, never a menu) → Speed-Up (4-5,
confusable distractors) → Final Rush (2-3, hardest tier on every axis at
once — the one place stacking every difficulty axis is deliberate) →
Results.

**Why 2 minutes, not 90s or 3 minutes**: 90s is closer to WarioWare's own
pace but under-serves content needing real cognition, not pure reflex. 3
minutes needs a second intermission, which fights the "continuous ambush"
feeling. 2 minutes gives 16-18 microgames — enough for Streak/Heat to mean
something — while staying short enough to invite an immediate replay, which
is where Clásico's actual replay value lives (§P), not a longer session.

## C. Session Arc

The emotional curve riding on top of the loop: **Warm-Up** (calm,
orienting) → **Momentum** (engaged, pattern-matching the command language)
→ **Speed-Up** (urgent, instinct over deliberation) → **Final Rush /
Chaos** (peak tension, no spare attention) → **Results** (release, not a
fifth tension beat).

## D. Microgame Grammar — ten mechanical archetypes

| Archetype | Core input | Tests | C8.1 example |
|---|---|---|---|
| Select | Pick 1 of N static options | Direct classification recall | — |
| Aim & Select | Move a cursor/reticle, confirm | Classification + spatial precision | Western Shootout |
| Choose Side | Binary left/right commit | Fast binary judgment | TV Game Show |
| Sort / Route | Send an item to a destination | Classification under a moving deadline | — |
| Sequence | Tap/arrange items in order | Process/order recall | — |
| Match | Connect pairs across two sets | Relational recall | — |
| Balance | Continuously adjust to equilibrium | Equation/identity reasoning | Balance Machine |
| Calculate | Produce/pick a numeric result | Arithmetic reasoning | — |
| Detect Error | Point at the anomalous item | Anomaly/audit-style scrutiny | Detective Lineup |
| Tap Timing / Catch | Act at the correct moment | Recognition under time pressure | — |

**Grammar rule**: every microgame concept declares exactly one primary
archetype. A concept needing two is really two microgames, or the wrong idea.

## E. Academic Challenge Types — ten content shapes

A Challenge Type is a data contract only — what it evaluates and what shape
its answer takes. No opinion about mechanics or theme.

| Challenge Type | Evaluates | Compatible archetypes |
|---|---|---|
| Classification | Category membership (Activo/Pasivo/Ingreso/Gasto…) | Select, Aim&Select, Sort/Route |
| TrueFalse | Whether a claim is correct | Choose Side, Tap Timing |
| DebitCredit | Which side an entry affects | Choose Side, Balance |
| Equation | Activo = Pasivo + Patrimonio | Balance, Calculate |
| FinancialStatement | Which statement an item belongs to | Sort/Route, Select |
| ErrorDetection | Spotting a wrong/anomalous item | Detect Error, Avoid |
| Sequence | Correct order of a process | Sequence |
| Matching | Pairing related concepts | Match |
| AmountCalculation | Basic arithmetic on values | Calculate, Select |
| AccountNature | An account's normal balance side | Choose Side, Select |

C8.1 implements exactly four: **Classification, TrueFalse, Equation,
ErrorDetection** — see `Docs/C8_1_GOLD_MICROGAME_SLICE.md`.

## F. Separation Model

Four layers, each only allowed to know about the layer directly below it:

```
Academic Content -> Challenge Type -> Mechanical Archetype -> Presentation Theme
```

Example: `"Cuentas por cobrar" -> Classification -> Aim&Select -> Western Shootout`.

**The rule that makes the system pay for itself**: "Cuentas por cobrar"
never knows Western Shootout exists, and Western Shootout never contains a
line of accounting logic. Western Shootout only knows "N labeled targets,
one correct index, a timer, an aim-and-confirm input" — the same shape any
Aim&Select-compatible Challenge Type can fill. Swap the Theme and the same
content plays as Space Defense instead; swap the content and the same Theme
teaches a different fact next run. This is also the entire replayability
engine (§P).

## G. Microgame Catalog (15 concepts)

Fifteen rather than twenty — depth over volume. Every archetype above is
exercised by at least one entry: Western Shootout (Aim&Select,
Classification), TV Game Show (Choose Side, TrueFalse), Boxing Ring (Choose
Side, DebitCredit), Balance Machine (Balance, Equation), Airport Customs
(Sort/Route, Classification/Statement), Factory Conveyor (Sort/Route,
Classification), Detective Lineup (Detect Error, ErrorDetection),
Restaurant Rush (Select, Classification), Racing Comparison (Calculate,
AmountCalculation), Elevator Floors (Select, FinancialStatement), Space
Defense (Aim&Select, Classification/Nature), Vault Combination (Sequence),
Cable Switchboard (Match, Matching), Bank Teller Alert (Avoid,
ErrorDetection), Falling Invoices (Tap Timing/Catch, AmountCalculation).

Full per-concept detail (fantasy, command, example, input, duration,
correct/incorrect, variants, difficulty) lives in the original design-lock
artifact; the four built for C8.1 are documented in full in
`Docs/C8_1_GOLD_MICROGAME_SLICE.md`.

## H. Gold Slice Recommendation

The originally-proposed Western Shootout / Game Show / Boxing / Balance
Machine set was revised: Game Show and Boxing are both **Choose Side**
underneath their costumes, and a Gold Slice exists specifically to prove
the grammar generalizes — two of four proving the same archetype
under-tests it.

**Locked recommendation**: Western Shootout (Aim&Select) · TV Game Show
(Choose Side) · Balance Machine (Balance) · **Detective Lineup** (Detect
Error, replacing Boxing) — four distinct archetypes, four distinct
challenge types, four distinct worlds, and a cost spread from cheap (Game
Show) to hard (Balance Machine) that surfaces production risk early. Boxing
isn't cut — it's the strongest candidate for the next batch (Débito/Crédito
is core content) — it just isn't a better *system-proving* pick than
Detective Lineup for this slice.

## I. Timing (baseline, single-tier)

`Scene Appear (0.0-0.4s) -> Command Beat (0.4-0.9s) -> Decision Window
(0.9-4.4s) -> Lock (4.4-4.6s) -> Feedback (4.6-5.3s) -> Cut (5.3-5.5s)`.
The "3-6 second" rule governs the *decision window*, not total scene time —
content and options must be legible and interactive from the same instant,
never staggered.

## J. Speed Escalation

Never just the timer. Four tiers each move seven knobs together: decision
window, distractor quality, scene noise, academic difficulty, step count,
input precision, intro length. **Hard rule**: the decision window never
drops below **1.2-1.5s**, at any tier — past that floor, escalate through
distractors/noise/content/steps, never through less time to think. Not
implemented in C8.1 (single tier only) — see the C8.1 doc's exact scope.

## K. Difficulty Model

Three axes, tuned independently: **A. Mechanical** (aim precision, steps,
noise, reaction window — set by tier), **B. Academic** (how hard the
underlying concept is — set once per content item), **C. Time pressure**
(wall-clock given — set by tier). Harder academic content is paired with
slightly more forgiving time pressure; only in Final Rush is stacking all
three deliberately allowed, as the run's one intended "boss moment."

## L. Scoring

`Total = Σ(correct microgame scores) + Streak bonuses + Perfect Clear
bonus`, where each microgame score = fixed BasePoints (by Challenge Type
difficulty tier: Easy 100 / Medium 150 / Hard 200) + a Speed bonus (0-50,
scaled by time remaining). Difficulty lives in the fixed base-point table,
never as a runtime multiplier — repeating C7's own rejection of unbounded
multiplier growth as an "exploit evidente." C8.1 ships Base + Speed only
(see its doc).

## M. Combo / Momentum

**Streak** stays the scoring backbone (unchanged C7 mechanic — flat bonus
every Nth hit). **Heat** is new: a purely presentational intensity layer
rising/falling with Streak (screen vignette, music percussion, character
reactions) — never a second number competing with Score. Lives and a
second multiplier stack were both rejected (lives conflict with the
never-punish value; a second multiplier reopens the growth problem already
solved for Streak). Not implemented in C8.1.

## N. Failure Model

**Integrity Meter** — a soft "system health" bar, not discrete lives. A
miss costs a chunk and breaks Streak; consecutive misses cost progressively
more; a correct answer partially refills it, so consistency literally
repairs the run. Reaching zero ends the run early ("Corte de sistema").
Sized generously early (3-4 consecutive misses to drain at Warm-Up) so a
single mistake never ends a session. Not implemented in C8.1.

## O. Results

Score, Accuracy%, Best Streak, Microgames cleared (X/Y), Average response
time, Grade (S/A/B/C/D, Results-only), Strongest/weakest domain (by
Challenge Type miss tally). Plus an optional collapsed "Repasa esto" list
(2-3 most-missed concepts). The single largest element besides the score is
**Jugar Otra Vez**. C8.1 ships score + accuracy + microgames cleared only —
barebones, deliberately (see its doc).

## P. Replayability

In order of leverage: **(1) Theme shuffling** — the same Challenge Type
instance can surface through any compatible Theme, the highest-leverage
lever since it's cheaper than new content; (2) run-seed variance in which
concepts appear and in what order; (3) rare events (Golden Round, surprise
character cameo, a Sudden Death microgame reserved strictly for bonus
contexts) — deferred past C8.1; (4) recurring character gags that deepen
with repeat plays. Not relying on reshuffled questions alone.

## Q. Characters — seven recurring archetypes

El Forastero Nervioso, El Sheriff Implacable, El Auditor Severo, El
Empresario Caótico, El Presentador Estelar, La Cajera en Pánico, and
**Hermit** (the mascot — never inside the 3-6s decision beat itself, only
transitions/Results/rare events; peeks further out of its shell the higher
the Streak climbs). C8.1 ships one lightweight character per Gold microgame
at prototype fidelity — see its doc.

## R. Visual Worlds

Ten, already earned by the catalog: Wild West, TV Studio, Boxing Arena,
Factory, Space, Airport, Detective Office, Restaurant, Bank Teller Counter,
Museum. Deliberately not expanded beyond what the catalog uses — new worlds
should arrive with a new mechanical concept attached, not on their own.

## S. Hermit Visual Glue

What never changes regardless of world: one HUD frame (score/streak/timer
chrome, `HermitTheme` tokens), one Command Typography treatment, one
Correct/Incorrect color law (`Theme.Correct`/`Theme.Incorrect`, never
re-skinned per world), one transition family, the Hermit mascot cameo at
consistent beats, one command-cue sound identity.

## T. Humor

Visual, fast, absurd, dry, exaggerated characters. Lives entirely in
reaction-animation timing, not dialogue — a stone-faced Auditor slowly
lowering his glasses *is* the joke. Comedic beats resolve inside the
existing Feedback window; humor never asks for extra time budget. Avoid:
dated memes, long dialogue, jokes that interfere with comprehension,
infantilization.

## U. Audio Direction (conceptual, no assets)

Command cue (a consistent rhythmic stinger, world-independent), Correct
(bright ascending chime, same everywhere — audio's own color law),
Incorrect (a short low buzz, never punitive-sounding), Countdown (reuse
C7's rising tick), Heat/Combo (layered intensity, not a discrete jingle),
Speed-up (a gear-shift whoosh per tier boundary), Results (a resolved
musical landing), Transitions (one consistent whip/swoosh cut). C8.1
ships without audio (deferred, documented — see its doc).

## V. Music Strategy

**One continuous adaptive beat-bed** for the whole run, layering up across
the four tiers, rather than per-world music beds — swapping full tracks
every ~5 seconds would undercut Hermit's glue and multiply production cost.
Individual world SFX layer on top of, not instead of, the one bed. Not
implemented in C8.1.

## W. Transitions

One signature Hermit radial iris/shutter wipe (~0.15-0.2s, tinted with the
Theme accent) as the default, plus a golden-tinted variant reserved for
rare events. Card-flip/distortion rejected as too slow at these durations.

## X. Instruction Language

**Rule**: the command word binds to the Mechanical Archetype, never the
Presentation Theme, so vocabulary stays small and teachable as the catalog
grows. Initial vocabulary: ¡DISPARA!/¡DEFIENDE! (Aim&Select), ¡DECIDE!/
¡GOLPEA!/¡ACELERA! (Choose Side), ¡ELIGE!/¡SUBE! (Select), ¡ENVÍA!/
¡CLASIFICA! (Sort/Route), ¡ORDENA! (Sequence), ¡CONECTA! (Match),
¡BALANCEA! (Balance), ¡ENCUÉNTRALO!/¡SEÑÁLALO! (Detect Error), ¡ATRAPA!
(Tap Timing/Catch), ¡BLOQUEA!/¡SALVA! (Avoid).

## Y. Learning Safeguards

Natural spaced repetition from theme-independent content resurfacing across
runs; the correct answer is always shown at the moment of a miss; harder
content gets relatively more time (§K); Results' "Repasa esto" surfaces
missed concepts once the adrenaline has passed. Deliberately deferred: true
mastery/spaced-repetition scheduling. Clásico is not becoming an LMS.

## Z. Wrong-Answer Correction

≤0.8s at baseline, ≤0.4s at Final Rush, always inside the existing Feedback
beat. The correct element highlights with color and a brief label — no
inline text explanation, ever. A deeper explanation is Results-only,
optional, one line. **Rule of thumb**: if a correction needs more than a
single glance at a single highlighted element, the content doesn't belong
in Clásico's format at all (§AA).

## AA. Content Boundaries & What Clásico Is NOT

Doesn't fit the format: 100+ word prompts, multi-step calculations needing
scratch work, free-form output (typed numbers/entries) rather than
selection/manipulation, debatable "it depends" content, distractors that
can't be dramatized visually. Deferred to a future, slower Hermit mode —
not deleted.

**Modo Clásico NO es**: un examen (no penalización terminal por una falla),
un banco de preguntas (cada pregunta vive dentro de una escena), un
simulador contable profundo, un curso, un tutorial largo, un modo historia,
sustituto de problemas complejos.

## AB. Accessibility

Every archetype needs a defined non-mouse-exclusive control scheme *before*
it's greenlit: Select/Choose Side → arrow/WASD + Enter; Aim&Select →
reticle via arrows, confirm Enter/Space; Sort/Route → cycle-select +
confirm; Sequence → number-key or arrow+confirm; Balance → arrow up/down +
Enter; Tap Timing/Catch → a single confirm key. Feedback never relies on
color alone; text never drops below the existing Theme scale; no aggressive
flashing; the 1.2-1.5s decision floor (§J) guarantees a fair minimum
reading time.

## AC. Production Feasibility

For the Gold Slice: Western Shootout (Eng Med, Art Med, high reuse), TV
Game Show (Eng Low, cheapest, highest scalability), Balance Machine (Eng
Med-High — the one real production risk), Detective Lineup (Eng Low-Med,
high reuse). Three of four lean Low/Med across the board.

## AD. Framework Impact

**The single most important structural finding**: today's
`ClasicoGameEngine` (one fixed Q&A rhythm) is not shaped to host a rotating
sequence of *heterogeneous* microgames. It likely needs to become a
**SessionDirector** hosting several small per-archetype micro-engines — but
this fits entirely inside the existing `IGameEngine` slot the outer
framework already sees, exactly like Countdown was added in C7 without a
new outer state. `GameFlowController`/`GameRegistry` need zero changes.
`Hermit.Networking` is not to be touched. See
`Docs/C8_1_GOLD_MICROGAME_SLICE.md`, "Migration strategy" for how this was
actually resolved.

## AE. Naming

`ClasicoSessionDirector`, `MicrogameDefinition`, `MicrogameChallenge`,
`MicrogamePresenter`, `MicrogameResult`, `MicrogameArchetype`,
`AcademicChallengeType` — see the C8.1 doc's "Naming" section for which of
these were actually built, and under what final names.

## AF. Decision Matrix

| Candidate | Fun | Ed. clarity | Visual personality | Impl. cost | Scalability | Input variety | Replay | Verdict |
|---|---|---|---|---|---|---|---|---|
| Western Shootout | High | High | High | Med | High | High | High | **Gold** |
| TV Game Show | High | Very High | Med | Low | Very High | Low | Med | **Gold** |
| Balance Machine | Med | High | Med | Med-High | Med | High | Med | **Gold** |
| Detective Lineup | Med | High | Med | Low-Med | High | Low | Med | **Gold** |
| Boxing Ring | High | Very High | High | Med | High | Low | Med | Next batch |

## AG. C8.1 Scope (as locked here; see the C8.1 doc for what was actually built)

Prove archetype-switching works before investing in pacing: a minimal
runtime for exactly the four Gold archetypes, a minimal SessionDirector
sequencing a short fixed run (8-10 microgames, single tier), the four Gold
microgames with placeholder/geometric art, one prototype-fidelity character
per microgame, Base+Speed scoring only, a barebones Results screen.
Explicitly out of scope: speed escalation, Heat, Integrity Meter, rare
events, adaptive music, full character roster, worlds beyond the four
picked.

## AH. Risks / Open Decisions (as locked here)

SessionDirector migration path (replace in place vs. new IGameEngine
alongside) left to C8.1 planning; Balance Machine's engineering cost is the
biggest unknown; Detective Lineup's ≤1s clarity is unverified and needs
scrutiny during prototyping; Theme↔Challenge pairing width (full mixing vs.
a curated allowlist) needs a compatibility matrix, not assumed; Integrity
Meter numbers need playtesting, not paper values; music-bed resourcing is
outside this design phase's scope.
