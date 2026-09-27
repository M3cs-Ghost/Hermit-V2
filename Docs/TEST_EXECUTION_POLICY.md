# Hermit V2 — Test Execution Policy

**Status: ACTIVE POLICY — effective immediately, applies to all phases from here forward.**

This document exists because repeated full-suite PlayMode execution during
normal implementation work has become expensive enough to be
counterproductive, without ever increasing actual confidence. It replaces
ad-hoc "just run everything, then run it again" behavior with a targeted,
evidence-based test strategy.

## 1. The observed problem

**Historical baseline** — full PlayMode suite: **~11 minutes.**

**Recent observed durations** — full PlayMode suite: **~43–60+ minutes**,
repeatedly, across multiple unrelated development phases (C8.1e.1 Game
Show validation, C8.1f.1 Balance implementation), not a one-off.

During these abnormal runs, the same pattern has now repeated enough times
to be a signature, not a coincidence:

- unrelated Western tests time out or fail
- Detective/RNG tests fail intermittently (a pre-existing, previously
  documented non-deterministic finding)
- Shell tests occasionally fail
- failures disappear immediately when the same test is rerun in isolation
- **the specific set of failing tests differs between runs** — no two full
  runs under load have failed the same combination of tests
- the actual feature scope under active development (Game Show-specific,
  Balance-specific) remains clean every time, even when the surrounding
  suite is failing
- `Get-Process` has repeatedly confirmed heavy concurrent CPU load from
  unrelated applications (other Claude sessions, browsers, Office, VPN
  clients, Unity Hub itself) at the exact times these runs were degraded

This combination — different failures each time, on tests unrelated to the
change, with the changed scope itself clean, under confirmed heavy
external load — is the signature of environmental degradation, not a
product regression. Chasing a fully green full-suite run under these
conditions by repeating the whole suite has already cost multiple hours
across recent phases without ever producing new information.

## 2. Development-loop testing (day-to-day, during active implementation)

**Do NOT run the full PlayMode suite after every local change.**

Instead, for each change, run only:

- **A.** Compile validation (a fast EditMode pass already does this — a
  compile error surfaces as an immediate, total EditMode failure).
- **B.** The EditMode tests for the system(s) actually changed.
- **C.** The PlayMode fixture/class/tests for the system(s) actually
  changed.
- **D.** A small set of directly-adjacent regression tests — not the
  entire suite.

**Worked example (Balance):** run the DebitCredit content-validation
tests, the Balance engine/director tests, the Balance
presenter/host-wiring tests, the Balance PlayMode tests, one or two
general session-flow regression tests, and — only if shared
`ClasicoGameHost`/`ClasicoHud`/`ClasicoSessionDirector` infrastructure was
touched — one Western smoke test and one Game Show smoke test. Do **not**
automatically run every Shell/Western/Detective test just because a
Balance-only change was made.

## 3. Full EditMode

Full EditMode is cheap — historically ~110-125 tests, sub-second to a few
hundred milliseconds. **Keep running it freely and often** — there is no
cost trade-off here, only PlayMode is rationed.

## 4. Full PlayMode — checkpoint-only

Run the **complete** PlayMode suite only at meaningful checkpoints:

- a feature's implementation is complete
- immediately before a manual-validation candidate build
- a pre-commit checkpoint
- a shared-framework change (anything touching `ClasicoGameHost`,
  `ClasicoHud`, `ClasicoSessionDirector`, `GameFlowController`, or other
  cross-archetype infrastructure)
- a session-architecture change
- a final regression pass before closing a phase

**Not** after every small iteration inside a phase.

## 5. Environmentally-degraded threshold

**Baseline:** ~11 minutes.

**If a full PlayMode run exceeds ~20 minutes** without evidence that the
current feature legitimately added that much execution time (e.g. a
genuinely new, slow scenario), **mark the run `ENVIRONMENTALLY DEGRADED`**
in whatever report references it. Do not interpret time-adjacent failures
from that run as immediate regressions — proceed only to targeted
diagnostics (section 6).

## 6. Failure isolation procedure

If a full-suite run is environmentally degraded (section 5) and unrelated
tests fail:

1. **Do not immediately rerun the full suite.**
2. Classify each failure by subsystem (which presenter/engine/director
   area does it touch?).
3. Judge plausibility: could the actually-changed code have affected this
   subsystem at all? (Different assembly, different archetype, untouched
   file → implausible.)
4. Rerun **only** the failed test, or at most its containing
   fixture/class — never the whole suite — to check it in isolation.
5. If the isolated rerun passes cleanly, record it as environmental/flaky
   evidence and move on. **A clean isolated rerun is sufficient evidence**
   for a known-unrelated flaky failure unless a deterministic relationship
   to the current change appears (section 7).

**Never** perform the loop: full suite → unrelated timeout → full suite
again → different timeout → full suite again. This exact loop has already
consumed multiple hours this project without increasing confidence, across
more than one phase. Two isolated reruns of the actually-affected tests
are worth more evidence than a third hour-long full-suite attempt.

## 7. This does not mean ignoring failures

A failure is still a **real regression candidate** — investigate it,
don't wave it away — if any of the following hold:

- it reproduces in isolation (not just inside a loaded full run)
- it consistently reproduces across multiple independent runs
- it touches code that was actually changed this phase
- it appears under normal (non-degraded) runtime conditions
- the assertion fails for a logical reason, not a timing reason (wrong
  value, wrong state, wrong classification — not "arrived one frame late")
- scoring, content, or persisted state is wrong

The policy narrows *when to stop re-running the full suite*, not *whether
to take a failure seriously*.

## 8. Timing-fragile tests — record, don't fix mid-feature

For failures involving `WaitForSeconds`, animation-completion polling,
audio sequencing, `LocalMotionFx` durations, phase transitions, timeout
polling, or other frame/wall-clock-dependent waits: when investigating,
note whether the test relies on a fragile wall-clock assumption (a fixed
sleep instead of a state-driven wait, or an assumed animation duration
that a content change could invalidate).

**Do not rewrite unrelated tests as a side effect of an unrelated feature
phase** (e.g. don't refactor Western timing tests while implementing
Balance). Instead, record the specific test as a candidate for the future
hardening pass (section 12).

One instance of this already happened and was fixed in-scope, not
deferred, because it was a real bug in the code the phase was actively
changing: C8.1f.1's own `Balance_TeachingRecap_...` PlayMode test first
failed because the presenter's reveal-animation timing didn't fit inside
the real Feedback-phase window before `Hide()` cut it off — a genuine
product timing bug surfaced by a timing-sensitive test, not a flaky test.
That distinction (product bug surfaced by timing vs. test itself being
wall-clock-fragile) is exactly what step-by-step investigation is for.

## 9. System diagnostic snapshot

When a run exceeds the 20-minute threshold, collect **one** lightweight
diagnostic snapshot — not continuous polling or logging:

- Unity Editor process CPU/memory (if obtainable)
- total system CPU across processes (`Get-Process | Sort-Object CPU
  -Descending | Select-Object -First N`)
- available system memory
- whether a machine sleep/resume occurred during the run, if detectable
  (e.g. a date/time discontinuity in logs)
- any unexpected heavy processes found (browsers, other AI sessions,
  office apps, VPN clients — all have been observed as contributors in
  this project already)
- which test was executing when the slowdown was noticed, if available

One snapshot per degraded run is sufficient to support the
"environmentally degraded" classification — this is evidence-gathering,
not ongoing monitoring.

## 10. Balance (C8.1f.1) — the concrete application of this policy

**Required during development** (already satisfied for the completed
C8.1f.1 implementation, and the template for anything that continues on
Balance):

- full EditMode
- all Balance-specific EditMode tests (`DebitCreditChallenge` validator
  tests, `ClasicoSessionDirector` Balance/DebitCredit tests)
- all Balance-specific PlayMode tests
- relevant content-validation regression tests (shipped-pool-valid checks)
- relevant session/host integration tests actually touched by the change

**Regression smoke** (not the full class, not the full suite):

- one representative Western test
- one representative Game Show test
- a Detective representative test only if shared code was touched (it
  wasn't, this phase — Balance's changes were archetype-local)

**Full PlayMode:** one attempt at the implementation checkpoint (done for
C8.1f.1). If that run is environmentally degraded and unrelated tests
fail, isolate those failures (section 6) — do not repeat another
hour-long full-suite run unless an isolated rerun itself reproduces the
problem.

## 11. Checkpoint reporting format

Reports must distinguish **targeted validation** from **full regression
validation**, e.g.:

```
Targeted Balance:
  12/12 PASS

Full EditMode:
  125/125 PASS

Full PlayMode:
  65/68 during an ENVIRONMENTALLY DEGRADED run (48 min vs. ~11 min baseline)

  Unrelated failures:
    - WesternCinematic_DuelMusic_PlaysOnlyDuringRound1Intro...
    - WesternShootout_DisparaCue_HiddenDuringCinematic...
    - ShellPlayModeTests.FutureSlots_AreNotPlayable...

  Isolated reruns:
    3/3 PASS

Conclusion:
  No deterministic regression attributable to Balance.
```

This is more informative than forcing repeated full-suite runs until one
happens to come back green by chance.

## 12. Future test hardening (not started now)

A future dedicated phase — **TEST RUNNER STABILITY / FLAKE HARDENING** —
should address the underlying fragility this policy works around rather
than permanently:

- Western timing/audio tests (cinematic duel music, `DisparaCue` reveal
  timing, screen-kick transform restoration) — recurring source of
  environmentally-triggered failures across multiple phases.
- Detective RNG determinism — the long-standing, previously-documented
  `RealShellScene_DecisionTimeoutOnActivePresenter_RestoresLocalReactionTransforms`
  finding (unseeded `GameContext.Rng` in `GameHub.cs`).
- Shell timing-sensitive tests (e.g. `FutureSlots_AreNotPlayable...`,
  session-restart tests) that have each flaked at least once under load.
- General audit: polling/state-driven waits vs. fixed
  `WaitForSeconds`/sleep-based waits, project-wide.
- Whether long PlayMode wall-clock behavior itself (real, unaccelerated
  time for animation/audio-sequencing tests) is inherently more
  load-sensitive than it needs to be, and whether a seeded/deterministic
  time source could reduce that.

**Do not start that refactor now** — this is a backlog entry, not a task.

## 13. Scope note

This policy governs test *execution strategy* only. It does not change
any production gameplay code, any existing test's assertions (beyond
whatever a specific phase's own scope already required), or scoring/
content/session semantics. Applying this policy required zero production
file changes.
