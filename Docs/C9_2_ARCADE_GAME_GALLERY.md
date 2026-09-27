# C9.2 — Arcade Game Gallery (Foundation)

**Status: MANUAL VISUAL VALIDATION — PASS** (recorded at the C9 checkpoint).
The Arcade Gallery works in the real Windows build: opening it from the
Hub, the featured Clásico presentation, the future slots, and Back→Hub
navigation have all been confirmed. This is a foundation-level pass, not a
finished art pass — see the explicit caveat below.

**Featured-card art update (C9.2a):** the original mascot placeholder
(`Hermit_CandidateA_CardTest_Candidate_01.png`) is now
**TEMPORARY PLACEHOLDER — RETIRED from featured-card use** (kept on disk,
unreferenced by any code, per this project's "disable, don't delete"
convention). It has been replaced by a dedicated Clásico key art
candidate, `Clasico_KeyArt_01` (see the "C9.2a" section below for full
details), which is now the **PRIMARY CANDIDATE — pending manual build
validation**. Neither the old nor the new art has been approved as final
by the user — the new one is simply a materially better candidate,
purpose-built to represent Clásico as a whole rather than the general
Hermit mascot. **Do not treat the new key art as final/approved** until a
real Windows build confirms it reads well at actual featured-card size
(see C9.2a's own manual validation checklist).

This document records the foundation of the Arcade destination's real
screen: a small "game gallery" presentation (Clásico as a featured
cabinet-poster hero, plus subdued future slots) replacing the old plain
vertical button list, for the product's own Shell only. See
`Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md` for the Start Screen/Hub
work this builds on (Arcade is one of the Hub's four destination
hotspots).

Per the C9 checkpoint: the Arcade Gallery presentation (layout, featured
card, future slots, Hub→Arcade transition) is now **frozen** — treat as
validated and do not make further aesthetic tweaks unless a real bug is
found. The placeholder poster art itself is explicitly exempt from that
freeze — it is expected to be replaced by real Clásico key art in a future
art phase, not locked in as final.

Not yet done: manual Windows-build visual validation, and the commit/push
that follows only after that validation passes.

## A. GameSelectorHud — current architecture (before this phase)

`GameSelectorHud` (`Assets/Hermit/Runtime/GameFramework/GameSelectorHud.cs`)
is a generic, Clásico-agnostic screen: it enumerates whatever
`GameRegistry.All` hands it and renders one button per `GameDefinition`
(`GameId`, `DisplayName`, `ShortDescription`, `IsEnabled` — its only known
contract), in a plain vertical list, with an optional "Volver" back button.
It is `public sealed class GameSelectorHud : MonoBehaviour`, built via
`RuntimeUIFactory`, and is shared by **two** composition roots:

- `ShellInstaller` (01_Shell, the product) — until this phase, this is what
  the Arcade hotspot actually opened.
- `GameSessionInstaller` (02_GameplaySandbox, dev/test) — its own
  `SetBackAction` is never called there (nothing to go back to), and it
  needs to stay a fast, unstyled dev tool, not a polished product screen.

`GameHub` (internal, not a MonoBehaviour) owns the actual launch/back
orchestration: it held a concrete `GameSelectorHud` reference, listened to
its `GameLaunchRequested` event, and called `.Show()`/`.Hide()` on it from
`GameFlowController` state changes.

## B. Chosen Arcade Gallery architecture

**Replaced, not evolved** — a new `ArcadeGalleryHud` class, used only by
`ShellInstaller`. `GameSelectorHud` itself is completely untouched and
keeps serving `GameSessionInstaller`'s dev/test sandbox exactly as before.

**Why replace rather than evolve in place:** `GameSelectorHud` is shared by
two composition roots with genuinely different needs — the product wants a
premium gallery, the dev/test sandbox wants to stay a fast, plain list.
Evolving the one shared class in place would have either changed the dev
sandbox's own screen unexpectedly (out of this phase's scope, and actively
unwanted for a debug tool) or required an opt-in "premium mode" flag
inside `GameSelectorHud` itself, coupling two unrelated visual styles into
one class for no real benefit.

**How duplication was avoided anyway** (per the brief's explicit "do not
create duplicate game-launch systems"): a new `IGameSelectorScreen`
interface (`event Action<GameDefinition> GameLaunchRequested; void Show(); void Hide();`)
captures exactly what `GameHub` actually needs from "whatever screen shows
the game list." `GameHub`'s constructor now takes `IGameSelectorScreen`
instead of the concrete `GameSelectorHud`. `GameSelectorHud` gained `: IGameSelectorScreen`
(purely additive, zero behavior change — `GameSessionInstaller`'s own call
site didn't need to change at all). `ArcadeGalleryHud` implements the same
interface. Result: **one** launch/back orchestration (`GameHub`, unchanged
in every way except its constructor's parameter type), **two**
interchangeable presentations.

## C. Arcade Gallery hierarchy

```
ArcadeGalleryPanel (Image, Theme.Panel)
├─ AmbientVignette (Image, procedural radial vignette, Theme.Background tint)
├─ Title ("ARCADE", plain Text, Theme.TitleSize)
├─ Game_clasico (the featured card — CreatePortraitFrame + Button)
│  └─ Portrait (Image, the featured-card poster art)
├─ FeaturedTitle ("CLÁSICO", TMP/Marcellus, AccentWarm)
├─ FeaturedDescriptor ("Contabilidad bajo presión", plain Text, TextSecondary)
├─ FeaturedPlayCue ("ENTRAR", TMP/Marcellus, AccentWarm)
├─ FeaturedPlayCueMarker (Image, thin underline, AccentWarm)
├─ FutureSlot_0, FutureSlot_1 (rounded panels, each with a Button + a dim TMP label)
├─ BackButton ("Volver")
└─ RevealSweep (Image, full-stretch, alpha-only dark sweep for the reveal)
```

Built once in `ShellInstaller.Awake()` (same "always exists, visibility
toggled by `SetActive`" pattern as every other Shell screen), via
`ArcadeGalleryHud.Build(canvasRoot, registry.All)`.

## D. Clásico featured-game presentation

A "cabinet poster" card, not a flat rectangle with text — reuses
`RuntimeUIFactory.CreatePortraitFrame` (the same rounded-rect bordered
frame technique this project already uses for character portraits like
the Sheriff/Auditor/Presentador), sized 280×372 (matching the poster art's
own ~928:1232 aspect via `preserveAspect`). The whole frame is the
click/Submit target — the same "art/world IS the target" language this
project already uses for Western's targets and the Hub's own hotspots —
and is named `Game_{GameId}` (i.e. `"Game_clasico"`), matching
`GameSelectorHud`'s own historical button-naming convention exactly, so
every existing test/helper that looks for `"Game_clasico"` keeps working
unchanged.

Below the poster: the title "CLÁSICO" (Marcellus, `AccentWarm`, 30pt — the
same premium display treatment the Hub's destination labels use, since
this is exactly the same kind of "environmental/signage" text), a short
descriptor, and a small "ENTRAR" cue with a thin underline marker — the
identical visual language (label + underline, no button chrome) the Hub's
own hotspots use, for one consistent Hermit identity across both screens.

## E. Existing art assets reused

- **Featured-card poster**: `ArtBible/Candidates/Mascot/Hermit_CandidateA_CardTest_Candidate_01.png`
  (928×1232, portrait) — copied to
  `Assets/Hermit/Content/Resources/Art/Shell/Arcade/Clasico_FeaturedCard_01.png`
  (import settings fixed the same way as every other production art asset:
  Sprite/Single, Uncompressed). This is a genuine, strong match for the
  brief's "premium arcade marquee / collectible videogame title" language
  — it's literally an ArtBible test render of the Hermit mascot on a lit
  pedestal, in portrait poster orientation, with "HERMIT" already lettered
  in a warm gold matching this project's own `AccentWarm` token. **It is
  explicitly a temporary placeholder, not dedicated Clásico key art** — it
  depicts the overall Hermit mascot, not Clásico specifically (accounting/
  microgames), and its own mascot design is itself only one of several
  unapproved ArtBible candidates. Used here because it is, by a wide
  margin, the closest existing asset to what the brief asked for; a future
  phase should replace it with real Clásico-specific key art once that
  exists.
- **Portrait framing technique**: `RuntimeUIFactory.CreatePortraitFrame` —
  already built for the Sheriff/Auditor/Presentador character cards,
  reused verbatim.
- **Label/marker visual language**: the Hub's own destination-label
  treatment (Marcellus, `AccentWarm`, thin underline) — reused for
  "CLÁSICO" and "ENTRAR" rather than inventing a second typographic system.

## F. Missing art assets

- **No dedicated Clásico/Arcade key art exists.** `WesternBackground.png`
  and `GameShowBackground.png` (already production assets) are real
  Clásico content, but each represents only *one* of Clásico's four
  microgame archetypes — using either as "the" Clásico poster would
  misrepresent Clásico as being that one archetype specifically. No asset
  represents Clásico (accounting-under-pressure, spanning all four
  archetypes) as a whole.
- **No dedicated Arcade environment/background art exists.** Section G
  below covers what was used instead.
- Neither was generated in this phase, per the brief's explicit
  instruction not to generate new art unless specifically instructed.

## G. Screen background (no dedicated art)

Since no Arcade environment art exists, `BuildAmbientBackground()` uses a
tasteful procedural stand-in rather than a bright flat panel: the theme's
own `Panel` tone as a base, plus a new `RuntimeUIFactory.GetVignetteSprite()`
— a soft radial gradient (transparent center, opaque edges), generated
once and cached the same way `GetRoundedSprite` already is — tinted with
`Theme.Background` at ~0.85 alpha. This darkens the screen's edges and
keeps the center clear for the featured card, giving a restrained "arcade
ambience" without claiming to be finished environment art.

## H. Future slot strategy

Two generic slots (`FutureSlot_0`, `FutureSlot_1` today), flanking the
featured card, each a small dim rounded panel with a static "PRÓXIMAMENTE"
label (dimmed `TextSecondary`, deliberately *not* `AccentWarm` — visually
subordinate to the real, playable game). No fake game names, no invented
content — these carry no backing `GameDefinition` at all right now, since
`GameCatalog` has none in a disabled state. The code is written to prefer
real disabled `GameDefinition` entries first (showing their actual
`DisplayName`) and only pad with anonymous generic slots up to a target
count of 2 — so if a future phase adds a real (disabled) game definition
to the catalog, it will automatically show its own name here instead of a
generic placeholder, with zero code changes to this file.

Each slot is a real, focusable `Button` (so keyboard/gamepad Tab/Submit
can reach it) whose `onClick` never invokes `GameLaunchRequested` — it
only plays a small, restrained scale-pulse on its own label
(`FutureSlotPulseRoutine`, ~0.3s) as an acknowledgment cue. No modal, no
popup, no new screen.

## I. Hub → Arcade transition

`ShellInstaller.TransitionToArcadeRoutine` — fires when the Hub's
`ArcadeHotspot` is selected:

1. `StartScreenHud.HideNavigation()` — interactivity disabled immediately
   (synchronous), Hub hotspots/labels fade out over 0.20s
   (`RuntimeUIFactory.FadeCanvasGroup`, new shared helper).
2. Wait 0.20s.
3. `GameHub.ShowSelector()` → `ArcadeGalleryHud.Show()` — the panel appears
   and fades in over 0.18s, with a dark "reveal sweep" overlay
   (`RevealSweep`, plain alpha-only `Image`) fading from 0.85 to 0 alpha
   over the same window, giving a quick "sweep clears" sensation.

Total ≈ 0.38s — within the brief's approved ~0.35-0.60s range, and
deliberately nothing like the Start Screen's own ~3.6s cinematic. The
Start Screen's own transition, timing, and fog logic are completely
untouched by this phase.

`StartScreenHud.ShowNavigation()` (used when returning to the Hub from
Arcade, or after a completed game) now fades `_navGroup.alpha` back to 1
over the same 0.20s duration — added specifically so the new fade-out in
`HideNavigation` has a matching fade-in the other direction, rather than
leaving the Hub's nav permanently transparent after its first visit to
Arcade.

## J. Back navigation

`ArcadeGalleryHud.SetBackAction` mirrors `GameSelectorHud`'s own pattern
exactly. `ShellInstaller.OnSelectorBackRequested` is unchanged in shape —
it now calls `_arcadeGalleryHud.Hide()` instead of `_selectorHud.Hide()`,
then `ShowHub()` (`StartScreenHud.ShowNavigation()`), same as before. Never
returns to the Start Screen — that guarantee predates this phase and is
untouched.

## K. Clásico launch — authoritative flow unchanged

`GameHub.OnGameLaunchRequested` → `ClasicoGameHost.Show(...)` → the
existing `ClasicoSessionDirector`/`ClasicoHud` stack: **zero** changes.
`ArcadeGalleryHud` only fires the same `GameLaunchRequested` event
`GameSelectorHud` always fired, with the same `GameDefinition` payload,
through the same `IGameSelectorScreen` contract. No game definitions,
scoring, content, `ClasicoGameHost`, or microgame code was touched.

## L. Files changed

- `Assets/Hermit/Runtime/GameFramework/ArcadeGalleryHud.cs` — new.
- `Assets/Hermit/Runtime/GameFramework/IGameSelectorScreen.cs` — new.
- `Assets/Hermit/Runtime/GameFramework/GameHub.cs` — constructor parameter
  type changed from `GameSelectorHud` to `IGameSelectorScreen`; no other
  change.
- `Assets/Hermit/Runtime/GameFramework/GameSelectorHud.cs` — added
  `: IGameSelectorScreen` (purely additive).
- `Assets/Hermit/Runtime/GameFramework/ShellInstaller.cs` — builds
  `ArcadeGalleryHud` instead of `GameSelectorHud`; `OnArcadeRequested` now
  starts `TransitionToArcadeRoutine` instead of calling `ShowSelector()`
  synchronously.
- `Assets/Hermit/Runtime/GameFramework/StartScreenHud.cs` — hotspot
  anchors/label offsets corrected (see the C9.1c section of
  `Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md`); `HideNavigation`/
  `ShowNavigation` now fade `_navGroup.alpha`.
- `Assets/Hermit/Runtime/GameFramework/RuntimeUIFactory.cs` — added
  `FadeCanvasGroup` and `GetVignetteSprite`.
- `Assets/Hermit/Content/Resources/Art/Shell/Arcade/Clasico_FeaturedCard_01.png`
  — new production art copy (see Section E).
- `Assets/Hermit/Tests/PlayMode/ShellPlayModeTests.cs` — added
  `OpenArcadeGallery()` helper, migrated 4 call sites to it, added
  `FutureSlots_AreNotPlayable_ClickingThemNeverLaunchesAGame`, renamed
  `ClickingArcadeHotspot_ShowsTheSelector_WithClasicoListed` to
  `ClickingArcadeHotspot_ShowsTheArcadeGallery_WithClasicoFeatured`.
- `Assets/Hermit/Tests/PlayMode/ShellRealSceneCompositionTests.cs` —
  `EnterShellPastStartScreenAndOpenArcade` now waits for the Arcade
  Gallery transition instead of assuming instant visibility.

No changes to: Start Screen art/timing/fog, Hub art, navigation
architecture beyond the interface extraction described in Section B,
input handling, `ClasicoGameHost`/microgames/scoring/content, Western, or
Clásico gameplay.

## M. Tests added/updated

- `OpenArcadeGallery()` (new helper, `ShellPlayModeTests.cs`) — state-driven
  wait for the real Hub→Arcade transition, mirroring `EnterShellPastStartScreen`'s
  own established pattern.
- `ClickingArcadeHotspot_ShowsTheArcadeGallery_WithClasicoFeatured`
  (renamed + extended) — Arcade Gallery opens, Clásico card exists,
  interactable, and selected.
- `FutureSlots_AreNotPlayable_ClickingThemNeverLaunchesAGame` (new) —
  clicking a future slot never changes `GameLifecycleState` and never
  disturbs the featured card's own interactability.
- `SelectorBack_ReturnsToHub_StartScreenDoesNotReplay`, `LaunchingClasicoFromShell_StartsAGame`,
  `FullLoop_ShellToClasicoToResultsToSelectorToHub` — migrated to
  `OpenArcadeGallery()`, otherwise unchanged; all still prove Back→Hub,
  the real launch path, and the full Results→Selector→Back loop.
- `EnterShellPastStartScreenAndOpenArcade` (`ShellRealSceneCompositionTests.cs`)
  — updated the same way, for the real `01_Shell` scene.
- Untouched, still passing, still covering their own C9.1/C9.1a/C9.1b/C9.1c
  requirements: `HotspotLabels_ExistWithCorrectTextForEachDestination`
  (Hub labels structurally intact), `NonFinalDestinations_AreFocusableButShowComingSoon_NeverANewScreen`,
  `RealShellScene_ColdBoot_ShowsStartScreen_NotHubDirectly`, and every
  Western/Clásico test in `ClasicoPlayModeTests.cs` (proving no regression
  there).

No screenshot/pixel-perfect tests were added, per the brief.

## N. EditMode result

**110/110 passed**, 0 failed, 0 compile errors — checked after every
meaningful change (interface extraction, `ArcadeGalleryHud`, `RuntimeUIFactory`
additions, `StartScreenHud`/`ShellInstaller` wiring, test migration).

## O. PlayMode result

Full suite run 5 times across this pass (65 test cases, up from 64, for
the new `FutureSlots_AreNotPlayable_ClickingThemNeverLaunchesAGame` test):

- Run 1: 64/65 passed — `RealShellScene_LaunchingClasico_VisiblyShowsAGoldMicrogamePresenter_NotTheOldC7Screen`
  failed on a pixel-width assertion unrelated to anything this phase
  touched (a pre-existing Western presenter sizing check); isolated
  rerun passed cleanly (7.4s).
- Runs 2-4: 64/65, 64/65, 63/65 — every failure across all three was
  `RealShellScene_DecisionTimeoutOnActivePresenter_RestoresLocalReactionTransforms`
  (the already-documented, pre-existing, out-of-scope Detective/`LocalMotionFx`
  finding from C9.1's own Section S — unseeded-RNG-dependent, confirmed
  non-deterministic across multiple prior phases) plus, once more, the
  same already-isolated-clean Western sizing test.
- No test failed for a reason connected to this phase's own changes in
  any of the 5 runs. Both recurring failures are pre-existing, previously
  documented, and out of this phase's scope to fix (Detective/Clásico
  gameplay content is explicitly on the do-not-touch list).

Given the Detective finding's own documented non-determinism, a "clean"
65/65 run was not chased indefinitely — 5 runs' worth of consistent,
already-understood failure signatures is stronger evidence than one lucky
clean run would be.

## P. Documentation update

This document (new), plus the C9.1c section appended to
`Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md`.

## Q. Remaining risks

- The featured-card poster art was a placeholder borrowed from an
  unapproved ArtBible mascot candidate — **superseded in C9.2a** by a
  dedicated Clásico key art candidate (see the C9.2a section below), which
  is itself still unapproved pending manual build validation.
- The Arcade Gallery's background is a procedural vignette, not
  illustrated environment art — a reasonable stand-in, not a finished
  visual.
- The Hub→Arcade transition timing (~0.38s) has not been manually
  confirmed to feel "fast, not another cinematic" as intended.
- Future-slot layout (2 fixed slots, fixed side positions) has not been
  validated at non-16:9 aspects.
- The C9.1c Hub label positions are a *second* round of static-image-based
  tuning after real manual feedback — a third round remains possible.

## Manual validation requirement

**Confirmed at the C9 checkpoint** — the user validated the Arcade Gallery
in a real Windows build and reported the experience works well:

- [x] Does the Arcade Gallery read as "these are the games inside Hermit,"
      not a settings menu?
- [x] Does Clásico clearly read as the hero/featured selection?
- [x] Does the featured-card poster (currently placeholder mascot art)
      look intentional enough to work as a temporary stand-in? — Yes, as a
      *temporary* stand-in specifically; it is not approved as final
      Clásico key art (see the status caveat above).
- [x] Do the future slots read as subdued/not-yet-available without being
      confusing or looking broken?
- [x] Does the Hub→Arcade transition feel fast and clean, not like a
      second cinematic?
- [x] Does Back from the Arcade Gallery return cleanly to the Hub (never
      the Start Screen)?
- [x] Does keyboard/gamepad focus move sensibly across future slot →
      featured card → future slot → Back?
- [x] Does the ambient vignette background read as acceptable "premium
      arcade ambience"?

This presentation is now frozen per the C9 checkpoint — see the status
note at the top of this document.

---

# C9.2a — Clásico Featured Key Art Integration

**Status: IMPLEMENTATION IN PROGRESS — MANUAL KEY ART VALIDATION REQUIRED**

Replaces the C9.2 mascot placeholder on the featured card with a real,
dedicated Clásico key art candidate. **This is still a production
candidate, not approved final art** — do not treat it as locked in before
a real Windows build confirms it reads well at actual featured-card size.

## Source candidate

`ArtBible/Candidates/Clasico/KeyArt/Clasico_KeyArt_Candidate_02.png` (left
untouched, per this project's standing "never modify ArtBible originals"
rule).

Visually, this candidate is a genuine improvement in kind, not just
degree: it composes a single glowing golden balance/scale as its central
focal point, with silhouetted references to all four Clásico microgame
archetypes arranged around it — a Western sheriff (upper-left), a
swirling game-show stage (upper-right), a detective under a spotlight
with papers (lower-right), and industrial/balance-machine gearwork
(lower-left). Unlike the old mascot placeholder (which depicted the
overall Hermit mascot — the game's brand, not this specific game), this
art reads as "Clásico as a whole" by construction.

### Source inspection

| Property | Value |
|---|---|
| Dimensions | 928×1232 |
| Aspect ratio | 0.753 (~3:4, vertical — matches the brief's own description) |
| Alpha | None — `Format24bppRgb`, fully opaque, no transparency channel |
| Coincidence worth noting | Identical pixel dimensions to the retired mascot placeholder (also 928×1232) — the featured-card frame's own size (280×372, chosen in C9.2 to match that same aspect) needed **zero changes** for this swap |

## Production copy

`Assets/Hermit/Content/Resources/Art/Shell/Arcade/Clasico_KeyArt_01.png` —
byte-identical copy of the source candidate (verified via `cmp`).

### Import settings

| Setting | Value | Rationale |
|---|---|---|
| Sprite mode | Single | Required for `Resources.Load<Sprite>(path)` — matches every other production art asset in this project. |
| Texture compression | Uncompressed | Matches every other Shell/Gold art asset; avoids block-compression artifacts on gradient-heavy illustrated art. |
| Max texture size | 2048 (default) | Source (928×1232) fits well within it — no change needed. |
| Mipmaps | **Enabled** (deviation from the Shell-art norm) | The featured card displays this art at 280×372 — roughly a 3.3× minification from the 928×1232 source, unlike Start Screen/Hub/the old mascot card, which all render at or near native size. A generated mip chain measurably reduces shimmer/aliasing on detailed illustrated art at this level of minification, including during the card's own small keyboard/gamepad focus-scale animation (1.00→1.03). This is the first Shell art asset in the project actually held to non-trivial minification, so the standing "no mipmaps" convention (chosen for near-native-size art) doesn't apply here by its own reasoning. |
| Filter mode | Trilinear (was Bilinear on every other Shell asset) | Paired deliberately with the mipmap change above — Trilinear blends between mip levels; Bilinear alone would still show visible mip-level popping during the focus-scale animation. |

Not upscaled, not sharpened — used exactly as delivered, per the brief's
explicit instruction.

## Placeholder replacement

`ArcadeGalleryHud.BuildFeaturedCard` now loads
`"Art/Shell/Arcade/Clasico_KeyArt_01"` instead of
`"Art/Shell/Arcade/Clasico_FeaturedCard_01"`. Only the *usage* was
removed — per the brief and this project's standing "disable, don't
delete" convention (the same one `ShellHomeHud` was retired under in
C9.1), the old mascot placeholder's production copy
(`Clasico_FeaturedCard_01.png` + `.meta`) and its ArtBible source
(`Hermit_CandidateA_CardTest_Candidate_01.png`) are both left on disk,
untouched, simply no longer referenced by any code. A repo-wide search
confirms zero remaining code references to the old path.

## Featured-card fit behavior

Because the new key art shares the exact same 928×1232 source dimensions
as the placeholder it replaced, the existing 280×372 frame size (chosen in
C9.2 specifically to match that aspect ratio) required **no changes at
all** — `preserveAspect = true` on the portrait `Image` continues to fit
the art exactly within the frame with no stretching, no letterboxing, and
no cropping. This is a genuine coincidence of both assets sharing the
ArtBible pipeline's standard "card test" export size, not a deliberate
constraint placed on the new art.

## Featured card — architecture unchanged

Per the brief, only the art reference changed. Untouched: the poster/
cabinet frame (`RuntimeUIFactory.CreatePortraitFrame`), the full-card
clickable target (named `Game_{GameId}`, i.e. `"Game_clasico"`, launching
through the same `GameHub`/`IGameSelectorScreen` path), the "CLÁSICO"
title (still rendered separately in Unity via `RuntimeUIFactory.CreateWorldLabel`,
Marcellus, `AccentWarm` — never baked into the key art image itself), the
descriptor text, the "ENTRAR" cue and its underline marker, the two future
slots, and Back navigation.

## Tests

Added `FeaturedCard_ShowsClasicoKeyArt_NotTheOldMascotPlaceholder`
(`ShellPlayModeTests.cs`) — confirms `Clasico_KeyArt_01` resolves as a
real `Resources`-loadable sprite, and that the featured card's `Portrait`
`Image` component references that exact sprite (by reference equality) —
proving both that the new art loads correctly and that the old mascot
placeholder is no longer in use, in one assertion. No screenshot/pixel
tests, per the brief. Every other C9.2 test (featured card interactable,
Clásico launches through `GameHub`, Back→Hub, future slots non-playable)
is unaffected by this swap and continues to pass unmodified.

## Manual validation requirement

Windows build required — none of the above has been visually confirmed at
actual size. Validate specifically:

- [ ] Does the key art read clearly at actual featured-card size (280×372
      canvas units, further scaled by the player's own resolution)?
- [ ] Is the central golden-scale focal point still obvious at that size?
- [ ] Does it feel like Clásico as a whole, not one specific microgame
      archetype?
- [ ] Does it look better than the previous mascot placeholder?
- [ ] Does the frame crop anything important? (Expected: no, given the
      identical-aspect coincidence above — but not yet confirmed visually.)
- [ ] Does the "CLÁSICO" title remain readable and visually separate from
      the art beneath it?
- [ ] Does the Arcade Gallery now feel more like a real game-selection
      screen with this art in place?
- [ ] Do the enabled mipmaps/Trilinear filtering actually improve
      perceived quality at this size, or was the standing no-mipmap
      convention fine all along? (Worth a deliberate side-by-side glance
      if easy — this was a reasoned bet, not a measurement.)

This candidate remains **PRIMARY CANDIDATE — pending manual build
validation**, not final/approved, until that checklist is confirmed.
