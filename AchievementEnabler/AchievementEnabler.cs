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
    ///     as cheated; and the vanilla commands that rely on those checks keep working.
    ///   * <see cref="MainMenuPatches"/> - the main menu still shows the game is modded.
    ///   * <see cref="CheatFlagPatches"/> - old cheat marks on characters and worlds stop showing in the
    ///     achievements panel, each behind a config toggle.
    ///   * <see cref="ClearCheatedItemsCommand"/> - an admin clears cheated items by command.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class AchievementEnabler : BaseUnityPlugin
    {
        public const string PluginGUID = "MidnightsFX.AchievementEnabler";
        public const string PluginName = "AchievementEnabler";
        public const string PluginVersion = "0.3.0";

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
