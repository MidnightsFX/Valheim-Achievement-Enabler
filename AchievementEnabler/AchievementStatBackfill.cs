using System.Collections.Generic;
using HarmonyLib;

namespace AchievementEnabler
{
    /// <summary>
    /// Counts the progress the game recorded but refused to count.
    ///
    /// Every stat is written twice. <c>PlayerProfile.m_playerStats[0]</c> is the raw table and always gets
    /// the increment; the tables achievements are actually read from - <c>[1]</c> for anything that works at
    /// any difficulty, and one per combat difficulty above that - only get it when
    /// <c>Achievements.CanGetAchievements</c> says yes. Being modded is enough for that to say no, so on a
    /// character that played at all before this mod was installed the raw table can hold hundreds of trees
    /// felled, kilometres sailed or meals eaten while the counted table holds nothing. The game never goes
    /// back for it: an achievement asks the counted table and nothing else, and no code path ever copies
    /// between them. That is why Lumberjack and the other long-haul achievements look stuck.
    ///
    /// So the raw table is merged into the counted one as the character loads, taking whichever number is
    /// higher. The raw table gets every increment the counted one does, so the higher number is the true
    /// total and re-running the merge changes nothing; later increments keep going to both, so the two stay
    /// level afterwards.
    ///
    /// Only the any-difficulty table is filled. The raw table mixes together everything the character has
    /// ever done, at whatever combat difficulty it was done at, so copying it into the Hard table would hand
    /// out the two hard-difficulty achievements for kills made on Easy. Three of the game's achievements ask
    /// for a specific difficulty; the other fifty ask this table.
    ///
    /// The unlock itself is the game's own: it re-tests every achievement against these tables when a world
    /// loads, which happens after the character has. Nothing here awards anything directly.
    /// </summary>
    internal static class AchievementStatBackfill
    {
        // DifficultyRequirement.RawStats and .Any. Indices rather than the enum because they index
        // m_playerStats, which is what the game does at every one of these sites too.
        private const int RawStats = 0;
        private const int Any = 1;

        // The same private method CheatFlagPatches hooks, and for the same reason: it is where the stat
        // tables have just been read off disk, and it runs before the world load that re-tests achievements.
        [HarmonyPatch(typeof(PlayerProfile), "LoadPlayerFromDisk")]
        private static class CharacterLoad
        {
            private static void Postfix(PlayerProfile __instance, bool __result)
            {
                if (!__result || !ValConfig.BackfillAchievementStats.Value)
                {
                    return;
                }

                Backfill(__instance);
            }
        }

        private static void Backfill(PlayerProfile profile)
        {
            PlayerProfile.PlayerStats[] tables = profile.m_playerStats;
            if (tables == null || tables.Length <= Any)
            {
                Logger.LogWarning("The character's stat tables are not the shape this game version was expected to have; left alone.");
                return;
            }

            PlayerProfile.PlayerStats raw = tables[RawStats];
            PlayerProfile.PlayerStats counted = tables[Any];
            if (raw == null || counted == null)
            {
                return;
            }

            int raised = Merge(raw.m_stats, counted.m_stats);

            // One dictionary per kill modifier: unarmed, magic, ranged, melee, and a total across all of
            // them. Achievements read whichever the requirement names.
            if (raw.m_enemyStats != null && counted.m_enemyStats != null)
            {
                for (int i = 0; i < raw.m_enemyStats.Length && i < counted.m_enemyStats.Length; i++)
                {
                    raised += Merge(raw.m_enemyStats[i], counted.m_enemyStats[i]);
                }
            }

            raised += Merge(raw.m_itemPickupStats, counted.m_itemPickupStats);
            raised += Merge(raw.m_itemCraftStats, counted.m_itemCraftStats);
            raised += Merge(raw.m_pickableStats, counted.m_pickableStats);
            raised += Merge(raw.m_foodEatenStats, counted.m_foodEatenStats);
            raised += Merge(raw.m_piecesPlacedStats, counted.m_piecesPlacedStats);

            if (raised > 0)
            {
                Logger.LogInfo($"Counted {raised} stat(s) towards achievements on character '{profile.GetName()}' that the game had recorded but not counted.");
            }
            else
            {
                Logger.LogDebug($"Nothing to count towards achievements on character '{profile.GetName()}'.");
            }
        }

        /// <summary>Raises each entry in <paramref name="counted"/> to the raw total, and reports how many moved.</summary>
        private static int Merge<TKey>(Dictionary<TKey, float> raw, Dictionary<TKey, float> counted)
        {
            if (raw == null || counted == null)
            {
                return 0;
            }

            int raised = 0;
            foreach (KeyValuePair<TKey, float> entry in raw)
            {
                // Both tables start life with every PlayerStatType present and zero, so this skips far more
                // than it moves on a character that has nothing owed.
                if (counted.TryGetValue(entry.Key, out float current) && current >= entry.Value)
                {
                    continue;
                }

                counted[entry.Key] = entry.Value;
                raised++;
            }

            return raised;
        }
    }
}
