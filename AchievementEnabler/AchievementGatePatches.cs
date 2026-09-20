using System.Reflection;
using HarmonyLib;

namespace AchievementEnabler
{
    /// <summary>
    /// The two checks that decide whether an achievement stat is recorded, the game's own switch for
    /// ignoring cheats and the new items made where the game forgets to check it, and the vanilla commands that lean
    /// on the second check.
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
    ///
    /// One achievement check is also repaired here: the one for achievements awarded for holding other
    /// achievements, which vanilla never fires.
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

        // Child of Odin and Kings & Queens are awarded for holding other achievements, and vanilla never
        // hands them out while you are playing. The check that should do it reads
        //
        //     ... && achievement.CheckUnlocked() && !achievement.m_unlocked
        //
        // but CheckUnlocked() sets m_unlocked before returning true, so the second test is always false and
        // the award never happens. Every other check in Achievements has the two the right way round. What
        // is left is the sweep on world load, which resets m_unlocked for everything and re-tests in list
        // order - so a meta achievement listed before the achievements it depends on is missed there too,
        // and a player can be owed one indefinitely.
        //
        // Replaced with the same walk, testing m_unlocked first, and repeated until a pass awards nothing so
        // that order in the list stops mattering.
        [HarmonyPatch(typeof(Achievements), nameof(Achievements.CheckMetaAchievementProgressUnlock))]
        private static class MetaAchievements
        {
            private static bool running;

            private static void Award(Achievement achievement)
            {
                // Quietly while the game is re-testing everything on world load: the player has had these
                // before and does not want a toast for each one every time they load in.
                if (InitializeSweep.Running)
                {
                    Logger.LogDebug($"'{achievement.m_id}' is earned by holding other achievements, and was already due.");
                    Achievements.AchievementEvent(achievement, synchronize: false, showPopup: false);
                    return;
                }

                Logger.LogInfo($"Awarding '{achievement.m_id}', which is earned by holding other achievements.");
                Achievements.AchievementEvent(achievement);
            }

            private static bool Prefix(string calleeId)
            {
                // AchievementEvent ends by calling this method again, so an award made below comes straight
                // back in here. The loop already covers what that call would have done.
                if (running || Achievements.m_instance == null)
                {
                    return false;
                }

                running = true;
                try
                {
                    bool awardedAny;
                    do
                    {
                        awardedAny = false;
                        foreach (AchievementList list in Achievements.m_instance.m_achievementLists)
                        {
                            foreach (Achievement achievement in list.m_achievements)
                            {
                                if (achievement.m_id == calleeId || achievement.m_otherAchievementTriggers.Count == 0)
                                {
                                    continue;
                                }
                                if (achievement.m_unlocked || !achievement.CheckUnlocked())
                                {
                                    continue;
                                }

                                Award(achievement);
                                awardedAny = true;
                            }
                        }
                    }
                    while (awardedAny);
                }
                finally
                {
                    running = false;
                }

                return false;
            }
        }

        // Achievements.Initialize() runs when a world loads: it forgets every achievement, re-tests them all
        // and re-awards the ones already earned quietly, with no popup, because the player has seen them
        // before. Each of those awards calls the meta check above, so without knowing we are inside it the
        // repaired check would pop a toast for Child of Odin every single load.
        //
        // Private, so named by string. If a future version renames it the patch fails on its own and the
        // only cost is that popup.
        [HarmonyPatch(typeof(Achievements), "Initialize")]
        private static class InitializeSweep
        {
            internal static bool Running;

            private static void Prefix() => Running = true;

            // A finalizer, not a postfix, so a sweep that throws part-way still clears the flag.
            private static void Finalizer() => Running = false;
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

        // The switch is checked separately wherever the game flags something, and three places that drop new items
        // skip the check. A felled log (TreeLog) and a rock or ore vein (MineRock5) flag everything they drop when
        // the tool that broke them is flagged, and a cooking station hands back cooked food with the flag of the raw
        // item put on it. So an axe or pickaxe flagged before this mod was installed kept turning wood, copper and
        // tin into cheated items, while stone from a small rock, which does check, came out clean. Every one of
        // those drops is created through this method, as are drops from pickables, destroyed pieces, smelters and
        // fermenters, so the flag is refused here for all of them. The GameObject overload calls this one.
        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.OnCreateNew), new[] { typeof(ItemDrop), typeof(bool) })]
        private static class NewItemDrop
        {
            private static void Prefix(ref bool cheated)
            {
                cheated = false;
            }
        }

        // Creature loot does not go through OnCreateNew. The switch stops creatures being flagged, but one flagged
        // before - spawned before this mod was installed, or by a player without it - keeps the flag in the world
        // save, and its loot copies that flag, whether it drops from the body or later from the ragdoll. Both call
        // this, and it sets the flag on each item itself.
        [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.DropItems))]
        private static class CreatureLoot
        {
            private static void Prefix(ref bool cheated)
            {
                cheated = false;
            }
        }

        // Traders do not check the switch either. Buying flags the new item if any coins you carry are flagged, and
        // selling flags the coins if any stack of what you sold is flagged. Both create the item through this
        // overload, as do starting item sets, which already check the switch, and the tombstone command, which flags
        // its coin every time. Crafting uses the overload this one calls, and checks the switch. Loading an
        // inventory uses neither, so flags already saved on items are left alone.
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(long), typeof(string), typeof(bool), typeof(bool) })]
        private static class NewInventoryItem
        {
            private static void Prefix(ref bool cheated)
            {
                cheated = false;
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
