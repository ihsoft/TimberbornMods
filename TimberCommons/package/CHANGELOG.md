# v1.5 (started on 1/28/2024):
* Relase irrigation towers code.
* Add contamination blocker effect for irrigation towers.
* Add growth rate modifier effect for irrigation towers.
* Add a feature control file (TimberDev_Features.txt).
* Add a feature to show duration as "Xd Yh" (instade of simple "XX hours") in the good consuming buidlings.
* Add a feature to show duration as "Xd Yh" (instead of rounding to teh closes whoel day) in the growable dialogs.

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
