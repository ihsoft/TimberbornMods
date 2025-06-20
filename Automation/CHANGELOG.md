# v2.5.9 (pre-release, June 18th, 2025):
* [Change] Make input field in script editor multi-line.
* [Change] Better handling the script errors in both UI and the execution phases.

# v2.5.8 (June 18th, 2025):
* [Fix] Properly convert the legacy seasons names into the season IDs.
* [Fix] Add a missing localization string for the settings dialog.

# v2.5.7 (June 17th, 2025):
* [Change] Don't crash if the modded seasons can't be loaded. Ignore the specs and use the standard seasons instead.
* [Fix] Fix a crash during a game load under some circumstances.

# v2.5.6 (June 16th, 2025):
* [Feature] Allow disabling arguments and values checking in the scripts via settings.
* [Feature] Add `Debug.Ticker` signal. You asked for it, you got it! Now you can use `Debug.Ticker` signal to get your rules executed every tick.
* [Feature] Load weather seasons from the game blueprints. This allows the modded seasons to work, given the mod that introduces them follows the specs naming convention.
* [Change] Improve circular execution detection.
* [Fix #103] Signals not restored on a game load.
* [Fix #104] Game crashes with ModdableWeather mod.
* [Fix #107] Signal values may not be truly global.
* [Fix #109] Add a debug option to re-evaluate scripts on a game load.

# v2.5.0 (June 5th, 2025):
* [Feature] Special short description for the rules that check signal value to itself: "signal change condition."
* [Feature #150] Add Modulo (`mod`) Operator.
* [Change] Rules that cannot execute on unfinished buildings are now grayed in the rule list.
* [Fix #101] Crash from circular execution of rule.
* [Fix #104] Game crashes with ModdableWeather mod.
* [Fix #102] NullReferenceException: Object reference not set to an instance of an object. 

# v2.4.4 (June 3rd, 2025):
* [Feature #86] Add multi-value support for the custom signals. Signal values can be aggregated via name suffix: `.Min`, `.Max`, `.Sum`, `.Avg`, and `.Count`. Not available in constructor.
* [Feature #88] Allow operator `add` accepting multiple arguments.
* [Feature #91] Allow extra comment styles in rule importing: prefixes `;` and `//`; and multi-line comments `/* ... */` and `#| ... |#`.

# v2.3.1 (26 May 2025):
* [Change] Support Timberborn `0.7.9.0`. Incompatible with the previous versions of Update 7.
* [Feature] Add import/export feature for the rules.
* [Fix #90] The placement tools prematurely place the building after an automation tool was used.

# v2.2.3 (27 May 2025), patch for `0.7.8.9`.
* [Fix #90] The placement tools prematurely place the building after an automation tool was used. 

# v2.2.2 (25 May 2025):
* [Fix #84] Output good signals are not shown on the gathering flags.

# v2.2.1 (22 May 2025):
* [Fix #76] Game crashes on recursive signals.

# v2.2.0 (20 May 2025):
* [Feature] Highlight the conditions that are being evaluated to "true" with green color.
* [Feature] New scriptable components `Workplace`. Now, the number of workers in the workshop can be changed from the script.
* [Change] Don't select the text when switching to the script input field.
* [Fix] Chained building tool doesn't show the blocked completion icon.

# v2.1.1 (16 May 2025):
* [Feature] Add settings to control how to show the rules on the building's panel.
* [Feature] Add new scripting component `FlowControl` to allow scripting the water flow on sluices and badwater domes.
* [Feature] The dynamite drilldown templates are now scripted. Such rules can be created and edited via the rules' editor.
* [Feature] New template tool to pause construction site when its progress reaches 100%.
* [Feature] The "Open water regulator" template can now be applied to the sluices as well.
* [Change] The rule editor now only allows inventory signals that are valid for the current building state: there must be a recipe or a storage good selected. 
* [Change] Various improvements to better check and show the script arguments values. 

# v2.1.0 (12 May 2025):
* [Feature] Buildings can now emit their state as global signals to be consumed by other buildings.
* [Feature] New scriptable components `Prioritizable` and `Constructable`.
* [Feature] New template tool that allows to prioritize by haulers a newly constructed building.
* [Feature] New template tool to sync floodgate heights.
* [Feature] New template tool to emit stream gauge state as signals.
* [Fix #71] Incorrect Good ID in inventory signals causes crash with scripting.

# v2.0.6 (10 Apr 2025):
* [Fix] Copy rules tool crashes when attempted to be used.

# v2.0.5 (10 Apr 2025):
* [Change] Support Timberborn `0.7.3.1`.
* [Fix] Dynamite drilldown could crash when getting close to the map bottom.

# v2.0.4 (8 Apr 2025):
* [Change] Support Timberborn `0.7.2.0`.
* [Feature] Allow editing the rules in the game.
* [Feature] Allow copying rules from one building to another.
* [Feature] Add scripted conditions and action to compose the rules.
* [Feature] Storage emptying is now applicable to the gathering flags! 
* [Fix] U7: Dynamite drilldown action crashes.
* [Fix] U7: Crashes in the editor mode.
* [Fix] U7: Improper dynamite drilldown action on the hangovers.
* [Fix] U7: Construction finished condition could crash on the game load.

# v1.3.0 (4 Apr 2025):
* Add support for Update 7 (0.7.1.2).

# v1.2.5 (5 Dec 2024):
* Fix crashes in some edge cases when Choo-Choo mod is installed.

# v1.2.4 (20 Oct 2024):
* [Fix #63] Mod crashes on path checking under undetermined conditions.

# v1.2.3 (29 Sep 2024):
* [Fix] Mod crashes in map editor.

# v1.2.2 (5 Sep 2024):
* [Change] Support Timberborn 0.6.5.1.

# v1.2.1 (4 Sep 2024):
* Migrate to Update 6. Incompatible with the older versions of Timberborn.
* [Fix #57] Pathfidning service fails under an edge case condition.

# v1.1.1 (19 May 2024):
* [Change] Refactor water templates. Now, open/close actions can be set differently per season.
* [Fix #53] Incorrect loading of the population rules.

# v1.0.3 (31 Mar 3/12/2024):
* [New] Add template to prevent completed building blocking builders and other building sites.
* [Fix #46] Mod crashes on applying population template to a non-complete building.

# v0.14 (29 Feb 2024):
* [New] Add population control templates.
* [New] Add water regulator control template.
* [Change] Re-arrange water template tools in the menu.
* [Fix #41] Dynamite drill down doesn't work on the new tools.
* [Fix #35] Dynamite drill down action should respect the priority.
* [Fix #12] Automation mod crashes in the editor.

# v0.13 (18 Feb 2024):
* Add "finish now" debug tool to immediatelty complete construction of the selected objects.
* Fix detecting current drought state condition.
* Change weather icons to the current game scheme.
* Add new skins for the debug tools.

# v0.12 (10 Jan 2024)
* Experimental version for Update 5.
* [Fix #22] Mod doesn't load in Timberborn 0.5.6.3.

# v0.11 (15 Dec 2023)
* Experimental version for Update 5.
* [Fix #21] Mod doesn't load in Timberborn 0.5.5.0.

# v0.10 (8 Dec 2023)
* Experimental version for Update 5.
* [Fix #19] Mod doesn't load in Timberborn 0.5.4.2.

# v0.9 (30 Sep 2023)
* Fixed to support Update 5.
* The start of a badtide season is not considered as a drought start in the wather automations.

# v0.8 (27 Aug 2023)
* Better checking logic for the characters on the detonated dynamite. Now checking an area of 5x5.
* Fix a bug in the dynamite drilldown tool that resulted in not palcing extra dynamites in some cases.

# v0.7 (5 Aug 2023)
* Allow multiple different templates on the same object.
* Add templates for floodgate automation.

# v0.6 (26 Jul 2023)
* Switch to TAPI 0.6.0 and deprecate CustomResources dep.
