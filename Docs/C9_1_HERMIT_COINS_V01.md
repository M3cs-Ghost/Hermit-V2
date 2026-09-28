# C9.1 — Hermit Coins v0.1 (off-chain, Clásico only)

> **All reward values below are TUNABLE PROTOTYPE VALUES — not permanent
> economy commitments.** They live in one place, `HermitEconomyDefinition`.

A player who completes a valid Clásico session earns Hermit Coins (HC), sees
exactly why, and the balance persists locally. OFF-CHAIN prototype: no
blockchain, smart contracts, private-key wallets, transfers, marketplace,
store, Codex, seasons, Academy progression or real money.

## Architecture

```
ClasicoSessionDirector ──(read-only)──► ClasicoSessionResult          (Hermit.Games)
                                            │
ClasicoGameHost.OnResultReadyForEconomy     ▼
        └──► HermitEconomy.Service.ProcessClasicoSession(...)         (Hermit.Economy)
                 ├─ HermitRewardCalculator  (pure, deterministic)
                 │     → HermitRewardResult (immutable breakdown)
                 ├─ HermitWallet.TryCredit  (idempotent per session id)
                 ├─ HermitActivityState     (immutable daily/weekly state)
                 └─ IHermitEconomyStore     (versioned JSON save)
        └──► ClasicoHud.ShowRewardSummary(outcome)                    (display only)
```

- `Hermit.Economy` (new assembly) references `Hermit.Core` + `Hermit.Games`.
  `Hermit.Games` does **not** reference it — Clásico never touches currency.
- The only Clásico change is read-only data: the director records one
  `ClasicoRoundRecord` per resolved round and builds `LastSessionResult`
  alongside the existing `GameResult`. No presentation, content, timing,
  scoring or streak logic changed.
- Future swaps stay behind the same seams: `HermitRewardCalculator` →
  server-authoritative calculation; `HermitWallet`/`IHermitEconomyStore` →
  account- or chain-backed wallet; `HermitWalletTransaction` → signed
  transaction. None of that is implemented.

## Session size (decision)

The brief assumed 3-round sessions; a real Clásico session is **9 rounds**
(Western's 3-round encounter + 6 others, every archetype at least twice). The
session was **not** changed. Rules scale by thirds (approved):

- **Accuracy by thirds:** `tier = floor(3 × correct / rounds)` → for 9 rounds
  0–2 correct = +0, 3–5 = +2, 6–8 = +5, 9 = +8 (a 3-round session maps
  0/1/2/3 → +0/+2/+5/+8 exactly as specified).
- **Interaction:** at least 2/3 of rounds (6 of 9).

## Reward formula

```
gameplay = Base (12) + Accuracy (0/2/5/8) + Speed (0–4)
after    = round_half_away_from_zero(gameplay × multiplier(dailySessionNumber))
total    = after + FirstSessionBonus + VarietyBonus + WeeklyConsistencyBonus
```

Bonuses are **never** multiplied. Rounding rule (the only one): round half
away from zero (`24 × 0.60 = 14.4 → 14`, `25 × 0.02 = 0.5 → 1`).

## Valid-session rule

A session pays only if **all** hold:

1. it reached its natural result (not aborted — `Salir` pays 0);
2. every planned round resolved (9 of 9);
3. real interaction on ≥ 2/3 of rounds (≥ 6 of 9).

**Interaction** = an answer actually committed through the real input path:
a submitted selection (Western / Game Show / Detective) or at least the debit
pick (Balance). A decision timeout is never interaction; nothing is inferred
from elapsed time. Timeouts are allowed — 3 timeouts in 9 is still valid, and
correctness is not required for the participation reward. An all-passive
session pays 0. An invalid session changes nothing: no session slot, no bonus,
no active day, no save.

## Speed normalization

Per round, only when the round was answered **correctly** by a real interaction:

```
s = clamp01(1 − responseSeconds / answerWindowSeconds)
```

`answerWindowSeconds` is that archetype's own window (Western 3.2 s, Game
Show 4.2 s, Detective 4.2 s, Balance 7 s), so rounds are comparable across
archetypes. Wrong answers and timeouts contribute 0 (no reward for fast random
clicking or waiting). Session score = `Σ s / rounds` ∈ [0, 1].

```
SpeedBonus = min(4, floor(score × 5))
score:  [0,0.2) → 0   [0.2,0.4) → 1   [0.4,0.6) → 2   [0.6,0.8) → 3   [0.8,1] → 4
```

Gameplay timers are untouched.

## Daily diminishing returns

By the number of the valid session today (1-based), applied to gameplay only:

| Session today | Multiplier |
|---|---|
| 1–2 | ×1.00 |
| 3–5 | ×0.60 |
| 6–10 | ×0.05 |
| 11+ | ×0.02 |

The summary always shows it (`Práctica adicional ×0.60`) — never a silent cut.

## Daily first-session bonus — +25 HC

First **valid** session of the local calendar day. Once per day; invalid
sessions don't consume it.

## Variety bonus — +15 HC (DEFERRED in v0.1)

Rule: all four archetypes played that day via valid sessions, once per day, in
the session that completes the set. **Disabled** in v0.1
(`DailyVarietyBonusEnabled = false`): every valid 9-round session already
contains all four archetypes, so it would always fire alongside +25. The value,
the per-day archetype tracking and the logic are implemented and tested; enable
it once variety spans multiple Hermit modes.

## Weekly consistency bonus — +225 HC

Awarded on the **5th distinct active day** (a day with ≥ 1 valid session) of
the same local **Monday–Sunday** week, in that day's first valid session. Once
per week; resets with the new week. No streak-loss punishment.

## Days, weeks, clock

`IHermitClock` (`SystemHermitClock` in the game, `FixedHermitClock` in tests)
is the only source of "now". Day = local calendar date; week = Monday–Sunday,
identified by its Monday (`yyyy-MM-dd`). `HermitActivityState.RolledOverTo`
resets daily fields on a new day and weekly fields on a new week — so an app
restart on a later day behaves exactly like staying open over midnight.

## Wallet, persistence, idempotency

- `HermitWallet`: `Balance`, `LifetimeEarned`, `LifetimeSpent` (0 — no store).
  No public setters; the only mutation is `TryCredit(reward)`, which records a
  `HermitWalletTransaction` (amount, type, UTC timestamp, reference = session
  id, source) and refuses invalid rewards and already-credited ids.
- `HermitEconomySaveData` (version 1): wallet, last 100 transactions,
  credited reward ids, and the daily/weekly activity state — one structured,
  versioned JSON file: `Application.persistentDataPath/hermit_economy.json`
  (same local-file approach as C4's session store). Writes go through a temp
  file; a corrupt or newer-version file is set aside as `.bak`, never silently
  overwritten.
- **Idempotency:** the reward id is the Clásico session id. Re-raising a
  result, re-showing the Results screen, reloading the scene or restarting the
  app never pays the same session twice (the credited-id list is persisted);
  claimed daily/weekly bonuses survive restarts.

## Reward summary UI

On Clásico's Results screen only — never during rounds. The results card moves
left; a "HERMIT COINS" plaque (Game Show plaque language: dark fill, gold edge,
Marcellus) shows:

```
HERMIT COINS
Sesión N de hoy
Participación              +12 HC
Precisión  9/9              +8 HC
Velocidad                   +3 HC
Práctica adicional         ×0.60     (only below ×1.00)
Primera sesión del día     +25 HC    (only when paid)
Constancia semanal        +225 HC    (only when paid)
──────────────────────────────────
GANASTE                     48 HC   (calm count-up)
SALDO                      123 HC
```

Invalid sessions show "Sin Hermit Coins esta vez" and why (abandoned /
incomplete / "Respondiste X de 9 rondas. Responde al menos 6…"). No coin
showers, casino sounds or lootbox language; no new art/audio.

## Dev tooling (not shipped)

Editor menu (Editor-only assembly): **Hermit → Economy (Dev) → Log Economy
State** (balance, today's sessions/archetypes, week's active days, claims) and
**RESET Economy** (confirm dialog, deletes the save). For a Windows build,
reset by deleting `%USERPROFILE%\AppData\LocalLow\<company>\<product>\hermit_economy.json`.

## Tests

- EditMode `HermitEconomyTests`: accuracy (9- and 3-round), speed buckets and
  per-archetype normalization, every multiplier boundary, rounding, first
  session, variety (disabled / not complete / completes now / claimed),
  weekly (days 1–4, 5, claimed, new week), invalid sessions (abort, passive,
  too few interactions, incomplete), valid-with-timeouts, wallet crediting,
  duplicate ids, invalid-doesn't-consume-bonus, save/reload, day & week
  rollover, reset, file store round-trip + corrupt file.
- EditMode `ClasicoSessionDirectorTests.SessionResult_RecordsEveryRound_InteractionAndCompletion`.
- PlayMode `ClasicoPlayModeTests.Economy_*`: first perfect session (breakdown,
  wallet, no duplicate after replay/restart, variety deferred), 3rd session of
  the day (×0.60 line, no +25), aborted, all-passive. Every PlayMode fixture
  that completes sessions runs against an in-memory economy — tests never touch
  the real save.

## Known limitations / deferred

- Local, plaintext, unauthenticated save — trivially editable (acceptable for
  an off-chain prototype; server-authoritative rewards are the future fix).
- Local-clock based days/weeks can be gamed by changing the system clock.
- Variety bonus deferred (see above).
- Transaction history capped at 100; credited-id memory at 1000.
- An already-credited session offered again after an app restart shows only
  "Recompensa ya acreditada" (its breakdown isn't persisted).
- Deferred entirely: blockchain / smart contracts / key wallets, transfers,
  marketplace, store (so `LifetimeSpent` stays 0), Codex, seasons, Academy.
