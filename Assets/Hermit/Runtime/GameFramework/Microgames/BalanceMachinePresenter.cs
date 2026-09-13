using System;
using UnityEngine;
using UnityEngine.UI;
using Hermit.Games.Clasico.Microgames;
using Hermit.Runtime.GameFramework;

namespace Hermit.Runtime.GameFramework.Microgames
{
    /// <summary>
    /// Equation, dressed as a giant balance scale — the C8.0 Design Lock's
    /// riskiest Gold pick, since it is the one archetype that isn't "pick 1
    /// of N" underneath (see <see cref="Hermit.Games.Clasico.Microgames.BalanceMicrogameEngine"/>).
    /// Per the brief's own priority order ("claridad &gt; simulación física"),
    /// the beam tilt is a cheap normalized-angle rotation, not a physics
    /// simulation — clarity that the scale is unbalanced matters more than
    /// a physically accurate one.
    ///
    /// C8.1b visual polish: an industrial base/frame and pivot cap so the
    /// scale reads as a machine rather than a floating bar, a small level
    /// indicator on the pivot that mirrors the beam's tilt, a "lock/clunk"
    /// settle punch on correct, and a strain shake on incorrect — all via
    /// the shared <see cref="LocalMotionFx"/> toolkit.
    /// </summary>
    internal sealed class BalanceMachinePresenter : IMicrogamePresenter
    {
        private const float MaxTiltDelta = 200f;
        private const float MaxTiltAngle = 18f;

        public event Action<int> NudgeRequested;
        public event Action ConfirmRequested;

        private readonly MonoBehaviour _host;

        private RectTransform _root;
        private RectTransform _beam;
        private RectTransform _leftPan;
        private RectTransform _rightPan;
        private RectTransform _pivotIndicator;
        private Image _leftPanImage;
        private Image _rightPanImage;
        private Text _leftValueText;
        private Text _rightValueText;
        private Text _equationText;
        private Text _valueText;
        private Button _upButton;
        private Button _downButton;
        private Button _confirmButton;

        private readonly MotionHandle _beamPunchHandle = new MotionHandle();
        private readonly MotionHandle _beamShakeHandle = new MotionHandle();
        private readonly MotionHandle _leftPanFlashHandle = new MotionHandle();
        private readonly MotionHandle _rightPanFlashHandle = new MotionHandle();

        private EquationChallenge _challenge;
        private HermitTheme Theme => RuntimeUIFactory.Theme;

        public BalanceMachinePresenter(MonoBehaviour host)
        {
            _host = host;
        }

        public void Build(Transform stageRoot)
        {
            _root = RuntimeUIFactory.CreatePanel(stageRoot, "BalanceMachine", new Color(0.14f, 0.17f, 0.22f, 1f));

            // Industrial base — a wide plate under the post plus a couple of
            // rivet dots, so the machine reads as bolted to a floor rather
            // than floating.
            var basePlate = RuntimeUIFactory.CreateRoundedPanel(_root, "BasePlate", new Color(0.22f, 0.24f, 0.28f, 1f), 14);
            basePlate.anchorMin = basePlate.anchorMax = new Vector2(0.5f, 0.25f);
            basePlate.anchoredPosition = new Vector2(0, -8);
            basePlate.sizeDelta = new Vector2(220, 26);

            for (var i = -1; i <= 1; i += 2)
            {
                var rivet = RuntimeUIFactory.CreateRoundedPanel(basePlate, "Rivet", new Color(0.12f, 0.13f, 0.16f, 1f), 5);
                rivet.anchorMin = rivet.anchorMax = new Vector2(0.5f, 0.5f);
                rivet.anchoredPosition = new Vector2(i * 90, 0);
                rivet.sizeDelta = new Vector2(10, 10);
            }

            var post = RuntimeUIFactory.CreatePanel(_root, "Post", new Color(0.3f, 0.32f, 0.36f, 1f));
            post.anchorMin = new Vector2(0.5f, 0.25f);
            post.anchorMax = new Vector2(0.5f, 0.62f);
            post.anchoredPosition = Vector2.zero;
            post.sizeDelta = new Vector2(18, 0);

            var pivotCap = RuntimeUIFactory.CreateRoundedPanel(_root, "PivotCap", new Color(0.42f, 0.44f, 0.48f, 1f), 16);
            pivotCap.anchorMin = pivotCap.anchorMax = new Vector2(0.5f, 0.62f);
            pivotCap.anchoredPosition = Vector2.zero;
            pivotCap.sizeDelta = new Vector2(32, 32);

            _pivotIndicator = RuntimeUIFactory.CreateRoundedPanel(pivotCap, "LevelIndicator", Theme.Warning, 3);
            _pivotIndicator.anchorMin = _pivotIndicator.anchorMax = new Vector2(0.5f, 0.5f);
            _pivotIndicator.pivot = new Vector2(0.5f, 0f);
            _pivotIndicator.anchoredPosition = Vector2.zero;
            _pivotIndicator.sizeDelta = new Vector2(4, 22);

            _beam = RuntimeUIFactory.CreateRoundedPanel(_root, "Beam", new Color(0.55f, 0.58f, 0.62f, 1f), 8);
            _beam.anchorMin = new Vector2(0.5f, 0.62f);
            _beam.anchorMax = new Vector2(0.5f, 0.62f);
            _beam.pivot = new Vector2(0.5f, 0.5f);
            _beam.anchoredPosition = Vector2.zero;
            _beam.sizeDelta = new Vector2(560, 16);

            _leftPan = RuntimeUIFactory.CreateRoundedPanel(_beam, "LeftPan", Theme.PanelRaised, 20);
            _leftPan.anchorMin = new Vector2(0f, 0.5f);
            _leftPan.anchorMax = new Vector2(0f, 0.5f);
            _leftPan.anchoredPosition = new Vector2(0, -60);
            _leftPan.sizeDelta = new Vector2(160, 90);
            _leftPanImage = _leftPan.GetComponent<Image>();

            _rightPan = RuntimeUIFactory.CreateRoundedPanel(_beam, "RightPan", Theme.PanelRaised, 20);
            _rightPan.anchorMin = new Vector2(1f, 0.5f);
            _rightPan.anchorMax = new Vector2(1f, 0.5f);
            _rightPan.anchoredPosition = new Vector2(0, -60);
            _rightPan.sizeDelta = new Vector2(160, 90);
            _rightPanImage = _rightPan.GetComponent<Image>();

            RuntimeUIFactory.CreateText(
                _leftPan, "Label", "ACTIVO", Theme.CaptionSize, TextAnchor.MiddleCenter, Theme.TextSecondary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(150, 26));
            _leftValueText = RuntimeUIFactory.CreateText(
                _leftPan, "LeftValue", string.Empty, Theme.BodySize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 16), new Vector2(150, 32));

            RuntimeUIFactory.CreateText(
                _rightPan, "Label", "PASIVO + PATRIMONIO", Theme.CaptionSize, TextAnchor.MiddleCenter, Theme.TextSecondary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(150, 26));
            _rightValueText = RuntimeUIFactory.CreateText(
                _rightPan, "RightValue", string.Empty, Theme.BodySize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 16), new Vector2(150, 32));

            _equationText = RuntimeUIFactory.CreateText(
                _root, "Equation", string.Empty, Theme.HeadingSize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(900, 60));

            _valueText = RuntimeUIFactory.CreateText(
                _root, "BalanceCurrentValue", string.Empty, Theme.BodySize, TextAnchor.MiddleCenter, Theme.Warning,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 190), new Vector2(400, 40));

            _downButton = RuntimeUIFactory.CreateButton(
                _root, "BalanceDown", "▼",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-180, 100), new Vector2(120, 70));
            _downButton.onClick.AddListener(() => NudgeRequested?.Invoke(-1));

            _upButton = RuntimeUIFactory.CreateButton(
                _root, "BalanceUp", "▲",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 100), new Vector2(120, 70));
            _upButton.onClick.AddListener(() => NudgeRequested?.Invoke(1));

            _confirmButton = RuntimeUIFactory.CreateButton(
                _root, "BalanceConfirm", "CONFIRMAR",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(180, 100), new Vector2(160, 70));
            _confirmButton.onClick.AddListener(() => ConfirmRequested?.Invoke());

            RuntimeUIFactory.ChainHorizontal(_downButton, _upButton, _confirmButton);

            _root.gameObject.SetActive(false);
        }

        public void ShowChallenge(EquationChallenge challenge)
        {
            _challenge = challenge;
            _root.gameObject.SetActive(true);
            _equationText.text = $"{challenge.KnownLabelA} {challenge.KnownValueA:0} = {challenge.KnownLabelB} {challenge.KnownValueB:0} + {challenge.UnknownLabel} ?";
            _beam.localRotation = Quaternion.identity;
            _beam.localScale = Vector3.one;
            _leftPanImage.color = Theme.PanelRaised;
            _rightPanImage.color = Theme.PanelRaised;
            _upButton.interactable = true;
            _downButton.interactable = true;
            _confirmButton.interactable = true;

            RenderLiveValue(challenge.StartValue);
            RuntimeUIFactory.Select(_upButton);
        }

        /// <summary>Called every frame during the Decision phase with the
        /// engine's live adjustable value — a transform-rotation write and a
        /// couple of label writes, no rebuild.</summary>
        public void RenderLiveValue(float currentValue)
        {
            if (_challenge == null)
            {
                return;
            }

            _valueText.text = $"{_challenge.UnknownLabel}: {currentValue:0}";

            var rightTotal = _challenge.KnownValueB + currentValue;
            _leftValueText.text = _challenge.KnownValueA.ToString("0");
            _rightValueText.text = rightTotal.ToString("0");

            var delta = Mathf.Clamp(_challenge.KnownValueA - rightTotal, -MaxTiltDelta, MaxTiltDelta);
            var angle = -(delta / MaxTiltDelta) * MaxTiltAngle;
            _beam.localRotation = Quaternion.Euler(0f, 0f, angle);
            _pivotIndicator.localRotation = Quaternion.Euler(0f, 0f, angle * 0.5f);
        }

        public void RevealOutcome(bool correct, float finalValue)
        {
            _upButton.interactable = false;
            _downButton.interactable = false;
            _confirmButton.interactable = false;

            RenderLiveValue(correct ? _challenge.CorrectValue : finalValue);
            _valueText.color = correct ? Theme.Correct : Theme.Incorrect;

            if (correct)
            {
                // Lock/clunk: the beam settles level and gives one firm
                // punch, both pans flash the correct color.
                _beam.localRotation = Quaternion.identity;
                _pivotIndicator.localRotation = Quaternion.identity;
                LocalMotionFx.Punch(_host, _beamPunchHandle, _beam, 0.24f, 1.08f);
                LocalMotionFx.FlashColor(_host, _leftPanFlashHandle, _leftPanImage, Theme.Correct, Theme.PanelRaised, 0.4f);
                LocalMotionFx.FlashColor(_host, _rightPanFlashHandle, _rightPanImage, Theme.Correct, Theme.PanelRaised, 0.4f);
            }
            else
            {
                _equationText.text += $"   (correcto: {_challenge.CorrectValue:0})";
                LocalMotionFx.Shake(_host, _beamShakeHandle, _beam, 0.35f, 8f);
                var tippedPan = finalValue > _challenge.CorrectValue ? _rightPanImage : _leftPanImage;
                var tippedHandle = finalValue > _challenge.CorrectValue ? _rightPanFlashHandle : _leftPanFlashHandle;
                LocalMotionFx.FlashColor(_host, tippedHandle, tippedPan, Theme.Incorrect, Theme.PanelRaised, 0.4f);
            }
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
            _valueText.color = Theme.Warning;
        }
    }
}
