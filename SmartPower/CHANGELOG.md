# v1.13.3 (10 Apr 2025)
* Update to support game version `0.7.3.1`.

# v1.13.2 (4 Apr 2025)
* Update to support game version `0.7.2.0`.
* [Fix] The suspended generator doesn't resume if paused or generator automation disabled.
* [Fix] All generators get automation enabled once the mod is added to an existing game. The existing generators should stay "off" be default.

# v1.13.1 (26 Feb 2025)
* [Fix] The Engines don't start on the game load if the automation was enabled.
* [Fix] The Engines suspended state doesn't restore on the game load.
* [Fix] The consumers suspended state doesn't restore on the game load.

# v1.13.0 (2 Feb 2025)
* Support Update 7.
* Incompatible with Update 6.
* Doesn't require TimberAPI.

# v1.12.6 (19 Dec 2024)
* [Fix] Enable power regulation for non-workshop buildings.

# v1.12.5 (15 Dec 2024)
* [Enhancement] Allow changing settings in the game.

# v1.12.4 (3 Dec 2024)
* Full refactoring of the power balancing logic.
* [Enhancement] Consumers can be configured to stop/start if there is not enough power in the network.
* [Enhancement] Display status for when the batteries aren't used (a positive balance) or are depleted (a negative balance and no charge). 
* [Enhancement] Show a floating icon over the suspended buildings. Configurable via settings.

# v1.11.2 (25 Sep 2024)
* [Fix] "Apply to all generators" button is not resized.

# v1.11.1 (21 Sep 2024)
* [Update] Some internal changes in preparation of upcoming TAPI update.

# v1.11.0 (15 Sep 2024)
* [Enhancement] Add `frFR` locale file. Thanks to Erazil@Discord.
* [Fix] Smart logic doesn't work on powered attractions and manufactories.

# v1.10.2 (5 Sep 2024)
* [Enhancement] Add smart power control for power wheels.
* [Update] Change `deDE` strings.
* [Fix] Game crashes when trying to apply settings on incomplete engine. 
* [Fix] Properly handle 0% and 100% setting in Engine settings.

# v1.9.3 (19 Aug 2024)
* [Fix] Game crashes on powered buildings with no recipe selected.

# v1.9.2 (18 Aug 2024)
* [Update] Allow applying generator settings to all generators in the network.

# v1.9.1 (preview)
* [Change] Idle attractions now consume 1hp. It solves the edge case with the unpowered networks.
* [Update] Set 5% steps for the charge battery slider.
* [Update] Add "less charge" / "more charge" buttons to quickly set slider presets.

# v1.9.0 (preview)
* [Update] Change `deDE` strings.
* [Enhancement] Move power generator UI fragment to the middle of the panel.
* [Enhancement] Give a MinMax slider to adjust the battery charging range.

# v1.8.0 (8 Aug 2024)
* Migrate to Update 6 mods model. Incompatible with the previous versions of Timberborn!

# v1.7 (1 Feb 2024)
* [Fix #27] Smart Power does not work for buildings with supply.
* [FIx #30] Manufactories without working places are handled incorrectly.

# v1.6 (8 Dec 2023)
* [Fix #20] Fix crashes on game load in Timberborn 0.5.4.2.

# v1.5 (5 Dec 2023)
* Experimental version for Update 5.
* [Fix #18] Fix crashes on Timberborn 0.5.4.0.

# v1.4 (27 Aug 2023)
* [Fix #8] Optimize UI fragments handling.

# v1.3 (28 Jul 2023)
* Switch to Timber API 0.6.0 and remove `CustomResources` dependency.
* Update `deDE` localization.

# v1.2 (19 Jul 2023)
* [Update] Change `deDE` strings.
* [Enhancement #1] Add a setting to control batteries charging at per engine level.
* [Enhancement #2] Allow specifying if the engine must be generating power regardless to the smart logic.

# v1.1 (14 Jul 2023)
* [Enhancement] Add `deDe` locale file. Thanks to juf0816@Discord.
* [Enhancement] Adjust power consumption in maufactures when they don't produce product. In the
  power save mode the consumption is 10% of the nominal building power.
* [Fix] Properly update attraction state on game load.

# v1.0 (12 Jul 2023)
* Initial version of the mod.
* [Enhancement] Smart logic on Ironteeth engines start/stop. All engines that don't need to cover
  the demand get stopped. They automatically start when the demand raises, but there can be a short
  moment of lacking energy (for a tick or two).
* [Enhancement] When charging batteries, the engines work at the full power only until the last
  battery has charged to 90% or above. After that, the batteries are not counted as a demand
  anymore. When the charge on any battery falls below 90%, all the engines will start to recharge
  it as quickly as possible.
* [Enhancement] All powered attractions (carousel, mud bath) stop consuming energy if there are not
  attendees.
* [Enhancement] Ironteeth charger station only consumes energy when there is a bot charging. In the
  idle state it doesn't consume power.
* [Enhancement] The mechanical nodes (generators and shafts) show extra info about the batteries
  status in the network (if any).
