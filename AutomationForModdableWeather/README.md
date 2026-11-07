# Timberborn: Automation+ModdableWeather mod

This mod exposes Moddable Weathers seasons through the source-only Automation API.
It registers the independent `Mods.ModdableWeathers.Season` signal and does not extend Automation's built-in Weather.Season.
Automation is optional; the adapter still requires Moddable Weathers. Existing rules using the old extended
Weather.Season signal are not migrated automatically.

* Steam Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3562952077.
* Mod.IO: https://mod.io/g/timberborn/m/automationmoddableweather.

# Building and releasing

1. Set up Dependencies/GameRoot and Dependencies/Workshop for the current Timberborn 1.1 installation.
2. Subscribe to Moddable Weathers (3630523180); the project references its version-1.1 DLL.
3. Build with an explicit ModPath pointing to the local Timberborn Mods directory.
4. Output is AutomationForModdableWeather/version-1.1, including its manifest and localizations.

The project links only Automation/Api/AutomationApi.cs, with no Automation assembly reference.
See [the API example](../Automation/Api/README.md) for registration and deferred update semantics.
