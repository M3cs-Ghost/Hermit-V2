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
| `Hermit.Core` | `Assets/Hermit/Core/` | — | Bootstrap, logging, environment config. Future home of the Game Framework (GameDefinition/Session/Context/Result/Manager/FlowController). |
| `Hermit.Networking` | `Assets/Hermit/Networking/` | Core | Auth/backend/session/connectivity **contracts only**. No implementation until C4. |
| `Hermit.UI` | `Assets/Hermit/UI/` | Core | Reserved. Empty until real UI work starts. |
| `Hermit.Games` | `Assets/Hermit/Games/` | Core | Reserved. One assembly for *all* minigames (not one per game) — see ADR-003 note on assembly count. |
| `Hermit.Editor` | `Assets/Hermit/Editor/` | Core | Editor-only tooling (content validators, custom inspectors). Empty today. |
| `Hermit.Tests.EditMode` | `Assets/Hermit/Tests/EditMode/` | Core, Networking | Pure-logic tests, Editor platform only. |
| `Hermit.Tests.PlayMode` | `Assets/Hermit/Tests/PlayMode/` | Core | Tests that need the runtime loop (e.g. Bootstrap). |

No circular references. Only `Hermit.Core` has zero dependencies; everything
else points at it, never the other way around.

## Namespaces

`Hermit.Core`, `Hermit.Networking`, `Hermit.UI`, `Hermit.Games`, `Hermit.Editor`,
`Hermit.Tests.EditMode`, `Hermit.Tests.PlayMode` — one namespace per assembly,
matching the folder 1:1.

## Environments

Three environments are modeled (`HermitEnvironment` enum: Development / Staging
/ Production) and one data type (`EnvironmentConfig`, a `ScriptableObject`) to
hold `supabaseUrl` / `supabaseAnonKey` per environment. **No `.asset` instances
exist yet** — creating `EnvironmentConfig_Dev.asset` (pointed at the same
Supabase project V1 already uses) is a manual Editor step for C4, not done in
this repo yet.

## Scenes

`00_Bootstrap` (build index 0, never unloaded) → `01_Shell` → `02_GameplaySandbox`.
`00_Bootstrap` currently holds nothing beyond confirming `Hermit.Core` loads
(see `HermitBootstrap`) — no additive-scene loading, no persistent Shell logic
yet. That wiring belongs to the Game Framework work, not C3.

## Folders not yet in use

`Content/`, `Data/`, `Text/`, `Art/`, `Audio/`, and `Runtime/` exist (created
during manual setup) but are empty and unused as of this commit. `Runtime/` in
particular has no assigned purpose yet — flagged for a decision in C4/C5
(fold into an existing assembly, repurpose, or remove) rather than guessed at
here.

## C4 objective

Unity ↔ Supabase Technical Spike. Exit criteria, SDK shortlist, and what this
repo already has ready for it: see `Docs/C4_SUPABASE_SPIKE.md`.
