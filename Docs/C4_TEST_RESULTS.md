# C4 — Test Results

**Status: COMPLETE. Gate: 10/10 PASS.**

This file previously recorded an honest 0/10 PENDING state, because the
agent authoring the code could not open the Unity Editor, type into the
debug panel, or run a build. That manual verification has since been
performed by the user, against the live Supabase backend, in both the Unity
Editor (Play mode) and a standalone Windows Development Build run outside
the Editor. This file now records the real, user-confirmed outcome.

## Gate — PASS/FAIL

| # | Criterion | Status | Evidence |
|---|---|---|---|
| 1 | Login | **PASS** | Invalid credentials returned the controlled `invalid_credentials` error (no stack trace, no raw exception). Valid credentials left `Auth state: authenticated` and returned a User ID. |
| 2 | Refresh | **PASS** | Refresh kept the session `authenticated` and produced a new expiration timestamp. |
| 3 | Logout | **PASS** | Logout cleared local session state; stopping and re-entering Play mode did not restore authentication. |
| 4 | Profile | **PASS** | Profile load returned `Display name = Marvin`. |
| 5 | Wallet | **PASS** | Wallet load returned `balance = 1.5`. Confirms `WalletRow.balance` (C# `double`) correctly deserializes PostgREST's `numeric` column as a JSON number — the type-assumption risk noted below is resolved. |
| 6 | One RPC | **PASS** | `get_my_rank` returned `has_score = true`, `best_score = 1013`, `global_rank = 1`. |
| 7 | Error handling | **PASS** | Invalid login was mapped to a friendly `invalid_credentials` message, never a raw exception or Postgres error code. |
| 8 | Session restore | **PASS** | Stopping Play mode and re-entering it left `Auth state: authenticated` without re-entering credentials — the local session file + restore-on-boot path (`HermitAppContext.RestoreSessionAsync` / `SessionExpiry.IsStillValid`) works end-to-end, not just in EditMode unit tests. |
| 9 | Connectivity loss/recovery | **PASS** | Disabling network produced a controlled failure (not a hang or crash); re-enabling network and retrying succeeded, with connectivity state flipping back to reachable. |
| 10 | Windows build | **PASS** | Windows Development Build (x86_64) run outside the Editor. Login, Profile, Wallet, RPC, session restore, and Logout were all re-verified against the standalone `.exe`. |

**Gate state: 10/10 PASS. C4 objective (Unity ↔ Supabase end-to-end, on a
real Windows build) is met.**

## Manual evidence log (user-confirmed)

- Login (invalid): returned `invalid_credentials`, handled in a controlled
  way — no stack trace, no raw exception surfaced.
- Login (valid): left state `authenticated`, returned a User ID.
- Profile: `Display name = Marvin`.
- Wallet: `balance = 1.5`.
- RPC `get_my_rank`: `has_score = true`, `best_score = 1013`,
  `global_rank = 1`.
- Refresh: session stayed `authenticated`, new expiration issued.
- Session restore: stopping and restarting Play mode preserved the
  authenticated state without re-entering credentials.
- Connectivity loss/recovery: loss of connection produced a controlled
  failure; reconnecting recovered correctly.
- Logout: session cleared; restarting did not silently re-authenticate.
- Windows build: Windows Development Build x86_64, run outside the Editor —
  login, profile, wallet, RPC, session restore, and logout all re-verified
  against the standalone executable.

## What was run by the coding agent (prior to manual verification)

- **JSON validity** of all `.asmdef` files — verified with `node -e
  "JSON.parse(...)"`, all pass.
- **`git status` / secret scan** — confirmed no `service_role` key, no real
  password, no JWT committed at the time (the anon key field was still
  blank).
- **V1 repo** — confirmed untouched.
- **Static review** of every new file for namespace/using correctness and
  asmdef reference consistency.

## Manual test procedure (as executed)

1. Filled in the anon key in
   `Assets/Hermit/Data/Resources/EnvironmentConfig_Development.asset` from
   V1's `supabase-client.js`.
2. Ran EditMode and PlayMode test suites.
3. Entered Play mode; the `C4 Debug / Temporary` panel appeared
   automatically (no scene wiring needed).
4. Ran Login (invalid, then valid), Load Profile, Load Wallet, RPC, Refresh,
   Session restore (stop/re-enter Play), Connectivity loss/recovery
   (network toggled off/on), and Logout — all as documented in the gate
   table above.
5. Built a Windows Development Build (x86_64), ran the produced `.exe`
   outside the Editor, and repeated Login, Profile, Wallet, RPC, session
   restore, and Logout against it.

## Known risks / open items (post-closeout)

- **`LocalFileSessionStore` is not secure storage.** Documented in the file
  itself and in `ARCHITECTURE.md` — plaintext JSON on disk, fine for a
  spike, not fine to ship. Real Keychain/Keystore/DPAPI storage is a
  separate, later task, not part of C4's scope.
- **`JsonArrayWrapper<T>` generic + `JsonUtility`** was exercised
  successfully by the Wallet/Profile calls in this round (both are
  single-row table reads returning a JSON array wrapped and parsed via this
  trick) — no further risk noted here.

## Resolved at closeout

- **`WalletRow.balance` type assumption** — resolved. Live Wallet call
  returned `1.5`, correctly deserialized by `JsonUtility` into the `double`
  field with no code change required.
- **`Assets/Hermit/Core/HermitBootstrap.cs` (old C3 file)** — resolved.
  Confirmed to have zero remaining references (no scene, prefab, or code
  pointed at its GUID or type after the `HermitBootstrap` `GameObject` was
  removed from `00_Bootstrap.unity`), then deleted along with its `.meta`.
- **No live network call had ever been made** — resolved. All 10 gate
  criteria above were exercised against the live Supabase backend by the
  user, in both the Editor and a standalone Windows build.
