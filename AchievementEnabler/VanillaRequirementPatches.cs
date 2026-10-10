using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AchievementEnabler
{
    /// <summary>
    /// Optionally trims the "do everything" achievements back to vanilla content.
    ///
    /// <c>Achievements.Initialize()</c> fills the dynamically populated achievements - craft every weapon, item
    /// and food, eat every food, place every build piece, find every trophy, kill every creature - from whatever
    /// ObjectDB and ZNetScene hold at the time, so content mods add their own items, pieces and creatures to
    /// them. Each requirement is keyed by the thing's localization token (<c>$item_…</c>, <c>$piece_…</c>,
    /// <c>$enemy_…</c>), and both the unlock checks and the achievements panel read those same lists.
    ///
    ///   * The vanilla tokens are recorded the first time the game's own ObjectDB and ZNetScene lists are seen,
    ///     before other mods' patches add to them. ObjectDB.CopyOtherDB shares the source list rather than
    ///     copying it, so anything added later stays in it for the session; hence first capture wins.
    ///   * After Initialize builds the lists, everything not in that record is removed, and the achievements
    ///     that changed get their unlock check again, since the game ran its own against the longer lists.
    ///
    /// The lists are rebuilt from scratch on every Initialize, so turning the option off, which runs
    /// Initialize again, puts them back. Achievements unlocked meanwhile stay unlocked on the platform.
    /// </summary>
    internal static class VanillaRequirementPatches
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly MethodInfo Initialize = typeof(Achievements).GetMethod("Initialize", AnyInstance, null, Type.EmptyTypes, null);
        private static readonly FieldInfo PlatformInitialized = typeof(Achievements).GetField("m_platformInitialized", AnyStatic);

        private static HashSet<string> s_itemNames;
        private static HashSet<string> s_pieceNames;
        private static HashSet<string> s_creatureNames;

        /// <summary>
        /// SettingChanged handler for <see cref="ValConfig.VanillaOnlyRequirements"/>: rebuilds the lists so the
        /// new value applies without a restart. Does nothing until the game has built them once.
        /// </summary>
        public static void Reapply(object sender, EventArgs e)
        {
            // Unity's null check: the instance is destroyed with its scene, leaving a stale reference.
            if (Initialize == null || Achievements.m_instance == null || Game.instance == null)
            {
                return;
            }
            if (PlatformInitialized != null && !(bool)PlatformInitialized.GetValue(null))
            {
                return;
            }
            Initialize.Invoke(Achievements.m_instance, null);
        }

        private static void RecordItems(List<GameObject> items)
        {
            if (s_itemNames != null || items == null || items.Count == 0)
            {
                return;
            }

            var itemNames = new HashSet<string>();
            var pieceNames = new HashSet<string>();
            foreach (GameObject item in items)
            {
                ItemDrop itemDrop = item ? item.GetComponent<ItemDrop>() : null;
                ItemDrop.ItemData.SharedData shared = itemDrop ? itemDrop.m_itemData?.m_shared : null;
                if (shared == null)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(shared.m_name))
                {
                    itemNames.Add(shared.m_name);
                }
                if (!shared.m_buildPieces)
                {
                    continue;
                }
                foreach (GameObject pieceObject in shared.m_buildPieces.m_pieces)
                {
                    Piece piece = pieceObject ? pieceObject.GetComponent<Piece>() : null;
                    if (piece != null && !string.IsNullOrEmpty(piece.m_name))
                    {
                        pieceNames.Add(piece.m_name);
                    }
                }
            }

            s_itemNames = itemNames;
            s_pieceNames = pieceNames;
            Logger.LogInfo($"Recorded {itemNames.Count} vanilla items and {pieceNames.Count} vanilla build pieces.");
        }

        private static void RecordCreatures(List<GameObject> prefabs)
        {
            if (s_creatureNames != null || prefabs == null || prefabs.Count == 0)
            {
                return;
            }

            var creatureNames = new HashSet<string>();
            foreach (GameObject prefab in prefabs)
            {
                Character character = prefab ? prefab.GetComponent<Character>() : null;
                if (character != null && !string.IsNullOrEmpty(character.m_name))
                {
                    creatureNames.Add(character.m_name);
                }
            }

            s_creatureNames = creatureNames;
            Logger.LogInfo($"Recorded {creatureNames.Count} vanilla creatures.");
        }

        // The recording prefixes run first, and ahead of Jotunn, so the lists are still the game's own. Recording
        // costs nothing much, so it happens whether or not the option is on; turning it on later then just works.
        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        private static class ObjectDBAwake
        {
            [HarmonyPriority(Priority.First)]
            [HarmonyBefore("com.jotunn.jotunn")]
            private static void Prefix(ObjectDB __instance) => RecordItems(__instance.m_items);
        }

        // The main menu's ObjectDB has no items of its own; it takes them from the game's ObjectDB prefab here.
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static class ObjectDBCopy
        {
            [HarmonyPriority(Priority.First)]
            [HarmonyBefore("com.jotunn.jotunn")]
            private static void Prefix(ObjectDB other) => RecordItems(other?.m_items);
        }

        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        private static class ZNetSceneAwake
        {
            [HarmonyPriority(Priority.First)]
            [HarmonyBefore("com.jotunn.jotunn")]
            private static void Prefix(ZNetScene __instance) => RecordCreatures(__instance.m_prefabs);
        }

        [HarmonyPatch]
        private static class Filter
        {
            private static bool Prepare()
            {
                if (Initialize != null) return true;
                Logger.LogWarning("Could not find Achievements.Initialize; VanillaOnlyRequirements will have no effect.");
                return false;
            }

            private static MethodBase TargetMethod() => Initialize;

            private static void Postfix()
            {
                if (!ValConfig.VanillaOnlyRequirements.Value || Achievements.m_instance == null)
                {
                    return;
                }
                if (s_itemNames == null || s_pieceNames == null || s_creatureNames == null)
                {
                    Logger.LogWarning("Vanilla content was not recorded before the achievements were set up; leaving their requirements as they are.");
                    return;
                }

                foreach (AchievementList achievementList in Achievements.m_instance.m_achievementLists)
                {
                    foreach (Achievement achievement in achievementList.m_achievements)
                    {
                        if (!achievement.m_isDynamicallyPopulated)
                        {
                            continue;
                        }

                        int removed;
                        int left;
                        switch (achievement.m_id)
                        {
                            case "AllWeaponCraft":
                            case "AllFoodCooked":
                            case "AllItemCraft":
                                removed = RemoveNonVanilla(achievement, achievement.m_itemCraftTriggers, r => r.m_stat, s_itemNames);
                                left = achievement.m_itemCraftTriggers.Count;
                                break;
                            case "AllFoodEaten":
                                removed = RemoveNonVanilla(achievement, achievement.m_foodEatenTriggers, r => r.m_stat, s_itemNames);
                                left = achievement.m_foodEatenTriggers.Count;
                                break;
                            case "FindAllTrophies":
                                removed = RemoveNonVanilla(achievement, achievement.m_itemPickupTriggers, r => r.m_stat, s_itemNames);
                                left = achievement.m_itemPickupTriggers.Count;
                                break;
                            case "AllBuildPieces":
                                removed = RemoveNonVanilla(achievement, achievement.m_piecePlacedTriggers, r => r.m_stat, s_pieceNames);
                                left = achievement.m_piecePlacedTriggers.Count;
                                break;
                            case "KillAllCreatures":
                            case "KillAllCreaturesHard":
                                removed = RemoveNonVanilla(achievement, achievement.m_enemyStatsTriggers, r => r.m_stat, s_creatureNames);
                                left = achievement.m_enemyStatsTriggers.Count;
                                break;
                            default:
                                continue;
                        }

                        Logger.LogDebug($"{achievement.m_id}: dropped {removed} non-vanilla requirements ({left} left).");
                        if (removed > 0 && !achievement.m_unlocked && achievement.CheckUnlocked())
                        {
                            // No popup, matching the game's own pass in Initialize. The platform unlock callback
                            // synchronizes by itself.
                            Achievements.AchievementEvent(achievement, synchronize: false, showPopup: false);
                            Logger.LogInfo($"{achievement.m_id} is complete with only vanilla requirements; unlocked it.");
                        }
                    }
                }
            }

            // Requirements without a stat are left alone, as the game has them.
            private static int RemoveNonVanilla<T>(Achievement achievement, List<T> requirements, Func<T, string> stat, HashSet<string> vanilla)
            {
                List<string> dropped = null;
                int removed = requirements.RemoveAll(r =>
                {
                    string name = stat(r);
                    if (name == null || vanilla.Contains(name))
                    {
                        return false;
                    }
                    (dropped ??= new List<string>()).Add(name);
                    return true;
                });
                if (dropped != null)
                {
                    Logger.LogDebug($"{achievement.m_id}: dropped {string.Join(", ", dropped)}");
                }
                return removed;
            }
        }
    }
}
