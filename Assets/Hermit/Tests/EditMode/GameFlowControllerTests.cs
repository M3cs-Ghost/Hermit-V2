using System;
using System.Collections.Generic;
using NUnit.Framework;
using Hermit.Games;
using Hermit.Games.Analytics;
using Hermit.Tests.EditMode.Fakes;

namespace Hermit.Tests.EditMode
{
    public class GameFlowControllerTests
    {
        private static GameContext NewContext() => new GameContext(NullGameAnalyticsSink.Instance, new Random(0));

        /// <summary>Builds a registration for a fresh FakeGameDefinition, along with
        /// the list every engine it ever creates is appended to (one entry per
        /// Start/Restart call), so tests can assert on engine-creation counts and
        /// per-instance call counts.</summary>
        private static (GameRegistration registration, List<FakeGameEngine> engines) NewFakeRegistration(int ticksToFinish)
        {
            var definition = FakeGameDefinition.CreateInMemory("fake_game", "Fake Game");
            var engines = new List<FakeGameEngine>();
            var registration = new GameRegistration(definition, () =>
            {
                var engine = new FakeGameEngine(ticksToFinish);
                engines.Add(engine);
                return engine;
            });
            return (registration, engines);
        }

        [Test]
        public void Start_TransitionsIdleToPlaying_AndCreatesASession()
        {
            var controller = new GameFlowController();
            var (registration, _) = NewFakeRegistration(3);

            controller.Start(registration, NewContext());

            Assert.AreEqual(GameLifecycleState.Playing, controller.State);
            Assert.IsNotNull(controller.CurrentSession);
            Assert.AreEqual("fake_game", controller.CurrentSession.GameId);
        }

        [Test]
        public void Tick_KeepsPlayingUntilEngineFinishes_ThenMovesToResults()
        {
            var controller = new GameFlowController();
            var (registration, _) = NewFakeRegistration(3);
            controller.Start(registration, NewContext());

            controller.Tick(0.1f);
            Assert.AreEqual(GameLifecycleState.Playing, controller.State);

            controller.Tick(0.1f);
            Assert.AreEqual(GameLifecycleState.Playing, controller.State);

            controller.Tick(0.1f);
            Assert.AreEqual(GameLifecycleState.Results, controller.State);
            Assert.IsNotNull(controller.LastResult);
            Assert.IsTrue(controller.LastResult.Completed);
            Assert.AreEqual(3, controller.LastResult.Score);
        }

        [Test]
        public void ResultReady_FiresExactlyOnce_WithTheFinalResult()
        {
            var controller = new GameFlowController();
            var (registration, _) = NewFakeRegistration(1);
            GameResult received = null;
            var fireCount = 0;
            controller.ResultReady += r => { received = r; fireCount++; };

            controller.Start(registration, NewContext());
            controller.Tick(0.1f);

            Assert.AreEqual(1, fireCount);
            Assert.AreSame(controller.LastResult, received);
        }

        [Test]
        public void Abort_WhilePlaying_ProducesAnIncompleteResult()
        {
            var controller = new GameFlowController();
            var (registration, _) = NewFakeRegistration(10);
            controller.Start(registration, NewContext());

            controller.Tick(0.1f);
            controller.Abort();

            Assert.AreEqual(GameLifecycleState.Results, controller.State);
            Assert.IsFalse(controller.LastResult.Completed);
        }

        [Test]
        public void Abort_WhileNotPlaying_IsANoOp()
        {
            var controller = new GameFlowController();
            controller.Abort();
            Assert.AreEqual(GameLifecycleState.Idle, controller.State);
        }

        [Test]
        public void AcknowledgeResults_ReturnsToIdle_AndAllowsStartingAgain()
        {
            var controller = new GameFlowController();
            var (registration, _) = NewFakeRegistration(1);
            controller.Start(registration, NewContext());
            controller.Tick(0.1f);

            controller.AcknowledgeResults();

            Assert.AreEqual(GameLifecycleState.Idle, controller.State);
            Assert.IsNull(controller.CurrentSession);

            controller.Start(registration, NewContext());
            Assert.AreEqual(GameLifecycleState.Playing, controller.State);
        }

        [Test]
        public void Restart_FromResults_CreatesABrandNewEngineAndFreshSession()
        {
            var controller = new GameFlowController();
            var (registration, engines) = NewFakeRegistration(1);
            controller.Start(registration, NewContext());
            var firstSessionId = controller.CurrentSession.SessionId;
            controller.Tick(0.1f);

            controller.Restart();

            Assert.AreEqual(GameLifecycleState.Playing, controller.State);
            Assert.AreNotEqual(firstSessionId, controller.CurrentSession.SessionId);
            Assert.AreEqual(0, controller.CurrentSession.Score);
            Assert.AreEqual(2, engines.Count, "Restart must create a fresh engine instance, not reuse the finished one.");
        }

        [Test]
        public void Cleanup_IsCalledExactlyOnce_OnFinish()
        {
            var controller = new GameFlowController();
            var (registration, engines) = NewFakeRegistration(1);
            controller.Start(registration, NewContext());
            controller.Tick(0.1f);

            Assert.AreEqual(1, engines[0].CleanupCallCount);
        }

        [Test]
        public void Start_WhileAlreadyPlaying_Throws()
        {
            var controller = new GameFlowController();
            var (registration, _) = NewFakeRegistration(10);
            controller.Start(registration, NewContext());

            Assert.Throws<InvalidOperationException>(() => controller.Start(registration, NewContext()));
        }

        [Test]
        public void Restart_WhenNotInResults_Throws()
        {
            var controller = new GameFlowController();
            Assert.Throws<InvalidOperationException>(() => controller.Restart());
        }
    }
}
