# Changelog

## 0.2.0

Valheim 1.0 added achievements, and with them the gate this mod was built to answer. It now works
out of the box instead of shipping inert machinery.

- Target the gate by default: `ScopedFalseMethods` now defaults to `Achievements::IsCheatedAtAll`,
  the only place in Valheim 1.0.7 where the modded flag decides anything.
- The preloader's IL rewrite already covered it with the shipped `Fields` / `Allowlist` defaults;
  verified against 1.0.7, where it folds the read in `Achievements::IsCheatedAtAll` to `false` and
  the one in `FejdStartup::SetupGui` to `true`.
- Cheating still disables achievements. `IsCheatedAtAll` also checks console cheats, spawned items
  and cheated world modifiers; none of those are touched.
- The log now warns, rather than reassures, when no gate method is in play — on 1.0 that means
  achievements are still blocked, not that there is nothing to do.

If you ran a pre-release build, your `MidnightsFX.AchievementEnabler.cfg` still has an empty
`ScopedFalseMethods` and BepInEx will keep it that way. Set it to `Achievements::IsCheatedAtAll`, or
delete the file and let it regenerate.

## 0.1.0

- Initial release
