# v1.1 (pre-release)
* [Enhancement] Add `deDe` locale file. Thanks to juf0816@Discord.
* [Enhancement] Adjust power consumption in mauufactures when they don't produce product. In the
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