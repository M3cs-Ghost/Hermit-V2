# C4 — Unity ↔ Supabase Technical Spike

**Status: COMPLETE. Gate: 10/10 PASS.**

Ratified in Phase C2.5 (ADR-009, Group B). Decision made and code written
this round, then manually verified end-to-end by the user in the Unity
Editor and in a standalone Windows build — see `Docs/C4_TEST_RESULTS.md` for
the full evidence log. All 10 gate criteria passed with real Supabase
network calls; no criterion remains PENDING.

## Objective

Prove Unity can talk to the existing Supabase backend end-to-end, on a real
Windows build, before any gameplay work starts.

## Decision: direct REST, not `supabase-csharp`

| Option | Verdict | Why |
|---|---|---|
| **A — `supabase-community/supabase-csharp`** (NuGet package `Supabase`, v1.4.0, last updated 2026-07-23) | Rejected for this spike | Actively maintained and full-featured, but: (1) pulls in Realtime + Storage sub-packages this spike doesn't use; (2) its own wiki documents real Unity-specific friction — requires the Unity-specific `com.unity.nuget.newtonsoft-json` package instead of a generic NuGet one, Managed Stripping must be set to Minimal to avoid IL2CPP breaking JSON reflection, and there are community reports of duplicate-DLL conflicts needing a `link.xml`; (3) most importantly — this session has no way to open the Unity Editor, resolve NuGet-in-Unity, or iteratively fix a compile error. Installing something I cannot verify compiles is a worse bet than a small amount of hand-written REST code I can review line by line. |
| **B — `kamyker/supabase-unity`** | Rejected | Still alpha, single maintainer, no material update found since ~April 2025. Reference only, never a real plan A. |
| **C — Direct REST** (`UnityWebRequest` + Unity's built-in `JsonUtility`) | **Chosen** | Zero new package dependencies — `JsonUtility` already ships with Unity (`com.unity.modules.jsonserialize`, already in `manifest.json`). The REST surface needed (GoTrue token endpoint, PostgREST table reads, one RPC) is small and was already fully documented during Phase C's backend audit. Every line is inspectable; nothing depends on a third party's install story working. |

**No new packages were added to `Packages/manifest.json` this round.**

## What was implemented (`Hermit.Networking` + `Hermit.Runtime`)

- `SupabaseAuthService : IAuthService` — `POST /auth/v1/token?grant_type=password`
  (login), `?grant_type=refresh_token` (refresh), `POST /auth/v1/logout`.
- `SupabaseRestBackendClient : IBackendClient` — `GET /rest/v1/{path}` (table
  reads) and `POST /rest/v1/rpc/{name}` (RPC calls), both with
  `apikey` + `Authorization: Bearer {token}` headers.
- `LocalFileSessionStore : ISessionStore` — plaintext JSON file under
  `Application.persistentDataPath`. **Not secure storage** (see Security
  section) — a deliberate, documented simplification for the spike.
- `UnityConnectivityService : IConnectivityService` — starts from
  `Application.internetReachability`, corrected by real request outcomes via
  `ReportBackendReachable(bool)` (called by both Supabase* services after every
  request). Device-online is never treated as proof Supabase is reachable.
- `HermitAppContext` (`Hermit.Runtime`) — the composition root. Owns the
  current `HermitSession`, wires the four concrete services by hand
  (constructor injection, no framework), exposes `LoginAsync` /
  `RefreshAsync` / `LogoutAsync` / `LoadProfileAsync` / `LoadWalletAsync` /
  `CallSampleRpcAsync`, all returning `HermitResult<string>` so callers never
  see a raw exception.
- `HermitRuntimeInstaller` — `[RuntimeInitializeOnLoadMethod]`, spawns
  everything with zero scene wiring (see `ARCHITECTURE.md`).
- `C4DebugPanel` — IMGUI-based diagnostic panel, `Hermit.Runtime` only,
  clearly out of `Hermit.UI`'s scope (see `ARCHITECTURE.md`).

## Interface contracts refined during the spike

`IAuthService`, `IBackendClient`, `IConnectivityService` all changed shape from
their C3 draft once a real implementation existed to write against — each
change and its reason is documented in the interface file itself, not
repeated here. `ISessionStore` needed no changes.

## Endpoints used (all read-only or session-scoped, none mutate game state)

| Purpose | Method | Path |
|---|---|---|
| Login | `POST` | `/auth/v1/token?grant_type=password` |
| Refresh | `POST` | `/auth/v1/token?grant_type=refresh_token` |
| Logout | `POST` | `/auth/v1/logout` |
| Profile | `GET` | `/rest/v1/profiles?id=eq.{userId}&select=id,display_name,theme` |
| Wallet | `GET` | `/rest/v1/wallets?user_id=eq.{userId}&select=user_id,balance` |
| RPC | `POST` | `/rest/v1/rpc/get_my_rank` — body `{"p_game_mode":"leyenda_arcade"}` |

`get_my_rank` was chosen per the user's own suggestion: read-only, already
exists, never touches `award_coins`/`submit_score` (explicitly forbidden this
round). **Verified against a live response** — manual test returned
`has_score = true`, `best_score = 1013`, `global_rank = 1`. See
`C4_TEST_RESULTS.md`.

## Environment configuration

`Assets/Hermit/Data/Resources/EnvironmentConfig_Development.asset` exists,
pointed at `https://pgmwxbtnwpggyxipobif.supabase.co` (the same project V1
uses — confirmed by reading, not modifying, V1's `supabase-client.js`).
`project_id` matches exactly what was given for this round; the old
`phclhwuvksxfyqaoorpv` project was never referenced.

**The anon key field is filled in.** It was left blank by this session (an
automated safety check refused to write a JWT-shaped string to a file — it
can't tell a public anon key from a real secret just by shape), and the user
subsequently pasted the real `SUPABASE_ANON_KEY` value from V1's
`supabase-client.js` into the `Supabase Anon Key` field via the Unity
Inspector, as a manual step. Decoding the JWT payload confirms `"role":
"anon"` — this is the public anon key, the only Supabase credential this
client is meant to carry (see Security audit below), not a secret leak.

## Security audit (see also C4_TEST_RESULTS.md)

- No `service_role` key anywhere in this repo — confirmed by grep across every
  file touched this round, re-confirmed at closeout.
- No real password anywhere in code, docs, or assets — the debug panel's
  password field is runtime-only (`GUILayout.PasswordField`, masked, never
  serialized). No hardcoded test-account password was found in the diff.
- The only credential now present in the repo is the Supabase **anon
  (public) key**, in `EnvironmentConfig_Development.asset`. This is the sole
  Supabase credential this client is permitted to carry — decoded payload
  confirms `"role": "anon"`. No `service_role` key, no access token, and no
  refresh token appear anywhere in the tracked diff.
- `HermitLog` receives technical error detail; `HermitResult.UserMessage` is
  the only thing ever shown to a human — errors never leak a raw exception,
  stack trace, or Postgres error code to the debug panel. Confirmed live:
  an invalid login returned the controlled `invalid_credentials` message,
  not a raw exception.
- RLS remains the real security boundary, not the anon key's secrecy — same
  model Supabase documents and V1 already relies on.

## Constraints carried over (not renegotiated)

- Purchases/spends still require a live connection — no optimistic spending.
- Every write needs a client-generated `eventId` for idempotency — not
  exercised this round since no mutating RPC was called.
- No migrations, no RLS changes, no grant changes — none were needed or made.

## Closeout

C4 is **COMPLETE**. All 10 gate criteria are **PASS**, verified manually by
the user against the live Supabase backend in both the Unity Editor and a
standalone Windows Development Build. See `Docs/C4_TEST_RESULTS.md` for the
full evidence log.

- `Assets/Hermit/Core/HermitBootstrap.cs` (the superseded C3 file, dead since
  its scene `GameObject` was removed and `Hermit.Runtime.HermitBootstrap`
  took over) has been deleted along with its `.meta`.
- The `WalletRow.balance` type risk (JSON number vs. string) is resolved:
  the live Wallet call returned `1.5` and deserialized correctly into the
  `double` field as written — no code change was needed.
- No new packages were added to `Packages/manifest.json` this round (still
  true at closeout).
- No V1 files, no migrations, and no Supabase schema/RLS/grant changes were
  made or are needed to close this phase.
