# C6 — Game Registry + Selector + Content Pipeline Foundation

**Status: COMPLETE.** All 16 gates below are PASS, manually verified by the
user in the Unity Editor and in a standalone Windows Development Build
(including a second build confirming the Bootstrap scene flow fix — see
"Bootstrap scene flow"). See "Manual validation" for the full evidence log.

## Objective

Turn C5's Game Framework — validated with exactly one game, wired directly
into a Clásico-specific installer — into a platform that can list, select,
and launch multiple games without touching central code to add one. Also lay
the minimum content-pipeline groundwork (stable IDs, versioning, a
validator) the future Knowledge Engine will need, without building that
engine now.

## Audit findings (before implementing)

- Clásico registered itself via `ClasicoVerticalSliceInstaller.Awake()`:
  `Resources.Load<ClasicoGameDefinition>("ClasicoGameDefinition")` (a fixed
  path naming the one game) then
  `_registry.Register(definition, () => _activeEngine = new ClasicoGameEngine())`
  (an externally-supplied factory closure naming the one engine type).
- `GameRegistry` was dictionary-only: no guaranteed enumeration order, no
  enabled/disabled concept, no selector metadata beyond GameId/DisplayName.
- `ClasicoHud`'s "Idle" screen — a "Jugar" button — was C5's de facto
  selector, hardcoded to exactly one game.
- `ClasicoVerticalSliceInstaller` mixed composition-root responsibilities
  (registry, EventSystem, canvas) with Clásico-specific ones (Update loop
  casting to `ClasicoGameEngine`, wiring `ClasicoHud` directly) in one file.

All four are exactly what this phase needed to eliminate.

## Registry (final)

`GameRegistry` (`Hermit.Games`) now holds `GameDefinition` instances
directly — C5's separate `GameRegistration` wrapper (definition + externally
supplied `Func<IGameEngine>`) is gone. See "Registration" below for why.

```csharp
void Register(GameDefinition definition);       // throws on null/empty id/duplicate id
bool TryGet(string gameId, out GameDefinition);
IReadOnlyList<GameDefinition> All { get; }        // ordered, see below
int Count { get; }
```

- **Enumerable**: `All` returns every registered definition — enabled and
  disabled alike; the caller (the selector) decides how to present a
  disabled one.
- **Ordering**: `All` sorts by `GameDefinition.DisplaySortOrder` ascending,
  falling back to registration order for ties. Explicit and deterministic —
  no reliance on `Dictionary<>`'s enumeration order, which .NET never
  documents as stable.
- **Duplicate protection**: unchanged from C5 — `Register` throws
  `InvalidOperationException` on a repeated `GameId`.
- **Enabled/disabled**: `GameDefinition.IsEnabled` (new). A disabled game is
  still registered and still enumerated — it is not hidden, it is shown and
  marked unavailable (see "Selector"). This has real value: a game that's
  registered but unfinished can exist in the catalog without being
  playable, rather than needing to be commented out of the catalog to hide
  it.
- No reflection, no assembly scanning, no service locator, no switch — the
  registry is populated by one explicit loop in the composition root, from
  one catalog asset (see "Resources strategy").

## Registration model

`GameDefinition` (`Hermit.Games`) is the entire registration unit now — no
separate wrapper type:

```csharp
public abstract class GameDefinition : ScriptableObject
{
    public string GameId { get; }
    public string DisplayName { get; }
    public string ShortDescription { get; }   // new — optional selector flavor text
    public bool IsEnabled { get; }             // new
    public int DisplaySortOrder { get; }       // new
    public abstract IGameEngine CreateEngine(); // new
}
```

**Why `GameRegistration` was dropped.** C5 wrapped a definition with an
externally-supplied `Func<IGameEngine>` because nothing on the definition
itself could build its own engine. C6 makes `CreateEngine()` abstract on
`GameDefinition` instead — `ClasicoGameDefinition.CreateEngine() => new
ClasicoGameEngine();` is the entire override. Once the factory lives on the
definition, wrapping it a second time added a type without adding a
guarantee: the registry can hold `GameDefinition` directly, `GameFlowController.Start`
takes a `GameDefinition` directly, and the selector already only ever needed
the definition's public data. One fewer type, same behavior, and it is *why*
`GameRegistry.Register` no longer needs a factory parameter.

The selector never sees `ClasicoGameEngine`, `ClasicoGameDefinition`, or any
other concrete type — only `GameDefinition`'s public contract. This is
enforced by construction, not by convention: `GameSelectorHud` (see below)
has no reference anywhere in its file to any Clasico type, and
`GameSelectorPlayModeTests` builds and drives it with two entirely
unrelated fake games to prove it.

## Selector

`GameSelectorHud` (`Hermit.Runtime.GameFramework`) — one uGUI screen, built
once from `GameRegistry.All`:

- One button per registered game, labelled with `DisplayName` (+ "
  (próximamente)" appended when `!IsEnabled`).
- A disabled game's button is shown but `interactable = false`, and its
  click handler independently checks `IsEnabled` before firing
  `GameLaunchRequested` — so even a direct `onClick.Invoke()` (as tests do,
  bypassing Unity's own interactable gating) cannot launch a disabled game.
- An empty registry shows a "No hay juegos disponibles." message instead of
  an empty screen or a crash.
- Fires `event Action<GameDefinition> GameLaunchRequested` — the composition
  root is the only listener.

No hardcoded "Clásico" button anywhere — the button set is built entirely
from whatever `GameRegistry.All` returns.

## Flow

```
Selector (Idle) → pick a game → Preparing → Playing → Results
                                                   ↓ Restart        ↓ Exit
                                              Playing (fresh)   Selector (Idle)
```

Still zero scene loads — everything happens inside one loaded scene
(`02_GameplaySandbox`), same as C5. `GameFlowController`'s lifecycle
(`Idle → Preparing → Playing → Ending → Results → Idle`) is unchanged in
shape; what changed is *who* reacts to it:

- `GameSessionInstaller` (the composition root) subscribes to
  `StateChanged` **only** for `Idle` — that is its one job: when the active
  game returns to Idle (via `AcknowledgeResults`), hide the active game's
  host and show the selector again. Restart never touches Idle (it goes
  Results → Preparing → Playing directly, per C5's design), so Restart
  correctly never bounces back to the selector.
- `ClasicoGameHost` (see below) owns everything else about Clasico's own
  screen transitions (Playing/Results) directly, inline after calling
  `Start`/`Restart` — no event subscription needed for those, since they are
  synchronous consequences of a call the host itself just made.

## Where a specific game's presentation gets wired in

Games stay UI-free (`Hermit.Games` boundary, unchanged since C5), so
*something* in `Hermit.Runtime` has to know which concrete Hud to show for a
given `GameId`. That something is `IGamePresenterHost`
(`Hermit.Runtime.GameFramework`) plus one `Dictionary<string, IGamePresenterHost>`
in `GameSessionInstaller`:

```csharp
_hostsByGameId["clasico"] = new ClasicoGameHost(clasicoHud); // the one Clasico-specific line
...
_hostsByGameId.TryGetValue(definition.GameId, out var host); // dictionary lookup, not a switch
```

This is the "adapter específico de Clásico" the C6 brief explicitly allows
(section 20) — a dictionary entry, not an `if`/`switch` on `GameId`, and it
is the *only* place in `Hermit.Runtime` that mentions Clásico by name outside
`ClasicoHud`/`ClasicoGameHost` themselves. Adding a second real game means:
implement `IGamePresenterHost` once for it, add one more dictionary entry —
no edit to `GameSelectorHud`, `GameRegistry`, `GameFlowController`, or this
dictionary's surrounding logic.

`ClasicoGameHost` itself wraps exactly what C5's installer used to do
inline: start/restart the flow controller, cast `GameFlowController.CurrentEngine`
(new in C6 — see below) to `ClasicoGameEngine` to read its view state, push
that to `ClasicoHud`.

`GameFlowController.CurrentEngine` (new) exposes the active `IGameEngine` —
generic on purpose. A presenter that needs game-specific state casts to the
concrete type it already knows it is hosting, same pattern C5 used via an
external closure capture; C6 just makes it a first-class property instead of
a workaround.

## Resources strategy

Evaluated in the order the brief asked for:

- **(A) Explicit ScriptableObject registry — chosen.** `GameCatalog`
  (`Hermit.Games`) is one ScriptableObject holding `GameDefinition[]`. One
  instance ships at `Assets/Hermit/Data/Resources/GameCatalog.asset`,
  referencing `ClasicoGameDefinition.asset`.
- **(C) Resources only as a single-root bootstrap — also true here, by
  construction.** `GameSessionInstaller` calls `Resources.Load<GameCatalog>("GameCatalog")`
  exactly once, at startup. There is no longer a `Resources.Load` naming any
  individual game — C5's `Resources.Load<ClasicoGameDefinition>("ClasicoGameDefinition")`
  is gone entirely (grepped and confirmed absent from the codebase). (A) and
  (C) are not actually competing options — (A) is the *what* (a
  ScriptableObject holds the list), (C) is the *how it's found* (one
  `Resources.Load` for that one asset) — C6 uses both together.
- **(B) Runtime composition explicit with serialized assets** was the
  fallback if a Resources-loaded root proved awkward; it did not, so it
  wasn't needed. `GameCatalog` already *is* a serialized-assets list —
  option A subsumes it here.
- **(D) / Addressables**: explicitly out of scope this phase per the brief;
  not evaluated further.

Adding a game to the selector, end to end, is now: implement its
`GameDefinition` subclass and `IGameEngine` (code, unavoidable — that is the
game itself), create one concrete definition asset, and drag it into
`GameCatalog.asset`'s list in the Inspector. Zero changes to
`GameSessionInstaller`, `GameRegistry`, or `GameSelectorHud` for the
registration/selection side (a *playable* second game would still need its
own `IGamePresenterHost` + one dictionary line, per the section above — that
part is presentation, not registration, and is the one seam this phase
deliberately leaves manual rather than over-generalizing for a single real
consumer).

## Content model

`IContentProvider`/`QuestionSetContentProvider` are unchanged from C5 — an
engine never touches a `QuestionSet` asset directly. What's new sits on the
data types themselves:

- **`QuestionDefinition`** — `Id`/`Domain`/`Topic`/`Difficulty`/`Tags`/
  `PromptText`/`Options` unchanged from C5. Added: `ContentVersion` (int,
  default 1), `SourceId`/`SourceLabel` (strings, optional), `ConfusableWith`
  (string, optional).
- **`QuestionSet`** — added `SchemaVersion` (int, default 1): the
  *structural* shape version of the collection, deliberately separate from
  each question's own `ContentVersion` (that question's own wording
  revision). Bump `SchemaVersion` if `QuestionDefinition`'s fields change
  shape in a way old serialized data can't be read against; bump a single
  question's `ContentVersion` when its wording/answers change.
- **`GameResult`** — added `ContentSetId`/`ContentSchemaVersion` (default
  `""`/`0` for a game with no content-set concept), populated by
  `ClasicoGameEngine.BuildResult` from the active `QuestionSet`.
- **Analytics** — `QuestionPresented`/`AnswerSubmitted` now also carry the
  presented question's `ContentVersion`.

None of this touches Supabase, coins, or rewards — it is purely local,
in-memory metadata carried alongside the existing content/session/result
types.

## Content IDs

`QuestionDefinition.Id` (already existed, unchanged) is the stable identity
— author-assigned, survives reorder and text edits, never an array index or
the prompt text itself. `ContentVersion`, analytics events, and any future
mastery tracking key off this `Id`, never off position in the array.

`Topic` (already existed) already functions as a stable topic id in
practice (e.g. `"activo"`, `"debito_credito"`) — a free-form string, not a
display label. A real Topic taxonomy (its own asset/registry, translations,
hierarchy) is Knowledge Engine work, explicitly out of scope here; C6 does
not invent a second, formal Topic type alongside the existing string field.

`GameId` (already existed) is the stable identity for a game.

`SetId` (already existed) is the stable identity for a content set.

## Versioning

Two independent counters, deliberately not unified into one "version"
concept:

- `QuestionDefinition.ContentVersion` — this question's own wording/answers
  revision. Bump it by hand when `PromptText` or `Options` change in a way
  that matters (a typo fix does not need a bump; changing which answer is
  correct does).
- `QuestionSet.SchemaVersion` — the collection's structural shape. Only
  needs to change if `QuestionDefinition`'s field set changes shape.

No migration framework was built — the ask was "know which version of a
question a student saw", which `GameResult.ContentSchemaVersion` +
per-question `ContentVersion` (visible in analytics events) already answer.
Actually reacting to a version mismatch (e.g. re-scoring old sessions) is
future work with a real need behind it, not built speculatively here.

## Validation

`ContentValidator.Validate(QuestionSet) -> IReadOnlyList<string>`
(`Hermit.Games.Content`) — a plain static function, not a framework or a
custom Editor tool. Checks: empty question id, duplicate question ids,
question with zero answer options, question with zero or more than one
correct option, invalid (`<= 1`) `ContentVersion`, invalid (`<= 1`)
`SchemaVersion`, empty question set. Returns every issue found, not just the
first.

Runs from two places: `ContentValidatorTests` (EditMode, exhaustive) and
`ClasicoGameEngine.Begin()` (runtime, logs each issue via `HermitLog.Warning`
rather than throwing — a malformed sample question should degrade
gracefully, not take down the vertical slice; `ClasicoGameEngineTests`
verifies the warning fires via `LogAssert.Expect` without asserting on the
now-invalid data actually crashing anything downstream).

## Analytics metadata

`IGameAnalyticsSink` (unchanged shape otherwise): `QuestionPresented` and
`AnswerSubmitted` now take an additional `int contentVersion` parameter —
the presented/answered question's own `ContentVersion`. `GameCompleted`/
`GameAborted` need no signature change since they already receive the full
`GameResult`, which now itself carries `ContentSetId`/`ContentSchemaVersion`.
Still nothing is sent anywhere — `HermitLogAnalyticsSink` just logs it,
matching C5.

## GameResult

Reviewed per the brief's own question ("does GameResult need game id;
content set id/version; session metadata mínima"): `GameId` and `SessionId`
already existed (C5). Added exactly `ContentSetId`/`ContentSchemaVersion` —
nothing else. No coins, no rewards, no leaderboard fields; still explicitly
out of scope.

## Runtime integration

`GameSessionInstaller` (`Hermit.Runtime.GameFramework`) replaces C5's
`ClasicoVerticalSliceInstaller` as the composition root on the one GameObject
in `02_GameplaySandbox` (same zero-scene-wiring pattern — the scene edit was
swapping which script that one GameObject's `MonoBehaviour` component points
at, not adding new scene structure). Its `Awake()` reads, in order: build
registry from `GameCatalog`, build `GameFlowController`, build `ClasicoHud`
+ `ClasicoGameHost` + register it under `"clasico"`, build `GameSelectorHud`,
show the selector. Its `Update()` is generic: tick the flow controller while
Playing, then let the active host render its own frame — no game-specific
code in the loop itself.

`HermitRuntimeInstaller` (C4's global Networking bootstrap,
`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`) is untouched — C6 needed
no Supabase access and made none. The two installers still don't know each
other exist.

## Bootstrap scene flow (standalone Windows build fix)

Manual validation found a real bug the Editor could never have caught:
the standalone Windows Development Build opened to a blank blue screen —
no selector, no Clásico — while the C4 debug panel (F1) still worked.

**Root cause, confirmed with evidence, not assumed:** a standalone Player
always starts at build index 0. `ProjectSettings/EditorBuildSettings.asset`
lists `00_Bootstrap` first, and that scene has only ever held a Main Camera
(same dark-blue clear color as the other two scenes — hence "blue screen",
not a crash). `GameSessionInstaller` lives only on a GameObject inside
`02_GameplaySandbox.unity`. No code anywhere in the project called
`SceneManager.LoadScene`/`LoadSceneAsync` before this fix (confirmed by
grep) — nothing ever advanced past scene 0. Editor manual validation always
passed because pressing Play with `02_GameplaySandbox` open loads *that*
scene directly, ignoring build order entirely — the standalone build is the
only path that ever actually starts at build index 0. The build's own
`Player.log` confirmed this precisely: `[Hermit] Bootstrap initialized` and
the session-restore log line both appeared (proving `HermitRuntimeInstaller`
ran, matching F1 working), but zero `GameSession`/`Analytics`/
`ContentValidation` log lines appeared anywhere in it — direct evidence
`02_GameplaySandbox` was never loaded.

**Fix:** `BootstrapSceneFlow` (`Hermit.Runtime`) — a second, deliberately
separate `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` hook. Checks
whether the active scene is `00_Bootstrap`; if so, loads
`02_GameplaySandbox` directly (`01_Shell` is skipped — it is still an empty
placeholder and loading it would just show another blank camera instead of
fixing anything). Kept separate from `HermitRuntimeInstaller` on purpose:
that one owns Networking bootstrap and must stay scene-agnostic; this one
owns scene routing and is intentionally scene-specific — merging them would
blur a boundary neither needed blurred. Both hooks fire once, at app start;
Unity does not guarantee their relative order, but neither's effect depends
on the other's timing (`HermitRuntimeInstaller`'s root is
`DontDestroyOnLoad` and survives the scene swap regardless of when it runs;
`GameSessionInstaller.Awake()` fires as a normal consequence of
`02_GameplaySandbox` loading, independent of either
`RuntimeInitializeOnLoadMethod` call).

The decision logic is a single pure function
(`BootstrapSceneFlow.ShouldAdvance(activeSceneName)`), covered by
`BootstrapSceneFlowTests` (3 tests) — actually loading a scene isn't
something a unit test should do, so only the decision is tested directly;
the real scene load was verified manually (see "Manual validation").

**No loop risk:** the hook only ever fires once per app lifetime (that is
what `RuntimeInitializeOnLoadMethod` means), and `ShouldAdvance` only
returns true for the literal string `"00_Bootstrap"` — `02_GameplaySandbox`
itself never re-triggers it, so there is no path back into this check after
the one scene swap.

**Confirmed fixed** — a rebuilt Windows Development Build now starts,
reaches the selector, and the user played Clásico through to Results and
back to the selector entirely inside that standalone build. See "Manual
validation" below.

## C4 debug panel

Left in place, not deleted. Its fixed top-left `OnGUI` `Rect` used to sit
directly on top of the new selector's own top-left content, so it now starts
**collapsed** by default (a one-line "press F1" label) and expands with
**F1** (also a "Hide (F1)" button once expanded, for mouse-only use). Still
gated to `Development` environment only, unchanged from C4/C5 — this is a
visibility default, not a new condition.

## Scene strategy

`00_Bootstrap`, `01_Shell`, `02_GameplaySandbox` all remain. Their roles are
now more precisely understood than at C6's start:

- `00_Bootstrap` is build index 0 — the Player's real entry scene. It is
  **not** load-bearing for Networking (C5's correction still holds:
  `HermitRuntimeInstaller` runs after whichever scene loads first,
  regardless of which one that is), but it **is** load-bearing for scene
  routing as of this phase: `BootstrapSceneFlow` specifically checks for it
  by name and advances past it. Still holds no content beyond a Main
  Camera — its job is purely "be scene 0", not to display anything itself.
- `01_Shell` remains an empty placeholder, now explicitly skipped by
  `BootstrapSceneFlow` rather than just unused.
- `02_GameplaySandbox` is where `GameSessionInstaller` lives and is now
  reached automatically from a cold start, in both the Editor and a
  standalone build.

The direct `00_Bootstrap → 02_GameplaySandbox` jump is a deliberate,
temporary shortcut for exactly the two scenes that currently have content —
not a final scene-flow design. Once `01_Shell` gets real content (a menu,
etc.), this hop should become `00_Bootstrap → 01_Shell` (with `01_Shell`
itself deciding when to advance to gameplay) rather than continuing to skip
straight past it. That housekeeping is explicitly deferred, not decided
here — kept as a known limitation below.

## Fake second game (extensibility proof)

Two independent proofs, at two levels:

- **EditMode, registry/flow level** (`Fakes/FakeGame.cs`,
  `GameFrameworkExtensibilityTests`, unchanged from C5 in spirit): a
  `FakeGameDefinition`/`FakeGameEngine` pair, outside `Hermit.Games`
  entirely, registered and run through `GameRegistry`/`GameFlowController`.
  C6 updated `FakeGameDefinition` to implement the new `CreateEngine()`
  contract (config — `TicksToFinish` — now lives on the definition and is
  read lazily in `Begin()`, mirroring `ClasicoGameDefinition`/`ClasicoGameEngine`'s
  own pattern, replacing C5's external factory-closure approach).
- **PlayMode, selector level** (new in C6 —
  `Tests/PlayMode/Fakes/SecondGameDefinition.cs`,
  `GameSelectorPlayModeTests`): a second, separate trivial fake, built
  directly into a real `GameSelectorHud` instance with **two** fake games
  and zero Clasico involvement at all — stronger isolation than mixing a
  fake into the real catalog would have been. Proves: N games render as N
  buttons, clicking one fires the correct definition, a disabled game's
  button is shown but can't be launched, and an empty registry renders
  without crashing.

Neither dummy is referenced by `GameCatalog.asset` — they do not exist in
any real build.

## Tests

**EditMode**, all in `Hermit.Tests.EditMode`:
- Updated for the new API: `GameFlowControllerTests`,
  `GameFrameworkExtensibilityTests` (now call `registry.Register(definition)`
  and `controller.Start(definition, context)` directly — no
  `GameRegistration`), `ClasicoGameEngineTests` (added `ContentSetId`/
  `ContentSchemaVersion` assertions and a content-validation-logs-a-warning
  test), `GameResultTests` (added `ContentSetId`/`ContentSchemaVersion`
  cases).
- New: `GameRegistryTests` (empty registry, register/lookup, duplicate id,
  empty id, null, sort-order-then-registration-order tie-breaking, disabled
  games still enumerated), `ContentValidatorTests` (every rule in
  "Validation" above, plus "multiple issues all reported, not just the
  first").

**PlayMode**:
- `ClasicoPlayModeTests` (in `Hermit.Tests.PlayMode`) — updated for the new
  entry point: launching Clasico is now `FindButton("Game_clasico").onClick.Invoke()`
  instead of C5's in-game "Jugar" button (which no longer exists — see
  "Selector"). All the C5 keyboard-selection assertions (valid, active
  selection on every screen transition) carry over unchanged in spirit.
- New: `GameSelectorPlayModeTests` — see "Fake second game" above.
- New: `BootstrapSceneFlowTests` (3 tests) — see "Bootstrap scene flow"
  above.

**Deliberately not covered by automated tests**: an actual mouse/touch event
routed through the Input System's raycaster. Same rationale as C5 — a human
clicking it in the Editor is the real gate for that (see "Manual
validation").

## Input

Reuses C5's fix wholesale rather than re-deriving it: `RuntimeUIFactory.Select`/
`ChainVertical`/`ChainHorizontal` (moved there from `ClasicoHud` in this
phase, so `GameSelectorHud` can use the exact same, already-tested helpers)
plus the `selectedColor` fix already in `RuntimeUIFactory.CreateButton`. This
means the selector inherits C5's keyboard fix "for free":

- The selector always has a visible initial selection (`Show()` selects the
  first enabled game button) — never nothing selected.
- Every screen transition (selector → Clasico, Clasico → Results,
  Results → Restart, Results/Abort → selector) explicitly re-selects a
  valid, active object — never left pointing at something just deactivated.
- Question/option rendering in `ClasicoHud` still only re-selects once per
  actual question change (`ClasicoGameHost`'s own `_lastRenderedQuestionId`
  guard, carried over from C5's installer), never every frame.

Real keys, verified against the installed Input System package (not
re-derived from memory): **arrow keys / W-A-S-D** navigate, **Enter**
submits (not Space, not Numpad Enter), **Escape** cancels, **Tab does
nothing by default**. See `Docs/C5_GAME_FRAMEWORK.md`, "Keyboard navigation
fix" for the verification method.

## Manual validation

**Status: COMPLETE. All gates PASS**, run by the user in both the Unity
Editor and a standalone Windows Development Build — including a second,
rebuilt standalone run after the Bootstrap scene flow fix.

| # | Gate | Status | Notes |
|---|---|---|---|
| 1 | Unity compiles | **PASS** | No console errors. |
| 2 | EditMode tests | **PASS** | All green, including the new `GameRegistryTests`/`ContentValidatorTests`. |
| 3 | PlayMode tests | **PASS** | All green, including `GameSelectorPlayModeTests` and `BootstrapSceneFlowTests`. |
| 4 | Game selector | **PASS** | Appears on Play, "Clásico" listed and visibly selected with no click. |
| 5 | Clásico visible from selector | **PASS** | |
| 6 | Launch from selector | **PASS** | Starts immediately — no secondary in-game "Jugar" screen. |
| 7 | Gameplay | **PASS** | Scoring/feedback/advance all correct. |
| 8 | Results | **PASS** | |
| 9 | Return to selector | **PASS** | Lands back on the selector, Clásico re-selectable, no dead end. |
| 10 | Mouse input | **PASS** | |
| 11 | Keyboard input | **PASS** | Selector and in-game navigation/Enter both confirmed. |
| 12 | Timeout | **PASS** | 8s per-question timeout still auto-submits and advances. |
| 13 | C4 debug toggle (F1) | **PASS** | Collapses/expands correctly, no longer covers the selector by default. |
| 14 | C4 regression | **PASS** | Debug panel still opens and functions once expanded. |
| 15 | Windows Development Build | **PASS** | Confirmed on a rebuild *after* the Bootstrap scene flow fix (the first build had the blue-screen bug — see "Bootstrap scene flow" above). |
| 16 | BootstrapSceneFlow | **PASS** | Standalone build now starts, reaches the selector, plays Clásico through to Results and back, entirely standalone. |

**Gate state: 16/16 PASS. C6 objective (enumerable registry, generic
selector, no per-game fixed Resources path, content pipeline foundation, a
working standalone build) is met.**

### Evidence log (user-confirmed)

- Selector, launch, gameplay, Results, return-to-selector, mouse, keyboard,
  and timeout all confirmed in the Unity Editor first.
- Standalone Windows build: the *first* build reproduced the blue-screen bug
  exactly as diagnosed (root cause in "Bootstrap scene flow" above). After
  `BootstrapSceneFlow` was added, a *second* build was produced and
  confirmed: starts correctly, reaches the selector, Clásico launches and is
  fully playable, Results and return-to-selector work, keyboard/mouse work,
  timeout works, F1 works — entirely inside the standalone `.exe`.
- `BootstrapSceneFlowTests` (3 tests) pass alongside the rest of the suite.

### Procedure as executed

1. Open the project in Unity 6.3. Confirm it compiles with no console errors.
2. Window → General → Test Runner → EditMode → Run All. Expect all tests
   green, including the updated `GameFlowControllerTests`/
   `GameFrameworkExtensibilityTests`/`ClasicoGameEngineTests`/
   `GameResultTests` and the new `GameRegistryTests`/`ContentValidatorTests`
   (plus every pre-existing C4 EditMode test, unaffected).
3. Test Runner → PlayMode → Run All. Expect the updated `ClasicoPlayModeTests`
   and the new `GameSelectorPlayModeTests` green, alongside
   `HermitBootstrapPlayModeTests`. Expect this to take noticeably longer
   than EditMode (a few tests play through all 10 real Clasico questions in
   real time) — that's expected, not a hang.
4. Open `Assets/Hermit/Scenes/02_GameplaySandbox.unity`, press Play.
5. Expect a selector screen ("Hermit — Selecciona un juego") with exactly
   one entry, "Clásico", already visibly selected (no click needed) — no
   console errors.
6. Click (or press Enter on) **Clásico**. Expect it to start immediately —
   no secondary in-game "Jugar" screen.
7. Play through as in C5 (answer questions, confirm score/feedback/timeout,
   reach Results, confirm Reintentar/Salir).
8. From Results, click **Salir**. Expect to land back on the **selector**
   screen (not a blank screen, not stuck) with Clásico visibly selected
   again, launchable a second time.
9. **Keyboard, from a fresh Play (mouse untouched):** confirm the selector
   itself is keyboard-navigable/activatable (Enter launches Clásico with no
   click), then repeat C5's in-game keyboard checklist
   (`Docs/C5_GAME_FRAMEWORK.md`, step 11) for Clásico itself.
10. **C4 regression:** press **F1** to expand the (now collapsed-by-default)
    C4 debug panel, confirm it still opens and one call (e.g. Login) still
    succeeds. Press F1 again to collapse it back.
11. **Windows Development Build:** File → Build Settings → Build (with
    `00_Bootstrap`/`01_Shell`/`02_GameplaySandbox` all enabled, their normal
    state). Run the produced `.exe`, repeat steps 5–10.

All steps above were executed by the user and passed — see the gate table
and evidence log at the top of this section.

## Known limitations

- `GameCatalog`/`ClasicoGameDefinition`/`C5SampleQuestions` are still one
  hand-authored sample set — C6 explicitly did not migrate the real academic
  library (out of scope per the brief).
- Only one real playable game exists (Clásico) — the `IGamePresenterHost`
  dictionary pattern is proven generic by tests (two *fake* games), not by a
  second *real* one. Adding the next real game is expected to need exactly
  one new `IGamePresenterHost` implementation + one dictionary line in
  `GameSessionInstaller`, per "Where a specific game's presentation gets
  wired in" above — this is a prediction based on the design, not yet
  empirically confirmed with real production code.
- `Topic` is still a free-form string, not a formal taxonomy — acceptable
  for 10 sample questions, likely insufficient once real content scales.
- No touch/gamepad testing was done (no such hardware in this session).
- ~~`01_Shell` remains an empty placeholder, now explicitly skipped by
  `BootstrapSceneFlow`'s direct `00_Bootstrap → 02_GameplaySandbox` jump~~ —
  **resolved in C7**: `01_Shell`/`ShellInstaller` is now the real product
  entry point, and `BootstrapSceneFlow.DestinationSceneName` points there
  instead. `02_GameplaySandbox` remains, unaffected, as the dev/test-only
  scene. See `Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md`.
- `BootstrapSceneFlow`'s entry-scene check is a literal string comparison
  against `"00_Bootstrap"` — correct and simple for exactly one entry
  scene, but would need to become a small explicit sequence (rather than a
  single string match) if the boot chain ever grows past two hops.

## Decision log

- Dropped `GameRegistration` — see "Registration model".
- `GameDefinition.CreateEngine()` made abstract rather than keeping an
  externally-supplied factory — moves engine construction to the one place
  that already owns all of a game's other config, consistent with how
  `ClasicoGameEngine` already reads its own timing/scoring config from
  `ClasicoGameDefinition` inside `Begin()`.
- Selector's game-launch presentation wiring is a `Dictionary<string, IGamePresenterHost>`
  populated by one explicit line per game — not a reflection scan, not a
  second registry, not an `if`/`switch`. Justified in detail above; revisit
  only once a second *real* game's needs prove this insufficient.
- `SchemaVersion` (per set) kept separate from `ContentVersion` (per
  question) rather than one unified "version" — they answer different
  questions and conflating them would have made "which version did the
  student see" ambiguous.
- `GameSelectorHud` made `public` (was going to default to `internal` like
  `ClasicoHud`) specifically so `GameSelectorPlayModeTests` can build one
  directly with fake games — same justification already established for
  `GameSessionInstaller` in C5/C6.
- `BootstrapSceneFlow` added as a *separate* hook from
  `HermitRuntimeInstaller` rather than folded into it — found necessary only
  after manual validation surfaced the standalone-build blue screen (see
  "Bootstrap scene flow"); kept separate specifically so Networking
  bootstrap stays scene-agnostic and scene routing stays its own concern,
  rather than quietly coupling the two the first time a fix touched both.

## Closeout

C6 is **COMPLETE**. 16/16 gates PASS, verified manually by the user in the
Unity Editor and in a standalone Windows Development Build — including a
second, rebuilt standalone run confirming the Bootstrap scene flow fix. No
V1, Supabase-write, Hermit Coins, blockchain, leaderboard, or second-real-game
work was done at any point in this phase, matching scope.
