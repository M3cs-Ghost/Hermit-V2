using System;
using UnityEngine;
using UnityEngine.UI;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// Shell's Home screen — Hermit's first real product entry point (C7).
    /// Deliberately small: a temporary typographic title, a one-line tagline,
    /// one primary "Juegos" action, and a discrete session status line. No
    /// dashboard, no profile, no settings — explicitly out of scope this
    /// phase (see Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md, "Shell").
    /// </summary>
    internal sealed class ShellHomeHud : MonoBehaviour
    {
        public event Action PlayRequested;

        private RectTransform _panel;
        private Button _playButton;
        private Text _sessionStatusText;

        private HermitTheme Theme => RuntimeUIFactory.Theme;

        public void Build(Transform canvasRoot)
        {
            _panel = RuntimeUIFactory.CreatePanel(canvasRoot, "ShellHomePanel", Theme.Background);

            RuntimeUIFactory.CreateText(
                _panel, "Title", "HERMIT", Theme.TitleSize + 12, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(700, 90));

            RuntimeUIFactory.CreateText(
                _panel, "Tagline", "Reflejos de contador, ritmo de juego.", Theme.SubtitleSize, TextAnchor.MiddleCenter, Theme.TextSecondary,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(700, 40));

            _playButton = RuntimeUIFactory.CreateButton(
                _panel, "JuegosButton", "Juegos",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(260, 68));
            _playButton.onClick.AddListener(() => PlayRequested?.Invoke());

            _sessionStatusText = RuntimeUIFactory.CreateText(
                _panel, "SessionStatus", string.Empty, Theme.CaptionSize, TextAnchor.LowerRight, Theme.TextSecondary,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24, 20), new Vector2(360, 28));

            _panel.gameObject.SetActive(false);
        }

        /// <summary>Discrete, optional session status — never more than one
        /// short line. Called with null/empty to show nothing (e.g. before
        /// HermitRuntimeInstaller has finished, or if Networking never
        /// started at all in a build that skips it).</summary>
        public void SetSessionStatus(string text)
        {
            _sessionStatusText.text = text ?? string.Empty;
        }

        public void Show()
        {
            _panel.gameObject.SetActive(true);
            RuntimeUIFactory.Select(_playButton);
        }

        public void Hide()
        {
            _panel.gameObject.SetActive(false);
        }
    }
}
