using NUnit.Framework;
using Hermit.Runtime;

namespace Hermit.Tests.PlayMode
{
    /// <summary>
    /// Pure decision-logic coverage for the fix to a real gap found in the C6
    /// Windows Development Build: a standalone Player always starts at build
    /// index 0 (00_Bootstrap, camera-only), and nothing advanced it past
    /// that — Editor manual testing never caught it because Editor Play loads
    /// whichever scene is open, not build index 0. C7 changed the
    /// destination from 02_GameplaySandbox to 01_Shell now that Shell is the
    /// real product entry point. See
    /// Docs/C7_SHELL_CLASICO_VISUAL_LANGUAGE.md for the full writeup.
    ///
    /// Only the decision function is tested here (not the actual
    /// SceneManager.LoadScene call) — driving a real scene load from a unit
    /// test would test the Editor/Player scene-loading machinery, not this
    /// class's own logic.
    /// </summary>
    public class BootstrapSceneFlowTests
    {
        [Test]
        public void ShouldAdvance_WhenActiveSceneIsTheEntryScene()
        {
            Assert.IsTrue(BootstrapSceneFlow.ShouldAdvance(BootstrapSceneFlow.EntrySceneName));
        }

        [Test]
        public void ShouldNotAdvance_WhenActiveSceneIsAlreadyTheDestination()
        {
            Assert.IsFalse(BootstrapSceneFlow.ShouldAdvance(BootstrapSceneFlow.DestinationSceneName));
        }

        [Test]
        public void ShouldNotAdvance_ForAnyOtherScene()
        {
            Assert.IsFalse(BootstrapSceneFlow.ShouldAdvance("02_GameplaySandbox"));
            Assert.IsFalse(BootstrapSceneFlow.ShouldAdvance(string.Empty));
            Assert.IsFalse(BootstrapSceneFlow.ShouldAdvance("SomeUnrelatedTestScene"));
        }
    }
}
