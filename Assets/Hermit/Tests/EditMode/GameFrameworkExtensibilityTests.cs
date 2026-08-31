using System;
using NUnit.Framework;
using Hermit.Games;
using Hermit.Games.Analytics;
using Hermit.Tests.EditMode.Fakes;

namespace Hermit.Tests.EditMode
{
    /// <summary>
    /// The condition C5/C6 must prove before either can close: a second game
    /// can plug into the framework without a single edit to Hermit.Games.
    /// FakeGame (Fakes/FakeGame.cs) lives in this test assembly — not in
    /// Hermit.Games, not anywhere near ClasicoGameEngine — and implements only
    /// the public GameDefinition/IGameEngine contract. If adding it had
    /// required touching GameRegistry, GameFlowController, or any Clasico
    /// type, this test would not compile.
    /// </summary>
    public class GameFrameworkExtensibilityTests
    {
        [Test]
        public void ASecondGame_CanRegisterAndPlayThrough_WithoutTouchingCoreTypes()
        {
            var registry = new GameRegistry();
            var fakeDefinition = FakeGameDefinition.CreateInMemory("fake_game", "Fake Game", ticksToFinish: 2);
            registry.Register(fakeDefinition);

            Assert.IsTrue(registry.TryGet("fake_game", out var definition));

            var controller = new GameFlowController();
            var context = new GameContext(NullGameAnalyticsSink.Instance, new Random(0));

            controller.Start(definition, context);
            Assert.AreEqual(GameLifecycleState.Playing, controller.State);

            controller.Tick(0.1f);
            controller.Tick(0.1f);

            Assert.AreEqual(GameLifecycleState.Results, controller.State);
            Assert.IsTrue(controller.LastResult.Completed);
            Assert.AreEqual("fake_game", controller.LastResult.GameId);
        }

        [Test]
        public void RegistryHoldsBothGames_WithoutEitherKnowingAboutTheOther()
        {
            var registry = new GameRegistry();
            var fakeDefinition = FakeGameDefinition.CreateInMemory("fake_game", "Fake Game");
            var otherFakeDefinition = FakeGameDefinition.CreateInMemory("another_fake_game", "Another Fake Game");

            registry.Register(fakeDefinition);
            registry.Register(otherFakeDefinition);

            Assert.AreEqual(2, registry.All.Count);
            Assert.IsTrue(registry.TryGet("fake_game", out _));
            Assert.IsTrue(registry.TryGet("another_fake_game", out _));
        }

        [Test]
        public void RegisteringTheSameGameIdTwice_Throws()
        {
            var registry = new GameRegistry();
            var definition = FakeGameDefinition.CreateInMemory("fake_game", "Fake Game");
            registry.Register(definition);

            Assert.Throws<InvalidOperationException>(() => registry.Register(definition));
        }
    }
}
