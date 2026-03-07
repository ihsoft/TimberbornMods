# v3.5.0 (started on February 27th, 2026):
* [Feature #128] New signal `Workplace.AssignedWorkers` to track the number of workers assigned to the workplace.
* [Feature] New action `Workplace.SetPriority` to set the workplace priority.
* [Feature] Big improvements to the rules UI consistency. Now, the numbers in the rule editor match the rule's description.
* [Feature] In the rules editor, give more verbose hints on the values of signals and action arguments.
* [Feature] Fully refactor the runtime script value verification. Now, it's fast and reliable. Don't disable it (you can, via the settings)!
* [Change] Update to game v1.0.11.
* [Change] No more automation tools in the map editor.
* [Change] Don't format percentile values of exported signals. Show them as-is: the normalized float values.

# v3.4.1 (February 12th, 2026):
* [Change] On building copy (construction or settings), the automation rules are now copied only if the buildings are _of the same type_.
* [Fix #135] Game crash when trying reordering the rules.
* [Fix #136] Rule copying results in a "parsing error" in the copied rules.
* [Fix] When placing a copied structure in finished state (dev mode), the game could crash.

# v3.4.0 (February 2nd, 2026):
* [Feature] The stock game "copy building settings" feature now also copies the automation rules.
* [Feature] Rules can now be selectively enabled or disabled. The disabled rules stay on the building, but they won't be executed until enabled.
* [Feature] Rules order can now be changed via the rules editor dialog. Note that it doesn't affect the execution logic. It's a purely cosmetic function.
* [Feature] Added a template tool to enable/disable all rules on the building(s).
* [Fix] Dynamite detonation doesn't work.

# v3.3.0 (January 22nd, 2026):
* [Feature] Introduce "Copy rule" button to the rules editor to quickly duplicate an existing rule.
* [Feature] Introduce "Invert rule" button to the rules editor that turns a rule into the opposite condition/action.

# v3.2.3 (January 5th, 2026):
* [Fix] Recipe selection in the action UI is not working.

# v3.2.2 (January 5th, 2026):
* [Fix] New game crashes on save.

# v3.2.1 (January 5th, 2026):
* [Feature] Support district signals for stock value and capacity: `District.ResourceStock.<GoodId>` and `District.ResourceCapacity.<GoodId>`.
* [Feature] Add change recipe action: `Manufactory.SetRecipe(<RecipeId>)`.
* [Feature] Add logging action with string formatting: `Debug.Log('arg1={0}, arg2={1}', 1, 'test')`.
* [Change] Deprecate debug signal: `Debug.DistrictStockTracker.<*>`.
* [Change] Drop tool "Prioritize by haulers on finish building". This option can now be set on unfinished building via the stock game UI.
* [Change] Deprecate operators `getnum` and `getstr`. Now, you should use: `getvalue`, `getelement`, and `getlen`. Good news: the value type is now detected from the property.
* [Change] Use remove/set workers instead of pause/resume in the planting tools. It avoids losing gatherables in some edge cases.

# v3.1.2 (December 17th, 2025):
* [Change] Support game version `v1.0`. Mod version `v3.0.2` is included in the package for the older game version.
* [Change] Mod dependency removed: TimberAPI.
* [Change] New mod dependency added: CustomTools.

# v3.0.2 (November 24th, 2025):
* [Feature] Support Python-like expressions for conditions and actions in the script editing mode.
* [Feature] You can select which syntax you prefer in the mod settings.
* [Change] Add new functions: `getvalue`, `getelement`, and `getlen`. They replace `getnum` and `getstr` functions, which will be deprecated soon.

# v2.8.8 (October 23rd, 2025):
* [Fix] Signal definition disappears in UI if allowed good is changed on stockpile.
* [Fix] Signals list is not updated in UI when changing allowed good or recipe.
* [Fix] Crash when saving definition of a signal that is not available on the building.

# v2.8.7 (October 22nd, 2025):
* [Fix #127] Crash when attempting Collectable.Ready signal on ruin scavenger flag.

# v2.8.6 (October 20th, 2025):
* [Fix #126] Game crashes when selecting a building with the "chain" template applied.

# v2.8.5 (October 19th, 2025):
* [Fix #125] The game frequently crashes when `Plantable.Ready` is used and paths are changed.

# v2.8.4 (October 19th, 2025):
* [Fix #124] Copy rules button is not working.

# v2.8.3 (October 19th, 2025):
* [Fix #123] ArgumentException: toExclusive 0 must be greater than fromInclusive 0 when scheduling T.

# v2.8.2 (October 19th, 2025):
* [Feature] Full refactor of the entity panel UI.
* [Feature] Limit the number of rules shown in the entity panel. Check the settings dialog.
* [Feature] Update signal values in the entity panel.
* [Change] Move script rules import/export functionality to the building panel UI.
* [Change] Improve script errors handling in UI. Fix some edge cases when game could crash due to a script error.
* [Change] The `concat` operator now shows the numbers as floats instead of fixed-point integers.
* [Fix] Properly display "add" operator when it has more than 2 arguments.
* [Fix #121] Moisture and contamination levels of the plantable spots are now considered when checking if planting is possible.

# v2.7.1 (September 5th, 2025):
* [Fix] Fix the "can plant" template definition.

# v2.7.0 (September 5th, 2025):
* [Feature] Add debug signals for district stock tracking: `Debug.DistrictStockTracker.<GoodId>`.
* [Feature] Add `Collectable.Ready` signal that tells how many gatherable or cuttable resources are in the range.
* [Feature] Add `Plantable.Ready` signal that tells how many plantable resources can be planted in the range.
* [Feature #82] Add templates to pause buildings if they can't gather or cut anything.
* [Feature] Add a template to pause buildings if they can't plant trees or crops.
* [Feature] Implement extensions support to allow other mods to modify rules options.
* [Change] `ModdedWeather` mod support moved into a separate extension mod: `Automation+ModdedWeather`.
* [Fix] In "describe mode," show action argument values as real numbers instead of script fixed-point values.
* [Fix] Don't change the floodgate height if it is already at the desired level. It may trigger "water deletion" without any useful effect.

# v2.6.0 (June 22nd, 2025):
* [Change] Make input field in script editor multi-line.
* [Change] Better handling of script errors in both UI and the execution phases.
* [Change #106] Allow choosing if the values should be evaluated or "described" in the UI. Check the settings dialog.
* [Fix #115] InvalidOperationException: Condition already activated.

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
* [Feature #150] Add Modulo (`mod`) operator.
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
* [Feature] Highlight the conditions that are being evaluated as "true" with green color.
* [Feature] New scriptable components `Workplace`. Now the number of workers in the workshop can be changed from the script.
* [Change] Don't select the text when switching to the script input field.
* [Fix] Chained building tool doesn't show the blocked completion icon.

# v2.1.1 (16 May 2025):
* [Feature] Add settings to control how to show the rules on the building's panel.
* [Feature] Add new scripting component `FlowControl` to allow scripting the water flow on sluices and badwater domes.
* [Feature] The dynamite drilldown templates are now scripted. Such rules can be created and edited via the rules editor.
* [Feature] New template tool to pause construction site when its progress reaches 100%.
* [Feature] The "Open water regulator" template can now be applied to sluices as well.
* [Change] The rule editor now only allows inventory signals that are valid for the current building state: there must be a recipe or a storage good selected.
* [Change] Various improvements to better check and show the script arguments values.

# v2.1.0 (12 May 2025):
* [Feature] Buildings can now emit their state as global signals to be consumed by other buildings.
* [Feature] New scriptable components `Prioritizable` and `Constructable`.
* [Feature] New template tool that allows prioritizing a newly constructed building by haulers.
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
* [Feature] Add scripted conditions and actions to compose the rules.
* [Feature] Storage emptying is now applicable to the gathering flags!
* [Fix] U7: Dynamite drilldown action crashes.
* [Fix] U7: Crashes in the editor mode.
* [Fix] U7: Improper dynamite drilldown action on the hangovers.
* [Fix] U7: Construction finished condition could crash on game load.

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
* Migrate to Update 6. Incompatible with older versions of Timberborn.
* [Fix #57] Pathfinding service fails under an edge case condition.

# v1.1.1 (19 May 2024):
* [Change] Refactor water templates. Now open/close actions can be set differently per season.
* [Fix #53] Incorrect loading of population rules.

# v1.0.3 (31 Mar 3/12/2024):
* [New] Add template to prevent completed buildings from blocking builders and other building sites.
* [Fix #46] Mod crashes when applying population template to a non-complete building.

# v0.14 (29 Feb 2024):
* [New] Add population control templates.
* [New] Add water regulator control template.
* [Change] Rearrange water template tools in the menu.
* [Fix #41] Dynamite drilldown doesn't work with the new tools.
* [Fix #35] Dynamite drilldown action should respect the priority.
* [Fix #12] Automation mod crashes in the editor.

# v0.13 (18 Feb 2024):
* Add "Finish Now" debug tool to immediately complete construction of the selected objects.
* Fix detecting the current drought state condition.
* Update weather icons to match the current game style.
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
* The start of a badtide season is not considered a drought start in water automations.

# v0.8 (27 Aug 2023)
* Better checking logic for characters on detonated dynamite. Now checking an area of 5x5.
* Fix a bug in the dynamite drilldown tool that resulted in not placing extra dynamites in some cases.

# v0.7 (5 Aug 2023)
* Allow multiple different templates on the same object.
* Add templates for floodgate automation.

# v0.6 (26 Jul 2023)
* Switch to TAPI 0.6.0 and deprecate CustomResources dependency.