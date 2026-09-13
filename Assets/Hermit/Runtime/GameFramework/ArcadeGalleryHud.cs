using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hermit.Core;
using Hermit.Games;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// C9.2 — the Arcade destination's real screen: "these are the games
    /// inside Hermit," presented as a small game gallery (one large featured
    /// title + a couple of subdued future slots), never a settings-menu-
    /// style vertical button list. Replaces <see cref="GameSelectorHud"/>
    /// for the product's own visual presentation — GameSelectorHud itself
    /// is untouched and stays exactly what GameSessionInstaller's dev/test
    /// sandbox uses (see its own doc-comment for why that one keeps the
    /// plain list). Implements <see cref="IGameSelectorScreen"/> so
    /// <see cref="GameHub"/>'s existing launch/back orchestration works
    /// identically for both screens — no duplicate game-launch system. See
    /// Docs/C9_2_ARCADE_GAME_GALLERY.md for the full architecture writeup.
    /// </summary>
    public sealed class ArcadeGalleryHud : MonoBehaviour, IGameSelectorScreen
    {
        public event Action<GameDefinition> GameLaunchRequested;

        // C9.2: the "fast, not another cinematic" half of the short
        // Hub<->Arcade transition (see ShellInstaller.TransitionToArcadeRoutine
        // for the Hub-nav-fade half) — a quick reveal fade plus a dark
        // sweep that clears as the gallery settles, entirely within the
        // brief's ~0.35-0.60s target, never anything Western-cinematic-length.
        private const float RevealFadeDuration = 0.18f;
        private const float RevealSweepPeakAlpha = 0.85f;

        private const float FocusLerpRate = 10f;
        private const float FeaturedFocusScale = 1.03f;
        private const float FutureSlotFocusScale = 1.05f;

        private RectTransform _panel;
        private CanvasGroup _panelGroup;
        private Image _revealSweep;
        private Button _backButton;
        private Action _onBack;

        private RectTransform _featuredFrame;
        private Button _featuredButton;
        private GameDefinition _featuredDefinition;

        private readonly List<FutureSlot> _futureSlots = new List<FutureSlot>();

        private HermitTheme Theme => RuntimeUIFactory.Theme;

        private struct FutureSlot
        {
            public RectTransform Frame;
            public Button Button;
        }

        public void Build(Transform canvasRoot, IReadOnlyList<GameDefinition> games)
        {
            _panel = RuntimeUIFactory.CreatePanel(canvasRoot, "ArcadeGalleryPanel", Theme.Panel);
            _panelGroup = _panel.gameObject.AddComponent<CanvasGroup>();

            BuildAmbientBackground();

            RuntimeUIFactory.CreateText(
                _panel, "Title", "ARCADE", Theme.TitleSize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(600, 60));

            var enabledGames = games.Where(g => g.IsEnabled).ToList();
            var disabledGames = games.Where(g => !g.IsEnabled).ToList();

            _featuredDefinition = enabledGames.FirstOrDefault();
            if (_featuredDefinition != null)
            {
                BuildFeaturedCard(_featuredDefinition);
            }
            else
            {
                HermitLog.Warning("ArcadeGalleryHud has no enabled game to feature — the gallery will show only future slots.");
            }

            BuildFutureSlots(disabledGames);
            BuildFocusChain();

            _backButton = RuntimeUIFactory.CreateButton(
                _panel, "BackButton", "Volver",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(140, 70), new Vector2(180, 52));
            _backButton.onClick.AddListener(() => _onBack?.Invoke());
            _backButton.gameObject.SetActive(false);

            BuildRevealSweep();

            _panel.gameObject.SetActive(false);
        }

        /// <summary>Dark, restrained arcade ambience — not this project's
        /// flat <c>Theme.Background</c> panel, per the brief's own "not a
        /// bright flat panel" instruction. No dedicated Arcade environment
        /// art exists yet (see Docs/C9_2_ARCADE_GAME_GALLERY.md, "Asset
        /// inventory"), so this is a tasteful procedural stand-in: the
        /// theme's own slightly-lighter panel tone plus a soft radial
        /// vignette darkening the edges, keeping the center clear for the
        /// featured card — never claiming to be finished environment art.</summary>
        private void BuildAmbientBackground()
        {
            var vignetteRect = RuntimeUIFactory.CreatePanel(_panel, "AmbientVignette", Color.white);
            var vignette = vignetteRect.GetComponent<Image>();
            vignette.sprite = RuntimeUIFactory.GetVignetteSprite();
            vignette.type = Image.Type.Simple;
            vignette.color = new Color(Theme.Background.r, Theme.Background.g, Theme.Background.b, 0.85f);
            vignette.raycastTarget = false;
        }

        /// <summary>The hero slot — Clásico, the only complete playable game,
        /// presented as a framed "cabinet poster" (reusing
        /// <see cref="RuntimeUIFactory.CreatePortraitFrame"/>, the same
        /// technique this project already uses for character portraits)
        /// rather than a flat rectangle with text. See
        /// Docs/C9_2_ARCADE_GAME_GALLERY.md, "Art asset strategy" for why
        /// the poster art is a temporary, explicitly-labeled placeholder
        /// (an ArtBible Mascot candidate, not dedicated Clásico key art).
        /// The whole frame is the click/Submit target — the same
        /// "art/world IS the target" language this project already uses for
        /// Western's targets and the Hub's own hotspots — named
        /// "Game_{GameId}" so it launches through the exact same
        /// GameHub.OnGameLaunchRequested path (and the exact same test
        /// helper naming convention) GameSelectorHud's button always
        /// used.</summary>
        private void BuildFeaturedCard(GameDefinition definition)
        {
            const float posterHeight = 372f;
            const float posterWidth = 280f; // matches the poster art's own ~928:1232 aspect

            var poster = RuntimeUIFactory.LoadArt("Art/Shell/Arcade/Clasico_FeaturedCard_01");
            var (frame, portrait) = RuntimeUIFactory.CreatePortraitFrame(
                _panel, $"Game_{definition.GameId}", poster,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -15), new Vector2(posterWidth, posterHeight),
                Theme.PanelRaised);
            portrait.preserveAspect = true;
            _featuredFrame = frame;

            _featuredButton = frame.gameObject.AddComponent<Button>();
            _featuredButton.targetGraphic = frame.GetComponent<Image>();
            _featuredButton.onClick.AddListener(() =>
            {
                if (definition.IsEnabled)
                {
                    GameLaunchRequested?.Invoke(definition);
                }
            });

            RuntimeUIFactory.CreateWorldLabel(
                _panel, "FeaturedTitle", definition.DisplayName.ToUpperInvariant(), 30f, Theme.AccentWarm,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -218), new Vector2(420, 44));

            if (!string.IsNullOrEmpty(definition.ShortDescription))
            {
                RuntimeUIFactory.CreateText(
                    _panel, "FeaturedDescriptor", definition.ShortDescription, Theme.CaptionSize, TextAnchor.MiddleCenter, Theme.TextSecondary,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -252), new Vector2(520, 30));
            }

            RuntimeUIFactory.CreateWorldLabel(
                _panel, "FeaturedPlayCue", "ENTRAR", 18f, Theme.AccentWarm,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -286), new Vector2(160, 26));

            var markerRect = RuntimeUIFactory.CreatePanel(_panel, "FeaturedPlayCueMarker", Theme.AccentWarm);
            markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRect.pivot = new Vector2(0.5f, 1f);
            markerRect.anchoredPosition = new Vector2(0, -302);
            markerRect.sizeDelta = new Vector2(56, 2);
            markerRect.GetComponent<Image>().raycastTarget = false;
        }

        /// <summary>2-3 subdued, generic slots for games that don't exist
        /// yet — never fake game content, never a name that isn't already
        /// defined somewhere real. Real disabled <see cref="GameDefinition"/>
        /// entries (if the catalog ever gains one) take priority and show
        /// their own real display name; any remaining slots up to the
        /// target count are purely-generic, unlabeled-by-name "PRÓXIMAMENTE"
        /// placeholders with no backing data at all.</summary>
        private void BuildFutureSlots(IReadOnlyList<GameDefinition> disabledGames)
        {
            const int targetSlotCount = 2;
            const float slotWidth = 130f;
            const float slotHeight = 180f;
            var xPositions = new[] { -230f, 230f };

            for (var i = 0; i < targetSlotCount; i++)
            {
                var definition = i < disabledGames.Count ? disabledGames[i] : null;
                var label = definition != null ? definition.DisplayName : "PRÓXIMAMENTE";
                var slotName = definition != null ? $"FutureSlot_{definition.GameId}" : $"FutureSlot_{i}";

                var frame = RuntimeUIFactory.CreateRoundedPanel(_panel, slotName, new Color(Theme.PanelRaised.r, Theme.PanelRaised.g, Theme.PanelRaised.b, 0.55f));
                frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
                frame.anchoredPosition = new Vector2(xPositions[i], -15);
                frame.sizeDelta = new Vector2(slotWidth, slotHeight);

                var slotLabel = RuntimeUIFactory.CreateWorldLabel(
                    frame, "Label", label, 15f, new Color(Theme.TextSecondary.r, Theme.TextSecondary.g, Theme.TextSecondary.b, 0.8f),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 60), characterSpacing: 1f);
                slotLabel.textWrappingMode = TextWrappingModes.Normal;

                var button = frame.gameObject.AddComponent<Button>();
                button.targetGraphic = frame.GetComponent<Image>();
                button.onClick.AddListener(() => OnFutureSlotSelected(slotLabel));

                _futureSlots.Add(new FutureSlot { Frame = frame, Button = button });
            }
        }

        /// <summary>Submit on a future slot must never open fake content —
        /// this only briefly re-affirms the same "PRÓXIMAMENTE" cue the
        /// slot already shows, a small restrained pulse rather than any
        /// modal/popup.</summary>
        private void OnFutureSlotSelected(TMP_Text label)
        {
            StartCoroutine(FutureSlotPulseRoutine(label));
        }

        private static IEnumerator FutureSlotPulseRoutine(TMP_Text label)
        {
            const float duration = 0.3f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                label.transform.localScale = Vector3.one * (1f + 0.06f * t);
                yield return null;
            }

            label.transform.localScale = Vector3.one;
        }

        private void BuildFocusChain()
        {
            var horizontal = new List<Selectable>();
            if (_futureSlots.Count > 0)
            {
                horizontal.Add(_futureSlots[0].Button);
            }

            if (_featuredButton != null)
            {
                horizontal.Add(_featuredButton);
            }

            if (_futureSlots.Count > 1)
            {
                horizontal.Add(_futureSlots[1].Button);
            }

            RuntimeUIFactory.ChainHorizontal(horizontal.ToArray());
        }

        private void BuildRevealSweep()
        {
            var sweepRect = RuntimeUIFactory.CreatePanel(_panel, "RevealSweep", new Color(0f, 0f, 0f, 0f));
            _revealSweep = sweepRect.GetComponent<Image>();
            _revealSweep.raycastTarget = false;
        }

        public void SetBackAction(Action onBack)
        {
            _onBack = onBack;
            _backButton.gameObject.SetActive(onBack != null);

            if (onBack == null)
            {
                return;
            }

            var focusables = new List<Selectable>();
            if (_futureSlots.Count > 0)
            {
                focusables.Add(_futureSlots[0].Button);
            }

            if (_featuredButton != null)
            {
                focusables.Add(_featuredButton);
            }

            if (_futureSlots.Count > 1)
            {
                focusables.Add(_futureSlots[1].Button);
            }

            foreach (var selectable in focusables)
            {
                var nav = selectable.navigation;
                nav.selectOnDown = _backButton;
                selectable.navigation = nav;
            }

            var backNav = _backButton.navigation;
            backNav.mode = Navigation.Mode.Explicit;
            backNav.selectOnUp = _featuredButton != null ? _featuredButton : (focusables.Count > 0 ? focusables[0] : null);
            _backButton.navigation = backNav;
        }

        public void Show()
        {
            _panel.gameObject.SetActive(true);
            _panelGroup.alpha = 0f;
            _revealSweep.color = new Color(0f, 0f, 0f, RevealSweepPeakAlpha);

            StartCoroutine(RuntimeUIFactory.FadeCanvasGroup(_panelGroup, RevealFadeDuration, 0f, 1f));
            StartCoroutine(FadeSweepRoutine());

            RuntimeUIFactory.Select(_featuredButton != null ? _featuredButton : (_futureSlots.Count > 0 ? _futureSlots[0].Button : _backButton));
        }

        private IEnumerator FadeSweepRoutine()
        {
            var elapsed = 0f;
            while (elapsed < RevealFadeDuration)
            {
                elapsed += Time.deltaTime;
                var alpha = Mathf.Lerp(RevealSweepPeakAlpha, 0f, Mathf.Clamp01(elapsed / RevealFadeDuration));
                _revealSweep.color = new Color(0f, 0f, 0f, alpha);
                yield return null;
            }

            _revealSweep.color = new Color(0f, 0f, 0f, 0f);
        }

        public void Hide()
        {
            _panel.gameObject.SetActive(false);
        }

        /// <summary>Restrained focus emphasis (B8/B11 in the brief) — the
        /// featured card and each future slot ease toward a slightly larger
        /// scale when they hold keyboard/gamepad focus, smoothly, same
        /// no-pulsing/no-bounce contract StartScreenHud's own hotspot focus
        /// already established.</summary>
        private void Update()
        {
            if (!_panel.gameObject.activeInHierarchy)
            {
                return;
            }

            var selected = UnityEngine.EventSystems.EventSystem.current != null
                ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
                : null;

            var lerpAmount = Time.deltaTime * FocusLerpRate;

            if (_featuredFrame != null)
            {
                var target = selected == _featuredFrame.gameObject ? FeaturedFocusScale : 1f;
                _featuredFrame.localScale = Vector3.one * Mathf.Lerp(_featuredFrame.localScale.x, target, lerpAmount);
            }

            foreach (var slot in _futureSlots)
            {
                var target = selected == slot.Frame.gameObject ? FutureSlotFocusScale : 1f;
                slot.Frame.localScale = Vector3.one * Mathf.Lerp(slot.Frame.localScale.x, target, lerpAmount);
            }
        }
    }
}
