# Hermit V2 — Architecture (repo-local reference)

This is a short on-repo reference, not a replacement for the Phase C / C2 / C2.5
documents (audit, Blueprint, ADR ratification). Read those for the *why*; this
file only tracks what actually exists in this repo *today*.

## Client / backend split

- **Unity 6.3 LTS (URP 2D)** is the client. It owns UI, scenes, gameplay, input,
  audio, local cache, and the future Game Framework.
- **Supabase** (Postgres + Auth + RPC + RLS) is the backend, unchanged from V1.
  Schema, migrations, RPCs and the 1,300+ line test suite live in the V1 repo
  and are never duplicated here — this repo only ever gets a *client* to them.

## Boundaries (non-negotiable, ADR-002 / ADR-006 / ADR-018)

- This client never talks to Postgres directly — only through RPCs the backend
  already exposes (or new ones added additively, per ADR-016).
- A `service_role` key must never exist in this repository. The `anon` key is
  not a secret (see `EnvironmentConfig` doc-comment) and is fine to version.
- `Hermit.Games` never references `Hermit.Networking` or `Hermit.UI` directly —
  only whatever contracts `Hermit.Core`'s Game Framework exposes once it exists.
- V1 (`contabilidad_app_backup_2025-08-31 - Copy (2) - Copy`) is a separate
  repo and is never touched from here.

## Assemblies

| Assembly | Folder | References | Purpose |
|---|---|---|---|
| `Hermit.Core` | `Assets/Hermit/Core/` | — | Pure data/utility: logging, environment config, `HermitError`/`HermitResult<T>`. Zero dependencies, on purpose — future home of the domain-agnostic Game Framework. |
| `Hermit.Networking` | `Assets/Hermit/Networking/` | Core | Auth/backend/session/connectivity contracts **and** their concrete REST implementations (`Supabase*Service`), added in C4. |
| `Hermit.Runtime` | `Assets/Hermit/Runtime/` | Core, Networking | **Composition root.** The one assembly allowed to construct concrete Networking services and wire them together. Holds `HermitBootstrap`, `HermitAppContext`, the auto-installer, and the C4 debug panel. Resolves the "what is `Runtime/` for" question left open after C3. |
| `Hermit.UI` | `Assets/Hermit/UI/` | Core | Reserved. Still empty — the C4 debug panel is deliberately **not** here (see below). |
| `Hermit.Games` | `Assets/Hermit/Games/` | Core | Reserved. One assembly for *all* minigames (not one per game). |
| `Hermit.Editor` | `Assets/Hermit/Editor/` | Core | Editor-only tooling. Empty today. |
| `Hermit.Tests.EditMode` | `Assets/Hermit/Tests/EditMode/` | Core, Networking | Pure-logic tests, Editor platform only. |
| `Hermit.Tests.PlayMode` | `Assets/Hermit/Tests/PlayMode/` | Runtime | Tests that need the runtime loop (Bootstrap lives in Runtime now, not Core). |

No circular references. `Hermit.Core` still has zero dependencies. Dependency
direction: `Core ← Networking ← Runtime`; `UI`/`Games`/`Editor` depend only on
`Core` and currently contain nothing.

**Why the debug panel lives in `Hermit.Runtime`, not `Hermit.UI`:** it needs to
call `HermitAppContext` directly (Login/Refresh/Logout/etc.), and `Hermit.UI`
must stay decoupled from Networking per the client/backend boundary above. The
panel is explicitly spike-only tooling, not product UI — putting it in the
composition-root assembly keeps that boundary honest instead of quietly
bending it "just for the debug panel."

## Namespaces

`Hermit.Core`, `Hermit.Networking`, `Hermit.Runtime`, `Hermit.UI`,
`Hermit.Games`, `Hermit.Editor`, `Hermit.Tests.EditMode`,
`Hermit.Tests.PlayMode` — one namespace per assembly, matching the folder 1:1.

## Environments

Three environments are modeled (`HermitEnvironment` enum) and one data type
(`EnvironmentConfig`, a `ScriptableObject`, now also exposing `IsConfigured`).
A **Development** instance exists at
`Assets/Hermit/Data/Resources/EnvironmentConfig_Development.asset`, pointed at
the same Supabase project V1 already uses
(`https://pgmwxbtnwpggyxipobif.supabase.co`). Its `_supabaseAnonKey` field is
**intentionally blank** — an automated safety check refused to let this
session write a JWT-shaped string to a file, so filling it in is a manual
step: open the asset in the Inspector and paste the anon key from V1's
`supabase-client.js` (`SUPABASE_ANON_KEY` constant). No Staging/Production
instances exist — not needed yet.

The asset lives under a `Resources/` folder specifically so
`HermitRuntimeInstaller` can `Resources.Load<EnvironmentConfig>(...)` it at
startup with zero scene wiring — nothing needs to be manually dragged onto any
GameObject.

## Boot flow (implemented in C4)

`HermitRuntimeInstaller.Install()` runs automatically via
`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` — no component needs to be
placed in `00_Bootstrap` (or any scene) by hand. It spawns one
`DontDestroyOnLoad` GameObject with `HermitBootstrap`, loads
`EnvironmentConfig_Development` from `Resources/`, constructs one
`HermitAppContext`, calls `RestoreSessionAsync()`, and — only when the loaded
config's environment is `Development` — attaches `C4DebugPanel`. A
Staging/Production build (once those configs exist) never shows the panel.

## Folders not yet in use

`Content/`, `Data/` (partially used now, see Environments above), `Text/`,
`Art/`, `Audio/` exist but are still empty/unused. `Runtime/` is now resolved
— see the Assemblies table.

## Known housekeeping item

Resolved at C4 closeout: the old C3 `Assets/Hermit/Core/HermitBootstrap.cs`
(superseded by `Hermit.Runtime.HermitBootstrap`) was confirmed to have zero
remaining references — its `GameObject` was already removed from
`00_Bootstrap.unity`, and no scene, prefab, or code pointed at its GUID or
type — and has been deleted along with its `.meta` file.

## C4 objective

Unity ↔ Supabase Technical Spike. Exit criteria, decision, and current
PASS/FAIL state: see `Docs/C4_SUPABASE_SPIKE.md` and `Docs/C4_TEST_RESULTS.md`.
