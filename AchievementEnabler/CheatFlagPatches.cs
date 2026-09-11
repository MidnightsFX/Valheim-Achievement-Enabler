using HarmonyLib;

namespace AchievementEnabler
{
    /// <summary>
    /// The cheat marks the game keeps on characters and worlds, and never takes back.
    ///
    ///   * A character carries <c>PlayerProfile.m_usedCheats</c>. It is set the first time the character
    ///     runs a cheat command - which <see cref="AchievementGatePatches"/> now prevents - saved with the
    ///     profile, and nothing in the game ever clears it.
    ///   * A world has no stored flag. <c>Achievements.IsWorldCheated()</c> works it out on each call from
    ///     the world's starting keys: any key the World Modifiers screen cannot set counts as a cheat, and
    ///     those keys stay with the world.
    ///
    /// Neither blocks achievements any more; the gate patch sees to that. What is left is the achievements
    /// panel, which reads both directly and shows a cheated notice for a flagged character or world.
    /// Both toggles are read each time they apply, so changing one needs no restart.
    /// </summary>
    internal static class CheatFlagPatches
    {
        // LoadPlayerFromDisk rather than the public Load(), because it is where m_usedCheats is read.
        [HarmonyPatch(typeof(PlayerProfile), "LoadPlayerFromDisk")]
        private static class CharacterLoad
        {
            private static void Postfix(PlayerProfile __instance, bool __result)
            {
                if (!__result || !__instance.m_usedCheats || !ValConfig.ClearCharacterCheatFlag.Value)
                {
                    return;
                }

                // In memory only; it reaches the save file the next time the game saves this character.
                __instance.m_usedCheats = false;
                Logger.LogInfo($"Cleared the cheat flag on character '{__instance.GetName()}'.");
            }
        }

        [HarmonyPatch(typeof(Achievements), nameof(Achievements.IsWorldCheated))]
        private static class WorldCheck
        {
            // Skips the original rather than overriding its result: it re-syncs the World Modifiers screen
            // and logs the first key it does not recognise, on every call, and none of that is wanted once
            // the answer is decided. Combat difficulty does not depend on the re-sync; it reads ZoneSystem.
            private static bool Prefix(ref bool __result)
            {
                if (!ValConfig.IgnoreWorldCheatFlag.Value)
                {
                    return true;
                }
                __result = false;
                return false;
            }
        }
    }
}
