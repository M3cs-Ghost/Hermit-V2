# C9.1 — Start Screen → Hermit Hub Implementation

**Status: MANUAL VISUAL VALIDATION — PASS** (recorded at the C9 checkpoint).
The full flow — cold boot → PRESS START → Start→Hub transition → Hermit Hub
→ hotspot navigation → Arcade → return to Hub — has been confirmed against
a real Windows build and works well. This covers C9.1, C9.1a, C9.1b, and
C9.1c (each sub-phase's own status banner below is updated to match).

**This validates the *implementation* — the interactive flow, timing,
navigation, and typography — not a final approval of the underlying art.**
The Start Screen and Hub illustrations remain **production candidates**
(`ArtBible/Candidates/Worlds/StartScreen/...`, `.../Worlds/Hub/...`,
copied into `Content/Resources/Art/Shell/...` for runtime use) — they have
not been explicitly approved as final art by the user, only confirmed to
work well enough to validate the surrounding experience. A later art phase
may still revise them without this document's implementation notes needing
to change.

This document records what C9.1 actually built: a cold-boot Title/Press Start
gate and a short cinematic entry into the interactive Hermit Hub, replacing
the old instant `ShellInstaller.Awake() -> ShowHome()` jump. It is the
as-built reference for this feature — see the approved C9.0 brief (prior
conversation turn) for the design rationale that isn't repeated here.

Per the C9 checkpoint: this experience (Start Screen composition, PRESS
START, the Start→Hub transition, Hub settled framing, Marcellus destination
typography, and hotspot positions) is now **frozen** — treat as validated
and do not make further aesthetic tweaks unless a real bug is found.

## A. Production assets

Two production copies were made from ArtBible *candidate* art (never
approved final art, and the ArtBible originals were never modified):

| Purpose | ArtBible source (untouched) | Production copy (used at runtime) |
|---|---|---|
| Start Screen | `ArtBible/Candidates/Worlds/StartScreen/Hermit_StartScreen_Candidate_01.png` | `Assets/Hermit/Content/Resources/Art/Shell/StartScreen/Hermit_StartScreen_01.png` |
| Hub | `ArtBible/Candidates/Worlds/Hub/Hermit_Hub_Candidate_01.png` | `Assets/Hermit/Content/Resources/Art/Shell/Hub/Hermit_Hub_01.png` |

Both are 1456×816 (verified directly, not assumed) — the same aspect ratio
(1.7843) within 0.37% of the project's 1280×720 CanvasScaler reference
resolution, so `AspectRatioFitter` in `EnvelopeParent` mode (the same
technique already proven on Western's full-bleed cinematic closeups) covers
both without any letterboxing.

Import settings were fixed by hand after Unity's first auto-generated
`.meta` used the wrong defaults for both files:

- Sprite mode: **Single** (Unity's auto-import defaulted to Multiple, which
  would have broken `Resources.Load<Sprite>(path)` — Multiple-mode sprites
  are sub-assets, not resolvable at the base path).
- Compression: **None/Uncompressed** on both DefaultTexturePlatform and
  Standalone (Unity's auto-import defaulted to Compressed; every other
  Gold-art asset in this project uses Uncompressed).
- No mipmaps, Bilinear filter, Clamp wrap, max size 2048 — matches every
  other full-bleed background import in this project.

The two candidates were visually inspected directly (not assumed similar)
and found compositionally mismatched: different camera angle/distance
(distant threshold view vs. a close-up inside the Hub), no reliable
foreground correspondence, and a blue→violet color-grade shift. This is why
the transition uses a fog-covered instant sprite swap rather than any
crossfade — see Section G.

## B. StartScreenHud hierarchy

`Assets/Hermit/Runtime/GameFramework/StartScreenHud.cs` — one new
`internal sealed class StartScreenHud : MonoBehaviour`, following the
project's existing one-HUD-per-screen convention (`ShellHomeHud`,
`GameSelectorHud`, `ClasicoHud`), built entirely in code via
`RuntimeUIFactory` (no prefabs, no scene wiring). Built once, in
`ShellInstaller.Awake()`, and lives for the life of the process.

```
StartScreenPanel (Image, Theme.Background)
├─ SceneArt (Image + AspectRatioFitter, EnvelopeParent) — swaps sprite once
├─ BloomPulse (Image, full-stretch, alpha-only warm flash overlay)
├─ FogOverlay (Image, full-stretch, alpha-only haze/cover overlay)
├─ HermitTitle (Text, "HERMIT")
├─ PressStartPrompt (Text, "PRESS START")
├─ ConfirmButton (Button, full-stretch, fully transparent — the whole
│  screen is the hit target, per this project's established
│  "art/world IS the target" pattern from Western's WesternTarget buttons)
└─ HubNavigation (CanvasGroup, alpha 0 until revealed)
   ├─ ArcadeHotspot   (Button, "ARCADE",      isFinal: true)
   ├─ AcademiaHotspot (Button, "ACADEMIA",    isFinal: false)
   ├─ CodexHotspot    (Button, "CODEX",       isFinal: false)
   ├─ HistorietasHotspot (Button, "HISTORIETAS", isFinal: false)
   ├─ ComingSoonLabel (Text, hidden until a non-final hotspot fires)
   └─ SessionStatus   (Text, moved here from the retired ShellHomeHud)
```

Each hotspot is an invisible full-hit-region `Button` (transparent
`Image.color`) with a small caption label and a 3px "underline" `Image`
beneath it that brightens (alpha 0.35→1.0 for Arcade, 0.18→1.0 for the
non-final three) when it holds keyboard/gamepad focus — a non-color focus
cue, since mouse hover already gets `Button`'s own built-in highlight tint
for free.

## C. State model

```csharp
private enum Phase { Title, Transitioning, Done }
```

- **Title** — cold-boot state. Holds indefinitely until confirmed. HERMIT
  title + PRESS START prompt visible and pulsing; ConfirmButton is the
  EventSystem selection; Hub art, hotspots and nav are all inert.
- **Transitioning** — the one-shot ~3.6s coroutine (Section D) runs.
  `OnConfirm()` is guarded by a phase check *and* immediately deactivates
  the ConfirmButton, so a second confirm during this phase is structurally
  impossible, not just debounced.
- **Done** — permanent for the rest of the session. Hub art shown, all four
  hotspots interactable, `Update()` polls `EventSystem.currentSelectedGameObject`
  to keep each hotspot's underline in sync with keyboard/gamepad focus.

`ShowTitle()` is called exactly once, from `ShellInstaller.Awake()`, and is
never called again for the life of the process — the Start Screen is
cold-boot entry only. Every return to the Hub after that (from the Arcade
selector's Back button, or after a game session ends) calls `ShowNavigation()`
instead, which never touches Title-state assets at all.

## D. Transition timeline (as implemented)

Total duration: **3.60s** (within the approved 3.4–3.8s window; deliberately
not shortened toward a Western-cinematic-length ~2.5s). Implemented as one
`TransitionRoutine` coroutine (matching `WesternShootoutPresenter.CinematicIntroRoutine`'s
proven shape: named `WaitForSeconds` deltas between beats, with small
concurrent sub-coroutines for push-in/fog/bloom), not a per-frame timer.

| t (abs) | Beat |
|---|---|
| 0.00 | Prompt/title fade begins (0.45s fade-out) |
| 0.20 | Entry swell/arrival audio cue starts (single clip, see Section L) |
| 0.45 | Continuous push-in begins (scale 1.00→1.06, small drift) — one motion spanning both art pieces, no seam at the swap |
| 0.85 | Warm bloom pulse (rise+fall, 0.25s, peak alpha 0.35) |
| 1.00 | Fog begins rising toward cover (0.80s, peak alpha 0.62) |
| 1.80 | Sprite swap (Start Screen → Hub) happens instantly behind the fog; fog begins clearing (0.80s) |
| 2.60 | Hub settled; brief hold |
| 3.00 | Hub navigation fades in (0.60s) |
| 3.60 | Hub fully interactive — hotspots enabled, ArcadeHotspot selected |

All of the above deltas are named constants at the top of `StartScreenHud.cs`
(`ToSwellStart`, `ToPushInStart`, `ToBloomStart`, etc.), each with a comment
giving its absolute timestamp for cross-reference against this table.

## E. Input implementation

No new input code and no per-device branching. `ConfirmButton` and all four
hotspots are real `UnityEngine.UI.Button`s driven by the project's existing
`InputSystemUIInputModule` + `EventSystem` plumbing — mouse click, keyboard
Submit, and gamepad Submit all reach `Button.onClick` identically, as long as
a Button is the current EventSystem selection. `RuntimeUIFactory.Select(...)`
is called on the ConfirmButton when Title begins, and on `_hotspots[0]`
(Arcade) whenever the Hub navigation becomes active (`TransitionRoutine`'s
final beat, and `ShowNavigation()` — see the bug this second call fixed, in
Section P).

## F. Fog / bloom implementation (full-screen effect safety)

Both `BloomPulse` and `FogOverlay` are plain full-stretch `Image`s whose
*only* animated property is `Image.color`'s alpha channel — never
`CanvasGroup`, never `Image.Type.Filled`/`Radial360`. This is the same rule
`WesternShootoutPresenter.GunshotFlashRoutine` already follows, to avoid the
documented historical black-frame bug class tied to `Filled`/`Radial360` on
full-screen overlays. No real URP Bloom post-processing is used anywhere.

## G. Art-swap implementation (why not a crossfade)

Because the two candidates are compositionally mismatched (Section A), a
visible crossfade would read as two unrelated photos dissolving into each
other, not a camera continuing forward. Instead:

1. Fog rises to `FogPeakAlpha = 0.62` over 0.80s (soft haze, never opaque —
   explicitly checked by `ArtSwap_OnlyHappensBehindSufficientFogCoverage`,
   which asserts fog alpha at swap time is `> 0.30 && < 0.95`).
2. `_sceneArt.sprite` is swapped from the Start Screen sprite to the Hub
   sprite in a single frame, fully behind the fog.
3. Fog clears back to 0 over the next 0.80s, revealing the Hub.

The continuous push-in (scale + drift) runs straight through this swap
without any discontinuity, since it only ever touches `localScale`/
`anchoredPosition` on the art's `RectTransform`, never the sprite itself —
so the swap reads as "the fog opened onto a new place," not a hard cut.

## H. ShellHomeHud evolution

`ShellHomeHud.cs` is **untouched** (per the "disable, don't delete" rule) —
it is simply no longer instantiated by `ShellInstaller`. Its former
responsibilities moved to `StartScreenHud`:

- The flat background + centered "HERMIT" title/tagline composition is
  retired from the active UI entirely; `StartScreenHud`'s own Title-state
  art/title/prompt is the new first-thing-seen, and the Hub's own
  illustration is the durable "home" background.
- The one-line "Sesión activa"/"Invitado" session status text moved to
  `StartScreenHud.SetSessionStatus(...)`, still a single unobtrusive line,
  now anchored lower-right over the Hub art.
- The old "Juegos" button is the direct conceptual ancestor of the new
  `ArcadeHotspot` — same destination (`GameSelectorHud`), now presented as
  an invisible hit region over the Hub's own violet structure instead of a
  standalone button.

## I. Hotspot mapping (temporary placeholder — not final creative direction)

Positions were chosen by direct visual inspection of the Hub candidate's
four distinct architectural regions (normalized, Unity Y-up anchors):

| Hotspot | Anchor (x, y) | Region inspected |
|---|---|---|
| ArcadeHotspot (final) | (0.79, 0.63) | right violet structure |
| AcademiaHotspot | (0.50, 0.53) | central grand hall |
| CodexHotspot | (0.21, 0.60) | left amber building |
| HistorietasHotspot | (0.69, 0.28) | lower-right lantern market |

This mapping is explicitly temporary and will very likely change once real
Hub art and creative direction for Academia/Codex/Historietas exist.

## J. Arcade → Clásico flow

`ArcadeHotspot` → `ShellInstaller.OnArcadeRequested()` → `StartScreenHud.HideNavigation()`
(Hub's interactive layer disabled; its art stays rendered underneath,
harmless since the selector's own opaque panel fully covers it) →
`GameHub.ShowSelector()` → the existing `GameSelectorHud` (unchanged) →
`Game_clasico` → the existing `ClasicoGameHost`/`ClasicoHud` stack
(unchanged) — identical to the stack `GameSessionInstaller` already uses in
`02_GameplaySandbox`.

## K. Back-navigation behavior

- Selector's "Volver" → `ShellInstaller.OnSelectorBackRequested()` →
  `ShowHub()` → `StartScreenHud.ShowNavigation()`. Never returns to the
  Start Screen.
- A completed game session → Results → Selector → "Volver" → the same
  `ShowHub()` path. Also never returns to the Start Screen.
- `ShowNavigation()` re-enables the nav `CanvasGroup` **and** re-selects
  `ArcadeHotspot` (see Section P — this reselection was a real bug found
  and fixed during this phase's test run).

## L. Placeholder audio (deliberately minimal)

Per explicit instruction, exactly two new `ProceduralAudio` clips were
added — no procedural audio "suite," no ambience layer, no per-hotspot
sounds:

- `ProceduralAudio.SoftConfirmChime` — a short (0.5s) two-partial soft sine
  chime (440Hz + 660Hz, gentle attack/release) played once on Press Start
  confirm. Deliberately not a sharp click/arcade blip.
- `ProceduralAudio.EntrySwell` — one 3.0s composite clip standing in for
  *both* the transition's rising swell and its Hub-arrival resolution (root
  220Hz + fifth 330Hz + a shimmer partial fading in), whose own amplitude
  envelope rises over its first third, holds, then settles over its last
  third. Played once, at t=0.20 in the transition. Deliberately a single
  clip, not two, per the "one restrained transition swell/arrival cue"
  instruction.

Final Start Screen/Hub music is explicitly out of scope for this phase.

## M. Responsive layout

Uses the project's existing `CanvasScaler` (ScaleWithScreenSize, 1280×720
reference, matchWidthOrHeight 0.5) and the `AspectRatioFitter`/`EnvelopeParent`
technique already proven on Western's full-bleed art — not re-derived for
this feature. Not manually verified in a real windowed/resized build yet
(see the manual validation checklist at the end of this document).

## N. Test migration helper

Every existing Shell test that only cares about what happens *after* the
Start Screen is a shared, state-driven helper — never a flat
`WaitForSeconds` — added once per test fixture:

```csharp
// ShellPlayModeTests.cs
private IEnumerator EnterShellPastStartScreen()
{
    FindButton("ConfirmButton").onClick.Invoke();
    yield return WaitUntil(
        () => FindButton("ArcadeHotspot").interactable,
        6f,
        "Hub navigation (ArcadeHotspot) never became interactable within 6s of confirming the Start Screen.");
}

// ShellRealSceneCompositionTests.cs
private IEnumerator EnterShellPastStartScreenAndOpenArcade()
{
    FindButtonInScene("ConfirmButton").onClick.Invoke();
    yield return WaitUntil(
        () => FindButtonInScene("ArcadeHotspot").interactable,
        6f, /* ... */);
    FindButtonInScene("ArcadeHotspot").onClick.Invoke();
    yield return null;
}
```

Both poll the real "is the Hub navigation interactable yet" signal rather
than guessing a duration, so a future change to the transition's timing
never requires touching every test that merely needs to get past it.

## O. Tests added / updated

**`ShellPlayModeTests.cs`** (synthetic `ShellInstaller`):
- `ShellInstaller_ColdBoot_ShowsStartScreen_PressStartVisible_HubNotInteractable` (renamed/rewritten) — PRESS START/HERMIT visible, ArcadeHotspot not interactable, ConfirmButton is the EventSystem selection.
- `MouseConfirm_StartsTransition_HubBecomesInteractiveWithinApprovedTimeline` (new) — measures real elapsed time, asserts `> 3.0s && < 6s` (proves the timeline was not shortened), confirms title/prompt hidden, ConfirmButton retired, scene art now references the Hub sprite, fog/bloom both settled back near 0.
- `RepeatedConfirmDuringTransition_IsIgnored` (new) — rapid-clicks ConfirmButton 3×, asserts only one transition ran.
- `ArtSwap_OnlyHappensBehindSufficientFogCoverage` (new) — polls until the sprite swap happens, asserts fog alpha at that instant is `> 0.30 && < 0.95`.
- `ClickingArcadeHotspot_ShowsTheSelector_WithClasicoListed` (renamed from `ClickingJuegos_...`).
- `SelectorBack_ReturnsToHub_StartScreenDoesNotReplay` (renamed from `SelectorBack_ReturnsToHome`) — also asserts the Start Screen never reappears.
- `LaunchingClasicoFromShell_StartsAGame`, `FullLoop_ShellToClasicoToResultsToSelectorToHub` (renamed from `...ToShell`), `F1DebugPanelToggle_DoesNotInterfereWithShell` — migrated to the new helper/hotspot names.
- `NonFinalDestinations_AreFocusableButShowComingSoon_NeverANewScreen` (new) — Academia is focusable and interactable, clicking it shows the "Próximamente" cue, and nothing else in the Hub is disturbed.

**`ShellRealSceneCompositionTests.cs`** (real `01_Shell` scene):
- `RealShellScene_ColdBoot_ShowsStartScreen_NotHubDirectly` (new) — same cold-boot assertions as above, against the real serialized scene.
- `RealShellScene_LaunchingClasico_VisiblyShowsAGoldMicrogamePresenter_NotTheOldC7Screen` and `RealShellScene_DecisionTimeoutOnActivePresenter_RestoresLocalReactionTransforms` — migrated to `EnterShellPastStartScreenAndOpenArcade()`.

No Western files, Western tests, or screenshot tests were added or touched.

## P. A real bug found and fixed during this phase's test run

The first full PlayMode run surfaced two real failures, both in new C9.1
code:

`StartScreenHud.ShowNavigation()` re-enabled the Hub's `CanvasGroup`
interactivity but never restored EventSystem focus. After Arcade →
Selector → Back, the EventSystem's `currentSelectedGameObject` was left
pointing at the Selector's now-inactive `Game_clasico` button, failing
`AssertValidSelection` in `SelectorBack_ReturnsToHub_StartScreenDoesNotReplay`
and `FullLoop_ShellToClasicoToResultsToSelectorToHub` with "Selected object
'Game_clasico' is not active in the hierarchy."

Fixed by adding `RuntimeUIFactory.Select(_hotspots[0].Button)` to
`ShowNavigation()`, matching what `TransitionRoutine` already did on first
entry. Both tests pass in isolation after the fix; confirmed with a second
full-suite run.

## Q. EditMode result

Ran 4 times across this phase (after each meaningful code change):
**110/110 passed**, 0 failed, 0 compile errors, every time.

## R. PlayMode result

Two full-suite runs, plus isolated `-testFilter` reruns of every failure
observed:

- **Run 1** (before the Section P fix): 63 total, 59 passed, 4 failed.
- **Run 2** (after the Section P fix): 63 total, 62 passed, 1 failed (the
  `RealShellScene_LaunchingClasico_...` environment/system-load timeout in
  the table below — the `RealShellScene_DecisionTimeoutOnActivePresenter_...`
  Detective test happened to pass this run, consistent with the
  flaky-by-design explanation in Section S).
- **Run 3** (final confirmation): **63 total, 63 passed, 0 failed.**

Every individual failure across both runs was isolated and root-caused
before being called anything:

| Test | Verdict |
|---|---|
| `ClasicoPlayModeTests.GameShow_ShowsIllustratedBackgroundAndPresentadorCard` | Environment/system-load anomaly — hit a hard 180000ms timeout once, passed cleanly in isolation (5.6s). Uses `GameSessionInstaller`, not `ShellInstaller` — cannot be caused by C9.1 code. |
| `ShellPlayModeTests.FullLoop_ShellToClasicoToResultsToSelectorToHub` | Real bug (Section P) — fixed, now passes in isolation. |
| `ShellPlayModeTests.SelectorBack_ReturnsToHub_StartScreenDoesNotReplay` | Real bug (Section P) — fixed, now passes in isolation. |
| `ShellRealSceneCompositionTests.RealShellScene_DecisionTimeoutOnActivePresenter_RestoresLocalReactionTransforms` | See Section S — a genuine, pre-existing, out-of-scope Detective bug, non-deterministically exposed. |
| `ShellRealSceneCompositionTests.RealShellScene_LaunchingClasico_VisiblyShowsAGoldMicrogamePresenter_NotTheOldC7Screen` | Environment/system-load anomaly — hit the same 180000ms timeout once (in a run that took 34 minutes total, vs. ~17 for the first run), passed cleanly in isolation (6.9s). |

## S. A real, pre-existing, out-of-scope finding (not fixed — Detective is on the do-not-touch list)

`RealShellScene_DecisionTimeoutOnActivePresenter_RestoresLocalReactionTransforms`
failed reproducibly (2/2) when run in isolation, with:

```
DetectiveLineup/Spotlight did not return to its resting anchoredPosition
after its decision-timeout reaction — baseline=(0.00, 0.00), now=(-321.00, -199.69).
```

Root cause traced to `GameHub.cs:79`:

```csharp
var context = new GameContext(new HermitLogAnalyticsSink(), new System.Random());
```

`GameContext.Rng` is seeded from an **unseeded `System.Random()`**
(`Environment.TickCount`-based) every time a real game session starts. This
predates C9.1 entirely. C9.1's ~3.6s Start Screen transition shifts *when*
(in wall-clock terms) that seed is drawn, compared to the old instant-boot
flow — which is enough to change which microgame archetype gets drawn
first. In two isolated `-testFilter` reruns (which have very consistent
batch-mode startup timing), this consistently landed on the
`DetectError`/Detective archetype, exposing what appears to be a genuine,
pre-existing bug in `DetectiveLineup`'s `LocalMotionFx` reset-after-timeout
logic. In the full-suite context (where preceding tests' variable duration
shifts the seed further), the same test passed on its second full run —
i.e. this is a real bug, but a *flaky-by-design* one, not a deterministic
C9.1 regression.

**Not fixed.** Detective, Clásico gameplay, and Balance are explicitly on
this phase's do-not-touch list. No Detective/`LocalMotionFx` code was
modified. This is reported here as a discovered, pre-existing, out-of-scope
defect for a future phase to address — not swept under "known flake."

## T. Documentation created

This document.

## U. Remaining risks / open items

- Hotspot placement (Section I) is placeholder and unvalidated against a
  real windowed/resized build — the CanvasScaler math should hold, but has
  not been eyeballed on real hardware yet.
- Title/prompt vertical placement (Section D's "top ~15-20% is clear sky"
  assumption) was chosen from static image inspection only, not a running
  build.
- The Section S Detective/`LocalMotionFx` bug is real and will surface
  again unpredictably in future runs of this same test (and possibly others
  that share the "let a random archetype run to timeout" pattern) — a
  future phase should either seed `GameContext.Rng` deterministically for
  tests or fix the underlying Detective reset bug, but neither is in scope
  here.
- No manual Windows-build validation has been performed yet (see the
  checklist below) — this is the primary reason this document's status is
  "IN PROGRESS," not "COMPLETE."

## V. Manual validation checklist

**Confirmed at the C9 checkpoint** — the user validated this full flow
against a real Windows build and reported it works well.

- [x] Cold boot a real Windows build; confirm HERMIT + PRESS START appear over the Start Screen art, not a black/blank frame.
- [x] Confirm via mouse click, via keyboard (Enter/Space with the button selected), and via a connected gamepad's Submit button.
- [x] Watch the full ~3.6s transition at real framerate; confirm the fog swap does not read as a black-frame flash or a jarring cut.
- [x] Confirm the push-in/drift reads as "gentle," never a dramatic zoom.
- [x] Confirm all four hotspot labels are legible and roughly aligned with sensible Hub regions at 16:9 and at least one other window size/aspect.
- [x] Confirm Arcade → Selector → Clásico → Results → Selector → Back lands back on the Hub, not the Start Screen.
- [x] Confirm Academia/Codex/Historietas show "Próximamente" and never a blank/broken screen.
- [x] Confirm the confirm chime and entry swell are audible, not clipped, and not jarring.
- [x] Confirm quitting and relaunching always shows the Start Screen again (cold boot only replays once per process, not once per install).

---

# C9.1a — Hub Composition and Destination Typography Polish

**Status: MANUAL VISUAL VALIDATION — PASS** (superseded in practice by
C9.1b/C9.1c's further composition/position corrections, but this pass's
own typography direction — Marcellus, AccentWarm — carried forward and is
part of what's now validated; see the top-of-document status).

Manual Windows-build validation of C9.1 found the transition itself good, but
flagged two settled-Hub issues: the Hub art sat slightly too high (the top
monumental building too close to the frame edge), and the four destination
labels read as placeholder/debug text against the illustration. This pass
fixes both, without touching the transition, its timing, the fog logic, or
either candidate art file.

## Manual visual feedback (source of this pass)

> "The Hub artwork reads slightly too high in the viewport... the current
> labels... look like temporary/debug text. They are too small, too plain,
> too white, weak against the artwork, insufficiently integrated with their
> architectural destinations." — manual Windows-build validation of C9.1.
> "The Start Screen → Hub transition itself looks GOOD."

## Hub vertical offset and settled scale

Added two fields to `StartScreenHud` that `PushInRoutine` now reads live,
every frame, instead of the fixed constants it used before:

```csharp
private static readonly Vector2 HubSettledOffset = new Vector2(0f, -24f);
private const float HubSettledScaleBoost = 1.015f;
private Vector2 _artBaseOffset = Vector2.zero;
private float _artScaleTarget = PushInPeakScale;
```

`_artBaseOffset`/`_artScaleTarget` start at `Vector2.zero`/`PushInPeakScale`
(i.e. the Start Screen's presentation is byte-for-byte unchanged) and are
only ever reassigned once, in `TransitionRoutine`, in the exact same
statement that swaps the sprite from Start Screen to Hub at t=1.80 —
**behind the fog, at the same instant as the already-approved hard-cut art
swap**, not as a new, separately-visible animation. This was a deliberate
choice over animating the shift openly: the brief explicitly permitted
"separate settled transform values" for Start vs. Hub, and bundling the
change into the swap that's already accepted as a hard cut avoids
introducing any *new* visible seam.

`PushInRoutine`'s own drift/scale math (`DriftAmount`, `PushInPeakScale`,
the easing curve) is completely unchanged — the Start Screen's push-in feel
is untouched, per the brief's explicit instruction.

- **Final Hub vertical offset**: `HubSettledOffset.y = -24`, plus the
  existing push-in drift's own `-DriftAmount * 0.5 = -2`, for a final
  settled Y of **-26** canvas-reference units (within the brief's requested
  20-35 unit range). Expressed entirely in 1280×720 CanvasScaler reference
  units, never raw screen pixels — the existing `CanvasScaler`/
  `AspectRatioFitter` stack already makes this resolution-independent.
- **Final Hub settled scale**: `PushInPeakScale (1.06) * HubSettledScaleBoost (1.015)` ≈
  **1.076** total (vs. 1.06 for the Start Screen, and vs. 1.06 pre-C9.1a for
  the Hub too) — a ~1.5% additional zoom, well inside the brief's approved
  ~1.01–1.02 range for covering the small edge the vertical shift can
  expose under `AspectRatioFitter.EnvelopeParent`. Not visually confirmed
  against a real build yet — the two candidate art pieces' aspect ratio is
  within 0.37% of the reference resolution, so the natural envelope overflow
  margin is thin; this value is a reasoned estimate, not a measurement.

## Destination typography

Replaced the four hotspots' plain `UnityEngine.UI.Text` labels with
TextMeshPro, via one new `RuntimeUIFactory.CreateWorldLabel(...)` method
(added specifically for text presented directly over illustrated world art
— every other screen in this project still uses the plain `Text`-based
`CreateText`). TextMeshPro's runtime/editor assemblies ship inside this
project's `com.unity.ugui@2.0.0` package (Unity 6 merged TMP into core
UGUI) but its Essential Resources (default font asset, TMP Settings) were
not yet imported — done once via a new one-time editor bootstrap,
`Assets/Hermit/Editor/TmpEssentialResourcesImporter.cs`, run via
`-executeMethod Hermit.Editor.TmpEssentialResourcesImporter.ImportIfMissing`
(**without** `-quit` — Unity 6000.3.23f1 was observed to skip
`-executeMethod` entirely when combined with `-quit` in this project; the
importer calls `EditorApplication.Exit` itself once `AssetDatabase`'s async
package-import callback actually fires, which is also why it isn't
just an inline `ImportPackage` + immediate exit). `Hermit.Runtime.asmdef`
and `Hermit.Tests.PlayMode.asmdef` both gained an explicit reference to
`Unity.TextMeshPro` (GUID `6055be8ebefd69e48b49212b09b47b2f`) — not
auto-referenced into custom asmdefs by default.

### Font / size / weight

- Font: LiberationSans SDF (this project's only imported TMP font asset —
  no separate bold weight file, so **Bold** is TMP's synthetic/faux bold via
  `FontStyles.Bold`, not a distinct bold font file).
- Size: **26pt** at the 1280×720 reference (`LabelFontSize`) — within the
  requested 24–28pt range, chosen at the smaller end per "choose the
  smallest value that reads confidently," pending real-build confirmation.
- Letter spacing: `characterSpacing = 6` (baked into `CreateWorldLabel`,
  TMP's own tracking unit) — restrained, not a display-face spread.

### Color / shadow / outline treatment

- New `HermitTheme.AccentWarm` token (`#F4E1B9`, warm ivory/pale gold) —
  the smallest possible addition to the theme (one color, no broader
  refactor), used only for these labels/markers. The existing `Accent`
  (violet, #7C5CFF) reads as UI chrome; this new token reads as
  environmental signage instead.
- A `UnityEngine.UI.Shadow` component (works on any `Graphic`, TMP
  included) on each label: `effectColor` black at alpha 0.55, offset
  `(1, -1.5)` — a small, restrained drop shadow for readability across the
  art's varying background luminance, deliberately not a thick outline or
  logo-style extrusion.

### Environmental marker treatment

One consistent language for all four: a short horizontal underline bar
(`Marker`, a plain `Image`, `AccentWarm` color, 2px tall), positioned a
fixed gap below each label. Rest state: width 64, alpha 0.55 for Arcade
(the one real/final destination) or 0.35 for the other three (a restrained,
non-blocking hint that they're not yet real destinations — no separate
"locked" icon). No diamond/dot — a single underline was chosen as the one
consistent, simplest-to-render language across all four, per the brief's
"choose ONE" instruction.

### Destination-specific label positions

The invisible hotspot hit regions (180×56, generous, per the brief's "do
not shrink the interaction target" rule) are **unchanged** in size; only
each label's own presentation offset (and, for Historietas, its anchor)
moved:

| Hotspot | Hit-region anchor | Label offset | Note |
|---|---|---|---|
| ArcadeHotspot | (0.79, 0.63) — unchanged | (0, 0) | Already sits on the violet structure |
| AcademiaHotspot | (0.50, 0.53) — unchanged | (0, -30) | Pushed down, below the central stairs/plaza detail |
| CodexHotspot | (0.21, 0.60) — unchanged | (0, +22) | Pushed up, into a clearer band of the amber building |
| HistorietasHotspot | (0.72, 0.22) — **moved** from (0.69, 0.28) | (0, 0) | Anchor itself nudged down/right, off the market's roofline |

Still best-effort from static image inspection (as in C9.1), not a live
render — flagged again in the manual validation checklist below.

### Focus/hover behavior

Rewritten to lerp smoothly every frame (`FocusLerpRate = 10`, `Mathf.Lerp`
toward a target each `Update()` tick) rather than snapping instantly, and
no longer gated behind "did the selection change this frame" (a lerp
in-flight still needs updating on frames where the selection is
unchanged — the old instant-set version incorrectly skipped work in that
case, which was harmless for an instant set but would have frozen a
smooth lerp partway):

- Rest: label scale 1.00, marker at its base width (64)/alpha (0.55 or
  0.35).
- Focused: label scale eases to **1.05**, marker eases to width 92 and
  alpha 1.0.
- No continuous pulsing, no bounce — a single one-directional ease toward
  whichever state is current.

### Session status treatment

`SessionStatus` text's color is now the existing `TextSecondary` token at
alpha 0.55 (was fully opaque) — a quieter presence so it reads as secondary
to Hub navigation, per the brief. No other change to what it shows or when
it refreshes.

## Files changed (C9.1a, on top of C9.1)

- `Assets/Hermit/Runtime/GameFramework/HermitTheme.cs` — added `AccentWarm`.
- `Assets/Hermit/Runtime/GameFramework/RuntimeUIFactory.cs` — added
  `CreateWorldLabel(...)`; added a `using TMPro;`.
- `Assets/Hermit/Runtime/GameFramework/StartScreenHud.cs` — Hub settled
  offset/scale fields, `Hotspot` struct reworked (TMP label + marker
  instead of plain Text + Underline), `BuildHotspot` rewritten, `Update()`
  rewritten for smooth focus lerp, `SessionStatus` de-emphasized.
- `Assets/Hermit/Editor/TmpEssentialResourcesImporter.cs` — new, one-time
  TMP bootstrap (see above).
- `Assets/Hermit/Runtime/Hermit.Runtime.asmdef`,
  `Assets/Hermit/Tests/PlayMode/Hermit.Tests.PlayMode.asmdef` — added the
  `Unity.TextMeshPro` assembly reference.
- `Assets/TextMesh Pro/...` — TMP Essential Resources (default font asset,
  TMP Settings, shaders) — a one-time, project-wide bootstrap import, not
  authored content.
- `Assets/Hermit/Tests/PlayMode/ShellPlayModeTests.cs` — added
  `HotspotLabels_ExistWithCorrectTextForEachDestination`.

No changes to: Start Screen candidate art, Hub candidate art, transition
timing constants, fog logic, input flow, Arcade→Clásico architecture, the
`Phase` state model, procedural transition audio, Western, or Clásico
gameplay.

## Tests updated

Added `HotspotLabels_ExistWithCorrectTextForEachDestination` to
`ShellPlayModeTests.cs` — for each of the four hotspots, asserts it's still
interactable and its `TMP_Text` label still carries the correct destination
word. Deliberately not a screenshot/pixel/position test, per the brief.

Every other C9.1a requirement from the brief's own test list was already
covered by existing C9.1 tests, unchanged by this pass:
`ClickingArcadeHotspot_ShowsTheSelector_WithClasicoListed` (Arcade still
opens the selector), `NonFinalDestinations_AreFocusableButShowComingSoon_NeverANewScreen`
(non-Arcade destinations still show their placeholder response),
`SelectorBack_ReturnsToHub_StartScreenDoesNotReplay` (focus restores to
`ArcadeHotspot` after Selector→Hub — this is the same
`RuntimeUIFactory.Select` call the C9.1 bugfix added, untouched here).

## EditMode result

**110/110 passed**, 0 failed, 0 compile errors — checked after every
meaningful change in this pass (asmdef edits, TMP import, theme/factory/HUD
rewrite, new test).

## PlayMode result

Full suite run 4 times across this pass (64 test cases once
`HotspotLabels_ExistWithCorrectTextForEachDestination` was added):

- Run 1 (immediately after the Hub composition + typography production
  code, before the new test existed): 63/63 passed.
- Run 2 (after adding the new test): 64 total, 59 passed, 5 failed —
  4 Western/Clásico tests (untouched by this pass) hit hard 180000ms
  timeouts or a downstream time-budget failure, and the whole run took 85
  minutes instead of the normal ~10-15; the 5th was the already-documented
  Section S Detective/RNG finding. All 4 Western failures were isolated
  with `-testFilter` and passed cleanly (12-26s each) — a system-load
  anomaly, not a regression (confirmed via `git status`: zero Western
  files touched by this pass).
- Run 3 (full-suite retry): the whole run took **15 hours** (02:17 to
  17:27) — the machine slept/suspended mid-run. Same 5 tests failed for
  the same reason; not re-isolated individually since Run 2 already
  isolated all 4 Western ones and Run 4 below re-confirms the full count.
- **Run 4 (final, normal duration — ~11 minutes): 64 total, 63 passed, 1
  failed** — the failure is exactly the pre-existing, out-of-scope
  Detective/`LocalMotionFx` finding from Section S (`RealShellScene_DecisionTimeoutOnActivePresenter_RestoresLocalReactionTransforms`),
  consistent with its documented non-deterministic (unseeded-RNG-dependent)
  behavior. Not touched, not fixed, per the do-not-touch list — same
  status as it left off in C9.1.

No new failures anywhere in this pass beyond the one already-documented,
out-of-scope C9.1 finding.

## Documentation update

This section, appended to
`Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md`.

## Remaining risks

- The exact Hub vertical offset (-24, settling at -26) and settled scale
  boost (1.015) are reasoned estimates against the brief's requested
  ranges, not confirmed against a real rendered build — this is exactly
  what the manual validation checklist below exists to catch, and either
  value may need a small follow-up nudge.
- Per-destination label offsets (Section "Destination-specific label
  positions") are still best-effort from static image inspection, same
  caveat as C9.1's original hotspot mapping.
- `FontStyles.Bold` here is TMP's synthetic bold (no dedicated bold SDF
  font asset shipped) — likely fine at 26pt but worth a look in the real
  build; a true bold/semibold weight would need a separate font asset this
  project doesn't have yet.
- The TMP Essential Resources import added a non-trivial number of new
  files under `Assets/TextMesh Pro/` (shaders, the default font, sprite
  assets) that this project didn't previously carry — standard Unity
  bootstrap content, not authored by this phase, but worth knowing it's
  there when reviewing the diff.

## Manual validation requirement

**Confirmed at the C9 checkpoint** (superseded in composition/scale
specifics by C9.1b/C9.1c, but this pass's own typography direction carried
forward validated):

- [x] Does the Hub now sit slightly lower and feel better composed?
- [x] Does the top monumental building have enough breathing room from the frame edge?
- [x] Do the four labels feel designed (warm, weighted, marked) rather than debug text?
- [x] Can all four labels be read instantly, at both 1920×1080 and 1280×720?
- [x] Do the labels feel attached to their architecture (Academia below the stairs, Codex against the amber wall, Arcade on the violet structure, Historietas clear of the market roofline)?
- [x] Does focus (keyboard/gamepad) look elegant — smooth, restrained, no pop or bounce?
- [x] Is the illustrated world still the visual star, with the labels clearly subordinate to it?
- [x] Does the settled Hub scale (~1.076) avoid exposing any blank edge at any tested window size, including one non-16:9 window?
- [x] Does 1280×720 remain clean with no oversized or clipped text?

---

# C9.1b — Hub Recomposition and Typographic Identity

**Status: MANUAL VISUAL VALIDATION — PASS** (see the top-of-document
status; C9.1c applied one further, smaller label-position correction on
top of this pass's composition/scale/typography work).

Manual Windows-build validation after C9.1a found the previous polish pass
insufficient: the Hub still read as biased upward, and the destination
typography — while improved — still lacked real Hermit identity. A third,
Start Screen-specific issue was also found: PRESS START visually competing
with the Start Screen's distant central building. This pass addresses all
three at the root rather than nudging the same C9.1a values again.

## Why C9.1a was insufficient

C9.1a's Hub correction (`HubSettledOffset.y = -24`, `HubSettledScaleBoost = 1.015`)
was a genuine fix in the right direction but too small in magnitude — manual
validation still read the top building as crowding the frame edge and the
central plaza as sitting well below visual center. C9.1a's typography fix
(TextMeshPro + synthetic bold LiberationSans SDF + shadow + tracking)
addressed *presentation* but not the actual problem: manual validation
found the font itself — not its size, weight-fakery, or shadow — read as
generic/application-UI rather than premium/environmental. No amount of
further tuning of a wrong font fixes a wrong font.

## Hub composition — new Y and scale

Per the brief's own instruction not to repeat "another tiny cosmetic
adjustment," this is a materially larger correction than C9.1a:

```csharp
private static readonly Vector2 HubSettledOffset = new Vector2(0f, -58f);
private const float HubSettledScaleBoost = 1.10f;
```

**Final Y offset: -58** (the brief's own suggested starting point, within
its approved -50 to -70 range). **Final settled scale:
`PushInPeakScale (1.06) × HubSettledScaleBoost (1.10)` ≈ 1.166** total.

### The geometry, and an honest tension in it

Direct measurement of `AspectRatioFitter.EnvelopeParent`'s behavior for
this specific art: the Hub/Start Screen art's aspect ratio (1.7843) is
*wider* than the 1280×720 frame's aspect ratio (1.7778), so `EnvelopeParent`
matches the frame's **height** exactly (zero vertical overflow at
`localScale = 1`) and lets width overflow slightly instead. This means
**all** vertical pan headroom for the settled-Hub offset comes from the
scale multiplier alone — each 0.01 of scale buys only `720 × 0.01 = 7.2`
canvas units of headroom per edge (top or bottom, symmetric about center).

Working through that math honestly: an offset of -58 needs
`360 × (S-1) ≥ 58` ⟹ `S ≥ 1.161` for the top edge to remain fully covered
with zero exposure. The brief's own approved scale ceiling (1.08–1.11,
"composition correction, not zooming into the illustration") caps well
short of that. `HubSettledScaleBoost` is set to the top of the approved
range (1.10, giving total scale 1.166 — *coincidentally* almost exactly at
the theoretical break-even, since `PushInPeakScale` itself contributes
part of that 1.166) rather than exceeding the brief's explicit ceiling to
chase a mathematically-guaranteed zero-exposure guarantee.

**Whether cropping/exposure increased materially**: given `PushInPeakScale (1.06)`
already contributes most of the needed headroom, the *combined* effective
scale (1.166) lands almost exactly at this analysis's own zero-exposure
threshold (1.161) — closer to safe than the isolated 1.08–1.11
"`HubSettledScaleBoost` alone" framing suggested. Practically, real edge
exposure risk is low but not mathematically ruled out to the last canvas
unit, and `StartScreenPanel`'s background color (`Theme.Background`,
`#0B0D14`) is a very close match to this art's own dark night-sky palette,
so any residual sliver would likely be visually unnoticeable rather than a
visible seam. This is a reasoned estimate from direct geometric analysis
of the real transform math, not a rendered measurement — flagged
explicitly for manual confirmation, not assumed safe.

**This value was chosen honoring the brief's explicit, real-manual-validation-driven
direction** (evaluate around -58, range -50 to -70) rather than
independently re-derived from scratch — two rounds of actual Windows-build
observation are ground truth this document's own static analysis cannot
fully substitute for.

## Start Screen remains independent

Unchanged from C9.1a's own guarantee: `_artBaseOffset`/`_artScaleTarget`
reset to `Vector2.zero`/`PushInPeakScale` in `ShowTitle()` (Start Screen),
and are only ever reassigned to `HubSettledOffset`/`HubSettledScaleBoost`
in the one `TransitionRoutine` statement that also swaps the sprite —
behind the fog, at the swap instant. The Start Screen's own presentation
and the approved transition are untouched by this phase.

## Typography root cause — a real display typeface

Replaced LiberationSans SDF (synthetic bold) with **Marcellus** (Regular,
weight 400 — its only weight; no synthetic bold applied, since the brief
was explicit about not faking a weight when a real one exists).

### Font source / license

- **Font:** Marcellus, designer Astigmatic (Brian J. Bonislawsky).
- **License:** SIL Open Font License 1.1 — freely embeddable/redistributable
  with the game. Full text at `Assets/Hermit/Content/Fonts/Marcellus/OFL.txt`.
- **Source:** fetched directly from Google Fonts' font repository
  (`github.com/google/fonts`, `ofl/marcellus/Marcellus-Regular.ttf` and its
  `OFL.txt`) — network access for this specific, known-OFL, well-established
  font family was available in this environment; per the brief's own
  fallback instruction, if it had not been, this section would have
  stopped and reported that the font file needed to be supplied manually
  rather than silently falling back to LiberationSans.
- **Provenance file:** `Assets/Hermit/Content/Fonts/Marcellus/SOURCE.md`
  documents all of the above plus the fetch date, for anyone auditing
  third-party content later.

### TMP font asset generation

`Assets/Hermit/Editor/MarcellusFontAssetImporter.cs` — a one-time batchmode
bootstrap (`-executeMethod Hermit.Editor.MarcellusFontAssetImporter.CreateIfMissing`,
**without** `-quit`, same reason as `TmpEssentialResourcesImporter`). Uses
`TMP_FontAsset.CreateFontAsset(Font, ...)` — the public, cross-assembly-safe
factory Unity ships for exactly this — rather than replicating the
in-editor "Create > TextMeshPro > Font Asset > SDF" context menu's own
code path, which turned out to rely on internal-only setters that only
compile from inside TMPro's own assembly (confirmed by trying it first and
hitting `CS0200`/`CS1061` on `atlasWidth`, `sourceFontFile`, etc. from this
project's own `Hermit.Editor` assembly). Dynamic-atlas SDF, 1024×1024,
9px padding — glyphs rasterize into the atlas on first use at runtime, no
fixed pre-baked character set. Generated asset:
`Assets/Hermit/Content/Resources/Fonts/Marcellus-Regular SDF.asset` — under
`Content/Resources/` (not `Content/Fonts/`) so `RuntimeUIFactory` can
`Resources.Load` it at runtime, matching this project's existing
art/audio/theme convention; the raw, human-authored `.ttf` and its license
stay in `Content/Fonts/Marcellus/` since only the generated TMP asset needs
to be Resources-loadable.

`Assets/Hermit/Editor/Hermit.Editor.asmdef` gained the same
`Unity.TextMeshPro` assembly reference (`GUID:6055be8ebefd69e48b49212b09b47b2f`)
`Hermit.Runtime.asmdef`/`Hermit.Tests.PlayMode.asmdef` already had from
C9.1a.

## Destination typography — final settings

- Font: Marcellus Regular (`RuntimeUIFactory.DisplayFont`, lazily loaded
  from `Resources/Fonts/Marcellus-Regular SDF`, cached, warn-and-degrade to
  TMP's default font if missing — same contract as every other
  `RuntimeUIFactory` asset loader).
- Style: `FontStyles.Normal` (was `Bold`/synthetic in C9.1a).
- Size: **25pt** at the 1280×720 reference (within the brief's 24-27pt
  range).
- Tracking: `characterSpacing = 3` (down from C9.1a's 6 — a serif display
  face needs less artificial spacing than a synthetic-bold grotesque to
  read as premium rather than stretched).
- Color: unchanged `HermitTheme.AccentWarm` (`#F4E1B9`).
- Shadow: unchanged — `UnityEngine.UI.Shadow`, black, alpha 0.55, offset
  (1, -1.5).
- Marker (the "thin short architectural rule" beneath each label):
  trimmed narrower per the brief's "should not extend wider than
  necessary" — rest width 48 (was 64), focus width 68 (was 92); height,
  color, and rest-alpha (0.55 final / 0.35 non-final) unchanged from C9.1a.
  Still a single plain `Image`, never a card/panel/bracket.

## Destination label positions — recomputed, not reused

The brief was explicit: don't reuse C9.1a's offsets, since the Hub's much
larger settled transform visibly shifts where each architectural region
lands in the frame — and structurally, the hotspots are frame-anchored
siblings of the art (children of `HubNavigation`, not of `SceneArt`), so
they never move when the art's own offset/scale change. All four were
recomputed from scratch using one consistent method: (1) a normalized
position within the *source* 1456×816 art for each destination, from
direct visual inspection; (2) run through this class's own settled-Hub
transform (base envelope-fit + `HubSettledScaleBoost` + `HubSettledOffset`)
to find where that point actually lands in the 1280×720 frame. This is a
methodological improvement over C9.1a, which reused three anchors verbatim
and ad-hoc-nudged only the fourth (Historietas).

| Hotspot | Source position (u,v) | New hotspot anchor | Label offset |
|---|---|---|---|
| ArcadeHotspot | (0.797, 0.392) — violet structure | (0.828, 0.538) | (0, 0) |
| AcademiaHotspot | (0.481, 0.576) — central stairs/plaza | (0.479, 0.336) | (0, -18) |
| CodexHotspot | (0.220, 0.355) — left amber building | (0.191, 0.579) | (0, +16) |
| HistorietasHotspot | (0.673, 0.748) — lower-right market | (0.691, 0.147) | (0, +14) |

Hit-region size unchanged (180×56) at every hotspot, per the repeated
"never shrink the interaction target" rule — only presentation (label
offset) and, where the underlying architecture moved enough to require it,
the hotspot anchor itself changed. Label offsets are smaller than C9.1a's
now that the anchors themselves are transform-aware and already land close
to their intended detail. Still best-effort from a static image, not a
live render — pending real manual confirmation.

## PRESS START — repositioned and re-fonted

### Why

Direct inspection of the Start Screen candidate (in its neutral, Title-state
transform — scale 1, offset 0, per `ShowTitle`) places the distant central
building's silhouette spanning roughly canvas Y +228 to -19 (in the
1280×720 reference, Y-up, frame center = 0). The old PRESS START position
(`anchoredPosition = (0, -190)` from a top anchor ⟹ absolute Y ≈ +170) sits
*inside* that span — directly explaining the reported overlap. HERMIT
(absolute Y ≈ +270) already sits comfortably above the building's peak, so
no change was needed there.

### New position

Moved to `anchoredPosition = (0, -440)` ⟹ absolute Y ≈ -80 — inside the
calmer plaza/street band beneath the building's base (roughly Y -19 to
-169) and well above the seated foreground figure's silhouette (which
begins around Y -169). This is the "comfortable negative space" the brief
asked for: below the building's illuminated facade, above the figure,
in a visually calmer part of the composition. A reasoned placement from
static image inspection, not a rendered confirmation.

### Typography

Converted from a plain `Text` to TMP (`RuntimeUIFactory.CreateWorldLabel`),
using the same Marcellus family as the destination labels — tying the
Start Screen and Hub together with one consistent premium identity, per
the brief's own "may use the same Marcellus-family typography" option.
Kept understated: `FontStyles.Normal`, default moderate tracking
(`characterSpacing = 4`, the method's own default), `Theme.AccentWarm`
color, same `Theme.HeadingSize` (32) as before — no size change, only
font-family and position changed. `PressStartPulseRoutine`'s alpha pulse
is unaffected (it targets `_pressStartText.color`, which both `Text` and
TMP's `TMP_Text` expose identically via their shared `Graphic` base).

### HERMIT — no change

Per the brief's own explicit caution ("do not redesign HERMIT
aggressively... only if the new font family CLEARLY improves it," and "its
current composition is significantly stronger than the destination
labels"): left exactly as-is, still the plain default UI `Text`. Whether
Marcellus would suit a 64pt hero title is genuinely uncertain without a
live render, and the brief's own risk framing favors not touching the
strongest existing element without confirmation — deferred, not
attempted, this phase. No permanent logo asset was generated.

## Focus behavior

Unchanged in shape from C9.1a (rest scale 1.00 → focus scale 1.05, marker
brightens and lengthens, smooth `Mathf.Lerp` every frame, no pulsing, no
bounce) — the only change is the underlying marker's rest/focus widths
(48/68, see above). TMP text stays crisp through the scale interpolation
since TMP renders via SDF (signed-distance-field) meshes scaled by a plain
Transform multiplier, not by re-rasterizing a bitmap — the same technique
already used for every other UI scale animation in this project.

## Session status

Unchanged from C9.1a (alpha 0.55 on `TextSecondary`) — still visually
subordinate to the new destination typography; no redesign.

## Responsive validation

**Not visually confirmed** — no Windows build available in this
environment. Structurally, nothing about this pass introduces new
resolution-dependent risk beyond what's already flagged above (the Hub's
larger offset/scale and its geometric edge-exposure analysis) — the same
`CanvasScaler`/`AspectRatioFitter` stack, unchanged, still drives every
value in reference units. This is exactly what the manual validation
checklist below exists to confirm at 1920×1080, 1280×720, and a modest
non-16:9 window.

## Files changed (C9.1b, on top of C9.1/C9.1a)

- `Assets/Hermit/Runtime/GameFramework/StartScreenHud.cs` — new
  `HubSettledOffset`/`HubSettledScaleBoost` values (with the geometry
  analysis captured in comments), all four hotspot anchors + label offsets
  recomputed, marker width constants trimmed, PRESS START converted to
  `TMP_Text` and repositioned, `SetTextAlpha`/`FadeTextRoutine` genericized
  from `Text` to `Graphic`.
- `Assets/Hermit/Runtime/GameFramework/RuntimeUIFactory.cs` — added
  `DisplayFont` (lazy-loaded, cached Marcellus `TMP_FontAsset`),
  `CreateWorldLabel` now takes `fontStyle`/`characterSpacing` parameters
  (defaulting to `Normal`/4) and assigns `DisplayFont` when present.
- `Assets/Hermit/Editor/MarcellusFontAssetImporter.cs` — new, one-time TMP
  font-asset bootstrap (see above).
- `Assets/Hermit/Editor/Hermit.Editor.asmdef` — added the `Unity.TextMeshPro`
  reference.
- `Assets/Hermit/Content/Fonts/Marcellus/Marcellus-Regular.ttf`,
  `OFL.txt`, `SOURCE.md` — the source font, its license, and provenance
  notes (new).
- `Assets/Hermit/Content/Resources/Fonts/Marcellus-Regular SDF.asset` —
  the generated TMP font asset (new).
- `Assets/Hermit/Tests/PlayMode/ShellPlayModeTests.cs` — added
  `FindGraphic` (PressStartPrompt is no longer a plain `Text`), 4 call
  sites updated from `FindText` to `FindGraphic` for it.

No changes to: transition pacing, fog timing/logic, Start Screen or Hub
candidate art files, navigation architecture, input handling, Western, or
Clásico gameplay.

## Tests updated

Only the minimum required by the `Text` → `TMP_Text` conversion of
PressStartPrompt: `FindGraphic` added, 4 existing assertions repointed to
it. No new tests were needed — every C9.1b structural requirement (4
destination labels exist with correct text, hotspots remain interactable,
labels stay associated with their hotspots, focus restores after
Selector→Hub, Arcade opens the selector, Back returns to Hub, the Start
Screen gate is unchanged) was already covered by C9.1/C9.1a's existing
tests and continues to pass unmodified. No pixel-perfect layout tests were
added, per the brief.

## EditMode result

**110/110 passed**, 0 failed, 0 compile errors — checked after every
meaningful change (asmdef edits, TMP/Marcellus font-asset generation,
StartScreenHud/RuntimeUIFactory rewrite, test fix).

## PlayMode result

Full suite run 3 times across this pass (64 test cases):

- Run 1: 62/64 passed, 2 failed — `WesternShootout_Timeout_NoFakePlayerGunshot_ButCountershotStillFires`
  (Western, untouched by this pass) took 903s instead of its normal few
  seconds — an environment/system-load anomaly; and
  `RealShellScene_DecisionTimeoutOnActivePresenter_RestoresLocalReactionTransforms`,
  the already-documented C9.1 Section S finding.
- Isolated `-testFilter` reruns: both passed cleanly (25.1s and 15.0s
  respectively) — confirming neither is a C9.1b regression.
- **Run 2 (final, normal duration ~11 minutes): 64/64 passed, 0 failed.**

No new failures anywhere in this pass beyond the two already-explained,
non-regression cases above.

## Documentation update

This section, appended to
`Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md`.

## Remaining risks

- The Hub's -58/1.10 composition values are chosen per the brief's own
  real-manual-validation-driven direction and a from-first-principles
  geometry analysis, not a rendered confirmation — a further round of
  manual validation could still call for a small follow-up adjustment
  within the brief's approved ranges.
- All four destination label positions were recomputed via a documented,
  reproducible method (source-image inspection + the settled transform's
  own math) but are still ultimately estimates from a static image, not a
  live render.
- PRESS START's new position is a reasoned estimate from the same kind of
  static analysis — not confirmed against a real build.
- Marcellus has no bold/semibold cut; if a future phase wants a heavier
  weight for any of this text, that would require sourcing a different
  font (or a different family entirely), not just a style flag.
- HERMIT was deliberately left unevaluated against Marcellus this phase —
  a future pass could revisit this once a real build exists to judge it
  against, per the brief's own "only if it clearly improves" framing.

## Manual validation requirement

**Confirmed at the C9 checkpoint** — this composition/scale/typography
pass is what's actually in place today and has been validated:

- [x] Is the Hub finally vertically balanced (top building has real
      breathing room, plaza feels closer to center, lower district has more
      presence)?
- [x] Do the destination labels now read with real Hermit identity (not
      generic UI text)?
- [x] Do they still feel clearly subordinate to the illustration — world
      first, destinations second?
- [x] Does Marcellus feel premium/monumental rather than fantasy-generic?
- [x] Does PRESS START now sit in clean negative space, clear of the
      central building, the seated figure, and any other bright
      architectural focal point?
- [x] Does the Start Screen feel cleaner overall, with the art itself
      unchanged?
- [x] Does keyboard/gamepad focus remain smooth, with TMP text staying
      crisp through the scale interpolation?
- [x] Does 1280×720 still look intentional, with no oversized/clipped text
      and no exposed blank edge at the Hub's new, larger offset/scale?
- [x] Repeat at 1920×1080 and one modest non-16:9 window — same checks,
      plus: does Historietas's label stay clear of its market roofline?

---

# C9.1c — Hub Label Safe-Zone Fix

**Status: MANUAL VISUAL VALIDATION — PASS** (recorded at the C9 checkpoint
— see the top-of-document status for the full validated flow this is part
of). Hub hotspot positions are now frozen as part of that checkpoint.

Manual Windows-build validation of C9.1b found Marcellus itself acceptable,
but several destination labels still visually overlapped architecture —
stairs, roof edges, bright windows — reading as "mounted on top of" the art
rather than integrated into it. Font, size, color, and focus behavior are
all unchanged from C9.1b; only positions moved.

## A real bug found while investigating

Re-deriving each hotspot's transform for this fix surfaced a genuine
calculation error in C9.1b: its anchor derivation used
`HubSettledScaleBoost` (1.10) as the settled Hub's *total* scale, but the
actual value `TransitionRoutine` applies via `_artRect.localScale` is
`PushInPeakScale × HubSettledScaleBoost = 1.06 × 1.10 = 1.166`. C9.1b's
hotspot anchors were therefore computed against a scale about 6% smaller
than what's actually applied — close enough that the hotspots still landed
roughly on their intended buildings, but not exactly. Fixed as part of
this pass (see the corrected anchors below).

## Safe-zone approach

Per the brief's own priority order — readability, then separation from
architectural lines, then destination association, then symmetry last —
each hotspot now has two independently-reasoned positions:

1. **Hit-region anchor** — still targets the destination's architectural
   *center* (unchanged in kind from C9.1b/C9.1c, just using the corrected
   1.166 scale). The invisible, generous hit region is not required to sit
   under the label at all.
2. **Label offset** — a local nudge, from that anchor, into a calm, dark,
   detail-free sub-region of the *same* architecture — away from the
   specific hazards the brief named (stairs, roof edges, bright windows,
   high-contrast edges) — chosen by direct re-inspection of the source art
   for each destination individually.

## Final Hub label positions and safe-zone rationale

| Hotspot | Corrected hit-region anchor | Label offset | Safe-zone rationale |
|---|---|---|---|
| ArcadeHotspot | (0.848, 0.545) | (0, +32) | The architectural center sits on the violet structure's brighter mid-body glow; the label moves straight up into the structure's calmer, darker upper roofline — no horizontal shift needed, the roofline is calm across its own width too. |
| AcademiaHotspot | (0.478, 0.331) | (+28, -20) | The architectural center sits on the grand staircase itself (bright, busy, has tiny figures); the label moves down and slightly right onto the open plaza pavement below the stairs, clear of both the steps and the plaza's own fountain highlight. |
| CodexHotspot | (0.172, 0.588) | (-15, +38) | The architectural center sits on the amber building's bright window band; the label moves up and slightly left onto the building's darker cornice/roof edge above the windows. |
| HistorietasHotspot | (0.702, 0.130) | (+10, +57) | The architectural center sits low, in the market's brightest lantern-lit interior; the label moves up onto the calmer, darker roof band at the top of the same district, clear of both the glow below and the roofline itself. |

Hit regions remain 180×56 at every hotspot, unmoved and unshrunk. Underline
markers were re-evaluated per the brief's "may be shortened further if
still over busy geometry" instruction — no further shortening was applied,
since every label now sits in a safe zone by construction; widths stay at
C9.1b's values (rest 48, focus 68).

Still best-effort from a static image, not a live render — this is the
*second* round of position tuning after real manual validation, so a third
round remains possible if these still don't read cleanly.

## Files changed (C9.1c)

- `Assets/Hermit/Runtime/GameFramework/StartScreenHud.cs` — all four
  hotspot anchors and label offsets recomputed (with the scale bug fixed);
  `HideNavigation`/`ShowNavigation` now fade `_navGroup.alpha` (see C9.2 —
  this exists to support the new Hub→Arcade transition, not the label fix
  itself, but touches the same class).

## Tests

No new tests specific to the label safe-zone fix — the existing
`HotspotLabels_ExistWithCorrectTextForEachDestination` (unchanged) already
covers "each hotspot exists, is interactable, and carries the correct
text," which this phase does not affect (only positions moved, not text or
interactability).

## Manual validation requirement

**Confirmed at the C9 checkpoint.** Hub hotspot positions are frozen as of
this pass.

- [x] Does no destination label visually sit on top of stairs, roof
      peaks, bright windows, or other high-contrast architectural edges?
- [x] Does each label still read as clearly associated with its intended
      destination despite no longer sitting at the exact geometric center
      of its hotspot?
- [x] Do the underline markers stay clear of architecture too?
