using System;
using System.Reflection;
using HarmonyLib;

namespace AchievementEnabler
{
    /// <summary>
    /// Read/write access to the game's modded flag, resolved by name at runtime.
    ///
    /// Today that flag is <c>public static bool Game.isModded</c>. This deliberately does not bind
    /// to it at compile time. If Iron Gate promotes it to a property when they start actually gating
    /// on it - a natural thing to do, and the single most likely way this mod breaks - a
    /// compile-time binding would throw <see cref="MissingFieldException"/> on first touch and take
    /// the plugin down. Resolved by name, the same refactor costs us nothing: the property is found
    /// instead, and everything downstream keeps working.
    ///
    /// If it resolves to neither, <see cref="Available"/> is false, every operation is a no-op, and
    /// the plugin says so once in the log rather than throwing on every access.
    /// </summary>
    internal static class ModdedFlag
    {
        private static FieldInfo _field;
        private static PropertyInfo _property;
        private static string _description = "unresolved";

        internal static bool Available => _field != null || _property != null;
        internal static string Description => _description;

        /// <param name="member">Where the flag lives, as <c>Type::member</c> - e.g. Game::isModded.</param>
        internal static void Resolve(string member)
        {
            _field = null;
            _property = null;
            _description = "unresolved";

            if (!MemberRef.TryParse(member, out string typeName, out string memberName))
            {
                Logger.LogWarning($"FlagMember '{member}' is not in Type::member form; the modded flag will not be touched.");
                return;
            }

            Type type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                Logger.LogWarning($"Type '{typeName}' not found; the modded flag will not be touched.");
                return;
            }

            _field = AccessTools.Field(type, memberName);
            if (_field != null && _field.FieldType == typeof(bool))
            {
                _description = $"field {type.FullName}::{memberName}";
                return;
            }
            _field = null;

            _property = AccessTools.Property(type, memberName);
            if (_property != null && _property.PropertyType == typeof(bool) && _property.CanRead && _property.CanWrite)
            {
                _description = $"property {type.FullName}::{memberName}";
                return;
            }
            _property = null;

            Logger.LogWarning(
                $"'{member}' resolved to no writable bool field or property. If the game renamed it, set " +
                $"General.FlagMember in the config; DumpSymbols in the patcher config will tell you the new name.");
        }

        internal static bool Value
        {
            get
            {
                try
                {
                    if (_field != null) return (bool)_field.GetValue(null);
                    if (_property != null) return (bool)_property.GetValue(null, null);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("Could not read the modded flag: " + ex.Message);
                }
                return false;
            }
            set
            {
                try
                {
                    if (_field != null) _field.SetValue(null, value);
                    else if (_property != null) _property.SetValue(null, value, null);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning("Could not write the modded flag: " + ex.Message);
                }
            }
        }
    }

    /// <summary>
    /// Parses the <c>Type::member</c> strings every config list in this mod uses.
    ///
    /// Nested types are accepted with either separator: Cecil prints them as <c>Outer/Inner</c>
    /// (which is what the patcher's symbol dump will show you), reflection wants <c>Outer+Inner</c>.
    /// Pasting either straight out of a log should work.
    /// </summary>
    internal static class MemberRef
    {
        internal static bool TryParse(string entry, out string typeName, out string memberName)
        {
            typeName = null;
            memberName = null;
            if (string.IsNullOrEmpty(entry)) return false;

            int sep = entry.IndexOf("::", StringComparison.Ordinal);
            if (sep <= 0 || sep + 2 >= entry.Length) return false;

            typeName = entry.Substring(0, sep).Trim().Replace('/', '+');
            memberName = entry.Substring(sep + 2).Trim();
            return typeName.Length > 0 && memberName.Length > 0;
        }

        /// <summary>
        /// Resolves a <c>Type::Method</c> entry to something Harmony can patch, or null with a
        /// logged reason. Never throws: a stale config entry after a game update must degrade to a
        /// warning, not a failed startup.
        /// </summary>
        internal static MethodBase ResolveMethod(string entry)
        {
            if (!TryParse(entry, out string typeName, out string methodName))
            {
                Logger.LogWarning($"'{entry}' is not in Type::Method form; skipped.");
                return null;
            }

            Type type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                Logger.LogWarning($"'{entry}': type '{typeName}' not found; skipped.");
                return null;
            }

            // Overloads are the one ambiguity this shorthand cannot express. Say so out loud rather
            // than silently picking one and leaving someone to wonder why the gate still fires.
            int overloads = 0;
            foreach (MethodInfo candidate in AccessTools.GetDeclaredMethods(type))
            {
                if (candidate.Name == methodName) overloads++;
            }
            if (overloads > 1)
            {
                Logger.LogWarning(
                    $"'{entry}' has {overloads} overloads; patching the first one AccessTools returns. " +
                    $"If that is the wrong one, this shorthand cannot say which - open an issue.");
            }

            MethodBase method = AccessTools.Method(type, methodName);
            if (method == null)
            {
                Logger.LogWarning($"'{entry}': method '{methodName}' not found on {type.FullName}; skipped.");
                return null;
            }

            if (method.IsAbstract || method.ContainsGenericParameters || method.GetMethodBody() == null)
            {
                Logger.LogWarning($"'{entry}' has no patchable body (abstract, generic or extern); skipped.");
                return null;
            }

            return method;
        }
    }
}
