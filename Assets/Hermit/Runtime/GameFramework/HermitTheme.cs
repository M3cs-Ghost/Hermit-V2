using UnityEngine;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// C7's first reusable visual grammar — one ScriptableObject holding every
    /// color/size/timing token this UI layer uses, so Shell and Clásico speak
    /// the same visual language instead of each screen hardcoding its own
    /// literals (C5/C6's approach — the same dark blue-gray was copy-pasted
    /// across ClasicoHud/GameSelectorHud independently).
    ///
    /// Deliberately small: this is a first pass at a design system, not a
    /// finished one. No per-widget style variants, no theming API beyond
    /// "read a token" — RuntimeUIFactory reads these values directly.
    ///
    /// Loaded once via Resources.Load (see RuntimeUIFactory.EnsureTheme),
    /// same single-root-asset pattern as GameCatalog/EnvironmentConfig.
    /// </summary>
    [CreateAssetMenu(fileName = "HermitTheme", menuName = "Hermit/UI/Theme")]
    public sealed class HermitTheme : ScriptableObject
    {
        // Deliberate "arcade premium" dark palette — dark navy-black ground,
        // an indigo/violet accent (distinctive, not generic Bootstrap blue,
        // not a neon-cyberpunk cyan/magenta cliché), high-contrast vivid
        // green/red feedback. See Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md,
        // "Visual language" for the direction this was chosen against.
        [Header("Background / panel hierarchy")]
        [SerializeField] private Color _background = new Color(0.0431f, 0.0510f, 0.0784f, 1f); // #0B0D14
        [SerializeField] private Color _panel = new Color(0.0863f, 0.1020f, 0.1412f, 1f);       // #161A24
        [SerializeField] private Color _panelRaised = new Color(0.1216f, 0.1490f, 0.2039f, 1f); // #1F2634

        [Header("Accent")]
        [SerializeField] private Color _accent = new Color(0.4863f, 0.3608f, 1.0f, 1f);    // #7C5CFF
        [SerializeField] private Color _accentDim = new Color(0.2941f, 0.2275f, 0.6f, 1f); // #4B3A99

        // C9.1a: a warm ivory/pale-gold token for text presented directly
        // over illustrated world art (the Hub's destination markers) — the
        // cool violet _accent/_textPrimary tokens read as "UI chrome" against
        // warm architectural illustration; this one reads as "environmental
        // signage" instead. See Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md,
        // "C9.1a".
        [SerializeField] private Color _accentWarm = new Color(0.9569f, 0.8824f, 0.7255f, 1f); // #F4E1B9

        [Header("Text")]
        [SerializeField] private Color _textPrimary = new Color(0.9608f, 0.9647f, 0.9804f, 1f);   // #F5F6FA
        [SerializeField] private Color _textSecondary = new Color(0.6510f, 0.6745f, 0.7529f, 1f); // #A6ACC0

        [Header("Feedback")]
        [SerializeField] private Color _correct = new Color(0.2275f, 0.8588f, 0.4314f, 1f);   // #3ADB6E
        [SerializeField] private Color _incorrect = new Color(1.0f, 0.3529f, 0.3725f, 1f);    // #FF5A5F
        [SerializeField] private Color _warning = new Color(1.0f, 0.6902f, 0.1255f, 1f);      // #FFB020

        [Header("Typography (pt)")]
        [SerializeField] private int _titleSize = 52;
        [SerializeField] private int _subtitleSize = 24;
        [SerializeField] private int _headingSize = 32;
        [SerializeField] private int _bodySize = 22;
        [SerializeField] private int _buttonSize = 24;
        [SerializeField] private int _captionSize = 18;

        [Header("Spacing / shape")]
        [SerializeField] private float _spacingUnit = 12f;
        [SerializeField] private float _cornerRadius = 16f;

        [Header("Animation timings (s)")]
        [SerializeField] private float _punchDuration = 0.18f;
        [SerializeField] private float _shakeDuration = 0.28f;
        [SerializeField] private float _transitionDuration = 0.22f;
        [SerializeField] private float _countdownBeatDuration = 0.8f;

        public Color Background => _background;
        public Color Panel => _panel;
        public Color PanelRaised => _panelRaised;
        public Color Accent => _accent;
        public Color AccentDim => _accentDim;
        public Color AccentWarm => _accentWarm;
        public Color TextPrimary => _textPrimary;
        public Color TextSecondary => _textSecondary;
        public Color Correct => _correct;
        public Color Incorrect => _incorrect;
        public Color Warning => _warning;

        public int TitleSize => _titleSize;
        public int SubtitleSize => _subtitleSize;
        public int HeadingSize => _headingSize;
        public int BodySize => _bodySize;
        public int ButtonSize => _buttonSize;
        public int CaptionSize => _captionSize;

        public float SpacingUnit => _spacingUnit;
        public float CornerRadius => _cornerRadius;

        public float PunchDuration => _punchDuration;
        public float ShakeDuration => _shakeDuration;
        public float TransitionDuration => _transitionDuration;
        public float CountdownBeatDuration => _countdownBeatDuration;

        /// <summary>Hardcoded fallback used only if HermitTheme.asset fails to
        /// load — keeps the UI usable (if generic) rather than crashing.</summary>
        public static HermitTheme CreateFallback() => CreateInstance<HermitTheme>();
    }
}
