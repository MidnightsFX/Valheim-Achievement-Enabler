# AchievementEnabler

Lets a modded Valheim earn achievements — without lying about being modded.

The game's modded flag stays **true**: the "modded" label still shows on the main menu, the startup
log still says so, and any other mod that reads the flag gets the truth. What changes is what the
*achievement gate* sees, and only while it is actually running.

## Status

Working on **Valheim 1.0.7**, the update that added achievements. It is configured for that gate out
of the box — install it and it works, nothing to point at anything.

**Cheating still costs you achievements.** The gate the game uses,
`Achievements.IsCheatedAtAll()`, asks four separate questions: have you used console cheats, are you
carrying a spawned item, is the world running cheated modifiers — and, until now, is the game
modded. Only the last of those is answered differently. The other three are untouched, so they still
disable achievements exactly as Iron Gate shipped them.

## Installation

Install with a mod manager, or unzip into your Valheim folder so that:

- `BepInEx/plugins/AchievementEnabler.dll`
- `BepInEx/patchers/AchievementEnabler/AchievementEnabler.Preloader.dll`

The patcher **must** be in `patchers/`, not `plugins/`. That is what lets it rewrite the game's IL
while BepInEx is loading it.

## Checking it worked

`BepInEx/LogOutput.log` should contain, under `AchievementEnabler`:

```
Modded flag: field Game::isModded = True
Preloader patcher:
  4 read(s) of [Game::isModded]: 2 folded, 1 skipped (address-of)
  Achievements::IsCheatedAtAll -> false
  FejdStartup::SetupGui -> true
Scoped swap on Achievements::IsCheatedAtAll - it will see the modded flag as false.
Ready: 1 scoped swap(s), 0 forced result(s).
```

Both mechanisms cover the same gate on purpose — either one alone is enough, and the patcher is the
one that keeps working if a future update inlines the read.

## Re-pointing it after a game update

If an update moves or renames the gate, the plugin will say so in the log rather than silently doing
nothing. To find the new one:

1. Open `BepInEx/config/AchievementEnabler.Preloader.cfg`, set `DumpSymbols = true`, and start the
   game.
2. In `BepInEx/LogOutput.log`, look for the `=== symbol dump ===` section. The
   `methods reading a targeted field` list is the one you want — on 1.0.7 it is four lines, and
   `Achievements::IsCheatedAtAll` is the gate among them.
3. Put the new name into `ScopedFalseMethods` in
   `BepInEx/config/MidnightsFX.AchievementEnabler.cfg`:

   ```
   ScopedFalseMethods = Achievements::IsCheatedAtAll
   ```

4. Restart. The log will confirm each one.

If the gate becomes a bool-returning method rather than a flag read, use `ForceFalseMethods`
instead. If the read happens somewhere no method usefully wraps, add the field to `Fields` in the
patcher config and it will be folded to a constant in the IL.

## How it works

Three mechanisms, in the order you should reach for them.

**Scoped swap** (`ScopedFalseMethods`) — the flag is set false for the duration of a named method and
restored right after, even if the method throws. This is the one to use: it works whether the gate
reads a field or a property, whether the read compiles to `ldsfld` or `ldsflda`, and whether the
value is used directly or copied somewhere first.

**Forced result** (`ForceFalseMethods`) — a named bool method always returns false.

**IL rewrite** (patcher, `Fields`) — reads of a named bool field are folded to a constant, except in
the methods on `Allowlist`, which see `true` instead. This is the fallback for a read that no
nameable method wraps.

## Limitations

- The scoped swap is not thread-safe. Anything reading the flag on another thread during the window
  sees false. Valheim's game logic is single-threaded, so this is theoretical.
- A value cached at startup and read later escapes the scoped swap — wrap the method that does the
  caching, or use the patcher's IL rewrite.
- An address-of read (`ldsflda`) cannot be folded to a constant, so the patcher skips and logs it.
  `Game.isModded.ToString()` in `Game.Awake` is one, which is why that method is allowlisted anyway.
- Progress is not retroactive. Valheim keeps achievement stats in a separate set of counters that it
  simply does not write to while the gate is closed, so anything you did in a modded game *before*
  installing this is not backfilled — it was never recorded. Progress from here on counts normally.

## Known issues

None yet.
