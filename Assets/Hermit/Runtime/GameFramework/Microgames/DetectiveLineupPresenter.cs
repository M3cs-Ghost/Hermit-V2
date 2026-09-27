using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Hermit.Games.Clasico.Microgames;
using Hermit.Runtime.GameFramework;

namespace Hermit.Runtime.GameFramework.Microgames
{
    /// <summary>
    /// ErrorDetection, dressed as a detective lineup: SPOT THE ACCOUNTING
    /// IMPOSTOR — four suspect dossiers, three that belong to the stated
    /// rule and one that doesn't, framed by a case prompt and a rule label.
    ///
    /// C8.1g.1's audit found this was functionally a generic 4-button quiz
    /// wearing lineup dressing, with the anomaly always at the same
    /// authored slot and an unfixed Spotlight lifecycle bug. C8.1g.2
    /// redesigned the mechanic (runtime display shuffle, ACCUSED/CORRECT/
    /// INCORRECT visual states, a required teaching recap, the Spotlight
    /// lifecycle fix, raycast hardening, and five Detective-specific audio
    /// hooks) using procedural placeholder visuals throughout.
    ///
    /// C8.1g.3 integrates the approved Gold art on top of that accepted
    /// mechanic — visual integration only, no mechanic changes:
    /// - <c>Detective_LineupRoom</c> replaces the procedural wall/height-
    ///   line/glow dressing as the actual environment backdrop (the
    ///   procedural pieces are the fallback only, never layered under the
    ///   real art — see <see cref="BuildCaseEnvironment"/>).
    /// - The Auditor now stands directly in the environment as a grounded,
    ///   transparent-background character (<c>Detective_Auditor</c>), not
    ///   inside a floating portrait card — "AuditorCard" is kept as the
    ///   container's own name purely for test-lookup backward
    ///   compatibility (see <see cref="BuildAuditorArea"/>).
    /// - All four suspect slots share one <c>Detective_SuspectDossier</c>
    ///   tile as their button art (replacing the flat rounded-rectangle +
    ///   procedural-face look) — the per-suspect procedural face is gone
    ///   entirely, since the dossier art already carries a bust silhouette
    ///   (see <see cref="BuildLineup"/>).
    /// - The Spotlight is retuned to a cool, restrained "investigative
    ///   lamp" tone (was a warm generic glow) and now pairs with a mild
    ///   per-dossier brightness tint on focus change — never a bright UI
    ///   outline as the main effect (see <see cref="RenderDecision"/>).
    /// - Every new Gold Image is <c>raycastTarget = false</c> except the
    ///   four suspect buttons' own root Image, whose Button ColorTint is
    ///   neutralized to white so it can never wash the real art with
    ///   Theme.Accent on selection.
    /// All art loads via <see cref="RuntimeUIFactory.LoadArt"/> and falls
    /// back to the exact C8.1g.2 procedural visuals if missing — this
    /// presenter never assumes the Gold assets exist.
    ///
    /// C8.1g.5 simplifies the result language after manual review found
    /// the C8.1g.2/g.3 reveal too "designed" for what it communicates —
    /// an environment bay, a dossier frame, AND a smaller nested
    /// ImpostorRevealArt frame inside that read as visual clutter. This
    /// phase retires, entirely: the AccusedFrame overlay (a tiny click
    /// punch is enough — see <see cref="ShowAccusedCue"/>), the
    /// ClearedOverlay dimming of the other three suspects (no theatrical
    /// "elimination"), the ImpostorRevealArt nested-frame image (its
    /// production asset stays on disk, just unused by this presenter —
    /// see <see cref="ExposeImpostor"/>), and any persistent color on a
    /// wrong guess (a brief shake is the only incorrect-answer reaction —
    /// see <see cref="IncorrectRevealRoutine"/>). Only ONE dossier ever
    /// carries result color now — the actual anomaly, always, regardless
    /// of outcome — via a restrained amber dossier tint + the same dynamic
    /// "IMPOSTOR" text as before, now floating directly above/on that
    /// dossier with no frame behind it.
    /// </summary>
    internal sealed class DetectiveLineupPresenter : IMicrogamePresenter
    {
        private const int MaxSuspects = 4;

        // Real dossier art is 260x705 (a single tile cropped from the
        // approved 4-across candidate) — these constants keep every
        // suspect button's own size/aspect matched to that art exactly,
        // so Image.preserveAspect=false never stretches it.
        //
        // C8.1g.4: width/spacing/baseline retuned against the REAL
        // LineupRoom backdrop's own measured geometry (pixel-sampled from
        // the production PNG, not guessed) — the four glass display bays
        // sit at even ~217-unit center-to-center spacing (in this
        // presenter's 1280x620 stage — CanvasScaler reference 1280x720
        // minus ClasicoHud's 100-unit top HUD band) starting at x=-325.5
        // from stage-center, and their own shelf line sits well above the
        // stage's bottom edge — the C8.1g.3 dossiers were anchored to the
        // stage's bottom edge with an arbitrary margin, unrelated to where
        // the backdrop's pedestals actually are, which is exactly why they
        // read as "sprites placed over the environment." DossierWidth
        // shrank 170->155 (~9%) specifically to buy enough headroom to
        // lift the baseline toward the pedestal shelf without the card's
        // own top edge colliding with RuleHeader — a deliberate trade
        // favoring "seated in the room" over maximum card size, chosen
        // over shrinking further (which would hurt label legibility,
        // section 3's own explicit priority).
        private const float DossierWidth = 155f;
        private const float DossierAspect = 705f / 260f;
        private const float DossierHeight = DossierWidth * DossierAspect;
        private const float DossierSpacing = 62f;
        private const float DossierBottomMargin = 70f;
        private const float DossierAnchorY = DossierHeight / 2f + DossierBottomMargin;

        // C8.1g.5: the nested ImpostorRevealArt frame is retired entirely
        // (brief section 1) — this is now just where the dynamic
        // "IMPOSTOR" text sits, no frame/art behind it. Kept in the same
        // upper bust/photo band C8.1g.4 already placed the old reveal art
        // in (verified clear of the label's own safe zone).
        private const float ImpostorTagAnchorY = 0.766f;

        // Label safe-zone, as a fraction of the dossier's own rect —
        // deliberately a SHORT band (not the whole "paper" area) so Best
        // Fit is forced to shrink enough for 2-line wrapping rather than
        // stopping at a large font that still needs 3+ lines — the exact
        // lesson from the C8.1f.6 Balance token-text hotfix (Best Fit only
        // shrinks as far as the HEIGHT budget demands, never to minimize
        // line count on its own). Verified against the full account-name
        // pool by PlayMode test.
        //
        // C8.1k.3: the old x 0.05..0.95 band was measured against the
        // dossier RECT, not the art — the Detective_SuspectDossier paper's
        // solid interior only spans x 39..236 of 260 source px (0.150..0.908,
        // and it sits right of center) and y 0.145..0.488 from the bottom.
        // A long wrapped line ("Préstamo bancario por pagar") therefore ran
        // ~16px onto the dark frame on the left and ~6px on the right while
        // still "fitting" the rect. The band is now the paper interior with
        // a ~2 source-px safety inset (x 0.158..0.900, ~115 display px,
        // centered on the paper) — the longest shipped name ("Papelería
        // comprada por adelantado para uso futuro") needs 113px at the 9pt
        // floor to stay on 2 lines — and tall enough for two 18pt lines.
        private static readonly Vector2 LabelAnchorMin = new Vector2(0.158f, 0.21f);
        private static readonly Vector2 LabelAnchorMax = new Vector2(0.900f, 0.32f);

        // C8.1k.3: account names are sized by TwoLineTextFit (largest size
        // that wraps into <= 2 lines inside the band) instead of Best Fit,
        // which only minimizes HEIGHT and would pick 3 small lines for the
        // longest names. Short names stay at the 18pt ceiling.
        private const int LabelMaxFontSize = 18;
        private const int LabelMinFontSize = 9;
        private const int LabelMaxLines = 2;

        // C8.1g.2/g.3/g.5: an investigative, restrained palette —
        // deliberately not Theme.Correct/Theme.Incorrect (that pure
        // green/red "casino" read is exactly what the brief bans for
        // Detective). The result tint is applied as a MULTIPLY color over
        // the real dossier art (not a flat replacement), so the artwork
        // stays visible underneath. C8.1g.5 retires WrongAccusationColor/
        // AccusedFrameColor/AccusationDimColor entirely — only ONE dossier
        // (the actual anomaly) ever carries persistent result color now,
        // regardless of outcome; a wrong guess gets a brief shake and
        // nothing else (brief section 4/6).
        private static readonly Color ImpostorExposedColor = new Color(0.82f, 0.62f, 0.25f, 1f);
        private static readonly Color ImpostorDossierTint = new Color(1.05f, 0.95f, 0.72f, 1f);
        private static readonly Color FocusTintColor = new Color(1.12f, 1.12f, 1.18f, 1f);
        // C8.1g.4: lower alpha (0.14 -> 0.22 peak but now via a radial
        // gradient that's already near-zero at its own edge, so the
        // effective average coverage is much lower than the old flat
        // rounded-rect at 0.14) — the gradient's own center is
        // deliberately a bit stronger than the old flat shape's uniform
        // alpha specifically because a radial falloff's PERCEIVED
        // brightness is concentrated in a much smaller central area, so a
        // higher peak is needed for the light to still read as present at
        // a glance without raising the edges into "glow" territory.
        private static readonly Color SpotlightColor = new Color(0.75f, 0.85f, 1f, 0.22f);

        // C8.1j premium typography pass: a dark navy-charcoal "evidence
        // board" plate with a muted brass edge — Detective's own distinct
        // variant of the industrial panel language (cooler/darker fill
        // than Balance's, so the two archetypes read as related but not
        // identical, per brief section 14).
        private static readonly Color RecapFillColor = new Color(0.055f, 0.065f, 0.09f, 1f);
        private static readonly Color RecapBorderColor = new Color(0.62f, 0.5f, 0.28f, 1f);

        public event Action<int> SuspectAccused;

        private readonly MonoBehaviour _host;

        private RectTransform _root;
        private Text _cueText;
        private Text _ruleLabelText;
        private RectTransform _spotlight;
        private Vector2 _spotlightRestAnchoredPosition;
        private Vector3 _spotlightRestScale;
        private Color _spotlightRestColor;
        private CanvasGroup _lightsOutGroup;
        private readonly List<Button> _suspects = new List<Button>();
        private readonly List<Text> _suspectLabels = new List<Text>();
        private readonly List<Image> _suspectImages = new List<Image>();
        private readonly List<RectTransform> _suspectRects = new List<RectTransform>();
        private readonly List<Text> _impostorTags = new List<Text>();
        private readonly List<MotionHandle> _protestHandles = new List<MotionHandle>();
        private readonly List<MotionHandle> _revealHandles = new List<MotionHandle>();
        private RectTransform _auditorFrame;
        private readonly MotionHandle _auditorHandle = new MotionHandle();
        private readonly MotionHandle _lightsOutHandle = new MotionHandle();

        private RectTransform _recapBorder;
        private RectTransform _recapPanel;
        private Text _recapRuleText;
        private Text _recapImpostorText;
        private Text _recapWhyText;

        private AudioSource _sfxAudioSource;
        private AudioClip _caseOpenClip;
        private AudioClip _focusClip;
        private AudioClip _accuseClip;
        private AudioClip _correctRevealClip;
        private AudioClip _incorrectRevealClip;
        private int _lastFocusedIndex = -1;

        private ErrorDetectionChallenge _challenge;
        private IReadOnlyList<int> _displayOrder;
        private int _displayAnomalyIndex = -1;
        private IEnumerator _revealRoutine;
        private HermitTheme Theme => RuntimeUIFactory.Theme;

        public DetectiveLineupPresenter(MonoBehaviour host)
        {
            _host = host;
        }

        public void Build(Transform stageRoot)
        {
            _root = RuntimeUIFactory.CreatePanel(stageRoot, "DetectiveLineup", new Color(0.1f, 0.11f, 0.14f, 1f));

            BuildCaseEnvironment();
            BuildRuleHeader();
            BuildAuditorArea();
            BuildLineup();
            BuildSpotlight();
            BuildTeachingRecap();
            BuildLightsOutOverlay();
            BuildAudio();

            _root.gameObject.SetActive(false);
        }

        /// <summary>C8.1g.3 brief section 5: the approved
        /// <c>Detective_LineupRoom</c> art is the actual scene backdrop —
        /// the old procedural wall/height-line/ambient-glow dressing is
        /// retired to a fallback-only path, never layered visibly under
        /// the real artwork. <c>raycastTarget = false</c> throughout either
        /// way (brief section 17/15).</summary>
        private void BuildCaseEnvironment()
        {
            var environment = new GameObject("Environment", typeof(RectTransform)).GetComponent<RectTransform>();
            environment.SetParent(_root, false);
            RuntimeUIFactory.StretchFull(environment);

            var lineupRoomArt = RuntimeUIFactory.LoadArt("Art/Gold/Detective/Environment/Detective_LineupRoom");
            if (lineupRoomArt != null)
            {
                var lineupRoomGo = new GameObject("LineupRoom", typeof(RectTransform), typeof(Image));
                var lineupRoomRect = (RectTransform)lineupRoomGo.transform;
                lineupRoomRect.SetParent(environment, false);
                RuntimeUIFactory.StretchFull(lineupRoomRect);
                var lineupRoomImage = lineupRoomGo.GetComponent<Image>();
                lineupRoomImage.sprite = lineupRoomArt;
                lineupRoomImage.type = Image.Type.Simple;
                // Deliberately fills the whole stage rather than
                // letterboxing (preserveAspect=false) — a backdrop reads
                // fine cropped slightly; letterboxed bars would not.
                lineupRoomImage.preserveAspect = false;
                lineupRoomImage.raycastTarget = false;
                return;
            }

            // Procedural fallback — the exact C8.1g.2 dressing, only built
            // if the Gold environment art hasn't landed.
            var wall = RuntimeUIFactory.CreatePanel(environment, "Wall", new Color(0.15f, 0.16f, 0.19f, 1f));
            wall.anchorMin = new Vector2(0f, 0.42f);
            wall.anchorMax = new Vector2(1f, 0.86f);
            wall.offsetMin = Vector2.zero;
            wall.offsetMax = Vector2.zero;
            wall.GetComponent<Image>().raycastTarget = false;

            for (var i = 0; i < 5; i++)
            {
                var line = RuntimeUIFactory.CreatePanel(wall, "HeightLine", new Color(0.32f, 0.33f, 0.37f, 0.5f));
                line.anchorMin = new Vector2(0f, i * 0.22f);
                line.anchorMax = new Vector2(1f, i * 0.22f);
                line.offsetMin = new Vector2(0, -1f);
                line.offsetMax = new Vector2(0, 1f);
                line.GetComponent<Image>().raycastTarget = false;
            }

            var glow = RuntimeUIFactory.CreateRoundedPanel(environment, "AmbientGlow", new Color(1f, 0.95f, 0.8f, 0.10f), 120);
            glow.anchorMin = glow.anchorMax = new Vector2(0.5f, 1f);
            glow.anchoredPosition = new Vector2(0, -20);
            glow.sizeDelta = new Vector2(900, 260);
            glow.GetComponent<Image>().raycastTarget = false;
        }

        /// <summary>The case's own framing text — a short, reusable
        /// CasePrompt above the RuleLabel. Both come straight from content
        /// (<see cref="ShowChallenge"/>). GameObject names ("Cue"/
        /// "RuleLabel") kept stable for existing layout-regression tests
        /// and cross-presenter checks.</summary>
        private void BuildRuleHeader()
        {
            var header = new GameObject("RuleHeader", typeof(RectTransform)).GetComponent<RectTransform>();
            header.SetParent(_root, false);
            RuntimeUIFactory.StretchFull(header);

            _cueText = RuntimeUIFactory.CreateText(
                header, "Cue", string.Empty, Theme.CaptionSize, TextAnchor.MiddleCenter, Theme.Warning,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(700, 32));

            _ruleLabelText = RuntimeUIFactory.CreateText(
                header, "RuleLabel", string.Empty, Theme.HeadingSize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(900, 60));
        }

        /// <summary>C8.1g.3 brief section 6: the Auditor now stands
        /// directly in the environment as a grounded, transparent-
        /// background character — no floating portrait card. "AuditorCard"
        /// is kept as this container's own name purely so existing
        /// FindRect/FindImageUnder("AuditorCard", ...) test lookups keep
        /// working; it carries no background Image of its own any more.
        /// Falls back to the original C8.1g.2 procedural detective-head
        /// dressing (unchanged) if the new art hasn't landed.</summary>
        private void BuildAuditorArea()
        {
            var auditorArea = new GameObject("AuditorArea", typeof(RectTransform)).GetComponent<RectTransform>();
            auditorArea.SetParent(_root, false);
            RuntimeUIFactory.StretchFull(auditorArea);

            var auditorArt = RuntimeUIFactory.LoadArt("Art/Gold/Detective/Characters/Detective_Auditor");
            if (auditorArt != null)
            {
                // C8.1g.4: shrunk 400->310 and nudged right 0.08->0.093 —
                // at the old size/position his own right edge sat barely
                // 1 unit clear of Suspect0's newly-measured left edge (see
                // DossierWidth's own doc-comment), reading as visually
                // competing with the lineup exactly as the manual review
                // reported. This keeps him comfortably inside the stage's
                // left margin AND clear of Suspect0, full-body and
                // grounded on the room floor (unchanged y=20 baseline —
                // he's an observer standing on the floor, not elevated
                // onto a display pedestal like the suspects are).
                const float auditorHeight = 310f;
                var auditorWidth = auditorHeight * (auditorArt.rect.width / auditorArt.rect.height);

                var card = new GameObject("AuditorCard", typeof(RectTransform)).GetComponent<RectTransform>();
                card.SetParent(auditorArea, false);
                card.anchorMin = card.anchorMax = new Vector2(0.093f, 0f);
                card.pivot = new Vector2(0.5f, 0f);
                card.sizeDelta = new Vector2(auditorWidth, auditorHeight);
                card.anchoredPosition = new Vector2(0, 20);

                var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                var portraitRect = (RectTransform)portraitGo.transform;
                portraitRect.SetParent(card, false);
                RuntimeUIFactory.StretchFull(portraitRect);
                var portrait = portraitGo.GetComponent<Image>();
                portrait.sprite = auditorArt;
                portrait.type = Image.Type.Simple;
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;

                _auditorFrame = card;
                return;
            }

            var detectiveHead = RuntimeUIFactory.CreateRoundedPanel(auditorArea, "DetectiveHead", new Color(0.78f, 0.63f, 0.5f, 1f), 26);
            detectiveHead.anchorMin = detectiveHead.anchorMax = new Vector2(0.08f, 0f);
            detectiveHead.anchoredPosition = new Vector2(0, 210);
            detectiveHead.sizeDelta = new Vector2(52, 52);
            detectiveHead.GetComponent<Image>().raycastTarget = false;
            CharacterPrimitives.Build(detectiveHead, 52f);

            var detectiveHat = RuntimeUIFactory.CreateRoundedPanel(detectiveHead, "Hat", new Color(0.22f, 0.2f, 0.24f, 1f), 6);
            detectiveHat.anchorMin = detectiveHat.anchorMax = new Vector2(0.5f, 1f);
            detectiveHat.anchoredPosition = new Vector2(0, 10);
            detectiveHat.sizeDelta = new Vector2(58, 14);
            detectiveHat.GetComponent<Image>().raycastTarget = false;

            var glassRing = RuntimeUIFactory.CreateRoundedPanel(auditorArea, "GlassRing", new Color(0.75f, 0.8f, 0.85f, 0.9f), 14);
            glassRing.anchorMin = glassRing.anchorMax = new Vector2(0.08f, 0f);
            glassRing.anchoredPosition = new Vector2(38, 178);
            glassRing.sizeDelta = new Vector2(28, 28);
            glassRing.GetComponent<Image>().raycastTarget = false;
            var glassHole = RuntimeUIFactory.CreateRoundedPanel(glassRing, "GlassHole", new Color(0.1f, 0.11f, 0.14f, 1f), 10);
            glassHole.anchorMin = glassHole.anchorMax = new Vector2(0.5f, 0.5f);
            glassHole.anchoredPosition = Vector2.zero;
            glassHole.sizeDelta = new Vector2(20, 20);
            glassHole.GetComponent<Image>().raycastTarget = false;
            var glassHandle = RuntimeUIFactory.CreatePanel(auditorArea, "GlassHandle", new Color(0.5f, 0.4f, 0.3f, 1f));
            glassHandle.anchorMin = glassHandle.anchorMax = new Vector2(0.08f, 0f);
            glassHandle.pivot = new Vector2(0f, 1f);
            glassHandle.anchoredPosition = new Vector2(50, 164);
            glassHandle.sizeDelta = new Vector2(6, 20);
            glassHandle.localRotation = Quaternion.Euler(0, 0, -35f);
            glassHandle.GetComponent<Image>().raycastTarget = false;
        }

        /// <summary>Builds the four suspect dossiers under a "Lineup"
        /// container. Each button keeps its exact pre-existing GameObject
        /// name ("DetectiveSuspect{i}") — every existing test looks it up
        /// by that literal name. C8.1g.3: all four now share the single
        /// approved <c>Detective_SuspectDossier</c> tile as their own
        /// button art (falls back to a flat palette color if missing) —
        /// the old per-suspect procedural face is gone; the dossier art
        /// already carries a bust silhouette. C8.1g.5: the AccusedFrame and
        /// ClearedOverlay per-suspect overlays, and the ImpostorRevealArt
        /// nested-frame image, are all retired (brief sections 1/5/6) — the
        /// dossier's own Image color and the "IMPOSTOR" text are now the
        /// entire result vocabulary; <c>Detective_ImpostorReveal</c> is no
        /// longer even loaded here.</summary>
        private void BuildLineup()
        {
            var lineup = new GameObject("Lineup", typeof(RectTransform)).GetComponent<RectTransform>();
            lineup.SetParent(_root, false);
            RuntimeUIFactory.StretchFull(lineup);

            var startX = -((MaxSuspects - 1) * (DossierWidth + DossierSpacing)) / 2f;

            var dossierArt = RuntimeUIFactory.LoadArt("Art/Gold/Detective/Suspects/Detective_SuspectDossier");

            var suspectPalette = new[]
            {
                new Color(0.62f, 0.5f, 0.42f, 1f),
                new Color(0.55f, 0.6f, 0.66f, 1f),
                new Color(0.66f, 0.52f, 0.55f, 1f),
                new Color(0.5f, 0.58f, 0.5f, 1f),
            };

            for (var i = 0; i < MaxSuspects; i++)
            {
                var index = i;
                var x = startX + i * (DossierWidth + DossierSpacing);

                var button = RuntimeUIFactory.CreateButton(
                    lineup, $"DetectiveSuspect{i}", string.Empty,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, DossierAnchorY), new Vector2(DossierWidth, DossierHeight));
                button.onClick.AddListener(() =>
                {
                    ShowAccusedCue(index);
                    SuspectAccused?.Invoke(index);
                });

                // C8.1g.3 brief section 9: the Button's own ColorTint
                // transition (hover/select/press) would otherwise tint the
                // real dossier art with Theme.Accent on keyboard selection
                // — exactly the "bright UI outline as the main focus
                // effect" the brief bans. Neutralized to white so the
                // roaming Spotlight + the mild per-dossier brightness tint
                // (see RenderDecision) are the only focus language.
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.pressedColor = Color.white;
                colors.selectedColor = Color.white;
                button.colors = colors;

                var buttonImage = button.GetComponent<Image>();
                if (dossierArt != null)
                {
                    buttonImage.sprite = dossierArt;
                    buttonImage.type = Image.Type.Simple;
                    buttonImage.preserveAspect = false;
                }
                else
                {
                    buttonImage.color = suspectPalette[i];
                }

                var slotTag = RuntimeUIFactory.CreateText(
                    button.transform, "SlotTag", $"SUJETO {i + 1}", Theme.CaptionSize - 4, TextAnchor.MiddleCenter, Theme.TextSecondary,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 16), new Vector2(DossierWidth, 18));
                slotTag.raycastTarget = false;

                var label = button.GetComponentInChildren<Text>();
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = LabelAnchorMin;
                labelRect.anchorMax = LabelAnchorMax;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Theme.TextPrimary;
                // C8.1k.3: sized per text by TwoLineTextFit in ShowChallenge
                // (Best Fit off). Overflow, not Truncate: a string that ever
                // failed to fit must be visible in review, never silently
                // cut — every shipped name is audited by
                // Detective_AllAccountNames_FitTheDossierPaper_InTwoLines_AtBothResolutions.
                label.resizeTextForBestFit = false;
                label.fontSize = LabelMaxFontSize;
                label.lineSpacing = 0.95f;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;

                // C8.1g.5 brief section 7: dynamic "IMPOSTOR" text, no
                // frame/art behind it — placed in the same upper bust/photo
                // band the retired ImpostorRevealArt used to occupy
                // (already verified clear of the label's own safe zone),
                // shown only on the actual anomaly regardless of outcome.
                var impostorTag = RuntimeUIFactory.CreateText(
                    button.transform, "ImpostorTag", "IMPOSTOR", Theme.CaptionSize - 4, TextAnchor.MiddleCenter, ImpostorExposedColor,
                    new Vector2(0.5f, ImpostorTagAnchorY), new Vector2(0.5f, ImpostorTagAnchorY), Vector2.zero, new Vector2(DossierWidth * 0.85f, DossierHeight * 0.14f));
                impostorTag.raycastTarget = false;
                impostorTag.fontStyle = FontStyle.Bold;
                SetAlpha(impostorTag, 0f);

                var rect = (RectTransform)button.transform;
                _suspects.Add(button);
                _suspectLabels.Add(label);
                _suspectImages.Add(buttonImage);
                _suspectRects.Add(rect);
                _impostorTags.Add(impostorTag);
                _protestHandles.Add(new MotionHandle());
                _revealHandles.Add(new MotionHandle());
            }

            RuntimeUIFactory.ChainHorizontal(_suspects.ToArray());
        }

        /// <summary>The roaming focus/inspection spotlight. C8.1g.3
        /// retunes it from a warm generic glow to a cooler, restrained
        /// "investigative lamp" tone (brief section 9) and drops the old
        /// <c>SetAsFirstSibling</c> — with an opaque LineupRoom backdrop
        /// now behind everything, a spotlight pushed to the very back
        /// would render fully hidden; drawn in its natural place (after
        /// Lineup) it reads as a soft light pool over whichever dossier it
        /// overlaps. Resting anchoredPosition/scale/color are still
        /// captured once, here, before it is ever moved (brief section
        /// 14's lifecycle fix, unchanged).</summary>
        private void BuildSpotlight()
        {
            // C8.1g.4 brief section 5: a rounded RECTANGLE (the C8.1g.2/g.3
            // shape) reads as a UI panel no matter how low its opacity —
            // switched to the same procedural radial-gradient sprite
            // GameShowPresenter already uses for its own soft glow
            // overlays (opaque center fading smoothly to fully transparent
            // edges), which is what actually produces "natural falloff"
            // and "stronger emphasis on the dossier center" rather than a
            // flat-alpha shape with a hard rounded silhouette.
            var spotlightGo = new GameObject("Spotlight", typeof(RectTransform), typeof(Image));
            _spotlight = (RectTransform)spotlightGo.transform;
            _spotlight.SetParent(_root, false);
            _spotlight.anchorMin = new Vector2(0.5f, 0.5f);
            _spotlight.anchorMax = new Vector2(0.5f, 0.5f);
            // Larger than the dossier itself — a radial gradient's own
            // edge is already near-zero alpha, so the glow needs room
            // beyond the card to breathe rather than clipping its own
            // soft falloff at the card's edge.
            _spotlight.sizeDelta = new Vector2(DossierWidth + 110f, DossierHeight + 110f);
            var spotlightImage = spotlightGo.GetComponent<Image>();
            spotlightImage.sprite = RuntimeUIFactory.GetRadialGlowSprite();
            spotlightImage.type = Image.Type.Simple;
            spotlightImage.color = SpotlightColor;
            spotlightImage.raycastTarget = false;
            _spotlight.gameObject.SetActive(false);

            _spotlightRestAnchoredPosition = _spotlight.anchoredPosition;
            _spotlightRestScale = _spotlight.localScale;
            _spotlightRestColor = spotlightImage.color;
        }

        /// <summary>C8.1g.2 brief section 10: the required teaching recap —
        /// Rule / Impostor / Why — shown after every reveal (including a
        /// timeout). C8.1g.3 gives it a restrained warm brass top accent
        /// (an evidence-folder tab read) instead of a flat panel edge,
        /// per brief section 15 — readability is unchanged, only the
        /// framing.</summary>
        // C8.1j premium typography pass: an "evidence board" panel — dark
        // navy-charcoal plate with a muted brass tab/edge (brief section
        // 10's own suggested language) — replacing the old flat
        // near-opaque rounded rect. The real fix here is internal layout,
        // not the base color: the old 120px-tall panel gave RecapWhy only
        // a 48-unit box with VerticalWrapMode.Truncate, which silently
        // DROPPED any explanation line that didn't fit — for Detective's
        // 30 (post-C8.1i) Explanation strings, several genuinely overflowed
        // that box, so the dropped/spilling text rendered past the panel's
        // own opaque bounds, into fully-transparent space where the
        // environment showed straight through — read by the manual review
        // as "the panel is too transparent" and "environment competes with
        // the text", when the actual defect was an undersized text box.
        // Fixed by (1) a taller panel (renders as a later sibling than
        // BuildLineup's dossiers, so growing it upward safely occludes only
        // the dossiers' own lower margin, well below every account-name/
        // IMPOSTOR label, which all sit in the dossiers' upper half) and
        // (2) VerticalWrapMode.Overflow + resizeTextForBestFit on RecapWhy
        // (same proven technique as Verdict/BalanceMachinePresenter) so
        // long explanations shrink to fit instead of being cut. Verified
        // per-string for all 30 shipped ErrorDetection challenges in
        // Detective_AllExplanations_FitWithinTheRecapPanel.
        private void BuildTeachingRecap()
        {
            var (border, content) = RuntimeUIFactory.CreatePremiumPanel(
                _root, "TeachingRecap", RecapFillColor, RecapBorderColor, 3f, 16);
            border.anchorMin = new Vector2(0.5f, 0f);
            border.anchorMax = new Vector2(0.5f, 0f);
            border.pivot = new Vector2(0.5f, 0f);
            border.anchoredPosition = new Vector2(0, 20);
            border.sizeDelta = new Vector2(820, 190);
            border.GetComponent<Image>().raycastTarget = false;
            content.GetComponent<Image>().raycastTarget = false;
            _recapBorder = border;
            _recapPanel = content;

            var tab = RuntimeUIFactory.CreateRoundedPanel(border, "RecapTab", new Color(0.72f, 0.57f, 0.3f, 1f), 6);
            tab.anchorMin = new Vector2(0.5f, 1f);
            tab.anchorMax = new Vector2(0.5f, 1f);
            tab.anchoredPosition = new Vector2(0, 4);
            tab.sizeDelta = new Vector2(120, 8);
            tab.GetComponent<Image>().raycastTarget = false;

            // Small brass/evidence-tab label (brief section 10's own
            // example: "small brass/evidence tab: REGLA").
            _recapRuleText = RuntimeUIFactory.CreateText(
                _recapPanel, "RecapRule", string.Empty, Theme.CaptionSize, TextAnchor.UpperCenter, RecapBorderColor,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -13), new Vector2(0, 26));
            _recapRuleText.raycastTarget = false;
            _recapRuleText.fontStyle = FontStyle.Bold;

            // The larger, prominent line — the actual named impostor.
            _recapImpostorText = RuntimeUIFactory.CreateText(
                _recapPanel, "RecapImpostor", string.Empty, Theme.BodySize, TextAnchor.UpperCenter, ImpostorExposedColor,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0, -46), new Vector2(0, 40));
            _recapImpostorText.raycastTarget = false;
            _recapImpostorText.fontStyle = FontStyle.Bold;

            // The teaching body — generous multi-line room (112px, up from
            // 48px) plus best-fit shrink as a defensive backstop, so no
            // Explanation string can ever be silently truncated again.
            _recapWhyText = RuntimeUIFactory.CreateText(
                _recapPanel, "RecapWhy", string.Empty, Theme.CaptionSize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 62), new Vector2(740, 112));
            _recapWhyText.raycastTarget = false;
            _recapWhyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _recapWhyText.verticalOverflow = VerticalWrapMode.Overflow;
            _recapWhyText.resizeTextForBestFit = true;
            _recapWhyText.resizeTextMinSize = 13;
            _recapWhyText.resizeTextMaxSize = Theme.CaptionSize;
            _recapWhyText.lineSpacing = 1.05f;

            border.gameObject.SetActive(false);
        }

        private void BuildLightsOutOverlay()
        {
            var lightsOutGo = new GameObject("LightsOutOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var lightsOutRect = (RectTransform)lightsOutGo.transform;
            lightsOutRect.SetParent(_root, false);
            RuntimeUIFactory.StretchFull(lightsOutRect);
            var lightsOutImage = lightsOutGo.GetComponent<Image>();
            lightsOutImage.color = Color.black;
            lightsOutImage.raycastTarget = false;
            _lightsOutGroup = lightsOutGo.GetComponent<CanvasGroup>();
            _lightsOutGroup.alpha = 0f;
            _lightsOutGroup.blocksRaycasts = false;
            lightsOutRect.SetAsLastSibling();
        }

        /// <summary>Five Detective-specific hooks (Detective_CaseOpen/
        /// Focus/Accuse/CorrectReveal/IncorrectReveal), loaded first from
        /// <c>Resources/Audio/Gold/Detective/...</c> and falling back to a
        /// temporary procedural placeholder. Unchanged this phase (brief
        /// section 18: audio integration is explicitly deferred) — kept
        /// exactly as C8.1g.2 shipped it.</summary>
        private void BuildAudio()
        {
            _sfxAudioSource = _host.gameObject.AddComponent<AudioSource>();
            _sfxAudioSource.playOnAwake = false;
            _sfxAudioSource.spatialBlend = 0f;

            _caseOpenClip = RuntimeUIFactory.LoadAudio("Audio/Gold/Detective/Detective_CaseOpen")
                ?? ProceduralAudio.CameraShutterClick("DetectiveCaseOpen_TemporaryFallback", 0.16f, 0.22f);
            _focusClip = RuntimeUIFactory.LoadAudio("Audio/Gold/Detective/Detective_Focus")
                ?? ProceduralAudio.PaperRustle("DetectiveFocus_TemporaryFallback", 0.08f, 0.08f);
            _accuseClip = RuntimeUIFactory.LoadAudio("Audio/Gold/Detective/Detective_Accuse")
                ?? ProceduralAudio.FileSlap("DetectiveAccuse_TemporaryFallback", 0.18f, 0.3f);
            _correctRevealClip = RuntimeUIFactory.LoadAudio("Audio/Gold/Detective/Detective_CorrectReveal")
                ?? ProceduralAudio.EvidenceConfirmChime("DetectiveCorrectReveal_TemporaryFallback", 0.22f, 0.3f);
            _incorrectRevealClip = RuntimeUIFactory.LoadAudio("Audio/Gold/Detective/Detective_IncorrectReveal")
                ?? ProceduralAudio.CaseRejectedTone("DetectiveIncorrectReveal_TemporaryFallback", 0.3f, 0.22f);
        }

        private void PlayPhaseAudio(AudioClip clip)
        {
            if (_sfxAudioSource == null)
            {
                return;
            }

            _sfxAudioSource.Stop();
            _sfxAudioSource.clip = clip;
            if (clip != null)
            {
                _sfxAudioSource.Play();
            }
        }

        /// <summary>C8.1g.2 brief section 3/4/5 (unchanged this phase):
        /// <paramref name="challenge"/> still owns its authored Items/
        /// AnomalyIndex, but <paramref name="displayOrder"/> and
        /// <paramref name="displayAnomalyIndex"/> are what this presenter
        /// actually renders and reveals against.</summary>
        public void ShowChallenge(ErrorDetectionChallenge challenge, IReadOnlyList<int> displayOrder, int displayAnomalyIndex)
        {
            if (_revealRoutine != null)
            {
                _host.StopCoroutine(_revealRoutine);
                _revealRoutine = null;
            }

            _challenge = challenge;
            _displayOrder = displayOrder;
            _displayAnomalyIndex = displayAnomalyIndex;
            _lastFocusedIndex = -1;

            _root.gameObject.SetActive(true);
            _cueText.text = challenge.CasePrompt;
            _ruleLabelText.text = challenge.RuleLabel;
            _recapBorder.gameObject.SetActive(false);
            ResetSpotlightTransform();
            _spotlight.gameObject.SetActive(true);
            _lightsOutGroup.alpha = 0f;
            if (_auditorFrame != null)
            {
                _auditorFrame.localScale = Vector3.one;
            }

            PlayPhaseAudio(_caseOpenClip);

            Button first = null;
            for (var i = 0; i < _suspects.Count; i++)
            {
                var hasItem = i < displayOrder.Count;
                _suspects[i].gameObject.SetActive(hasItem);
                if (!hasItem)
                {
                    continue;
                }

                _suspectLabels[i].text = challenge.Items[displayOrder[i]];
                TwoLineTextFit.Apply(_suspectLabels[i], LabelMaxFontSize, LabelMinFontSize, LabelMaxLines);
                _suspectImages[i].color = Color.white;
                _suspects[i].interactable = true;
                SetAlpha(_impostorTags[i], 0f);
                first ??= _suspects[i];
            }

            RuntimeUIFactory.Select(first);
        }

        /// <summary>Spotlight follows the current selection — same cheap
        /// transform-follow trick as Western Shootout's reticle. C8.1g.3
        /// adds a mild per-dossier brightness tint on the newly-focused
        /// suspect (brief section 9's "mild contrast increase... not a
        /// bright UI outline") — the previously-focused suspect's tint is
        /// cleared back to neutral in the same step. Plays the (very
        /// quiet) focus cue only on an actual selection CHANGE. Initial
        /// selection never implies correctness.</summary>
        public void RenderDecision()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null)
            {
                return;
            }

            var selectedIndex = _suspects.FindIndex(s => s.gameObject == selected);
            if (selectedIndex < 0)
            {
                return;
            }

            _spotlight.position = _suspectRects[selectedIndex].position;

            if (selectedIndex != _lastFocusedIndex)
            {
                if (_lastFocusedIndex >= 0 && _lastFocusedIndex < _suspectImages.Count)
                {
                    _suspectImages[_lastFocusedIndex].color = Color.white;
                }

                _suspectImages[selectedIndex].color = FocusTintColor;
                _lastFocusedIndex = selectedIndex;
                PlayPhaseAudio(_focusClip);
            }
        }

        /// <summary>-1 means the decision window ran out with no accusation.
        /// Otherwise <paramref name="accusedIndex"/> is the ON-SCREEN slot
        /// the player clicked — compared directly against
        /// <see cref="_displayAnomalyIndex"/>, never against the
        /// challenge's own authored AnomalyIndex.</summary>
        public void RevealOutcome(int accusedIndex)
        {
            _spotlight.gameObject.SetActive(false);
            ResetSpotlightTransform();

            for (var i = 0; i < _suspects.Count; i++)
            {
                if (_suspects[i].gameObject.activeSelf)
                {
                    _suspects[i].interactable = false;
                }
            }

            if (accusedIndex < 0)
            {
                _revealRoutine = TimeoutRevealRoutine();
                _host.StartCoroutine(_revealRoutine);
                return;
            }

            if (accusedIndex == _displayAnomalyIndex)
            {
                _revealRoutine = CorrectRevealRoutine(accusedIndex);
            }
            else
            {
                _revealRoutine = IncorrectRevealRoutine(accusedIndex);
            }

            _host.StartCoroutine(_revealRoutine);
        }

        /// <summary>C8.1g.5 brief section 6: the click itself already
        /// communicates selection — a tiny press/punch response is enough,
        /// no accusation frame, no dimming the other three, no color of
        /// any kind. The correct/incorrect result (see the reveal routines
        /// below) is the only thing that ever colors a dossier now.</summary>
        private void ShowAccusedCue(int index)
        {
            if (index < 0 || index >= _suspectRects.Count)
            {
                return;
            }

            LocalMotionFx.Punch(_host, _protestHandles[index], _suspectRects[index], 0.14f, 1.06f);
            PlayPhaseAudio(_accuseClip);
        }

        /// <summary>CORRECT ACCUSATION: accusation -&gt; brief pause -&gt;
        /// the accused (= actual anomaly) dossier gets the restrained amber
        /// tint + IMPOSTOR text -&gt; explanation (brief section 3). The
        /// other three are left alone — no dimming, no "elimination"
        /// (brief section 5).</summary>
        private IEnumerator CorrectRevealRoutine(int accusedIndex)
        {
            yield return new WaitForSeconds(0.12f);

            ResetAllDossierTints();
            ExposeImpostor(accusedIndex);
            PlayPhaseAudio(_correctRevealClip);

            if (_auditorFrame != null)
            {
                // Restrained per the brief — Auditor Severo's "small
                // satisfied smirk", not a big celebration.
                LocalMotionFx.Punch(_host, _auditorHandle, _auditorFrame, 0.24f, 1.06f);
            }

            ShowRecap();
        }

        /// <summary>INCORRECT ACCUSATION (brief section 4 — "the key
        /// behavior change"): accusation -&gt; ONE brief restrained shake on
        /// the wrong pick (no color at all, so it returns to neutral the
        /// instant the shake ends — nothing to "revert") -&gt; brief pause
        /// -&gt; the ACTUAL anomaly gets the exact same amber tint + IMPOSTOR
        /// treatment a correct guess would -&gt; explanation. The message is
        /// simply "you chose incorrectly, this was the correct one" — never
        /// a multi-stage accusation/rejection/reveal spectacle, never a
        /// persistent color on the wrong suspect.</summary>
        private IEnumerator IncorrectRevealRoutine(int accusedIndex)
        {
            yield return new WaitForSeconds(0.12f);

            ResetAllDossierTints();
            LocalMotionFx.Shake(_host, _protestHandles[accusedIndex], _suspectRects[accusedIndex], 0.2f, 6f);
            PlayPhaseAudio(_incorrectRevealClip);

            yield return new WaitForSeconds(0.22f);

            ExposeImpostor(_displayAnomalyIndex);
            PlayPhaseAudio(_correctRevealClip);

            ShowRecap();
        }

        /// <summary>No valid pair to react to — never synthesize a fake
        /// accusation for a missing selection. Same simplified language as
        /// every other outcome: highlight only the actual anomaly, show
        /// IMPOSTOR, show the recap (brief section 8).</summary>
        private IEnumerator TimeoutRevealRoutine()
        {
            LocalMotionFx.FadeAlpha(_host, _lightsOutHandle, _lightsOutGroup, 0f, 0.6f, 0.15f);
            yield return new WaitForSeconds(0.25f);
            LocalMotionFx.FadeAlpha(_host, _lightsOutHandle, _lightsOutGroup, 0.6f, 0f, 0.35f);
            yield return new WaitForSeconds(0.35f);

            ResetAllDossierTints();
            ExposeImpostor(_displayAnomalyIndex);
            PlayPhaseAudio(_incorrectRevealClip);

            ShowRecap();
        }

        /// <summary>C8.1g.5 brief section 3/7: the ENTIRE result vocabulary
        /// now — a restrained amber MULTIPLY tint on the dossier's own art
        /// (never a flat color replace, so the artwork stays visible
        /// underneath) plus the dynamic "IMPOSTOR" text, with a small
        /// confirming punch on the dossier itself (the nested
        /// ImpostorRevealArt frame this used to punch is retired — see the
        /// brief's own "environment bay -&gt; dossier frame -&gt; smaller
        /// reveal frame" clutter complaint). Used identically whether the
        /// player found the impostor themselves or not — the real answer
        /// must always be unmistakable.</summary>
        private void ExposeImpostor(int index)
        {
            if (index < 0 || index >= _suspects.Count || !_suspects[index].gameObject.activeSelf)
            {
                return;
            }

            _suspectImages[index].color = ImpostorDossierTint;
            SetAlpha(_impostorTags[index], 1f);
            LocalMotionFx.Punch(_host, _revealHandles[index], _suspectRects[index], 0.28f, 1.15f);
        }

        /// <summary>C8.1g.2 brief section 10: Rule / Impostor / Why — always
        /// derived from the AUTHORED challenge data, never from display
        /// order.</summary>
        private void ShowRecap()
        {
            _recapRuleText.text = $"Regla: {_challenge.RuleLabel}";
            _recapImpostorText.text = $"Impostor: {_challenge.Items[_challenge.AnomalyIndex]}";
            _recapWhyText.text = _challenge.Explanation;
            _recapBorder.gameObject.SetActive(true);
        }

        /// <summary>Clears every dossier's own tint (the focus brightness
        /// tint from <see cref="RenderDecision"/>) back to neutral white —
        /// called at the start of every reveal routine, so the one result
        /// tint (see <see cref="ExposeImpostor"/>) always applies cleanly
        /// on top of a known-neutral state rather than compounding with
        /// whatever was showing a moment before.</summary>
        private void ResetAllDossierTints()
        {
            for (var i = 0; i < _suspectImages.Count; i++)
            {
                if (_suspects[i].gameObject.activeSelf)
                {
                    _suspectImages[i].color = Color.white;
                }
            }
        }

        /// <summary>C8.1g.2 brief section 14: the confirmed fix for the
        /// Spotlight anchoredPosition/scale/color lifecycle bug — called
        /// from every path that could leave the Spotlight mid-follow.</summary>
        private void ResetSpotlightTransform()
        {
            _spotlight.anchoredPosition = _spotlightRestAnchoredPosition;
            _spotlight.localScale = _spotlightRestScale;
            _spotlight.GetComponent<Image>().color = _spotlightRestColor;
        }

        private static void SetAlpha(Image image, float alpha)
        {
            image.color = new Color(image.color.r, image.color.g, image.color.b, alpha);
        }

        private static void SetAlpha(Text text, float alpha)
        {
            text.color = new Color(text.color.r, text.color.g, text.color.b, alpha);
        }

        /// <summary>C8.1g.5 brief section 10: Hide/reset/re-entry must
        /// clear every remaining piece of result state — dossier result
        /// tint, IMPOSTOR text, focus tint, the recap, and the lights-out
        /// overlay/Auditor reaction transform (unchanged from C8.1g.2/g.3).
        /// The retired AccusedFrame/ClearedOverlay/ImpostorRevealArt no
        /// longer exist at all, so there is nothing left for them to leak —
        /// they can never "accidentally appear" because this presenter
        /// never creates them any more.</summary>
        public void Hide()
        {
            if (_revealRoutine != null)
            {
                _host.StopCoroutine(_revealRoutine);
                _revealRoutine = null;
            }

            if (_sfxAudioSource != null)
            {
                _sfxAudioSource.Stop();
            }

            ResetSpotlightTransform();
            _spotlight.gameObject.SetActive(false);
            ResetAllDossierTints();

            for (var i = 0; i < _suspects.Count; i++)
            {
                SetAlpha(_impostorTags[i], 0f);
            }

            _lastFocusedIndex = -1;
            _recapBorder.gameObject.SetActive(false);
            _lightsOutGroup.alpha = 0f;
            if (_auditorFrame != null)
            {
                _auditorFrame.localScale = Vector3.one;
            }

            _root.gameObject.SetActive(false);
        }
    }
}
