using System;
using Hermit.Games;

namespace Hermit.Runtime.GameFramework
{
    /// <summary>
    /// C9.2: the contract <see cref="GameHub"/> actually needs from
    /// "whatever screen shows the game list" — extracted so
    /// <see cref="ArcadeGalleryHud"/> (the product's own premium Arcade
    /// presentation) and <see cref="GameSelectorHud"/> (kept, unchanged,
    /// for GameSessionInstaller's dev/test sandbox) can both plug into the
    /// same <see cref="GameHub"/> launch/back orchestration with zero
    /// duplication — GameHub only ever calls <see cref="Show"/>/<see cref="Hide"/>
    /// and listens for <see cref="GameLaunchRequested"/>, never anything
    /// about how either screen actually presents itself.
    /// </summary>
    internal interface IGameSelectorScreen
    {
        event Action<GameDefinition> GameLaunchRequested;

        void Show();
        void Hide();
    }
}
