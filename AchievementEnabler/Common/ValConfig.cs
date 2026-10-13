using BepInEx.Configuration;

#pragma warning disable IDE0130
namespace AchievementEnabler {
#pragma warning restore IDE0130
    /// <summary>
    /// The plugin's settings. Enabled is read once, at startup; the rest are read each time they apply,
    /// so they can be changed while the game is running.
    ///
    /// Trimmed down from the JotunnModStub template's ValConfig: the BindServerConfig overloads and
    /// ConfigurationManagerAttributes there are Jotunn's, and there is nothing to server-sync here.
    /// </summary>
    internal class ValConfig {
        private static ConfigFile cfg;

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> EnableDebugMode;
        public static ConfigEntry<bool> ClearCharacterCheatFlag;
        public static ConfigEntry<bool> IgnoreWorldCheatFlag;
        public static ConfigEntry<bool> ClearItemCheatFlagOnLoad;
        public static ConfigEntry<bool> BackfillAchievementStats;
        public static ConfigEntry<bool> KeepBestStats;
        public static ConfigEntry<bool> ShowAllRequirements;
        public static ConfigEntry<bool> ShowSecretAchievements;
        public static ConfigEntry<bool> VanillaOnlyRequirements;

        public static void Init(ConfigFile config) {
            cfg = config;
            // Leave SaveOnConfigSet alone while binding - every Bind would otherwise write the file -
            // then flush once at the end. Same trick the template uses, for the same reason.
            cfg.SaveOnConfigSet = false;

            Enabled = cfg.Bind("General", "Enabled", true,
                "Master switch, read at startup. Off means nothing is patched and the clearcheateditems command\n" +
                "is not added.");

            EnableDebugMode = cfg.Bind("General", "EnableDebugMode", false,
                "Verbose logging.");

            ClearCharacterCheatFlag = cfg.Bind("CheatFlags", "ClearCharacterCheatFlag", true,
                "Clear the cheat flag from a character when it loads. The flag no longer blocks achievements, and\n" +
                "cheat commands no longer set it, but without this a character flagged before still gets a cheated\n" +
                "notice in the achievements panel.");

            IgnoreWorldCheatFlag = cfg.Bind("CheatFlags", "IgnoreWorldCheatFlag", true,
                "Never treat a world as cheated. The game counts a world as cheated for good once it has a starting\n" +
                "key the World Modifiers screen cannot set. That no longer blocks achievements, but without this\n" +
                "such a world still gets a cheated notice in the achievements panel. The keys are left alone.");

            ClearItemCheatFlagOnLoad = cfg.Bind("CheatFlags", "ClearItemCheatFlagOnLoad", true,
                "Clear the cheated flag from every item as it loads, wherever it loads from: your inventory,\n" +
                "chests, tombstones and items lying in the world. Since Valheim 1.0.14 a flagged item will not\n" +
                "stack with a clean one, so a single flagged stack keeps splitting every stack of that item you\n" +
                "pick up. Clearing it is what puts stacking back. The flag is gone from the save the next time\n" +
                "the item is written, so this cannot be undone by turning the setting off again.");

            BackfillAchievementStats = cfg.Bind("Achievements", "BackfillAchievementStats", true,
                "Count progress you made while achievements were switched off. The game records every stat twice:\n" +
                "once in a raw table that always counts, and once in the table achievements actually read, which\n" +
                "it skips whenever cheats or mods are detected. Anything done before this mod was installed is\n" +
                "therefore in the raw table only, and the game never goes back for it - which is why a character\n" +
                "with hundreds of trees felled can still show no progress towards Lumberjack. This copies the raw\n" +
                "numbers across when the character loads, taking the higher of the two, so nothing is counted twice.");

            KeepBestStats = cfg.Bind("Achievements", "KeepBestStats", true,
                "Stop the game lowering your best-ever stats, and put back the ones it already lowered: most days\n" +
                "survived in a row, highest comfort, highest piece built, and the most of each kind of piece built\n" +
                "around you. The game checks a new value against your best at the world's current combat difficulty\n" +
                "only, then writes it over your overall best, so the first days after a change of difficulty can\n" +
                "write a small number over a large one and relock achievements such as surviving 30 days. With this\n" +
                "on, a best is only ever raised, your best run of days keeps up with your current one, and bests the\n" +
                "game already lowered are put back from its per-difficulty records when the character loads.");

            ShowAllRequirements = cfg.Bind("AchievementsPanel", "ShowAllRequirements", false,
                "Show what the achievements you already know about ask for. Every achievement in the panel that is\n" +
                "not concealed opens, including the ones the game keeps shut, such as The Homesteader, and its\n" +
                "details name every requirement with your progress, not only the ones already met. Achievements\n" +
                "earned by holding others list those others. Secret achievements you have not earned stay\n" +
                "concealed, and so does any requirement that would give one away, such as the bosses The Viking\n" +
                "asks for. Turn on ShowSecretAchievements too to see every achievement and every requirement.\n" +
                "Client side: changes only what your own panel shows, never what counts or unlocks, and takes\n" +
                "effect the next time the panel is opened.");

            ShowSecretAchievements = cfg.Bind("AchievementsPanel", "ShowSecretAchievements", false,
                "Show every achievement in the achievements panel. Secret achievements are listed under their real\n" +
                "name and description instead of \"Concealed\" and \"Only Odin knows what is hidden here.\". With\n" +
                "ShowAllRequirements on too, every achievement opens and names every requirement. Off by default\n" +
                "because it spoils them. Client side: changes only what your own panel shows, never what counts or\n" +
                "unlocks, and takes effect the next time the panel is opened.");
            VanillaOnlyRequirements = cfg.Bind("Achievements", "VanillaOnlyRequirements", false,
                "Limit the craft-, eat-, build-, find- and kill-everything achievements to vanilla items, food, build\n" +
                "pieces, trophies and creatures, leaving out what other mods add. Client-side, and can be changed\n" +
                "while the game is running. Achievements this lets you unlock stay unlocked if you turn it off again.");

            cfg.Save();
            cfg.SaveOnConfigSet = true;

            Logger.CheckEnableDebugLogging();
            EnableDebugMode.SettingChanged += Logger.EnableDebugLogging;
            VanillaOnlyRequirements.SettingChanged += VanillaRequirementPatches.Reapply;
        }
    }
}
