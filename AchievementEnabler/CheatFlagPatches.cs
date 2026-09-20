using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace AchievementEnabler
{
    /// <summary>
    /// The cheat marks the game keeps on characters, worlds and items, and never takes back.
    ///
    ///   * A character carries <c>PlayerProfile.m_usedCheats</c>. It is set the first time the character
    ///     runs a cheat command - which <see cref="AchievementGatePatches"/> now prevents - saved with the
    ///     profile, and nothing in the game ever clears it.
    ///   * A world has no stored flag. <c>Achievements.IsWorldCheated()</c> works it out on each call from
    ///     the world's starting keys: any key the World Modifiers screen cannot set counts as a cheat, and
    ///     those keys stay with the world.
    ///   * An item carries <c>ItemDrop.ItemData.m_cheated</c>, saved with the item and restored on load.
    ///
    /// None of them blocks achievements any more; the gate patch sees to that. What is left for the first
    /// two is the achievements panel, which reads both directly and shows a cheated notice for a flagged
    /// character or world. The item flag costs more than a notice: since 1.0.14 a flagged item will not
    /// stack with a clean one, so it is cleared as each item loads. Every toggle is read each time it
    /// applies, so changing one needs no restart.
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

        // Every saved flag comes back through here: this is the ZPackage reader for a single item, and it
        // is what a loading inventory, a chest, a tombstone and an item lying in the world all go through.
        // The two-argument overload, which hands back a fresh ItemData in a tuple, calls this one, so one
        // patch covers every caller. Dropping the flag here is what lets a formerly cheated stack merge
        // with a clean one again; it also means the flag is written out as clear the next time the item is
        // saved, which is the point of no return for this setting.
        //
        // ZDOMan's one-off world upgrade (the DeepNorthNoChunk migration) can put the flag back from a
        // legacy ZDO key straight after this runs, but it saves the item through the same blob, so the next
        // load clears it again. Not worth a patch of its own.
        [HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.Load),
            new[] { typeof(ZPackage), typeof(ItemDrop.ItemData), typeof(Version.Item) })]
        private static class ItemLoad
        {
            private static void Postfix(ItemDrop.ItemData itemData)
            {
                if (!ValConfig.ClearItemCheatFlagOnLoad.Value)
                {
                    return;
                }
                itemData.m_cheated = false;
            }
        }

        // Clearing the saved flag is not quite enough on its own. An inventory rebuilds each item through a
        // private Inventory.AddItem, which flags anything with over 10,000 total damage whatever the item
        // was saved as - the one cheat check in the game that ignores its own bypass switch. That runs after
        // the reader above, so on a server with damage-scaling mods every weapon comes back flagged each
        // session. Sweeping the inventory once it has finished loading catches it, and catches anything else
        // that flags an item on the way in.
        //
        // The two Load overloads have the same body copied out rather than one calling the other, so both
        // are patched; LoadOld is reached from both and is covered either way.
        [HarmonyPatch]
        private static class InventoryLoad
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.Load), new[] { typeof(ZPackage) });
                yield return AccessTools.Method(typeof(Inventory), nameof(Inventory.Load), new[] { typeof(ZPackage), typeof(bool) });
            }

            private static void Postfix(Inventory __instance)
            {
                if (!ValConfig.ClearItemCheatFlagOnLoad.Value)
                {
                    return;
                }

                foreach (ItemDrop.ItemData item in __instance.GetAllItems())
                {
                    item.m_cheated = false;
                }
            }
        }
    }
}
