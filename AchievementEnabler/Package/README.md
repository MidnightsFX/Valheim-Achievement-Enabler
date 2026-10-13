# AchievementEnabler

Lets Modded Valheim players earn achievements.

## What it does

**Cheating no longer affects achievements.** Valheim 1.0.12 added a way to opt-into achievements after using commands.
This mod extends that to no longer be necessary and to also ignore the game's own "cheated" flag on items and creatures. 
You can now use commands, spawn items, and build with cheated materials without losing achievements.

**Progress you already made counts.** Valheim records every stat twice: once in a raw table that always
counts, and once in the table achievements are actually read from, which it skips whenever it sees cheats or
mods. Everything you did before installing this mod is therefore in the raw table only, and the game never
goes back for it — which is why a character with hundreds of trees felled can still show no progress towards
Lumberjack. Your raw totals are now copied across when the character loads, taking whichever number is
higher so nothing is counted twice. Turn it off with `BackfillAchievementStats` if you would rather start
from where the game left you.

**Your best-ever stats stop going down.** A few achievements read a record rather than a total: the most
days survived in a row, your highest comfort, your highest build, and how much of each kind of piece you have
built in one place. The game checks each new value against your record at the world's current combat
difficulty only, then writes it over your overall record. So after a world's difficulty changes, or on your
first visit to a world at another difficulty, the first few days write a small number over your real best,
and an achievement like surviving 30 days in a row relocks. Records now only ever go up, your best run of
days keeps up with the run you are on, and records the game already lowered are put back when the character
loads, from the per-difficulty records it kept. Turn it off with `KeepBestStats`.

**Old cheat marks are cleared.** Characters, worlds and items that were already marked as cheated stop
showing as cheated. A character's mark is cleared each time it loads, a world's keys are left alone, and an
item's flag is cleared as the item loads — from your inventory, chests, tombstones or the ground. Each can be
turned off in `MidnightsFX.AchievementEnabler.cfg`.

Clearing the item flag matters beyond the tooltip: since Valheim 1.0.14 a flagged item will not stack with a
clean one, so one flagged stack kept splitting every stack of that item you picked up. The flag is written
out as clear the next time the item is saved, so this one is not reversible by turning the setting back off.

**Cheated items can be cleared by hand too.** `clearcheateditems` is a cheat command: turn on `devcommands`,
then run it in the console to clear the flag from everything your character is carrying right now. It covers
the one case loading cannot: an item handed to you by a player who does not have this mod. Like the game's
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

**Optionally, see what your achievements ask for.** Turn on `ShowAllRequirements` and every achievement you
already know about opens, including the ones the game keeps shut, like The Homesteader, and its details name
every requirement with your progress towards it, not just the ones you have already met. Child of Odin and
Kings & Queens list the achievements they need. Secret achievements you have not earned stay concealed, and
so does anything that would give one away: The Viking still shows "???" for the bosses you have not killed.

**Optionally, see every achievement.** Turn on `ShowSecretAchievements` and secret achievements show their
real name and description instead of "Concealed" and "Only Odin knows what is hidden here." That spoils
them, so it is worth leaving off until you want to know. With both on, you see every achievement and
everything it asks for.

Both are client side and off by default. They only change what your own panel shows, never what counts.

## What this can't fix

Some achievements have conditions in the game itself that have nothing to do with cheating, and this mod
does not change them. If one looks stuck, it is usually one of these:

- A tree only counts if you hit it yourself and your game owns it. Trees knocked over by a falling log
  count for nobody, and in multiplayer a tree loaded by another player counts for them.
- A kill only counts if you damaged the creature, and it is reported by whichever game owns it.
- Food taken off a cooking station is credited to whoever's game owns the station, not whoever takes it.
- The two hard-difficulty achievements and the normal-difficulty one read their own tables, which only
  receive kills made at that difficulty or above.
- Build achievements recheck your surroundings once every twenty pieces placed, and only count pieces you
  built that are currently loaded.

## Installation

Install with a mod manager, or unzip into your Valheim folder so that you have
`BepInEx/plugins/AchievementEnabler.dll`.

Upgrading from 0.2.0 by hand? Delete `BepInEx/patchers/AchievementEnabler/`. The patcher is no longer needed.
