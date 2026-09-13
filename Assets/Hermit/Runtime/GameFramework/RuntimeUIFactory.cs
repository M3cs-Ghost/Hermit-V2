using System.Collections;
using System.Collections.Generic;
using TMPro;
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
        private static readonly Dictionary<string, Sprite> ArtCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, AudioClip> AudioCache = new Dictionary<string, AudioClip>();

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
            return CreateRoundedPanel(parent, name, background, Mathf.RoundToInt(Theme.CornerRadius));
        }

        /// <summary>Same as <see cref="CreateRoundedPanel(Transform,string,Color)"/>
        /// but with an explicit corner radius instead of the theme default —
        /// added in C8.1 so the four Gold microgame presenters can build
        /// circular "character" dressing (heads, badges, spotlights) by
        /// passing a radius equal to half the shape's size, without each
        /// presenter hand-rolling its own rounded-rect sprite generation.</summary>
        public static RectTransform CreateRoundedPanel(Transform parent, string name, Color background, int cornerRadius)
        {
            var rect = CreatePanel(parent, name, background);
            var image = rect.GetComponent<Image>();
            image.sprite = GetRoundedSprite(cornerRadius);
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

        /// <summary>Loaded once, cached for the process lifetime — the
        /// premium display serif (Marcellus, OFL-licensed; see
        /// Assets/Hermit/Content/Fonts/Marcellus/SOURCE.md) C9.1b introduced
        /// for text presented directly over illustrated world art, replacing
        /// C9.1a's synthetic-bold LiberationSans SDF (found, on manual
        /// validation, to still read as generic/insufficient no matter how
        /// much size/shadow/tracking was piled onto it — the font identity
        /// itself was the problem). Missing asset degrades to TMP's own
        /// default font (never a crash), same warn-once contract as
        /// <see cref="Theme"/>.</summary>
        private static TMP_FontAsset _displayFont;
        private static bool _displayFontMissingWarned;

        private static TMP_FontAsset DisplayFont
        {
            get
            {
                if (_displayFont != null)
                {
                    return _displayFont;
                }

                _displayFont = Resources.Load<TMP_FontAsset>("Fonts/Marcellus-Regular SDF");
                if (_displayFont == null && !_displayFontMissingWarned)
                {
                    HermitLog.Warning("Marcellus TMP font asset not found at Resources/Fonts/Marcellus-Regular SDF — environmental labels fall back to TMP's default font.");
                    _displayFontMissingWarned = true;
                }

                return _displayFont;
            }
        }

        /// <summary>C9.1a/C9.1b: TextMeshPro text styled for an environmental
        /// destination marker (or the Start Screen's PRESS START prompt)
        /// sitting directly over illustrated world art — Marcellus
        /// (<see cref="DisplayFont"/>), its own real Regular weight (never
        /// synthetic bold — C9.1b's brief was explicit: don't fake a weight
        /// when a real one exists), moderate tracking, and a minimal dark
        /// drop shadow for readability across the art's varying background
        /// luminance, never a thick outline or logo-like extrusion. Not a
        /// general-purpose TMP wrapper: every other screen in this project
        /// still uses the plain <see cref="Text"/> from <see cref="CreateText"/>
        /// — this exists specifically for text that has to compete with a
        /// busy illustrated background, which nothing before the Hub
        /// needed. See Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md,
        /// "C9.1b".</summary>
        public static TMP_Text CreateWorldLabel(
            Transform parent,
            string name,
            string content,
            float fontSize,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            FontStyles fontStyle = FontStyles.Normal,
            float characterSpacing = 4f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = content;
            label.fontSize = fontSize;
            label.color = color;
            label.fontStyle = fontStyle;
            label.characterSpacing = characterSpacing;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            if (DisplayFont != null)
            {
                label.font = DisplayFont;
            }

            // A small, restrained drop shadow — readability against a
            // photographic/illustrated background, never a logo-style
            // extrusion. Shadow (UnityEngine.UI) works on any Graphic,
            // TextMeshProUGUI included, by duplicating its mesh — no custom
            // TMP material/outline-shader property tuning needed.
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(1f, -1.5f);
            shadow.useGraphicAlpha = true;

            return label;
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

        /// <summary>C8.1d: loads an illustrated Gold-world art asset from a
        /// Resources/Art/Gold/... path, cached for the process lifetime.
        /// Returns null (and logs one warning per distinct missing path,
        /// never per-call) when the asset hasn't shipped yet — every caller
        /// must treat null as "fall back to the existing procedural
        /// presentation", never as an error, since the C8.1c Art Bible's
        /// candidate art lands one world/character at a time (see
        /// Docs/C8_1D_GOLD_ART_INTEGRATION.md, "Missing assets").</summary>
        public static Sprite LoadArt(string resourcePath)
        {
            if (ArtCache.TryGetValue(resourcePath, out var cached))
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                HermitLog.Warning($"Gold art asset not found at Resources/{resourcePath} — presenter falls back to procedural art.");
            }

            ArtCache[resourcePath] = sprite;
            return sprite;
        }

        /// <summary>C8.1d.6: same "load once, cache, warn-and-degrade rather
        /// than crash" contract as <see cref="LoadArt"/>, for a real
        /// (non-procedural) audio asset under Resources/Audio/Gold/... — used
        /// for the Western duel music candidate. A missing clip must degrade
        /// to "no music" (the cinematic's own sound design/gunshot cues are
        /// unaffected), never an exception.</summary>
        public static AudioClip LoadAudio(string resourcePath)
        {
            if (AudioCache.TryGetValue(resourcePath, out var cached))
            {
                return cached;
            }

            var clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null)
            {
                HermitLog.Warning($"Gold audio asset not found at Resources/{resourcePath} — presenter falls back to no music.");
            }

            AudioCache[resourcePath] = clip;
            return clip;
        }

        /// <summary>A full-bleed, non-interactive illustrated background —
        /// stretched to fill its parent exactly like <see cref="CreatePanel"/>,
        /// so it can drop straight into a presenter's existing "Sky"/"Ground"
        /// slot. Every Gold-world background candidate ships at (or very near)
        /// the stage's own 16:9 aspect, so a plain stretch — not
        /// letterboxing — is imperceptible; see Docs/C8_1D_GOLD_ART_INTEGRATION.md,
        /// "Sprite import rules".</summary>
        public static Image CreateBackgroundImage(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            StretchFull(rect);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;

            return image;
        }

        /// <summary>C8.1d: the standard presentation for every recurring-cast
        /// illustrated portrait (Sheriff, Auditor, Presentador). These source
        /// images are full-body character art on their own opaque studio
        /// gradient backdrop, not alpha-cut cutouts — see
        /// Docs/C8_1D_GOLD_ART_INTEGRATION.md, "Sprite import rules", for why
        /// silently faking transparency was rejected. A bordered rounded-rect
        /// frame (matching the existing panel/card language, section P of the
        /// C8.1c Art Bible) presents the portrait honestly as a character
        /// card inset — separate from world/environment art per the layering
        /// rule in section 12 of the C8.1d brief — rather than pretending the
        /// art is a world-embedded cutout. Returns the frame (for reactions —
        /// punch/shake the whole card) and the inner portrait Image (for
        /// sprite-swap reactions where an expression set exists).</summary>
        public static (RectTransform frame, Image portrait) CreatePortraitFrame(
            Transform parent,
            string name,
            Sprite sprite,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Color frameColor)
        {
            var frame = CreateRoundedPanel(parent, name, frameColor, 16);
            frame.anchorMin = anchorMin;
            frame.anchorMax = anchorMax;
            frame.anchoredPosition = anchoredPosition;
            frame.sizeDelta = sizeDelta;

            const float inset = 6f;
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            var portraitRect = (RectTransform)portraitGo.transform;
            portraitRect.SetParent(frame, false);
            portraitRect.anchorMin = Vector2.zero;
            portraitRect.anchorMax = Vector2.one;
            portraitRect.offsetMin = new Vector2(inset, inset);
            portraitRect.offsetMax = new Vector2(-inset, -inset);

            var portrait = portraitGo.GetComponent<Image>();
            portrait.sprite = sprite;
            portrait.type = Image.Type.Simple;
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;

            return (frame, portrait);
        }

        private static Color AdjustBrightness(Color color, float delta)
        {
            return new Color(
                Mathf.Clamp01(color.r + delta),
                Mathf.Clamp01(color.g + delta),
                Mathf.Clamp01(color.b + delta),
                color.a);
        }

        /// <summary>C9.2: a plain alpha-only fade for any <see cref="CanvasGroup"/>
        /// — the shared shape both StartScreenHud's Hub-nav fade and
        /// ArcadeGalleryHud's reveal fade use for the short (~0.2-0.35s)
        /// Hub&lt;-&gt;Arcade transition. A static factory returning an
        /// <see cref="IEnumerator"/> works from any MonoBehaviour's
        /// <c>StartCoroutine</c> regardless of which class actually calls
        /// it — no MonoBehaviour dependency of its own.</summary>
        public static IEnumerator FadeCanvasGroup(CanvasGroup group, float duration, float fromAlpha, float toAlpha)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = toAlpha;
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

        private static Sprite _vignetteSprite;

        /// <summary>C9.2: a soft radial vignette (transparent center, opaque
        /// edges) — generated once and cached, same "no asset hunt, no
        /// external texture" spirit as <see cref="GetRoundedSprite"/>. Used
        /// as ArcadeGalleryHud's restrained dark ambience: tinted with
        /// Theme.Background at the call site rather than baked in here, so
        /// the one generated texture works for any tint a future screen
        /// might need.</summary>
        public static Sprite GetVignetteSprite()
        {
            if (_vignetteSprite != null)
            {
                return _vignetteSprite;
            }

            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "HermitVignette"
            };

            var center = size / 2f;
            var maxDist = center * 1.05f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x + 0.5f - center;
                    var dy = y + 0.5f - center;
                    var dist = Mathf.Sqrt(dx * dx + dy * dy) / maxDist;
                    var alpha = Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, dist));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();

            _vignetteSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            _vignetteSprite.name = texture.name;
            return _vignetteSprite;
        }
    }
}
