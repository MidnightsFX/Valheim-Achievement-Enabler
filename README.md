# AchievementEnabler — development

A Valheim BepInEx mod that lets a modded game earn achievements while the modded flag itself stays
`true`. The user-facing README is [AchievementEnabler/Package/README.md](AchievementEnabler/Package/README.md);
this file is about building it.

No Jotunn dependency. No publicizer. Two assemblies, one Thunderstore package.

## The gate

Valheim **1.0** added achievements. The modded flag is still `public static bool Game.isModded`, and
across the whole game assembly there are exactly four IL references to it:

```
ldsfld    | Achievements::IsCheatedAtAll   the achievement gate            <- the one that matters
ldsfld    | FejdStartup::SetupGui          the main-menu "modded" label
ldsflda   | Game::Awake                    ZLog.Log("isModded: " + ...ToString())
stsfld    | Game::.cctor                   the `= false` initializer
```

`Achievements.IsCheatedAtAll()` ends with

```csharp
m_cheatCheckCache = (usedCheats | cheatedItem | cheatedWorld) || Game.isModded;
```

and `Achievements.CanGetAchievements()` returns false whenever that is true. Every achievement stat
write on `PlayerProfile` — `IncrementStat`, `IncrementStatEnemy`, `IncrementStatItemCraft`, and the
rest — is behind that check, and unlocks are driven off those stats, so neutralising this one term
covers the whole system while leaving the three genuine cheat checks beside it working.

Note the per-frame cache in front of it. Both mechanisms are still correct with it in play: every
computation of `m_cheatCheckCache` happens inside `IsCheatedAtAll`, so there is no path that fills
the cache while the flag still reads true.

## Why it is shaped this way

Nothing is bound at compile time even now. Every target is named in config and resolved by name at
runtime, so an update that renames or moves the gate costs a line in a `.cfg` rather than a new
build, and the patcher ships the symbol dump that finds the new name.

That also explains the mechanism split. A *field read* cannot be Harmony-patched, which is why the
preloader patcher exists. But a field-read rewrite is blind to a gate that reads via `ldsflda`, or
one where the flag has been promoted to a property — so the plugin's scoped swap, which changes what
the flag *holds* for the duration of a named call, is the primary mechanism and the patcher is the
fallback. Both are enabled by default and both target the same gate; either alone is sufficient.

| | Mechanism | Where |
|---|---|---|
| 1 | Scoped swap — flag is false for the duration of a named method | plugin, `ScopedFalseMethods` |
| 2 | Forced result — a named bool method returns false | plugin, `ForceFalseMethods` |
| 3 | IL rewrite — a named field's reads fold to a constant | patcher, `Fields` |

## Layout

```
AchievementEnabler/            the plugin       -> BepInEx/plugins/
  Package/                     Thunderstore zip root
AchievementEnabler.Preloader/  the patcher      -> BepInEx/patchers/AchievementEnabler/
Directory.Build.props          game + BepInEx path probing
Environment.props.example      copy to Environment.props to override those paths
```

`Directory.Build.props` finds Valheim through the Steam registry keys and BepInEx through the game
folder or, failing that, the first Gale / r2modman / Thunderstore profile that has one. Nothing is
hardcoded; copy `Environment.props.example` to `Environment.props` (gitignored) to pin either.

## Build

```bash
dotnet build -c Debug
```

Both projects deploy themselves on every build — into the `Modtest` profile of whichever mod
managers are installed, and into `AchievementEnabler/Package/{plugins,patchers}/` so the Thunderstore
payload stays current. Release packaging is [mod-tools](https://github.com/MidnightsFX):

```bash
mod-tools print-config   # check the resolved layout
mod-tools check          # release checks, tag, and build the zip
```

`PluginVersion` in `AchievementEnabler/AchievementEnabler.cs` is the single source of truth for the
version; `Properties/AssemblyInfo.cs` derives the assembly version from it and `mod-tools check`
compares both against `Package/manifest.json`.

## Testing the IL rewrite without launching the game

`FieldReadRewriter` can be driven directly against the shipped `assembly_valheim.dll`: read it with
BepInEx's own Cecil, call `Rewrite`, and write the module out. Cecil refusing to write is the signal
that the IL is malformed. Pointing `Fields` at both `Game::isModded` (static, `ldsfld`) and
`PlayerProfile::m_usedCheats` (instance, `ldfld`) exercises both rewrite paths and confirms
compiler-generated closure types are covered — `Terminal/<>c::<InitTerminal>b__7_44` reads the latter.

On 1.0.7 the check that matters is that `Achievements::IsCheatedAtAll` comes out as

```csharp
m_cheatCheckCache = ((num | flag2 | flag) ? true : false);   // Game.isModded term gone
```

with `FejdStartup::SetupGui` folded the other way, to `m_moddedText.SetActive(value: true)`.

The plugin's half is reflection rather than IL, so it needs a different check: load
`assembly_valheim.dll` into a plain net48 host and call `ModdedFlag.Resolve` and
`MemberRef.ResolveMethod` against it. That confirms the config strings still resolve and that
`Achievements` is unambiguous — no other assembly the game loads defines that type name. It stops
short of the Harmony patch: MonoMod cannot prepare a Unity-derived method outside Unity's own Mono,
so that step only ever runs for real in the game.

To see the rewrite in the real game instead, set `DumpAssemblies = true` under `[Preloader]` in
`BepInEx.cfg` and decompile `BepInEx/DumpedAssemblies/assembly_valheim.dll`.

## When an update moves the gate

1. Set `DumpSymbols = true` in `BepInEx/config/AchievementEnabler.Preloader.cfg` and start the game.
2. Read the `=== symbol dump ===` block in `BepInEx/LogOutput.log` — `methods reading a targeted
   field` is the section that matters.
3. Put the gate into `ScopedFalseMethods` (or `ForceFalseMethods`) — no rebuild needed.
4. Re-decompile `assembly_valheim` into `DecompiledProject`, fold the finding into
   `ValConfig.DefaultScopedFalseMethods`, and cut a release.

Step 3 is the point of the design: a rename is a config edit for every existing install, and the
rebuild in step 4 is only so new installs get it by default.
