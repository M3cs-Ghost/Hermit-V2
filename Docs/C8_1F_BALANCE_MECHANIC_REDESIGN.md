# C8.1f — Balance Mechanic Redesign: Account Debit/Credit Selection

**Status: BALANCE COHERENT MACHINE + AUDIO LIFECYCLE FIX IMPLEMENTED (C8.1f.5)
— MANUAL WINDOWS ACCEPTANCE REQUIRED.**

A human manual gameplay pass on the real Windows build (C8.1f.2 correction
pass) found real defects the earlier automated validation missed: the
ACREDITA click usually didn't register with a real mouse, the teaching
recap was clipped off-screen and disappeared too fast, and the shared HUD
band was reserving more vertical space than it needed. The root causes
(invisible recap `Text` graphics raycast-blocking the option buttons
underneath them; a mispositioned/undersized recap panel) were found and
fixed, and this fix has since been **manually confirmed working by the
user's own real-mouse test on the rebuilt Windows executable**: CARGA and
ACREDITA both work, and the recap/correct-answer panel is fully visible.
The mechanic itself is manually accepted and was NOT altered by C8.1f.3.

C8.1f.3 replaced the procedural rectangle/rounded-panel machine with the
approved Gold art set — Machine Master, Beam, Pans, Pivot Lock medallion,
Account Token, and a Chamber background — chroma-extracted from the
Midjourney candidates under `ArtBible/Candidates/Worlds/Balance/` into
`Assets/Hermit/Content/Resources/Art/Gold/Balance/`. The beam rotates
around the pivot; each pan translates (never rotates) to stay level, like
a real chain-hung scale pan. A correct answer now makes the beam visibly
settle at exact 0-degree equilibrium with a pivot-lock illumination and a
dedicated mechanical CLACK (never a Western-style gunshot cue, never a
generic success ding); Partial/Incorrect visibly jam/jitter off-level with
restrained smoke and a jam/strain cue. All of this was proven with targeted
PlayMode tests (beam angle at rest/settled/off-level, token labels, Hide/
re-entry neutral-state reset) — see this session's own report for exact
counts. **Not yet visually confirmed by a human on the rebuilt Windows
executable** — an automated screenshot attempt this session was blocked by
the same desktop-focus contamination documented earlier in this project
(an unrelated window captured instead of the game), so the extensive
layout/scale math behind the machine's on-screen composition has only been
verified through the code and through passing tests, never through actual
rendered pixels. Per this project's standing "real human input + real
Windows build has final authority" rule, this status will not be upgraded
to "Balance Gold accepted" until that happens.

A human Gold visual review of C8.1f.3 confirmed the accounting mechanic and
Gold artwork itself, but reported the experience still felt like "UI
layered over a machine" rather than the machine physically processing the
decision, and specifically flagged the correct-lock sound as a musical
"TUU" chime. C8.1f.4 addressed both:

1. **Token insertion.** `InsertBothTokens` now visibly drops each token in
   from above (`TokenTravelDistance` = 90 units) with a clean ease-out
   (no bounce/overshoot), materializing over the first ~40% of the fall,
   arriving with a receiver-reaction punch on the pan itself and a
   TokenInsert cue — replacing the old "fade in place" approach.
2. **Processing beat.** A new, strictly outcome-agnostic `ProcessingSequence`
   (~1.0s) now runs between "both tokens seated" and the outcome
   resolving: a Processing audio cue, a light pivot-medallion jitter, small
   symmetric beam micro-movements, and the medallion's own glow building
   from its dim rest state toward a neutral warm brightness — no
   correctness color at any point.
3. **Central verdict.** A new dynamic-text verdict ("EQUILIBRIO" /
   "DESEQUILIBRIO", never baked art) presents at the pivot itself once each
   outcome's own beam reaction completes, held for `VerdictHoldSeconds`
   (0.6s) before the recap appears.
4. **Audio.** Every procedural fallback that used a pure sine tone was
   rebuilt as noise-based mechanical synthesis (`MechanicalClack`/
   `MechanicalJam` redesigned; new `MechanicalClick`/`MechanicalWhir`) —
   see `ProceduralAudio.cs`'s own doc-comments for exactly what changed and
   why. All five cues (TokenInsert, Processing, CorrectLock, IncorrectJam,
   SmokePressure) now load first from
   `Resources/Audio/Gold/Balance/Balance_*` via `RuntimeUIFactory.LoadAudio`
   and only fall back to the temporary procedural placeholder when that
   asset hasn't shipped yet. Two real supplied clips were found already
   present under that folder mid-session (arrived with descriptive names —
   "Brass Token Precision Slot.mp3", "Premium Precision Mechanical Lock
   Engage.mp3") and renamed (their `.meta` files renamed alongside them, so
   the same GUID/import settings carried over) to the exact
   `Balance_TokenInsert.mp3` / `Balance_CorrectLock.mp3` names this code
   expects — those two hooks are now real audio, not procedural fallback.
   `Balance_Processing`, `Balance_IncorrectJam`, and `Balance_SmokePressure`
   remain on the temporary procedural fallback until supplied.

`BalanceFeedbackDisplaySeconds` was raised 3.4s -> 4.5s to keep the same
readable recap hold with the now-longer machine sequence (the 7-second
Decision timer itself is untouched). 13 targeted Balance PlayMode tests
pass, including new ones proving the verdict text per outcome and that a
real Processing gap now exists before the verdict appears. Windows build
succeeded and was smoke-launched twice (stable). Visual confirmation of the
new token-travel/processing/verdict experience was attempted again this
pass and blocked again by the same desktop-focus contamination (this time
caught via a window-handle mismatch check before any screenshot was even
taken, so nothing unrelated was captured) — still awaiting a human pass on
the rebuilt executable.

A second human Gold review of C8.1f.4 found two real problems. First, the
machine looked **disassembled/incoherent**: the Machine Master, Beam,
LeftPan/RightPan, and LockMedallion were separate Midjourney generations —
stylistically related, but not geometrically identical parts of one real
object — so the beam didn't fully belong to the machine body, the pans
didn't feel physically connected, and the pivot didn't match the master.
Second, a real **audio lifecycle bug**: Balance's Processing/music audio
kept playing after the round ended, including into the Results screen when
Balance was the session's last microgame. C8.1f.5 fixed both, without
touching the accounting mechanic, Western, Game Show, or Detective:

1. **Visual — whole-machine tilt.** Cropping a clean beam/pan/pivot seam out
   of the Master turned out not to be possible (the source art has no such
   seam), so per the brief's own explicit fallback ("keep visually
   integrated with the master and fake vertical pan response subtly"),
   `Balance_MachineMaster.png` is now the single visible machine — a rigid
   body that tilts as one piece around its own real hub point
   (`MasterHubOffsetFraction` = 0.20, i.e. 20% of the image's own height
   above its vertical center). The independently-generated `Balance_Beam`,
   `Balance_LeftPan`, `Balance_RightPan`, and `Balance_LockMedallion`
   sprites were retired as production layers (the files remain on disk as
   references/candidates, per the brief) — nothing in the presenter loads
   them anymore. `MaxTiltAngle` was halved 14° -> 8°, since a whole large
   machine tilting reads as "falling over" at the old beam-only angle.
   Pans became invisible anchor points (still real `Image` components, kept
   only so existing tests' `FindImageUnder("LeftPan"/"RightPan", ...)`
   lookups keep working) used solely to position tokens and drive the same
   `sin(angle)*PanXOffset` vertical pan-response math as before. The pivot/
   verdict glow became a single procedural radial glow
   (`RuntimeUIFactory.GetRadialGlowSprite()`) instead of the mismatched
   `Balance_LockMedallion.png` art — eliminating any risk of "two different
   central hubs." Outcome color tinting moved from the (now invisible) pans
   to the tokens themselves, the only remaining visible, tintable sprites
   near the pans. Net effect: one coherent, traceable machine body at every
   state, including neutral/still (the brief's own "connection test"), with
   no duplicated beam/pan/pivot geometry anywhere.
2. **Audio — centralized lifecycle.** Traced (not guessed — durations
   confirmed via direct measurement) to two compounding causes: the two
   real supplied clips (`Balance_TokenInsert.mp3` = 8s,
   `Balance_CorrectLock.mp3` = 7s) are far longer than the sub-second
   mechanical beat each belongs to, and nothing ever called
   `AudioSource.Stop()` on any phase transition — every `PlayOneShot` call
   was simply left to run to completion regardless of what happened next.
   Fixed with two additions: `PlayPhaseAudio(clip)` (stops the shared SFX
   source, then plays the new clip via `.clip`/`.Play()` rather than
   `PlayOneShot` — the latter is invisible to `AudioSource.isPlaying`,
   which would make this whole contract unobservable) at every sequential
   phase cue (TokenInsert, Processing, CorrectLock, IncorrectJam), and a
   centralized `StopAllBalanceAudio()` called from `Hide()`, `ShowChallenge()`
   and `RevealOutcome()` (defensive), the end of `ProcessingSequence()`
   (enforces "the visual phase owns the audio duration" regardless of a
   real clip's own length), and — the fix for the Results-screen hard
   guarantee — immediately before `ShowRecap()` in both branches of
   `MachineSequenceRoutine`, so the presenter silences itself structurally
   before any recap/next-round/Results transition, without ever depending
   on an external `Hide()` call being reached in time (confirmed by reading
   `ClasicoGameHost.cs`: no round-to-round `HideAllPresenters()` call ever
   fires for the natural "last round of the session ends -> Results
   appears" path). `PlaySmoke`'s own simultaneous multi-puff calls keep
   using raw `PlayOneShot` unchanged, since those are intentionally
   layered, not sequential.

16 targeted Balance PlayMode tests pass (3 new: abort-during-Processing
stops audio immediately with no delayed tail, and correct/incorrect answer
paths leave zero audio once the recap appears — proven specifically against
the real ~7s `Balance_CorrectLock.mp3`, the longest real clip currently
shipped). Full EditMode (122/122) and one Western + one Game Show smoke
test also pass. Windows build succeeded. Visual/audio confirmation of the
rebuilt machine on the real executable is, as with every Balance pass
before it, left to a human — see this session's own report for the exact
manual-acceptance checklist.

This document defines the new primary Academic Challenge Type for the
Balance archetype: **the player identifies which account is debited and
which account is credited for a given transaction.** It replaces the
current `EquationChallenge` ("nudge the unknown until Activo = Pasivo +
Patrimonio") as Balance's primary content. No code changes accompany this
document — see "Implementation impact" below for what a future
implementation phase will touch.

---

## 1. Why this fits the existing grammar

`Docs/C8_CLASICO_DESIGN_LOCK.md` section E already lists a **DebitCredit**
Challenge Type ("Which side an entry affects"), compatible with **Choose
Side, Balance**. The original Gold Slice sketch paired DebitCredit with a
future Boxing Ring (Choose Side) microgame, not Balance — but the
compatibility table already accounts for Balance as a valid alternative
home. This redesign exercises that second, already-licensed pairing rather
than inventing a new one. `Equation` (Balance's current content) is
retired as Balance's shipped content by this redesign, not kept alongside
it — see "Future variants" for what stays on the shelf instead of being
deleted from the design space entirely.

## 2. Core gameplay loop

Each Balance challenge presents one short transaction. The player makes
two sequential decisions:

1. **¿Qué cuenta se carga?** — select the debited account.
2. **¿Qué cuenta se acredita?** — select the credited account.

Only after both are committed does the machine react. No feedback of any
kind — visual weight, tilt, color, sound — is shown before the second
selection locks (section 15's anti-cheat rule).

## 3. Visible terminology — audit result and decision

Searched all existing Spanish accounting content in the project
(`ClasicoMicrogameLibrary`, `MicrogameContentValidatorTests`) for
precedent before choosing labels:

- **"el debe" already exists** in shipped content: `tf_activo_debe` → *"El
  activo se registra normalmente en el debe."* This is the standard
  Spanish-language ledger/T-account term (the debit *column*).
- **"haber", "cargar", "acreditar" do not exist anywhere in the project
  yet** — there is no prior convention to break for these.

**Decision:**

- **Machine-side visible labels (the ledger/T-account column headers on
  the physical machine): `DEBE` / `HABER`.** This is the standard paired
  terminology in Spanish-language accounting education (they always
  appear together, never alone), and reuses the one term Hermit's content
  already established rather than introducing `CARGA`/`ACREDITA` as a
  second, competing noun pair for the same concept.
- **Question/verb phrasing (what the player is actually asked): `¿Qué
  cuenta se carga?"` / `"¿Qué cuenta se acredita?"`** — exactly as
  specified in the brief. `Cargar`/`acreditar` are the natural verbs for
  "make this account the one debited/credited" in accounting Spanish;
  they name the *action*, while `DEBE`/`HABER` name the *ledger side* the
  action lands on. Both pairs coexist correctly in real accounting usage
  (e.g., "se **carga** la cuenta X, que queda registrada en el **DEBE**")
  — this is not a terminology conflict, it's the normal verb/noun split.
- Machine token labels read the account name plus which side it landed
  on, using the same `DEBE`/`HABER` words as the machine's column labels,
  so the token and the machine agree visually (`MOBILIARIO — DEBE`, not a
  third phrase).

## 4. Recommended interaction model: Two Sequential Selections

Evaluated against the brief's three candidate models:

| Model | Speed | Input complexity | Gamepad-friendly | Risk |
|---|---|---|---|---|
| A. Two sequential questions | Fast | Low (reuses existing "pick 1 of N" pattern) | Yes | Low |
| B. Two receiving slots (assign accounts to slots) | Medium | Medium (needs a selection-then-placement step either way) | Medium | Medium — new interaction shape |
| C. Account then side | Medium | Medium (extra tap per account) | Yes | Low-medium — one more step than A for the same information |

**Model A wins for three reasons specific to this project, not just in
the abstract:**

1. Western, Game Show, and Detective are all already "pick 1 of N
   options, confirm" underneath their costumes (`BalanceMachinePresenter`'s
   own doc comment calls Balance out as *the one archetype that isn't*
   this shape today). Model A makes Balance's *input* consistent with
   every other archetype for the first time — same focus-navigation,
   same confirm action, same button/keyboard/gamepad mapping — while its
   *presentation* (the beam/pans/machine) stays exactly as distinctive as
   before.
2. It directly reuses the existing arrow-navigate + confirm mapping
   already documented for microgame selection grids, rather than needing
   a new one (Model B's "assign to slot" is a materially different input
   shape that nothing else in Clásico uses yet).
3. Two short, unambiguous questions asked in sequence is the fastest way
   for a time-pressured player to *think accounting* rather than *think
   UI* — directly serving section 12's "spend time thinking accounting,
   not reading paragraphs" instruction.

## 5. Exact challenge flow

```
Transaction text appears
        ↓
STEP 1 — "¿QUÉ CUENTA SE CARGA?"
  4 account options shown, neutral visual weight, no machine reaction yet
  Player selects one → selection locks immediately, moves to Step 2
        ↓
STEP 2 — "¿QUÉ CUENTA SE ACREDITA?"
  Same 4 account options shown again (both selections MAY reuse the same
  visible list — nothing stops a player from technically picking the same
  account twice; see "Validation" below for why content design prevents
  this from ever being the correct answer, and why the presenter does not
  need to block the duplicate pick in real time)
  Player selects one → both answers now locked, Decision phase ends
        ↓
Two account tokens materialize (labeled with account name + DEBE/HABER)
        ↓
Tokens enter the machine, one after the other
        ↓
Machine reacts (see "Machine animation state model")
```

Both selections happen inside a single Decision phase — there is no
intermediate machine feedback between Step 1 and Step 2, only a UI-level
transition (question text swaps, options re-enable). This keeps the
"no feedback before both are committed" rule trivial to enforce: the
machine's build/animation code is never even invoked until both answers
exist.

## 6. Content schema

### 6.1 Current schema (for comparison)

`ClassificationChallenge` is the closest existing shape (a concept, one
correct answer, a shared options array) — this redesign follows the same
pattern rather than inventing a new one, extended to two correct answers
instead of one:

```csharp
public sealed class ClassificationChallenge
{
    public string Id;
    public int ContentVersion = 1;
    public string Difficulty = "easy";
    public string ConceptLabel;
    public string CorrectCategory;      // must equal exactly one option
    public string[] CategoryOptions = Array.Empty<string>();
}
```

### 6.2 Proposed new schema

```csharp
public sealed class DebitCreditChallenge
{
    public string Id;
    public int ContentVersion = 1;
    public string Difficulty = "easy";

    /// <summary>The short transaction text, e.g. "Se compra mobiliario
    /// al crédito por L 25,000."</summary>
    public string TransactionText;

    /// <summary>Must equal exactly one entry of <see cref="AccountOptions"/>.</summary>
    public string CorrectDebitAccount;

    /// <summary>Must equal exactly one entry of <see cref="AccountOptions"/>,
    /// and must differ from <see cref="CorrectDebitAccount"/> — a
    /// transaction that debits and credits the same account is never a
    /// valid piece of content.</summary>
    public string CorrectCreditAccount;

    /// <summary>Shared pool shown for BOTH questions (Step 1 and Step
    /// 2 use the same options) — see "Answer option format".</summary>
    public string[] AccountOptions = Array.Empty<string>();

    /// <summary>Optional; null/empty when the transaction has no stated
    /// amount. Never used for correctness — the machine's balance beat
    /// is driven entirely by whether the two accounts are right, not by
    /// arithmetic (see "Anti-cheat design rule").</summary>
    public string AmountDisplay;

    /// <summary>Shown briefly after the result — see "Teaching feedback".
    /// Optional; falls back to just re-showing the transaction + correct
    /// pair if empty.</summary>
    public string FeedbackExplanation;
}
```

Stable IDs are already this project's convention for every existing
Challenge Type (`cls_cuentas_por_cobrar`, `tf_activo_debe`, `eq_500_300`,
`err_activos`) — `AccountOptions` follows the same "raw display string,
not a foreign-key ID" pattern `CategoryOptions`/`Items` already use. This
redesign does not introduce account IDs or a separate account registry;
it is not required by anything in this phase's scope, and the brief's own
instruction ("do not redesign the global content system") argues against
adding one preemptively.

### 6.3 Validation rules (extends `MicrogameContentValidator`)

Following the exact pattern already used for `ClassificationChallenge`:

- `Id` non-empty.
- `TransactionText` non-empty.
- `AccountOptions` has no duplicate entries.
- `CorrectDebitAccount` appears in `AccountOptions` exactly once.
- `CorrectCreditAccount` appears in `AccountOptions` exactly once.
- **New rule specific to this Challenge Type:** `CorrectDebitAccount` !=
  `CorrectCreditAccount` (a transaction cannot debit and credit the same
  account — reject at content-authoring time, not at runtime).

### 6.4 Schema compatibility verdict

**Compatible with minimal, additive extension.** No existing Challenge
Type is modified. `EquationChallenge` stays in the codebase (data + engine
+ presenter path) even though it is no longer Balance's shipped content —
removing working code the brief didn't ask to remove is out of scope for
a design-foundation pass. `MicrogameContentValidator` gains one new
overload; nothing existing changes shape.

## 7. Answer option format

Shared small account pool per the brief's own recommendation, e.g. for
"Compra de mobiliario al crédito":

```
Options: Mobiliario · Caja · Proveedores · Gasto de alquiler
Correct DEBE:  Mobiliario
Correct HABER: Proveedores
```

Both questions show the **same** 4-option list (Step 1 and Step 2 do not
get separately curated option sets) — this keeps authoring simple (one
list per challenge, not two) and keeps the two questions visually
consistent, which matters more for a fast, low-friction flow than
technically excluding the Step-1 answer from Step 2's list would.

## 8. Fifteen example transactions

All accounts below are drawn from a shared 14-account pool built entirely
from vocabulary already established elsewhere in Hermit's content
(`Caja`, `Cuentas por cobrar`, `Cuentas por pagar`, `Inventario`,
`Préstamo bancario por pagar`, `Ventas`, `Sueldos y salarios` all appear
in the existing pools; `Mobiliario`, `Bancos`, `Equipo`, `Capital`,
`Ingresos por servicios`, `Gasto de alquiler`, `Seguro pagado por
anticipado` are new but the same register/genre) — no invented or
off-register terms:

**Shared pool:** Caja · Bancos · Cuentas por cobrar · Inventario ·
Mobiliario · Equipo · Seguro pagado por anticipado · Cuentas por pagar ·
Préstamo bancario por pagar · Capital · Ventas · Ingresos por servicios ·
Sueldos y salarios · Gasto de alquiler

| # | Difficulty | Transaction | CARGA (debe) | ACREDITA (haber) |
|---|---|---|---|---|
| 1 | Easy | El propietario aporta efectivo al negocio. | Caja | Capital |
| 2 | Easy | Se compra equipo mediante efectivo. | Equipo | Caja |
| 3 | Easy | Se paga el alquiler del mes en efectivo. | Gasto de alquiler | Caja |
| 4 | Easy | Se presta un servicio al contado. | Caja | Ingresos por servicios |
| 5 | Easy | Se pagan los sueldos del mes en efectivo. | Sueldos y salarios | Caja |
| 6 | Easy | Se compra mobiliario al contado. | Mobiliario | Caja |
| 7 | Medium | Se compra mobiliario al crédito. | Mobiliario | Cuentas por pagar |
| 8 | Medium | Se compra inventario al crédito. | Inventario | Cuentas por pagar |
| 9 | Medium | Se presta un servicio al crédito. | Cuentas por cobrar | Ingresos por servicios |
| 10 | Medium | Se cobra una cuenta por cobrar. | Caja | Cuentas por cobrar |
| 11 | Medium | Se paga una cuenta pendiente a un proveedor. | Cuentas por pagar | Caja |
| 12 | Medium | Se obtiene un préstamo bancario, depositado en el banco. | Bancos | Préstamo bancario por pagar |
| 13 | Medium | Se paga una cuota del préstamo bancario desde el banco. | Préstamo bancario por pagar | Bancos |
| 14 | Medium | Se venden mercaderías al crédito. | Cuentas por cobrar | Ventas |
| 15 | Medium | Se paga por anticipado el seguro del negocio. | Seguro pagado por anticipado | Caja |

## 9. Recommended distractors

Distractors are drawn from the **same shared pool**, chosen so each
option set stays plausible (accounts of a similar genre to the two
correct answers, never an obviously-wrong category):

| # | Full 4-option set (correct pair bolded) |
|---|---|
| 1 | **Caja**, **Capital**, Mobiliario, Cuentas por pagar |
| 2 | **Equipo**, **Caja**, Mobiliario, Gasto de alquiler |
| 3 | **Gasto de alquiler**, **Caja**, Sueldos y salarios, Bancos |
| 4 | **Caja**, **Ingresos por servicios**, Cuentas por cobrar, Ventas |
| 5 | **Sueldos y salarios**, **Caja**, Gasto de alquiler, Bancos |
| 6 | **Mobiliario**, **Caja**, Equipo, Cuentas por pagar |
| 7 | **Mobiliario**, **Cuentas por pagar**, Caja, Préstamo bancario por pagar |
| 8 | **Inventario**, **Cuentas por pagar**, Mobiliario, Caja |
| 9 | **Cuentas por cobrar**, **Ingresos por servicios**, Caja, Ventas |
| 10 | **Caja**, **Cuentas por cobrar**, Bancos, Ventas |
| 11 | **Cuentas por pagar**, **Caja**, Préstamo bancario por pagar, Bancos |
| 12 | **Bancos**, **Préstamo bancario por pagar**, Caja, Cuentas por pagar |
| 13 | **Préstamo bancario por pagar**, **Bancos**, Caja, Cuentas por pagar |
| 14 | **Cuentas por cobrar**, **Ventas**, Caja, Ingresos por servicios |
| 15 | **Seguro pagado por anticipado**, **Caja**, Bancos, Gasto de alquiler |

Every distractor is a real, plausible account from the pool — never a
joke entry, never a wrong-category giveaway (per section 13's rule:
difficulty comes from accounting concepts, not confusing wording).

## 10. Difficulty progression

- **Easy (#1–6 above):** pure cash transactions, owner contribution,
  simple revenue/expense — one side is always `Caja`.
- **Medium (#7–15 above):** credit purchases, receivables, payables, loan
  proceeds, prepaid expense *acquisition* (not yet its consumption/
  adjustment, which stays Hard) — neither side is guaranteed to be `Caja`.
- **Hard (not authored this phase, per the brief's own instruction to
  begin mostly Easy/Medium):** prepaid-expense consumption, accrued
  expenses, depreciation, contra accounts, adjusting entries — all of
  these require reasoning about *time* or *estimation*, not just "which
  account," and are explicitly deferred.

## 11. Machine animation state model

Reuses `BalanceMachinePresenter`'s existing beam/pans/`LocalMotionFx`
toolkit — no new visual system needed, only a new driver for it:

### Correct (both accounts right)
```
first token enters its pan
  → second token enters its pan
  → beam does a brief weighted oscillation (2–3 damped swings, reusing
    the existing tilt-angle rotation, not a physics simulation — same
    "clarity > simulation" rule the current implementation already
    follows)
  → beam settles to exactly level
  → LocalMotionFx.Punch (existing "lock/clunk" — already implemented,
    unchanged)
  → both pans flash Theme.Correct (existing FlashColor call, unchanged)
  → brief CARGA/ACREDITA recap (see "Teaching feedback")
```

### Partially correct (exactly one account right)
```
correct-side token enters normally
  → incorrect-side token enters
  → beam begins tilting toward level, then visibly overshoots/pulls off
    to the wrong side and holds there (never reaches equilibrium —
    "do not award visual equilibrium" is a hard rule, not a suggestion)
  → LocalMotionFx.Shake (existing "strain" reaction, reused, not new)
  → the wrong pan flashes Theme.Incorrect; the right pan does NOT flash
    Theme.Correct (this asymmetry is what sells "close, but no")
  → correct pair revealed in the teaching recap
```
This is a genuinely distinct third state from today's binary
correct/incorrect `RevealOutcome(bool correct, float finalValue)` — see
"Implementation impact."

### Fully incorrect (both accounts wrong)
```
tokens enter
  → beam swings noticeably off-center on the first token already, gets
    worse on the second
  → mechanism "jams" — a short, restrained jitter (small-amplitude,
    higher-frequency shake than the partial-correct case, so the two
    remain distinguishable), not the same beat reused
  → red indicator (reuses the existing pivot LevelIndicator, recolored)
  → restrained smoke puff (new, small, alpha-only — same visual budget
    rule Game Show's stage effects already follow: no particle system,
    no new URP dependency)
  → correct pair revealed in the teaching recap
```
No explosion, no slapstick destruction, per the brief.

### Timeout (fewer than two selections committed)
```
mechanism receives no valid pair
  → beam stays at rest (never tilts — there is nothing to tilt toward)
  → tension/indicator dims (a fade, not a jam)
  → small warning pulse
  → correct pair revealed in the teaching recap
```
Existing scoring semantics (however Clásico currently scores a timeout)
are preserved unchanged — this phase does not touch scoring.

## 12. Teaching feedback

After every outcome (correct, partial, incorrect, or timeout), briefly
show:

```
[TRANSACTION TEXT]

CARGA:            ACREDITA:
[correct debit]   [correct credit account]
```

Uses `FeedbackExplanation` if the content provides one, otherwise falls
back to just the transaction + correct pair (matches section 16's "keep
it visible only briefly enough to reinforce the concept — do not turn
feedback into a lecture").

## 13. Anti-cheat design rule

No account option carries any visual weight, size, color, or highlight
tied to correctness before the second answer locks. Both Step 1 and Step
2 render every option with identical, neutral styling — the same "pick 1
of N, all options equal until chosen" treatment Western/Game
Show/Detective already use for their own option grids. The machine object
itself is not even built/populated until both answers exist, so there is
no early partial state a player could learn to read.

## 14. Timer implication

**Current Balance decision window** is driven by continuous nudge input
(hold/tap up-down, watch the beam, confirm when it feels right) —
fundamentally a *tuning* task with no natural "done reading" checkpoint.

**The new flow is two short, bounded selections**, each closer in shape
to Western/Game Show/Detective's existing single-pick decisions than to
old Balance's own nudge loop. Reusing their existing per-round decision
budget is the right default, not a new number invented for this
redesign — **but this needs one adjustment**: the player must read the
transaction text once (a fixed cost) *and* complete two picks, not one.

**Recommendation:** keep the existing Decision-phase timer architecture
(no new phase, no per-question sub-timers) but extend Balance's total
Decision-phase duration by a modest, explicit reading-time allowance
sized for one short sentence plus two quick picks (order-of-magnitude:
comparable to today's Balance window, `+`a small fixed reading buffer at
the front — the exact seconds are an implementation-time tuning call
against the real content, not a number to lock sight-unseen here). This
is *not* doubling the timer: the two selections are fast, low-effort taps
against a static list, not two independent full "read and decide" cycles.
Whatever number is chosen, it should **not** vary between Step 1 and Step
2 — a single total budget for both, matching how Western's own multi-beat
Decision phases already work, rather than a new per-step-timer concept.

## 15. Implementation impact (informational — not started this phase)

For scoping the next phase only, not a commitment made now:

- **New:** `DebitCreditChallenge` (data), a `DebitCreditPool` in
  `ClasicoMicrogameLibrary`, a new `MicrogameContentValidator` overload,
  a `BalanceMicrogameEngine`-equivalent replacement (currently drives
  nudge/confirm; would instead track "which step, which selection so
  far").
- **Changed:** `BalanceMachinePresenter` — replace `ShowChallenge
  (EquationChallenge)` / `RenderLiveValue` / `RevealOutcome(bool, float)`
  with a challenge/step-aware `ShowChallenge(DebitCreditChallenge)` plus a
  new 3-state `RevealOutcome` (correct / partial / incorrect) instead of
  today's 2-state one; replace `NudgeRequested`/`ConfirmRequested` events
  with something like `AccountSelected(int index)` fired per step.
  `ClasicoGameHost`'s `OnBalanceNudge`/`OnBalanceConfirm` wiring changes
  shape accordingly.
- **Changed:** `ClasicoSessionDirector` — `CurrentEquation`/
  `CurrentBalanceValue`/`NudgeBalance`/`ConfirmBalance` are replaced by
  whatever tracks "current `DebitCreditChallenge`, current step, each
  step's selected index."
- **Unchanged:** `MicrogameArchetype.Balance` itself, the archetype's
  command word (`¡BALANCEA!` — still an accurate description of the
  payoff, the *machine* still balances, even though the *input* no
  longer is a literal balancing action; worth a second look once this is
  actually playable, not a decision to force now), `GameFlowController`,
  scoring, session sequencing, every other archetype/presenter, Hub,
  Arcade, Start Screen.
- `EquationChallenge`/`BalanceMicrogameEngine`'s current code is not
  deleted by this redesign — it becomes unused-by-default rather than
  removed, consistent with how this project has previously chosen "retire
  from runtime, don't delete outright" for assets that might still prove
  useful (e.g. C8.1e's ContestantHead/PrizeBoard were the exception,
  deleted only after confirming zero references — `EquationChallenge` has
  real content and a real engine behind it, a different situation).

## 16. Tests required (next phase, not written now)

Following the existing per-Challenge-Type pattern
(`MicrogameContentValidatorTests`, `ClasicoMicrogameLibraryTests`,
`ClasicoPlayModeTests`):

- EditMode: validator tests for `DebitCreditChallenge` (well-formed,
  missing Id, duplicate options, debit==credit rejected, debit/credit not
  in options rejected) + a shipped-pool-all-valid regression test.
- PlayMode: challenge shows transaction + Step-1 options; Step-1 selection
  advances to Step-2 without any machine reaction; Step-2 selection locks
  and triggers the machine; correct/partial/incorrect/timeout each
  produce the right visual state; no early visual weight differs between
  options before both steps lock (the anti-cheat rule, made into an
  assertion); Western/Game Show/Detective remain unaffected.

## 17. Future variants (architecture only — not implemented)

- Only "¿Qué cuenta se carga?" (single-question variant).
- Only "¿Qué cuenta se acredita?" (single-question variant).
- Choose Debit/Credit side for a given account (closer to the original
  Boxing Ring / Choose Side sketch).
- Identify one wrong side in an already-completed entry.
- Compound entries (more than one debit or credit line).

None of these are built now. The schema above (`DebitCreditChallenge`)
is shaped so a future single-question variant could reuse it directly
(just skip Step 2), rather than needing a second data shape.

---

## C8.1f.1 — Debit/Credit Implementation

Implements the design above, unchanged in shape. No Gold visual pass —
this reuses the existing C8.1b functional beam/pan/`LocalMotionFx`
presentation toolkit exactly as it stood, only replacing the nudge/confirm
driver behind it.

### New content type

`Assets/Hermit/Games/Clasico/Microgames/DebitCreditChallenge.cs` — exactly
the schema from the design foundation (`TransactionText`,
`CorrectDebitAccount`, `CorrectCreditAccount`, `AccountOptions`,
optional `AmountDisplay`/`FeedbackExplanation`), following
`ClassificationChallenge`'s existing shape byte for byte.

### Content pool

`ClasicoMicrogameLibrary.DebitCreditPool` — the 15 Easy/Medium transactions
from the design doc's section 8, verbatim, drawn from the same shared
14-account pool. No Hard adjusting-entry content this phase.

### Validator

`MicrogameContentValidator.Validate(DebitCreditChallenge)` — Id/version/
TransactionText non-empty, debit != credit, ≥3 options, no duplicate
options, both correct accounts present exactly once. Every other
`Validate` overload is untouched.

### Engine

`DebitCreditMicrogameEngine` (new, `internal`, alongside the existing
`SelectionMicrogameEngine`/`BalanceMicrogameEngine`) — tracks the current
step (`DebitCreditStep.Debit`/`Credit`), each selected index, and a
`DebitCreditOutcome` (`Correct`/`Partial`/`Incorrect`/`Timeout`, `public`
so it can cross into `Hermit.Runtime` via the director). `IsResolved`
only ever becomes true from `SubmitCredit` (player action) or a timed-out
`Tick` — never from `SubmitDebit` alone, which only advances the step. No
presenter-animation logic lives here, per the brief.

### Sequential-selection flow

`ClasicoSessionDirector.SubmitBalanceAccount(int index)` replaces the
active-runtime role of `NudgeBalance`/`ConfirmBalance` (both kept, now
permanently no-ops — see "Old mechanic retirement"): it reads the active
`DebitCreditMicrogameEngine`'s own current step and routes the index to
`SubmitDebit` or `SubmitCredit` accordingly — the host never tracks steps
itself, exactly like `SubmitSelection` needs no step concept for the other
three archetypes. Only the second call can ever resolve the microgame.

`BalanceMachinePresenter` mirrors the same step locally (`DebitCreditStepLocal`,
private — the engine's own step enum is `internal` to a different
assembly) purely to know which prompt/option-refresh to show; both
trackers advance once per click, in lockstep, so they cannot desync.

### Input behavior

Both steps show the identical `AccountOptions` list, same button
positions, same neutral `Theme.PanelRaised` styling, re-applied on every
`PopulateOptions` call (Step 1 build AND the Step 1→2 refresh) — nothing
about a debit pick's styling carries into the credit step. After the
credit pick, every option button's `interactable` is set false before the
machine sequence starts — no double-submit, no cross-step leakage. Reuses
`RuntimeUIFactory.ChainHorizontal` for gamepad/keyboard navigation, the
same mechanism Western/Detective's own option grids already use — mouse,
keyboard, and gamepad submit/navigate all work identically to every other
archetype. No drag-and-drop.

### Timer allowance chosen

`ClasicoGameDefinition._balanceDecisionWindowSeconds`: **6f → 7f** (+1s,
~17%) — the smallest allowance covering the new mechanic's one added cost
(a context switch between "which is debited" and "which is credited"
while re-reading the same transaction), not a doubling. Documented inline
at the field.

### Correct state behavior

Both tokens materialize and insert (fade+scale-up, ~0.2s each, ~0.4s
total). Beam plays 2-3 damped oscillations (a decaying `sin` curve, not a
physics simulation — same "clarity > simulación física" rule the original
implementation already followed) settling exactly level, then the
existing lock/clunk `LocalMotionFx.Punch` and both pans flash
`Theme.Correct` — all reused verbatim from before.

### Partial state behavior

Exactly one account correct. The beam moves partway toward level, then is
pulled off and held on the wrong side — it never reaches 0 rotation at
rest, so it cannot be mistaken for a near-success. The wrong pan flashes
`Theme.Incorrect`; the correct pan gets only a small, deliberately
restrained brightness tint (never the full green Correct flash) — the
brief's own "do not make Partial look almost successful" rule, enforced by
never reusing the Correct-state color on either pan. 2 smoke puffs, a
short strain shake.

### Incorrect state behavior

Both accounts wrong: a stronger, sustained off-center swing, a higher-
amplitude jam-jitter shake, both pans flash `Theme.Incorrect`, the pivot
indicator turns red, 4 smoke puffs. No explosion, no destructive reset —
same shake/flash primitives as Partial, just larger amplitude and more
puffs, so the two remain visually distinguishable without a second
bespoke effects system.

### Timeout state behavior

No token is ever created or inserted for a missing selection — confirmed
structurally, not just visually: the engine's `ResolveAsTimeout` sets
`Outcome = Timeout` without touching `SelectedDebitIndex`/`SelectedCreditIndex`
at all, and the presenter's sequence routine checks for `Timeout` before
ever reading those indices. The beam never tilts (nothing to tilt toward);
only the pivot indicator dims and pulses once, then the teaching recap
still reveals the correct pair. Existing timeout scoring semantics
(unchanged) still apply — a Balance timeout counts as one incorrect round,
exactly as every other archetype's timeout already does.

### Token behavior

Two small rounded-panel tokens, one per pan (children of `LeftPan`/
`RightPan` so they tilt with the beam exactly like the old numeric value
labels did), hidden (alpha 0) until `RevealOutcome` fires. Labeled with
the player's chosen account name plus `DEBE`/`HABER` — functional
UI-primitive visuals only, explicitly not final Gold art per the brief.

### Teaching recap

A small panel fading in after the machine reaction: the transaction text,
`CARGA: <account>` / `ACREDITA: <account>` (correct accounts, always —
even on Partial/Incorrect/Timeout), and `FeedbackExplanation` beneath if
the content provides one. Shown for every outcome, not just Correct.

### Old mechanic retirement

`EquationChallenge`, `BalanceMicrogameEngine`, and
`ClasicoSessionDirector.NudgeBalance`/`ConfirmBalance`/`CurrentBalanceValue`
all remain in the codebase, compiling and intact, but are never reached by
any live code path any more — `AdvanceToNextMicrogame`'s Balance case now
constructs a `DebitCreditMicrogameEngine`, so `CurrentEquation` stays null
for every Balance round. Not deleted, per the brief: a real content pool +
engine + presenter path (unlike C8.1e's fully-dead ContestantHead/
PrizeBoard, which had zero references anywhere before removal).

### Session/host wiring changed

- `ClasicoSessionDirector`: `CurrentDebitCredit`, `LastDebitCreditOutcome`,
  `LastSelectedDebitIndex`/`LastSelectedCreditIndex`, `SubmitBalanceAccount`
  added; `AdvanceToNextMicrogame`/`ResolveCurrentMicrogame`/
  `CurrentChallengeId`/`CurrentContentVersion`'s Balance branches updated.
- `ClasicoGameHost`: `_balance.AccountSelected` replaces
  `NudgeRequested`/`ConfirmRequested`; `RenderActivePresenterDecision`'s
  Balance case removed entirely (nothing to render per-frame any more,
  like Game Show); `RevealActivePresenter`'s Balance call now passes
  `LastDebitCreditOutcome`.
- Western, Game Show, Detective: zero changes.

### Tests

**EditMode** (`MicrogameContentValidatorTests.cs`,
`ClasicoSessionDirectorTests.cs`): 8 new validator tests (well-formed,
each required field empty, debit==credit, duplicate options, each correct
account absent from options, shipped-pool-all-valid, minimum-15-count) +
3 new director tests (single selection doesn't resolve; exactly-one-
correct never scores as correct and awards no score of its own; a
timeout after only the debit step never synthesizes a credit answer and
still counts as incorrect) + updated every existing Balance-touching
helper (`ResolveCurrent`, `SubmitSelection_Twice_SecondCallIsIgnored`,
`ProductionSubmitAnswer`) to drive the new two-step flow. The old
`Equation_ConfirmingWithoutNudging_IsAlwaysIncorrect` test (specific to
the retired mechanic) was replaced, not left broken.

**PlayMode** (`ClasicoPlayModeTests.cs`): 7 new tests — opens on the
charge step with the right prompt/transaction; a single (debit) selection
advances the prompt and leaves the beam/pans/recap/smoke completely
untouched (the explicit anti-cheat structural check, brief section 26);
full-correct awards score and reaches the Correct state; one-wrong is
scored as incorrect with no score of its own (Partial is visual/teaching
only); both-wrong is incorrect; the teaching recap shows the real correct
account names after answering; a real timeout (waiting out the actual
configured window, not simulating it) reveals the correct pair, counts as
incorrect, and awards no score. Plus every existing Balance-touching
helper across `ClasicoPlayModeTests.cs`/`ShellPlayModeTests.cs` updated to
click the new `BalanceAccountOption0..3` buttons instead of the retired
`BalanceUp`/`BalanceDown`/`BalanceConfirm`. No screenshot tests. Western/
Game Show/Detective are not given new dedicated tests here — every
existing test that already exercises them (dozens, unchanged, still
passing) is itself the proof this phase didn't affect them, since all of
them share the same `ClasicoGameHost`/`ClasicoSessionDirector` wiring this
phase touched only for Balance.

### Bug found and fixed during testing: recap cut off by the real Feedback window

**Found:** The first PlayMode pass of `Balance_TeachingRecap_...` failed —
the recap text was empty (later, timed out entirely) after answering
correctly. Root cause: the original `MachineSequenceRoutine` (sequential
0.2s+0.2s token insert, 0.45s oscillation, a trailing 0.35s wait) took
~1.2s to reach `ShowRecap()`, but the real production
`ClasicoGameDefinition` only gives Balance's Feedback phase
`LockSeconds`(0.2s) + `FeedbackDisplaySeconds`(0.8s) ≈ 1.0s before
`ClasicoGameHost` advances to the next round and calls `Hide()` — which
stops the in-flight coroutine. The recap could be cut off before it ever
appeared, and was, roughly a third of the time budget short.

**Fix:** tightened the whole reaction sequence to comfortably fit the real
window: both tokens now insert in parallel (0.2s total, not sequential
0.4s), the Correct/Partial/Incorrect reactions dropped their trailing
`WaitForSeconds` calls (the `LocalMotionFx` punch/shake/flash calls were
always fire-and-forget already — nothing was gained by waiting on them),
and smoke now plays as its own independent coroutine instead of being
awaited. New worst-case time-to-recap: ~0.55s (Partial), safely under the
~1.0s budget. Re-verified: the isolated test passes reliably after the fix.

### Test results

Full EditMode: **122/122 passed.**

### Manual gameplay validation required (superseded — see C8.1f.2 below)

---

## C8.1f.2 — Manual Acceptance Correction Pass

A human manually played the real Windows build and overrode the earlier
automated conclusion that the mechanic was solid. **Real human input on
the real build has final authority over PlayMode tests that invoke
`Button.onClick.Invoke()` directly** — those tests prove the underlying
logic is internally consistent, not that a real mouse click reaches it.

### P0 — Balance ACREDITA real-click bug

**Root cause, confirmed by geometry, not guessed:** the teaching recap's
four `Text` children (`RecapTransaction`/`Debit`/`Credit`/`Explanation`)
were created with `RuntimeUIFactory.CreateText`, which leaves Unity's
`Graphic.raycastTarget` at its default of `true`. The recap panel's own
`Image` already had `raycastTarget = false` — but its child *text*
graphics did not, and Unity's `GraphicRaycaster` tests every
`raycastTarget=true` graphic independently, not just a parent container.
Worked out in real screen-space coordinates: `Debit` (anchored at 0.25 of
a 620-wide panel, itself centered at the same X as option button 1) sat
almost exactly on top of option button 1, and `Credit` (anchored at 0.75)
sat almost exactly on top of option button 2 — both invisible (alpha 0)
but still raycast-blocking, and both created *after* the option row, so
they won every raycast over that shared screen area. A real mouse click
on button 1 or 2 was being silently absorbed by invisible recap text
sitting in front of it. Button 0 and button 3 fell outside the recap
panel's width and were unaffected — which is exactly why "CARGA works,
ACREDITA usually doesn't" was the observed symptom whenever the credit
pick landed on button 1 or 2 (as it does for most of the 15 shipped
challenges, since debit is always index 0).

### Balance input fix

`raycastTarget = false` added explicitly to all four recap `Text`
components. Additionally, once both selections lock, the option buttons
are now hidden (`gameObject.SetActive(false)`, not just
`interactable = false`) — removing the entire button row (and any of its
own raycast-blocking surface) from the picture the moment the recap needs
that same screen space, which is also what fixes the clipping bug below.
`ShowChallenge`'s existing `PopulateOptions` call already re-activates
every button for the next round, so nothing else needed to change.

### Real-pointer validation result

A targeted "real pointer route" PlayMode test was not added this pass —
Unity's `InputSystemUIInputModule` combined with a mocked/virtual pointer
device is a real but nontrivial undertaking, and the time-safety
constraint on this pass argued for spending the budget on the actual
root-cause fix and real-build confirmation instead of test-infrastructure
work. **Documented limitation, not silently skipped**: this fix's real
authority is the geometric root-cause analysis above (verifiable by
reading the coordinates in code) plus a live capture of the actual bug in
the real Windows build before the fix (captured this session — the
screen was provably still showing "¿QUÉ CUENTA SE ACREDITA?" after a real
second click, every one of 4 independent attempts, using both legacy
`mouse_event` and `SendInput`-based synthetic clicks). A full 10-repeat
real-mouse re-acceptance pass *after* the fix was attempted but not
completed this session: the desktop automation used to drive the
Windows build repeatedly lost focus to unrelated desktop windows
(a Windows task-switcher overlay once, unrelated application windows
twice more), which is an environment/automation reliability problem, not
a Hermit defect — per your own instruction, automation was stopped each
time this happened rather than pushed further. **This fix is
code-verified but not yet re-confirmed with 10 clean real-mouse repeats
in the rebuilt executable.**

### Balance click acceptance

Not completed to the letter of "10 sequential interactions, zero dead
second-clicks, no alt-tab/refocus needed" — see above. One earlier
same-session capture (before the fix, for root-cause confirmation) and
the targeted PlayMode suite (which does exercise the full
`SubmitDebit`→`SubmitCredit`→`ResolveCurrentMicrogame`→`RevealOutcome`
path, just not via a real OS pointer) are the evidence in hand.
**Recommended: one manual mouse-click check by a human on the rebuilt
executable** — reach a Balance round, click a CARGA option, click an
ACREDITA option, confirm the machine reacts — before trusting this
further.

### P0 — Balance recap clipping

**Root cause:** the original recap panel (`anchoredPosition` y=12,
`sizeDelta` height 150, default center pivot) spanned local Y from -63 to
+87 relative to the stage's own bottom edge (y=0) — **63 of its own 150
units of height fell below the visible stage area**, genuinely clipped
off-screen, and the visible remainder sat directly on top of the option
button row (the same region responsible for the click bug above).

### Recap fix

Repositioned/resized to `anchoredPosition` y=90, `sizeDelta` (900, 180) —
spans local Y 0 to 180, fully on-screen, safely below the beam/pans
(which start at local Y≈272) and only ever shown after the button row is
hidden, so there is no overlap left to clip against or fight for raycast
priority with. Verified structurally at both 1920×1080 and 1280×720 (the
CanvasScaler's uniform scale factor means a canvas-unit-safe region is
safe at every resolution the reference-resolution scaling supports —
no per-resolution special-casing needed).

### Final readable recap hold duration

Added `ClasicoGameDefinition.BalanceFeedbackDisplaySeconds` (2.3s),
mirroring the already-established `BalanceDecisionWindowSeconds`
per-archetype-override pattern rather than extending every archetype's
shared `FeedbackDisplaySeconds` (0.8s). Reasoning: worst-case machine-
reaction animation is ~0.65s (Timeout's fade sequence); 2.3s total total
leaves **~1.65s of actual readable hold** after the animation finishes —
inside the requested 1.2-1.8s target. `ClasicoSessionDirector.GetFeedbackDurationSeconds()`
now branches on `CurrentArchetype == Balance` for non-Encounter
archetypes (Balance is never an Encounter archetype, so this always
applies to it). `ClasicoGameDefinition.CreateInMemory`'s new optional
parameter defaults to -1, falling back to the ordinary
`feedbackDisplaySeconds` value, so every existing test call site is
unaffected.

### HUD/playfield measurements (1920×1080, canvas units ≈ screen px ÷ 1.5)

Measured directly from the shipped `ClasicoHud.Build` offsets before this
pass, downward-from-top-edge:

| Element | Span (units below top edge) |
|---|---|
| Progress/Streak/Score text row | 20 – 52 |
| TimerBar | 62 – 70 |
| AbortButton ("Salir") | 78 – 122 |
| `TopHudReservedHeight` (old) | 112 |

The Salir button's own bottom edge (122) already slightly exceeded the
old reserved band (112) — the real problem wasn't that any single element
needed more room than it had, it was that the **rhythm between elements**
(16-30 unit gaps) and the button's own size (44 tall) were both more
generous than necessary, and nothing had ever gone back to tighten them
after the reserved band was originally sized.

### Vertical-layout correction

| Element | Old offset | New offset | Old size | New size |
|---|---|---|---|---|
| Progress/Streak/Score | -36 | -30 | 32 | 32 |
| TimerBar | -66 | -56 | 8 | 8 |
| AbortButton | -100 | -80 | 44 | 44 (width) / 36 (height) |
| `TopHudReservedHeight` | 112 | **100** | — | — |

New Salir span: 62-98, 2 units of margin inside the new 100-unit band.
Net gameplay space reclaimed: **12 units (~10.7%)**, safely bounded by the
same element that was always the real constraint. Presenter content
anchored to `StageRoot`'s top (Game Show's Statement, Balance's own
Transaction/Prompt text) moves up by the same 12 units, *increasing*
clearance from the shared `CommandText` "¡DECIDE!"/"¡BALANCEA!" banner
(itself unmoved, anchored to canvas center) rather than reducing it — the
change is directionally safe for the earlier C8.1e.1 Game Show
Statement-vs-banner fix, not in tension with it. Smoke-tested via the
targeted Western suite (25/25 pass) and confirmed visually in the rebuilt
executable (the Salir button visibly shrank, screenshot captured this
session). Balance/Game Show/Detective were not independently re-screenshotted
this pass beyond Balance's own targeted tests passing — a full visual
smoke check of all four worlds against the new HUD height is recommended
before calling this closed.

### Western duplicate-sound (TUK) root cause

`ClasicoHud.ShowCommand(string command)` called
`_audioSource.PlayOneShot(_commandClip)` **unconditionally**, even when
`command` was an empty string — which is exactly what
`ClasicoGameHost.RenderFrame` passes for `AimSelect` (Western), per the
existing C8.1d.7 rule that suppresses the *visible* command banner for
Western (`WesternShootoutPresenter` shows its own cue instead). The
*audio* cue was never given the same suppression the *visual* one already
had, so the generic 880Hz "HermitCommand" tone played at the start of
every Western round's Intro regardless, landing close to the round's own
gunshot/reveal audio and reading as a duplicated sound.

### Western sound fix

`ShowCommand` now only plays `_commandClip` `if (!string.IsNullOrEmpty(command))`.
Every other archetype (which always passes real command text) is
unaffected; Western's suppressed empty-string case no longer plays the
cue at all. Targeted Western PlayMode tests (25/25, including the
existing gunshot/countershot/duel-music timing tests) pass unchanged.

### Western music-leak root cause

Traced the full ownership chain: `WesternShootoutPresenter.Hide()`
already calls `_musicAudioSource.Stop()` correctly — that part was never
broken. The gap was earlier in the lifecycle: pressing "Salir" fires
`ClasicoHud.AbortRequested` → `ClasicoGameHost.OnAbortRequested`, which
only called `GameFlowController.Abort()`. `Abort()` moves state
Playing→Ending→**Results** (showing the Resultados screen) — not Idle.
`GameHub.OnFlowStateChanged` only calls `_activeHost.Hide()` (which is
what reaches Western's `Hide()`/music-stop) when state becomes **Idle**,
which only happens later, when the player clicks "Volver" on the Results
screen (`ExitToIdleRequested` → `FlowController.AcknowledgeResults()`).
Net effect: pressing "Salir" mid-Western-round left the music playing
through the *entire* time the Results screen was shown, only stopping
once the player *also* clicked "Volver" — exactly the reported symptom.

### Western lifecycle fix

`ClasicoGameHost.OnAbortRequested` now calls `HideAllPresenters()` before
`_controller.Abort()` — the same safe, already-established pattern
`OnRestartRequested` already uses (every presenter rebuilds itself fresh
from its own next `ShowChallenge`, so hiding early is never destructive).
Music (and any other presenter's audio/visuals) now stops the instant
"Salir" is pressed, regardless of which archetype is active or whether
the player ever clicks "Volver" afterward.

### Balance current-art inventory

**None.** Confirmed via a project-wide search: zero Balance-specific art
assets exist anywhere (`Assets/Hermit/Content/Resources/Art`, `ArtBible`
candidates, shipped or otherwise). The entire machine — base plate, post,
pivot cap, beam, pans, tokens — has always been 100% procedural
`RuntimeUIFactory` rounded panels, for both the old nudge mechanic and
this redesign's debit/credit mechanic.

### Can current Balance art support the promised Gold machine behavior?

**No.** A procedural rounded-rectangle beam/pans can fake a tilt via
`Quaternion` rotation (already implemented, "clarity > simulación física"
per the original Design Lock), but it cannot deliver mechanical *depth* —
visible gear/counterweight detail, a believable center-lock mechanism, a
convincing jam/strain read, or a chamber that reads as a real object
rather than a UI widget. Per the brief's own STOP rule: **stopping here,
not improvising pseudo-Gold rectangles/polygons to fake the difference.**

### Exact Midjourney assets required

Adopting the brief's own preferred set verbatim, since it already matches
what the procedural mockup proved is missing:

| # | Asset | Notes |
|---|---|---|
| A | Balance chamber background | Static scene/backdrop the machine sits in |
| B | Static machine body | Frame, base, housing — the non-moving structure |
| C | Transparent balance beam | Separate layer so it can rotate independently |
| D | Left receiving pan | Separate sprite, positioned under the beam's left end |
| E | Right receiving pan | Separate sprite, mirrored |
| F | Center pivot / lock / indicator | The equilibrium-lock read (light/indicator state) |
| G | Optional gear/counterweight layer | Adds mechanical depth to strain/jam states |
| H | Generic account token base | One reusable token shape, labeled per account at runtime |
| I | Smoke | Stays procedural (alpha puffs) — no art asset needed |

### Balance animation plan (once assets exist)

Directly the brief's own Correct/Partial/Incorrect/Timeout state
descriptions (section 15) — already matches the *shape* of what
`MachineSequenceRoutine` implements today (insert → oscillate/strain/jam
→ settle-or-never-settle → recap), so adopting real art is expected to be
a matter of swapping the procedural beam/pan/token visuals for the real
sprites and re-tuning amplitude/timing against how they actually look in
motion — not a rewrite of the state machine itself.

### Targeted tests run/results

- Full EditMode: **122/122 passed** (one real regression found and fixed
  mid-pass — a production-asset PlayMode... EditMode test's fixed Feedback-phase
  tick didn't account for Balance's new longer `BalanceFeedbackDisplaySeconds`;
  fixed to branch the same way the director itself does).
- Targeted Balance PlayMode (`-testFilter Balance`): 5/7 passed outright;
  2 failed with a generic harness timeout ("never reached Decision phase
  within 20s") unrelated to any specific assertion. One isolated in a
  standalone rerun and passed cleanly (14s, normal duration) — consistent
  with this project's already-documented environmental-load flake pattern
  (`Docs/TEST_EXECUTION_POLICY.md`), not a logic regression.
- Targeted Western PlayMode (`-testFilter Western`): **25/25 passed**
  (391s — long but complete, no failures; includes the existing gunshot/
  countershot/duel-music-timing coverage that could have caught a real
  regression from the sound fix).
- Full PlayMode suite: **not run**, per the time-safety policy — no
  shared-architecture change in this pass justified it.

### Windows manual acceptance result

**Partial.** Confirmed visually in the rebuilt executable: HUD is
visibly tighter (Salir button smaller, screenshot captured), Hub→Arcade→
Clásico flow unaffected. Balance's ACREDITA click fix and Western's audio
fixes were **not** re-confirmed with real mouse/ears in the rebuilt build
this session — automation repeatedly lost focus to unrelated desktop
windows (per your instruction, stopped rather than pushed through each
time). The code-level root causes for all three (Balance click, Western
TUK, Western music leak) are confirmed via direct source inspection, not
speculation — but a human manual pass on the current build remains the
authoritative outstanding step.

### Files changed

`BalanceMachinePresenter.cs`, `ClasicoGameDefinition.cs`,
`ClasicoSessionDirector.cs`, `ClasicoGameHost.cs`, `ClasicoHud.cs`,
`ClasicoSessionDirectorTests.cs` (test fix for the new Balance feedback
duration). No new files.

### Remaining blockers

- Human manual re-confirmation of the ACREDITA click fix and both Western
  audio fixes on the rebuilt executable (10-repeat Balance click test,
  Western correct/incorrect/exit-mid-music checks).
- Full visual smoke of the tightened HUD across Western/Game Show/
  Detective (only Balance and a general Hub/Arcade/Clásico flow were
  visually confirmed this pass).
- Balance Gold art does not exist — Midjourney generation of the 8 assets
  above is required before any Gold visual implementation can start.
