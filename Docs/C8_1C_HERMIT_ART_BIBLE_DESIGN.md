# C8.1c — Hermit Art Bible v0.1 + Gold World Art Direction

**Status: DESIGN EXPLORATION — VISUAL STYLE NOT LOCKED**

This is a design-lock phase. No production code, no Unity changes, no
presenter rewrites, no generated-asset integration, no commit, no push, no
C8.2. Everything below is the reference document for choosing Hermit's
permanent visual language — separate from, and outliving, the C8.1b
procedural prototype art. Nothing here is approved until you say so.

---

## A. Hermit visual thesis

**The permanent/per-world split.** Hermit has two visual layers that must
be designed separately and never conflated:

- **Hermit Visual DNA** (this document's main subject) — the constant
  across every game, world, and medium: character anatomy, facial grammar,
  shape language, line treatment, shading model, color philosophy, FX
  vocabulary, motion timing, and how illustration relates to UI. This is
  brand-level and changes rarely, deliberately, and all at once.
- **World Art Direction** — Western, Game Show, Detective, Balance,
  future worlds (Space, Restaurant, a card battler, a narrative game).
  Each gets its own palette, materials, props, and setting logic, but is
  built entirely from the DNA's vocabulary. A player should be able to
  tell two characters from different worlds belong to the same universe
  even with zero shared color.

**Emotional target.** Premium, playful, intelligent, energetic,
stylized, expressive, slightly absurd, visually confident, collectible,
character-driven — pitched at adults/university students without being
unwelcoming to anyone younger. Explicitly not: childish edu-software,
generic corporate vector art, generic mobile F2P art, default cyberpunk,
chibi-by-default, flat emoji faces, photoreal humans, sterile SaaS
dashboard art, visually inconsistent AI-slop, or an imitation of any
specific studio's house style (Marvel/Supercell/Disney/Pixar/anime).

**Working thesis in one line:** *Hermit looks like a confident editorial
illustration that learned to make jokes* — closer to modern board-game box
art or a well-art-directed animated short's key-frame style than to a
mobile-game asset store, with just enough graphic shading to read as
dimensional rather than flat-vector.

## B. Three candidate styles (overall art direction)

These are the three genuinely different rendering/production directions —
distinct from the *character-proportion* systems in section C, which is a
separate axis. Full comparison and prompt pack for each is in sections W
and Y.

1. **"Ledger Ink"** — clean 2D vector-illustration base with graphic
   (2–3 tier, hard-edged) cel shading, confident black or dark-navy
   selective outlines, flat color fields broken up by shape-driven
   shading blocks rather than gradients. Closest to premium
   board-game/editorial character illustration.
2. **"Hot Press"** — illustrated 2D with painterly graphic shading:
   soft-edged shadow shapes, textured brush-like fills, warm rim light,
   heavier reliance on light-and-material rendering than on outline. Sits
   between vector and painterly — more "illustrated poster" than "clean
   sticker."
3. **"Pressure Plate"** — pseudo-3D / stylized 3D-render-look achieved in
   2D: hard graphic shading with sharp specular highlights, a toylike
   material read (as if lit like a diorama or a physical collectible
   figure), minimal outline, form defined almost entirely by shading
   shape. Closest in *feel* (not content) to a premium physical
   collectible — a painted miniature, not a screen character.

Section W scores these; section X gives a working recommendation subject
to your approval.

## C. Character-language comparison

Three genuinely different character systems, compared point-by-point. All
three are described against the mandatory scalability test from the
brief — full-body microgame character → waist-up crop → portrait →
reaction pose → silhouette → small UI icon → premium card illustration →
marketing key art — and any system that fails that test at any tier is
disqualified regardless of how good it looks in a single 16:9 microgame
frame.

| | **System 1 — "Compact Confident"** | **System 2 — "Spring-Loaded"** | **System 3 — "Studio Portrait"** |
|---|---|---|---|
| Head-to-body | ~1:5.5 (heads read large enough for a 32px icon, not chibi) | ~1:4.5 (bouncier, more cartoon-forward) | ~1:7.5 (near-naturalistic) |
| Shoulder width | Wide, squared — reads as a strong silhouette block | Wide but rounded, "puffy" | Naturalistic, narrower |
| Limb exaggeration | Moderate — slightly thick limbs, no noodle-arms | High — limbs can stretch/squash for reactions | Minimal — anatomically plausible |
| Hand treatment | Simplified, 3–4 "finger clusters," always readable in silhouette | Mitten/glove-simplified, very graphic | Detailed, articulated |
| Eyes | Large, simple shapes (oval/almond), high expressive range via shape not detail | Very large, dominate the face, cartoon-elastic | Naturalistic size, expression via brow/lid detail |
| Eyebrows | Bold, thick,독립적으로 animatable shape (single graphic stroke) | Thick, highly mobile, often detached from the face plane for comic effect | Thin/naturalistic, subtle |
| Nose | Minimal — a shape or shadow, rarely outlined | Often omitted or a single dot/curve | Modeled, present |
| Mouth | Bold single shape, wide expressive range, no visible teeth detail by default | Extremely elastic, can exceed face bounds briefly for comic beats | Naturalistic, restrained range |
| Ears | Simplified, often stylized/oversized for silhouette ID | Simplified, sometimes exaggerated as a gag | Naturalistic |
| Hair | Graphic mass-shapes, flat color blocks, no strand rendering | Bouncy, exaggerated volume, active in motion | Rendered with texture/highlight detail |
| Facial hair | Graphic shape blocks (same treatment as hair) | Graphic, often oversized for character ID | Detailed, textured |
| Age variation | Via proportion + costume + posture, not wrinkles | Via proportion + color + energy | Via facial detail (lines, texture) |
| Body-type variation | Strong silhouette variety (tall/wide/round), same construction rules | Strong and elastic, variety pushed further via exaggeration | Naturalistic range, subtler silhouette variety |
| Gender differentiation | Via posture, costume, color, prop — never caricatured secondary features | Same approach, more exaggerated overall so it reads as "in-genre" rather than singling anyone out | Via naturalistic proportion, most caricature-risk here |
| Costume detail | Medium — enough graphic detail to be a "costume," not fussy | Low-medium — costume subordinate to pose energy | High — costume rendered with material fidelity |
| Silhouette strategy | Primary design driver — every character passes a black-silhouette ID test first | Primary driver, pushed toward exaggerated readability | Secondary to facial/material fidelity — weakest silhouette scores |
| Expression exaggeration | High, but held (poses, not constant wobble) | Very high, elastic, most "cartoon" of the three | Low-moderate, restrained/dignified |
| Anatomical realism | Low-moderate | Low | High |
| **Card-illustration fit** | Strong — silhouette + bold shape reads as "collectible" at a glance | Moderate — can read as too goofy for a premium card frame | Strong — but risks reading generic-realistic rather than distinctly Hermit |
| **≤1s microgame fit** | Strong — shape ID is instant | Strong — motion sells it even faster | Weak — needs more read time, more expensive to animate quickly |
| **UI/icon fit (24–32px)** | Strong — built for silhouette from the start | Moderate — elastic shapes lose clarity at tiny scale | Weak — detail collapses at icon scale |

**Recommendation to challenge, not accept blindly:** System 1 ("Compact
Confident") is the strongest all-around fit for Hermit's actual
requirements — it's the only one that scores well at *every* tier of the
scalability test in section C's own table, including the two hardest
(24–32px icon and premium card). System 2 is more purely "funny" but
loses card-premium feel and icon clarity. System 3 wins on premium/card
alone but fails the ≤1s microgame test and the icon test outright — both
non-negotiable per the brief. A hybrid is also viable: **System 1's
proportions and silhouette discipline, with System 2's expression
elasticity dialed to roughly 60%** — bold confident shapes at rest, with
room to stretch for a reaction beat without becoming rubber-hose. This
hybrid is the working recommendation carried into the rest of this
document; it is not locked (section X).

### Character Model Sheet specification

Every recurring Hermit character (mascot and cast) gets one model sheet
before any world-specific art is produced. A model sheet contains:

- **Views:** front, 3/4, side — same pose, same scale, same ground line.
- **Full body, neutral pose** — the character's default "rest" silhouette.
- **Silhouette study** — pure black shape, all seven other views overlaid
  or referenced, used as the pass/fail readability check.
- **Color palette** — locked hex swatches for skin/fur, hair, costume
  primary/secondary/accent, prop color. No new colors introduced outside
  this swatch without a version bump.
- **Facial construction diagram** — eye spacing, brow height, mouth
  baseline, proportions relative to head width/height, so any artist or
  generator reproduces the same face.
- **6 core expressions** (section K) drawn at consistent scale/crop.
- **3 gameplay poses** (section L) — anticipation, correct-reaction,
  incorrect-reaction, at minimum.
- **Height relative to other cast members** — a single "height chart"
  line-up, since relative scale is part of identity (the mascot's scale
  relationship to human characters especially).
- **Signature prop(s)** — the one or two objects that ID this character
  even in silhouette (a badge, a ledger, a magnifying glass).
- **Costume rules** — what's fixed (silhouette, primary garment shapes)
  vs. what can reasonably vary by world/skin (secondary color, minor
  accessories).
- **Allowed variations** — palette swaps for alternate skins/rarities,
  pose variety, prop swaps within the signature-prop family.
- **Forbidden variations** — anything that changes face construction,
  head-to-body ratio, silhouette read, or the locked palette's *identity*
  colors (accent/secondary can flex; the character's primary ID color
  cannot).

## D. Mascot directions

Treated seriously, per the brief — this is the character most likely to
become Hermit's primary brand mark. Three directions, none locked:

1. **"The Shell-Bound Observer"** — a small, round, shell-backed creature
   (not a literal old man, not a literal hermit crab either — an
   original silhouette that *rhymes* with both a shell and a hood without
   copying either). Reads instantly in silhouette as a rounded shape with
   two small expressive eyes peeking from a hooded/shelled opening. Scales
   perfectly to a 24px icon (it's already icon-simple), and the "shell"
   surface becomes a natural canvas for a card-rarity foil treatment
   later. Personality plays as watchful, dry-witted, unbothered.
2. **"The Hooded Ledger-Keeper"** — a small robed/hooded figure whose
   face is mostly shadowed except for two glowing/simple eye-shapes and
   an expressive mouth-line, carrying (or made partly of) an abstracted
   ledger/scroll motif. More overtly "accounting/knowledge" coded than
   direction 1, at some cost to instant silhouette-uniqueness (hooded
   figures are a common shape elsewhere) — mitigated by giving it a
   non-generic silhouette break (an asymmetric shell-plate shoulder, an
   oversized single eye, or a ledger-tab "tail").
3. **"The Abstract Coin-Creature"** — an original creature built from
   accounting/value motifs abstracted into anatomy rather than costume: a
   rounded coin/seal-shaped body, thin expressive limbs, a face that's
   almost entirely eyes-and-mouth on a flat "seal" surface (like a wax
   stamp brought to life). The most distinctive and hardest-to-confuse-
   with-anything-else option; also the riskiest to make feel warm rather
   than corporate, since coin/seal shapes default toward "logo," not
   "character."

**Working lean:** Direction 1 (Shell-Bound Observer) is the safest strong
choice — it satisfies "relate to the name without a literal old hermit
man," reads at 24px, animates simply (a shell can bob, an eye can blink,
a hood-opening can widen for surprise), and has the clearest path to a
premium card treatment (the shell becomes the card frame's natural
material motif). Direction 3 is the most *original* but needs the most
design work to avoid reading as a logo. Direction 2 is the most legible
as "knowledge/ledger" but the least distinctive silhouette. Not locking
any of the three — this needs the visual experiment batch (section Z)
before a real decision.

## E. Recurring cast — visual briefs (archetypes, not lore)

All seven use System 1's construction rules (section C) regardless of
which of the three overall styles (section B) is eventually chosen.

**1. Hermit (mascot)** — see section D; not a "cast member" in the same
sense, treated as its own brand element.

**2. Sheriff Implacable**
- *Premise:* the unshakeable authority figure — Western world's stand-in
  for "the correct answer, enforced."
- *Silhouette:* tall, wide-shouldered, a wide-brimmed hat that reads
  instantly even blacked out.
- *Body type:* broad, grounded, low center of gravity — reads as
  immovable.
- *Face:* squared jaw, minimal brow movement at rest (stoic), eyes narrow
  by default.
- *Costume:* duster coat, star badge as signature prop, boots.
- *Signature prop:* the star badge — doubles as a UI/achievement icon
  later.
- *Dominant personality:* deadpan certainty.
- *Neutral:* flat mouth, hat-shadowed eyes.
- *Success reaction:* one sharp nod, badge catches a highlight flash.
- *Failure reaction:* single raised eyebrow, no other movement — the
  joke is how little he reacts.
- *Recurring gag:* he is never surprised, even when the scene around him
  should surprise him.
- *Card-art pose:* three-quarter, coat mid-swirl, hand near badge.
- *Color notes:* desaturated warm neutrals + one saturated badge-gold
  accent.

**3. Forastero Nervioso ("The Nervous Outlaw/Stranger")**
- *Premise:* the wrong-answer stand-in — visibly not cut out for this.
- *Silhouette:* narrow, hunched, asymmetric (one shoulder higher).
- *Body type:* thin, slightly too-big clothes.
- *Face:* wide nervous eyes, sweat-drop-ready brow.
- *Costume:* ill-fitting poncho/vest, mismatched buttons.
- *Signature prop:* a hand permanently half-raised as if to object.
- *Dominant personality:* guilty before proven guilty.
- *Neutral:* eyes darting (implied via asymmetric pupil placement).
- *Success reaction (rare, when correctly cast as the answer):*
  surprised relief, whole body straightens.
- *Failure reaction:* flinch, hat/hair puff of dust.
- *Recurring gag:* always looks like he's about to confess to something
  unrelated.
- *Card-art pose:* mid-flinch, dynamic, dust/motion lines.
- *Color notes:* dusty desaturated palette, no saturated accent (visually
  "the one who doesn't stand out" — deliberate contrast to the Sheriff).

**4. Auditor Severo**
- *Premise:* Detective world's authority — finds the error, no
  exceptions.
- *Silhouette:* narrow, vertical, sharp — a single strong vertical line
  (long coat, straight posture).
- *Body type:* tall, thin, precise.
- *Face:* sharp brows, narrow rectangular glasses (signature prop
  overlap), thin unimpressed mouth.
- *Costume:* dark tailored coat, single visible pocket-ledger.
- *Signature prop:* a magnifying glass or red grease-pencil.
- *Dominant personality:* clinical, unimpressed, precise.
- *Neutral:* flat stare over glasses.
- *Success reaction:* small satisfied smirk, glasses glint.
- *Failure reaction:* single disappointed head-shake, no exaggeration —
  the restraint is the joke.
- *Recurring gag:* circles the "error" with visible red-pencil FX
  regardless of the actual game mechanic.
- *Card-art pose:* looking directly at viewer over glasses, pencil raised.
- *Color notes:* cool noir palette (charcoal/navy) + one red accent tied
  to the "error" state color.

**5. Presentador Estelar ("The Star Presenter")**
- *Premise:* Game Show world's host — maximum energy, theatrical
  certainty.
- *Silhouette:* medium build, wide open-armed default pose, big hair or
  hair-shape silhouette.
- *Body type:* average, posed for maximum stage presence rather than
  bulk or thinness.
- *Face:* huge readable smile by default, expressive brows.
- *Costume:* sequined/glossy jacket (rendered via the material language
  in section J, not literal sequin texture), bow tie.
- *Signature prop:* an oversized microphone.
- *Dominant personality:* relentless showmanship.
- *Neutral:* mid-gesture, never fully "at rest."
- *Success reaction:* arms fully thrown up, confetti-ready pose.
- *Failure reaction:* forced smile, one bead of flop-sweat, keeps
  "performing" through the loss.
- *Recurring gag:* his enthusiasm never dips even when the contestant
  clearly lost.
- *Card-art pose:* mid-announcement, spotlight-lit, arm extended toward
  viewer.
- *Color notes:* Game Show's electric palette concentrated in his jacket
  — he's the brightest object in his own world by design.

**6. Empresario Caótico ("The Chaotic Executive")**
- *Premise:* a recurring "everything is on fire but I'm fine" business
  figure — flexible across worlds as the source of the problem the player
  is solving.
- *Silhouette:* round/soft body, slightly too-tight suit, tie askew.
- *Body type:* stout, low, comedic instability (implied weight isn't on
  balance).
- *Face:* wide forced grin, one eyebrow permanently raised.
- *Costume:* suit with visible stress details (loose tie, undone button)
  — costume itself tells the "chaotic" story.
- *Signature prop:* a phone or folder always mid-drop.
- *Dominant personality:* confident denial.
- *Neutral:* strained smile.
- *Success reaction:* relief so big it's suspicious.
- *Failure reaction:* the forced grin doesn't change — only the
  surroundings visibly get worse (a Hermit-world running gag rather than
  his own pose changing much).
- *Recurring gag:* always mid-catching something that's falling.
- *Card-art pose:* off-balance, one foot lifted, papers flying.
- *Color notes:* warm but slightly sickly palette — confident color choices
  that don't quite match, on purpose.

**7. Cajera en Pánico ("The Panicking Cashier")**
- *Premise:* front-line stand-in for time pressure — she's the one
  actually doing the math while everyone else panics around her.
- *Silhouette:* small, compact, high energy implied through asymmetric
  hair/posture even at rest.
- *Body type:* small/quick-reading build — contrast to the Executive's
  bulk.
- *Face:* wide alert eyes, mouth often mid-word.
- *Costume:* apron/vest with a visible name-tag (a natural spot for a
  UI-callback detail later), sleeves pushed up.
- *Signature prop:* a receipt roll or calculator.
- *Dominant personality:* competent under pressure, visibly so.
- *Neutral:* mid-calculation stare.
- *Success reaction:* quick triumphant fist with the receipt roll.
- *Failure reaction:* wide-eyed freeze, receipt roll unspools.
- *Recurring gag:* the receipt roll's length is a visual gag that scales
  with how chaotic the current scene is.
- *Card-art pose:* leaning forward over a counter, calculator raised
  like a weapon.
- *Color notes:* Hermit's most saturated "competence" accent color, to
  read as the one reliable figure in a chaotic frame.

## F. Shape language

Dominant vocabulary: **rounded-but-confident** — soft outer silhouette
corners (no sharp mechanical edges on organic characters) paired with
large, simple, confidently-placed interior shapes (bold brows, large
props, geometric costume blocks). Controlled asymmetry over strict
symmetry — a raised shoulder, an off-center hat tilt, an asymmetric
stance — reads as alive and specific rather than templated/generic,
without tipping into visual noise. Compact over elongated for cast
characters (grounded, readable at small sizes); the mascot is the one
exception allowed a more elongated silhouette if direction 3 (section D)
is chosen, since it's a creature, not a human archetype.

How shape communicates state, deliberately kept simple and reused
everywhere:

- **Friendly / correct:** rounded, open shapes, upward-curving silhouette
  lines, symmetric or gently-open posture.
- **Dangerous / incorrect:** sharper negative-space breaks, downward or
  jagged silhouette accents (used sparingly — Hermit stays playful, never
  actually threatening), closed/defensive posture.
- **Important / collectible:** larger relative scale, more negative space
  around the shape (isolation reads as importance), a slightly more
  symmetric pose than the character's usual asymmetric default.
- **Interactive:** rounded rectangle/pill shapes for anything clickable —
  matches `RuntimeUIFactory`'s existing rounded-panel language (section O
  makes this connection explicit).
- **Correct/incorrect (state, not character):** carried primarily by
  color (section I) and motion (section M), with shape reinforcing —
  correct states get a small radius-increase "bloom," incorrect states
  get a small angular "jolt" silhouette break, both brief.

## G. Line treatment

Three approaches compared, evaluated specifically against "must stay
readable on busy microgame backgrounds *and* on clean card
illustrations":

1. **Uniform visible outline** (constant-weight dark line around every
   shape) — most reliable readability on busy backgrounds, but flattens
   card-illustration premium feel and reads closer to flat sticker art
   than the "premium" emotional target.
2. **No outlines, shading-only silhouette definition** — best premium/
   card feel (this is how most painterly card illustrations separate
   forms), but risks characters visually merging into busy microgame
   backgrounds without careful contrast/rim-light discipline.
3. **Selective, weighted, colored outline** — outline only where a shape
   needs it to survive a busy background (outer silhouette edge,
   occlusion boundaries) using a *dark, desaturated version of the
   adjacent color* rather than flat black, with line weight varying
   (heavier on outer silhouette, thinner/absent on interior details).

**Recommendation:** approach 3. It's the only one that scores well on
both ends of the readability requirement — the outer-silhouette weighted
line keeps characters legible against Western dust, Game Show stage
lighting, or Detective noir shadow, while the colored (not flat black)
treatment and the willingness to drop interior lines entirely keeps card
illustrations from reading as a coloring-book page. This is graded
per-system in section W and gets its own generation-prompt language in
section Y.

## H. Rendering model

Six models compared against the brief's own stated evaluation criteria —
production cost, animation feasibility, scalability, character
consistency, card compatibility, ≤1s microgame readability:

| Model | Production cost | Animation | Scalability | Consistency | Card fit | ≤1s readability |
|---|---|---|---|---|---|---|
| Clean 2D vector | Low | High (easy to rig flat shapes) | Excellent | Excellent (shapes are explicit, easy to reproduce) | Weak — reads flat/cheap next to premium card expectations | Excellent |
| Illustrated 2D, graphic shading | Medium | High | Excellent | Good | Strong | Excellent |
| Cel-shaded illustration | Medium | Medium-high | Good | Good | Strong | Strong |
| Painterly 2D | High | Low (hard to keep paint texture consistent frame-to-frame) | Poor | Weak (paint texture drifts between generations/artists) | Excellent | Weak — too soft/slow-reading at speed |
| Pseudo-3D | Medium-high | Medium (needs consistent "virtual" lighting rig) | Medium | Medium (lighting/render consistency is its own challenge) | Excellent | Good |
| Hybrid 2D/3D | High | Medium | Poor (two pipelines to keep in sync) | Weak | Good | Medium |

**Evaluating the working hypothesis** ("Stylized 2D illustration with
graphic/cel-like shading, strong silhouettes, controlled texture and
expressive posing") **against this table rather than accepting it
outright:** it lands almost exactly on **"Illustrated 2D, graphic
shading"**, which is the strongest scorer across every column except
being tied with cel-shading on card fit. The hypothesis survives the
challenge — it is not the most *unique* option (pseudo-3D and painterly
are more visually distinctive) but it is the only option with no weak
column at all, and both its weaknesses in less-favored rows are shared by
every other viable candidate. **Recommendation: adopt the hypothesis**,
specifically as "Illustrated 2D with graphic/cel-adjacent shading" rather
than pure flat vector — flat vector's animation/scalability edge doesn't
offset its card-fit weakness, and pure cel-shading's harder shadow edges
cost some warmth the brief explicitly asked for ("playful," "slightly
absurd"). This is not locked — section X carries it forward as the
recommendation, pending your review alongside the three overall style
candidates in section B (of which "Ledger Ink" is this rendering model's
closest realization).

## I. Shading / lighting

- **Shading tiers:** 3 — base color, one core shadow tier, one small
  specular/highlight accent. No smooth gradients (gradients read as
  "generic mobile game" per the avoid-list); shadow shapes are drawn as
  deliberate graphic shapes, not a rendered falloff.
- **Shadow softness:** hard-edged shape, soft-edged *only* at the very
  outer 10–15% (a slight feather so it doesn't look like a paper cutout
  pasted on).
- **Rim light:** used selectively, not universally — reserved for
  "important" reads (a card hero pose, a correct-reaction highlight, the
  mascot) rather than every character in every frame, so it stays
  meaningful instead of becoming wallpaper.
- **Ambient light:** each world sets one dominant ambient tint (warm
  desert light for Western, cool stage-wash for Game Show, cold noir blue
  for Detective, neutral workshop light for Balance) that all characters
  in that world receive on top of their own local palette — this is a
  large part of how a world feels distinct without characters needing
  different construction rules (section R).
- **Highlight treatment:** small, hard-edged, placed on the single most
  "readable" surface per material (see section J) — never a full specular
  sweep.
- **Material rendering:** graphic/economical throughout — see section J
  for the full material table.
- **Face lighting:** faces stay closer to flat/base-color than bodies —
  legibility of expression is prioritized over lighting fidelity;
  shadow/highlight shapes on a face are simple and never obscure eyes or
  mouth.
- **World lighting:** each Gold world gets one clearly-named "light
  logic" (golden-hour desert, theatrical spotlight wash, single
  desk-lamp noir, even industrial overhead) — detailed per-world in
  section R.

## J. Color system

Four layers, kept explicitly separate so a world can look different
without breaking brand recognition:

**A. Brand colors** — Hermit's own fixed identity colors, present in the
mascot, the UI chrome, and any marketing/key art regardless of world:
one primary "Hermit" hue (a warm, confident accent — working candidate:
a warm amber/gold, tying naturally into "ledger gold" and the existing
`HermitTheme.Accent` direction already in the codebase) plus one cool
neutral base (a deep blue-charcoal, close to the existing dark UI base) —
these two never get reassigned to a world palette.

**B. State colors** — Correct / Incorrect / Warning stay globally fixed
hues (green-leaning correct, red-leaning incorrect, amber warning) across
every world and every medium, including cards — this is a hard rule, not
a style choice, since it's a comprehension mechanic (`Theme.Correct`/
`Theme.Incorrect`/`Theme.Warning` in the current codebase already
establish this and should not be touched by any future re-skin).

**C. Character colors** — each recurring cast member's *own* locked
identity color(s) (Sheriff's badge-gold, Auditor's error-red accent,
Presenter's electric jacket color) — fixed per the Model Sheet spec
(section C), portable across whichever world they appear in.

**D. World palettes** — everything else: environment, ambient light tint,
secondary props. This is where Western/Game Show/Detective/Balance
actually differentiate:

- **Western:** warm/desert — dusty ochre, sun-bleached tan, deep rust,
  one saturated warm-red accent.
- **Game Show:** electric — deep stage-purple base, hot magenta/gold
  stage-light accents, high-saturation contrast.
- **Detective:** noir-leaning — desaturated charcoal/navy base, single
  warm desk-lamp amber accent, everything else cool.
- **Balance:** industrial — cool steel greys, brass/copper accent on
  mechanical details, minimal saturation overall (the most "neutral"
  world on purpose, letting the beam-tilt read stay the visual star).

The unifying rule that keeps four very different palettes "unmistakably
Hermit": **every world palette must still contain the Brand-color amber
somewhere load-bearing** (a prop, a UI element, a key accent) and every
character crossing into that world keeps their own Character color fixed
— the world changes around them, not on them.

## K. Materials

Kept stylized and economical — no material gets more than the 3-shading-
tier budget from section I:

- **Metal:** flat base + one hard-edged specular streak; badges/machine
  parts get a slightly cooler base tone than skin/cloth to separate
  "hard" from "soft" at a glance.
- **Wood:** flat base + a few short, irregular hard-edged "grain"
  accent shapes (never a repeating texture pattern).
- **Cloth:** flat base + soft-edged fold shadows only at major stress
  points (elbows, waist) — no fabric-weave texture.
- **Skin/fur:** flattest material of all — base + one soft core-shadow
  shape, prioritizing expression clarity over material fidelity.
- **Glass:** near-transparent base tint + one hard highlight shape + a
  thin edge-line; used sparingly (glasses, a display screen).
- **Paper:** flat off-white/cream base + 1–2 fold-shadow accents; ledger/
  receipt props read as paper primarily through shape (curled edge,
  visible lines) rather than texture.
- **Neon/light (Game Show world):** treated as an FX layer, not a
  material — a flat-colored core shape plus a soft outer glow (see
  section N), never a literal light-bulb render.
- **Dust (Western):** graphic puff shapes (per the existing C8.1b dust-
  puff pattern), flat 1–2 tone, never particle-noise.
- **Smoke:** same graphic-shape approach as dust, cooler tone.
- **Gold/rare materials (future card rarity treatment):** the one
  material allowed a 4th shading tier plus an animated highlight sweep —
  deliberately reserved for rarity/foil moments so it stays special (see
  section P).

## L. Facial expression language

Nine core expressions, each defined by three fast-reading levers —
**eyes, brows, mouth** — plus optional head tilt and squash/stretch, so
every expression is identifiable from a thumbnail-sized crop:

| Expression | Eyes | Brows | Mouth | Head tilt | Squash/stretch |
|---|---|---|---|---|---|
| Neutral | Standard oval, centered pupil | Flat, resting | Closed, flat line | None | None |
| Confident | Slightly narrowed, steady | One raised | Small closed smirk | Slight, chin up | None |
| Nervous | Wide, pupil off-center | Both raised, close together | Small wavering line | Slight, away from viewer | Minor tremor allowance |
| Shocked | Very wide, small pupil | Both raised high | Small "O" | Back | Brief stretch-up |
| Delighted | Curved/closed (happy-arc) | Raised, relaxed | Wide open smile | Slight up | Brief squash-bounce |
| Defeated | Half-lidded | Both lowered, angled down | Downturned line | Down/forward | Brief squash-down |
| Suspicious | Narrowed, off-center pupil | One lowered, one raised | Small sideways line | Slight side | None |
| Angry | Narrowed, sharp | Both sharply lowered, angled in | Open, angular shout-shape | Forward | Brief stretch on the shout beat |
| Smug | Half-lidded, steady | One raised, held | Closed asymmetric smirk | Slight, chin up | None |

Rule for every expression: it must read correctly with the mouth alone
covered, and separately with the eyes/brows alone covered — redundant
encoding is what makes ≤1s readability possible (section Q).

## M. Pose language

Seven poses, all required to pass a pure-silhouette read (no color, no
face) before being considered finished:

- **Idle:** relaxed asymmetric stance, weight on one side — never
  perfectly symmetric (symmetric reads as "static UI element," not
  "character").
- **Anticipation:** a small, brief pull-back/coil before the main action
  — telegraphs "something is about to happen" in under ~0.15s.
- **Correct reaction:** an upward, opening silhouette — arms/limbs move
  outward and up, chest opens.
- **Incorrect reaction:** a downward/inward, closing silhouette — a
  flinch or contraction, never a "hurt" or genuinely distressed pose
  (Hermit stays playful, not punishing).
- **Timeout reaction:** distinct from incorrect — a "still waiting/
  confused" beat (a shrug-adjacent silhouette) rather than a flinch,
  since timing out isn't the same failure as answering wrong.
- **Celebration:** the most extreme silhouette in the set — full
  extension, biggest prop-flourish, reserved for genuine milestones (not
  played every single correct answer, to keep it meaningful).
- **Card hero pose:** three-quarter, most "composed" pose in the set —
  less kinetic than the gameplay poses, designed to hold still and read
  as a portrait rather than a mid-action frame.

## N. Motion language

Timing built around short, held key poses rather than continuous
animation — directly continuing the discipline `LocalMotionFx` already
established in C8.1b (punch/shake/flash, all short, all self-restoring):

- **Anticipation:** ~0.08–0.15s, small.
- **Key pose hold:** the actual "read" moment — held long enough to
  register (~0.15–0.25s) before settling.
- **Overshoot:** fast, ~0.05–0.1s past the key pose, immediately pulled
  back — this is what makes a punch/reaction feel snappy rather than
  linear.
- **Settle:** ~0.1–0.2s ease back to rest.
- **Total reaction budget:** ~0.3–0.5s end-to-end for a standard
  correct/incorrect beat — matches the durations already in use in
  `LocalMotionFx.Punch`/`Shake` (0.22–0.35s) almost exactly, meaning the
  motion-timing philosophy here is a confirmation of, not a change to,
  what C8.1b already built.
- **Squash/stretch:** allowed, but capped — no dimension should exceed
  roughly 1.3–1.4x its rest scale, and always resolves back to exactly
  1.0x (the existing `LocalMotionFx` "always restore" contract already
  enforces the *return*; this adds the *ceiling*).
- **Hard rules:** no strobing, no idle-loop constant motion/wobble on
  characters at rest (a character should be able to sit still), no
  animation longer than ~0.6s for a standard gameplay reaction (only
  Celebration and card-reveal-style moments earn a longer beat).

## O. FX language

Ten FX categories, all built from the shape/color/material rules above —
explicitly not generic particle spam:

- **Correct:** a small upward "bloom" — the state-color (green-leaning)
  radius-increase shape from section F, brief, local to the
  character/element.
- **Incorrect:** a small angular "jolt" — a few short state-color
  (red-leaning) shape fragments, brief, local.
- **Impact:** a short radial burst-shape (already prototyped as the
  Western dust-puff) — flat 1–2 tone, no particle noise.
- **Score:** small upward-drifting number/glyph with a brief scale-punch,
  brand-amber colored.
- **Combo/streak:** an escalating version of the score FX — same shape
  language, larger scale and a secondary color pulse at higher streak
  tiers, never a new shape vocabulary.
- **Rare/gold:** the one place the 4th shading tier (section K) and an
  animated highlight sweep are allowed — reserved exclusively for genuine
  rarity/milestone moments so it stays special.
- **Spotlight:** a soft-edged radial light shape (already prototyped in
  Detective's ambient glow) — reused world-to-world with only color/size
  changing.
- **Transition:** stays exactly what C8.1 already fixed it to be — a
  `CanvasGroup.alpha` fade. This document does not propose any change to
  the transition mechanism; section S records why.
- **Selection:** a rounded-rect highlight frame matching the UI pill
  language (section F/section O-UI) — never a full glow-halo, to keep it
  crisp against busy backgrounds.
- **Card reveal (future compatibility):** not built yet, but the visual
  vocabulary is pre-reserved — a rarity-tier sweep built from the Rare/
  gold FX language above, so the card game's reveal moment is a scale-up
  of an FX language players already recognize from Clásico, not a new one.

## P. UI relationship

Characters/illustration and HUD must read as one product. Concretely:

- **Typography:** UI headings/labels should pick up the same "confident,
  slightly graphic" character as the linework in section G — a typeface
  with visible personality in its bold weight (not a neutral system
  font), while body/data text stays a clean, highly-legible companion
  face. (No specific typeface is chosen in this document — that's a
  follow-up type-pairing pass, not a C8.1c deliverable.)
- **Borders/panels:** the existing `RuntimeUIFactory.CreateRoundedPanel`
  rounded-corner language is already correct for this direction and
  should be the standard "frame" shape illustration sits inside —
  characters and panels should share the same corner-radius family, not
  fight each other (illustration in sharp rectangles inside rounded UI,
  or vice versa, breaks the "one product" read).
- **Highlights/selection:** section O's Selection FX (a rounded-rect
  highlight frame) is the same shape family as the panels it highlights —
  no separate "glow" vocabulary invented for UI vs. illustration.
- **Icons:** built from the same silhouette-first discipline as
  characters (section C/F) — an icon is a character/prop reduced to its
  silhouette, not a separate flat-icon style bolted on.
- **Shadows:** UI drop-shadows (if used at all) should match the
  illustration's own shadow logic — hard-edged, small, graphic — never a
  soft blurred web-UI shadow, which would read as a different product.
- **Buttons:** the rounded-pill "interactive" shape language from section
  F is the button shape — already true in the current codebase
  (`RuntimeUIFactory.CreateButton` uses the same rounded-panel sprite),
  and this document confirms that choice rather than proposing a change.
- **Selection frames:** consistent rounded-rect family, colored via the
  Brand/State color layers (section J) — never a color invented only for
  UI chrome.

This section explicitly does not redesign any current UI implementation —
it records the *relationship* the eventual re-skin must honor.

## Q. Card-game stress test

Mandatory per the brief, without designing any card-game mechanics. A
Hermit character must support, from one Model Sheet:

- **Card portrait:** the character's card hero pose (section M),
  cropped to a portrait aspect, silhouette intact against a simplified
  world-palette background (section J-D).
- **Full-bleed hero illustration:** the same character, same construction
  rules, posed and lit for a wider marketing crop — no re-design, only
  re-pose/re-crop from the same Model Sheet.
- **Rarity treatments:** built entirely from the existing material system
  (section K) — the Rare/Gold material's 4th tier and highlight sweep is
  the *entire* rarity-escalation vocabulary; no separate rarity art style
  needed.
- **Foil/effect layers:** treated as an FX/material overlay (section
  O's Rare/gold FX + section K's Gold material), applied on top of a
  finished character illustration, never baked into the character art
  itself — keeps one piece of character art reusable across every rarity
  tier.
- **Frame overlays:** a UI-language object (section P), built from the
  same rounded-panel/border family as the rest of Hermit's UI — never
  something the character illustration has to accommodate by changing its
  own composition.
- **Ability icon:** a silhouette-reduced version of the character's
  signature prop (section E) — already planned for at the archetype-brief
  level, not an afterthought.
- **Small collection thumbnail:** the character's silhouette study
  (Model Sheet requirement, section C) at reduced scale — this is
  literally why the silhouette study exists as a mandatory deliverable.

**Abstract requirements analyzed (not copied from any specific product):**
immediate character recognition (solved by the silhouette-first
discipline running through sections C/F/M), strong silhouette (same),
depth (solved by the 3-tier shading + selective rim light in section I),
focal hierarchy (character always the brightest/most-detailed element
against a simplified background, per section I's "world ambient tint
behind, character detail in front" rule), crop resilience (solved by the
Model Sheet's multiple locked views + the section C scalability test),
visually distinct rarity/effect treatment (solved by section K's
reserved Gold-material tier), collectible appeal (the entire thesis of
section A).

**One conceptual card mock-layout, as a pure style test — no mechanics:**

> A vertical card frame in the rounded-panel family (section P), brand-
> amber border on a deep charcoal card body. Character illustration
> (e.g., Sheriff Implacable, card-hero pose, section E) fills roughly
> the top 65% of the frame, full-bleed to the frame's side edges but not
> its top, set against a heavily simplified Western world-palette
> background (a flat dusty-ochre gradient-free sky block and a single
> horizon line — not the full detailed scene). A thin brand-amber
> divider separates the illustration from a lower name-plate band
> (character name in the confident display typeface, section P) and a
> single ability-icon slot (the star-badge silhouette, section E) in one
> corner. No rarity treatment applied in this base mock — that's the
> next layer, added on top per section K/O, not designed into this base
> layout.

## R. Microgame readability requirements

Distinct from the card stress test — this is the ≤1s, 1920×1080, 16:9
constraint:

- **Minimum character size:** no readable character (face + primary
  silhouette) smaller than roughly 8–10% of frame height — smaller than
  that, expression-reading fails within the ≤1s budget.
- **Label-safe zones:** any text label sits in a zone with guaranteed
  flat/simplified background behind it (the existing `ClasicoHud`
  `TopHudReservedHeight` inset is exactly this principle already applied
  to the shared timer — section S/R of the prior C8.1b document; this
  extends the same rule to in-world labels).
- **Maximum background detail:** environment art stays at the "flat
  block + a few graphic accent shapes" level established in section K/R
  — no background element should compete with character silhouette
  detail; backgrounds are allowed *fewer* shading tiers than characters,
  not more.
- **Contrast behind interactive elements:** every clickable element gets
  a guaranteed-contrast zone (a panel, a simplified backdrop patch)
  between it and any busy environment art behind it — never relies on
  color alone against a variable background.
- **Foreground/background separation:** enforced via the ambient-tint
  system (section I) — background receives the world's ambient tint at
  full strength, characters/interactive elements receive a slightly
  desaturated/lightened version of their own color so they sit visually
  "in front" without needing a heavy drop-shadow.
- **Character overlap rules:** characters may overlap *environment*
  props freely, but never each other's silhouettes when multiple are on
  screen (Western's four targets, Detective's four suspects) — each gets
  its own clear horizontal band, matching how they're already laid out
  in the current C8.1b implementation.

## S. Four Gold world directions

All four apply the DNA above; none introduce a new character-construction
rule. Comparisons to the current C8.1b procedural prototype are included
specifically to show *what changes* when real art direction replaces
placeholder shapes.

### Western Shootout
- **Environment:** a compact main-street read — one building silhouette,
  a hitching post, sparse desert brush — rather than the current flat
  sky/ground split; "a tiny readable western *scene*," not a colored
  backdrop with props scattered on it.
- **Architecture:** one or two low, simple building silhouettes (flat
  roofline, minimal window detail) anchoring the horizon so the eye has
  something other than "sky" to read instantly as *place*.
- **Sky:** warm gradient-free sky block (per section R's flat-background
  rule) with the current sun-glow kept — it already fits this direction.
- **Ground:** dusty ochre base + 2–3 graphic dust-patch shapes (no
  gradient) — texture implied by shape, not noise.
- **Props:** rocks (kept from C8.1b, refined to the material language),
  a hitching post, a cactus silhouette — small, secondary, never
  competing with target silhouettes.
- **Outlaw (target) design:** built on System 1/C8.1b's target concept —
  head + bandana + hat — but with real costume variety per the palette
  rules (section E's cast are the *named* recurring roles; the four
  interchangeable targets get a small rotating wardrobe of bandana/hat
  color combinations from the Western world palette, section J-D).
- **Sheriff:** Sheriff Implacable (section E) as an optional environment
  cameo (not currently in the C8.1b prototype) — a future addition, not
  required for this phase.
- **Costume:** duster/vest silhouettes kept simple per section E/C.
- **Labels:** sit inside the label-safe zone (section R), flat
  background patch behind each.
- **Reticle:** kept from C8.1b's crosshair redesign — it already matches
  this direction's "small, graphic, functional" requirement.
- **Muzzle flash:** treated as the Impact FX (section O), not a bespoke
  effect.
- **Dust:** treated as the Dust material (section K) — already matches.
- **Lighting:** golden-hour desert ambient (section I).
- **Palette:** Western world palette (section J-D).

### TV Game Show
- **Studio:** kept close to C8.1b's curtain/floor/backdrop composition,
  refined with real material rendering (section K's cloth for curtains,
  neon treatment for stage lights per section K).
- **Presenter:** Presentador Estelar (section E) replaces the current
  generic silhouette — this is the single biggest upgrade this world
  needs, since C8.1b's presenter has no defined personality yet.
- **Nervous contestant:** a rotating-cast role (not a single named
  character) built on System 1's proportions, palette drawn from the
  Game Show world palette — reads as "the person in the hot seat,"
  distinct from Presentador Estelar's fixed identity.
- **Podiums:** a light industrial-glossy podium shape (new — not in
  C8.1b) separating presenter/contestant physically, reinforcing "game
  show," not "two people standing in a room."
- **Lights:** kept from C8.1b, upgraded to the Neon/light FX+material
  treatment (section K/O) instead of flat translucent circles.
- **Prize screen:** the current prize board, re-rendered with the Gold
  material (section K) reserved-tier treatment — ties the "prize" object
  directly to the same visual vocabulary as future card rarity, which is
  a deliberate, low-cost brand-reinforcing choice.
- **Audience suggestion:** a simple silhouette row (a few flat dark
  shape "heads" along the very bottom edge, out of the label-safe zone)
  — enough to imply "there's an audience" without a crowd-render budget.
- **Confetti:** kept from C8.1b's reusable-piece system, recolored to
  the Game Show palette's full saturation range.
- **Palette:** Game Show world palette (section J-D).
- **Lighting:** theatrical spotlight wash (section I).

### Balance Machine
- **Machine identity:** upgraded from C8.1b's post-and-beam toward a
  small foundry/scale-workshop read — the base plate and pivot cap added
  last session are the right direction; this phase adds a simple
  frame/gantry silhouette around the whole assembly so it reads as "a
  machine in a room," not "a bar floating on a plinth."
- **Industrial materials:** Metal (section K) throughout — beam, base,
  pans — with Wood only on a (new, optional) operator's control lever, to
  give one warm material contrast in an otherwise cool/metal world.
- **Operator:** not currently in C8.1b — a small background figure
  (not a named cast member; could later become one) standing at a
  control lever, implying agency behind the machine rather than the
  machine balancing itself.
- **Weights:** the pans' contents get simple flat-shape "weight/coin
  stack" iconography rather than pure text-in-a-box, reinforcing "this
  is a scale," not "this is a form field."
- **Indicators:** the pivot-cap level indicator (added last session)
  kept and refined with the Metal highlight treatment.
- **Mechanical details:** rivets (kept), gauge/dial accents on the base
  (new, small, decorative) reinforcing "machine" without adding new
  interactive surface.
- **Lighting:** neutral overhead workshop light (section I) — the most
  restrained lighting of the four worlds, on purpose, so the beam's tilt
  stays the clear visual focus.
- **Palette:** Balance world palette (section J-D) — steel/brass, the
  most desaturated of the four.

### Detective Lineup
- **Interrogation/lineup space:** kept close to C8.1b's height-wall
  composition — it already matches this direction's "instantly
  communicate the setting" goal.
- **Suspects:** upgraded from C8.1b's palette-only differentiation to
  real costume-silhouette variety per suspect slot (not fixed named
  characters — a small rotating wardrobe, same principle as Western's
  outlaws) so all four are distinguishable by silhouette alone, not just
  card color.
- **Detective/auditor:** Auditor Severo (section E) replaces the
  currently-generic detective silhouette — same "biggest upgrade" logic
  as Game Show's presenter.
- **Height wall:** kept from C8.1b, refined with the Paper/graphic
  material treatment on the marker lines themselves.
- **Spotlight:** treated as the shared Spotlight FX (section O) —
  already matches.
- **Props:** Auditor's magnifying glass/red-pencil (section E) as a
  recurring environment detail, a hanging bare-bulb light shape to
  reinforce "interrogation room" beyond the current ambient glow alone.
- **Palette:** Detective world palette (section J-D), noir-leaning.
- **Lighting:** single desk-lamp/bare-bulb noir lighting (section I) —
  the most dramatic, high-contrast lighting of the four worlds.

## T. Style-sheet specification

One visual style sheet is the required deliverable *before* any
production assets — a single reference canvas (or small canvas set)
containing:

- Hermit mascot (chosen direction, front + 3/4 + expression strip).
- 3 human character archetypes (recommend: Sheriff Implacable, Auditor
  Severo, Presentador Estelar — one per Gold world already anchored by a
  named character, covering the widest personality range).
- Front/3-4 views for each of the above.
- Expression strip (the 9-expression grammar from section L) for at
  least one character — enough to prove the grammar works, not required
  for every character at this stage.
- Silhouette strip — every character above, pure black shape, same scale.
- Material swatches — the section K material table, rendered as small
  physical swatches (metal/wood/cloth/skin/glass/paper/neon/dust/smoke/
  gold), same lighting rig.
- FX swatches — the section O FX list, rendered as small isolated
  examples (correct/incorrect/impact/score/rare-gold/spotlight).
- 4 world thumbnails — one small establishing image per Gold world
  (section S), proving the palette-differentiation system works.
- HUD fragment — a small mockup showing section P's typography/panel/
  button relationship applied to one real HUD element (e.g., the score/
  streak readout).
- Hypothetical card — the section Q mock-layout, realized as one actual
  image.
- One full microgame mockup — a single Gold world (recommend Western,
  since it's the most-developed already) at real 16:9 gameplay
  composition, characters at real minimum-size (section R), to prove the
  whole system survives actual gameplay framing, not just isolated
  character art.

**Canvas/asset requirements:** style sheet delivered as a set of
individually-labeled image files (not one giant uneditable poster), each
at minimum 2048px on the long edge for character/material/FX sheets
(enough resolution to later crop a clean icon or card asset from), and
1920×1080 for the world thumbnails and the full microgame mockup (matches
the actual target render resolution, so readability judgments made on it
are valid for section R's requirements). Every file follows the
versioning scheme in section U from the moment it's generated.

## U. AI-assisted art pipeline

Recommended three-tool split, matching each tool's actual strength rather
than using one tool for everything:

- **A. Primary ideation — Midjourney (current version).** Best raw
  stylization range and aesthetic control for exploring the three overall
  style candidates (section B) and the three mascot/character-system
  directions quickly and cheaply. Weakest at deliberate character
  *consistency* across multiple generations — which is exactly why it's
  scoped to ideation, not final character-sheet production.
- **B. Character-consistency tool — a reference-image-driven workflow
  (image-to-image / character-reference features, e.g. Midjourney's own
  `--cref`-style reference input, or a dedicated consistency tool such as
  Leonardo AI's character-reference features) driven from an *approved*
  Model Sheet, never from text alone.** Once a character's Model Sheet is
  APPROVED (section U-versioning), every further generation of that
  character must be reference-conditioned on it — this is a process rule,
  not a specific-vendor lock-in, since tool capabilities here change
  quickly; the requirement is "reference-image-driven," whichever current
  tool does that best when production actually starts.
- **C. Production cleanup — traditional tools (Photoshop/Affinity/
  Illustrator).** No generative model output goes directly into the game
  or into marketing without a human cleanup pass: fixing hands/anatomy
  errors, enforcing the exact locked palette (section C's Model Sheet
  swatches), re-drawing outlines to match section G's weighted-line
  spec precisely, and producing the actual clean, layered, production-
  ready files (with alpha-cut silhouettes for the silhouette-strip
  requirement).

**Why this split:** no current generative tool reliably holds facial
identity, proportion, and palette across dozens of independent
generations — treating generation as *ideation and raw material*, with
consistency enforced by a reference-conditioned second pass and hard
enforcement (palette/line/proportion correction) by a human cleanup pass,
is the only version of this pipeline that produces IP-consistent
characters. This document does not claim perfect character consistency
from any generative model at any stage — that claim would be false.

## V. Source-of-truth / versioning

**Reference-image policy**, once a character/style sheet is approved:

- The approved sheet becomes canonical — the only reference used for all
  future generation of that character.
- No character is ever regenerated from a text prompt alone once a
  Model Sheet exists for it — every future generation is reference-
  conditioned on the approved sheet.
- Any generated variant that alters facial construction, head-to-body
  proportion, or the character's locked identity color is rejected
  outright, not "close enough."
- Costume changes are never silent — any costume variation ships as an
  explicitly labeled *variant*, reviewed the same way the base sheet was.

**Versioning scheme:**

```
CharacterName_v001                    (an individual approved illustration)
CharacterName_ModelSheet_v001         (the full model sheet document)
CharacterName_Expression_<name>_v001  (an individual expression, if versioned separately)
CharacterName_Pose_<name>_v001        (an individual pose)
WorldName_Thumbnail_v001
WorldName_Palette_v001
HermitStyle_MasterSheet_v001          (the overall style-sheet deliverable, section T)
```

A version bumps (`v001` → `v002`) only on an *approved* change — never on
an exploratory variant, which stays in the `EXPLORE`/`CANDIDATE` tiers
below and is never referenced by version number until it's promoted.

**Conceptual directory structure** (proposed, **not created this
phase**):

```
ArtBible/
  Characters/
    Hermit/
    Sheriff/
    Auditor/
    Presentador/
    ... (one folder per recurring cast member)
  Worlds/
    Western/
    GameShow/
    Detective/
    Balance/
  FX/
  UI/
  Cards/
  References/
  Approved/
  Exploration/
```

**Status tiers**, applied to every asset regardless of folder:
**EXPLORE** (raw generation output, disposable, never referenced by
anything) → **CANDIDATE** (a promising result worth a second look,
shortlisted, still not used anywhere) → **APPROVED** (locked, versioned,
usable as reference and in-production) → **PRODUCTION** (cleaned up per
section U-C, in its final game/marketing-ready form). Nothing skips a
tier.

## W. IP guardrails

Hermit must remain an original visual identity. Hard rules:

- No prompt ever references a living artist's name or a specific studio's
  proprietary style ("in the style of [artist]," "make it exactly like
  Marvel Snap," "make it WarioWare style") — this applies to every tool
  in section U, at every pipeline stage.
- Reference material may be discussed and used **only** in terms of
  abstract qualities — readability, energy, framing, exaggeration,
  collectible appeal, silhouette strength — never as "make it look like
  X." Every abstract quality referenced this way is already independently
  specified in this document (sections F, L, M, N, Q) precisely so a
  prompt-writer never needs to name a source to communicate the target.
- Final visual direction is judged against **this document's own
  described characteristics** (sections B–S), not against how closely it
  resembles any reference — the reference is a discussion aid, the
  written spec is the actual target.
- This rule applies retroactively to any output that *does* end up
  resembling a specific existing property too closely, regardless of
  prompt wording that produced it — resemblance is judged on the result,
  not excused by an innocent prompt.

## X. Decision matrix

Scoring the three overall style candidates (section B) across the
brief's own evaluation axes, 1–5 (5 = strongest):

| Axis | Ledger Ink | Hot Press | Pressure Plate |
|---|---|---|---|
| Uniqueness | 3 | 4 | 5 |
| Premium feel | 4 | 5 | 4 |
| Readability | 5 | 4 | 4 |
| Comedy fit | 4 | 3 | 3 |
| Character appeal | 4 | 4 | 4 |
| Card-game scalability | 4 | 5 | 4 |
| Animation feasibility | 5 | 3 | 3 |
| AI-generation consistency | 4 | 3 | 2 |
| Asset production cost (lower cost = higher score) | 4 | 3 | 2 |
| Mobile/PC readability | 5 | 4 | 4 |
| Long-term brand strength | 4 | 4 | 4 |
| **Total (of 55)** | **46** | **42** | **39** |

**Reading the matrix honestly:** Ledger Ink wins on total score, driven
almost entirely by production-practical axes (readability, animation,
AI-consistency, cost) rather than by being the most *distinctive* choice
— Pressure Plate actually scores highest on uniqueness alone, and Hot
Press wins outright on premium feel and card-game scalability. This is a
real trade-off, not a clean win, and it's exactly the kind of call this
document is required to surface rather than quietly resolve.

## Y. Recommended direction — NOT LOCKED

Working recommendation, offered for your review alongside ChatGPT's,
not adopted by this document on its own authority:

**Ledger Ink**, built on the **"Compact Confident" character system**
(section C) with System 2's expression elasticity dialed to ~60%, using
**approach 3 line treatment** (selective weighted colored outline,
section G) and the **"Illustrated 2D, graphic shading" rendering model**
(section H) — i.e., sections B, C, G, and H's individual recommendations
already converge on one coherent package without needing to be
reconciled after the fact.

**Why not Hot Press despite its premium/card-scalability lead:** Hot
Press's painterly shading tier is the most expensive to reproduce
consistently through the AI pipeline in section U (lowest AI-consistency
score in the matrix) and the hardest to animate at the ≤0.5s reaction
budget in section N — both are hard production constraints, not
aesthetic preferences, and both would compound across every future world
and every recurring character. Ledger Ink's readability/animation/
consistency strength is what actually lets this become a *scalable*
brand system rather than a beautiful but unsustainable one-off.

This recommendation is explicitly not final. Section AA lists what still
needs your (and ChatGPT's) review before anything here is locked.

## Z. Exact generation prompt pack

Written primarily for Midjourney/Firefly-style visual generation, for
**each of the three candidate styles** (Ledger Ink, Hot Press, Pressure
Plate) — swap the bracketed `[STYLE BLOCK]` per candidate using the
descriptions in section B; every other line stays constant across all
three so the resulting comparison is a fair one-variable test.

**Style blocks** (used in every prompt below):
- `[LEDGER INK]` = "clean vector-illustration character design, bold
  confident flat color shapes broken by hard-edged 2–3 tier graphic cel
  shading, selective weighted outline in a dark desaturated tone (not
  flat black), no gradients, no texture noise, premium editorial
  board-game box-art illustration quality"
- `[HOT PRESS]` = "illustrated character design, soft-edged painterly
  graphic shading, textured brush-like color fills, warm rim lighting,
  minimal outline, poster-illustration quality, no photorealism"
- `[PRESSURE PLATE]` = "stylized pseudo-3D character illustration,
  hard graphic shading with sharp specular highlights, toylike
  collectible-figurine material read, minimal outline, form defined by
  shading shape not linework, diorama lighting"

**1. Master style prompt**
```
Hermit brand character illustration, original IP, [STYLE BLOCK],
compact confident proportions (head-to-body ratio approximately 1:5.5,
wide squared shoulders, simplified expressive hands), large simple
expressive eyes, bold single-shape eyebrows, minimal nose, wide
expressive mouth, silhouette-first character design, playful confident
premium tone, adult-appropriate stylization (not childish, not chibi,
not photorealistic), warm amber and deep charcoal brand accent colors
present, plain neutral studio background, front-facing full body,
character sheet lighting --ar 3:4
```

**2. Character model sheet prompt**
```
Character model sheet, [STYLE BLOCK], same original character shown in
front view, three-quarter view, and side view, consistent proportions
and palette across all three, full body neutral standing pose, plain
neutral background, consistent flat studio lighting across all views,
turnaround reference sheet layout, no text, no watermark --ar 16:9
```

**3. World thumbnail prompt**
```
Small establishing environment illustration, [STYLE BLOCK], [WORLD
DESCRIPTION FROM SECTION S — e.g. "compact western main-street scene,
one low building silhouette, hitching post, sparse desert brush, warm
dusty ochre palette, golden-hour desert lighting, flat gradient-free
sky"], no characters in frame, background only, flat simplified detail
level suitable for gameplay backdrop, wide shot --ar 16:9
```

**4. Card art test prompt**
```
Premium collectible card character illustration, [STYLE BLOCK], three-
quarter hero pose, strong instantly-readable silhouette, character
filling upper two-thirds of a vertical frame, simplified flat background
in a single world-palette color with a single horizon accent line (no
detailed scene), dramatic focal lighting on the character with selective
rim light, deep charcoal card body color with a warm amber border
accent, no text, no UI frame elements, original character design, not
based on any existing card game --ar 5:7
```

**5. Microgame mockup prompt**
```
Wide gameplay scene illustration, [STYLE BLOCK], [WORLD DESCRIPTION FROM
SECTION S], one to four small original characters at approximately 10%
of frame height, clear horizontal separation between characters, flat
simplified background behind each character for label/UI contrast,
foreground characters slightly brighter/more saturated than background
environment, readable at a glance, no text or UI elements in the
generated image, 1920x1080 composition --ar 16:9
```

## AA. First visual experiment batch

Your proposed batch is evaluated below, then adopted with one addition.

**Proposed batch:**
- Hermit mascot × 3 directions
- Sheriff × 3 directions
- Auditor × 3 directions
- Nervous Contestant × 3 directions
- one Western thumbnail per direction
- one Game Show thumbnail per direction
- one hypothetical Sheriff/Auditor card per direction

**Evaluation:** this is enough to choose the **overall style** (the three
`[STYLE BLOCK]` candidates, section B/Z) — it covers a character, a
recurring named cast member, a second recurring named cast member, a
rotating-role archetype, two of the four world thumbnails, and one card
test, all ×3 styles, which is exactly the comparison the decision matrix
in section X needs real images to validate rather than table scores
alone. It is **not** enough to choose the **mascot direction** (section
D) — the batch generates the mascot only once its style is chosen, but
the mascot's *own* three conceptual directions (Shell-Bound Observer /
Hooded Ledger-Keeper / Abstract Coin-Creature) are a separate decision
axis from the three overall styles, and collapsing them into one batch
risks judging the mascot concept and the render style at the same time,
muddying both decisions.

**Recommended addition:** run the mascot's three *conceptual directions*
as a small separate mini-batch, in whichever single style wins the main
batch (not all three styles × all three mascot directions — that's 9
images for a decision that only needs 3 once the style is settled).
Sequence: **(1)** run the proposed 7-subject × 3-style batch first to
lock the overall style candidate-of-choice (not final-locked, but
working), **(2)** then run the 3 mascot directions in that one style to
choose the mascot concept. This keeps the total experiment small (the
original ~13 generations × 3 styles, plus 3 follow-up mascot
generations) while still answering both questions cleanly.

## Open decisions requiring your (and ChatGPT's) review

None of the following are decided by this document — they are exactly
what the STOP GATE below exists for:

1. Which of the three overall style candidates (Ledger Ink / Hot Press /
   Pressure Plate, section B/X) actually wins once real generated images
   exist, not just the table score in section X.
2. Whether the "Compact Confident" character system + 60%-dialed System-2
   expression elasticity hybrid (section C) holds up in actual generated
   art, or needs the elasticity dial adjusted either direction.
3. Which of the three mascot directions (section D) to pursue — and
   whether it needs a fourth, unlisted direction once the first three are
   seen as real images.
4. Whether the line-treatment recommendation (approach 3, section G)
   actually stays readable against busy world backgrounds once real art
   exists, or whether a heavier outline is needed for the busier worlds
   specifically (Western, Detective) even if lighter-outline works for
   the cleaner ones (Game Show, Balance).
5. Final naming — every candidate style, mascot direction, and cast
   member here uses a working name; none of these names are treated as
   locked/final brand copy.
6. Whether the recommended character-consistency tool approach in section
   U-B is still the right one by the time actual production starts — this
   space moves fast enough that the *process rule* (reference-conditioned
   generation from an approved sheet) should outlive any specific vendor
   named as today's best option.
7. Whether Empresario Caótico and Cajera en Pánico (section E, roles 6–7)
   are actually needed for the first production wave, or whether the
   initial four named characters (Hermit, Sheriff, Auditor, Presentador)
   are sufficient until a fifth Gold world justifies expanding the cast.

---

**STOP GATE.** This design document and its prompt pack are the complete
deliverable for C8.1c. No implementation, no mass asset generation, no
Unity changes happen from this document alone. The three candidate
directions (and, after them, the mascot directions) are reviewed by you
and ChatGPT before any of section AA's experiment batch is run for real.
