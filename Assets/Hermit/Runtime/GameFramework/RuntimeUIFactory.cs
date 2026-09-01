using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Hermit.Core;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// Builds plain uGUI elements entirely from code. Exists so the vertical
    /// slice needs zero manually-wired scene references or prefabs — the same
    /// "no scene wiring" approach C4's debug panel already used, just with real
    /// uGUI instead of IMGUI (gameplay UI must be uGUI).
    ///
    /// Not a general-purpose UI toolkit: it covers exactly the handful of
    /// widgets this project needs (full-screen canvas, text, button, panel,
    /// progress bar). A real UI pass later should use hand-authored prefabs,
    /// not this.
    ///
    /// C7 added <see cref="Theme"/>: every widget factory method now reads its
    /// default colors/sizes from one HermitTheme asset instead of the
    /// per-file hardcoded literals C5/C6 shipped with (the same dark blue-gray
    /// was independently copy-pasted into ClasicoHud and GameSelectorHud) —
    /// see Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md, "Theme/tokens".
    /// </summary>
    internal static class RuntimeUIFactory
    {
        private static Font _builtinFont;
        private static HermitTheme _theme;
        private static readonly Dictionary<int, Sprite> RoundedSpriteCache = new Dictionary<int, Sprite>();

        private static Font BuiltinFont => _builtinFont != null
            ? _builtinFont
            : _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>Loaded once (Resources.Load), cached for the process
        /// lifetime, with a hardcoded-default fallback instance if the asset
        /// is missing — a missing theme should degrade the look, not crash
        /// the app.</summary>
        public static HermitTheme Theme
        {
            get
            {
                if (_theme != null)
                {
                    return _theme;
                }

                _theme = Resources.Load<HermitTheme>("HermitTheme");
                if (_theme == null)
                {
                    HermitLog.Warning("HermitTheme not found under a Resources/ folder — using hardcoded fallback values.");
                    _theme = HermitTheme.CreateFallback();
                }

                return _theme;
            }
        }

        public static Canvas CreateCanvas(Transform root, string name)
        {
            var canvasGo = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(root, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(go);
        }

        public static RectTransform CreatePanel(Transform parent, string name, Color background)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            StretchFull(rect);

            var image = go.GetComponent<Image>();
            image.color = background;

            return rect;
        }

        /// <summary>A panel with theme corner-rounding — for cards/HUD chrome
        /// that should read as "game", not as a flat debug rectangle.</summary>
        public static RectTransform CreateRoundedPanel(Transform parent, string name, Color background)
        {
            var rect = CreatePanel(parent, name, background);
            var image = rect.GetComponent<Image>();
            image.sprite = GetRoundedSprite(Mathf.RoundToInt(Theme.CornerRadius));
            image.type = Image.Type.Sliced;
            return rect;
        }

        public static Text CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            TextAnchor alignment,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var text = go.GetComponent<Text>();
            text.font = BuiltinFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

        public static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var image = go.GetComponent<Image>();
            image.sprite = GetRoundedSprite(Mathf.RoundToInt(Theme.CornerRadius));
            image.type = Image.Type.Sliced;

            // Selectable's ColorTint transition *replaces* the graphic's color
            // with the state color (normalColor/highlightedColor/etc, times
            // colorMultiplier) — it does not multiply against whatever the
            // graphic's own color was set to. So every state below has to be
            // an explicit, complete color, not a tint layered on image.color.
            var button = go.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Theme.PanelRaised;
            colors.highlightedColor = AdjustBrightness(Theme.PanelRaised, 0.10f);
            colors.pressedColor = AdjustBrightness(Theme.PanelRaised, -0.08f);
            // ColorBlock's default selectedColor (~0.96,0.96,0.96) barely moves a
            // dark button's tint — a keyboard-selected button looked identical to
            // an unselected one. Tint clearly toward the theme's accent instead.
            colors.selectedColor = Theme.Accent;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;

            CreateText(
                rect,
                "Label",
                label,
                Theme.ButtonSize,
                TextAnchor.MiddleCenter,
                Theme.TextPrimary,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            return button;
        }

        /// <summary>A horizontally-filled bar (Image.Type.Filled) for a timer,
        /// streak meter, or any other 0..1 progress readout — reads its colors
        /// from the theme but takes an explicit fill color so callers (e.g. a
        /// timer that reddens under pressure) can drive it every frame.</summary>
        public static Image CreateFillBar(
            Transform parent,
            string name,
            Color fillColor,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var track = CreateRoundedPanel(parent, name, Theme.Panel);
            track.anchorMin = anchorMin;
            track.anchorMax = anchorMax;
            track.anchoredPosition = anchoredPosition;
            track.sizeDelta = sizeDelta;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.SetParent(track, false);
            StretchFull(fillRect);

            var fillImage = fillGo.GetComponent<Image>();
            fillImage.sprite = GetRoundedSprite(Mathf.RoundToInt(Theme.CornerRadius));
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;
            fillImage.color = fillColor;

            return fillImage;
        }

        private static Color AdjustBrightness(Color color, float delta)
        {
            return new Color(
                Mathf.Clamp01(color.r + delta),
                Mathf.Clamp01(color.g + delta),
                Mathf.Clamp01(color.b + delta),
                color.a);
        }

        public static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        /// <summary>Moves the EventSystem's selection explicitly. Shared by every
        /// Hud in this namespace — the C5 keyboard-navigation bug was exactly a
        /// missing call to this on every screen/question change (see
        /// Docs/C5_GAME_FRAMEWORK.md, "Keyboard navigation fix"), so every new
        /// screen (Shell and the selector included) reuses this one path rather
        /// than re-deriving the fix.</summary>
        public static void Select(Selectable selectable)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || selectable == null)
            {
                return;
            }

            eventSystem.SetSelectedGameObject(selectable.gameObject);
        }

        public static void ChainVertical(IReadOnlyList<Selectable> selectables)
        {
            for (var i = 0; i < selectables.Count; i++)
            {
                var nav = selectables[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = i > 0 ? selectables[i - 1] : null;
                nav.selectOnDown = i < selectables.Count - 1 ? selectables[i + 1] : null;
                selectables[i].navigation = nav;
            }
        }

        public static void ChainHorizontal(params Selectable[] selectables)
        {
            for (var i = 0; i < selectables.Length; i++)
            {
                var nav = selectables[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnLeft = i > 0 ? selectables[i - 1] : null;
                nav.selectOnRight = i < selectables.Length - 1 ? selectables[i + 1] : null;
                selectables[i].navigation = nav;
            }
        }

        /// <summary>Procedural rounded-rect sprite, generated once per radius
        /// and cached — deliberately not an external asset (no asset hunt),
        /// deliberately not a shader (would need a custom Shader Graph asset
        /// this project has no pipeline for yet). A small texture sliced with
        /// a border equal to the radius, so it stretches cleanly to any button/
        /// panel size via Image.Type.Sliced.</summary>
        private static Sprite GetRoundedSprite(int radius)
        {
            radius = Mathf.Max(2, radius);
            if (RoundedSpriteCache.TryGetValue(radius, out var cached) && cached != null)
            {
                return cached;
            }

            var size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = $"HermitRoundedRect_{radius}"
            };

            var half = size / 2f;
            var innerHalf = half - radius;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - innerHalf, 0f);
                    var dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - innerHalf, 0f);
                    var dist = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = texture.name;

            RoundedSpriteCache[radius] = sprite;
            return sprite;
        }
    }
}
