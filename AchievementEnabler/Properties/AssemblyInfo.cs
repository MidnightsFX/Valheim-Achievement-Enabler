using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("AchievementEnabler")]
[assembly: AssemblyDescription("Lets a modded Valheim earn achievements. Mods no longer count as cheating, and old cheat marks can be cleared.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("MidnightMods")]
[assembly: AssemblyProduct("AchievementEnabler")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]
[assembly: Guid("6f2a1c74-58d0-4a3e-9b21-7c4e0d8f5a13")]

// Derived from the one const in AchievementEnabler.cs, so the assembly version cannot drift from
// the BepInPlugin version. Only works because GenerateAssemblyInfo is false in the .csproj.
// The Thunderstore manifest is the third copy and is hand-matched; `mod-tools check` compares them.
[assembly: AssemblyVersion(AchievementEnabler.AchievementEnabler.PluginVersion)]
[assembly: AssemblyFileVersion(AchievementEnabler.AchievementEnabler.PluginVersion)]
