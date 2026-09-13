using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// C9.1 — Hermit's real cold-boot entry point, replacing the instant
    /// <c>ShellInstaller.Awake() -&gt; ShowHome()</c> jump with a title/
    /// press-start gate and a short cinematic entry into the visual Hermit
    /// Hub (see Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md for the full
    /// brief this implements). One HUD, one file, matching the existing
    /// one-HUD-per-screen convention (<see cref="ShellHomeHud"/>,
    /// <see cref="GameSelectorHud"/>, <c>ClasicoHud</c>) — this presenter is
    /// simply large because it now owns three visual states that used to be
    /// two separate (and much smaller) screens.
    ///
    /// Owns, across its lifetime: the Start Screen candidate art + "HERMIT"
    /// title + "PRESS START" prompt (Title phase, holds indefinitely); the
    /// push-in/fog/bloom/art-swap transition (Transitioning phase, ~3.6s,
    /// plays exactly once per process — <see cref="ShellInstaller"/> never
    /// calls <see cref="ShowTitle"/> a second time); and the settled Hub
    /// art plus its four placeholder destination hotspots (Done phase,
    /// permanent for the rest of the session). <see cref="ShellHomeHud"/>
    /// is retired from the active composition entirely (its file is
    /// untouched, just no longer built/shown by <see cref="ShellInstaller"/>)
    /// rather than deleted, per the brief's "disable, don't delete" rule.
    ///
    /// State model is a private <c>enum Phase</c> + explicit named timing
    /// constants, matching <c>ClasicoSessionDirector</c>'s own convention —
    /// but the actual Transitioning choreography is driven by one
    /// <see cref="TransitionRoutine"/> coroutine (a sequence of
    /// <c>WaitForSeconds</c> deltas between named beats plus small
    /// concurrent sub-coroutines for push-in/fog/bloom), matching
    /// <c>WesternShootoutPresenter.CinematicIntroRoutine</c>'s own proven
    /// shape for a one-shot audiovisual sequence — the more directly
    /// applicable precedent here than a per-frame Update-ticked timer,
    /// since (unlike <c>ClasicoSessionDirector</c>) this class is already a
    /// real <see cref="MonoBehaviour"/> that can just use Unity's own
    /// coroutine scheduler.
    /// </summary>
    internal sealed class StartScreenHud : MonoBehaviour
    {
        /// <summary>Fired when the player picks the Arcade hotspot — the
        /// only destination with a real screen behind it this phase.
        /// Academia/Codex/Historietas are visible/focusable but resolve to
        /// an inline "Próximamente" cue, not a new screen (see
        /// <see cref="OnNonFinalDestinationSelected"/>) — no fake systems.</summary>
        public event Action ArcadeRequested;

        private enum Phase { Title, Transitioning, Done }
        private Phase _phase = Phase.Title;

        // --- Approved timeline (Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md,
        // "Exact timeline") — expressed as the deltas TransitionRoutine
        // actually waits between beats, not absolute timestamps. Comments
        // on each constant give the absolute timestamp for cross-reference
        // against the brief. Total: 0.20+0.25+0.40+0.15+0.80+0.80+0.40+0.60
        // = 3.60s, matching "approximately 3.4-3.8s", never shortened to
        // Western-cinematic-adjacent ~2.5s.
        private const float ToSwellStart = 0.20f;      // t=0.00 -> 0.20
        private const float ToPushInStart = 0.25f;     // t=0.20 -> 0.45 (prompt/logo fade also completes here)
        private const float ToBloomStart = 0.40f;      // t=0.45 -> 0.85
        private const float ToFogRiseStart = 0.15f;    // t=0.85 -> 1.00
        private const float ToArtSwap = 0.80f;         // t=1.00 -> 1.80 (fog reaches cover)
        private const float ToFogClearEnd = 0.80f;     // t=1.80 -> 2.60
        private const float ToNavRevealStart = 0.40f;  // t=2.60 -> 3.00 (Hub settle hold)
        private const float ToDone = 0.60f;            // t=3.00 -> 3.60 (nav reveal)

        private const float PromptFadeDuration = 0.45f;
        private const float PushInDuration = 2.65f; // 0.45 -> 3.10, one continuous gentle motion spanning both art pieces
        private const float BloomDuration = 0.25f;  // 0.85 -> 1.10
        private const float FogRiseDuration = 0.80f; // 1.00 -> 1.80
        private const float FogClearDuration = 0.80f; // 1.80 -> 2.60
        private const float NavRevealDuration = 0.60f; // 3.00 -> 3.60

        // C9.2: the Hub-navigation half of the short Hub<->Arcade Gallery
        // transition — deliberately much shorter than anything in the
        // approved cinematic above; see ShellInstaller.TransitionToArcadeRoutine
        // for the full ~0.35s sequence this is one half of.
        private const float HubNavTransitionFadeDuration = 0.20f;

        private const float PushInPeakScale = 1.06f; // "gentle scale increase", never a dramatic zoom
        private const float DriftAmount = 4f;        // "tiny positional drift" — a few canvas units, not a slideshow
        private const float FogPeakAlpha = 0.62f;     // "soft haze" — enough to mask a sprite swap, never obscures
        private const float BloomPeakAlpha = 0.35f;

        private static readonly Color FogColor = new Color(1f, 0.94f, 0.82f, 0f);
        private static readonly Color BloomColor = new Color(1f, 0.97f, 0.88f, 0f);

        // --- C9.1b: settled Hub composition. C9.1a's -24/1.015 correction
        // was manually validated and found INSUFFICIENT — the Hub still
        // read as biased upward. This is a materially larger correction,
        // not another small nudge, per the brief's own explicit instruction
        // not to repeat a "tiny cosmetic adjustment". Applies ONLY to the
        // settled Hub — never to the Start Screen's own held/pushed-in
        // presentation, which this phase must not touch. Expressed in the
        // same 1280x720 CanvasScaler reference units every other
        // RectTransform value in this class already uses, never raw screen
        // pixels. Applied atomically at the same instant as the
        // already-approved fog-covered sprite swap (see TransitionRoutine)
        // via the two live-read fields below, rather than animated
        // separately — bundled into the one hard cut already hidden behind
        // fog rather than introducing a second, new visible seam of its own.
        //
        // Geometry note (see Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md,
        // "C9.1b" for the full derivation): AspectRatioFitter.EnvelopeParent
        // matches this art's height exactly to the 720-unit frame at
        // localScale=1 (the image is very slightly WIDER than the frame,
        // not taller), so ALL vertical pan headroom comes from the settled
        // scale multiplier alone — each 0.01 of scale buys only ~7.2 units
        // of headroom per edge. An offset of -58 would need scale ~1.16 for
        // zero theoretical top-edge exposure, well past the brief's
        // approved 1.08-1.11 ceiling ("composition correction, not zooming
        // into the illustration"). HubSettledScaleBoost is set to the top
        // of that approved range rather than exceeding it; any residual gap
        // this leaves is mitigated by StartScreenPanel's own background
        // color (Theme.Background, #0B0D14) being a very close match to
        // this art's own dark night-sky palette at the frame's top edge —
        // but this is a reasoned estimate, not a rendered measurement, and
        // is explicitly flagged for manual confirmation.
        private static readonly Vector2 HubSettledOffset = new Vector2(0f, -58f);
        private const float HubSettledScaleBoost = 1.10f;
        private Vector2 _artBaseOffset = Vector2.zero;
        private float _artScaleTarget = PushInPeakScale;

        private RectTransform _root;
        private RectTransform _artRect;
        private Image _sceneArt;
        private Image _fogOverlay;
        private Image _bloomOverlay;
        private Text _titleText;
        private TMP_Text _pressStartText;
        private Button _confirmButton;
        private Coroutine _pulseRoutine;

        private CanvasGroup _navGroup;
        private Text _sessionStatusText;
        private Text _comingSoonText;
        private Coroutine _comingSoonRoutine;
        private readonly System.Collections.Generic.List<Hotspot> _hotspots = new System.Collections.Generic.List<Hotspot>();

        private Sprite _startScreenSprite;
        private Sprite _hubSprite;

        private AudioSource _audioSource;
        private AudioClip _confirmChimeClip;
        private AudioClip _entrySwellClip;

        private HermitTheme Theme => RuntimeUIFactory.Theme;

        // --- C9.1b: destination label typography. C9.1a's LiberationSans
        // SDF + synthetic bold was manually validated and found still
        // generic/insufficient — piling on size/shadow/tracking couldn't
        // fix a font-identity problem, so this phase replaced the font
        // itself with Marcellus (RuntimeUIFactory.DisplayFont; see
        // Assets/Hermit/Content/Fonts/Marcellus/SOURCE.md), its own real
        // Regular weight (no synthetic bold — Marcellus has no separate
        // bold cut, and the brief was explicit: don't fake a weight a real
        // one doesn't have). Tracking eased back slightly from C9.1a (a
        // serif display face needs less artificial spacing than a
        // synthetic-bold grotesque to read as premium rather than
        // stretched). Marker width trimmed too — "a thin short
        // architectural rule... should not extend wider than necessary".
        // See Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md, "C9.1b".
        private const float LabelFontSize = 25f; // within the brief's 24-27pt range at the 1280x720 reference
        private const float LabelCharacterSpacing = 3f;
        private const float LabelFocusScale = 1.05f;
        private const float FocusLerpRate = 10f; // smooth, quick settle — no pulsing, no bounce
        private const float MarkerHeight = 2f;
        private const float MarkerRestWidth = 48f;
        private const float MarkerFocusWidth = 68f;
        private const float MarkerFinalRestAlpha = 0.55f;
        private const float MarkerNonFinalRestAlpha = 0.35f;
        private const float MarkerFocusAlpha = 1f;
        private const float MarkerGapBelowLabel = 24f;

        /// <summary>One placeholder destination hotspot — an invisible hit
        /// region (same "art/world IS the target, no card" language
        /// Western's own WesternTarget buttons established) plus a
        /// TextMeshPro label and a short underline marker that brightens
        /// and lengthens on keyboard/gamepad focus (mouse hover already
        /// gets <see cref="RuntimeUIFactory.CreateButton"/>'s own built-in
        /// highlighted-color tint for free) — a non-color cue, per the
        /// brief's "do not rely on color alone". <paramref name="IsFinal"/>
        /// distinguishes Arcade (a real destination) from the other three
        /// (an inline "Próximamente" cue only) — see
        /// Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md, "Temporary hotspot
        /// mapping" for why each name landed on its current anchor.</summary>
        private struct Hotspot
        {
            public Button Button;
            public RectTransform LabelRect;
            public Image Marker;
            public bool IsFinal;
        }

        public void Build(Transform canvasRoot)
        {
            _root = RuntimeUIFactory.CreatePanel(canvasRoot, "StartScreenPanel", Theme.Background);

            BuildSceneArt();
            BuildOverlays();
            BuildTitleAndPrompt();
            BuildConfirmButton();
            BuildNavigation();
            BuildAudio();

            _root.gameObject.SetActive(false);
        }

        private void BuildSceneArt()
        {
            _startScreenSprite = RuntimeUIFactory.LoadArt("Art/Shell/StartScreen/Hermit_StartScreen_01");
            _hubSprite = RuntimeUIFactory.LoadArt("Art/Shell/Hub/Hermit_Hub_01");

            var artGo = new GameObject("SceneArt", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            _artRect = (RectTransform)artGo.transform;
            _artRect.SetParent(_root, false);
            RuntimeUIFactory.StretchFull(_artRect);

            _sceneArt = artGo.GetComponent<Image>();
            _sceneArt.sprite = _startScreenSprite;
            _sceneArt.preserveAspect = false;
            _sceneArt.raycastTarget = false;
            _sceneArt.gameObject.SetActive(_startScreenSprite != null);

            // Both candidates share the identical 1456x816 source aspect
            // (measured directly, not assumed) — one fixed ratio covers
            // both without recomputing on sprite swap.
            var fitter = artGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 1456f / 816f;
        }

        /// <summary>Two independent full-stretch overlays — bloom is a
        /// brief warm flash (0.85-1.10s), fog is the longer "opening" haze
        /// that also covers the art swap (1.00-2.60s). Plain
        /// <c>Image.color</c> alpha only, never <c>CanvasGroup</c> and
        /// never <c>Image.Type.Filled</c>/<c>Radial360</c> — the documented
        /// historical black-frame bug class this project avoids everywhere
        /// full-screen effects appear (see
        /// <c>WesternShootoutPresenter.GunshotFlashRoutine</c>'s identical
        /// reasoning).</summary>
        private void BuildOverlays()
        {
            var bloomRect = RuntimeUIFactory.CreatePanel(_root, "BloomPulse", BloomColor);
            _bloomOverlay = bloomRect.GetComponent<Image>();
            _bloomOverlay.raycastTarget = false;

            var fogRect = RuntimeUIFactory.CreatePanel(_root, "FogOverlay", FogColor);
            _fogOverlay = fogRect.GetComponent<Image>();
            _fogOverlay.raycastTarget = false;
        }

        /// <summary>HERMIT: unchanged since C9.1 — per the C9.1b brief's own
        /// explicit caution ("do not redesign HERMIT aggressively... only
        /// minor typography consistency changes if the new font family
        /// CLEARLY improves it"), left on the plain default TMP/UI font
        /// rather than switched to Marcellus, since that can't be confirmed
        /// without a real render and this is already the strongest element
        /// on the Start Screen. PRESS START: C9.1b repositioned and
        /// re-fonted — manual validation of C9.1a found it visually
        /// competing with the distant central building's illuminated
        /// facade. Direct inspection of the Start Screen candidate places
        /// that building's silhouette spanning roughly canvas Y +228 to -19
        /// (in this Title state's neutral, unscaled/unshifted transform —
        /// see ShowTitle), with a calmer, comparatively plain plaza/street
        /// band beneath it (roughly Y -19 to -169) before the seated
        /// foreground figure's silhouette begins — PRESS START now sits in
        /// that calmer band instead of against the building itself.</summary>
        private void BuildTitleAndPrompt()
        {
            _titleText = RuntimeUIFactory.CreateText(
                _root, "HermitTitle", "HERMIT", Theme.TitleSize + 12, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -90), new Vector2(700, 100));

            _pressStartText = RuntimeUIFactory.CreateWorldLabel(
                _root, "PressStartPrompt", "PRESS START", Theme.HeadingSize, Theme.AccentWarm,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -440), new Vector2(420, 56));
        }

        /// <summary>One full-stretch invisible <see cref="Button"/> covering
        /// the whole panel — "PRESS START" is conceptual wording, not a
        /// literal key, so anywhere on screen accepts confirm, exactly like
        /// mouse click/keyboard Submit/gamepad Submit all already route
        /// through the same <c>EventSystem</c>/<c>Selectable</c> plumbing
        /// once this button is selected (<see cref="RuntimeUIFactory.Select"/>)
        /// — no new input code, no per-device branching.</summary>
        private void BuildConfirmButton()
        {
            _confirmButton = RuntimeUIFactory.CreateButton(_root, "ConfirmButton", string.Empty, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var confirmRect = (RectTransform)_confirmButton.transform;
            RuntimeUIFactory.StretchFull(confirmRect);
            _confirmButton.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            _confirmButton.onClick.AddListener(OnConfirm);
        }

        /// <summary>Four placeholder destination hotspots, anchored over
        /// the Hub illustration's own distinct regions (see the class
        /// doc-comment reference to the mapping doc) — invisible hit
        /// regions plus a small label and a dim "underline" bar that
        /// brightens on keyboard/gamepad selection (mouse hover already
        /// gets <see cref="RuntimeUIFactory.CreateButton"/>'s own built-in
        /// highlighted-color tint for free) — a non-color cue, per the
        /// brief's "do not rely on color alone". Built inactive/non-
        /// interactable; <see cref="ShowNavigation"/> is what actually
        /// reveals and enables them.</summary>
        private void BuildNavigation()
        {
            var navGo = new GameObject("HubNavigation", typeof(RectTransform), typeof(CanvasGroup));
            var navRect = (RectTransform)navGo.transform;
            navRect.SetParent(_root, false);
            RuntimeUIFactory.StretchFull(navRect);
            _navGroup = navGo.GetComponent<CanvasGroup>();
            _navGroup.alpha = 0f;
            _navGroup.blocksRaycasts = false;
            _navGroup.interactable = false;

            // Temporary placeholder mapping (documented, not final art
            // direction — see the brief's own section 14): central grand
            // hall -> Academia, left amber building -> Codex, right violet
            // structure -> Arcade, lower-right lantern market -> Historietas.
            //
            // C9.1c: label SAFE-ZONE fix. C9.1b's anchor derivation had a
            // real bug (it used HubSettledScaleBoost, 1.10, as the *total*
            // settled scale — it forgot to multiply by PushInPeakScale,
            // 1.06, even though that's genuinely part of the same
            // localScale value TransitionRoutine applies; the real total is
            // 1.166). Fixed here. On top of that fix, each hotspot's own
            // *hit-region anchor* still targets its architectural center
            // (unchanged in kind from C9.1b, just corrected math), while
            // each *label offset* now specifically targets a calm, dark,
            // detail-free sub-region near that architecture — away from
            // stairs, roof edges, and bright windows — per this phase's own
            // priority order: readability and separation from architectural
            // lines over strict centering or symmetry. See
            // Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md, "C9.1c" for the
            // full per-destination rationale and the corrected transform
            // math. Still best-effort from a static image, not a live
            // render — pending real manual confirmation.
            BuildHotspot(navRect, "ArcadeHotspot", "ARCADE", new Vector2(0.848f, 0.545f), new Vector2(0f, 32f), isFinal: true, OnArcadeSelected);
            BuildHotspot(navRect, "AcademiaHotspot", "ACADEMIA", new Vector2(0.478f, 0.331f), new Vector2(28f, -20f), isFinal: false, OnNonFinalDestinationSelected);
            BuildHotspot(navRect, "CodexHotspot", "CODEX", new Vector2(0.172f, 0.588f), new Vector2(-15f, 38f), isFinal: false, OnNonFinalDestinationSelected);
            BuildHotspot(navRect, "HistorietasHotspot", "HISTORIETAS", new Vector2(0.702f, 0.130f), new Vector2(10f, 57f), isFinal: false, OnNonFinalDestinationSelected);

            RuntimeUIFactory.ChainHorizontal(_hotspots[2].Button, _hotspots[1].Button, _hotspots[0].Button, _hotspots[3].Button);

            _comingSoonText = RuntimeUIFactory.CreateText(
                navRect, "ComingSoonLabel", string.Empty, Theme.CaptionSize, TextAnchor.MiddleCenter, Theme.Warning,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360, 40));
            _comingSoonText.gameObject.SetActive(false);

            // C9.1a: dimmed (alpha 0.55, down from a fully-opaque
            // TextSecondary) so it reads as secondary to Hub navigation
            // rather than competing with the destination labels — still a
            // single short line, no redesign of what it shows.
            var sessionColor = Theme.TextSecondary;
            _sessionStatusText = RuntimeUIFactory.CreateText(
                navRect, "SessionStatus", string.Empty, Theme.CaptionSize, TextAnchor.LowerRight,
                new Color(sessionColor.r, sessionColor.g, sessionColor.b, 0.55f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24, 20), new Vector2(360, 28));
        }

        private void BuildHotspot(Transform parent, string name, string label, Vector2 normalizedAnchor, Vector2 labelOffset, bool isFinal, Action onSelected)
        {
            var button = RuntimeUIFactory.CreateButton(parent, name, string.Empty, normalizedAnchor, normalizedAnchor, Vector2.zero, new Vector2(180, 56));
            button.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            button.interactable = false;

            // CreateButton always builds its own plain-Text "Label" child —
            // unused here in favor of the TMP label below, so it's removed
            // rather than left as dead, empty-string clutter in the hierarchy.
            Destroy(button.GetComponentInChildren<Text>().gameObject);

            var tmpLabel = RuntimeUIFactory.CreateWorldLabel(
                button.transform, "Label", label, LabelFontSize, Theme.AccentWarm,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), labelOffset, new Vector2(260, 44),
                characterSpacing: LabelCharacterSpacing);

            var markerRect = RuntimeUIFactory.CreatePanel(button.transform, "Marker", Theme.AccentWarm);
            markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 1f);
            markerRect.anchoredPosition = labelOffset + new Vector2(0f, -MarkerGapBelowLabel);
            markerRect.sizeDelta = new Vector2(MarkerRestWidth, MarkerHeight);
            var marker = markerRect.GetComponent<Image>();
            marker.raycastTarget = false;
            var warm = Theme.AccentWarm;
            // Non-final destinations (Academia/Codex/Historietas) sit a
            // touch dimmer at rest — a restrained, non-blocking hint that
            // they are not yet a real destination, without a separate
            // "locked" icon or label.
            marker.color = new Color(warm.r, warm.g, warm.b, isFinal ? MarkerFinalRestAlpha : MarkerNonFinalRestAlpha);

            button.onClick.AddListener(() => onSelected());
            _hotspots.Add(new Hotspot { Button = button, LabelRect = (RectTransform)tmpLabel.transform, Marker = marker, IsFinal = isFinal });
        }

        private void BuildAudio()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;

            _confirmChimeClip = ProceduralAudio.SoftConfirmChime("StartScreenConfirmChime", 0.5f);
            _entrySwellClip = ProceduralAudio.EntrySwell("StartScreenEntrySwell", 3.0f);
        }

        /// <summary>Called exactly once per process, from
        /// <see cref="ShellInstaller.Awake"/> — never again for the life of
        /// the session (returning from a game calls <see cref="ShowNavigation"/>,
        /// not this).</summary>
        public void ShowTitle()
        {
            _root.gameObject.SetActive(true);
            _phase = Phase.Title;

            _sceneArt.sprite = _startScreenSprite;
            _sceneArt.gameObject.SetActive(_startScreenSprite != null);
            _artRect.localScale = Vector3.one;
            _artRect.anchoredPosition = Vector2.zero;
            _artBaseOffset = Vector2.zero;
            _artScaleTarget = PushInPeakScale;

            _fogOverlay.color = new Color(FogColor.r, FogColor.g, FogColor.b, 0f);
            _bloomOverlay.color = new Color(BloomColor.r, BloomColor.g, BloomColor.b, 0f);

            _titleText.gameObject.SetActive(true);
            _pressStartText.gameObject.SetActive(true);
            SetTextAlpha(_titleText, 1f);
            SetTextAlpha(_pressStartText, 1f);

            _confirmButton.gameObject.SetActive(true);
            _confirmButton.interactable = true;
            RuntimeUIFactory.Select(_confirmButton);

            _navGroup.alpha = 0f;
            _navGroup.blocksRaycasts = false;
            _navGroup.interactable = false;
            foreach (var hotspot in _hotspots)
            {
                hotspot.Button.interactable = false;
            }

            _pulseRoutine = StartCoroutine(PressStartPulseRoutine());
        }

        /// <summary>The confirm accepted from Title — mouse click, keyboard
        /// Submit, or gamepad Submit all reach here identically via
        /// <c>Button.onClick</c>; there is no per-device branch anywhere in
        /// this class. Guarded twice (phase check, then immediately
        /// disabling the button) so a second confirm during Transitioning
        /// is structurally impossible to double-fire, not just
        /// debounced.</summary>
        private void OnConfirm()
        {
            if (_phase != Phase.Title)
            {
                return;
            }

            _phase = Phase.Transitioning;
            _confirmButton.interactable = false;
            _confirmButton.gameObject.SetActive(false);

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }

            _audioSource.PlayOneShot(_confirmChimeClip);
            StartCoroutine(TransitionRoutine());
        }

        /// <summary>The approved ~3.6s sequence — see the class-level
        /// timing constants for the exact deltas, each carrying the
        /// absolute timestamp from the brief in its own comment.</summary>
        private IEnumerator TransitionRoutine()
        {
            // t=0.00 — prompt/logo fade begins (concurrent, 0.45s).
            StartCoroutine(FadeTextRoutine(_titleText, PromptFadeDuration, 1f, 0f));
            StartCoroutine(FadeTextRoutine(_pressStartText, PromptFadeDuration, 1f, 0f));

            yield return new WaitForSeconds(ToSwellStart);

            // t=0.20 — the single swell/arrival cue starts.
            _audioSource.PlayOneShot(_entrySwellClip);

            yield return new WaitForSeconds(ToPushInStart);

            // t=0.45 — push-in begins: one continuous gentle scale-up +
            // tiny drift spanning both the Start Screen art and (after the
            // t=1.80 swap) the Hub art, so it reads as one uninterrupted
            // motion rather than two separate animations.
            _titleText.gameObject.SetActive(false);
            _pressStartText.gameObject.SetActive(false);
            StartCoroutine(PushInRoutine(PushInDuration));

            yield return new WaitForSeconds(ToBloomStart);

            // t=0.85 — brief warm light pulse (0.25s).
            StartCoroutine(AlphaPulseRoutine(_bloomOverlay, BloomColor, BloomDuration, BloomPeakAlpha));

            yield return new WaitForSeconds(ToFogRiseStart);

            // t=1.00 — fog begins rising toward cover (0.80s).
            StartCoroutine(AlphaRoutine(_fogOverlay, FogColor, FogRiseDuration, 0f, FogPeakAlpha));

            yield return new WaitForSeconds(ToArtSwap);

            // t=1.80 — swap behind the fog (never a visible crossfade —
            // the two candidates were inspected and found compositionally
            // mismatched for one; see the doc's "Art swap" section) and
            // begin clearing the fog (0.80s). The C9.1a settled-Hub
            // composition offset/scale (see the class-level fields) is
            // applied in this exact same statement — one hard cut already
            // hidden behind fog, not a second new visible seam.
            _sceneArt.sprite = _hubSprite;
            _sceneArt.gameObject.SetActive(_hubSprite != null);
            _artBaseOffset = HubSettledOffset;
            _artScaleTarget = PushInPeakScale * HubSettledScaleBoost;
            StartCoroutine(AlphaRoutine(_fogOverlay, FogColor, FogClearDuration, FogPeakAlpha, 0f));

            yield return new WaitForSeconds(ToFogClearEnd);

            // t=2.60 — Hub settled visually; brief hold before nav reveals.
            yield return new WaitForSeconds(ToNavRevealStart);

            // t=3.00 — navigation fades in (0.60s).
            StartCoroutine(NavRevealRoutine(NavRevealDuration));

            yield return new WaitForSeconds(ToDone);

            // t=3.60 — Hub fully interactive.
            _phase = Phase.Done;
            foreach (var hotspot in _hotspots)
            {
                hotspot.Button.interactable = true;
            }

            RuntimeUIFactory.Select(_hotspots[0].Button);
        }

        private IEnumerator PressStartPulseRoutine()
        {
            const float period = 1.6f;
            while (true)
            {
                var wave = (Mathf.Sin(Time.time / period * Mathf.PI * 2f) + 1f) * 0.5f;
                SetTextAlpha(_pressStartText, Mathf.Lerp(0.55f, 1f, wave));
                yield return null;
            }
        }

        /// <summary>Takes <see cref="Graphic"/> (the common base of both
        /// <see cref="Text"/> and TMP's <see cref="TMP_Text"/>) rather than
        /// either concrete type — C9.1b converted PressStartPrompt to TMP
        /// while HermitTitle stayed plain <see cref="Text"/>, and this
        /// fade/alpha helper is shared by both.</summary>
        private static IEnumerator FadeTextRoutine(Graphic text, float duration, float fromAlpha, float toAlpha)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                SetTextAlpha(text, Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            SetTextAlpha(text, toAlpha);
        }

        private static void SetTextAlpha(Graphic text, float alpha)
        {
            var color = text.color;
            text.color = new Color(color.r, color.g, color.b, alpha);
        }

        /// <summary>One continuous, barely-perceptible push-in: scale
        /// 1.00 -&gt; <see cref="PushInPeakScale"/> plus a few units of
        /// positional drift — never a rotation, never a dramatic zoom, per
        /// the brief's explicit "no obvious Ken Burns slideshow" rule. Runs
        /// straight through the t=1.80 art swap with no visible seam, since
        /// it only ever touches <c>localScale</c>/<c>anchoredPosition</c>,
        /// never the sprite itself. Reads <see cref="_artBaseOffset"/> and
        /// <see cref="_artScaleTarget"/> live every frame (not captured once
        /// at start) so the C9.1a settled-Hub composition fix can change
        /// them mid-flight, atomically, at the exact swap instant — see
        /// TransitionRoutine.</summary>
        private IEnumerator PushInRoutine(float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 2f);
                _artRect.localScale = Vector3.one * Mathf.Lerp(1f, _artScaleTarget, eased);
                _artRect.anchoredPosition = _artBaseOffset + new Vector2(Mathf.Lerp(0f, DriftAmount, eased), Mathf.Lerp(0f, -DriftAmount * 0.5f, eased));
                yield return null;
            }

            _artRect.localScale = Vector3.one * _artScaleTarget;
            _artRect.anchoredPosition = _artBaseOffset + new Vector2(DriftAmount, -DriftAmount * 0.5f);
        }

        /// <summary>A plain alpha ramp on one full-stretch <see cref="Image"/>
        /// from <paramref name="fromAlpha"/> to <paramref name="toAlpha"/> —
        /// the one shared shape both the fog rise and fog clear beats use.</summary>
        private static IEnumerator AlphaRoutine(Image image, Color baseColor, float duration, float fromAlpha, float toAlpha)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var alpha = Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(elapsed / duration));
                image.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }

            image.color = new Color(baseColor.r, baseColor.g, baseColor.b, toAlpha);
        }

        /// <summary>Rise-then-fall in one call, for the brief bloom pulse —
        /// shares <see cref="AlphaRoutine"/>'s shape twice back to back
        /// rather than duplicating the loop.</summary>
        private static IEnumerator AlphaPulseRoutine(Image image, Color baseColor, float duration, float peakAlpha)
        {
            var half = duration * 0.5f;
            yield return AlphaRoutine(image, baseColor, half, 0f, peakAlpha);
            yield return AlphaRoutine(image, baseColor, half, peakAlpha, 0f);
        }

        private IEnumerator NavRevealRoutine(float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _navGroup.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            _navGroup.alpha = 1f;
            _navGroup.blocksRaycasts = true;
            _navGroup.interactable = true;
        }

        private void OnArcadeSelected()
        {
            ArcadeRequested?.Invoke();
        }

        /// <summary>Academia/Codex/Historietas have no content yet — a
        /// small inline "Próximamente" cue, never a new screen and never a
        /// fake system, per the brief's explicit constraint.</summary>
        private void OnNonFinalDestinationSelected()
        {
            if (_comingSoonRoutine != null)
            {
                StopCoroutine(_comingSoonRoutine);
            }

            _comingSoonText.text = "Próximamente";
            _comingSoonRoutine = StartCoroutine(ComingSoonRoutine());
        }

        private IEnumerator ComingSoonRoutine()
        {
            _comingSoonText.gameObject.SetActive(true);
            yield return new WaitForSeconds(1.2f);
            _comingSoonText.gameObject.SetActive(false);
            _comingSoonRoutine = null;
        }

        /// <summary>Called when the Arcade destination (currently
        /// <c>GameSelectorHud</c>) is shown over the Hub — the Hub's own
        /// art stays rendered underneath (harmless; the selector's own
        /// opaque panel fully covers it) but its interactive layer is
        /// disabled so no stray keyboard-nav/raycast bleeds through.</summary>
        /// <summary>C9.2: the "Hub label/hotspots fade" half of the short
        /// Hub -&gt; Arcade transition (see ShellInstaller.TransitionToArcadeRoutine)
        /// — interactivity is disabled immediately (synchronous, so no
        /// stray click on another hotspot can land mid-fade), the visual
        /// fade itself just eases the hand-off rather than a hard cut.</summary>
        public void HideNavigation()
        {
            _navGroup.blocksRaycasts = false;
            _navGroup.interactable = false;
            StartCoroutine(RuntimeUIFactory.FadeCanvasGroup(_navGroup, HubNavTransitionFadeDuration, _navGroup.alpha, 0f));
        }

        /// <summary>Called whenever the Shell returns to the Hub — from the
        /// Arcade selector's own Back button, or after a game session ends.
        /// Never replays <see cref="ShowTitle"/>; the Start Screen is
        /// cold-boot entry only, per the brief's explicit "does NOT go back
        /// to Start Screen" rule. Also restores keyboard/gamepad focus to
        /// the Arcade hotspot — <see cref="HideNavigation"/> leaves the
        /// EventSystem's current selection pointing at whatever destination
        /// screen (e.g. GameSelectorHud's own game button) was focused when
        /// it was hidden, which becomes an inactive selection once that
        /// screen hides itself.</summary>
        public void ShowNavigation()
        {
            _navGroup.blocksRaycasts = true;
            _navGroup.interactable = true;
            RuntimeUIFactory.Select(_hotspots[0].Button);
            StartCoroutine(RuntimeUIFactory.FadeCanvasGroup(_navGroup, HubNavTransitionFadeDuration, _navGroup.alpha, 1f));
        }

        /// <summary>Discrete, optional session status — moved here from the
        /// retired <c>ShellHomeHud</c>, same "never more than one short
        /// line" contract.</summary>
        public void SetSessionStatus(string text)
        {
            _sessionStatusText.text = text ?? string.Empty;
        }

        /// <summary>C9.1a: smooth (never instant, never a bounce/pulse)
        /// focus feedback — the focused hotspot's label eases toward
        /// <see cref="LabelFocusScale"/> and its marker eases toward a
        /// brighter, slightly longer bar; every other hotspot eases back
        /// toward its own rest state. Runs every frame while the Hub is
        /// interactive (not gated behind "did the selection just change",
        /// unlike its C9.1 predecessor — a lerp mid-flight still needs
        /// updating on frames where the selection itself is unchanged).</summary>
        private void Update()
        {
            if (_phase != Phase.Done)
            {
                return;
            }

            var selected = UnityEngine.EventSystems.EventSystem.current != null
                ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                : null;

            var lerpAmount = Time.deltaTime * FocusLerpRate;
            foreach (var hotspot in _hotspots)
            {
                var isSelected = hotspot.Button.gameObject == selected;

                var targetLabelScale = isSelected ? LabelFocusScale : 1f;
                var currentLabelScale = hotspot.LabelRect.localScale.x;
                hotspot.LabelRect.localScale = Vector3.one * Mathf.Lerp(currentLabelScale, targetLabelScale, lerpAmount);

                var targetWidth = isSelected ? MarkerFocusWidth : MarkerRestWidth;
                var markerRect = hotspot.Marker.rectTransform;
                var currentWidth = markerRect.sizeDelta.x;
                markerRect.sizeDelta = new Vector2(Mathf.Lerp(currentWidth, targetWidth, lerpAmount), MarkerHeight);

                var restAlpha = hotspot.IsFinal ? MarkerFinalRestAlpha : MarkerNonFinalRestAlpha;
                var targetAlpha = isSelected ? MarkerFocusAlpha : restAlpha;
                var markerColor = hotspot.Marker.color;
                hotspot.Marker.color = new Color(markerColor.r, markerColor.g, markerColor.b, Mathf.Lerp(markerColor.a, targetAlpha, lerpAmount));
            }
        }
    }
}
