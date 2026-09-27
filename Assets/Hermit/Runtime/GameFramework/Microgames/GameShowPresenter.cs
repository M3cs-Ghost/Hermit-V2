using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hermit.Games.Clasico.Microgames;
using Hermit.Runtime.GameFramework;

namespace Hermit.Runtime.GameFramework.Microgames
{
    /// <summary>
    /// C8.1e Gold replacement — TrueFalse, dressed as a real stylized TV
    /// game-show stage. Replaces C8.1d's <c>CreatePortraitFrame</c> presenter
    /// card (structurally fine, visually rejected — see
    /// Docs/C8_1D_GOLD_ART_INTEGRATION.md, "Manual Validation Correction —
    /// Portrait Integration Rejected") with a real in-world actor standing
    /// directly on the illustrated stage, matching the model
    /// `WesternShootoutPresenter` already established. Full design record,
    /// art inventory, and the Step 1/Step 2 asset-sufficiency gate this
    /// phase went through: Docs/C8_1E_GAME_SHOW_GOLD_REPLACEMENT.md.
    ///
    /// C8.1k showmanship pass (Docs/C8_1K_GAME_SHOW_SHOWMANSHIP.md): the
    /// accepted stage/layout is unchanged; on top of it the round now plays
    /// as a short broadcast — a preamble (lights/host entrance -&gt; PREMIO
    /// $1,000,000 plaque -&gt; statement entrance) inside Game Show's own
    /// Intro phase, an answer-lock suspense beat before the reveal, a
    /// dedicated correct/incorrect reveal (the correct zone is always
    /// confirmed, even on a wrong answer or timeout), a post-reveal
    /// explanation card, and dedicated GameShow_* audio hooks. Every piece of
    /// motion runs on this presenter's own tracked coroutines, all cancelled
    /// by <see cref="Hide"/>/<see cref="ShowChallenge"/> — no residue between
    /// rounds.
    ///
    /// Presentation only — <see cref="AnswerChosen"/>, <see cref="ShowChallenge"/>,
    /// <see cref="RevealOutcome"/>, and the exact GameObject names
    /// <c>"GameShowTrue"</c>/<c>"GameShowFalse"</c> are unchanged from every
    /// prior phase; scoring and question generation were not touched.
    /// </summary>
    internal sealed class GameShowPresenter : IMicrogamePresenter
    {
        private const int ConfettiCount = 8;

        private const float PresenterHeight = 420f;
        private const float PresenterWidth = 320f; // matches the actor sprite's own ~891:1168 post-crop aspect
        private const float PresenterRestScale = 1f;
        private const float PresenterIntroStartScale = 0.94f;
        private static readonly Vector2 PresenterRestPosition = new Vector2(0f, 40f);
        private const float IdleDriftAmplitude = 2f;
        private const float IdleDriftPeriod = 2.6f;

        private const float ZoneWidth = 260f;
        private const float ZoneHeight = 130f;
        private const float ZoneOffsetX = 340f;
        private const float ZoneCenterY = 110f;
        private const float ZoneRestGlowAlpha = 0.16f;
        private const float ZoneLockGlowAlpha = 0.42f;
        private const float ZoneRevealGlowAlpha = 0.55f;
        private const float ZoneLockedScale = 1.04f;

        // C8.1k.1: -78 -> -56. The -78 offset existed only to clear
        // ClasicoHud's shared "¡DECIDE!" Intro banner, which Game Show no
        // longer shows (C8.1k); the 22px it frees is what lets the prize
        // plaque sit above the host. The panel's top still clears the HUD's
        // Salir button by ~8px (verified in
        // GameShow_ShowmanshipElements_DoNotOverlapStatementZonesPresenterOrHud).
        private static readonly Vector2 StatementRestPosition = new Vector2(0f, -56f);

        private static readonly Vector2 PrizePlaqueRestPosition = new Vector2(0f, 481f);
        private static readonly Vector2 PrizePlaqueSize = new Vector2(260f, 58f);
        private const float StatementSlideOffset = 18f;

        // C8.1k preamble choreography — sized to finish inside
        // ClasicoGameDefinition.GameShowIntroSeconds (1.5s) with a small
        // margin; RevealAfterIntro (the director's real Intro->Decision
        // gate) still force-finishes it and is the only thing that ever
        // enables input.
        private const float OpeningEnd = 0.40f;
        private const float PrizeStart = 0.30f;
        private const float PrizeEnd = 0.75f;
        private const float QuestionStart = 0.75f;
        private const float QuestionEnd = 1.10f;
        private const float ZonesStart = 1.00f;
        private const float ZonesEnd = 1.30f;
        private const float PrizeSettleStart = 1.10f;
        private const float PreambleVisualDuration = 1.35f;

        private const float PrizeSecondaryScale = 0.92f;
        private const float PrizeSecondaryAlpha = 0.82f;

        // Answer-lock suspense: the director's own Lock phase (0.2s) already
        // separates the click from Feedback; this adds the remainder of a
        // ~0.4s total beat before correctness is shown. A timeout has no
        // lock to hold, so it only gets a short breath.
        private const float SuspenseAfterLockSeconds = 0.20f;
        private const float SuspenseUnlockedSeconds = 0.15f;
        private const float ExplanationDelaySeconds = 0.35f;

        private const string PrizeAmountLabel = "$1,000,000";

        public event Action<bool> AnswerChosen;

        private readonly MonoBehaviour _host;

        private RectTransform _root;
        private RectTransform _statementArea;
        private CanvasGroup _statementGroup;
        private Text _statementText;

        private RectTransform _presenterRect;
        private Image _presenterImage;
        private RectTransform _presenterSpotlight;
        private Image _presenterSpotlightImage;
        private RectTransform _contactShadow;
        private bool _presenterIsProcedural;
        private CharacterPrimitives.Face _proceduralFace;

        private Image _resultFlash;

        private Button _trueButton;
        private Button _falseButton;
        private Image _trueGlow;
        private Image _falseGlow;
        private RectTransform _trueZone;
        private RectTransform _falseZone;
        private Text _trueLabel;
        private Text _falseLabel;
        private TMP_Text _trueCorrectTag;
        private TMP_Text _falseCorrectTag;

        private RectTransform _prizePlaque;
        private CanvasGroup _prizeGroup;
        private TMP_Text _prizeAmountText;
        private TMP_Text _progressText;
        private RectTransform _progressBadge;
        private CanvasGroup _progressGroup;
        private RectTransform _prizeSweep;

        private RectTransform _explanationCard;
        private CanvasGroup _explanationGroup;
        private TMP_Text _explanationVerdictText;
        private TMP_Text _explanationText;

        private readonly List<Image> _confetti = new List<Image>();
        private readonly List<RectTransform> _confettiRects = new List<RectTransform>();

        private AudioSource _sfxAudioSource;
        private AudioSource _ambienceAudioSource;
        private AudioClip _openingClip;
        private AudioClip _prizeRevealClip;
        private AudioClip _questionRevealClip;
        private AudioClip _answerLockClip;
        private AudioClip _correctRevealClip;
        private AudioClip _incorrectRevealClip;
        private AudioClip _ambienceClip;
        private const float AmbienceVolume = 0.6f;

        // Every coroutine this presenter starts is tracked here and stopped
        // by CancelSequence — plus a generation counter every routine checks
        // after each yield, the same belt-and-braces pattern Western's
        // C8.1d.10 cancellation fix established.
        private readonly List<Coroutine> _activeRoutines = new List<Coroutine>();
        private int _sequenceGeneration;
        private Coroutine _preambleRoutine;
        private Coroutine _idleRoutine;

        private bool _introActive;
        private bool _decisionOpen;
        private bool _answerLocked;
        private int _lockedIndex = -1;
        private int _questionNumber;

        private TrueFalseChallenge _challenge;
        private HermitTheme Theme => RuntimeUIFactory.Theme;

        private static readonly Color PrizeGold = new Color(0.97f, 0.80f, 0.42f, 1f);
        private static readonly Color PlaqueFill = new Color(0.07f, 0.04f, 0.10f, 0.88f);
        private static readonly Color PlaqueBorder = new Color(0.78f, 0.60f, 0.30f, 1f);
        private static readonly Color RevealConfirmColor = new Color(0.55f, 0.92f, 0.52f, 1f);
        private static readonly Color MutedIncorrectColor = new Color(0.86f, 0.42f, 0.42f, 1f);

        public GameShowPresenter(MonoBehaviour host)
        {
            _host = host;
        }

        public void Build(Transform stageRoot)
        {
            _root = RuntimeUIFactory.CreatePanel(stageRoot, "GameShow", new Color(0.08f, 0.05f, 0.11f, 1f));

            BuildBackground();
            BuildAmbientVignette();
            BuildPresenterSpotlight();
            BuildPrizePlaque();
            BuildContactShadow();
            BuildPresenterActor();
            BuildStatementArea();
            BuildProgressBadge();
            BuildChoiceZone(isTrue: true, xOffset: -ZoneOffsetX, name: "ChoiceLeft", buttonName: "GameShowTrue", label: "VERDADERO");
            BuildChoiceZone(isTrue: false, xOffset: ZoneOffsetX, name: "ChoiceRight", buttonName: "GameShowFalse", label: "FALSO");
            BuildExplanationCard();
            BuildResultFlash();
            BuildConfetti(_root);
            BuildAudio();

            ResetVisuals(forPreamble: false);
            _root.gameObject.SetActive(false);
        }

        /// <summary>Unchanged from C8.1d — the illustrated stage already
        /// passed this phase's Step 1 audit as Gold-ready as-is (see the
        /// doc, "Existing Game Show world art"). Only the procedural
        /// curtain/floor/stage-light fallback (missing-asset degrade path)
        /// remains from the old implementation, kept only because the
        /// background could theoretically fail to load, never because it's
        /// still expected to trigger.</summary>
        private void BuildBackground()
        {
            var background = RuntimeUIFactory.LoadArt("Art/Gold/GameShow/GameShowBackground");
            if (background != null)
            {
                RuntimeUIFactory.CreateBackgroundImage(_root, "Background", background);
                return;
            }

            BuildCurtain(_root, 0.06f);
            BuildCurtain(_root, 0.94f);

            var floor = RuntimeUIFactory.CreatePanel(_root, "Floor", new Color(0.10f, 0.06f, 0.14f, 1f));
            floor.anchorMin = Vector2.zero;
            floor.anchorMax = new Vector2(1f, 0.16f);
            floor.offsetMin = Vector2.zero;
            floor.offsetMax = Vector2.zero;
        }

        private static void BuildCurtain(Transform parent, float xAnchor)
        {
            var curtain = RuntimeUIFactory.CreatePanel(parent, "Curtain", new Color(0.22f, 0.08f, 0.18f, 1f));
            curtain.anchorMin = new Vector2(xAnchor - 0.05f, 0f);
            curtain.anchorMax = new Vector2(xAnchor + 0.05f, 1f);
            curtain.offsetMin = Vector2.zero;
            curtain.offsetMax = Vector2.zero;
        }

        /// <summary>A very restrained darkening at the frame edges — unlike
        /// ArcadeGalleryHud's own vignette (which compensates for a flat
        /// panel with no art of its own), this stage is already a rich,
        /// fully-composed illustration, so this only needs to nudge focus
        /// toward the center, never redarken the scene.</summary>
        private void BuildAmbientVignette()
        {
            var vignetteRect = RuntimeUIFactory.CreatePanel(_root, "AmbientVignette", Color.white);
            var vignette = vignetteRect.GetComponent<Image>();
            vignette.sprite = RuntimeUIFactory.GetVignetteSprite();
            vignette.type = Image.Type.Simple;
            vignette.color = new Color(0.04f, 0.02f, 0.06f, 0.22f);
            vignette.raycastTarget = false;
        }

        private void BuildPresenterSpotlight()
        {
            var spotlightGo = new GameObject("PresenterSpotlight", typeof(RectTransform), typeof(Image));
            _presenterSpotlight = (RectTransform)spotlightGo.transform;
            _presenterSpotlight.SetParent(_root, false);
            _presenterSpotlight.anchorMin = _presenterSpotlight.anchorMax = new Vector2(0.5f, 0f);
            _presenterSpotlight.anchoredPosition = new Vector2(0f, 260f);
            _presenterSpotlight.sizeDelta = new Vector2(620f, 520f);

            _presenterSpotlightImage = spotlightGo.GetComponent<Image>();
            _presenterSpotlightImage.sprite = RuntimeUIFactory.GetRadialGlowSprite();
            _presenterSpotlightImage.raycastTarget = false;
            _presenterSpotlightImage.color = new Color(1f, 0.93f, 0.72f, 0f);
        }

        /// <summary>The presenter's own soft floor contact — a low-alpha,
        /// blurred-reading dark ellipse (a rounded panel scaled flat), never
        /// a harsh black oval, per the brief's explicit grounding
        /// requirement. Built (and z-ordered) before the actor itself so it
        /// always renders behind him.</summary>
        private void BuildContactShadow()
        {
            _contactShadow = RuntimeUIFactory.CreateRoundedPanel(_root, "ContactShadow", new Color(0f, 0f, 0f, 0.30f), 40);
            _contactShadow.anchorMin = _contactShadow.anchorMax = new Vector2(0.5f, 0f);
            _contactShadow.anchoredPosition = new Vector2(0f, 34f);
            _contactShadow.sizeDelta = new Vector2(210f, 46f);
            _contactShadow.GetComponent<Image>().raycastTarget = false;
        }

        /// <summary>The real in-world actor — Presentador_Gameplay_01,
        /// standing directly on the stage floor, never inside a portrait
        /// frame/card. Falls back to a small procedural silhouette (same
        /// "warn and degrade, never crash" contract every Gold presenter
        /// uses) if the art is ever missing — dormant today since the art
        /// resolves.</summary>
        private void BuildPresenterActor()
        {
            var art = RuntimeUIFactory.LoadArt("Art/Gold/GameShow/Actors/Presentador_Gameplay_01");

            var actorGo = new GameObject("PresenterActor", typeof(RectTransform), typeof(Image));
            _presenterRect = (RectTransform)actorGo.transform;
            _presenterRect.SetParent(_root, false);
            _presenterRect.anchorMin = _presenterRect.anchorMax = new Vector2(0.5f, 0f);
            _presenterRect.pivot = new Vector2(0.5f, 0f);
            _presenterRect.anchoredPosition = PresenterRestPosition;
            _presenterRect.sizeDelta = new Vector2(PresenterWidth, PresenterHeight);

            _presenterImage = actorGo.GetComponent<Image>();
            _presenterImage.raycastTarget = false;

            if (art != null)
            {
                _presenterImage.sprite = art;
                _presenterImage.preserveAspect = true;
                _presenterIsProcedural = false;
            }
            else
            {
                _presenterImage.color = new Color(0.16f, 0.16f, 0.22f, 1f);
                var head = RuntimeUIFactory.CreateRoundedPanel(_presenterRect, "ProceduralHead", new Color(0.85f, 0.68f, 0.52f, 1f), 30);
                head.anchorMin = head.anchorMax = new Vector2(0.5f, 1f);
                head.anchoredPosition = new Vector2(0, -50);
                head.sizeDelta = new Vector2(70, 70);
                _proceduralFace = CharacterPrimitives.Build(head, 70f);
                _presenterIsProcedural = true;
            }
        }

        private void BuildStatementArea()
        {
            var area = new GameObject("StatementArea", typeof(RectTransform), typeof(CanvasGroup));
            _statementArea = (RectTransform)area.transform;
            _statementArea.SetParent(_root, false);
            _statementArea.anchorMin = new Vector2(0f, 1f);
            _statementArea.anchorMax = new Vector2(1f, 1f);
            // C8.1e.1: -46 (this area's original offset) put the statement
            // directly under ClasicoHud's shared "¡DECIDE!" Intro banner
            // (CommandText, anchored independently to the full playing
            // panel) for the ~0.6s Intro window every Game Show round —
            // confirmed by a real Windows-build screenshot at 1280x720,
            // where the two texts visibly overlapped. -78 clears the
            // banner's glyphs with margin while staying well above the
            // presenter's head. (C8.1k: Game Show no longer shows that
            // banner at all — its own preamble is the cue. C8.1k.1 then
            // moved it to -56 to make room for the prize plaque above the
            // host; see StatementRestPosition.)
            _statementArea.anchoredPosition = StatementRestPosition;
            _statementArea.sizeDelta = new Vector2(0, 100);

            // C8.1k: the statement now fades/slides in as the last preamble
            // beat instead of sitting there from frame one.
            _statementGroup = area.GetComponent<CanvasGroup>();
            _statementGroup.interactable = false;
            _statementGroup.blocksRaycasts = false;

            // C8.1j: the statement previously floated with no backing panel
            // at all over the illustrated stage — every other text surface
            // in every other archetype has one (brief section 14: "avoid
            // one generic semi-transparent black rectangle reused across
            // all four worlds... each archetype should have its own visual
            // grammar", and Game Show already HAS its own grammar for this —
            // the same translucent display-panel + radial glow
            // BuildChoiceZone already uses for the True/False zones — this
            // was simply never applied to the Statement text itself. Reused
            // verbatim rather than inventing a new treatment, so Game
            // Show's "accepted" layout/grammar is extended, not redesigned.
            var glowGo = new GameObject("StatementGlow", typeof(RectTransform), typeof(Image));
            var glowRect = (RectTransform)glowGo.transform;
            glowRect.SetParent(_statementArea, false);
            RuntimeUIFactory.StretchFull(glowRect);
            // C8.1m: vertical overhang only. The area already spans the full
            // stage width, so the old +40 horizontal overhang only pushed
            // the glow 20px past both stage edges (off-screen) and made it
            // wider than the Game Show root — caught by the real-scene
            // "no descendant wider than its presenter" guard.
            glowRect.sizeDelta = new Vector2(0, 40);
            var glowImage = glowGo.GetComponent<Image>();
            glowImage.sprite = RuntimeUIFactory.GetRadialGlowSprite();
            glowImage.color = new Color(Theme.AccentWarm.r, Theme.AccentWarm.g, Theme.AccentWarm.b, 0.10f);
            glowImage.raycastTarget = false;

            var statementPanel = RuntimeUIFactory.CreateRoundedPanel(_statementArea, "StatementPanel", new Color(0.05f, 0.04f, 0.08f, 0.55f));
            statementPanel.sizeDelta = new Vector2(-40, -8);
            statementPanel.GetComponent<Image>().raycastTarget = false;

            // Best-fit auto-shrink (brief section 13: Game Show now has 30
            // statements, several notably longer than the original 8 — must
            // never overflow past this area into ClasicoHud's Intro banner
            // above it, per this area's own -78 tuning note) — verified
            // per-statement in Statement fit test.
            _statementText = RuntimeUIFactory.CreateText(
                _statementArea, "Statement", string.Empty, Theme.BodySize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760, 96));
            _statementText.resizeTextForBestFit = true;
            _statementText.resizeTextMinSize = 16;
            _statementText.resizeTextMaxSize = Theme.BodySize;
        }

        /// <summary>C8.1k: the broadcast "prize at stake" graphic — a
        /// restrained gold-edged plaque (PREMIO / $1,000,000). A purely
        /// thematic show prize: it is never tied to score or to any real
        /// currency. Revealed with a single pop + light sweep during the
        /// preamble, then settles smaller/dimmer so the statement stays
        /// primary once gameplay starts.
        ///
        /// C8.1k.1: moved from stage-left to directly ABOVE THE HOST so he
        /// visibly "owns" the prize. Sized to the real gap between the host
        /// sprite's head (its opaque top sits at stage y ~445 — the sprite
        /// has a 46/1232 transparent top margin inside the 420px actor rect)
        /// and the statement panel (moved up 22px for this, see
        /// StatementRestPosition): the plaque spans y 452..510 at rest.
        /// Built BEFORE the actor so it draws behind him — the host's own
        /// entrance/celebration lift can pass in front of it, never the
        /// other way round. The "PREGUNTA n" tag no longer fits inside, so
        /// it became its own small badge (<see cref="BuildProgressBadge"/>).</summary>
        private void BuildPrizePlaque()
        {
            var (plaque, content) = RuntimeUIFactory.CreatePremiumPanel(_root, "PrizePlaque", PlaqueFill, PlaqueBorder, 2.5f, 12);
            _prizePlaque = plaque;
            _prizePlaque.anchorMin = _prizePlaque.anchorMax = new Vector2(0.5f, 0f);
            _prizePlaque.anchoredPosition = PrizePlaqueRestPosition;
            _prizePlaque.sizeDelta = PrizePlaqueSize;
            _prizePlaque.GetComponent<Image>().raycastTarget = false;
            content.gameObject.AddComponent<RectMask2D>();

            _prizeGroup = plaque.gameObject.AddComponent<CanvasGroup>();
            _prizeGroup.interactable = false;
            _prizeGroup.blocksRaycasts = false;

            // A soft diagonal light band that crosses the plaque once as it
            // is revealed (and again on a correct answer) — clipped by the
            // plaque's own RectMask2D, so it reads as a broadcast-graphic
            // sheen, never as a separate flying object.
            var sweepGo = new GameObject("PrizeSweep", typeof(RectTransform), typeof(Image));
            _prizeSweep = (RectTransform)sweepGo.transform;
            _prizeSweep.SetParent(content, false);
            _prizeSweep.anchorMin = _prizeSweep.anchorMax = new Vector2(0.5f, 0.5f);
            _prizeSweep.sizeDelta = new Vector2(40f, 120f);
            _prizeSweep.localRotation = Quaternion.Euler(0f, 0f, -24f);
            var sweepImage = sweepGo.GetComponent<Image>();
            sweepImage.sprite = RuntimeUIFactory.GetRadialGlowSprite();
            sweepImage.color = new Color(1f, 0.92f, 0.70f, 0.35f);
            sweepImage.raycastTarget = false;

            RuntimeUIFactory.CreatePremiumText(
                content, "PrizeCaption", "PREMIO", 13f, Theme.AccentWarm,
                TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(240f, 16f),
                characterSpacing: 10f);

            _prizeAmountText = RuntimeUIFactory.CreatePremiumText(
                content, "PrizeAmount", PrizeAmountLabel, 28f, PrizeGold,
                TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(240f, 32f),
                fontStyle: FontStyles.Bold, autoShrinkMinSize: 22f);
        }

        /// <summary>C8.1k.1: the on-air "PREGUNTA n" tag, split out of the
        /// prize plaque when the plaque moved above the host — a small
        /// broadcast caption with a gold underline in the open upper
        /// stage-left space (where the plaque used to be), revealed and
        /// dimmed together with the plaque.</summary>
        private void BuildProgressBadge()
        {
            var badgeGo = new GameObject("ProgressBadge", typeof(RectTransform), typeof(CanvasGroup));
            _progressBadge = (RectTransform)badgeGo.transform;
            _progressBadge.SetParent(_root, false);
            _progressBadge.anchorMin = _progressBadge.anchorMax = new Vector2(0.5f, 0f);
            _progressBadge.anchoredPosition = new Vector2(-440f, 455f);
            _progressBadge.sizeDelta = new Vector2(220f, 34f);
            _progressGroup = badgeGo.GetComponent<CanvasGroup>();
            _progressGroup.interactable = false;
            _progressGroup.blocksRaycasts = false;

            _progressText = RuntimeUIFactory.CreatePremiumText(
                _progressBadge, "ProgressTag", string.Empty, 17f, new Color(Theme.AccentWarm.r, Theme.AccentWarm.g, Theme.AccentWarm.b, 0.9f),
                TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -13f), new Vector2(220f, 26f),
                characterSpacing: 6f);

            var underline = RuntimeUIFactory.CreatePanel(_progressBadge, "ProgressUnderline", new Color(PlaqueBorder.r, PlaqueBorder.g, PlaqueBorder.b, 0.7f));
            underline.anchorMin = underline.anchorMax = new Vector2(0.5f, 0f);
            underline.anchoredPosition = new Vector2(0f, 3f);
            underline.sizeDelta = new Vector2(150f, 1.5f);
            underline.GetComponent<Image>().raycastTarget = false;
        }

        /// <summary>One "stage choice" zone — a soft radial glow (the fake-
        /// lighting language this whole stage uses) behind a small
        /// translucent display panel behind the actual label, with the
        /// invisible full-zone <see cref="Button"/> on top as the real hit
        /// target. Reads as an illuminated architectural stage panel, never
        /// a flat opaque UI card. <paramref name="buttonName"/> is preserved
        /// exactly (<c>"GameShowTrue"</c>/<c>"GameShowFalse"</c>) — every
        /// existing test and input helper depends on it.</summary>
        private void BuildChoiceZone(bool isTrue, float xOffset, string name, string buttonName, string label)
        {
            var zone = new GameObject(name, typeof(RectTransform));
            var zoneRect = (RectTransform)zone.transform;
            zoneRect.SetParent(_root, false);
            zoneRect.anchorMin = zoneRect.anchorMax = new Vector2(0.5f, 0f);
            zoneRect.anchoredPosition = new Vector2(xOffset, ZoneCenterY);
            zoneRect.sizeDelta = new Vector2(ZoneWidth, ZoneHeight);

            var glowRect = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            var glow = (RectTransform)glowRect.transform;
            glow.SetParent(zoneRect, false);
            RuntimeUIFactory.StretchFull(glow);
            glow.sizeDelta = new Vector2(60, 60);
            var glowImage = glowRect.GetComponent<Image>();
            glowImage.sprite = RuntimeUIFactory.GetRadialGlowSprite();
            glowImage.raycastTarget = false;
            glowImage.color = new Color(Theme.AccentWarm.r, Theme.AccentWarm.g, Theme.AccentWarm.b, 0f);

            var panel = RuntimeUIFactory.CreateRoundedPanel(zoneRect, "Panel", new Color(0.05f, 0.04f, 0.08f, 0.38f));
            panel.sizeDelta = new Vector2(-20, -30);
            panel.GetComponent<Image>().raycastTarget = false;

            var button = RuntimeUIFactory.CreateButton(zoneRect, buttonName, string.Empty, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var buttonImage = button.GetComponent<Image>();
            buttonImage.color = new Color(0f, 0f, 0f, 0f);
            UnityEngine.Object.Destroy(button.GetComponentInChildren<Text>().gameObject);

            var labelText = RuntimeUIFactory.CreateText(
                button.transform, "Label", label, Theme.ButtonSize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            labelText.fontStyle = FontStyle.Bold;
            labelText.raycastTarget = false;

            // C8.1k: "the correct answer must be unmistakable" — a small
            // caption above whichever zone is right, shown on every reveal
            // (correct, incorrect, or timeout). Sits in the open band
            // between the zone's own glow and the plaque/explanation card.
            var correctTag = RuntimeUIFactory.CreatePremiumText(
                zoneRect, "CorrectTag", "RESPUESTA CORRECTA", 15f, RevealConfirmColor,
                TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 26f), new Vector2(ZoneWidth, 24f),
                fontStyle: FontStyles.Bold, characterSpacing: 4f);

            button.onClick.AddListener(() => AnswerChosen?.Invoke(isTrue));

            if (isTrue)
            {
                _trueButton = button;
                _trueGlow = glowImage;
                _trueZone = zoneRect;
                _trueLabel = labelText;
                _trueCorrectTag = correctTag;
            }
            else
            {
                _falseButton = button;
                _falseGlow = glowImage;
                _falseZone = zoneRect;
                _falseLabel = labelText;
                _falseCorrectTag = correctTag;
            }
        }

        /// <summary>C8.1k: the post-reveal "on-air graphic" for
        /// <see cref="TrueFalseChallenge.FeedbackExplanation"/> — the same
        /// gold-edged plaque language as the prize graphic, stage-right
        /// above the FALSO zone (mirroring the plaque), so it overlaps
        /// neither the statement band, the presenter, the zones, nor the
        /// shared HUD. Carries Game Show's own verdict line, replacing the
        /// shared HUD banner for this archetype. All 30 explanations are
        /// verified to fit in GameShow_AllFeedbackExplanations_FitTheExplanationCard.</summary>
        private void BuildExplanationCard()
        {
            var (card, content) = RuntimeUIFactory.CreatePremiumPanel(_root, "ExplanationCard", PlaqueFill, PlaqueBorder, 3f, 14);
            _explanationCard = card;
            _explanationCard.anchorMin = _explanationCard.anchorMax = new Vector2(0.5f, 0f);
            _explanationCard.anchoredPosition = new Vector2(405f, 335f);
            _explanationCard.sizeDelta = new Vector2(400f, 176f);
            _explanationCard.GetComponent<Image>().raycastTarget = false;

            _explanationGroup = card.gameObject.AddComponent<CanvasGroup>();
            _explanationGroup.interactable = false;
            _explanationGroup.blocksRaycasts = false;

            _explanationVerdictText = RuntimeUIFactory.CreatePremiumText(
                content, "ExplanationVerdict", string.Empty, 20f, PrizeGold,
                TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(370f, 30f),
                fontStyle: FontStyles.Bold, characterSpacing: 3f);

            var divider = RuntimeUIFactory.CreatePanel(content, "ExplanationDivider", new Color(PlaqueBorder.r, PlaqueBorder.g, PlaqueBorder.b, 0.55f));
            divider.anchorMin = divider.anchorMax = new Vector2(0.5f, 1f);
            divider.anchoredPosition = new Vector2(0f, -42f);
            divider.sizeDelta = new Vector2(200f, 1.5f);
            divider.GetComponent<Image>().raycastTarget = false;

            _explanationText = RuntimeUIFactory.CreatePremiumText(
                content, "GameShowExplanation", string.Empty, ExplanationMaxSize, Theme.TextPrimary,
                TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(ExplanationBoxWidth, ExplanationBoxHeight),
                lineSpacing: 2f, autoShrinkMinSize: ExplanationMinSize);
        }

        // Mirrored by ClasicoTypographyFitTests' Game Show explanation
        // constants — change both together.
        internal const float ExplanationBoxWidth = 368f;
        internal const float ExplanationBoxHeight = 106f;
        internal const float ExplanationMinSize = 14f;
        internal const float ExplanationMaxSize = 18f;

        private void BuildResultFlash()
        {
            var flashRect = RuntimeUIFactory.CreatePanel(_root, "ResultFlash", new Color(0f, 0f, 0f, 0f));
            _resultFlash = flashRect.GetComponent<Image>();
            _resultFlash.raycastTarget = false;
        }

        private void BuildConfetti(Transform parent)
        {
            // C8.1k: restrained broadcast gold/ivory only — no rainbow.
            var palette = new[] { PrizeGold, Theme.AccentWarm, new Color(1f, 0.85f, 0.55f, 1f), RevealConfirmColor };
            for (var i = 0; i < ConfettiCount; i++)
            {
                var piece = RuntimeUIFactory.CreatePanel(parent, "Confetti", palette[i % palette.Length]);
                piece.anchorMin = piece.anchorMax = new Vector2(0.5f, 0f);
                piece.anchoredPosition = new Vector2(0, 300f);
                piece.sizeDelta = new Vector2(8, 14);
                var img = piece.GetComponent<Image>();
                img.raycastTarget = false;
                img.color = new Color(palette[i % palette.Length].r, palette[i % palette.Length].g, palette[i % palette.Length].b, 0f);
                _confetti.Add(img);
                _confettiRects.Add(piece);
            }
        }

        /// <summary>C8.1k: seven GameShow_* hooks, each loading a real asset
        /// from <c>Resources/Audio/Gold/GameShow/...</c>. C8.1k.1: a missing
        /// asset now means SILENCE — the C8.1k noise-built temporary
        /// fallbacks were removed after manual review (they read as ocean
        /// noise / sustained hiss). <see cref="PlayCue"/> and the ambience
        /// start both skip a null clip. The shared HUD correct/incorrect ding
        /// stays opted out for this archetype (ClasicoGameHost). Both
        /// sources live under this presenter's own root, so they are named
        /// and scoped to Game Show alone.</summary>
        private void BuildAudio()
        {
            var audioGo = new GameObject("GameShowAudio");
            audioGo.transform.SetParent(_root, false);

            _sfxAudioSource = audioGo.AddComponent<AudioSource>();
            _sfxAudioSource.playOnAwake = false;
            _sfxAudioSource.spatialBlend = 0f;

            _ambienceAudioSource = audioGo.AddComponent<AudioSource>();
            _ambienceAudioSource.playOnAwake = false;
            _ambienceAudioSource.spatialBlend = 0f;
            _ambienceAudioSource.loop = true;

            _openingClip = RuntimeUIFactory.LoadAudio("Audio/Gold/GameShow/GameShow_Opening");
            _prizeRevealClip = RuntimeUIFactory.LoadAudio("Audio/Gold/GameShow/GameShow_PrizeReveal");
            _questionRevealClip = RuntimeUIFactory.LoadAudio("Audio/Gold/GameShow/GameShow_QuestionReveal");
            _answerLockClip = RuntimeUIFactory.LoadAudio("Audio/Gold/GameShow/GameShow_AnswerLock");
            _correctRevealClip = RuntimeUIFactory.LoadAudio("Audio/Gold/GameShow/GameShow_CorrectReveal");
            _incorrectRevealClip = RuntimeUIFactory.LoadAudio("Audio/Gold/GameShow/GameShow_IncorrectReveal");
            _ambienceClip = RuntimeUIFactory.LoadAudio("Audio/Gold/GameShow/GameShow_AmbienceLoop");
        }

        private void PlayCue(string hookName, AudioClip clip)
        {
            if (clip == null || _sfxAudioSource == null)
            {
                return;
            }

            _sfxAudioSource.PlayOneShot(clip);
            GameShowAudioEvents.Record(hookName, clip);
        }

        /// <summary>Called by <see cref="ClasicoGameHost"/> whenever a new
        /// Clásico session starts (launch or restart), so the on-air
        /// "PREGUNTA n" count restarts with it. Presentation-only — not a
        /// progression system.</summary>
        public void ResetSession()
        {
            _questionNumber = 0;
        }

        public void ShowChallenge(TrueFalseChallenge challenge)
        {
            CancelSequence();

            _challenge = challenge;
            _questionNumber++;
            _root.gameObject.SetActive(true);

            _statementText.text = challenge.Statement;
            _progressText.text = $"PREGUNTA {_questionNumber}";
            _explanationText.text = challenge.FeedbackExplanation ?? string.Empty;
            _explanationVerdictText.text = string.Empty;

            ResetVisuals(forPreamble: true);

            _introActive = true;
            _decisionOpen = false;

            if (_ambienceClip != null)
            {
                _ambienceAudioSource.clip = _ambienceClip;
                _ambienceAudioSource.volume = AmbienceVolume;
                _ambienceAudioSource.Play();
                GameShowAudioEvents.Record("GameShow_AmbienceLoop", _ambienceClip);
            }

            _preambleRoutine = StartTracked(PreambleRoutine());
        }

        /// <summary>The broadcast opening, played inside Game Show's own
        /// Intro phase (ClasicoGameDefinition.GameShowIntroSeconds):
        ///
        /// 0.00-0.40 OPENING — spotlight rises, the host fades in with a
        /// small entrance punch (0.94 -&gt; 1.03 -&gt; 1.00). GameShow_Opening.
        /// 0.30-0.75 PRIZE — the PREMIO $1,000,000 plaque pops in (0.85 -&gt;
        /// 1.04 -&gt; 1.00) and a light sweep crosses it. GameShow_PrizeReveal.
        /// 0.75-1.10 QUESTION — the host gives a small "presenting" nod and
        /// the statement panel fades/slides into place. GameShow_QuestionReveal.
        /// 1.00-1.30 the VERDADERO/FALSO zones light up to rest.
        /// 1.10-1.35 the plaque settles smaller/dimmer (secondary).
        ///
        /// Cosmetic only — input is never enabled here; see
        /// <see cref="RevealAfterIntro"/>.</summary>
        private IEnumerator PreambleRoutine()
        {
            var generation = _sequenceGeneration;
            var prizeCued = false;
            var questionCued = false;

            PlayCue("GameShow_Opening", _openingClip);

            var elapsed = 0f;
            while (elapsed < PreambleVisualDuration)
            {
                elapsed += Time.deltaTime;
                if (generation != _sequenceGeneration)
                {
                    yield break;
                }

                // OPENING
                var openT = Mathf.Clamp01(elapsed / OpeningEnd);
                var openEased = 1f - Mathf.Pow(1f - openT, 2f);
                _presenterImage.color = new Color(1f, 1f, 1f, openEased);
                _presenterSpotlightImage.color = new Color(1f, 0.93f, 0.72f, Mathf.Lerp(0f, 0.24f, openEased));
                var entranceScale = openT < 0.75f
                    ? Mathf.Lerp(PresenterIntroStartScale, 1.03f, openT / 0.75f)
                    : Mathf.Lerp(1.03f, PresenterRestScale, (openT - 0.75f) / 0.25f);

                // QUESTION — the host's "presenting" nod layers on top.
                var nodT = Mathf.Clamp01((elapsed - QuestionStart) / 0.25f);
                var nod = elapsed >= QuestionStart ? Mathf.Sin(nodT * Mathf.PI) * 0.025f : 0f;
                _presenterRect.localScale = Vector3.one * (entranceScale + nod);

                // PRIZE
                if (!prizeCued && elapsed >= PrizeStart)
                {
                    prizeCued = true;
                    PlayCue("GameShow_PrizeReveal", _prizeRevealClip);
                }

                var prizeT = Mathf.Clamp01((elapsed - PrizeStart) / (PrizeEnd - PrizeStart));
                var settleT = Mathf.Clamp01((elapsed - PrizeSettleStart) / (PreambleVisualDuration - PrizeSettleStart));
                var popScale = prizeT < 0.55f
                    ? Mathf.Lerp(0.85f, 1.04f, prizeT / 0.55f)
                    : Mathf.Lerp(1.04f, 1f, (prizeT - 0.55f) / 0.45f);
                _prizePlaque.localScale = Vector3.one * Mathf.Lerp(popScale, PrizeSecondaryScale, settleT);
                _prizeGroup.alpha = Mathf.Lerp(Mathf.Clamp01(prizeT / 0.35f), PrizeSecondaryAlpha, settleT);
                _progressGroup.alpha = Mathf.Clamp01(prizeT / 0.35f);
                SetSweepProgress(Mathf.Clamp01((prizeT - 0.2f) / 0.8f));

                if (!questionCued && elapsed >= QuestionStart)
                {
                    questionCued = true;
                    PlayCue("GameShow_QuestionReveal", _questionRevealClip);
                }

                var questionT = Mathf.Clamp01((elapsed - QuestionStart) / (QuestionEnd - QuestionStart));
                var questionEased = 1f - Mathf.Pow(1f - questionT, 2f);
                _statementGroup.alpha = questionEased;
                _statementArea.anchoredPosition = StatementRestPosition + new Vector2(0f, StatementSlideOffset * (1f - questionEased));

                // ZONES
                var zoneAlpha = Mathf.Lerp(0f, ZoneRestGlowAlpha, Mathf.Clamp01((elapsed - ZonesStart) / (ZonesEnd - ZonesStart)));
                SetGlow(_trueGlow, Theme.AccentWarm, zoneAlpha);
                SetGlow(_falseGlow, Theme.AccentWarm, zoneAlpha);

                yield return null;
            }

            FinishPreambleVisuals();
            _preambleRoutine = null;
        }

        private void FinishPreambleVisuals()
        {
            _presenterImage.color = Color.white;
            _presenterRect.localScale = Vector3.one * PresenterRestScale;
            _presenterSpotlightImage.color = new Color(1f, 0.93f, 0.72f, 0.24f);
            _prizePlaque.localScale = Vector3.one * PrizeSecondaryScale;
            _prizeGroup.alpha = PrizeSecondaryAlpha;
            _progressGroup.alpha = 1f;
            SetSweepProgress(1f);
            _statementGroup.alpha = 1f;
            _statementArea.anchoredPosition = StatementRestPosition;
            SetGlow(_trueGlow, Theme.AccentWarm, ZoneRestGlowAlpha);
            SetGlow(_falseGlow, Theme.AccentWarm, ZoneRestGlowAlpha);
        }

        /// <summary>Called by <see cref="ClasicoGameHost"/> at the exact
        /// moment the session director's own Intro phase ends and Decision
        /// begins — the same pattern <c>WesternShootoutPresenter.RevealAfterIntro</c>
        /// already established. This is what actually gates input (not the
        /// preamble timing above, which is cosmetic and force-finished here
        /// if still running): buttons only become interactable now.</summary>
        public void RevealAfterIntro()
        {
            if (!_introActive)
            {
                return;
            }

            _introActive = false;

            if (_preambleRoutine != null)
            {
                _host.StopCoroutine(_preambleRoutine);
                _preambleRoutine = null;
            }

            FinishPreambleVisuals();

            _decisionOpen = true;
            _trueButton.interactable = true;
            _falseButton.interactable = true;
            RuntimeUIFactory.Select(_trueButton);

            _idleRoutine = StartTracked(IdleBreathingRoutine());
        }

        /// <summary>Subtle, nearly-subconscious idle motion — a couple of
        /// pixels of vertical drift, never anything that reads as its own
        /// animation. Position-only, so it never conflicts with the scale-
        /// based reactions. Stopped (and the host re-seated at rest) the
        /// moment an answer locks.</summary>
        private IEnumerator IdleBreathingRoutine()
        {
            var generation = _sequenceGeneration;
            while (generation == _sequenceGeneration)
            {
                var wave = Mathf.Sin(Time.time / IdleDriftPeriod * Mathf.PI * 2f);
                _presenterRect.anchoredPosition = PresenterRestPosition + new Vector2(0f, wave * IdleDriftAmplitude);
                yield return null;
            }
        }

        private void StopIdle()
        {
            if (_idleRoutine != null)
            {
                _host.StopCoroutine(_idleRoutine);
                _idleRoutine = null;
            }

            _presenterRect.anchoredPosition = PresenterRestPosition;
        }

        /// <summary>C8.1k: called by <see cref="ClasicoGameHost"/> only once
        /// the director has actually ACCEPTED a submission (so a stray click
        /// outside Decision can never lock anything visually). Locks the
        /// chosen zone — focus light, a small held scale, the other zone
        /// dimmed — and the host leans in, but deliberately reveals nothing
        /// about correctness: that waits for <see cref="RevealOutcome"/>'s
        /// own suspense beat.</summary>
        public void LockAnswer(int selectedIndex)
        {
            if (!_decisionOpen || selectedIndex < 0 || selectedIndex > 1)
            {
                return;
            }

            _decisionOpen = false;
            _answerLocked = true;
            _lockedIndex = selectedIndex;
            _trueButton.interactable = false;
            _falseButton.interactable = false;

            StopIdle();
            PlayCue("GameShow_AnswerLock", _answerLockClip);
            StartTracked(LockRoutine(selectedIndex));
        }

        private IEnumerator LockRoutine(int selectedIndex)
        {
            var generation = _sequenceGeneration;
            var chosenZone = ZoneFor(selectedIndex);
            var otherIndex = 1 - selectedIndex;

            SetGlow(GlowFor(otherIndex), Theme.AccentWarm, 0.04f);
            SetLabelAlpha(LabelFor(otherIndex), 0.45f);
            SetGlow(GlowFor(selectedIndex), Theme.AccentWarm, ZoneLockGlowAlpha);

            const float duration = 0.18f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (generation != _sequenceGeneration)
                {
                    yield break;
                }

                var t = Mathf.Clamp01(elapsed / duration);
                var scale = t < 0.5f ? Mathf.Lerp(1f, 1.07f, t / 0.5f) : Mathf.Lerp(1.07f, ZoneLockedScale, (t - 0.5f) / 0.5f);
                chosenZone.localScale = Vector3.one * scale;
                _presenterRect.localScale = Vector3.one * Mathf.Lerp(PresenterRestScale, 1.015f, t);
                yield return null;
            }

            chosenZone.localScale = Vector3.one * ZoneLockedScale;
            _presenterRect.localScale = Vector3.one * 1.015f;
        }

        /// <summary>-1 means the decision window ran out with nothing
        /// chosen — always incorrect, same convention as the other two
        /// selection-based presenters. C8.1k: correctness is only shown
        /// after a short suspense beat; <paramref name="onRevealed"/> fires
        /// at that exact moment (the host uses it to update the shared HUD
        /// score/streak in sync with the reveal rather than before it).</summary>
        public void RevealOutcome(int selectedIndex, Action onRevealed = null)
        {
            _introActive = false;
            _decisionOpen = false;
            _trueButton.interactable = false;
            _falseButton.interactable = false;
            StopIdle();

            StartTracked(RevealRoutine(selectedIndex, onRevealed));
        }

        private IEnumerator RevealRoutine(int selectedIndex, Action onRevealed)
        {
            var generation = _sequenceGeneration;

            float suspense;
            if (_answerLocked && _lockedIndex == selectedIndex)
            {
                suspense = SuspenseAfterLockSeconds;
            }
            else if (selectedIndex >= 0)
            {
                // Defensive: a submission that never went through LockAnswer
                // still gets the lock look and the full beat.
                _answerLocked = true;
                _lockedIndex = selectedIndex;
                SetGlow(GlowFor(selectedIndex), Theme.AccentWarm, ZoneLockGlowAlpha);
                ZoneFor(selectedIndex).localScale = Vector3.one * ZoneLockedScale;
                suspense = SuspenseAfterLockSeconds + 0.2f;
            }
            else
            {
                suspense = SuspenseUnlockedSeconds;
            }

            yield return new WaitForSeconds(suspense);
            if (generation != _sequenceGeneration)
            {
                yield break;
            }

            var correctIndex = _challenge.IsTrue ? 0 : 1;
            var correct = selectedIndex == correctIndex;

            // The right answer is always confirmed — on a correct answer, a
            // wrong answer, and a timeout alike.
            SetGlow(GlowFor(correctIndex), RevealConfirmColor, ZoneRevealGlowAlpha);
            LabelFor(correctIndex).color = RevealConfirmColor;
            SetTmpAlpha(CorrectTagFor(correctIndex), 1f);
            StartTracked(ZonePopRoutine(ZoneFor(correctIndex), ZoneFor(correctIndex).localScale.x, 1.06f));

            var otherIndex = 1 - correctIndex;
            if (!correct && selectedIndex >= 0)
            {
                SetGlow(GlowFor(selectedIndex), MutedIncorrectColor, 0.22f);
                LabelFor(selectedIndex).color = new Color(MutedIncorrectColor.r, MutedIncorrectColor.g, MutedIncorrectColor.b, 0.75f);
                ZoneFor(selectedIndex).localScale = Vector3.one * 0.97f;
            }
            else
            {
                SetGlow(GlowFor(otherIndex), Theme.AccentWarm, 0.04f);
                SetLabelAlpha(LabelFor(otherIndex), 0.45f);
            }

            if (_presenterIsProcedural)
            {
                CharacterPrimitives.ApplyPose(_proceduralFace, correct ? CharacterPrimitives.FacePose.Correct : CharacterPrimitives.FacePose.Incorrect, 70f);
            }

            if (correct)
            {
                PlayCue("GameShow_CorrectReveal", _correctRevealClip);
                StartTracked(PresenterCelebrateRoutine());
                StartTracked(ResultFlashRoutine(RevealConfirmColor, 0.14f));
                StartTracked(ConfettiBurstRoutine(ZoneFor(correctIndex).anchoredPosition + new Vector2(0f, 40f)));
                StartTracked(PrizeWinPulseRoutine());
            }
            else
            {
                PlayCue("GameShow_IncorrectReveal", _incorrectRevealClip);
                StartTracked(PresenterDeflateRoutine());
                StartTracked(ResultFlashRoutine(MutedIncorrectColor, 0.07f));
                _prizeGroup.alpha = 0.5f;
            }

            onRevealed?.Invoke();

            yield return new WaitForSeconds(ExplanationDelaySeconds);
            if (generation != _sequenceGeneration)
            {
                yield break;
            }

            _explanationVerdictText.text = correct
                ? "¡RESPUESTA CORRECTA!"
                : selectedIndex < 0 ? "SE ACABÓ EL TIEMPO" : "RESPUESTA INCORRECTA";
            _explanationVerdictText.color = correct ? PrizeGold : MutedIncorrectColor;

            if (!string.IsNullOrEmpty(_explanationText.text))
            {
                yield return ExplanationEntranceRoutine(generation);
            }
        }

        private IEnumerator ExplanationEntranceRoutine(int generation)
        {
            const float duration = 0.25f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (generation != _sequenceGeneration)
                {
                    yield break;
                }

                var t = Mathf.Clamp01(elapsed / duration);
                _explanationGroup.alpha = t;
                _explanationCard.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, t);
                yield return null;
            }

            _explanationGroup.alpha = 1f;
            _explanationCard.localScale = Vector3.one;
        }

        private IEnumerator ZonePopRoutine(RectTransform zone, float fromScale, float peakScale)
        {
            var generation = _sequenceGeneration;
            const float duration = 0.28f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (generation != _sequenceGeneration)
                {
                    yield break;
                }

                var t = Mathf.Clamp01(elapsed / duration);
                var scale = t < 0.45f ? Mathf.Lerp(fromScale, peakScale, t / 0.45f) : Mathf.Lerp(peakScale, ZoneLockedScale, (t - 0.45f) / 0.55f);
                zone.localScale = Vector3.one * scale;
                yield return null;
            }

            zone.localScale = Vector3.one * ZoneLockedScale;
        }

        /// <summary>Correct: a brief celebratory lift — same sprite, a small
        /// rise + scale punch, then back to rest.</summary>
        private IEnumerator PresenterCelebrateRoutine()
        {
            var generation = _sequenceGeneration;
            var fromScale = _presenterRect.localScale.x;
            const float duration = 0.40f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (generation != _sequenceGeneration)
                {
                    yield break;
                }

                var t = Mathf.Clamp01(elapsed / duration);
                var arc = Mathf.Sin(t * Mathf.PI);
                _presenterRect.localScale = Vector3.one * (Mathf.Lerp(fromScale, PresenterRestScale, t) + arc * 0.07f);
                _presenterRect.anchoredPosition = PresenterRestPosition + new Vector2(0f, arc * 8f);
                yield return null;
            }

            _presenterRect.localScale = Vector3.one * PresenterRestScale;
            _presenterRect.anchoredPosition = PresenterRestPosition;
        }

        /// <summary>Incorrect: a restrained deflate — a small dip and sink,
        /// a short hold, then a recovery to rest. Never a shake.</summary>
        private IEnumerator PresenterDeflateRoutine()
        {
            var generation = _sequenceGeneration;
            var fromScale = _presenterRect.localScale.x;
            const float dip = 0.20f;
            const float hold = 0.25f;
            const float recover = 0.30f;
            var elapsed = 0f;
            while (elapsed < dip + hold + recover)
            {
                elapsed += Time.deltaTime;
                if (generation != _sequenceGeneration)
                {
                    yield break;
                }

                float depth;
                if (elapsed < dip)
                {
                    depth = elapsed / dip;
                }
                else if (elapsed < dip + hold)
                {
                    depth = 1f;
                }
                else
                {
                    depth = 1f - Mathf.Clamp01((elapsed - dip - hold) / recover);
                }

                var baseScale = elapsed < dip ? Mathf.Lerp(fromScale, PresenterRestScale, elapsed / dip) : PresenterRestScale;
                _presenterRect.localScale = Vector3.one * (baseScale - depth * 0.045f);
                _presenterRect.anchoredPosition = PresenterRestPosition + new Vector2(0f, -6f * depth);
                yield return null;
            }

            _presenterRect.localScale = Vector3.one * PresenterRestScale;
            _presenterRect.anchoredPosition = PresenterRestPosition;
        }

        private IEnumerator PrizeWinPulseRoutine()
        {
            var generation = _sequenceGeneration;
            const float duration = 0.45f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (generation != _sequenceGeneration)
                {
                    yield break;
                }

                var t = Mathf.Clamp01(elapsed / duration);
                _prizePlaque.localScale = Vector3.one * (PrizeSecondaryScale + Mathf.Sin(t * Mathf.PI) * 0.08f);
                _prizeGroup.alpha = Mathf.Lerp(PrizeSecondaryAlpha, 1f, Mathf.Clamp01(t * 3f));
                SetSweepProgress(t);
                yield return null;
            }

            _prizePlaque.localScale = Vector3.one * PrizeSecondaryScale;
            _prizeGroup.alpha = 1f;
            SetSweepProgress(1f);
        }

        private IEnumerator ResultFlashRoutine(Color accent, float peakAlpha)
        {
            var generation = _sequenceGeneration;
            const float riseDuration = 0.12f;
            const float fallDuration = 0.30f;

            var elapsed = 0f;
            while (elapsed < riseDuration)
            {
                elapsed += Time.deltaTime;
                if (generation != _sequenceGeneration)
                {
                    yield break;
                }

                _resultFlash.color = new Color(accent.r, accent.g, accent.b, Mathf.Lerp(0f, peakAlpha, elapsed / riseDuration));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < fallDuration)
            {
                elapsed += Time.deltaTime;
                if (generation != _sequenceGeneration)
                {
                    yield break;
                }

                _resultFlash.color = new Color(accent.r, accent.g, accent.b, Mathf.Lerp(peakAlpha, 0f, elapsed / fallDuration));
                yield return null;
            }

            _resultFlash.color = new Color(0f, 0f, 0f, 0f);
        }

        /// <summary>C8.1k: a small, restrained gold/ivory burst rising from
        /// the correct zone itself (was: from center-stage) — eight pieces,
        /// short travel, fading out; never a full-screen particle shower.</summary>
        private IEnumerator ConfettiBurstRoutine(Vector2 origin)
        {
            var generation = _sequenceGeneration;
            const float duration = 0.6f;
            var directions = new Vector2[ConfettiCount];
            for (var i = 0; i < ConfettiCount; i++)
            {
                var angle = Mathf.Lerp(0.15f, 0.85f, i / (float)(ConfettiCount - 1)) * Mathf.PI;
                directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 110f;
                _confettiRects[i].anchoredPosition = origin;
                _confettiRects[i].localRotation = Quaternion.identity;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (generation != _sequenceGeneration)
                {
                    yield break;
                }

                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 2f);
                for (var i = 0; i < ConfettiCount; i++)
                {
                    _confettiRects[i].anchoredPosition = origin + directions[i] * eased + new Vector2(0f, -30f * t * t);
                    _confettiRects[i].localRotation = Quaternion.Euler(0, 0, t * 220f * (i % 2 == 0 ? 1 : -1));
                    var c = _confetti[i].color;
                    _confetti[i].color = new Color(c.r, c.g, c.b, 1f - t);
                }

                yield return null;
            }

            ResetConfetti();
        }

        private void ResetConfetti()
        {
            for (var i = 0; i < ConfettiCount; i++)
            {
                var c = _confetti[i].color;
                _confetti[i].color = new Color(c.r, c.g, c.b, 0f);
                _confettiRects[i].anchoredPosition = new Vector2(0f, 300f);
                _confettiRects[i].localRotation = Quaternion.identity;
            }
        }

        /// <summary>Puts every C8.1k showmanship element back to a known
        /// state. <paramref name="forPreamble"/> = true stages the round
        /// hidden, ready for <see cref="PreambleRoutine"/> (host at its
        /// entrance scale, plaque/statement invisible); false (Build/Hide)
        /// leaves every transform at its true rest.</summary>
        private void ResetVisuals(bool forPreamble)
        {
            _presenterRect.anchoredPosition = PresenterRestPosition;
            _presenterRect.localScale = Vector3.one * (forPreamble ? PresenterIntroStartScale : PresenterRestScale);
            _presenterImage.color = forPreamble ? new Color(1f, 1f, 1f, 0f) : Color.white;
            _presenterSpotlightImage.color = new Color(1f, 0.93f, 0.72f, 0f);
            if (_presenterIsProcedural)
            {
                CharacterPrimitives.ApplyPose(_proceduralFace, CharacterPrimitives.FacePose.Idle, 70f);
            }

            _prizePlaque.localScale = Vector3.one * (forPreamble ? 0.85f : PrizeSecondaryScale);
            _prizeGroup.alpha = 0f;
            _progressGroup.alpha = 0f;
            SetSweepProgress(0f);

            _statementGroup.alpha = 0f;
            _statementArea.anchoredPosition = StatementRestPosition + (forPreamble ? new Vector2(0f, StatementSlideOffset) : Vector2.zero);

            _trueZone.localScale = Vector3.one;
            _falseZone.localScale = Vector3.one;
            SetGlow(_trueGlow, Theme.AccentWarm, 0f);
            SetGlow(_falseGlow, Theme.AccentWarm, 0f);
            _trueLabel.color = Theme.TextPrimary;
            _falseLabel.color = Theme.TextPrimary;
            SetTmpAlpha(_trueCorrectTag, 0f);
            SetTmpAlpha(_falseCorrectTag, 0f);
            _trueButton.interactable = false;
            _falseButton.interactable = false;

            _explanationGroup.alpha = 0f;
            _explanationCard.localScale = Vector3.one;

            _resultFlash.color = new Color(0f, 0f, 0f, 0f);
            ResetConfetti();

            _answerLocked = false;
            _lockedIndex = -1;
        }

        private Coroutine StartTracked(IEnumerator routine)
        {
            var coroutine = _host.StartCoroutine(routine);
            _activeRoutines.Add(coroutine);
            return coroutine;
        }

        /// <summary>Stops every coroutine this presenter started (preamble,
        /// idle, lock, reveal, reactions, confetti, flashes), invalidates any
        /// that are mid-yield via the generation counter, and silences both
        /// audio sources — the single cancellation point for Hide(), abort,
        /// restart, and a fresh ShowChallenge.</summary>
        private void CancelSequence()
        {
            _sequenceGeneration++;

            foreach (var routine in _activeRoutines)
            {
                if (routine != null)
                {
                    _host.StopCoroutine(routine);
                }
            }

            _activeRoutines.Clear();
            _preambleRoutine = null;
            _idleRoutine = null;

            _sfxAudioSource.Stop();
            _ambienceAudioSource.Stop();

            _introActive = false;
            _decisionOpen = false;
        }

        private void SetSweepProgress(float t)
        {
            _prizeSweep.anchoredPosition = new Vector2(Mathf.Lerp(-170f, 170f, t), 0f);
        }

        private static void SetGlow(Image glow, Color color, float alpha)
        {
            glow.color = new Color(color.r, color.g, color.b, alpha);
        }

        private static void SetLabelAlpha(Text label, float alpha)
        {
            var c = label.color;
            label.color = new Color(c.r, c.g, c.b, alpha);
        }

        private static void SetTmpAlpha(TMP_Text text, float alpha)
        {
            var c = text.color;
            text.color = new Color(c.r, c.g, c.b, alpha);
        }

        private Image GlowFor(int index) => index == 0 ? _trueGlow : _falseGlow;

        private RectTransform ZoneFor(int index) => index == 0 ? _trueZone : _falseZone;

        private Text LabelFor(int index) => index == 0 ? _trueLabel : _falseLabel;

        private TMP_Text CorrectTagFor(int index) => index == 0 ? _trueCorrectTag : _falseCorrectTag;

        public void Hide()
        {
            CancelSequence();
            ResetVisuals(forPreamble: false);
            _root.gameObject.SetActive(false);
        }
    }
}
