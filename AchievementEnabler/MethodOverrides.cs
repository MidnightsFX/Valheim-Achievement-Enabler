using System;
using System.Reflection;
using HarmonyLib;

namespace AchievementEnabler
{
    /// <summary>
    /// For the case where the gate is a method rather than a flag read - <c>bool Game.IsModded()</c>,
    /// a property getter, <c>Terminal.IsCheatsEnabled()</c>, whatever tomorrow brings. A postfix
    /// forcing the result is two lines and needs no IL work at all, so there is no reason to reach
    /// for the patcher when the gate has this shape.
    ///
    /// Empty by default, and still empty on Valheim 1.0: the gate there is a flag read inside
    /// <c>Achievements.IsCheatedAtAll()</c>, which the scoped swap handles without needing this.
    /// </summary>
    internal static class MethodOverrides
    {
        private static readonly Harmony Harmony = new Harmony(AchievementEnabler.PluginGUID + ".overrides");

        internal static int Apply(string[] entries)
        {
            if (entries.Length == 0) return 0;

            var postfix = new HarmonyMethod(AccessTools.Method(typeof(MethodOverrides), nameof(ForceFalse)));

            int patched = 0;
            foreach (string entry in entries)
            {
                MethodBase target = MemberRef.ResolveMethod(entry);
                if (target == null) continue;

                // Attaching a `ref bool __result` postfix to a method that does not return bool is a
                // config typo, and Harmony's own failure for it is an obscure one. Catch it here.
                var asMethod = target as MethodInfo;
                if (asMethod == null || asMethod.ReturnType != typeof(bool))
                {
                    Logger.LogWarning($"'{entry}' does not return bool; skipped. ForceFalseMethods only applies to bool methods.");
                    continue;
                }

                try
                {
                    Harmony.Patch(target, postfix: postfix);
                    Logger.LogInfo($"Forcing {entry} to return false.");
                    patched++;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Could not patch '{entry}': {ex.Message}");
                }
            }
            return patched;
        }

        private static void ForceFalse(ref bool __result)
        {
            __result = false;
        }
    }
}
