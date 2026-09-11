using System.Collections.Generic;

namespace AchievementEnabler
{
    /// <summary>
    /// <c>clearcheateditems</c>: clears the cheated flag from every item the local character is carrying.
    ///
    /// Item flags are the one cheat mark the game scatters rather than keeps in one place. With
    /// <c>s_bypassCheatChecks</c> on the game stops creating them, but items flagged before - or in the game of
    /// a player without this mod - keep theirs. A flagged item no longer affects achievements, but its tooltip
    /// still says it is cheated, the achievements panel still shows a cheated notice while you carry it, and
    /// the flag is saved with the item for whenever this mod is not installed. Clearing them is an explicit,
    /// admin-only command rather than something that happens on load.
    ///
    /// The admin check is the game's own <c>ZNet.LocalPlayerIsAdminOrHost()</c>: true for the host,
    /// singleplayer included, and on a dedicated server for anyone in the adminlist.txt the server sends
    /// each client. Like every achievement check, it runs on the client.
    ///
    /// One case this cannot fix for good: the game flags any item with over 10,000 total damage as cheated
    /// again whenever an inventory loads, so such an item stays clear only until the character next loads.
    /// </summary>
    internal static class ClearCheatedItemsCommand
    {
        internal const string Name = "clearcheateditems";

        internal static void Register()
        {
            // Not isCheat, which would put it behind devcommands. isNetwork makes it invalid in the main menu,
            // where there is no character.
            _ = new Terminal.ConsoleCommand(Name,
                "clears the cheated flag from every item you are carrying (admin only)",
                (Terminal.ConsoleEventFailable)Run,
                isCheat: false,
                isNetwork: true);
        }

        // A string return is printed by the game as "Error executing command: ...".
        private static object Run(Terminal.ConsoleEventArgs args)
        {
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null)
            {
                return "you need to be in a world with a character loaded";
            }
            if (!ZNet.instance.LocalPlayerIsAdminOrHost())
            {
                return "only admins can clear cheated items";
            }

            var cleared = new List<string>();
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (!item.m_cheated) continue;

                item.m_cheated = false;
                string name = Localization.instance.Localize(item.m_shared.m_name);
                cleared.Add(item.m_stack > 1 ? $"{name} x{item.m_stack}" : name);
            }

            if (cleared.Count == 0)
            {
                args.Context.AddString("Nothing to clear: none of the items you are carrying are flagged as cheated.");
                return true;
            }

            args.Context.AddString($"Cleared the cheated flag from {cleared.Count} item(s): {string.Join(", ", cleared)}");
            Logger.LogInfo($"{Name} cleared the cheated flag from {cleared.Count} item(s).");
            return true;
        }
    }
}
