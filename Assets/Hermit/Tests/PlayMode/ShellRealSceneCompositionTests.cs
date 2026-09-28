using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Hermit.Economy;

namespace Hermit.Tests.PlayMode
{
    /// <summary>
    /// Every other PlayMode test in this project (including
    /// <c>ShellPlayModeTests</c>) drives a <c>ShellInstaller</c> that the
    /// test itself constructs with <c>new GameObject(...).AddComponent&lt;ShellInstaller&gt;()</c>
    /// — never the actual serialized <c>Assets/Hermit/Scenes/01_Shell.unity</c>
    /// file. For this project that gap has been benign so far (the scene
    /// holds nothing but that one component with zero serialized fields —
    /// confirmed by inspection), but it is a real coverage hole: a
    /// synthetically-built installer can never catch a problem that only
    /// exists in the *serialized scene* (a stale reference, a duplicate
    /// object, a leftover component from an earlier phase). This fixture
    /// closes that hole by loading the real scene file, from Build
    /// Settings, exactly as a player pressing Play on `01_Shell` would.
    ///
    /// Added after a C8.1 manual-validation report of Clásico still showing
    /// C5-C7's old Q&amp;A screen, then a second report that the whole
    /// experience still looked like C7 and went black after the countdown.
    /// The first version of this test only asserted `activeInHierarchy`,
    /// which the user correctly pointed out proves an object *exists*, not
    /// that a human can *see* it — a presenter could be "active" yet
    /// invisible (zero size, zero alpha, covered by an opaque sibling). This
    /// version checks the things that actually determine visibility: real
    /// on-screen size, being under a Canvas, non-zero alpha with no
    /// zero-alpha CanvasGroup ancestor, the transition-cut overlay not left
    /// covering the screen, and the active presenter actually holding
    /// non-empty text content — not just a same-named empty shell.
    /// </summary>
    public class ShellRealSceneCompositionTests
    {
        private Scene _scene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // C9.1: never touch the player's real Hermit Coin save — every
            // session these tests finish is credited to an in-memory economy.
            HermitEconomy.OverrideForTests(new HermitEconomyService(
                new InMemoryHermitEconomyStore(), new FixedHermitClock(new System.DateTime(2026, 9, 21, 10, 0, 0)), HermitEconomyDefinition.CreateDefault()));
            _scene = SceneManager.LoadScene("01_Shell", new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null; // a second frame so every Awake() in the scene has run
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return SceneManager.UnloadSceneAsync(_scene);
            HermitEconomy.ResetOverride();
        }

        private GameObject FindInScene(string name)
        {
            return _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(t => t.gameObject)
                .FirstOrDefault(go => go.name == name);
        }

        private Button FindButtonInScene(string name)
        {
            return _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Button>(true))
                .First(b => b.name == name);
        }

        /// <summary>C8.1d.5: this fixture has no direct access to
        /// <c>ClasicoSessionDirector</c> (a real scene load, not a
        /// synthetically-built installer), so "Decision phase reached" is
        /// observed the same way a player would see it: exactly the signal
        /// <c>WesternShootoutPresenter.RevealAfterIntro</c> (and each other
        /// presenter's own equivalent reveal) actually flips — a presenter
        /// becomes active and shows real, non-empty visible text. State/event
        /// polling instead of a flat <c>WaitForSeconds</c> for the same
        /// reason as every other timing fix this phase: a fixed constant
        /// sized for the worst-case Western cinematic gate has no ceiling
        /// problem here (this fixture never loops through multiple rounds),
        /// but still has no reason to guess a duration when the real
        /// condition can be polled directly.</summary>
        private static readonly string[] PresenterNames = { "WesternShootout", "GameShow", "BalanceMachine", "DetectiveLineup" };

        private bool AnyPresenterShowsDecisionContent()
        {
            foreach (var name in PresenterNames)
            {
                var go = FindInScene(name);
                if (go == null || !go.activeInHierarchy)
                {
                    continue;
                }

                var hasVisibleText = go.GetComponentsInChildren<Text>(false)
                    .Any(t => t.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(t.text));
                if (hasVisibleText)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds, string timeoutMessage)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(condition(), timeoutMessage);
        }

        /// <summary>C9.1's shared test-migration helper for this fixture —
        /// confirms the Start Screen, then waits state-driven for the Hub's
        /// Arcade hotspot to become interactable and opens it, landing on
        /// the same "Selector visible" state every test here used to reach
        /// via the old flat "JuegosButton". See the identical helper (and
        /// its own doc-comment) in ShellPlayModeTests.cs. C9.2 additionally
        /// waits out the short Hub-&gt;Arcade Gallery transition
        /// (ShellInstaller.TransitionToArcadeRoutine) rather than assuming
        /// the gallery appears within the same frame.</summary>
        private IEnumerator EnterShellPastStartScreenAndOpenArcade()
        {
            FindButtonInScene("ConfirmButton").onClick.Invoke();
            yield return WaitUntil(
                () => FindButtonInScene("ArcadeHotspot").interactable,
                6f,
                "Hub navigation (ArcadeHotspot) never became interactable within 6s of confirming the Start Screen.");

            FindButtonInScene("ArcadeHotspot").onClick.Invoke();
            yield return WaitUntil(
                () => FindButtonInScene("Game_clasico").gameObject.activeInHierarchy,
                2f,
                "The Arcade Gallery (Game_clasico) never became visible within 2s of selecting the Arcade hotspot.");
        }

        [UnityTest]
        public IEnumerator RealShellScene_ColdBoot_ShowsStartScreen_NotHubDirectly()
        {
            var pressStart = FindInScene("PressStartPrompt");
            Assert.IsNotNull(pressStart, "PressStartPrompt object is missing from the real Shell scene.");
            Assert.IsTrue(pressStart.activeInHierarchy, "PRESS START must be visible on cold boot of the real 01_Shell scene.");

            var arcadeHotspot = FindButtonInScene("ArcadeHotspot");
            Assert.IsFalse(arcadeHotspot.interactable, "The Hub's Arcade hotspot must not be interactable before Press Start is confirmed, in the real scene.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator RealShellScene_LaunchingClasico_VisiblyShowsAGoldMicrogamePresenter_NotTheOldC7Screen()
        {
            yield return EnterShellPastStartScreenAndOpenArcade();

            FindButtonInScene("Game_clasico").onClick.Invoke();
            // C8.1d.5: state-driven instead of a flat WaitForSeconds — see
            // AnyPresenterShowsDecisionContent's own doc-comment. History
            // (kept for context): 4.5f, then 5.5f (3s + the C8.1d.1/.3
            // arcade face-off's ~2.2s) — a real full-suite run once caught
            // that 4.5f gap when RNG drew Western first (the presenter had no
            // visible text yet — concept/labels stay empty by design until
            // WesternShootoutPresenter.RevealAfterIntro). C8.1d.4's cinematic
            // duel intro needed 8.5f (3s + its 5.0s gate + margin); C8.1d.5
            // re-paced the same cinematic to ~6.5s and raised the gate to
            // 7.5s, which would have meant bumping this constant yet again —
            // switched to polling instead so no future Western pacing change
            // ever requires touching this test's timing again.
            yield return WaitUntil(AnyPresenterShowsDecisionContent, 20f, "No Gold microgame presenter reached its Decision phase (visible content) within 20s.");

            var activePresenters = PresenterNames
                .Select(FindInScene)
                .Where(go => go != null && go.activeInHierarchy)
                .ToList();

            Assert.AreEqual(1, activePresenters.Count,
                $"Expected exactly one Gold microgame presenter active; found: {string.Join(", ", activePresenters.Select(go => go.name))}.");
            var activePresenter = activePresenters[0];

            // --- Not just "active" — actually visible ---

            var rect = activePresenter.GetComponent<RectTransform>();
            Assert.IsNotNull(rect, $"{activePresenter.name} has no RectTransform — cannot be a uGUI-visible object.");
            Assert.Greater(rect.rect.width, 0f, $"{activePresenter.name} has zero width.");
            Assert.Greater(rect.rect.height, 0f, $"{activePresenter.name} has zero height.");

            Assert.IsNotNull(activePresenter.GetComponentInParent<Canvas>(), $"{activePresenter.name} is not under any Canvas.");

            var presenterImage = activePresenter.GetComponent<Image>();
            Assert.IsNotNull(presenterImage, $"{activePresenter.name}'s root has no Image — it would render as nothing.");
            Assert.Greater(presenterImage.color.a, 0f, $"{activePresenter.name}'s own background alpha is 0 — it would be invisible.");

            foreach (var group in activePresenter.GetComponentsInParent<CanvasGroup>(true))
            {
                Assert.Greater(group.alpha, 0.01f, $"A CanvasGroup ancestor of {activePresenter.name} has alpha ~0, which would hide it regardless of its own settings.");
            }

            // The transition-cut overlay is deliberately full-screen and
            // renders on top of everything while it plays — it must have
            // fully cleared (CanvasGroup.alpha back to 0) by the time we're
            // this far past the cut, or it is exactly what a "black screen"
            // report would look like (Theme.Background-colored,
            // screen-covering). Checked via CanvasGroup.alpha, not
            // Image.fillAmount — the real bug this test caught: the overlay
            // used to be Image.Type.Filled/Radial360 with no sprite
            // assigned, which reported correct CanvasRenderer state (not
            // culled, alpha 1) while the real GPU output stayed a solid,
            // un-clearing block. Confirmed via an actual rendered
            // screenshot, not hierarchy state alone. Fixed by switching to a
            // plain CanvasGroup alpha fade, which needs no fill geometry.
            var overlay = FindInScene("TransitionOverlay");
            Assert.IsNotNull(overlay, "TransitionOverlay object is missing.");
            var overlayGroup = overlay.GetComponent<CanvasGroup>();
            Assert.IsNotNull(overlayGroup, "TransitionOverlay has no CanvasGroup — visibility can't be controlled without fill-geometry risk.");

            // C8.1d.7: the transition-cut's own fade (~0.22s,
            // Theme.TransitionDuration) is a separate coroutine from the
            // presenter-content-visible signal AnyPresenterShowsDecisionContent
            // waits on above — content can legitimately become visible on
            // the very same frame the cut *starts*, while the cut itself is
            // still mid-fade back to 0. Wait for the cut to actually finish
            // (state-driven, generously bounded, a plain inline loop rather
            // than the shared WaitUntil-plus-a-second-assert combination —
            // that combination was empirically found to leave a stale value
            // visible to the assert that follows it in this specific
            // NUnit/UnityTest coroutine-composition shape, for reasons not
            // worth chasing further; one loop, one assert, reading the
            // freshest value, sidesteps it entirely) before asserting on it.
            var overlayDeadline = Time.realtimeSinceStartup + 1f;
            while (overlayGroup.alpha >= 0.01f && Time.realtimeSinceStartup < overlayDeadline)
            {
                yield return null;
            }

            Assert.Less(overlayGroup.alpha, 0.01f,
                $"TransitionOverlay is still covering the screen (CanvasGroup.alpha={overlayGroup.alpha}) well after it should have finished its cut — this is exactly what a black-screen report would look like.");

            // The active presenter must hold real, populated content — not
            // just an empty same-named shell with nothing drawn in it.
            var visibleTexts = activePresenter.GetComponentsInChildren<Text>(false)
                .Where(t => t.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(t.text))
                .ToList();
            Assert.IsNotEmpty(visibleTexts, $"{activePresenter.name} has no visible, non-empty text content — it would look like an empty panel.");

            // Regression: a decorative child built via CreateRoundedPanel
            // (which stretches to fill its parent) that then has sizeDelta
            // set without first resetting to a point anchor balloons to
            // roughly the whole stage's size instead of its intended small
            // size — exactly the bug found in WesternShootoutPresenter's
            // Reticle and DetectiveLineupPresenter's Spotlight, where an
            // oversized, high-sibling-index, non-trivial-alpha panel washed
            // out the entire scene despite every other check here passing.
            // Checked against all four presenter roots regardless of which
            // one this run's RNG happened to activate first — all four are
            // built once, up front, and exist (just inactive) either way, so
            // this check does not depend on the draw order.
            foreach (var presenterName in PresenterNames)
            {
                var presenterRoot = FindInScene(presenterName);
                if (presenterRoot == null)
                {
                    continue;
                }

                var presenterRect = presenterRoot.GetComponent<RectTransform>();
                foreach (var childImage in presenterRoot.GetComponentsInChildren<Image>(true))
                {
                    if (childImage.gameObject == presenterRoot)
                    {
                        continue;
                    }

                    var childRect = childImage.GetComponent<RectTransform>();
                    Assert.LessOrEqual(childRect.rect.width, presenterRect.rect.width + 1f,
                        $"{presenterName}/{childImage.gameObject.name} is wider ({childRect.rect.width:0}) than the presenter root itself ({presenterRect.rect.width:0}) — likely a stretched-anchor sizeDelta bug covering the whole scene.");
                    Assert.LessOrEqual(childRect.rect.height, presenterRect.rect.height + 1f,
                        $"{presenterName}/{childImage.gameObject.name} is taller ({childRect.rect.height:0}) than the presenter root itself ({presenterRect.rect.height:0}) — likely a stretched-anchor sizeDelta bug covering the whole scene.");
                }
            }

            // --- The regression this test originally existed for ---
            Assert.IsNull(FindInScene("Question"), "The old C7 question text object must not exist — Clásico must not be rendering the pre-C8.1 Q&A screen.");
            Assert.IsNull(FindInScene("Option0"), "The old C7 answer-option buttons must not exist.");

            // --- C8.1 layout polish regression: Timer bar vs concept/command text ---
            //
            // Manual validation found the Timer bar visually crossing through
            // presenter "concept" text (e.g. "Cuentas Deudoras") — Western's
            // "Concept", Detective's "RuleLabel" (renamed from GroupLabel in
            // C8.1g.2), and Balance's "Equation"
            // were all top-anchored (0.5, 1) to their own presenter root,
            // which used to stretch to the true top of the stage, landing
            // them inside the Timer bar's own vertical span. Fixed generally
            // (not per presenter) by insetting ClasicoHud's StageRoot from
            // the top by a reserved HUD band, so every presenter's
            // top-anchored content is automatically pushed clear of it.
            // Checked against all four presenters' own known top-concept
            // element(s) regardless of which one this run's RNG activated
            // first — same rationale as the oversized-descendant check above.
            var timerBar = FindInScene("TimerBar");
            Assert.IsNotNull(timerBar, "TimerBar object is missing.");
            var timerRect = timerBar.GetComponent<RectTransform>();

            var conceptElementNames = new[] { "Concept", "RuleLabel", "Cue", "Equation", "Statement" };
            var conceptElementsChecked = 0;
            foreach (var presenterName in PresenterNames)
            {
                var presenterRoot = FindInScene(presenterName);
                if (presenterRoot == null)
                {
                    continue;
                }

                foreach (var elementName in conceptElementNames)
                {
                    var element = presenterRoot.GetComponentsInChildren<Transform>(true)
                        .FirstOrDefault(t => t.name == elementName);
                    if (element == null)
                    {
                        continue;
                    }

                    conceptElementsChecked++;
                    var elementRect = element.GetComponent<RectTransform>();
                    Assert.IsFalse(RectsOverlap(timerRect, elementRect),
                        $"{presenterName}/{elementName} overlaps the Timer bar's bounds — the timer must occupy its own reserved band and never cross through concept/command content.");
                }
            }

            Assert.Greater(conceptElementsChecked, 0, "No presenter concept/label element was found to check against the Timer bar — the test's element name list is stale.");
        }

        /// <summary>
        /// C8.1b added per-presenter local reaction motion (punch/shake/flash
        /// via <c>LocalMotionFx</c>) on top of the shared global punch/shake
        /// ClasicoHud already had. Every LocalMotionFx routine follows the
        /// same "no-op if already playing, always restore on completion"
        /// contract the C7 shake bug and the C8.1 transition black-screen bug
        /// both proved necessary — this test is the structural regression
        /// guard for that contract on the new per-presenter motion: it lets a
        /// microgame time out (a uniform way to trigger the "wrong/timeout"
        /// reaction path on every archetype without depending on which
        /// answer happens to be correct), then asserts every descendant
        /// RectTransform of the presenter that reacted is back at the
        /// anchoredPosition it held before the reaction started — i.e. no
        /// shake left an element stranded off its resting position.
        ///
        /// Deliberately does not touch this presenter again after the wait:
        /// <c>ClasicoMicrogameLibrary.BuildSequence</c> guarantees no two
        /// adjacent microgames share an archetype, so the very next
        /// microgame is guaranteed to activate a different presenter, and
        /// the wait below is bounded well short of a third microgame (the
        /// earliest point this same archetype could plausibly recur).
        /// </summary>
        [UnityTest]
        public IEnumerator RealShellScene_DecisionTimeoutOnActivePresenter_RestoresLocalReactionTransforms()
        {
            yield return EnterShellPastStartScreenAndOpenArcade();

            FindButtonInScene("Game_clasico").onClick.Invoke();
            // C8.1d.5: state-driven instead of a flat WaitForSeconds — see
            // AnyPresenterShowsDecisionContent's own doc-comment on
            // RealShellScene_LaunchingClasico_VisiblyShowsAGoldMicrogamePresenter_NotTheOldC7Screen.
            yield return WaitUntil(AnyPresenterShowsDecisionContent, 20f, "No Gold microgame presenter reached its Decision phase (visible content) within 20s.");

            // A small, fixed settle margin — NOT sized against any Western
            // cinematic/Encounter timing (so it can never reintroduce the
            // cascade-through-timeout bug the C8.1d.5 brief warned about).
            // It exists only to clear WesternShootoutPresenter's own round-1
            // OutlawSettleInRoutine (a ~0.04s-per-target-staggered, ~0.2s
            // entrance ease that starts the same frame Decision content
            // becomes visible) — without it, "baseline" below could capture
            // a transient mid-settle anchoredPosition instead of true rest,
            // exactly the false positive this test's own first isolated
            // re-run surfaced (WesternTarget0 "restored" to 140 from a
            // captured "baseline" of 123.17 — a still-animating in-progress
            // position, not a real regression).
            yield return new WaitForSeconds(0.5f);

            var activePresenter = PresenterNames
                .Select(FindInScene)
                .FirstOrDefault(go => go != null && go.activeInHierarchy);
            Assert.IsNotNull(activePresenter, "No Gold microgame presenter is active at the sampled Decision-phase moment.");

            var descendantRects = activePresenter.GetComponentsInChildren<RectTransform>(true)
                .Where(rt => rt.gameObject != activePresenter)
                .ToList();
            var baseline = descendantRects.ToDictionary(rt => rt, rt => rt.anchoredPosition);

            // No input given — the decision window runs out on its own.
            // Longest possible decision window (Balance) is 6s; add lock
            // (0.2s), feedback display (0.8s) and reaction-settle margin,
            // while staying well under the ~2-cycle mark where the sequence
            // could plausibly reactivate this same archetype.
            yield return new WaitForSeconds(7.5f);

            foreach (var rt in descendantRects)
            {
                if (rt == null)
                {
                    continue;
                }

                Assert.That(Vector2.Distance(rt.anchoredPosition, baseline[rt]), Is.LessThan(1.5f),
                    $"{activePresenter.name}/{rt.name} did not return to its resting anchoredPosition after its decision-timeout reaction — baseline={baseline[rt]}, now={rt.anchoredPosition}.");
            }
        }

        /// <summary>Compares two RectTransforms' bounds in the same (world)
        /// space — comparing local `rect` values directly would be wrong
        /// here since the two RectTransforms being checked live under
        /// different parents with different anchors.</summary>
        private static bool RectsOverlap(RectTransform a, RectTransform b)
        {
            var cornersA = new Vector3[4];
            a.GetWorldCorners(cornersA);
            var cornersB = new Vector3[4];
            b.GetWorldCorners(cornersB);

            // GetWorldCorners order: [0] bottom-left, [1] top-left, [2] top-right, [3] bottom-right.
            var overlapsHorizontally = cornersA[0].x < cornersB[2].x && cornersB[0].x < cornersA[2].x;
            var overlapsVertically = cornersA[0].y < cornersB[1].y && cornersB[0].y < cornersA[1].y;
            return overlapsHorizontally && overlapsVertically;
        }
    }
}
