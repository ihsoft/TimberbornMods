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
