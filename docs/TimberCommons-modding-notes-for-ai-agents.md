# TimberCommons Modding Notes for AI Agents

This document contains notes specific to the TimberCommons mod.

Use this together with:

- repository-level `csharp-formatting-rules-for-ai-agents.md`
- repository-level `timberborn-modding-rules-for-ai-agents.md`

## Project role

TimberCommons is a regular player-facing Timberborn mod.

It is named "Commons" because it contains many small gameplay and UI changes, not because it is a general shared
library.

The important reusable API surface is the irrigation tower system. Components based on `IrrigationTower`, such as
`GoodConsumingIrrigationTower` and `ManufactoryIrrigationTower`, may be used by other mods, including third-party
mods.

When changing the irrigation tower components, specs, save/load behavior, or public API, consider compatibility for
external mods. Do not treat unrelated TimberCommons features as reusable infrastructure unless there is evidence.

When changing `IrrigationTower`, `GoodConsumingIrrigationTower`, `ManufactoryIrrigationTower`, `IRangeEffect`, or
their specs/effects, check whether the Timber Commons Wiki needs to be updated:

```text
https://github.com/ihsoft/TimberbornMods/wiki/Timber-Commons
```

## Data location

TimberCommons is a Unity-based mod.

Its package data is located here:

```text
ModsUnityProject/Assets/Mods/TimberCommons/Data/
```

Localization files are located here:

```text
ModsUnityProject/Assets/Mods/TimberCommons/Data/Localizations/
```

Known localization files:

```text
enUS.txt
ruRU.txt
deDE.txt
frFR.txt
```

When adding user-facing TimberCommons text, update all existing localization files when practical.

## Localization key prefix

For Common UI patches, prefer keys under:

```text
IgorZ.TimberCommons.CommonUIPatches
```

Example:

```csharp
const string ShowActiveModsLocKey = "IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowActiveMods";
const string ShowAllModsLocKey = "IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowAllMods";
```

## Settings style

Some older TimberCommons settings classes use older notation. Do not copy that style automatically for new settings
work.

For new TimberCommons settings, follow the general ModSettings guidance in
`timberborn-modding-rules-for-ai-agents.md`. Compare against the closest current settings style before coding. Prefer
the Automation-style pattern when it fits:

- declare local `const string ...LocKey` values near the top of the settings class;
- use `BaseSettings<T>` where available;
- use callback-backed static setting values when runtime code needs static access to a setting value;
- keep localized labels and tooltips in localization files rather than inline settings strings.

For existing released TimberCommons settings, do not rename settings owner classes or public `ModSetting` properties
only for style unless the user accepts the persisted-setting reset or a migration/stable id is implemented.

## Mod manager active-mod filter

TimberCommons currently patches the Timberborn mod manager list via:

```text
TimberCommons/CommonUIPatches/ModListViewInitializePatch.cs
```

The feature adds a button near the stock `ResetOrderButton`.

Behavior:

- When all mods are visible, button text should be `Show active ({activeCount}/{totalCount})`.
- When only active mods are visible, button text should be `Show all ({totalCount})`.
- The filter hides/shows existing `ModItem.Root` UI elements.
- It should not rebuild the mod list.
- It should not patch `OnModToggled`.
- If the user disables a mod while filtered, the row does not need to disappear immediately.

Relevant game classes observed via ILSpy:

```text
Timberborn.ModdingUI.ModListView
Timberborn.ModdingUI.ModItem
Timberborn.ModManagerSceneUI.ModManagerScenePanel
```

Useful game APIs:

```csharp
ModPlayerPrefsHelper.IsModEnabled(mod)
item.Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
```

Private field access used by the patch:

```csharp
static Dictionary<Mod, ModItem> GetModItems(ModListView modListView) {
  return AccessTools.FieldRefAccess<ModListView, Dictionary<Mod, ModItem>>(modListView, "_modItems");
}
```

## Mod manager scene caveat

The Timberborn mod manager dialog can appear at an early startup stage before any mods are loaded.

At that stage, TimberCommons patches are not active yet.

Do not try to make the TimberCommons button appear in the pre-mod-load version of the mod manager unless explicitly requested.
It is acceptable that the button appears only after TimberCommons itself is loaded.

## `ILoc` bridge for `ModListViewInitializePatch`

`ModListViewInitializePatch` needs localized button text.

The patch is static, so `ILoc` is passed through a static bridge:

```csharp
static ILoc _loc = null!;

public static void SetLoc(ILoc loc) {
  _loc = loc;
}
```

Important observed lifecycle detail:

`ModListViewInitializePatch` can run before `ILoadableSingleton.Load()`, but after DI constructs the singleton.

Therefore, initialize the patch from the singleton constructor:

```csharp
sealed class ModListViewLocInitializer : ILoadableSingleton {
  public ModListViewLocInitializer(ILoc loc) {
    ModListViewInitializePatch.SetLoc(loc);
  }

  public void Load() {
  }
}
```

Do not move `SetLoc(loc)` into `Load()` for this patch unless the lifecycle is re-tested.

## Localization strings for the active-mod filter

Use these IDs:

```text
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowActiveMods
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowAllMods
```

English:

```csv
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowActiveMods,"Show active ({0}/{1})","Button in mod manager. Shows only enabled mods."
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowAllMods,"Show all ({0})","Button in mod manager. Shows all installed mods."
```

Russian:

```csv
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowActiveMods,"Показать активные ({0}/{1})","Button in mod manager. Shows only enabled mods."
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowAllMods,"Показать все ({0})","Button in mod manager. Shows all installed mods."
```

German:

```csv
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowActiveMods,"Aktive anzeigen ({0}/{1})","Button in mod manager. Shows only enabled mods."
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowAllMods,"Alle anzeigen ({0})","Button in mod manager. Shows all installed mods."
```

French:

```csv
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowActiveMods,"Afficher les mods actifs ({0}/{1})","Button in mod manager. Shows only enabled mods."
IgorZ.TimberCommons.CommonUIPatches.ModListView.ShowAllMods,"Afficher tous les mods ({0})","Button in mod manager. Shows all installed mods."
```

## Common UI dialogs

TimberCommons common UI patches may use shared TimberDev UI helpers such as `AbstractDialog`.

Treat shared TimberDev helpers as general infrastructure. Add behavior there only when it is clearly reusable by
multiple dialogs. Keep feature-specific layout, loading, confirmation, and validation behavior in the TimberCommons
subclass.

Do not casually replace helper methods in shared UI infrastructure. For example, if existing code uses `Root.Q2`, keep
that helper unless the change has been checked against its intended behavior and all affected dialogs.

For UI replacement patches such as save-load dialogs, keep the Harmony patch thin. The patch should intercept the game
method, resolve the TimberCommons dialog or service from DI, delegate to it, and return the appropriate Harmony result.
The dialog class should own UI loading, validation, button behavior, confirmation flow, and state.

When a dialog asks for confirmation before continuing, preserve the user's context. If the confirmation has Cancel,
Cancel should leave the user in the original dialog instead of closing it and returning to the previous screen.

## Save-load mod compatibility dialog

Save-file mod checks must compare logical mod identity, not only the specific package item that appeared in the save or
repository list.

Local-folder and Workshop copies can share the same manifest id. If an equivalent local-folder copy is already active,
do not offer to enable a disabled Workshop duplicate just because that package item is disabled. Reason by manifest id
or equivalent logical identity first, then decide whether any installed mod actually needs to be enabled.
