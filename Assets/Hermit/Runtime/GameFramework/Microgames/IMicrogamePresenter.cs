using UnityEngine;

namespace Hermit.Runtime.GameFramework.Microgames
{
    /// <summary>
    /// The generic half of a microgame presenter's contract — build once,
    /// hide when not active. Showing a *specific* challenge is typed per
    /// presenter (e.g. <c>WesternShootoutPresenter.ShowChallenge(ClassificationChallenge)</c>)
    /// rather than forced through a shared object-typed method, so
    /// <see cref="Hermit.Runtime.GameFramework.ClasicoGameHost"/> routes to
    /// the right presenter with a plain, explicit switch on
    /// <see cref="Hermit.Games.Clasico.Microgames.MicrogameArchetype"/> — the
    /// one place a switch is appropriate, since it is UI routing, not game logic.
    /// </summary>
    internal interface IMicrogamePresenter
    {
        /// <summary>Called exactly once, at composition time — the
        /// presenter's GameObjects are created once and toggled active/inactive
        /// afterward, never rebuilt per microgame (see Docs/C8_1_GOLD_MICROGAME_SLICE.md,
        /// "Performance").</summary>
        void Build(Transform stageRoot);

        void Hide();
    }
}
