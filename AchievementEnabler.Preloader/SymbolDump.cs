using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AchievementEnabler.Preloader
{
    /// <summary>
    /// Day-one diagnostics. Off by default.
    ///
    /// The whole design of this mod assumes the achievement code can move under it - it was written
    /// before that code existed at all, and the gate it targets today only appeared in Valheim 1.0.
    /// Rather than decompile the game again after every update, turn this on and the patcher reports
    /// what the new assembly contains: anything named like achievement machinery, and every method
    /// that reads the fields we are already targeting. The <see cref="ModuleDefinition"/> is in hand
    /// either way, so the scan is nearly free.
    ///
    /// On 1.0.7 the "methods reading a targeted field" section is the useful one, and it is four
    /// lines long - <c>Achievements::IsCheatedAtAll</c> is the gate, and the other three are the
    /// menu label, the startup log line and the static initialiser.
    ///
    /// This looks at names only. It is a lead generator, not an analysis.
    /// </summary>
    internal static class SymbolDump
    {
        // A gate is a handful of members; thousands of hits means the keywords are too loose, and
        // dumping all of them would just bury the log.
        private const int MaxHitsPerSection = 200;

        internal static void Run(ModuleDefinition module, PatcherConfig config, ManualLogSource log)
        {
            if (config.Keywords.Length == 0)
            {
                log.LogWarning("DumpSymbols is on but Keywords is empty; nothing to look for.");
                return;
            }

            log.LogInfo($"=== symbol dump: {module.Name}, keywords [{string.Join(", ", config.Keywords)}] ===");

            var types = new List<string>();
            var members = new List<string>();
            var readers = new List<string>();

            foreach (TypeDefinition type in module.GetTypes())
            {
                if (Matches(type.Name, config.Keywords)) types.Add("  type   " + type.FullName);

                foreach (FieldDefinition field in type.Fields)
                {
                    if (Matches(field.Name, config.Keywords))
                        members.Add($"  field  {type.FullName}::{field.Name} : {field.FieldType.Name}");
                }

                foreach (MethodDefinition method in type.Methods)
                {
                    if (Matches(method.Name, config.Keywords))
                        members.Add($"  method {type.FullName}::{method.Name} : {method.ReturnType.Name}");

                    if (!method.HasBody) continue;

                    // Two things worth knowing from the body: who reads the fields we target, and
                    // who calls anything keyword-shaped (a call into Steamworks, say, would show up
                    // here even though the calling method's own name says nothing).
                    bool readsTarget = false, callsMatch = false;
                    foreach (Instruction ins in method.Body.Instructions)
                    {
                        var field = ins.Operand as FieldReference;
                        if (field != null && config.Fields.Contains(field.DeclaringType.FullName + "::" + field.Name))
                        {
                            readsTarget = true;
                            continue;
                        }

                        var call = ins.Operand as MethodReference;
                        if (call != null && (Matches(call.Name, config.Keywords) || Matches(call.DeclaringType.Name, config.Keywords)))
                            callsMatch = true;
                    }

                    if (readsTarget) readers.Add($"  reads  {type.FullName}::{method.Name}");
                    if (callsMatch) members.Add($"  calls  {type.FullName}::{method.Name} -> keyword-matching member");
                }
            }

            Report(log, "types matching keywords", types);
            Report(log, "members matching keywords", members);
            Report(log, "methods reading a targeted field", readers);
            log.LogInfo("=== end symbol dump ===");
        }

        private static void Report(ManualLogSource log, string heading, List<string> hits)
        {
            if (hits.Count == 0)
            {
                log.LogInfo($"-- {heading}: none");
                return;
            }

            log.LogInfo($"-- {heading}: {hits.Count}");
            for (int i = 0; i < hits.Count && i < MaxHitsPerSection; i++) log.LogInfo(hits[i]);
            if (hits.Count > MaxHitsPerSection)
                log.LogInfo($"  ... {hits.Count - MaxHitsPerSection} more (narrow Keywords to see them)");
        }

        private static bool Matches(string name, string[] keywords)
        {
            foreach (string keyword in keywords)
            {
                if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
    }
}
