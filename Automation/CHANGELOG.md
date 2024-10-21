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
