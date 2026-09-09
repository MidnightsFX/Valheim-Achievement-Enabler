using BepInEx.Logging;
using System;

#pragma warning disable IDE0130
namespace AchievementEnabler {
#pragma warning restore IDE0130
    internal class Logger {
        public static LogLevel Level = LogLevel.Info;

        public static void EnableDebugLogging(object sender, EventArgs e) {
            CheckEnableDebugLogging();
        }

        public static void CheckEnableDebugLogging() {
            if (ValConfig.EnableDebugMode.Value) {
                Level = LogLevel.Debug;
            } else {
                Level = LogLevel.Info;
            }
        }

        public static void SetDebugLogging(bool state) {
            if (state) {
                Level = LogLevel.Debug;
            } else {
                Level = LogLevel.Info;
            }
        }

        public static void LogDebug(string message) {
            if (Level >= LogLevel.Debug) {
                AchievementEnabler.Log.LogInfo("[DEBUG]" + message);
            }
        }
        public static void LogInfo(string message) {
            if (Level >= LogLevel.Info) {
                AchievementEnabler.Log.LogInfo(message);
            }
        }

        public static void LogWarning(string message) {
            if (Level >= LogLevel.Warning) {
                AchievementEnabler.Log.LogWarning(message);
            }
        }

        public static void LogError(string message) {
            if (Level >= LogLevel.Error) {
                AchievementEnabler.Log.LogError(message);
            }
        }
    }
}
