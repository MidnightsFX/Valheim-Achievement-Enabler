using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Mono.Cecil;

namespace AchievementEnabler.Preloader
{
    /// <summary>
    /// BepInEx preloader patcher entry point. BepInEx discovers a patcher by this exact shape: a
    /// static <see cref="TargetDLLs"/> returning <c>IEnumerable&lt;string&gt;</c> and a static
    /// <c>Patch(AssemblyDefinition)</c>, which it calls once for each named assembly while loading it.
    ///
    /// Why a patcher and not a plugin: the thing we need to change is a <i>field read</i>.
    /// <c>Game.isModded</c> is a plain <c>public static bool</c>, and a read of it compiles to
    /// <c>ldsfld</c> inside whichever method is doing the checking. Harmony patches methods, not
    /// field accesses, so the only way to make a reader observe a different value is to rewrite the
    /// IL. Doing that here - before the runtime has ever seen the assembly - also sidesteps the two
    /// problems the Harmony route would have had: mapping Cecil methods back to runtime
    /// <c>MethodBase</c> handles, and the chance that Mono inlines a small reader into its caller
    /// before we get to patch it.
    ///
    /// IMPORTANT: during the preloader phase nothing here may touch a type whose graph reaches
    /// UnityEngine. Doing so loads UnityEngine.CoreModule before BepInEx injects its Chainloader
    /// hook into it and breaks the whole bootstrap. This assembly therefore references only Cecil
    /// and the Unity-free corners of BepInEx (<see cref="Paths"/>, <see cref="ConfigFile"/>,
    /// <see cref="ManualLogSource"/>), and never loads assembly_valheim itself - it only ever works
    /// on Cecil's model of it.
    ///
    /// Setting <c>Game.isModded = true</c>, and any gate that turns out to be a <i>method</i> rather
    /// than a field, are the companion plugin's job. Both need the game loaded; neither belongs here.
    /// </summary>
    public static class ModdedFlagPatcher
    {
        internal const string PatcherName = "AchievementEnabler.Preloader";

        /// <summary>
        /// AppDomain data slot the companion plugin reads to log what this patcher did. The value is
        /// a <see cref="Func{TResult}"/> of <c>string[]</c> - both BCL types - so the plugin needs no
        /// reference to this assembly and works fine when the patcher is absent entirely.
        /// </summary>
        internal const string SummaryKey = "AchievementEnabler.Preloader.Summary.v1";

        internal static readonly ManualLogSource Log = Logger.CreateLogSource(PatcherName);

        public static IEnumerable<string> TargetDLLs => new[] { "assembly_valheim.dll" };

        public static void Patch(AssemblyDefinition assembly)
        {
            // A patcher that throws can stop the game booting, and this one is not important enough
            // to be worth that. Everything below is best-effort: on failure we log and hand the
            // assembly back exactly as we got it.
            var summary = new List<string>();
            try
            {
                PatcherConfig config = PatcherConfig.Load();

                if (!config.Enabled)
                {
                    Log.LogInfo("Disabled in config; assembly_valheim left untouched.");
                    summary.Add("disabled in " + PatcherConfig.FileName);
                    return;
                }

                if (config.DumpSymbols)
                {
                    SymbolDump.Run(assembly.MainModule, config, Log);
                }

                RewriteResult result = FieldReadRewriter.Rewrite(assembly.MainModule, config, Log);
                summary.AddRange(result.SummaryLines);
            }
            catch (Exception ex)
            {
                Log.LogError("Patching failed, leaving assembly_valheim untouched: " + ex);
                summary.Add("FAILED: " + ex.Message);
            }
            finally
            {
                string[] lines = summary.ToArray();
                AppDomain.CurrentDomain.SetData(SummaryKey, (Func<string[]>)(() => lines));
            }
        }
    }

    /// <summary>
    /// The patcher's settings, in their own file rather than the plugin's.
    ///
    /// That split is deliberate and not just tidiness: everything here is applied while
    /// assembly_valheim is being loaded, so a change to any of it only takes effect on the next
    /// launch. Keeping it in a separate file from the plugin's live-editable settings is the
    /// clearest way to say so.
    /// </summary>
    internal sealed class PatcherConfig
    {
        internal const string FileName = ModdedFlagPatcher.PatcherName + ".cfg";

        internal bool Enabled;
        internal HashSet<string> Fields;
        internal HashSet<string> Allowlist;
        internal bool DumpSymbols;
        internal string[] Keywords;

        internal static PatcherConfig Load()
        {
            // saveOnInit: true so the file appears on first launch with every key and its
            // description, which is what makes the day-one workflow "edit the cfg" rather than
            // "read the source".
            var file = new ConfigFile(Path.Combine(Paths.ConfigPath, FileName), true);

            ConfigEntry<bool> enabled = file.Bind(
                "General", "Enabled", true,
                "Master switch for the IL rewrite. Turn this off to run the game with the patcher installed but inert.");

            ConfigEntry<string> fields = file.Bind(
                "Spoof", "Fields", "Game::isModded",
                "Comma-separated bool fields, written as Type::field, whose reads are folded to a constant.\n" +
                "Type is Cecil's full name, so a type in the global namespace is bare (Game::isModded) and a\n" +
                "namespaced one is qualified (Some.Namespace.Type::field).");

            ConfigEntry<string> allowlist = file.Bind(
                "Spoof", "Allowlist", "FejdStartup::SetupGui, Game::Awake",
                "Comma-separated methods, written as Type::Method, that keep seeing TRUE. Every other reader\n" +
                "sees FALSE. These are the places that only *report* the flag rather than gate on it - the\n" +
                "main-menu 'modded' label and the startup log line - so the game stays honest about being modded.");

            ConfigEntry<bool> dumpSymbols = file.Bind(
                "Diagnostics", "DumpSymbols", false,
                "Log every type, method and field in assembly_valheim whose name matches Keywords, plus every\n" +
                "method that reads one of the Fields above. Use this after a game update to find what the new\n" +
                "achievement code actually checks.");

            ConfigEntry<string> keywords = file.Bind(
                "Diagnostics", "Keywords", "achiev,unlock,steamuserstats,trophy,milestone",
                "Comma-separated, case-insensitive substrings that DumpSymbols matches names against.");

            return new PatcherConfig
            {
                Enabled = enabled.Value,
                Fields = ToSet(fields.Value),
                Allowlist = ToSet(allowlist.Value),
                DumpSymbols = dumpSymbols.Value,
                Keywords = ToArray(keywords.Value),
            };
        }

        private static HashSet<string> ToSet(string csv)
        {
            // Ordinal, case-sensitive: these are IL identifiers, and Game::isModded is not the same
            // name as Game::IsModded.
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string entry in ToArray(csv)) set.Add(entry);
            return set;
        }

        private static string[] ToArray(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return new string[0];
            string[] parts = csv.Split(',');
            var kept = new List<string>(parts.Length);
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0) kept.Add(trimmed);
            }
            return kept.ToArray();
        }
    }
}
