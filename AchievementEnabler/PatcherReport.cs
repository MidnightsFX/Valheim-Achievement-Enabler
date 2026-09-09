using System;

namespace AchievementEnabler
{
    /// <summary>
    /// Surfaces what the preloader patcher did, in the plugin's own log section.
    ///
    /// The patcher runs long before the Chainloader and logs under its own source, so its output is
    /// easy to miss among BepInEx's preload chatter - which matters, because that output is how you
    /// find the achievement gate after a game update. It hands the summary over through an AppDomain
    /// data slot holding a <c>Func&lt;string[]&gt;</c>: both BCL types, so neither assembly needs a
    /// reference to the other and the plugin works perfectly well when the patcher is not installed.
    /// Same handoff ValheimMonitor.Preloader uses for its profiling data.
    /// </summary>
    internal static class PatcherReport
    {
        private const string SummaryKey = "AchievementEnabler.Preloader.Summary.v1";

        internal static void LogSummary()
        {
            var accessor = AppDomain.CurrentDomain.GetData(SummaryKey) as Func<string[]>;
            if (accessor == null)
            {
                Logger.LogInfo(
                    "Preloader patcher not present. The scoped swap below is the only mechanism in play, " +
                    "which is fine unless you need a flag read that no method wraps.");
                return;
            }

            string[] lines;
            try
            {
                lines = accessor();
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Preloader patcher summary could not be read: " + ex.Message);
                return;
            }

            if (lines == null || lines.Length == 0)
            {
                Logger.LogInfo("Preloader patcher ran but reported nothing.");
                return;
            }

            Logger.LogInfo("Preloader patcher:");
            foreach (string line in lines) Logger.LogInfo("  " + line);
        }
    }
}
