using System.Reflection;
using HarmonyLib;

namespace AchievementEnabler
{
    /// <summary>
    /// The two checks that decide whether an achievement stat is recorded, the game's own switch for
    /// ignoring cheats, and the vanilla commands that lean on the second check.
    ///
    /// <c>Achievements.CanGetAchievements(bool cheated)</c> guards every achievement stat write, and the
    /// build-achievement unlock in <c>Piece</c>. It refuses when either:
    ///   * <c>cheated</c> is true. The caller decides that per event: a kill of a flagged creature (spawned,
    ///     or hit in god mode, ghost mode or while debug flying), picking up, eating or building with a
    ///     cheated item, building with nocost.
    ///   * <c>Achievements.IsCheatedAtAll()</c> is true: the character has run a cheat command, the world has
    ///     a key the World Modifiers screen cannot set, the inventory holds a cheated item, or the game is
    ///     modded.
    ///
    /// Both are overridden. <c>CanGetAchievements</c> would already say yes on its own now that
    /// <c>PlayerProfile.s_bypassCheatChecks</c> answers true; the override stays so achievements keep counting
    /// if anything else patches that switch back off. <c>IsCheatedAtAll</c> ignores the switch entirely.
    /// </summary>
    internal static class AchievementGatePatches
    {
        [HarmonyPatch(typeof(Achievements), nameof(Achievements.CanGetAchievements))]
        private static class StatWriteGate
        {
            private static bool Prefix(ref bool __result)
            {
                __result = true;
                return false;
            }
        }

        [HarmonyPatch(typeof(Achievements), nameof(Achievements.IsCheatedAtAll))]
        private static class CheatedAtAllGate
        {
            private static bool Prefix(ref bool __result)
            {
                __result = false;
                return false;
            }
        }

        // The game's own switch for ignoring cheats. In 1.0.12 it is a getter, not a field: true only when the
        // local player carries a "bypasscheatchecks" unique key set to "1", which the devcommands-only
        // yesiuseddevcommandsbutiwantmyachievementsanyway command toggles per character. Answering true here
        // turns it on for every character, and on dedicated servers, which have no local player, without
        // writing anything to a save; setting that command to 0 does nothing while this mod is loaded.
        // With it on, the game stops flagging new things as cheated - whatever spawn creates, creatures hit in
        // god mode, ghost mode or while debug flying, and what is built, crafted, cooked, smelted or fermented
        // from cheated materials. Anything flagged before stays flagged.
        //
        // A dedicated server can be on an older build than its clients, and up to 1.0.7 the switch is a plain
        // static field that nothing in the game writes. There the field is set to true once instead, since there
        // is no getter to patch. Both are looked up with plain reflection: AccessTools logs a warning for
        // whichever of the two is missing.
        [HarmonyPatch]
        private static class BypassCheatChecks
        {
            private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            private static readonly MethodInfo Getter = typeof(PlayerProfile)
                .GetProperty(nameof(PlayerProfile.s_bypassCheatChecks), AnyStatic)?.GetGetMethod(true);

            // False skips the class without an error. Harmony calls this again before patching the getter, so
            // the field is only written on the path that patches nothing.
            private static bool Prepare()
            {
                if (Getter != null) return true;

                FieldInfo field = typeof(PlayerProfile).GetField(nameof(PlayerProfile.s_bypassCheatChecks), AnyStatic);
                if (field != null && field.FieldType == typeof(bool))
                {
                    field.SetValue(null, true);
                    Logger.LogInfo("PlayerProfile.s_bypassCheatChecks is a field in this game version; set it to true.");
                }
                else
                {
                    Logger.LogWarning("Could not find PlayerProfile.s_bypassCheatChecks, so the game will keep flagging new things as cheated. Achievements still count.");
                }
                return false;
            }

            private static MethodBase TargetMethod() => Getter;

            private static bool Prefix(ref bool __result)
            {
                __result = true;
                return false;
            }
        }

        // RunAction refuses every cheat command, printing the "this will disable achievements" prompt, until
        // IsCheatedAtAll() is true - which confirmcheats normally arranges by setting m_usedCheats. It can no
        // longer be true, so without this no cheat command would ever run. Clearing IsCheat for the call skips
        // the prompt, and the block at the end that sets m_usedCheats, so the achievements panel does not
        // report the character as cheated either. devcommands is checked before RunAction, so it still applies.
        [HarmonyPatch(typeof(Terminal.ConsoleCommand), nameof(Terminal.ConsoleCommand.RunAction))]
        private static class CheatCommand
        {
            private static void Prefix(Terminal.ConsoleCommand __instance, out bool __state)
            {
                __state = __instance.IsCheat;
                __instance.IsCheat = false;
            }

            // A finalizer, not a postfix, so a command that throws still gets IsCheat back.
            private static void Finalizer(Terminal.ConsoleCommand __instance, bool __state)
            {
                __instance.IsCheat = __state;
            }
        }

        // setkey refuses a key the World Modifiers screen cannot set unless IsCheatedAtAll() is true. That can
        // no longer happen, and the world flag such a key would set no longer blocks anything.
        [HarmonyPatch(typeof(ServerOptionsGUI), nameof(ServerOptionsGUI.SetKeyAndValueIsCheat))]
        private static class SetKey
        {
            private static bool Prefix(ref bool __result)
            {
                __result = false;
                return false;
            }
        }
    }
}
