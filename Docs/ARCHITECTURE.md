# Hermit V2 — Architecture (repo-local reference)

This is a short on-repo reference, not a replacement for the Phase C / C2 / C2.5
documents (audit, Blueprint, ADR ratification). Read those for the *why*; this
file only tracks what actually exists in this repo *today*.

## Client / backend split

- **Unity 6.3 LTS (URP 2D)** is the client. It owns UI, scenes, gameplay, input,
  audio, local cache, and the Game Framework (`Hermit.Games`, added in C5).
- **Supabase** (Postgres + Auth + RPC + RLS) is the backend, unchanged from V1.
  Schema, migrations, RPCs and the 1,300+ line test suite live in the V1 repo
  and are never duplicated here — this repo only ever gets a *client* to them.

## Boundaries (non-negotiable, ADR-002 / ADR-006 / ADR-018)

- This client never talks to Postgres directly — only through RPCs the backend
  already exposes (or new ones added additively, per ADR-016).
- A `service_role` key must never exist in this repository. The `anon` key is
  not a secret (see `EnvironmentConfig` doc-comment) and is fine to version.
- `Hermit.Games` (the Game Framework + all game implementations, added in C5)
  never references `Hermit.Networking` or `Hermit.UI` directly. A game's only
  external dependencies are what `GameContext` exposes — analytics + RNG,
  never a Supabase/HTTP client. See `Docs/C5_GAME_FRAMEWORK.md`.
- V1 (`contabilidad_app_backup_2025-08-31 - Copy (2) - Copy`) is a separate
  repo and is never touched from here.

## Assemblies

| Assembly | Folder | References | Purpose |
|---|---|---|---|
| `Hermit.Core` | `Assets/Hermit/Core/` | — | Pure data/utility: logging, environment config, `HermitError`/`HermitResult<T>`. Zero dependencies, on purpose. (C2 once expected the Game Framework to live here; it ended up in `Hermit.Games` instead — see that row.) |
| `Hermit.Networking` | `Assets/Hermit/Networking/` | Core | Auth/backend/session/connectivity contracts **and** their concrete REST implementations (`Supabase*Service`), added in C4. |
| `Hermit.Runtime` | `Assets/Hermit/Runtime/` | Core, Networking, Games | **Composition root.** The one assembly allowed to construct concrete Networking services and games, and wire them together. Holds `HermitBootstrap`, `HermitAppContext`, the auto-installer, the C4 debug panel, and (added in C5) the Clásico vertical-slice UI (`GameFramework/`). |
| `Hermit.UI` | `Assets/Hermit/UI/` | Core | Reserved. Still empty — both the C4 debug panel and C5's Clásico screen are deliberately **not** here (see below and `Docs/C5_GAME_FRAMEWORK.md`). |
| `Hermit.Games` | `Assets/Hermit/Games/` | Core | **The Game Framework + all game implementations**, added in C5 (`GameDefinition`/`GameSession`/`GameContext`/`GameResult`/`IGameEngine`/`GameFlowController`/`GameRegistry`, `Content/`, `Analytics/`, `Clasico/`). Never references Networking or UI. See `Docs/C5_GAME_FRAMEWORK.md`. |
| `Hermit.Editor` | `Assets/Hermit/Editor/` | Core | Editor-only tooling. Empty today. |
| `Hermit.Tests.EditMode` | `Assets/Hermit/Tests/EditMode/` | Core, Networking, Games | Pure-logic tests, Editor platform only. |
| `Hermit.Tests.PlayMode` | `Assets/Hermit/Tests/PlayMode/` | Runtime, Games | Tests that need the runtime loop (Bootstrap lives in Runtime now, not Core). |

No circular references. `Hermit.Core` still has zero dependencies. Dependency
direction: `Core ← Networking ← Runtime` and `Core ← Games ← Runtime`, as two
parallel branches that never touch each other except inside `Runtime`;
`UI`/`Editor` depend only on `Core` and currently contain nothing.

**Why the debug panel lives in `Hermit.Runtime`, not `Hermit.UI`:** it needs to
call `HermitAppContext` directly (Login/Refresh/Logout/etc.), and `Hermit.UI`
must stay decoupled from Networking per the client/backend boundary above. The
panel is explicitly spike-only tooling, not product UI — putting it in the
composition-root assembly keeps that boundary honest instead of quietly
bending it "just for the debug panel."

**Asmdef gotcha found in C5 (applies to any assembly using uGUI + the New
Input System's UI module):** `com.unity.ugui`'s `UnityEngine.UI` assembly is
genuinely auto-referenced, but `com.unity.inputsystem`'s `Unity.InputSystem`
assembly was **not** picked up automatically for a custom, non-predefined
asmdef in this project despite also being marked `autoReferenced: true` in
its own package asmdef — any assembly whose code touches
`UnityEngine.InputSystem.UI.InputSystemUIInputModule` (or other Input System
types) needs an explicit reference. Both `Hermit.Runtime.asmdef` and
`Hermit.Tests.PlayMode.asmdef` now carry
`"GUID:75469ad4d38634e559750d17036d5f7c"` for exactly this reason (verified
against `Unity.InputSystem.asmdef.meta` in the installed package, not
guessed).

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

**This hook is scene-agnostic, confirmed in practice, not just in theory:**
`RuntimeInitializeOnLoadMethod(AfterSceneLoad)` fires once after whichever
scene loads first, regardless of which one that is — `00_Bootstrap` is not
special-cased anywhere in this path. C5's manual validation built with only
`02_GameplaySandbox` enabled and the C4 panel still started correctly. See
`Docs/C5_GAME_FRAMEWORK.md`, "Runtime bootstrap" for the full writeup
(including what `00_Bootstrap.unity` actually does today — nothing
functional; it's an empty placeholder scene).

## Folders not yet in use

`Text/`, `Art/`, `Audio/` exist but are still empty/unused. `Runtime/` is
resolved (see the Assemblies table). `Content/` and `Data/` are both now in
use — see Environments above and `Docs/C5_GAME_FRAMEWORK.md` ("Content
separation") for `Content/Resources/C5SampleQuestions.asset`.

## C5 objective

Game Framework + vertical slice (Clásico) — **COMPLETE**, 12/12 gates PASS.
Contracts, lifecycle, content model, scoring, extensibility proof, the
keyboard-navigation fix, and the full manual validation log: see
`Docs/C5_GAME_FRAMEWORK.md`.

## Known housekeeping item

Resolved at C4 closeout: the old C3 `Assets/Hermit/Core/HermitBootstrap.cs`
(superseded by `Hermit.Runtime.HermitBootstrap`) was confirmed to have zero
remaining references — its `GameObject` was already removed from
`00_Bootstrap.unity`, and no scene, prefab, or code pointed at its GUID or
type — and has been deleted along with its `.meta` file.

## C4 objective

Unity ↔ Supabase Technical Spike. Exit criteria, decision, and current
PASS/FAIL state: see `Docs/C4_SUPABASE_SPIKE.md` and `Docs/C4_TEST_RESULTS.md`.
