# C8.1e — Game Show Gold Art Replacement

**Status: VISUAL GOLD PASS ACCEPTED — SHOWMANSHIP / EXPERIENCE POLISH
DEFERRED.** (Step 2 implementation, validated in Step C8.1e.1 against the
real Windows build below). This status applies to **Game Show only** — it
does not mark the rest of Clásico Gold. Not reopened or modified in the
C8.1f.2 correction pass — recorded here only to make explicit that visual
integration acceptance is not the same as calling Game Show experientially
finished: a stronger preamble/show-opening, prize stakes, a real
"you are competing" feeling, and dedicated Game Show audio/showmanship all
remain outstanding future work, not permanently dropped ideas.

Step 1 (below) found the world/stage art already Gold-ready, but the only
presenter candidate at the time unusable for a clean in-world cutout. A
second candidate (`Presentador/Gameplay/Presentador_Gameplay_Candidate_01.png`,
a flat chroma-green render) unblocked Step 2 — see "Step 2 — Implementation"
further down for the full record of what was built.

---

## Step 1 — Audit (original findings, preserved as written)

This phase's brief explicitly required two steps: (1) audit the current
Game Show presentation and existing art, and define the exact Gold asset
plan; (2) implement, but *only if* existing art is sufficient — otherwise
stop and report exactly what new art is needed. Step 1 is complete. It
found the world/stage art is Gold-ready as-is, but the presenter art is
**not** usable for the brief's core requirement ("characters are actors
inside the world," never a portrait card) without new or re-processed art.
**No code was changed in this phase** — this document is the full Step 1
report the brief asked for.

## A. Current Game Show runtime hierarchy

From `Assets/Hermit/Runtime/GameFramework/Microgames/GameShowPresenter.cs`
(unchanged, read-only audit):

```
GameShow (Image panel, dark purple fallback color)
├─ Background (Image, full-bleed — Art/Gold/GameShow/GameShowBackground, currently resolves)
├─ Curtain ×2, Floor, StageLight ×3          [procedural fallback — inactive, since Background resolves]
├─ Backdrop (rounded panel, transparent when Background art is present)
│  ├─ Statement (Text — the True/False prompt)
│  ├─ PresentadorCard (CreatePortraitFrame: frame + "Portrait" Image)   [ACTIVE — art resolves]
│  │  — OR, if presenter art were missing: PresenterBody/PresenterHead(+CharacterPrimitives)/PresenterArm (procedural fallback)
│  ├─ ContestantHead (CharacterPrimitives face — always procedural, no contestant art exists or was requested)
│  └─ PrizeBoard (rounded panel, Theme.Accent) + "Prize" Text ("PREMIO")
├─ Confetti ×8 (plain colored rects, reusable pool, animated on correct)
├─ GameShowTrue (Button, "VERDADERO", plain rounded rect)
└─ GameShowFalse (Button, "FALSO", plain rounded rect)
```

Built once in `ClasicoGameHost`'s presenter set-up (never rebuilt per
round), toggled active/inactive via `Build`/`Hide`, matching every other
`IMicrogamePresenter`.

## B. Which elements are procedural

- Fallback curtain/floor/stage-light primitives (inactive today, since
  `GameShowBackground` art resolves — kept only as the missing-asset
  degrade path, per this project's "warn and fall back, never crash"
  convention).
- `ContestantHead` — always `CharacterPrimitives` (a procedural
  two-eye/one-mouth face kit). No contestant art exists, and the brief
  doesn't ask for any.
- `PrizeBoard` — a plain `Theme.Accent`-colored rounded rectangle with a
  "PREMIO" label. Purely procedural, no art backing it, no counterpart in
  the brief's own stage-language list.
- `Confetti` — 8 plain colored rectangles, animated via a burst-out
  coroutine on correct. Procedural motion FX, not art.
- `GameShowTrue`/`GameShowFalse` — plain `RuntimeUIFactory.CreateButton`
  rounded rectangles, theme-colored. Zero stage integration today — this
  is exactly the "giant rectangular UI buttons" the brief wants replaced
  with stage-integrated choice zones.

## C. Which elements are image/card-based

- `Background` — the illustrated Game Show stage (see Section D/J below).
- `PresentadorCard` — `RuntimeUIFactory.CreatePortraitFrame` wrapping the
  Presentador art: a bordered rounded-rect frame with the portrait inset,
  positioned in the lower-right of the stage. **This is precisely the
  pattern C8.1d's own "Manual Validation Correction — Portrait Integration
  Rejected" section (`Docs/C8_1D_GOLD_ART_INTEGRATION.md`) already
  identified as visually wrong** — that correction was applied to Western
  (replaced with an in-world actor) but explicitly left Game Show and
  Detective untouched "until Western's model is validated." Western's
  model *has* since been validated (shipped, manually approved). Game
  Show's presenter is the exact same rejected pattern, still live in
  production today.

## D. Which elements can be reused

- `GameShowBackground` (the illustrated stage art) — see Section J. Usable
  as-is, no new art needed for the background itself.
- `AnswerChosen` event, `ShowChallenge`/`RevealOutcome`/`Build`/`Hide`
  contract, and the exact GameObject names `GameShowTrue`/`GameShowFalse`
  — load-bearing across the test suite (`ClasicoPlayModeTests.cs` lines
  140-141, 212, 917; `ShellPlayModeTests.AnswerCurrentMicrogame`) and must
  not change.
- `LocalMotionFx.Punch`/`FlashColor` — the same reusable motion vocabulary
  Western already uses for reactions; directly applicable to Game Show's
  correct/incorrect feedback without new art.
- The confetti burst mechanic — a reasonable "correct" celebration cue,
  reusable as-is or lightly restyled to the new palette.
- `TopHudReservedHeight` (112f, `ClasicoHud.cs`) — already respected
  automatically, since `GameShowPresenter.Build` is parented under the
  same `stageRoot` every presenter uses; no special handling needed.

## E. Which elements should be retired visually

- `PresentadorCard` (the framed portrait) — per Section C, this is the
  rejected pattern. Must become an in-world actor, not a card.
- `PrizeBoard` — not mentioned anywhere in the brief's stage-language
  vocabulary (host/contestant positions, stage architecture, lighting,
  choice zones, audience energy). A candidate for retirement or
  reimagining as part of the stage's own set design (e.g. folded into the
  big central screen the background art already depicts) rather than a
  freestanding colored rectangle — a Step 2 decision, not resolved here.
- `GameShowTrue`/`GameShowFalse`'s current plain-rectangle presentation —
  needs the "architectural answer panel integrated into set design"
  treatment the brief asks for (Section 7/8 of the brief).
- The procedural curtain/floor/stage-light fallback — no longer relevant
  to *this* asset (Background already resolves) but should stay as the
  missing-asset degrade path per project convention; not something to
  delete.

## F. Gameplay events presentation must continue to support

Exactly what `IMicrogamePresenter` + `GameShowPresenter`'s own public
surface already require — none of this may change without breaking
`ClasicoGameHost`, `ClasicoSessionDirector`, or the test suite:

- `event Action<bool> AnswerChosen` — `true` for VERDADERO, `false` for
  FALSO.
- `void Build(Transform stageRoot)` / `void Hide()` (the `IMicrogamePresenter`
  contract).
- `void ShowChallenge(TrueFalseChallenge challenge)` — sets the statement
  text and resets visual state (button colors, presenter/prize scale,
  contestant face to Idle).
- `void RevealOutcome(int selectedIndex)` — `-1` means the decision window
  timed out with nothing chosen (always incorrect, same convention as the
  other two selection-based presenters); `0`/`1` map to
  True/False via `challenge.IsTrue ? 0 : 1`.
- The exact GameObject names `"GameShowTrue"` and `"GameShowFalse"` —
  every test that drives Game Show finds buttons by these names directly.

## G. Existing presenter art candidates

`ArtBible/Candidates/Presentador/` contains exactly **one** file:

| File | Dimensions | Pose |
|---|---|---|
| `Presentador_Hybrid_Candidate_01.png` | 928×1232, `Format24bppRgb` (no alpha) | Full-body, dynamic three-quarter stance, one arm extended outward/upward in a presenting gesture, the other holding a microphone on a stand, broad genuine TV-host smile. Costume: purple/violet tuxedo with gold/pink geometric piping. |

This is already the exact file copied, unprocessed, into
`Assets/Hermit/Content/Resources/Art/Gold/GameShow/Presentador.png`
(confirmed byte-identical via `cmp`) — i.e. today's "Gold" presenter art
*is* this same raw candidate, currently displayed inside the rejected
portrait-card frame.

**Does it work as an in-world actor?** The pose/costume/design is strong
and usable in principle — but the file has **no alpha channel and a
non-flat studio backdrop** (a soft directional gradient/vignette, sampled
corners ranging roughly from `(121,102,97)` to `(244,199,174)`, with a
visible warm "hot spot" concentrated toward the lower-right, not a simple
photography two/three-point gradient). This is fundamentally different
from how Western's Sheriff/Outlaw candidates were built — those used a
**flat, saturated, single-color chroma-key backdrop** (confirmed
`Format24bppRgb`, sampled to a consistent magenta family per file),
purpose-built for reliable automated background removal (see
`Docs/C8_1D_GOLD_ART_INTEGRATION.md`, "Background-removal method").

**I attempted the extraction anyway**, rather than assuming failure from
inspection alone. Since a flat-color chroma key (Western's method) cannot
handle a gradient, I built a more sophisticated variant of the same
"small Add-Type C# tool run through PowerShell" approach Western's own
pipeline used: a Coons-patch bilinear interpolation from all four image
borders (not just corners) to estimate the expected local background
color at every pixel, then keyed on distance from that local estimate
(with the same edge-decontamination step Western's pipeline used).
Border-transparency came out to 99.9% (comparable to Western's own
98.8–100% validation bar) and the character's silhouette itself extracted
cleanly — face, hands, suit, shoes all intact, no holes.

**However, full-resolution visual inspection found a real defect**: a
visible vertical band of residual, incompletely-keyed background (a hazy,
faintly-colored streak) in the gap between the character's raised arm and
torso. Diagnosis: the backdrop isn't a clean analytic gradient — it has
the kind of subtle painterly/textured variation typical of AI-generated
"studio backdrop" renders, which a smooth border-interpolation model
cannot fully capture (a discontinuity in the middle of the image doesn't
show up in border samples at all). This is not a threshold-tuning
problem; it's a mismatch between the tool and this specific asset's
actual structure. Per the brief's explicit instruction ("do not improvise
low-quality procedural substitutes just to finish implementation"), this
result is **not shipped** — no processed file was written into the
project, and `ArtBible/Candidates/Presentador/` was never modified.

**Conclusion: this candidate is not currently usable as a clean in-world
actor asset without further work outside this session** — either (a) a
re-render with a flat, solid, chroma-key-friendly backdrop (matching
exactly what worked for Western), or (b) manual alpha cleanup of this
same candidate in Affinity, which is the exact human-in-the-loop step
this project's own established pipeline already names for this situation
(`Midjourney/approved raster art → alpha cleanup if needed → Affinity as
review/cleanup/export station → Unity`). Neither (a) nor (b) can be done
inside this session — no candidate here has been called "approved," and
none of the above changes anyone's earlier approval status.

Only one pose exists. No dedicated reaction/gesture pose exists, but per
Section K below, none is strictly required — the single hosting pose,
combined with the same transform/light-based reaction language Western
already uses, should be enough once real transparency exists.

## H. Existing Game Show world art

`ArtBible/Candidates/Worlds/` contains, for Game Show:

| File | Dimensions | Notes |
|---|---|---|
| `GameShow_Hybrid_Candidate_01.png` | 1456×816, `Format24bppRgb` | Note: the brief expected this under a `Worlds/GameShow/` subfolder — it actually lives directly under `Worlds/`, alongside `Western_Hybrid_Candidate_01.png`. Flagging the path discrepancy rather than silently assuming. |

**This is genuinely excellent, Gold-ready stage art.** A premium
theatrical TV quiz-show stage: deep violet/indigo curtains and set walls,
warm gold trim and accent lighting, a large central screen/backdrop
panel, an overhead lighting rig with multiple visible spotlights and
lens-flare highlights, a geometric hexagonal-faceted stage platform, and
a silhouetted audience along the bottom edge. This matches the brief's
requested direction almost exactly: theatrical architecture, strong
central stage composition, rich studio lighting, deep indigo/violet/
amber/gold palette, premium 2D illustration, adult-friendly, energetic
without being childish or neon-casino.

**Already in production**, byte-identical to
`Assets/Hermit/Content/Resources/Art/Gold/GameShow/GameShowBackground.png`
(confirmed via `cmp`) — the exact same file `GameShowPresenter.cs`
already loads today. No copying, import-setting fixes, or new art needed
for the background itself.

**Suitability for left/right choice**: the stage as composed is
symmetric and does not have two visually pre-separated podium/lane zones
baked into the art. This is not a defect — it's a single wide establishing
shot, and the brief's own Section 7 explicitly allows building the "two
zones" via UI-side treatment ("two illuminated stage zones... Labels may
remain Unity UI but must visually belong to the stage"). The stage's own
lighting rig (multiple distinct spotlight cones already painted into the
art, roughly symmetric left/right of center) gives a natural anchor for
laying two illustrated/alpha-overlay "spotlight pools" or podium-panel
graphics on the left and right thirds of the existing floor — achievable
with the same restrained alpha-overlay technique already proven in
`StartScreenHud` (fog/bloom) and `ArcadeGalleryHud` (vignette), not new
painted art.

**Presenter staging fit**: the stage's central screen/platform area
(roughly the middle third, above the geometric dais) reads as the natural
presenter position — center, slightly elevated, matching the brief's
"preferred positioning." This composition supports the brief's staging
intent well; it is the presenter's own art (Section G), not the stage,
that blocks proceeding.

## I. Gold asset strategy (what this audit concludes)

| Category | Status |
|---|---|
| A. Stage/background | **Sufficient today.** Already production-ready, already wired. Zero new art. |
| B. Presenter neutral/hosting pose | **Blocked.** Candidate exists and is well-designed, but cannot be reliably converted to a transparent in-world asset without a re-render (flat backdrop) or manual Affinity cleanup — see Section G. |
| C. Presenter reaction/gesture pose | **Not required**, per Section K — the single hosting pose plus motion/light FX should suffice, matching Western's own "pose swaps, transform motion, light/FX" philosophy. Revisit only if manual validation later finds the single pose insufficient. |
| D. Left/right stage choice treatment | **Achievable without new art** — procedural alpha-overlay spotlight/panel treatment on the existing stage art (Section H). |
| E. Feedback lighting/effects | **Achievable without new art** — `LocalMotionFx` + alpha-overlay light pulses, the same vocabulary already proven across every Gold presenter. |

**Net conclusion: the single blocking item is the presenter's alpha
cutout.** Everything else in the brief's requested Gold asset set is
either already available or buildable with existing techniques and zero
new art.

## J. Recommended Gold runtime hierarchy (once presenter art is resolved)

Not implemented this phase — recorded here as the Step 2 plan, to avoid
re-deriving it once art is ready:

```
GameShow (root panel)
├─ Background (Image, GameShowBackground — unchanged)
├─ LeftChoiceZone (alpha-overlay spotlight/panel treatment, procedural)
│  └─ GameShowTrue (Button, restyled to belong to the stage panel, label kept legible)
├─ RightChoiceZone (mirror of the above)
│  └─ GameShowFalse
├─ Presenter (Image, real transparent cutout, positioned center/elevated — an
│  in-world actor per Western's own model, NOT a CreatePortraitFrame card)
├─ Statement (Text — unchanged position/role, likely relocated to read
│  clearly against the new composition)
├─ ContestantHead (unchanged — procedural, out of this phase's scope)
├─ RevealSpotlight / feedback light overlays (alpha-only, Correct/Incorrect accent color)
└─ Confetti ×8 (unchanged, or restyled to the new palette)
```

`PrizeBoard`'s fate (retire vs. fold into the stage's own screen) is a
Step 2 decision, not resolved here.

## K. Recommended intro sequence (Step 2 plan, not implemented)

Per the brief's explicit "own identity, punchier than Western, ~1.5-2.2s,
not a 6-second cinematic":

| t | Beat |
|---|---|
| 0.00–0.40 | Stage dark / ambient ready state |
| 0.40–0.90 | Stage lights snap on (alpha-overlay flash/sweep) |
| 0.90–1.40 | Presenter's actor sprite becomes active/settles (scale or alpha-in, not a full cinematic camera move) |
| 1.40–1.80 | Left/right choice zones illuminate |
| 1.80+ | Gameplay begins (buttons interactable) |

This reuses this project's established "named-beat coroutine" pattern
(`WesternShootoutPresenter.CinematicIntroRoutine`, `StartScreenHud.TransitionRoutine`)
at roughly a tenth of the Start Screen's own duration, deliberately far
shorter than Western's own Encounter intro, per the brief's explicit
"needs its own identity" instruction.

## L. Recommended feedback (Step 2 plan, not implemented)

**Correct**: choice → lock (buttons non-interactable) → chosen zone's
panel confirms (accent-colored light rise) → presenter reaction (a
`LocalMotionFx.Punch` on the actor sprite, the same technique already
used for Sheriff/outlaws) → brief celebratory light sweep (alpha overlay)
→ confetti (kept, restyled) → next challenge. Never a generic "ding + green
rectangle."

**Incorrect**: choice → lock → wrong zone dims/reddens → correct zone
reveals (accent light rise on the *other* side) → presenter reaction
(`LocalMotionFx` a subdued/negative variant — e.g. a small alpha-fade
or FlashColor rather than Punch) → short failure cue → next challenge. No
comedy beyond what's already established as Hermit's tone elsewhere.

## M. Audio placeholder plan (Step 2 plan, not implemented)

Per the brief, procedural placeholders only, extending
`ProceduralAudio`'s existing tone/sweep/noise vocabulary (the same
approach already used for Start Screen's confirm chime/entry swell and
Clásico's own feedback dings) — never canned laughter/applause/jackpot/
buzzer samples:

- A short "stage reveal" sting for the intro's lights-on beat.
- A choice-lock cue (distinct from Western's own gunshot-family sounds).
- Correct/incorrect accent tones (can likely reuse `ClasicoHud`'s existing
  shared feedback `AudioSource` and cue set rather than adding new clips,
  pending a Step 2 look at what already exists there).
- Optional: a very subtle audience/stage ambience bed, restrained.

No final music. Not implemented — recorded as direction only.

## N. Tests

**No tests were added or changed this phase** — no implementation
occurred. For the record, once Step 2 proceeds, `GameShow_ShowsIllustratedBackgroundAndPresentadorCard`
(`ClasicoPlayModeTests.cs`) will need structural updates (it currently
asserts on `"PresentadorCard"`/`"Portrait"` by name, which won't exist
once the portrait-card is retired) — this is expected, not a regression,
and should be renamed/rewritten alongside the presenter change itself,
never left half-updated.

## O. EditMode / PlayMode result

Not run this phase — no code changed. The last known-good baseline
(recorded in the C9.2a checkpoint) was 110/110 EditMode, 65/66 PlayMode
(the one failure being the already-documented, pre-existing, non-
deterministic Detective/RNG finding, unrelated to Game Show).

## P. Documentation created

This document.

## Q. Remaining risks / open questions

- The presenter art gap is the single hard blocker. Until it's resolved
  (re-render with a flat backdrop, or manual Affinity cleanup of the
  existing candidate), Step 2 cannot proceed on the brief's own terms
  ("do not improvise low-quality procedural substitutes").
- The `GameShow_Hybrid_Candidate_01.png` path discrepancy (brief expected
  `Worlds/GameShow/`, actual file is `Worlds/GameShow_Hybrid_Candidate_01.png`
  directly) is noted in case it signals a different file was intended.
- `PrizeBoard`'s fate is undecided — worth a explicit product call before
  Step 2, not assumed here.
- No candidate mentioned in this document is approved final art — all
  status language above reflects that.

## Exact next step

**STOP, per the brief's own Step 1/Step 2 gate.** To unblock Step 2,
one of the following is needed, supplied by the user (this is exactly
the human-in-the-loop step the project's own established art pipeline
names for this situation):

1. A new presenter render, same pose/costume/design direction, on a
   **flat, solid, saturated chroma-key-friendly background** (ideally the
   same magenta convention Western's Sheriff/Outlaw candidates used) —
   this would let the already-proven flat-chroma-key pipeline extract a
   clean transparent sprite with high confidence, exactly as it did for
   Western's cast; **or**
2. A manually alpha-cleaned version of the existing
   `Presentador_Hybrid_Candidate_01.png` (produced in Affinity or
   equivalent), delivered as a real RGBA PNG ready to drop into
   `Assets/Hermit/Content/Resources/Art/Gold/GameShow/`.

Once either exists, Step 2 (the actual Gold implementation — stage
choice-zone treatment, in-world presenter actor, intro sequence, feedback
states, and the corresponding test updates) can proceed using the plan
already recorded in Sections I–M above, without needing to re-derive it.

---

## Step 2 — Implementation

### Selected source and why

`ArtBible/Candidates/Presentador/Gameplay/Presentador_Gameplay_Candidate_01.png`
— the **third** image from the latest Midjourney batch, selected because
it was the most anatomically coherent variant and did not contain the
floating-hand/floating-microphone artifacts present in other variants from
the same batch. Same pose/costume/design as Step 1's original candidate,
but rendered on a flat chroma-green backdrop specifically for extraction
(unlike Step 1's candidate, which had a soft studio gradient).

### A. Source validation

Inspected before any extraction, per the brief's checklist:

- **One presenter only** — confirmed.
- **Full body visible**, head to shoes — confirmed.
- **No floating hands** — both hands are visibly attached to their arms
  (the extended gesture hand and the mic-gripping hand).
- **No floating microphone** — held by the hand, connected to a stand/cable
  reaching the floor.
- **No duplicated accessories** — one microphone, one stand.
- **No detached anatomy** — confirmed by direct visual inspection.
- **Hands/fingers coherent** — both hands show clearly-formed, non-fused
  fingers.
- **Feet/shoes coherent** — two distinct, correctly-posed shoes.
- **No body parts touching crop edges** — checked programmatically, not
  just by eye: the extended hand's leftmost pixel sits ~44-56px from the
  left edge (at the row where the hand is widest), the head's topmost
  pixel sits ~50px from the top, both shoes end 20-65px above the bottom
  edge. A minor darkening in the final ~6px at the right edge is a
  smooth background vignette artifact, not an accessory or body part
  (confirmed by sampling — it fades gradually, not a hard silhouette
  edge).
- **Chroma-green background uniformity** — sampled at 10 points spanning
  the full canvas (all four corners, edge midpoints, and interior points):
  every sample landed within `(97-100, 163-166, 92-97)` — an exceptionally
  flat, solid background, more uniform even than Western's own magenta
  candidates were per-file.

No anatomical or artifact problems were found. Proceeded to extraction.

### B. Presenter extraction

**Method**: the same flat-color chroma-key technique already proven for
Western's Sheriff/Outlaw sprites (`Docs/C8_1D_GOLD_ART_INTEGRATION.md`,
"Background-removal method") — a small Add-Type C# tool run through
PowerShell (`System.Drawing`, `LockBits`), not a repeat of Step 1's more
complex Coons-patch gradient approach, since this background is genuinely
flat:

1. Background sampled as one constant `(99, 164, 95)` (the average of the
   whole-canvas uniformity check above).
2. Alpha from Euclidean color distance: fully transparent ≤22, fully
   opaque ≥60, linear ramp between.
3. Edge decontamination on partial-alpha pixels (unblend the known
   background color's contribution).
4. **Green-spill suppression**: any resulting pixel (fully opaque or
   recovered from partial alpha) where green is the dominant channel by
   more than 25 units over the max of red/blue gets pulled 65% of the way
   toward the red/blue average — a targeted fix for the thin green-tinted
   fringe chroma-key extraction typically leaves on soft/anti-aliased
   edges (hair, cuffs), without touching the character's own genuinely
   green-free palette (purple suit, warm skin, dark hair — none of which
   trip this test).
5. Zero the RGB of fully-transparent pixels (avoids a faint fringe under
   bilinear filtering even at alpha 0).

**Quantitative result**: 79.2% fully transparent, 20.5% fully opaque
(reasonable for a full-body figure with an extended arm leaving a lot of
empty space around it), only **0.3% partial-alpha** (edge antialiasing —
compare to Step 1's rejected gradient attempt, which had 20.1% partial and
a visible defect), 99.9% border transparency, and the de-spill pass
actively corrected 2,714 pixels.

**Visual inspection at full resolution**: clean. No green fringe, no
halos around hair, no holes in skin/clothing, no chroma contamination, no
detached fragments, no leftover green islands. Hands, fingers, shoes, the
microphone, and its stand/cable are all fully intact. This result was
shipped — unlike Step 1's attempt, no defect was found requiring a stop.

Unity's own automatic alpha-bounds detection (visible in the generated
`.meta`'s sprite rect, `37,19` to `928,1187`) independently confirms the
transparent margins were detected correctly.

`ArtBible/Candidates/Presentador/Gameplay/Presentador_Gameplay_Candidate_01.png`
itself was never modified — only read.

### C. Production presenter path

`Assets/Hermit/Content/Resources/Art/Gold/GameShow/Actors/Presentador_Gameplay_01.png`

| Property | Source | Result |
|---|---|---|
| Dimensions | 928×1232 | 928×1232 (unchanged — no upscale) |
| Format | `Format24bppRgb` (no alpha) | `Format32bppArgb` (real alpha) |

### D. Import settings

| Setting | Value | Rationale |
|---|---|---|
| Texture type | Sprite (2D and UI) | Standard. |
| Sprite mode | Single | Required for `Resources.Load<Sprite>(path)`. |
| Compression | Uncompressed | Matches every other Gold actor sprite; avoids block-compression artifacts. |
| Max size | 2048 (default) | Source fits without upscaling. |
| Mipmaps | **Enabled** | Followed the *directly analogous* existing precedent — `Sheriff_Idle.png` (same 928×1232 source resolution, same "gameplay actor sprite" asset class) already ships with mipmaps enabled in this project. Not "blindly copying an unrelated Shell asset" — this is the single most relevant comparison available, a same-resolution character actor sprite, not a full-bleed background. |
| Filter mode | Bilinear | Matches the same Sheriff precedent (mipmaps + Bilinear together, not Trilinear — C9.2a's Arcade card used Trilinear for a different reason: a much larger single minification factor and a static, non-repositioned display; the presenter here is a moving/scaling gameplay actor, so mirroring Western's own proven actor-sprite settings exactly was preferred over inventing a third combination). |

### E. Final Game Show hierarchy

```
GameShow (root panel)
├─ Background (Image, GameShowBackground — unchanged art, unchanged loading code)
├─ AmbientVignette (Image, restrained radial darkening toward the edges)
├─ PresenterSpotlight (Image, radial glow behind the actor, fades in during intro)
├─ ContactShadow (Image, soft dark ellipse at the actor's feet, rendered behind him)
├─ PresenterActor (Image, real transparent Presentador_Gameplay_01 sprite — or a small procedural silhouette if the art were ever missing)
├─ StatementArea
│  └─ Statement (Text — unchanged name, so the existing generic "concept element" test lookup still finds it)
├─ ChoiceLeft (zone container)
│  ├─ Glow (radial glow, AccentWarm-tinted, fades in during intro, brightens on reveal)
│  ├─ Panel (subtle translucent stage-display backing for text contrast)
│  └─ GameShowTrue (Button, transparent, full-zone hit target)
│     └─ Label (Text, "VERDADERO")
├─ ChoiceRight (zone container, mirror of ChoiceLeft)
│  └─ GameShowFalse
│     └─ Label (Text, "FALSO")
├─ ResultFlash (Image, full-stretch alpha-only correct/incorrect pulse)
└─ Confetti ×8 (unchanged mechanic, restyled palette)
```

Retired entirely (see Section G): `PresentadorCard`, `Portrait`, the
procedural `PresenterBody`/`PresenterHead`/`PresenterArm` fallback
sub-parts (superseded by the single `PresenterActor` + its own smaller
procedural fallback), `ContestantHead`, `PrizeBoard`.

### F. Presenter staging

- **Anchor**: `(0.5, 0)` (bottom-center of the stage), pivot `(0.5, 0)` —
  positions from the feet up, matching "feet anchored to the floor."
- **Size**: 320×420 (canvas reference units), preserving the sprite's own
  post-crop ~891:1168 aspect via `preserveAspect = true`. Taller than a
  single Western outlaw target (240×320) since the presenter is the sole,
  central actor here, not one of several interchangeable targets.
- **Position**: `anchoredPosition (0, 40)` — feet sit 40 units above the
  very bottom of the stage area (which already excludes the top HUD band
  via `ClasicoHud`'s existing `TopHudReservedHeight`), leaving a small
  floor margin.
- **Grounding**: a `ContactShadow` (a flattened, low-alpha-0.30 dark
  rounded panel, 210×46, positioned at the same floor height, rendered
  before/behind the actor) plus a `PresenterSpotlight` (a soft warm radial
  glow behind him, fading in during the intro) — the same
  "shadow + glow" grounding language `WesternShootoutPresenter` already
  uses for its own targets (`GroundShadow`), reused here rather than
  invented fresh.
- **Never inside a rectangle/card** — confirmed structurally: no
  `CreatePortraitFrame` call anywhere in the new file, and a dedicated
  test (`GameShow_ShowsInWorldPresenterOnTheGoldStage_NotTheOldPortraitCard`)
  asserts `PresentadorCard` no longer exists anywhere under `GameShow`.

### G. Old visuals retired

Per the brief's explicit list — verified absent (not merely hidden) by a
new test asserting zero matching descendants under the `GameShow` root:

- `PresentadorCard` (the `CreatePortraitFrame` wrapper) and its `Portrait`
  child.
- `ContestantHead` — fully removed. It had zero external references
  (confirmed via a project-wide search) and no gameplay logic depends on
  it, so it was deleted outright rather than hidden-but-built, per the
  brief's own "disable... rather than deleting behavior blindly" guidance
  applying specifically to code *other systems reference* — nothing does
  here.
- `PrizeBoard` and its "PREMIO" label — see Section H.
- The old flat rounded-rectangle `GameShowTrue`/`GameShowFalse` buttons —
  replaced by the stage-integrated `ChoiceLeft`/`ChoiceRight` zone
  treatment (Section I). The button GameObjects themselves keep their
  exact names; only their visual presentation changed.

The old procedural curtain/floor/stage-light *fallback* (used only if
`GameShowBackground` art fails to load) remains, dormant, as the
missing-asset degrade path — not part of the normal Gold presentation, so
not "old visual language" in the sense the brief means.

### H. PrizeBoard decision

**Retired.** Inspected whether it carried essential gameplay information:
it only ever displayed a static "PREMIO" label with no dynamic score/prize
data — the actual score/streak is already shown by `ClasicoHud`'s shared
top HUD bar, which `PrizeBoard` never touched or duplicated. It was purely
decorative flavor text with no counterpart in the brief's own stage-
language vocabulary (host/contestant positions, stage architecture,
lighting, choice zones, audience energy). Removed rather than restyled.

### I. Choice-zone treatment

Each of `GameShowTrue`/`GameShowFalse` now sits inside a `ChoiceLeft`/
`ChoiceRight` container (260×130, anchored bottom-center at ±340 units
from center) built from three layers, all using the fake-2D-lighting
alpha-overlay language this project already established (never real 3D
lighting, never a new URP Bloom dependency):

1. **Glow** — `RuntimeUIFactory.GetRadialGlowSprite()` (new: opaque center
   fading to transparent edge, the alpha-inverse of the existing vignette
   sprite, generated once and cached the same procedural way), tinted
   `Theme.AccentWarm`, low alpha (0.16) at rest, rising during the intro
   and brightening further (to the actual `Theme.Correct`/`Incorrect`
   color) on reveal.
2. **Panel** — a small, very translucent (alpha 0.38) dark rounded
   backing, purely for text contrast against the stage's own busy
   lighting, never an opaque card.
3. **Button + Label** — the interaction target is still exactly
   `GameShowTrue`/`GameShowFalse` (transparent, full-zone hit region — the
   same "invisible button, the visual IS the target" pattern this project
   uses everywhere else), with a bold, high-contrast `Theme.TextPrimary`
   label on top.

No huge opaque UI card sits over the stage — the zone reads as an
illuminated architectural area, not a floating button.

### J. Intro timing

**A real architectural constraint was found and respected, not worked
around**: Game Show's Intro-phase duration is driven by
`ClasicoSessionDirector.GetIntroDurationSeconds()`, which for any
non-Encounter archetype (Game Show, Balance, *and* Detective all share
this) returns `ClasicoGameDefinition.CommandBeatSeconds` — a single field,
currently **0.6 seconds**, shared identically by all three. The brief's
suggested ~1.5-2.2s intro target would require changing that shared value,
which would also change Balance's and Detective's own intro timing — both
explicitly on this phase's do-not-touch list. **This value was not
changed.**

Instead, the new intro (`GameShowPresenter.IntroRoutine`) was compressed
to fit entirely inside the existing 0.6s budget: the presenter fades/
scales in (0.94→1.00 scale, 0→1 alpha) while a spotlight glow rises behind
him, and both choice zones' glows fade in starting partway through — all
within `IntroVisualDuration = 0.5f`, leaving a small margin before the
real phase transition. Input gating itself does **not** depend on this
visual timer at all — see Section J.1.

#### J.1 — Real input gating (a small, precedented `ClasicoGameHost` change)

`GameShowPresenter` gained a `RevealAfterIntro()` method, called by
`ClasicoGameHost.RenderFrame()` at the exact moment
`ClasicoSessionDirector`'s own Intro phase ends and Decision begins —
**this exactly mirrors the pattern `WesternShootoutPresenter.RevealAfterIntro`
already established** for the identical problem, just added as a second,
parallel `else if` branch next to Western's existing one. This was judged
a "strictly required locally" presentation-routing change (the brief's own
exception clause) rather than a session-architecture change: it touches
`ClasicoGameHost` (Shell-side presentation glue, not
`ClasicoSessionDirector`/`GameFlowController`/`GameRegistry`, which remain
completely untouched), adds one line following an existing precedent, and
changes *when a button's `interactable` flag flips*, never *when the
session itself accepts input* (that guard already existed, unconditionally,
in `ClasicoSessionDirector.SubmitSelection`'s own phase check).

Before this change, Game Show's buttons became visually interactable the
instant `ShowChallenge` was called — before Decision phase technically
began — meaning a click during that ~0.6s window would have looked
clickable but silently done nothing (the director's own phase guard would
have ignored it). This is now fixed: buttons are non-interactable for the
entire real Intro phase and become interactable at the exact real
transition, matching Western's already-correct behavior. Verified by a
new test — see Section Q.

### K. Correct feedback

Choice → lock (`interactable = false`) → the correct zone's `Glow` flashes
to `Theme.Correct` and settles to a restrained translucent green
(`LocalMotionFx.FlashColor`) → the presenter plays a small, restrained
scale-punch on the *same* sprite (`LocalMotionFx.Punch`, peak scale 1.09 —
no second pose needed) → a brief warm-gold `ResultFlash` sweep across the
whole stage (rise 0.12s, fall 0.30s) → the existing confetti burst plays →
next challenge. No generic "ding + green rectangle" — the ding itself
(`ClasicoHud`'s own shared feedback audio) is unchanged/untouched, this
phase only changed the *visual* language layered on top of it.

### L. Incorrect feedback

Choice → lock → the wrong zone's `Glow` flashes to `Theme.Incorrect` and
dims to a faint red → the correct zone's `Glow` reveals in green (both
zones animate independently via their own `MotionHandle`, so a
simultaneous flash on both sides never fights over one shared handle) →
the presenter plays the *same* Punch motion with `peakScale = 0.94`
(a shrink-and-recover, reading as a restrained recoil/dip — no new bespoke
motion code, no second pose) → a short, low-alpha red `ResultFlash` sweep
→ next challenge. No comedy bounce, no exaggerated animation.

### M. Timeout behavior

Unchanged gameplay semantics: `RevealOutcome(-1)` (nothing selected) is
already handled by the existing `correct = selectedIndex == correctIndex`
check (`-1 != correctIndex` is always true, so `correct` is always
`false`) — the incorrect-feedback visual path runs exactly as it would for
a genuine wrong answer, with the correct side still revealing. No new
branch, no invented scoring logic — this is the same convention the other
two selection-based presenters already use.

### N. Audio placeholders

**Not implemented this phase.** `ClasicoHud`'s existing shared
correct/incorrect feedback `AudioSource`/cues already play for Game Show
exactly as they did before (untouched) — this phase only changed the
*visual* feedback layer. Per the brief's own Step 1 plan (Section M
there), a stage-reveal sting, choice-lock cue, and distinct correct/
incorrect accents remain a documented direction for later, not built now,
to keep this phase's actual diff focused on the presentation fix the
brief most needed. No canned/casino/laugh-track audio was added or
considered.

### O. Responsive validation

**Not yet performed in a real build.** Structurally: every element is
built with anchors relative to the stage area (which already excludes the
top 112-unit HUD band via `ClasicoHud.TopHudReservedHeight`, respected
automatically since `GameShowPresenter.Build` is parented under the same
`stageRoot` every presenter uses), and the presenter/choice-zone
`anchoredPosition`s are all bottom-anchored (not top or stretch), so
resizing the reference canvas should scale everything proportionally
without new collision risk — but this is a structural argument, not a
substitute for actually looking at 1920×1080 and 1280×720 in a real
window. See the manual validation checklist (Section X below).

### P. Files changed

- `Assets/Hermit/Runtime/GameFramework/Microgames/GameShowPresenter.cs` —
  fully rewritten (Gold in-world stage presentation).
- `Assets/Hermit/Runtime/GameFramework/ClasicoGameHost.cs` — added one
  `else if` branch calling `_gameShow.RevealAfterIntro()` at the Intro→
  Decision transition, mirroring the existing Western branch.
- `Assets/Hermit/Runtime/GameFramework/RuntimeUIFactory.cs` — added
  `GetRadialGlowSprite()` (new procedural sprite generator, alpha-inverse
  of the existing `GetVignetteSprite()`).
- `Assets/Hermit/Content/Resources/Art/Gold/GameShow/Actors/Presentador_Gameplay_01.png`
  (+ `.meta`) — new production sprite.
- `Assets/Hermit/Tests/PlayMode/ClasicoPlayModeTests.cs` — see Section Q.
- `Docs/C8_1E_GAME_SHOW_GOLD_REPLACEMENT.md` — this Step 2 section.

No changes to: Western, Balance, Detective, Hub, Arcade Gallery, Start
Screen, Clásico key art, scoring, question generation,
`ClasicoSessionDirector`, `GameFlowController`, `GameRegistry`, or any
`GameDefinition` data.

### Q. Tests added/updated

- **Renamed and rewritten**: `GameShow_ShowsIllustratedBackgroundAndPresentadorCard`
  → `GameShow_ShowsInWorldPresenterOnTheGoldStage_NotTheOldPortraitCard` —
  now checks the background still resolves, `PresenterActor`'s sprite is
  exactly `Presentador_Gameplay_01` (by reference), `PresentadorCard`/
  `ContestantHead`/`PrizeBoard` are all fully absent (not merely hidden),
  `ChoiceLeft`/`ChoiceRight` exist, both buttons are interactable in
  Decision phase, and the presenter's scale restores after a correct-
  answer reaction.
- **New**: `GameShow_IntroGatesInput_ThenDecisionPhaseRestoresInteraction`
  — proves the new `RevealAfterIntro` gating actually works: buttons stay
  non-interactable for the entire real Intro phase and become interactable
  exactly when Decision begins. Uses the same frame-polling-across-the-
  whole-session strategy `WesternEncounter_Round1Intro_GatesConceptAndInputUntilIntroCompletes`
  already proved non-flaky (never a fixed-duration race against the short
  ~0.6s window).
- **New**: `AnswerWhicheverPrecedesGameShow` — a small test helper
  mirroring the existing `AnswerWhicheverPrecedesWestern`, used only to
  keep a session moving toward a fresh Game Show round while polling.
- **New**: `GameShow_CorrectAndIncorrectRevealsProduceDifferentZoneStates`
  — proves the correct/incorrect visual states actually differ (compared
  as full `Color` values, not just alpha, since `Theme.Correct`/
  `Theme.Incorrect` are both fully-opaque colors that would false-
  positive-fail an alpha-only comparison). Checks state during the
  round's own Feedback phase via `WaitUntil`, not after
  `AnswerCurrentMicrogame` returns (that helper already advances into the
  *next* round, by which point `ShowChallenge` has reset both glows back
  to rest).

No screenshot/pixel tests. No scoring/`SubmitSelection`/session-flow
tests were touched — they didn't need to be, and weren't.

### R. EditMode result

**110/110 passed**, 0 failed, 0 compile errors.

### S. PlayMode result

Full suite run 3 times across this pass (68 test cases, up from 66, for
the 2 new Game Show tests):

- Run 1: 66/68 passed — `WesternShootout_SelectedOutlawSwitchesToHit_OthersStayNeutral`
  (Western, untouched) and the already-documented Detective/RNG finding.
  Western failure isolated and passed cleanly (21.5s).
- Run 2: 64/68 passed — the whole run took **60.6 minutes** (a severe
  system-load anomaly, matching this project's own well-established
  pattern); `DetectiveLineup_ShowsAuditorCard`, `WesternCinematic_PreDrawCue_FiresBeforeGunshot_QuieterThanGunshot_ExactlyOnce`,
  and `WesternShootout_GameplayGunshot_FiresBeforeImpact_ForCorrectAndIncorrect`
  (all Detective/Western, all untouched this phase) plus the known
  Detective/RNG finding. All 3 non-RNG failures isolated and passed
  cleanly (5.8s, 16.1s, 23.0s respectively).
- **Run 3 (final, normal duration ~11 minutes): 67/68 passed** — the sole
  failure is exactly the already-documented, pre-existing, non-
  deterministic `RealShellScene_DecisionTimeoutOnActivePresenter_RestoresLocalReactionTransforms`
  finding (C9.1's Section S), unrelated to Game Show.

All 3 new/rewritten Game Show tests
(`GameShow_ShowsInWorldPresenterOnTheGoldStage_NotTheOldPortraitCard`,
`GameShow_IntroGatesInput_ThenDecisionPhaseRestoresInteraction`,
`GameShow_CorrectAndIncorrectRevealsProduceDifferentZoneStates`) passed in
every run. No new failure connected to Game Show, Western, session flow,
or scoring appeared in any run.

### T. Documentation status

This document — status **IMPLEMENTATION COMPLETE, MANUAL WINDOWS-BUILD
VALIDATION REQUIRED**. Explicitly not "GOLD COMPLETE."

### U. Remaining risks

- The presenter's exact stage position/scale (320×420, feet at y=40,
  choice zones at ±340) is a reasoned layout, not yet confirmed against a
  real render — could need a small manual nudge.
- The compressed 0.5s intro (vs. the brief's suggested ~1.5-2.2s) is a
  deliberate, documented trade-off against the shared `CommandBeatSeconds`
  constraint — worth a second look once seen in motion in case 0.5s reads
  as too abrupt, but changing it would require either accepting a longer
  shared Intro for Balance/Detective too, or a more invasive per-archetype
  timing override neither attempted here.
- No dedicated Game Show audio was added — the existing shared
  correct/incorrect dings continue to play; whether they still feel right
  against the new visual language is a manual-validation question.
- The green-spill de-spill heuristic (25-unit dominant-green threshold,
  65% pull toward red/blue average) was tuned against this one image; if
  a future Gold actor uses a different chroma-green shade, the threshold
  may need revisiting.

## Manual Windows-build validation checklist (Step 2)

- [ ] Does the presenter read as physically standing on the stage, not
      pasted over it?
- [ ] Is the contact shadow/spotlight grounding convincing without being
      distracting?
- [ ] Does the ~0.5s intro feel punchy and theatrical, not abrupt or
      missing a beat?
- [ ] Do the left/right choice zones read as "stage architecture," not
      generic UI buttons?
- [ ] Is VERDADERO/FALSO still instantly readable against the busy
      background at both 1920×1080 and 1280×720?
- [ ] Does the correct/incorrect reveal read clearly at a glance?
- [ ] Does the presenter's reaction (punch/deflate) feel appropriately
      restrained, not cartoonish?
- [ ] Does Game Show now feel like a genuinely different world from
      Western while still clearly belonging to the same Clásico?
- [ ] Any clipping, collision with the top HUD, or off-stage floating at
      either tested resolution?

---

## C8.1e.1 — Manual Windows Build Validation

Performed against a real Windows64 Development build produced from this
exact working tree (`Builds/Windows/Dev/Hermit V2.exe`, rebuilt twice: once
for the initial pass, once after the fix below). Validated by driving the
actual executable (window automation + screen capture, not the Unity Game
View) through Start Screen → Hub → Arcade → Clásico → live Game Show
rounds, at both required resolutions.

### Resolutions tested

1920×1080 and 1280×720, both windowed, both from the same build.

### Presenter grounding

Reads as standing on the stage at both resolutions — feet, contact shadow,
and spotlight are consistent with the floor plane in the background art.
No "pasted sticker" impression at normal viewing scale. A close pixel-level
crop of the feet was inspected specifically for floating; the apparent gap
above the foreground audience-silhouette band is the same kind of
depth-cue occlusion a real elevated stage view would produce, not a
positioning defect, but this is a closer call than the other checks below
and worth a second human look during acceptance.

### Presenter scale

320×420 reads as correctly proportioned at both resolutions — supports the
scene without dominating it, comparable in relative footprint to Western's
Outlaw targets.

### Intro

The compressed ~0.5s intro plays as a fast, punchy reveal rather than an
abrupt cut, consistent with the "fast and theatrical, not Western-like"
direction. Not extended, per the shared `CommandBeatSeconds` constraint
documented above.

### Choice-zone readability

Confirmed via pixel sampling of the actual build screenshot (not visual
guess): background luminance dips at the exact horizontal midpoint between
the two zones, confirming the left/right glow+panel treatment renders as
two visually separate illuminated stage areas, not one continuous band.
VERDADERO/FALSO remain instantly readable at both resolutions.

### Correct / incorrect feedback

Both triggered live in the real build (chosen by evaluating the actual
accounting statements shown, not scripted). Correct: winning zone glows
green, presenter's small scale-up reaction plays, green confetti pieces
appear, "¡Correcto!" is unambiguous. Incorrect: the correct zone reveals
green while the chosen zone's side reads red, "Incorrecto" is unambiguous,
score does not increase. `ResultFlash`'s full-stage color wash (peak alpha
0.30 correct / 0.18 incorrect) is a little stronger than "restrained" but
stays translucent, keeps every element readable through it, and does not
bleed into the top HUD (confirmed — `ResultFlash` is parented under
`stageRoot`, which already excludes `TopHudReservedHeight`). Judged
acceptable as-is; flagged below as an optional future polish item rather
than a required fix, to avoid a second rebuild cycle for a marginal call.

### Defect found and fixed: Statement/command-banner overlap at Intro

**Found:** At both resolutions, the shared `ClasicoHud` "¡DECIDE!" Intro
command banner (shown for every non-Western archetype, unchanged and
out of scope for this pass) visually overlapped Game Show's own Statement
text for the ~0.6s Intro window of every round. Confirmed reproducible in
2 independent real-build captures at 1280×720 before the fix, and
explained by exact position math, not guessed: Step 2 moved the Statement
text to the top of the stage (`GameShowPresenter.BuildStatementArea`,
previously part of the retired portrait-card layout, which likely didn't
sit in this band) to read correctly with the new in-world presenter
staging — which newly puts it in the banner's vertical span. The bar
shown alongside the banner is pinned at 100% during this state (verified
by pixel sampling across its width, not eyeballed), confirming this is
genuinely the Intro-phase banner and not a stuck Decision-phase timer.

**Fix applied (in scope — GameShowPresenter-local, not ClasicoHud):**
`BuildStatementArea`'s vertical offset moved from `-46` to `-78` (canvas
units; 1:1 with screen pixels at the 1280×720 reference resolution),
clearing the banner's glyphs with margin while staying well clear of the
presenter's head. `ClasicoHud` (on the do-not-touch list) was not
modified — the banner's own shared timing/suppression logic is unchanged
and still governs Balance/Detective/Game Show identically.

**Re-verified after the fix:** rebuilt, relaunched, and caught the Game
Show Intro live twice more in the rebuilt executable at 1280×720 — the
Statement text and the command banner no longer overlap in either
capture. Decision- and Feedback-phase framing (where the banner is already
hidden) was unaffected, as expected.

### 1280×720 result

Presenter fully visible, feet anchored, no collision with the (now
corrected) Statement text or the top HUD, TRUE/FALSO zones remain
distinct and readable, no clipping, no stretched background, no aspect
distortion.

### Corrections made

- `Assets/Hermit/Runtime/GameFramework/Microgames/GameShowPresenter.cs`:
  `BuildStatementArea`'s vertical anchor offset, `-46` → `-78` (Statement
  text position only — the one item on the Allowed Fixes list this
  finding required).

No other change was made. Presenter position/scale, contact shadow,
spotlight, choice-zone geometry, `ResultFlash` alpha, reaction amplitudes,
and intro timing were all inspected and left as Step 2 built them.

### Tests after the fix

- Game Show–specific PlayMode tests (`GameShow_*`, 3 tests): **3/3
  passed**, 30.9s.
- Full EditMode: **110/110 passed**.
- Full PlayMode: 3 separate runs were required to get a clean read on an
  unusually loaded machine (Get-Process confirmed heavy concurrent CPU
  use from several unrelated applications during this window; run
  durations of 43–57 minutes vs. the normal ~11 minutes confirm this).
  Across all 3 runs, 8 individual test failures occurred and every one
  was isolated: no two runs failed the same set of tests, and none of the
  failures ever touched Game Show, Western, session flow, or scoring in a
  way connected to this pass's one-line change. This matches this
  project's previously-documented system-load flake pattern exactly (see
  C9.1's Section S and this document's own Step 2 test notes above) —
  treated as environmental, not a regression, consistent with established
  practice: isolate every failure, never assume regression, and confirm
  the actually-changed scope passes cleanly (which it did, repeatedly).

### Documentation status

**GAME SHOW GOLD — MANUAL VISUAL VALIDATION PASS.** This applies to Game
Show only; the rest of Clásico's Gold status is unaffected and not
addressed by this pass.

### Remaining risks

- The `ResultFlash` full-stage color wash reads a little stronger than
  "restrained" language in the original brief suggests, though it stays
  functional, readable, and HUD-safe — worth a lighter-touch alpha pass in
  a future polish cycle if the team wants it more subtle.
- Presenter grounding at the exact stage/audience boundary is defensible
  but was the closest call in this validation — worth a second human look
  during acceptance, specifically at the feet/shadow region.
- No dedicated Game Show audio exists yet; the shared correct/incorrect
  dings were not re-evaluated for feel against the new visuals beyond a
  structural check.
- This pass did not attempt to fix or investigate the pre-existing
  Detective/RNG finding, nor any of the other transient failures surfaced
  by the loaded test machine — none reproduced when isolated.
