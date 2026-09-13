using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Hermit.Games.Clasico.Microgames;
using Hermit.Runtime.GameFramework;

namespace Hermit.Runtime.GameFramework.Microgames
{
    /// <summary>
    /// TrueFalse, dressed as a TV game show — a nervous contestant under a
    /// prize display, two big buzzers. The cheapest of the four Gold
    /// microgames to build (see Docs/C8_1_GOLD_MICROGAME_SLICE.md,
    /// "Production feasibility") and, deliberately, the one meant to prove
    /// the format can be understood in under a second with zero learning curve.
    ///
    /// C8.1b visual polish: stage lights and curtain bars for an
    /// unmistakable "TV studio" read, a presenter character (with a raised
    /// "arm" on correct), the contestant's face upgraded to
    /// <see cref="CharacterPrimitives"/>, a reusable (never Instantiate'd
    /// per round) confetti burst on correct, and a prize-board dim/shrink on
    /// incorrect. The comedy stays purely visual — no new dialogue text.
    /// </summary>
    internal sealed class GameShowPresenter : IMicrogamePresenter
    {
        private const int ConfettiCount = 8;

        public event Action<bool> AnswerChosen;

        private readonly MonoBehaviour _host;

        private RectTransform _root;
        private Text _statementText;
        private CharacterPrimitives.Face _contestantFace;
        private RectTransform _presenterArm;
        private RectTransform _presenterFrame;
        private RectTransform _prizeBoard;
        private Image _prizeImage;
        private Button _trueButton;
        private Button _falseButton;
        private Image _trueImage;
        private Image _falseImage;

        private readonly List<Image> _stageLights = new List<Image>();
        private readonly List<RectTransform> _confetti = new List<RectTransform>();
        private readonly MotionHandle _prizeHandle = new MotionHandle();
        private readonly MotionHandle _armHandle = new MotionHandle();

        private TrueFalseChallenge _challenge;
        private HermitTheme Theme => RuntimeUIFactory.Theme;

        public GameShowPresenter(MonoBehaviour host)
        {
            _host = host;
        }

        public void Build(Transform stageRoot)
        {
            _root = RuntimeUIFactory.CreatePanel(stageRoot, "GameShow", new Color(0.14f, 0.08f, 0.19f, 1f));

            // C8.1d: the illustrated stage (curtains, spotlights, prize
            // screen, audience silhouette) replaces the C8.1b curtain-bar/
            // floor/stage-light primitives wherever the Art Bible candidate
            // has landed — falls back to the original procedural studio
            // untouched otherwise (Docs/C8_1D_GOLD_ART_INTEGRATION.md).
            var background = RuntimeUIFactory.LoadArt("Art/Gold/GameShow/GameShowBackground");
            if (background != null)
            {
                RuntimeUIFactory.CreateBackgroundImage(_root, "Background", background);
            }
            else
            {
                BuildCurtain(_root, 0.06f);
                BuildCurtain(_root, 0.94f);

                var floor = RuntimeUIFactory.CreatePanel(_root, "Floor", new Color(0.10f, 0.06f, 0.14f, 1f));
                floor.anchorMin = Vector2.zero;
                floor.anchorMax = new Vector2(1f, 0.16f);
                floor.offsetMin = Vector2.zero;
                floor.offsetMax = Vector2.zero;

                for (var i = 0; i < 3; i++)
                {
                    var light = RuntimeUIFactory.CreateRoundedPanel(_root, "StageLight", new Color(1f, 0.92f, 0.7f, 0.5f), 20);
                    light.anchorMin = light.anchorMax = new Vector2(0.28f + i * 0.22f, 1f);
                    light.anchoredPosition = new Vector2(0, -18);
                    light.sizeDelta = new Vector2(40, 40);
                    _stageLights.Add(light.GetComponent<Image>());
                }
            }

            // "stage" is a layout anchor for the statement/presenter/
            // contestant/prize cluster regardless of which background is
            // active — painted only when falling back to the procedural
            // studio, left transparent over the illustrated background
            // (which already draws its own backdrop/screen).
            var stage = RuntimeUIFactory.CreateRoundedPanel(_root, "Backdrop", background != null ? new Color(0, 0, 0, 0) : new Color(0.28f, 0.16f, 0.36f, 1f));
            stage.anchorMin = new Vector2(0.5f, 0.55f);
            stage.anchorMax = new Vector2(0.5f, 0.55f);
            stage.anchoredPosition = Vector2.zero;
            stage.sizeDelta = new Vector2(760, 260);

            _statementText = RuntimeUIFactory.CreateText(
                stage, "Statement", string.Empty, Theme.BodySize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(660, 120));

            // Presentador Estelar — the illustrated portrait card replaces
            // the procedural body/head/arm silhouette wherever his art has
            // landed; the whole card takes the "arm raised" punch reaction
            // instead of an isolated arm rect (see RevealOutcome).
            var presentadorArt = RuntimeUIFactory.LoadArt("Art/Gold/GameShow/Presentador");
            if (presentadorArt != null)
            {
                var (frame, _) = RuntimeUIFactory.CreatePortraitFrame(
                    stage, "PresentadorCard", presentadorArt,
                    new Vector2(0.82f, 0f), new Vector2(0.82f, 0f), new Vector2(0, 130), new Vector2(150, 240),
                    new Color(0.12f, 0.05f, 0.16f, 0.95f));
                _presenterFrame = frame;
            }
            else
            {
                var presenterBody = RuntimeUIFactory.CreateRoundedPanel(stage, "PresenterBody", new Color(0.16f, 0.16f, 0.22f, 1f), 24);
                presenterBody.anchorMin = presenterBody.anchorMax = new Vector2(0.82f, 0f);
                presenterBody.anchoredPosition = new Vector2(0, 10);
                presenterBody.sizeDelta = new Vector2(64, 90);

                var presenterHead = RuntimeUIFactory.CreateRoundedPanel(stage, "PresenterHead", new Color(0.85f, 0.68f, 0.52f, 1f), 22);
                presenterHead.anchorMin = presenterHead.anchorMax = new Vector2(0.82f, 0f);
                presenterHead.anchoredPosition = new Vector2(0, 78);
                presenterHead.sizeDelta = new Vector2(46, 46);
                CharacterPrimitives.Build(presenterHead, 46f);

                _presenterArm = RuntimeUIFactory.CreatePanel(stage, "PresenterArm", new Color(0.85f, 0.68f, 0.52f, 1f));
                _presenterArm.anchorMin = _presenterArm.anchorMax = new Vector2(0.82f, 0f);
                _presenterArm.pivot = new Vector2(0f, 0.5f);
                _presenterArm.anchoredPosition = new Vector2(-30, 45);
                _presenterArm.sizeDelta = new Vector2(46, 12);
                _presenterArm.localRotation = Quaternion.Euler(0, 0, 18f);
            }

            var contestantHead = RuntimeUIFactory.CreateRoundedPanel(stage, "ContestantHead", new Color(0.92f, 0.78f, 0.6f, 1f), 44);
            contestantHead.anchorMin = new Vector2(0.18f, 0f);
            contestantHead.anchorMax = new Vector2(0.18f, 0f);
            contestantHead.anchoredPosition = new Vector2(0, 20);
            contestantHead.sizeDelta = new Vector2(88, 88);
            _contestantFace = CharacterPrimitives.Build(contestantHead, 88f);

            _prizeBoard = RuntimeUIFactory.CreateRoundedPanel(stage, "PrizeBoard", Theme.Accent);
            _prizeBoard.anchorMin = new Vector2(0.85f, 0f);
            _prizeBoard.anchorMax = new Vector2(0.85f, 0f);
            _prizeBoard.anchoredPosition = new Vector2(0, 25);
            _prizeBoard.sizeDelta = new Vector2(120, 70);
            _prizeImage = _prizeBoard.GetComponent<Image>();

            RuntimeUIFactory.CreateText(
                _prizeBoard, "Prize", "PREMIO", Theme.CaptionSize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            BuildConfetti(_root);

            _trueButton = RuntimeUIFactory.CreateButton(
                _root, "GameShowTrue", "VERDADERO",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-160, 90), new Vector2(280, 90));
            _trueButton.onClick.AddListener(() => AnswerChosen?.Invoke(true));

            _falseButton = RuntimeUIFactory.CreateButton(
                _root, "GameShowFalse", "FALSO",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(160, 90), new Vector2(280, 90));
            _falseButton.onClick.AddListener(() => AnswerChosen?.Invoke(false));

            _trueImage = _trueButton.GetComponent<Image>();
            _falseImage = _falseButton.GetComponent<Image>();
            RuntimeUIFactory.ChainHorizontal(_trueButton, _falseButton);

            _root.gameObject.SetActive(false);
        }

        private static void BuildCurtain(Transform parent, float xAnchor)
        {
            var curtain = RuntimeUIFactory.CreatePanel(parent, "Curtain", new Color(0.22f, 0.08f, 0.18f, 1f));
            curtain.anchorMin = new Vector2(xAnchor - 0.05f, 0f);
            curtain.anchorMax = new Vector2(xAnchor + 0.05f, 1f);
            curtain.offsetMin = Vector2.zero;
            curtain.offsetMax = Vector2.zero;
        }

        private void BuildConfetti(Transform parent)
        {
            var palette = new[] { Theme.Correct, Theme.Warning, Theme.Accent, new Color(1f, 0.5f, 0.7f, 1f) };
            for (var i = 0; i < ConfettiCount; i++)
            {
                var piece = RuntimeUIFactory.CreatePanel(parent, "Confetti", palette[i % palette.Length]);
                piece.anchorMin = piece.anchorMax = new Vector2(0.5f, 0.55f);
                piece.sizeDelta = new Vector2(10, 16);
                piece.GetComponent<Image>().color = new Color(palette[i % palette.Length].r, palette[i % palette.Length].g, palette[i % palette.Length].b, 0f);
                _confetti.Add(piece);
            }
        }

        public void ShowChallenge(TrueFalseChallenge challenge)
        {
            _challenge = challenge;
            _root.gameObject.SetActive(true);
            _statementText.text = challenge.Statement;
            CharacterPrimitives.ApplyPose(_contestantFace, CharacterPrimitives.FacePose.Idle, 88f);
            _prizeImage.color = Theme.Accent;
            _prizeBoard.localScale = Vector3.one;
            if (_presenterFrame != null)
            {
                _presenterFrame.localScale = Vector3.one;
            }
            _trueImage.color = Theme.PanelRaised;
            _falseImage.color = Theme.PanelRaised;
            _trueButton.interactable = true;
            _falseButton.interactable = true;

            RuntimeUIFactory.Select(_trueButton);
        }

        /// <summary>-1 means the decision window ran out with nothing
        /// chosen — always incorrect, same convention as the other two
        /// selection-based presenters.</summary>
        public void RevealOutcome(int selectedIndex)
        {
            _trueButton.interactable = false;
            _falseButton.interactable = false;

            var correctIndex = _challenge.IsTrue ? 0 : 1;
            var correctImage = correctIndex == 0 ? _trueImage : _falseImage;
            correctImage.color = Theme.Correct;

            var correct = selectedIndex == correctIndex;
            if (!correct && selectedIndex >= 0)
            {
                var wrongImage = selectedIndex == 0 ? _trueImage : _falseImage;
                wrongImage.color = Theme.Incorrect;
            }

            CharacterPrimitives.ApplyPose(_contestantFace, correct ? CharacterPrimitives.FacePose.Correct : CharacterPrimitives.FacePose.Incorrect, 88f);

            if (correct)
            {
                LocalMotionFx.FlashColor(_host, _prizeHandle, _prizeImage, new Color(1f, 0.95f, 0.6f, 1f), Theme.Correct, 0.4f);
                LocalMotionFx.Punch(_host, _armHandle, _presenterFrame != null ? _presenterFrame : _presenterArm, 0.3f, _presenterFrame != null ? 1.08f : 1.3f);
                _host.StartCoroutine(ConfettiBurstRoutine());
            }
            else
            {
                LocalMotionFx.FlashColor(_host, _prizeHandle, _prizeImage, Theme.Incorrect, new Color(0.3f, 0.12f, 0.14f, 1f), 0.35f);
                _prizeBoard.localScale = Vector3.one * 0.9f;
            }
        }

        private IEnumerator ConfettiBurstRoutine()
        {
            const float duration = 0.5f;
            var directions = new Vector2[ConfettiCount];
            for (var i = 0; i < ConfettiCount; i++)
            {
                var angle = (i / (float)ConfettiCount) * Mathf.PI * 2f;
                directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f + 0.4f) * 130f;
                _confetti[i].anchoredPosition = Vector2.zero;
                _confetti[i].localRotation = Quaternion.identity;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                for (var i = 0; i < ConfettiCount; i++)
                {
                    _confetti[i].anchoredPosition = directions[i] * t;
                    _confetti[i].localRotation = Quaternion.Euler(0, 0, t * 260f * (i % 2 == 0 ? 1 : -1));
                    var img = _confetti[i].GetComponent<Image>();
                    var c = img.color;
                    img.color = new Color(c.r, c.g, c.b, 1f - t);
                }

                yield return null;
            }

            for (var i = 0; i < ConfettiCount; i++)
            {
                var img = _confetti[i].GetComponent<Image>();
                var c = img.color;
                img.color = new Color(c.r, c.g, c.b, 0f);
            }
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
        }
    }
}
