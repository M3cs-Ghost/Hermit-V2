using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Hermit.Games;
using Hermit.Games.Clasico;
using Hermit.Games.Clasico.Microgames;
using Hermit.Runtime;
using Hermit.Runtime.GameFramework;

namespace Hermit.Tests.PlayMode
{
    /// <summary>
    /// Exercises the real, composed Shell (ShellInstaller + StartScreenHud +
    /// the same GameHub/GameSelectorHud/ClasicoGameHost stack
    /// GameSessionInstaller uses) end to end, driven by invoking the actual
    /// UI Button.onClick events a player's click would fire. Covers the full
    /// flow from the C9.1 brief: cold boot -> Start Screen -> Press Start ->
    /// Hermit Hub -> Arcade -> Selector -> Clásico -> Results -> Selector ->
    /// Hub. See Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md.
    /// </summary>
    public class ShellPlayModeTests
    {
        private GameObject _root;
        private ShellInstaller _installer;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _root = new GameObject("ShellTest");
            _installer = _root.AddComponent<ShellInstaller>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            yield return null;
        }

        private Button FindButton(string name) => _root.GetComponentsInChildren<Button>(true).First(b => b.name == name);
        private Text FindText(string name) => _root.GetComponentsInChildren<Text>(true).First(t => t.name == name);
        private Image FindImage(string name) => _root.GetComponentsInChildren<Image>(true).First(i => i.name == name);

        /// <summary>C9.1b: PressStartPrompt moved from a plain <see cref="Text"/>
        /// to TMP's <see cref="TextMeshProUGUI"/> (Marcellus) — <see cref="Graphic"/>
        /// is the common base both derive from, so this finds either without
        /// caring which concrete type a given label happens to use.</summary>
        private Graphic FindGraphic(string name) => _root.GetComponentsInChildren<Graphic>(true).First(g => g.name == name);

        private ClasicoSessionDirector GetDirector() => (ClasicoSessionDirector)_installer.FlowController.CurrentEngine;

        private bool IsPlaying() => _installer.FlowController.State == GameLifecycleState.Playing;

        /// <summary>C8.1d.5: state-driven polling instead of a fixed sleep —
        /// see the identical reasoning in ClasicoPlayModeTests.WaitUntil's
        /// own doc-comment (a flat "wait past the worst-case Western
        /// cinematic" constant can no longer also stay safely under a
        /// Balance round's own real decision-timeout window once the
        /// cinematic gate grew past ~5.9s).</summary>
        private IEnumerator WaitUntil(System.Func<bool> condition, float timeoutSeconds, string timeoutMessage)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(condition(), timeoutMessage);
        }

        /// <summary>C9.1's shared test-migration helper — every test that
        /// only cares about what happens *after* the Shell is usable calls
        /// this first, rather than each re-deriving its own wait. State-
        /// driven (polls the real "is the Hub navigation interactable yet"
        /// signal), never a flat WaitForSeconds, so no future Start Screen
        /// timing change ever requires touching every test that merely
        /// needs to get past it — the same lesson the Western Encounter
        /// timing work already established for this project.</summary>
        private IEnumerator EnterShellPastStartScreen()
        {
            FindButton("ConfirmButton").onClick.Invoke();
            yield return WaitUntil(
                () => FindButton("ArcadeHotspot").interactable,
                6f,
                "Hub navigation (ArcadeHotspot) never became interactable within 6s of confirming the Start Screen.");
        }

        /// <summary>C9.2's shared test-migration helper for the short (~0.35s)
        /// Hub -&gt; Arcade Gallery transition (ShellInstaller.TransitionToArcadeRoutine)
        /// — state-driven (waits for the real "Game_clasico" card to
        /// actually become visible), never a flat sleep sized to the
        /// transition's own current duration, so a future timing tweak
        /// there never requires touching every test that merely needs to
        /// get past it.</summary>
        private IEnumerator OpenArcadeGallery()
        {
            FindButton("ArcadeHotspot").onClick.Invoke();
            yield return WaitUntil(
                () => FindButton("Game_clasico").gameObject.activeInHierarchy,
                2f,
                "The Arcade Gallery (Game_clasico) never became visible within 2s of selecting the Arcade hotspot.");
        }

        /// <summary>Answers whichever microgame is currently showing, always
        /// correctly, via the real named control for its archetype — same
        /// helper ClasicoPlayModeTests uses, kept local here since Shell's
        /// own tests only need a generic "play through a session" driver,
        /// not per-archetype assertions (those live in ClasicoPlayModeTests).</summary>
        private IEnumerator AnswerCurrentMicrogame()
        {
            var director = GetDirector();
            switch (director.CurrentArchetype)
            {
                case MicrogameArchetype.AimSelect:
                {
                    var options = director.CurrentClassification.CategoryOptions;
                    var correctIndex = System.Array.IndexOf(options, director.CurrentClassification.CorrectCategory);
                    FindButton($"WesternTarget{correctIndex}").onClick.Invoke();
                    break;
                }

                case MicrogameArchetype.ChooseSide:
                    FindButton(director.CurrentTrueFalse.IsTrue ? "GameShowTrue" : "GameShowFalse").onClick.Invoke();
                    break;

                case MicrogameArchetype.Balance:
                {
                    var challenge = director.CurrentEquation;
                    var steps = Mathf.RoundToInt((challenge.CorrectValue - challenge.StartValue) / challenge.StepSize);
                    var buttonName = steps >= 0 ? "BalanceUp" : "BalanceDown";
                    for (var i = 0; i < Mathf.Abs(steps); i++)
                    {
                        FindButton(buttonName).onClick.Invoke();
                    }

                    FindButton("BalanceConfirm").onClick.Invoke();
                    break;
                }

                default:
                    FindButton($"DetectiveSuspect{director.CurrentErrorDetection.AnomalyIndex}").onClick.Invoke();
                    break;
            }

            yield return WaitUntil(
                () => !IsPlaying() || !GetDirector().IsDecisionPhase,
                5f,
                "Answering did not leave Decision phase (Lock/Feedback never began) within 5s.");

            yield return WaitUntil(
                () => !IsPlaying() || GetDirector().IsDecisionPhase,
                20f,
                "The next round never reached its Decision phase within 20s.");
        }

        private static GameObject AssertValidSelection(string expectedName = null)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.IsNotNull(selected, "Nothing is selected — keyboard Navigate/Submit have nothing to act on.");
            Assert.IsTrue(selected.activeInHierarchy, $"Selected object '{selected.name}' is not active in the hierarchy.");
            if (expectedName != null)
            {
                Assert.AreEqual(expectedName, selected.name);
            }

            return selected;
        }

        [UnityTest]
        public IEnumerator ShellInstaller_ColdBoot_ShowsStartScreen_PressStartVisible_HubNotInteractable()
        {
            Assert.AreEqual(GameLifecycleState.Idle, _installer.FlowController.State);

            var pressStart = FindGraphic("PressStartPrompt");
            Assert.IsTrue(pressStart.gameObject.activeInHierarchy, "PRESS START must be visible in the Title state.");
            Assert.IsTrue(FindText("HermitTitle").gameObject.activeInHierarchy, "The HERMIT title must be visible in the Title state.");

            Assert.IsFalse(FindButton("ArcadeHotspot").interactable, "The Hub's Arcade hotspot must not be interactable before Press Start is confirmed.");

            // Proves the keyboard/gamepad path is wired without simulating
            // real hardware input (this project's tests never do — every
            // existing test invokes .onClick.Invoke() directly regardless
            // of "device"): Submit routes through EventSystem selection, so
            // ConfirmButton being selected here is what makes keyboard/
            // gamepad Submit equivalent to a mouse click on it.
            AssertValidSelection("ConfirmButton");

            yield return null;
        }

        [UnityTest]
        public IEnumerator MouseConfirm_StartsTransition_HubBecomesInteractiveWithinApprovedTimeline()
        {
            var startTime = Time.realtimeSinceStartup;
            FindButton("ConfirmButton").onClick.Invoke();

            yield return WaitUntil(() => FindButton("ArcadeHotspot").interactable, 6f, "ArcadeHotspot never became interactable within 6s.");
            var elapsed = Time.realtimeSinceStartup - startTime;

            // The approved timeline targets ~3.4-3.8s (never Western-length,
            // never shortened to ~2.5s) — checked loosely (real-clock, one
            // real invocation) rather than pinned to the exact constant, so
            // this test survives small future timing tweaks without
            // becoming brittle.
            Assert.Greater(elapsed, 3.0f, $"The transition finished suspiciously fast ({elapsed:0.00}s) — it must not be shortened toward ~2.5s.");
            Assert.Less(elapsed, 6f, $"The transition took too long ({elapsed:0.00}s) to reach the Hub.");

            Assert.IsFalse(FindGraphic("PressStartPrompt").gameObject.activeInHierarchy, "PRESS START must be hidden once the transition finishes.");
            Assert.IsFalse(FindText("HermitTitle").gameObject.activeInHierarchy, "The HERMIT title must be hidden once the transition finishes.");
            Assert.IsFalse(FindButton("ConfirmButton").gameObject.activeInHierarchy, "ConfirmButton must be retired once Title ends.");

            var hubSprite = Resources.Load<Sprite>("Art/Shell/Hub/Hermit_Hub_01");
            Assume.That(hubSprite, Is.Not.Null);
            Assert.AreSame(hubSprite, FindImage("SceneArt").sprite, "The scene art must have swapped to the Hub sprite by the time the Hub is interactive.");

            // No black-frame regression: neither overlay may be left
            // covering the screen.
            Assert.Less(FindImage("FogOverlay").color.a, 0.01f, "FogOverlay must have fully cleared.");
            Assert.Less(FindImage("BloomPulse").color.a, 0.01f, "BloomPulse must have fully cleared.");
        }

        [UnityTest]
        public IEnumerator RepeatedConfirmDuringTransition_IsIgnored()
        {
            var confirmButton = FindButton("ConfirmButton");
            confirmButton.onClick.Invoke();

            // ConfirmButton disables itself synchronously inside the same
            // handler that starts the transition — a second invocation a
            // frame later must already be a no-op, proving input is locked
            // structurally (not merely debounced by a timer).
            yield return null;
            Assert.IsFalse(confirmButton.interactable, "ConfirmButton must already be non-interactable on the frame after the first confirm.");
            confirmButton.onClick.Invoke();
            confirmButton.onClick.Invoke();

            yield return WaitUntil(() => FindButton("ArcadeHotspot").interactable, 6f, "ArcadeHotspot never became interactable within 6s.");

            // If the repeated confirms had each queued their own transition,
            // the Hub's own reveal state would be corrupted (e.g. multiple
            // overlapping nav-reveal fades). A clean, fully-opaque nav group
            // is the observable proof only one transition ever ran.
            var navGroup = _root.GetComponentsInChildren<CanvasGroup>(true).First(g => g.gameObject.name == "HubNavigation");
            Assert.AreEqual(1f, navGroup.alpha, 0.01f, "Hub navigation must have settled at full opacity from exactly one transition.");
        }

        [UnityTest]
        public IEnumerator ArtSwap_OnlyHappensBehindSufficientFogCoverage()
        {
            var startScreenSprite = Resources.Load<Sprite>("Art/Shell/StartScreen/Hermit_StartScreen_01");
            var hubSprite = Resources.Load<Sprite>("Art/Shell/Hub/Hermit_Hub_01");
            Assume.That(startScreenSprite, Is.Not.Null);
            Assume.That(hubSprite, Is.Not.Null);

            FindButton("ConfirmButton").onClick.Invoke();

            var sceneArt = FindImage("SceneArt");
            var fogOverlay = FindImage("FogOverlay");
            var swapFogAlpha = -1f;

            var deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline && sceneArt.sprite != hubSprite)
            {
                yield return null;
            }

            Assert.AreSame(hubSprite, sceneArt.sprite, "The scene art never swapped to the Hub sprite within 6s.");
            swapFogAlpha = fogOverlay.color.a;

            Assert.Greater(swapFogAlpha, 0.30f,
                $"The art swap must happen while the fog overlay provides meaningful coverage (observed alpha={swapFogAlpha:0.00}) — the two candidates are compositionally mismatched for a visible crossfade.");
            Assert.Less(swapFogAlpha, 0.95f,
                $"The fog overlay must never fully obscure the scene (observed alpha={swapFogAlpha:0.00}) — a near-opaque cover would read as a black-frame/teleport, not an 'opening'.");
        }

        [UnityTest]
        public IEnumerator ClickingArcadeHotspot_ShowsTheArcadeGallery_WithClasicoFeatured()
        {
            yield return EnterShellPastStartScreen();

            yield return OpenArcadeGallery();

            Assert.IsNotNull(FindButton("Game_clasico"));
            Assert.IsTrue(FindButton("Game_clasico").interactable, "The featured Clásico card must be interactable.");
            AssertValidSelection("Game_clasico");
        }

        /// <summary>C9.2: future slots are purely-visual placeholders (no
        /// backing GameDefinition at all today, since the catalog has no
        /// disabled entries) — Submit on one must never start a game or
        /// disturb the real, playable Clásico card.</summary>
        [UnityTest]
        public IEnumerator FutureSlots_AreNotPlayable_ClickingThemNeverLaunchesAGame()
        {
            yield return EnterShellPastStartScreen();
            yield return OpenArcadeGallery();

            var stateBefore = _installer.FlowController.State;
            FindButton("FutureSlot_0").onClick.Invoke();
            yield return null;

            Assert.AreEqual(stateBefore, _installer.FlowController.State, "Clicking a future slot must never start a game.");
            Assert.IsTrue(FindButton("Game_clasico").interactable, "Selecting a future slot must not disturb the featured Clásico card.");
        }

        [UnityTest]
        public IEnumerator SelectorBack_ReturnsToHub_StartScreenDoesNotReplay()
        {
            yield return EnterShellPastStartScreen();

            yield return OpenArcadeGallery();

            FindButton("BackButton").onClick.Invoke();
            yield return null;

            Assert.IsTrue(FindButton("ArcadeHotspot").interactable, "The Arcade hotspot must be interactable again after returning from the Selector.");
            AssertValidSelection("ArcadeHotspot");

            Assert.IsFalse(FindGraphic("PressStartPrompt").gameObject.activeInHierarchy, "The Start Screen must never replay when returning to the Hub.");
            Assert.IsFalse(FindButton("ConfirmButton").gameObject.activeInHierarchy, "ConfirmButton must stay retired — Start Screen is cold-boot entry only.");
        }

        [UnityTest]
        public IEnumerator LaunchingClasicoFromShell_StartsAGame()
        {
            yield return EnterShellPastStartScreen();

            yield return OpenArcadeGallery();

            FindButton("Game_clasico").onClick.Invoke();
            yield return WaitUntil(() => GetDirector().IsDecisionPhase, 20f, "The first microgame never reached its Decision phase within 20s.");

            Assert.AreEqual(GameLifecycleState.Playing, _installer.FlowController.State);
            Assert.IsFalse(GetDirector().IsCountingDown);
            Assert.IsTrue(GetDirector().IsDecisionPhase);
        }

        [UnityTest]
        public IEnumerator FullLoop_ShellToClasicoToResultsToSelectorToHub()
        {
            yield return EnterShellPastStartScreen();

            // Shell Hub -> Arcade -> Selector -> Clásico
            yield return OpenArcadeGallery();
            FindButton("Game_clasico").onClick.Invoke();
            yield return WaitUntil(() => GetDirector().IsDecisionPhase, 20f, "The first microgame never reached its Decision phase within 20s.");

            // Play through to Results.
            const int maxMicrogames = 12;
            var iterations = 0;
            while (_installer.FlowController.State == GameLifecycleState.Playing && iterations < maxMicrogames)
            {
                yield return AnswerCurrentMicrogame();
                iterations++;
            }

            Assert.AreEqual(GameLifecycleState.Results, _installer.FlowController.State);

            // Results -> Selector.
            FindButton("ExitButton").onClick.Invoke();
            yield return null;
            Assert.AreEqual(GameLifecycleState.Idle, _installer.FlowController.State);
            Assert.IsNotNull(FindButton("Game_clasico"));
            AssertValidSelection("Game_clasico");

            // Selector -> Hub.
            FindButton("BackButton").onClick.Invoke();
            yield return null;
            Assert.IsTrue(FindButton("ArcadeHotspot").interactable);
            AssertValidSelection("ArcadeHotspot");
            Assert.IsFalse(FindGraphic("PressStartPrompt").gameObject.activeInHierarchy, "The Start Screen must not replay after a full game loop.");
        }

        [UnityTest]
        public IEnumerator NonFinalDestinations_AreFocusableButShowComingSoon_NeverANewScreen()
        {
            yield return EnterShellPastStartScreen();

            var academia = FindButton("AcademiaHotspot");
            Assert.IsTrue(academia.interactable, "Academia must be focusable once the Hub is ready, even though it has no content yet.");

            academia.onClick.Invoke();
            yield return null;

            var comingSoon = FindText("ComingSoonLabel");
            Assert.IsTrue(comingSoon.gameObject.activeInHierarchy, "A 'Próximamente' cue must appear for a non-final destination.");
            Assert.IsFalse(string.IsNullOrEmpty(comingSoon.text));

            // Never a new screen: the Hub navigation itself must still be
            // the visible/interactive layer, not replaced by anything.
            Assert.IsTrue(FindButton("ArcadeHotspot").interactable, "Selecting a non-final destination must not disturb the rest of the Hub.");
        }

        /// <summary>C9.1a: structural coverage for the destination-label
        /// typography pass — deliberately NOT a screenshot/pixel test (per
        /// the brief's explicit "do not add tests for exact font pixels or
        /// screen positions"). Only checks the things that would actually
        /// break gameplay/navigation if the TMP rewrite went wrong: each
        /// hotspot still exists, is still interactable, and still carries
        /// the correct destination text.</summary>
        [UnityTest]
        public IEnumerator HotspotLabels_ExistWithCorrectTextForEachDestination()
        {
            yield return EnterShellPastStartScreen();

            var expected = new (string hotspot, string label)[]
            {
                ("ArcadeHotspot", "ARCADE"),
                ("AcademiaHotspot", "ACADEMIA"),
                ("CodexHotspot", "CODEX"),
                ("HistorietasHotspot", "HISTORIETAS"),
            };

            foreach (var (hotspotName, expectedLabel) in expected)
            {
                var button = FindButton(hotspotName);
                Assert.IsTrue(button.interactable, $"{hotspotName} must remain interactable.");

                var label = button.GetComponentInChildren<TMP_Text>(true);
                Assert.IsNotNull(label, $"{hotspotName} must have a destination label.");
                Assert.AreEqual(expectedLabel, label.text, $"{hotspotName}'s label text must match its own destination, not another hotspot's.");
            }
        }

        [UnityTest]
        public IEnumerator F1DebugPanelToggle_DoesNotInterfereWithShell()
        {
            yield return EnterShellPastStartScreen();

            var debugPanel = Object.FindFirstObjectByType<C4DebugPanel>();

            Assert.IsNotNull(
                debugPanel,
                "C4DebugPanel should exist because HermitRuntimeInstaller runs globally during PlayMode."
            );

            Assert.IsTrue(
                FindButton("ArcadeHotspot").interactable,
                "Shell Hub must remain usable while C4DebugPanel exists."
            );

            AssertValidSelection("ArcadeHotspot");

            yield return null;
        }
    }
}
