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
    /// That read of the flag is rewritten to the constant <c>true</c>, so the label shows no matter what the
    /// flag holds - this plugin being loaded is proof enough. The flag itself is left as mods set it.
    /// </summary>
    internal static class MainMenuPatches
    {
        [HarmonyPatch(typeof(FejdStartup), "SetupGui")]
        private static class ModdedLabel
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                FieldInfo isModded = AccessTools.Field(typeof(Game), nameof(Game.isModded));
                var code = new List<CodeInstruction>(instructions);

                int replaced = 0;
                foreach (CodeInstruction instruction in code)
                {
                    if (!instruction.LoadsField(isModded)) continue;

                    // Changed in place rather than swapped for a new instruction, so any label or exception
                    // block attached to it stays attached.
                    instruction.opcode = OpCodes.Ldc_I4_1;
                    instruction.operand = null;
                    replaced++;
                }

                if (replaced == 0)
                {
                    Logger.LogWarning("FejdStartup.SetupGui no longer reads Game.isModded, so the main menu's modded label was not changed.");
                }
                return code;
            }
        }
    }
}
