using BepInEx;
using BepInEx.Logging;

// BaseUnityPlugin inherits a `Logger` property of its own, and inside the plugin class an inherited
// member outranks a same-named type from the enclosing namespace. Without this alias, every
// `Logger.LogInfo` below would silently bind to ManualLogSource instead of our level-gated wrapper -
// it compiles either way, which is exactly what makes it worth naming explicitly.
using ModLog = AchievementEnabler.Logger;

namespace AchievementEnabler
{
    /// <summary>
    /// Lets a modded Valheim earn achievements, without lying about being modded.
    ///
    /// The flag itself stays true - the main-menu label shows, the startup log says so, and any other
    /// mod that reads it gets the truth. What changes is what the achievement gate observes, and only
    /// for the duration of the calls that do the gating.
    ///
    /// Valheim 1.0 is the build this finally has a job to do on. The gate is
    /// <c>Achievements.IsCheatedAtAll()</c>, whose verdict is <c>(real cheats) || Game.isModded</c>,
    /// and <c>Achievements.CanGetAchievements()</c> guards every achievement stat write behind it.
    /// Making that one call see a false flag is the whole fix, and it leaves the genuine cheat
    /// checks - console cheats, cheated items, cheated world modifiers - working exactly as shipped.
    ///
    /// Nothing here is bound at compile time even so. Every target is named in config and resolved
    /// by name at runtime, because the gate moving or being renamed by a future update should cost a
    /// line in a .cfg rather than a new build; the patcher's DumpSymbols option is what tells you the
    /// new name. On a build with no gate at all, this correctly does nothing.
    ///
    /// Three mechanisms, in the order you should reach for them:
    ///   1. <see cref="ScopedFlagSwap"/> - flag is false for the duration of a named method. Covers
    ///      almost every shape a gate can take. Start here.
    ///   2. <see cref="MethodOverrides"/> - a named bool method always returns false. For when the
    ///      gate is a method rather than a flag read.
    ///   3. The preloader patcher's IL rewrite - folds the flag read itself to a constant. The
    ///      fallback for a read that no nameable method wraps.
    /// </summary>
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class AchievementEnabler : BaseUnityPlugin
    {
        public const string PluginGUID = "MidnightsFX.AchievementEnabler";
        public const string PluginName = "AchievementEnabler";
        public const string PluginVersion = "0.2.0";

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

            ModdedFlag.Resolve(ValConfig.FlagMember.Value);

            if (ValConfig.KeepFlagTrue.Value && ModdedFlag.Available)
            {
                // Awake runs during the Chainloader pass, so a mod loading after us could still
                // overwrite this. That is fine and intended: every one of them sets it to true.
                ModdedFlag.Value = true;
            }

            // Logged after the write, and reading the real value back rather than assuming it, so
            // the log answers "is this install still honest about being modded?" on its own.
            ModLog.LogInfo($"Modded flag: {ModdedFlag.Description} = {ModdedFlag.Value}");

            PatcherReport.LogSummary();

            int scoped = ScopedFlagSwap.Apply(ValConfig.Split(ValConfig.ScopedFalseMethods.Value));
            int forced = MethodOverrides.Apply(ValConfig.Split(ValConfig.ForceFalseMethods.Value));

            if (scoped == 0 && forced == 0)
            {
                ModLog.LogWarning(
                    "No gate methods are in play. Since Valheim 1.0 there is a real one - " +
                    ValConfig.DefaultScopedFalseMethods + " - so unless the preloader patcher above " +
                    "reported folding the flag read, achievements are still blocked. Restore that value " +
                    "in ScopedFalseMethods, or if the game has since renamed it, set DumpSymbols in the " +
                    "patcher config, restart, and put what the symbol dump finds there instead.");
            }
            else
            {
                ModLog.LogInfo($"Ready: {scoped} scoped swap(s), {forced} forced result(s).");
            }
        }
    }
}
