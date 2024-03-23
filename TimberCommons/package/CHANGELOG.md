# v1.7.3 (22 Mar 2024):
* [Fix #37] Water tower with zero coverage consumes water.
* [Fix #43] Water towers override the highlighted range of other buildings.
* [Fix #45] Mechanical towers don't properly update the effective range.
* [Fix #48] Irrigation range highlighting from the previous saved game says after loading.
* [Fix #49] Manufacture water tower misses localization string.
* [Enhancement] Improved powered irrigation towers performance when the Power Grid gets fluctuations.
* [Enhancement] Update `deDE` localization (by @juf0816).
* [Enhancement] Prevent beavers contamination when moving in tunnels under bad water.

# v1.6 (21 Feb 2024):
* [Fix #33] Water valve doesn't handle contamination correctly.
* [Fix #38] "Supply will last for" text disappeared in UI.
* [Feature] Make WaterValve pausable.
* [Feature] Show the tiles, irrigated by the `WaterTower`, as *green* on the map instead of
  "barely green, maybe dying".
* [Enhancement] Show duration values below `0.01` as zero.
* [Enhancement] In the IrrigationTower, consider all foundation tiles to find the irrigated area.
* [Enhancement] Improve irrigated area calculation and update performance.
* [Enhancement] Add feature `CommonUI.DisableAllPatches` to disable all UI related Harmony patches
  from the mod. It's disabled by default.

# v1.5 (3 Feb 2024):
* Relase irrigation towers code.
* Introduce the irrigation tower components: `GoodConsumingIrrigationTower` and `ManufactoryIrrigationTower`.
* Add contamination blocker effect for irrigation towers.
* Add growth rate modifier effect for irrigation towers.
* Add a feature control file (`TimberDev_Features.txt`).
* Add a feature to show duration as "Xd Yh" (instead of simple "XX hours") in the good consuming buidlings.
* Add a feature to show duration as "Xd Yh" (instead of rounding to the closes whole day) in the growable dialogs.
* Add `PrefabOptimizer.MaxExpectedRegistrySize` feature to override the value and stop logs spam. Disabled by default.

# v1.4 (27 Jan 2024)
* [Fix #28] "Water bump" at the water intake.

# v1.3 (10 Nov 2023)
* [Fix #17] Support contaminated water (Update 5).
* This version is not compatible with Update 4.

# v1.2 (30 Sep 2023)
* [Fix #15] Support new water simulation logic in Update 5.

# v1.1 (27 Jul 2023)
* [Fix #4] In rare cases the game crashes when valve gets built.
* Add `deDE` localization from @juf0816.
* Update `ruRU` localization.
* Add `Particle System` property to the `WaterValve` component.

# v1.0 (25 Jul 2023)
* Initial version of the mod.
* `DirectWaterServiceAccessor` class to handle custom water movers.
* `WaterValve` component for one-way water valve that mover water based on the levels at the input and
  output.
