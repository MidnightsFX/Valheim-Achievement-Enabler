using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

// BaseUnityPlugin inherits a `Logger` property of its own, and inside the plugin class an inherited
// member outranks a same-named type from the enclosing namespace. Without this alias, every
// `Logger.LogInfo` below would silently bind to ManualLogSource instead of our level-gated wrapper -
// it compiles either way, which is exactly what makes it worth naming explicitly.
using ModLog = AchievementEnabler.Logger;

namespace AchievementEnabler
{
    /// <summary>
    /// Lets a modded Valheim earn achievements.
    ///
    ///   * <see cref="AchievementGatePatches"/> - both checks that decide whether an achievement stat counts,
    ///     <c>CanGetAchievements</c> and <c>IsCheatedAtAll</c>, let everything through; the game's own
    ///     <c>PlayerProfile.s_bypassCheatChecks</c> switch always answers true, so it stops flagging new things
    ///     as cheated; new drops, creature loot and trader items never start flagged, which covers the places that
    ///     do not check that switch; the vanilla commands that rely on those checks keep working; and the check
    ///     for achievements earned by holding other achievements, which vanilla never fires, is repaired.
    ///   * <see cref="AchievementStatBackfill"/> - progress the game recorded while achievements were switched
    ///     off is counted towards them when the character loads.
    ///   * <see cref="BestStatPatches"/> - best-ever stats, such as the most days survived in a row, are never
    ///     lowered by a change of combat difficulty, and ones the game already lowered are put back on load.
    ///   * <see cref="MainMenuPatches"/> - the main menu still shows the game is modded.
    ///   * <see cref="CheatFlagPatches"/> - old cheat marks on characters and worlds stop showing in the
    ///     achievements panel, each behind a config toggle.
    ///   * <see cref="VanillaRequirementPatches"/> - optionally, the craft-, eat-, build-, find- and kill-everything
    ///     achievements only ask for vanilla content, not what other mods add.
    ///   * <see cref="ClearCheatedItemsCommand"/> - a cheat command clears the cheated flag from carried items.
    ///   * <see cref="AchievementsPanelPatches"/> - optionally, and each on its own toggle, the achievements panel
    ///     names every requirement with its progress instead of "???", and reveals secret achievements.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class AchievementEnabler : BaseUnityPlugin
    {
        public const string PluginGUID = "MidnightsFX.AchievementEnabler";
        public const string PluginName = "AchievementEnabler";
        public const string PluginVersion = "0.5.1";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = base.Logger;
            ValConfig.Init(Config);

            if (!ValConfig.Enabled.Value)
            {
                ModLog.LogInfo("Disabled in config; nothing patched.");
                return;
            }

            // PatchAll, one class at a time. Two of the targets are private methods named by string, so a
            // game update can break one, and that should cost the one feature rather than the plugin.
            var harmony = new Harmony(PluginGUID);
            foreach (Type type in typeof(AchievementEnabler).Assembly.GetTypes())
            {
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception ex)
                {
                    ModLog.LogError($"Could not apply {type.FullName}: {ex.Message}");
                }
            }

            ClearCheatedItemsCommand.Register();

            ModLog.LogInfo("Ready.");
        }
    }
}
