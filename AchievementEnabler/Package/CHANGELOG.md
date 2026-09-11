# Changelog

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
