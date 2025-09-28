# v1.13.1 (September 28th, 2025)
* [Fix] In rare cases the game load may crash when irrigation towers are present.

# v1.13.0 (September 9th, 2025)
* [Change] Update to support game version `v0.7.10.9`. It is incompatible with the previous game versions.

# v1.12.2 (July 16th, 2025)
* [Fix #117] Crash when irrigation tower is flooded.

# v1.12.1 (June 17th, 2025)
* [Fix] Fix a crash during a game load under some circumstances.

# v1.11.6 (28 May 2025) - Patch for `0.7.6.1`
* Fix IrrigationTower component for Update 7.

# v1.12.0 (27 May 2025)
* [Change] Update to support game version `v0.7.9.0`. It is incompatible with the previous game versions.
* [Change] Fix IrrigationTower component for Update 7.

# v1.11.5 (27 Apr 2025)
* Update to support game version `0.7.6.1`.

# v1.11.4 (10 Apr 2025)
* Update to support game version `0.7.3.1`.

# v1.11.3 (4 Apr 2025)
* Update to support game version `0.7.2.0`.

# v1.11.2 (11 Mar 2025)
* [Fix] The mod crashes on game version `0.7.1.2`.

# v1.11.1 (26 Feb 2025)
* [Fix] The mod crashes on game version `0.7.1.0`.

# v1.11.0 (22 Feb 2025)
* [Enhancement] Add Update 7 support. Incompatible with the previous versions.
* [Change] No more dependency to TimberAPI.
* For Update 6, v1.10.3 is included in the package.

# v1.10.3 (15 Dec 2024)
* [Enhancement] Allow changing settings in the game.

# v1.10.2 (22 Oct 2024):
* [Fix] Game crashes on selecting a tower after the game was loaded with unfinished towers.

# v1.10.1 (8 Oct 2024):
* [Fix] `IrrigationTower` component crashes on game `0.6.8.3`.

# v1.10.0 (3 Oct 2024):
* [Enhancement] Add `frFR` localization (by @Erazil).
* [Fix] The current strength of the sluice is not shown.

# v1.9.1 (27 Sep 2024):
* [Feature] Show injury probability on the buildings that can cause injury.
* [Update] `deDE` localization from @juf0816.

# v1.8.3 (25 Sep 2024):
* Migrate to Update 6. Not compatible with the previous versions.
* [Feature] Add a replacement to the stock `GoodAmountTransformHeight` component that cannot work with manufactures.
* [Enhancement] Allow changing settings via Mods UI.
* [Enhancement] Show current strength on sluice.
* [Enhancement] Allow adjusting water level at spillway for mechanical pumps and fluid pumps.
* [Change] Water valve building is depercated.

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
