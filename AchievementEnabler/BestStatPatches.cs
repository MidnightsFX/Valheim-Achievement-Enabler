using System;
using System.Collections.Generic;
using HarmonyLib;

namespace AchievementEnabler
{
    /// <summary>
    /// Stops the game lowering the "best ever" stats, and puts back the ones it already lowered.
    ///
    /// A handful of stats keep a record rather than a running total: the most days survived in a row, the
    /// highest comfort, the highest piece built (above the ground and above sea level), and the most pieces
    /// of each kind built around you (the BuildCluster stats). Every site that updates one does the same
    /// thing: read the record with <c>PlayerProfile.GetStat</c>, and if the new value beats it, write it
    /// with <c>SetStat</c>. But <c>GetStat</c> reads the table for the world's current combat difficulty,
    /// while <c>SetStat</c> writes the raw table, the any-difficulty table and that difficulty's table, and
    /// overwrites whatever is there. So the first time a character plays at a difficulty it has no record
    /// for, the first small value beats that empty table and is written over the real record in the other
    /// two. The any-difficulty table is the one achievements read, so achievements relock: a character with
    /// a 60-day streak at Normal had its best streak written down to 1 on its first day at Hard, and then
    /// lost Survive 30 days on the next world load, when the game re-tests every achievement.
    ///
    /// The days streak has a second problem on top: its record is only compared with the current
    /// difficulty's streak, so the any-difficulty streak can run well past its record without ever raising
    /// it.
    ///
    /// Three fixes, all behind <c>Achievements/KeepBestStats</c>:
    ///   * a record write only ever raises each table, never lowers it;
    ///   * after each day survived, every table's best streak is raised to that table's current streak;
    ///   * when the character loads, the raw and any-difficulty records are raised to the highest record in
    ///     any difficulty's table, and every best streak to its current streak.
    ///
    /// The repair hands out nothing the game did not already give. A difficulty's table is only written
    /// while achievements are counting, and always together with the any-difficulty table, and it only
    /// ever grows, since it is compared and written in the same table. So its record is a value the game
    /// already wrote into the any-difficulty table once; the bug is only that it was written over later.
    /// </summary>
    internal static class BestStatPatches
    {
        // DifficultyRequirement.RawStats and .Any, and the first per-difficulty table after them. Indices
        // rather than the enum because they index m_playerStats, as AchievementStatBackfill does.
        private const int RawStats = 0;
        private const int Any = 1;
        private const int FirstDifficulty = 2;

        private static readonly HashSet<PlayerStatType> BestStats = FindBestStats();

        // The four named records, plus one BuildCluster stat per piece usage tag; Piece writes those as
        // (PlayerStatType)(171 + tag index), so they are found by name rather than by that offset.
        private static HashSet<PlayerStatType> FindBestStats()
        {
            var stats = new HashSet<PlayerStatType>
            {
                PlayerStatType.ConsecutiveDaysSurvivedMax,
                PlayerStatType.MaxComfort,
                PlayerStatType.MaxBuildingHeight,
                PlayerStatType.MaxBuildingHeightWorld,
            };
            foreach (PlayerStatType stat in Enum.GetValues(typeof(PlayerStatType)))
            {
                if (stat.ToString().StartsWith("BuildCluster", StringComparison.Ordinal))
                {
                    stats.Add(stat);
                }
            }
            return stats;
        }

        // Skips the original for the record stats only. Everything else, including the days streak itself,
        // which a death resets to zero through this same method, is left to the game.
        [HarmonyPatch(typeof(PlayerProfile), nameof(PlayerProfile.SetStat))]
        private static class RecordWrite
        {
            private static bool Prefix(PlayerProfile __instance, PlayerStatType stat, float amount, bool cheated)
            {
                if (!ValConfig.KeepBestStats.Value || !BestStats.Contains(stat))
                {
                    return true;
                }

                PlayerProfile.PlayerStats[] tables = __instance.m_playerStats;
                if (tables == null || tables.Length <= Any)
                {
                    return true;
                }

                // The same three tables the game writes, under the same condition.
                Raise(tables[RawStats].m_stats, stat, amount);
                if (Achievements.CanGetAchievements(cheated))
                {
                    Raise(tables[Any].m_stats, stat, amount);
                    Raise(tables[Achievements.GetCurrentAchievementDifficultyIndex()].m_stats, stat, amount);
                    if (tables[Any][stat] > amount)
                    {
                        Logger.LogDebug($"Kept {stat} at {tables[Any][stat]} rather than lowering it to {amount}.");
                    }

                    // The game passes the new value here. The any-difficulty record is what the platform
                    // stat and the unlock check should see, and it is never lower.
                    Achievements.AchievementStatSetEvent(stat, (uint)tables[Any][stat]);
                }

                return false;
            }
        }

        // Runs after the game has added the day to every table's streak and, if the current difficulty's
        // streak beat its own record, written that record.
        [HarmonyPatch(typeof(Player), nameof(Player.SurvivedOneDay))]
        private static class DaySurvived
        {
            private static void Postfix()
            {
                if (!ValConfig.KeepBestStats.Value)
                {
                    return;
                }

                PlayerProfile.PlayerStats[] tables = Game.instance.GetPlayerProfile()?.m_playerStats;
                if (tables == null || tables.Length <= Any)
                {
                    return;
                }

                bool countedRaised = false;
                for (int i = 0; i < tables.Length; i++)
                {
                    if (StreakIntoBest(tables[i]) && i == Any)
                    {
                        countedRaised = true;
                    }
                }

                if (countedRaised)
                {
                    Achievements.AchievementStatSetEvent(PlayerStatType.ConsecutiveDaysSurvivedMax,
                        (uint)tables[Any][PlayerStatType.ConsecutiveDaysSurvivedMax]);
                }
            }
        }

        // Low priority so it runs after AchievementStatBackfill's postfix on the same method, which can
        // raise the any-difficulty streak from the raw one.
        [HarmonyPatch(typeof(PlayerProfile), "LoadPlayerFromDisk")]
        private static class CharacterLoad
        {
            [HarmonyPriority(Priority.Low)]
            private static void Postfix(PlayerProfile __instance, bool __result)
            {
                if (!__result || !ValConfig.KeepBestStats.Value)
                {
                    return;
                }

                Repair(__instance);
            }
        }

        private static void Repair(PlayerProfile profile)
        {
            PlayerProfile.PlayerStats[] tables = profile.m_playerStats;
            if (tables == null || tables.Length <= Any || tables[RawStats] == null || tables[Any] == null)
            {
                return;
            }

            var before = new Dictionary<PlayerStatType, float>();
            foreach (PlayerStatType stat in BestStats)
            {
                before[stat] = Get(tables[Any].m_stats, stat);
            }

            foreach (PlayerProfile.PlayerStats table in tables)
            {
                StreakIntoBest(table);
            }

            foreach (PlayerStatType stat in BestStats)
            {
                float best = 0f;
                for (int i = FirstDifficulty; i < tables.Length; i++)
                {
                    if (tables[i] != null)
                    {
                        best = Math.Max(best, Get(tables[i].m_stats, stat));
                    }
                }
                Raise(tables[RawStats].m_stats, stat, best);
                Raise(tables[Any].m_stats, stat, best);
            }

            var restored = new List<string>();
            foreach (PlayerStatType stat in BestStats)
            {
                float after = Get(tables[Any].m_stats, stat);
                if (after > before[stat])
                {
                    restored.Add($"{stat} {before[stat]:0.#} -> {after:0.#}");
                }
            }

            if (restored.Count > 0)
            {
                Logger.LogInfo($"Restored best stats on character '{profile.GetName()}' that the game had lowered: {string.Join(", ", restored)}.");
            }
            else
            {
                Logger.LogDebug($"No lowered best stats to restore on character '{profile.GetName()}'.");
            }
        }

        /// <summary>Raises the table's best days-survived streak to its current one, and reports whether it moved.</summary>
        private static bool StreakIntoBest(PlayerProfile.PlayerStats table)
        {
            if (table == null)
            {
                return false;
            }
            return Raise(table.m_stats, PlayerStatType.ConsecutiveDaysSurvivedMax, Get(table.m_stats, PlayerStatType.ConsecutiveDaysSurvived));
        }

        /// <summary>Sets the stat to <paramref name="value"/> only if that is higher, and reports whether it moved.</summary>
        private static bool Raise(Dictionary<PlayerStatType, float> stats, PlayerStatType stat, float value)
        {
            if (stats.TryGetValue(stat, out float current) && current >= value)
            {
                return false;
            }

            stats[stat] = value;
            return true;
        }

        private static float Get(Dictionary<PlayerStatType, float> stats, PlayerStatType stat)
        {
            return stats.TryGetValue(stat, out float value) ? value : 0f;
        }
    }
}
