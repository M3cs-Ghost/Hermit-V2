using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Hermit.Games.Clasico.Microgames;
using Hermit.Runtime.GameFramework;

namespace Hermit.Runtime.GameFramework.Microgames
{
    /// <summary>
    /// ErrorDetection, dressed as a detective lineup. The C8.0 Design Lock
    /// explicitly flagged this archetype's ≤1s clarity as unverified (unlike
    /// the other three, which are self-evident) — this presenter adds an
    /// explicit "¿Cuál no pertenece?" category cue and a spotlight framing
    /// device specifically to test whether that extra beat of framing is
    /// enough (see Docs/C8_1_GOLD_MICROGAME_SLICE.md, "Risks").
    ///
    /// C8.1b visual polish: interrogation-room dressing (a height-marked back
    /// wall, an ambient overhead glow), a detective/auditor character with a
    /// magnifying-glass prop watching from the side, each suspect upgraded to
    /// a <see cref="CharacterPrimitives"/> face, a badge pop on the correct
    /// suspect, a protest shake on a wrong accusation, and a brief
    /// "lights out" dim on a timeout.
    /// </summary>
    internal sealed class DetectiveLineupPresenter : IMicrogamePresenter
    {
        private const int MaxSuspects = 4;

        public event Action<int> SuspectAccused;

        private readonly MonoBehaviour _host;

        private RectTransform _root;
        private Text _groupLabelText;
        private Text _cueText;
        private RectTransform _spotlight;
        private CanvasGroup _lightsOutGroup;
        private readonly List<Button> _suspects = new List<Button>();
        private readonly List<Text> _suspectLabels = new List<Text>();
        private readonly List<Image> _suspectImages = new List<Image>();
        private readonly List<CharacterPrimitives.Face> _suspectFaces = new List<CharacterPrimitives.Face>();
        private readonly List<RectTransform> _suspectRects = new List<RectTransform>();
        private readonly List<MotionHandle> _protestHandles = new List<MotionHandle>();
        private RectTransform _badge;
        private readonly MotionHandle _badgeHandle = new MotionHandle();
        private readonly MotionHandle _lightsOutHandle = new MotionHandle();
        private RectTransform _auditorFrame;
        private readonly MotionHandle _auditorHandle = new MotionHandle();

        private ErrorDetectionChallenge _challenge;
        private HermitTheme Theme => RuntimeUIFactory.Theme;

        public DetectiveLineupPresenter(MonoBehaviour host)
        {
            _host = host;
        }

        public void Build(Transform stageRoot)
        {
            _root = RuntimeUIFactory.CreatePanel(stageRoot, "DetectiveLineup", new Color(0.1f, 0.11f, 0.14f, 1f));

            // Back wall with lineup height markers — an unmistakable
            // "interrogation room" read that doesn't depend on the text.
            var wall = RuntimeUIFactory.CreatePanel(_root, "Wall", new Color(0.15f, 0.16f, 0.19f, 1f));
            wall.anchorMin = new Vector2(0f, 0.42f);
            wall.anchorMax = new Vector2(1f, 0.86f);
            wall.offsetMin = Vector2.zero;
            wall.offsetMax = Vector2.zero;

            for (var i = 0; i < 5; i++)
            {
                var line = RuntimeUIFactory.CreatePanel(wall, "HeightLine", new Color(0.32f, 0.33f, 0.37f, 0.5f));
                line.anchorMin = new Vector2(0f, i * 0.22f);
                line.anchorMax = new Vector2(1f, i * 0.22f);
                line.offsetMin = new Vector2(0, -1f);
                line.offsetMax = new Vector2(0, 1f);
            }

            var glow = RuntimeUIFactory.CreateRoundedPanel(_root, "AmbientGlow", new Color(1f, 0.95f, 0.8f, 0.10f), 120);
            glow.anchorMin = glow.anchorMax = new Vector2(0.5f, 1f);
            glow.anchoredPosition = new Vector2(0, -20);
            glow.sizeDelta = new Vector2(900, 260);

            _cueText = RuntimeUIFactory.CreateText(
                _root, "Cue", "¿Cuál no pertenece?", Theme.CaptionSize, TextAnchor.MiddleCenter, Theme.Warning,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40), new Vector2(700, 32));

            _groupLabelText = RuntimeUIFactory.CreateText(
                _root, "GroupLabel", string.Empty, Theme.HeadingSize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(900, 60));

            // Auditor Severo watching from the side — the illustrated
            // portrait card (user-selected candidate #1, per
            // Docs/C8_1D_GOLD_ART_INTEGRATION.md) replaces the procedural
            // detective head/hat/magnifying-glass wherever his art has
            // landed, falling back to the original procedural dressing
            // otherwise. Pure environment presence, never interactive.
            var auditorArt = RuntimeUIFactory.LoadArt("Art/Gold/Detective/Auditor");
            if (auditorArt != null)
            {
                var (frame, _) = RuntimeUIFactory.CreatePortraitFrame(
                    _root, "AuditorCard", auditorArt,
                    new Vector2(0.08f, 0f), new Vector2(0.08f, 0f), new Vector2(0, 220), new Vector2(150, 260),
                    new Color(0.08f, 0.09f, 0.12f, 0.95f));
                _auditorFrame = frame;
            }
            else
            {
                var detectiveHead = RuntimeUIFactory.CreateRoundedPanel(_root, "DetectiveHead", new Color(0.78f, 0.63f, 0.5f, 1f), 26);
                detectiveHead.anchorMin = detectiveHead.anchorMax = new Vector2(0.08f, 0f);
                detectiveHead.anchoredPosition = new Vector2(0, 210);
                detectiveHead.sizeDelta = new Vector2(52, 52);
                CharacterPrimitives.Build(detectiveHead, 52f);

                var detectiveHat = RuntimeUIFactory.CreateRoundedPanel(detectiveHead, "Hat", new Color(0.22f, 0.2f, 0.24f, 1f), 6);
                detectiveHat.anchorMin = detectiveHat.anchorMax = new Vector2(0.5f, 1f);
                detectiveHat.anchoredPosition = new Vector2(0, 10);
                detectiveHat.sizeDelta = new Vector2(58, 14);

                var glassRing = RuntimeUIFactory.CreateRoundedPanel(_root, "GlassRing", new Color(0.75f, 0.8f, 0.85f, 0.9f), 14);
                glassRing.anchorMin = glassRing.anchorMax = new Vector2(0.08f, 0f);
                glassRing.anchoredPosition = new Vector2(38, 178);
                glassRing.sizeDelta = new Vector2(28, 28);
                var glassHole = RuntimeUIFactory.CreateRoundedPanel(glassRing, "GlassHole", new Color(0.1f, 0.11f, 0.14f, 1f), 10);
                glassHole.anchorMin = glassHole.anchorMax = new Vector2(0.5f, 0.5f);
                glassHole.anchoredPosition = Vector2.zero;
                glassHole.sizeDelta = new Vector2(20, 20);
                var glassHandle = RuntimeUIFactory.CreatePanel(_root, "GlassHandle", new Color(0.5f, 0.4f, 0.3f, 1f));
                glassHandle.anchorMin = glassHandle.anchorMax = new Vector2(0.08f, 0f);
                glassHandle.pivot = new Vector2(0f, 1f);
                glassHandle.anchoredPosition = new Vector2(50, 164);
                glassHandle.sizeDelta = new Vector2(6, 20);
                glassHandle.localRotation = Quaternion.Euler(0, 0, -35f);
            }

            const float cardWidth = 190f;
            const float spacing = 24f;
            var startX = -((MaxSuspects - 1) * (cardWidth + spacing)) / 2f;

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
                var x = startX + i * (cardWidth + spacing);

                var button = RuntimeUIFactory.CreateButton(
                    _root, $"DetectiveSuspect{i}", string.Empty,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 160), new Vector2(cardWidth, 220));
                button.onClick.AddListener(() => SuspectAccused?.Invoke(index));

                var head = RuntimeUIFactory.CreateRoundedPanel(button.transform, "Face", suspectPalette[i], 30);
                head.anchorMin = new Vector2(0.5f, 1f);
                head.anchorMax = new Vector2(0.5f, 1f);
                head.anchoredPosition = new Vector2(0, -40);
                head.sizeDelta = new Vector2(64, 64);
                var face = CharacterPrimitives.Build(head, 64f);

                var label = button.GetComponentInChildren<Text>();
                label.fontSize = Theme.CaptionSize;
                label.alignment = TextAnchor.LowerCenter;

                var rect = (RectTransform)button.transform;
                _suspects.Add(button);
                _suspectLabels.Add(label);
                _suspectImages.Add(button.GetComponent<Image>());
                _suspectFaces.Add(face);
                _suspectRects.Add(rect);
                _protestHandles.Add(new MotionHandle());
            }

            RuntimeUIFactory.ChainHorizontal(_suspects.ToArray());

            _spotlight = RuntimeUIFactory.CreateRoundedPanel(_root, "Spotlight", new Color(1f, 0.92f, 0.5f, 0.18f), 24);
            // Same fix as WesternShootoutPresenter's Reticle: CreateRoundedPanel
            // stretches to fill its parent, so sizeDelta must follow a point
            // anchor reset or it balloons to roughly the whole stage's size
            // instead of one card-sized spotlight.
            _spotlight.anchorMin = new Vector2(0.5f, 0.5f);
            _spotlight.anchorMax = new Vector2(0.5f, 0.5f);
            _spotlight.sizeDelta = new Vector2(cardWidth + 24f, 260);
            _spotlight.SetAsFirstSibling();
            _spotlight.gameObject.SetActive(false);

            _badge = RuntimeUIFactory.CreateRoundedPanel(_root, "Badge", Theme.Correct, 18);
            _badge.anchorMin = _badge.anchorMax = new Vector2(0.5f, 0.5f);
            _badge.sizeDelta = new Vector2(36, 36);
            RuntimeUIFactory.CreateText(
                _badge, "Star", "*", Theme.HeadingSize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _badge.gameObject.SetActive(false);

            var lightsOutGo = new GameObject("LightsOutOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var lightsOutRect = (RectTransform)lightsOutGo.transform;
            lightsOutRect.SetParent(_root, false);
            RuntimeUIFactory.StretchFull(lightsOutRect);
            lightsOutGo.GetComponent<Image>().color = Color.black;
            _lightsOutGroup = lightsOutGo.GetComponent<CanvasGroup>();
            _lightsOutGroup.alpha = 0f;
            _lightsOutGroup.blocksRaycasts = false;
            lightsOutRect.SetAsLastSibling();

            _root.gameObject.SetActive(false);
        }

        public void ShowChallenge(ErrorDetectionChallenge challenge)
        {
            _challenge = challenge;
            _root.gameObject.SetActive(true);
            _groupLabelText.text = challenge.GroupLabel;
            _spotlight.gameObject.SetActive(true);
            _badge.gameObject.SetActive(false);
            _lightsOutGroup.alpha = 0f;
            if (_auditorFrame != null)
            {
                _auditorFrame.localScale = Vector3.one;
            }

            Button first = null;
            for (var i = 0; i < _suspects.Count; i++)
            {
                var hasItem = i < challenge.Items.Length;
                _suspects[i].gameObject.SetActive(hasItem);
                if (!hasItem)
                {
                    continue;
                }

                _suspectLabels[i].text = challenge.Items[i];
                _suspectImages[i].color = Theme.PanelRaised;
                _suspects[i].interactable = true;
                CharacterPrimitives.ApplyPose(_suspectFaces[i], CharacterPrimitives.FacePose.Idle, 64f);
                first ??= _suspects[i];
            }

            RuntimeUIFactory.Select(first);
        }

        /// <summary>Spotlight follows the current selection — same cheap
        /// transform-follow trick as Western Shootout's reticle.</summary>
        public void RenderDecision()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null)
            {
                return;
            }

            var selectedRect = selected.GetComponent<RectTransform>();
            if (selectedRect == null || !_suspects.Exists(s => s.gameObject == selected))
            {
                return;
            }

            _spotlight.position = selectedRect.position;
        }

        /// <summary>-1 means the decision window ran out with no accusation
        /// — plays a brief "lights out" dim instead of any per-suspect
        /// reaction, since nobody was actually pointed at.</summary>
        public void RevealOutcome(int accusedIndex)
        {
            _spotlight.gameObject.SetActive(false);

            for (var i = 0; i < _suspects.Count; i++)
            {
                if (!_suspects[i].gameObject.activeSelf)
                {
                    continue;
                }

                _suspects[i].interactable = false;
                if (i == _challenge.AnomalyIndex)
                {
                    _suspectImages[i].color = Theme.Correct;
                    CharacterPrimitives.ApplyPose(_suspectFaces[i], CharacterPrimitives.FacePose.Correct, 64f);
                }
                else if (i == accusedIndex)
                {
                    _suspectImages[i].color = Theme.Incorrect;
                    CharacterPrimitives.ApplyPose(_suspectFaces[i], CharacterPrimitives.FacePose.Incorrect, 64f);
                    LocalMotionFx.Shake(_host, _protestHandles[i], _suspectRects[i], 0.3f, 8f);
                }
            }

            if (accusedIndex < 0)
            {
                LocalMotionFx.FadeAlpha(_host, _lightsOutHandle, _lightsOutGroup, 0f, 0.6f, 0.15f);
                _host.StartCoroutine(RestoreLightsRoutine());
                return;
            }

            if (accusedIndex == _challenge.AnomalyIndex)
            {
                _badge.gameObject.SetActive(true);
                _badge.position = _suspectRects[accusedIndex].position + new Vector3(0, 60f, 0);
                _badge.localScale = Vector3.one;
                LocalMotionFx.Punch(_host, _badgeHandle, _badge, 0.3f, 1.4f);

                if (_auditorFrame != null)
                {
                    // Restrained per the brief — Auditor Severo's "small
                    // satisfied smirk", not a big celebration.
                    LocalMotionFx.Punch(_host, _auditorHandle, _auditorFrame, 0.24f, 1.06f);
                }
            }
        }

        private System.Collections.IEnumerator RestoreLightsRoutine()
        {
            yield return new WaitForSeconds(0.25f);
            LocalMotionFx.FadeAlpha(_host, _lightsOutHandle, _lightsOutGroup, 0.6f, 0f, 0.35f);
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
        }
    }
}
