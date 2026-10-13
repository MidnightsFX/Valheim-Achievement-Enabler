# Changelog

## 0.5.1
- Fixes best-ever stats, such as the most days survived in a row, being lowered after a world's combat difficulty changes, which could relock achievements already earned.
- Puts back best-ever stats the game had already lowered when a character loads.

## 0.5.0
- New option `VanillaOnlyRequirements` (off by default, client-side): the craft-, eat-, build-, find- and
  kill-everything achievements only ask for vanilla content, leaving out items, pieces and creatures added
  by other mods. Can be changed while the game is running.

## 0.4.1
- New option `ShowAllRequirements`, off by default: every achievement you already know about opens in the
  achievements panel, including the ones the game keeps shut, and its details name every requirement with
  your progress instead of "???". Secret achievements you have not earned stay concealed, and so do
  requirements that would give them away.
- New option `ShowSecretAchievements`, off by default: the achievements panel shows secret achievements
  under their real name and description instead of "Concealed". With both on, you see every achievement and
  every requirement.

## 0.4.0
- Cheated items stack again. Valheim 1.0.14 stopped a flagged item stacking with a clean one, so a single
  flagged stack kept splitting every stack of that item. The flag is now cleared from every item as it
  loads, wherever it loads from.
- Counts progress made before the mod was installed.
- Special Achievements checked

## 0.3.3
- Prevents cheated coins from causing cheated items from haldor
- Prevents _existing_ cheated creatures from causing cheated drops

## 0.3.2
- Adds flexible support for older and newer builds setting BypassCheatChecks

## 0.3.1
- Reverts to gating behind devcommands for admin commands, supporting the 1.0.12 update better

## 0.3.0

- Removed the preloader patcher. If you installed by hand, delete `BepInEx/patchers/AchievementEnabler/`.
- Cheating no longer affects achievements. Being modded, having used cheat commands, a world with
  non-standard keys and carrying spawned items no longer turn them off, and kills of spawned or god-mode
  creatures, and picking up, eating or building with cheated items, now count.
- The game no longer flags new items, creatures or buildings as cheated. Anything flagged before keeps
  its flag.
- Cheat commands run without the confirmation prompt and no longer mark your character as cheated, and
  `setkey` accepts any key.
- The main menu still shows the "modded" label.
- Characters and worlds already marked as cheated no longer show as cheated in the achievements panel. Both
  can be turned off in the config.
- New cheat command `clearcheateditems` clears the cheated flag from the items you are carrying. Like other
  cheat commands it needs `devcommands`, and only works in singleplayer or for the host.

## 0.2.0

Valheim 1.0
- Running mods no longer disables achievements.
- Cheating still disables achievements. `IsCheatedAtAll` also checks console cheats, spawned items
  and cheated world modifiers; none of those are touched.
