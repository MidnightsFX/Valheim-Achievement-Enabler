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

            cfg.Save();
            cfg.SaveOnConfigSet = true;

            Logger.CheckEnableDebugLogging();
            EnableDebugMode.SettingChanged += Logger.EnableDebugLogging;
        }
    }
}
