using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Hermit.Economy;

namespace Hermit.Editor
{
    /// <summary>
    /// C9.1 DEV-ONLY economy inspection/reset — lives in the Editor assembly
    /// (Editor platform only), so it never ships in a player build. Reads and
    /// writes the same local save the game uses
    /// (Application.persistentDataPath/hermit_economy.json). For a Windows
    /// build, the equivalent reset is deleting that file (see
    /// Docs/C9_1_HERMIT_COINS_V01.md, "Dev tooling").
    /// </summary>
    public static class HermitEconomyDevMenu
    {
        [MenuItem("Hermit/Economy (Dev)/Log Economy State")]
        public static void LogState()
        {
            var store = new LocalFileHermitEconomyStore();
            var service = new HermitEconomyService(store, new SystemHermitClock(), HermitEconomyDefinition.CreateDefault());
            var state = service.CurrentState;
            var wallet = service.Wallet;

            var report = new StringBuilder();
            report.AppendLine("[Hermit Economy — dev]");
            report.AppendLine($"Save file: {store.FilePath}");
            report.AppendLine($"Balance: {wallet.Balance} HC   LifetimeEarned: {wallet.LifetimeEarned} HC   LifetimeSpent: {wallet.LifetimeSpent} HC");
            report.AppendLine($"Today ({state.CurrentLocalDate}): {state.ValidSessionsToday} valid session(s); " +
                              $"archetypes: [{string.Join(", ", state.ArchetypesCompletedToday)}]; " +
                              $"first-session bonus claimed: {state.FirstSessionBonusClaimedToday}; variety bonus claimed: {state.VarietyBonusClaimedToday}");
            report.AppendLine($"Week of {state.CurrentWeekId}: active days [{string.Join(", ", state.ActiveDaysThisWeek)}] ({state.ActiveDaysThisWeek.Count}); weekly bonus claimed: {state.WeeklyBonusClaimed}");
            report.AppendLine($"Last transactions: {string.Join(" | ", wallet.Transactions.Reverse().Take(5).Select(t => $"+{t.Amount} ({t.TimestampUtc:u})"))}");
            Debug.Log(report.ToString());
        }

        [MenuItem("Hermit/Economy (Dev)/RESET Economy")]
        public static void ResetEconomy()
        {
            if (!EditorUtility.DisplayDialog(
                    "Reset Hermit economy (dev)",
                    "Delete the local Hermit Coin save (balance, lifetime totals, daily/weekly bonus state)?",
                    "Reset", "Cancel"))
            {
                return;
            }

            new LocalFileHermitEconomyStore().Clear();
            HermitEconomy.ResetOverride();
            Debug.Log("[Hermit Economy — dev] Local economy save deleted.");
        }
    }
}
