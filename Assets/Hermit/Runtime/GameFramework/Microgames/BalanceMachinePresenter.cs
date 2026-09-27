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
    /// C8.1f — Balance Machine, redesigned around the Debit/Credit account
    /// selection mechanic (Docs/C8_1F_BALANCE_MECHANIC_REDESIGN.md): the
    /// player answers "¿Qué cuenta se carga?" then "¿Qué cuenta se
    /// acredita?" as two sequential picks from a shared option list, and
    /// only once both lock does the machine react at all. That mechanic is
    /// UNCHANGED by C8.1f.3/C8.1f.4 — every field/method below that drives
    /// <see cref="AccountSelected"/>, option population, or outcome
    /// branching is the same logic, only reconnected to new presentation.
    ///
    /// C8.1f.3 first replaced the C8.1b-era procedural rectangles/
    /// rounded-panel "industrial base/post/pivot cap/beam/pans" with the
    /// approved Gold art set, chroma-extracted from the Midjourney
    /// candidates under ArtBible/Candidates/Worlds/Balance/ — independent
    /// Beam/Pans/Pivot-Lock sprites overlaid on the Machine Master's own
    /// static body.
    ///
    /// C8.1f.4 answered a human Gold review that the result still felt
    /// like "UI layered over a machine" rather than the machine physically
    /// processing the decision: token insertion now visibly travels in
    /// from above (<see cref="InsertBothTokens"/>) rather than fading in
    /// place; a new outcome-agnostic Processing beat
    /// (<see cref="ProcessingSequence"/>) sits between "both tokens seated"
    /// and the outcome resolving; and a central dynamic-text verdict
    /// ("EQUILIBRIO"/"DESEQUILIBRIO", never baked art — see
    /// <see cref="ShowVerdict"/>) now presents at the pivot itself before
    /// the recap. That same pass also replaced every procedural audio
    /// fallback that used a pure sine tone (the original "equilibrium
    /// lock" clip was two sine tones a clean octave apart — a human review
    /// correctly heard it as a musical "TUU", not a mechanical clack) with
    /// noise-based mechanical synthesis — see <see cref="BuildAudio"/>.
    ///
    /// C8.1f.5 addressed two further human-reported problems. First, the
    /// C8.1f.3 independent Beam/LeftPan/RightPan/PivotLock sprites turned
    /// out to be stylistically related but NOT geometrically identical
    /// parts of the Machine Master's own illustration (a different pivot
    /// hub design, a direct-bolt vs. chain-hung pan style) — visually
    /// "Frankenstein", exactly as reported. <c>Balance_MachineMaster.png</c>
    /// is now the single visual authority: those four candidate sprites
    /// are retired as production layers (kept in the project only as
    /// reference/candidate art, per the brief's own explicit allowance —
    /// see <see cref="BuildMachine"/> for the full reasoning and the
    /// minimum-layer solution actually used: the whole Master image tilts
    /// as one rigid body, and a purely procedural, abstract radial glow
    /// (<see cref="RuntimeUIFactory.GetRadialGlowSprite"/> — never a second
    /// illustrated "hub") stands in for pivot/verdict feedback, so nothing
    /// visible can ever conflict with the Master's own baked geometry.
    /// Second, a real lifecycle bug: Balance's processing/result audio
    /// could keep playing well past the round ending — including into the
    /// Results screen, if Balance was the session's last microgame — traced
    /// to real supplied clips (<c>Balance_TokenInsert.mp3</c>,
    /// <c>Balance_CorrectLock.mp3</c>) being 7-8 seconds long while every
    /// <c>PlayOneShot</c> call simply let them run to completion regardless
    /// of the (much shorter) visual beat they belong to. See
    /// <see cref="StopAllBalanceAudio"/> and <see cref="PlayPhaseAudio"/>
    /// for the fix.
    /// </summary>
    internal sealed class BalanceMachinePresenter : IMicrogamePresenter
    {
        private const int MaxOptions = 4;

        // C8.1f.5: now a WHOLE-MACHINE tilt (rotating the entire coherent
        // Master illustration as one rigid body — see BuildMachine), not an
        // isolated beam sprite rotating in front of a static body. Halved
        // from the old 14f: a large machine tilting by 14 degrees reads as
        // "falling over"; a "subtle whole-machine reaction" (brief section
        // 5) needs a more restrained range. Existing test thresholds
        // (Partial > 1 degree, Incorrect > 5 degrees) both still hold
        // comfortably at this value — see those tests' own assertions.
        private const float MaxTiltAngle = 8f;

        // C8.1f.4: token travel + a dedicated Processing beat, inserted
        // between "both tokens seated" and "the outcome resolves" — see
        // ProcessingSequence's own doc-comment for what happens during it.
        private const float TokenTravelSeconds = 0.5f;
        private const float TokenTravelDistance = 90f;
        private const float ProcessingSeconds = 1.0f;
        private const float VerdictHoldSeconds = 0.6f;
        private const float TokenRestY = -PanWidth * 0.62f;

        // C8.1f.3/C8.1f.5 machine layout constants — see BuildMachine's own
        // comments for the pixel-alignment reasoning behind each of these.
        private const float MachineWidth = 550f;
        private const float MachineAnchorFraction = 0.476f;
        private const float PivotAnchorFraction = 0.5735f;
        private const float PanXOffset = 190f;
        private const float PanWidth = 170f;
        private const float TokenWidth = 130f;

        // C8.1f.5: the Machine Master's own pivot/wheel hub sits at ~70% of
        // the image's height up from the bottom (measured directly from the
        // source art) — 20% of the image's own height above its vertical
        // center. Used to offset the whole image within its rotating parent
        // so that parent's rotation origin lands exactly on the real hub.
        private const float MasterHubOffsetFraction = 0.20f;

        /// <summary>Fired once per locked pick — first call is the debit
        /// answer, second is the credit answer. The host forwards both,
        /// unchanged, to <see cref="Hermit.Games.Clasico.ClasicoSessionDirector.SubmitBalanceAccount"/>,
        /// which itself knows which step each call belongs to.</summary>
        public event Action<int> AccountSelected;

        private readonly MonoBehaviour _host;

        private RectTransform _root;
        private RectTransform _beamPivotRoot;
        private RectTransform _pivotGlowRect;
        private Image _pivotGlowImage;

        private RectTransform _leftPan;
        private RectTransform _rightPan;
        private Image _leftPanImage;
        private Image _rightPanImage;
        private float _panBaselineY;

        private Text _transactionText;
        private Text _promptText;

        private readonly List<Button> _optionButtons = new List<Button>();
        private readonly List<Text> _optionLabels = new List<Text>();
        private readonly List<Image> _optionImages = new List<Image>();

        private RectTransform _debitTokenRect;
        private RectTransform _creditTokenRect;
        private Image _debitTokenImage;
        private Image _creditTokenImage;
        private Text _debitTokenText;
        private Text _creditTokenText;

        private readonly List<Image> _smoke = new List<Image>();
        private readonly List<RectTransform> _smokeRects = new List<RectTransform>();

        private RectTransform _recapBorder;
        private RectTransform _recapPanel;
        private Text _recapTransactionText;
        private Text _recapDebitText;
        private Text _recapCreditText;
        private TMP_Text _recapExplanationText;

        // C8.1j premium typography pass: an industrial dark navy/charcoal
        // plate with a restrained brass edge (brief section 5/7) — replaces
        // the recap panel's old flat single-color rounded rect.
        private static readonly Color RecapFillColor = new Color(0.071f, 0.086f, 0.114f, 1f);
        private static readonly Color RecapBorderColor = new Color(0.56f, 0.45f, 0.24f, 1f);

        private RectTransform _verdictBorder;
        private RectTransform _verdictPanel;
        private Text _verdictText;

        // C8.1f.4: named after the external hook they represent (brief
        // section 4/12), not after the temporary procedural sound that
        // currently fills each one — see BuildAudio's own doc-comment.
        private AudioSource _sfxAudioSource;
        private AudioClip _tokenInsertClip;
        private AudioClip _processingClip;
        private AudioClip _correctLockClip;
        private AudioClip _incorrectJamClip;
        private AudioClip _smokePressureClip;

        private readonly MotionHandle _beamPunchHandle = new MotionHandle();
        private readonly MotionHandle _beamShakeHandle = new MotionHandle();
        private readonly MotionHandle _leftPanFlashHandle = new MotionHandle();
        private readonly MotionHandle _rightPanFlashHandle = new MotionHandle();
        private readonly MotionHandle _leftPanPunchHandle = new MotionHandle();
        private readonly MotionHandle _rightPanPunchHandle = new MotionHandle();
        private readonly MotionHandle _pivotGlowPunchHandle = new MotionHandle();

        private DebitCreditChallenge _challenge;
        private DebitCreditStepLocal _currentStep;
        private int _selectedDebitIndex = -1;
        private int _selectedCreditIndex = -1;
        private Coroutine _sequenceRoutine;
        private HermitTheme Theme => RuntimeUIFactory.Theme;

        // The art's own cyan accent (see the Account Token/Pivot Lock
        // candidates) — none of HermitTheme's existing colors are cyan, and
        // this is specifically meant to echo that imported artwork's own
        // highlight rather than introduce an unrelated hue.
        private static readonly Color LockCyan = new Color(0.45f, 0.85f, 0.95f, 1f);

        /// <summary>Mirrors <see cref="Hermit.Games.Clasico.Microgames.DebitCreditStep"/>
        /// locally — that type is internal to Hermit.Games and this
        /// presenter lives in Hermit.Runtime, so it tracks the same "which
        /// prompt is showing" concept independently rather than crossing
        /// the assembly boundary for it. Both always advance in lockstep,
        /// one call per click, so they cannot desync.</summary>
        private enum DebitCreditStepLocal
        {
            Debit,
            Credit
        }

        public BalanceMachinePresenter(MonoBehaviour host)
        {
            _host = host;
        }

        public void Build(Transform stageRoot)
        {
            _root = RuntimeUIFactory.CreatePanel(stageRoot, "BalanceMachine", new Color(0.10f, 0.12f, 0.16f, 1f));

            BuildMachine();
            BuildVerdict();
            BuildOptionRow();
            BuildSmoke();
            BuildAudio();
            BuildRecap();

            _transactionText = RuntimeUIFactory.CreateText(
                _root, "Transaction", string.Empty, Theme.BodySize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -46), new Vector2(820, 60));

            _promptText = RuntimeUIFactory.CreateText(
                _root, "Prompt", string.Empty, Theme.HeadingSize, TextAnchor.MiddleCenter, Theme.Warning,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -104), new Vector2(820, 40));

            _root.gameObject.SetActive(false);
        }

        /// <summary>C8.1f.5: builds the Gold-art machine body as ONE
        /// coherent object, per the brief's own root-cause finding and
        /// "Machine Master is now the single visual authority" rule. The
        /// C8.1f.3 independent Beam/LeftPan/RightPan/PivotLock sprites were
        /// stylistically related Midjourney generations but NOT
        /// geometrically identical parts of the same illustration (a
        /// different pivot-hub design; a direct-bolt vs. chain-hung pan
        /// style) — visually "Frankenstein", exactly as the human review
        /// described. Cropping/masking a clean beam or pan cutout directly
        /// FROM the Master (the brief's stated preferred approach) was
        /// evaluated and rejected: the Master's own beam/pans are baked
        /// into a single flat illustration with no clean seam to cut along
        /// without visible tearing, so per the brief's own explicit
        /// fallback ("if extracting beam/pans independently creates
        /// obvious holes or artifacts... use the full coherent Machine
        /// Master as the visible machine and animate only: a subtle
        /// whole-machine reaction, token anchors, central verdict/pivot
        /// overlay, smoke") — that is exactly what this method does:
        ///
        /// - The chamber background stays a separate, non-rotating
        ///   backdrop (it was never part of the coherence complaint).
        /// - The ENTIRE Machine Master image becomes the single visible
        ///   machine body, parented under <see cref="_beamPivotRoot"/> (see
        ///   that field's own naming note) so rotating that one transform
        ///   tilts the whole coherent object as one rigid body — "beam
        ///   movement" is now a whole-machine reaction, not an isolated
        ///   beam sprite fighting a static body. <see cref="MasterHubOffsetFraction"/>
        ///   offsets the image within its parent so the parent's rotation
        ///   origin lands exactly on the Master's own real pivot/wheel hub
        ///   (measured directly from the source art), not the image's
        ///   arbitrary center.
        /// - A single, purely procedural, abstract radial glow
        ///   (<see cref="RuntimeUIFactory.GetRadialGlowSprite"/>) sits at
        ///   the hub for pivot/verdict feedback — never a second
        ///   illustrated "hub" sprite that could visually conflict with the
        ///   Master's own baked wheel (brief section 6: "no two different
        ///   central hubs").
        /// - The pans are no longer independent visible sprites at all —
        ///   <see cref="BuildPan"/> now builds an invisible anchor only,
        ///   used purely to position each token and to fake a subtle
        ///   vertical "pan response" via <see cref="SetBeamAngle"/>'s
        ///   existing math (brief section 4's explicit fallback: "KEEP
        ///   THEM visually integrated with the master and fake vertical pan
        ///   response subtly").
        ///
        /// Net result: exactly one piece of illustrated machine geometry is
        /// ever visible (the Master itself), so there is no possible
        /// "two beams" / "duplicate pan rims" / "conflicting hubs" — brief
        /// section 6's "remove duplicated geometry" is satisfied
        /// structurally, not by careful positioning. The retired candidate
        /// sprites (Balance_Beam/LeftPan/RightPan/LockMedallion) remain on
        /// disk under Resources/Art/Gold/Balance/Machine/Parts/ as
        /// reference art (per the brief's own "may remain in the project as
        /// references/candidates") but are never loaded by this
        /// method.</summary>
        private void BuildMachine()
        {
            var chamberArt = RuntimeUIFactory.LoadArt("Art/Gold/Balance/Environment/Balance_Chamber");
            if (chamberArt != null)
            {
                var chamberWidth = MachineWidth * 1.13f;
                var chamberHeight = chamberWidth * (chamberArt.rect.height / chamberArt.rect.width);
                CreateArtImage(
                    _root, "ChamberBackground", chamberArt,
                    new Vector2(0.5f, MachineAnchorFraction), new Vector2(0.5f, MachineAnchorFraction), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(chamberWidth, chamberHeight));
            }

            // Named "Beam" (not "WholeMachineRoot") deliberately — this
            // exact transform's own localRotation is what the pre-existing
            // Balance_FirstSelection_LocksDebitAndAdvancesToCreditStep_WithoutAnyMachineReaction
            // PlayMode test (and every equilibrium/off-level outcome test)
            // already looks up by that name; renaming it would have broken
            // every one of those already-passing, already-correct tests for
            // no reason. It now rotates the WHOLE coherent machine, not an
            // isolated beam sprite — see this method's own doc-comment.
            _beamPivotRoot = new GameObject("Beam", typeof(RectTransform)).GetComponent<RectTransform>();
            _beamPivotRoot.SetParent(_root, false);
            _beamPivotRoot.anchorMin = _beamPivotRoot.anchorMax = new Vector2(0.5f, PivotAnchorFraction);
            _beamPivotRoot.pivot = new Vector2(0.5f, 0.5f);
            _beamPivotRoot.anchoredPosition = Vector2.zero;
            _beamPivotRoot.sizeDelta = Vector2.zero;

            var masterArt = RuntimeUIFactory.LoadArt("Art/Gold/Balance/Machine/Balance_MachineMaster");
            if (masterArt != null)
            {
                var masterHeight = MachineWidth * (masterArt.rect.height / masterArt.rect.width);
                var hubOffsetFromCenter = masterHeight * MasterHubOffsetFraction;
                CreateArtImage(
                    _beamPivotRoot, "MachineMaster", masterArt,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0, -hubOffsetFromCenter), new Vector2(MachineWidth, masterHeight));
            }

            // The single, abstract pivot/verdict feedback surface — see
            // this method's own doc-comment for why it is never a second
            // illustrated sprite. Dim/neutral until Processing or an
            // outcome earns something brighter — see SetPivotGlowDim/
            // SetPivotGlowColor/FadePivotGlow.
            _pivotGlowImage = CreateArtImage(
                _beamPivotRoot, "PivotGlow", RuntimeUIFactory.GetRadialGlowSprite(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(90, 90));
            _pivotGlowRect = (RectTransform)_pivotGlowImage.transform;
            _pivotGlowImage.color = new Color(1f, 1f, 1f, 0.22f);

            BuildPan("LeftPan", -PanXOffset, out _leftPan, out _leftPanImage, out _debitTokenRect, out _debitTokenImage, out _debitTokenText, "DEBE");
            BuildPan("RightPan", PanXOffset, out _rightPan, out _rightPanImage, out _creditTokenRect, out _creditTokenImage, out _creditTokenText, "HABER");

            _panBaselineY = -10f;
        }

        /// <summary>C8.1f.4: the central verdict focal point (brief section
        /// 5/10) — "EQUILIBRIO"/"DESEQUILIBRIO", dynamic Unity text (never
        /// baked art), sitting just below the pivot glow (C8.1f.5) inside
        /// the machine's own base housing so it reads as an integrated
        /// readout rather than a floating HUD label. Hidden (alpha 0, empty
        /// text, transparent backing) until an outcome's own sequence calls
        /// <see cref="ShowVerdict"/>, well after the Processing beat —
        /// never revealed early.</summary>
        private void BuildVerdict()
        {
            // C8.1j: same bordered industrial-panel language as the recap
            // below it (brief section 14: one consistent visual grammar per
            // world) — a compact "machine readout" header rather than a
            // flat rounded rect.
            var (border, content) = RuntimeUIFactory.CreatePremiumPanel(
                _root, "VerdictPanel", RecapFillColor, RecapBorderColor, 2f, 10);
            border.anchorMin = border.anchorMax = new Vector2(0.5f, PivotAnchorFraction);
            border.anchoredPosition = new Vector2(0, -78f);
            border.sizeDelta = new Vector2(210, 40);
            border.GetComponent<Image>().raycastTarget = false;
            content.GetComponent<Image>().raycastTarget = false;
            _verdictBorder = border;
            _verdictPanel = content;

            _verdictText = RuntimeUIFactory.CreateText(
                _verdictPanel, "Verdict", string.Empty, Theme.BodySize, TextAnchor.MiddleCenter, Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _verdictText.color = new Color(1f, 1f, 1f, 0f);
            _verdictText.raycastTarget = false;
            _verdictText.fontStyle = FontStyle.Bold;
            _verdictText.resizeTextForBestFit = true;
            _verdictText.resizeTextMinSize = 12;
            _verdictText.resizeTextMaxSize = Theme.BodySize;
        }

        /// <summary>C8.1f.5: no longer a visible sprite — the independent
        /// Balance_LeftPan/Balance_RightPan candidates are retired as
        /// production layers (see <see cref="BuildMachine"/>'s own
        /// doc-comment: they didn't match the Master's own direct-bolt pan
        /// style, and were part of the reported "Frankenstein" look). This
        /// is now purely an invisible anchor point: still a real
        /// <see cref="Image"/> component on the pan's own named GameObject
        /// (alpha 0, no sprite) so the pre-existing
        /// <c>FindImageUnder("BalanceMachine", "LeftPan"/"RightPan")</c>
        /// PlayMode lookups keep working unchanged — used only to position
        /// the token and to fake a subtle vertical "pan response" via
        /// <see cref="SetBeamAngle"/>'s existing math (brief section 4's
        /// explicit fallback for pans that can't be cleanly separated from
        /// the master).</summary>
        private void BuildPan(string name, float xOffset, out RectTransform panRect, out Image panImage,
            out RectTransform tokenRect, out Image tokenImage, out Text tokenText, string sideLabel)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            panRect = (RectTransform)go.transform;
            panRect.SetParent(_root, false);
            panRect.anchorMin = panRect.anchorMax = new Vector2(0.5f, PivotAnchorFraction);
            panRect.pivot = new Vector2(0.5f, 1f);
            panRect.anchoredPosition = new Vector2(xOffset, -10f);
            panRect.sizeDelta = new Vector2(PanWidth, PanWidth * 0.6f);

            panImage = go.GetComponent<Image>();
            panImage.raycastTarget = false;
            panImage.color = new Color(1f, 1f, 1f, 0f);

            RuntimeUIFactory.CreateText(
                panRect, "SideLabel", sideLabel, Theme.CaptionSize, TextAnchor.MiddleCenter, Theme.TextSecondary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, 12), new Vector2(PanWidth - 10, 22));

            BuildToken(panRect, out tokenRect, out tokenImage, out tokenText);
        }

        /// <summary>One small labeled token per pan — hidden (alpha 0,
        /// centered) until <see cref="RevealOutcome"/> materializes and
        /// inserts it. A child of its pan so it travels with it exactly
        /// like the old numeric value labels did. Uses the approved Account
        /// Token art as the physical plate; dynamic Unity text sits on top
        /// — no account name is ever baked into artwork (brief section 7).</summary>
        private void BuildToken(RectTransform pan, out RectTransform rect, out Image image, out Text label)
        {
            var tokenArt = RuntimeUIFactory.LoadArt("Art/Gold/Balance/Tokens/Balance_AccountToken");

            var go = new GameObject("Token", typeof(RectTransform));
            rect = (RectTransform)go.transform;
            rect.SetParent(pan, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, TokenRestY);

            if (tokenArt != null)
            {
                var tokenHeight = TokenWidth * (tokenArt.rect.height / tokenArt.rect.width);
                rect.sizeDelta = new Vector2(TokenWidth, tokenHeight);
                image = CreateArtImage(rect, "Plate", tokenArt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                image.rectTransform.anchorMin = Vector2.zero;
                image.rectTransform.anchorMax = Vector2.one;
                image.rectTransform.offsetMin = Vector2.zero;
                image.rectTransform.offsetMax = Vector2.zero;
            }
            else
            {
                rect.sizeDelta = new Vector2(TokenWidth, TokenWidth * 0.62f);
                image = RuntimeUIFactory.CreateRoundedPanel(rect, "Plate", new Color(0.85f, 0.8f, 0.65f, 1f), 10).GetComponent<Image>();
                RuntimeUIFactory.StretchFull((RectTransform)image.transform);
            }

            image.color = new Color(image.color.r, image.color.g, image.color.b, 0f);

            var theme = RuntimeUIFactory.Theme;
            // C8.1f.6 hotfix: the old 70%x60%-of-token inset (0.15-0.85 /
            // 0.2-0.8) let the pool's longer real account names ("Préstamo
            // bancario por pagar", "Seguro pagado por anticipado") wrap
            // onto 3+ lines — and since RuntimeUIFactory.CreateText
            // defaults every label to VerticalWrapMode.Overflow (correct
            // for most of this project's un-boxed captions, wrong for a
            // label that must stay inside a small piece of art), the extra
            // line(s) rendered right past the token's edge — the reported
            // "text overflows outside the token frame" defect.
            //
            // Measuring directly with Unity's own TextGenerator (see the
            // PlayMode test for this label) found a non-obvious property of
            // Legacy Text's Best Fit: it only shrinks the font enough to
            // make the WRAPPED text's total height fit the box — it does
            // NOT try to minimize line count. A tall, generous box (tried
            // first, ~76% of the token's height) gave Best Fit so much
            // vertical room that it happily stopped at a large font that
            // still wrapped long names to 3 lines, because 3 lines at that
            // size already fit the tall box. The actual fix is a
            // deliberately SHORT box (48% of the token's height): with
            // only ~2 lines' worth of vertical room available, Best Fit is
            // forced to keep shrinking until the width also cooperates,
            // which is what makes 2 lines the natural stopping point. Width
            // stayed generous (88% of the token) so that shrinking has
            // real horizontal room to work with. Verified against every
            // name in DebitCreditPool. verticalOverflow is explicitly
            // Truncate (not CreateText's Overflow default) as a backstop:
            // even if some future longer name defeats this sizing, nothing
            // renders past the label's own bounds — worst case is a
            // clipped trailing line, never a spill past the token art.
            label = RuntimeUIFactory.CreateText(
                rect, "Label", string.Empty, theme.CaptionSize, TextAnchor.MiddleCenter, theme.TextPrimary,
                new Vector2(0.06f, 0.26f), new Vector2(0.94f, 0.74f), Vector2.zero, Vector2.zero);
            label.color = new Color(label.color.r, label.color.g, label.color.b, 0f);
            // Account names vary a lot in length ("Caja" vs "Cuentas por
            // cobrar" vs "Préstamo bancario por pagar") — auto-shrink
            // within a sane range rather than overflowing the token's own
            // label window (brief section 7; re-tuned C8.1f.6). Min lowered
            // 10 -> 7: the longest pool names need it to resolve to 2
            // lines within the box above; short names are unaffected since
            // Best Fit only shrinks as far as a given string actually needs.
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 7;
            label.resizeTextMaxSize = theme.CaptionSize;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static Image CreateArtImage(Transform parent, string name, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Balance's own mechanical sound identity (brief section
        /// 18, revised by C8.1f.4 sections 4/12/13) — never Western's
        /// gunshot-family cues, and — the specific regression C8.1f.4 fixes
        /// — never a tonal/polyphonic procedural "success" sound either
        /// (the original <c>MechanicalClack</c> built from two sine tones a
        /// clean octave apart, which a human Gold review correctly heard as
        /// a musical "TUU" chime, not a mechanical clack). Every clip here
        /// is loaded FIRST from a real, supplied Gold audio asset under
        /// <c>Resources/Audio/Gold/Balance/...</c> via
        /// <see cref="RuntimeUIFactory.LoadAudio"/> (its own "degrade with a
        /// warning, never crash" contract), and only falls back to a
        /// temporary procedural placeholder when that asset hasn't shipped
        /// yet. Every one of those fallbacks is noise-based mechanical
        /// synthesis now (see each <see cref="ProceduralAudio"/> method's
        /// own doc-comment) — none may ever be a pure sine tone standing in
        /// as the intended Gold result.</summary>
        private void BuildAudio()
        {
            _sfxAudioSource = _host.gameObject.AddComponent<AudioSource>();
            _sfxAudioSource.playOnAwake = false;
            _sfxAudioSource.spatialBlend = 0f;

            _tokenInsertClip = RuntimeUIFactory.LoadAudio("Audio/Gold/Balance/Balance_TokenInsert")
                ?? ProceduralAudio.MechanicalClick("BalanceTokenInsert_TemporaryFallback", 0.05f, 0.22f);
            _processingClip = RuntimeUIFactory.LoadAudio("Audio/Gold/Balance/Balance_Processing")
                ?? ProceduralAudio.MechanicalWhir("BalanceProcessing_TemporaryFallback", ProcessingSeconds, 0.10f);
            _correctLockClip = RuntimeUIFactory.LoadAudio("Audio/Gold/Balance/Balance_CorrectLock")
                ?? ProceduralAudio.MechanicalClack("BalanceCorrectLock_TemporaryFallback", 0.22f, 0.42f);
            _incorrectJamClip = RuntimeUIFactory.LoadAudio("Audio/Gold/Balance/Balance_IncorrectJam")
                ?? ProceduralAudio.MechanicalJam("BalanceIncorrectJam_TemporaryFallback", 0.4f, 0.30f);
            _smokePressureClip = RuntimeUIFactory.LoadAudio("Audio/Gold/Balance/Balance_SmokePressure")
                ?? ProceduralAudio.Noise("BalanceSmokePressure_TemporaryFallback", 0.22f, 0.10f);
        }

        /// <summary>Up to <see cref="MaxOptions"/> account buttons, built
        /// once and reused for both the debit and credit step — same
        /// "N labeled buttons, chain them for gamepad/keyboard navigation"
        /// shape as Detective Lineup's suspects.</summary>
        private void BuildOptionRow()
        {
            const float buttonWidth = 190f;
            const float spacing = 20f;
            var startX = -((MaxOptions - 1) * (buttonWidth + spacing)) / 2f;

            for (var i = 0; i < MaxOptions; i++)
            {
                var index = i;
                var x = startX + i * (buttonWidth + spacing);

                var button = RuntimeUIFactory.CreateButton(
                    _root, $"BalanceAccountOption{i}", string.Empty,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 70), new Vector2(buttonWidth, 64));
                button.onClick.AddListener(() => OnOptionClicked(index));

                _optionButtons.Add(button);
                _optionLabels.Add(button.GetComponentInChildren<Text>());
                _optionImages.Add(button.GetComponent<Image>());
            }

            RuntimeUIFactory.ChainHorizontal(_optionButtons.ToArray());
        }

        private void BuildSmoke()
        {
            for (var i = 0; i < 4; i++)
            {
                var puff = RuntimeUIFactory.CreateRoundedPanel(_root, "Smoke", new Color(0.5f, 0.52f, 0.55f, 0f), 40);
                puff.anchorMin = puff.anchorMax = new Vector2(0.5f, PivotAnchorFraction);
                // Originates low, near the base/mechanism area below the
                // pivot — a believable stress point — not the pivot itself
                // (brief section 17).
                puff.anchoredPosition = new Vector2(0, -70);
                puff.sizeDelta = new Vector2(30, 30);
                puff.GetComponent<Image>().raycastTarget = false;
                _smoke.Add(puff.GetComponent<Image>());
                _smokeRects.Add(puff);
            }
        }

        private void BuildRecap()
        {
            // C8.1f.2: the panel spans y 0..180 — the proven-safe gap below
            // the pans and above the stage floor. C8.1j keeps this exact
            // outer footprint/position unchanged (still "do not regress its
            // vertical position") but rebuilds it as a genuine bordered
            // industrial panel (RuntimeUIFactory.CreatePremiumPanel) instead
            // of a flat rounded rect, and — the actual C8.1j readability
            // fix — rebalances the INTERNAL layout so the Explanation zone
            // gets real multi-line room instead of the old single 30px-tall
            // line. That old box was too small for any real
            // FeedbackExplanation string: at CaptionSize the text wrapped
            // past its own box (Text's default Overflow mode never clips),
            // spilling down past the panel's own bottom edge into the exact
            // screen band ClasicoHud's shared bottom-center Feedback banner
            // occupies — the real, structural cause of the reported
            // verdict/explanation collision. Fixed two ways at once: (1)
            // ClasicoGameHost now suppresses that shared banner for Balance
            // entirely (its own pivot verdict + this recap are strictly
            // more specific — see ClasicoHud.RenderFeedback's own C8.1j
            // doc-comment), and (2) the Explanation text itself now uses
            // TMP best-fit auto-sizing in a properly-sized multi-line box,
            // so it can never overflow regardless of string length.
            var (border, content) = RuntimeUIFactory.CreatePremiumPanel(
                _root, "TeachingRecap", RecapFillColor, RecapBorderColor, 3f, 16);
            border.anchorMin = border.anchorMax = new Vector2(0.5f, 0f);
            border.anchoredPosition = new Vector2(0, 90);
            border.sizeDelta = new Vector2(900, 180);
            border.GetComponent<Image>().raycastTarget = false;
            _recapBorder = border;
            _recapPanel = content;

            _recapTransactionText = RuntimeUIFactory.CreateText(
                _recapPanel, "RecapTransaction", string.Empty, Theme.CaptionSize, TextAnchor.MiddleCenter, Theme.TextSecondary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(860, 32));
            _recapTransactionText.color = new Color(_recapTransactionText.color.r, _recapTransactionText.color.g, _recapTransactionText.color.b, 0f);
            _recapTransactionText.raycastTarget = false;

            // Debit/Credit ("CARGA:"/"ACREDITA:") — moved up slightly and
            // given a shorter box than before to free real height for the
            // Explanation zone below. Ivory (Theme.AccentWarm), not the
            // generic arcade-green Theme.Correct — this is a teaching label,
            // not a correctness flash.
            _recapDebitText = RuntimeUIFactory.CreateText(
                _recapPanel, "Debit", string.Empty, Theme.BodySize, TextAnchor.MiddleCenter, Theme.AccentWarm,
                new Vector2(0.25f, 1f), new Vector2(0.25f, 1f), new Vector2(0, -58), new Vector2(260, 52));
            _recapDebitText.color = new Color(_recapDebitText.color.r, _recapDebitText.color.g, _recapDebitText.color.b, 0f);
            _recapDebitText.raycastTarget = false;

            _recapCreditText = RuntimeUIFactory.CreateText(
                _recapPanel, "Credit", string.Empty, Theme.BodySize, TextAnchor.MiddleCenter, Theme.AccentWarm,
                new Vector2(0.75f, 1f), new Vector2(0.75f, 1f), new Vector2(0, -58), new Vector2(260, 52));
            _recapCreditText.color = new Color(_recapCreditText.color.r, _recapCreditText.color.g, _recapCreditText.color.b, 0f);
            _recapCreditText.raycastTarget = false;

            // The actual fix: a real multi-line body (up to ~3-4 lines,
            // 84px tall — content's own bottom band, non-overlapping with
            // the Debit/Credit row above it) with TMP best-fit auto-sizing
            // between 13 and 18pt (brief section 7: "slightly reduce font
            // size within a controlled min/max range") — guarantees every
            // one of the 35 shipped FeedbackExplanation strings fits with
            // no clipping/overflow regardless of length, verified
            // per-string in Balance_AllFeedbackExplanations_FitWithinTheRecapPanel.
            _recapExplanationText = RuntimeUIFactory.CreatePremiumText(
                _recapPanel, "Explanation", string.Empty, Theme.CaptionSize, Theme.TextPrimary,
                TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 44), new Vector2(840, 84),
                lineSpacing: 2f, autoShrinkMinSize: 13f);
            SetTmpAlpha(_recapExplanationText, 0f);
        }

        public void ShowChallenge(DebitCreditChallenge challenge)
        {
            _challenge = challenge;
            _root.gameObject.SetActive(true);

            if (_sequenceRoutine != null)
            {
                _host.StopCoroutine(_sequenceRoutine);
                _sequenceRoutine = null;
            }

            // C8.1f.5: every lifecycle exit funnels through here or Hide()
            // — see StopAllBalanceAudio's own doc-comment for the full
            // contract this is one half of. Defensive: a genuinely clean
            // round transition should already be silent by the time this
            // runs, but a fresh round must never risk inheriting audio from
            // an interrupted previous one.
            StopAllBalanceAudio();

            _currentStep = DebitCreditStepLocal.Debit;
            _selectedDebitIndex = -1;
            _selectedCreditIndex = -1;

            _transactionText.text = challenge.TransactionText;
            _promptText.text = "¿QUÉ CUENTA SE CARGA?";

            SetBeamAngle(0f);
            SetPivotGlowDim();
            _pivotGlowRect.localScale = Vector3.one;

            // Reset to natural (untinted) color, not just alpha — a
            // Partial/Incorrect outcome tints the token RGB red/cyan
            // (see PartialSequence/IncorrectSequence), and SetTokenVisible
            // below only zeroes alpha, which would otherwise leave a stale
            // tint ready to reappear the instant the token re-materializes
            // next round.
            _debitTokenImage.color = Color.white;
            _creditTokenImage.color = Color.white;

            SetTokenVisible(_debitTokenRect, _debitTokenImage, _debitTokenText, false);
            SetTokenVisible(_creditTokenRect, _creditTokenImage, _creditTokenText, false);
            SetRecapVisible(false);
            SetSmokeVisible(false);
            HideVerdict();

            PopulateOptions(challenge.AccountOptions);
        }

        /// <summary>Sets the beam's own rotation AND moves both pans to the
        /// height that rotation implies at their real attachment point on
        /// the beam (<see cref="PanXOffset"/> either side of the pivot) —
        /// y = x * sin(angle), the standard small-rotation displacement of
        /// a point on a rigid beam. The pans themselves never rotate (a
        /// hinged/chain-hung pan stays level while the beam tilts — see
        /// <see cref="Build"/>'s own doc-comment) — only their Y
        /// position changes.</summary>
        private void SetBeamAngle(float degrees)
        {
            _beamPivotRoot.localRotation = Quaternion.Euler(0f, 0f, degrees);

            var radians = degrees * Mathf.Deg2Rad;
            var delta = PanXOffset * Mathf.Sin(radians);
            _leftPan.anchoredPosition = new Vector2(-PanXOffset, _panBaselineY - delta);
            _rightPan.anchoredPosition = new Vector2(PanXOffset, _panBaselineY + delta);
        }

        private void PopulateOptions(string[] options)
        {
            Button first = null;
            for (var i = 0; i < _optionButtons.Count; i++)
            {
                var hasOption = i < options.Length;
                _optionButtons[i].gameObject.SetActive(hasOption);
                if (!hasOption)
                {
                    continue;
                }

                _optionLabels[i].text = options[i];
                // Neutral visual weight for every option, every time this is
                // called (Step 1 build AND the Step 1 -> Step 2 refresh) —
                // the anti-cheat rule: nothing about an option's appearance
                // may hint at correctness before both picks lock (redesign
                // doc, section 13).
                _optionImages[i].color = Theme.PanelRaised;
                _optionButtons[i].interactable = true;
                first ??= _optionButtons[i];
            }

            RuntimeUIFactory.Select(first);
        }

        private void OnOptionClicked(int index)
        {
            if (_currentStep == DebitCreditStepLocal.Debit)
            {
                _selectedDebitIndex = index;
                AccountSelected?.Invoke(index);

                _currentStep = DebitCreditStepLocal.Credit;
                _promptText.text = "¿QUÉ CUENTA SE ACREDITA?";
                // Re-populate with identical neutral styling — same list,
                // same positions, nothing about the debit pick carries over
                // visually (redesign doc, section 7/13).
                PopulateOptions(_challenge.AccountOptions);
                return;
            }

            _selectedCreditIndex = index;
            foreach (var button in _optionButtons)
            {
                button.interactable = false;
                // Hidden, not just disabled: frees the whole button row as
                // a safe area for the teaching recap (redesign doc, C8.1f.2
                // recap-clipping fix) — the recap used to occupy that exact
                // region while these buttons (and their invisible-but-
                // raycastable child graphics) were still present, which was
                // also the ACREDITA click bug's root cause. ShowChallenge's
                // PopulateOptions call re-activates every button for the
                // next round.
                button.gameObject.SetActive(false);
            }

            AccountSelected?.Invoke(index);
        }

        /// <summary>No per-frame decision rendering needed — unlike the old
        /// continuous-nudge mechanic, this is a static "pick 1 of N" screen
        /// exactly like Game Show's, so <see cref="Hermit.Runtime.GameFramework.ClasicoGameHost"/>
        /// no longer calls anything here during the Decision phase.</summary>
        public void RevealOutcome(DebitCreditOutcome outcome)
        {
            foreach (var button in _optionButtons)
            {
                button.interactable = false;
            }

            if (_sequenceRoutine != null)
            {
                _host.StopCoroutine(_sequenceRoutine);
            }

            // Defensive — see StopAllBalanceAudio's own doc-comment. A
            // fresh outcome should never overlap the tail of a previous
            // one, though in normal play RevealOutcome only ever fires
            // once per round.
            StopAllBalanceAudio();

            _sequenceRoutine = _host.StartCoroutine(MachineSequenceRoutine(outcome));
        }

        private IEnumerator MachineSequenceRoutine(DebitCreditOutcome outcome)
        {
            if (outcome == DebitCreditOutcome.Timeout)
            {
                // No valid pair to react to — never synthesize a fake token
                // for a missing selection (redesign doc, section 11/16).
                // The beam never tilts; only the pivot glow dims.
                yield return FadePivotGlow(0.35f, 0.12f, 0.35f);
                yield return new WaitForSeconds(0.1f);
                yield return FadePivotGlow(0.12f, 0.35f, 0.2f);
                ShowRecap();
                yield break;
            }

            // Both accounts were actually chosen — materialize and insert
            // both tokens before any correctness reaction is possible.
            var debitLabel = _challenge.AccountOptions[_selectedDebitIndex];
            var creditLabel = _challenge.AccountOptions[_selectedCreditIndex];
            _debitTokenText.text = $"{debitLabel}\nDEBE";
            _creditTokenText.text = $"{creditLabel}\nHABER";

            // Both tokens travel in together — "sequence may overlap
            // slightly" (redesign doc, section 12) — and, just as
            // importantly, the whole travel + Processing + reaction +
            // recap sequence has to fit inside the real Feedback-phase
            // window (Lock + BalanceFeedbackDisplaySeconds) before
            // ClasicoGameHost hides this presenter for the next round,
            // which would cut the routine off mid-way.
            yield return InsertBothTokens();

            // C8.1f.4: the machine now visibly "thinks" before it commits
            // to a result — see ProcessingSequence's own doc-comment. This
            // runs identically regardless of outcome (no branch on
            // `outcome` anywhere in it) — the anti-cheat rule from the
            // original mechanic (nothing may hint at correctness before
            // the reveal) applies just as much to this new beat as to
            // everything before it.
            yield return ProcessingSequence();

            switch (outcome)
            {
                case DebitCreditOutcome.Correct:
                    yield return CorrectSequence();
                    break;
                case DebitCreditOutcome.Partial:
                    yield return PartialSequence();
                    break;
                default:
                    yield return IncorrectSequence();
                    break;
            }

            // C8.1f.5 brief section 13/15: the physical result cue (or a
            // long-tailed real supplied clip — see StopAllBalanceAudio's
            // own doc-comment) must never keep sounding underneath the
            // recap, the next microgame, or Results. This is the single
            // point every outcome path funnels through before the recap
            // ever appears, so it is also what makes the Results-screen
            // hard guarantee (brief section 15) hold even when Balance is
            // the session's very last microgame — this presenter never
            // waits for an external Hide() call to go quiet.
            StopAllBalanceAudio();
            ShowRecap();
        }

        /// <summary>C8.1f.4: token insertion rebuilt to actually read as
        /// "the two accounts I selected just entered the machine" (brief
        /// section 2) — the old version only faded the token in/scaled it
        /// up in place on the pan; this drops it in from above
        /// (<see cref="TokenTravelDistance"/> units) with a clean ease-out
        /// (no bounce/overshoot — brief section 11 explicitly bans elastic
        /// motion for the machine), materializing (alpha) over roughly the
        /// first 40% of that fall so it doesn't look like it teleported in
        /// already-solid, then a receiver reaction — a small punch on the
        /// pan itself — plus the TokenInsert cue right as it seats.</summary>
        private IEnumerator InsertBothTokens()
        {
            const float duration = TokenTravelSeconds;
            var elapsed = 0f;
            var seated = false;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - (1f - t) * (1f - t);
                SetTokenTravelProgress(_debitTokenRect, _debitTokenImage, _debitTokenText, eased);
                SetTokenTravelProgress(_creditTokenRect, _creditTokenImage, _creditTokenText, eased);
                if (!seated && t >= 0.92f)
                {
                    seated = true;
                    PlayPhaseAudio(_tokenInsertClip);
                    LocalMotionFx.Punch(_host, _leftPanPunchHandle, _leftPan, 0.18f, 1.06f);
                    LocalMotionFx.Punch(_host, _rightPanPunchHandle, _rightPan, 0.18f, 1.06f);
                }

                yield return null;
            }

            SetTokenTravelProgress(_debitTokenRect, _debitTokenImage, _debitTokenText, 1f);
            SetTokenTravelProgress(_creditTokenRect, _creditTokenImage, _creditTokenText, 1f);
        }

        private static void SetTokenTravelProgress(RectTransform rect, Image image, Text label, float eased)
        {
            // Materializes over the first ~40% of the fall (alpha), then
            // stays fully opaque while it finishes traveling the rest of
            // the way in — never a flat "fade in place".
            var alpha = Mathf.Clamp01(eased / 0.4f);
            image.color = new Color(image.color.r, image.color.g, image.color.b, alpha);
            label.color = new Color(label.color.r, label.color.g, label.color.b, alpha);
            rect.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, eased);
            rect.anchoredPosition = new Vector2(0, Mathf.Lerp(TokenRestY + TokenTravelDistance, TokenRestY, eased));
        }

        /// <summary>C8.1f.4 brief section 3: the machine "thinking" beat,
        /// inserted between both tokens seating and the outcome actually
        /// resolving. Deliberately outcome-agnostic (never reads
        /// <c>_selectedDebitIndex</c>/<c>_selectedCreditIndex</c>/the
        /// challenge's correct answers) — a symmetric wobble that could
        /// equally precede any of the three resolvable outcomes, so it
        /// never leaks a hint before the reveal. Rear-mechanism wake +
        /// gear-like ratcheting (the Processing audio cue), a light pivot
        /// vibration (a subtle pulse on the pivot glow, since a symmetric
        /// radial glow has no visible "rotation" to jitter — see
        /// <see cref="BuildMachine"/>'s own reasoning for why this is a
        /// glow, not a second illustrated hub), restrained analytical whole-
        /// machine micro-movements (a few degrees, far short of any
        /// outcome's real tilt), and the glow building from its dim resting
        /// state toward a neutral warm (never cyan/red — brief section 3's
        /// "no large result colors yet") brightness. C8.1f.5 brief section
        /// 13/14: explicitly stops the Processing cue at the end of this
        /// method (<see cref="StopAllBalanceAudio"/>), regardless of the
        /// clip's own real length — "the visual phase owns the audio
        /// duration" — so it can never bleed into the beam-oscillate/
        /// verdict phase that follows.</summary>
        private IEnumerator ProcessingSequence()
        {
            PlayPhaseAudio(_processingClip);

            var elapsed = 0f;
            while (elapsed < ProcessingSeconds)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / ProcessingSeconds);

                var microAngle = Mathf.Sin(elapsed * 14f) * 3f * (1f - t * 0.5f);
                SetBeamAngle(microAngle);

                var pulse = 1f + Mathf.Sin(elapsed * 40f) * 0.04f;
                _pivotGlowRect.localScale = Vector3.one * pulse;

                SetPivotGlowColor(Color.Lerp(Color.white, new Color(1f, 0.92f, 0.75f, 1f), t), Mathf.Lerp(0.35f, 0.75f, t));

                yield return null;
            }

            SetBeamAngle(0f);
            _pivotGlowRect.localScale = Vector3.one;

            // The visual phase owns the audio duration (brief section 14) —
            // never let a real supplied Processing clip (or any future
            // longer one) keep sounding into the beam-oscillate/verdict
            // phase that follows.
            StopAllBalanceAudio();
        }

        /// <summary>The machine visibly solving itself: a short damped
        /// oscillation settling to an exact 0-degree equilibrium, then the
        /// pivot lock's own hero beat — a small snap, a restrained cyan/gold
        /// illumination, the CorrectLock cue, and the central "EQUILIBRIO"
        /// verdict (brief sections 5/6/11-13). A cheap eased rotation, never
        /// a physics simulation — "clarity over simulación física",
        /// unchanged from the original implementation.</summary>
        private IEnumerator CorrectSequence()
        {
            const float oscillateDuration = 0.75f;
            var elapsed = 0f;
            while (elapsed < oscillateDuration)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / oscillateDuration;
                var damping = 1f - t;
                var angle = Mathf.Sin(t * Mathf.PI * 5f) * MaxTiltAngle * damping * damping;
                SetBeamAngle(angle);
                yield return null;
            }

            SetBeamAngle(0f);
            LocalMotionFx.Punch(_host, _beamPunchHandle, _beamPivotRoot, 0.24f, 1.06f);

            // No token tint here on purpose — a correct answer is
            // deliberately read through the machine reaching perfect
            // equilibrium plus the pivot glow's own restrained
            // illumination/CLACK below, not a colored flash over the
            // artwork itself (brief section 13: "avoid casino
            // celebration").
            LocalMotionFx.Punch(_host, _pivotGlowPunchHandle, _pivotGlowRect, 0.3f, 1.25f);
            SetPivotGlowColor(LockCyan, 1f);
            PlayPhaseAudio(_correctLockClip);
            ShowVerdict("EQUILIBRIO", LockCyan);

            // The verdict hold (brief section 14: "~0.5-0.8s hold") before
            // the caller shows the recap — the physical result must read
            // before it gets covered.
            yield return new WaitForSeconds(VerdictHoldSeconds);
        }

        /// <summary>Exactly one account correct. Never reaches level — the
        /// beam visibly starts toward center, then overshoots and holds off
        /// to the wrong side, so it reads as "close, but no" rather than a
        /// near-success (redesign doc, section 14: "do NOT make Partial
        /// look almost successful enough to imply partial scoring"). The
        /// pivot lock visibly TRIES and fails — a jitter, never a
        /// clack.</summary>
        private IEnumerator PartialSequence()
        {
            const float duration = 0.55f;
            var wrongIsCredit = _selectedCreditIndex != Array.IndexOf(_challenge.AccountOptions, _challenge.CorrectCreditAccount);
            var restAngle = wrongIsCredit ? -MaxTiltAngle * 0.7f : MaxTiltAngle * 0.7f;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                // Toward level first (t up to ~0.4), then pulled off to the
                // wrong side and held — never actually reaching 0 at rest.
                var angle = t < 0.4f
                    ? Mathf.Lerp(0f, restAngle * 0.3f, t / 0.4f)
                    : Mathf.Lerp(restAngle * 0.3f, restAngle, (t - 0.4f) / 0.6f);
                SetBeamAngle(angle);
                yield return null;
            }

            LocalMotionFx.Shake(_host, _beamShakeHandle, _beamPivotRoot, 0.3f, 4f);
            PlayPhaseAudio(_incorrectJamClip);

            // C8.1f.5: tints the TOKEN itself, not an (now invisible) pan —
            // see BuildPan's own doc-comment for why pans no longer carry
            // any visible art to tint.
            var wrongImage = wrongIsCredit ? _creditTokenImage : _debitTokenImage;
            var wrongHandle = wrongIsCredit ? _rightPanFlashHandle : _leftPanFlashHandle;
            var correctImage = wrongIsCredit ? _debitTokenImage : _creditTokenImage;
            var correctHandle = wrongIsCredit ? _leftPanFlashHandle : _rightPanFlashHandle;

            LocalMotionFx.FlashColor(_host, wrongHandle, wrongImage, Theme.Incorrect, Color.white, 0.4f);
            // Subtle positive read on the correct side — a light neutral
            // cyan tint, restrained on purpose and never the full Correct
            // lock illumination, so Partial can never be mistaken for a
            // near-success.
            var subtleCyan = Color.Lerp(Color.white, LockCyan, 0.35f);
            LocalMotionFx.FlashColor(_host, correctHandle, correctImage, subtleCyan, Color.white, 0.3f);

            SetPivotGlowColor(Theme.Incorrect, 0.5f);
            ShowVerdict("DESEQUILIBRIO", Theme.Incorrect);

            // Fire-and-forget, like the flashes above — the recap must not
            // wait for smoke to finish fading before it can appear (same
            // Feedback-phase budget concern as CorrectSequence).
            _host.StartCoroutine(PlaySmoke(2));
            yield return new WaitForSeconds(VerdictHoldSeconds);
        }

        /// <summary>Both accounts wrong — a stronger, sustained imbalance
        /// with a short jam/jitter, restrained smoke, never an explosion.
        /// The machine should feel temporarily JAMMED, not destroyed (brief
        /// section 15).</summary>
        private IEnumerator IncorrectSequence()
        {
            const float duration = 0.4f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var angle = Mathf.Lerp(0f, MaxTiltAngle, t);
                SetBeamAngle(angle);
                yield return null;
            }

            LocalMotionFx.Shake(_host, _beamShakeHandle, _beamPivotRoot, 0.4f, 7f);
            PlayPhaseAudio(_incorrectJamClip);
            SetPivotGlowColor(Theme.Incorrect, 0.6f);
            ShowVerdict("DESEQUILIBRIO", Theme.Incorrect);
            LocalMotionFx.FlashColor(_host, _leftPanFlashHandle, _debitTokenImage, Theme.Incorrect, Color.white, 0.4f);
            LocalMotionFx.FlashColor(_host, _rightPanFlashHandle, _creditTokenImage, Theme.Incorrect, Color.white, 0.4f);

            _host.StartCoroutine(PlaySmoke(4));
            yield return new WaitForSeconds(VerdictHoldSeconds);
        }

        private IEnumerator PlaySmoke(int puffCount)
        {
            const float duration = 0.5f;
            var count = Mathf.Min(puffCount, _smoke.Count);
            var directions = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                var angle = (i / (float)count) * Mathf.PI * 2f;
                directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Abs(Mathf.Sin(angle)) + 0.3f) * 34f;
                _smokeRects[i].anchoredPosition = new Vector2(0, -70);
                _smoke[i].color = new Color(_smoke[i].color.r, _smoke[i].color.g, _smoke[i].color.b, 0.32f);
                PlayOneShot(_smokePressureClip);
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                for (var i = 0; i < count; i++)
                {
                    _smokeRects[i].anchoredPosition = new Vector2(0, -70) + directions[i] * t;
                    var c = _smoke[i].color;
                    _smoke[i].color = new Color(c.r, c.g, c.b, 0.32f * (1f - t));
                }

                yield return null;
            }

            SetSmokeVisible(false);
        }

        private IEnumerator FadePivotGlow(float from, float to, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var a = Mathf.Lerp(from, to, elapsed / duration);
                _pivotGlowImage.color = new Color(1f, 1f, 1f, a);
                yield return null;
            }

            _pivotGlowImage.color = new Color(1f, 1f, 1f, to);
        }

        private void SetPivotGlowDim()
        {
            _pivotGlowImage.color = new Color(1f, 1f, 1f, 0.22f);
        }

        private void SetPivotGlowColor(Color color, float alpha)
        {
            _pivotGlowImage.color = new Color(color.r, color.g, color.b, alpha);
        }

        /// <summary>C8.1f.5: plays a new phase's own cue after first
        /// stopping whatever the previous phase left playing — see
        /// <see cref="StopAllBalanceAudio"/>'s own doc-comment for why this
        /// matters. Used for every discrete "a new beat just started"
        /// moment (token seat, Correct/Incorrect lock/jam) — never for
        /// <see cref="PlaySmoke"/>'s own layered puffs, which are
        /// deliberately simultaneous, not sequential.</summary>
        private void PlayPhaseAudio(AudioClip clip)
        {
            if (_sfxAudioSource == null)
            {
                return;
            }

            // C8.1f.5: uses the source's main clip slot + Play() rather than
            // PlayOneShot. Functionally this only ever plays one phase cue
            // at a time anyway (Stop() always precedes it), but Play()
            // additionally makes AudioSource.isPlaying accurately reflect
            // this cue's real state — PlayOneShot voices do NOT surface
            // through isPlaying, which would make this whole lifecycle
            // contract unobservable (by tests, and by any future in-game
            // code that might need to ask "is Balance audio still going").
            _sfxAudioSource.Stop();
            _sfxAudioSource.clip = clip;
            if (clip != null)
            {
                _sfxAudioSource.Play();
            }
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip != null && _sfxAudioSource != null)
            {
                _sfxAudioSource.PlayOneShot(clip);
            }
        }

        /// <summary>C8.1f.5 brief section 10-12: the single, centralized
        /// audio-stop point every lifecycle exit calls instead of
        /// scattering its own <c>Stop()</c> calls. Root cause this fixes:
        /// Balance's processing/result audio could keep playing well past
        /// the round ending — including into the Results screen when
        /// Balance was the session's last microgame — because the two real
        /// supplied clips wired up in C8.1f.4
        /// (<c>Balance_TokenInsert.mp3</c>, <c>Balance_CorrectLock.mp3</c>)
        /// turned out to be 7-8 seconds long, and every
        /// <c>_sfxAudioSource.PlayOneShot(...)</c> call simply let them run
        /// to completion regardless of the (much shorter) visual beat they
        /// belong to — nothing ever called <c>AudioSource.Stop()</c>, which
        /// is the only thing that cuts an in-flight PlayOneShot voice short
        /// (the same fact this project's own Western lifecycle fix already
        /// established). <see cref="_sfxAudioSource"/> is the single shared
        /// source every Balance cue has ever played on, so stopping it here
        /// is already complete — there is no second source, no loop flag,
        /// and no delayed/nested coroutine that could schedule further
        /// audio after this point (unlike Western, Balance's own sequence
        /// coroutine is the only thing that ever calls PlayOneShot, and it
        /// is already tracked/stoppable via <see cref="_sequenceRoutine"/>).
        /// Called from: <see cref="Hide"/> (abort/session-exit, since those
        /// route through <c>ClasicoGameHost.HideAllPresenters</c>),
        /// <see cref="ShowChallenge"/> (defensive reset/re-entry),
        /// <see cref="RevealOutcome"/> (defensive), the end of
        /// <see cref="ProcessingSequence"/> (Processing must never bleed
        /// into the verdict), and — the fix for the Results-screen hard
        /// guarantee — right before <see cref="ShowRecap"/> in
        /// <see cref="MachineSequenceRoutine"/>, so this presenter is
        /// already silent well before any external Hide() call could ever
        /// arrive, even when Balance is the last microgame of the whole
        /// session.</summary>
        private void StopAllBalanceAudio()
        {
            if (_sfxAudioSource != null)
            {
                _sfxAudioSource.Stop();
            }
        }

        private void ShowRecap()
        {
            _recapTransactionText.text = _challenge.TransactionText;
            _recapDebitText.text = $"CARGA:\n{_challenge.CorrectDebitAccount}";
            _recapCreditText.text = $"ACREDITA:\n{_challenge.CorrectCreditAccount}";
            _recapExplanationText.text = string.IsNullOrEmpty(_challenge.FeedbackExplanation) ? string.Empty : _challenge.FeedbackExplanation;

            SetRecapVisible(true);
        }

        private void SetRecapVisible(bool visible)
        {
            var alpha = visible ? 1f : 0f;
            // C8.1j: near-opaque (0.97, not 0.85) — brief section 7: "NOT
            // transparent enough for machine art to interfere." Both the
            // brass border and the dark fill fade together so the whole
            // premium panel appears/disappears as one coherent surface.
            var panelAlpha = visible ? 0.97f : 0f;
            _recapBorder.GetComponent<Image>().color = new Color(RecapBorderColor.r, RecapBorderColor.g, RecapBorderColor.b, panelAlpha);
            _recapPanel.GetComponent<Image>().color = new Color(RecapFillColor.r, RecapFillColor.g, RecapFillColor.b, panelAlpha);
            SetTextAlpha(_recapTransactionText, alpha);
            SetTextAlpha(_recapDebitText, alpha);
            SetTextAlpha(_recapCreditText, alpha);
            SetTmpAlpha(_recapExplanationText, alpha);
        }

        private static void SetTextAlpha(Text text, float alpha)
        {
            text.color = new Color(text.color.r, text.color.g, text.color.b, alpha);
        }

        private static void SetTmpAlpha(TMP_Text text, float alpha)
        {
            text.color = new Color(text.color.r, text.color.g, text.color.b, alpha);
        }

        private static void SetTokenVisible(RectTransform rect, Image image, Text label, bool visible)
        {
            rect.localScale = Vector3.one;
            // Reset to the rest position, not wherever InsertBothTokens'
            // travel animation last left it — a fresh round must never
            // start with a token already mid-flight from a previous one.
            rect.anchoredPosition = new Vector2(0, TokenRestY);
            image.color = new Color(image.color.r, image.color.g, image.color.b, visible ? 1f : 0f);
            label.color = new Color(label.color.r, label.color.g, label.color.b, visible ? 1f : 0f);
        }

        /// <summary>C8.1f.4: presents the central verdict — dynamic text
        /// only (brief section 5), never baked art. Called exclusively from
        /// an outcome's own sequence, after Processing and after the beam
        /// itself has already settled/jammed — never earlier.</summary>
        private void ShowVerdict(string text, Color color)
        {
            _verdictText.text = text;
            _verdictText.color = new Color(color.r, color.g, color.b, 1f);
            _verdictPanel.GetComponent<Image>().color = new Color(RecapFillColor.r, RecapFillColor.g, RecapFillColor.b, 0.92f);
            _verdictBorder.GetComponent<Image>().color = new Color(RecapBorderColor.r, RecapBorderColor.g, RecapBorderColor.b, 0.92f);
        }

        private void HideVerdict()
        {
            _verdictText.text = string.Empty;
            _verdictText.color = new Color(1f, 1f, 1f, 0f);
            _verdictPanel.GetComponent<Image>().color = new Color(RecapFillColor.r, RecapFillColor.g, RecapFillColor.b, 0f);
            _verdictBorder.GetComponent<Image>().color = new Color(RecapBorderColor.r, RecapBorderColor.g, RecapBorderColor.b, 0f);
        }

        private void SetSmokeVisible(bool visible)
        {
            var alpha = visible ? 0.32f : 0f;
            foreach (var image in _smoke)
            {
                image.color = new Color(image.color.r, image.color.g, image.color.b, alpha);
            }
        }

        public void Hide()
        {
            // C8.1f.5 brief section 11.A/16: see StopAllBalanceAudio's own
            // doc-comment — this is the guarantee for abort/SALIR and
            // interrupted mid-sequence exits (token insertion, Processing,
            // Correct lock, Incorrect jam all covered — this stops
            // whichever one was in flight).
            StopAllBalanceAudio();

            if (_sequenceRoutine != null)
            {
                _host.StopCoroutine(_sequenceRoutine);
                _sequenceRoutine = null;
            }

            _root.gameObject.SetActive(false);
        }
    }
}
