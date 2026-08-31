using System;
using NUnit.Framework;
using Hermit.Games;
using Hermit.Tests.EditMode.Fakes;

namespace Hermit.Tests.EditMode
{
    public class GameRegistryTests
    {
        [Test]
        public void EmptyRegistry_EnumeratesEmpty_WithoutCrashing()
        {
            var registry = new GameRegistry();

            Assert.AreEqual(0, registry.Count);
            Assert.AreEqual(0, registry.All.Count);
            Assert.IsFalse(registry.TryGet("anything", out _));
        }

        [Test]
        public void Register_ThenLookupById_ReturnsTheSameDefinition()
        {
            var registry = new GameRegistry();
            var definition = FakeGameDefinition.CreateInMemory("fake_game", "Fake Game");

            registry.Register(definition);

            Assert.IsTrue(registry.TryGet("fake_game", out var found));
            Assert.AreSame(definition, found);
        }

        [Test]
        public void All_EnumeratesEveryRegisteredGame()
        {
            var registry = new GameRegistry();
            registry.Register(FakeGameDefinition.CreateInMemory("a", "A"));
            registry.Register(FakeGameDefinition.CreateInMemory("b", "B"));
            registry.Register(FakeGameDefinition.CreateInMemory("c", "C"));

            Assert.AreEqual(3, registry.All.Count);
            Assert.AreEqual(3, registry.Count);
        }

        [Test]
        public void Register_DuplicateGameId_Throws()
        {
            var registry = new GameRegistry();
            var definition = FakeGameDefinition.CreateInMemory("fake_game", "Fake Game");
            registry.Register(definition);

            Assert.Throws<InvalidOperationException>(() =>
                registry.Register(FakeGameDefinition.CreateInMemory("fake_game", "A different display name")));
        }

        [Test]
        public void Register_EmptyGameId_Throws()
        {
            var registry = new GameRegistry();
            var definition = FakeGameDefinition.CreateInMemory(string.Empty, "No id");

            Assert.Throws<ArgumentException>(() => registry.Register(definition));
        }

        [Test]
        public void Register_Null_Throws()
        {
            var registry = new GameRegistry();
            Assert.Throws<ArgumentNullException>(() => registry.Register(null));
        }

        [Test]
        public void All_OrdersByDisplaySortOrder_ThenByRegistrationOrder()
        {
            var registry = new GameRegistry();
            var third = FakeGameDefinition.CreateInMemory("third", "Third", displaySortOrder: 10);
            var first = FakeGameDefinition.CreateInMemory("first", "First", displaySortOrder: 0);
            var secondA = FakeGameDefinition.CreateInMemory("second_a", "Second A", displaySortOrder: 5);
            var secondB = FakeGameDefinition.CreateInMemory("second_b", "Second B", displaySortOrder: 5);

            // Registered out of display order on purpose — All() must still sort by
            // DisplaySortOrder, falling back to registration order for the tie.
            registry.Register(third);
            registry.Register(secondB);
            registry.Register(first);
            registry.Register(secondA);

            var ordered = registry.All;

            Assert.AreEqual("first", ordered[0].GameId);
            Assert.AreEqual("second_b", ordered[1].GameId, "secondB was registered before secondA, so it must win the sort-order tie.");
            Assert.AreEqual("second_a", ordered[2].GameId);
            Assert.AreEqual("third", ordered[3].GameId);
        }

        [Test]
        public void All_IncludesDisabledGames_CallerDecidesHowToPresentThem()
        {
            var registry = new GameRegistry();
            registry.Register(FakeGameDefinition.CreateInMemory("enabled_game", "Enabled", isEnabled: true));
            registry.Register(FakeGameDefinition.CreateInMemory("disabled_game", "Disabled", isEnabled: false));

            Assert.AreEqual(2, registry.All.Count);
            Assert.IsTrue(registry.TryGet("disabled_game", out var disabled));
            Assert.IsFalse(disabled.IsEnabled);
        }
    }
}
