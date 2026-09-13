using Hermit.Games.Clasico.Microgames;

namespace Hermit.Runtime.GameFramework.Microgames
{
    /// <summary>
    /// The command word shown during a microgame's Intro beat — bound to the
    /// Mechanical Archetype, never to the Presentation Theme, per
    /// Docs/C8_CLASICO_DESIGN_LOCK.md, section X: once a player learns
    /// DISPARA means "pick one", any future AimSelect microgame in a new
    /// world is instantly familiar.
    ///
    /// C8.1d.7: AimSelect's word dropped its opening/closing exclamation
    /// marks ("¡DISPARA!" -&gt; "DISPARA") as part of refining it into a
    /// short action cue — see
    /// <see cref="Hermit.Runtime.GameFramework.Microgames.WesternShootoutPresenter.ShowDisparaCue"/>
    /// and Docs/C8_1D_GOLD_ART_INTEGRATION.md, "C8.1d.7". This word is no
    /// longer routed through <c>ClasicoGameHost</c>'s generic
    /// <c>ClasicoHud.ShowCommand</c> banner for AimSelect specifically (that
    /// banner stayed onscreen for Western's entire cinematic intro, which is
    /// exactly what this phase fixed) — <c>WesternShootoutPresenter</c> now
    /// reads this same word directly for its own, presentation-appropriate
    /// cue. Every other archetype's word is unchanged, still shown via the
    /// original generic mechanism.
    /// </summary>
    internal static class MicrogameVocabulary
    {
        public static string CommandFor(MicrogameArchetype archetype)
        {
            switch (archetype)
            {
                case MicrogameArchetype.AimSelect: return "DISPARA";
                case MicrogameArchetype.ChooseSide: return "¡DECIDE!";
                case MicrogameArchetype.Balance: return "¡BALANCEA!";
                default: return "¡ENCUÉNTRALO!";
            }
        }
    }
}
