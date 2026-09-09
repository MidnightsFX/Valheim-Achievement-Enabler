using System.Collections.Generic;
using BepInEx.Configuration;

#pragma warning disable IDE0130
namespace AchievementEnabler {
#pragma warning restore IDE0130
    /// <summary>
    /// The plugin's settings. Everything here is applied at runtime, so it can be reasoned about as
    /// live state - unlike the patcher's settings, which are read while assembly_valheim is loading
    /// and therefore only take effect on the next launch. That is why they are two separate files.
    ///
    /// Trimmed down from the JotunnModStub template's ValConfig: the BindServerConfig overloads and
    /// ConfigurationManagerAttributes there are Jotunn's, and there is nothing to server-sync here.
    /// </summary>
    internal class ValConfig {
        /// <summary>
        /// The achievement gate as of Valheim 1.0.7, and the reason this mod finally does something.
        ///
        /// <c>Achievements.IsCheatedAtAll()</c> ends with
        /// <c>m_cheatCheckCache = (usedCheats | cheatedItem | cheatedWorld) || Game.isModded;</c>,
        /// and <c>Achievements.CanGetAchievements()</c> - which guards every stat write on
        /// <c>PlayerProfile</c>, and so every unlock - is false whenever that is true. It is
        /// the only read of the flag in the game that decides anything, so wrapping this one method
        /// covers the entire achievement system.
        ///
        /// Still config rather than a constant: the next update can rename it, and editing a line in
        /// a .cfg is a far better answer to that than waiting for a new build of this mod.
        /// </summary>
        internal const string DefaultScopedFalseMethods = "Achievements::IsCheatedAtAll";

        private static ConfigFile cfg;

        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<bool> KeepFlagTrue;
        public static ConfigEntry<string> FlagMember;
        public static ConfigEntry<bool> EnableDebugMode;
        public static ConfigEntry<string> ScopedFalseMethods;
        public static ConfigEntry<string> ForceFalseMethods;

        public static void Init(ConfigFile config) {
            cfg = config;
            // Leave SaveOnConfigSet alone while binding - every Bind would otherwise write the file -
            // then flush once at the end. Same trick the template uses, for the same reason.
            cfg.SaveOnConfigSet = false;

            Enabled = cfg.Bind("General", "Enabled", true,
                "Master switch. Off means no patching at all; the flag is left exactly as other mods set it.");

            KeepFlagTrue = cfg.Bind("General", "KeepFlagTrue", true,
                "Set the modded flag to true at startup, so the main menu shows the 'modded' label and the\n" +
                "startup log stays honest. Leave this on: the whole point is that the flag is true and only\n" +
                "the gates are lied to. Mod loaders like Jotunn already set it, but not every setup has one.");

            FlagMember = cfg.Bind("General", "FlagMember", "Game::isModded",
                "Where the modded flag lives, as Type::member. Resolved by name at runtime, so it copes with\n" +
                "the flag being a field or a property. Change this only if the game renames or moves it.");

            EnableDebugMode = cfg.Bind("General", "EnableDebugMode", false,
                "Verbose logging.");

            ScopedFalseMethods = cfg.Bind("Spoof", "ScopedFalseMethods", DefaultScopedFalseMethods,
                "Comma-separated methods, as Type::Method, that should see the modded flag as FALSE.\n" +
                "The flag is set false for the duration of each call and restored afterwards, so it stays true\n" +
                "everywhere else. This is the main mechanism.\n" +
                "The default is Valheim 1.0's achievement gate. Achievements::IsCheatedAtAll is the only place\n" +
                "in the game that reads the modded flag to decide anything, and everything achievement-related\n" +
                "reaches it through Achievements::CanGetAchievements. Swapping the flag just for that call\n" +
                "leaves the real cheat checks it also performs - console cheats, cheated items, cheated world\n" +
                "modifiers - completely intact, so cheating still costs you achievements.\n" +
                "Clear this to turn the scoped swap off.");

            ForceFalseMethods = cfg.Bind("Spoof", "ForceFalseMethods", "",
                "Comma-separated bool-returning methods, as Type::Method, whose result is forced to false.\n" +
                "Use this when the gate is a method rather than a flag read - e.g. a Game::IsModded() helper.\n" +
                "Empty by default: Valheim 1.0's gate is a flag read, which the scoped swap above covers.");

            cfg.Save();
            cfg.SaveOnConfigSet = true;

            Logger.CheckEnableDebugLogging();
            EnableDebugMode.SettingChanged += Logger.EnableDebugLogging;
        }

        /// <summary>Splits a comma-separated config value, dropping blanks and trimming each entry.</summary>
        public static string[] Split(string csv) {
            if (string.IsNullOrEmpty(csv)) return new string[0];
            var kept = new List<string>();
            foreach (string part in csv.Split(',')) {
                string trimmed = part.Trim();
                if (trimmed.Length > 0) kept.Add(trimmed);
            }
            return kept.ToArray();
        }
    }
}
