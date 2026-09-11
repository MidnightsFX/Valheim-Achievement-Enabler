# AchievementEnabler

Lets Modded Valheim players earn achievements.

## What it does

**Cheating no longer affects achievements.** Valheim 1.0.12 added a way to opt-into achievements after using commands.
This mod extends that to no longer be necessary and to also ignore the game's own "cheated" flag on items and creatures. 
You can now use commands, spawn items, and build with cheated materials without losing achievements.

**Old cheat marks are cleared.** Characters and worlds that were already marked as cheated stop showing as
cheated in the achievements panel. A character's mark is cleared each time it loads; a world's keys are
left alone. Either can be turned off in `MidnightsFX.AchievementEnabler.cfg`.

**Admins can clear cheated items.** Run `clearcheateditems` in the console to clear the cheated flag from
everything your character is carrying. It needs admin rights: in singleplayer, or when hosting, that is
you; on a dedicated server you need to be in its `adminlist.txt`.

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
