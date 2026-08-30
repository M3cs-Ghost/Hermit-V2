# C4 — Unity ↔ Supabase Technical Spike

Ratified in Phase C2.5 (ADR-009, Group B). Restated here so the criteria live
next to the code they judge, not only in an external doc. **Nothing below is
implemented in this repo yet.**

## Objective

Prove Unity can talk to the existing Supabase backend end-to-end, on a real
Windows build, before any gameplay work starts.

## Exit criteria (all 10 must pass)

1. **Login** — a test user authenticates with email/password from Unity and
   receives a valid session.
2. **Refresh** — the access token renews in the background before expiry,
   without user action (verified by leaving the app open past the token's
   lifetime).
3. **Logout** — clears tokens + cached remote state, without touching another
   user's queued offline actions.
4. **Profile** — reads real `display_name`/avatar/theme from `profiles` after
   login.
5. **Wallet** — reads real `wallets.balance` and reflects it in UI.
6. **One RPC** — at least one real round trip (e.g. `purchase_item` or
   `equip_avatar` against test data, **never production data**) confirms Unity
   can invoke an RPC and read its response.
7. **Error handling** — a network error and a business error (e.g.
   insufficient funds) both show friendly copy — never a raw stack trace or
   Postgres error code.
8. **Session restore** — closing and reopening the app restores the session
   without asking for login again.
9. **Connectivity loss/recovery** — losing network mid-flow does not log the
   user out; reconnecting reconciles state automatically.
10. **Windows build** — the spike runs as a standalone build, not only in the
    Editor.

## Integration shortlist (the spike decides, not this document)

| # | Option | Notes |
|---|---|---|
| 1 | `supabase-community/supabase-csharp` (NuGet, "Supabase") | Most used/documented; needs a NuGet→Unity bridge (e.g. NuGetForUnity); verify its session/refresh model fits Unity's process lifecycle, not just a long-lived server process. |
| 2 | `kamyker/supabase-unity` | Unity-packaged, dependency resolution already done — but **alpha**, single maintainer. Reference only, not plan A. |
| 3 | Direct REST (`UnityWebRequest` + Newtonsoft/`System.Text.Json` against PostgREST + GoTrue) | Zero third-party dependency risk; more code to write by hand, but the 7-8 RPCs involved are already documented in the V1 repo's migrations. Fallback if Option 1 has real friction. |

Recommended order: try **Option 1** first; fall back to **Option 3** if NuGet-in-Unity
friction or session-lifecycle mismatches show up. Option 2 stays a reference.

## What this repo already has ready

- `Hermit.Networking`: `IAuthService`, `IBackendClient`, `ISessionStore`,
  `IConnectivityService` — contracts only, zero implementation.
- `Hermit.Core`: `EnvironmentConfig` (ScriptableObject class, no instances
  yet), `HermitEnvironment` enum, `HermitLog`.
- `HermitSession` data struct (`Hermit.Networking`).

## What C4 must still do

- Pick the SDK (or confirm direct REST) via the spike above.
- Create `EnvironmentConfig_Dev.asset` in the Editor, pointed at the **same**
  Supabase project V1 already uses (same URL/anon key already public in V1's
  `supabase-client.js`) — no new Supabase project.
- Implement concrete classes for the 4 interfaces above.
- Wire them into `HermitBootstrap` (or a successor) — still by hand, no DI
  framework (ADR-003 already rejected Zenject/Extenject for this).

## Constraints carried over from C2.5 (not renegotiated here)

- No `service_role` key, ever, anywhere in this repo.
- Purchases/spends still require a live connection — no optimistic spending
  (same law as V1's `sync/coins-sync.js`).
- Every write needs a client-generated `eventId` for idempotency, matching the
  server's existing unique-constraint pattern.
