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
    /// Classification, dressed as a Wild West shootout — the C8.0 Design
    /// Lock's own worked example (Cuentas por cobrar -&gt; Activo -&gt;
    /// Classification -&gt; AimSelect -&gt; Western Shootout). Knows nothing
    /// about accounting beyond the strings a <see cref="ClassificationChallenge"/>
    /// hands it: a concept label, a set of category labels, and which one is
    /// correct. Mouse: click an outlaw directly. Keyboard: arrows move the
    /// selection, Enter fires — the reticle is a pure visual that snaps to
    /// whichever target is currently selected, so both input paths look and
    /// feel identical.
    ///
    /// C8.1d correction: manual validation rejected the earlier portrait-card
    /// Sheriff (a framed illustration floating over the scene) — Sheriff
    /// Implacable is an in-world actor that stands lower-left, aims toward
    /// whichever outlaw is selected every frame during Decision, and fires
    /// (muzzle flash from its own MuzzleAnchor, a recoil punch) at Reveal.
    ///
    /// C8.1d.1 correction: manual validation then rejected the outlaws
    /// sitting inside dark rectangular target cards, and found them too
    /// small/pixelated — see Docs/C8_1D_GOLD_ART_INTEGRATION.md, "Target
    /// Visual Correction + Pixelation Investigation". Every
    /// <c>WesternTarget</c> button's own background is now fully transparent
    /// (input/keyboard-nav/accessibility unchanged — only its Image.color is
    /// invisible); the outlaw sprite itself is the target, sized
    /// substantially larger, and reveal feedback moved from a button-panel
    /// color flash to a subtle tint on the sprite itself (art path) or the
    /// existing face-pose change (procedural fallback path).
    ///
    /// C8.1d.1 also introduces the Western Encounter: one face-off intro
    /// (background, a short procedural sting, a tumbleweed crossing) plays
    /// once before round 1, then <see cref="ShowChallenge"/> is called again
    /// for rounds 2/3 with <c>isEncounterStart: false</c> — a quick in-place
    /// reset (outlaws back to Neutral, new labels), never a world rebuild —
    /// see <see cref="Hermit.Games.Clasico.ClasicoSessionDirector"/> and
    /// <see cref="ClasicoGameHost"/> for where the encounter boundary is
    /// decided; this presenter only ever reacts to the bool it's handed.
    ///
    /// C8.1d.2 correction: the intro flourish above used to launch as
    /// fire-and-forget while the round was already fully revealed on the
    /// same frame, so it played invisibly — <see cref="ShowChallenge"/> now
    /// stages round 1 hidden and only <see cref="RevealAfterIntro"/> (host-
    /// triggered, at the director's real Intro-&gt;Decision boundary) turns
    /// it live.
    ///
    /// C8.1d.3 redesign: manual validation still found no real presentation
    /// — Sheriff read as a static, undersized actor competing with the
    /// outlaws for the same small stage. Sheriff was made intro-ONLY: a
    /// large protagonist entrance/beat/exit that hid again before gameplay
    /// ever began; the outlaws are gameplay's entire visual field, and the
    /// shot is fired from a fixed off-screen origin
    /// (<see cref="FireOffscreenShot"/>) rather than from an on-screen
    /// Sheriff — see Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.3".
    ///
    /// C8.1d.4 replaces that in-world Sheriff intro entirely with a short
    /// cinematic 2D montage (<see cref="CinematicIntroRoutine"/>): hard-cut
    /// close-ups (Sheriff, Outlaw, Sheriff's hand/holster) instead of a
    /// large procedural actor walking on/off the stage. The SheriffActor
    /// build/pose machinery from C8.1d.3 is gone — fully superseded, not
    /// dead code kept around — see Docs/C8_1D_GOLD_ART_INTEGRATION.md,
    /// "C8.1d.4". The off-screen gameplay shot (<see cref="FireOffscreenShot"/>)
    /// is unrelated and unchanged: the cinematic's own gunshot is pure
    /// punctuation and never touches scoring/hit-state/answer-selection.
    ///
    /// C8.1d.5 re-paces that same montage from ~4.11s to ~6.5s per manual
    /// validation ("visually successful but too fast, want more suspense
    /// before the gunshot") — same shots, same hard-cut grammar, no new art,
    /// no architecture change; see <see cref="CinematicIntroRoutine"/>'s own
    /// doc-comment for the new timeline and
    /// Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.5" for the outlaw
    /// render-quality investigation run alongside it.
    ///
    /// C8.1d.6 replaces the C8.1d.4/.5 procedural twang/tension-note/
    /// low-pulse cues (which were standing in for *music*, not sound
    /// design) with a real, manually-selected duel music track ("Dust &amp;
    /// Silence") on its own dedicated AudioSource
    /// (<see cref="_musicAudioSource"/>) — independently volume-faded
    /// through the tension-silence window and force-stopped at the gunshot,
    /// never sharing a source with sound-design one-shots
    /// (<see cref="_sfxAudioSource"/>: wind, leather creak, pre-draw tick,
    /// gunshot, dust accent), which are all unchanged. No shot timing, no
    /// push-in, no Encounter/architecture change — see
    /// Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.6".
    ///
    /// C8.1d.7 is a gameplay polish pass, not a cinematic one: (1) the
    /// generic "¡DISPARA!" HUD command banner — which used to stay onscreen
    /// for Western's entire Intro phase, i.e. the whole cinematic — is
    /// replaced for AimSelect specifically by this presenter's own small
    /// "DISPARA" cue (<see cref="ShowDisparaCue"/>), shown only at the
    /// actual moment of gameplay reveal (round 1 and every continuation
    /// round alike) and faded on the player's first shot
    /// (<see cref="HideDisparaCueOnFire"/>); (2) a real gameplay firearm
    /// cue (<see cref="_gameplayGunshotClip"/>,
    /// <see cref="ProceduralAudio.GameplayGunshot"/>) now fires on every
    /// shot in <see cref="FireSequenceRoutine"/> — deliberately distinct
    /// from the cinematic's own <see cref="_gunshotClip"/>, on
    /// <see cref="_sfxAudioSource"/> (never the music source), for both
    /// correct and incorrect selections. No cinematic timing, no music, no
    /// outlaw art, no Encounter architecture touched — see
    /// Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.7".
    ///
    /// C8.1d.9 removes Western's own generic correct/incorrect HUD ding
    /// entirely (via <c>ClasicoHud.RenderFeedback</c>'s new
    /// <c>playAudio</c> opt-out, called from <c>ClasicoGameHost</c> —
    /// C8.1d.8's deferred-timing workaround for that same ding is gone too,
    /// no longer needed once the ding itself is gone) and replaces the
    /// incorrect-answer beat with a countershot
    /// (<see cref="CountershotRoutine"/>): the correct outlaw fires back
    /// (<see cref="_enemyGunshotClip"/>, distinct from both the player's own
    /// <see cref="_gameplayGunshotClip"/> and the cinematic's
    /// <see cref="_gunshotClip"/>) and a brief red "you got hit" flash
    /// (<see cref="PlayerHitFlashRoutine"/>) plays — on a wrong answer *or*
    /// a timeout, never on a correct answer. Every visual correctness cue
    /// (outlaw tint, Hit sprite, shake) is unchanged; no new aiming/
    /// projectile system, no cinematic/music/art/scoring change — see
    /// Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.9".
    /// </summary>
    internal sealed class WesternShootoutPresenter : IMicrogamePresenter
    {
        private const int MaxTargets = 4;

        // How strongly the correct/incorrect reveal tints an outlaw's own
        // sprite (0 = no tint, 1 = fully the state color) — kept well under
        // 1 so the illustration's own shading stays legible, per the C8.1d.1
        // brief's "subtle character-local highlight", never a rectangular
        // color-panel reveal.
        private const float RevealTintStrength = 0.55f;

        private static readonly char[] OutlawLetters = { 'A', 'B', 'C', 'D' };

        public event Action<int> TargetSelected;

        private readonly MonoBehaviour _host;

        private RectTransform _root;
        private Text _conceptText;
        private RectTransform _reticle;
        private Image _muzzleFlash;
        private readonly MotionHandle _muzzleFlashHandle = new MotionHandle();

        // C8.1d.7 — the refined "DISPARA" action cue: a small, presenter-
        // owned cue shown only at gameplay reveal (round 1's RevealAfterIntro
        // and every continuation round's own immediate reveal), replacing
        // the generic ClasicoHud CommandText banner for Western specifically
        // — that banner used to be shown for the *entire* Intro phase, which
        // for Western now means the whole ~7.5s cinematic, exactly the
        // "competes with the cinematic" complaint this phase fixes. See
        // "ShowDisparaCue"/"HideDisparaCue" and
        // Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.7".
        private Text _disparaText;
        private Coroutine _disparaRoutine;

        // C8.1d.3: the off-screen shooter rig — the shot's visible origin
        // once Sheriff himself is no longer part of the gameplay
        // composition (see "Western Encounter Presentation" /
        // "C8.1d.3" in Docs/C8_1D_GOLD_ART_INTEGRATION.md). Authored on the
        // left side of the stage per the brief's "OffscreenShotOrigin".
        private RectTransform _offscreenShotOrigin;
        private RectTransform _tracer;
        private readonly MotionHandle _screenKickHandle = new MotionHandle();

        private readonly List<Button> _targets = new List<Button>();
        private readonly List<Text> _targetLabels = new List<Text>();
        private readonly List<CharacterPrimitives.Face?> _targetFaces = new List<CharacterPrimitives.Face?>();
        private readonly List<Image> _dustPuffs = new List<Image>();
        private readonly List<MotionHandle> _targetPunchHandles = new List<MotionHandle>();
        private readonly List<MotionHandle> _targetShakeHandles = new List<MotionHandle>();
        private readonly List<MotionHandle> _dustHandles = new List<MotionHandle>();

        // One outlaw "actor" per target slot — a real Neutral/Hit sprite pair
        // when both exist under Resources/Art/Gold/Western/Actors/, else the
        // original procedural head/face/bandana/hat placeholder. Decided
        // independently per slot so a single missing file only degrades that
        // one outlaw, never the whole world.
        private readonly List<Image> _outlawSpriteImages = new List<Image>();
        private readonly List<Sprite> _outlawNeutralArt = new List<Sprite>();
        private readonly List<Sprite> _outlawHitArt = new List<Sprite>();
        private readonly List<bool> _outlawUsesArt = new List<bool>();

        // Encounter-intro-only flourish (C8.1d.1, still used) — a procedural
        // tumbleweed crossing, now confined to the cinematic's own brief
        // establishing-shot window (C8.1d.4) instead of running its full
        // original length across the whole old intro.
        private RectTransform _tumbleweed;

        // C8.1d.6 — split into two independent AudioSources (was one shared
        // "_audioSource") specifically so duel music can be volume-faded/
        // stopped without ever touching sound-design one-shots (wind, creak,
        // pre-draw tick, gunshot, dust accent) sharing the GameObject —
        // Unity's AudioSource.Stop() also cuts any of that source's own
        // in-flight PlayOneShot voices, so music and SFX must live on
        // different sources for "gunshot unaffected by music fade" to hold.
        private AudioSource _sfxAudioSource;
        private AudioSource _musicAudioSource;

        // C8.1d.4 — the cinematic duel intro rig: three real close-up
        // stills (Sheriff, Outlaw, Sheriff's hand/holster) shown one at a
        // time in one Image, cut together with hard edits — see
        // "CinematicIntroRoutine" and Docs/C8_1D_GOLD_ART_INTEGRATION.md,
        // "C8.1d.4". Replaces C8.1d.3's large in-world SheriffActor
        // entirely — that machinery is gone, not left unused.
        private RectTransform _cinematicRoot;
        private Image _cinematicImage;
        private Image _cinematicFlash;
        private Sprite _cinematicSheriffCloseup;
        private Sprite _cinematicOutlawCloseup;
        private Sprite _cinematicHandCloseup;
        private readonly MotionHandle _cinematicPunchHandle = new MotionHandle();

        // C8.1d.4/.5 sound-design cues — kept as separate clips/cues (never
        // baked into one inseparable track) so any one of them can be
        // swapped later without touching the others. See
        // BuildCinematicAudio's own doc-comment for which brief-section
        // channel each one is. C8.1d.6 removed the three *musical*
        // placeholders that used to live here (twang/tension-note/low-pulse
        // — see "Procedural placeholder removal" in
        // Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.6") now that
        // <see cref="_duelMusicClip"/> carries the tension build for real;
        // every cue remaining below is sound design, not music, and stays.
        private AudioClip _windClip;
        private AudioClip _predrawClip;
        private AudioClip _gunshotClip;
        private AudioClip _dustAccentClip;

        // C8.1d.5 — for the tension-silence window (5.6-6.2s): a very
        // quiet, slow tonal glide standing in for a leather creak.
        private AudioClip _creakClip;

        // C8.1d.7 — the real gameplay firearm cue, fired once per shot from
        // FireSequenceRoutine (never from the cinematic) — deliberately a
        // distinct clip from _gunshotClip above, on the same _sfxAudioSource
        // (never _musicAudioSource), so it is completely unaffected by any
        // cinematic music volume envelope.
        private AudioClip _gameplayGunshotClip;

        // C8.1d.9 — the correct outlaw's own "return fire" cue, fired only
        // by CountershotRoutine on a wrong answer/timeout, and the
        // dedicated full-screen "you got hit" flash overlay it triggers.
        // Never the generic ClasicoHud incorrect ding (removed for Western
        // entirely this phase) — see
        // Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.9".
        private AudioClip _enemyGunshotClip;
        private Image _playerHitFlash;

        // C8.1d.6 — the real, manually-selected Western duel music
        // candidate ("Dust & Silence"), loaded from
        // Resources/Audio/Gold/Western/Western_DuelMusic_01 — see
        // Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.6". Null (degrading to
        // "no music", never a crash) until that asset ships, same contract
        // as every other Gold art/audio asset in this presenter.
        private AudioClip _duelMusicClip;
        private const float MusicBaseVolume = 0.50f;
        private const float MusicDuckVolume = 0.20f;
        private const float MusicNearSilentVolume = 0.05f;

        private ClassificationChallenge _challenge;
        private HermitTheme Theme => RuntimeUIFactory.Theme;

        public WesternShootoutPresenter(MonoBehaviour host)
        {
            _host = host;
        }

        public void Build(Transform stageRoot)
        {
            _root = RuntimeUIFactory.CreatePanel(stageRoot, "WesternShootout", new Color(0.20f, 0.13f, 0.09f, 1f));

            // C8.1d: the illustrated Western establishing shot (main-street
            // silhouette, dusk sky, dust ground) replaces the C8.1b flat
            // sky/ground/sun/rocks split wherever the Art Bible candidate has
            // landed — falls back to the original procedural environment
            // untouched if the asset hasn't shipped yet.
            var background = RuntimeUIFactory.LoadArt("Art/Gold/Western/WesternBackground");
            if (background != null)
            {
                RuntimeUIFactory.CreateBackgroundImage(_root, "Background", background);
            }
            else
            {
                BuildProceduralEnvironment(_root);
            }

            // A contrast band behind the target row, independent of which
            // background is active — guarantees the outlaw labels stay
            // readable against a busy illustrated scene. Not the rejected
            // "answer card" — it's one continuous strip behind the whole
            // row, not a panel per outlaw.
            var targetStage = RuntimeUIFactory.CreatePanel(_root, "TargetStage", new Color(0.05f, 0.03f, 0.02f, 0.35f));
            targetStage.anchorMin = new Vector2(0f, 0f);
            targetStage.anchorMax = new Vector2(1f, 0.34f);
            targetStage.offsetMin = Vector2.zero;
            targetStage.offsetMax = Vector2.zero;

            BuildOffscreenShotRig(_root);
            BuildTumbleweed(_root);
            BuildCinematicAudio();

            _conceptText = RuntimeUIFactory.CreateText(
                _root, "Concept", string.Empty, Theme.HeadingSize, TextAnchor.MiddleCenter, Theme.TextPrimary,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -70), new Vector2(900, 80));

            // C8.1d.7: sits slightly above Concept (same top-center anchor,
            // a smaller box, a smaller/subtler font size than the concept
            // heading) — "near upper-center but below the top HUD, or
            // slightly above the concept/question area" per the brief.
            // Built hidden (alpha 0, inactive) — only ever shown by
            // ShowDisparaCue at the exact moment gameplay input becomes
            // available, never during the cinematic.
            _disparaText = RuntimeUIFactory.CreateText(
                _root, "DisparaCue", string.Empty, Theme.BodySize, TextAnchor.MiddleCenter, Theme.Accent,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(300, 32));
            _disparaText.color = new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0f);
            _disparaText.fontStyle = FontStyle.Bold;
            _disparaText.gameObject.SetActive(false);

            // C8.1d.1: substantially larger than the C8.1d production pass
            // (190x220) — the outlaws were reported too small/pixelated (see
            // the class doc-comment and Docs/C8_1D_GOLD_ART_INTEGRATION.md's
            // pixelation investigation). Sheriff's own box shrank slightly
            // (150x210 -> 140x200, anchor 0.08 -> 0.04) to make room without
            // the two overlapping — see that same doc section for the exact
            // layout math.
            const float targetWidth = 240f;
            const float targetHeight = 320f;
            const float spacing = 14f;
            var startX = -((MaxTargets - 1) * (targetWidth + spacing)) / 2f;
            var vestPalette = new[]
            {
                new Color(0.42f, 0.16f, 0.13f, 1f),
                new Color(0.16f, 0.30f, 0.24f, 1f),
                new Color(0.30f, 0.22f, 0.10f, 1f),
                new Color(0.20f, 0.18f, 0.34f, 1f),
            };

            for (var i = 0; i < MaxTargets; i++)
            {
                var index = i;
                var x = startX + i * (targetWidth + spacing);

                var button = RuntimeUIFactory.CreateButton(
                    _root, $"WesternTarget{i}", string.Empty,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, 140), new Vector2(targetWidth, targetHeight));

                var buttonRect = (RectTransform)button.transform;
                buttonRect.pivot = new Vector2(0.5f, 0f);
                buttonRect.anchoredPosition = new Vector2(x, 140);
                buttonRect.sizeDelta = new Vector2(targetWidth, targetHeight);
                button.onClick.AddListener(() => TargetSelected?.Invoke(index));

                // C8.1d.1: the button is the whole clickable/selectable
                // target (keyboard nav, mouse, EventSystem selection,
                // accessibility all keep working) but is now fully
                // invisible — no rectangular "answer card" behind the
                // outlaw. Selection/reveal feedback moved to the reticle and
                // the outlaw sprite/face itself (see ShowChallenge/
                // FireSequenceRoutine).
                button.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

                // A ground shadow first (renders behind everything else in
                // the button) so the outlaw reads as standing on the dirt
                // rather than floating over it.
                var shadow = RuntimeUIFactory.CreateRoundedPanel(button.transform, "GroundShadow", new Color(0f, 0f, 0f, 0.28f), 12);
                shadow.anchorMin = shadow.anchorMax = new Vector2(0.5f, 0f);
                shadow.anchoredPosition = new Vector2(0, 4);
                shadow.sizeDelta = new Vector2(88, 20);
                shadow.GetComponent<Image>().raycastTarget = false;
                shadow.SetAsFirstSibling();

                var letter = OutlawLetters[i];
                var neutralArt = RuntimeUIFactory.LoadArt($"Art/Gold/Western/Actors/Outlaw_{letter}_Neutral");
                var hitArt = RuntimeUIFactory.LoadArt($"Art/Gold/Western/Actors/Outlaw_{letter}_Hit");
                var usesArt = neutralArt != null && hitArt != null;
                _outlawNeutralArt.Add(neutralArt);
                _outlawHitArt.Add(hitArt);
                _outlawUsesArt.Add(usesArt);

                CharacterPrimitives.Face? face = null;
                Image spriteImage = null;

                if (usesArt)
                {
                    // The sprite fills the whole (now much larger) button
                    // footprint with preserveAspect on — the character
                    // dominates the target, exactly the brief's "no card,
                    // the sprite IS the target" requirement.
                    var spriteGo = new GameObject("Sprite", typeof(RectTransform), typeof(Image));
                    var spriteRect = (RectTransform)spriteGo.transform;
                    spriteRect.SetParent(button.transform, false);
                    spriteRect.anchorMin = new Vector2(0.5f, 0f);
                    spriteRect.anchorMax = new Vector2(0.5f, 0f);
                    spriteRect.pivot = new Vector2(0.5f, 0f);
                    spriteRect.anchoredPosition = Vector2.zero;
                    spriteRect.sizeDelta = new Vector2(targetWidth, targetHeight);

                    spriteImage = spriteGo.GetComponent<Image>();
                    spriteImage.sprite = neutralArt;
                    spriteImage.preserveAspect = true;
                    spriteImage.raycastTarget = false;
                    spriteRect.SetAsLastSibling();
                }
                else
                {
                    // A silhouetted head sits above the vest (the button
                    // itself) so each outlaw reads as a character, not a
                    // relabeled panel.
                    var head = RuntimeUIFactory.CreateRoundedPanel(button.transform, "Head", new Color(0.78f, 0.60f, 0.45f, 1f), 26);
                    head.anchorMin = head.anchorMax = new Vector2(0.5f, 1f);
                    head.anchoredPosition = new Vector2(0, -66);
                    head.sizeDelta = new Vector2(56, 56);

                    face = CharacterPrimitives.Build(head, 56f);

                    // A bandana strip across the lower face — the one prop
                    // that makes this unmistakably "outlaw" rather than
                    // "generic head".
                    var bandana = RuntimeUIFactory.CreatePanel(head, "Bandana", vestPalette[i % vestPalette.Length]);
                    bandana.anchorMin = bandana.anchorMax = new Vector2(0.5f, 0.5f);
                    bandana.anchoredPosition = new Vector2(0, -16);
                    bandana.sizeDelta = new Vector2(56, 18);
                    bandana.SetAsLastSibling();

                    var hat = RuntimeUIFactory.CreateRoundedPanel(button.transform, "Hat", new Color(0.12f, 0.09f, 0.06f, 1f), 40);
                    hat.anchorMin = hat.anchorMax = new Vector2(0.5f, 1f);
                    hat.anchoredPosition = new Vector2(0, -42);
                    hat.sizeDelta = new Vector2(80, 34);
                }

                var dust = RuntimeUIFactory.CreateRoundedPanel(button.transform, "DustPuff", new Color(0.85f, 0.72f, 0.5f, 0f), 56);
                dust.anchorMin = dust.anchorMax = new Vector2(0.5f, 0.35f);
                dust.anchoredPosition = Vector2.zero;
                dust.sizeDelta = new Vector2(56, 56);
                dust.SetAsLastSibling();

                var label = button.GetComponentInChildren<Text>();
                label.fontSize = Theme.CaptionSize;
                label.rectTransform.anchorMin = new Vector2(0.5f, 0f);
                label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                label.rectTransform.anchoredPosition = new Vector2(0, -18);
                label.rectTransform.sizeDelta = new Vector2(targetWidth, 32);
                label.rectTransform.SetAsLastSibling();

                _targets.Add(button);
                _targetLabels.Add(label);
                _targetFaces.Add(face);
                _outlawSpriteImages.Add(spriteImage);
                _dustPuffs.Add(dust.GetComponent<Image>());
                _targetPunchHandles.Add(new MotionHandle());
                _targetShakeHandles.Add(new MotionHandle());
                _dustHandles.Add(new MotionHandle());
            }

            RuntimeUIFactory.ChainHorizontal(_targets.ToArray());

            // A crosshair reads as "aiming", a filled dot reads as
            // "selection" — a thin ring plus two short cross bars, all
            // children of the reticle root so they follow it as one unit.
            // This is the player's targeting readout — the ONLY selection
            // chrome left now that the target buttons are invisible.
            _reticle = RuntimeUIFactory.CreateRoundedPanel(_root, "Reticle", new Color(0f, 0f, 0f, 0f), 18);
            _reticle.anchorMin = new Vector2(0.5f, 0.5f);
            _reticle.anchorMax = new Vector2(0.5f, 0.5f);
            _reticle.sizeDelta = new Vector2(2, 2);
            var reticleColor = new Color(1f, 0.95f, 0.85f, 0.9f);

            var ring = RuntimeUIFactory.CreateRoundedPanel(_reticle, "Ring", new Color(0f, 0f, 0f, 0f), 18);
            ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f);
            ring.sizeDelta = new Vector2(36, 36);
            var ringImage = ring.GetComponent<Image>();
            ringImage.color = reticleColor;
            ringImage.type = Image.Type.Sliced;
            // A thin ring look from a filled rounded sprite: shrink an
            // inner copy of the same color as the background to punch a
            // hole — cheap, no mask component needed.
            var ringHole = RuntimeUIFactory.CreateRoundedPanel(ring, "RingHole", new Color(0.20f, 0.13f, 0.09f, 1f), 14);
            ringHole.anchorMin = ringHole.anchorMax = new Vector2(0.5f, 0.5f);
            ringHole.sizeDelta = new Vector2(26, 26);

            BuildReticleBar(_reticle, true, reticleColor);
            BuildReticleBar(_reticle, false, reticleColor);

            _reticle.gameObject.SetActive(false);

            // C8.1d.9: the countershot's "you got hit" overlay — a plain
            // full-stretch Image.color alpha flash (never CanvasGroup, never
            // Image.Type.Filled/Radial — the same reasoning as the
            // cinematic's own GunshotFlashRoutine/CinematicFlash), built
            // after the reticle so it draws over the whole gameplay layer
            // during Decision/Feedback. The cinematic rig below is a later
            // sibling still, but is always inactive during gameplay, so
            // draw order between the two never actually matters at runtime.
            var playerHitFlashRect = RuntimeUIFactory.CreatePanel(_root, "PlayerHitFlash", new Color(0.85f, 0.1f, 0.1f, 0f));
            RuntimeUIFactory.StretchFull(playerHitFlashRect);
            _playerHitFlash = playerHitFlashRect.GetComponent<Image>();
            _playerHitFlash.raycastTarget = false;

            // Built last (topmost sibling) so it draws over the gameplay
            // layer above — matters for the brief moment the gunshot flash
            // fires right as gameplay is about to be revealed.
            BuildCinematicRig(_root);

            _root.gameObject.SetActive(false);
        }

        /// <summary>C8.1d.4: builds the cinematic duel-intro rig — a
        /// full-stage-covering <c>CinematicRoot</c> (transparent by default,
        /// so the real Western background/environment shows through
        /// whenever no shot is active — the brief's establishing-shot
        /// phase needs exactly this) holding one <c>CinematicImage</c> that
        /// gets its sprite swapped between the three close-up stills
        /// (<see cref="SetCinematicShot"/>), plus a <c>CinematicFlash</c>
        /// overlay for the gunshot punctuation. Uses
        /// <see cref="AspectRatioFitter"/> in EnvelopeParent mode so each
        /// 16:9 close-up fills the stage cleanly with no stretch/distortion,
        /// cropping only the minimum needed if the stage's own aspect
        /// differs slightly (brief section 7). No letterbox bars — judged
        /// unnecessary complexity for this pass (brief section 8 explicitly
        /// allows skipping it). Hidden by default; only
        /// <see cref="CinematicIntroRoutine"/> (round 1 of an Encounter)
        /// ever activates it.</summary>
        private void BuildCinematicRig(Transform root)
        {
            _cinematicSheriffCloseup = RuntimeUIFactory.LoadArt("Art/Gold/Western/Cinematic/Western_Sheriff_Closeup");
            _cinematicOutlawCloseup = RuntimeUIFactory.LoadArt("Art/Gold/Western/Cinematic/Western_OutlawA_Closeup");
            _cinematicHandCloseup = RuntimeUIFactory.LoadArt("Art/Gold/Western/Cinematic/Western_Sheriff_HandGun_Closeup");

            _cinematicRoot = RuntimeUIFactory.CreatePanel(root, "CinematicRoot", new Color(0f, 0f, 0f, 0f));
            RuntimeUIFactory.StretchFull(_cinematicRoot);
            _cinematicRoot.GetComponent<Image>().raycastTarget = false;

            var imageGo = new GameObject("CinematicImage", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            var imageRect = (RectTransform)imageGo.transform;
            imageRect.SetParent(_cinematicRoot, false);
            RuntimeUIFactory.StretchFull(imageRect);

            _cinematicImage = imageGo.GetComponent<Image>();
            _cinematicImage.raycastTarget = false;
            _cinematicImage.preserveAspect = false;

            var fitter = imageGo.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
            _cinematicImage.gameObject.SetActive(false);

            var flashRect = RuntimeUIFactory.CreatePanel(_cinematicRoot, "CinematicFlash", new Color(1f, 0.95f, 0.85f, 0f));
            RuntimeUIFactory.StretchFull(flashRect);
            _cinematicFlash = flashRect.GetComponent<Image>();
            _cinematicFlash.raycastTarget = false;

            _cinematicRoot.gameObject.SetActive(false);
        }

        /// <summary>C8.1d.3: the off-screen shooter rig — replaces the old
        /// Sheriff-anchored muzzle flash now that Sheriff himself is
        /// intro-only and never visible during gameplay. <see cref="_offscreenShotOrigin"/>
        /// is an invisible anchor authored on the left edge of the stage
        /// (the brief's "OffscreenShotOrigin"); <see cref="_muzzleFlash"/> is
        /// a child of it (was previously a child of Sheriff's own
        /// MuzzleAnchor); <see cref="_tracer"/> is a short streak, built
        /// once inactive, stretched/rotated at fire time
        /// (<see cref="FireOffscreenShot"/>) toward whichever outlaw was
        /// selected — this is what lets one fixed origin serve all four
        /// targets without a per-target Sheriff aim pose.</summary>
        private void BuildOffscreenShotRig(Transform root)
        {
            _offscreenShotOrigin = new GameObject("OffscreenShotOrigin", typeof(RectTransform)).GetComponent<RectTransform>();
            _offscreenShotOrigin.SetParent(root, false);
            _offscreenShotOrigin.anchorMin = _offscreenShotOrigin.anchorMax = new Vector2(0.02f, 0f);
            _offscreenShotOrigin.pivot = new Vector2(0.5f, 0.5f);
            _offscreenShotOrigin.anchoredPosition = new Vector2(0, 150);
            _offscreenShotOrigin.sizeDelta = Vector2.zero;

            _muzzleFlash = RuntimeUIFactory.CreateRoundedPanel(_offscreenShotOrigin, "MuzzleFlash", new Color(1f, 0.85f, 0.4f, 0f), 24).GetComponent<Image>();
            _muzzleFlash.rectTransform.anchorMin = _muzzleFlash.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _muzzleFlash.rectTransform.anchoredPosition = Vector2.zero;
            _muzzleFlash.rectTransform.sizeDelta = new Vector2(64, 64);
            _muzzleFlash.raycastTarget = false;

            _tracer = RuntimeUIFactory.CreateRoundedPanel(root, "Tracer", new Color(1f, 0.92f, 0.75f, 0f), 4);
            _tracer.anchorMin = _tracer.anchorMax = new Vector2(0f, 0f);
            _tracer.pivot = new Vector2(0f, 0.5f);
            _tracer.sizeDelta = new Vector2(140, 6);
            _tracer.GetComponent<Image>().raycastTarget = false;
            _tracer.gameObject.SetActive(false);
        }

        /// <summary>A procedural "ball of twigs" — overlapping thin rounded
        /// bars fanned around a center point plus a filled core, built once,
        /// inactive except while <see cref="TumbleweedRoutine"/> is running
        /// (C8.1d.1 brief, section 10: "no persistent object after intro").
        /// Positioned mid-ground, well clear of the concept text above and
        /// the target-row baseline it crosses in front of.
        ///
        /// C8.1d.2: manual validation found the original 54x54, 5-twig
        /// version too small/abstract to read as anything at a glance —
        /// enlarged and given more (uneven-length) twigs plus a filled core
        /// silhouette and a ground shadow, so it reads as one tangled mass
        /// rather than a thin spinning fan.</summary>
        private void BuildTumbleweed(Transform root)
        {
            _tumbleweed = RuntimeUIFactory.CreatePanel(root, "Tumbleweed", new Color(0f, 0f, 0f, 0f));
            _tumbleweed.GetComponent<Image>().raycastTarget = false;
            _tumbleweed.anchorMin = _tumbleweed.anchorMax = new Vector2(0.5f, 0f);
            _tumbleweed.pivot = new Vector2(0.5f, 0.5f);
            _tumbleweed.sizeDelta = new Vector2(100, 100);
            _tumbleweed.anchoredPosition = new Vector2(0, 130);

            var shadow = RuntimeUIFactory.CreateRoundedPanel(_tumbleweed, "GroundShadow", new Color(0f, 0f, 0f, 0.30f), 16);
            shadow.anchorMin = shadow.anchorMax = new Vector2(0.5f, 0f);
            shadow.anchoredPosition = new Vector2(0, -34);
            shadow.sizeDelta = new Vector2(70, 18);
            shadow.GetComponent<Image>().raycastTarget = false;

            var core = RuntimeUIFactory.CreateRoundedPanel(_tumbleweed, "Core", new Color(0.50f, 0.37f, 0.18f, 0.9f), 30);
            core.anchorMin = core.anchorMax = new Vector2(0.5f, 0.5f);
            core.sizeDelta = new Vector2(56, 56);
            core.GetComponent<Image>().raycastTarget = false;

            var twigColor = new Color(0.60f, 0.45f, 0.22f, 0.95f);
            var twigLengths = new[] { 84f, 70f, 90f, 66f, 80f, 72f, 88f };
            for (var i = 0; i < twigLengths.Length; i++)
            {
                var twig = RuntimeUIFactory.CreateRoundedPanel(_tumbleweed, "Twig", twigColor, 8);
                twig.anchorMin = twig.anchorMax = new Vector2(0.5f, 0.5f);
                twig.sizeDelta = new Vector2(twigLengths[i], 14);
                twig.localRotation = Quaternion.Euler(0f, 0f, i * (180f / twigLengths.Length) + 11f);
                twig.GetComponent<Image>().raycastTarget = false;
            }

            _tumbleweed.gameObject.SetActive(false);
        }

        /// <summary>C8.1d.4/.6: builds the cinematic duel intro's two
        /// independent AudioSources (added to the shared host GameObject,
        /// alongside ClasicoHud's own — multiple sources per GameObject is
        /// normal) and synthesizes/loads its audio cues:
        /// A. <see cref="_windClip"/> — Western ambient wind, one long quiet
        ///    bed covering the whole ~6.5s intro (a single non-looping clip,
        ///    not a seamless loop, since the intro only ever plays once).
        /// B. <see cref="_duelMusicClip"/> — C8.1d.6: the real, manually-
        ///    selected duel music candidate ("Dust &amp; Silence"), replacing
        ///    the C8.1d.4/.5 procedural twang/tension-note/low-pulse
        ///    placeholders that used to carry this role (removed, not kept
        ///    disabled — see the class-level field doc-comment). Played on
        ///    its own <see cref="_musicAudioSource"/>, never mixed into
        ///    <see cref="_sfxAudioSource"/>'s one-shots.
        /// C. <see cref="_predrawClip"/> — a tiny metallic pre-draw tick.
        /// C2. <see cref="_creakClip"/> — C8.1d.5: a very quiet, slow tonal
        ///    glide standing in for a leather creak during the tension-
        ///    silence window (5.6-6.2s) — placeholder per the brief's "tiny
        ///    leather creak" ask, not a real foley sample.
        /// D. <see cref="_gunshotClip"/> — the intro's own punctuation shot
        ///    (see <see cref="ProceduralAudio.Gunshot"/>) — distinct from
        ///    and unrelated to the real gameplay shot fired later from
        ///    <see cref="FireOffscreenShot"/>.
        /// E. <see cref="_dustAccentClip"/> — optional dust/ricochet accent
        ///    right after the shot.
        /// Every cue above except B is still placeholder/procedural per the
        /// original brief — no further Suno/Stable Audio finalization in
        /// this phase beyond the one real music asset.</summary>
        private void BuildCinematicAudio()
        {
            var hostGameObject = ((Component)_host).gameObject;

            _sfxAudioSource = hostGameObject.AddComponent<AudioSource>();
            _sfxAudioSource.playOnAwake = false;
            _sfxAudioSource.spatialBlend = 0f;

            _musicAudioSource = hostGameObject.AddComponent<AudioSource>();
            _musicAudioSource.playOnAwake = false;
            _musicAudioSource.spatialBlend = 0f;
            _musicAudioSource.loop = false;

            _windClip = ProceduralAudio.Noise("WesternCinematicWind", 6.6f, 0.05f);

            // C8.1d.8: replaced the old sharp 1800Hz Tone "tick" — manual
            // validation reported it as reading like a click/pop/mini-shot
            // immediately before the cinematic gunshot. Duration/volume are
            // deliberately well under the gunshot's own — see
            // Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.8".
            _predrawClip = ProceduralAudio.PreDrawTension("WesternPreDrawTension", 0.12f, 0.05f);
            _creakClip = ProceduralAudio.Sweep("WesternDuelLeatherCreak", 320f, 210f, 0.55f, 0.05f);
            _gunshotClip = ProceduralAudio.Gunshot("WesternDuelGunshot", 0.35f);
            _dustAccentClip = ProceduralAudio.Noise("WesternDuelDustAccent", 0.3f, 0.12f);

            // C8.1d.7 — the real gameplay firearm cue, deliberately a
            // different composite from _gunshotClip above (see
            // ProceduralAudio.GameplayGunshot's own doc-comment): dry,
            // short, crisp, repeatable — a CRACK, not the cinematic's own
            // BOOM. Fired from FireSequenceRoutine, never from the
            // cinematic.
            _gameplayGunshotClip = ProceduralAudio.GameplayGunshot("WesternGameplayGunshot", 0.16f);

            // C8.1d.9 — the countershot's own "return fire" cue, fired only
            // on a wrong answer/timeout — see CountershotRoutine.
            _enemyGunshotClip = ProceduralAudio.EnemyGunshot("WesternEnemyGunshot", 0.18f);

            _duelMusicClip = RuntimeUIFactory.LoadAudio("Audio/Gold/Western/Western_DuelMusic_01");
        }

        /// <summary>C8.1d.6: starts the real duel music track (if it shipped
        /// — degrades silently to no music otherwise, same contract as every
        /// other Gold asset) at the cinematic's own authoritative start
        /// point — the very first statement of
        /// <see cref="CinematicIntroRoutine"/>, the same coroutine that owns
        /// every other intro-start event — never a separately-delayed
        /// coroutine, per the brief's explicit "do not delay it behind
        /// another arbitrary coroutine".</summary>
        private void StartDuelMusic()
        {
            if (_duelMusicClip == null)
            {
                return;
            }

            _musicAudioSource.clip = _duelMusicClip;
            _musicAudioSource.volume = MusicBaseVolume;
            _musicAudioSource.Play();
        }

        /// <summary>A linear volume fade on <see cref="_musicAudioSource"/>
        /// only — mirrors <see cref="PushInRoutine"/>'s own shape/contract
        /// (a plain lerp over a caller-given duration, launched as its own
        /// concurrent coroutine so it runs alongside the visual shot it
        /// belongs to rather than blocking it). Implements the C8.1d.6
        /// brief's suggested envelope (moderate-low -&gt; gentle attenuation
        /// -&gt; very low) without ever touching the source's own
        /// <c>.clip</c> or re-triggering playback — the raw MP3 is never
        /// edited, no new rendered file is created.</summary>
        private IEnumerator MusicVolumeRoutine(float duration, float fromVolume, float toVolume)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                _musicAudioSource.volume = Mathf.Lerp(fromVolume, toVolume, t);
                yield return null;
            }

            _musicAudioSource.volume = toVolume;
        }

        /// <summary>C8.1d.4: <paramref name="duration"/> is now caller-
        /// specified — the cinematic establishing shot only has a ~1.0s
        /// window (C8.1d.5), well short of this routine's original fixed
        /// 1.6s crossing, so the cinematic confines it to a shorter pass
        /// instead of letting it run past the shot it's meant to belong
        /// to.</summary>
        private IEnumerator TumbleweedRoutine(float duration)
        {
            const float startX = -700f;
            const float endX = 700f;
            const float baseY = 130f;

            _tumbleweed.gameObject.SetActive(true);
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var x = Mathf.Lerp(startX, endX, t);
                var bounce = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 4f)) * 14f;
                _tumbleweed.anchoredPosition = new Vector2(x, baseY + bounce);
                _tumbleweed.localRotation = Quaternion.Euler(0f, 0f, t * 720f);
                yield return null;
            }

            _tumbleweed.gameObject.SetActive(false);
            _tumbleweed.anchoredPosition = new Vector2(0, baseY);
            _tumbleweed.localRotation = Quaternion.identity;
        }

        /// <summary>C8.1b's original flat sky/ground/sun/rock environment —
        /// kept verbatim as the fallback: used only when the illustrated
        /// Western background asset hasn't shipped, never mixed with it.</summary>
        private static void BuildProceduralEnvironment(Transform root)
        {
            var sky = RuntimeUIFactory.CreatePanel(root, "Sky", new Color(0.36f, 0.24f, 0.14f, 1f));
            sky.anchorMin = new Vector2(0f, 0.55f);
            sky.anchorMax = Vector2.one;
            sky.offsetMin = Vector2.zero;
            sky.offsetMax = Vector2.zero;

            var sun = RuntimeUIFactory.CreateRoundedPanel(root, "Sun", new Color(0.95f, 0.75f, 0.35f, 0.55f), 90);
            sun.anchorMin = sun.anchorMax = new Vector2(0.16f, 1f);
            sun.anchoredPosition = new Vector2(0, -90);
            sun.sizeDelta = new Vector2(180, 180);

            var horizon = RuntimeUIFactory.CreatePanel(root, "Horizon", new Color(0.58f, 0.42f, 0.22f, 1f));
            horizon.anchorMin = new Vector2(0f, 0.55f);
            horizon.anchorMax = new Vector2(1f, 0.62f);
            horizon.offsetMin = Vector2.zero;
            horizon.offsetMax = Vector2.zero;

            var ground = RuntimeUIFactory.CreatePanel(root, "Ground", new Color(0.30f, 0.20f, 0.11f, 1f));
            ground.anchorMin = Vector2.zero;
            ground.anchorMax = new Vector2(1f, 0.55f);
            ground.offsetMin = Vector2.zero;
            ground.offsetMax = Vector2.zero;

            BuildRock(ground, new Vector2(0.12f, 0f), new Vector2(60, 40));
            BuildRock(ground, new Vector2(0.88f, 0f), new Vector2(80, 50));
        }

        private static void BuildRock(Transform parent, Vector2 anchor, Vector2 size)
        {
            var rock = RuntimeUIFactory.CreateRoundedPanel(parent, "Rock", new Color(0.22f, 0.16f, 0.10f, 1f), 16);
            rock.anchorMin = rock.anchorMax = anchor;
            rock.anchoredPosition = new Vector2(0, size.y * 0.4f);
            rock.sizeDelta = size;
        }

        private static void BuildReticleBar(Transform reticle, bool horizontal, Color color)
        {
            var bar = RuntimeUIFactory.CreatePanel(reticle, "ReticleBar", color);
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
            bar.sizeDelta = horizontal ? new Vector2(26, 3) : new Vector2(3, 26);
        }

        /// <summary><paramref name="isEncounterStart"/> is true exactly once
        /// per Western Encounter (round 1) and false for its continuation
        /// rounds (2, 3, ...) — decided by <see cref="ClasicoGameHost"/>, not
        /// by this presenter. True stages the round hidden and plays
        /// <see cref="CinematicIntroRoutine"/> (the cinematic duel montage,
        /// then gameplay reveal — C8.1d.4); false is a quick in-place reset
        /// only, gameplay content revealed immediately. The outlaw world
        /// itself is never hidden/rebuilt between continuation rounds — see
        /// the class doc-comment.</summary>
        public void ShowChallenge(ClassificationChallenge challenge, bool isEncounterStart)
        {
            _challenge = challenge;
            _root.gameObject.SetActive(true);

            // C8.1d.9: idempotent safety — CountershotRoutine's own flash
            // always clears itself well before a round transitions, but a
            // fresh round should never risk starting with any residual
            // red tint regardless.
            _playerHitFlash.color = new Color(0.85f, 0.1f, 0.1f, 0f);

            for (var i = 0; i < _targets.Count; i++)
            {
                var hasOption = i < challenge.CategoryOptions.Length;
                if (!hasOption)
                {
                    continue;
                }

                // Every outlaw restores to Neutral before its next use — a
                // Hit sprite/pose/tint must never persist into a future
                // round. Reset regardless of active state — an inactive
                // GameObject's components can still be written to safely.
                if (_outlawUsesArt[i])
                {
                    _outlawSpriteImages[i].sprite = _outlawNeutralArt[i];
                    _outlawSpriteImages[i].color = Color.white;
                }
                else if (_targetFaces[i].HasValue)
                {
                    CharacterPrimitives.ApplyPose(_targetFaces[i].Value, CharacterPrimitives.FacePose.Idle, 56f);
                }
            }

            if (isEncounterStart)
            {
                // C8.1d.3/.4: the outlaws stay fully hidden during the intro
                // (not merely non-interactable, as in C8.1d.2) — the
                // cinematic's own "no gameplay UI during the montage"
                // requirement. Activation is deferred to RevealRoundContent,
                // called from RevealAfterIntro once the intro finishes.
                for (var i = 0; i < _targets.Count; i++)
                {
                    _targets[i].gameObject.SetActive(false);
                    _targetLabels[i].text = string.Empty;
                }

                _conceptText.text = string.Empty;
                _reticle.gameObject.SetActive(false);
                HideDisparaCueImmediately();

                _host.StartCoroutine(CinematicIntroRoutine());
            }
            else
            {
                RevealRoundContent(challenge, animateOutlawEntrance: false);
            }
        }

        /// <summary>C8.1d.5 re-paced this montage from ~4.11s to a ~6.5s
        /// target; C8.1d.6 then replaced the procedural twang/tension-note/
        /// low-pulse musical placeholders with the real duel music candidate
        /// ("Dust &amp; Silence" — see
        /// Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.6") without touching
        /// any shot's timing, push-in, or the sound-design cues (wind,
        /// creak, pre-draw tick, gunshot, dust accent). Sequenced to land
        /// safely inside <c>ClasicoGameDefinition.EncounterIntroSeconds</c>'s
        /// declared default (see that field's own doc-comment for the exact
        /// margin arithmetic) so the cinematic is always finished before
        /// <see cref="RevealAfterIntro"/> fires:
        ///
        /// 0.00-1.00 — WIDE ESTABLISHING SHOT: the real Western background
        /// is already visible (CinematicImage inactive); a lengthened
        /// (0.85s) tumbleweed crossing plays, wind fades in — duel music
        /// starts here too (<see cref="StartDuelMusic"/>), the cinematic's
        /// own authoritative t=0.
        /// 1.00-2.00 — CUT TO SHERIFF CLOSE-UP, slow push-in.
        /// 2.00-3.00 — CUT TO OUTLAW CLOSE-UP, mirrored push-in.
        /// 3.00-4.10 — CUT TO SHERIFF HAND/HOLSTER — the longest detail
        /// shot, letting the hand/revolver tension breathe.
        /// 4.10-4.90 — CUT BACK TO SHERIFF FACE, slightly tighter push-in,
        /// tension rising (carried by the music itself now, not discrete
        /// pulse cues).
        /// 4.90-5.60 — CUT TO OUTLAW FACE, a short response shot; duel music
        /// begins its gentle attenuation
        /// (<see cref="MusicVolumeRoutine"/>, base -&gt; duck) across this
        /// exact 0.70s window — the MUSIC SYNC MAP's "gentle attenuation"
        /// region.
        /// 5.60-6.20 — TENSION SILENCE: duel music collapses to near-silence
        /// (duck -&gt; near-silent) across this exact 0.60s window — the
        /// MUSIC SYNC MAP's tension-drop region; only the ambient wind bed
        /// (already playing), a tiny leather-creak cue, and (near the end) a
        /// tiny metallic pre-draw tick remain audible.
        /// 6.20 — GUNSHOT: duel music is force-stopped (not merely faded) so
        /// the transient never shares the mix with any residual tail, then
        /// flash + tiny camera punch + the intro's own gunshot cue — pure
        /// punctuation, never scoring/hit-state/answer selection (brief
        /// section 15); the real gameplay shot is
        /// <see cref="FireOffscreenShot"/>, fired later after the player
        /// answers.
        /// 6.20-6.50 — FLASH/CUT TRANSITION: hard cut, cinematic gone (no
        /// fade, per the brief's "cuts, not fades" — only the flash itself
        /// briefly eases).
        ///
        /// <see cref="RevealAfterIntro"/> remains the actual gameplay-reveal
        /// gate (host-triggered, authoritative, at the director's real
        /// Intro-&gt;Decision boundary) — this routine only owns the
        /// cinematic's own cosmetic choreography/audio, exactly the C8.1d.2
        /// lesson that fire-and-forget animation must never be the
        /// correctness gate.</summary>
        private IEnumerator CinematicIntroRoutine()
        {
            _cinematicRoot.gameObject.SetActive(true);
            _cinematicImage.gameObject.SetActive(false);
            _cinematicFlash.color = new Color(1f, 0.95f, 0.85f, 0f);

            _sfxAudioSource.PlayOneShot(_windClip);
            StartDuelMusic();

            // 0.00-1.00 — WIDE ESTABLISHING SHOT.
            _host.StartCoroutine(TumbleweedRoutine(0.85f));
            yield return new WaitForSeconds(1.00f);

            // 1.00-2.00 — CUT TO SHERIFF CLOSE-UP.
            SetCinematicShot(_cinematicSheriffCloseup);
            yield return PushInRoutine(1.00f, 1.0f, 1.03f);

            // 2.00-3.00 — CUT TO OUTLAW CLOSE-UP (opposing eyeline/push-in).
            SetCinematicShot(_cinematicOutlawCloseup);
            yield return PushInRoutine(1.00f, 1.0f, 1.04f);

            // 3.00-4.10 — CUT TO SHERIFF HAND/HOLSTER — the longest detail
            // shot; let the hand/revolver tension breathe.
            SetCinematicShot(_cinematicHandCloseup);
            yield return PushInRoutine(1.10f, 1.0f, 1.02f);

            // 4.10-4.90 — CUT BACK TO SHERIFF FACE, tension rising (the
            // music itself carries this now — C8.1d.6 removed the two
            // discrete low-pulse cues that used to land here).
            SetCinematicShot(_cinematicSheriffCloseup);
            yield return PushInRoutine(0.80f, 1.0f, 1.035f);

            // 4.90-5.60 — CUT TO OUTLAW FACE, a short response shot. Duel
            // music begins its gentle attenuation across this exact 0.70s
            // shot.
            SetCinematicShot(_cinematicOutlawCloseup);
            _host.StartCoroutine(MusicVolumeRoutine(0.70f, MusicBaseVolume, MusicDuckVolume));
            yield return PushInRoutine(0.70f, 1.0f, 1.02f);

            // 5.60-6.20 — TENSION SILENCE: duel music collapses toward
            // near-silence across this exact 0.60s window; only wind
            // (already playing), a tiny leather creak, and a late, soft
            // pre-draw tension touch remain.
            _host.StartCoroutine(MusicVolumeRoutine(0.60f, MusicDuckVolume, MusicNearSilentVolume));
            _sfxAudioSource.PlayOneShot(_creakClip);
            yield return new WaitForSeconds(0.45f);
            _sfxAudioSource.PlayOneShot(_predrawClip);
            WesternAudioEvents.Record("CinematicPreDraw", _predrawClip);
            yield return new WaitForSeconds(0.15f);

            // 6.20 — GUNSHOT. Duel music is force-stopped here (not just
            // faded) so the transient never shares the mix with any
            // residual music tail.
            _musicAudioSource.Stop();
            _sfxAudioSource.PlayOneShot(_gunshotClip);
            WesternAudioEvents.Record("CinematicGunshot", _gunshotClip);
            _sfxAudioSource.PlayOneShot(_dustAccentClip);
            yield return GunshotFlashRoutine();

            // 6.20-6.50 — FLASH/CUT TRANSITION: hard cut, cinematic gone.
            _cinematicRoot.gameObject.SetActive(false);
        }

        /// <summary>Swaps the one shared CinematicImage to a new close-up and
        /// resets its push-in scale to rest — a genuine hard cut (instant
        /// sprite swap, no crossfade), per the brief's "cuts, not fades".</summary>
        private void SetCinematicShot(Sprite sprite)
        {
            _cinematicImage.sprite = sprite;
            _cinematicImage.gameObject.SetActive(sprite != null);
            _cinematicImage.rectTransform.localScale = Vector3.one;
        }

        /// <summary>A barely-perceptible scale-only "push-in" over
        /// <paramref name="duration"/> — the one reusable local helper every
        /// cinematic shot uses (brief section 13: "no large zooms, no
        /// rotations, no warping"). Never touches position except where a
        /// caller explicitly wants drift, which none currently do — kept to
        /// the simplest motion that still reads as a camera move.</summary>
        private IEnumerator PushInRoutine(float duration, float fromScale, float toScale)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                _cinematicImage.rectTransform.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, t);
                yield return null;
            }

            _cinematicImage.rectTransform.localScale = Vector3.one * toScale;
        }

        /// <summary>The gunshot's visual punctuation: a brief warm/white
        /// flash transient (~0.16s alpha ramp, well inside a snappy "strong
        /// transient" read) plus a tiny camera punch on the whole
        /// CinematicRoot, then a held blank beat so the whole "FLASH / CUT"
        /// beat (B1's 6.20-6.50 window) spans ~0.30s before
        /// <see cref="CinematicIntroRoutine"/> hard-cuts the cinematic away.
        /// Deliberately a plain CanvasGroup-free <c>Image.color</c> alpha
        /// ramp on a single full-stretch panel — never the old
        /// TransitionOverlay Filled/Radial360 technique that caused the
        /// C7-era black-screen bug (see ClasicoHud's own transition-cut
        /// comment history); full-screen transitions in this codebase use
        /// plain alpha only.</summary>
        private IEnumerator GunshotFlashRoutine()
        {
            const float flashDuration = 0.16f;
            const float totalDuration = 0.30f;
            LocalMotionFx.Punch(_host, _cinematicPunchHandle, _cinematicRoot, 0.14f, 1.025f);

            var elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                var t = elapsed / flashDuration;
                var alpha = t < 0.5f ? Mathf.Lerp(0f, 0.85f, t / 0.5f) : Mathf.Lerp(0.85f, 0f, (t - 0.5f) / 0.5f);
                _cinematicFlash.color = new Color(1f, 0.95f, 0.85f, alpha);
                yield return null;
            }

            _cinematicFlash.color = new Color(1f, 0.95f, 0.85f, 0f);

            var remaining = totalDuration - flashDuration;
            if (remaining > 0f)
            {
                yield return new WaitForSeconds(remaining);
            }
        }

        /// <summary>Called by <see cref="ClasicoGameHost"/> exactly once per
        /// round, at the moment the director's Intro phase ends and Decision
        /// begins — the actual, authoritative "GO" of the round, independent
        /// of <see cref="CinematicIntroRoutine"/>'s own cosmetic timing.
        /// Force-hides the cinematic (idempotent safety — it should already
        /// be hidden by its own final hard cut) and reveals the outlaws/
        /// concept/labels/reticle/input, with a small staggered settle-in
        /// for the outlaws on round 1 only. For a continuation round (2/3),
        /// <see cref="ShowChallenge"/> already revealed everything
        /// synchronously with no animation, so this call is a harmless
        /// idempotent re-application, not a second reveal/replay.</summary>
        public void RevealAfterIntro()
        {
            _cinematicRoot.gameObject.SetActive(false);

            // C8.1d.6: idempotent safety, same pattern as the CinematicRoot
            // hide above — the music should already be stopped by
            // CinematicIntroRoutine's own 6.20 gunshot beat, but this
            // guarantees no audio leak into gameplay/round 2/3 even if the
            // gate ever fires before that routine's own stop does.
            _musicAudioSource.Stop();

            if (_challenge != null)
            {
                RevealRoundContent(_challenge, animateOutlawEntrance: true);
            }
        }

        /// <summary><paramref name="animateOutlawEntrance"/> is true only for
        /// round 1 of an Encounter (the outlaws were hidden during the
        /// intro) — each outlaw gets a small staggered settle-in
        /// (C8.1d.3 brief section 6), under ~0.35s total across all four,
        /// never repeated for continuation rounds.</summary>
        private void RevealRoundContent(ClassificationChallenge challenge, bool animateOutlawEntrance)
        {
            _conceptText.text = challenge.ConceptLabel;
            _reticle.gameObject.SetActive(true);

            Button first = null;
            for (var i = 0; i < _targets.Count; i++)
            {
                var hasOption = i < challenge.CategoryOptions.Length;
                _targets[i].gameObject.SetActive(hasOption);
                if (!hasOption)
                {
                    continue;
                }

                _targetLabels[i].text = challenge.CategoryOptions[i];
                _targets[i].interactable = true;
                first ??= _targets[i];

                if (animateOutlawEntrance)
                {
                    _host.StartCoroutine(OutlawSettleInRoutine(i, i * 0.04f));
                }
            }

            RuntimeUIFactory.Select(first);
            ShowDisparaCue();
        }

        /// <summary>C8.1d.7: shows the refined "DISPARA" action cue at the
        /// exact moment gameplay input becomes available — called from
        /// <see cref="RevealRoundContent"/>, so it fires identically on
        /// round 1 (via <see cref="RevealAfterIntro"/>, right after the
        /// cinematic's own hard cut) and on every continuation round (2/3,
        /// an immediate reveal with no cinematic) per the brief's "may
        /// reappear at gameplay start" for those rounds. A quick scale/alpha
        /// punch (0.92/alpha 0 -&gt; 1.04/alpha 1 -&gt; 1.00, ~0.16s — brief
        /// section 3's suggested numbers), then holds static until
        /// <see cref="HideDisparaCueOnFire"/> fades it out on the player's
        /// first shot. Never a continuous pulse.</summary>
        private void ShowDisparaCue()
        {
            _disparaText.text = MicrogameVocabulary.CommandFor(MicrogameArchetype.AimSelect);
            _disparaText.gameObject.SetActive(true);

            if (_disparaRoutine != null)
            {
                _host.StopCoroutine(_disparaRoutine);
            }

            _disparaRoutine = _host.StartCoroutine(DisparaPunchInRoutine());
        }

        private IEnumerator DisparaPunchInRoutine()
        {
            const float duration = 0.16f;
            var rect = _disparaText.rectTransform;
            var baseColor = Theme.Accent;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var scale = t < 0.6f
                    ? Mathf.Lerp(0.92f, 1.04f, t / 0.6f)
                    : Mathf.Lerp(1.04f, 1.00f, (t - 0.6f) / 0.4f);
                rect.localScale = Vector3.one * scale;
                _disparaText.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0f, 1f, Mathf.Clamp01(t / 0.6f)));
                yield return null;
            }

            rect.localScale = Vector3.one;
            _disparaText.color = baseColor;
            _disparaRoutine = null;
        }

        /// <summary>C8.1d.7 brief section 11: "DISPARA may fade quickly after
        /// the shot... do not leave it permanently onscreen if it no longer
        /// provides useful information." Called from
        /// <see cref="FireSequenceRoutine"/> at the exact moment the player's
        /// shot fires — a quick alpha-only fade (no scale change, keeps the
        /// exit calm rather than another punch).</summary>
        private void HideDisparaCueOnFire()
        {
            if (!_disparaText.gameObject.activeSelf)
            {
                return;
            }

            if (_disparaRoutine != null)
            {
                _host.StopCoroutine(_disparaRoutine);
            }

            _disparaRoutine = _host.StartCoroutine(DisparaFadeOutRoutine());
        }

        private IEnumerator DisparaFadeOutRoutine()
        {
            const float duration = 0.15f;
            var startColor = _disparaText.color;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                _disparaText.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));
                yield return null;
            }

            _disparaText.gameObject.SetActive(false);
            _disparaRoutine = null;
        }

        /// <summary>A hard, immediate hide (no fade) — used only when staging
        /// round 1 hidden for the cinematic (<see cref="ShowChallenge"/>),
        /// mirroring how the concept/labels/reticle are also hard-cleared
        /// there rather than faded, since nothing should be visible at all
        /// during that staged-hidden window.</summary>
        private void HideDisparaCueImmediately()
        {
            if (_disparaRoutine != null)
            {
                _host.StopCoroutine(_disparaRoutine);
                _disparaRoutine = null;
            }

            _disparaText.text = string.Empty;
            _disparaText.color = new Color(Theme.Accent.r, Theme.Accent.g, Theme.Accent.b, 0f);
            _disparaText.rectTransform.localScale = Vector3.one;
            _disparaText.gameObject.SetActive(false);
        }

        /// <summary>A small, cheap settle-in (vertical rise + scale ease) for
        /// one outlaw — never an alpha fade (no CanvasGroup needed per
        /// target). <paramref name="delay"/> staggers A/B/C/D so they don't
        /// all pop in on the same frame.</summary>
        private IEnumerator OutlawSettleInRoutine(int index, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            var rect = _targets[index].transform as RectTransform;
            var restY = rect.anchoredPosition.y;
            const float duration = 0.2f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                rect.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, eased);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, Mathf.Lerp(restY - 18f, restY, eased));
                yield return null;
            }

            rect.localScale = Vector3.one;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, restY);
        }

        /// <summary>Called every frame during the Decision phase. The
        /// reticle is a cheap transform-position follow of whichever target
        /// is currently selected (mouse click and keyboard navigation both
        /// update EventSystem selection identically), never a rebuild.
        /// Sheriff is not part of gameplay at all (intro-only, and now a
        /// cinematic close-up rather than an in-world actor — C8.1d.4), so
        /// there is nothing left to aim here beyond the reticle itself.</summary>
        public void RenderDecision()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null)
            {
                return;
            }

            var selectedRect = selected.GetComponent<RectTransform>();
            if (selectedRect == null || !_targets.Exists(t => t.gameObject == selected))
            {
                return;
            }

            _reticle.position = selectedRect.position;
        }

        public void RevealOutcome(int selectedIndex)
        {
            _reticle.gameObject.SetActive(false);

            for (var i = 0; i < _targets.Count; i++)
            {
                if (_targets[i].gameObject.activeSelf)
                {
                    _targets[i].interactable = false;
                }
            }

            var correctIndex = Array.IndexOf(_challenge.CategoryOptions, _challenge.CorrectCategory);
            _host.StartCoroutine(FireSequenceRoutine(selectedIndex, correctIndex));
        }

        /// <summary>C8.1d.3: ~80ms anticipation -&gt; the shot fires from
        /// <see cref="_offscreenShotOrigin"/> (muzzle flash + a short tracer
        /// streak toward the selected outlaw + a small screen kick — Sheriff
        /// himself is never on-screen during gameplay, so there is no aim/
        /// fire pose or recoil to animate here) -&gt; outlaw reveal/reaction.
        /// Sheriff fires at whoever was selected regardless of correctness.
        /// Only the selected outlaw ever swaps to its Hit sprite; correct/
        /// incorrect is communicated by a subtle tint on the sprite itself
        /// (art path) or the existing face-pose change (fallback path) —
        /// never a rectangular color-panel reveal (C8.1d.1 correction).
        /// C8.1d.7 adds the real gameplay firearm cue
        /// (<see cref="_gameplayGunshotClip"/>, distinct from the cinematic's
        /// own <see cref="_gunshotClip"/>) and a small impact accent here —
        /// fired once per shot, for both correct and incorrect selections
        /// alike, since the shot itself is a physical action independent of
        /// the outcome it will go on to reveal. C8.1d.9 adds
        /// <see cref="CountershotRoutine"/> on a wrong answer or timeout
        /// (never on a correct answer) — the correct outlaw's own "return
        /// fire" replaces the old generic incorrect-answer HUD ding
        /// entirely.</summary>
        private IEnumerator FireSequenceRoutine(int selectedIndex, int correctIndex)
        {
            var hasTarget = selectedIndex >= 0 && selectedIndex < _targets.Count && _targets[selectedIndex].gameObject.activeSelf;

            // C8.1d.7: "DISPARA" no longer provides useful information once
            // Decision has ended (hit or timeout alike) — fade it now rather
            // than leaving it onscreen through the reveal.
            HideDisparaCueOnFire();

            yield return new WaitForSeconds(0.08f);

            if (hasTarget)
            {
                var targetRect = _targets[selectedIndex].transform as RectTransform;

                // C8.1d.7: the gameplay firearm cue fires here — aligned
                // with the muzzle flash/tracer start, never delayed behind
                // the outlaw's own hit reaction, and regardless of
                // correctness (the shot is a physical action; correctness
                // is communicated separately by the reveal tint/pose below).
                // Deliberately _sfxAudioSource, never _musicAudioSource —
                // this must be unaffected by any cinematic music envelope
                // (moot here anyway, since duel music only ever plays during
                // round 1's intro and is long since stopped by Decision).
                _sfxAudioSource.PlayOneShot(_gameplayGunshotClip);
                WesternAudioEvents.Record("GameplayGunshot", _gameplayGunshotClip);
                LocalMotionFx.FlashColor(_host, _muzzleFlashHandle, _muzzleFlash, new Color(1f, 0.85f, 0.4f, 0.9f), new Color(1f, 0.85f, 0.4f, 0f), 0.14f);
                LocalMotionFx.Punch(_host, _screenKickHandle, _root, 0.16f, 1.015f);
                yield return FireOffscreenShot(targetRect);

                // C8.1d.7 brief section 10: a very small, optional impact
                // accent, separate from the firearm shot itself, once the
                // tracer actually arrives — reuses the existing quiet dust
                // accent cue rather than adding a new one, kept subtle,
                // never gore/blood.
                _sfxAudioSource.PlayOneShot(_dustAccentClip);
                WesternAudioEvents.Record("GameplayImpact", _dustAccentClip);
            }

            var hit = hasTarget && selectedIndex == correctIndex;

            for (var i = 0; i < _targets.Count; i++)
            {
                if (!_targets[i].gameObject.activeSelf)
                {
                    continue;
                }

                if (i == correctIndex)
                {
                    if (_outlawUsesArt[i])
                    {
                        _outlawSpriteImages[i].color = Color.Lerp(Color.white, Theme.Correct, RevealTintStrength);
                    }
                    else if (_targetFaces[i].HasValue)
                    {
                        CharacterPrimitives.ApplyPose(_targetFaces[i].Value, CharacterPrimitives.FacePose.Correct, 56f);
                    }

                    if (hit)
                    {
                        if (_outlawUsesArt[i])
                        {
                            _outlawSpriteImages[i].sprite = _outlawHitArt[i];
                        }

                        LocalMotionFx.Punch(_host, _targetPunchHandles[i], _targets[i].transform as RectTransform, 0.22f, 1.2f);
                        PlayDustPuff(i);
                    }
                }
                else if (i == selectedIndex)
                {
                    if (_outlawUsesArt[i])
                    {
                        _outlawSpriteImages[i].sprite = _outlawHitArt[i];
                        _outlawSpriteImages[i].color = Color.Lerp(Color.white, Theme.Incorrect, RevealTintStrength);
                    }
                    else if (_targetFaces[i].HasValue)
                    {
                        CharacterPrimitives.ApplyPose(_targetFaces[i].Value, CharacterPrimitives.FacePose.Incorrect, 56f);
                    }

                    LocalMotionFx.Shake(_host, _targetShakeHandles[i], _targets[i].transform as RectTransform, 0.24f, 8f);
                }
            }

            if (!hasTarget)
            {
                // Timeout — nobody fired. All outlaws get a small, shared
                // flinch instead of one specific hit reaction; none of them
                // switch to Hit, since nobody was actually shot.
                for (var i = 0; i < _targets.Count; i++)
                {
                    if (_targets[i].gameObject.activeSelf)
                    {
                        LocalMotionFx.Shake(_host, _targetShakeHandles[i], _targets[i].transform as RectTransform, 0.18f, 4f);
                    }
                }

                // C8.1d.9 brief section 10's preferred timeout behavior: no
                // player gunshot was fired (none is invented here either),
                // but the correct outlaw still counters as failure
                // punctuation — the same beat a fired wrong answer gets
                // below, just without a preceding player shot.
                _host.StartCoroutine(CountershotRoutine(correctIndex));
            }
            else if (!hit)
            {
                // C8.1d.9: wrong answer — the correct outlaw fires back,
                // replacing the old generic incorrect-answer ding entirely
                // (removed in ClasicoGameHost/ClasicoHud this phase).
                _host.StartCoroutine(CountershotRoutine(correctIndex));
            }
        }

        /// <summary>C8.1d.9: the correct outlaw "fires back" on a wrong
        /// answer or timeout — replaces the old generic incorrect-answer
        /// ding entirely, per manual validation that the outlaw's own red/
        /// green tint and hit reaction already communicate correctness on
        /// their own. Presentation only: <paramref name="correctIndex"/> is
        /// consumed directly from the already-authoritative answer mapping
        /// <see cref="FireSequenceRoutine"/> passes in — never recomputed —
        /// and nothing here is a new aiming/projectile/bullet system, just
        /// a punch on the correct outlaw (reusing the same handle/magnitude
        /// the correct-hit case already uses elsewhere, since the two never
        /// fire in the same sequence), a distinct "enemy" gunshot cue, and
        /// the brief red screen flash. Lands the countershot itself roughly
        /// 0.35-0.45s after <see cref="FireSequenceRoutine"/> began (0.08s
        /// anticipation + 0.12s tracer + this routine's own 0.18s pause),
        /// inside the brief's suggested 0.40-0.55s window.</summary>
        private IEnumerator CountershotRoutine(int correctIndex)
        {
            yield return new WaitForSeconds(0.18f);

            if (_targets[correctIndex].gameObject.activeSelf)
            {
                LocalMotionFx.Punch(_host, _targetPunchHandles[correctIndex], _targets[correctIndex].transform as RectTransform, 0.22f, 1.2f);
            }

            _sfxAudioSource.PlayOneShot(_enemyGunshotClip);
            WesternAudioEvents.Record("EnemyGunshot", _enemyGunshotClip);

            yield return PlayerHitFlashRoutine();
        }

        /// <summary>The countershot's visual "you got hit" punctuation — a
        /// brief red alpha rise-then-fade on <see cref="_playerHitFlash"/>
        /// (plain <c>Image.color</c> alpha, never <c>CanvasGroup</c> or
        /// <c>Image.Type.Filled</c>/<c>Radial</c> — the same reasoning as
        /// <see cref="GunshotFlashRoutine"/>'s own doc-comment references
        /// this codebase's C7-era black-screen bug) plus a small punch on
        /// the whole <c>WesternShootout</c> root. Deliberately restrained
        /// per the brief: peak alpha ~0.32 (within its suggested 0.25-0.40),
        /// clears within ~0.20s, no held red screen, no gore — "you got
        /// hit," not "you died."</summary>
        private IEnumerator PlayerHitFlashRoutine()
        {
            const float riseDuration = 0.05f;
            const float fadeDuration = 0.20f;
            const float peakAlpha = 0.32f;

            LocalMotionFx.Punch(_host, _screenKickHandle, _root, 0.16f, 1.02f);

            var elapsed = 0f;
            while (elapsed < riseDuration)
            {
                elapsed += Time.deltaTime;
                var alpha = Mathf.Lerp(0f, peakAlpha, Mathf.Clamp01(elapsed / riseDuration));
                _playerHitFlash.color = new Color(0.85f, 0.1f, 0.1f, alpha);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                var alpha = Mathf.Lerp(peakAlpha, 0f, Mathf.Clamp01(elapsed / fadeDuration));
                _playerHitFlash.color = new Color(0.85f, 0.1f, 0.1f, alpha);
                yield return null;
            }

            _playerHitFlash.color = new Color(0.85f, 0.1f, 0.1f, 0f);
        }

        /// <summary>C8.1d.3's off-screen shot: a brief muzzle flash at
        /// <see cref="_offscreenShotOrigin"/> plus a short (0.12s) directional
        /// streak toward <paramref name="targetRect"/>'s world position — no
        /// visible gun, no giant projectile, reads and clears quickly. A
        /// fixed-length streak (not stretched to the exact origin-target
        /// distance) deliberately avoids fragile anchored-space geometry;
        /// the direction/arrival read clearly without it.</summary>
        private IEnumerator FireOffscreenShot(RectTransform targetRect)
        {
            var startPos = _offscreenShotOrigin.position;
            var endPos = targetRect.position;
            var diff = endPos - startPos;
            var angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

            _tracer.rotation = Quaternion.Euler(0f, 0f, angle);
            _tracer.gameObject.SetActive(true);

            const float duration = 0.12f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                _tracer.position = Vector3.Lerp(startPos, endPos, t);
                var tracerImage = _tracer.GetComponent<Image>();
                tracerImage.color = new Color(1f, 0.92f, 0.75f, Mathf.Lerp(0.95f, 0f, t));
                yield return null;
            }

            _tracer.gameObject.SetActive(false);
        }

        private void PlayDustPuff(int index)
        {
            _host.StartCoroutine(DustPuffRoutine(index));
        }

        private IEnumerator DustPuffRoutine(int index)
        {
            var image = _dustPuffs[index];
            var rect = image.rectTransform;
            const float duration = 0.3f;
            var elapsed = 0f;
            rect.localScale = Vector3.one * 0.6f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                rect.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.6f, t);
                image.color = new Color(0.85f, 0.72f, 0.5f, Mathf.Lerp(0.7f, 0f, t));
                yield return null;
            }

            image.color = new Color(0.85f, 0.72f, 0.5f, 0f);
            rect.localScale = Vector3.one;
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
            _musicAudioSource.Stop();
        }
    }
}
