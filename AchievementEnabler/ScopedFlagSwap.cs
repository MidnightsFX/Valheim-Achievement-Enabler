using System.Reflection;
using HarmonyLib;

namespace AchievementEnabler
{
    /// <summary>
    /// The primary mechanism, and a direct reading of the requirement: the modded flag stays
    /// <c>true</c> everywhere, except for the duration of the specific methods that gate on it.
    ///
    /// A prefix stashes the current value and sets the flag false; a finalizer puts it back. Because
    /// a finalizer runs even when the method throws, the flag cannot be left stuck false. Nesting is
    /// safe without a counter: the inner swap saves <c>false</c> and restores <c>false</c>, and the
    /// outer one restores <c>true</c>.
    ///
    /// Why this rather than rewriting the field read in IL, which is where this mod started: we do
    /// not know what the gate looks like yet, and this covers far more of the shapes it could take.
    /// It works whether the read compiles to <c>ldsfld</c> or <c>ldsflda</c> (the latter is real -
    /// <c>Game.isModded.ToString()</c> in <c>Game.Awake</c> compiles that way, and no constant can
    /// be folded into an address-of); it works if the flag is promoted from a field to a property;
    /// and it works if the value is copied into a local or another field, because we are changing
    /// what the source *holds*, not what one instruction *loads*. What it needs in exchange is the
    /// name of a method to wrap - which the patcher's symbol dump is there to find.
    ///
    /// Two honest limitations, both worth knowing before trusting it:
    ///   * It is not thread-safe. Anything reading the flag on another thread during the window sees
    ///     false. Valheim's game logic is single-threaded, so this is theoretical here.
    ///   * It only covers reads that happen inside the wrapped call. A value cached at startup and
    ///     read later escapes it - wrap the method that does the caching instead, or fall back to
    ///     the patcher's IL rewrite.
    /// </summary>
    internal static class ScopedFlagSwap
    {
        private static readonly Harmony Harmony = new Harmony(AchievementEnabler.PluginGUID + ".scoped");

        internal static int Apply(string[] entries)
        {
            if (entries.Length == 0) return 0;

            if (!ModdedFlag.Available)
            {
                Logger.LogWarning("ScopedFalseMethods is configured but the modded flag did not resolve; nothing to swap.");
                return 0;
            }

            var prefix = new HarmonyMethod(AccessTools.Method(typeof(ScopedFlagSwap), nameof(Prefix)));
            var finalizer = new HarmonyMethod(AccessTools.Method(typeof(ScopedFlagSwap), nameof(Finalizer)));

            int patched = 0;
            foreach (string entry in entries)
            {
                MethodBase target = MemberRef.ResolveMethod(entry);
                if (target == null) continue;

                try
                {
                    Harmony.Patch(target, prefix: prefix, finalizer: finalizer);
                    Logger.LogInfo($"Scoped swap on {entry} - it will see the modded flag as false.");
                    patched++;
                }
                catch (System.Exception ex)
                {
                    Logger.LogWarning($"Could not patch '{entry}': {ex.Message}");
                }
            }
            return patched;
        }

        private static void Prefix(out bool __state)
        {
            __state = ModdedFlag.Value;
            ModdedFlag.Value = false;
        }

        private static void Finalizer(bool __state)
        {
            ModdedFlag.Value = __state;
        }
    }
}
