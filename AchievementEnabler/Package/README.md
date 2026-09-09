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

## Known issues

None yet.
