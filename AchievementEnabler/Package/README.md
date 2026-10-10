# AchievementEnabler

Lets Modded Valheim players earn achievements.

## What it does

**Cheating no longer affects achievements.** Valheim 1.0.12 added a way to opt-into achievements after using commands.
This mod extends that to no longer be necessary and to also ignore the game's own "cheated" flag on items and creatures. 
You can now use commands, spawn items, and build with cheated materials without losing achievements.

**Old cheat marks are cleared.** Characters and worlds that were already marked as cheated stop showing as
cheated in the achievements panel. A character's mark is cleared each time it loads; a world's keys are
left alone. Either can be turned off in `MidnightsFX.AchievementEnabler.cfg`.

**Cheated items can be cleared.** `clearcheateditems` is a cheat command: turn on `devcommands`, then run
it in the console to clear the cheated flag from everything your character is carrying. Like the game's
other cheat commands, it only works in singleplayer or for the host.

## Vanilla-only achievement lists (optional)

Some achievements ask for everything of a kind: craft every weapon, item or food, eat every food, place every
build piece, find every trophy, kill every creature. The game builds those lists from whatever is loaded, so
content mods add their own items, pieces and creatures to them, some of which you may never be able to get.

Set `VanillaOnlyRequirements = true` in the `[Achievements]` section of `MidnightsFX.AchievementEnabler.cfg` to
limit those lists to vanilla content. It is off by default, only affects your own game (nothing to set on the
server), and can be changed while the game is running.

If the shorter list means you have already done everything an achievement asks for, it unlocks straight away.
Turning the option back off puts the modded entries back in the lists, but doesn't take away achievements
already unlocked on Steam.

## What still shows as cheated

Items flagged before you installed this mod keep the flag, and so do items flagged in the game of a player
who doesn't have it. Creatures flagged before still drop flagged items. None of that affects achievements,
but:

- Those items' tooltips say they are cheated.
- While you carry one, the achievements panel shows a cheated notice, and picking one up shows the game's
  cheated-item message.
- The flag is saved with the item, so it matters again if you remove this mod. `clearcheateditems` clears it.

Items with over 10,000 total damage are still flagged every time your character loads; the game's switch
doesn't cover that check.

## Installation

Install with a mod manager, or unzip into your Valheim folder so that you have
`BepInEx/plugins/AchievementEnabler.dll`.

Upgrading from 0.2.0 by hand? Delete `BepInEx/patchers/AchievementEnabler/`. The patcher is no longer needed.
