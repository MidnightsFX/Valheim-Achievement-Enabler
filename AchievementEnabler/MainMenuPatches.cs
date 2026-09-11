using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace AchievementEnabler
{
    /// <summary>
    /// The main menu keeps saying the game is modded.
    ///
    /// <c>FejdStartup.SetupGui</c> shows its "modded" label with <c>m_moddedText.SetActive(Game.isModded)</c>.
    /// The argument to that call is rewritten to the constant <c>true</c>, so the label shows no matter what the
    /// flag holds - this plugin being loaded is proof enough. The flag itself is left as mods set it.
    ///
    /// The match is on the call, not on the flag read. Anything that already rewrote the read - the 0.2.0
    /// preloader patcher folded it to a constant before Harmony ever saw it - leaves a different instruction
    /// in that slot, and the label is still the thing to fix.
    /// </summary>
    internal static class MainMenuPatches
    {
        [HarmonyPatch(typeof(FejdStartup), "SetupGui")]
        private static class ModdedLabel
        {
            // Harmony rebuilds a patched method, rerunning every transpiler on it, each time another mod patches
            // it too. Without this the warning below repeats once per mod that touches SetupGui.
            private static bool warned;

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                FieldInfo moddedText = AccessTools.Field(typeof(FejdStartup), nameof(FejdStartup.m_moddedText));
                MethodInfo setActive = AccessTools.Method(typeof(UnityEngine.GameObject), nameof(UnityEngine.GameObject.SetActive));
                var code = new List<CodeInstruction>(instructions);

                int replaced = 0;
                for (int i = 0; i + 2 < code.Count; i++)
                {
                    // Load m_moddedText, push SetActive's bool with one instruction, call SetActive.
                    if (!code[i].LoadsField(moddedText) || !code[i + 2].Calls(setActive)) continue;

                    // Changed in place rather than swapped for a new instruction, so any label or exception block
                    // attached to it stays attached.
                    code[i + 1].opcode = OpCodes.Ldc_I4_1;
                    code[i + 1].operand = null;
                    replaced++;
                }

                if (replaced == 0 && !warned)
                {
                    warned = true;
                    Logger.LogWarning("Could not find m_moddedText.SetActive in FejdStartup.SetupGui, so the main menu's modded label was not changed.");
                }
                return code;
            }
        }
    }
}
