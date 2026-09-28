using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace AchievementEnabler
{
    /// <summary>
    /// Two optional views of the achievements panel, each behind its own setting.
    ///
    /// The panel keeps things back in three ways. A secret achievement that is still locked is listed as
    /// "Concealed", described as "Only Odin knows what is hidden here.", and cannot be opened. Some achievements
    /// cannot be opened at all, secret or not. And in an achievement's details, each requirement not met yet
    /// reads "???" with "??? / ???" for progress; only finished ones are named.
    ///
    /// <c>ShowSecretAchievements</c> lists every secret achievement under its own name and description, and
    /// lets it open whenever the game lets that achievement open at all. Its details are then the same as any
    /// other achievement's.
    ///
    /// <c>ShowAllRequirements</c> covers the achievements the player already knows about: every one that is not
    /// concealed opens, every requirement is named with the progress towards it (unmet grey, met green, as
    /// before), and an achievement earned by holding others lists those others, which the game's details never
    /// show. What would give a concealed achievement away stays hidden: an unmet requirement it shares with
    /// one, such as the bosses The Viking asks for, still reads "???". With both settings on, nothing is
    /// concealed, so every achievement opens and names every requirement.
    ///
    /// Display only. Nothing here changes what counts or what unlocks, and both settings are read every time the
    /// panel opens, so they can be switched while playing.
    /// </summary>
    internal static class AchievementsPanelPatches
    {
        // Unmet requirements that belong to an achievement still concealed, gathered each time the details open.
        // Stats by PlayerStatType name, everything else by key, which is how CreateStatRow receives them.
        private static readonly HashSet<string> concealedRequirements = new HashSet<string>();

        // Stands in for each read of Achievement.m_isSecret in the panel code. It takes the achievement and
        // leaves a bool, exactly as the field load it replaces did.
        private static bool IsSecret(Achievement achievement)
        {
            return achievement.m_isSecret && !ValConfig.ShowSecretAchievements.Value;
        }

        // Stands in for the read of Achievement.m_clickable, the same way.
        private static bool IsClickable(Achievement achievement)
        {
            return achievement.m_clickable || ValConfig.ShowAllRequirements.Value;
        }

        // Listed as "Concealed" and kept shut: the game's test, with the setting applied.
        private static bool IsConcealed(Achievement achievement)
        {
            return IsSecret(achievement) && !achievement.m_unlocked;
        }

        private static void GatherConcealedRequirements()
        {
            concealedRequirements.Clear();
            foreach (AchievementList list in Achievements.m_instance.m_achievementLists)
            {
                foreach (Achievement achievement in list.m_achievements)
                {
                    if (!IsConcealed(achievement)) continue;

                    foreach (Achievement.PlayerStatRequirement requirement in achievement.m_statTrigger)
                    {
                        concealedRequirements.Add(requirement.m_stat.ToString());
                    }
                    foreach (Achievement.EnemyStatRequirement requirement in achievement.m_enemyStatsTriggers)
                    {
                        concealedRequirements.Add(requirement.m_stat);
                    }
                    foreach (List<Achievement.DictStatRequirement> triggers in new[] {
                        achievement.m_knownWorldTriggers, achievement.m_knownWorldKeysTriggers, achievement.m_knownCommandsTriggers,
                        achievement.m_piecePlacedTriggers, achievement.m_itemPickupTriggers, achievement.m_itemCraftTriggers,
                        achievement.m_foodEatenTriggers, achievement.m_pickableTriggers })
                    {
                        foreach (Achievement.DictStatRequirement requirement in triggers)
                        {
                            concealedRequirements.Add(requirement.m_stat);
                        }
                    }
                }
            }
        }

        // Swaps each load of an Achievement field for a call to one of the stand-ins above; returns how many.
        private static int ReplaceFieldReads(List<CodeInstruction> code, string field, string replacement)
        {
            FieldInfo read = AccessTools.Field(typeof(Achievement), field);
            MethodInfo call = AccessTools.Method(typeof(AchievementsPanelPatches), replacement);

            int replaced = 0;
            foreach (CodeInstruction instruction in code)
            {
                if (!instruction.LoadsField(read)) continue;

                // Changed in place rather than swapped for a new instruction, so any label attached to it stays.
                instruction.opcode = OpCodes.Call;
                instruction.operand = call;
                replaced++;
            }
            return replaced;
        }

        // Adds one row to the open details, laid out the way the game lays out a met one.
        private static void AddRow(AchievementDetailUnlockCondition prefab, RectTransform root, string name, float current, float total)
        {
            AchievementDetailUnlockCondition row = UnityEngine.Object.Instantiate(prefab, root);
            row.gameObject.SetActive(true);

            Color color = current >= total ? Color.green : Color.gray;
            row.StatName.color = color;
            row.Progress.color = color;
            row.StatName.text = name;
            // Rounded down, so a requirement that is nearly met never reads as done. Distances sailed and
            // travelled are fractional, and the full float is noise.
            row.Progress.text = $"{Mathf.FloorToInt(current)} / {total}";
        }

        // What a requirement row is called. A plain stat arrives as the enum member's name and is localized
        // under $stat_; everything else already arrives as a key, like $enemy_troll.
        private static string RequirementName(string statKey)
        {
            if (!Enum.TryParse(statKey, out PlayerStatType _))
            {
                string localized = Localization.instance.Localize(statKey);
                return string.IsNullOrEmpty(localized) ? statKey : localized;
            }

            string key = "stat_" + statKey;
            string name = Localization.instance.Localize("$" + key);
            if (name != "[" + key + "]")
            {
                return name;
            }

            // The game has no text for the stats behind most of the achievements it never lets you open, such
            // as BuildClusterFloor for The Homesteader, and a missing key comes back in brackets. Spell out the
            // member name instead: "Build cluster floor".
            string spaced = Regex.Replace(statKey, "(?<=[a-z])(?=[A-Z0-9])", " ");
            return spaced.Substring(0, 1) + spaced.Substring(1).ToLowerInvariant();
        }

        // Builds the list: each achievement's name, description and icon, and whether it can be opened. Reads
        // m_isSecret twice and m_clickable once. Private, so named by string.
        [HarmonyPatch(typeof(InventoryGui), "UpdateAchievementsList")]
        private static class AchievementsList
        {
            // Harmony reruns transpilers each time another mod patches the same method; warn once.
            private static bool warned;

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var code = new List<CodeInstruction>(instructions);
                int secret = ReplaceFieldReads(code, nameof(Achievement.m_isSecret), nameof(IsSecret));
                int clickable = ReplaceFieldReads(code, nameof(Achievement.m_clickable), nameof(IsClickable));

                if (!warned && secret == 0)
                {
                    Logger.LogWarning("Could not find the secret-achievement check in InventoryGui.UpdateAchievementsList, so secret achievements stay concealed there.");
                }
                if (!warned && clickable == 0)
                {
                    Logger.LogWarning("Could not find the clickable check in InventoryGui.UpdateAchievementsList, so achievements the game keeps shut stay shut.");
                }
                warned |= secret == 0 || clickable == 0;
                return code;
            }
        }

        // Opens the details. A locked secret achievement gets a single "???" row here and nothing else, which
        // matters for the two secret achievements the game does let you open: That Can't Be an Accident and
        // The Treasure Hunter.
        [HarmonyPatch(typeof(AchievementsGui), nameof(AchievementsGui.OnOpenAchievementDetails))]
        private static class AchievementDetails
        {
            private static bool warned;

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var code = new List<CodeInstruction>(instructions);
                if (ReplaceFieldReads(code, nameof(Achievement.m_isSecret), nameof(IsSecret)) == 0 && !warned)
                {
                    warned = true;
                    Logger.LogWarning("Could not find the secret-achievement check in AchievementsGui.OnOpenAchievementDetails, so secret achievements stay concealed there.");
                }
                return code;
            }

            // Whether this call fills the details, decided before it runs: afterwards they are open either way.
            // Same conditions as the original: not already open, clickable, and not concealed. Also gathers what
            // the rows must not give away, before the original starts adding them; the rows it adds a few at a
            // time over the next frames read the same set.
            private static void Prefix(AchievementsGui __instance, Achievement achievement, bool clickable, out bool __state)
            {
                __state = clickable && !__instance.m_achievementDetails.activeSelf && !IsConcealed(achievement);
                if (__state)
                {
                    GatherConcealedRequirements();
                }
            }

            // Child of Odin and Kings & Queens are earned by holding other achievements, and the game's details
            // have no rows for those, so with every requirement shown they would open empty. One row each,
            // after the rows the game adds. A concealed one keeps its "Concealed" name here too.
            private static void Postfix(
                AchievementDetailUnlockCondition ___m_achievementDetailsElementPrefab,
                RectTransform ___m_achievementDetailsListRoot,
                Achievement achievement, bool __state)
            {
                if (!__state || !ValConfig.ShowAllRequirements.Value)
                {
                    return;
                }

                foreach (Achievement other in achievement.m_otherAchievementTriggers)
                {
                    string name = IsConcealed(other) ? "$inventory_achievement_secret" : other.m_name;
                    AddRow(___m_achievementDetailsElementPrefab, ___m_achievementDetailsListRoot,
                        Localization.instance.Localize(name), other.m_unlocked ? 1f : 0f, 1f);
                }
            }
        }

        // One row of the details. For a requirement not met yet the game writes "???" and "??? / ???"; this
        // builds the row itself instead, named and with progress, in the game's grey or green. Met rows are
        // included so a stat with no text gets a readable name there too. An unmet row that would give away a
        // concealed achievement is left to the game. A prefix rather than a postfix because the original
        // rewrites statKey as it goes, and a postfix would see the rewritten value.
        [HarmonyPatch(typeof(AchievementsGui), "CreateStatRow")]
        private static class RequirementRow
        {
            private static bool Prefix(
                AchievementDetailUnlockCondition ___m_achievementDetailsElementPrefab,
                RectTransform ___m_achievementDetailsListRoot,
                string statKey, float currentAmount, float totalAmount)
            {
                if (!ValConfig.ShowAllRequirements.Value
                    || (currentAmount < totalAmount && concealedRequirements.Contains(statKey)))
                {
                    return true;
                }

                AddRow(___m_achievementDetailsElementPrefab, ___m_achievementDetailsListRoot,
                    RequirementName(statKey), currentAmount, totalAmount);
                return false;
            }
        }
    }
}
